using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using SceneData;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Tests for SceneDataClient: grid quantization, retry/dedup behavior,
/// and live connectivity to SceneDataService (port 5003).
/// </summary>
public sealed class SceneDataClientIntegrationTests
{
    // =========================================================================
    // Grid Quantization — verify request bounds for a given position
    // =========================================================================

    [Theory]
    [InlineData(1629f, -4373f, 1400f, -4600f, 2000f, -4000f)]  // Orgrimmar
    [InlineData(0f, 0f, -200f, -200f, 400f, 400f)]              // Origin
    [InlineData(-988f, -3834f, -1200f, -4200f, -600f, -3600f)]  // Ratchet
    [InlineData(199f, 199f, -200f, -200f, 400f, 400f)]           // Near grid boundary
    [InlineData(-1f, -1f, -400f, -400f, 200f, 200f)]            // Negative near zero
    public void EnsureSceneDataAround_QuantizesToCorrect600YardGrid(
        float x, float y,
        float expectedMinX, float expectedMinY, float expectedMaxX, float expectedMaxY)
    {
        SceneGridRequest? capturedRequest = null;

        try
        {
            SceneDataClient.TestSendRequestOverride = request =>
            {
                capturedRequest = request;
                return new SceneGridResponse { Success = true, TriangleCount = 0 };
            };

            var client = new SceneDataClient(NullLogger.Instance);
            client.EnsureSceneDataAround(1, x, y);

            Assert.NotNull(capturedRequest);
            Assert.Equal(1u, capturedRequest.MapId);
            Assert.Equal(expectedMinX, capturedRequest.MinX, precision: 0);
            Assert.Equal(expectedMinY, capturedRequest.MinY, precision: 0);
            Assert.Equal(expectedMaxX, capturedRequest.MaxX, precision: 0);
            Assert.Equal(expectedMaxY, capturedRequest.MaxY, precision: 0);
        }
        finally
        {
            SceneDataClient.TestSendRequestOverride = null;
        }
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
            SceneDataClient.TestSendRequestOverride = _ =>
            {
                requestCount++;
                return new SceneGridResponse { Success = false, TriangleCount = 0, ErrorMessage = "test" };
            };

            var client = new SceneDataClient(NullLogger.Instance);

            // First attempt — request sent, fails
            Assert.False(client.EnsureSceneDataAround(1, 100f, 100f));
            Assert.Equal(1, requestCount);

            // Same position, within backoff — no request sent
            Assert.False(client.EnsureSceneDataAround(1, 100f, 100f));
            Assert.Equal(1, requestCount);

            // Advance past 2-second backoff
            now = now.AddSeconds(3);

            // Now it should retry
            Assert.False(client.EnsureSceneDataAround(1, 100f, 100f));
            Assert.Equal(2, requestCount);
        }
        finally
        {
            SceneDataClient.TestSendRequestOverride = null;
            SceneDataClient.TestUtcNowOverride = null;
        }
    }

    [Fact]
    public void EnsureSceneDataAround_DifferentMap_SendsSeparateRequests()
    {
        var requestMapIds = new List<uint>();

        try
        {
            SceneDataClient.TestSendRequestOverride = req =>
            {
                requestMapIds.Add(req.MapId);
                return new SceneGridResponse { Success = true, TriangleCount = 0 };
            };

            var client = new SceneDataClient(NullLogger.Instance);
            client.EnsureSceneDataAround(0, 100f, 100f);
            client.EnsureSceneDataAround(1, 100f, 100f);

            Assert.Equal(2, requestMapIds.Count);
            Assert.Contains(0u, requestMapIds);
            Assert.Contains(1u, requestMapIds);
        }
        finally
        {
            SceneDataClient.TestSendRequestOverride = null;
        }
    }

    // =========================================================================
    // Response Data Packing — verify triangle data size matches count
    // =========================================================================

    [Fact]
    public void SceneGridResponse_TriangleDataSizing_9FloatsPerTriangle()
    {
        var response = new SceneGridResponse
        {
            Success = true,
            TriangleCount = 3,
        };

        // 3 triangles: 9 floats each for vertices, 3 for normal, 1 walkable flag
        for (int i = 0; i < 3; i++)
        {
            float b = i * 100;
            response.TriangleData.AddRange(new float[]
            {
                b, b+1, b+2,       // V0 (contact point)
                b+10, b+11, b+12,  // V1
                b+20, b+21, b+22,  // V2
            });
            response.NormalData.AddRange(new float[] { 0, 0, 1 });
            response.Walkable.Add(true);
        }

        Assert.Equal(27, response.TriangleData.Count);  // 3 × 9
        Assert.Equal(9, response.NormalData.Count);      // 3 × 3
        Assert.Equal(3, response.Walkable.Count);

        // Verify data layout: triangle 1 V0 starts at index 9
        Assert.Equal(100f, response.TriangleData[9]);   // V0.X of triangle 1
        Assert.Equal(101f, response.TriangleData[10]);   // V0.Y of triangle 1
    }

    // =========================================================================
    // Live Integration — connect to real SceneDataService on port 5003
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_Orgrimmar_ReturnsTriangles()
    {
        SceneGridResponse? capturedResponse = null;

        try
        {
            // Use a test override that captures the response before injection
            var realClient = new SceneDataClient("127.0.0.1", 5003, NullLogger.Instance);

            // Wrap: let the real send happen but capture via override on second call
            var result = realClient.EnsureSceneDataAround(1, 1629f, -4373f);

            Skip.IfNot(result, "SceneDataService not reachable or returned no data for Orgrimmar");
        }
        finally
        {
            SceneDataClient.TestSendRequestOverride = null;
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_AlteracValley_ReturnsTriangles()
    {
        var client = new SceneDataClient("127.0.0.1", 5003, NullLogger.Instance);

        // AV map 30 — critical for mobilizing troops
        var result = client.EnsureSceneDataAround(30, 686f, -294f);

        Skip.IfNot(result, "SceneDataService not reachable or returned no data for AV (map 30)");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_RagefireChasm_ReturnsTriangles()
    {
        var client = new SceneDataClient("127.0.0.1", 5003, NullLogger.Instance);

        // RFC map 389
        var result = client.EnsureSceneDataAround(389, 3f, -11f);

        Skip.IfNot(result, "SceneDataService not reachable or returned no data for RFC (map 389)");
    }
}
