using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

    [Fact]
    public void Update_CombatPause_KeepsTaskAndResumesWithoutTimingOut()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var inCombat = false;
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        player.Setup(p => p.IsInCombat).Returns(() => inCombat);
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GameObjects).Returns([]);
        var combatTask = new Mock<IBotTask>();
        var combatFactoryCalls = 0;
        var classContainer = new Mock<BotRunner.Interfaces.IClassContainer>();
        classContainer
            .Setup(c => c.CreatePvERotationTask)
            .Returns((BotRunner.Interfaces.IBotContext context) =>
            {
                combatFactoryCalls++;
                Assert.Same(ctx.Object, context);
                return combatTask.Object;
            });
        Mock.Get(ctx.Object.Container).Setup(c => c.ClassContainer).Returns(classContainer.Object);

        var task = new GatheringRouteTask(ctx.Object, [new Position(40, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Build route and select the first candidate.
        RewindStateTimer(task, TimeSpan.FromMilliseconds(46_000));

        inCombat = true;
        task.Update(); // Pause instead of popping on combat and hand combat off.

        Assert.Equal(2, stack.Count);
        Assert.Same(combatTask.Object, stack.Peek());
        om.Verify(o => o.StopAllMovement(), Times.Once);
        Assert.Equal(1, combatFactoryCalls);

        stack.Pop(); // Simulate combat rotation task finishing once combat is over.
        inCombat = false;
        task.Update(); // Resume the current candidate instead of timing out immediately.

        Assert.Single(stack);
        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Once);
    }

    [Fact]
    public void Update_CombatPause_PushesCombatRotationTaskAboveGatherTask()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0), inCombat: true);
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GameObjects).Returns([]);
        var combatTask = new Mock<IBotTask>();
        var combatFactoryCalls = 0;
        var classContainer = new Mock<BotRunner.Interfaces.IClassContainer>();
        classContainer
            .Setup(c => c.CreatePvERotationTask)
            .Returns((BotRunner.Interfaces.IBotContext context) =>
            {
                combatFactoryCalls++;
                Assert.Same(ctx.Object, context);
                return combatTask.Object;
            });
        Mock.Get(ctx.Object.Container).Setup(c => c.ClassContainer).Returns(classContainer.Object);

        var task = new GatheringRouteTask(ctx.Object, [new Position(40, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Pause immediately on combat and queue combat handling.

        Assert.Equal(2, stack.Count);
        Assert.Same(combatTask.Object, stack.Peek());
        Assert.Same(task, stack.ToArray()[1]);
        Assert.Equal(1, combatFactoryCalls);
    }

    private static void RewindStateTimer(GatheringRouteTask task, TimeSpan elapsed)
    {
        var field = typeof(GatheringRouteTask).GetField(
            "_stateEnteredAt",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._stateEnteredAt field not found.");

        field.SetValue(task, DateTime.UtcNow - elapsed);
    }
}
