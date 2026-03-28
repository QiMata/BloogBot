using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailProbeRerouteCandidateTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate_FirstAttemptAcceptsAndNormalizesCandidate()
    {
        Vector3 normalizedInputDirection = new(0.6f, 0.8f, 0.0f);
        Vector3 lateralOffset = new(0.0f, 0.0f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
            attemptIndex: 1u,
            normalizedInputDirection,
            remainingMagnitude: 2.0f,
            lateralOffset,
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(4.0f, 5.0f, 6.0f),
            originalHorizontalMagnitude: 1.0f,
            originalVerticalMagnitude: 3.0f,
            previousField68: 0.6f,
            previousField6c: 0.8f,
            previousField84: 0.5f,
            out GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind.AcceptCandidate, kind);
        Assert.Equal(0u, trace.CheckedDriftThresholds);
        Assert.Equal(1u, trace.NormalizedCandidate2D);
        Assert.Equal(1u, trace.UpdatedDirectionFields);
        Assert.Equal(0.6f, trace.OutputField68, 6);
        Assert.Equal(0.8f, trace.OutputField6c, 6);
        Assert.Equal(0.5f, trace.OutputField84, 6);
        Assert.Equal(2.0f, trace.CandidateLength2D, 6);
        Assert.Equal(2.0f, trace.OutputMagnitude, 6);
        Assert.Equal(2.0f, trace.OutputNextInputVector.Length(), 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate_LaterAttemptAbortsWhenDriftExceedsBothThresholds()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
            attemptIndex: 2u,
            normalizedInputDirection: new Vector3(1.0f, 0.0f, 0.0f),
            remainingMagnitude: 3.0f,
            lateralOffset: new Vector3(0.0f, 0.0f, 0.0f),
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(0.0f, 0.0f, 0.0f),
            originalHorizontalMagnitude: 1.0f,
            originalVerticalMagnitude: 1.0f,
            previousField68: 1.0f,
            previousField6c: 0.0f,
            previousField84: 1.0f,
            out GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind.AbortReset, kind);
        Assert.Equal(1u, trace.CheckedDriftThresholds);
        Assert.Equal(1u, trace.ExceededHorizontalDrift);
        Assert.Equal(1u, trace.ExceededVerticalAbortThreshold);
        Assert.Equal(0u, trace.UpdatedDirectionFields);
        Assert.Equal(3.0f, trace.CandidateDriftDistance2D, 6);
        Assert.Equal(3.0f, trace.CandidateVector.X, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate_LaterAttemptAcceptsAndZeroesField84WhenAlignmentTurnsNegative()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
            attemptIndex: 2u,
            normalizedInputDirection: new Vector3(1.0f, 0.0f, 0.0f),
            remainingMagnitude: 3.0f,
            lateralOffset: new Vector3(0.0f, 0.0f, 0.0f),
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(0.0f, 0.0f, 0.0f),
            originalHorizontalMagnitude: 1.0f,
            originalVerticalMagnitude: 4.0f,
            previousField68: -1.0f,
            previousField6c: 0.0f,
            previousField84: 2.0f,
            out GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind.AcceptCandidate, kind);
        Assert.Equal(1u, trace.CheckedDriftThresholds);
        Assert.Equal(1u, trace.ExceededHorizontalDrift);
        Assert.Equal(0u, trace.ExceededVerticalAbortThreshold);
        Assert.Equal(1u, trace.ZeroedField84);
        Assert.Equal(1u, trace.UpdatedDirectionFields);
        Assert.Equal(1.0f, trace.OutputField68, 6);
        Assert.Equal(0.0f, trace.OutputField6c, 6);
        Assert.Equal(0.0f, trace.OutputField84, 6);
        Assert.Equal(3.0f, trace.OutputMagnitude, 6);
    }
}
