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

                logger.LogInformation("Loading Navigation data...");
                _navigation = new Navigation();

                logger.LogInformation("Loading Physics data and preloading maps...");
                _physics = new Physics();

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
                    _ => ErrorResponse("Unknown or unset request type.")
                };
            }
            catch (Exception ex)
            {
                logger.LogError($"[PathfindingSocketServer] Error: {ex.Message}\n{ex.StackTrace}");
                return ErrorResponse($"Internal error: {ex.Message}");
            }
        }

        private PathfindingResponse HandlePath(CalculatePathRequest req)
        {
            if (!CheckPosition(req.MapId, req.Start, req.End, out var err))
                return err;

            var start = new XYZ(req.Start.X, req.Start.Y, req.Start.Z);
            var end = new XYZ(req.End.X, req.End.Y, req.End.Z);
            var path = _navigation.CalculatePath(req.MapId, start, end, req.Straight);

            var resp = new CalculatePathResponse();
            resp.Corners.AddRange(path.Select(p => new Game.Position { X = p.X, Y = p.Y, Z = p.Z }));

            return new PathfindingResponse { Path = resp };
        }

        private int _physicsLogCounter = 0;
        private PathfindingResponse HandlePhysics(Pathfinding.PhysicsInput step)
        {
            var physicsInput = step.ToPhysicsInput();
            var physicsOutput = _physics.StepPhysicsV2(physicsInput, step.DeltaTime);

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

            return new PathfindingResponse { Step = physicsOutput.ToPhysicsOutput() };
        }

        private PathfindingResponse HandleLineOfSight(LineOfSightRequest req)
        {
            if (!CheckPosition(req.MapId, req.From, req.To, out var err))
                return err;

            var from = new XYZ(req.From.X, req.From.Y, req.From.Z);
            var to = new XYZ(req.To.X, req.To.Y, req.To.Z);

            bool hasLOS = _physics.LineOfSight(req.MapId, from, to);

            return new PathfindingResponse
            {
                Los = new LineOfSightResponse { InLos = hasLOS }
            };
        }

        // ------------- Validation and Helpers ----------------

        private static bool CheckPosition(uint mapId, Game.Position a, Game.Position b, out PathfindingResponse error)
        {
            if (a == null || b == null)
            {
                error = ErrorResponse("Missing start/end position.");
                return false;
            }
            error = null!;
            return true;
        }

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
				standingOnLocalZ = proto.StandingOnLocalZ
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
				StandingOnLocalZ = nav.standingOnLocalZ
            };
        }
    }
}