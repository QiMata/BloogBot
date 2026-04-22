using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Real-combat integration validation against the dedicated arena fixture.
///
/// Contract:
/// - Dedicated accounts (ARENAFG1 + ARENABG1), not shared TESTBOT* pool.
/// - The fixture logs fresh characters in (cinematic auto-dismisses), teleports
///   both to the Valley of Trials boar cluster, and hands off control here.
/// - No <c>.damage</c>, no runtime GM-mode toggling, no <c>.respawn</c>.
///   Damage comes from the bot's own auto-attack via <see cref="ActionType.StartMeleeAttack"/>;
///   BotRunner's behavior tree owns chase, facing, and attack toggle.
/// - The test acts as StateManager's consumer: it asks the fixture for a mob
///   GUID, dispatches one StartMeleeAttack to the BG bot, and waits for the
///   snapshot-observed death of the boar.
/// - A level-1 warrior with starter gear beats a level-1 mottled boar reliably,
///   so the surviving-attacker assertion holds without runtime GM-mode toggles.
/// </summary>
[RequiresMangosStack]
[Collection(CombatArenaCollection.Name)]
public class CombatLoopTests
{
    private readonly CombatArenaFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint MottledBoarEntry = 3098;
    private const string MottledBoarName = "Mottled Boar";
    private const ulong CreatureGuidHighMask = 0xF000000000000000UL;
    private const ulong CreatureGuidHighPrefix = 0xF000000000000000UL;

    public CombatLoopTests(CombatArenaFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "CombatArena fixture not ready");
    }

    [SkippableFact]
    public async Task Combat_AutoAttackLevel1Boar_KillsMobFromRealMeleeCombat()
    {
        await _bot.RefreshSnapshotsAsync();

        var bgSnap = await _bot.GetSnapshotAsync(_bot.BgAccount);
        var fgSnap = await _bot.GetSnapshotAsync(_bot.FgAccount);
        Assert.NotNull(bgSnap);
        Assert.NotNull(fgSnap);

        var bgSelfGuid = bgSnap!.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        Assert.True(bgSelfGuid != 0, "BG bot must have a self GUID after world entry.");
        var fgSelfGuid = fgSnap!.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        Assert.True(fgSelfGuid != 0, "FG bot must have a self GUID after world entry.");

        _output.WriteLine(
            $"[ARENA] Start state: BG={_bot.BgAccount}/{bgSnap.CharacterName} " +
            $"(level={bgSnap.Player?.Unit?.GameObject?.Level}, HP={bgSnap.Player?.Unit?.Health}/{bgSnap.Player?.Unit?.MaxHealth}); " +
            $"FG={_bot.FgAccount}/{fgSnap.CharacterName} " +
            $"(level={fgSnap.Player?.Unit?.GameObject?.Level}, HP={fgSnap.Player?.Unit?.Health}/{fgSnap.Player?.Unit?.MaxHealth})");

        // Pick a single boar visible to BOTH bots so the FG viewport frames the fight
        // (both attackers piling on the same mob keeps them clustered rather than
        // chasing divergent targets off into the brush).
        var targetGuid = await FindBoarVisibleToBothBotsAsync(
            bgSelfGuid,
            fgSelfGuid,
            TimeSpan.FromSeconds(20));
        Assert.True(targetGuid != 0,
            "A Mottled Boar must be visible in both BG and FG NearbyUnits within 20s of staging.");

        _output.WriteLine($"[ARENA] Both bots auto-attacking boar 0x{targetGuid:X}");
        var attack = new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        };
        var bgDispatch = await _bot.SendActionAsync(_bot.BgAccount, attack);
        var fgDispatch = await _bot.SendActionAsync(_bot.FgAccount, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });
        Assert.Equal(ResponseResult.Success, bgDispatch);
        Assert.Equal(ResponseResult.Success, fgDispatch);

        // BotRunner's BuildStartMeleeAttackSequence owns chase, facing, and
        // auto-attack toggle per bot. With both attacking the same mob it dies
        // faster; we poll BOTH snapshots and accept the first that reports it
        // dead/gone — creature despawn removes it from NearbyUnits for every
        // observer, but health can snapshot as 0 on one side before the other.
        var deadOrGone = await WaitForMobDeadOrGoneOnEitherBotAsync(
            targetGuid,
            TimeSpan.FromSeconds(45));
        Assert.True(deadOrGone,
            $"Boar 0x{targetGuid:X} should die from real melee combat within 45s (no .damage used).");

        await _bot.RefreshSnapshotsAsync();
        var bgAfter = await _bot.GetSnapshotAsync(_bot.BgAccount);
        var fgAfter = await _bot.GetSnapshotAsync(_bot.FgAccount);
        Assert.NotNull(bgAfter);
        Assert.NotNull(fgAfter);
        var bgHealth = bgAfter!.Player?.Unit?.Health ?? 0;
        var fgHealth = fgAfter!.Player?.Unit?.Health ?? 0;
        Assert.True(bgHealth > 0,
            $"BG attacker should survive a level 1 boar fight (HP={bgHealth} after kill).");
        Assert.True(fgHealth > 0,
            $"FG attacker should survive a level 1 boar fight (HP={fgHealth} after kill).");

        _output.WriteLine(
            $"[ARENA] Success: boar dead via dual auto-attack; " +
            $"BG HP={bgAfter.Player?.Unit?.Health}/{bgAfter.Player?.Unit?.MaxHealth} lvl={bgAfter.Player?.Unit?.GameObject?.Level}, " +
            $"FG HP={fgAfter.Player?.Unit?.Health}/{fgAfter.Player?.Unit?.MaxHealth} lvl={fgAfter.Player?.Unit?.GameObject?.Level}");
    }

    private async Task<ulong> FindBoarVisibleToBothBotsAsync(
        ulong bgSelfGuid,
        ulong fgSelfGuid,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var bgSnap = await _bot.GetSnapshotAsync(_bot.BgAccount);
            var fgSnap = await _bot.GetSnapshotAsync(_bot.FgAccount);

            var bgBoarGuids = CollectLivingBoarGuids(bgSnap, bgSelfGuid);
            var fgBoarGuids = CollectLivingBoarGuids(fgSnap, fgSelfGuid);

            // Prefer a boar observed in BOTH snapshots — that guarantees both
            // bots are in interaction range and the FG viewport can frame it.
            var shared = bgBoarGuids.Intersect(fgBoarGuids).FirstOrDefault();
            if (shared != 0)
            {
                _output.WriteLine(
                    $"    Shared target 0x{shared:X} visible to both bots " +
                    $"(BG knows {bgBoarGuids.Count} boars, FG knows {fgBoarGuids.Count}).");
                return shared;
            }

            if (bgBoarGuids.Count > 0 && fgBoarGuids.Count > 0)
            {
                _output.WriteLine(
                    $"    Both bots see boars but no overlap yet " +
                    $"(BG={bgBoarGuids.Count}, FG={fgBoarGuids.Count}); waiting for FG/BG range to align.");
            }

            await Task.Delay(500);
        }

        return 0UL;
    }

    private static List<ulong> CollectLivingBoarGuids(WoWActivitySnapshot? snap, ulong selfGuid)
    {
        return snap?.NearbyUnits?
            .Where(u =>
            {
                var guid = u.GameObject?.Base?.Guid ?? 0UL;
                if (guid == 0 || guid == selfGuid)
                    return false;
                if ((guid & CreatureGuidHighMask) != CreatureGuidHighPrefix)
                    return false;
                if (u.Health == 0 || u.MaxHealth == 0)
                    return false;
                if (u.GameObject?.Level > 10)
                    return false;
                if (u.MaxHealth > 200)
                    return false;
                if (u.NpcFlags != 0)
                    return false;

                var entry = u.GameObject?.Entry ?? 0;
                var name = u.GameObject?.Name ?? string.Empty;
                return entry == MottledBoarEntry
                    || string.Equals(name, MottledBoarName, StringComparison.OrdinalIgnoreCase)
                    || name.Contains("mottled boar", StringComparison.OrdinalIgnoreCase);
            })
            .Select(u => u.GameObject?.Base?.Guid ?? 0UL)
            .Where(guid => guid != 0)
            .ToList() ?? new List<ulong>();
    }

    private async Task<bool> WaitForMobDeadOrGoneOnEitherBotAsync(ulong targetGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var bgSnap = await _bot.GetSnapshotAsync(_bot.BgAccount);
            var fgSnap = await _bot.GetSnapshotAsync(_bot.FgAccount);

            if (IsTargetDeadOrGone(bgSnap, targetGuid) || IsTargetDeadOrGone(fgSnap, targetGuid))
                return true;

            await Task.Delay(350);
        }

        return false;
    }

    private static bool IsTargetDeadOrGone(WoWActivitySnapshot? snap, ulong targetGuid)
    {
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return target == null || target.Health == 0;
    }

}
