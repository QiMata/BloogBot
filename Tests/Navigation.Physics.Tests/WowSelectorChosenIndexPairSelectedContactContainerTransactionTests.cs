using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairSelectedContactContainerTransactionTests
{
    [Fact]
    public void SelectedContactContainerTransaction_OverrideUsesAmbientCachedContainer()
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
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[4];
        SelectorPair[] outputPairs = new SelectorPair[4];

        int count = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition: new Vector3(7.0f, 8.0f, 9.0f),
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
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
            outputContacts,
            outputPairs,
            outputContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace);

        Assert.Equal(2, count);
        Assert.Equal(1u, trace.UsedAmbientCachedContainerWithoutQuery);
        Assert.Equal(0u, trace.UsedProducedSelectedContactContainer);
        Assert.Equal(0u, trace.ContainerInvoked);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(2u, trace.OutputSelectedContactCount);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(11u, outputContacts[0].InstanceId);
        Assert.Equal(22u, outputContacts[1].InstanceId);
        Assert.Equal(1.5f, outputPairs[0].First, 6);
        Assert.Equal(4.5f, outputPairs[1].Second, 6);
    }

    [Fact]
    public void SelectedContactContainerTransaction_CacheHitReusesExistingContainerThroughComposedHelper()
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
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[4];
        SelectorPair[] outputPairs = new SelectorPair[4];

        int count = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition: null,
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
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
            outputContacts,
            outputPairs,
            outputContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace);

        Assert.Equal(2, count);
        Assert.Equal(0u, trace.UsedAmbientCachedContainerWithoutQuery);
        Assert.Equal(1u, trace.UsedProducedSelectedContactContainer);
        Assert.Equal(1u, trace.ContainerInvoked);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(2u, trace.OutputSelectedContactCount);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(1u, trace.ContainerTrace.ReusedExistingContainer);
        Assert.Equal(0u, trace.ContainerTrace.CopiedQueryResults);
        Assert.Equal(11u, outputContacts[0].InstanceId);
        Assert.Equal(22u, outputContacts[1].InstanceId);
        Assert.Equal(1.5f, outputPairs[0].First, 6);
        Assert.Equal(4.5f, outputPairs[1].Second, 6);
    }

    [Fact]
    public void SelectedContactContainerTransaction_CacheMissSuccessCopiesOnlyWalkableQueryResults()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts =
        [
            CreateContact(instanceId: 101u, normalZ: 0.9f, pointX: 3.0f),
            CreateContact(instanceId: 202u, normalZ: 0.2f, pointX: 4.0f),
            CreateContact(instanceId: 303u, normalZ: 0.8f, pointX: 5.0f),
        ];
        SelectorPair[] queryPairs =
        [
            new SelectorPair { First = 5.0f, Second = 6.0f },
            new SelectorPair { First = 7.0f, Second = 8.0f },
            new SelectorPair { First = 9.0f, Second = 10.0f },
        ];
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[4];
        SelectorPair[] outputPairs = new SelectorPair[4];

        int count = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition: null,
            projectedPosition: new Vector3(10.0f, 20.0f, 30.0f),
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
            outputContacts,
            outputPairs,
            outputContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace);

        Assert.Equal(2, count);
        Assert.Equal(1u, trace.UsedProducedSelectedContactContainer);
        Assert.Equal(1u, trace.ContainerInvoked);
        Assert.Equal(0u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(2u, trace.OutputSelectedContactCount);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(0u, trace.ContainerTrace.ReusedExistingContainer);
        Assert.Equal(1u, trace.ContainerTrace.CopiedQueryResults);
        Assert.Equal(101u, outputContacts[0].InstanceId);
        Assert.Equal(303u, outputContacts[1].InstanceId);
        Assert.Equal(5.0f, outputPairs[0].First, 6);
        Assert.Equal(10.0f, outputPairs[1].Second, 6);
    }

    [Fact]
    public void SelectedContactContainerTransaction_CacheMissFailureReturnsEmptyOutput()
    {
        TerrainAabbContact[] existingContacts = [CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f)];
        SelectorPair[] existingPairs = [new SelectorPair { First = 1.0f, Second = 2.0f }];
        TerrainAabbContact[] queryContacts = [CreateContact(instanceId: 99u, normalZ: 0.9f, pointX: 9.0f)];
        SelectorPair[] queryPairs = [new SelectorPair { First = 9.0f, Second = 10.0f }];
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[2];
        SelectorPair[] outputPairs = new SelectorPair[2];

        int count = EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
            overridePosition: null,
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
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
            outputContacts,
            outputPairs,
            outputContacts.Length,
            out SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace);

        Assert.Equal(0, count);
        Assert.Equal(1u, trace.ContainerInvoked);
        Assert.Equal(1u, trace.ZeroedOutputsOnContainerFailure);
        Assert.Equal(0u, trace.OutputSelectedContactCount);
        Assert.Equal(0u, trace.ReturnedSuccess);
        Assert.Equal(0u, trace.ContainerTrace.ReturnedSuccess);
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
            Walkable = normalZ >= 0.6427876353263855f ? 1u : 0u,
        };
}
