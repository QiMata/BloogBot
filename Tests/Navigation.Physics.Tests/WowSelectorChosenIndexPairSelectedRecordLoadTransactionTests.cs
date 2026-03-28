using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairSelectedRecordLoadTransactionTests
{
    [Fact]
    public void SelectedRecordLoadTransaction_InRangeIndexLoadsChosenContactAndPair()
    {
        TerrainAabbContact[] selectedContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
            CreateContact(instanceId: 22u, normalZ: 0.8f, pointX: 2.0f),
        ];
        SelectorPair[] directPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
            new SelectorPair { First = 3.5f, Second = 4.5f },
        ];

        bool result = EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
            selectedRecordIndex: 1,
            selectedContacts,
            selectedContacts.Length,
            directPairs,
            directPairs.Length,
            out TerrainAabbContact chosenContact,
            out SelectorPair chosenPair,
            out SelectorChosenIndexPairSelectedRecordLoadTrace trace);

        Assert.True(result);
        Assert.Equal(1, trace.InputSelectedRecordIndex);
        Assert.Equal((uint)selectedContacts.Length, trace.InputSelectedContactCount);
        Assert.Equal((uint)directPairs.Length, trace.InputDirectPairCount);
        Assert.Equal(0u, trace.SelectedIndexUnset);
        Assert.Equal(0u, trace.SelectedIndexMatchesContactCountSentinel);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.SelectedIndexPastEndMismatch);
        Assert.Equal(1u, trace.LoadedChosenContact);
        Assert.Equal(1u, trace.LoadedChosenPair);
        Assert.Equal(22u, chosenContact.InstanceId);
        Assert.Equal(3.5f, chosenPair.First, 6);
        Assert.Equal(4.5f, chosenPair.Second, 6);
    }

    [Fact]
    public void SelectedRecordLoadTransaction_SentinelIndexLeavesChosenOutputsZero()
    {
        TerrainAabbContact[] selectedContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
        ];
        SelectorPair[] directPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
        ];

        bool result = EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
            selectedRecordIndex: selectedContacts.Length,
            selectedContacts,
            selectedContacts.Length,
            directPairs,
            directPairs.Length,
            out TerrainAabbContact chosenContact,
            out SelectorPair chosenPair,
            out SelectorChosenIndexPairSelectedRecordLoadTrace trace);

        Assert.True(result);
        Assert.Equal(0u, trace.SelectedIndexUnset);
        Assert.Equal(1u, trace.SelectedIndexMatchesContactCountSentinel);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.SelectedIndexPastEndMismatch);
        Assert.Equal(0u, trace.LoadedChosenContact);
        Assert.Equal(0u, trace.LoadedChosenPair);
        Assert.Equal(0u, chosenContact.InstanceId);
        Assert.Equal(0.0f, chosenPair.First, 6);
        Assert.Equal(0.0f, chosenPair.Second, 6);
    }

    [Fact]
    public void SelectedRecordLoadTransaction_PastEndMismatchLeavesChosenOutputsZero()
    {
        TerrainAabbContact[] selectedContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
            CreateContact(instanceId: 22u, normalZ: 0.8f, pointX: 2.0f),
        ];
        SelectorPair[] directPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
            new SelectorPair { First = 3.5f, Second = 4.5f },
        ];

        bool result = EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
            selectedRecordIndex: selectedContacts.Length + 1,
            selectedContacts,
            selectedContacts.Length,
            directPairs,
            directPairs.Length,
            out TerrainAabbContact chosenContact,
            out SelectorPair chosenPair,
            out SelectorChosenIndexPairSelectedRecordLoadTrace trace);

        Assert.True(result);
        Assert.Equal(0u, trace.SelectedIndexUnset);
        Assert.Equal(0u, trace.SelectedIndexMatchesContactCountSentinel);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.SelectedIndexPastEndMismatch);
        Assert.Equal(0u, trace.LoadedChosenContact);
        Assert.Equal(0u, trace.LoadedChosenPair);
        Assert.Equal(0u, chosenContact.InstanceId);
        Assert.Equal(0.0f, chosenPair.First, 6);
        Assert.Equal(0.0f, chosenPair.Second, 6);
    }

    [Fact]
    public void SelectedRecordLoadTransaction_NegativeIndexClassifiesUnset()
    {
        TerrainAabbContact[] selectedContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
        ];
        SelectorPair[] directPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
        ];

        bool result = EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
            selectedRecordIndex: -1,
            selectedContacts,
            selectedContacts.Length,
            directPairs,
            directPairs.Length,
            out TerrainAabbContact chosenContact,
            out SelectorPair chosenPair,
            out SelectorChosenIndexPairSelectedRecordLoadTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.SelectedIndexUnset);
        Assert.Equal(0u, trace.SelectedIndexMatchesContactCountSentinel);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.SelectedIndexPastEndMismatch);
        Assert.Equal(0u, trace.LoadedChosenContact);
        Assert.Equal(0u, trace.LoadedChosenPair);
        Assert.Equal(0u, chosenContact.InstanceId);
        Assert.Equal(0.0f, chosenPair.First, 6);
        Assert.Equal(0.0f, chosenPair.Second, 6);
    }

    [Fact]
    public void SelectedRecordLoadTransaction_InRangeIndexLoadsContactWithoutPairWhenPairCountShort()
    {
        TerrainAabbContact[] selectedContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.9f, pointX: 1.0f),
            CreateContact(instanceId: 22u, normalZ: 0.8f, pointX: 2.0f),
        ];
        SelectorPair[] directPairs =
        [
            new SelectorPair { First = 1.5f, Second = 2.5f },
        ];

        bool result = EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
            selectedRecordIndex: 1,
            selectedContacts,
            selectedContacts.Length,
            directPairs,
            directPairs.Length,
            out TerrainAabbContact chosenContact,
            out SelectorPair chosenPair,
            out SelectorChosenIndexPairSelectedRecordLoadTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.LoadedChosenContact);
        Assert.Equal(0u, trace.LoadedChosenPair);
        Assert.Equal(22u, chosenContact.InstanceId);
        Assert.Equal(0.0f, chosenPair.First, 6);
        Assert.Equal(0.0f, chosenPair.Second, 6);
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
