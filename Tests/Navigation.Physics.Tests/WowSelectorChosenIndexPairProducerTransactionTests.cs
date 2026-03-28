using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairProducerTransactionTests
{
    private const float DirectThresholdNormalZ = 0.6427876353263855f;

    [Fact]
    public void ProducerTransaction_NoOverrideCacheMissCopiesWalkablesAndReturnsDirectPair()
    {
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
        SelectorSupportPlane[] candidatePlanes =
        [
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
        ];

        int returnCode = EvaluateWoWSelectorChosenIndexPairProducerTransaction(
            defaultPosition: new Vector3(10.0f, 20.0f, 30.0f),
            overridePosition: null,
            projectedPosition: new Vector3(10.0f, 20.0f, 30.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(0.0f, 0.0f, -1.0f),
            candidateDirection: new Vector3(0.0f, 0.0f, -1.0f),
            initialBestRatio: 1.0f,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin: new Vector3(9.666667f, 19.666666f, 30.0f),
            cachedBoundsMax: new Vector3(10.333333f, 20.333334f, 32.0f),
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
            rankingAccepted: true,
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 1,
            rankingReportedBestRatio: 0.001f,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 12.0f, Second = 13.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairProducerTransactionTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(1u, trace.VariableTrace.TerrainQueryInvoked);
        Assert.Equal(1u, trace.ContainerInvoked);
        Assert.Equal(1u, trace.UsedProducedSelectedContactContainer);
        Assert.Equal(0u, trace.UsedAmbientCachedContainerWithoutQuery);
        Assert.Equal(1u, trace.ContainerTrace.CopiedQueryResults);
        Assert.Equal(2u, trace.ContainerTrace.OutputContactCount);
        Assert.Equal(2u, trace.BridgeSelectedContactCount);
        Assert.Equal(1u, trace.BridgeTrace.LoadedSelectedContact);
        Assert.Equal(1u, trace.BridgeTrace.LoadedDirectPair);
        Assert.Equal(SelectorPairSource.Direct, trace.BridgeTrace.PairSource);
        Assert.Equal(0.0f, reportedBestRatio, 6);
        Assert.Equal(1u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(9.0f, outputPair.First, 6);
        Assert.Equal(10.0f, outputPair.Second, 6);
    }

    [Fact]
    public void ProducerTransaction_NoOverrideCacheHitReusesExistingContainer()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 42u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 3.0f, Second = 4.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 8.0f, Second = 9.0f }];
        SelectorSupportPlane[] candidatePlanes =
        [
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
        ];

        int returnCode = EvaluateWoWSelectorChosenIndexPairProducerTransaction(
            defaultPosition: new Vector3(3.0f, 4.0f, 5.0f),
            overridePosition: null,
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(0.0f, 0.0f, -1.0f),
            candidateDirection: new Vector3(0.0f, 0.0f, -1.0f),
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin: new Vector3(2.5f, 3.5f, 5.0f),
            cachedBoundsMax: new Vector3(3.5f, 4.5f, 7.0f),
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
            rankingAccepted: true,
            rankingCandidateCount: 1u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.75f,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 12.0f, Second = 13.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairProducerTransactionTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(1u, trace.ContainerInvoked);
        Assert.Equal(1u, trace.UsedProducedSelectedContactContainer);
        Assert.Equal(1u, trace.ContainerTrace.ReusedExistingContainer);
        Assert.Equal(0u, trace.ContainerTrace.CopiedQueryResults);
        Assert.Equal(1u, trace.BridgeTrace.LoadedDirectPair);
        Assert.Equal(SelectorPairSource.Direct, trace.BridgeTrace.PairSource);
        Assert.Equal(0.75f, reportedBestRatio, 6);
        Assert.Equal(1u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(3.0f, outputPair.First, 6);
        Assert.Equal(4.0f, outputPair.Second, 6);
    }

    [Fact]
    public void ProducerTransaction_OverrideBypassesTerrainQueryAndUsesAmbientCachedContainer()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 77u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 7.0f, Second = 8.0f }];
        SelectorSupportPlane[] candidatePlanes =
        [
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
        ];

        int returnCode = EvaluateWoWSelectorChosenIndexPairProducerTransaction(
            defaultPosition: new Vector3(1.0f, 2.0f, 3.0f),
            overridePosition: new Vector3(-4.0f, 5.5f, 6.5f),
            projectedPosition: new Vector3(7.0f, 8.0f, 9.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(0.0f, 0.0f, -1.0f),
            candidateDirection: new Vector3(0.0f, 0.0f, -1.0f),
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            cachedBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            Array.Empty<TerrainAabbContact>(),
            Array.Empty<SelectorPair>(),
            0,
            queryDispatchSucceeded: false,
            rankingAccepted: true,
            rankingCandidateCount: 1u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.25f,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 11.0f, Second = 12.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairProducerTransactionTrace trace);

        Assert.Equal(1, returnCode);
        Assert.Equal(1u, trace.VariableTrace.UsedOverridePosition);
        Assert.Equal(0u, trace.VariableTrace.TerrainQueryInvoked);
        Assert.Equal(0u, trace.ContainerInvoked);
        Assert.Equal(1u, trace.UsedAmbientCachedContainerWithoutQuery);
        Assert.Equal(0u, trace.UsedProducedSelectedContactContainer);
        Assert.Equal(1u, trace.BridgeSelectedContactCount);
        Assert.Equal(SelectorPairSource.Direct, trace.BridgeTrace.PairSource);
        Assert.Equal(0.25f, reportedBestRatio, 6);
        Assert.Equal(1u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(7.0f, outputPair.First, 6);
        Assert.Equal(8.0f, outputPair.Second, 6);
    }

    [Fact]
    public void ProducerTransaction_QueryFailureZeroesOutputsBeforeContainerOrBridge()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.0f, Second = 10.0f }];

        int returnCode = EvaluateWoWSelectorChosenIndexPairProducerTransaction(
            defaultPosition: new Vector3(10.0f, 20.0f, 30.0f),
            overridePosition: null,
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(0.0f, 0.0f, -1.0f),
            candidateDirection: new Vector3(0.0f, 0.0f, -1.0f),
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
            rankingAccepted: true,
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.75f,
            candidatePlanes: Array.Empty<SelectorSupportPlane>(),
            candidatePlaneCount: 0,
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
            trace: out SelectorChosenIndexPairProducerTransactionTrace trace);

        Assert.Equal(0, returnCode);
        Assert.Equal(1u, trace.VariableTrace.QueryFailureZeroedOutput);
        Assert.Equal(1u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.ContainerInvoked);
        Assert.Equal(0u, trace.BridgeInvoked);
        Assert.Equal(0.0f, reportedBestRatio, 6);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

    [Fact]
    public void ProducerTransaction_RankingRejectedStillFlowsIntoPairConsumerFailure()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 55u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 4.0f, Second = 5.0f }];
        SelectorSupportPlane[] candidatePlanes =
        [
            new() { Normal = new Vector3(0.0f, 0.0f, -0.4756366014f), PlaneDistance = 0.0f },
        ];

        int returnCode = EvaluateWoWSelectorChosenIndexPairProducerTransaction(
            defaultPosition: new Vector3(1.0f, 2.0f, 3.0f),
            overridePosition: new Vector3(-4.0f, 5.5f, 6.5f),
            projectedPosition: new Vector3(7.0f, 8.0f, 9.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(0.0f, 0.0f, -1.0f),
            candidateDirection: new Vector3(0.0f, 0.0f, -1.0f),
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            cachedBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            Array.Empty<TerrainAabbContact>(),
            Array.Empty<SelectorPair>(),
            0,
            queryDispatchSucceeded: false,
            rankingAccepted: false,
            rankingCandidateCount: 1u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.25f,
            candidatePlanes,
            candidatePlanes.Length,
            currentPosition: new Vector3(0.2f, 0.2f, 0.0f),
            requestedDistance: 1.0f,
            inputMove: new Vector3(0.1f, 0.0f, 0.0f),
            useStandardWalkableThreshold: true,
            airborneTimeScalar: 0.0f,
            elapsedTimeScalar: 0.0f,
            horizontalSpeedScale: 0.0f,
            alternatePair: new SelectorPair { First = 11.0f, Second = 12.0f },
            outputPair: out SelectorPair outputPair,
            directStateDword: out uint directStateDword,
            alternateUnitZStateDword: out uint alternateUnitZStateDword,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairProducerTransactionTrace trace);

        Assert.Equal(2, returnCode);
        Assert.Equal(1u, trace.BridgeInvoked);
        Assert.Equal(SelectorPairSource.RankingFailure, trace.BridgeTrace.PairSource);
        Assert.Equal(1u, trace.BridgeTrace.ConsumerTrace.ZeroedMoveOnRankingFailure);
        Assert.Equal(0.25f, reportedBestRatio, 6);
        Assert.Equal(0u, directStateDword);
        Assert.Equal(0u, alternateUnitZStateDword);
        Assert.Equal(0.0f, outputPair.First, 6);
        Assert.Equal(0.0f, outputPair.Second, 6);
    }

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
