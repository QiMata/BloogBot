using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneThirdPassSetupTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup_FailureAdvancesPositionAndExitsWithoutSelection()
    {
        Vector3 inputPackedPairVector = new(3.0f, -4.0f, 9.0f);

        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
            inputPackedPairVector,
            inputPositionZ: 10.0f,
            followupScalar: 0.75f,
            scalarFloor: 1.25f,
            thirdPassRerankSucceeded: 0u,
            out GroundedDriverSelectedPlaneThirdPassSetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneThirdPassSetupKind.ExitWithoutSelection, dispatchKind);
        Assert.Equal(1u, trace.LoadedPackedPairXY);
        Assert.Equal(1u, trace.ZeroedWorkingVectorZ);
        Assert.Equal(1u, trace.AdvancedPositionZ);
        Assert.Equal(1u, trace.InvokedThirdPassRerank);
        Assert.Equal(0u, trace.ThirdPassRerankSucceeded);
        Assert.Equal(inputPackedPairVector.X, trace.InputPackedPairVector.X, 6);
        Assert.Equal(inputPackedPairVector.Y, trace.InputPackedPairVector.Y, 6);
        Assert.Equal(inputPackedPairVector.Z, trace.InputPackedPairVector.Z, 6);
        Assert.Equal(3.0f, trace.ThirdPassWorkingVector.X, 6);
        Assert.Equal(-4.0f, trace.ThirdPassWorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.ThirdPassWorkingVector.Z, 6);
        Assert.Equal(10.75f, trace.OutputPositionZ, 6);
        Assert.Equal(1.25f, trace.ScalarFloor, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup_SuccessContinuesToBlendCorrection()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
            inputPackedPairVector: new Vector3(-2.0f, 5.5f, -1.0f),
            inputPositionZ: -3.0f,
            followupScalar: -0.25f,
            scalarFloor: 0.5f,
            thirdPassRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneThirdPassSetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneThirdPassSetupKind.ContinueToBlendCorrection, dispatchKind);
        Assert.Equal(1u, trace.ThirdPassRerankSucceeded);
        Assert.Equal(-2.0f, trace.ThirdPassWorkingVector.X, 6);
        Assert.Equal(5.5f, trace.ThirdPassWorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.ThirdPassWorkingVector.Z, 6);
        Assert.Equal(-3.25f, trace.OutputPositionZ, 6);
        Assert.Equal(-0.25f, trace.FollowupScalar, 6);
    }
}
