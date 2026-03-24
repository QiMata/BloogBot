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

    private sealed class TestableBgOnlyFixture : LiveBotFixture
    {
        private static readonly MethodInfo SeedExpectedAccountsMethod =
            typeof(LiveBotFixture).GetMethod("SeedExpectedAccountsFromStateManagerSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(LiveBotFixture).FullName, "SeedExpectedAccountsFromStateManagerSettings");

        public void ApplySettingsPath(string path) => SetCustomSettingsPath(path);

        public static string? ResolveSettingsPath(string fileName) => ResolveTestSettingsPath(fileName);

        public void SeedExpectedAccounts() => SeedExpectedAccountsMethod.Invoke(this, null);
    }
}
