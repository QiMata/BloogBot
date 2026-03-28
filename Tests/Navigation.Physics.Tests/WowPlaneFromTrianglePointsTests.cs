using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowPlaneFromTrianglePointsTests
{
    [Fact]
    public void BuildWoWPlaneFromTrianglePoints_UsesBinaryTriangleOrientation()
    {
        bool built = BuildWoWPlaneFromTrianglePoints(
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            out SelectorSupportPlane plane);

        Assert.True(built);
        Assert.Equal(0f, plane.Normal.X, 6);
        Assert.Equal(0f, plane.Normal.Y, 6);
        Assert.Equal(-1f, plane.Normal.Z, 6);
        Assert.Equal(0f, plane.PlaneDistance, 6);
    }

    [Fact]
    public void BuildWoWPlaneFromTrianglePoints_ComputesNegativeDotDistanceFromFirstPoint()
    {
        Vector3 point0 = new(1f, 2f, 5f);

        bool built = BuildWoWPlaneFromTrianglePoints(
            point0,
            new Vector3(2f, 2f, 5f),
            new Vector3(1f, 3f, 5f),
            out SelectorSupportPlane plane);

        Assert.True(built);
        Assert.Equal(0f, plane.Normal.X, 6);
        Assert.Equal(0f, plane.Normal.Y, 6);
        Assert.Equal(-1f, plane.Normal.Z, 6);
        Assert.Equal(5f, plane.PlaneDistance, 6);
    }

    [Fact]
    public void BuildWoWPlaneFromTrianglePoints_DegenerateTriangleFails()
    {
        bool built = BuildWoWPlaneFromTrianglePoints(
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 1f, 1f),
            new Vector3(2f, 2f, 2f),
            out _);

        Assert.False(built);
    }
}
