using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorNeighborhoodTests
{
    [Fact]
    public void BuildSelectorNeighborhood_PointsMatchBinaryLayout()
    {
        var position = new Vector3(10f, 20f, 30f);
        BuildNeighborhood(position, verticalOffset: 2f, horizontalRadius: 0.25f, out var points, out _);

        Assert.Equal(position, points[0]);
        Assert.Equal(new Vector3(9.75f, 19.75f, 29.75f), points[1]);
        Assert.Equal(new Vector3(9.75f, 20.25f, 29.75f), points[2]);
        Assert.Equal(new Vector3(10.25f, 20.25f, 29.75f), points[3]);
        Assert.Equal(new Vector3(10.25f, 19.75f, 29.75f), points[4]);
        Assert.Equal(new Vector3(9.75f, 19.75f, 32f), points[5]);
        Assert.Equal(new Vector3(9.75f, 20.25f, 32f), points[6]);
        Assert.Equal(new Vector3(10.25f, 20.25f, 32f), points[7]);
        Assert.Equal(new Vector3(10.25f, 19.75f, 32f), points[8]);
    }

    [Fact]
    public void BuildSelectorNeighborhood_SelectorTableMatchesBinaryBytes()
    {
        BuildNeighborhood(new Vector3(10f, 20f, 30f), verticalOffset: 2f, horizontalRadius: 0.25f, out _, out var selectorIndices);

        byte[] expected = [
            1, 2, 6, 5,
            3, 4, 8, 7,
            2, 3, 7, 6,
            4, 1, 5, 8,
            5, 6, 7, 8,
            0, 1, 2, 0,
            3, 4, 0, 2,
            3, 0, 4, 1
        ];

        Assert.Equal(expected, selectorIndices);
    }

    private static void BuildNeighborhood(Vector3 position, float verticalOffset, float horizontalRadius, out Vector3[] outPoints, out byte[] outSelectorIndices)
    {
        outPoints = new Vector3[9];
        outSelectorIndices = new byte[32];
        bool ok = BuildWoWSelectorNeighborhood(position, verticalOffset, horizontalRadius, outPoints, outPoints.Length, outSelectorIndices, outSelectorIndices.Length);
        Assert.True(ok);
    }
}
