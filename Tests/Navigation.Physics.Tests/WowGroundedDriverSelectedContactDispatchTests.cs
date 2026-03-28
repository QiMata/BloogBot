using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedContactDispatchTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedContactDispatch_WalkableSelectedBypassesNonWalkablePathAndRescalesRemainingDistance()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedContactDispatch(
            checkWalkableAccepted: 1u,
            consumedSelectedState: 0u,
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.OnTransport),
            remainingDistanceBeforeDispatch: 9.0f,
            sweepDistanceBeforeVertical: 6.0f,
            sweepDistanceAfterVertical: 3.0f,
            inputFallTime: 77u,
            inputFallStartZ: 4.0f,
            inputVerticalSpeed: -2.5f,
            positionZ: 12.0f,
            out GroundedDriverSelectedContactDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedContactDispatchKind.WalkableSelectedVertical, dispatchKind);
        Assert.Equal(1u, trace.BypassedNonWalkableDispatch);
        Assert.Equal(0u, trace.DelegatedToNonWalkableDispatch);
        Assert.Equal(0u, trace.StartedFallWithZeroVelocity);
        Assert.Equal(4.5f, trace.RemainingDistanceAfterDispatch, 6);
        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.OnTransport), trace.OutputMovementFlags);
        Assert.Equal(77u, trace.OutputFallTime);
        Assert.Equal(4.0f, trace.OutputFallStartZ, 6);
        Assert.Equal(-2.5f, trace.OutputVerticalSpeed, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedContactDispatch_UnconsumedNonWalkableDelegatesToNextDispatch()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedContactDispatch(
            checkWalkableAccepted: 0u,
            consumedSelectedState: 0u,
            movementFlags: (uint)(MoveFlags.Backward | MoveFlags.Swimming),
            remainingDistanceBeforeDispatch: 7.0f,
            sweepDistanceBeforeVertical: 5.0f,
            sweepDistanceAfterVertical: 2.0f,
            inputFallTime: 19u,
            inputFallStartZ: 8.5f,
            inputVerticalSpeed: -1.0f,
            positionZ: 14.0f,
            out GroundedDriverSelectedContactDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedContactDispatchKind.DelegateToNonWalkableDispatch, dispatchKind);
        Assert.Equal(0u, trace.BypassedNonWalkableDispatch);
        Assert.Equal(1u, trace.DelegatedToNonWalkableDispatch);
        Assert.Equal(0u, trace.StartedFallWithZeroVelocity);
        Assert.Equal(7.0f, trace.RemainingDistanceAfterDispatch, 6);
        Assert.Equal((uint)(MoveFlags.Backward | MoveFlags.Swimming), trace.OutputMovementFlags);
        Assert.Equal(19u, trace.OutputFallTime);
        Assert.Equal(8.5f, trace.OutputFallStartZ, 6);
        Assert.Equal(-1.0f, trace.OutputVerticalSpeed, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedContactDispatch_ConsumedNonWalkableStartsFallAndDropsChosenPair()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedContactDispatch(
            checkWalkableAccepted: 0u,
            consumedSelectedState: 1u,
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.SplineElevation | MoveFlags.Swimming),
            remainingDistanceBeforeDispatch: 11.0f,
            sweepDistanceBeforeVertical: 8.0f,
            sweepDistanceAfterVertical: 4.0f,
            inputFallTime: 31u,
            inputFallStartZ: 6.5f,
            inputVerticalSpeed: -7.0f,
            positionZ: 42.25f,
            out GroundedDriverSelectedContactDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedContactDispatchKind.StartFallZero, dispatchKind);
        Assert.Equal(1u, trace.StartedFallWithZeroVelocity);
        Assert.Equal(1u, trace.ClearedSplineElevation04000000);
        Assert.Equal(1u, trace.ClearedSwimming00200000);
        Assert.Equal(1u, trace.SetJumping);
        Assert.Equal(1u, trace.ResetFallTime);
        Assert.Equal(1u, trace.ResetFallStartZ);
        Assert.Equal(1u, trace.ResetVerticalSpeed);
        Assert.Equal(1u, trace.DroppedChosenPair);
        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.Jumping), trace.OutputMovementFlags);
        Assert.Equal(0u, trace.OutputFallTime);
        Assert.Equal(42.25f, trace.OutputFallStartZ, 6);
        Assert.Equal(0.0f, trace.OutputVerticalSpeed, 6);
    }
}
