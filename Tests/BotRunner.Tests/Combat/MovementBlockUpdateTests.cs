using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;

namespace BotRunner.Tests.Combat;

public class MovementBlockUpdateCloneTests
{
    [Fact]
    public void Clone_CopiesSpeeds()
    {
        var original = new MovementBlockUpdate
        {
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
            TurnRate = 3.14159f
        };

        var clone = original.Clone();

        Assert.Equal(2.5f, clone.WalkSpeed);
        Assert.Equal(7.0f, clone.RunSpeed);
        Assert.Equal(4.5f, clone.RunBackSpeed);
        Assert.Equal(4.722f, clone.SwimSpeed);
        Assert.Equal(2.5f, clone.SwimBackSpeed);
        Assert.Equal(3.14159f, clone.TurnRate);
    }

    [Fact]
    public void Clone_CopiesSplineData()
    {
        var original = new MovementBlockUpdate
        {
            SplineFlags = SplineFlags.Flying,
            SplineFinalPoint = new Position(100, 200, 300),
            SplineTargetGuid = 12345UL,
            SplineFinalOrientation = 1.5f,
            SplineTimePassed = 5000,
            SplineDuration = 10000,
            SplineId = 42,
            SplineFinalDestination = new Position(400, 500, 600)
        };

        var clone = original.Clone();

        Assert.Equal(SplineFlags.Flying, clone.SplineFlags);
        Assert.NotNull(clone.SplineFinalPoint);
        Assert.Equal(100f, clone.SplineFinalPoint!.X);
        Assert.Equal(12345UL, clone.SplineTargetGuid);
        Assert.Equal(1.5f, clone.SplineFinalOrientation);
        Assert.Equal(5000, clone.SplineTimePassed);
        Assert.Equal(10000, clone.SplineDuration);
        Assert.Equal(42u, clone.SplineId);
        Assert.NotNull(clone.SplineFinalDestination);
        Assert.Equal(400f, clone.SplineFinalDestination!.X);
    }

    [Fact]
    public void Clone_DeepCopiesSplineNodes()
    {
        var original = new MovementBlockUpdate
        {
            SplineNodes = [new Position(1, 2, 3), new Position(4, 5, 6)]
        };

        var clone = original.Clone();

        Assert.NotNull(clone.SplineNodes);
        Assert.Equal(2, clone.SplineNodes!.Count);
        Assert.Equal(1f, clone.SplineNodes[0].X);
        Assert.Equal(4f, clone.SplineNodes[1].X);

        // Verify independence — modifying clone's list doesn't affect original
        clone.SplineNodes.Add(new Position(7, 8, 9));
        Assert.Equal(2, original.SplineNodes.Count);
    }

    [Fact]
    public void Clone_NullSplineNodes_StaysNull()
    {
        var original = new MovementBlockUpdate { SplineNodes = null };

        var clone = original.Clone();

        Assert.Null(clone.SplineNodes);
    }

    [Fact]
    public void Clone_IsNewInstance()
    {
        var original = new MovementBlockUpdate { RunSpeed = 7.0f };
        var clone = original.Clone();

        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Clone_SpeedIndependence()
    {
        var original = new MovementBlockUpdate { RunSpeed = 7.0f };
        var clone = original.Clone();

        clone.RunSpeed = 14.0f;
        Assert.Equal(7.0f, original.RunSpeed);
    }

    [Fact]
    public void Clone_NullSplineFields()
    {
        var original = new MovementBlockUpdate
        {
            SplineFlags = null,
            SplineFinalPoint = null,
            SplineTargetGuid = null,
            SplineFinalOrientation = null,
            SplineTimePassed = null,
            SplineDuration = null,
            SplineId = null,
            SplineFinalDestination = null
        };

        var clone = original.Clone();

        Assert.Null(clone.SplineFlags);
        Assert.Null(clone.SplineFinalPoint);
        Assert.Null(clone.SplineTargetGuid);
        Assert.Null(clone.SplineFinalOrientation);
        Assert.Null(clone.SplineTimePassed);
        Assert.Null(clone.SplineDuration);
        Assert.Null(clone.SplineId);
        Assert.Null(clone.SplineFinalDestination);
    }

    [Fact]
    public void DefaultValues()
    {
        var block = new MovementBlockUpdate();

        Assert.Equal(0f, block.WalkSpeed);
        Assert.Equal(0f, block.RunSpeed);
        Assert.Equal(0f, block.RunBackSpeed);
        Assert.Equal(0f, block.SwimSpeed);
        Assert.Equal(0f, block.SwimBackSpeed);
        Assert.Equal(0f, block.TurnRate);
        Assert.Null(block.SplineFlags);
        Assert.Null(block.SplineFinalPoint);
        Assert.Null(block.SplineTargetGuid);
        Assert.NotNull(block.SplineNodes);
        Assert.Empty(block.SplineNodes!);
        Assert.NotNull(block.SplinePoints);
        Assert.Empty(block.SplinePoints);
        Assert.Equal(default, block.SplineType);
        Assert.Equal(0UL, block.FacingTargetGuid);
        Assert.Equal(0f, block.FacingAngle);
    }
}

public class MovementInfoUpdateCloneTests
{
    [Fact]
    public void Clone_CopiesBasicFields()
    {
        var original = new MovementInfoUpdate
        {
            Guid = 12345UL,
            MovementFlags = MovementFlags.MOVEFLAG_FORWARD,
            LastUpdated = 100,
            X = 1.0f,
            Y = 2.0f,
            Z = 3.0f,
            Facing = 1.5f,
            FallTime = 500
        };

        var clone = original.Clone();

        Assert.Equal(12345UL, clone.Guid);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, clone.MovementFlags);
        Assert.Equal(100u, clone.LastUpdated);
        Assert.Equal(1.0f, clone.X);
        Assert.Equal(2.0f, clone.Y);
        Assert.Equal(3.0f, clone.Z);
        Assert.Equal(1.5f, clone.Facing);
        Assert.Equal(500u, clone.FallTime);
    }

    [Fact]
    public void Clone_CopiesTransportData()
    {
        var original = new MovementInfoUpdate
        {
            TransportGuid = 99UL,
            TransportOffset = new Position(10, 20, 30),
            TransportOrientation = 0.5f,
            TransportLastUpdated = 200
        };

        var clone = original.Clone();

        Assert.Equal(99UL, clone.TransportGuid);
        Assert.NotNull(clone.TransportOffset);
        Assert.Equal(10f, clone.TransportOffset!.X);
        Assert.Equal(0.5f, clone.TransportOrientation);
        Assert.Equal(200u, clone.TransportLastUpdated);
    }

    [Fact]
    public void Clone_CopiesJumpData()
    {
        var original = new MovementInfoUpdate
        {
            JumpVerticalSpeed = 7.5f,
            JumpSinAngle = 0.5f,
            JumpCosAngle = 0.866f,
            JumpHorizontalSpeed = 5.0f
        };

        var clone = original.Clone();

        Assert.Equal(7.5f, clone.JumpVerticalSpeed);
        Assert.Equal(0.5f, clone.JumpSinAngle);
        Assert.Equal(0.866f, clone.JumpCosAngle);
        Assert.Equal(5.0f, clone.JumpHorizontalSpeed);
    }

    [Fact]
    public void Clone_CopiesSwimAndSplineElevation()
    {
        var original = new MovementInfoUpdate
        {
            SwimPitch = 0.3f,
            SplineElevation = 100f
        };

        var clone = original.Clone();

        Assert.Equal(0.3f, clone.SwimPitch);
        Assert.Equal(100f, clone.SplineElevation);
    }

    [Fact]
    public void Clone_IsNewInstance()
    {
        var original = new MovementInfoUpdate { X = 1.0f };
        var clone = original.Clone();

        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Clone_FieldIndependence()
    {
        var original = new MovementInfoUpdate { X = 1.0f, Y = 2.0f };
        var clone = original.Clone();

        clone.X = 99.0f;
        Assert.Equal(1.0f, original.X);
    }

    [Fact]
    public void Clone_DoesNotDeepCopyMovementBlockUpdate()
    {
        // Clone does a shallow copy of MovementBlockUpdate — just verify it's present
        var blockUpdate = new MovementBlockUpdate { RunSpeed = 7.0f };
        var original = new MovementInfoUpdate { MovementBlockUpdate = blockUpdate };

        var clone = original.Clone();

        Assert.NotNull(clone.MovementBlockUpdate);
        Assert.Same(original.MovementBlockUpdate, clone.MovementBlockUpdate);
    }
}

public class MovementInfoUpdateComputedPropertyTests
{
    [Fact]
    public void HasTransport_FalseByDefault()
    {
        var info = new MovementInfoUpdate();
        Assert.False(info.HasTransport);
    }

    [Fact]
    public void HasTransport_TrueWhenFlagSet()
    {
        var info = new MovementInfoUpdate { MovementFlags = MovementFlags.MOVEFLAG_ONTRANSPORT };
        Assert.True(info.HasTransport);
    }

    [Fact]
    public void IsSwimming_FalseByDefault()
    {
        var info = new MovementInfoUpdate();
        Assert.False(info.IsSwimming);
    }

    [Fact]
    public void IsSwimming_TrueWhenFlagSet()
    {
        var info = new MovementInfoUpdate { MovementFlags = MovementFlags.MOVEFLAG_SWIMMING };
        Assert.True(info.IsSwimming);
    }

    [Fact]
    public void IsFalling_FalseByDefault()
    {
        var info = new MovementInfoUpdate();
        Assert.False(info.IsFalling);
    }

    [Fact]
    public void IsFalling_TrueWhenJumpingFlagSet()
    {
        var info = new MovementInfoUpdate { MovementFlags = MovementFlags.MOVEFLAG_JUMPING };
        Assert.True(info.IsFalling);
    }

    [Fact]
    public void HasSplineElevation_FalseByDefault()
    {
        var info = new MovementInfoUpdate();
        Assert.False(info.HasSplineElevation);
    }

    [Fact]
    public void HasSplineElevation_TrueWhenFlagSet()
    {
        var info = new MovementInfoUpdate { MovementFlags = MovementFlags.MOVEFLAG_SPLINE_ELEVATION };
        Assert.True(info.HasSplineElevation);
    }

    [Fact]
    public void HasSpline_FalseByDefault()
    {
        var info = new MovementInfoUpdate();
        Assert.False(info.HasSpline);
    }

    [Fact]
    public void HasSpline_TrueWhenFlagSet()
    {
        var info = new MovementInfoUpdate { MovementFlags = MovementFlags.MOVEFLAG_SPLINE_ENABLED };
        Assert.True(info.HasSpline);
    }

    [Fact]
    public void MultipleFlags_AllComputedCorrectly()
    {
        var info = new MovementInfoUpdate
        {
            MovementFlags = MovementFlags.MOVEFLAG_SWIMMING | MovementFlags.MOVEFLAG_JUMPING
        };

        Assert.True(info.IsSwimming);
        Assert.True(info.IsFalling);
        Assert.False(info.HasTransport);
        Assert.False(info.HasSpline);
    }

    [Fact]
    public void DefaultValues()
    {
        var info = new MovementInfoUpdate();

        Assert.Equal(0UL, info.Guid);
        Assert.Equal(0UL, info.TargetGuid);
        Assert.Equal(0u, info.HighGuid);
        Assert.Equal(0u, info.UpdateAll);
        Assert.Equal(0u, info.MovementCounter);
        Assert.Equal(default, info.MovementFlags);
        Assert.Equal(0f, info.X);
        Assert.Equal(0f, info.Y);
        Assert.Equal(0f, info.Z);
        Assert.Equal(0f, info.Facing);
        Assert.Equal(0u, info.FallTime);
        Assert.Null(info.TransportGuid);
        Assert.Null(info.SwimPitch);
        Assert.Null(info.JumpVerticalSpeed);
        Assert.Null(info.SplineElevation);
        Assert.Null(info.MovementBlockUpdate);
    }
}
