using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowAabbContainmentTests
{
    [Fact]
    public void PointInsideAabbInclusive_AcceptsPointsOnBothBounds()
    {
        Vector3 min = new(1f, 2f, 3f);
        Vector3 max = new(4f, 5f, 6f);

        Assert.True(EvaluateWoWPointInsideAabbInclusive(min, max, min));
        Assert.True(EvaluateWoWPointInsideAabbInclusive(min, max, max));
    }

    [Fact]
    public void PointInsideAabbInclusive_RejectsPointBelowMin()
    {
        Vector3 min = new(1f, 2f, 3f);
        Vector3 max = new(4f, 5f, 6f);
        Vector3 point = new(0.999f, 4f, 5f);

        bool inside = EvaluateWoWPointInsideAabbInclusive(min, max, point);

        Assert.False(inside);
    }

    [Fact]
    public void PointInsideAabbInclusive_RejectsPointAboveMax()
    {
        Vector3 min = new(1f, 2f, 3f);
        Vector3 max = new(4f, 5f, 6f);
        Vector3 point = new(2f, 5.001f, 5f);

        bool inside = EvaluateWoWPointInsideAabbInclusive(min, max, point);

        Assert.False(inside);
    }
}
