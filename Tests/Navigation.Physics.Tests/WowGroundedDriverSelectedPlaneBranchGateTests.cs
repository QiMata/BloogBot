using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneBranchGateTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneBranchGate_WalkableSelectedBypassesGateAndUsesVerticalCorrection()
    {
        uint branchKind = EvaluateWoWGroundedDriverSelectedPlaneBranchGate(
            walkableSelectedContact: 1u,
            gateReturnCode: 0u,
            requestedMove: new Vector3(3.0f, 4.0f, 5.0f),
            selectedPlaneNormal: new Vector3(0.6f, 0.8f, 0.0f),
            verticalLimit: 8.0f,
            remainingDistanceBefore: 12.0f,
            sweepDistanceBefore: 6.0f,
            sweepDistanceAfter: 3.0f,
            inputSweepFraction: 0.75f,
            inputDistancePointer: 9.0f,
            out GroundedDriverSelectedPlaneBranchGateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.WalkableSelectedVertical, branchKind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.WalkableSelectedVertical, trace.BranchKind);
        Assert.Equal(1u, trace.WalkableSelectedContact);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(1u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(1u, trace.RemainingDistanceRescaled);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(6.0f, trace.InputSweepDistance, 6);
        Assert.Equal(3.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(12.0f, trace.InputRemainingDistance, 6);
        Assert.Equal(6.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.375f, trace.OutputSweepFraction, 6);
        Assert.Equal(4.5f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Y, 6);
        Assert.Equal(5.0f, trace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneBranchGate_NonWalkableGateZeroExitsWithoutBookkeepingMutation()
    {
        uint branchKind = EvaluateWoWGroundedDriverSelectedPlaneBranchGate(
            walkableSelectedContact: 0u,
            gateReturnCode: 0u,
            requestedMove: new Vector3(2.0f, 1.0f, 6.0f),
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            verticalLimit: 10.0f,
            remainingDistanceBefore: 7.0f,
            sweepDistanceBefore: 5.0f,
            sweepDistanceAfter: 2.0f,
            inputSweepFraction: 0.9f,
            inputDistancePointer: 3.25f,
            out GroundedDriverSelectedPlaneBranchGateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.ExitWithoutMutation, branchKind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.ExitWithoutMutation, trace.BranchKind);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(0u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(0u, trace.RemainingDistanceRescaled);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.RescaledSweepFraction);
        Assert.Equal(7.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(5.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0.9f, trace.OutputSweepFraction, 6);
        Assert.Equal(3.25f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneBranchGate_NonWalkableGateTwoSetsGroundedFlagAndUsesVerticalRetry()
    {
        uint branchKind = EvaluateWoWGroundedDriverSelectedPlaneBranchGate(
            walkableSelectedContact: 0u,
            gateReturnCode: 2u,
            requestedMove: new Vector3(2.0f, 0.0f, 5.0f),
            selectedPlaneNormal: new Vector3(0.6f, 0.0f, 0.8f),
            verticalLimit: 0.5f,
            remainingDistanceBefore: 10.0f,
            sweepDistanceBefore: 8.0f,
            sweepDistanceAfter: 4.0f,
            inputSweepFraction: 0.5f,
            inputDistancePointer: 2.0f,
            out GroundedDriverSelectedPlaneBranchGateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.VerticalRetry, branchKind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.VerticalRetry, trace.BranchKind);
        Assert.Equal(1u, trace.SetGroundedWall04000000);
        Assert.Equal(1u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(1u, trace.RemainingDistanceRescaled);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(0.5f, trace.OutputCorrection.Z, 6);
        Assert.Equal(5.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(4.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0.25f, trace.OutputSweepFraction, 6);
        Assert.Equal(1.0f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneBranchGate_NonWalkableNonTwoUsesHorizontalCorrectionWithoutRescale()
    {
        uint branchKind = EvaluateWoWGroundedDriverSelectedPlaneBranchGate(
            walkableSelectedContact: 0u,
            gateReturnCode: 5u,
            requestedMove: new Vector3(3.0f, 4.0f, 9.0f),
            selectedPlaneNormal: new Vector3(0.6f, 0.8f, 0.0f),
            verticalLimit: 2.0f,
            remainingDistanceBefore: 11.0f,
            sweepDistanceBefore: 6.0f,
            sweepDistanceAfter: 3.0f,
            inputSweepFraction: 0.4f,
            inputDistancePointer: 1.75f,
            out GroundedDriverSelectedPlaneBranchGateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.HorizontalRetry, branchKind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.HorizontalRetry, trace.BranchKind);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(0u, trace.UsesVerticalHelper);
        Assert.Equal(1u, trace.UsesHorizontalHelper);
        Assert.Equal(0u, trace.RemainingDistanceRescaled);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.RescaledSweepFraction);
        Assert.Equal(11.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(6.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0.4f, trace.OutputSweepFraction, 6);
        Assert.Equal(1.75f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0006f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0008f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(GroundedDriverHorizontalCorrectionKind.HorizontalEpsilonProjection, trace.HorizontalCorrectionTrace.CorrectionKind);
        Assert.Equal(1u, trace.HorizontalCorrectionTrace.AppliedEpsilonPushout);
    }
}
