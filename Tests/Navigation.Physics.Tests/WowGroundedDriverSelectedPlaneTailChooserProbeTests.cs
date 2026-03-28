using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailChooserProbeTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe_AlignedProbeOutsideRadiusAcceptsSelectedPlane()
    {
        Vector3 inputPackedPairVector = new(1.0f, 0.0f, 0.0f);
        Vector3 inputProjectedMove = new(1.0f, 0.0f, 7.0f);
        Vector3 probePosition = new(2.0f, 0.05f, 9.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe(
            inputPackedPairVector,
            inputProjectedMove,
            chooserInputScalar: 0.4f,
            probePosition,
            collisionRadius: 0.5f,
            out GroundedDriverSelectedPlaneTailChooserProbeTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailChooserProbeKind.AcceptSelectedPlane, kind);
        Assert.Equal(228, trace.ProbeBudgetMilliseconds);
        Assert.Equal(1u, trace.OutsideCollisionRadius);
        Assert.Equal(1u, trace.AlignmentAccepted);
        Assert.Equal(1.0f, trace.ProbeDelta.X, 6);
        Assert.Equal(0.05f, trace.ProbeDelta.Y, 6);
        Assert.True(trace.AlignmentDot > 0.9848077f);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe_DeltaInsideCollisionRadiusRejectsBeforeAlignment()
    {
        Vector3 inputPackedPairVector = new(1.0f, 0.0f, 0.0f);
        Vector3 inputProjectedMove = new(4.0f, 5.0f, 0.0f);
        Vector3 probePosition = new(4.2f, 5.1f, 3.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe(
            inputPackedPairVector,
            inputProjectedMove,
            chooserInputScalar: 0.4f,
            probePosition,
            collisionRadius: 0.5f,
            out GroundedDriverSelectedPlaneTailChooserProbeTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailChooserProbeKind.RejectHorizontal, kind);
        Assert.Equal(228, trace.ProbeBudgetMilliseconds);
        Assert.Equal(0u, trace.OutsideCollisionRadius);
        Assert.Equal(0u, trace.AlignmentAccepted);
        Assert.True(trace.ProbeDistance2D < 0.5f);
        Assert.Equal(0.0f, trace.AlignmentDot, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe_MisalignedProbeRejectsSelectedPlane()
    {
        Vector3 inputPackedPairVector = new(1.0f, 0.0f, 0.0f);
        Vector3 inputProjectedMove = new(0.0f, 0.0f, 0.0f);
        Vector3 probePosition = new(0.0f, 2.0f, 1.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe(
            inputPackedPairVector,
            inputProjectedMove,
            chooserInputScalar: 0.4f,
            probePosition,
            collisionRadius: 0.5f,
            out GroundedDriverSelectedPlaneTailChooserProbeTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailChooserProbeKind.RejectHorizontal, kind);
        Assert.Equal(1u, trace.OutsideCollisionRadius);
        Assert.Equal(0u, trace.AlignmentAccepted);
        Assert.Equal(0.0f, trace.AlignmentDot, 6);
    }
}
