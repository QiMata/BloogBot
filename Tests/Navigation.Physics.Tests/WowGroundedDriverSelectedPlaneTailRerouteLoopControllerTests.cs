using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailRerouteLoopControllerTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController_FirstAttemptAcceptsCandidateAndSurfacesWritebacks()
    {
        Vector3 normalizedInputDirection = new(0.6f, 0.8f, 0.0f);
        Vector3 lateralOffset = new(0.5f, -0.25f, 0.0f);

        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController(
            attemptIndex: 1u,
            incrementAttemptBeforeProbe: 0u,
            normalizedInputDirection,
            remainingMagnitude: 2.0f,
            currentHorizontalMagnitude: 2.0f,
            lateralOffset,
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(1.0f, 1.0f, 0.0f),
            originalHorizontalMagnitude: 10.0f,
            originalVerticalMagnitude: 10.0f,
            previousField68: 0.6f,
            previousField6c: 0.8f,
            previousField84: 0.5f,
            verticalFallbackAlreadyUsed: 0u,
            out GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailRerouteLoopControllerKind.AcceptCandidate, kind);
        Assert.Equal(1u, trace.InvokedCandidateProbe);
        Assert.Equal(0u, trace.InvokedVerticalFallback);
        Assert.Equal(0u, trace.InvokedResetStateHandler);
        Assert.Equal(1u, trace.RerouteLoopUsed);
        Assert.Equal(1u, trace.OutputAttemptIndex);
        Assert.Equal(trace.CandidateTrace.OutputNextInputVector.X, trace.OutputNextInputVector.X, 6);
        Assert.Equal(trace.CandidateTrace.OutputNextInputVector.Y, trace.OutputNextInputVector.Y, 6);
        Assert.Equal(trace.CandidateTrace.OutputMagnitude, trace.OutputMagnitude, 6);
        Assert.Equal(trace.CandidateTrace.OutputField68, trace.OutputField68, 6);
        Assert.Equal(trace.CandidateTrace.OutputField6c, trace.OutputField6c, 6);
        Assert.Equal(trace.CandidateTrace.OutputField84, trace.OutputField84, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController_LaterAttemptAbortTriggersReset()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController(
            attemptIndex: 2u,
            incrementAttemptBeforeProbe: 0u,
            normalizedInputDirection: new Vector3(1.0f, 0.0f, 0.0f),
            remainingMagnitude: 3.0f,
            currentHorizontalMagnitude: 3.0f,
            lateralOffset: new Vector3(0.0f, 0.0f, 0.0f),
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(0.0f, 0.0f, 0.0f),
            originalHorizontalMagnitude: 1.0f,
            originalVerticalMagnitude: 1.0f,
            previousField68: 1.0f,
            previousField6c: 0.0f,
            previousField84: 1.0f,
            verticalFallbackAlreadyUsed: 0u,
            out GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailRerouteLoopControllerKind.ResetState, kind);
        Assert.Equal(1u, trace.InvokedCandidateProbe);
        Assert.Equal(1u, trace.InvokedResetStateHandler);
        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind.AbortReset, (uint)trace.CandidateTrace.DispatchKind);
        Assert.Equal(1u, trace.CandidateTrace.ExceededHorizontalDrift);
        Assert.Equal(1u, trace.CandidateTrace.ExceededVerticalAbortThreshold);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController_ExceededAttemptBudgetUsesVerticalFallback()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController(
            attemptIndex: 5u,
            incrementAttemptBeforeProbe: 1u,
            normalizedInputDirection: new Vector3(0.3f, 0.4f, -0.5f),
            remainingMagnitude: 2.0f,
            currentHorizontalMagnitude: 0.25f,
            lateralOffset: new Vector3(0.0f, 0.0f, 0.0f),
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(0.0f, 0.0f, 0.0f),
            originalHorizontalMagnitude: 0.0f,
            originalVerticalMagnitude: 0.0f,
            previousField68: 0.0f,
            previousField6c: 0.0f,
            previousField84: 1.0f,
            verticalFallbackAlreadyUsed: 0u,
            out GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailRerouteLoopControllerKind.UseVerticalFallback, kind);
        Assert.Equal(1u, trace.IncrementedAttemptBeforeProbe);
        Assert.Equal(1u, trace.AttemptLimitExceeded);
        Assert.Equal(1u, trace.InvokedVerticalFallback);
        Assert.Equal(1u, trace.OutputVerticalFallbackUsed);
        Assert.Equal(0u, trace.OutputAttemptIndex);
        Assert.Equal(0.0f, trace.OutputNextInputVector.X, 6);
        Assert.Equal(0.0f, trace.OutputNextInputVector.Y, 6);
        Assert.Equal(-1.0f, trace.OutputNextInputVector.Z, 6);
        Assert.Equal(0.0f, trace.OutputField84, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController_ExceededAttemptBudgetWithoutFallbackResetsState()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController(
            attemptIndex: 5u,
            incrementAttemptBeforeProbe: 1u,
            normalizedInputDirection: new Vector3(0.0f, 0.0f, 1.0f),
            remainingMagnitude: 2.0f,
            currentHorizontalMagnitude: 0.25f,
            lateralOffset: new Vector3(0.0f, 0.0f, 0.0f),
            originalPosition: new Vector3(0.0f, 0.0f, 0.0f),
            currentPosition: new Vector3(0.0f, 0.0f, 0.0f),
            originalHorizontalMagnitude: 0.0f,
            originalVerticalMagnitude: 0.0f,
            previousField68: 0.0f,
            previousField6c: 0.0f,
            previousField84: 1.0f,
            verticalFallbackAlreadyUsed: 1u,
            out GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneTailRerouteLoopControllerKind.ResetState, kind);
        Assert.Equal(1u, trace.AttemptLimitExceeded);
        Assert.Equal(1u, trace.InvokedVerticalFallback);
        Assert.Equal(1u, trace.InvokedResetStateHandler);
        Assert.Equal((uint)GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind.RejectNoFallback, (uint)trace.VerticalFallbackTrace.DispatchKind);
        Assert.Equal(1u, trace.RerouteLoopUsed);
        Assert.Equal(6u, trace.OutputAttemptIndex);
    }
}
