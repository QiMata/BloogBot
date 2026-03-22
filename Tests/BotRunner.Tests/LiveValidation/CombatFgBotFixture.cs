using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for FG combat tests. Launches StateManager with CombatFg.settings.json:
/// TESTBOT1 (BG) + COMBATTEST (FG). COMBATTEST runs as the injected WoW.exe client
/// for gold-standard combat validation.
/// </summary>
public class CombatFgBotFixture : LiveBotFixture, IAsyncLifetime
{
    async Task IAsyncLifetime.InitializeAsync()
    {
        var settingsPath = ResolveTestSettingsPath("CombatFg.settings.json");
        if (settingsPath != null)
            SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }
}
