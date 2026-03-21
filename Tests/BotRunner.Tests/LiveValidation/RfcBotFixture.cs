using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for Ragefire Chasm tests. Launches StateManager directly with the
/// 10-bot RFC config and coordinator enabled. No default config, no restarts.
/// </summary>
public class RfcBotFixture : LiveBotFixture, IAsyncLifetime
{
    async Task IAsyncLifetime.InitializeAsync()
    {
        // Enable coordinator — it owns group lifecycle, so skip fixture group cleanup
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        // Hooks verified safe — crash is not caused by packet/signal/WndProc hooks.
        // (WWOW_DISABLE_PACKET_HOOKS, WWOW_DISABLE_WNDPROC_HOOK both tested)
        SkipGroupCleanup = true;

        // Set RFC settings before launching StateManager
        var settingsPath = ResolveRfcSettingsPath();
        if (settingsPath != null)
            SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }

    private static string? ResolveRfcSettingsPath()
    {
        // Look for the RFC settings file relative to the build output
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "LiveValidation", "Settings", "RagefireChasm.settings.json");
            if (File.Exists(candidate))
                return candidate;

            // Also check Bot/Release path where StateManager copies settings
            candidate = Path.Combine(dir.FullName, "Bot", "Release", "net8.0", "LiveValidation", "Settings", "RagefireChasm.settings.json");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
