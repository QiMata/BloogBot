using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverHoverRerankDispatchTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverHoverRerankDispatch_FirstRerankFailureReturnsWithoutCommit()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverHoverRerankDispatch(
            firstRerankSucceeded: 0u,
            selectedIndex: 2u,
            selectedCount: 5u,
            useStandardWalkableThreshold: 1u,
            selectedNormalZ: 0.9f,
            selectedPair: new SelectorPair { First = 3.0f, Second = 4.0f },
            inputWindowSpanScalar: 1.25f,
            followupScalarCandidate: 0.5f,
            secondRerankSucceeded: 1u,
            movementFlags: (uint)MoveFlags.Hover,
            positionZ: 10.0f,
            inputFallTime: 14u,
            inputFallStartZ: 8.0f,
            inputVerticalSpeed: -2.0f,
            out GroundedDriverHoverRerankTrace trace);

        Assert.Equal((uint)GroundedDriverHoverRerankDispatchKind.ReturnWithoutCommit, dispatchKind);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.ForwardedPair);
        Assert.Equal(0u, trace.StartedFallWithZeroVelocity);
        Assert.Equal(0u, trace.CalledSecondRerank);
        Assert.Equal(0.0f, trace.OutputPair.First, 6);
        Assert.Equal(0.0f, trace.OutputPair.Second, 6);
        Assert.Equal(10.0f, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverHoverRerankDispatch_RelaxedThresholdEqualRejectsAndStartsFallZero()
    {
        const float relaxedThreshold = 0.1736481785774231f;

        uint dispatchKind = EvaluateWoWGroundedDriverHoverRerankDispatch(
            firstRerankSucceeded: 1u,
            selectedIndex: 1u,
            selectedCount: 5u,
            useStandardWalkableThreshold: 0u,
            selectedNormalZ: relaxedThreshold,
            selectedPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            inputWindowSpanScalar: 1.25f,
            followupScalarCandidate: 0.75f,
            secondRerankSucceeded: 1u,
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.Hover | MoveFlags.SplineElevation | MoveFlags.Swimming),
            positionZ: 12.0f,
            inputFallTime: 31u,
            inputFallStartZ: 9.0f,
            inputVerticalSpeed: -6.0f,
            out GroundedDriverHoverRerankTrace trace);

        Assert.Equal((uint)GroundedDriverHoverRerankDispatchKind.StartFallZero, dispatchKind);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(relaxedThreshold, trace.ThresholdNormalZ, 6);
        Assert.Equal(0u, trace.SelectedNormalAccepted);
        Assert.Equal(0u, trace.LoadedSelectedPair);
        Assert.Equal(1u, trace.StartedFallWithZeroVelocity);
        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.Hover | MoveFlags.Jumping), trace.OutputMovementFlags);
        Assert.Equal(0u, trace.OutputFallTime);
        Assert.Equal(12.0f, trace.OutputFallStartZ, 6);
        Assert.Equal(0.0f, trace.OutputVerticalSpeed, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverHoverRerankDispatch_WindowAboveOneForwardsSelectedPairWithoutSecondRerank()
    {
        SelectorPair selectedPair = new() { First = 5.0f, Second = 6.0f };

        uint dispatchKind = EvaluateWoWGroundedDriverHoverRerankDispatch(
            firstRerankSucceeded: 1u,
            selectedIndex: 2u,
            selectedCount: 5u,
            useStandardWalkableThreshold: 1u,
            selectedNormalZ: 0.8f,
            selectedPair: selectedPair,
            inputWindowSpanScalar: 1.25f,
            followupScalarCandidate: 0.5f,
            secondRerankSucceeded: 0u,
            movementFlags: (uint)MoveFlags.Hover,
            positionZ: 4.0f,
            inputFallTime: 7u,
            inputFallStartZ: 3.0f,
            inputVerticalSpeed: -1.0f,
            out GroundedDriverHoverRerankTrace trace);

        Assert.Equal((uint)GroundedDriverHoverRerankDispatchKind.ForwardPair, dispatchKind);
        Assert.Equal(1u, trace.SelectedNormalAccepted);
        Assert.Equal(1u, trace.LoadedSelectedPair);
        Assert.Equal(1u, trace.UsedDirectForwardAboveOne);
        Assert.Equal(0u, trace.CalledSecondRerank);
        Assert.Equal(1u, trace.ForwardedPair);
        Assert.Equal(selectedPair.First, trace.OutputPair.First, 6);
        Assert.Equal(selectedPair.Second, trace.OutputPair.Second, 6);
        Assert.Equal(4.0f, trace.OutputPositionZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverHoverRerankDispatch_OutOfRangeIndexClampsFollowupScalarAndAdvancesPosition()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverHoverRerankDispatch(
            firstRerankSucceeded: 1u,
            selectedIndex: 5u,
            selectedCount: 5u,
            useStandardWalkableThreshold: 1u,
            selectedNormalZ: 0.0f,
            selectedPair: new SelectorPair { First = 11.0f, Second = 12.0f },
            inputWindowSpanScalar: 0.75f,
            followupScalarCandidate: 0.6f,
            secondRerankSucceeded: 1u,
            movementFlags: (uint)MoveFlags.Hover,
            positionZ: 20.0f,
            inputFallTime: 1u,
            inputFallStartZ: 2.0f,
            inputVerticalSpeed: -3.0f,
            out GroundedDriverHoverRerankTrace trace);

        Assert.Equal((uint)GroundedDriverHoverRerankDispatchKind.ForwardPair, dispatchKind);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.LoadedSelectedPair);
        Assert.Equal(1u, trace.ClampedFollowupScalar);
        Assert.Equal(0.25f, trace.OutputFollowupScalar, 6);
        Assert.Equal(1u, trace.CalledSecondRerank);
        Assert.Equal(1u, trace.SecondRerankSucceeded);
        Assert.Equal(1u, trace.ForwardedPair);
        Assert.Equal(1u, trace.AdvancedPositionZ);
        Assert.Equal(20.25f, trace.OutputPositionZ, 6);
        Assert.Equal(0.0f, trace.OutputPair.First, 6);
        Assert.Equal(0.0f, trace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverHoverRerankDispatch_NonPositiveFollowupScalarZeroesBeforeSecondRerank()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverHoverRerankDispatch(
            firstRerankSucceeded: 1u,
            selectedIndex: 0u,
            selectedCount: 3u,
            useStandardWalkableThreshold: 1u,
            selectedNormalZ: 0.9f,
            selectedPair: new SelectorPair { First = 13.0f, Second = 14.0f },
            inputWindowSpanScalar: 0.5f,
            followupScalarCandidate: -0.2f,
            secondRerankSucceeded: 1u,
            movementFlags: (uint)MoveFlags.Hover,
            positionZ: 6.0f,
            inputFallTime: 5u,
            inputFallStartZ: 4.0f,
            inputVerticalSpeed: -2.0f,
            out GroundedDriverHoverRerankTrace trace);

        Assert.Equal((uint)GroundedDriverHoverRerankDispatchKind.ForwardPair, dispatchKind);
        Assert.Equal(1u, trace.ZeroedFollowupScalar);
        Assert.Equal(0u, trace.ClampedFollowupScalar);
        Assert.Equal(0.0f, trace.OutputFollowupScalar, 6);
        Assert.Equal(1u, trace.CalledSecondRerank);
        Assert.Equal(1u, trace.SecondRerankSucceeded);
        Assert.Equal(1u, trace.AdvancedPositionZ);
        Assert.Equal(6.0f, trace.OutputPositionZ, 6);
        Assert.Equal(13.0f, trace.OutputPair.First, 6);
        Assert.Equal(14.0f, trace.OutputPair.Second, 6);
    }
}
