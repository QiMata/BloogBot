using System.Reflection;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

public class BgOnlyBotFixtureConfigurationTests
{
    [Fact]
    public void BgOnlySettings_SeedsOnlyBackgroundAccount()
    {
        var settingsPath = TestableBgOnlyFixture.ResolveSettingsPath("BgOnly.settings.json");
        Assert.False(string.IsNullOrWhiteSpace(settingsPath));

        var fixture = new TestableBgOnlyFixture();
        fixture.ApplySettingsPath(settingsPath!);
        fixture.SeedExpectedAccounts();

        Assert.Equal("TESTBOT2", fixture.BgAccountName);
        Assert.Null(fixture.FgAccountName);
        Assert.Null(fixture.CombatTestAccountName);
    }

    [Fact]
    public void LongPathingSettings_SeedsConfiguredForegroundAndBackgroundAccounts()
    {
        var settingsPath = ResolveRepoPath(
            "Services",
            "WoWStateManager",
            "Settings",
            "Configs",
            "LongPathing.config.json");

        var fixture = new TestableBgOnlyFixture();
        fixture.ApplySettingsPath(settingsPath);
        fixture.SeedExpectedAccounts();

        Assert.Equal("LPATHFG1", fixture.FgAccountName);
        Assert.Equal("LPATHBG1", fixture.BgAccountName);
        Assert.Equal("SHODAN", fixture.ShodanAccountName);
        Assert.Equal("Shodan", fixture.ShodanExpectedCharacterName);
        Assert.Null(fixture.CombatTestAccountName);
    }

    [Fact]
    public void LongPathingSettings_DoesNotLetShodanSnapshotReplaceConfiguredBackgroundRole()
    {
        var settingsPath = ResolveRepoPath(
            "Services",
            "WoWStateManager",
            "Settings",
            "Configs",
            "LongPathing.config.json");

        var fixture = new TestableBgOnlyFixture();
        fixture.ApplySettingsPath(settingsPath);
        fixture.SeedExpectedAccounts();

        fixture.IdentifyBots(
            new Communication.WoWActivitySnapshot { AccountName = "SHODAN", CharacterName = "Shodan" });

        Assert.Equal("LPATHFG1", fixture.FgAccountName);
        Assert.Equal("LPATHBG1", fixture.BgAccountName);
        Assert.Null(fixture.BackgroundBot);
        Assert.Equal("SHODAN", fixture.ShodanAccountName);

        fixture.IdentifyBots(
            new Communication.WoWActivitySnapshot { AccountName = "SHODAN", CharacterName = "Shodan" },
            new Communication.WoWActivitySnapshot { AccountName = "LPATHBG1", CharacterName = "Kargganshwte" },
            new Communication.WoWActivitySnapshot { AccountName = "LPATHFG1", CharacterName = "Horuntusktmc" });

        Assert.Equal("LPATHFG1", fixture.FgAccountName);
        Assert.Equal("Horuntusktmc", fixture.FgCharacterName);
        Assert.Equal("LPATHBG1", fixture.BgAccountName);
        Assert.Equal("Kargganshwte", fixture.BgCharacterName);
        Assert.Equal("SHODAN", fixture.ShodanAccountName);
        Assert.Equal("Shodan", fixture.ShodanCharacterName);
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var parts = new string[segments.Length + 1];
            parts[0] = dir.FullName;
            Array.Copy(segments, 0, parts, 1, segments.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not resolve repo path '{string.Join("/", segments)}'.");
    }

    private sealed class TestableBgOnlyFixture : LiveBotFixture
    {
        private static readonly MethodInfo SeedExpectedAccountsMethod =
            typeof(LiveBotFixture).GetMethod("SeedExpectedAccountsFromStateManagerSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(LiveBotFixture).FullName, "SeedExpectedAccountsFromStateManagerSettings");

        private static readonly MethodInfo IdentifyBotsMethod =
            typeof(LiveBotFixture).GetMethod("IdentifyBots", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(LiveBotFixture).FullName, "IdentifyBots");

        public void ApplySettingsPath(string path) => SetCustomSettingsPath(path);

        public static string? ResolveSettingsPath(string fileName) => ResolveTestSettingsPath(fileName);

        public void SeedExpectedAccounts() => SeedExpectedAccountsMethod.Invoke(this, null);

        public void IdentifyBots(params Communication.WoWActivitySnapshot[] snapshots)
            => IdentifyBotsMethod.Invoke(this, new object[] { snapshots.ToList() });
    }
}
