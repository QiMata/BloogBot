using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairPreBridgeTransactionTests
{
    private const float DirectThresholdNormalZ = 0.6427876353263855f;

    [Fact]
    public void PreBridgeTransaction_ComposesDirectionSetupAndSelectedContactContainerAndLoadsChosenPair()
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

        TerrainAabbContact[] expectedContacts = new TerrainAabbContact[4];
        SelectorPair[] expectedPairs = new SelectorPair[4];
        int expectedSelectedContactCount = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition: null,
            projectedPosition,
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
            expectedContacts,
            expectedPairs,
            expectedContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace containerTrace);

        SelectorSupportPlane[] actualCandidatePlanes = new SelectorSupportPlane[5];
        TerrainAabbContact[] actualContacts = new TerrainAabbContact[4];
        SelectorPair[] actualPairs = new SelectorPair[4];
        bool result = EvaluateWoWSelectorChosenIndexPairPreBridgeTransaction(
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
            requestedDistance: 0.5f,
            horizontalRadius: 0.5f,
            outCandidatePlanes: actualCandidatePlanes,
            out uint actualCandidateCount,
            out int actualSelectedRecordIndex,
            out uint actualDirectionRankingAccepted,
            out TerrainAabbContact actualChosenContact,
            out SelectorPair actualChosenPair,
            actualContacts,
            actualPairs,
            actualContacts.Length,
            out float actualReportedBestRatio,
            out SelectorChosenIndexPairPreBridgeTransactionTrace trace);

        Assert.True(result);
        Assert.Equal(expectedReportedBestRatio, actualReportedBestRatio, 6);
        Assert.Equal(directionTrace.RankingAccepted, actualDirectionRankingAccepted);
        Assert.Equal(expectedCandidateCount, actualCandidateCount);
        Assert.Equal(expectedSelectedRecordIndex, actualSelectedRecordIndex);
        Assert.Equal(expectedSelectedContactCount, (int)trace.OutputSelectedContactCount);
        Assert.Equal(containerTrace.OutputSelectedContactCount, trace.SelectedContactContainerTrace.OutputSelectedContactCount);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.LoadedChosenContact);
        Assert.Equal(0u, trace.LoadedChosenPair);
        Assert.Equal(0u, actualChosenContact.InstanceId);
        Assert.Equal(0.0f, actualChosenPair.First, 6);
        Assert.Equal(0.0f, actualChosenPair.Second, 6);
        Assert.Equal(expectedContacts[0].InstanceId, actualContacts[0].InstanceId);
        Assert.Equal(expectedPairs[1].Second, actualPairs[1].Second, 6);
        Assert.Equal(directionTrace.OutputCandidateCount, trace.OutputCandidatePlaneCount);
        Assert.Equal(directionTrace.OutputSelectedRecordIndex, trace.OutputSelectedRecordIndex);
    }

    [Fact]
    public void PreBridgeTransaction_QueryFailureZeroesOutputsBeforeDirectionSetupOrContainer()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(new Vector3(-1.0f, 0.0f, 0.0f))
        ];
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.0f, Second = 10.0f }];
        SelectorSupportPlane[] actualCandidatePlanes = new SelectorSupportPlane[5];
        TerrainAabbContact[] actualContacts = new TerrainAabbContact[2];
        SelectorPair[] actualPairs = new SelectorPair[2];

        bool result = EvaluateWoWSelectorChosenIndexPairPreBridgeTransaction(
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
            requestedDistance: 0.5f,
            horizontalRadius: 0.5f,
            outCandidatePlanes: actualCandidatePlanes,
            out uint actualCandidateCount,
            out int actualSelectedRecordIndex,
            out uint actualDirectionRankingAccepted,
            out TerrainAabbContact actualChosenContact,
            out SelectorPair actualChosenPair,
            actualContacts,
            actualPairs,
            actualContacts.Length,
            out float actualReportedBestRatio,
            out SelectorChosenIndexPairPreBridgeTransactionTrace trace);

        Assert.False(result);
        Assert.Equal(1u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.ZeroedOutputsOnDirectionSetupFailure);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(0u, actualCandidateCount);
        Assert.Equal(-1, actualSelectedRecordIndex);
        Assert.Equal(0u, actualDirectionRankingAccepted);
        Assert.Equal(0u, actualChosenContact.InstanceId);
        Assert.Equal(0.0f, actualChosenPair.First, 6);
        Assert.Equal(0.0f, actualReportedBestRatio, 6);
        Assert.Equal(0u, trace.DirectionSetupTrace.RankingInvoked);
        Assert.Equal(0u, trace.SelectedContactContainerTrace.ContainerInvoked);
    }

    [Fact]
    public void PreBridgeTransaction_InvalidCandidatePlaneBufferFailsAfterVariableBeforeContainer()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(new Vector3(-1.0f, 0.0f, 0.0f))
        ];
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 101u, normalZ: DirectThresholdNormalZ, walkable: true)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 5.0f, Second = 6.0f }];
        SelectorSupportPlane[] undersizedCandidatePlanes = new SelectorSupportPlane[4];
        TerrainAabbContact[] actualContacts = new TerrainAabbContact[2];
        SelectorPair[] actualPairs = new SelectorPair[2];

        bool result = EvaluateWoWSelectorChosenIndexPairPreBridgeTransaction(
            records,
            defaultPosition: new Vector3(10.0f, 20.0f, 30.0f),
            overridePosition: null,
            projectedPosition: new Vector3(10.0f, 20.0f, 30.0f),
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint: new Vector3(1.0f, 0.0f, 0.0f),
            candidateDirection: new Vector3(-1.0f, 0.0f, 0.5f),
            initialBestRatio: 1.0f,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin: new Vector3(9.666667f, 19.666666f, 30.0f),
            cachedBoundsMax: new Vector3(10.333334f, 20.333334f, 32.0f),
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
            requestedDistance: 0.5f,
            horizontalRadius: 0.5f,
            outCandidatePlanes: undersizedCandidatePlanes,
            out uint actualCandidateCount,
            out int actualSelectedRecordIndex,
            out uint actualDirectionRankingAccepted,
            out TerrainAabbContact actualChosenContact,
            out SelectorPair actualChosenPair,
            actualContacts,
            actualPairs,
            actualContacts.Length,
            out float actualReportedBestRatio,
            out SelectorChosenIndexPairPreBridgeTransactionTrace trace);

        Assert.False(result);
        Assert.Equal(1u, trace.VariableTrace.ReturnedSuccess);
        Assert.Equal(0u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(1u, trace.ZeroedOutputsOnDirectionSetupFailure);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(0u, actualCandidateCount);
        Assert.Equal(-1, actualSelectedRecordIndex);
        Assert.Equal(0u, actualDirectionRankingAccepted);
        Assert.Equal(0u, actualChosenContact.InstanceId);
        Assert.Equal(0.0f, actualChosenPair.First, 6);
        Assert.Equal(1.0f, actualReportedBestRatio, 6);
        Assert.Equal(0u, trace.SelectedContactContainerTrace.ContainerInvoked);
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
