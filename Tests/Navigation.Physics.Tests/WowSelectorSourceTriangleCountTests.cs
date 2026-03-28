using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceTriangleCountTests
{
    [Fact]
    public void CountWoWSelectorSourceTrianglesPassingPlaneOutcodes_UsesFixedBinarySampleAndTriangleTables()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = 0f,
            },
        ];

        Vector3[] points = new Vector3[19];
        points[0] = new Vector3(-0.03f, 0f, 0f);
        points[1] = new Vector3(0f, 0f, 0f);
        points[9] = new Vector3(-0.03f, 0f, 0f);
        points[17] = new Vector3(-0.03f, 0f, 0f);
        points[18] = new Vector3(0f, 0f, 0f);

        uint count = CountWoWSelectorSourceTrianglesPassingPlaneOutcodes(
            planes,
            planes.Length,
            points,
            points.Length);

        Assert.Equal(3u, count);
    }

    [Fact]
    public void CountWoWSelectorSourceTrianglesPassingPlaneOutcodes_WhenAllSampledPointsReject_ReturnsZero()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = 0f,
            },
        ];

        Vector3[] points = new Vector3[19];
        points[0] = new Vector3(-0.03f, 0f, 0f);
        points[1] = new Vector3(-0.03f, 0f, 0f);
        points[9] = new Vector3(-0.03f, 0f, 0f);
        points[17] = new Vector3(-0.03f, 0f, 0f);
        points[18] = new Vector3(-0.03f, 0f, 0f);

        uint count = CountWoWSelectorSourceTrianglesPassingPlaneOutcodes(
            planes,
            planes.Length,
            points,
            points.Length);

        Assert.Equal(0u, count);
    }
}
