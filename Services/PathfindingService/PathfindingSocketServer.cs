using BotCommLayer;
using GameData.Core.Models;
using Pathfinding;
using PathfindingService.NavSummary;
using PathfindingService.RouteCaching;
using PathfindingService.Repository;
using PathfindingService.RoutePacks;
using GameData.Core.Constants;
using GameData.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Globalization;
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
    public class PathfindingSocketServer(string ipAddress, int port, ILogger logger, IConfiguration? configuration = null)
        : ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(ipAddress, port, logger)
    {
        private const string NativePreloadMapsEnvironmentVariable = "WWOW_NAVIGATION_PRELOAD_MAPS";
        private const string DynamicObjectOverlayEnvironmentVariable = "WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY";

        private Navigation _navigation;
        private readonly IConfiguration? _configuration = configuration;
        private StaticRoutePackCache? _mainPathCache;
        private readonly INavigationDataSignatureProvider _navigationDataSignatureProvider = new FileSystemNavigationDataSignatureProvider();
        private readonly RouteResultCache _routeResultCache = new();
        private readonly NavSummaryRouteResolver _navSummaryRouteResolver = NavSummaryRouteResolver.FromConfiguration(configuration, logger);
        private readonly RequestScopedDynamicObjectOverlay _dynamicObjectOverlay = new(new NativeDynamicObjectOverlayRegistry());
        private volatile bool _isInitialized;
        private readonly object _initLock = new();

        public bool IsInitialized => _isInitialized;
        public RouteResultCacheSnapshot RouteCacheStats => _routeResultCache.Snapshot;
        public NavigationPerformanceSnapshot NavigationPerformanceStats => NavigationPerformanceMetrics.Snapshot;

        public void InitializeNavigation()
        {
            lock (_initLock)
            {
                if (_isInitialized) return;

                WriteStatus(false, "Loading navigation data...", []);

                var initSw = System.Diagnostics.Stopwatch.StartNew();
                logger.LogInformation("Loading Navigation data...");
                _navigation = new Navigation();
                var loadedMaps = PreloadConfiguredMaps(_navigation);
                logger.LogInformation("Navigation loaded in {Elapsed:F1}s", initSw.Elapsed.TotalSeconds);
                WarmStaticRoutePacks();

                _isInitialized = true;

                if (IsStartupDiagnosticsEnabled())
                    DiagnoseNativePathfinding(logger);
                else
                    logger.LogInformation("[Navigation] startup diagnostics disabled; set Navigation:RunStartupDiagnostics=true to run native sample paths");

                WriteStatus(true, "Ready - navigation initialized", loadedMaps);
                logger.LogInformation("Navigation system initialized.");
                // PFS-OVERHAUL-006: explicit machine-parseable marker for
                // PathfindingTestFixture to poll. Goes through stdout, gets
                // captured by Process.OutputDataReceived. Format is stable;
                // tools/scripts and the test fixture grep for the literal
                // prefix "[PathfindingService] PRELOAD_COMPLETE".
                logger.LogInformation(
                    "[PathfindingService] PRELOAD_COMPLETE ready_to_serve=true maps={MapCount}",
                    loadedMaps.Count);
            }
        }

        private IReadOnlyList<uint> PreloadConfiguredMaps(Navigation navigation)
        {
            var configuredValue = ResolveConfiguredPreloadMapSetting(_configuration);
            var mapIds = ParsePreloadMapIds(configuredValue, Environment.GetEnvironmentVariable("WWOW_DATA_DIR"));
            if (mapIds.Count == 0)
            {
                logger.LogInformation("[Navigation] startup mmap preload disabled");
                return mapIds;
            }

            logger.LogInformation(
                "[Navigation] preloading {Count} configured map(s): {Maps}",
                mapIds.Count,
                string.Join(",", mapIds));

            foreach (var mapId in mapIds)
                navigation.PreloadMap(mapId);

            return mapIds;
        }

        private bool IsStartupDiagnosticsEnabled()
        {
            var configured = _configuration?["Navigation:RunStartupDiagnostics"]
                ?? _configuration?["PathfindingService:Navigation:RunStartupDiagnostics"];
            return bool.TryParse(configured, out var enabled) && enabled;
        }

        public static string ResolveConfiguredPreloadMapSetting(IConfiguration? configuration)
        {
            var nativeEnvironmentValue = Environment.GetEnvironmentVariable(NativePreloadMapsEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(nativeEnvironmentValue))
                return nativeEnvironmentValue;

            return configuration?["Navigation:PreloadMaps"]
                ?? configuration?["PathfindingService:Navigation:PreloadMaps"]
                ?? "none";
        }

        public static IReadOnlyList<uint> ParsePreloadMapIds(string? configuredValue, string? dataRoot)
        {
            var value = configuredValue?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return [];

            if (value.Equals("none", StringComparison.OrdinalIgnoreCase)
                || value.Equals("off", StringComparison.OrdinalIgnoreCase)
                || value.Equals("false", StringComparison.OrdinalIgnoreCase)
                || value.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            if (value.Equals("all", StringComparison.OrdinalIgnoreCase)
                || value.Equals("*", StringComparison.OrdinalIgnoreCase))
            {
                return DiscoverAvailableMMapIds(dataRoot);
            }

            var normalized = value
                .Replace(';', ',')
                .Replace('|', ',')
                .Replace(' ', ',')
                .Replace('\t', ',')
                .Replace('\r', ',')
                .Replace('\n', ',');

            var result = new SortedSet<uint>();
            foreach (var token in normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Equals("all", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("*", StringComparison.OrdinalIgnoreCase))
                {
                    return DiscoverAvailableMMapIds(dataRoot);
                }

                if (uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var mapId))
                    result.Add(mapId);
            }

            return result.ToArray();
        }

        private static IReadOnlyList<uint> DiscoverAvailableMMapIds(string? dataRoot)
        {
            if (string.IsNullOrWhiteSpace(dataRoot))
                return [];

            string mmapsDir;
            try
            {
                mmapsDir = Path.Combine(Path.GetFullPath(dataRoot), "mmaps");
            }
            catch
            {
                return [];
            }

            if (!Directory.Exists(mmapsDir))
                return [];

            var ids = new SortedSet<uint>();
            foreach (var path in Directory.EnumerateFiles(mmapsDir, "*.mmap"))
            {
                if (uint.TryParse(Path.GetFileNameWithoutExtension(path), NumberStyles.None, CultureInfo.InvariantCulture, out var mapId))
                    ids.Add(mapId);
            }

            if (ids.Count == 0)
            {
                foreach (var path in Directory.EnumerateFiles(mmapsDir, "*.mmtile"))
                {
                    var stem = Path.GetFileNameWithoutExtension(path);
                    if (stem.Length >= 3
                        && uint.TryParse(stem[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var mapId))
                    {
                        ids.Add(mapId);
                    }
                }
            }

            return ids.ToArray();
        }

        private void WarmStaticRoutePacks()
        {
            // PFS-OVERHAUL-006 (2026-05-07): the static route pack is OFF by default.
            // While the pathfinding overhaul is in flight we want every path query
            // to exercise the live navmesh so MmapGen iteration produces visible
            // results. The cache pre-bakes routes against the navmesh that was
            // current at warm-up time and serves them in front of Detour, which
            // makes mesh changes invisible (the OG zeppelin tower stall point at
            // (1338.1, -4646.0, 51.6) was the canonical case). Set
            // WWOW_ENABLE_STATIC_ROUTE_PACK=1 to opt back in when deploying a
            // stable image; the seeds and warm-up logic are otherwise unchanged.
            if (!IsStaticRoutePackEnabled())
            {
                _mainPathCache = null;
                logger.LogWarning(
                    "[ROUTE_PACK] disabled by default during pathfinding overhaul; set "
                    + "WWOW_ENABLE_STATIC_ROUTE_PACK=1 to re-enable. All path queries "
                    + "will go through live Detour without route-pack interception.");
                logger.LogInformation(
                    "[PATH_REPAIR] retired from the default runtime path. Queries "
                    + "return raw native Detour output so bake problems remain "
                    + "visible to MmapGen iteration.");
                return;
            }

            var warmSw = System.Diagnostics.Stopwatch.StartNew();
            _mainPathCache = new StaticRoutePackCache(
                StaticRoutePackCache.CreateDefaultSeeds(),
                _navigationDataSignatureProvider,
                seed =>
                {
                    var (radius, height) = seed.Capsule;
                    return seed.GenerationMode == StaticRoutePackGenerationMode.CorridorSeedPath
                        ? _navigation.CalculateRoutePackSeedPath(
                            seed.MapId,
                            seed.StartAnchor,
                            seed.EndAnchor,
                            seed.SmoothPath,
                            radius,
                            height)
                        : _navigation.CalculateStaticRoutePackPath(
                            seed.MapId,
                            seed.StartAnchor,
                            seed.EndAnchor,
                            seed.SmoothPath,
                            radius,
                            height);
                });

            if (IsStaticRoutePackStartupWarmupEnabled())
            {
                _mainPathCache.WarmUpAll(logger);
            }
            else
            {
                logger.LogWarning(
                    "[ROUTE_PACK] startup warmup disabled; set WWOW_ROUTE_PACK_STARTUP_WARMUP=1 to pre-generate route packs during service initialization");
            }

            logger.LogInformation(
                "[ROUTE_PACK] startup warmup completed in {ElapsedMs}ms packs={PackCount}",
                warmSw.ElapsedMilliseconds,
                _mainPathCache.Count);
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

        private void WriteStatus(bool isReady, string message, IEnumerable<uint> loadedMaps)
        {
            try
            {
                new PathfindingServiceStatus
                {
                    IsReady = isReady,
                    StatusMessage = message,
                    LoadedMaps = loadedMaps.ToList(),
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

            logger.LogDebug(
                "[PATH_DIAG] id={RequestId} race={Race} gender={Gender} capsule=({Radius:F4},{Height:F4}) start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1})",
                requestId, req.Race, req.Gender, agentRadius, agentHeight, start.X, start.Y, start.Z, end.X, end.Y, end.Z);

            NavigationPathResult pathResult;
            StaticRoutePackMatch? routePackMatch = null;
            NavSummaryRouteMatch? navSummaryMatch = null;
            RouteResultCacheStatus routeCacheStatus;
            try
            {
                var dynamicOverlayEnabled = IsDynamicObjectOverlayEnabled();
                IReadOnlyList<DynamicObjectProto> effectiveNearbyObjects = dynamicOverlayEnabled
                    ? req.NearbyObjects
                    : Array.Empty<DynamicObjectProto>();

                if (!dynamicOverlayEnabled && req.NearbyObjects.Count > 0 && dist2D >= 100f)
                {
                    logger.LogInformation(
                        "[PATH_REQ] id={RequestId} ignored {OverlayCount} nearby object overlay(s); static mmap navigation is authoritative unless dynamic overlay is explicitly enabled",
                        requestId,
                        req.NearbyObjects.Count);
                }

                var routePackDynamicObjects = effectiveNearbyObjects
                    .Select(static obj => new StaticRoutePackDynamicObject(
                        obj.DisplayId,
                        new XYZ(obj.X, obj.Y, obj.Z),
                        obj.Scale > 0f && float.IsFinite(obj.Scale) ? obj.Scale : 1f))
                    .ToArray();
                var routePackRequest = new StaticRoutePackRequest(
                    req.MapId,
                    start,
                    end,
                    (Race)req.Race,
                    (Gender)req.Gender,
                    req.Straight,
                    StaticRoutePackCache.DefaultRoutePolicy,
                    effectiveNearbyObjects.Count,
                    routePackDynamicObjects);
                var routeCacheRequest = new RouteResultCacheRequest(
                    req.MapId,
                    start,
                    end,
                    req.Race,
                    req.Gender,
                    agentRadius,
                    agentHeight,
                    req.Straight,
                    StaticRoutePackCache.DefaultRoutePolicy,
                    _navigationDataSignatureProvider.GetSignature(req.MapId),
                    _navSummaryRouteResolver.ApplyToRouteAlgorithmSignature(RouteResultCache.RouteAlgorithmSignature),
                    CreateDynamicOverlaySignature(effectiveNearbyObjects.Count));

                var lookup = _routeResultCache.GetOrAdd(
                    routeCacheRequest,
                    () =>
                    {
                        if (_mainPathCache is not null &&
                            _mainPathCache.TryGetPath(routePackRequest, agentRadius, agentHeight, out var cachedPathResult, out var match))
                        {
                            return new RouteComputationResult(cachedPathResult, match);
                        }

                        NavigationPathResult CalculateDetailedPath(XYZ queryStart, XYZ queryEnd)
                        {
                            var pathQueryFn =
                                (Func<NavigationPathResult>)(() => _navigation.CalculateRawPath(
                                    req.MapId,
                                    queryStart,
                                    queryEnd,
                                    req.Straight,
                                    agentRadius,
                                    agentHeight));
                            var overlayResult = _dynamicObjectOverlay.ExecuteWithOverlay(
                                req.MapId, effectiveNearbyObjects,
                                pathQueryFn,
                                logger, operationName: "path");
                            return overlayResult.Value;
                        }

                        var summaryRequest = new NavSummaryRouteRequest(
                            req.MapId,
                            start,
                            end,
                            req.Straight,
                            agentRadius,
                            agentHeight,
                            dist2D,
                            effectiveNearbyObjects.Count);
                        if (_navSummaryRouteResolver.TryResolve(
                            summaryRequest,
                            CalculateDetailedPath,
                            out var summaryResolution))
                        {
                            return new RouteComputationResult(summaryResolution.PathResult, summaryResolution.Match);
                        }

                        return new RouteComputationResult(CalculateDetailedPath(start, end));
                    });

                routeCacheStatus = lookup.Status;
                pathResult = lookup.Result.PathResult;
                if (lookup.Result.MatchMetadata is StaticRoutePackMatch match)
                {
                    routePackMatch = match;
                }

                if (lookup.Result.MatchMetadata is NavSummaryRouteMatch summaryMatch)
                {
                    navSummaryMatch = summaryMatch;
                }
            }
            finally
            {
                slowRequestCts.Cancel();
            }

            var sanitizedPath = pathResult.Path.Where(IsFinitePoint).ToArray();
            if (routePackMatch is StaticRoutePackMatch cacheHit)
            {
                logger.LogInformation(
                    "[ROUTE_PACK] id={RequestId} hit seed={SeedId} result={Result} startSegment={StartSegment} corridorDist={CorridorDist:F2} corners={Corners} navSig={NavSig}",
                    requestId,
                    cacheHit.SeedId,
                    cacheHit.Result,
                    cacheHit.StartSegmentIndex,
                    cacheHit.StartDistanceFromCorridor,
                    sanitizedPath.Length,
                    cacheHit.NavDataSignature.Length <= 12 ? cacheHit.NavDataSignature : cacheHit.NavDataSignature[..12]);
            }

            if (navSummaryMatch is NavSummaryRouteMatch summaryHit)
            {
                logger.LogInformation(
                    "[NAV_SUMMARY] id={RequestId} hit graph={GraphId} anchors={Anchors} segments={Segments} estimatedCost={EstimatedCost:F1} corners={Corners} graphSig={GraphSig}",
                    requestId,
                    summaryHit.GraphId,
                    summaryHit.AnchorCount,
                    summaryHit.SegmentCount,
                    summaryHit.EstimatedCost,
                    sanitizedPath.Length,
                    summaryHit.GraphSignature.Length <= 12 ? summaryHit.GraphSignature : summaryHit.GraphSignature[..12]);
            }

            if (ShouldLogRouteCacheStatus(routeCacheStatus, dist2D, requestId))
            {
                var cacheStats = _routeResultCache.Snapshot;
                logger.LogInformation(
                    "[ROUTE_CACHE] id={RequestId} status={Status} entries={Entries} inFlight={InFlight} hits={Hits} misses={Misses} coalesced={Coalesced} expired={Expired} bypassed={Bypassed} negativeStores={NegativeStores} slow={Slow}",
                    requestId,
                    routeCacheStatus,
                    cacheStats.EntryCount,
                    cacheStats.InFlightCount,
                    cacheStats.HitCount,
                    cacheStats.MissCount,
                    cacheStats.CoalescedCount,
                    cacheStats.ExpiredCount,
                    cacheStats.BypassCount,
                    cacheStats.StoredNegativeCount,
                    cacheStats.SlowRequestCount);
            }

            if (ShouldLogPathResultDiagnostic(routeCacheStatus, pathResult, requestSw.ElapsedMilliseconds, dist2D, requestId))
            {
                logger.LogInformation(
                    "[PATH_DIAG] id={RequestId} result={Result} pathLen={PathLen} rawPathLen={RawPathLen} blockedIdx={BlockedIdx} blockedReason={BlockedReason} elapsedMs={ElapsedMs}",
                    requestId, pathResult.Result, sanitizedPath.Length, pathResult.RawPath.Length,
                    pathResult.BlockedSegmentIndex?.ToString() ?? "none",
                    pathResult.BlockedReason,
                    requestSw.ElapsedMilliseconds);
            }

            if (ShouldLogNavigationMetrics(routeCacheStatus, pathResult, requestSw.ElapsedMilliseconds, dist2D, requestId))
            {
                var navStats = NavigationPerformanceMetrics.Snapshot;
                logger.LogInformation(
                    "[NAV_METRICS] id={RequestId} validated={Validated} avgValidatedMs={AvgValidatedMs:F1} maxValidatedMs={MaxValidatedMs} resolver={Resolver} avgResolverMs={AvgResolverMs:F1} nativeFind={NativeFind} avgNativeFindMs={AvgNativeFindMs:F1} maxNativeFindMs={MaxNativeFindMs} corridor={Corridor} avgCorridorMs={AvgCorridorMs:F1} managedValidation={ManagedValidation} avgValidationMs={AvgValidationMs:F1} repairs(los={LosRepairs},wall={WallRepairs},steep={SteepRepairs},localLayer={LocalLayerRepairs},segment={SegmentRepairs},dynamic={DynamicRepairs}) blocked={Blocked} noPath={NoPath} slow(path={SlowPath},native={SlowNative},validation={SlowValidation})",
                    requestId,
                    navStats.ValidatedPathRequests,
                    navStats.AverageValidatedPathMs,
                    navStats.ValidatedPathMaxMs,
                    navStats.PathResolverAttempts,
                    navStats.AveragePathResolverMs,
                    navStats.NativeFindPathAttempts,
                    navStats.AverageNativeFindPathMs,
                    navStats.NativeFindPathMaxMs,
                    navStats.CorridorQueryAttempts,
                    navStats.AverageCorridorQueryMs,
                    navStats.ManagedValidationRuns,
                    navStats.AverageManagedValidationMs,
                    navStats.LongLineOfSightRepairCount,
                    navStats.StaticWallRepairCount,
                    navStats.SteepAffordanceRepairCount,
                    navStats.LocalPhysicsLayerRepairCount,
                    navStats.SegmentValidationRepairCount,
                    navStats.DynamicOverlayRepairCount,
                    navStats.BlockedPathResults,
                    navStats.NoPathResults,
                    navStats.SlowValidatedPathCount,
                    navStats.SlowNativeFindPathCount,
                    navStats.SlowManagedValidationCount);
            }

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
            resp.HasBlockedSegment = pathResult.BlockedSegmentIndex.HasValue;
            if (pathResult.BlockedSegmentIndex is int blockedSegmentIndex)
            {
                resp.BlockedSegmentIndex = blockedSegmentIndex;
            }

            resp.BlockedReason = pathResult.BlockedReason;

            var affordanceSummary = PathAffordanceClassifier.Summarize(
                req.MapId,
                sanitizedPath,
                agentRadius,
                agentHeight);
            resp.MaxAffordance = affordanceSummary.MaxAffordance;
            resp.PathSupported = affordanceSummary.PathSupported;
            resp.StepUpCount = (uint)affordanceSummary.StepUpCount;
            resp.DropCount = (uint)affordanceSummary.DropCount;
            resp.CliffCount = (uint)affordanceSummary.CliffCount;
            resp.VerticalCount = (uint)affordanceSummary.VerticalCount;
            resp.TotalZGain = affordanceSummary.TotalZGain;
            resp.TotalZLoss = affordanceSummary.TotalZLoss;
            resp.MaxSlopeAngleDeg = affordanceSummary.MaxSlopeAngleDeg;
            resp.JumpGapCount = (uint)affordanceSummary.JumpGapCount;
            resp.SafeDropCount = (uint)affordanceSummary.SafeDropCount;
            resp.UnsafeDropCount = (uint)affordanceSummary.UnsafeDropCount;
            resp.BlockedCount = (uint)affordanceSummary.BlockedCount;
            resp.MaxClimbHeight = affordanceSummary.MaxClimbHeight;
            resp.MaxGapDistance = affordanceSummary.MaxGapDistance;
            resp.MaxDropHeight = affordanceSummary.MaxDropHeight;

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

        private static bool ShouldLogRouteCacheStatus(RouteResultCacheStatus status, float dist2D, long requestId)
            => status != RouteResultCacheStatus.Miss
                || dist2D >= 100f
                || requestId % 100 == 0;

        private static bool ShouldLogNavigationMetrics(
            RouteResultCacheStatus routeCacheStatus,
            NavigationPathResult pathResult,
            long elapsedMs,
            float dist2D,
            long requestId)
            => elapsedMs >= 1_000
                || dist2D >= 100f
                || requestId % 100 == 0
                || routeCacheStatus is RouteResultCacheStatus.Bypassed or RouteResultCacheStatus.Coalesced or RouteResultCacheStatus.Expired
                || string.Equals(pathResult.Result, "no_path", StringComparison.OrdinalIgnoreCase)
                || pathResult.Result.StartsWith("repaired_", StringComparison.OrdinalIgnoreCase)
                || pathResult.BlockedSegmentIndex.HasValue
                || !string.Equals(pathResult.BlockedReason, "none", StringComparison.OrdinalIgnoreCase);

        private static bool ShouldLogPathResultDiagnostic(
            RouteResultCacheStatus routeCacheStatus,
            NavigationPathResult pathResult,
            long elapsedMs,
            float dist2D,
            long requestId)
            => elapsedMs >= 1_000
                || dist2D >= 100f
                || requestId % 100 == 0
                || routeCacheStatus is RouteResultCacheStatus.Bypassed or RouteResultCacheStatus.Coalesced or RouteResultCacheStatus.Expired
                || string.Equals(pathResult.Result, "no_path", StringComparison.OrdinalIgnoreCase)
                || pathResult.Result.StartsWith("repaired_", StringComparison.OrdinalIgnoreCase)
                || pathResult.BlockedSegmentIndex.HasValue
                || !string.Equals(pathResult.BlockedReason, "none", StringComparison.OrdinalIgnoreCase);

        public static bool IsDynamicObjectOverlayEnabled(IConfiguration? configuration = null)
        {
            var configured = Environment.GetEnvironmentVariable(DynamicObjectOverlayEnvironmentVariable)
                ?? configuration?["Navigation:EnableDynamicObjectOverlay"]
                ?? configuration?["PathfindingService:Navigation:EnableDynamicObjectOverlay"];
            return IsTruthy(configured);
        }

        private bool IsDynamicObjectOverlayEnabled()
            => IsDynamicObjectOverlayEnabled(_configuration);

        private static bool IsTruthy(string? configured)
            => !string.IsNullOrWhiteSpace(configured)
                && (string.Equals(configured.Trim(), "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(configured.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(configured.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(configured.Trim(), "on", StringComparison.OrdinalIgnoreCase));

        private static string CreateDynamicOverlaySignature(int nearbyObjectCount)
        {
            if (nearbyObjectCount == 0)
                return RouteResultCache.StaticOverlaySignature;

            return $"dynamic:{nearbyObjectCount}";
        }

        private static bool IsStaticRoutePackStartupWarmupEnabled()
            => string.Equals(
                Environment.GetEnvironmentVariable("WWOW_ROUTE_PACK_STARTUP_WARMUP"),
                "1",
                StringComparison.OrdinalIgnoreCase);

        // PFS-OVERHAUL-006: gate StaticRoutePackCache creation. Default OFF
        // during the pathfinding overhaul so live Detour serves every path
        // query and MmapGen iteration is visible. Set to "1" to re-enable
        // pre-baked route caching for production deploys.
        private static bool IsStaticRoutePackEnabled()
            => string.Equals(
                Environment.GetEnvironmentVariable("WWOW_ENABLE_STATIC_ROUTE_PACK"),
                "1",
                StringComparison.OrdinalIgnoreCase);

        private static PathfindingResponse ErrorResponse(string msg)
            => new() { Error = new Error { Message = msg } };
    }
}
