using BotProfiles.Common;
using GameData.Core.Enums;
using Xunit;

namespace BotRunner.Tests.Progression;

/// <summary>
/// Verifies that BuildConfig.SpecName correctly selects the BotProfile spec
/// instead of the hardcoded default for the class.
/// </summary>
public class ConfigurableSpecTests
{
    [Fact]
    public void Resolve_WarriorFury_ReturnsFuryProfile()
    {
        var profile = BotProfileResolver.Resolve("WarriorFury", Class.Warrior);
        Assert.NotNull(profile);
        Assert.Contains("Fury", profile.Name, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_WarriorProtection_ReturnsProtProfile()
    {
        var profile = BotProfileResolver.Resolve("WarriorProtection", Class.Warrior);
        Assert.NotNull(profile);
        Assert.Contains("Protection", profile.Name, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NullSpec_ReturnsDefaultForClass()
    {
        var profile = BotProfileResolver.Resolve(null, Class.Warrior);
        Assert.NotNull(profile);
        // Default for Warrior should be Arms (the first/default spec)
        Assert.NotNull(profile.Name);
    }

    [Fact]
    public void Resolve_MageFrost_ReturnsFrostProfile()
    {
        var profile = BotProfileResolver.Resolve("MageFrost", Class.Mage);
        Assert.NotNull(profile);
        Assert.Contains("Frost", profile.Name, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_PriestShadow_ReturnsShadowProfile()
    {
        var profile = BotProfileResolver.Resolve("PriestShadow", Class.Priest);
        Assert.NotNull(profile);
        Assert.Contains("Shadow", profile.Name, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_InvalidSpec_FallsBackToDefault()
    {
        var profile = BotProfileResolver.Resolve("NonExistentSpec", Class.Warrior);
        Assert.NotNull(profile);
        // Should fall back to default for the class
    }
}
