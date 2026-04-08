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

        public static string GetStatusFilePath()
            => Path.Combine(AppContext.BaseDirectory, "pathfinding_status.json");

        public void WriteToFile()
        {
            var path = GetStatusFilePath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public static PathfindingServiceStatus? ReadFromFile()
        {
            var path = GetStatusFilePath();
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PathfindingServiceStatus>(json);
            }
            catch { return null; }
        }

        public static void DeleteStatusFile()
        {
            var path = GetStatusFilePath();
            if (File.Exists(path))
                try { File.Delete(path); } catch { }
        }
    }

    /// <summary>
    /// PathfindingService socket server — handles path calculation requests ONLY.
    /// Physics, GroundZ, LOS, and navmesh queries are handled locally by the bot's
    /// in-process Navigation.dll.
    /// </summary>
    public class PathfindingSocketServer(string ipAddress, int port, ILogger logger)
        : ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(ipAddress, port, logger)
    {
        private Navigation _navigation;
        private readonly RequestScopedDynamicObjectOverlay _dynamicObjectOverlay = new(new NativeDynamicObjectOverlayRegistry());
        private volatile bool _isInitialized;
        private readonly object _initLock = new();

        public bool IsInitialized => _isInitialized;

        public void InitializeNavigation()
        {
            lock (_initLock)
            {
                if (_isInitialized) return;

                WriteStatus(false, "Loading navigation data...", []);

                var initSw = System.Diagnostics.Stopwatch.StartNew();
                logger.LogInformation("Loading Navigation data and preloading maps...");
                _navigation = new Navigation();
                logger.LogInformation("Navigation loaded in {Elapsed:F1}s", initSw.Elapsed.TotalSeconds);

                _isInitialized = true;

                DiagnoseNativePathfinding(logger);

                var loadedMaps = new List<uint>();
                WriteStatus(true, "Ready - navigation initialized", loadedMaps);
                logger.LogInformation("Navigation system initialized.");
            }
        }

        private static void DiagnoseNativePathfinding(ILogger logger)
        {
            var (len1, ok1) = Navigation.DiagnosticFindPath(1, -284f, -4383f, 57f, -320f, -4420f, 57f);
            logger.LogInformation("[DIAG] Map 1 VoT path: length={Length}, success={Success}", len1, ok1);

            var (len0, ok0) = Navigation.DiagnosticFindPath(0, -8949f, -132f, 83f, -8920f, -110f, 83f);
            logger.LogInformation("[DIAG] Map 0 Elwynn path: length={Length}, success={Success}", len0, ok0);

            if (!ok1 && !ok0)
                logger.LogError("[DIAG] BOTH maps returned no path — mmaps may not be loaded.");
        }

        private void WriteStatus(bool isReady, string message, List<uint> loadedMaps)
        {
            try
            {
                new PathfindingServiceStatus
                {
                    IsReady = isReady,
                    StatusMessage = message,
                    LoadedMaps = loadedMaps,
                    Timestamp = DateTime.UtcNow,
                    ProcessId = Environment.ProcessId
                }.WriteToFile();
                logger.LogInformation("Status file updated: IsReady={IsReady}, Message='{Message}'", isReady, message);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to write status file: {Error}", ex.Message);
            }
        }

        protected override PathfindingResponse HandleRequest(PathfindingRequest request)
        {
            try
            {
                if (!_isInitialized)
                {
                    return new PathfindingResponse
                    {
                        Error = new Error { Message = "PathfindingService is still initializing." }
                    };
                }

                return request.PayloadCase switch
                {
                    PathfindingRequest.PayloadOneofCase.Path => HandlePath(request.Path),
                    _ => ErrorResponse($"Unsupported request type: {request.PayloadCase}. PathfindingService only handles path requests.")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PathfindingSocketServer] Error handling request");
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
            _ = LogLongRunningPathRequestAsync(requestId, req.MapId, start, end, dist2D, req.Straight, req.NearbyObjects.Count, slowRequestCts.Token);

            if (dist2D >= 100f)
            {
                logger.LogInformation(
                    "[PATH_REQ] id={RequestId} start map={MapId} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1}) dist2D={Dist:F1} smoothPath={SmoothPath} overlayRequested={OverlayCount}",
                    requestId, req.MapId, start.X, start.Y, start.Z, end.X, end.Y, end.Z, dist2D, req.Straight, req.NearbyObjects.Count);
            }

            var agentRadius = 0.6f;
            var agentHeight = 2.0f;
            if (req.Race != 0)
            {
                try
                {
                    var (r, h) = RaceDimensions.GetCapsuleForRace((Race)req.Race, (Gender)req.Gender);
                    agentRadius = r;
                    agentHeight = h;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("[PATH_DIAG] RaceDimensions lookup failed for race={Race} gender={Gender}: {Error}. Using defaults.",
                        req.Race, req.Gender, ex.Message);
                }
            }

            logger.LogInformation(
                "[PATH_DIAG] id={RequestId} race={Race} gender={Gender} capsule=({Radius:F4},{Height:F4}) start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1})",
                requestId, req.Race, req.Gender, agentRadius, agentHeight, start.X, start.Y, start.Z, end.X, end.Y, end.Z);

            OverlayExecutionResult<NavigationPathResult> overlayResult;
            try
            {
                overlayResult = _dynamicObjectOverlay.ExecuteWithOverlay(
                    req.MapId, req.NearbyObjects,
                    () => _navigation.CalculateValidatedPath(req.MapId, start, end, req.Straight, agentRadius, agentHeight),
                    logger, operationName: "path");
            }
            finally
            {
                slowRequestCts.Cancel();
            }

            var pathResult = overlayResult.Value;
            var sanitizedPath = pathResult.Path.Where(IsFinitePoint).ToArray();

            logger.LogInformation(
                "[PATH_DIAG] id={RequestId} result={Result} pathLen={PathLen} rawPathLen={RawPathLen} blockedIdx={BlockedIdx} elapsedMs={ElapsedMs}",
                requestId, pathResult.Result, sanitizedPath.Length, pathResult.RawPath.Length,
                pathResult.BlockedSegmentIndex?.ToString() ?? "none", requestSw.ElapsedMilliseconds);

            if (dist2D >= 100f)
            {
                logger.LogInformation(
                    "[PATH_REQ] id={RequestId} done elapsedMs={ElapsedMs} result={Result} corners={Corners} rawCorners={RawCorners}",
                    requestId, requestSw.ElapsedMilliseconds, pathResult.Result, sanitizedPath.Length, pathResult.RawPath.Length);
            }

            var requestOrdinal = Interlocked.Increment(ref _pathLogCounter);
            if (PathRouteDiagnostics.ShouldLogRoute(dist2D, sanitizedPath.Length, pathResult.RawPath.Length, pathResult.Result, requestOrdinal))
            {
                logger.LogInformation(
                    "[PATH_DIAG] reason={Reason} map={MapId} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1}) dist2D={Dist:F1} result={Result} corners={Corners} rawCorners={RawCorners} path=[{PathCorners}] rawPath=[{RawPathCorners}]",
                    PathRouteDiagnostics.GetReason(dist2D, sanitizedPath.Length, pathResult.RawPath.Length, pathResult.Result, requestOrdinal),
                    req.MapId, start.X, start.Y, start.Z, end.X, end.Y, end.Z, dist2D,
                    pathResult.Result, sanitizedPath.Length, pathResult.RawPath.Length,
                    PathRouteDiagnostics.FormatCorners(sanitizedPath), PathRouteDiagnostics.FormatCorners(pathResult.RawPath));
            }

            var resp = new CalculatePathResponse();
            resp.Corners.AddRange(sanitizedPath.Select(p => new Game.Position { X = p.X, Y = p.Y, Z = p.Z }));
            resp.RawCornerCount = (uint)pathResult.RawPath.Length;
            resp.Result = pathResult.Result;

            return new PathfindingResponse { Path = resp };
        }

        private async Task LogLongRunningPathRequestAsync(
            long requestId, uint mapId, XYZ start, XYZ end,
            float dist2D, bool smoothPath, int overlayCount, CancellationToken ct)
        {
            foreach (var threshold in new[] { 5, 15, 25 })
            {
                try { await Task.Delay(TimeSpan.FromSeconds(threshold == 5 ? 5 : 10), ct); }
                catch (OperationCanceledException) { return; }

                logger.LogWarning(
                    "[PATH_REQ] id={RequestId} still-running elapsed>={Seconds}s map={MapId} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1}) dist2D={Dist:F1}",
                    requestId, threshold, mapId, start.X, start.Y, start.Z, end.X, end.Y, end.Z, dist2D);
            }
        }

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

        private static bool IsFinitePosition(Game.Position p)
            => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

        private static bool IsFinitePoint(XYZ p)
            => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

        private static PathfindingResponse ErrorResponse(string msg)
            => new() { Error = new Error { Message = msg } };
    }
}
