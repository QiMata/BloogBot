using BotRunner.Combat;
using BotRunner.Clients;
using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Movement;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using Pathfinding;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tests.Combat;

// ==================== Shared Helpers ====================

internal static class AtomicTaskTestHelpers
{
    internal static (Mock<IBotContext> Ctx, Mock<IObjectManager> Om, Stack<IBotTask> Stack) CreateContext(Action<Mock<PathfindingClient>>? configurePathfinding = null)
    {
        var ctx = new Mock<IBotContext>();
        var om = new Mock<IObjectManager>();
        var eventHandler = new Mock<IWoWEventHandler>();
        var container = new Mock<IDependencyContainer>();
        var pathfinding = new Mock<PathfindingClient>();
        var stack = new Stack<IBotTask>();

        pathfinding
            .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
            .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
            [
                new Position(start.X, start.Y, start.Z),
                new Position(end.X, end.Y, end.Z)
            ]);
        pathfinding
            .Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
            .Returns(true);
        pathfinding
            .Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
            .Returns((uint _, Position position, float __) => (1u, position));
        pathfinding
            .Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
            .Returns((uint _, Position[] positions, float __) =>
                positions.Select(_ => (groundZ: 0f, found: false)).ToArray());
        configurePathfinding?.Invoke(pathfinding);

        // Wire 7-parameter GetPath to delegate to the 4-parameter overload (which
        // configurePathfinding may have overridden). This ensures both overloads
        // behave consistently when tests only configure the 4-parameter version.
        pathfinding
            .Setup(p => p.GetPath(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint mapId, Position start, Position end, IReadOnlyList<DynamicObjectProto>? nearbyObjects, bool smoothPath, Race race, Gender gender) =>
                pathfinding.Object.GetPath(mapId, start, end, smoothPath));

        ctx.Setup(c => c.ObjectManager).Returns(om.Object);
        ctx.Setup(c => c.Config).Returns(new BotBehaviorConfig());
        ctx.Setup(c => c.EventHandler).Returns(eventHandler.Object);
        ctx.Setup(c => c.BotTasks).Returns(stack);
        ctx.Setup(c => c.Container).Returns(container.Object);
        container.Setup(c => c.PathfindingClient).Returns(pathfinding.Object);
        container.Setup(c => c.ClassContainer).Returns(Mock.Of<IClassContainer>());
        container.Setup(c => c.QuestRepository).Returns((IQuestRepository?)null);

        return (ctx, om, stack);
    }

    internal static Mock<IWoWUnit> CreateUnit(ulong guid, string name, Position pos, uint health = 100)
    {
        var unit = new Mock<IWoWUnit>();
        unit.Setup(u => u.Guid).Returns(guid);
        unit.Setup(u => u.Name).Returns(name);
        unit.Setup(u => u.Position).Returns(pos);
        unit.Setup(u => u.Health).Returns(health);
        return unit;
    }

    internal static Mock<IWoWLocalPlayer> CreatePlayer(Position pos, bool inCombat = false)
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.Setup(p => p.Position).Returns(pos);
        player.Setup(p => p.IsInCombat).Returns(inCombat);
        player.Setup(p => p.MapId).Returns(0);
        return player;
    }

    internal static Mock<IWoWItem> CreateItem(uint itemId, uint stackCount = 1)
    {
        var item = new Mock<IWoWItem>();
        item.Setup(i => i.ItemId).Returns(itemId);
        item.Setup(i => i.Name).Returns($"Item_{itemId}");
        item.Setup(i => i.StackCount).Returns(stackCount);
        item.Setup(i => i.Guid).Returns(itemId);
        return item;
    }

    internal static Mock<IWoWGameObject> CreateGameObject(ulong guid, uint entry, uint typeId, Position pos, string name = "Fishing Pool")
    {
        var go = new Mock<IWoWGameObject>();
        go.Setup(g => g.Guid).Returns(guid);
        go.Setup(g => g.Entry).Returns(entry);
        go.Setup(g => g.TypeId).Returns(typeId);
        go.Setup(g => g.Position).Returns(pos);
        go.Setup(g => g.Name).Returns(name);
        go.Setup(g => g.CreatedBy).Returns(new HighGuid(0UL));
        return go;
    }

    internal static void RewindFishingTaskState(FishingTask task, int elapsedMs)
    {
        var stateEnteredAtField = typeof(FishingTask).GetField(
            "_stateEnteredAt",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FishingTask._stateEnteredAt field not found.");

        stateEnteredAtField.SetValue(task, DateTime.UtcNow.AddMilliseconds(-elapsedMs));
    }

    internal static void RewindFishingTaskSearchWaypoint(FishingTask task, int elapsedMs)
    {
        var searchWaypointEnteredAtField = typeof(FishingTask).GetField(
            "_searchWaypointEnteredAt",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FishingTask._searchWaypointEnteredAt field not found.");

        searchWaypointEnteredAtField.SetValue(task, DateTime.UtcNow.AddMilliseconds(-elapsedMs));
    }

    internal static void RewindFishingTaskApproachProgress(FishingTask task, int elapsedMs)
    {
        var approachProgressAtField = typeof(FishingTask).GetField(
            "_approachProgressAt",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FishingTask._approachProgressAt field not found.");

        approachProgressAtField.SetValue(task, DateTime.UtcNow.AddMilliseconds(-elapsedMs));
    }
}

// ==================== IdleTask Tests ====================

public class IdleTaskTests
{
    [Fact]
    public void Update_DoesNothing_NeverPops()
    {
        var (ctx, _, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new IdleTask(ctx.Object);
        stack.Push(task);

        for (int i = 0; i < 100; i++)
            task.Update();

        Assert.Single(stack);
        Assert.Same(task, stack.Peek());
    }
}

// ==================== StartAttackTask Tests ====================

public class StartAttackTaskTests
{
    [Fact]
    public void Update_SetsTargetAndStartsAttack()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new StartAttackTask(ctx.Object, 0xABCDUL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.SetTarget(0xABCDUL), Times.Once);
        om.Verify(o => o.StartMeleeAttack(), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_ZeroGuid_PopsWithoutAttacking()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new StartAttackTask(ctx.Object, 0UL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.SetTarget(It.IsAny<ulong>()), Times.Never);
        om.Verify(o => o.StartMeleeAttack(), Times.Never);
        Assert.Empty(stack);
    }
}

// ==================== StopAttackTask Tests ====================

public class StopAttackTaskTests
{
    [Fact]
    public void Update_StopsAttackAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new StopAttackTask(ctx.Object);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.StopAttack(), Times.Once);
        Assert.Empty(stack);
    }
}

// ==================== CastSpellTask Tests ====================

public class CastSpellTaskTests
{
    [Fact]
    public void Update_SetsTargetAndCastsSpell()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new CastSpellTask(ctx.Object, spellId: 133, targetGuid: 0x1234UL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.SetTarget(0x1234UL), Times.Once);
        om.Verify(o => o.CastSpell(133, -1, false), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NoTarget_CastsWithoutSettingTarget()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new CastSpellTask(ctx.Object, spellId: 774);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.SetTarget(It.IsAny<ulong>()), Times.Never);
        om.Verify(o => o.CastSpell(774, -1, false), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_CastOnSelf_PassesTrueFlag()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new CastSpellTask(ctx.Object, spellId: 1459, castOnSelf: true);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.CastSpell(1459, -1, true), Times.Once);
        Assert.Empty(stack);
    }
}

// ==================== FishingTask Tests ====================

public class FishingTaskTests
{
    [Fact]
    public void Update_WithoutEquippedPole_EquipsPoleBeforeCasting()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(100, 100, 0));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 1 }
        ]);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GetItem(0, 0)).Returns(AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object);
        om.Setup(o => o.GameObjects).Returns(Enumerable.Empty<IWoWGameObject>());
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.EquipItem(0, 0, null), Times.Once);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithVisiblePool_MovesTowardFishingRange()
    {
        // With a visible pool and a resolved shoreline sample outside the arrival radius,
        // FishingTask should move toward the approach position instead of casting in place.
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isApproachCandidate = Math.Abs(position.X + 30f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 0f, found: isApproachCandidate);
                        })
                        .ToArray());
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-45f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 1 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA11UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(0f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            position.X < 0f
            && Math.Abs(position.Y) < 0.5f
            && Math.Abs(position.Z) < 0.1f)), Times.AtLeastOnce);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithVisibleCastablePool_CastsFromCurrentPositionWithoutWalkingToApproach()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isApproachCandidate = Math.Abs(position.X + 30f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 0f, found: isApproachCandidate);
                        })
                        .ToArray());
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-14.5f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA13UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(0f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        task.Update();

        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        om.Verify(o => o.CastSpell((int)FishingData.FishingRank2, -1, false), Times.Once);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask in_cast_range_current", StringComparison.Ordinal))), Times.Once);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithVisiblePoolAtOuterNearRange_DoesNotUseCurrentPositionFastPath()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isApproachCandidate = Math.Abs(position.X + 30f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 0f, found: isApproachCandidate);
                        })
                        .ToArray());
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-21.8f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA14UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(0f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        task.Update();

        om.Verify(o => o.CastSpell((int)FishingData.FishingRank2, -1, false), Times.Once);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask in_cast_range_current", StringComparison.Ordinal))), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_IgnoresFarPoolAndKeepsFollowingSearchWaypoints()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA50UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(10f, 0f, 0f),
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 8f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.DoesNotContain(diagnostics, message => message.Contains("search_walk_found_pool", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("pool_acquired", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_OutsideTightArrivalRadius_KeepsClosingOnCurrentWaypoint()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var currentPosition = new Position(2f, 0f, 0f);
        var player = AtomicTaskTestHelpers.CreatePlayer(currentPosition);
        player.Setup(p => p.Position).Returns(() => currentPosition);
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA52UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(10f, 0f, 0f),
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 10f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.DoesNotContain(diagnostics, message => message.Contains("search_walk waypoint=1/2", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithFarWaypoint_UsesIntermediateTravelTarget()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5AUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 8f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithUnreachableFullStride_UsesShorterReachableStep()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint _, Position start, Position end, bool __) =>
                    Math.Abs(end.X) <= 4.1f
                        ? [start, end]
                        : []);
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5CUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 4f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_UsesValidatedProbeCorridorInsteadOfCuttingDirectlyToStrideTarget()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint _, Position start, Position end, bool __) =>
                    Math.Abs(end.X - 8f) <= 0.1f
                        ? [start, new Position(2f, 0f, 0f), new Position(4f, 0f, 0f), end]
                        : []);
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5DUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 2f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        om.Verify(o => o.SetNavigationPath(It.Is<Position[]>(path =>
            path.Length == 3
            && Math.Abs(path[0].X - 2f) < 0.01f
            && Math.Abs(path[1].X - 4f) < 0.01f
            && Math.Abs(path[2].X - 8f) < 0.01f)), Times.AtLeastOnce);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_RejectsProbeCorridorThatDropsOffCurrentSupportLayer()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint _, Position start, Position end, bool __) =>
                {
                    if (Math.Abs(end.X - 8f) <= 0.1f)
                    {
                        return
                        [
                            start,
                            new Position(2f, 0f, -3.1f),
                            new Position(4f, 0f, -3.1f),
                            end
                        ];
                    }

                    if (Math.Abs(end.X - 4f) <= 0.1f)
                        return [start, end];

                    return [];
                });
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5EUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 4f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 2f) < 0.01f
            && Math.Abs(position.Z + 3.1f) < 0.01f)), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithUnreachableWaypoint_SkipsUnsafeDirectFallback()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>());
            pf.Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns(false);
        });
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA53UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f),
            new Position(40f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        Assert.Contains(diagnostics, message => message.Contains("search_walk_unreachable waypoint=1/2", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithProbePathRejectedBySupportLayerGuard_SkipsWaypointWithoutRequeryingNavigation()
    {
        var probePath = new[]
        {
            new Position(0f, 0f, 1.2f),
            new Position(2f, 0f, 1.7f),
            new Position(4f, -0.5f, 1.9f),
            new Position(7f, 1.5f, 3.7f),
            new Position(7.5f, 2.8f, 4.2f),
            new Position(7.0f, 5.0f, 5.2f),
            new Position(6.5f, 5.2f, 5.7f),
            new Position(10f, 6f, 5.3f)
        };

        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(probePath);
            pf.Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns(false);
        });
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 1.2f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5CUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 1.2f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 1.2f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        om.Verify(o => o.SetNavigationPath(It.IsAny<Position[]>()), Times.Never);
        Assert.Contains(diagnostics, message => message.Contains("search_walk_unreachable waypoint=1/1", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithCandidateThatDoesNotAdvanceTowardWaypoint_SkipsWaypointWithoutGrindingNearCurrentPosition()
    {
        var nearCurrentCandidate = new Position(0.4f, 0.1f, 5f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) =>
                {
                    if (position.X < 10f)
                        return (1u, nearCurrentCandidate);

                    return (1u, position);
                });
            pf.Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint _, Position start, Position end, bool __) =>
                    end.X < 10f
                        ? new[]
                        {
                            new Position(start.X, start.Y, start.Z),
                            nearCurrentCandidate
                        }
                        : Array.Empty<Position>());
            pf.Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns(false);
        });
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 5f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5DUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 5f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 5f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        om.Verify(o => o.SetNavigationPath(It.IsAny<Position[]>()), Times.Never);
        Assert.Contains(diagnostics, message => message.Contains("search_walk_unreachable waypoint=1/1", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithStalledWaypoint_SkipsToNextProbe()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var currentPosition = new Position(0f, 0f, 0f);
        var player = AtomicTaskTestHelpers.CreatePlayer(currentPosition);
        player.Setup(p => p.Position).Returns(() => currentPosition);
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA55UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f),
            new Position(40f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Invocations.Clear();
        AtomicTaskTestHelpers.RewindFishingTaskSearchWaypoint(task, 21000);
        task.Update();
        currentPosition = new Position(32f, 0f, 0f);
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - 40f) < 0.01f
            && Math.Abs(position.Y) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.Contains(diagnostics, message => message.Contains("search_walk_stalled waypoint=1/2", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WhenMovementStuckRecoverySignals_SkipsCurrentProbeBeforeFullTimeout()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var currentPosition = new Position(0f, 0f, 0f);
        var stuckRecoveryGeneration = 0;
        var player = AtomicTaskTestHelpers.CreatePlayer(currentPosition);
        player.Setup(p => p.Position).Returns(() => currentPosition);
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA56UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));
        om.Setup(o => o.MovementStuckRecoveryGeneration).Returns(() => stuckRecoveryGeneration);

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f),
            new Position(0f, 20f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Invocations.Clear();
        stuckRecoveryGeneration = 1;
        AtomicTaskTestHelpers.RewindFishingTaskSearchWaypoint(task, 2000);

        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X) < 0.01f
            && Math.Abs(position.Y - 8f) < 0.01f
            && Math.Abs(position.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.Contains(diagnostics, message =>
            message.Contains("search_walk_stalled waypoint=1/2", StringComparison.Ordinal)
            && message.Contains("reason=movement_stuck", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithProgressOnCurrentWaypoint_ResetsStallTimer()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var diagnostics = new List<string>();
        ctx.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(message => diagnostics.Add(message));

        var currentPosition = new Position(0f, 0f, 0f);
        var player = AtomicTaskTestHelpers.CreatePlayer(currentPosition);
        player.Setup(p => p.Position).Returns(() => currentPosition);
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA5BUL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 0f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        currentPosition = new Position(12f, 0f, 0f);
        AtomicTaskTestHelpers.RewindFishingTaskSearchWaypoint(task, 21000);
        om.Invocations.Clear();
        task.Update();

        Assert.DoesNotContain(diagnostics, message => message.Contains("search_walk_stalled", StringComparison.Ordinal));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_SnapsWaypointToNearestWalkablePointBeforeNavigating()
    {
        var snappedWaypoint = new Position(16f, 4f, 5f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) =>
                {
                    var shouldSnap = Math.Abs(position.X - 20f) < 0.01f && Math.Abs(position.Y) < 0.01f;
                    return shouldSnap ? (1u, snappedWaypoint) : (1u, position);
                });
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(10f, 0f, 5f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA54UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 5f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 5f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - snappedWaypoint.X) < 0.01f
            && Math.Abs(position.Y - snappedWaypoint.Y) < 0.01f
            && Math.Abs(position.Z - snappedWaypoint.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithLowerSupportSample_SnapsAgainstReferenceLayerInsteadOfDroppingBelowPier()
    {
        var lowerSupportCandidate = new Position(20f, 0f, 1.5f);
        var upperPierCandidate = new Position(20f, 0f, 5.2f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isProbeCenter = Math.Abs(position.X - 20f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 1.5f, found: isProbeCenter);
                        })
                        .ToArray());
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) =>
                {
                    if (Math.Abs(position.X - 20f) < 0.25f && Math.Abs(position.Y) < 0.25f)
                        return position.Z >= 4.5f
                            ? (1u, upperPierCandidate)
                            : (1u, lowerSupportCandidate);

                    return (1u, position);
                });
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(10f, 0f, 5f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA56UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 5f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 5f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - upperPierCandidate.X) < 0.01f
            && Math.Abs(position.Y - upperPierCandidate.Y) < 0.01f
            && Math.Abs(position.Z - upperPierCandidate.Z) < 0.01f)), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - lowerSupportCandidate.X) < 0.01f
            && Math.Abs(position.Y - lowerSupportCandidate.Y) < 0.01f
            && Math.Abs(position.Z - lowerSupportCandidate.Z) < 0.01f)), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_SearchWalk_WithLowerAndHigherProbeCandidates_PrefersHigherWalkableEdge()
    {
        var lowCandidate = new Position(19.5f, 0f, 2f);
        var highCandidate = new Position(21.5f, 0f, 5.5f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isCenterSample = Math.Abs(position.X - 20f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            var isHigherEdgeSample = Math.Abs(position.X - 22f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 5f, found: isCenterSample || isHigherEdgeSample);
                        })
                        .ToArray());
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) =>
                {
                    if (Math.Abs(position.X - 20f) < 0.25f && Math.Abs(position.Y) < 0.25f)
                        return (1u, lowCandidate);
                    if (Math.Abs(position.X - 22f) < 0.25f && Math.Abs(position.Y) < 0.25f)
                        return (1u, highCandidate);
                    return (1u, position);
                });
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(14f, 0f, 5f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var farPool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA57UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(80f, 0f, 5f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([farPool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object,
        [
            new Position(20f, 0f, 5f)
        ]);
        stack.Push(task);

        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 16000);
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            position.X > 21f
            && Math.Abs(position.Y) < 0.01f
            && position.Z >= 5f)), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - lowCandidate.X) < 0.01f
            && Math.Abs(position.Y - lowCandidate.Y) < 0.01f
            && Math.Abs(position.Z - lowCandidate.Z) < 0.01f)), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithLowerAndHigherWalkablePierCandidates_PrefersHigherSupportEdge()
    {
        var lowCandidate = new Position(-25f, 0f, 2f);
        var highCandidate = new Position(-28f, 0f, 5f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isLowSample = Math.Abs(position.X + 25f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            var isHighSample = Math.Abs(position.X + 28f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 5f, found: isLowSample || isHighSample);
                        })
                        .ToArray());
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) =>
                {
                    if (Math.Abs(position.X + 25f) < 0.25f && Math.Abs(position.Y) < 0.25f)
                        return (1u, lowCandidate);
                    if (Math.Abs(position.X + 28f) < 0.25f && Math.Abs(position.Y) < 0.25f)
                        return (1u, highCandidate);
                    return (1u, position);
                });
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-35f, 0f, 5f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA55UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(0f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            position.X < -23f
            && Math.Abs(position.Y) < 0.5f
            && Math.Abs(position.Z - highCandidate.Z) < 0.01f)), Times.AtLeastOnce);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithLowerPierSample_ApproachSnapStaysOnCurrentSupportLayer()
    {
        var lowerSupportCandidate = new Position(-25f, 0f, 1.4f);
        var upperPierCandidate = new Position(-25f, 0f, 5.1f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isApproachSample = Math.Abs(position.X + 25f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 1.4f, found: isApproachSample);
                        })
                        .ToArray());
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) =>
                {
                    if (Math.Abs(position.X + 25f) < 0.25f && Math.Abs(position.Y) < 0.25f)
                        return position.Z >= 4.5f
                            ? (1u, upperPierCandidate)
                            : (1u, lowerSupportCandidate);

                    return (1u, position);
                });
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-35f, 0f, 5f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA56UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(0f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - upperPierCandidate.X) < 0.01f
            && Math.Abs(position.Y - upperPierCandidate.Y) < 0.01f
            && Math.Abs(position.Z - upperPierCandidate.Z) < 0.01f)), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            Math.Abs(position.X - lowerSupportCandidate.X) < 0.01f
            && Math.Abs(position.Y - lowerSupportCandidate.Y) < 0.01f
            && Math.Abs(position.Z - lowerSupportCandidate.Z) < 0.01f)), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithOppositeSidePierCandidate_PrefersPlayerSideApproach()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions
                        .Select(position =>
                        {
                            var isPlayerSideCandidate = Math.Abs(position.X + 37f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            var isOppositeSideCandidate = Math.Abs(position.X - 10f) < 0.25f && Math.Abs(position.Y) < 0.25f;
                            return (groundZ: 0f, found: isPlayerSideCandidate || isOppositeSideCandidate);
                        })
                        .ToArray());
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-45f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);

        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA51UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(0f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            position.X < 0f
            && Math.Abs(position.Y) < 0.5f
            && Math.Abs(position.Z) < 0.1f)), Times.AtLeastOnce);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithPoolInsideTaskCastWindow_CastsAtLosFriendlyWaterTarget()
    {
        // Player at (0,0,0), pool at (20,0,0) — 20y distance is within casting range.
        // State machine: EnsurePole → EnsureLure → AcquirePool → MoveToPool → ResolveAndCast.
        // MoveToPool immediately transitions to ResolveAndCast because player is already at approach.
        // CastStabilizeDelay requires 250ms, so rewind must happen AFTER entering ResolveAndCast.
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        var poolPosition = new Position(20f, 0f, 0f);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA12UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: poolPosition);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        // Updates 1-3: EnsurePole → EnsureLure → AcquirePool
        task.Update();
        task.Update();
        task.Update();
        // Update 4: MoveToPool → transitions to ResolveAndCast (player at approach)
        task.Update();
        // Rewind AFTER entering ResolveAndCast to satisfy CastStabilizeDelay
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        // Update 5: ResolveAndCast — casts the spell
        task.Update();

        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
        om.Verify(o => o.CastSpell((int)FishingData.FishingRank2, -1, false), Times.Once);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask cast_started", StringComparison.Ordinal)
            && message.Contains("mode=fishing_cast", StringComparison.Ordinal))), Times.Once);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WhileWaitingToCast_DoesNotResendStopAndFacingEveryTick()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        var poolPosition = new Position(20f, 0f, 0f);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA12UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: poolPosition);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Invocations.Clear();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 100);
        task.Update();
        task.Update();

        om.Verify(o => o.Face(It.IsAny<Position>()), Times.Once);
        om.Verify(o => o.ForceStopImmediate(), Times.Once);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithPoolInsideTaskCastWindowButLosBlocked_RepositionsInsteadOfCasting()
    {
        // LOS is blocked from Y<=1 positions. Player at (0,0,0) has LOS blocked.
        // After entering ResolveAndCast, the task should detect LOS blocked and transition back to
        // MoveToFishingPool, which navigates toward an approach candidate.
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.IsInLineOfSight(
                    It.IsAny<uint>(),
                    It.IsAny<Position>(),
                    It.IsAny<Position>()))
                .Returns((uint _, Position from, Position to) => from.Y > 1f || to.Y > 1f);
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        var poolPosition = new Position(20f, 0f, 0f);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA13UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: poolPosition);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        // 1-3: EnsurePole → EnsureLure → AcquirePool
        task.Update();
        task.Update();
        task.Update();
        // 4: MoveToPool → ResolveAndCast (at approach)
        task.Update();
        // 5: ResolveAndCast → LOS blocked → back to MoveToFishingPool
        task.Update();
        // 6: MoveToFishingPool navigates toward approach via pathfinding
        task.Update();

        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.AtLeastOnce);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_AfterCastRetry_RepositionsAwayFromRejectedApproach()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions.Select(position =>
                    {
                        var isCurrentApproach = MathF.Abs(position.X - 1f) < 0.6f && MathF.Abs(position.Y) < 0.6f;
                        var isAlternateApproach = MathF.Abs(position.X - 4f) < 0.6f && MathF.Abs(position.Y) < 0.6f;
                        return (groundZ: 0f, found: isCurrentApproach || isAlternateApproach);
                    }).ToArray());
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) => (1u, position));
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        player.Setup(p => p.ChannelingId).Returns(0u);
        player.Setup(p => p.IsChanneling).Returns(false);

        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA14UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(20f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        task.Update();

        AtomicTaskTestHelpers.RewindFishingTaskState(task, 5001);
        task.Update();

        om.Invocations.Clear();
        task.Update();
        task.Update();

        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask reject_approach", StringComparison.Ordinal))), Times.Once);
        om.Verify(o => o.MoveToward(It.Is<Position>(position =>
            position.DistanceTo2D(new Position(1f, 0f, 0f)) > 2f)), Times.AtLeastOnce);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WhenApproachStalls_RepositionsAwayFromRejectedApproachTarget()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pf =>
        {
            pf.Setup(p => p.BatchGetGroundZ(It.IsAny<uint>(), It.IsAny<Position[]>(), It.IsAny<float>()))
                .Returns((uint _, Position[] positions, float __) =>
                    positions.Select(position =>
                    {
                        var isInitialApproach = MathF.Abs(position.X - 4f) < 0.6f && MathF.Abs(position.Y) < 0.6f;
                        var isAlternateApproach = MathF.Abs(position.X - 9f) < 0.6f && MathF.Abs(position.Y) < 0.6f;
                        return (groundZ: 0f, found: isInitialApproach || isAlternateApproach);
                    }).ToArray());
            pf.Setup(p => p.FindNearestWalkablePoint(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<float>()))
                .Returns((uint _, Position position, float __) => (1u, position));
            pf.Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns((uint _, Position fromPosition, Position __) => fromPosition.X > 2f);
        });

        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1f, 0f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        player.Setup(p => p.IsMoving).Returns(false);
        player.Setup(p => p.ChannelingId).Returns(0u);
        player.Setup(p => p.IsChanneling).Returns(false);

        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA15UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(20f, 0f, 0f));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object]);
        om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank2]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank2, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Invocations.Clear();
        AtomicTaskTestHelpers.RewindFishingTaskApproachProgress(task, 13000);
        task.Update();
        task.Update();
        task.Update();

        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask reject_approach", StringComparison.Ordinal)
            && message.Contains("reason=approach_stalled", StringComparison.Ordinal))), Times.Once);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask retry reason=approach_stalled", StringComparison.Ordinal))), Times.Once);
        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.AtLeastOnce);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithAvailableBait_UsesLureBeforeApproachingPool()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-995f, -3850f, 4f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 75 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(false);

        om.Setup(o => o.Player).Returns(player.Object);
        var equippedPole = AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole);
        equippedPole.Setup(i => i.Guid).Returns(0UL);
        om.Setup(o => o.GetEquippedItems()).Returns([equippedPole.Object]);
        om.Setup(o => o.GetEquippedItem(EquipSlot.MainHand)).Returns(equippedPole.Object);
        om.Setup(o => o.GetEquippedItemGuid(EquipSlot.MainHand)).Returns(0xF1A5UL);
        om.Setup(o => o.GetContainedItems()).Returns([AtomicTaskTestHelpers.CreateItem(FishingData.NightcrawlerBait).Object]);
        om.Setup(o => o.GetItem(0, 1)).Returns(AtomicTaskTestHelpers.CreateItem(FishingData.NightcrawlerBait).Object);
        om.Setup(o => o.GameObjects).Returns(Enumerable.Empty<IWoWGameObject>());
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        task.Update();
        task.Update();

        om.Verify(o => o.UseItem(0, 1, 0xF1A5UL), Times.Once);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask lure_use_started", StringComparison.Ordinal))), Times.Once);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_AfterLootWindowLootsCatchAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        // Player at exactly 24y from pool (DesiredPoolDistance) on same Z → within approach arrival radius.
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-999.7f, -3835.2f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 1 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xBB22UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(-975.7f, -3835.2f, 0f));
        var equippedPole = AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object;
        var caughtFish = AtomicTaskTestHelpers.CreateItem(6361).Object;

        uint channelingId = 0;
        bool isChanneling = false;
        bool lootOpen = false;
        IEnumerable<IWoWItem> bagItems = Enumerable.Empty<IWoWItem>();
        player.Setup(p => p.ChannelingId).Returns(() => channelingId);
        player.Setup(p => p.IsChanneling).Returns(() => isChanneling);

        var lootFrame = new Mock<ILootFrame>();
        lootFrame.Setup(l => l.IsOpen).Returns(() => lootOpen);
        lootFrame.Setup(l => l.LootCount).Returns(() => lootOpen ? 1 : 0);
        lootFrame.Setup(l => l.LootItems).Returns(Array.Empty<LootItem>());
        lootFrame.Setup(l => l.Close()).Callback(() => lootOpen = false);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([equippedPole]);
        om.Setup(o => o.GetContainedItems()).Returns(() => bagItems);
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank1]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank1, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));
        om.Setup(o => o.LootFrame).Returns(lootFrame.Object);

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        // 1-3: EnsurePole → EnsureLure → AcquirePool
        task.Update();
        task.Update();
        task.Update();
        // 4: MoveToPool → ResolveAndCast (player at ~13y from pool, within approach)
        task.Update();
        // Rewind AFTER entering ResolveAndCast
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        // 5: ResolveAndCast → casts
        task.Update();

        om.Verify(o => o.CastSpell((int)FishingData.FishingRank1, -1, false), Times.Once);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);

        channelingId = FishingData.FishingRank1;
        isChanneling = true;
        task.Update();
        Assert.Single(stack);

        channelingId = 0;
        isChanneling = false;
        task.Update();
        lootOpen = true;
        task.Update();
        bagItems = [caughtFish];
        task.Update();
        task.Update();
        task.Update();

        lootFrame.Verify(l => l.LootAll(), Times.Once);
        lootFrame.Verify(l => l.Close(), Times.AtLeastOnce);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask loot_window_open", StringComparison.Ordinal)
            || message.Contains("FishingTask fishing_loot_success", StringComparison.Ordinal))), Times.AtLeastOnce);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_BagDeltaAfterFishingWithoutLootWindow_PopsSuccess()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        // Player at exactly 24y from pool (DesiredPoolDistance) on same Z → within approach arrival radius.
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-999.7f, -3835.2f, 0f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 1 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xCC33UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(-975.7f, -3835.2f, 0f));
        var equippedPole = AtomicTaskTestHelpers.CreateItem(FishingData.FishingPole).Object;
        var caughtFish = AtomicTaskTestHelpers.CreateItem(6361).Object;

        uint channelingId = 0;
        bool isChanneling = false;
        IEnumerable<IWoWItem> bagItems = Enumerable.Empty<IWoWItem>();
        player.Setup(p => p.ChannelingId).Returns(() => channelingId);
        player.Setup(p => p.IsChanneling).Returns(() => isChanneling);

        var lootFrame = new Mock<ILootFrame>();
        lootFrame.Setup(l => l.IsOpen).Returns(false);
        lootFrame.Setup(l => l.LootCount).Returns(0);
        lootFrame.Setup(l => l.LootItems).Returns(Array.Empty<LootItem>());

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetEquippedItems()).Returns([equippedPole]);
        om.Setup(o => o.GetContainedItems()).Returns(() => bagItems);
        om.Setup(o => o.KnownSpellIds).Returns([FishingData.FishingRank1]);
        om.Setup(o => o.CanCastSpell((int)FishingData.FishingRank1, 0UL)).Returns(true);
        om.Setup(o => o.GameObjects).Returns([pool.Object]);
        om.Setup(o => o.PlayerGuid).Returns(new HighGuid(0UL));
        om.Setup(o => o.LootFrame).Returns(lootFrame.Object);

        var task = new FishingTask(ctx.Object);
        stack.Push(task);

        // 1-3: EnsurePole → EnsureLure → AcquirePool
        task.Update();
        task.Update();
        task.Update();
        // 4: MoveToPool → ResolveAndCast (player at ~13y from pool, within approach)
        task.Update();
        // Rewind AFTER entering ResolveAndCast
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        // 5: ResolveAndCast → casts
        task.Update();

        om.Verify(o => o.CastSpell((int)FishingData.FishingRank1, -1, false), Times.Once);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);

        channelingId = FishingData.FishingRank1;
        isChanneling = true;
        task.Update();

        channelingId = 0;
        isChanneling = false;
        task.Update();
        bagItems = [caughtFish];
        task.Update();

        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask loot_bag_delta", StringComparison.Ordinal)
            || message.Contains("FishingTask fishing_loot_success", StringComparison.Ordinal))), Times.AtLeastOnce);
        Assert.Empty(stack);
    }
}

// ==================== UseItemTask Tests ====================

public class UseItemTaskTests
{
    [Fact]
    public void Update_UsesItemAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new UseItemTask(ctx.Object, bagId: 0, slotId: 3);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.UseItem(0, 3, 0UL), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_WithTarget_SetsTargetFirst()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new UseItemTask(ctx.Object, bagId: 1, slotId: 5, targetGuid: 0x9999UL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.SetTarget(0x9999UL), Times.Once);
        om.Verify(o => o.UseItem(1, 5, 0x9999UL), Times.Once);
        Assert.Empty(stack);
    }
}

// ==================== EquipItemTask Tests ====================

public class EquipItemTaskTests
{
    [Fact]
    public void Update_EquipsItemAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new EquipItemTask(ctx.Object, bagId: 0, slotId: 7);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.UseContainerItem(0, 7), Times.Once);
        Assert.Empty(stack);
    }
}

// ==================== ReleaseCorpseTask Tests ====================

public class ReleaseCorpseTaskTests
{
    [Fact]
    public void Update_ReleasesAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var task = new ReleaseCorpseTask(ctx.Object);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.ReleaseSpirit(), Times.Once);
        Assert.Empty(stack);
    }
}

// ==================== SelectGossipTask Tests ====================

public class SelectGossipTaskTests
{
    [Fact]
    public void Update_SelectsGossipOptionAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var gossipFrame = new Mock<IGossipFrame>();
        om.Setup(o => o.GossipFrame).Returns(gossipFrame.Object);

        var task = new SelectGossipTask(ctx.Object, optionIndex: 2);
        stack.Push(task);

        task.Update();

        gossipFrame.Verify(g => g.SelectGossipOption(2), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NoGossipFrame_PopsWithoutCrashing()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.GossipFrame).Returns((IGossipFrame?)null);

        var task = new SelectGossipTask(ctx.Object, optionIndex: 0);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== AcceptQuestTask Tests ====================

public class AcceptQuestTaskTests
{
    [Fact]
    public void Update_AcceptsQuestAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var questFrame = new Mock<IQuestFrame>();
        om.Setup(o => o.QuestFrame).Returns(questFrame.Object);

        var task = new AcceptQuestTask(ctx.Object);
        stack.Push(task);

        task.Update();

        questFrame.Verify(q => q.AcceptQuest(), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NoQuestFrame_PopsWithoutCrashing()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.QuestFrame).Returns((IQuestFrame?)null);

        var task = new AcceptQuestTask(ctx.Object);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== CompleteQuestTask Tests ====================

public class CompleteQuestTaskTests
{
    [Fact]
    public void Update_CompletesQuestWithRewardAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var questFrame = new Mock<IQuestFrame>();
        om.Setup(o => o.QuestFrame).Returns(questFrame.Object);

        var task = new CompleteQuestTask(ctx.Object, rewardIndex: 1);
        stack.Push(task);

        task.Update();

        questFrame.Verify(q => q.CompleteQuest(1), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_DefaultRewardIndex_PassesZero()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var questFrame = new Mock<IQuestFrame>();
        om.Setup(o => o.QuestFrame).Returns(questFrame.Object);

        var task = new CompleteQuestTask(ctx.Object);
        stack.Push(task);

        task.Update();

        questFrame.Verify(q => q.CompleteQuest(0), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NoQuestFrame_PopsWithoutCrashing()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.QuestFrame).Returns((IQuestFrame?)null);

        var task = new CompleteQuestTask(ctx.Object);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== TrainSpellTask Tests ====================

public class TrainSpellTaskTests
{
    [Fact]
    public void Update_TrainsSpellAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var trainerFrame = new Mock<ITrainerFrame>();
        om.Setup(o => o.TrainerFrame).Returns(trainerFrame.Object);

        var task = new TrainSpellTask(ctx.Object, spellIndex: 3);
        stack.Push(task);

        task.Update();

        trainerFrame.Verify(t => t.TrainSpell(3), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NoTrainerFrame_PopsWithoutCrashing()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.TrainerFrame).Returns((ITrainerFrame?)null);

        var task = new TrainSpellTask(ctx.Object, spellIndex: 0);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== LootCorpseTask Tests ====================

public class LootCorpseTaskTests
{
    [Fact]
    public void Update_LootsAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.LootTargetAsync(0xDEADUL, It.IsAny<System.Threading.CancellationToken>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        var task = new LootCorpseTask(ctx.Object, 0xDEADUL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.LootTargetAsync(0xDEADUL, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_LootFails_StillPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.LootTargetAsync(It.IsAny<ulong>(), It.IsAny<System.Threading.CancellationToken>()))
            .Throws(new System.Exception("No loot"));

        var task = new LootCorpseTask(ctx.Object, 123UL);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== SkinCorpseTask Tests ====================

public class SkinCorpseTaskTests
{
    [Fact]
    public void Update_SkinsAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.LootTargetAsync(0xBEEFUL, It.IsAny<System.Threading.CancellationToken>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        var task = new SkinCorpseTask(ctx.Object, 0xBEEFUL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.LootTargetAsync(0xBEEFUL, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_SkinFails_StillPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.LootTargetAsync(It.IsAny<ulong>(), It.IsAny<System.Threading.CancellationToken>()))
            .Throws(new System.Exception("Not skinnable"));

        var task = new SkinCorpseTask(ctx.Object, 456UL);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== InteractWithUnitTask Tests ====================

public class InteractWithUnitTaskTests
{
    [Fact]
    public void Update_UnitNotFound_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(100, 100, 0));
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.Units).Returns(Enumerable.Empty<IWoWUnit>());

        var task = new InteractWithUnitTask(ctx.Object, 0x5555UL);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }

    [Fact]
    public void Update_UnitFarAway_NavigatesToward()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        var npc = AtomicTaskTestHelpers.CreateUnit(0x7777UL, "Innkeeper", new Position(100, 100, 0));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.Units).Returns(new[] { npc.Object });

        var task = new InteractWithUnitTask(ctx.Object, 0x7777UL);
        stack.Push(task);

        task.Update();

        // Should still be on stack (navigating, not arrived)
        Assert.Single(stack);
    }

    [Fact]
    public void Update_UnitInRange_SetsTargetAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(100, 100, 0));
        var npc = AtomicTaskTestHelpers.CreateUnit(0x8888UL, "Vendor", new Position(102, 100, 0));

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.Units).Returns(new[] { npc.Object });

        var task = new InteractWithUnitTask(ctx.Object, 0x8888UL);
        stack.Push(task);

        // First update: in range → stop + set target
        task.Update();
        // Second update: _interacted is true → pop
        task.Update();

        om.Verify(o => o.StopAllMovement(), Times.Once);
        om.Verify(o => o.SetTarget(0x8888UL), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NullPlayer_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        var task = new InteractWithUnitTask(ctx.Object, 0x1111UL);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== MoveToPositionTask Tests ====================

public class MoveToPositionTaskTests
{
    [Fact]
    public void Update_AlreadyAtDestination_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(50, 50, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new MoveToPositionTask(ctx.Object, new Position(51, 50, 0), tolerance: 3f);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.StopAllMovement(), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_FarFromDestination_DoesNotPop()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new MoveToPositionTask(ctx.Object, new Position(200, 200, 0));
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
    }

    [Fact]
    public void Update_InCombat_StopsAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0), inCombat: true);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new MoveToPositionTask(ctx.Object, new Position(200, 200, 0));
        stack.Push(task);

        task.Update();

        om.Verify(o => o.StopAllMovement(), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NullPlayer_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        var task = new MoveToPositionTask(ctx.Object, new Position(100, 100, 0));
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== RetrieveCorpseTask Tests ====================

public class RetrieveCorpseTaskTests
{
    [Fact]
    public void FormatNavigationTraceSummary_IncludesKeyFieldsAndTruncatesPathsAndSamples()
    {
        var trace = new NavigationTraceSnapshot(
            MapId: 1,
            RequestedStart: new Position(0f, 0f, 0f),
            RequestedDestination: new Position(100f, 50f, 0f),
            ServiceWaypoints:
            [
                new Position(0f, 0f, 0f),
                new Position(10f, 0f, 0f),
                new Position(20f, 5f, 0f),
                new Position(30f, 10f, 0f),
                new Position(40f, 15f, 0f)
            ],
            PlannedWaypoints:
            [
                new Position(0f, 0f, 0f),
                new Position(12f, 1f, 0f),
                new Position(24f, 7f, 0f),
                new Position(36f, 12f, 0f),
                new Position(48f, 18f, 0f)
            ],
            Affordances: PathAffordanceInfo.Empty,
            ActiveWaypoint: new Position(24f, 7f, 0f),
            CurrentWaypointIndex: 2,
            PlanVersion: 3,
            LastReplanReason: NavigationTraceReason.StalledNearWaypoint,
            LastResolution: "waypoint",
            UsedDirectFallback: false,
            UsedNearbyObjectOverlay: true,
            NearbyObjectCount: 6,
            SmoothPath: false,
            IsShortRoute: false,
            LastPlanTick: 1234,
            ExecutionSamples:
            [
                new NavigationExecutionSample(new Position(0f, 0f, 0f), new Position(100f, 50f, 0f), new Position(10f, 0f, 0f), 1, 1, 10f, false, 100, "waypoint"),
                new NavigationExecutionSample(new Position(9f, 0f, 0f), new Position(100f, 50f, 0f), new Position(20f, 5f, 0f), 2, 2, 12f, false, 200, "waypoint"),
                new NavigationExecutionSample(new Position(18f, 4f, 0f), new Position(100f, 50f, 0f), new Position(24f, 7f, 0f), 2, 3, 6f, false, 300, "waypoint"),
                new NavigationExecutionSample(new Position(23f, 6f, 0f), new Position(100f, 50f, 0f), new Position(36f, 12f, 0f), 3, 3, 14f, false, 400, "waypoint")
            ]);

        var summary = RetrieveCorpseTask.FormatNavigationTraceSummary(trace);

        Assert.Contains("plan=3", summary);
        Assert.Contains($"reason={NavigationTraceReason.StalledNearWaypoint}", summary);
        Assert.Contains("resolution=waypoint", summary);
        Assert.Contains("idx=2", summary);
        Assert.Contains("overlay=6", summary);
        Assert.Contains("request=(0.0,0.0,0.0)->(100.0,50.0,0.0)", summary);
        Assert.Contains("service=[(0.0,0.0,0.0), (10.0,0.0,0.0), (20.0,5.0,0.0), (30.0,10.0,0.0), +1 more]", summary);
        Assert.Contains("planned=[(0.0,0.0,0.0), (12.0,1.0,0.0), (24.0,7.0,0.0), (36.0,12.0,0.0), +1 more]", summary);
        Assert.Contains("samples=[+1 earlier", summary);
        Assert.Contains("p3:waypoint:idx3:(23.0,6.0,0.0)->(36.0,12.0,0.0)", summary);
    }

    [Fact]
    public void PathAffordanceInfo_Classify_LabelsSegmentsCorrectly()
    {
        var waypoints = new Position[]
        {
            new(0f, 0f, 0f),      // start
            new(10f, 0f, 0.3f),   // walk (flat, <1y Z)
            new(20f, 0f, 2.5f),   // step-up (Z gain 2.2y)
            new(20.1f, 0f, 8f),   // vertical (2D < 0.5y, Z > 2y)
            new(30f, 0f, 5f),     // drop (Z loss 3y)
            new(40f, 0f, -3f),    // cliff (Z loss 8y)
        };

        var info = PathAffordanceInfo.Classify(waypoints);

        Assert.Equal(5, info.Segments.Length);
        Assert.Equal(SegmentAffordance.Walk, info.Segments[0]);
        Assert.Equal(SegmentAffordance.StepUp, info.Segments[1]);
        Assert.Equal(SegmentAffordance.Vertical, info.Segments[2]);
        Assert.Equal(SegmentAffordance.Drop, info.Segments[3]);
        Assert.Equal(SegmentAffordance.Cliff, info.Segments[4]);
        Assert.Equal(1, info.VerticalCount);
        Assert.Equal(1, info.DropCount);
        Assert.Equal(1, info.CliffCount);
        Assert.True(info.TotalZGain > 0f);
        Assert.True(info.TotalZLoss > 0f);
    }

    [Fact]
    public void PathAffordanceInfo_Classify_EmptyPath_ReturnsEmpty()
    {
        var info = PathAffordanceInfo.Classify([]);
        Assert.Empty(info.Segments);
        Assert.Equal(0, info.StepUpCount);
    }

    private static void ForceRunbackSampleReady(RetrieveCorpseTask task)
    {
        var sampleField = typeof(RetrieveCorpseTask).GetField(
            "_lastRunbackSampleUtc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _lastRunbackSampleUtc field.");
        sampleField.SetValue(task, DateTime.UtcNow - TimeSpan.FromSeconds(2));
    }

    private static void ForceRunbackProgressExpired(RetrieveCorpseTask task, float bestDistance2D = 95f)
    {
        var bestField = typeof(RetrieveCorpseTask).GetField(
            "_bestRunbackCorpseDistance2D",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _bestRunbackCorpseDistance2D field.");
        bestField.SetValue(task, bestDistance2D);

        var lastProgressField = typeof(RetrieveCorpseTask).GetField(
            "_lastRunbackProgressUtc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _lastRunbackProgressUtc field.");
        lastProgressField.SetValue(task, DateTime.UtcNow - TimeSpan.FromSeconds(25));
    }

    private static void ForceRunbackWaypointProgressExpired(
        RetrieveCorpseTask task,
        Position trackedWaypoint,
        float bestWaypointDistance = 20f)
    {
        var trackedField = typeof(RetrieveCorpseTask).GetField(
            "_trackedRunbackWaypoint",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _trackedRunbackWaypoint field.");
        trackedField.SetValue(task, trackedWaypoint);

        var bestField = typeof(RetrieveCorpseTask).GetField(
            "_bestRunbackWaypointDistance",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _bestRunbackWaypointDistance field.");
        bestField.SetValue(task, bestWaypointDistance);

        var lastProgressField = typeof(RetrieveCorpseTask).GetField(
            "_lastRunbackWaypointProgressUtc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _lastRunbackWaypointProgressUtc field.");
        lastProgressField.SetValue(task, DateTime.UtcNow - TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void Update_AtCorpseAsGhost_RetrievesAndRemainsOnStack()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var corpsePos = new Position(50, 50, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(51, 50, 0));
        player.Setup(p => p.Health).Returns(0u);
        player.Setup(p => p.InGhostForm).Returns(true);
        player.Setup(p => p.CorpseRecoveryDelaySeconds).Returns(0);
        player.Setup(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);
        player.Setup(p => p.Bytes1).Returns([0u]);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.ForceStopImmediate(), Times.Once);
        om.Verify(o => o.RetrieveCorpse(), Times.Once);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_AtCorpseAlive_PopsWithoutRetrieve()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var corpsePos = new Position(50, 50, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(51, 50, 0));
        player.Setup(p => p.Health).Returns(100u);
        player.Setup(p => p.InGhostForm).Returns(false);
        player.Setup(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_NONE);
        player.Setup(p => p.Bytes1).Returns([0u]);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.ForceStopImmediate(), Times.Once);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_FarFromCorpse_WithRoute_DrivesWaypoint()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var corpsePos = new Position(500, 500, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        // Should still be on stack (navigating)
        Assert.Single(stack);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        om.Verify(o => o.StopMovement(
            ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight), Times.Never);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - corpsePos.X) < 0.01f &&
            MathF.Abs(p.Y - corpsePos.Y) < 0.01f &&
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.Once);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_WithProbeStyleLeadIn_DrivesFirstServiceWaypoint()
    {
        var corpsePos = new Position(100, 0, 0);
        // firstWaypoint (2.8y) is within WAYPOINT_REACH_DISTANCE (3.5y) of origin,
        // so the non-strict advance loop skips past it. secondWaypoint (4.2y) is
        // outside the radius and becomes the drive target.
        var firstWaypoint = new Position(2.8f, 0f, 0f);
        var secondWaypoint = new Position(4.2f, 0f, 0f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    firstWaypoint,
                    secondWaypoint,
                    new Position(end.X, end.Y, end.Z)
                ]));
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        om.Verify(o => o.StopMovement(
            ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight), Times.Never);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - secondWaypoint.X) < 0.01f &&
            MathF.Abs(p.Y - secondWaypoint.Y) < 0.01f &&
            MathF.Abs(p.Z - secondWaypoint.Z) < 0.01f)), Times.Once);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_WithValidCornerRoute_DrivesFirstServiceWaypointInsteadOfDirectCorpse()
    {
        var corpsePos = new Position(100, 100, 0);
        var firstCorner = new Position(20f, 0f, 0f);
        var secondCorner = new Position(20f, 40f, 0f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    firstCorner,
                    secondCorner,
                    new Position(end.X, end.Y, end.Z)
                ]));
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        om.Verify(o => o.StopMovement(
            ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight), Times.Never);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - firstCorner.X) < 0.01f &&
            MathF.Abs(p.Y - firstCorner.Y) < 0.01f &&
            MathF.Abs(p.Z - firstCorner.Z) < 0.01f)), Times.Once);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - corpsePos.X) < 0.01f &&
            MathF.Abs(p.Y - corpsePos.Y) < 0.01f &&
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.Never);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_WithInvalidServicePath_DoesNotDriveRouteOrDirectCorpse()
    {
        var corpsePos = new Position(500, 500, 0);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    new Position(float.NaN, start.Y + 3f, start.Z),
                    new Position(end.X, end.Y, end.Z)
                ]));
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_WithServicePathBlockedByLineOfSight_DrivesFirstCornerInNonStrictMode()
    {
        // Corpse run NavigationPath uses strictPathValidation: false and
        // enableProbeHeuristics: false, so LOS blocks between corners do NOT
        // prevent the bot from driving the navmesh path. The bot trusts the
        // navmesh and drives the first corner waypoint.
        var corpsePos = new Position(100, 100, 0);
        var firstCorner = new Position(20f, 0f, 0f);
        var blockedCorner = new Position(20f, 30f, 0f);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
        {
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    firstCorner,
                    blockedCorner,
                    new Position(end.X, end.Y, end.Z)
                ]);
            pathfinding
                .Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
                .Returns((uint mapId, Position from, Position to) =>
                {
                    var blockedSegment =
                        MathF.Abs(from.X - firstCorner.X) < 0.01f &&
                        MathF.Abs(from.Y - firstCorner.Y) < 0.01f &&
                        MathF.Abs(to.X - blockedCorner.X) < 0.01f &&
                        MathF.Abs(to.Y - blockedCorner.Y) < 0.01f;
                    return !blockedSegment;
                });
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - firstCorner.X) < 0.01f &&
            MathF.Abs(p.Y - firstCorner.Y) < 0.01f &&
            MathF.Abs(p.Z - firstCorner.Z) < 0.01f)), Times.Once);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_NoPath_StopsAndTriggersRecoveryInsteadOfDirectFallbackDrive()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>()));
        var corpsePos = new Position(500, 500, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
        om.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - corpsePos.X) < 0.01f &&
            MathF.Abs(p.Y - corpsePos.Y) < 0.01f &&
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_NoPath_NoMoveFlags_TriggersStallRecovery()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>()));
        var corpsePos = new Position(500, 500, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        player.Setup(p => p.MovementFlags).Returns((MovementFlags)0);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        for (int i = 0; i < 12; i++)
        {
            ForceRunbackSampleReady(task);
            task.Update();
        }

        Assert.Single(stack);
        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - corpsePos.X) < 0.01f &&
            MathF.Abs(p.Y - corpsePos.Y) < 0.01f &&
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.Never);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
        om.Verify(o => o.StopMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_AfterStallRecovery_DoesNotIssueSyntheticStrafeCommands()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Position>()));
        var corpsePos = new Position(500, 500, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0, 0, 0));
        player.Setup(p => p.MovementFlags).Returns((MovementFlags)0);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        for (int i = 0; i < 12; i++)
        {
            ForceRunbackSampleReady(task);
            task.Update();
        }

        Assert.Single(stack);
        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
        om.Verify(o => o.StartMovement(It.IsAny<ControlBits>()), Times.Never);
        om.Verify(o => o.StopMovement(It.IsAny<ControlBits>()), Times.Never);
    }

    [Fact]
    public void Update_FarFromCorpse_MovingButNoDistanceProgress_TriggersStallRecovery()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var corpsePos = new Position(100, 0, 0);

        var currentPosition = new Position(0, 0, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(currentPosition);
        player.Setup(p => p.Position).Returns(() => currentPosition);
        player.Setup(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_FORWARD);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        // Seed baseline runback tracking.
        task.Update();

        // Simulate movement that changes position but does not improve corpse distance.
        currentPosition = new Position(0, 0.5f, 0);
        ForceRunbackProgressExpired(task);
        ForceRunbackSampleReady(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
    }

    [Fact]
    public void Update_FarFromCorpse_MovingButNoWaypointProgress_TriggersStallRecovery()
    {
        var forcedWaypoint = new Position(0, 20, 0);
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(pathfinding =>
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    forcedWaypoint,
                    new Position(end.X, end.Y, end.Z)
                ]));
        var corpsePos = new Position(100, 0, 0);

        var currentPosition = new Position(0, 0, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(currentPosition);
        player.Setup(p => p.Position).Returns(() => currentPosition);
        player.Setup(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_FORWARD);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        // Seed runback samples and waypoint tracking.
        task.Update();

        currentPosition = new Position(1f, 0, 0);
        ForceRunbackSampleReady(task);
        ForceRunbackWaypointProgressExpired(task, forcedWaypoint, bestWaypointDistance: 20f);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
    }

    [Fact]
    public void Update_WithinRetrieveRangeAsGhost_WithCooldown_WaitsWithoutMovementOrRetrieve()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var corpsePos = new Position(50, 50, 0);
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(60, 50, 0));
        player.Setup(p => p.Health).Returns(0u);
        player.Setup(p => p.InGhostForm).Returns(true);
        player.Setup(p => p.CorpseRecoveryDelaySeconds).Returns(7);
        player.Setup(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);
        player.Setup(p => p.Bytes1).Returns([0u]);
        om.Setup(o => o.Player).Returns(player.Object);

        var task = new RetrieveCorpseTask(ctx.Object, corpsePos);
        stack.Push(task);

        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.ForceStopImmediate(), Times.Once);
        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        om.Verify(o => o.RetrieveCorpse(), Times.Never);
    }

    [Fact]
    public void Update_NullPlayer_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        var task = new RetrieveCorpseTask(ctx.Object, new Position(50, 50, 0));
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}

// ==================== GatherNodeTask Tests ====================

public class GatherNodeTaskTests
{
    [Fact]
    public void Update_NodeNotFound_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(100, 100, 0));
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GameObjects).Returns(Enumerable.Empty<IWoWGameObject>());

        var task = new GatherNodeTask(ctx.Object, 0xAAAAUL);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }

    [Fact]
    public void Update_InCombat_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(100, 100, 0), inCombat: true);
        om.Setup(o => o.Player).Returns(player.Object);

        var node = new Mock<IWoWGameObject>();
        node.Setup(n => n.Guid).Returns(0xBBBBUL);
        node.Setup(n => n.Position).Returns(new Position(102, 100, 0));
        node.Setup(n => n.Name).Returns("Copper Vein");
        om.Setup(o => o.GameObjects).Returns(new[] { node.Object });

        var task = new GatherNodeTask(ctx.Object, 0xBBBBUL);
        stack.Push(task);

        task.Update();

        om.Verify(o => o.StopAllMovement(), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NodeInRange_SetsTargetAndPops()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(100, 100, 0));
        om.Setup(o => o.Player).Returns(player.Object);

        var node = new Mock<IWoWGameObject>();
        node.Setup(n => n.Guid).Returns(0xCCCCUL);
        node.Setup(n => n.Position).Returns(new Position(102, 100, 0));
        node.Setup(n => n.Name).Returns("Peacebloom");
        om.Setup(o => o.GameObjects).Returns(new[] { node.Object });

        var task = new GatherNodeTask(ctx.Object, 0xCCCCUL);
        stack.Push(task);

        // First update: in range → stop + set target
        task.Update();
        // Second update: _interacted → pop
        task.Update();

        om.Verify(o => o.StopAllMovement(), Times.Once);
        om.Verify(o => o.SetTarget(0xCCCCUL), Times.Once);
        Assert.Empty(stack);
    }

    [Fact]
    public void Update_NullPlayer_PopsImmediately()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        var task = new GatherNodeTask(ctx.Object, 0xDDDDUL);
        stack.Push(task);

        task.Update();

        Assert.Empty(stack);
    }
}
