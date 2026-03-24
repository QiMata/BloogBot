using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for BG-only live suites. Launches StateManager with BgOnly.settings.json
/// so BG-authoritative tests do not pay the startup/runtime cost of an unnecessary FG client.
/// </summary>
public class BgOnlyBotFixture : LiveBotFixture, IAsyncLifetime
{
    async Task IAsyncLifetime.InitializeAsync()
    {
        var settingsPath = ResolveTestSettingsPath("BgOnly.settings.json");
        if (settingsPath != null)
            SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }
}
