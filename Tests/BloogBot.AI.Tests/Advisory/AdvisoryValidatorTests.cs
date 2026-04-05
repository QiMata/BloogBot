using BloogBot.AI.Advisory;
using BloogBot.AI.Observable;
using BloogBot.AI.States;
using BloogBot.AI.Transitions;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using Moq;

namespace WWoWBot.AI.Tests.Advisory;

public sealed class AdvisoryValidatorTests
{
    private readonly Mock<IForbiddenTransitionRegistry> _registry;
    private readonly InMemoryAdvisoryOverrideLog _log;
    private readonly AdvisoryValidator _validator;

    public AdvisoryValidatorTests()
    {
        _registry = new Mock<IForbiddenTransitionRegistry>(MockBehavior.Strict);
        _log = new InMemoryAdvisoryOverrideLog();
        _validator = new AdvisoryValidator(_registry.Object, _log);
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AdvisoryValidator(null!));
    }

    [Fact]
    public void Validate_AllowedTransition_NoOverrides_ReturnsAccepted()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Resting);
        var om = CreateObjectManager();

        SetupRegistryAllow(BotActivity.Resting, BotActivity.Grinding);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.False(result.WasOverridden);
        Assert.Equal(BotActivity.Grinding, result.FinalActivity);
        Assert.Same(advisory, result.Original);
    }

    [Fact]
    public void Validate_ForbiddenTransition_ReturnsOverridden()
    {
        var advisory = CreateAdvisory(BotActivity.Chatting);
        var state = CreateState(BotActivity.Combat);
        var om = CreateObjectManager();

        _registry
            .Setup(r => r.CheckTransition(BotActivity.Combat, BotActivity.Chatting, It.IsAny<TransitionContext>()))
            .Returns(TransitionCheckResult.Forbidden("Cannot chat in combat", "CombatToChatBlocked"));

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Combat, result.FinalActivity);
        Assert.Equal("CombatToChatBlocked", result.OverrideRule);
    }

    [Fact]
    public void Validate_CombatSafety_OverridesToCombat()
    {
        var advisory = CreateAdvisory(BotActivity.Resting);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(hasAggressors: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Resting);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Combat, result.FinalActivity);
        Assert.Equal("CombatSafetyRule", result.OverrideRule);
    }

    [Fact]
    public void Validate_CombatSafety_AdviserySuggestsCombat_NotOverridden()
    {
        var advisory = CreateAdvisory(BotActivity.Combat);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(hasAggressors: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Combat);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.False(result.WasOverridden);
        Assert.Equal(BotActivity.Combat, result.FinalActivity);
    }

    [Fact]
    public void Validate_LowHealth_OverridesToResting()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(healthPercent: 20);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Grinding);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Resting, result.FinalActivity);
        Assert.Equal("HealthSafetyRule", result.OverrideRule);
    }

    [Fact]
    public void Validate_LowHealth_AlreadySuggestsResting_NotOverridden()
    {
        var advisory = CreateAdvisory(BotActivity.Resting);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(healthPercent: 20);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Resting);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.False(result.WasOverridden);
    }

    [Fact]
    public void Validate_GhostForm_OverridesToResting()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(inGhostForm: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Grinding);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Resting, result.FinalActivity);
        Assert.Equal("GhostSafetyRule", result.OverrideRule);
    }

    [Fact]
    public void Validate_TradeFrameOpen_OverridesToTrading()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(tradeFrameOpen: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Grinding);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Trading, result.FinalActivity);
        Assert.Equal("TradeFramePriorityRule", result.OverrideRule);
    }

    [Fact]
    public void Validate_QuestFrameOpen_OverridesToQuesting()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(questFrameOpen: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Grinding);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Questing, result.FinalActivity);
        Assert.Equal("QuestFramePriorityRule", result.OverrideRule);
    }

    [Fact]
    public void Validate_LootFrameOpen_OverridesToCombat()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(lootFrameOpen: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Grinding);

        var result = _validator.Validate(advisory, state, om.Object);

        Assert.True(result.WasOverridden);
        Assert.Equal(BotActivity.Combat, result.FinalActivity);
        Assert.Equal("LootFramePriorityRule", result.OverrideRule);
    }

    [Fact]
    public void Validate_Override_LogsToOverrideLog()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Grinding);
        var om = CreateObjectManager(inGhostForm: true);

        SetupRegistryAllow(BotActivity.Grinding, BotActivity.Grinding);

        _validator.Validate(advisory, state, om.Object);

        Assert.Equal(1, _log.TotalOverrideCount);
        var recent = _log.GetRecentOverrides(1);
        Assert.Single(recent);
        Assert.Equal("GhostSafetyRule", recent[0].OverrideRule);
    }

    [Fact]
    public void Validate_Accepted_DoesNotLogToOverrideLog()
    {
        var advisory = CreateAdvisory(BotActivity.Grinding);
        var state = CreateState(BotActivity.Resting);
        var om = CreateObjectManager();

        SetupRegistryAllow(BotActivity.Resting, BotActivity.Grinding);

        _validator.Validate(advisory, state, om.Object);

        Assert.Equal(0, _log.TotalOverrideCount);
    }

    private static LlmAdvisoryResult CreateAdvisory(BotActivity activity, double confidence = 0.8) =>
        LlmAdvisoryResult.Create(activity, null, "test reasoning", confidence);

    private static StateChangeEvent CreateState(BotActivity activity) =>
        StateChangeEvent.CreateInitial(activity);

    private void SetupRegistryAllow(BotActivity from, BotActivity to)
    {
        _registry
            .Setup(r => r.CheckTransition(from, to, It.IsAny<TransitionContext>()))
            .Returns(TransitionCheckResult.Allowed());
    }

    private static Mock<IObjectManager> CreateObjectManager(
        bool hasAggressors = false,
        uint healthPercent = 100,
        bool inGhostForm = false,
        bool tradeFrameOpen = false,
        bool questFrameOpen = false,
        bool lootFrameOpen = false)
    {
        var om = new Mock<IObjectManager>(MockBehavior.Loose);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);

        player.SetupGet(p => p.Guid).Returns(1234UL);
        player.SetupGet(p => p.HealthPercent).Returns(healthPercent);
        player.SetupGet(p => p.InGhostForm).Returns(inGhostForm);

        om.SetupGet(o => o.Player).Returns(player.Object);

        if (hasAggressors)
        {
            var aggressor = new Mock<IWoWUnit>(MockBehavior.Loose);
            aggressor.SetupGet(a => a.TargetGuid).Returns(1234UL);
            om.SetupGet(o => o.Aggressors).Returns(new List<IWoWUnit> { aggressor.Object });
        }
        else
        {
            om.SetupGet(o => o.Aggressors).Returns(new List<IWoWUnit>());
        }

        // Trade frame
        var tradeFrame = new Mock<ITradeFrame>(MockBehavior.Loose);
        tradeFrame.SetupGet(f => f.IsOpen).Returns(tradeFrameOpen);
        om.SetupGet(o => o.TradeFrame).Returns(tradeFrameOpen ? tradeFrame.Object : null);

        // Quest frame
        var questFrame = new Mock<IQuestFrame>(MockBehavior.Loose);
        questFrame.SetupGet(f => f.IsOpen).Returns(questFrameOpen);
        om.SetupGet(o => o.QuestFrame).Returns(questFrameOpen ? questFrame.Object : null);

        // Loot frame
        var lootFrame = new Mock<ILootFrame>(MockBehavior.Loose);
        lootFrame.SetupGet(f => f.IsOpen).Returns(lootFrameOpen);
        lootFrame.SetupGet(f => f.LootCount).Returns(lootFrameOpen ? 1 : 0);
        om.SetupGet(o => o.LootFrame).Returns(lootFrameOpen ? lootFrame.Object : null);

        return om;
    }
}
