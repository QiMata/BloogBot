using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairVariableContainerTransactionTests
{
    private const float DirectThresholdNormalZ = 0.6427876353263855f;

    [Fact]
    public void VariableContainerTransaction_OverrideUsesAmbientCachedContainerAndPreservesReportedRatio()
    {
        TerrainAabbContact[] existingContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
            CreateContact(instanceId: 22u, normalZ: 0.8f, pointX: 2.0f),
        ];
        SelectorPair[] existingPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
            new SelectorPair { First = 3.5f, Second = 4.5f },
        ];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: 1.0f, pointX: 9.0f)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.5f, Second = 10.5f }];
        TerrainAabbContact[] expectedContacts = new TerrainAabbContact[4];
        SelectorPair[] expectedPairs = new SelectorPair[4];
        TerrainAabbContact[] actualContacts = new TerrainAabbContact[4];
        SelectorPair[] actualPairs = new SelectorPair[4];

        Vector3 defaultPosition = new(1.0f, 2.0f, 3.0f);
        Vector3 overridePosition = new(-4.0f, 5.5f, 6.5f);
        Vector3 projectedPosition = new(7.0f, 8.0f, 9.0f);
        Vector3 cachedBoundsMin = new(0.0f, 0.0f, 0.0f);
        Vector3 cachedBoundsMax = new(1.0f, 1.0f, 1.0f);
        Vector3 testPoint = new(0.0f, 0.0f, -1.0f);
        Vector3 candidateDirection = new(0.0f, 0.0f, -1.0f);

        bool variableSucceeded = EvaluateWoWSelectorTriangleSourceVariableTransaction(
            defaultPosition,
            overridePosition,
            projectedPosition,
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint,
            candidateDirection,
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false,
            queryDispatchSucceeded: false,
            rankingAccepted: false,
            rankingCandidateCount: 3u,
            rankingSelectedRecordIndex: -1,
            rankingReportedBestRatio: 0.25f,
            out SelectorTriangleSourceVariableTransactionTrace variableTrace);
        Assert.True(variableSucceeded);

        int expectedCount = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition,
            projectedPosition,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            queryContacts,
            queryPairs,
            queryContacts.Length,
            queryDispatchSucceeded: false,
            expectedContacts,
            expectedPairs,
            expectedContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace containerTrace);

        int actualCount = EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction(
            defaultPosition,
            overridePosition,
            projectedPosition,
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint,
            candidateDirection,
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false,
            existingContacts,
            existingPairs,
            existingContacts.Length,
            queryContacts,
            queryPairs,
            queryContacts.Length,
            queryDispatchSucceeded: false,
            rankingAccepted: false,
            rankingCandidateCount: 3u,
            rankingSelectedRecordIndex: -1,
            rankingReportedBestRatio: 0.25f,
            actualContacts,
            actualPairs,
            actualContacts.Length,
            out float actualReportedBestRatio,
            out SelectorChosenIndexPairVariableContainerTransactionTrace trace);

        Assert.Equal(expectedCount, actualCount);
        Assert.Equal(variableTrace.OutputReportedBestRatio, actualReportedBestRatio, 6);
        Assert.Equal(variableTrace.OutputReportedBestRatio, trace.OutputReportedBestRatio, 6);
        Assert.Equal(variableTrace.UsedOverridePosition, trace.VariableTrace.UsedOverridePosition);
        Assert.Equal(containerTrace.UsedAmbientCachedContainerWithoutQuery, trace.SelectedContactContainerTrace.UsedAmbientCachedContainerWithoutQuery);
        Assert.Equal(containerTrace.OutputSelectedContactCount, trace.SelectedContactContainerTrace.OutputSelectedContactCount);
        Assert.Equal(0u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal((uint)expectedCount, trace.OutputSelectedContactCount);
        Assert.Equal(1u, trace.ReturnedSuccess);
        AssertContactsAndPairsEqual(expectedContacts, expectedPairs, actualContacts, actualPairs, actualCount);
    }

    [Fact]
    public void VariableContainerTransaction_QueryFailureZeroesOutputsBeforeContainer()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: 0.9f, pointX: 9.0f)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.0f, Second = 10.0f }];
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[2];
        SelectorPair[] outputPairs = new SelectorPair[2];

        int count = EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction(
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
            rankingSelectedRecordIndex: 4,
            rankingReportedBestRatio: 0.75f,
            outputContacts,
            outputPairs,
            outputContacts.Length,
            out float reportedBestRatio,
            out SelectorChosenIndexPairVariableContainerTransactionTrace trace);

        Assert.Equal(0, count);
        Assert.Equal(0f, reportedBestRatio, 6);
        Assert.Equal(1u, trace.VariableTrace.QueryFailureZeroedOutput);
        Assert.Equal(1u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.SelectedContactContainerTrace.ContainerInvoked);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(0u, trace.OutputSelectedContactCount);
        Assert.Equal(0u, trace.ReturnedSuccess);
        Assert.Equal(0f, trace.OutputReportedBestRatio, 6);
    }

    [Fact]
    public void VariableContainerTransaction_CacheHitReusesExistingContainerThroughComposedHelper()
    {
        TerrainAabbContact[] existingContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
            CreateContact(instanceId: 22u, normalZ: 0.8f, pointX: 2.0f),
        ];
        SelectorPair[] existingPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
            new SelectorPair { First = 3.5f, Second = 4.5f },
        ];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: 1.0f, pointX: 9.0f)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.5f, Second = 10.5f }];
        TerrainAabbContact[] expectedContacts = new TerrainAabbContact[4];
        SelectorPair[] expectedPairs = new SelectorPair[4];
        TerrainAabbContact[] actualContacts = new TerrainAabbContact[4];
        SelectorPair[] actualPairs = new SelectorPair[4];

        Vector3 defaultPosition = new(11.5f, -3.25f, 7.75f);
        Vector3 projectedPosition = new(3.0f, 4.0f, 5.0f);
        Vector3 cachedBoundsMin = new(2.5f, 3.5f, 5.0f);
        Vector3 cachedBoundsMax = new(3.5f, 4.5f, 7.0f);
        Vector3 testPoint = new(0.0f, 0.0f, -1.0f);
        Vector3 candidateDirection = new(0.0f, 0.0f, -1.0f);

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
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: false,
            movementFlags: 0x10000000u,
            field20Value: -0.5f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            queryDispatchSucceeded: false,
            rankingAccepted: true,
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.001f,
            out SelectorTriangleSourceVariableTransactionTrace variableTrace);
        Assert.True(variableSucceeded);

        int expectedCount = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition: null,
            projectedPosition,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
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
            expectedContacts,
            expectedPairs,
            expectedContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace containerTrace);

        int actualCount = EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction(
            defaultPosition,
            overridePosition: null,
            projectedPosition,
            supportPlaneInitCount: 7u,
            validationPlaneInitCount: 9u,
            scratchPointZeroCount: 9u,
            testPoint,
            candidateDirection,
            initialBestRatio: 1.0f,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
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
            rankingReportedBestRatio: 0.001f,
            actualContacts,
            actualPairs,
            actualContacts.Length,
            out float actualReportedBestRatio,
            out SelectorChosenIndexPairVariableContainerTransactionTrace trace);

        Assert.Equal(expectedCount, actualCount);
        Assert.Equal(variableTrace.OutputReportedBestRatio, actualReportedBestRatio, 6);
        Assert.Equal(containerTrace.ContainerTrace.ReusedExistingContainer, trace.SelectedContactContainerTrace.ContainerTrace.ReusedExistingContainer);
        Assert.Equal(containerTrace.UsedProducedSelectedContactContainer, trace.SelectedContactContainerTrace.UsedProducedSelectedContactContainer);
        Assert.Equal(0u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(1u, trace.ReturnedSuccess);
        AssertContactsAndPairsEqual(expectedContacts, expectedPairs, actualContacts, actualPairs, actualCount);
    }

    [Fact]
    public void VariableContainerTransaction_CacheMissSuccessCopiesOnlyWalkableQueryResults()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts =
        [
            CreateContact(instanceId: 101u, normalZ: DirectThresholdNormalZ, pointX: 3.0f),
            CreateContact(instanceId: 202u, normalZ: 0.2f, pointX: 4.0f),
            CreateContact(instanceId: 303u, normalZ: 0.8f, pointX: 5.0f),
        ];
        SelectorPair[] queryPairs =
        [
            new SelectorPair { First = 5.0f, Second = 6.0f },
            new SelectorPair { First = 7.0f, Second = 8.0f },
            new SelectorPair { First = 9.0f, Second = 10.0f },
        ];
        TerrainAabbContact[] expectedContacts = new TerrainAabbContact[4];
        SelectorPair[] expectedPairs = new SelectorPair[4];
        TerrainAabbContact[] actualContacts = new TerrainAabbContact[4];
        SelectorPair[] actualPairs = new SelectorPair[4];

        Vector3 defaultPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 projectedPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 cachedBoundsMin = new(9.666667f, 19.666666f, 30.0f);
        Vector3 cachedBoundsMax = new(10.333333f, 20.333334f, 32.0f);
        Vector3 testPoint = new(0.0f, 0.0f, -1.0f);
        Vector3 candidateDirection = new(0.0f, 0.0f, -1.0f);

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
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.75f,
            out SelectorTriangleSourceVariableTransactionTrace variableTrace);
        Assert.True(variableSucceeded);

        int expectedCount = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
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

        int actualCount = EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction(
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
            rankingAccepted: true,
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.75f,
            actualContacts,
            actualPairs,
            actualContacts.Length,
            out float actualReportedBestRatio,
            out SelectorChosenIndexPairVariableContainerTransactionTrace trace);

        Assert.Equal(expectedCount, actualCount);
        Assert.Equal(variableTrace.OutputReportedBestRatio, actualReportedBestRatio, 6);
        Assert.Equal(containerTrace.ContainerTrace.CopiedQueryResults, trace.SelectedContactContainerTrace.ContainerTrace.CopiedQueryResults);
        Assert.Equal(containerTrace.OutputSelectedContactCount, trace.SelectedContactContainerTrace.OutputSelectedContactCount);
        Assert.Equal(1u, trace.SelectedContactContainerTrace.UsedProducedSelectedContactContainer);
        Assert.Equal(0u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(1u, trace.ReturnedSuccess);
        AssertContactsAndPairsEqual(expectedContacts, expectedPairs, actualContacts, actualPairs, actualCount);
    }

    private static void AssertContactsAndPairsEqual(
        TerrainAabbContact[] expectedContacts,
        SelectorPair[] expectedPairs,
        TerrainAabbContact[] actualContacts,
        SelectorPair[] actualPairs,
        int count)
    {
        for (int index = 0; index < count; ++index)
        {
            Assert.Equal(expectedContacts[index].InstanceId, actualContacts[index].InstanceId);
            Assert.Equal(expectedContacts[index].Walkable, actualContacts[index].Walkable);
            Assert.Equal(expectedContacts[index].Normal.Z, actualContacts[index].Normal.Z, 6);
            Assert.Equal(expectedPairs[index].First, actualPairs[index].First, 6);
            Assert.Equal(expectedPairs[index].Second, actualPairs[index].Second, 6);
        }
    }

    private static TerrainAabbContact CreateContact(uint instanceId, float normalZ, float pointX) =>
        new()
        {
            Point = new Vector3(pointX, pointX + 1.0f, pointX + 2.0f),
            Normal = new Vector3(0.0f, 0.0f, normalZ),
            RawNormal = new Vector3(0.0f, 0.0f, normalZ),
            TriangleA = new Vector3(pointX, 0.0f, 0.0f),
            TriangleB = new Vector3(pointX, 1.0f, 0.0f),
            TriangleC = new Vector3(pointX, 0.0f, 1.0f),
            PlaneDistance = -pointX,
            Distance = pointX * 0.1f,
            InstanceId = instanceId,
            SourceType = 1u,
            Walkable = normalZ >= DirectThresholdNormalZ ? 1u : 0u,
        };
}
