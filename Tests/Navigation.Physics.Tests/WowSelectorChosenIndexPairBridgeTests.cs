using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairBridgeTests
{
    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_DirectPathLoadsSelectedContactAndDirectPair()
    {
        Triangle[] selectedTriangles =
        {
            new(
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)),
        };
        Vector3[] contactNormals = { new(0.0f, 0.8f, 0.6f) };
        SelectorPair[] directPairs = { new() { First = 7.0f, Second = 8.0f } };
        SelectorSupportPlane[] candidatePlanes =
        {
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
            new() { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 0.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles,
            contactNormals,
            selectedTriangles.Length,
            directPairs,
            directPairs.Length,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.LoadedSelectedContact);
        Assert.Equal(1u, trace.LoadedDirectPair);
        Assert.Equal(1u, trace.NegativeDiagonalCandidateFound);
        Assert.Equal(0u, trace.UnitZCandidateFound);
        Assert.Equal(1u, trace.CurrentPositionInsidePrism);
        Assert.Equal(1u, trace.ProjectedPositionInsidePrism);
        Assert.Equal(1u, trace.ThresholdSensitive);
        Assert.Equal(SelectorPairSource.Direct, trace.PairSource);
        Assert.Equal(1u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(7.0f, outputPair.First, 6);
        Assert.Equal(8.0f, outputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_DirectGateWithoutNegativeDiagonalReturnsZeroPair()
    {
        Triangle[] selectedTriangles =
        {
            new(
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)),
        };
        Vector3[] contactNormals = { new(0.0f, 0.8f, 0.6f) };
        SelectorPair[] directPairs = { new() { First = 7.0f, Second = 8.0f } };
        SelectorSupportPlane[] candidatePlanes =
        {
            new() { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 0.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles,
            contactNormals,
            selectedTriangles.Length,
            directPairs,
            directPairs.Length,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.LoadedSelectedContact);
        Assert.Equal(1u, trace.LoadedDirectPair);
        Assert.Equal(0u, trace.NegativeDiagonalCandidateFound);
        Assert.Equal(SelectorPairSource.DirectZero, trace.PairSource);
        Assert.Equal(1u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_AlternateUnitZPathScansUnitZCandidate()
    {
        Triangle[] selectedTriangles =
        {
            new(
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)),
        };
        Vector3[] contactNormals = { new(0.0f, 0.8f, 0.6f) };
        SelectorPair[] directPairs = { new() { First = 7.0f, Second = 8.0f } };
        SelectorSupportPlane[] candidatePlanes =
        {
            new() { Normal = new Vector3(0.0f, 0.0f, 1.0f), PlaneDistance = 0.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles,
            contactNormals,
            selectedTriangles.Length,
            directPairs,
            directPairs.Length,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(1.0f, 1.0f, -0.25f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            airborneTimeScalar: 2.0f,
            elapsedTimeScalar: 1.5f,
            horizontalSpeedScale: 4.0f,
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.LoadedSelectedContact);
        Assert.Equal(0u, trace.NegativeDiagonalCandidateFound);
        Assert.Equal(1u, trace.UnitZCandidateFound);
        Assert.Equal(1u, trace.AlternateUnitZFallbackGateAccepted);
        Assert.Equal(SelectorPairSource.AlternateUnitZZero, trace.PairSource);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(1u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_DirectRejectFallsBackToAlternatePair()
    {
        Triangle[] selectedTriangles =
        {
            new(
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)),
        };
        Vector3[] contactNormals = { new(0.0f, 0.8f, 0.6f) };
        SelectorPair[] directPairs = { new() { First = 7.0f, Second = 8.0f } };
        SelectorSupportPlane[] candidatePlanes =
        {
            new() { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 0.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles,
            contactNormals,
            selectedTriangles.Length,
            directPairs,
            directPairs.Length,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(1.0f, 1.0f, 0.25f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: 0,
            airborneTimeScalar: 2.0f,
            elapsedTimeScalar: 1.5f,
            horizontalSpeedScale: 0.5f,
            alternatePair: new SelectorPair { First = 9.0f, Second = 10.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(SelectorPairSource.Alternate, trace.PairSource);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(9.0f, outputPair.First, 6);
        Assert.Equal(10.0f, outputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_SentinelSelectedIndexPreservesInputMoveAndZeroesPair()
    {
        Triangle[] selectedTriangles =
        {
            new(
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)),
        };
        Vector3[] contactNormals = { new(0.0f, 0.8f, 0.6f) };
        SelectorPair[] directPairs = { new() { First = 7.0f, Second = 8.0f } };
        SelectorSupportPlane[] candidatePlanes =
        {
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
            new() { Normal = new Vector3(0.0f, 0.0f, 1.0f), PlaneDistance = 0.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles,
            contactNormals,
            selectedTriangles.Length,
            directPairs,
            directPairs.Length,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.5f,
            inputMove: new Vector3(0.25f, 0.75f, 0.5f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: true,
            selectedIndex: selectedTriangles.Length,
            airborneTimeScalar: 2.0f,
            elapsedTimeScalar: 1.5f,
            horizontalSpeedScale: 4.0f,
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(0, returnCode);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.LoadedSelectedContact);
        Assert.Equal(0u, trace.LoadedDirectPair);
        Assert.Equal(SelectorPairSource.PreservedInput, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.PreservedInputMove);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_RankingFailureReturnsTwoAndZeroesMove()
    {
        Triangle[] selectedTriangles =
        {
            new(
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)),
        };
        Vector3[] contactNormals = { new(0.0f, 0.8f, 0.6f) };
        SelectorPair[] directPairs = { new() { First = 7.0f, Second = 8.0f } };
        SelectorSupportPlane[] candidatePlanes =
        {
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles,
            contactNormals,
            selectedTriangles.Length,
            directPairs,
            directPairs.Length,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.25f, -0.125f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: false,
            selectedIndex: 0,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(2, returnCode);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.LoadedSelectedContact);
        Assert.Equal(1u, trace.LoadedDirectPair);
        Assert.Equal(SelectorPairSource.RankingFailure, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.ZeroedMoveOnRankingFailure);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorChosenIndexPairBridge_InRangeIndexWithNullSelectedContactsSkipsContactLoadAndStillUsesRankingFailure()
    {
        Vector3[] contactNormals =
        {
            new(0.0f, 0.8f, 0.6f),
        };
        SelectorPair[] directPairs =
        {
            new() { First = 7.0f, Second = 8.0f },
        };

        int returnCode = EvaluateWoWSelectorChosenIndexPairBridge(
            selectedTriangles: null,
            contactNormals,
            selectedContactCount: 1,
            directPairs,
            directPairs.Length,
            candidatePlanes: new SelectorSupportPlane[0],
            candidatePlaneCount: 0,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 2.0f,
            inputMove: new Vector3(0.5f, 0.25f, -0.125f),
            useStandardWalkableThreshold: true,
            directionRankingAccepted: false,
            selectedIndex: 0,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 1.0f, Second = 2.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            trace: out SelectorChosenIndexPairBridgeTrace trace);

        Assert.Equal(2, returnCode);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.LoadedSelectedContact);
        Assert.Equal(1u, trace.LoadedDirectPair);
        Assert.Equal(SelectorPairSource.RankingFailure, trace.PairSource);
        Assert.Equal(1u, trace.ConsumerTrace.ZeroedMoveOnRankingFailure);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }
}
