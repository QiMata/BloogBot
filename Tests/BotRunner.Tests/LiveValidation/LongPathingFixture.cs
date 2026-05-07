using System;
using System.IO;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Long-pathing live fixture that starts StateManager with the long-pathing
/// roster before the shared live fixture initialization launches any clients.
///
/// PFS-OVERHAUL-006 (2026-05-07): when WWOW_USE_LOCAL_PATHFINDING_SERVICE=1,
/// this fixture spawns a fresh PathfindingService.exe on a test-allocated
/// port (default 5101 via WWOW_TEST_PATHFINDING_PORT) BEFORE StateManager
/// starts, and points the StateManager + BotRunner at it via
/// WWOW_TEST_PATHFINDING_PORT. The Docker `wwow-pathfinding` container
/// (port 5001) is left untouched — it's the production target after a
/// release, not a test dependency. Without the env var, behavior is
/// unchanged (assumes someone is listening on port 5001).
/// </summary>
public sealed class LongPathingFixture : LiveBotFixture, IAsyncLifetime
{
    private PathfindingTestFixture? _pathfindingFixture;

    async Task IAsyncLifetime.InitializeAsync()
    {
        SetCustomSettingsPath(ResolveLongPathingSettingsPath());

        if (PathfindingTestFixture.IsLocalPathfindingEnabled())
        {
            _pathfindingFixture = await PathfindingTestFixture.LaunchAsync(msg => Console.WriteLine(msg))
                .ConfigureAwait(false);
            // Make BotServiceFixture's pathfinding-port lookup find our spawned port,
            // so the StateManager process inherits the right address.
            Environment.SetEnvironmentVariable("WWOW_TEST_PATHFINDING_PORT", _pathfindingFixture.Port.ToString());
        }

        await base.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            if (_pathfindingFixture != null)
            {
                await _pathfindingFixture.DisposeAsync().ConfigureAwait(false);
                _pathfindingFixture = null;
            }
        }
    }

    private static string ResolveLongPathingSettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "Services",
                "WoWStateManager",
                "Settings",
                "Configs",
                "LongPathing.config.json");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find LongPathing.config.json.");
    }
}

[CollectionDefinition(Name)]
public sealed class LongPathingValidationCollection : ICollectionFixture<LongPathingFixture>
{
    public const string Name = "LongPathing";
}
