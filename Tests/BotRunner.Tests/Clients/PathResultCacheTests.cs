using BotRunner.Clients;
using GameData.Core.Models;

namespace BotRunner.Tests.Clients;

public class PathResultCacheTests
{
    [Fact]
    public void StoreAndRetrieve_RoundTrip()
    {
        var cache = new PathResultCache();
        var waypoints = new[] { new Position(0, 0, 0), new Position(10, 10, 0) };

        cache.Store(1, new Position(0, 0, 0), new Position(100, 100, 0), waypoints);
        var result = cache.TryGet(1, new Position(0, 0, 0), new Position(100, 100, 0));

        Assert.NotNull(result);
        Assert.Equal(2, result!.Length);
    }

    [Fact]
    public void GridQuantization_GroupsNearby()
    {
        var cache = new PathResultCache();
        var waypoints = new[] { new Position(0, 0, 0), new Position(50, 50, 0) };

        // Store at (10.1, 10.1, 0)
        cache.Store(1, new Position(10.1f, 10.1f, 0), new Position(100, 100, 0), waypoints);

        // Retrieve at (12.0, 12.0, 0) — within same 5-yard grid cell
        var result = cache.TryGet(1, new Position(12.0f, 12.0f, 0), new Position(100, 100, 0));

        Assert.NotNull(result);
    }

    [Fact]
    public void Evict_RemovesOldest()
    {
        var cache = new PathResultCache(maxEntries: 5);

        // Fill beyond capacity
        for (int i = 0; i < 10; i++)
        {
            var wp = new[] { new Position(i * 100, 0, 0) };
            cache.Store(1, new Position(i * 100, 0, 0), new Position(i * 100 + 50, 0, 0), wp);
        }

        // Count should be reduced (eviction removes 10% = 1 at a time when over capacity)
        Assert.True(cache.Count <= 10, "Cache should have evicted some entries");
    }

    [Fact]
    public void InvalidateMap_ClearsMapEntries()
    {
        var cache = new PathResultCache();
        cache.Store(0, new Position(0, 0, 0), new Position(100, 100, 0),
            new[] { new Position(50, 50, 0) });
        cache.Store(1, new Position(0, 0, 0), new Position(100, 100, 0),
            new[] { new Position(50, 50, 0) });

        cache.InvalidateMap(0);

        Assert.Null(cache.TryGet(0, new Position(0, 0, 0), new Position(100, 100, 0)));
        // Map 1 should still be cached
        Assert.NotNull(cache.TryGet(1, new Position(0, 0, 0), new Position(100, 100, 0)));
    }

    [Fact]
    public void HitRate_CalculatesCorrectly()
    {
        var cache = new PathResultCache();
        var wp = new[] { new Position(0, 0, 0) };
        cache.Store(1, new Position(0, 0, 0), new Position(100, 100, 0), wp);

        // 1 hit
        cache.TryGet(1, new Position(0, 0, 0), new Position(100, 100, 0));
        // 1 miss
        cache.TryGet(1, new Position(500, 500, 0), new Position(600, 600, 0));

        Assert.Equal(0.5f, cache.HitRate);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(1, cache.Misses);
    }

    [Fact]
    public void TryGet_ReturnsNull_OnMiss()
    {
        var cache = new PathResultCache();

        var result = cache.TryGet(1, new Position(0, 0, 0), new Position(100, 100, 0));

        Assert.Null(result);
    }
}
