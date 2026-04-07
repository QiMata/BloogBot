using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using SceneData;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement;

public sealed class SceneDataClientTests
{
    [Fact]
    public void EnsureSceneDataAround_SuppressesImmediateRetryAfterFailure()
    {
        var now = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var requestCount = 0;

        try
        {
            SceneDataClient.TestUtcNowOverride = () => now;
            SceneDataClient.TestSendTileRequestOverride = _ =>
            {
                requestCount++;
                throw new IOException("SceneDataService unavailable");
            };

            var client = new SceneDataClient(NullLogger.Instance);

            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));
            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));

            Assert.Equal(1, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestUtcNowOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_RetriesAfterBackoffExpires()
    {
        var now = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var requestCount = 0;

        try
        {
            SceneDataClient.TestUtcNowOverride = () => now;
            SceneDataClient.TestSendTileRequestOverride = _ =>
            {
                requestCount++;
                throw new IOException("SceneDataService unavailable");
            };

            var client = new SceneDataClient(NullLogger.Instance);

            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));

            now = now.AddSeconds(3);

            Assert.False(client.EnsureSceneDataAround(30, 100f, 200f));

            Assert.Equal(2, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestUtcNowOverride = null;
        }
    }

    [Fact]
    public void WorldToTile_ConvertsOrgrimmarCorrectly()
    {
        // Orgrimmar: ~(1629, -4373) should map to a tile around center
        var (tileX, tileY) = SceneDataClient.WorldToTile(1629f, -4373f);

        // tileX = 32 - floor(1629/533.33) = 32 - 3 = 29
        // tileY = 32 - floor(-4373/533.33) = 32 - (-9) = 41
        Assert.Equal(29u, tileX);
        Assert.Equal(41u, tileY);
    }

    [Fact]
    public void WorldToTile_ConvertsOriginCorrectly()
    {
        var (tileX, tileY) = SceneDataClient.WorldToTile(0f, 0f);
        Assert.Equal(32u, tileX);
        Assert.Equal(32u, tileY);
    }

    [Fact]
    public void WorldToTile_ConvertsNegativeCorrectly()
    {
        // Ratchet: ~(-988, -3834)
        var (tileX, tileY) = SceneDataClient.WorldToTile(-988f, -3834f);

        // tileX = 32 - floor(-988/533.33) = 32 - (-2) = 34
        // tileY = 32 - floor(-3834/533.33) = 32 - (-8) = 40
        Assert.Equal(34u, tileX);
        Assert.Equal(40u, tileY);
    }

    [Fact]
    public void TileBounds_RoundTrips()
    {
        var (minX, minY, maxX, maxY) = SceneDataClient.TileBounds(29, 41);

        // Tile 29,41 → minX = (32-29)*533.33 = 1600, minY = (32-41)*533.33 = -4800
        Assert.Equal(1600f, minX, precision: 0);
        Assert.Equal(-4800f, minY, precision: 0);
        Assert.Equal(2133f, maxX, precision: 0);
        Assert.Equal(-4267f, maxY, precision: 0);
    }

    [Fact]
    public void EnsureSceneDataAround_Requests3x3Neighborhood()
    {
        var requestedTiles = new List<(uint TX, uint TY)>();

        try
        {
            SceneDataClient.TestSendTileRequestOverride = req =>
            {
                requestedTiles.Add((req.TileX, req.TileY));
                return new SceneTileResponse { Success = true, TriangleCount = 0 };
            };
            SceneDataClient.TestInjectOverride = (_, _, _, _, _, _) => true;

            var client = new SceneDataClient(NullLogger.Instance);
            client.EnsureSceneDataAround(1, 1629f, -4373f);

            // Center tile is (29, 41), so 3x3 = 9 tiles
            Assert.Equal(9, requestedTiles.Count);
            Assert.Contains((28u, 40u), requestedTiles);
            Assert.Contains((29u, 41u), requestedTiles); // center
            Assert.Contains((30u, 42u), requestedTiles);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestInjectOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_CachesTilesAcrossCalls()
    {
        var requestCount = 0;

        try
        {
            SceneDataClient.TestSendTileRequestOverride = req =>
            {
                requestCount++;
                return new SceneTileResponse { Success = true, TriangleCount = 0 };
            };
            SceneDataClient.TestInjectOverride = (_, _, _, _, _, _) => true;

            var client = new SceneDataClient(NullLogger.Instance);

            // First call: requests all 9 tiles
            client.EnsureSceneDataAround(1, 1629f, -4373f);
            Assert.Equal(9, requestCount);

            // Same position: no new requests (center tile unchanged)
            client.EnsureSceneDataAround(1, 1629f, -4373f);
            Assert.Equal(9, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestInjectOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_OnlyRequestsMissingTilesOnBoundaryCross()
    {
        var requestedTiles = new List<(uint TX, uint TY)>();

        try
        {
            SceneDataClient.TestSendTileRequestOverride = req =>
            {
                requestedTiles.Add((req.TileX, req.TileY));
                return new SceneTileResponse { Success = true, TriangleCount = 0 };
            };
            SceneDataClient.TestInjectOverride = (_, _, _, _, _, _) => true;

            var client = new SceneDataClient(NullLogger.Instance);

            // First call at tile (29,41)
            client.EnsureSceneDataAround(1, 1629f, -4373f);
            Assert.Equal(9, requestedTiles.Count);

            requestedTiles.Clear();

            // Move one tile east: center becomes (28,41)
            // New 3x3 spans (27-29, 40-42). Tiles (29,40-42) already cached.
            // Only (27,40), (27,41), (27,42) are new.
            client.EnsureSceneDataAround(1, 2163f, -4373f);

            // Should only request the 3 new tiles
            Assert.Equal(3, requestedTiles.Count);
            Assert.All(requestedTiles, t => Assert.Equal(27u, t.TX));
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestInjectOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_EvictsTilesOutside5x5()
    {
        NativePhysics.InjectedTriangle[]? lastInjected = null;
        var requestedTiles = new List<(uint TX, uint TY)>();

        try
        {
            SceneDataClient.TestSendTileRequestOverride = req =>
            {
                requestedTiles.Add((req.TileX, req.TileY));
                var resp = new SceneTileResponse
                {
                    MapId = req.MapId,
                    TileX = req.TileX,
                    TileY = req.TileY,
                    Success = true,
                    TriangleCount = 1,
                };
                resp.TriangleData.AddRange(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 });
                resp.NormalData.AddRange(new float[] { 0, 0, 1 });
                resp.Walkable.Add(true);
                return resp;
            };
            SceneDataClient.TestInjectOverride = (_, _, _, _, _, tris) =>
            {
                lastInjected = (NativePhysics.InjectedTriangle[])tris.Clone();
                return true;
            };

            var client = new SceneDataClient(NullLogger.Instance);

            // Load 3x3 at center (29,41) → 9 tiles (28-30, 40-42)
            client.EnsureSceneDataAround(1, 1629f, -4373f);
            Assert.Equal(9, lastInjected!.Length);

            requestedTiles.Clear();

            // Move far east → center (26,41). New 3x3 = (25-27, 40-42).
            // Old tiles at x=29,30 are dx=3,4 from center 26 → evicted.
            // Old tile at x=28 is dx=2 → retained within 5x5.
            // So 6 new tiles requested (25-27 × 40-42 minus 28 × 40-42 retained = 9 - 3 = 6 new)
            client.EnsureSceneDataAround(1, 3229f, -4373f);

            // New 3x3 (25-27, 40-42) doesn't overlap old 3x3 (28-30, 40-42)
            // So all 9 new tiles are requested fresh
            Assert.Equal(9, requestedTiles.Count);
            Assert.DoesNotContain(requestedTiles, t => t.TX == 29 || t.TX == 30);

            // After eviction: x=29 (dx=3>2) and x=30 (dx=4>2) evicted.
            // x=28 (dx=2) retained. New: 25,26,27. Total: 4 x-values × 3 y-values = 12 tiles.
            Assert.Equal(12, lastInjected!.Length);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestInjectOverride = null;
        }
    }
}
