using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneCorrectionTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrection_WritesVerticalOnlyCorrectionAndRescalesBookkeeping()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrection(
            requestedMove: new Vector3(3.0f, 4.0f, 5.0f),
            selectedPlaneNormal: new Vector3(0.6f, 0.8f, 0.0f),
            verticalLimit: 8.0f,
            remainingDistanceBefore: 12.0f,
            sweepDistanceBefore: 6.0f,
            sweepDistanceAfter: 3.0f,
            inputSweepFraction: 0.75f,
            inputDistancePointer: 9.0f,
            out GroundedDriverSelectedPlaneCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionKind.VerticalOnly, correctionKind);
        Assert.Equal(GroundedDriverSelectedPlaneCorrectionKind.VerticalOnly, trace.CorrectionKind);
        Assert.Equal(1u, trace.WroteVerticalOnlyCorrection);
        Assert.Equal(0u, trace.ClampedVerticalMagnitude);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledRemainingDistance);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(5.0f, trace.IntoPlane, 6);
        Assert.Equal(5.0f, trace.ProjectedVertical, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Y, 6);
        Assert.Equal(5.0f, trace.OutputCorrection.Z, 6);
        Assert.Equal(6.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(3.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0.375f, trace.OutputSweepFraction, 6);
        Assert.Equal(4.5f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrection_ClampsVerticalMagnitudeWhenLimitIsTighter()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrection(
            requestedMove: new Vector3(2.0f, 0.0f, 5.0f),
            selectedPlaneNormal: new Vector3(0.6f, 0.0f, 0.8f),
            verticalLimit: 0.5f,
            remainingDistanceBefore: 10.0f,
            sweepDistanceBefore: 8.0f,
            sweepDistanceAfter: 4.0f,
            inputSweepFraction: 0.5f,
            inputDistancePointer: 2.0f,
            out GroundedDriverSelectedPlaneCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionKind.VerticalOnly, correctionKind);
        Assert.Equal(1u, trace.WroteVerticalOnlyCorrection);
        Assert.Equal(1u, trace.ClampedVerticalMagnitude);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(1u, trace.RescaledRemainingDistance);
        Assert.Equal(1u, trace.RescaledSweepFraction);
        Assert.Equal(-1.12f, trace.ProjectedCorrection.X, 5);
        Assert.Equal(0.0f, trace.ProjectedCorrection.Y, 6);
        Assert.Equal(0.5f, trace.ProjectedCorrection.Z, 6);
        Assert.Equal(0.5f, trace.ProjectedVertical, 6);
        Assert.Equal(0.5f, trace.OutputCorrection.Z, 6);
        Assert.Equal(5.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(4.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0.25f, trace.OutputSweepFraction, 6);
        Assert.Equal(1.0f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneCorrection_LeavesBookkeepingUntouchedWhenSweepDistanceIsZero()
    {
        uint correctionKind = EvaluateWoWGroundedDriverSelectedPlaneCorrection(
            requestedMove: new Vector3(1.0f, 2.0f, 3.0f),
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            verticalLimit: 10.0f,
            remainingDistanceBefore: 7.0f,
            sweepDistanceBefore: 0.0f,
            sweepDistanceAfter: 0.0f,
            inputSweepFraction: 0.9f,
            inputDistancePointer: 3.25f,
            out GroundedDriverSelectedPlaneCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneCorrectionKind.VerticalOnly, correctionKind);
        Assert.Equal(1u, trace.WroteVerticalOnlyCorrection);
        Assert.Equal(0u, trace.ClampedVerticalMagnitude);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.RescaledRemainingDistance);
        Assert.Equal(0u, trace.RescaledSweepFraction);
        Assert.Equal(7.0f, trace.OutputRemainingDistance, 6);
        Assert.Equal(0.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0.9f, trace.OutputSweepFraction, 6);
        Assert.Equal(3.25f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Z, 6);
    }
}
