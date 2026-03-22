using BotRunner.Tasks;
using GameData.Core.Models;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tests.Combat;

public class GatheringRouteTaskTests
{
    [Fact]
    public void OptimizeRoute_OrdersCandidatesByNearestNeighbor()
    {
        var route = GatheringRouteTask.OptimizeRoute(
            new Position(0, 0, 0),
            [
                new Position(12, 0, 0),
                new Position(4, 0, 0),
                new Position(8, 0, 0)
            ]);

        Assert.Collection(route,
            point => Assert.Equal(4f, point.X),
            point => Assert.Equal(8f, point.X),
            point => Assert.Equal(12f, point.X));
    }

    [Fact]
    public void Update_NoPlayer_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.Player).Returns((GameData.Core.Interfaces.IWoWLocalPlayer?)null);

        var task = new GatheringRouteTask(ctx.Object, [new Position(0, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }

    [Fact]
    public void Update_VisibleNodeInRange_StartsGatherSequence()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var node = AtomicTaskTestHelpers.CreateGameObject(0xAAAAUL, 1731u, 3u, new Position(1, 0, 0), "Copper Vein");
        om.Setup(o => o.GameObjects).Returns([node.Object]);

        var task = new GatheringRouteTask(ctx.Object, [new Position(0, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Build route
        task.Update(); // Candidate reached
        task.Update(); // Visible node found
        task.Update(); // Gather starts

        om.Verify(o => o.StopAllMovement(), Times.Once);
        om.Verify(o => o.ForceStopImmediate(), Times.Once);
        om.Verify(o => o.InteractWithGameObject(0xAAAAUL), Times.Once);
        om.Verify(o => o.CastSpellOnGameObject(2575, 0xAAAAUL), Times.Once);
        om.Verify(o => o.SetTarget(0), Times.Once);
        Assert.Single(stack);
    }
}
