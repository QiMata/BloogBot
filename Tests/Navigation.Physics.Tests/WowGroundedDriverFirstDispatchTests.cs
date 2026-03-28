using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverFirstDispatchTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverFirstDispatch_WalkableSelectedUsesDirectVerticalAndIgnoresGate()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverFirstDispatch(
            walkableSelectedContact: 1u,
            gateReturnCode: 0u,
            remainingDistanceBeforeDispatch: 9.0f,
            sweepDistanceBeforeVertical: 6.0f,
            sweepDistanceAfterVertical: 3.0f,
            out GroundedDriverFirstDispatchTrace trace);

        Assert.Equal((uint)GroundedWallBranchKind.WalkableSelectedVertical, dispatchKind);
        Assert.Equal(1u, trace.WalkableSelectedContact);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(1u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(1u, trace.RemainingDistanceRescaled);
        Assert.Equal(4.5f, trace.RemainingDistanceAfterDispatch, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverFirstDispatch_NonWalkableGateZeroExitsWithoutBookkeepingMutation()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverFirstDispatch(
            walkableSelectedContact: 0u,
            gateReturnCode: 0u,
            remainingDistanceBeforeDispatch: 7.0f,
            sweepDistanceBeforeVertical: 5.0f,
            sweepDistanceAfterVertical: 2.0f,
            out GroundedDriverFirstDispatchTrace trace);

        Assert.Equal((uint)GroundedWallBranchKind.None, dispatchKind);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(0u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(0u, trace.RemainingDistanceRescaled);
        Assert.Equal(7.0f, trace.RemainingDistanceAfterDispatch, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverFirstDispatch_NonWalkableNonTwoNonZeroGateFallsIntoHorizontalPath()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverFirstDispatch(
            walkableSelectedContact: 0u,
            gateReturnCode: 5u,
            remainingDistanceBeforeDispatch: 11.0f,
            sweepDistanceBeforeVertical: 8.0f,
            sweepDistanceAfterVertical: 1.0f,
            out GroundedDriverFirstDispatchTrace trace);

        Assert.Equal((uint)GroundedWallBranchKind.Horizontal, dispatchKind);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(0u, trace.UsesVerticalHelper);
        Assert.Equal(1u, trace.UsesHorizontalHelper);
        Assert.Equal(0u, trace.RemainingDistanceRescaled);
        Assert.Equal(11.0f, trace.RemainingDistanceAfterDispatch, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverFirstDispatch_NonWalkableGateTwoSetsFlagAndRescalesRemainingDistance()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverFirstDispatch(
            walkableSelectedContact: 0u,
            gateReturnCode: 2u,
            remainingDistanceBeforeDispatch: 10.0f,
            sweepDistanceBeforeVertical: 4.0f,
            sweepDistanceAfterVertical: 1.5f,
            out GroundedDriverFirstDispatchTrace trace);

        Assert.Equal((uint)GroundedWallBranchKind.NonWalkableVertical, dispatchKind);
        Assert.Equal(1u, trace.SetGroundedWall04000000);
        Assert.Equal(1u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(1u, trace.RemainingDistanceRescaled);
        Assert.Equal(3.75f, trace.RemainingDistanceAfterDispatch, 6);
    }
}
