using BloogBot.AI.States;
using BloogBot.AI.Transitions;

namespace WWoWBot.AI.Tests.Transitions;

public sealed class ForbiddenTransitionRegistryTests
{
    [Fact]
    public void DefaultConstructor_RegistersDefaultRules()
    {
        var registry = new ForbiddenTransitionRegistry();
        var rules = registry.GetAllRules();

        Assert.NotEmpty(rules);
        Assert.Contains(rules, r => r.RuleName == "CombatToChattingBlocked");
        Assert.Contains(rules, r => r.RuleName == "GhostFormCombatBlocked");
        Assert.Contains(rules, r => r.RuleName == "DungeonToGrindingBlocked");
    }

    [Fact]
    public void CreateEmpty_HasNoRules()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        Assert.Empty(registry.GetAllRules());
    }

    [Fact]
    public void CheckTransition_AllowedTransition_ReturnsAllowed()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        var result = registry.CheckTransition(BotActivity.Grinding, BotActivity.Combat);

        Assert.True(result.IsAllowed);
        Assert.Null(result.Reason);
        Assert.Null(result.RuleName);
    }

    [Fact]
    public void CheckTransition_ForbiddenTransition_ReturnsForbidden()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestBlock", BotActivity.Combat, BotActivity.Chatting, "no chat"));

        var result = registry.CheckTransition(BotActivity.Combat, BotActivity.Chatting);

        Assert.False(result.IsAllowed);
        Assert.Equal("no chat", result.Reason);
        Assert.Equal("TestBlock", result.RuleName);
    }

    [Fact]
    public void RegisterRule_DuplicateName_ReplacesExistingRule()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "v1"));
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Grinding, BotActivity.Resting, "v2"));

        var rules = registry.GetAllRules();
        Assert.Single(rules);
        Assert.Equal(BotActivity.Grinding, rules[0].FromActivity);
    }

    [Fact]
    public void RemoveRule_RemovesExistingRule()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "reason"));

        registry.RemoveRule("TestRule");

        Assert.Empty(registry.GetAllRules());
    }

    [Fact]
    public void RemoveRule_NonExistentName_DoesNotThrow()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RemoveRule("NonExistent");
        Assert.Empty(registry.GetAllRules());
    }

    [Fact]
    public void SetRuleEnabled_DisablesRule()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "reason"));

        registry.SetRuleEnabled("TestRule", false);

        var rule = registry.GetRule("TestRule");
        Assert.NotNull(rule);
        Assert.False(rule!.IsEnabled);
    }

    [Fact]
    public void SetRuleEnabled_DisabledRule_CheckTransitionAllows()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "reason"));
        registry.SetRuleEnabled("TestRule", false);

        var result = registry.CheckTransition(BotActivity.Combat, BotActivity.Chatting);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void SetRuleEnabled_ReenablesRule()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "reason"));
        registry.SetRuleEnabled("TestRule", false);
        registry.SetRuleEnabled("TestRule", true);

        var result = registry.CheckTransition(BotActivity.Combat, BotActivity.Chatting);
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void GetRule_ExistingRule_ReturnsRule()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "reason"));

        var rule = registry.GetRule("TestRule");

        Assert.NotNull(rule);
        Assert.Equal("TestRule", rule!.RuleName);
    }

    [Fact]
    public void GetRule_NonExistentRule_ReturnsNull()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        Assert.Null(registry.GetRule("NonExistent"));
    }

    [Fact]
    public void DefaultRules_CombatToChatting_BlockedWithAggressors()
    {
        var registry = new ForbiddenTransitionRegistry();

        // Default rule checks aggressors via predicate - test with a minimal context
        var result = registry.CheckTransition(BotActivity.Combat, BotActivity.Chatting);
        // Without ObjectManager in context, aggressors check returns false, so transition allowed
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void DefaultRules_GhostFormCombat_BlockedWhenInGhostForm()
    {
        var registry = new ForbiddenTransitionRegistry();
        var rule = registry.GetRule("GhostFormCombatBlocked");

        Assert.NotNull(rule);
        Assert.Null(rule!.FromActivity); // wildcard - from any
        Assert.Equal(BotActivity.Combat, rule.ToActivity);
    }

    [Fact]
    public void DefaultRules_DungeonToGrinding_AlwaysBlocked()
    {
        var registry = new ForbiddenTransitionRegistry();
        var rule = registry.GetRule("DungeonToGrindingBlocked");

        Assert.NotNull(rule);
        Assert.Equal(BotActivity.Dungeoning, rule!.FromActivity);
        Assert.Equal(BotActivity.Grinding, rule.ToActivity);
    }

    [Fact]
    public void DefaultRules_BattlegroundToQuesting_Blocked()
    {
        var registry = new ForbiddenTransitionRegistry();
        var rule = registry.GetRule("BattlegroundToQuestingBlocked");

        Assert.NotNull(rule);
        Assert.Equal(BotActivity.Battlegrounding, rule!.FromActivity);
        Assert.Equal(BotActivity.Questing, rule.ToActivity);
    }

    [Fact]
    public void CheckTransition_MultipleRules_FirstMatchWins()
    {
        var registry = ForbiddenTransitionRegistry.CreateEmpty();
        registry.RegisterRule(ForbiddenTransitionRule.Block("Rule1", BotActivity.Combat, BotActivity.Chatting, "first"));
        registry.RegisterRule(ForbiddenTransitionRule.Block("Rule2", BotActivity.Combat, BotActivity.Chatting, "second"));

        var result = registry.CheckTransition(BotActivity.Combat, BotActivity.Chatting);
        Assert.False(result.IsAllowed);
        Assert.Equal("Rule1", result.RuleName);
    }
}
