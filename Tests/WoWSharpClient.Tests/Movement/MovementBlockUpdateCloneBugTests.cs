using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Tests that expose missing fields in MovementBlockUpdate.Clone() and MovementInfoUpdate.Clone().
/// These require InternalsVisibleTo access because the properties have internal setters.
/// </summary>
public class MovementBlockUpdateCloneBugTests
{
    [Fact]
    public void Clone_MissesSplineType()
    {
        // BUG: Clone() doesn't copy SplineType
        var original = new MovementBlockUpdate();
        original.SplineType = SplineType.FacingTarget;

        var clone = original.Clone();

        Assert.Equal(SplineType.FacingTarget, clone.SplineType);
    }

    [Fact]
    public void Clone_MissesFacingTargetGuid()
    {
        // BUG: Clone() doesn't copy FacingTargetGuid
        var original = new MovementBlockUpdate();
        original.FacingTargetGuid = 99999UL;

        var clone = original.Clone();

        Assert.Equal(99999UL, clone.FacingTargetGuid);
    }

    [Fact]
    public void Clone_MissesFacingAngle()
    {
        // BUG: Clone() doesn't copy FacingAngle
        var original = new MovementBlockUpdate();
        original.FacingAngle = 2.5f;

        var clone = original.Clone();

        Assert.Equal(2.5f, clone.FacingAngle);
    }

    [Fact]
    public void Clone_MissesSplineTimestamp()
    {
        // BUG: Clone() doesn't copy SplineTimestamp
        var original = new MovementBlockUpdate();
        original.SplineTimestamp = 12345u;

        var clone = original.Clone();

        Assert.Equal(12345u, clone.SplineTimestamp);
    }

    [Fact]
    public void Clone_MissesSplinePoints()
    {
        // BUG: Clone() doesn't copy SplinePoints (separate from SplineNodes)
        var original = new MovementBlockUpdate();
        original.SplinePoints = [new Position(10, 20, 30)];

        var clone = original.Clone();

        Assert.NotNull(clone.SplinePoints);
        Assert.Single(clone.SplinePoints);
        Assert.Equal(10f, clone.SplinePoints[0].X);
    }

    [Fact]
    public void Clone_MissesHighGuid()
    {
        // BUG: Clone() doesn't copy HighGuid
        var original = new MovementBlockUpdate();
        original.HighGuid = 42u;

        var clone = original.Clone();

        Assert.Equal(42u, clone.HighGuid);
    }

    [Fact]
    public void Clone_MissesUpdateAll()
    {
        // BUG: Clone() doesn't copy UpdateAll
        var original = new MovementBlockUpdate();
        original.UpdateAll = 1u;

        var clone = original.Clone();

        Assert.Equal(1u, clone.UpdateAll);
    }

    [Fact]
    public void Clone_MissesTargetGuid()
    {
        // BUG: Clone() doesn't copy TargetGuid
        var original = new MovementBlockUpdate();
        original.TargetGuid = 55555UL;

        var clone = original.Clone();

        Assert.Equal(55555UL, clone.TargetGuid);
    }

    [Fact]
    public void Clone_MissesFacingSpot()
    {
        // BUG: Clone() doesn't copy FacingSpot
        var original = new MovementBlockUpdate();
        original.FacingSpot = new Position(1, 2, 3);

        var clone = original.Clone();

        Assert.Equal(1f, clone.FacingSpot.X);
        Assert.Equal(2f, clone.FacingSpot.Y);
        Assert.Equal(3f, clone.FacingSpot.Z);
    }
}

public class MovementInfoUpdateCloneBugTests
{
    [Fact]
    public void Clone_MissesMovementCounter()
    {
        // BUG: Clone() doesn't copy MovementCounter
        var original = new MovementInfoUpdate { MovementCounter = 42 };

        var clone = original.Clone();

        Assert.Equal(42u, clone.MovementCounter);
    }

    [Fact]
    public void Clone_MissesTargetGuid()
    {
        // BUG: Clone() doesn't copy TargetGuid
        var original = new MovementInfoUpdate { TargetGuid = 77777UL };

        var clone = original.Clone();

        Assert.Equal(77777UL, clone.TargetGuid);
    }

    [Fact]
    public void Clone_MissesHighGuid()
    {
        // BUG: Clone() doesn't copy HighGuid
        var original = new MovementInfoUpdate { HighGuid = 5u };

        var clone = original.Clone();

        Assert.Equal(5u, clone.HighGuid);
    }

    [Fact]
    public void Clone_MissesUpdateAll()
    {
        // BUG: Clone() doesn't copy UpdateAll
        var original = new MovementInfoUpdate { UpdateAll = 1u };

        var clone = original.Clone();

        Assert.Equal(1u, clone.UpdateAll);
    }
}
