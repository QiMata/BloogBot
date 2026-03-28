using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowPlaneFromNormalAndPointTests
{
    [Fact]
    public void BuildWoWPlaneFromNormalAndPoint_CopiesNormalAndComputesNegativeDotDistance()
    {
        Vector3 normal = new(2f, -3f, 4f);
        Vector3 point = new(5f, -6f, 7f);

        bool built = BuildWoWPlaneFromNormalAndPoint(normal, point, out SelectorSupportPlane plane);

        Assert.True(built);
        Assert.Equal(2f, plane.Normal.X, 5);
        Assert.Equal(-3f, plane.Normal.Y, 5);
        Assert.Equal(4f, plane.Normal.Z, 5);
        Assert.Equal(-56f, plane.PlaneDistance, 5);
    }

    [Fact]
    public void BuildWoWPlaneFromNormalAndPoint_PlacesInputPointOnPlaneWithoutRenormalizing()
    {
        Vector3 normal = new(-0.5f, 0.25f, 1.5f);
        Vector3 point = new(3f, 4f, -2f);

        bool built = BuildWoWPlaneFromNormalAndPoint(normal, point, out SelectorSupportPlane plane);
        float signedDistance = Vector3.Dot(plane.Normal, point) + plane.PlaneDistance;

        Assert.True(built);
        Assert.Equal(normal.X, plane.Normal.X, 5);
        Assert.Equal(normal.Y, plane.Normal.Y, 5);
        Assert.Equal(normal.Z, plane.Normal.Z, 5);
        Assert.Equal(0f, signedDistance, 5);
    }
}
