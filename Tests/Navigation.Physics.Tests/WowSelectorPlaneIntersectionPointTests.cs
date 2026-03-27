using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPlaneIntersectionPointTests
{
    [Fact]
    public void BuildWoWSelectorPlaneIntersectionPoint_OrthogonalPlanes_ReturnsExactIntersection()
    {
        SelectorSupportPlane selected = new()
        {
            Normal = new Vector3(0.0f, 0.0f, 1.0f),
            PlaneDistance = -3.0f,
        };
        SelectorSupportPlane first = new()
        {
            Normal = new Vector3(1.0f, 0.0f, 0.0f),
            PlaneDistance = -1.0f,
        };
        SelectorSupportPlane second = new()
        {
            Normal = new Vector3(0.0f, 1.0f, 0.0f),
            PlaneDistance = -2.0f,
        };

        Assert.True(BuildWoWSelectorPlaneIntersectionPoint(selected, first, second, out Vector3 intersection));
        Assert.Equal(1.0f, intersection.X, 6);
        Assert.Equal(2.0f, intersection.Y, 6);
        Assert.Equal(3.0f, intersection.Z, 6);
    }

    [Fact]
    public void BuildWoWSelectorPlaneIntersectionPoint_ScaledPlaneCoefficients_PreservesIntersection()
    {
        SelectorSupportPlane selected = new()
        {
            Normal = new Vector3(1.0f, 1.0f, 1.0f),
            PlaneDistance = -6.0f,
        };
        SelectorSupportPlane first = new()
        {
            Normal = new Vector3(2.0f, 0.0f, 0.0f),
            PlaneDistance = -2.0f,
        };
        SelectorSupportPlane second = new()
        {
            Normal = new Vector3(0.0f, 3.0f, 0.0f),
            PlaneDistance = -6.0f,
        };

        Assert.True(BuildWoWSelectorPlaneIntersectionPoint(selected, first, second, out Vector3 intersection));
        Assert.Equal(1.0f, intersection.X, 5);
        Assert.Equal(2.0f, intersection.Y, 5);
        Assert.Equal(3.0f, intersection.Z, 5);
    }
}
