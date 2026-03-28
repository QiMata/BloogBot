using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneRetryTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction_WalkableSelectedUsesVerticalScalarWithoutFlagMutation()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
            walkableSelectedContact: 1u,
            gateReturnCode: 0u,
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(-0.25f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 6.0f,
            inputSweepFraction: 0.75f,
            out GroundedDriverSelectedPlaneRetryTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.WalkableSelectedVertical, kind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.WalkableSelectedVertical, trace.BranchKind);
        Assert.Equal(1u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(6.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.75f, trace.OutputSweepFraction, 6);
        Assert.Equal(0.25f, trace.OutputCorrection.Z, 6);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.DirectScalar, trace.DistancePointerTrace.OutputKind);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction_GateZeroExitsWithoutVerticalMutation()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
            walkableSelectedContact: 0u,
            gateReturnCode: 0u,
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(-2.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 8.0f,
            inputSweepFraction: 0.5f,
            out GroundedDriverSelectedPlaneRetryTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.ExitWithoutMutation, kind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.ExitWithoutMutation, trace.BranchKind);
        Assert.Equal(0u, trace.UsesVerticalHelper);
        Assert.Equal(0u, trace.UsesHorizontalHelper);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(8.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.5f, trace.OutputSweepFraction, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction_GateTwoSetsFlagAndRescalesFromPositiveClamp()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
            walkableSelectedContact: 0u,
            gateReturnCode: 2u,
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(-2.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 10.0f,
            inputSweepFraction: 0.5f,
            out GroundedDriverSelectedPlaneRetryTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.VerticalRetry, kind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.VerticalRetry, trace.BranchKind);
        Assert.Equal(1u, trace.SetGroundedWall04000000);
        Assert.Equal(1u, trace.UsesVerticalHelper);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledRemainingDistance);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(0x04000000u, trace.OutputMovementFlags);
        Assert.Equal(5.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.25f, trace.OutputSweepFraction, 6);
        Assert.Equal(0.5f, trace.OutputDistancePointer, 6);
        Assert.Equal(1.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.PositiveRadiusClamp, trace.DistancePointerTrace.OutputKind);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction_GateTwoFlaggedNegativeScalarZeroesDistanceAndBudget()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
            walkableSelectedContact: 0u,
            gateReturnCode: 2u,
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(2.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 9.0f,
            inputSweepFraction: 0.6f,
            out GroundedDriverSelectedPlaneRetryTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.VerticalRetry, kind);
        Assert.Equal(1u, trace.SetGroundedWall04000000);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(0.0f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.0f, trace.OutputSweepFraction, 6);
        Assert.Equal(1.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.FlaggedNegativeZeroDistance, trace.DistancePointerTrace.OutputKind);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction_NonWalkableNonTwoUsesHorizontalCorrectionWithoutMutatingBudgets()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
            walkableSelectedContact: 0u,
            gateReturnCode: 1u,
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.6f, 0.8f, 0.0f),
            inputWorkingVector: new Vector3(3.0f, 4.0f, 9.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.25f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 8.0f,
            inputSweepFraction: 0.5f,
            out GroundedDriverSelectedPlaneRetryTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBranchGateKind.HorizontalRetry, kind);
        Assert.Equal(GroundedDriverSelectedPlaneBranchGateKind.HorizontalRetry, trace.BranchKind);
        Assert.Equal(0u, trace.SetGroundedWall04000000);
        Assert.Equal(0u, trace.UsesVerticalHelper);
        Assert.Equal(1u, trace.UsesHorizontalHelper);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.RescaledRemainingDistance);
        Assert.Equal(0u, trace.RescaledSweepFraction);
        Assert.Equal(8.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.5f, trace.OutputSweepFraction, 6);
        Assert.Equal(1.25f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0006f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0008f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(GroundedDriverHorizontalCorrectionKind.HorizontalEpsilonProjection, trace.HorizontalCorrectionTrace.CorrectionKind);
        Assert.Equal(1u, trace.HorizontalCorrectionTrace.AppliedEpsilonPushout);
    }
}
