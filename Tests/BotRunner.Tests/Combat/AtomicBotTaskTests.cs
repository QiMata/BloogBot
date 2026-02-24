using BotRunner.Combat;
using BotRunner.Clients;
using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
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
    private static void ForceRunbackSampleReady(RetrieveCorpseTask task)
    {
        var sampleField = typeof(RetrieveCorpseTask).GetField(
            "_lastRunbackSampleUtc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _lastRunbackSampleUtc field.");
        sampleField.SetValue(task, DateTime.UtcNow - TimeSpan.FromSeconds(2));
    }

    private static void ForceExpireUnstickManeuver(RetrieveCorpseTask task)
    {
        var untilField = typeof(RetrieveCorpseTask).GetField(
            "_unstickManeuverUntilUtc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing _unstickManeuverUntilUtc field.");
        untilField.SetValue(task, DateTime.UtcNow - TimeSpan.FromSeconds(1));
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

        om.Verify(o => o.StopAllMovement(), Times.Once);
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

        om.Verify(o => o.StopAllMovement(), Times.Once);
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
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - corpsePos.X) < 0.01f &&
            MathF.Abs(p.Y - corpsePos.Y) < 0.01f &&
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.Once);
    }

    [Fact]
    public void Update_FarFromCorpse_NoPath_DrivesFallbackTarget()
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
        om.Verify(o => o.StopAllMovement(), Times.Never);
        om.Verify(o => o.MoveToward(It.Is<Position>(p =>
            MathF.Abs(p.X - corpsePos.X) < 0.01f &&
            MathF.Abs(p.Y - corpsePos.Y) < 0.01f &&
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.Once);
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
            MathF.Abs(p.Z - corpsePos.Z) < 0.01f)), Times.AtLeastOnce);
        om.Verify(o => o.StartMovement(It.Is<ControlBits>(b =>
            b == ControlBits.StrafeLeft || b == ControlBits.StrafeRight || b == ControlBits.Back)), Times.AtLeastOnce);
    }

    [Fact]
    public void Update_AfterStallRecovery_ExecutesAndStopsUnstickManeuver()
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

        task.Update();
        ForceExpireUnstickManeuver(task);
        task.Update();

        Assert.Single(stack);
        om.Verify(o => o.StartMovement(ControlBits.StrafeLeft), Times.AtLeastOnce);
        om.Verify(o => o.StopMovement(ControlBits.StrafeLeft), Times.AtLeastOnce);
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
        om.Verify(o => o.StopAllMovement(), Times.Once);
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
