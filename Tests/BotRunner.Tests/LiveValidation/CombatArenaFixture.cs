using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

[CollectionDefinition(CombatArenaCollection.Name)]
public sealed class CombatArenaCollection : ICollectionFixture<CombatArenaFixture>
{
    public const string Name = "CombatArenaCollection";
}

/// <summary>
/// Dedicated fixture for the combat loop test. Two dedicated accounts (one FG,
/// one BG, both Orc Warrior Male) are driven by
/// <c>Services/WoWStateManager/Settings/Configs/CombatArena.config.json</c>.
/// Each run erases any pre-existing character on those accounts so the bots
/// log in fresh (cinematic auto-dismiss is handled by WorldClient on BG and
/// ForegroundBotWorker on FG). After world entry the fixture teleports both
/// bots to the Valley of Trials boar cluster so the test can run real combat
/// against natural mob spawns — no GM .damage, no .gm on/off toggling.
/// </summary>
public sealed class CombatArenaFixture : CoordinatorFixtureBase
{
    // Valley of Trials boar cluster (Durotar, map 1). Keeps us away from camp
    // allies/trainers and within aggro range of mottled boars.
    public const int MobAreaMapId = 1;
    public const float MobAreaX = -620f;
    public const float MobAreaY = -4385f;
    public const float MobAreaZ = 44f;

    public string FgAccount => "ARENAFG1";
    public string BgAccount => "ARENABG1";

    protected override string SettingsFileName => "CombatArena.settings.json";

    protected override string FixtureLabel => "ARENA";

    // No battleground / dungeon coordinator interference — this test drives
    // actions directly through SendActionAsync.
    protected override bool DisableCoordinatorDuringPreparation => true;

    // Prep runs after both bots are in-world: teleport to the boar cluster,
    // revive if needed, leave no GM .gm toggle footprint.
    protected override bool PrepareDuringInitialization => true;

    // Fresh-login path still includes the intro cinematic skip round-trip; keep
    // the enter-world budget generous so cold StateManager spin-up doesn't
    // time out before both bots hydrate.
    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(4);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromSeconds(90);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
        => LoadCharacterSettingsFromConfig("CombatArena.config.json");

    protected override async Task PrepareBotsAsync()
    {
        // Revive via SOAP if either bot is dead from a previous run. New
        // characters are fully alive, but we can re-enter this fixture when a
        // prior test left a character in corpse/ghost state.
        foreach (var account in AccountNames)
        {
            var snap = await GetSnapshotAsync(account);
            var character = snap?.CharacterName;
            if (string.IsNullOrWhiteSpace(character))
                continue;

            var player = snap?.Player;
            var unit = player?.Unit;
            if (player == null || unit == null)
                continue;

            const uint playerFlagGhost = 0x10;
            const uint standStateMask = 0xFF;
            const uint standStateDead = 7;
            var standState = unit.Bytes1 & standStateMask;
            var hasGhostFlag = (player.PlayerFlags & playerFlagGhost) != 0;
            var needsRevive = unit.Health == 0 || hasGhostFlag || standState == standStateDead;
            if (!needsRevive)
                continue;

            Console.WriteLine($"[{FixtureLabel}:Prep] reviving {character} before arena stage");
            await ExecuteGMCommandAsync($".revive {character}");
            await Task.Delay(400);
        }

        await RefreshSnapshotsAsync();

        // Stage both bots at the Valley of Trials boar cluster via bot-chat
        // `.go xyz` — the self-teleport form that the headless/injected bots
        // can both service. Retry once if either bot doesn't land near the
        // target within the enter-stage budget.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            foreach (var account in AccountNames)
            {
                await BotTeleportAsync(account, MobAreaMapId, MobAreaX, MobAreaY, MobAreaZ);
                await Task.Delay(250);
            }

            var staged = await WaitForAllAccountsNearArenaAsync(TimeSpan.FromSeconds(15));
            if (staged)
                return;

            Console.WriteLine($"[{FixtureLabel}:Prep] not all bots near arena after attempt {attempt}/2; retrying.");
        }

        throw new Xunit.Sdk.XunitException(
            $"[{FixtureLabel}:Prep] failed to stage both bots at the boar cluster after 2 teleport attempts.");
    }

    private async Task<bool> WaitForAllAccountsNearArenaAsync(TimeSpan timeout)
    {
        const float stageRadius = 60f;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await RefreshSnapshotsAsync();

            var allStaged = AccountNames.All(account =>
            {
                var snap = AllBots.FirstOrDefault(candidate =>
                    account.Equals(candidate.AccountName, StringComparison.OrdinalIgnoreCase));
                var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                if (pos == null)
                    return false;

                var mapId = snap!.Player?.Unit?.GameObject?.Base?.MapId ?? snap.CurrentMapId;
                if (mapId != MobAreaMapId)
                    return false;

                var dx = pos.X - MobAreaX;
                var dy = pos.Y - MobAreaY;
                return MathF.Sqrt((dx * dx) + (dy * dy)) <= stageRadius;
            });

            if (allStaged)
                return true;

            await Task.Delay(500);
        }

        return false;
    }
}
