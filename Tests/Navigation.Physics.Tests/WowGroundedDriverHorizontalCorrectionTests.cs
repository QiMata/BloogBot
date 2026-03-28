using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverHorizontalCorrectionTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverHorizontalCorrection_ProjectsOntoPlaneAndAddsEpsilonPushout()
    {
        uint kind = EvaluateWoWGroundedDriverHorizontalCorrection(
            requestedMove: new Vector3(3.0f, 4.0f, 9.0f),
            selectedPlaneNormal: new Vector3(0.6f, 0.8f, 0.0f),
            out GroundedDriverHorizontalCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverHorizontalCorrectionKind.HorizontalEpsilonProjection, kind);
        Assert.Equal(1u, trace.EntryGateAccepted);
        Assert.Equal(1u, trace.NormalizedHorizontalNormal);
        Assert.Equal(1u, trace.AppliedEpsilonPushout);
        Assert.Equal(0u, trace.ZeroedOutputOnReject);
        Assert.Equal(5.0f, trace.IntoPlane, 6);
        Assert.Equal(1.0f, trace.HorizontalMagnitude, 6);
        Assert.Equal(1.0f, trace.InverseHorizontalMagnitude, 6);
        Assert.Equal(0.6f, trace.NormalizedHorizontalNormalVector.X, 6);
        Assert.Equal(0.8f, trace.NormalizedHorizontalNormalVector.Y, 6);
        Assert.Equal(0.0006f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0008f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(0.001f, trace.OutputResolved2D, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverHorizontalCorrection_RejectsWhenPlaneHasNoHorizontalComponent()
    {
        uint kind = EvaluateWoWGroundedDriverHorizontalCorrection(
            requestedMove: new Vector3(7.0f, 8.0f, 9.0f),
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            out GroundedDriverHorizontalCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverHorizontalCorrectionKind.ZeroedOnReject, kind);
        Assert.Equal(0u, trace.EntryGateAccepted);
        Assert.Equal(0u, trace.AppliedEpsilonPushout);
        Assert.Equal(1u, trace.ZeroedOutputOnReject);
        Assert.Equal(0.0f, trace.HorizontalMagnitude, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(0.0f, trace.OutputResolved2D, 6);
    }
}
