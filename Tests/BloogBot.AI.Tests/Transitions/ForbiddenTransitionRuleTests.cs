using BloogBot.AI.States;
using BloogBot.AI.Transitions;

namespace WWoWBot.AI.Tests.Transitions;

public sealed class ForbiddenTransitionRuleTests
{
    [Fact]
    public void Constructor_EmptyRuleName_ThrowsArgumentException()
    {
        var act = () => new ForbiddenTransitionRule("", BotActivity.Combat, BotActivity.Resting, null, "reason");
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_WhitespaceRuleName_ThrowsArgumentException()
    {
        var act = () => new ForbiddenTransitionRule("   ", BotActivity.Combat, BotActivity.Resting, null, "reason");
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_PreservesProperties()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Resting, null, "test reason");

        Assert.Equal("TestRule", rule.RuleName);
        Assert.Equal(BotActivity.Combat, rule.FromActivity);
        Assert.Equal(BotActivity.Resting, rule.ToActivity);
        Assert.Null(rule.Predicate);
        Assert.Equal("test reason", rule.HumanReadableReason);
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void Constructor_DisabledByDefault_WhenSpecified()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Resting, null, "reason", isEnabled: false);
        Assert.False(rule.IsEnabled);
    }

    [Fact]
    public void Matches_ExactFromAndTo_ReturnsTrue()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, null, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.True(rule.Matches(BotActivity.Combat, BotActivity.Chatting, ctx));
    }

    [Fact]
    public void Matches_WrongFrom_ReturnsFalse()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, null, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.False(rule.Matches(BotActivity.Grinding, BotActivity.Chatting, ctx));
    }

    [Fact]
    public void Matches_WrongTo_ReturnsFalse()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, null, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.False(rule.Matches(BotActivity.Combat, BotActivity.Resting, ctx));
    }

    [Fact]
    public void Matches_WildcardFrom_MatchesAnyFromActivity()
    {
        var rule = new ForbiddenTransitionRule("TestRule", ForbiddenTransitionRule.Any, BotActivity.Combat, null, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.True(rule.Matches(BotActivity.Grinding, BotActivity.Combat, ctx));
        Assert.True(rule.Matches(BotActivity.Resting, BotActivity.Combat, ctx));
        Assert.True(rule.Matches(BotActivity.Questing, BotActivity.Combat, ctx));
    }

    [Fact]
    public void Matches_WildcardTo_MatchesAnyToActivity()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, ForbiddenTransitionRule.Any, null, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.True(rule.Matches(BotActivity.Combat, BotActivity.Grinding, ctx));
        Assert.True(rule.Matches(BotActivity.Combat, BotActivity.Resting, ctx));
    }

    [Fact]
    public void Matches_Disabled_ReturnsFalse()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, null, "reason", isEnabled: false);
        var ctx = new TransitionContext(null!, null!, null);

        Assert.False(rule.Matches(BotActivity.Combat, BotActivity.Chatting, ctx));
    }

    [Fact]
    public void Matches_PredicateReturnsFalse_ReturnsFalse()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, _ => false, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.False(rule.Matches(BotActivity.Combat, BotActivity.Chatting, ctx));
    }

    [Fact]
    public void Matches_PredicateReturnsTrue_ReturnsTrue()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, _ => true, "reason");
        var ctx = new TransitionContext(null!, null!, null);

        Assert.True(rule.Matches(BotActivity.Combat, BotActivity.Chatting, ctx));
    }

    [Fact]
    public void Block_CreatesUnconditionalRule()
    {
        var rule = ForbiddenTransitionRule.Block("test", BotActivity.Combat, BotActivity.Chatting, "no chat in combat");

        Assert.Equal("test", rule.RuleName);
        Assert.Equal(BotActivity.Combat, rule.FromActivity);
        Assert.Equal(BotActivity.Chatting, rule.ToActivity);
        Assert.Null(rule.Predicate);
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void BlockWhen_CreatesConditionalRule()
    {
        Func<TransitionContext, bool> predicate = _ => true;
        var rule = ForbiddenTransitionRule.BlockWhen("test", BotActivity.Combat, BotActivity.Chatting, predicate, "conditional");

        Assert.Equal("test", rule.RuleName);
        Assert.NotNull(rule.Predicate);
    }

    [Fact]
    public void BlockAllTo_CreatesWildcardFromRule()
    {
        var rule = ForbiddenTransitionRule.BlockAllTo("test", BotActivity.Combat, "no combat");

        Assert.Null(rule.FromActivity);
        Assert.Equal(BotActivity.Combat, rule.ToActivity);
    }

    [Fact]
    public void BlockAllFrom_CreatesWildcardToRule()
    {
        var rule = ForbiddenTransitionRule.BlockAllFrom("test", BotActivity.Combat, "no leaving combat");

        Assert.Equal(BotActivity.Combat, rule.FromActivity);
        Assert.Null(rule.ToActivity);
    }

    [Fact]
    public void ToString_IncludesRuleNameAndActivities()
    {
        var rule = ForbiddenTransitionRule.Block("TestRule", BotActivity.Combat, BotActivity.Chatting, "reason");
        var str = rule.ToString();

        Assert.Contains("TestRule", str);
        Assert.Contains("Combat", str);
        Assert.Contains("Chatting", str);
        Assert.Contains("enabled", str);
    }

    [Fact]
    public void ToString_DisabledRule_ShowsDisabled()
    {
        var rule = new ForbiddenTransitionRule("TestRule", BotActivity.Combat, BotActivity.Chatting, null, "reason", isEnabled: false);
        Assert.Contains("disabled", rule.ToString());
    }

    [Fact]
    public void Any_IsNull()
    {
        Assert.Null(ForbiddenTransitionRule.Any);
    }
}
