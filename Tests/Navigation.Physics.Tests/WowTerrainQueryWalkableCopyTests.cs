using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryWalkableCopyTests
{
    private const float WalkableThreshold = 0.6427876353263855f;

    [Fact]
    public void CopyWoWTerrainQueryWalkableContactsAndPairs_FiltersByNormalZAndPreservesPairAlignment()
    {
        TerrainAabbContact[] inputContacts =
        [
            CreateContact(instanceId: 11u, normalZ: 0.90f, walkable: 0u, pointX: 1.0f),
            CreateContact(instanceId: 22u, normalZ: 0.25f, walkable: 1u, pointX: 2.0f),
            CreateContact(instanceId: 33u, normalZ: 0.80f, walkable: 1u, pointX: 3.0f),
            CreateContact(instanceId: 44u, normalZ: -1.0f, walkable: 1u, pointX: 4.0f),
        ];
        TerrainQueryPairPayload[] inputPairs =
        [
            new TerrainQueryPairPayload { First = 1.0f, Second = 2.0f },
            new TerrainQueryPairPayload { First = 3.0f, Second = 4.0f },
            new TerrainQueryPairPayload { First = 5.0f, Second = 6.0f },
            new TerrainQueryPairPayload { First = 7.0f, Second = 8.0f },
        ];
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[4];
        TerrainQueryPairPayload[] outputPairs = new TerrainQueryPairPayload[4];

        int count = CopyWoWTerrainQueryWalkableContactsAndPairs(
            inputContacts,
            inputPairs,
            inputContacts.Length,
            outputContacts,
            outputPairs,
            outputContacts.Length);

        Assert.Equal(2, count);

        Assert.Equal(11u, outputContacts[0].InstanceId);
        Assert.Equal(0u, outputContacts[0].Walkable);
        Assert.Equal(1.0f, outputContacts[0].Point.X, 6);
        Assert.Equal(1.0f, outputPairs[0].First, 6);
        Assert.Equal(2.0f, outputPairs[0].Second, 6);

        Assert.Equal(33u, outputContacts[1].InstanceId);
        Assert.Equal(1u, outputContacts[1].Walkable);
        Assert.Equal(3.0f, outputContacts[1].Point.X, 6);
        Assert.Equal(5.0f, outputPairs[1].First, 6);
        Assert.Equal(6.0f, outputPairs[1].Second, 6);
    }

    [Fact]
    public void CopyWoWTerrainQueryWalkableContactsAndPairs_IncludesExactThresholdContact()
    {
        TerrainAabbContact[] inputContacts =
        [
            CreateContact(instanceId: 55u, normalZ: WalkableThreshold, walkable: 1u, pointX: 9.0f),
        ];
        TerrainQueryPairPayload[] inputPairs =
        [
            new TerrainQueryPairPayload { First = 9.5f, Second = 10.5f },
        ];
        TerrainAabbContact[] outputContacts = new TerrainAabbContact[1];
        TerrainQueryPairPayload[] outputPairs = new TerrainQueryPairPayload[1];

        int count = CopyWoWTerrainQueryWalkableContactsAndPairs(
            inputContacts,
            inputPairs,
            inputContacts.Length,
            outputContacts,
            outputPairs,
            outputContacts.Length);

        Assert.Equal(1, count);
        Assert.Equal(55u, outputContacts[0].InstanceId);
        Assert.Equal(WalkableThreshold, outputContacts[0].Normal.Z, 6);
        Assert.Equal(9.5f, outputPairs[0].First, 6);
        Assert.Equal(10.5f, outputPairs[0].Second, 6);
    }

    private static TerrainAabbContact CreateContact(uint instanceId, float normalZ, uint walkable, float pointX) =>
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
            Walkable = walkable,
        };
}
