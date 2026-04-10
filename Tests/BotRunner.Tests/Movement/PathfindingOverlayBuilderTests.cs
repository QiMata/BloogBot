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
            CreateGameObject(0x1001, (uint)GameObjectType.Door, 17, new Position(5f, 0f, 1f), facing: 1.25f, scale: 1.5f, goState: GOState.Active).Object,
            CreateGameObject(0x1002, (uint)GameObjectType.QuestGiver, 18, new Position(6f, 0f, 1f)).Object,
            CreateGameObject(0x1003, (uint)GameObjectType.Generic, 0, new Position(7f, 0f, 1f)).Object,
            CreateGameObject(0x1004, (uint)GameObjectType.Generic, 19, new Position(float.NaN, 0f, 1f)).Object,
            CreateGameObject(0x1005, (uint)GameObjectType.Generic, 20, new Position(100f, 0f, 1f)).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f));

        var result = Assert.Single(nearby);
        Assert.Equal(0x1001UL, result.Guid);
        Assert.Equal(17u, result.DisplayId);
        Assert.Equal(5f, result.X);
        Assert.Equal(1.25f, result.Orientation);
        Assert.Equal(1.5f, result.Scale);
        Assert.Equal((uint)GOState.Active, result.GoState);
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
    public void BuildNearbyObjects_UsesConservativeCollisionAndGameplayTypeAllowlist()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.GameObjects).Returns(
        [
            CreateGameObject(0x3001, (uint)GameObjectType.Generic, 101, new Position(4f, 0f, 0f)).Object,
            CreateGameObject(0x3002, (uint)GameObjectType.Goober, 102, new Position(5f, 0f, 0f)).Object,
            CreateGameObject(0x3003, (uint)GameObjectType.Chest, 103, new Position(6f, 0f, 0f)).Object,
            CreateGameObject(0x3004, (uint)GameObjectType.Mailbox, 104, new Position(7f, 0f, 0f)).Object,
            CreateGameObject(0x3005, (uint)GameObjectType.Door, 105, new Position(8f, 0f, 0f)).Object,
            CreateGameObject(0x3006, (uint)GameObjectType.FlagStand, 106, new Position(9f, 0f, 0f)).Object
        ]);

        var nearby = PathfindingOverlayBuilder.BuildNearbyObjects(
            objectManager.Object,
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f));

        Assert.Equal(2, nearby.Length);
        Assert.Contains(nearby, item => item.Guid == 0x3005UL);
        Assert.Contains(nearby, item => item.Guid == 0x3006UL);
    }

    private static Mock<IWoWGameObject> CreateGameObject(
        ulong guid,
        uint typeId,
        uint displayId,
        Position position,
        float facing = 0f,
        float scale = 1f,
        GOState goState = GOState.Ready)
    {
        var gameObject = new Mock<IWoWGameObject>();
        gameObject.SetupGet(x => x.Guid).Returns(guid);
        gameObject.SetupGet(x => x.TypeId).Returns(typeId);
        gameObject.SetupGet(x => x.DisplayId).Returns(displayId);
        gameObject.SetupGet(x => x.Position).Returns(position);
        gameObject.SetupGet(x => x.Facing).Returns(facing);
        gameObject.SetupGet(x => x.ScaleX).Returns(scale);
        gameObject.SetupGet(x => x.GoState).Returns(goState);
        return gameObject;
    }
}
