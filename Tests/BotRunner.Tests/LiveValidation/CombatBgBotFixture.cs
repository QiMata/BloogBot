using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for BG combat tests. Launches StateManager with CombatBg.settings.json:
/// TESTBOT1 (FG) + COMBATTEST (BG). COMBATTEST relies on account-level GM access
/// only, so faction data stays normal and mobs engage naturally.
/// </summary>
public class CombatBgBotFixture : LiveBotFixture, IAsyncLifetime
{
    async Task IAsyncLifetime.InitializeAsync()
    {
        var settingsPath = ResolveTestSettingsPath("CombatBg.settings.json");
        if (settingsPath != null)
            SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }
}
