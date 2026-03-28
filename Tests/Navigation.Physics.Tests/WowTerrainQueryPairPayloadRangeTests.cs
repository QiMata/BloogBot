using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryPairPayloadRangeTests
{
    [Fact]
    public void AppendTerrainQueryPairPayloadRange_FillsOnlyNewlyAppendedSlots()
    {
        TerrainQueryPairPayload[] inputPairs =
        [
            new TerrainQueryPairPayload { First = 1.0f, Second = 2.0f },
            new TerrainQueryPairPayload { First = 3.0f, Second = 4.0f },
        ];
        TerrainQueryPairPayload payload = new() { First = 9.0f, Second = 10.0f };
        TerrainQueryPairPayload[] outputPairs = new TerrainQueryPairPayload[5];

        int count = AppendWoWTerrainQueryPairPayloadRange(
            inputPairs,
            inputPairs.Length,
            previousRecordCount: 2u,
            currentRecordCount: 5u,
            payload,
            outputPairs,
            outputPairs.Length);

        Assert.Equal(5, count);
        AssertPair(1.0f, 2.0f, outputPairs[0]);
        AssertPair(3.0f, 4.0f, outputPairs[1]);
        AssertPair(9.0f, 10.0f, outputPairs[2]);
        AssertPair(9.0f, 10.0f, outputPairs[3]);
        AssertPair(9.0f, 10.0f, outputPairs[4]);
    }

    [Fact]
    public void AppendTerrainQueryPairPayloadRange_WithoutGrowth_PreservesExistingPairs()
    {
        TerrainQueryPairPayload[] inputPairs =
        [
            new TerrainQueryPairPayload { First = 11.0f, Second = 12.0f },
            new TerrainQueryPairPayload { First = 13.0f, Second = 14.0f },
            new TerrainQueryPairPayload { First = 15.0f, Second = 16.0f },
        ];
        TerrainQueryPairPayload payload = new() { First = 99.0f, Second = 100.0f };
        TerrainQueryPairPayload[] outputPairs = new TerrainQueryPairPayload[3];

        int count = AppendWoWTerrainQueryPairPayloadRange(
            inputPairs,
            inputPairs.Length,
            previousRecordCount: 3u,
            currentRecordCount: 3u,
            payload,
            outputPairs,
            outputPairs.Length);

        Assert.Equal(3, count);
        AssertPair(11.0f, 12.0f, outputPairs[0]);
        AssertPair(13.0f, 14.0f, outputPairs[1]);
        AssertPair(15.0f, 16.0f, outputPairs[2]);
    }

    private static void AssertPair(float first, float second, TerrainQueryPairPayload actual)
    {
        Assert.Equal(first, actual.First, 6);
        Assert.Equal(second, actual.Second, 6);
    }
}
