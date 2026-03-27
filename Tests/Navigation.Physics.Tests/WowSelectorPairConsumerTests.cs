using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPairConsumerTests
{
    [Fact]
    public void EvaluateSelectorAlternateUnitZFallbackGate_RejectsWhenFallbackLimitExceedsRadius()
    {
        bool accepted = EvaluateWoWSelectorAlternateUnitZFallbackGate(
            boundingRadiusValue: 1.0f,
            fallbackLimit: 1.25f,
            horizontalSpeedScale: 7.0f,
            requestedDistance: 0.5f);

        Assert.False(accepted);
    }

    [Fact]
    public void EvaluateSelectorAlternateUnitZFallbackGate_RejectsWhenScaledWindowIsShorterThanRequestedDistance()
    {
        bool accepted = EvaluateWoWSelectorAlternateUnitZFallbackGate(
            boundingRadiusValue: 2.0f,
            fallbackLimit: 1.5f,
            horizontalSpeedScale: 4.0f,
            requestedDistance: 2.01f);

        Assert.False(accepted);
    }

    [Fact]
    public void EvaluateSelectorAlternateUnitZFallbackGate_AcceptsAtExactDistanceThreshold()
    {
        bool accepted = EvaluateWoWSelectorAlternateUnitZFallbackGate(
            boundingRadiusValue: 2.0f,
            fallbackLimit: 1.5f,
            horizontalSpeedScale: 4.0f,
            requestedDistance: 2.0f);

        Assert.True(accepted);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_ZeroDistanceReturnsZeroAndPreservesInputMove()
    {
        Vector3 inputMove = new(0.5f, -0.25f, 0.125f);

        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 0.0f,
            inputMove,
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            directGateAccepted: true,
            hasNegativeDiagonalCandidate: true,
            alternateUnitZFallbackGateAccepted: true,
            hasUnitZCandidate: true,
            directPair: new SelectorPair { First = 3.0f, Second = 4.0f },
            alternatePair: new SelectorPair { First = 5.0f, Second = 6.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(0, trace.ReturnCode);
        Assert.Equal(1u, trace.PreservedInputMove);
        Assert.Equal(inputMove.X, trace.OutputMove.X, 6);
        Assert.Equal(inputMove.Y, trace.OutputMove.Y, 6);
        Assert.Equal(inputMove.Z, trace.OutputMove.Z, 6);
        Assert.Equal(0f, trace.OutputPair.First, 6);
        Assert.Equal(0f, trace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_RankingFailureReturnsTwoAndZeroesMove()
    {
        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.25f, -0.125f),
            directionRankingAccepted: false,
            selectedIndex: 0,
            selectedCount: 1,
            directGateAccepted: false,
            hasNegativeDiagonalCandidate: false,
            alternateUnitZFallbackGateAccepted: false,
            hasUnitZCandidate: false,
            directPair: new SelectorPair { First = 1.0f, Second = 2.0f },
            alternatePair: new SelectorPair { First = 3.0f, Second = 4.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(2, trace.ReturnCode);
        Assert.Equal(1u, trace.ZeroedMoveOnRankingFailure);
        Assert.Equal(0f, trace.OutputMove.X, 6);
        Assert.Equal(0f, trace.OutputMove.Y, 6);
        Assert.Equal(0f, trace.OutputMove.Z, 6);
        Assert.Equal(0f, trace.OutputPair.First, 6);
        Assert.Equal(0f, trace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_SelectedIndexSentinelReturnsZeroAndPreservesInputMove()
    {
        Vector3 inputMove = new(0.25f, 0.75f, 0.5f);

        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 1.5f,
            inputMove,
            directionRankingAccepted: true,
            selectedIndex: 2,
            selectedCount: 2,
            directGateAccepted: false,
            hasNegativeDiagonalCandidate: false,
            alternateUnitZFallbackGateAccepted: true,
            hasUnitZCandidate: true,
            directPair: new SelectorPair { First = 1.0f, Second = 2.0f },
            alternatePair: new SelectorPair { First = 3.0f, Second = 4.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(0, trace.ReturnCode);
        Assert.Equal(1u, trace.PreservedInputMove);
        Assert.Equal(inputMove.X, trace.OutputMove.X, 6);
        Assert.Equal(inputMove.Y, trace.OutputMove.Y, 6);
        Assert.Equal(inputMove.Z, trace.OutputMove.Z, 6);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_DirectGateReturnsDirectPairAndSetsDirectState()
    {
        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.0f, -0.25f),
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            directGateAccepted: true,
            hasNegativeDiagonalCandidate: true,
            alternateUnitZFallbackGateAccepted: false,
            hasUnitZCandidate: false,
            directPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(1, trace.ReturnCode);
        Assert.Equal(1u, trace.DirectGateState);
        Assert.Equal(1u, trace.ReturnedDirectPair);
        Assert.Equal(7.0f, trace.OutputPair.First, 6);
        Assert.Equal(8.0f, trace.OutputPair.Second, 6);
        Assert.Equal(1.0f, trace.OutputMove.X, 6);
        Assert.Equal(0.0f, trace.OutputMove.Y, 6);
        Assert.Equal(-0.5f, trace.OutputMove.Z, 6);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_DirectGateWithoutNegativeDiagonalReturnsZeroPair()
    {
        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.0f, 0.25f),
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            directGateAccepted: true,
            hasNegativeDiagonalCandidate: false,
            alternateUnitZFallbackGateAccepted: false,
            hasUnitZCandidate: false,
            directPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(1, trace.ReturnCode);
        Assert.Equal(1u, trace.DirectGateState);
        Assert.Equal(1u, trace.ReturnedZeroPair);
        Assert.Equal(0f, trace.OutputPair.First, 6);
        Assert.Equal(0f, trace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_AlternateUnitZGateReturnsZeroPairAndSetsAlternateState()
    {
        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.25f, -0.5f),
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            directGateAccepted: false,
            hasNegativeDiagonalCandidate: false,
            alternateUnitZFallbackGateAccepted: true,
            hasUnitZCandidate: true,
            directPair: new SelectorPair { First = 1.0f, Second = 2.0f },
            alternatePair: new SelectorPair { First = 3.0f, Second = 4.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(1, trace.ReturnCode);
        Assert.Equal(1u, trace.AlternateUnitZState);
        Assert.Equal(1u, trace.ReturnedZeroPair);
        Assert.Equal(0f, trace.OutputPair.First, 6);
        Assert.Equal(0f, trace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateSelectorPairConsumer_AlternateFallbackReturnsAlternatePair()
    {
        bool evaluated = EvaluateWoWSelectorPairConsumer(
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.25f, 0.5f),
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            directGateAccepted: false,
            hasNegativeDiagonalCandidate: false,
            alternateUnitZFallbackGateAccepted: false,
            hasUnitZCandidate: false,
            directPair: new SelectorPair { First = 1.0f, Second = 2.0f },
            alternatePair: new SelectorPair { First = 9.0f, Second = 10.0f },
            out SelectorPairConsumerTrace trace);

        Assert.True(evaluated);
        Assert.Equal(1, trace.ReturnCode);
        Assert.Equal(1u, trace.ReturnedAlternatePair);
        Assert.Equal(9.0f, trace.OutputPair.First, 6);
        Assert.Equal(10.0f, trace.OutputPair.Second, 6);
    }
}
