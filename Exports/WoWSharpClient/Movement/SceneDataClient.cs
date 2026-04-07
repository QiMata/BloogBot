using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using SceneData;

namespace WoWSharpClient.Movement;

/// <summary>
/// Client that requests tile-based scene collision data from SceneDataService and injects
/// it into the local Navigation.dll SceneCache. Uses 533y ADT tiles with a 3x3
/// neighborhood around the bot's position. Tiles are cached locally and only
/// requested when missing. Tiles outside a 5x5 eviction radius are unloaded.
/// </summary>
public sealed class SceneDataClient : ProtobufSocketClient<SceneTileRequest, SceneTileResponse>, IDisposable
{
    private const int SceneDataConnectTimeoutMs = 1500;
    private const int SceneDataReadTimeoutMs = 30000;
    private const int SceneDataWriteTimeoutMs = 10000;
    private static readonly TimeSpan SceneDataRetryDelay = TimeSpan.FromSeconds(2);
    private readonly ILogger _logger;

    /// <summary>WoW ADT tile size in yards (64 × 533.33 = 34133.33 total map size).</summary>
    internal const float TileSize = 533.33333f;

    /// <summary>ADT grid center offset (0-based tile coords centered at 32).</summary>
    internal const int CenterGrid = 32;

    /// <summary>Per-tile cached triangle data keyed by "mapId_tileX_tileY".</summary>
    private readonly Dictionary<string, CachedTile> _tileCache = new();

    /// <summary>The tile key of the last injected center tile (for dedup).</summary>
    private string? _lastCenterTileKey;

    internal static Func<uint, float, float, bool>? TestEnsureSceneDataAroundOverride { get; set; }
    internal static Func<SceneTileRequest, SceneTileResponse>? TestSendTileRequestOverride { get; set; }
    internal static Func<DateTime>? TestUtcNowOverride { get; set; }
    /// <summary>Test override: captures injected triangles instead of P/Invoking InjectSceneTriangles.</summary>
    internal static Func<uint, float, float, float, float, NativePhysics.InjectedTriangle[], bool>? TestInjectOverride { get; set; }
    private DateTime _nextRetryUtc = DateTime.MinValue;

    public SceneDataClient(string ipAddress, int port, ILogger logger)
        : base(ipAddress, port, logger, connectImmediately: false)
    {
        _logger = logger;
    }

    internal SceneDataClient(ILogger logger)
        : base()
    {
        _logger = logger;
    }

    /// <summary>
    /// Convert world XY position to ADT tile coordinates.
    /// Formula: tileCoord = CenterGrid - floor(worldCoord / TileSize)
    /// </summary>
    internal static (uint TileX, uint TileY) WorldToTile(float x, float y)
    {
        uint tileX = (uint)(CenterGrid - (int)MathF.Floor(x / TileSize));
        uint tileY = (uint)(CenterGrid - (int)MathF.Floor(y / TileSize));
        return (tileX, tileY);
    }

    /// <summary>
    /// Convert tile coordinates back to world-space bounds.
    /// </summary>
    internal static (float MinX, float MinY, float MaxX, float MaxY) TileBounds(uint tileX, uint tileY)
    {
        float minX = (CenterGrid - (int)tileX) * TileSize;
        float minY = (CenterGrid - (int)tileY) * TileSize;
        return (minX, minY, minX + TileSize, minY + TileSize);
    }

    /// <summary>
    /// Ensure scene data is loaded for the 3x3 tile neighborhood around the position.
    /// Missing tiles are requested from the service. Tiles outside 5x5 are evicted.
    /// All loaded tiles are merged and injected into Navigation.dll.
    /// </summary>
    public bool EnsureSceneDataAround(uint mapId, float x, float y)
    {
        if (TestEnsureSceneDataAroundOverride != null)
            return TestEnsureSceneDataAroundOverride(mapId, x, y);

        var (centerTileX, centerTileY) = WorldToTile(x, y);
        string centerKey = $"{mapId}_{centerTileX}_{centerTileY}";

        // If center tile hasn't changed, no work needed
        if (string.Equals(_lastCenterTileKey, centerKey, StringComparison.Ordinal))
            return true;

        var now = GetUtcNow();
        if (now < _nextRetryUtc)
        {
            _logger.LogDebug("[SceneData] Skipping tile request until retry window opens at {RetryUtc:O}",
                _nextRetryUtc);
            return false;
        }

        // Compute 3x3 neighborhood
        var neededTiles = new List<(uint TX, uint TY, string Key)>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                uint tx = (uint)((int)centerTileX + dx);
                uint ty = (uint)((int)centerTileY + dy);
                string key = $"{mapId}_{tx}_{ty}";
                neededTiles.Add((tx, ty, key));
            }
        }

        // Request missing tiles
        bool anyNewTile = false;
        bool anyFailure = false;
        foreach (var (tx, ty, key) in neededTiles)
        {
            if (_tileCache.ContainsKey(key))
                continue;

            try
            {
                var request = new SceneTileRequest
                {
                    MapId = mapId,
                    TileX = tx,
                    TileY = ty,
                };

                _logger.LogInformation("[SceneData] Requesting tile ({TileX},{TileY}) for map {MapId}...",
                    tx, ty, mapId);

                var response = TestSendTileRequestOverride != null
                    ? TestSendTileRequestOverride(request)
                    : SendMessage(request, SceneDataReadTimeoutMs, SceneDataWriteTimeoutMs, SceneDataConnectTimeoutMs);

                if (!response.Success || response.TriangleCount == 0)
                {
                    _logger.LogWarning("[SceneData] Tile ({TileX},{TileY}) empty or failed: {Error}",
                        tx, ty, response.ErrorMessage ?? "empty");
                    // Cache empty tile to avoid re-requesting
                    _tileCache[key] = new CachedTile(mapId, tx, ty, [], 0, 0, 0, 0);
                    continue;
                }

                var (minX, minY, maxX, maxY) = TileBounds(tx, ty);
                // Use bounds from response if available, otherwise compute
                if (response.MinX != 0 || response.MinY != 0 || response.MaxX != 0 || response.MaxY != 0)
                {
                    minX = response.MinX;
                    minY = response.MinY;
                    maxX = response.MaxX;
                    maxY = response.MaxY;
                }

                _tileCache[key] = new CachedTile(mapId, tx, ty,
                    UnpackTriangles(response), minX, minY, maxX, maxY);
                anyNewTile = true;

                _logger.LogInformation("[SceneData] Cached tile ({TileX},{TileY}): {Count} triangles",
                    tx, ty, response.TriangleCount);
            }
            catch (Exception ex)
            {
                anyFailure = true;
                MarkRetryAfterFailure(now);
                _logger.LogError(ex, "[SceneData] FAILED to request tile ({TileX},{TileY})", tx, ty);
                break; // Stop requesting on network failure
            }
        }

        if (anyFailure && !anyNewTile)
            return false;

        // Evict tiles outside 5x5 radius
        EvictDistantTiles(mapId, centerTileX, centerTileY);

        // If we got new tiles, merge and inject all loaded tiles
        if (anyNewTile || !string.Equals(_lastCenterTileKey, centerKey, StringComparison.Ordinal))
        {
            InjectMergedTiles(mapId);
        }

        _lastCenterTileKey = centerKey;
        _nextRetryUtc = DateTime.MinValue;
        return true;
    }

    /// <summary>
    /// Request scene data for the grid tile containing the given position (legacy compat).
    /// </summary>
    public bool EnsureSceneDataAt(uint mapId, float x, float y)
        => EnsureSceneDataAround(mapId, x, y);

    private void EvictDistantTiles(uint mapId, uint centerTileX, uint centerTileY)
    {
        var toRemove = new List<string>();
        foreach (var (key, tile) in _tileCache)
        {
            if (tile.MapId != mapId)
                continue;

            int dx = Math.Abs((int)tile.TileX - (int)centerTileX);
            int dy = Math.Abs((int)tile.TileY - (int)centerTileY);
            if (dx > 2 || dy > 2) // Outside 5x5 (center +-2)
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
        {
            _tileCache.Remove(key);
            _logger.LogDebug("[SceneData] Evicted tile {Key}", key);
        }
    }

    private void InjectMergedTiles(uint mapId)
    {
        // Compute merged bounds and total triangle count
        float mergedMinX = float.MaxValue, mergedMinY = float.MaxValue;
        float mergedMaxX = float.MinValue, mergedMaxY = float.MinValue;
        int totalCount = 0;

        foreach (var tile in _tileCache.Values)
        {
            if (tile.MapId != mapId || tile.Triangles.Length == 0)
                continue;

            totalCount += tile.Triangles.Length;
            if (tile.MinX < mergedMinX) mergedMinX = tile.MinX;
            if (tile.MinY < mergedMinY) mergedMinY = tile.MinY;
            if (tile.MaxX > mergedMaxX) mergedMaxX = tile.MaxX;
            if (tile.MaxY > mergedMaxY) mergedMaxY = tile.MaxY;
        }

        if (totalCount == 0)
        {
            _logger.LogWarning("[SceneData] No triangles to inject for map {MapId}", mapId);
            return;
        }

        // Merge all tile triangles into one array
        var merged = new NativePhysics.InjectedTriangle[totalCount];
        int offset = 0;
        foreach (var tile in _tileCache.Values)
        {
            if (tile.MapId != mapId || tile.Triangles.Length == 0)
                continue;

            Array.Copy(tile.Triangles, 0, merged, offset, tile.Triangles.Length);
            offset += tile.Triangles.Length;
        }

        // Inject into Navigation.dll
        if (TestInjectOverride != null)
        {
            TestInjectOverride(mapId, mergedMinX, mergedMinY, mergedMaxX, mergedMaxY, merged);
            return;
        }

        var handle = GCHandle.Alloc(merged, GCHandleType.Pinned);
        try
        {
            NativePhysics.InjectSceneTriangles(mapId, mergedMinX, mergedMinY, mergedMaxX, mergedMaxY,
                handle.AddrOfPinnedObject(), totalCount);
        }
        finally
        {
            handle.Free();
        }

        _logger.LogInformation("[SceneData] Injected {Count} merged triangles for map {MapId} ({Tiles} tiles)",
            totalCount, mapId, _tileCache.Count);
    }

    private static NativePhysics.InjectedTriangle[] UnpackTriangles(SceneTileResponse response)
    {
        int count = (int)response.TriangleCount;
        if (count == 0) return [];

        var triangles = new NativePhysics.InjectedTriangle[count];
        for (int i = 0; i < count; i++)
        {
            int tBase = i * 9;
            triangles[i] = new NativePhysics.InjectedTriangle
            {
                V0X = response.TriangleData[tBase + 0],
                V0Y = response.TriangleData[tBase + 1],
                V0Z = response.TriangleData[tBase + 2],
                V1X = response.TriangleData[tBase + 3],
                V1Y = response.TriangleData[tBase + 4],
                V1Z = response.TriangleData[tBase + 5],
                V2X = response.TriangleData[tBase + 6],
                V2Y = response.TriangleData[tBase + 7],
                V2Z = response.TriangleData[tBase + 8],
            };
        }

        return triangles;
    }

    private void MarkRetryAfterFailure(DateTime now)
    {
        _nextRetryUtc = now + SceneDataRetryDelay;
    }

    private static DateTime GetUtcNow()
        => TestUtcNowOverride?.Invoke() ?? DateTime.UtcNow;

    /// <summary>Cached tile data for a single 533y ADT tile.</summary>
    private sealed record CachedTile(
        uint MapId, uint TileX, uint TileY,
        NativePhysics.InjectedTriangle[] Triangles,
        float MinX, float MinY, float MaxX, float MaxY);
}
