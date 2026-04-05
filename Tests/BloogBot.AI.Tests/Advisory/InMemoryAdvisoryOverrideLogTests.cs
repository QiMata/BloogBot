using BloogBot.AI.Advisory;
using BloogBot.AI.States;

namespace WWoWBot.AI.Tests.Advisory;

public sealed class InMemoryAdvisoryOverrideLogTests
{
    [Fact]
    public void TotalOverrideCount_InitiallyZero()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        Assert.Equal(0, log.TotalOverrideCount);
    }

    [Fact]
    public void LogOverride_IncrementsCount()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        log.LogOverride(CreateOverride("Rule1"));

        Assert.Equal(1, log.TotalOverrideCount);
    }

    [Fact]
    public void LogOverride_NonOverriddenResolution_Ignored()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        var accepted = AdvisoryResolution.Accepted(
            LlmAdvisoryResult.Create(BotActivity.Grinding, null, "test"));

        log.LogOverride(accepted);

        Assert.Equal(0, log.TotalOverrideCount);
    }

    [Fact]
    public void GetRecentOverrides_ReturnsInReverseOrder()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        log.LogOverride(CreateOverride("Rule1"));
        log.LogOverride(CreateOverride("Rule2"));
        log.LogOverride(CreateOverride("Rule3"));

        var recent = log.GetRecentOverrides(3);

        Assert.Equal(3, recent.Count);
        Assert.Equal("Rule3", recent[0].OverrideRule);
        Assert.Equal("Rule2", recent[1].OverrideRule);
        Assert.Equal("Rule1", recent[2].OverrideRule);
    }

    [Fact]
    public void GetRecentOverrides_LimitedCount()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        log.LogOverride(CreateOverride("Rule1"));
        log.LogOverride(CreateOverride("Rule2"));
        log.LogOverride(CreateOverride("Rule3"));

        var recent = log.GetRecentOverrides(2);
        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public void GetOverridesByRule_FiltersCorrectly()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        log.LogOverride(CreateOverride("Rule1"));
        log.LogOverride(CreateOverride("Rule2"));
        log.LogOverride(CreateOverride("Rule1"));

        var rule1 = log.GetOverridesByRule("Rule1");
        Assert.Equal(2, rule1.Count);

        var rule2 = log.GetOverridesByRule("Rule2");
        Assert.Single(rule2);
    }

    [Fact]
    public void GetOverrideCountsByRule_ReturnsAccurateCounts()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        log.LogOverride(CreateOverride("Rule1"));
        log.LogOverride(CreateOverride("Rule2"));
        log.LogOverride(CreateOverride("Rule1"));
        log.LogOverride(CreateOverride("Rule1"));

        var counts = log.GetOverrideCountsByRule();

        Assert.Equal(3, counts["Rule1"]);
        Assert.Equal(1, counts["Rule2"]);
    }

    [Fact]
    public void MaxEntries_TrimOldEntries()
    {
        var log = new InMemoryAdvisoryOverrideLog(maxEntries: 3);

        log.LogOverride(CreateOverride("Rule1"));
        log.LogOverride(CreateOverride("Rule2"));
        log.LogOverride(CreateOverride("Rule3"));
        log.LogOverride(CreateOverride("Rule4"));

        // Queue trimmed to 3 entries, but total count still reflects all logged
        Assert.Equal(4, log.TotalOverrideCount);

        var recent = log.GetRecentOverrides(10);
        Assert.Equal(3, recent.Count);
        // Oldest entry (Rule1) should have been trimmed
        Assert.DoesNotContain(recent, r => r.OverrideRule == "Rule1");
    }

    [Fact]
    public void GetOverridesByRule_NonExistentRule_ReturnsEmpty()
    {
        var log = new InMemoryAdvisoryOverrideLog();
        var result = log.GetOverridesByRule("NonExistent");
        Assert.Empty(result);
    }

    private static AdvisoryResolution CreateOverride(string ruleName)
    {
        var advisory = LlmAdvisoryResult.Create(BotActivity.Grinding, null, "test");
        return AdvisoryResolution.Overridden(advisory, BotActivity.Resting, null, "reason", ruleName);
    }
}
