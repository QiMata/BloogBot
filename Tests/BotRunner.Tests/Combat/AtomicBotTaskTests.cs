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
            .Setup(p => p.GetPath(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>>(),
                It.IsAny<bool>()))
            .Returns((uint mapId, Position start, Position end, IReadOnlyList<DynamicObjectProto>? nearbyObjects, bool smoothPath) =>
            [
                new Position(start.X, start.Y, start.Z),
                new Position(end.X, end.Y, end.Z)
            ]);
        pathfinding
            .Setup(p => p.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
            .Returns(true);
        configurePathfinding?.Invoke(pathfinding);

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
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-995f, -3850f, 4f));
        player.Setup(p => p.SkillInfo).Returns(
        [
            new SkillInfo { SkillInt1 = FishingData.FishingSkillId, SkillInt2 = 1 }
        ]);
        player.Setup(p => p.MainhandIsEnchanted).Returns(true);
        var pool = AtomicTaskTestHelpers.CreateGameObject(
            guid: 0xAA11UL,
            entry: 180582,
            typeId: (uint)GameObjectType.FishingHole,
            pos: new Position(-975.7f, -3835.2f, 0f));

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

        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.AtLeastOnce);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithPoolInsideTaskCastWindow_CastsAtLosFriendlyWaterTarget()
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
        var expectedCastTarget = FishingData.GetPoolCastTarget(player.Object.Position!, poolPosition, 4f);
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
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        task.Update();

        om.Verify(o => o.ForceStopImmediate(), Times.AtLeastOnce);
        om.Verify(o => o.MoveToward(It.IsAny<Position>()), Times.Never);
        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(
            (int)FishingData.FishingRank2,
            It.Is<float>(x => MathF.Abs(x - expectedCastTarget.X) < 0.01f),
            It.Is<float>(y => MathF.Abs(y - expectedCastTarget.Y) < 0.01f),
            It.Is<float>(z => MathF.Abs(z - expectedCastTarget.Z) < 0.01f)), Times.Once);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask cast_started", StringComparison.Ordinal)
            && message.Contains($"target=({expectedCastTarget.X:F1},{expectedCastTarget.Y:F1},{expectedCastTarget.Z:F1})", StringComparison.Ordinal))), Times.Once);
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WithPoolInsideTaskCastWindowButLosBlocked_RepositionsInsteadOfCasting()
    {
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

        task.Update();
        task.Update();
        task.Update();
        task.Update();
        task.Update();

        om.Verify(o => o.MoveToward(It.Is<Position>(position => position.Y > 0f)), Times.AtLeastOnce);
        om.Verify(o => o.CastSpellAtLocation(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("FishingTask los_blocked", StringComparison.Ordinal))), Times.AtLeastOnce);
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
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-988.5f, -3834f, 5.7f));
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
        var expectedCastTarget = FishingData.GetPoolCastTarget(player.Object.Position!, pool.Object.Position!, 4f);
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

        task.Update();
        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        task.Update();

        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(
            (int)FishingData.FishingRank1,
            It.Is<float>(x => MathF.Abs(x - expectedCastTarget.X) < 0.01f),
            It.Is<float>(y => MathF.Abs(y - expectedCastTarget.Y) < 0.01f),
            It.Is<float>(z => MathF.Abs(z - expectedCastTarget.Z) < 0.01f)), Times.Once);

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
    public void Update_BagDeltaWithoutLootWindow_DoesNotPopSuccess()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-988.5f, -3834f, 5.7f));
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
        var expectedCastTarget = FishingData.GetPoolCastTarget(player.Object.Position!, pool.Object.Position!, 4f);
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

        task.Update();
        task.Update();
        task.Update();
        AtomicTaskTestHelpers.RewindFishingTaskState(task, 300);
        task.Update();

        om.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        om.Verify(o => o.CastSpellAtLocation(
            (int)FishingData.FishingRank1,
            It.Is<float>(x => MathF.Abs(x - expectedCastTarget.X) < 0.01f),
            It.Is<float>(y => MathF.Abs(y - expectedCastTarget.Y) < 0.01f),
            It.Is<float>(z => MathF.Abs(z - expectedCastTarget.Z) < 0.01f)), Times.Once);

        channelingId = FishingData.FishingRank1;
        isChanneling = true;
        task.Update();

        channelingId = 0;
        isChanneling = false;
        task.Update();
        bagItems = [caughtFish];
        task.Update();

        ctx.Verify(c => c.AddDiagnosticMessage(It.Is<string>(message =>
            message.Contains("fishing_loot_success", StringComparison.Ordinal))), Times.Never);
        Assert.Single(stack);
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
            MathF.Abs(p.X - firstWaypoint.X) < 0.01f &&
            MathF.Abs(p.Y - firstWaypoint.Y) < 0.01f &&
            MathF.Abs(p.Z - firstWaypoint.Z) < 0.01f)), Times.Once);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - secondWaypoint.X) < 0.01f &&
            MathF.Abs(p.Y - secondWaypoint.Y) < 0.01f &&
            MathF.Abs(p.Z - secondWaypoint.Z) < 0.01f)), Times.Never);
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
