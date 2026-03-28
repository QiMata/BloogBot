using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlanePostFastReturnTailTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction_ThirdPassFailureSkipsBlendCorrection()
    {
        Vector3 inputPackedPairVector = new(3.0f, -4.0f, 9.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);
        Vector3 horizontalProjectedMove = new(4.5f, 0.0f, 0.0f);

        uint expectedThirdPassKind = EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: 10.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            thirdPassRerankSucceeded: 0u,
            out GroundedDriverSelectedPlaneThirdPassSetupTrace expectedThirdPassTrace);

        uint actualKind = EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            inputPackedPairVector,
            inputPositionZ: 10.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            thirdPassRerankSucceeded: 0u,
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 1.0f,
            horizontalProjectedMove,
            horizontalResolved2D: 4.5f,
            out GroundedDriverSelectedPlanePostFastReturnTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlanePostFastReturnTailKind.ExitWithoutSelection, actualKind);
        Assert.Equal((uint)GroundedDriverSelectedPlaneThirdPassSetupKind.ExitWithoutSelection, expectedThirdPassKind);
        Assert.Equal(0u, trace.InvokedBlendCorrection);
        Assert.Equal(expectedThirdPassTrace.DispatchKind, trace.ThirdPassSetupTrace.DispatchKind);
        Assert.Equal(expectedThirdPassTrace.OutputPositionZ, trace.ThirdPassSetupTrace.OutputPositionZ, 6);
        Assert.Equal(expectedThirdPassTrace.ThirdPassWorkingVector.X, trace.ThirdPassSetupTrace.ThirdPassWorkingVector.X, 6);
        Assert.Equal(expectedThirdPassTrace.ThirdPassWorkingVector.Y, trace.ThirdPassSetupTrace.ThirdPassWorkingVector.Y, 6);
        Assert.Equal(expectedThirdPassTrace.ThirdPassWorkingVector.Z, trace.ThirdPassSetupTrace.ThirdPassWorkingVector.Z, 6);
        Assert.Equal(10.75f, trace.OutputPositionZ, 6);
        Assert.Equal(horizontalProjectedMove.X, trace.OutputProjectedMove.X, 6);
        Assert.Equal(horizontalProjectedMove.Y, trace.OutputProjectedMove.Y, 6);
        Assert.Equal(horizontalProjectedMove.Z, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(4.5f, trace.OutputResolved2D, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction_ThirdPassSuccessUsesHorizontalFallbackWhenBlendRejects()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);
        Vector3 horizontalProjectedMove = new(4.5f, 0.0f, 0.0f);

        _ = EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: -3.0f,
            followupScalar: -0.25f,
            scalarFloor: 0.5f,
            thirdPassRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneThirdPassSetupTrace expectedThirdPassTrace);
        uint expectedBlendKind = EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection(
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 1.0f,
            horizontalProjectedMove,
            horizontalResolved2D: 4.5f,
            out GroundedDriverSelectedPlaneBlendCorrectionTrace expectedBlendTrace);

        uint actualKind = EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            inputPackedPairVector,
            inputPositionZ: -3.0f,
            followupScalar: -0.25f,
            scalarFloor: 0.5f,
            thirdPassRerankSucceeded: 1u,
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 1.0f,
            horizontalProjectedMove,
            horizontalResolved2D: 4.5f,
            out GroundedDriverSelectedPlanePostFastReturnTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlanePostFastReturnTailKind.UseHorizontalFallback, actualKind);
        Assert.Equal((uint)GroundedDriverSelectedPlaneBlendCorrectionKind.UseHorizontalFallback, expectedBlendKind);
        Assert.Equal(1u, trace.InvokedBlendCorrection);
        Assert.Equal(expectedThirdPassTrace.OutputPositionZ, trace.ThirdPassSetupTrace.OutputPositionZ, 6);
        Assert.Equal(expectedBlendTrace.DispatchKind, trace.BlendCorrectionTrace.DispatchKind);
        Assert.Equal(expectedBlendTrace.DiscardedUphillBlend, trace.BlendCorrectionTrace.DiscardedUphillBlend);
        Assert.Equal(expectedBlendTrace.OutputProjectedMove.X, trace.OutputProjectedMove.X, 6);
        Assert.Equal(expectedBlendTrace.OutputProjectedMove.Y, trace.OutputProjectedMove.Y, 6);
        Assert.Equal(expectedBlendTrace.OutputProjectedMove.Z, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(expectedBlendTrace.OutputResolved2D, trace.OutputResolved2D, 6);
        Assert.Equal(-3.25f, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction_ThirdPassSuccessUsesSelectedPlaneBlendWhenAccepted()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(3.0f, 0.0f, 4.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.6f, 0.8f);
        Vector3 horizontalProjectedMove = new(1.0f, 0.0f, 0.0f);

        _ = EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: -3.0f,
            followupScalar: -0.25f,
            scalarFloor: 0.5f,
            thirdPassRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneThirdPassSetupTrace expectedThirdPassTrace);
        uint expectedBlendKind = EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection(
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 0.5f,
            horizontalProjectedMove,
            horizontalResolved2D: 1.0f,
            out GroundedDriverSelectedPlaneBlendCorrectionTrace expectedBlendTrace);

        uint actualKind = EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            inputPackedPairVector,
            inputPositionZ: -3.0f,
            followupScalar: -0.25f,
            scalarFloor: 0.5f,
            thirdPassRerankSucceeded: 1u,
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 0.5f,
            horizontalProjectedMove,
            horizontalResolved2D: 1.0f,
            out GroundedDriverSelectedPlanePostFastReturnTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlanePostFastReturnTailKind.UseSelectedPlaneBlend, actualKind);
        Assert.Equal((uint)GroundedDriverSelectedPlaneBlendCorrectionKind.UseSelectedPlaneBlend, expectedBlendKind);
        Assert.Equal(1u, trace.InvokedBlendCorrection);
        Assert.Equal(expectedThirdPassTrace.DispatchKind, trace.ThirdPassSetupTrace.DispatchKind);
        Assert.Equal(expectedBlendTrace.DispatchKind, trace.BlendCorrectionTrace.DispatchKind);
        Assert.Equal(expectedBlendTrace.AcceptedSelectedPlaneBlend, trace.BlendCorrectionTrace.AcceptedSelectedPlaneBlend);
        Assert.Equal(expectedBlendTrace.OutputProjectedMove.X, trace.OutputProjectedMove.X, 5);
        Assert.Equal(expectedBlendTrace.OutputProjectedMove.Y, trace.OutputProjectedMove.Y, 5);
        Assert.Equal(expectedBlendTrace.OutputProjectedMove.Z, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(expectedBlendTrace.OutputResolved2D, trace.OutputResolved2D, 5);
        Assert.Equal(-3.25f, trace.OutputPositionZ, 6);
    }
}
