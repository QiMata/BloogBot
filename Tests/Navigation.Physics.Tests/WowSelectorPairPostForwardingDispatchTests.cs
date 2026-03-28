using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPairPostForwardingDispatchTests
{
    [Fact]
    public void EvaluateWoWSelectorPairPostForwardingDispatch_ReturnCodeZeroSkipsAdjustmentAndUsesNonStatefulPath()
    {
        Vector3 move = new(0.5f, 0.25f, -0.125f);

        uint dispatchKind = EvaluateWoWSelectorPairPostForwardingDispatch(
            pairForwardReturnCode: 0,
            directStateBit: 0u,
            alternateUnitZStateBit: 0u,
            windowSpanScalar: 3.0f,
            windowStartScalar: 1.0f,
            moveVector: move,
            horizontalReferenceMagnitude: 5.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f,
            out SelectorPairPostForwardingTrace trace);

        Assert.Equal((uint)SelectorPairPostForwardingDispatchKind.NonStateful, dispatchKind);
        Assert.Equal(0u, trace.UsedWindowAdjustment);
        Assert.Equal(0u, trace.OutputMagnitudeWritten);
        Assert.Equal(3.0f, trace.OutputWindowScalar, 6);
        Assert.Equal(move.X, trace.OutputMove.X, 6);
        Assert.Equal(move.Y, trace.OutputMove.Y, 6);
        Assert.Equal(move.Z, trace.OutputMove.Z, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorPairPostForwardingDispatch_ReturnCodeOneUsesAdjustmentAndAlternateStateWinsDispatchOrder()
    {
        uint dispatchKind = EvaluateWoWSelectorPairPostForwardingDispatch(
            pairForwardReturnCode: 1,
            directStateBit: 1u,
            alternateUnitZStateBit: 1u,
            windowSpanScalar: 1.0f,
            windowStartScalar: 0.2f,
            moveVector: new Vector3(1.0f, 0.0f, 1.0f),
            horizontalReferenceMagnitude: 10.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: -8.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 0.0f,
            positionZ: 0.0f,
            out SelectorPairPostForwardingTrace trace);

        Assert.Equal((uint)SelectorPairPostForwardingDispatchKind.AlternateUnitZ, dispatchKind);
        Assert.Equal(1u, trace.UsedWindowAdjustment);
        Assert.Equal(1u, trace.OutputMagnitudeWritten);
        Assert.Equal(0.0f, trace.OutputWindowScalar, 6);
        Assert.Equal(0.0f, trace.OutputMove.X, 6);
        Assert.Equal(0.0f, trace.OutputMove.Y, 6);
        Assert.Equal(0.0f, trace.OutputMove.Z, 6);
        Assert.Equal(0.0f, trace.OutputMoveMagnitude, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorPairPostForwardingDispatch_ReturnCodeOneWithDirectStateKeepsWindowSpanWhenAdjustmentDoesNotClamp()
    {
        uint dispatchKind = EvaluateWoWSelectorPairPostForwardingDispatch(
            pairForwardReturnCode: 1,
            directStateBit: 1u,
            alternateUnitZStateBit: 0u,
            windowSpanScalar: 0.25f,
            windowStartScalar: 0.1f,
            moveVector: new Vector3(3.0f, 4.0f, 0.0f),
            horizontalReferenceMagnitude: 5.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f,
            out SelectorPairPostForwardingTrace trace);

        Assert.Equal((uint)SelectorPairPostForwardingDispatchKind.Direct, dispatchKind);
        Assert.Equal(1u, trace.UsedWindowAdjustment);
        Assert.Equal(0u, trace.OutputMagnitudeWritten);
        Assert.Equal(0.25f, trace.OutputWindowScalar, 6);
        Assert.Equal(3.0f, trace.OutputMove.X, 6);
        Assert.Equal(4.0f, trace.OutputMove.Y, 6);
        Assert.Equal(0.0f, trace.OutputMove.Z, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorPairPostForwardingDispatch_ReturnCodeTwoUsesFailureBranch()
    {
        uint dispatchKind = EvaluateWoWSelectorPairPostForwardingDispatch(
            pairForwardReturnCode: 2,
            directStateBit: 1u,
            alternateUnitZStateBit: 1u,
            windowSpanScalar: 3.0f,
            windowStartScalar: 1.0f,
            moveVector: new Vector3(9.0f, 8.0f, 7.0f),
            horizontalReferenceMagnitude: 5.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f,
            out SelectorPairPostForwardingTrace trace);

        Assert.Equal((uint)SelectorPairPostForwardingDispatchKind.Failure, dispatchKind);
        Assert.Equal(0u, trace.UsedWindowAdjustment);
        Assert.Equal(3.0f, trace.OutputWindowScalar, 6);
        Assert.Equal(9.0f, trace.OutputMove.X, 6);
        Assert.Equal(8.0f, trace.OutputMove.Y, 6);
        Assert.Equal(7.0f, trace.OutputMove.Z, 6);
    }
}
