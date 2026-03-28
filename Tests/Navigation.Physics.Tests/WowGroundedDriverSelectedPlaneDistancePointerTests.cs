using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneDistancePointerTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_WithinRadiusKeepsDistancePointerAndWritesDirectScalar()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(-0.25f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.DirectScalar, kind);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.DirectScalar, trace.OutputKind);
        Assert.Equal(0u, trace.UsedInfiniteScalar);
        Assert.Equal(0u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.ZeroedDistancePointer);
        Assert.Equal(0.25f, trace.DotScaledDistance, 6);
        Assert.Equal(0.25f, trace.RawScalar, 6);
        Assert.Equal(0.25f, trace.OutputScalar, 6);
        Assert.Equal(1.0f, trace.OutputDistancePointer, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.X, 6);
        Assert.Equal(0.0f, trace.OutputCorrection.Y, 6);
        Assert.Equal(0.25f, trace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_PositiveScalarAboveRadiusClampsAndRescalesDistancePointer()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(-2.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.PositiveRadiusClamp, kind);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.PositiveRadiusClamp, trace.OutputKind);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(0u, trace.ZeroedDistancePointer);
        Assert.Equal(2.0f, trace.RawScalar, 6);
        Assert.Equal(1.0f, trace.OutputScalar, 6);
        Assert.Equal(0.5f, trace.OutputDistancePointer, 6);
        Assert.Equal(1.0f, trace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_NegativeScalarWithGroundedWallFlagZeroesDistanceAndReturnsPositiveRadius()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(2.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0x04000000u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.FlaggedNegativeZeroDistance, kind);
        Assert.Equal(GroundedDriverSelectedPlaneDistancePointerKind.FlaggedNegativeZeroDistance, trace.OutputKind);
        Assert.Equal(1u, trace.GroundedWall04000000Set);
        Assert.Equal(1u, trace.ZeroedDistancePointer);
        Assert.Equal(1u, trace.MutatedDistancePointer);
        Assert.Equal(-2.0f, trace.RawScalar, 6);
        Assert.Equal(1.0f, trace.OutputScalar, 6);
        Assert.Equal(0.0f, trace.OutputDistancePointer, 6);
        Assert.Equal(1.0f, trace.OutputCorrection.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_ThresholdQualifiedOverrideUsesNegatedSelectedPlaneNormal()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 1u,
            selectedContactNormalZ: 0.6427876353263855f,
            selectedPlaneNormal: new Vector3(0.6f, 0.0f, 0.8f),
            inputWorkingVector: new Vector3(0.0f, 1.0f, 0.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 2.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.NegativeRadiusClamp, kind);
        Assert.Equal(1u, trace.UseSelectedPlaneOverride);
        Assert.Equal(1u, trace.SelectedContactNormalWithinOverrideBand);
        Assert.Equal(1u, trace.UsedSelectedPlaneNormalOverride);
        Assert.Equal(1.0f, trace.SelectedPlaneMagnitudeSquared, 6);
        Assert.Equal(-0.6f, trace.EffectiveWorkingVector.X, 6);
        Assert.Equal(0.0f, trace.EffectiveWorkingVector.Y, 6);
        Assert.Equal(-0.8f, trace.EffectiveWorkingVector.Z, 6);
        Assert.Equal(-1.5f, trace.RawScalar, 6);
        Assert.Equal(-1.0f, trace.OutputScalar, 6);
        Assert.Equal(1.3333334f, trace.OutputDistancePointer, 5);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_NormalAboveThresholdKeepsInputWorkingVector()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 1u,
            selectedContactNormalZ: 0.8f,
            selectedPlaneNormal: new Vector3(0.6f, 0.0f, 0.8f),
            inputWorkingVector: new Vector3(0.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 2.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.DirectScalar, kind);
        Assert.Equal(1u, trace.UseSelectedPlaneOverride);
        Assert.Equal(0u, trace.SelectedContactNormalWithinOverrideBand);
        Assert.Equal(0u, trace.UsedSelectedPlaneNormalOverride);
        Assert.Equal(1.0f, trace.SelectedPlaneMagnitudeSquared, 6);
        Assert.Equal(0.0f, trace.EffectiveWorkingVector.X, 6);
        Assert.Equal(0.0f, trace.EffectiveWorkingVector.Y, 6);
        Assert.Equal(1.0f, trace.EffectiveWorkingVector.Z, 6);
        Assert.Equal(0.0f, trace.RawScalar, 6);
        Assert.Equal(0.0f, trace.OutputScalar, 6);
        Assert.Equal(2.0f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_DegenerateSelectedPlaneSkipsOverride()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 1u,
            selectedContactNormalZ: 0.25f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 0.0f),
            inputWorkingVector: new Vector3(0.0f, 0.0f, 1.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 2.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.DirectScalar, kind);
        Assert.Equal(1u, trace.UseSelectedPlaneOverride);
        Assert.Equal(1u, trace.SelectedContactNormalWithinOverrideBand);
        Assert.Equal(0u, trace.UsedSelectedPlaneNormalOverride);
        Assert.Equal(0.0f, trace.SelectedPlaneMagnitudeSquared, 6);
        Assert.Equal(0.0f, trace.EffectiveWorkingVector.X, 6);
        Assert.Equal(0.0f, trace.EffectiveWorkingVector.Y, 6);
        Assert.Equal(1.0f, trace.EffectiveWorkingVector.Z, 6);
        Assert.Equal(0.0f, trace.RawScalar, 6);
        Assert.Equal(0.0f, trace.OutputScalar, 6);
        Assert.Equal(2.0f, trace.OutputDistancePointer, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation_ZeroDenominatorUsesSignedInfinityBeforeClamp()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride: 0u,
            selectedContactNormalZ: 0.0f,
            selectedPlaneNormal: new Vector3(0.0f, 0.0f, 1.0f),
            inputWorkingVector: new Vector3(1.0f, 0.0f, 0.0f),
            inputMoveDirection: new Vector3(1.0f, 0.0f, 0.0f),
            inputDistancePointer: 1.0f,
            movementFlags: 0u,
            boundingRadius: 1.0f,
            out GroundedDriverSelectedPlaneDistancePointerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneDistancePointerKind.NegativeRadiusClamp, kind);
        Assert.Equal(1u, trace.UsedInfiniteScalar);
        Assert.True(trace.RawScalar < -1.0e20f);
        Assert.Equal(-1.0f, trace.OutputScalar, 6);
        Assert.True(trace.OutputDistancePointer >= 0.0f);
        Assert.True(trace.OutputDistancePointer < 1.0e-20f);
    }
}
