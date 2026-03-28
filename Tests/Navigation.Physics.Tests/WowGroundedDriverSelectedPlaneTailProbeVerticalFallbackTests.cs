using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailProbeVerticalFallbackTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback_UsesVerticalOnlyFallbackWhenHorizontalMagnitudeRemains()
    {
        Vector3 normalizedInputDirection = new(0.3f, 0.4f, -0.5f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback(
            normalizedInputDirection,
            currentHorizontalMagnitude: 0.25f,
            remainingMagnitude: 2.0f,
            verticalFallbackAlreadyUsed: 0u,
            out GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind.UseVerticalFallback, kind);
        Assert.Equal(1u, trace.HorizontalMagnitudeExceedsEpsilon);
        Assert.Equal(1u, trace.ClearedField84);
        Assert.Equal(0.0f, trace.OutputNextInputVector.X, 6);
        Assert.Equal(0.0f, trace.OutputNextInputVector.Y, 6);
        Assert.Equal(-1.0f, trace.OutputNextInputVector.Z, 6);
        Assert.Equal(1.0f, trace.OutputMagnitude, 6);
        Assert.Equal(0.0f, trace.OutputField84, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback_RejectsWhenFallbackAlreadyUsed()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback(
            normalizedInputDirection: new Vector3(0.0f, 0.0f, 1.0f),
            currentHorizontalMagnitude: 0.25f,
            remainingMagnitude: 2.0f,
            verticalFallbackAlreadyUsed: 1u,
            out GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind.RejectNoFallback, kind);
        Assert.Equal(1u, trace.VerticalFallbackAlreadyUsed);
        Assert.Equal(0u, trace.HorizontalMagnitudeExceedsEpsilon);
        Assert.Equal(0.0f, trace.OutputMagnitude, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback_RejectsWhenHorizontalMagnitudeIsZero()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback(
            normalizedInputDirection: new Vector3(0.0f, 0.0f, 1.0f),
            currentHorizontalMagnitude: 0.0f,
            remainingMagnitude: 2.0f,
            verticalFallbackAlreadyUsed: 0u,
            out GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind.RejectNoFallback, kind);
        Assert.Equal(0u, trace.HorizontalMagnitudeExceedsEpsilon);
        Assert.Equal(0u, trace.ClearedField84);
        Assert.Equal(0.0f, trace.OutputMagnitude, 6);
    }
}
