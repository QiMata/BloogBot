using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneBlendCorrectionTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection_DiscardsUphillBlendWhenHorizontalFallbackResolvesFarther()
    {
        Vector3 requestedMove = new(4.0f, 0.0f, 1.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.8f, 0.6f);
        Vector3 horizontalProjectedMove = new(4.5f, 0.0f, 0.0f);

        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection(
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 1.0f,
            horizontalProjectedMove,
            horizontalResolved2D: 4.5f,
            out GroundedDriverSelectedPlaneBlendCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBlendCorrectionKind.UseHorizontalFallback, dispatchKind);
        Assert.Equal(0u, trace.ClampedVerticalMagnitude);
        Assert.Equal(1u, trace.DiscardedUphillBlend);
        Assert.Equal(0u, trace.AcceptedSelectedPlaneBlend);
        Assert.Equal(0.6f, trace.IntoPlane, 6);
        Assert.Equal(4.0f, trace.SelectedPlaneProjectedMove.X, 6);
        Assert.Equal(-0.48f, trace.SelectedPlaneProjectedMove.Y, 6);
        Assert.Equal(0.64f, trace.SelectedPlaneProjectedMove.Z, 6);
        Assert.Equal(4.5f, trace.OutputProjectedMove.X, 6);
        Assert.Equal(0.0f, trace.OutputProjectedMove.Y, 6);
        Assert.Equal(0.0f, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(4.0286970f, trace.SlopedResolved2D, 5);
        Assert.Equal(4.5f, trace.OutputResolved2D, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection_ClampsAndAcceptsSelectedPlaneBlend()
    {
        Vector3 requestedMove = new(3.0f, 0.0f, 4.0f);
        Vector3 selectedPlaneNormal = new(0.0f, 0.6f, 0.8f);
        Vector3 horizontalProjectedMove = new(1.0f, 0.0f, 0.0f);

        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection(
            requestedMove,
            selectedPlaneNormal,
            verticalLimit: 0.5f,
            horizontalProjectedMove,
            horizontalResolved2D: 1.0f,
            out GroundedDriverSelectedPlaneBlendCorrectionTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneBlendCorrectionKind.UseSelectedPlaneBlend, dispatchKind);
        Assert.Equal(1u, trace.ClampedVerticalMagnitude);
        Assert.Equal(0u, trace.DiscardedUphillBlend);
        Assert.Equal(1u, trace.AcceptedSelectedPlaneBlend);
        Assert.Equal(3.2f, trace.IntoPlane, 6);
        Assert.Equal(1.0416666f, trace.SelectedPlaneProjectedMove.X, 5);
        Assert.Equal(-0.6666667f, trace.SelectedPlaneProjectedMove.Y, 5);
        Assert.Equal(0.5f, trace.SelectedPlaneProjectedMove.Z, 6);
        Assert.Equal(1.0416666f, trace.OutputProjectedMove.X, 5);
        Assert.Equal(-0.6666667f, trace.OutputProjectedMove.Y, 5);
        Assert.Equal(0.5f, trace.OutputProjectedMove.Z, 6);
        Assert.Equal(1.2367353f, trace.SlopedResolved2D, 5);
        Assert.Equal(1.2367353f, trace.OutputResolved2D, 5);
    }
}
