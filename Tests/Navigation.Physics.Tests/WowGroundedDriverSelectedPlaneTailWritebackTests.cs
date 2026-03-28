using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailWritebackTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailWriteback_TailScalarMatchKeepsOnlyThirdPassXYWriteback()
    {
        Vector3 inputPosition = new(10.0f, -2.0f, 5.0f);
        Vector3 inputPackedPairVector = new(2.0f, -3.0f, 9.0f);
        Vector3 projectedTailMove = new(7.0f, 8.0f, 1.5f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailWriteback(
            inputPosition,
            inputPackedPairVector,
            followupScalar: 0.75f,
            scalarFloor: 0.75f,
            inputSelectedContactNormalZ: 0.6f,
            checkWalkableAccepted: 1u,
            projectedTailRerankSucceeded: 1u,
            projectedTailMove,
            projectedTailResolved2D: 2.5f,
            out GroundedDriverSelectedPlaneTailWritebackTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailWritebackKind.ThirdPassOnly, kind);
        Assert.Equal(0u, trace.TailScalarDiffExceedsEpsilon);
        Assert.Equal(1u, trace.CheckWalkableAccepted);
        Assert.Equal(1u, trace.ProjectedTailRerankSucceeded);
        Assert.Equal(0u, trace.AppliedProjectedTailWriteback);
        Assert.Equal(10.0f + (2.0f * 0.75f), trace.OutputPosition.X, 6);
        Assert.Equal(-2.0f + (-3.0f * 0.75f), trace.OutputPosition.Y, 6);
        Assert.Equal(5.0f, trace.OutputPosition.Z, 6);
        Assert.Equal(0.75f, trace.OutputResolved2D, 6);
        Assert.Equal(0.6f, trace.OutputSelectedContactNormalZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailWriteback_WalkableRejectKeepsOnlyThirdPassXYWriteback()
    {
        Vector3 inputPosition = new(-4.0f, 6.0f, -1.0f);
        Vector3 inputPackedPairVector = new(-1.5f, 0.5f, 0.0f);
        Vector3 projectedTailMove = new(0.5f, 1.0f, 2.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailWriteback(
            inputPosition,
            inputPackedPairVector,
            followupScalar: 0.25f,
            scalarFloor: 0.75f,
            inputSelectedContactNormalZ: 0.2f,
            checkWalkableAccepted: 0u,
            projectedTailRerankSucceeded: 1u,
            projectedTailMove,
            projectedTailResolved2D: 1.25f,
            out GroundedDriverSelectedPlaneTailWritebackTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailWritebackKind.ThirdPassOnly, kind);
        Assert.Equal(1u, trace.TailScalarDiffExceedsEpsilon);
        Assert.Equal(0u, trace.CheckWalkableAccepted);
        Assert.Equal(1u, trace.ProjectedTailRerankSucceeded);
        Assert.Equal(0u, trace.AppliedProjectedTailWriteback);
        Assert.Equal(-4.0f + (-1.5f * 0.25f), trace.OutputPosition.X, 6);
        Assert.Equal(6.0f + (0.5f * 0.25f), trace.OutputPosition.Y, 6);
        Assert.Equal(-1.0f, trace.OutputPosition.Z, 6);
        Assert.Equal(0.25f, trace.OutputResolved2D, 6);
        Assert.Equal(0.2f, trace.OutputSelectedContactNormalZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailWriteback_ProjectedTailSuccessAddsXYZWritebackAndNormalZ()
    {
        Vector3 inputPosition = new(1.0f, 2.0f, 3.0f);
        Vector3 inputPackedPairVector = new(4.0f, -2.0f, 7.0f);
        Vector3 projectedTailMove = new(0.5f, -1.0f, 0.75f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailWriteback(
            inputPosition,
            inputPackedPairVector,
            followupScalar: 0.5f,
            scalarFloor: 1.25f,
            inputSelectedContactNormalZ: 0.9f,
            checkWalkableAccepted: 1u,
            projectedTailRerankSucceeded: 1u,
            projectedTailMove,
            projectedTailResolved2D: 1.125f,
            out GroundedDriverSelectedPlaneTailWritebackTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailWritebackKind.ThirdPassPlusProjectedTail, kind);
        Assert.Equal(1u, trace.TailScalarDiffExceedsEpsilon);
        Assert.Equal(1u, trace.CheckWalkableAccepted);
        Assert.Equal(1u, trace.ProjectedTailRerankSucceeded);
        Assert.Equal(1u, trace.AppliedProjectedTailWriteback);
        Assert.Equal(1.0f + (4.0f * 0.5f) + 0.5f, trace.OutputPosition.X, 6);
        Assert.Equal(2.0f + (-2.0f * 0.5f) + -1.0f, trace.OutputPosition.Y, 6);
        Assert.Equal(3.0f + 0.75f, trace.OutputPosition.Z, 6);
        Assert.Equal(0.5f + 1.125f, trace.OutputResolved2D, 6);
        Assert.Equal(0.9f + 0.75f, trace.OutputSelectedContactNormalZ, 6);
    }
}
