using BotRunner.Tasks;
using BotRunner.Tests.Combat;
using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using Pathfinding;
using System;
using System.Reflection;

namespace BotRunner.Tests;

public class GoToTaskFallbackTests
{
    [Fact]
    public void Update_NoPathInitially_DoesNotDirectFallbackMove()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pathfinding =>
        {
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>());
            pathfinding
                .Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns(true);
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.MapId).Returns(30u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new GoToTask(ctx.Object, 100f, 0f, 0f, 3f);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public void Update_NoPathPersists_UsesDirectFallbackAfterGraceWindow()
    {
        var target = new Position(100f, 0f, 0f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pathfinding =>
        {
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>());
            pathfinding
                .Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns(true);
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.MapId).Returns(30u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new GoToTask(ctx.Object, target.X, target.Y, target.Z, 3f);
        stack.Push(task);

        task.Update();
        SetPrivateField(task, "_noPathSinceUtc", DateTime.UtcNow - TimeSpan.FromSeconds(6));

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.MoveToward(
            It.Is<Position>(p =>
                MathF.Abs(p.X - target.X) < 0.01f &&
                MathF.Abs(p.Y - target.Y) < 0.01f &&
                MathF.Abs(p.Z - target.Z) < 0.01f),
            It.IsAny<float>()), Times.Once);
    }

    [Fact]
    public void Update_NoPathPersists_WithoutLineOfSight_DoesNotDirectFallbackMove()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pathfinding =>
        {
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>());
            pathfinding
                .Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns(false);
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.MapId).Returns(30u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new GoToTask(ctx.Object, 100f, 0f, 0f, 3f);
        stack.Push(task);

        task.Update();
        SetPrivateField(task, "_noPathSinceUtc", DateTime.UtcNow - TimeSpan.FromSeconds(6));

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public void Update_StackedElevatorTarget_DoesNotPopTaskAsArrived()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var upperStop = new Position(1544.24f, 240.77f, 55.40f);
        var lowerStop = new Position(1544.24f, 240.77f, -43.0f);
        var player = AtomicTaskTestHelpers.CreatePlayer(upperStop);
        player.Setup(p => p.MapId).Returns(0u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        player.Setup(p => p.TransportGuid).Returns(0UL);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new GoToTask(ctx.Object, lowerStop.X, lowerStop.Y, lowerStop.Z, 4f);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()), Times.Never);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }
}
