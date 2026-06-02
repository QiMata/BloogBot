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
/// (port 9002) is left untouched — it's the production target after a
/// release, not a test dependency. Without the env var, behavior is
/// unchanged (assumes someone is listening on port 9002).
///
/// The roster source can be overridden per run via
/// `WWOW_LONG_PATHING_SETTINGS_PATH`, which lets the same live proof execute
/// against alternate foreground capsules without editing the default checked-in
/// `LongPathing.config.json`.
/// </summary>
public sealed class LongPathingFixture : LiveBotFixture, IAsyncLifetime
{
    private PathfindingTestFixture? _pathfindingFixture;

    /// <summary>
    /// Pathfinding fixtures leave bots in GM mode so aggressive mobs along long
    /// routes don't interrupt TravelTo execution. Pathfinding tests don't read
    /// UnitReaction, so the GM-mode faction-bit corruption that breaks combat /
    /// NPC / social / quest tests is harmless here. This narrows the per-repo
    /// "No .gm on in WWoW tests" rule (docs/Spec/00_VISION.md §7,
    /// docs/Spec/13_TESTING.md, Tests/CLAUDE.md) to pathfinding-only.
    /// Memory key: feedback-wwow-gm-on-pathfinding-tests (2026-05-19 directive).
    /// </summary>
    protected override bool EnableGmModeAfterCleanSlate => true;

    async Task IAsyncLifetime.InitializeAsync()
    {
        SetCustomSettingsPath(LongPathingSettings.ResolveSettingsPath());

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
}

[CollectionDefinition(Name)]
public sealed class LongPathingValidationCollection : ICollectionFixture<LongPathingFixture>
{
    public const string Name = "LongPathing";
}
