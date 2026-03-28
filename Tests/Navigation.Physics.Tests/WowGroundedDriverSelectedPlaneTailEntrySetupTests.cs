using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailEntrySetupTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup_ZeroMagnitudeExitsBeforeProbeButStillCapturesSetupState()
    {
        Vector3 requestedMove = new(0.0f, 0.0f, 0.0f);
        Vector3 currentPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 field44Vector = new(1.0f, 2.0f, 3.0f);
        Vector3 field50Vector = new(4.0f, 5.0f, 6.0f);
        Vector3 field5cVector = new(7.0f, 8.0f, 9.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup(
            inputWindowMilliseconds: 250u,
            requestedMove,
            currentPosition,
            field44Vector,
            field50Vector,
            field78: 125u,
            field5cVector,
            field68: 0.6f,
            field6c: -0.8f,
            field40Flags: 0x20u,
            field84: 0.75f,
            out GroundedDriverSelectedPlaneTailEntrySetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailEntrySetupKind.ExitWithoutProbe, kind);
        Assert.Equal(1u, trace.ZeroedPairForwardState);
        Assert.Equal(1u, trace.ZeroedDirectStateBit);
        Assert.Equal(1u, trace.ZeroedSidecarState);
        Assert.Equal(1u, trace.ZeroedLateralOffset);
        Assert.Equal(0u, trace.BuiltNormalizedInputDirection);
        Assert.Equal(0.25f, trace.InputWindowSeconds, 5);
        Assert.Equal(0.125f, trace.Field78Seconds, 5);
        Assert.Equal(0.125f, trace.CurrentWindowScalar, 5);
        Assert.Equal(0.0f, trace.CurrentMagnitude, 5);
        Assert.Equal(0.0f, trace.CurrentHorizontalMagnitude, 5);
        Assert.Equal(0.0f, trace.AbsoluteVerticalMagnitude, 5);
        Assert.Equal(field44Vector.X, trace.SnapshotTrace.Field44Vector.X, 5);
        Assert.Equal(field50Vector.Y, trace.SnapshotTrace.Field50Vector.Y, 5);
        Assert.Equal(field5cVector.Z, trace.SnapshotTrace.Field5cVector.Z, 5);
        Assert.Equal(125u, trace.SnapshotTrace.Field78);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup_NonZeroMagnitudeBuildsNormalizedDirectionAndScalars()
    {
        Vector3 requestedMove = new(3.0f, 4.0f, -12.0f);
        Vector3 currentPosition = new(-1.0f, -2.0f, -3.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup(
            inputWindowMilliseconds: 90u,
            requestedMove,
            currentPosition,
            field44Vector: new Vector3(1.0f, 1.5f, 2.0f),
            field50Vector: new Vector3(2.5f, 3.0f, 3.5f),
            field78: 40u,
            field5cVector: new Vector3(0.6f, 0.8f, 0.0f),
            field68: 0.6f,
            field6c: 0.8f,
            field40Flags: 0u,
            field84: 0.5f,
            out GroundedDriverSelectedPlaneTailEntrySetupTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailEntrySetupKind.ContinueToProbe, kind);
        Assert.Equal(1u, trace.BuiltNormalizedInputDirection);
        Assert.Equal(0.09f, trace.InputWindowSeconds, 5);
        Assert.Equal(0.04f, trace.Field78Seconds, 5);
        Assert.Equal(0.0f, trace.ElapsedScalar, 5);
        Assert.Equal(0.04f, trace.CurrentWindowScalar, 5);
        Assert.Equal(13.0f, trace.CurrentMagnitude, 5);
        Assert.Equal(5.0f, trace.CurrentHorizontalMagnitude, 5);
        Assert.Equal(12.0f, trace.AbsoluteVerticalMagnitude, 5);
        Assert.Equal(3.0f / 13.0f, trace.OutputNormalizedInputDirection.X, 5);
        Assert.Equal(4.0f / 13.0f, trace.OutputNormalizedInputDirection.Y, 5);
        Assert.Equal(-12.0f / 13.0f, trace.OutputNormalizedInputDirection.Z, 5);
        Assert.Equal(currentPosition.X, trace.CurrentPosition.X, 5);
        Assert.Equal(currentPosition.Y, trace.CurrentPosition.Y, 5);
        Assert.Equal(currentPosition.Z, trace.CurrentPosition.Z, 5);
    }
}
