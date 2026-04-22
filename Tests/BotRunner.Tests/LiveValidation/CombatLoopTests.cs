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
/// - No <c>.damage</c>, no <c>.gm on/off</c> toggling, no <c>.respawn</c>.
///   Damage comes from the bot's own auto-attack via <see cref="ActionType.StartMeleeAttack"/>;
///   BotRunner's behavior tree owns chase, facing, and attack toggle.
/// - The test acts as StateManager's consumer: it asks the fixture for a mob
///   GUID, dispatches one StartMeleeAttack to the BG bot, and waits for the
///   snapshot-observed death of the boar.
/// - A level-1 warrior with starter gear beats a level-1 mottled boar reliably,
///   so the surviving-attacker assertion holds even with GM mode OFF.
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

        var targetGuid = await FindLivingBoarGuidAsync(_bot.BgAccount, bgSelfGuid, TimeSpan.FromSeconds(20));
        Assert.True(targetGuid != 0,
            "Valley of Trials Mottled Boar must be visible in BG snapshot within 20s of staging.");

        _output.WriteLine($"[ARENA] BG dispatching StartMeleeAttack on boar 0x{targetGuid:X}");
        var dispatch = await _bot.SendActionAsync(_bot.BgAccount, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        // BotRunner's BuildStartMeleeAttackSequence owns chase, facing, and
        // auto-attack toggle. We only poll for the snapshot-observed death.
        var deadOrGone = await WaitForMobDeadOrGoneAsync(_bot.BgAccount, targetGuid, TimeSpan.FromSeconds(45));
        Assert.True(deadOrGone,
            $"Boar 0x{targetGuid:X} should die from real melee combat within 45s (no .damage used).");

        await _bot.RefreshSnapshotsAsync();
        var bgAfter = await _bot.GetSnapshotAsync(_bot.BgAccount);
        Assert.NotNull(bgAfter);
        var bgHealth = bgAfter!.Player?.Unit?.Health ?? 0;
        Assert.True(bgHealth > 0,
            $"BG attacker should survive a level 1 boar fight (HP={bgHealth} after kill).");

        _output.WriteLine(
            $"[ARENA] Success: boar dead via auto-attack; BG HP={bgAfter.Player?.Unit?.Health}/{bgAfter.Player?.Unit?.MaxHealth}, " +
            $"level={bgAfter.Player?.Unit?.GameObject?.Level}");
    }

    private async Task<ulong> FindLivingBoarGuidAsync(string account, ulong selfGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var candidates = snap?.NearbyUnits?
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
                .OrderBy(u => u.GameObject?.Level ?? uint.MaxValue)
                .ThenBy(u => u.MaxHealth)
                .ToList() ?? [];

            if (candidates.Count > 0)
            {
                var mob = candidates[0];
                var guid = mob.GameObject?.Base?.Guid ?? 0UL;
                var pos = mob.GameObject?.Base?.Position;
                var name = string.IsNullOrWhiteSpace(mob.GameObject?.Name) ? "<unknown>" : mob.GameObject.Name;
                _output.WriteLine(
                    $"    Candidate boar 0x{guid:X}: name='{name}' entry={mob.GameObject?.Entry ?? 0} " +
                    $"HP={mob.Health}/{mob.MaxHealth} at ({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F1})");
                if (guid != 0)
                    return guid;
            }

            await Task.Delay(500);
        }

        return 0UL;
    }

    private async Task<bool> WaitForMobDeadOrGoneAsync(string account, ulong targetGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            if (target == null || target.Health == 0)
                return true;

            await Task.Delay(350);
        }

        return false;
    }
}
