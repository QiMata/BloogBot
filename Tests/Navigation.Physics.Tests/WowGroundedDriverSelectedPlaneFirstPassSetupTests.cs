using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneFirstPassSetupTests
{
    private const float WalkableMinNormalZ = 0.6427876353263855f;

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup_InBandBuildsNegativeNormalizedHorizontalWorkingVector()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup(
            inputPackedPairVector: new Vector3(4.0f, 5.0f, 0.0f),
            fieldB0: 0.5f,
            boundingRadius: 1.5f,
            inputContactNormal: new Vector3(0.6f, 0.8f, 0.5f),
            firstPassRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneFirstPassSetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFirstPassSetupKind.ContinueToFollowupRerank, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFirstPassSetupKind.ContinueToFollowupRerank, trace.DispatchKind);
        Assert.Equal(7u, trace.SupportPlaneInitCount);
        Assert.Equal(1u, trace.LoadedInputPackedPair);
        Assert.Equal(1u, trace.UsedBoundingRadiusTanFloor);
        Assert.Equal(1u, trace.EnteredFirstPassNormalBand);
        Assert.Equal(1u, trace.InvokedFirstPassRerank);
        Assert.Equal(1u, trace.FirstPassRerankSucceeded);
        Assert.Equal(1.0f, trace.HorizontalMagnitude, 6);
        Assert.Equal(1.0f, trace.InverseHorizontalMagnitude, 6);
        Assert.Equal(-0.6f, trace.FirstPassWorkingVector.X, 6);
        Assert.Equal(-0.8f, trace.FirstPassWorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.FirstPassWorkingVector.Z, 6);
        Assert.Equal(1.7876304f, trace.OutputScalarFloor, 5);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup_NegativeNormalZSkipsFirstPassAndKeepsFieldFloor()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup(
            inputPackedPairVector: new Vector3(1.0f, 2.0f, 0.0f),
            fieldB0: 2.0f,
            boundingRadius: 0.5f,
            inputContactNormal: new Vector3(0.4f, 0.3f, -0.1f),
            firstPassRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneFirstPassSetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFirstPassSetupKind.SkipToFollowupRerank, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFirstPassSetupKind.SkipToFollowupRerank, trace.DispatchKind);
        Assert.Equal(0u, trace.UsedBoundingRadiusTanFloor);
        Assert.Equal(0u, trace.EnteredFirstPassNormalBand);
        Assert.Equal(0u, trace.InvokedFirstPassRerank);
        Assert.Equal(0u, trace.FirstPassRerankSucceeded);
        Assert.Equal(2.0013888f, trace.OutputScalarFloor, 5);
        Assert.Equal(0.0f, trace.FirstPassWorkingVector.X, 6);
        Assert.Equal(0.0f, trace.FirstPassWorkingVector.Y, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup_AboveWalkableThresholdSkipsFirstPass()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup(
            inputPackedPairVector: new Vector3(-2.0f, 3.0f, 0.0f),
            fieldB0: 0.75f,
            boundingRadius: 1.0f,
            inputContactNormal: new Vector3(0.3f, 0.4f, WalkableMinNormalZ + 0.01f),
            firstPassRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneFirstPassSetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFirstPassSetupKind.SkipToFollowupRerank, kind);
        Assert.Equal(0u, trace.EnteredFirstPassNormalBand);
        Assert.Equal(0u, trace.InvokedFirstPassRerank);
        Assert.Equal(0u, trace.FirstPassRerankSucceeded);
        Assert.Equal(0.0f, trace.HorizontalMagnitude, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup_WalkableThresholdBoundaryStillInvokesFirstPassAndCanFail()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup(
            inputPackedPairVector: new Vector3(0.0f, -1.0f, 0.0f),
            fieldB0: 0.25f,
            boundingRadius: 1.0f,
            inputContactNormal: new Vector3(1.0f, 0.0f, WalkableMinNormalZ),
            firstPassRerankSucceeded: 0u,
            out GroundedDriverSelectedPlaneFirstPassSetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFirstPassSetupKind.FirstPassFailureExit, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFirstPassSetupKind.FirstPassFailureExit, trace.DispatchKind);
        Assert.Equal(1u, trace.EnteredFirstPassNormalBand);
        Assert.Equal(1u, trace.InvokedFirstPassRerank);
        Assert.Equal(0u, trace.FirstPassRerankSucceeded);
        Assert.Equal(-1.0f, trace.FirstPassWorkingVector.X, 6);
        Assert.Equal(0.0f, trace.FirstPassWorkingVector.Y, 6);
    }
}
