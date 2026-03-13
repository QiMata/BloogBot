using BotCommLayer;
using GameData.Core.Models;
using Pathfinding;
using PathfindingService.Repository;
using GameData.Core.Constants;
using GameData.Core.Enums;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PathfindingService
{
    /// <summary>
    /// Status information written to a file for service-to-service communication.
    /// </summary>
    public class PathfindingServiceStatus
    {
        public bool IsReady { get; set; }
        public string StatusMessage { get; set; } = "";
        public List<uint> LoadedMaps { get; set; } = [];
        public DateTime Timestamp { get; set; }
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the default path for the status file.
        /// </summary>
        public static string GetStatusFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, "pathfinding_status.json");
        }

        /// <summary>
        /// Writes the status to the default status file.
        /// </summary>
        public void WriteToFile()
        {
            var path = GetStatusFilePath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Reads status from the default status file. Returns null if file doesn't exist or is invalid.
        /// </summary>
        public static PathfindingServiceStatus? ReadFromFile()
        {
            var path = GetStatusFilePath();
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PathfindingServiceStatus>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes the status file if it exists.
        /// </summary>
        public static void DeleteStatusFile()
        {
            var path = GetStatusFilePath();
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }
    }

    public class PathfindingSocketServer(string ipAddress, int port, ILogger logger) : ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(ipAddress, port, logger)
    {
        private Navigation _navigation;
        private Physics _physics;
        private readonly RequestScopedDynamicObjectOverlay _dynamicObjectOverlay = new(new NativeDynamicObjectOverlayRegistry());
        private volatile bool _isInitialized;
        private readonly object _initLock = new();

        /// <summary>
        /// Indicates whether the navigation and physics systems are fully loaded.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes the navigation and physics systems.
        /// Call this after the socket server is running to allow early connections.
        /// </summary>
        public void InitializeNavigation()
        {
            lock (_initLock)
            {
                if (_isInitialized) return;

                // Write initial "loading" status
                WriteStatus(false, "Loading navigation data...", []);

                // Ensure native library is loaded first (with helpful error messages)
                Physics.EnsureNativeLibraryLoaded();

                var initSw = System.Diagnostics.Stopwatch.StartNew();
                logger.LogInformation("Loading Navigation data...");
                _navigation = new Navigation();
                logger.LogInformation("Navigation loaded in {Elapsed:F1}s", initSw.Elapsed.TotalSeconds);

                initSw.Restart();
                logger.LogInformation("Loading Physics data and preloading maps...");
                _physics = new Physics();
                logger.LogInformation("Physics loaded in {Elapsed:F1}s", initSw.Elapsed.TotalSeconds);

                _isInitialized = true;

                // Write "ready" status with loaded maps
                // Maps 0, 1, 389 are preloaded by Physics.EnsureNativeLibraryLoaded()
                var loadedMaps = new List<uint> { 0, 1, 389 };
                WriteStatus(true, "Ready - navigation and physics systems initialized", loadedMaps);

                logger.LogInformation("Navigation and Physics systems initialized.");
            }
        }

        /// <summary>
        /// Writes the current service status to the status file.
        /// </summary>
        private void WriteStatus(bool isReady, string message, List<uint> loadedMaps)
        {
            try
            {
                var status = new PathfindingServiceStatus
                {
                    IsReady = isReady,
                    StatusMessage = message,
                    LoadedMaps = loadedMaps,
                    Timestamp = DateTime.UtcNow,
                    ProcessId = Environment.ProcessId
                };
                status.WriteToFile();
                logger.LogInformation($"Status file updated: IsReady={isReady}, Message='{message}'");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to write status file: {ex.Message}");
            }
        }

        protected override PathfindingResponse HandleRequest(PathfindingRequest request)
        {
            try
            {
                // Check if navigation/physics are ready
                if (!_isInitialized)
                {
                    return new PathfindingResponse
                    {
                        Error = new Error { Message = "PathfindingService is still initializing. Please wait for navigation data to load." }
                    };
                }

                return request.PayloadCase switch
                {
                    PathfindingRequest.PayloadOneofCase.Path => HandlePath(request.Path),
                    PathfindingRequest.PayloadOneofCase.Los => HandleLineOfSight(request.Los),
                    PathfindingRequest.PayloadOneofCase.Step => HandlePhysics(request.Step),
                    PathfindingRequest.PayloadOneofCase.GroundZ => HandleGroundZ(request.GroundZ),
                    PathfindingRequest.PayloadOneofCase.BatchGroundZ => HandleBatchGroundZ(request.BatchGroundZ),
                    PathfindingRequest.PayloadOneofCase.SegmentDynCheck => HandleSegmentDynCheck(request.SegmentDynCheck),
                    _ => ErrorResponse("Unknown or unset request type.")
                };
            }
            catch (Exception ex)
            {
                logger.LogError($"[PathfindingSocketServer] Error: {ex.Message}\n{ex.StackTrace}");
                return ErrorResponse($"Internal error: {ex.Message}");
            }
        }

        private int _pathLogCounter = 0;
        private long _pathRequestCounter = 0;
        private PathfindingResponse HandlePath(CalculatePathRequest req)
        {
            if (!CheckPosition(req.MapId, req.Start, req.End, out var err))
                return err;

            var start = new XYZ(req.Start.X, req.Start.Y, req.Start.Z);
            var end = new XYZ(req.End.X, req.End.Y, req.End.Z);
            var dist2D = MathF.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));
            var requestId = Interlocked.Increment(ref _pathRequestCounter);
            var requestSw = System.Diagnostics.Stopwatch.StartNew();
            using var slowRequestCts = new CancellationTokenSource();
            _ = LogLongRunningPathRequestAsync(
                requestId,
                req.MapId,
                start,
                end,
                dist2D,
                req.Straight,
                req.NearbyObjects.Count,
                slowRequestCts.Token);

            if (dist2D >= 100f)
            {
                logger.LogInformation(
                    "[PATH_REQ] id={RequestId} start map={MapId} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1}) dist2D={Dist:F1} smoothPath={SmoothPath} overlayRequested={OverlayCount}",
                    requestId,
                    req.MapId,
                    start.X,
                    start.Y,
                    start.Z,
                    end.X,
                    end.Y,
                    end.Z,
                    dist2D,
                    req.Straight,
                    req.NearbyObjects.Count);
            }

            OverlayExecutionResult<NavigationPathResult> overlayResult;
            try
            {
                overlayResult = _dynamicObjectOverlay.ExecuteWithOverlay(
                    req.MapId,
                    req.NearbyObjects,
                    () => _navigation.CalculateValidatedPath(req.MapId, start, end, req.Straight),
                    logger,
                    operationName: "path");
            }
            finally
            {
                slowRequestCts.Cancel();
            }

            var pathResult = overlayResult.Value;
            var path = pathResult.Path;
            var sanitizedPath = path
                .Where(IsFinitePoint)
                .ToArray();

            // Proto field "straight" is actually smoothPath (see pathfinding.proto comment and PathfindingClient.cs)
            var smoothPath = req.Straight;

            if (dist2D >= 100f)
            {
                logger.LogInformation(
                    "[PATH_REQ] id={RequestId} done elapsedMs={ElapsedMs} result={Result} corners={Corners} rawCorners={RawCorners} blockedSegment={BlockedSegment}",
                    requestId,
                    requestSw.ElapsedMilliseconds,
                    pathResult.Result,
                    sanitizedPath.Length,
                    pathResult.RawPath.Length,
                    pathResult.BlockedSegmentIndex?.ToString() ?? string.Empty);
            }

            if (sanitizedPath.Length != path.Length)
            {
                logger.LogWarning(
                    "[PathfindingSocketServer] Filtered {DroppedCount} non-finite path corners (map={MapId}, smoothPath={SmoothPath})",
                    path.Length - sanitizedPath.Length,
                    req.MapId,
                    smoothPath);
            }

            // Log path requests that return few/no corners for diagnostics
            var requestOrdinal = ++_pathLogCounter;
            if (PathRouteDiagnostics.ShouldLogRoute(
                dist2D,
                sanitizedPath.Length,
                pathResult.RawPath.Length,
                pathResult.Result,
                requestOrdinal))
            {
                logger.LogInformation(
                    "[PATH_DIAG] reason={Reason} map={MapId} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1}) dist2D={Dist:F1} smoothPath={SmoothPath} result={Result} blockedSegment={BlockedSegment} corners={Corners} rawCorners={RawCorners} path=[{PathCorners}] rawPath=[{RawPathCorners}] overlayRequested={OverlayRequested} overlayRegistered={OverlayRegistered} overlayFiltered={OverlayFiltered} overlayDisplayIds=[{OverlayDisplayIds}]",
                    PathRouteDiagnostics.GetReason(
                        dist2D,
                        sanitizedPath.Length,
                        pathResult.RawPath.Length,
                        pathResult.Result,
                        requestOrdinal),
                    req.MapId,
                    start.X,
                    start.Y,
                    start.Z,
                    end.X,
                    end.Y,
                    end.Z,
                    dist2D,
                    smoothPath,
                    pathResult.Result,
                    pathResult.BlockedSegmentIndex?.ToString() ?? string.Empty,
                    sanitizedPath.Length,
                    pathResult.RawPath.Length,
                    PathRouteDiagnostics.FormatCorners(sanitizedPath),
                    PathRouteDiagnostics.FormatCorners(pathResult.RawPath),
                    overlayResult.Summary.RequestedCount,
                    overlayResult.Summary.RegisteredCount,
                    overlayResult.Summary.FilteredCount,
                    overlayResult.Summary.RegisteredDisplayIds.Count > 0
                        ? string.Join(",", overlayResult.Summary.RegisteredDisplayIds)
                        : string.Empty);
            }

            var resp = new CalculatePathResponse();
            resp.Corners.AddRange(sanitizedPath.Select(p => new Game.Position { X = p.X, Y = p.Y, Z = p.Z }));
            resp.RawCornerCount = (uint)pathResult.RawPath.Length;
            resp.Result = pathResult.Result;

            return new PathfindingResponse { Path = resp };
        }

        private async Task LogLongRunningPathRequestAsync(
            long requestId,
            uint mapId,
            XYZ start,
            XYZ end,
            float dist2D,
            bool smoothPath,
            int overlayCount,
            CancellationToken cancellationToken)
        {
            foreach (var thresholdSeconds in new[] { 5, 15, 25 })
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(thresholdSeconds == 5 ? 5 : 10), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                logger.LogWarning(
                    "[PATH_REQ] id={RequestId} still-running elapsed>={ElapsedSeconds}s map={MapId} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1}) dist2D={Dist:F1} smoothPath={SmoothPath} overlayRequested={OverlayCount}",
                    requestId,
                    thresholdSeconds,
                    mapId,
                    start.X,
                    start.Y,
                    start.Z,
                    end.X,
                    end.Y,
                    end.Z,
                    dist2D,
                    smoothPath,
                    overlayCount);
            }
        }

        private int _physicsLogCounter = 0;
        private int _zeroForwardCount = 0;
        private PathfindingResponse HandlePhysics(Pathfinding.PhysicsInput step)
        {
            return _dynamicObjectOverlay.ExecuteExclusive(() =>
            {
                var physicsInput = step.ToPhysicsInput();

                // Marshal nearby dynamic objects (if any) from proto to pinned native array
                Repository.DynamicObjectInfo[]? nativeObjects = null;
                System.Runtime.InteropServices.GCHandle pinHandle = default;
                if (step.NearbyObjects.Count > 0)
                {
                    nativeObjects = new Repository.DynamicObjectInfo[step.NearbyObjects.Count];
                    for (int i = 0; i < step.NearbyObjects.Count; i++)
                    {
                        var obj = step.NearbyObjects[i];
                        nativeObjects[i] = new Repository.DynamicObjectInfo
                        {
                            guid = obj.Guid,
                            displayId = obj.DisplayId,
                            x = obj.X,
                            y = obj.Y,
                            z = obj.Z,
                            orientation = obj.Orientation,
                            scale = obj.Scale > 0 ? obj.Scale : 1.0f,
                            goState = obj.GoState
                        };
                    }
                    pinHandle = System.Runtime.InteropServices.GCHandle.Alloc(nativeObjects, System.Runtime.InteropServices.GCHandleType.Pinned);
                    physicsInput.nearbyObjects = pinHandle.AddrOfPinnedObject();
                    physicsInput.nearbyObjectCount = nativeObjects.Length;
                }

                Repository.PhysicsOutput physicsOutput;
                try
                {
                    physicsOutput = _physics.StepPhysicsV2(physicsInput, step.DeltaTime);
                }
                finally
                {
                    if (pinHandle.IsAllocated) pinHandle.Free();
                }

                // Log every 100th physics step to diagnose ground snapping
                if (++_physicsLogCounter % 100 == 1)
                {
                    float dz = physicsOutput.z - physicsInput.z;
                    logger.LogInformation(
                        "[PHYS_DIAG] frame={Frame} in=({X:F1},{Y:F1},{Z:F1}) out=({OX:F1},{OY:F1},{OZ:F1}) dZ={DZ:F2} groundZ={GZ:F1} flags=0x{F:X} prevGZ={PGZ:F1} dt={DT:F4}",
                        physicsInput.frameCounter,
                        physicsInput.x, physicsInput.y, physicsInput.z,
                        physicsOutput.x, physicsOutput.y, physicsOutput.z,
                        dz, physicsOutput.groundZ, physicsOutput.moveFlags,
                        physicsInput.prevGroundZ, physicsInput.deltaTime);
                }

                // Detect zero-delta with FORWARD flag (physics stuck)
                {
                    const uint MOVEFLAG_FORWARD = 0x1;
                    float dx = physicsOutput.x - physicsInput.x;
                    float dy = physicsOutput.y - physicsInput.y;
                    bool hasForward = (physicsInput.moveFlags & MOVEFLAG_FORWARD) != 0;
                    if (hasForward && Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                    {
                        _zeroForwardCount++;
                        if (_zeroForwardCount == 1 || _zeroForwardCount % 200 == 0)
                        {
                            logger.LogWarning(
                                "[PHYS_STUCK] Zero-delta with FORWARD: count={Count} frame={Frame} " +
                                "pos=({X:F1},{Y:F1},{Z:F1}) map={Map} groundZ={GZ:F2} speed={Spd:F1}",
                                _zeroForwardCount, physicsInput.frameCounter,
                                physicsInput.x, physicsInput.y, physicsInput.z,
                                physicsInput.mapId, physicsOutput.groundZ, physicsInput.runSpeed);
                        }
                    }
                    else if (hasForward)
                    {
                        _zeroForwardCount = 0;
                    }
                }

                return new PathfindingResponse { Step = physicsOutput.ToPhysicsOutput() };
            });
        }

        private PathfindingResponse HandleLineOfSight(LineOfSightRequest req)
        {
            if (!CheckPosition(req.MapId, req.From, req.To, out var err))
                return err;

            var from = new XYZ(req.From.X, req.From.Y, req.From.Z);
            var to = new XYZ(req.To.X, req.To.Y, req.To.Z);

            bool hasLOS = _dynamicObjectOverlay.ExecuteExclusive(
                () => _physics.LineOfSight(req.MapId, from, to));

            return new PathfindingResponse
            {
                Los = new LineOfSightResponse { InLos = hasLOS }
            };
        }

        private PathfindingResponse HandleSegmentDynCheck(SegmentDynCheckRequest req)
        {
            if (req.From == null || req.To == null || !IsFinitePosition(req.From) || !IsFinitePosition(req.To))
                return ErrorResponse("Missing or non-finite position for SegmentDynCheck.");

            bool intersects = _dynamicObjectOverlay.ExecuteExclusive(
                () => _navigation.SegmentIntersectsDynamicObjects(
                    req.MapId,
                    req.From.X,
                    req.From.Y,
                    req.From.Z,
                    req.To.X,
                    req.To.Y,
                    req.To.Z));

            return new PathfindingResponse
            {
                SegmentDynCheck = new SegmentDynCheckResponse { Intersects = intersects }
            };
        }

        private PathfindingResponse HandleGroundZ(GetGroundZRequest req)
        {
            if (req.Position == null || !IsFinitePosition(req.Position))
                return ErrorResponse("Missing or non-finite position for GetGroundZ.");

            float maxDist = req.MaxSearchDist > 0 ? req.MaxSearchDist : 10.0f;
            var (groundZ, found) = _dynamicObjectOverlay.ExecuteExclusive(
                () => _physics.GetGroundZ(req.MapId, req.Position.X, req.Position.Y, req.Position.Z, maxDist));

            return new PathfindingResponse
            {
                GroundZ = new GetGroundZResponse { GroundZ = groundZ, Found = found }
            };
        }

        private PathfindingResponse HandleBatchGroundZ(BatchGroundZRequest req)
        {
            if (req.Positions.Count == 0)
                return ErrorResponse("BatchGroundZ request contains no positions.");

            float maxDist = req.MaxSearchDist > 0 ? req.MaxSearchDist : 10.0f;
            var response = _dynamicObjectOverlay.ExecuteExclusive(() =>
            {
                var batchResponse = new BatchGroundZResponse();

                foreach (var pos in req.Positions)
                {
                    if (pos == null || !IsFinitePosition(pos))
                    {
                        batchResponse.Results.Add(new BatchGroundZEntry { GroundZ = 0f, Found = false });
                        continue;
                    }

                    var (groundZ, found) = _physics.GetGroundZ(req.MapId, pos.X, pos.Y, pos.Z, maxDist);
                    batchResponse.Results.Add(new BatchGroundZEntry { GroundZ = groundZ, Found = found });
                }

                return batchResponse;
            });

            return new PathfindingResponse { BatchGroundZ = response };
        }

        // ------------- Validation and Helpers ----------------

        private static bool CheckPosition(uint mapId, Game.Position a, Game.Position b, out PathfindingResponse error)
        {
            if (a == null || b == null)
            {
                error = ErrorResponse("Missing start/end position.");
                return false;
            }

            if (!IsFinitePosition(a) || !IsFinitePosition(b))
            {
                error = ErrorResponse("Start/end position contains non-finite coordinates.");
                return false;
            }
            error = null!;
            return true;
        }

        private static bool IsFinitePosition(Game.Position position)
            => float.IsFinite(position.X)
                && float.IsFinite(position.Y)
                && float.IsFinite(position.Z);

        private static bool IsFinitePoint(XYZ point)
            => float.IsFinite(point.X)
                && float.IsFinite(point.Y)
                && float.IsFinite(point.Z);

        private static PathfindingResponse ErrorResponse(string msg)
        {
            return new PathfindingResponse
            {
                Error = new Error { Message = msg }
            };
        }
    }

    public static class ProtoInteropExtensions
    {
        // Convert from Protobuf PhysicsInput to Navigation.PhysicsInput
        public static Repository.PhysicsInput ToPhysicsInput(this Pathfinding.PhysicsInput proto)
        {
            (float radius, float height) value = RaceDimensions.GetCapsuleForRace((Race)proto.Race, (Gender)proto.Gender);
            return new Repository.PhysicsInput
            {
                // Position and orientation
                x = proto.PosX,
                y = proto.PosY,
                z = proto.PosZ,
                orientation = proto.Facing,
                pitch = proto.SwimPitch,

                // Movement speeds
                walkSpeed = proto.WalkSpeed,
                runSpeed = proto.RunSpeed,
                runBackSpeed = proto.RunBackSpeed,
                swimSpeed = proto.SwimSpeed,
                swimBackSpeed = proto.SwimBackSpeed,
                flightSpeed = 7.0f, // Default flight speed (vanilla has no flying)

                // State
                moveFlags = proto.MovementFlags,
                fallTime = (uint)proto.FallTime,
                fallStartZ = proto.FallStartZ != 0 ? proto.FallStartZ : -200000f,
                mapId = proto.MapId,

                // Transport
                transportGuid = proto.TransportGuid,
                transportX = proto.TransportOffsetX,
                transportY = proto.TransportOffsetY,
                transportZ = proto.TransportOffsetZ,
                transportO = proto.TransportOrientation,

                // Velocity
                vx = proto.VelX,
                vy = proto.VelY,
                vz = proto.VelZ,

                // Collision
                height = value.height,
                radius = value.radius,

                // Spline (not used)
                hasSplinePath = false,
                splineSpeed = 0,
                splinePoints = IntPtr.Zero,
                splinePointCount = 0,
                currentSplineIndex = 0,

                // Time
                deltaTime = proto.DeltaTime,
				frameCounter = proto.FrameCounter,

				// Previous ground tracking
				prevGroundZ = proto.PrevGroundZ,
				prevGroundNx = proto.PrevGroundNx,
				prevGroundNy = proto.PrevGroundNy,
				prevGroundNz = proto.PrevGroundNz,

				// Pending depenetration
				pendingDepenX = proto.PendingDepenX,
				pendingDepenY = proto.PendingDepenY,
				pendingDepenZ = proto.PendingDepenZ,

				// Standing-on reference
				standingOnInstanceId = proto.StandingOnInstanceId,
				standingOnLocalX = proto.StandingOnLocalX,
				standingOnLocalY = proto.StandingOnLocalY,
				standingOnLocalZ = proto.StandingOnLocalZ,

				// Behaviour flags from protobuf (e.g. TRUST_INPUT_VELOCITY).
				physicsFlags = proto.PhysicsFlags,

				// Step-up height persistence
				stepUpBaseZ = proto.StepUpBaseZ,
				stepUpAge = proto.StepUpAge
            };
        }

        // Convert from Navigation.PhysicsOutput to Protobuf PhysicsOutput
        public static Pathfinding.PhysicsOutput ToPhysicsOutput(this Repository.PhysicsOutput nav)
        {
            return new Pathfinding.PhysicsOutput
            {
                NewPosX = nav.x,
                NewPosY = nav.y,
                NewPosZ = nav.z,
                NewVelX = nav.vx,
                NewVelY = nav.vy,
                NewVelZ = nav.vz,
                MovementFlags = nav.moveFlags,
                Orientation = nav.orientation,
                Pitch = nav.pitch,
                // Removed deprecated state flags
                FallTime = nav.fallTime,
                CurrentSplineIndex = nav.currentSplineIndex,
				SplineProgress = nav.splineProgress,

				GroundZ = nav.groundZ,
				GroundNx = nav.groundNx,
				GroundNy = nav.groundNy,
				GroundNz = nav.groundNz,
				LiquidZ = nav.liquidZ,
				LiquidType = nav.liquidType,

				PendingDepenX = nav.pendingDepenX,
				PendingDepenY = nav.pendingDepenY,
				PendingDepenZ = nav.pendingDepenZ,

				StandingOnInstanceId = nav.standingOnInstanceId,
				StandingOnLocalX = nav.standingOnLocalX,
				StandingOnLocalY = nav.standingOnLocalY,
				StandingOnLocalZ = nav.standingOnLocalZ,

				FallDistance = nav.fallDistance,
				FallStartZ = nav.fallStartZ,

				HitWall = nav.hitWall,
				WallNormalX = nav.wallNormalX,
				WallNormalY = nav.wallNormalY,
				WallNormalZ = nav.wallNormalZ,
				BlockedFraction = nav.blockedFraction,

				StepUpBaseZ = nav.stepUpBaseZ,
				StepUpAge = nav.stepUpAge
            };
        }
    }
}
