using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailProjectedBlendTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction_ThirdPassFailureReturnsExitAfterPreTailSetup()
    {
        Vector3 inputPackedPairVector = new(3.0f, -4.0f, 9.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);

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
            out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace expectedPreTrace);
        uint expectedPostKind = EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            inputPackedPairVector,
            inputPositionZ: 10.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            thirdPassRerankSucceeded: 0u,
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 1.0f,
            expectedPreTrace.HorizontalProjectedMove,
            expectedPreTrace.HorizontalResolved2D,
            out GroundedDriverSelectedPlanePostFastReturnTailTrace expectedPostTrace);

        uint actualKind = EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
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
            thirdPassRerankSucceeded: 0u,
            verticalLimit: 1.0f,
            out GroundedDriverSelectedPlaneTailProjectedBlendTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProjectedBlendKind.ExitWithoutSelection, actualKind);
        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProjectedBlendKind.ExitWithoutSelection, expectedPostKind);
        Assert.Equal(0u, trace.UsedProjectedTailRerankInputs);
        Assert.Equal(expectedPreTrace.DispatchKind, trace.TailPreThirdPassSetupTrace.DispatchKind);
        Assert.Equal(expectedPostTrace.DispatchKind, trace.PostFastReturnTailTrace.DispatchKind);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.X, trace.PostFastReturnTailTrace.HorizontalProjectedMove.X, 6);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.Y, trace.PostFastReturnTailTrace.HorizontalProjectedMove.Y, 6);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.Z, trace.PostFastReturnTailTrace.HorizontalProjectedMove.Z, 6);
        Assert.Equal(expectedPreTrace.HorizontalResolved2D, trace.PostFastReturnTailTrace.HorizontalResolved2D, 6);
        Assert.Equal(expectedPostTrace.OutputPositionZ, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction_WithoutVerticalCorrectionFeedsPrecomputedTailInputsIntoPostTail()
    {
        Vector3 inputPackedPairVector = new(3.0f, -4.0f, 9.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);

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
            out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace expectedPreTrace);
        uint expectedPostKind = EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            inputPackedPairVector,
            inputPositionZ: 10.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            thirdPassRerankSucceeded: 1u,
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 1.0f,
            expectedPreTrace.HorizontalProjectedMove,
            expectedPreTrace.HorizontalResolved2D,
            out GroundedDriverSelectedPlanePostFastReturnTailTrace expectedPostTrace);

        uint actualKind = EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
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
            thirdPassRerankSucceeded: 1u,
            verticalLimit: 1.0f,
            out GroundedDriverSelectedPlaneTailProjectedBlendTrace trace);

        Assert.Equal(expectedPostKind, actualKind);
        Assert.Equal(0u, trace.UsedProjectedTailRerankInputs);
        Assert.Equal(expectedPreTrace.DispatchKind, trace.TailPreThirdPassSetupTrace.DispatchKind);
        Assert.Equal(expectedPostTrace.DispatchKind, trace.PostFastReturnTailTrace.DispatchKind);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.X, trace.PostFastReturnTailTrace.HorizontalProjectedMove.X, 6);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.Y, trace.PostFastReturnTailTrace.HorizontalProjectedMove.Y, 6);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.Z, trace.PostFastReturnTailTrace.HorizontalProjectedMove.Z, 6);
        Assert.Equal(expectedPreTrace.HorizontalResolved2D, trace.PostFastReturnTailTrace.HorizontalResolved2D, 6);
        Assert.Equal(expectedPostTrace.OutputProjectedMove.X, trace.OutputProjectedMove.X, 6);
        Assert.Equal(expectedPostTrace.OutputProjectedMove.Y, trace.OutputProjectedMove.Y, 6);
        Assert.Equal(expectedPostTrace.OutputProjectedMove.Z, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(expectedPostTrace.OutputResolved2D, trace.OutputResolved2D, 6);
        Assert.Equal(expectedPostTrace.OutputPositionZ, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction_ProjectedInputsPreparedFeedPostTailBlend()
    {
        Vector3 inputPackedPairVector = new(2.0f, -1.0f, 7.0f);
        Vector3 requestedMove = new(0.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.0f, 1.0f);

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
            out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace expectedPreTrace);
        uint expectedPostKind = EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            inputPackedPairVector,
            inputPositionZ: 5.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            thirdPassRerankSucceeded: 1u,
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 0.5f,
            expectedPreTrace.HorizontalProjectedMove,
            expectedPreTrace.HorizontalResolved2D,
            out GroundedDriverSelectedPlanePostFastReturnTailTrace expectedPostTrace);

        uint actualKind = EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
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
            thirdPassRerankSucceeded: 1u,
            verticalLimit: 0.5f,
            out GroundedDriverSelectedPlaneTailProjectedBlendTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProjectedBlendKind.UseSelectedPlaneBlend, actualKind);
        Assert.Equal((uint)GroundedDriverSelectedPlaneTailPreThirdPassSetupKind.UseProjectedTailRerankInputs, (uint)expectedPreTrace.DispatchKind);
        Assert.Equal(1u, trace.UsedProjectedTailRerankInputs);
        Assert.Equal(expectedPostKind, actualKind);
        Assert.Equal(expectedPreTrace.DispatchKind, trace.TailPreThirdPassSetupTrace.DispatchKind);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.X, trace.PostFastReturnTailTrace.HorizontalProjectedMove.X, 6);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.Y, trace.PostFastReturnTailTrace.HorizontalProjectedMove.Y, 6);
        Assert.Equal(expectedPreTrace.HorizontalProjectedMove.Z, trace.PostFastReturnTailTrace.HorizontalProjectedMove.Z, 6);
        Assert.Equal(expectedPreTrace.HorizontalResolved2D, trace.PostFastReturnTailTrace.HorizontalResolved2D, 6);
        Assert.Equal(expectedPostTrace.DispatchKind, trace.PostFastReturnTailTrace.DispatchKind);
        Assert.Equal(expectedPostTrace.OutputProjectedMove.X, trace.OutputProjectedMove.X, 6);
        Assert.Equal(expectedPostTrace.OutputProjectedMove.Y, trace.OutputProjectedMove.Y, 6);
        Assert.Equal(expectedPostTrace.OutputProjectedMove.Z, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(expectedPostTrace.OutputResolved2D, trace.OutputResolved2D, 6);
        Assert.Equal(expectedPostTrace.OutputPositionZ, trace.OutputPositionZ, 6);
    }
}
