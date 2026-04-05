using BloogBot.AI.Configuration;

namespace WWoWBot.AI.Tests.Configuration;

public sealed class DecisionInvocationSettingsTests
{
    [Fact]
    public void Defaults_DefaultInterval_Is5Seconds()
    {
        var settings = new DecisionInvocationSettings();
        Assert.Equal(TimeSpan.FromSeconds(5), settings.DefaultInterval);
    }

    [Fact]
    public void Defaults_MinimumInterval_Is500Ms()
    {
        var settings = new DecisionInvocationSettings();
        Assert.Equal(TimeSpan.FromMilliseconds(500), settings.MinimumInterval);
    }

    [Fact]
    public void Defaults_MaximumInterval_Is5Minutes()
    {
        var settings = new DecisionInvocationSettings();
        Assert.Equal(TimeSpan.FromMinutes(5), settings.MaximumInterval);
    }

    [Fact]
    public void Defaults_ResetTimerOnAdHocInvocation_IsTrue()
    {
        var settings = new DecisionInvocationSettings();
        Assert.True(settings.ResetTimerOnAdHocInvocation);
    }

    [Fact]
    public void Defaults_EnableAutomaticInvocation_IsTrue()
    {
        var settings = new DecisionInvocationSettings();
        Assert.True(settings.EnableAutomaticInvocation);
    }

    [Fact]
    public void Validate_IntervalBelowMinimum_ClampedToMinimum()
    {
        var settings = new DecisionInvocationSettings
        {
            DefaultInterval = TimeSpan.FromMilliseconds(100)
        };

        settings.Validate();

        Assert.Equal(TimeSpan.FromMilliseconds(500), settings.DefaultInterval);
    }

    [Fact]
    public void Validate_IntervalAboveMaximum_ClampedToMaximum()
    {
        var settings = new DecisionInvocationSettings
        {
            DefaultInterval = TimeSpan.FromMinutes(10)
        };

        settings.Validate();

        Assert.Equal(TimeSpan.FromMinutes(5), settings.DefaultInterval);
    }

    [Fact]
    public void Validate_IntervalWithinRange_Unchanged()
    {
        var settings = new DecisionInvocationSettings
        {
            DefaultInterval = TimeSpan.FromSeconds(3)
        };

        settings.Validate();

        Assert.Equal(TimeSpan.FromSeconds(3), settings.DefaultInterval);
    }

    [Fact]
    public void CreateDefault_ReturnsDefaultSettings()
    {
        var settings = DecisionInvocationSettings.CreateDefault();

        Assert.Equal(TimeSpan.FromSeconds(5), settings.DefaultInterval);
        Assert.True(settings.EnableAutomaticInvocation);
    }

    [Fact]
    public void SectionName_IsDecisionInvocation()
    {
        Assert.Equal("DecisionInvocation", DecisionInvocationSettings.SectionName);
    }
}
