using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup_WithoutVerticalCorrectionUsesHorizontalFallbackInputs()
    {
        Vector3 inputPackedPairVector = new(3.0f, -4.0f, 9.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);

        uint expectedKind = EvaluateWoWGroundedDriverHorizontalCorrection(
            requestedMove,
            selectedPlaneNormal,
            out GroundedDriverHorizontalCorrectionTrace expectedHorizontalTrace);

        _ = EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: 10.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            tailTransformScalar: 0.5f,
            requestedMove,
            selectedPlaneNormal,
            selectedContactNormalZ: 0.6f,
            movementFlags: 0u,
            boundingRadius: 1.25f,
            invokeVerticalCorrection: 0u,
            out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace trace);

        Assert.Equal(GroundedDriverSelectedPlaneTailPreThirdPassSetupKind.UseHorizontalFallbackInputs, trace.DispatchKind);
        Assert.Equal((uint)GroundedDriverHorizontalCorrectionKind.HorizontalEpsilonProjection, expectedKind);
        Assert.Equal(0u, trace.InvokedVerticalCorrection);
        Assert.Equal(0u, trace.RejectedOnTransformMagnitude);
        Assert.Equal(0u, trace.PreparedTailRerankInputs);
        Assert.Equal(inputPackedPairVector.X, trace.OutputPackedPairVector.X, 6);
        Assert.Equal(inputPackedPairVector.Y, trace.OutputPackedPairVector.Y, 6);
        Assert.Equal(inputPackedPairVector.Z, trace.OutputPackedPairVector.Z, 6);
        Assert.Equal(expectedHorizontalTrace.OutputCorrection.X, trace.HorizontalProjectedMove.X, 6);
        Assert.Equal(expectedHorizontalTrace.OutputCorrection.Y, trace.HorizontalProjectedMove.Y, 6);
        Assert.Equal(expectedHorizontalTrace.OutputCorrection.Z, trace.HorizontalProjectedMove.Z, 6);
        Assert.Equal(expectedHorizontalTrace.OutputResolved2D, trace.HorizontalResolved2D, 6);
        Assert.Equal(10.0f, trace.OutputPositionZ, 6);
        Assert.Equal(0.75f, trace.OutputFollowupScalar, 6);
        Assert.Equal(1.25f, trace.OutputScalarFloor, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup_ZeroProjectedMagnitudeRejectsTailRerankInputs()
    {
        Vector3 inputPackedPairVector = new(0.0f, 0.0f, 3.0f);
        Vector3 requestedMove = new(0.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.0f, 1.0f);

        _ = EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: -2.0f,
            followupScalar: 0.5f,
            scalarFloor: 0.0f,
            tailTransformScalar: 0.0f,
            requestedMove,
            selectedPlaneNormal,
            selectedContactNormalZ: 1.0f,
            movementFlags: 0u,
            boundingRadius: 0.0f,
            invokeVerticalCorrection: 1u,
            out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace trace);

        Assert.Equal(GroundedDriverSelectedPlaneTailPreThirdPassSetupKind.UseHorizontalFallbackInputs, trace.DispatchKind);
        Assert.Equal(1u, trace.InvokedVerticalCorrection);
        Assert.Equal(1u, trace.RejectedOnTransformMagnitude);
        Assert.Equal(0u, trace.PreparedTailRerankInputs);
        Assert.Equal(0.0f, trace.HorizontalProjectedMove.X, 6);
        Assert.Equal(0.0f, trace.HorizontalProjectedMove.Y, 6);
        Assert.Equal(0.0f, trace.HorizontalProjectedMove.Z, 6);
        Assert.Equal(0.0f, trace.ProjectedTailDistance, 6);
        Assert.Equal(0.0f, trace.ProjectedTailWorkingVector.X, 6);
        Assert.Equal(0.0f, trace.ProjectedTailWorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.ProjectedTailWorkingVector.Z, 6);
        Assert.Equal(0.0f, trace.CorrectionTransactionTrace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup_NonZeroProjectedMagnitudePreparesTailRerankInputs()
    {
        Vector3 inputPackedPairVector = new(2.0f, -1.0f, 7.0f);
        Vector3 requestedMove = new(0.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.0f, 1.0f);
        Vector3 packedPairHorizontal = new(inputPackedPairVector.X, inputPackedPairVector.Y, 0.0f);

        _ = EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 1.0f,
            selectedPlaneNormal,
            packedPairHorizontal,
            requestedMove,
            inputDistancePointer: 0.4f,
            movementFlags: 0u,
            boundingRadius: 1.25f,
            remainingDistanceBefore: 0.75f,
            inputSweepFraction: 1.0f,
            out GroundedDriverSelectedPlaneCorrectionTransactionTrace expectedCorrectionTrace);

        Vector3 expectedProjectedMove = new(
            expectedCorrectionTrace.OutputCorrection.X + (packedPairHorizontal.X * expectedCorrectionTrace.OutputDistancePointer),
            expectedCorrectionTrace.OutputCorrection.Y + (packedPairHorizontal.Y * expectedCorrectionTrace.OutputDistancePointer),
            expectedCorrectionTrace.OutputCorrection.Z);
        float expectedProjectedDistance = MathF.Sqrt(
            (expectedProjectedMove.X * expectedProjectedMove.X) +
            (expectedProjectedMove.Y * expectedProjectedMove.Y) +
            (expectedProjectedMove.Z * expectedProjectedMove.Z));

        _ = EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: 5.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            tailTransformScalar: 0.4f,
            requestedMove,
            selectedPlaneNormal,
            selectedContactNormalZ: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.25f,
            invokeVerticalCorrection: 1u,
            out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace trace);

        Assert.Equal(GroundedDriverSelectedPlaneTailPreThirdPassSetupKind.UseProjectedTailRerankInputs, trace.DispatchKind);
        Assert.Equal(1u, trace.InvokedVerticalCorrection);
        Assert.Equal(0u, trace.RejectedOnTransformMagnitude);
        Assert.Equal(1u, trace.PreparedTailRerankInputs);
        Assert.Equal(expectedCorrectionTrace.OutputDistancePointer, trace.OutputTailTransformScalar, 6);
        Assert.Equal(expectedProjectedMove.X, trace.HorizontalProjectedMove.X, 6);
        Assert.Equal(expectedProjectedMove.Y, trace.HorizontalProjectedMove.Y, 6);
        Assert.Equal(expectedProjectedMove.Z, trace.HorizontalProjectedMove.Z, 6);
        Assert.Equal(expectedProjectedDistance, trace.HorizontalResolved2D, 6);
        Assert.Equal(expectedProjectedDistance, trace.ProjectedTailDistance, 6);
        Assert.Equal(expectedProjectedMove.X / expectedProjectedDistance, trace.ProjectedTailWorkingVector.X, 6);
        Assert.Equal(expectedProjectedMove.Y / expectedProjectedDistance, trace.ProjectedTailWorkingVector.Y, 6);
        Assert.Equal(expectedProjectedMove.Z / expectedProjectedDistance, trace.ProjectedTailWorkingVector.Z, 6);
        Assert.Equal(inputPackedPairVector.X, trace.OutputPackedPairVector.X, 6);
        Assert.Equal(inputPackedPairVector.Y, trace.OutputPackedPairVector.Y, 6);
        Assert.Equal(inputPackedPairVector.Z, trace.OutputPackedPairVector.Z, 6);
        Assert.Equal(5.0f, trace.OutputPositionZ, 6);
        Assert.Equal(0.75f, trace.OutputFollowupScalar, 6);
        Assert.Equal(1.25f, trace.OutputScalarFloor, 6);
    }
}
