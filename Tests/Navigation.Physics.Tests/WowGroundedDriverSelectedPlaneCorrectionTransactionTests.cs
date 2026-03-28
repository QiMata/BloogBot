using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneCorrectionTransactionTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction_UsesDirectScalarWithoutRescale()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
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
            out GroundedDriverSelectedPlaneCorrectionTransactionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionTransactionKind.DirectScalar, correctionKind);
        Assert.Equal(GroundedDriverSelectedPlaneCorrectionTransactionKind.DirectScalar, trace.CorrectionKind);
        Assert.Equal(1u, trace.WroteVerticalOnlyCorrection);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.RescaledRemainingDistance);
        Assert.Equal(0u, trace.RescaledSweepFraction);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.DirectScalar, trace.DistancePointerTrace.OutputKind);
        Assert.Equal(0.25f, trace.OutputCorrection.Z, 6);
        Assert.Equal(6.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.75f, trace.OutputSweepFraction, 6);
        Assert.Equal(1.0f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction_RescalesBudgetFromPositiveClamp()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
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
            out GroundedDriverSelectedPlaneCorrectionTransactionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionTransactionKind.PositiveRadiusClamp, correctionKind);
        Assert.Equal(GroundedDriverSelectedPlaneCorrectionTransactionKind.PositiveRadiusClamp, trace.CorrectionKind);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledRemainingDistance);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.PositiveRadiusClamp, trace.DistancePointerTrace.OutputKind);
        Assert.Equal(1.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(5.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.25f, trace.OutputSweepFraction, 6);
        Assert.Equal(0.5f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction_RescalesBudgetFromNegativeClamp()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
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
            out GroundedDriverSelectedPlaneCorrectionTransactionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionTransactionKind.NegativeRadiusClamp, correctionKind);
        Assert.Equal(GroundedDriverSelectedPlaneCorrectionTransactionKind.NegativeRadiusClamp, trace.CorrectionKind);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledRemainingDistance);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.NegativeRadiusClamp, trace.DistancePointerTrace.OutputKind);
        Assert.Equal(-1.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(4.5f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.3f, trace.OutputSweepFraction, 6);
        Assert.Equal(0.5f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction_FlaggedNegativeScalarZeroesBudget()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(2.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0x04000000u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 9.0f,
            inputSweepFraction: 0.6f,
            out GroundedDriverSelectedPlaneCorrectionTransactionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionTransactionKind.FlaggedNegativeZeroDistance, correctionKind);
        Assert.Equal(GroundedDriverSelectedPlaneCorrectionTransactionKind.FlaggedNegativeZeroDistance, trace.CorrectionKind);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledRemainingDistance);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.FlaggedNegativeZeroDistance, trace.DistancePointerTrace.OutputKind);
        Assert.Equal(1.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(0.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.0f, trace.OutputSweepFraction, 6);
        Assert.Equal(0.0f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction_SuppressesRescaleForDegenerateInputDistancePointer()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(2.0f, 0.0f, 0.00000001f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 0.0000001f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            remainingDistanceBefore: 5.0f,
            inputSweepFraction: 0.25f,
            out GroundedDriverSelectedPlaneCorrectionTransactionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionTransactionKind.NegativeRadiusClamp, correctionKind);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.NegativeRadiusClamp, trace.DistancePointerTrace.OutputKind);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.RescaledRemainingDistance);
        Assert.Equal(0u, trace.RescaledSweepFraction);
        Assert.Equal(-1.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(5.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.25f, trace.OutputSweepFraction, 6);
    }
}
