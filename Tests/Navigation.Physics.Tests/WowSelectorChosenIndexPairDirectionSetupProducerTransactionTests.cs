using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairDirectionSetupProducerTransactionTests
{
    private const float DirectThresholdNormalZ = 0.6427876353263855f;

    [Fact]
    public void DirectionSetupProducerTransaction_ComposesDirectionSetupIntoInjectedProducerPath()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(new Vector3(-1.0f, 0.0f, 0.0f))
        ];
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts =
        [
            CreateContact(instanceId: 101u, normalZ: DirectThresholdNormalZ, walkable: true),
            CreateContact(instanceId: 202u, normalZ: 0.2f, walkable: false),
            CreateContact(instanceId: 303u, normalZ: DirectThresholdNormalZ, walkable: true),
        ];
        SelectorPair[] queryPairs =
        [
            new SelectorPair { First = 5.0f, Second = 6.0f },
            new SelectorPair { First = 7.0f, Second = 8.0f },
            new SelectorPair { First = 9.0f, Second = 10.0f },
        ];

        Vector3 defaultPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 projectedPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 testPoint = new(1.0f, 0.0f, 0.0f);
        Vector3 candidateDirection = new(-1.0f, 0.0f, 0.5f);
        Vector3 cachedBoundsMin = new(9.666667f, 19.666666f, 30.0f);
        Vector3 cachedBoundsMax = new(10.333334f, 20.333334f, 32.0f);

        bool variableSucceeded = EvaluateWoWSelectorTriangleSourceVariableTransaction(
            defaultPosition,
            overridePosition: null,
            projectedPosition,
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint,
            candidateDirection,
            initialBestRatio: 1.0f,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: true,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            queryDispatchSucceeded: true,
            rankingAccepted: true,
            rankingCandidateCount: 0u,
            rankingSelectedRecordIndex: -1,
            rankingReportedBestRatio: 1.0f,
            out SelectorTriangleSourceVariableTransactionTrace variableTrace);
        Assert.True(variableSucceeded);

        SelectorSupportPlane[] expectedCandidatePlanes = new SelectorSupportPlane[5];
        bool directionSetupSucceeded = EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
            records,
            defaultPosition,
            overridePosition: null,
            inputReportedBestRatioSeed: variableTrace.OutputReportedBestRatio,
            inputVerticalOffset: 0.75f,
            swimVerticalOffsetScale: 1.0f,
            selectorBaseMatchesSwimReference: false,
            movementFlags: 0u,
            requestedDistance: 0.5f,
            requestedDistanceClamp: 1.0f,
            testPoint,
            candidateDirection,
            horizontalRadius: 0.5f,
            outCandidatePlanes: expectedCandidatePlanes,
            out uint expectedCandidateCount,
            out int expectedSelectedRecordIndex,
            out float expectedReportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupTrace directionTrace);
        Assert.True(directionSetupSucceeded);

        int expectedReturnCode = EvaluateWoWSelectorChosenIndexPairProducerTransaction(
            defaultPosition,
            overridePosition: null,
            projectedPosition,
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint,
            candidateDirection,
            initialBestRatio: 1.0f,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: true,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            queryContacts,
            queryPairs,
            queryContacts.Length,
            queryDispatchSucceeded: true,
            rankingAccepted: directionTrace.RankingAccepted != 0u,
            rankingCandidateCount: expectedCandidateCount,
            rankingSelectedRecordIndex: expectedSelectedRecordIndex,
            rankingReportedBestRatio: expectedReportedBestRatio,
            candidatePlanes: expectedCandidatePlanes,
            candidatePlaneCount: (int)expectedCandidateCount,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 12.0f, Second = 13.0f },
            outputPair: out SelectorPair expectedPair,
            directStateDword: out uint expectedDirectState,
            alternateUnitZStateDword: out uint expectedAlternateUnitZState,
            reportedBestRatio: out float expectedOutputReportedBestRatio,
            trace: out SelectorChosenIndexPairProducerTransactionTrace injectedTrace);

        int actualReturnCode = EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransaction(
            records,
            defaultPosition,
            overridePosition: null,
            projectedPosition,
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint,
            candidateDirection,
            initialBestRatio: 1.0f,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: true,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            queryContacts,
            queryPairs,
            queryContacts.Length,
            queryDispatchSucceeded: true,
            inputVerticalOffset: 0.75f,
            swimVerticalOffsetScale: 1.0f,
            selectorBaseMatchesSwimReference: false,
            requestedDistanceClamp: 1.0f,
            horizontalRadius: 0.5f,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 12.0f, Second = 13.0f },
            outputPair: out SelectorPair actualPair,
            directStateDword: out uint actualDirectState,
            alternateUnitZStateDword: out uint actualAlternateUnitZState,
            reportedBestRatio: out float actualReportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupProducerTransactionTrace trace);

        Assert.Equal(expectedReturnCode, actualReturnCode);
        Assert.Equal(expectedPair.First, actualPair.First, 6);
        Assert.Equal(expectedPair.Second, actualPair.Second, 6);
        Assert.Equal(expectedDirectState, actualDirectState);
        Assert.Equal(expectedAlternateUnitZState, actualAlternateUnitZState);
        Assert.Equal(expectedOutputReportedBestRatio, actualReportedBestRatio, 6);
        Assert.Equal(directionTrace.RankingAccepted, trace.DirectionSetupTrace.RankingAccepted);
        Assert.Equal(directionTrace.OutputCandidateCount, trace.DirectionSetupTrace.OutputCandidateCount);
        Assert.Equal(directionTrace.OutputSelectedRecordIndex, trace.DirectionSetupTrace.OutputSelectedRecordIndex);
        Assert.Equal(injectedTrace.BridgeTrace.PairSource, trace.BridgeTrace.PairSource);
        Assert.Equal(injectedTrace.BridgeSelectedContactCount, trace.BridgeSelectedContactCount);
        Assert.Equal(1u, trace.ContainerInvoked);
        Assert.Equal(1u, trace.UsedProducedSelectedContactContainer);
    }

    [Fact]
    public void DirectionSetupProducerTransaction_QueryFailureZeroesOutputsBeforeDirectionSetupOrBridge()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(new Vector3(-1.0f, 0.0f, 0.0f))
        ];
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.0f, Second = 10.0f }];

        int returnCode = EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransaction(
            records,
            defaultPosition: new Vector3(10.0f, 20.0f, 30.0f),
            overridePosition: null,
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(1.0f, 0.0f, 0.0f),
            candidateDirection: new Vector3(-1.0f, 0.0f, 0.5f),
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin: new Vector3(2.0f, 3.4f, 4.5f),
            cachedBoundsMax: new Vector3(3.5f, 4.5f, 6.999f),
            modelPropertyFlagSet: false,
            movementFlags: 0x10000000u,
            field20Value: -0.5f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            queryContacts,
            queryPairs,
            queryContacts.Length,
            queryDispatchSucceeded: false,
            inputVerticalOffset: 0.75f,
            swimVerticalOffsetScale: 1.0f,
            selectorBaseMatchesSwimReference: false,
            requestedDistanceClamp: 1.0f,
            horizontalRadius: 0.5f,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 5.0f, Second = 6.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupProducerTransactionTrace trace);

        Assert.Equal(0, returnCode);
        Assert.Equal(1u, trace.VariableTrace.QueryFailureZeroedOutput);
        Assert.Equal(1u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.DirectionSetupTrace.RankingInvoked);
        Assert.Equal(0u, trace.ContainerInvoked);
        Assert.Equal(0u, trace.BridgeInvoked);
        Assert.Equal(0.0f, reportedBestRatio, 6);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

    private static SelectorCandidateRecord CreateRecord(Vector3 filterNormal) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = filterNormal,
                PlaneDistance = 0f
            },
            Point0 = new Vector3(0.2f, 0f, 0f),
            Point1 = new Vector3(0.6f, 0.2f, 0f),
            Point2 = new Vector3(0.5f, -0.2f, 0f)
        };

    private static TerrainAabbContact CreateContact(uint instanceId, float normalZ, bool walkable)
    {
        Vector3 normal = new(0.0f, MathF.Sqrt(MathF.Max(0.0f, 1.0f - (normalZ * normalZ))), normalZ);
        return new TerrainAabbContact
        {
            Point = new Vector3(0.2f, 0.2f, 0.0f),
            Normal = normal,
            RawNormal = normal,
            TriangleA = new Vector3(0.0f, 0.0f, 0.0f),
            TriangleB = new Vector3(1.0f, 0.0f, 0.0f),
            TriangleC = new Vector3(0.0f, 1.0f, 0.0f),
            PlaneDistance = 0.0f,
            Distance = 0.0f,
            InstanceId = instanceId,
            SourceType = 1u,
            Walkable = walkable ? 1u : 0u,
        };
    }
}
