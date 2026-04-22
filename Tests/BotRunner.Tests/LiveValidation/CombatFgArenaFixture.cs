using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

[CollectionDefinition(CombatFgArenaCollection.Name)]
public sealed class CombatFgArenaCollection : ICollectionFixture<CombatFgArenaFixture>
{
    public const string Name = "CombatFgArenaCollection";
}

/// <summary>
/// Dedicated fresh-account fixture for the mixed FG/BG combat baseline. One
/// foreground warrior and one background warrior are staged at the Valley of
/// Trials boar cluster so the test can dispatch one shared-target melee attack
/// per bot without any helper-owned combat setup.
/// </summary>
public sealed class CombatFgArenaFixture : CoordinatorFixtureBase
{
    public const int MobAreaMapId = 1;
    public const float MobAreaX = -620f;
    public const float MobAreaY = -4385f;
    public const float MobAreaZ = 44f;

    public string FgAccount => "FGONLYFG1";
    public string BgAccount => "FGONLYBG1";

    protected override string SettingsFileName => "CombatFg.settings.json";

    protected override string FixtureLabel => "COMBAT-FG";

    protected override bool DisableCoordinatorDuringPreparation => true;

    protected override bool PrepareDuringInitialization => true;

    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(4);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromSeconds(90);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
        => LoadCharacterSettingsFromConfig("CombatFg.config.json");

    protected override async Task PrepareBotsAsync()
    {
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
