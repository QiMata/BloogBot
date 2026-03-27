using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSwimQueryPlaneFlipTests
{
    [Fact]
    public void BuildWoWNegatedPlane_FlipsNormalAndDistance()
    {
        Vector3 normal = new(1.5f, -2.25f, 3.75f);

        bool built = BuildWoWNegatedPlane(normal, -4.5f, out SelectorSupportPlane plane);

        Assert.True(built);
        Assert.Equal(-1.5f, plane.Normal.X, 5);
        Assert.Equal(2.25f, plane.Normal.Y, 5);
        Assert.Equal(-3.75f, plane.Normal.Z, 5);
        Assert.Equal(4.5f, plane.PlaneDistance, 5);
    }

    [Fact]
    public void BuildWoWNegatedPlane_DoubleFlipReturnsOriginalPlane()
    {
        Vector3 normal = new(-0.5f, 0.25f, 1.0f);
        Assert.True(BuildWoWNegatedPlane(normal, 0.75f, out SelectorSupportPlane plane));
        Vector3 flippedNormal = plane.Normal;

        bool rebuilt = BuildWoWNegatedPlane(flippedNormal, plane.PlaneDistance, out SelectorSupportPlane restoredPlane);

        Assert.True(rebuilt);
        Assert.Equal(normal.X, restoredPlane.Normal.X, 5);
        Assert.Equal(normal.Y, restoredPlane.Normal.Y, 5);
        Assert.Equal(normal.Z, restoredPlane.Normal.Z, 5);
        Assert.Equal(0.75f, restoredPlane.PlaneDistance, 5);
    }
}
