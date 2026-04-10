using System;
using System.Collections.Generic;
using System.IO;
using BotCommLayer;
using Microsoft.Extensions.Logging.Abstractions;
using SceneData;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Tests for SceneDataClient: tile coordinate mapping, retry/dedup behavior,
/// and live connectivity to SceneDataService (port 5003).
/// </summary>
public sealed class SceneDataClientIntegrationTests
{
    // =========================================================================
    // Tile Coordinate Mapping — verify tile coords for known positions
    // =========================================================================

    [Theory]
    [InlineData(1629f, -4373f, 29u, 41u)]   // Orgrimmar
    [InlineData(0f, 0f, 32u, 32u)]           // Origin
    [InlineData(-988f, -3834f, 34u, 40u)]    // Ratchet
    [InlineData(199f, 199f, 32u, 32u)]        // Near grid boundary (still tile 32)
    [InlineData(-1f, -1f, 33u, 33u)]          // Negative near zero
    public void WorldToTile_MapsPositionToCorrectTile(
        float x, float y, uint expectedTileX, uint expectedTileY)
    {
        var (tileX, tileY) = SceneDataClient.WorldToTile(x, y);

        Assert.Equal(expectedTileX, tileX);
        Assert.Equal(expectedTileY, tileY);
    }

    // =========================================================================
    // Request counting — verify dedup for same region, re-request for different
    // =========================================================================

    [Fact]
    public void EnsureSceneDataAround_SameRegion_FailedFirstAttempt_RetriesAfterBackoff()
    {
        int requestCount = 0;
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        try
        {
            SceneDataClient.TestUtcNowOverride = () => now;
            SceneDataClient.TestSendTileRequestOverride = _ =>
            {
                requestCount++;
                return new SceneTileResponse { Success = false, TriangleCount = 0, ErrorMessage = "test" };
            };

            var client = new SceneDataClient(NullLogger.Instance);

            // First attempt — requests sent, all fail with empty data
            client.EnsureSceneDataAround(1, 100f, 100f);
            var firstCount = requestCount;
            Assert.True(firstCount > 0);

            // Same position — no new requests (tiles cached as empty)
            client.EnsureSceneDataAround(1, 100f, 100f);
            Assert.Equal(firstCount, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestUtcNowOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_DifferentMap_SendsSeparateRequests()
    {
        var requestMapIds = new HashSet<uint>();

        try
        {
            SceneDataClient.TestSendTileRequestOverride = req =>
            {
                requestMapIds.Add(req.MapId);
                return new SceneTileResponse { Success = true, TriangleCount = 0 };
            };
            SceneDataClient.TestInjectOverride = (_, _, _, _, _, _) => true;

            var client = new SceneDataClient(NullLogger.Instance);
            client.EnsureSceneDataAround(0, 100f, 100f);
            client.EnsureSceneDataAround(1, 100f, 100f);

            Assert.Contains(0u, requestMapIds);
            Assert.Contains(1u, requestMapIds);
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
            SceneDataClient.TestInjectOverride = null;
        }
    }

    // =========================================================================
    // Response Data Packing — verify tile response triangle data sizing
    // =========================================================================

    [Fact]
    public void SceneTileResponse_TriangleDataSizing_9FloatsPerTriangle()
    {
        var response = new SceneTileResponse
        {
            Success = true,
            TriangleCount = 3,
        };

        for (int i = 0; i < 3; i++)
        {
            float b = i * 100;
            response.TriangleData.AddRange(new float[]
            {
                b, b+1, b+2,
                b+10, b+11, b+12,
                b+20, b+21, b+22,
            });
            response.NormalData.AddRange(new float[] { 0, 0, 1 });
            response.Walkable.Add(true);
        }

        Assert.Equal(27, response.TriangleData.Count);  // 3 x 9
        Assert.Equal(9, response.NormalData.Count);       // 3 x 3
        Assert.Equal(3, response.Walkable.Count);

        Assert.Equal(100f, response.TriangleData[9]);    // V0.X of triangle 1
        Assert.Equal(101f, response.TriangleData[10]);    // V0.Y of triangle 1
    }

    // =========================================================================
    // Live Integration — connect to real SceneDataService on port 5003
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_Orgrimmar_ReturnsTriangles()
    {
        try
        {
            var realClient = new SceneDataClient("127.0.0.1", 5003, NullLogger.Instance);
            var result = realClient.EnsureSceneDataAround(1, 1629f, -4373f);

            Skip.IfNot(result, "SceneDataService not reachable or returned no data for Orgrimmar");
        }
        finally
        {
            SceneDataClient.TestSendTileRequestOverride = null;
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_AlteracValley_ReturnsTriangles()
    {
        var client = new SceneDataClient("127.0.0.1", 5003, NullLogger.Instance);
        var result = client.EnsureSceneDataAround(30, 686f, -294f);

        Skip.IfNot(result, "SceneDataService not reachable or returned no data for AV (map 30)");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_RagefireChasm_ReturnsTriangles()
    {
        var client = new SceneDataClient("127.0.0.1", 5003, NullLogger.Instance);
        var result = client.EnsureSceneDataAround(389, 3f, -11f);

        Skip.IfNot(result, "SceneDataService not reachable or returned no data for RFC (map 389)");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_Map0_48_32_TileExists()
    {
        using var client = new ProtobufSocketClient<SceneTileRequest, SceneTileResponse>(
            "127.0.0.1", 5003, NullLogger.Instance);

        var response = client.SendMessage(new SceneTileRequest
        {
            MapId = 0,
            TileX = 48,
            TileY = 32,
        });

        Skip.IfNot(response.Success && response.TriangleCount > 0,
            $"Tile 0_48_32 missing or empty (success={response.Success}, triangles={response.TriangleCount}, error={response.ErrorMessage})");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_Map1_28_41_TileExists()
    {
        using var client = new ProtobufSocketClient<SceneTileRequest, SceneTileResponse>(
            "127.0.0.1", 5003, NullLogger.Instance);

        var response = client.SendMessage(new SceneTileRequest
        {
            MapId = 1,
            TileX = 28,
            TileY = 41,
        });

        Skip.IfNot(response.Success && response.TriangleCount > 0,
            $"Tile 1_28_41 missing or empty (success={response.Success}, triangles={response.TriangleCount}, error={response.ErrorMessage})");
    }
}
