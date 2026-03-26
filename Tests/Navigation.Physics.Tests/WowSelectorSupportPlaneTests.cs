using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSupportPlaneTests
{
    [Fact]
    public void BuildSelectorSupportPlanes_AxisPlanesMatchBinaryFormulas()
    {
        var planes = BuildPlanes(new Vector3(4f, 5f, 6f), verticalOffset: 2f, horizontalRadius: 0.25f);

        Assert.Equal(9, planes.Length);
        AssertPlane(planes[0], -1f, 0f, 0f, 3.75f);
        AssertPlane(planes[1], 1f, 0f, 0f, -4.25f);
        AssertPlane(planes[2], 0f, 1f, 0f, -5.25f);
        AssertPlane(planes[3], 0f, -1f, 0f, 4.75f);
        AssertPlane(planes[4], 0f, 0f, 1f, -8f);
    }

    [Fact]
    public void BuildSelectorSupportPlanes_DiagonalPlanesMatchBinaryConstants()
    {
        const float diagonalX = 0.8796418905f;
        const float diagonalZ = 0.4756366014f;
        var position = new Vector3(4f, 5f, 6f);
        var planes = BuildPlanes(position, verticalOffset: 2f, horizontalRadius: 0.25f);

        AssertPlane(planes[5], -diagonalX, 0f, -diagonalZ, (position.X * diagonalX) + (position.Z * diagonalZ));
        AssertPlane(planes[6], diagonalX, 0f, -diagonalZ, (position.Z * diagonalZ) - (position.X * diagonalX));
        AssertPlane(planes[7], 0f, diagonalX, -diagonalZ, (position.Z * diagonalZ) - (position.Y * diagonalX));
        AssertPlane(planes[8], 0f, -diagonalX, -diagonalZ, (position.Y * diagonalX) + (position.Z * diagonalZ));
    }

    private static SelectorSupportPlane[] BuildPlanes(Vector3 position, float verticalOffset, float horizontalRadius)
    {
        var planes = new SelectorSupportPlane[9];
        int count = BuildWoWSelectorSupportPlanes(position, verticalOffset, horizontalRadius, planes, planes.Length);
        Assert.Equal(9, count);
        return planes;
    }

    private static void AssertPlane(SelectorSupportPlane plane, float nx, float ny, float nz, float distance)
    {
        Assert.Equal(nx, plane.Normal.X, 5);
        Assert.Equal(ny, plane.Normal.Y, 5);
        Assert.Equal(nz, plane.Normal.Z, 5);
        Assert.Equal(distance, plane.PlaneDistance, 5);
    }
}
