using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailChooserContractTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract_WalkableChooserRejectReturnsHorizontalAndTracksMutation()
    {
        Vector3 chooserInputPackedPairVector = new(2.0f, -1.0f, 0.0f);
        Vector3 chooserInputProjectedMove = new(1.0f, 2.0f, 3.0f);
        Vector3 chooserOutputProjectedMove = new(4.0f, 5.0f, 6.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract(
            chooserInputPackedPairVector,
            chooserInputProjectedMove,
            chooserInputScalar: 1.25f,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.9f,
            chooserAcceptedSelectedPlane: 0u,
            movementFlags: 0u,
            chooserOutputProjectedMove,
            out GroundedDriverSelectedPlaneTailChooserContractTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailChooserContractKind.Return1Horizontal, kind);
        Assert.Equal(1u, trace.FinalSelectedIndexInRange);
        Assert.Equal(1u, trace.FinalSelectedPlaneWalkable);
        Assert.Equal(1u, trace.Called635F80);
        Assert.Equal(0u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(1u, trace.ProjectedMoveMutatedByChooser);
        Assert.Equal(1.25f, trace.ChooserInputScalar, 6);
        Assert.Equal(chooserInputPackedPairVector.X, trace.ChooserInputPackedPairVector.X, 6);
        Assert.Equal(chooserInputProjectedMove.Y, trace.ChooserInputProjectedMove.Y, 6);
        Assert.Equal(chooserOutputProjectedMove.Z, trace.ChooserOutputProjectedMove.Z, 6);
        Assert.Equal(0u, trace.WroteField80FromSelectedZ);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract_WalkableChooserAcceptReturnsSelectedPlaneAndWritesField80()
    {
        Vector3 chooserInputPackedPairVector = new(2.0f, -1.0f, 0.0f);
        Vector3 chooserInputProjectedMove = new(1.0f, 2.0f, 3.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract(
            chooserInputPackedPairVector,
            chooserInputProjectedMove,
            chooserInputScalar: -0.5f,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.9f,
            chooserAcceptedSelectedPlane: 1u,
            movementFlags: 0u,
            chooserOutputProjectedMove: chooserInputProjectedMove,
            out GroundedDriverSelectedPlaneTailChooserContractTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailChooserContractKind.Return2SelectedPlane, kind);
        Assert.Equal(1u, trace.Called635F80);
        Assert.Equal(1u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(0u, trace.ProjectedMoveMutatedByChooser);
        Assert.Equal(1u, trace.WroteField80FromSelectedZ);
        Assert.Equal(chooserInputProjectedMove.X, trace.ChooserOutputProjectedMove.X, 6);
        Assert.Equal(chooserInputProjectedMove.Y, trace.ChooserOutputProjectedMove.Y, 6);
        Assert.Equal(chooserInputProjectedMove.Z, trace.ChooserOutputProjectedMove.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract_NonWalkableSkipsChooserAndSplineElevationSuppressesField80()
    {
        Vector3 chooserInputPackedPairVector = new(2.0f, -1.0f, 0.0f);
        Vector3 chooserInputProjectedMove = new(1.0f, 2.0f, 3.0f);
        Vector3 chooserOutputProjectedMove = new(7.0f, 8.0f, 9.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract(
            chooserInputPackedPairVector,
            chooserInputProjectedMove,
            chooserInputScalar: 0.25f,
            finalSelectedIndex: 0u,
            finalSelectedCount: 1u,
            finalSelectedNormalZ: 0.2f,
            chooserAcceptedSelectedPlane: 1u,
            movementFlags: (uint)MoveFlags.SplineElevation,
            chooserOutputProjectedMove,
            out GroundedDriverSelectedPlaneTailChooserContractTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailChooserContractKind.Return2SelectedPlane, kind);
        Assert.Equal(1u, trace.FinalSelectedIndexInRange);
        Assert.Equal(0u, trace.FinalSelectedPlaneWalkable);
        Assert.Equal(0u, trace.Called635F80);
        Assert.Equal(0u, trace.ChooserAcceptedSelectedPlane);
        Assert.Equal(1u, trace.ProjectedMoveMutatedByChooser);
        Assert.Equal(1u, trace.GroundedWall04000000Set);
        Assert.Equal(0u, trace.WroteField80FromSelectedZ);
        Assert.Equal(chooserOutputProjectedMove.X, trace.ChooserOutputProjectedMove.X, 6);
        Assert.Equal(chooserOutputProjectedMove.Y, trace.ChooserOutputProjectedMove.Y, 6);
        Assert.Equal(chooserOutputProjectedMove.Z, trace.ChooserOutputProjectedMove.Z, 6);
    }
}
