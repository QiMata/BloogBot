using System;
using System.Runtime.InteropServices;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using SceneData;
using System.Globalization;
using System.Collections.Concurrent;

namespace SceneDataService;

/// <summary>
/// TCP server that extracts and serves scene collision data from Navigation.dll on demand.
/// Bots request scene data for bounded regions; the service extracts triangles from
/// VMAP + ADT via Navigation.dll and returns them as packed float arrays.
/// </summary>
public sealed class SceneDataSocketServer : ProtobufSocketServer<SceneGridRequest, SceneGridResponse>
{
    private readonly ILogger _logger;
    private bool _initialized;
    private List<uint> _preloadedMapIds = [];
    private readonly ConcurrentDictionary<string, SceneGridResponse> _regionCache = new();
    private readonly ConcurrentDictionary<string, object> _regionLocks = new();

    public SceneDataSocketServer(string ipAddress, int port, ILogger logger)
        : base(ipAddress, port, logger)
    {
        _logger = logger;
    }

    private readonly object _initLock = new();

    public void InitializeNavigation()
    {
        lock (_initLock)
        {
            if (_initialized) return;

            // Set data directory from environment before loading maps
            var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            if (!string.IsNullOrEmpty(dataDir))
            {
                _logger.LogInformation("[SceneDataService] Setting data directory: {DataDir}", dataDir);
                NativeScene.SetDataDirectory(dataDir);
            }

            _preloadedMapIds = DiscoverMapIds(dataDir);
            _logger.LogInformation("[SceneDataService] Discovered {Count} maps to preload: {Maps}",
                _preloadedMapIds.Count, string.Join(", ", _preloadedMapIds));

            foreach (var mapId in _preloadedMapIds)
            {
                _logger.LogInformation("[SceneDataService] Preloading map {MapId}...", mapId);
                NativeScene.PreloadMap(mapId);
                _logger.LogInformation("[SceneDataService] Map {MapId} preloaded.", mapId);
            }

            _initialized = true;
            _logger.LogInformation("[SceneDataService] Navigation initialized for {Count} maps.",
                _preloadedMapIds.Count);
        }
    }

    protected override SceneGridResponse HandleRequest(SceneGridRequest request)
    {
        _logger.LogInformation("[SceneDataService] HandleRequest: map={Map} bounds=({MinX:F0},{MinY:F0})-({MaxX:F0},{MaxY:F0})",
            request.MapId, request.MinX, request.MinY, request.MaxX, request.MaxY);

        if (!_initialized)
            InitializeNavigation();

        var cacheKey = BuildRegionCacheKey(request);
        if (_regionCache.TryGetValue(cacheKey, out var cachedResponse))
        {
            _logger.LogInformation("[SceneDataService] Cache hit for {Key}: {Count} triangles", cacheKey, cachedResponse.TriangleCount);
            return CloneResponse(cachedResponse);
        }

        var regionLock = _regionLocks.GetOrAdd(cacheKey, static _ => new object());
        lock (regionLock)
        {
            try
            {
                if (_regionCache.TryGetValue(cacheKey, out cachedResponse))
                    return CloneResponse(cachedResponse);

                var response = ExtractSceneGrid(request);
                _regionCache[cacheKey] = CloneResponse(response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SceneDataService] Error extracting scene grid for map {MapId}", request.MapId);
                return new SceneGridResponse
                {
                    MapId = request.MapId,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                _regionLocks.TryRemove(cacheKey, out _);
            }
        }
    }

    private SceneGridResponse ExtractSceneGrid(SceneGridRequest request)
    {
        _logger.LogDebug("[SceneDataService] Extracting grid: map={Map} bounds=({MinX},{MinY})-({MaxX},{MaxY})",
            request.MapId, request.MinX, request.MinY, request.MaxX, request.MaxY);

        // Use Navigation.dll's SceneQuery to extract triangles in the bounded region.
        // We query GetGroundZ at a grid of points to determine terrain triangles,
        // then extract the full AABB contact set for the region.
        var response = new SceneGridResponse
        {
            MapId = request.MapId,
            MinX = request.MinX,
            MinY = request.MinY,
            MaxX = request.MaxX,
            MaxY = request.MaxY,
        };

        // Extract triangles via the TestTerrainAABB export.
        // Use a ground-focused Z range to avoid including roofs/upper stories in
        // dense cities like Orgrimmar. Physics needs ground-level collision, not
        // building roofs 50y above the player.
        int maxTriangles = 50000;
        var triangles = new NativeScene.AABBContact[maxTriangles];
        var handle = GCHandle.Alloc(triangles, GCHandleType.Pinned);
        try
        {
            // Full Z range query — .scene files include all geometry.
            // Client-side physics handles ground detection; we send everything
            // and let the physics capsule sweep find the correct ground surface.
            int count = NativeScene.QueryTerrainAABBTriangles(
                request.MapId,
                request.MinX, request.MinY, -500f,
                request.MaxX, request.MaxY, 2000f,
                handle.AddrOfPinnedObject(),
                maxTriangles);

            response.TriangleCount = (uint)count;

            for (int i = 0; i < count; i++)
            {
                ref var t = ref triangles[i];
                // 9 floats per triangle (3 vertices × 3 components)
                response.TriangleData.Add(t.PointX); response.TriangleData.Add(t.PointY); response.TriangleData.Add(t.PointZ);
                response.TriangleData.Add(t.V1X); response.TriangleData.Add(t.V1Y); response.TriangleData.Add(t.V1Z);
                response.TriangleData.Add(t.V2X); response.TriangleData.Add(t.V2Y); response.TriangleData.Add(t.V2Z);
                // Normal
                response.NormalData.Add(t.NormalX); response.NormalData.Add(t.NormalY); response.NormalData.Add(t.NormalZ);
                // Walkability
                response.Walkable.Add(t.Walkable != 0);
            }

            response.Success = true;
            _logger.LogInformation("[SceneDataService] Extracted {Count} triangles for map {Map} bounds ({MinX:F0},{MinY:F0})-({MaxX:F0},{MaxY:F0})",
                count, request.MapId, request.MinX, request.MinY, request.MaxX, request.MaxY);
        }
        finally
        {
            handle.Free();
        }

        return response;
    }

    private static string BuildRegionCacheKey(SceneGridRequest request)
        => FormattableString.Invariant($"{request.MapId}_{request.MinX:F0}_{request.MinY:F0}_{request.MaxX:F0}_{request.MaxY:F0}");

    private static SceneGridResponse CloneResponse(SceneGridResponse response)
    {
        var clone = new SceneGridResponse
        {
            MapId = response.MapId,
            MinX = response.MinX,
            MinY = response.MinY,
            MaxX = response.MaxX,
            MaxY = response.MaxY,
            Success = response.Success,
            ErrorMessage = response.ErrorMessage,
            TriangleCount = response.TriangleCount,
        };

        clone.TriangleData.Add(response.TriangleData);
        clone.NormalData.Add(response.NormalData);
        clone.Walkable.Add(response.Walkable);
        return clone;
    }

    private static List<uint> DiscoverMapIds(string? dataDir)
    {
        var ids = new HashSet<uint>();

        if (!string.IsNullOrWhiteSpace(dataDir) && Directory.Exists(dataDir))
        {
            // SceneDataService serves collision geometry. Priority:
            // 1. .scene files — pre-extracted collision caches (fast, preferred)
            // 2. vmaps — raw VMAP data for on-demand extraction (fallback)
            // NO mmaps — those are navmesh for PathfindingService only.
            AddIdsFromDirectory(Path.Combine(dataDir, "scenes"), "*.scene", ParseWholeStem, ids);
            AddIdsFromDirectory(Path.Combine(dataDir, "vmaps"), "*.vmtree", ParseFirstThreeDigits, ids);
        }

        if (ids.Count == 0)
        {
            ids.Add(0);
            ids.Add(1);
        }

        return ids.OrderBy(id => id).ToList();
    }

    private static void AddIdsFromDirectory(string directory, string pattern, Func<string, uint?> parser, HashSet<uint> ids)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            var id = parser(Path.GetFileNameWithoutExtension(file));
            if (id.HasValue)
                ids.Add(id.Value);
        }
    }

    private static uint? ParseWholeStem(string stem)
        => uint.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var mapId) ? mapId : null;

    private static uint? ParseFirstThreeDigits(string stem)
    {
        if (stem.Length < 3)
            return null;

        return uint.TryParse(stem[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var mapId) ? mapId : null;
    }
}

/// <summary>
/// P/Invoke declarations for Navigation.dll's scene query exports.
/// </summary>
internal static class NativeScene
{
    private const string DllName = "Navigation";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void SetDataDirectory(string dataDir);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void PreloadMap(uint mapId);

    // This export doesn't exist yet — we need to add it to DllMain.cpp.
    // For now, use GetGroundZ as a fallback to verify the service works.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int QueryTerrainAABBTriangles(
        uint mapId,
        float minX, float minY, float minZ,
        float maxX, float maxY, float maxZ,
        IntPtr outContacts,
        int maxContacts);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetGroundZ(uint mapId, float x, float y, float queryZ, float maxSearchDist);

    [StructLayout(LayoutKind.Sequential)]
    public struct AABBContact
    {
        public float PointX, PointY, PointZ;    // Contact point
        public float NormalX, NormalY, NormalZ;  // Surface normal
        public float V1X, V1Y, V1Z;             // Triangle vertex 1
        public float V2X, V2Y, V2Z;             // Triangle vertex 2
        public int Walkable;                      // 1 = walkable surface
        public uint InstanceId;                   // VMAP instance
    }
}
