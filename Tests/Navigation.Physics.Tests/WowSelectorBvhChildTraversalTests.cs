using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorBvhChildTraversalTests
{
    [Fact]
    public void BuildWoWSelectorBvhChildTraversal_LeafNodeReturnsFalse()
    {
        SelectorBvhNodeRecord node = new()
        {
            ControlWord = 0x4,
            LowChildIndex = 7,
            HighChildIndex = 11,
            SplitCoordinate = 3.5f,
        };

        Assert.False(BuildWoWSelectorBvhChildTraversal(
            node,
            new Vector3(-2.0f, -1.0f, -3.0f),
            new Vector3(6.0f, 5.0f, 9.0f),
            out _));
    }

    [Fact]
    public void BuildWoWSelectorBvhChildTraversal_WhollyLowSlabVisitsOnlyLowChildAndClipsMaxAlongSplitAxis()
    {
        SelectorBvhNodeRecord node = new()
        {
            ControlWord = 0x0,
            LowChildIndex = 3,
            HighChildIndex = 9,
            SplitCoordinate = 4.0f,
        };

        Assert.True(BuildWoWSelectorBvhChildTraversal(
            node,
            new Vector3(-2.0f, 1.0f, -3.0f),
            new Vector3(1.5f, 6.0f, 8.0f),
            out SelectorBvhChildTraversal traversal));

        Assert.Equal(0u, traversal.Axis);
        Assert.Equal(4.0f, traversal.SplitCoordinate, 6);
        Assert.Equal(3u, traversal.LowChildIndex);
        Assert.Equal(9u, traversal.HighChildIndex);
        Assert.Equal(1u, traversal.VisitLow);
        Assert.Equal(0u, traversal.VisitHigh);
        AssertVector(new Vector3(-2.0f, 1.0f, -3.0f), traversal.LowBoundsMin);
        AssertVector(new Vector3(1.5f, 6.0f, 8.0f), traversal.LowBoundsMax);
        AssertVector(new Vector3(-2.0f, 1.0f, -3.0f), traversal.HighBoundsMin);
        AssertVector(new Vector3(1.5f, 6.0f, 8.0f), traversal.HighBoundsMax);
    }

    [Fact]
    public void BuildWoWSelectorBvhChildTraversal_WhollyHighSlabVisitsOnlyHighChildAndClipsMinAlongSplitAxis()
    {
        SelectorBvhNodeRecord node = new()
        {
            ControlWord = 0x1,
            LowChildIndex = 5,
            HighChildIndex = 8,
            SplitCoordinate = -1.25f,
        };

        Assert.True(BuildWoWSelectorBvhChildTraversal(
            node,
            new Vector3(-3.0f, 2.0f, -4.0f),
            new Vector3(7.0f, 9.0f, 6.0f),
            out SelectorBvhChildTraversal traversal));

        Assert.Equal(1u, traversal.Axis);
        Assert.Equal(-1.25f, traversal.SplitCoordinate, 6);
        Assert.Equal(5u, traversal.LowChildIndex);
        Assert.Equal(8u, traversal.HighChildIndex);
        Assert.Equal(0u, traversal.VisitLow);
        Assert.Equal(1u, traversal.VisitHigh);
        AssertVector(new Vector3(-3.0f, 2.0f, -4.0f), traversal.LowBoundsMin);
        AssertVector(new Vector3(7.0f, 9.0f, 6.0f), traversal.LowBoundsMax);
        AssertVector(new Vector3(-3.0f, 2.0f, -4.0f), traversal.HighBoundsMin);
        AssertVector(new Vector3(7.0f, 9.0f, 6.0f), traversal.HighBoundsMax);
    }

    [Fact]
    public void BuildWoWSelectorBvhChildTraversal_StraddlingSlabVisitsBothChildrenAndClipsEachSideToSplitPlane()
    {
        SelectorBvhNodeRecord node = new()
        {
            ControlWord = 0x2,
            LowChildIndex = 10,
            HighChildIndex = 12,
            SplitCoordinate = 0.75f,
        };

        Assert.True(BuildWoWSelectorBvhChildTraversal(
            node,
            new Vector3(-5.0f, -1.0f, -2.0f),
            new Vector3(4.0f, 3.0f, 5.0f),
            out SelectorBvhChildTraversal traversal));

        Assert.Equal(2u, traversal.Axis);
        Assert.Equal(0.75f, traversal.SplitCoordinate, 6);
        Assert.Equal(10u, traversal.LowChildIndex);
        Assert.Equal(12u, traversal.HighChildIndex);
        Assert.Equal(1u, traversal.VisitLow);
        Assert.Equal(1u, traversal.VisitHigh);
        AssertVector(new Vector3(-5.0f, -1.0f, -2.0f), traversal.LowBoundsMin);
        AssertVector(new Vector3(4.0f, 3.0f, 0.75f), traversal.LowBoundsMax);
        AssertVector(new Vector3(-5.0f, -1.0f, 0.75f), traversal.HighBoundsMin);
        AssertVector(new Vector3(4.0f, 3.0f, 5.0f), traversal.HighBoundsMax);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
