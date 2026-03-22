using BloogBot.AI.Advisory;
using BloogBot.AI.States;

namespace WWoWBot.AI.Tests.Advisory;

public sealed class AdvisoryResolutionTests
{
    [Fact]
    public void Accepted_WasOverridden_IsFalse()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Accepted(advisory);

        Assert.False(result.WasOverridden);
    }

    [Fact]
    public void Accepted_FinalActivity_MatchesAdvisory()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Accepted(advisory);

        Assert.Equal(BotActivity.Grinding, result.FinalActivity);
    }

    [Fact]
    public void Accepted_OverrideReason_IsNull()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Accepted(advisory);

        Assert.Null(result.OverrideReason);
        Assert.Null(result.OverrideRule);
    }

    [Fact]
    public void Accepted_PreservesOriginalAdvisory()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Accepted(advisory);

        Assert.Same(advisory, result.Original);
    }

    [Fact]
    public void Overridden_WasOverridden_IsTrue()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Overridden(advisory, BotActivity.Combat, null, "combat required", "CombatRule");

        Assert.True(result.WasOverridden);
    }

    [Fact]
    public void Overridden_FinalActivity_DiffersFromAdvisory()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Overridden(advisory, BotActivity.Combat, null, "combat required", "CombatRule");

        Assert.Equal(BotActivity.Combat, result.FinalActivity);
        Assert.NotEqual(advisory.SuggestedActivity, result.FinalActivity);
    }

    [Fact]
    public void Overridden_PreservesReasonAndRule()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var result = AdvisoryResolution.Overridden(advisory, BotActivity.Combat, null, "combat required", "CombatRule");

        Assert.Equal("combat required", result.OverrideReason);
        Assert.Equal("CombatRule", result.OverrideRule);
    }

    [Fact]
    public void Overridden_PreservesMinorState()
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "reason");
        var minorState = MinorState.None(BotActivity.Combat);
        var result = AdvisoryResolution.Overridden(advisory, BotActivity.Combat, minorState, "reason", "Rule");

        Assert.Equal(minorState, result.FinalMinorState);
    }
}
