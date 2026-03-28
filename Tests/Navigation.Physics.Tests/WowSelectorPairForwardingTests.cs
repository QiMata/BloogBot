using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPairForwardingTests
{
    [Fact]
    public void EvaluateWoWSelectorChosenPairForwarding_DirectGateReturnsSelectedPair()
    {
        Triangle triangle = new(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));

        Assert.True(EvaluateWoWSelectorChosenPairForwarding(
            triangle,
            contactNormal: new Vector3(0.0f, 0.8f, 0.6f),
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            hasNegativeDiagonalCandidate: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            hasUnitZCandidate: false,
            directPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            trace: out SelectorPairForwardingTrace trace));

        Assert.Equal(1u, trace.DirectGateAccepted);
        Assert.Equal(1u, trace.CurrentPositionInsidePrism);
        Assert.Equal(1u, trace.ProjectedPositionInsidePrism);
        Assert.Equal(1u, trace.ThresholdSensitive);
        Assert.Equal(SelectorPairSource.Direct, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.DirectGateState);
        Assert.Equal(1u, trace.ConsumerTrace.ReturnedDirectPair);
        Assert.Equal(7.0f, trace.ConsumerTrace.OutputPair.First, 6);
        Assert.Equal(8.0f, trace.ConsumerTrace.OutputPair.Second, 6);
        Assert.Equal(0.2f, trace.ConsumerTrace.OutputMove.X, 6);
        Assert.Equal(0.0f, trace.ConsumerTrace.OutputMove.Y, 6);
        Assert.Equal(0.0f, trace.ConsumerTrace.OutputMove.Z, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenPairForwarding_DirectGateWithoutNegativeDiagonalReturnsZeroPair()
    {
        Triangle triangle = new(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));

        Assert.True(EvaluateWoWSelectorChosenPairForwarding(
            triangle,
            contactNormal: new Vector3(0.0f, 0.8f, 0.6f),
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            hasNegativeDiagonalCandidate: false,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            hasUnitZCandidate: false,
            directPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            trace: out SelectorPairForwardingTrace trace));

        Assert.Equal(1u, trace.DirectGateAccepted);
        Assert.Equal(SelectorPairSource.DirectZero, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.DirectGateState);
        Assert.Equal(1u, trace.ConsumerTrace.ReturnedZeroPair);
        Assert.Equal(0f, trace.ConsumerTrace.OutputPair.First, 6);
        Assert.Equal(0f, trace.ConsumerTrace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenPairForwarding_AlternateUnitZPathReturnsZeroPair()
    {
        Triangle triangle = new(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));

        Assert.True(EvaluateWoWSelectorChosenPairForwarding(
            triangle,
            contactNormal: new Vector3(0.0f, 0.8f, 0.6f),
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(1.0f, 1.0f, -0.25f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            hasNegativeDiagonalCandidate: false,
            airborneTimeScalar: 2.0f,
            elapsedTimeScalar: 1.5f,
            horizontalSpeedScale: 4.0f,
            hasUnitZCandidate: true,
            directPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            trace: out SelectorPairForwardingTrace trace));

        Assert.Equal(0u, trace.DirectGateAccepted);
        Assert.Equal(1u, trace.AlternateUnitZFallbackGateAccepted);
        Assert.Equal(SelectorPairSource.AlternateUnitZZero, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.AlternateUnitZState);
        Assert.Equal(1u, trace.ConsumerTrace.ReturnedZeroPair);
        Assert.Equal(0f, trace.ConsumerTrace.OutputPair.First, 6);
        Assert.Equal(0f, trace.ConsumerTrace.OutputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenPairForwarding_DirectRejectFallsBackToAlternatePair()
    {
        Triangle triangle = new(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));

        Assert.True(EvaluateWoWSelectorChosenPairForwarding(
            triangle,
            contactNormal: new Vector3(0.0f, 0.8f, 0.6f),
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(1.0f, 1.0f, 0.25f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            selectedCount: 1,
            hasNegativeDiagonalCandidate: false,
            airborneTimeScalar: 2.0f,
            elapsedTimeScalar: 1.5f,
            horizontalSpeedScale: 0.5f,
            hasUnitZCandidate: true,
            directPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            alternatePair: new SelectorPair { First = 9.0f, Second = 10.0f },
            trace: out SelectorPairForwardingTrace trace));

        Assert.Equal(0u, trace.DirectGateAccepted);
        Assert.Equal(0u, trace.AlternateUnitZFallbackGateAccepted);
        Assert.Equal(SelectorPairSource.Alternate, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.ReturnedAlternatePair);
        Assert.Equal(9.0f, trace.ConsumerTrace.OutputPair.First, 6);
        Assert.Equal(10.0f, trace.ConsumerTrace.OutputPair.Second, 6);
    }
}
