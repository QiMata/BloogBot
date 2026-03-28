using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailPostForwardingTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding_ReturnCode2SkipsJoinAndLeavesStateHandlersIdle()
    {
        Vector3 moveVector = new(3.0f, 4.0f, 5.0f);
        Vector3 startPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 cachedPosition = new(1.0f, 2.0f, 3.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding(
            pairForwardReturnCode: 2,
            directStateBit: 1u,
            alternateUnitZStateBit: 1u,
            windowSpanScalar: 0.2f,
            windowStartScalar: 0.05f,
            moveVector,
            horizontalReferenceMagnitude: 4.0f,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: 6.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 8.0f,
            positionZ: 9.0f,
            startPosition,
            elapsedScalar: 0.15f,
            facing: 1.25f,
            pitch: -0.5f,
            cachedPosition,
            cachedFacing: 0.75f,
            cachedPitch: 0.125f,
            cachedMoveTimestamp: 77u,
            cachedScalar84: 0.6f,
            recomputedScalar84: 0.9f,
            out GroundedDriverSelectedPlaneTailPostForwardingTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailPostForwardingKind.Return2LateBranch, kind);
        Assert.Equal(0u, trace.AppliedMoveToPosition);
        Assert.Equal(0u, trace.AdvancedElapsedScalar);
        Assert.Equal(0u, trace.InvokedAlternateUnitZStateHandler);
        Assert.Equal(0u, trace.InvokedDirectStateHandler);
        Assert.Equal(startPosition.X, trace.OutputPosition.X, 5);
        Assert.Equal(startPosition.Y, trace.OutputPosition.Y, 5);
        Assert.Equal(startPosition.Z, trace.OutputPosition.Z, 5);
        Assert.Equal(0.15f, trace.OutputElapsedScalar, 5);
        Assert.Equal(SelectorPairPostForwardingDispatchKind.Failure, trace.PostForwardingTrace.DispatchKind);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding_AlternateUnitZDispatchUsesUpdatedPositionZ()
    {
        Vector3 moveVector = new(1.0f, -2.0f, 3.5f);
        Vector3 startPosition = new(4.0f, 5.0f, 6.0f);
        Vector3 cachedPosition = new(7.0f, 8.0f, 9.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding(
            pairForwardReturnCode: 0,
            directStateBit: 0u,
            alternateUnitZStateBit: 1u,
            windowSpanScalar: 0.25f,
            windowStartScalar: 0.1f,
            moveVector,
            horizontalReferenceMagnitude: 2.0f,
            movementFlags: (uint)MoveFlags.Forward,
            verticalSpeed: 1.0f,
            horizontalSpeedScale: 1.0f,
            referenceZ: 12.0f,
            positionZ: 6.0f,
            startPosition,
            elapsedScalar: 0.4f,
            facing: 0.0f,
            pitch: 0.0f,
            cachedPosition,
            cachedFacing: 0.0f,
            cachedPitch: 0.0f,
            cachedMoveTimestamp: 0u,
            cachedScalar84: 0.0f,
            recomputedScalar84: 0.0f,
            out GroundedDriverSelectedPlaneTailPostForwardingTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailPostForwardingKind.AlternateUnitZ, kind);
        Assert.Equal(1u, trace.AppliedMoveToPosition);
        Assert.Equal(1u, trace.AdvancedElapsedScalar);
        Assert.Equal(1u, trace.InvokedAlternateUnitZStateHandler);
        Assert.Equal(0u, trace.InvokedDirectStateHandler);
        Assert.Equal(startPosition.X + moveVector.X, trace.OutputPosition.X, 5);
        Assert.Equal(startPosition.Y + moveVector.Y, trace.OutputPosition.Y, 5);
        Assert.Equal(startPosition.Z + moveVector.Z, trace.OutputPosition.Z, 5);
        Assert.Equal(0.65f, trace.OutputElapsedScalar, 5);
        Assert.Equal(trace.OutputPosition.Z, trace.AlternateUnitZStateTrace.OutputFallStartZ, 5);
        Assert.True((((MoveFlags)trace.AlternateUnitZStateTrace.OutputMovementFlags) & MoveFlags.FallingFar) != 0);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding_DirectDispatchUsesUpdatedPositionForCachedWriteback()
    {
        Vector3 moveVector = new(-1.5f, 2.25f, -0.75f);
        Vector3 startPosition = new(20.0f, 30.0f, 40.0f);
        Vector3 cachedPosition = new(1.0f, 1.0f, 1.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding(
            pairForwardReturnCode: 0,
            directStateBit: 1u,
            alternateUnitZStateBit: 0u,
            windowSpanScalar: 0.3f,
            windowStartScalar: 0.05f,
            moveVector,
            horizontalReferenceMagnitude: 5.0f,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: 3.0f,
            horizontalSpeedScale: 1.0f,
            referenceZ: 0.0f,
            positionZ: 40.0f,
            startPosition,
            elapsedScalar: 0.1f,
            facing: 1.75f,
            pitch: -0.25f,
            cachedPosition,
            cachedFacing: 0.5f,
            cachedPitch: 0.125f,
            cachedMoveTimestamp: 123u,
            cachedScalar84: 0.4f,
            recomputedScalar84: 0.9f,
            out GroundedDriverSelectedPlaneTailPostForwardingTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailPostForwardingKind.DirectState, kind);
        Assert.Equal(1u, trace.InvokedDirectStateHandler);
        Assert.Equal(0u, trace.InvokedAlternateUnitZStateHandler);
        Assert.Equal(startPosition.X + moveVector.X, trace.OutputPosition.X, 5);
        Assert.Equal(startPosition.Y + moveVector.Y, trace.OutputPosition.Y, 5);
        Assert.Equal(startPosition.Z + moveVector.Z, trace.OutputPosition.Z, 5);
        Assert.Equal(trace.OutputPosition.X, trace.DirectStateTrace.OutputCachedPosition.X, 5);
        Assert.Equal(trace.OutputPosition.Y, trace.DirectStateTrace.OutputCachedPosition.Y, 5);
        Assert.Equal(trace.OutputPosition.Z, trace.DirectStateTrace.OutputCachedPosition.Z, 5);
        Assert.Equal(0u, trace.DirectStateTrace.OutputMoveTimestamp);
        Assert.Equal(0.9f, trace.DirectStateTrace.OutputScalar84, 5);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding_NonStatefulContinueStillAccumulatesMoveAndElapsedScalar()
    {
        Vector3 moveVector = new(0.5f, 1.5f, -2.5f);
        Vector3 startPosition = new(-4.0f, -5.0f, -6.0f);
        Vector3 cachedPosition = new(0.0f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding(
            pairForwardReturnCode: 0,
            directStateBit: 0u,
            alternateUnitZStateBit: 0u,
            windowSpanScalar: 0.125f,
            windowStartScalar: 0.0f,
            moveVector,
            horizontalReferenceMagnitude: 1.0f,
            movementFlags: 0u,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 0.0f,
            referenceZ: 0.0f,
            positionZ: -6.0f,
            startPosition,
            elapsedScalar: 0.2f,
            facing: 0.0f,
            pitch: 0.0f,
            cachedPosition,
            cachedFacing: 0.0f,
            cachedPitch: 0.0f,
            cachedMoveTimestamp: 0u,
            cachedScalar84: 0.0f,
            recomputedScalar84: 0.0f,
            out GroundedDriverSelectedPlaneTailPostForwardingTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailPostForwardingKind.NonStatefulContinue, kind);
        Assert.Equal(1u, trace.AppliedMoveToPosition);
        Assert.Equal(1u, trace.AdvancedElapsedScalar);
        Assert.Equal(0u, trace.InvokedAlternateUnitZStateHandler);
        Assert.Equal(0u, trace.InvokedDirectStateHandler);
        Assert.Equal(startPosition.X + moveVector.X, trace.OutputPosition.X, 5);
        Assert.Equal(startPosition.Y + moveVector.Y, trace.OutputPosition.Y, 5);
        Assert.Equal(startPosition.Z + moveVector.Z, trace.OutputPosition.Z, 5);
        Assert.Equal(0.325f, trace.OutputElapsedScalar, 5);
        Assert.Equal(SelectorPairPostForwardingDispatchKind.NonStateful, trace.PostForwardingTrace.DispatchKind);
    }
}
