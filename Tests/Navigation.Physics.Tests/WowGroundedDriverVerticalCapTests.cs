using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverVerticalCapTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverVerticalCap_IgnoresCapWhenFlagIsClear()
    {
        uint appliedCap = EvaluateWoWGroundedDriverVerticalCap(
            movementFlags: (uint)MoveFlags.Forward,
            combinedMoveZ: 3.0f,
            nextSweepDistance: 5.0f,
            currentZ: 10.0f,
            boundingRadius: 1.5f,
            capField80: 12.0f,
            out GroundedDriverVerticalCapTrace trace);

        Assert.Equal(0u, appliedCap);
        Assert.Equal(0u, trace.CapBitSet);
        Assert.Equal(0u, trace.AppliedCap);
        Assert.Equal(5.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0u, trace.SetFinalizeFlag20);
        Assert.Equal(0u, trace.SetTinySweepFlag30);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverVerticalCap_IgnoresCapWhenPredictedZIsWithinLimit()
    {
        uint appliedCap = EvaluateWoWGroundedDriverVerticalCap(
            movementFlags: (uint)MoveFlags.SplineElevation,
            combinedMoveZ: 2.0f,
            nextSweepDistance: 4.0f,
            currentZ: 10.0f,
            boundingRadius: 1.0f,
            capField80: 11.5f,
            out GroundedDriverVerticalCapTrace trace);

        Assert.Equal(0u, appliedCap);
        Assert.Equal(1u, trace.CapBitSet);
        Assert.Equal(0u, trace.AppliedCap);
        Assert.Equal(4.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(0u, trace.SetFinalizeFlag20);
        Assert.Equal(0u, trace.SetTinySweepFlag30);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverVerticalCap_ClampsSweepAndSetsFinalizeWhenUpwardMoveOvershootsCap()
    {
        uint appliedCap = EvaluateWoWGroundedDriverVerticalCap(
            movementFlags: (uint)MoveFlags.SplineElevation,
            combinedMoveZ: 5.0f,
            nextSweepDistance: 10.0f,
            currentZ: 20.0f,
            boundingRadius: 2.0f,
            capField80: 22.0f,
            out GroundedDriverVerticalCapTrace trace);

        Assert.Equal(1u, appliedCap);
        Assert.Equal(1u, trace.AppliedCap);
        Assert.Equal(8.0f, trace.OutputSweepDistance, 6);
        Assert.Equal(1u, trace.SetFinalizeFlag20);
        Assert.Equal(0u, trace.SetTinySweepFlag30);
        Assert.Equal(4.0f, trace.AllowedDeltaZ, 6);
        Assert.Equal(24.0f, trace.CapAbsoluteZ, 6);
        Assert.Equal(25.0f, trace.PredictedZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverVerticalCap_SetsTinySweepFlagWhenClampDropsBelowThreshold()
    {
        uint appliedCap = EvaluateWoWGroundedDriverVerticalCap(
            movementFlags: (uint)MoveFlags.SplineElevation,
            combinedMoveZ: 4.0f,
            nextSweepDistance: 0.002f,
            currentZ: 30.0f,
            boundingRadius: 1.0f,
            capField80: 30.5f,
            out GroundedDriverVerticalCapTrace trace);

        Assert.Equal(1u, appliedCap);
        Assert.Equal(1u, trace.AppliedCap);
        Assert.Equal(0.00075f, trace.OutputSweepDistance, 6);
        Assert.Equal(1u, trace.SetFinalizeFlag20);
        Assert.Equal(1u, trace.SetTinySweepFlag30);
    }
}
