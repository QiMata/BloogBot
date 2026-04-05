using System.Reflection;
using BloogBot.AI.StateMachine;
using BloogBot.AI.States;
using BloogBot.AI.Transitions;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace WWoWBot.AI.Tests.StateMachine;

public sealed class BotActivityStateMachineTests
{
    [Fact]
    public void Fire_BattlegroundTrigger_WhenPlayerIsInBattleground_TransitionsToBattlegrounding()
    {
        var harness = CreateHarness(initialActivity: BotActivity.Grinding);
        Assert.Equal(BotActivity.Grinding, harness.StateMachine.Current);

        harness.InBattleground = true;

        FireTrigger(harness.StateMachine, Trigger.BattlegroundStarted);

        Assert.Equal(BotActivity.Battlegrounding, harness.StateMachine.Current);
    }

    [Fact]
    public void Fire_CombatTrigger_WhenForbiddenTransitionBlocks_StaysInCurrentActivity()
    {
        var registry = new Mock<IForbiddenTransitionRegistry>(MockBehavior.Strict);
        registry
            .Setup(r => r.CheckTransition(BotActivity.Grinding, BotActivity.Combat, It.IsAny<TransitionContext>()))
            .Returns(TransitionCheckResult.Forbidden("Combat transition blocked for test", "TestRule"));

        var harness = CreateHarness(initialActivity: BotActivity.Grinding, forbiddenTransitions: registry.Object);
        Assert.Equal(BotActivity.Grinding, harness.StateMachine.Current);

        var aggressor = new Mock<IWoWUnit>(MockBehavior.Strict);
        aggressor.SetupGet(a => a.TargetGuid).Returns(harness.PlayerGuid);
        harness.Aggressors.Add(aggressor.Object);

        FireTrigger(harness.StateMachine, Trigger.CombatStarted);

        Assert.Equal(BotActivity.Grinding, harness.StateMachine.Current);
        registry.Verify(
            r => r.CheckTransition(BotActivity.Grinding, BotActivity.Combat, It.IsAny<TransitionContext>()),
            Times.Once);
    }

    private static TestHarness CreateHarness(
        BotActivity initialActivity,
        IForbiddenTransitionRegistry? forbiddenTransitions = null)
    {
        const ulong playerGuid = 9001;
        var harness = new TestHarness { PlayerGuid = playerGuid };

        var loggerFactory = new Mock<ILoggerFactory>(MockBehavior.Strict);
        loggerFactory
            .Setup(l => l.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Strict);
        player.SetupGet(p => p.Guid).Returns(playerGuid);
        player.SetupGet(p => p.InGhostForm).Returns(() => harness.InGhostForm);
        player.SetupGet(p => p.HealthPercent).Returns(() => harness.HealthPercent);
        player.SetupGet(p => p.InBattleground).Returns(() => harness.InBattleground);
        player.SetupGet(p => p.HasQuestTargets).Returns(() => harness.HasQuestTargets);
        harness.Player = player;
        harness.PartyMembers.Add(player.Object);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.SetupGet(o => o.HasEnteredWorld).Returns(() => harness.HasEnteredWorld);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Aggressors).Returns(() => harness.Aggressors);
        objectManager.SetupGet(o => o.PartyMembers).Returns(() => harness.PartyMembers);
        objectManager.SetupGet(o => o.TradeFrame).Returns((ITradeFrame)null!);
        objectManager.SetupGet(o => o.MerchantFrame).Returns((IMerchantFrame)null!);
        objectManager.SetupGet(o => o.TalentFrame).Returns((ITalentFrame)null!);
        objectManager.SetupGet(o => o.TrainerFrame).Returns((ITrainerFrame)null!);
        objectManager.SetupGet(o => o.CraftFrame).Returns((ICraftFrame)null!);
        objectManager.SetupGet(o => o.TaxiFrame).Returns((ITaxiFrame)null!);
        objectManager.SetupGet(o => o.QuestFrame).Returns((IQuestFrame)null!);
        objectManager.SetupGet(o => o.QuestGreetingFrame).Returns((IQuestGreetingFrame)null!);
        objectManager.SetupGet(o => o.GossipFrame).Returns((IGossipFrame)null!);
        objectManager.SetupGet(o => o.LootFrame).Returns((ILootFrame)null!);
        harness.ObjectManager = objectManager;

        harness.StateMachine = new BotActivityStateMachine(
            loggerFactory.Object,
            objectManager.Object,
            initialActivity,
            forbiddenTransitions);

        return harness;
    }

    private static void FireTrigger(BotActivityStateMachine stateMachine, Trigger trigger)
    {
        var fireMethod = typeof(BotActivityStateMachine).GetMethod("Fire", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(fireMethod);

        fireMethod!.Invoke(stateMachine, [trigger]);
    }

    private sealed class TestHarness
    {
        public bool HasEnteredWorld { get; set; } = true;
        public bool InGhostForm { get; set; }
        public uint HealthPercent { get; set; } = 100;
        public bool InBattleground { get; set; }
        public bool HasQuestTargets { get; set; }
        public ulong PlayerGuid { get; set; }
        public List<IWoWUnit> Aggressors { get; } = [];
        public List<IWoWPlayer> PartyMembers { get; } = [];
        public Mock<IObjectManager> ObjectManager { get; set; } = null!;
        public Mock<IWoWLocalPlayer> Player { get; set; } = null!;
        public BotActivityStateMachine StateMachine { get; set; } = null!;
    }
}
