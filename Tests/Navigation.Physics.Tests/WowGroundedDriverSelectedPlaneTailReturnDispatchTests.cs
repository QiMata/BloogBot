using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailReturnDispatchTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch_ThirdPassFailureReturnsZeroWithoutTailRerank()
    {
        Vector3 inputPackedPairVector = new(3.0f, -4.0f, 9.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);
        Vector3 horizontalProjectedMove = new(4.5f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
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
            tailRerankSucceeded: 1u,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 1.0f,
            chooserAcceptedSelectedPlane: 0u,
            movementFlags: 0u,
            out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailReturnDispatchKind.Return0Exit, kind);
        Assert.Equal(GroundedDriverSelectedPlaneTailReturnDispatchKind.Return0Exit, trace.DispatchKind);
        Assert.Equal(0u, trace.CalledTailRerank);
        Assert.Equal(0u, trace.Called635F80);
        Assert.Equal((uint)GroundedDriverSelectedPlanePostFastReturnTailKind.ExitWithoutSelection, (uint)trace.PostFastReturnTailTrace.DispatchKind);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch_TailRerankFailureReturnsZeroAfterPostTailSuccess()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(3.0f, 0.0f, 4.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.6f, 0.8f);
        Vector3 horizontalProjectedMove = new(1.0f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
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
            tailRerankSucceeded: 0u,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 1.0f,
            chooserAcceptedSelectedPlane: 0u,
            movementFlags: 0u,
            out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailReturnDispatchKind.Return0Exit, kind);
        Assert.Equal(1u, trace.CalledTailRerank);
        Assert.Equal(0u, trace.TailRerankSucceeded);
        Assert.Equal(0u, trace.Called635F80);
        Assert.Equal((uint)GroundedDriverSelectedPlanePostFastReturnTailKind.UseSelectedPlaneBlend, (uint)trace.PostFastReturnTailTrace.DispatchKind);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch_WalkableFinalSelectionWithChooserRejectReturnsHorizontalPath()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);
        Vector3 horizontalProjectedMove = new(4.5f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
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
            tailRerankSucceeded: 1u,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.9f,
            chooserAcceptedSelectedPlane: 0u,
            movementFlags: 0u,
            out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailReturnDispatchKind.Return1Horizontal, kind);
        Assert.Equal(1u, trace.CalledTailRerank);
        Assert.Equal(1u, trace.TailRerankSucceeded);
        Assert.Equal(1u, trace.FinalSelectedIndexInRange);
        Assert.Equal(1u, trace.FinalSelectedPlaneWalkable);
        Assert.Equal(1u, trace.Called635F80);
        Assert.Equal(0u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(0u, trace.WroteField80FromSelectedZ);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputProjectedMove.X, trace.OutputProjectedMove.X, 6);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputProjectedMove.Y, trace.OutputProjectedMove.Y, 6);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputProjectedMove.Z, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputResolved2D, trace.OutputResolved2D, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch_ChooserAcceptOrNonWalkableFallsThroughToSelectedPlanePath()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(3.0f, 0.0f, 4.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.6f, 0.8f);
        Vector3 horizontalProjectedMove = new(1.0f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
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
            tailRerankSucceeded: 1u,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.9f,
            chooserAcceptedSelectedPlane: 1u,
            movementFlags: 0u,
            out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailReturnDispatchKind.Return2SelectedPlane, kind);
        Assert.Equal(1u, trace.CalledTailRerank);
        Assert.Equal(1u, trace.TailRerankSucceeded);
        Assert.Equal(1u, trace.FinalSelectedIndexInRange);
        Assert.Equal(1u, trace.FinalSelectedPlaneWalkable);
        Assert.Equal(1u, trace.Called635F80);
        Assert.Equal(1u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(1u, trace.WroteField80FromSelectedZ);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputPositionZ, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch_NonWalkableFinalSelectionSkipsChooserAndReturnsSelectedPlanePath()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(3.0f, 0.0f, 4.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.6f, 0.8f);
        Vector3 horizontalProjectedMove = new(1.0f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
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
            tailRerankSucceeded: 1u,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.2f,
            chooserAcceptedSelectedPlane: 0u,
            movementFlags: 0u,
            out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailReturnDispatchKind.Return2SelectedPlane, kind);
        Assert.Equal(1u, trace.CalledTailRerank);
        Assert.Equal(1u, trace.TailRerankSucceeded);
        Assert.Equal(1u, trace.FinalSelectedIndexInRange);
        Assert.Equal(0u, trace.FinalSelectedPlaneWalkable);
        Assert.Equal(0u, trace.Called635F80);
        Assert.Equal(0u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(1u, trace.WroteField80FromSelectedZ);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputPositionZ, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch_GroundedWallFlagSuppressesField80Writeback()
    {
        Vector3 inputPackedPairVector = new(-2.0f, 5.5f, -1.0f);
        Vector3 requestedMove = new(3.0f, 0.0f, 4.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.6f, 0.8f);
        Vector3 horizontalProjectedMove = new(1.0f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
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
            tailRerankSucceeded: 1u,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.9f,
            chooserAcceptedSelectedPlane: 1u,
            movementFlags: (uint)MoveFlags.SplineElevation,
            out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailReturnDispatchKind.Return2SelectedPlane, kind);
        Assert.Equal(1u, trace.CalledTailRerank);
        Assert.Equal(1u, trace.FinalSelectedPlaneWalkable);
        Assert.Equal(1u, trace.Called635F80);
        Assert.Equal(1u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(1u, trace.GroundedWall04000000Set);
        Assert.Equal(0u, trace.WroteField80FromSelectedZ);
        Assert.Equal(trace.PostFastReturnTailTrace.OutputPositionZ, trace.OutputPositionZ, 6);
    }
}
