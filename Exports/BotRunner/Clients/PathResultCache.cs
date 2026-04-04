using GameData.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Clients;

/// <summary>
/// LRU cache for pathfinding results. Caches by (mapId, startCell, endCell) key.
/// Bots in the same area requesting similar paths get a cached result with
/// start/end adjusted. Target: 50-80% cache hit rate for bots in same zone.
/// Capacity: 10,000 entries.
/// </summary>
public class PathResultCache
{
    private readonly int _maxEntries;
    private readonly ConcurrentDictionary<PathCacheKey, CachedPath> _cache = new();
    private long _hits;
    private long _misses;

    // Grid cell size in yards — positions snapped to this grid for cache key
    private const float CellSize = 5f;

    public PathResultCache(int maxEntries = 10000)
    {
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Try to get a cached path. Returns null on miss.
    /// Positions are quantized to a grid for fuzzy matching.
    /// </summary>
    public Position[]? TryGet(uint mapId, Position start, Position end)
    {
        var key = MakeKey(mapId, start, end);
        if (_cache.TryGetValue(key, out var cached))
        {
            cached.LastAccessed = DateTime.UtcNow;
            System.Threading.Interlocked.Increment(ref _hits);
            return cached.Waypoints;
        }

        System.Threading.Interlocked.Increment(ref _misses);
        return null;
    }

    /// <summary>
    /// Store a path result in the cache.
    /// </summary>
    public void Store(uint mapId, Position start, Position end, Position[] waypoints)
    {
        if (waypoints.Length == 0) return;

        var key = MakeKey(mapId, start, end);
        var entry = new CachedPath(waypoints, DateTime.UtcNow);

        _cache[key] = entry;

        // Evict oldest entries if over capacity
        if (_cache.Count > _maxEntries)
            Evict(_maxEntries / 10); // Evict 10% at a time
    }

    /// <summary>Cache hit rate (0.0 - 1.0).</summary>
    public float HitRate
    {
        get
        {
            var total = _hits + _misses;
            return total == 0 ? 0f : (float)_hits / total;
        }
    }

    /// <summary>Total cached entries.</summary>
    public int Count => _cache.Count;

    /// <summary>Total hits.</summary>
    public long Hits => _hits;

    /// <summary>Total misses.</summary>
    public long Misses => _misses;

    /// <summary>Clear the entire cache.</summary>
    public void Clear()
    {
        _cache.Clear();
        _hits = 0;
        _misses = 0;
    }

    /// <summary>Invalidate all entries for a specific map (e.g., on map change).</summary>
    public void InvalidateMap(uint mapId)
    {
        var keysToRemove = _cache.Keys.Where(k => k.MapId == mapId).ToList();
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
    }

    private void Evict(int count)
    {
        var oldest = _cache
            .OrderBy(kv => kv.Value.LastAccessed)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in oldest)
            _cache.TryRemove(key, out _);
    }

    private static PathCacheKey MakeKey(uint mapId, Position start, Position end)
    {
        return new PathCacheKey(
            mapId,
            QuantizeCoord(start.X), QuantizeCoord(start.Y), QuantizeCoord(start.Z),
            QuantizeCoord(end.X), QuantizeCoord(end.Y), QuantizeCoord(end.Z));
    }

    private static int QuantizeCoord(float value)
        => (int)MathF.Floor(value / CellSize);

    private record PathCacheKey(uint MapId, int StartCellX, int StartCellY, int StartCellZ,
        int EndCellX, int EndCellY, int EndCellZ);

    private class CachedPath(Position[] waypoints, DateTime lastAccessed)
    {
        public Position[] Waypoints { get; } = waypoints;
        public DateTime LastAccessed { get; set; } = lastAccessed;
    }
}
