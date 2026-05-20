using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests.Movement;

public class PathfindingOverlayBuilderTests
{
    [Fact]
    public void BuildNearbyObjects_FiltersToNearbyCollidableFiniteObjects()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.GameObjects).Returns(
        [
            CreateGameObject(0x1001, (uint)GameObjectType.Door, 17, new Position(5f, 0f, 1f), entry: 17001, facing: 1.25f, scale: 1.5f, goState: GOState.Active).Object,
            CreateGameObject(0x1002, (uint)GameObjectType.QuestGiver, 18, new Position(6f, 0f, 1f)).Object,
            CreateGameObject(0x1003, (uint)GameObjectType.Generic, 19, new Position(7f, 0f, 1f)).Object,
            CreateGameObject(0x1004, (uint)GameObjectType.Generic, 19, new Position(float.NaN, 0f, 1f)).Object,
            CreateGameObject(0x1005, (uint)GameObjectType.Generic, 20, new Position(100f, 0f, 1f)).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f));

        Assert.Single(nearby);
        var door = Assert.Single(nearby, item => item.Guid == 0x1001UL);
        Assert.Equal(17001u, door.Entry);
        Assert.Equal(17u, door.DisplayId);
        Assert.Equal(5f, door.X);
        Assert.Equal(1.25f, door.Orientation);
        Assert.Equal(1.5f, door.Scale);
        Assert.Equal((uint)GOState.Active, door.GoState);
        Assert.DoesNotContain(nearby, item => item.Guid == 0x1003UL);
    }

    [Fact]
    public void BuildNearbyObjects_IncludesCollisionRelevantObjectsNearDestinationEvenWhenFarFromStart()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.GameObjects).Returns(
        [
            CreateGameObject(0x2001, (uint)GameObjectType.Door, 42, new Position(75f, 0f, 1f)).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(80f, 0f, 0f));

        var result = Assert.Single(nearby);
        Assert.Equal(0x2001UL, result.Guid);
        Assert.Equal(75f, result.X);
    }

    [Fact]
    public void BuildNearbyObjects_CanonicalizesMovingTransportEntryFromGuid()
    {
        var transportGuid = 0x1FC0000000000000UL | TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry;
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.GameObjects).Returns(
        [
            CreateGameObject(
                transportGuid,
                (uint)GameObjectType.MapObjectTransport,
                TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
                new Position(4f, 0f, 1f),
                entry: TransportData.ZeppelinOrgrimmarGromgol.GameObjectEntry).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(10f, 0f, 0f));

        var result = Assert.Single(nearby);
        Assert.Equal(TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry, result.Entry);
        Assert.Equal(transportGuid, result.Guid);
    }

    [Fact]
    public void BuildNearbyObjects_SkipsReadyStaticPropsButKeepsStatefulAndGameplayObjects()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.GameObjects).Returns(
        [
            CreateGameObject(0x3001, (uint)GameObjectType.Generic, 101, new Position(4f, 0f, 0f)).Object,
            CreateGameObject(0x3002, (uint)GameObjectType.Goober, 102, new Position(5f, 0f, 0f), goState: GOState.Active).Object,
            CreateGameObject(0x3003, (uint)GameObjectType.SpellFocus, 103, new Position(6f, 0f, 0f)).Object,
            CreateGameObject(0x3004, (uint)GameObjectType.Chest, 104, new Position(7f, 0f, 0f)).Object,
            CreateGameObject(0x3005, (uint)GameObjectType.Mailbox, 105, new Position(8f, 0f, 0f)).Object,
            CreateGameObject(0x3006, (uint)GameObjectType.Door, 106, new Position(9f, 0f, 0f)).Object,
            CreateGameObject(0x3007, (uint)GameObjectType.FlagStand, 107, new Position(10f, 0f, 0f)).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f));

        Assert.Equal(3, nearby.Length);
        Assert.DoesNotContain(nearby, item => item.Guid == 0x3001UL);
        Assert.Contains(nearby, item => item.Guid == 0x3002UL);
        Assert.DoesNotContain(nearby, item => item.Guid == 0x3003UL);
        Assert.Contains(nearby, item => item.Guid == 0x3006UL);
        Assert.Contains(nearby, item => item.Guid == 0x3007UL);
    }

    [Fact]
    public void BuildNearbyObjects_LongTravelOptInIncludesReadyStaticProps()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.GameObjects).Returns(
        [
            CreateGameObject(0x4001, (uint)GameObjectType.Generic, 201, new Position(4f, 0f, 0f)).Object,
            CreateGameObject(0x4002, (uint)GameObjectType.MapObject, 202, new Position(5f, 0f, 0f)).Object,
            CreateGameObject(0x4003, (uint)GameObjectType.Goober, 203, new Position(6f, 0f, 0f)).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            includeReadyStaticProps: true);

        Assert.Equal(3, nearby.Length);
        Assert.Contains(nearby, item => item.Guid == 0x4001UL);
        Assert.Contains(nearby, item => item.Guid == 0x4002UL);
        Assert.Contains(nearby, item => item.Guid == 0x4003UL);
    }

    private static Mock<IWoWGameObject> CreateGameObject(
        ulong guid,
        uint typeId,
        uint displayId,
        Position position,
        uint entry = 0,
        float facing = 0f,
        float scale = 1f,
        GOState goState = GOState.Ready)
    {
        var gameObject = new Mock<IWoWGameObject>();
        gameObject.SetupGet(x => x.Guid).Returns(guid);
        gameObject.SetupGet(x => x.Entry).Returns(entry);
        gameObject.SetupGet(x => x.TypeId).Returns(typeId);
        gameObject.SetupGet(x => x.DisplayId).Returns(displayId);
        gameObject.SetupGet(x => x.Position).Returns(position);
        gameObject.SetupGet(x => x.Facing).Returns(facing);
        gameObject.SetupGet(x => x.ScaleX).Returns(scale);
        gameObject.SetupGet(x => x.GoState).Returns(goState);
        return gameObject;
    }
}
