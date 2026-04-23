using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
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
    public void Update_VisibleNodeInRange_StopsUsesNodeThenDelaysGatherSpell()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var node = AtomicTaskTestHelpers.CreateGameObject(0xAAAAUL, 1731u, 3u, new Position(1, 0, 0), "Copper Vein");
        om.Setup(o => o.GameObjects).Returns([node.Object]);
        var calls = new List<string>();
        om.Setup(o => o.ForceStopImmediate()).Callback(() => calls.Add("stop"));
        om.Setup(o => o.Face(It.IsAny<Position>())).Callback(() => calls.Add("face"));
        om.Setup(o => o.InteractWithGameObject(0xAAAAUL)).Callback(() => calls.Add("use"));
        om.Setup(o => o.CastSpellOnGameObject(2575, 0xAAAAUL)).Callback(() => calls.Add("cast"));
        om.Setup(o => o.SetTarget(0)).Callback(() => calls.Add("clear_target"));

        var task = new GatheringRouteTask(ctx.Object, [new Position(0, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Build route
        task.Update(); // Candidate reached
        task.Update(); // Visible node found
        task.Update(); // GAMEOBJ_USE starts, spell is intentionally delayed

        om.Verify(o => o.StopAllMovement(), Times.Once);
        om.Verify(o => o.ForceStopImmediate(), Times.Once);
        om.Verify(o => o.InteractWithGameObject(0xAAAAUL), Times.Once);
        om.Verify(o => o.CastSpellOnGameObject(2575, 0xAAAAUL), Times.Never);
        om.Verify(o => o.SetTarget(0), Times.Never);
        Assert.Equal(["stop", "face", "use"], calls);

        RewindStateTimer(task, TimeSpan.FromMilliseconds(GatheringRouteTask.GatherCastDelayMs + 1));
        task.Update(); // Gathering spell starts after stop/use packet ordering window

        om.Verify(o => o.CastSpellOnGameObject(2575, 0xAAAAUL), Times.Once);
        om.Verify(o => o.SetTarget(0), Times.Once);
        Assert.Equal(["stop", "face", "use", "cast", "clear_target"], calls);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_RetryableGatherCastFailure_ReusesSameVisibleNode()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var eventHandler = new Mock<IWoWEventHandler>();
        ctx.Setup(c => c.EventHandler).Returns(eventHandler.Object);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var node = AtomicTaskTestHelpers.CreateGameObject(0xAAAAUL, 1731u, 3u, new Position(1, 0, 0), "Copper Vein");
        om.Setup(o => o.GameObjects).Returns([node.Object]);

        var task = new GatheringRouteTask(ctx.Object, [new Position(0, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Build route
        task.Update(); // Candidate reached
        task.Update(); // Visible node found
        task.Update(); // GAMEOBJ_USE starts

        RewindStateTimer(task, TimeSpan.FromMilliseconds(GatheringRouteTask.GatherCastDelayMs + 1));
        task.Update(); // Gathering spell starts

        eventHandler.Raise(
            e => e.OnErrorMessage += null!,
            new OnUiMessageArgs("Cast failed for spell 2575: TRY_AGAIN"));
        task.Update(); // Server failure packet schedules a retry.

        RewindStateTimer(task, TimeSpan.FromMilliseconds(GatheringRouteTask.GatherRetryDelayMs + 1));
        task.Update(); // Retry delay elapsed; return to node approach state.
        task.Update(); // Re-use the same visible node.

        om.Verify(o => o.InteractWithGameObject(0xAAAAUL), Times.Exactly(2));
        om.Verify(o => o.CastSpellOnGameObject(2575, 0xAAAAUL), Times.Once);
        Assert.Single(stack);
    }

    [Fact]
    public void ComputeApproachPosition_StopsShortOfNodeByGatherBuffer()
    {
        var player = new Position(0f, 0f, 0f);
        var node = new Position(8f, 0f, 0f);

        var approach = GatheringRouteTask.ComputeApproachPosition(player, node);

        Assert.Equal(3.5f, approach.X, 3);
        Assert.Equal(0f, approach.Y, 3);
        Assert.Equal(0f, approach.Z, 3);
        Assert.Equal(GatheringRouteTask.GatherRange - GatheringRouteTask.GatherApproachBuffer, approach.DistanceTo(node), 3);
    }

    [Fact]
    public void Update_VisibleNodeOutOfRange_NavigatesTowardApproachPointInsteadOfNodeCenter()
    {
        Position? requestedDestination = null;
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                {
                    return
                    [
                        new Position(start.X, start.Y, start.Z),
                        new Position(end.X, end.Y, end.Z)
                    ];
                }));

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.MoveToward(It.IsAny<Position>()))
            .Callback<Position>(position => requestedDestination = new Position(position.X, position.Y, position.Z));

        var node = AtomicTaskTestHelpers.CreateGameObject(0xAAAAUL, 1731u, 3u, new Position(8f, 0f, 0f), "Copper Vein");
        om.Setup(o => o.GameObjects).Returns([node.Object]);

        var task = new GatheringRouteTask(ctx.Object, [new Position(0f, 0f, 0f)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Build route
        task.Update(); // Candidate reached
        task.Update(); // Visible node found
        task.Update(); // Navigate toward node

        Assert.NotNull(requestedDestination);
        Assert.Equal(3.5f, requestedDestination!.X, 3);
        Assert.Equal(0f, requestedDestination.Y, 3);
        Assert.Equal(0f, requestedDestination.Z, 3);
    }

    [Fact]
    public void Update_VisibleNodeShortRangeNoPath_FallsBackToDirectMove()
    {
        Position? requestedDestination = null;
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns([]);
            WoWSharpClient.Movement.NativeLocalPhysics.TestLineOfSightOverride = (_, _, _, _, _, _, _) => false;
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.MoveToward(It.IsAny<Position>()))
            .Callback<Position>(position => requestedDestination = new Position(position.X, position.Y, position.Z));

        var node = AtomicTaskTestHelpers.CreateGameObject(0xBBBBUL, 1731u, 3u, new Position(8f, 0f, 0f), "Copper Vein");
        om.Setup(o => o.GameObjects).Returns([node.Object]);

        var task = new GatheringRouteTask(ctx.Object, [new Position(0f, 0f, 0f)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Build route
        task.Update(); // Candidate reached
        task.Update(); // Visible node found
        task.Update(); // Short-range fallback move

        Assert.NotNull(requestedDestination);
        Assert.Equal(3.5f, requestedDestination!.X, 3);
        Assert.Equal(0f, requestedDestination.Y, 3);
        Assert.Equal(0f, requestedDestination.Z, 3);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_CombatPause_KeepsTaskAndResumesWithoutTimingOut()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    new Position(end.X, end.Y, end.Z)
                ]));
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

    [Fact]
    public void Update_CombatPause_RepushesCombatRotationTaskIfCombatPersistsAfterFirstHandoff()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0), inCombat: true);
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GameObjects).Returns([]);

        var firstCombatTask = new Mock<IBotTask>();
        var secondCombatTask = new Mock<IBotTask>();
        var combatTaskQueue = new Queue<IBotTask>([firstCombatTask.Object, secondCombatTask.Object]);
        var combatFactoryCalls = 0;
        var classContainer = new Mock<BotRunner.Interfaces.IClassContainer>();
        classContainer
            .Setup(c => c.CreatePvERotationTask)
            .Returns((BotRunner.Interfaces.IBotContext context) =>
            {
                combatFactoryCalls++;
                Assert.Same(ctx.Object, context);
                return combatTaskQueue.Dequeue();
            });
        Mock.Get(ctx.Object.Container).Setup(c => c.ClassContainer).Returns(classContainer.Object);

        var task = new GatheringRouteTask(ctx.Object, [new Position(40, 0, 0)], [1731u], 2575);
        stack.Push(task);

        task.Update(); // Initial combat handoff.
        Assert.Equal(2, stack.Count);
        Assert.Same(firstCombatTask.Object, stack.Peek());

        stack.Pop(); // Simulate the handed-off combat task ending while still in combat.
        task.Update();

        Assert.Equal(2, stack.Count);
        Assert.Same(secondCombatTask.Object, stack.Peek());
        Assert.Same(task, stack.ToArray()[1]);
        Assert.Equal(2, combatFactoryCalls);
    }

    [Fact]
    public void AdvanceToNextCandidate_TimeoutReason_ResequencesRemainingRouteFromCurrentPosition()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(105f, 0f, 0f));
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GameObjects).Returns([]);

        var task = new GatheringRouteTask(
            ctx.Object,
            [new Position(100f, 0f, 0f), new Position(1000f, 0f, 0f), new Position(110f, 0f, 0f)],
            [1731u],
            2575);
        stack.Push(task);

        // Seed an in-progress route where the next sequential candidate is far but a closer
        // candidate still exists in the remaining list.
        var orderedRouteField = typeof(GatheringRouteTask).GetField("_orderedRoute", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._orderedRoute field not found.");
        var route = (List<Position>)orderedRouteField.GetValue(task)!;
        route.Clear();
        route.AddRange(
        [
            new Position(100f, 0f, 0f),
            new Position(1000f, 0f, 0f),
            new Position(110f, 0f, 0f)
        ]);

        var routeIndexField = typeof(GatheringRouteTask).GetField("_routeIndex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._routeIndex field not found.");
        routeIndexField.SetValue(task, 1);

        var advanceMethod = typeof(GatheringRouteTask).GetMethod("AdvanceToNextCandidate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask.AdvanceToNextCandidate method not found.");
        advanceMethod.Invoke(task, ["candidate_timeout"]);

        var currentCandidateField = typeof(GatheringRouteTask).GetField("_currentCandidate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._currentCandidate field not found.");
        var selected = (Position?)currentCandidateField.GetValue(task);

        Assert.NotNull(selected);
        Assert.Equal(110f, selected!.X);
        Assert.Equal(0f, selected.Y);
    }

    [Fact]
    public void AdvanceToNextCandidate_UnknownReason_DoesNotResequenceRemainingRoute()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(105f, 0f, 0f));
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GameObjects).Returns([]);

        var task = new GatheringRouteTask(
            ctx.Object,
            [new Position(100f, 0f, 0f), new Position(1000f, 0f, 0f), new Position(110f, 0f, 0f)],
            [1731u],
            2575);
        stack.Push(task);

        // Seed an in-progress route where resequencing would choose 110 next.
        var orderedRouteField = typeof(GatheringRouteTask).GetField("_orderedRoute", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._orderedRoute field not found.");
        var route = (List<Position>)orderedRouteField.GetValue(task)!;
        route.Clear();
        route.AddRange(
        [
            new Position(100f, 0f, 0f),
            new Position(1000f, 0f, 0f),
            new Position(110f, 0f, 0f)
        ]);

        var routeIndexField = typeof(GatheringRouteTask).GetField("_routeIndex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._routeIndex field not found.");
        routeIndexField.SetValue(task, 1);

        var advanceMethod = typeof(GatheringRouteTask).GetMethod("AdvanceToNextCandidate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask.AdvanceToNextCandidate method not found.");
        advanceMethod.Invoke(task, ["unknown_reason"]);

        var currentCandidateField = typeof(GatheringRouteTask).GetField("_currentCandidate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GatheringRouteTask._currentCandidate field not found.");
        var selected = (Position?)currentCandidateField.GetValue(task);

        Assert.NotNull(selected);
        Assert.Equal(1000f, selected!.X);
        Assert.Equal(0f, selected.Y);
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
