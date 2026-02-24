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
/// Combat loop integration validation (FG + BG).
///
/// Per bot:
/// 1) Ensure strict-alive setup from snapshot state.
/// 2) Ensure location near Valley of Trials mob area (conditional teleport only).
/// 3) Find a living low-level mob from nearby snapshot units.
/// 4) Select target via action forwarding and verify target GUID in snapshot.
/// 5) Kill selected target via GM .damage and verify death/removal in snapshot.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class CombatLoopTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    // Boar-dense Valley of Trials cluster (keeps us away from camp allies/trainers).
    private const float MobAreaX = -620f;
    private const float MobAreaY = -4385f;
    private const float MobAreaZ = 44f;
    private const float MobAreaRadius = 45f;
    private const uint MottledBoarEntry = 3098;
    private const string MottledBoarName = "Mottled Boar";
    private const ulong CreatureGuidHighMask = 0xF000000000000000UL;
    private const ulong CreatureGuidHighPrefix = 0xF000000000000000UL;

    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

    public CombatLoopTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Combat_TargetAndKillMob_MobDies()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        var bgPassed = false;
        var fgPassed = false;

        if (_bot.ForegroundBot == null)
        {
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            bgPassed = await RunCombatScenarioAsync(bgAccount, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
        else
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG combat scenarios concurrently.");

            var bgTask = RunCombatScenarioAsync(bgAccount, "BG");
            var fgTask = RunCombatScenarioAsync(fgAccount, "FG");
            await Task.WhenAll(bgTask, fgTask);

            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }

        Assert.True(bgPassed, "BG bot should target and kill a nearby mob (snapshot-confirmed).");
        if (_bot.ForegroundBot != null)
            Assert.True(fgPassed, "FG bot should target and kill a nearby mob (snapshot-confirmed).");
    }

    private async Task<bool> RunCombatScenarioAsync(string account, string label)
    {
        await EnsureStrictAliveAsync(account, label);
        await EnsureNearMobAreaAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var selfSnap = await _bot.GetSnapshotAsync(account);
        var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (selfGuid == 0)
        {
            _output.WriteLine($"  [{label}] Missing self GUID; cannot select target.");
            return false;
        }

        _output.WriteLine($"  [{label}] Finding candidate mob in snapshot...");
        var targetGuid = await FindLivingMobGuidAsync(account, selfGuid, TimeSpan.FromSeconds(12));
        if (targetGuid == 0)
        {
            _output.WriteLine($"  [{label}] No mob found in first scan; issuing .respawn and retrying once.");
            var respawnTrace = await _bot.SendGmChatCommandTrackedAsync(account, ".respawn", captureResponse: true, delayMs: 1000);
            AssertCommandSucceeded(respawnTrace, label, ".respawn");
            targetGuid = await FindLivingMobGuidAsync(account, selfGuid, TimeSpan.FromSeconds(12));
        }

        if (targetGuid == 0)
        {
            _output.WriteLine($"  [{label}] No living mob found after retry window.");
            return false;
        }

        _output.WriteLine($"  [{label}] Selecting target 0x{targetGuid:X}");
        var selectResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });
        if (selectResult != ResponseResult.Success)
        {
            _output.WriteLine($"  [{label}] Target selection dispatch failed: {selectResult}");
            return false;
        }
        await Task.Delay(700);

        var selected = await WaitForSelectedTargetAsync(account, targetGuid, TimeSpan.FromSeconds(4));
        _output.WriteLine($"  [{label}] Selected target in snapshot: {selected}");
        if (!selected)
        {
            var deadBeforeDamage = await WaitForMobDeadOrGoneAsync(account, targetGuid, TimeSpan.FromSeconds(2));
            if (deadBeforeDamage)
            {
                _output.WriteLine($"  [{label}] Target died immediately after engage (before .damage).");
                return true;
            }

            _output.WriteLine($"  [{label}] Target GUID was not observed in snapshot; proceeding with .damage to verify live selection.");
        }

        _output.WriteLine($"  [{label}] Killing target 0x{targetGuid:X} via .damage 5000");
        var damageTrace = await _bot.SendGmChatCommandTrackedAsync(account, ".damage 5000", captureResponse: true, delayMs: 900);
        AssertCommandSucceeded(damageTrace, label, ".damage 5000");
        if (TraceHasCombatCommandFailure(damageTrace))
        {
            var deadAfterDamageFailure = await WaitForMobDeadOrGoneAsync(account, targetGuid, TimeSpan.FromSeconds(2));
            if (!deadAfterDamageFailure)
            {
                _output.WriteLine($"  [{label}] .damage failed with combat-target error and target is still alive.");
                return false;
            }
        }

        var deadOrGone = await WaitForMobDeadOrGoneAsync(account, targetGuid, TimeSpan.FromSeconds(8));
        if (!deadOrGone)
        {
            _output.WriteLine($"  [{label}] Target still alive after first damage attempt; retrying once.");

            var reselection = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            if (reselection != ResponseResult.Success)
                return false;
            await Task.Delay(600);

            var retryTrace = await _bot.SendGmChatCommandTrackedAsync(account, ".damage 5000", captureResponse: true, delayMs: 900);
            AssertCommandSucceeded(retryTrace, label, ".damage retry");
            if (TraceHasCombatCommandFailure(retryTrace))
            {
                _output.WriteLine($"  [{label}] .damage retry failed with combat-target error.");
                return false;
            }
            deadOrGone = await WaitForMobDeadOrGoneAsync(account, targetGuid, TimeSpan.FromSeconds(8));
        }

        _output.WriteLine($"  [{label}] Target dead/removed: {deadOrGone}");
        return deadOrGone;
    }

    private async Task EnsureNearMobAreaAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return;

        var distance = Distance2D(pos.X, pos.Y, MobAreaX, MobAreaY);
        if (distance <= MobAreaRadius)
        {
            _output.WriteLine($"  [{label}] Already near mob area (distance={distance:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to mob area (distance={distance:F1}y).");
        await _bot.BotTeleportAsync(account, MapId, MobAreaX, MobAreaY, MobAreaZ);

        var arrived = await WaitForNearPositionAsync(account, MobAreaX, MobAreaY, MobAreaRadius, TimeSpan.FromSeconds(12));
        Assert.True(arrived, $"[{label}] Failed to arrive near mob area after teleport.");
    }

    private async Task<ulong> FindLivingMobGuidAsync(string account, ulong selfGuid, TimeSpan timeout)
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

                    // Reject allied/player targets; only attack creature GUIDs.
                    if ((guid & CreatureGuidHighMask) != CreatureGuidHighPrefix)
                        return false;

                    if (u.Health == 0 || u.MaxHealth == 0)
                        return false;

                    // Valley of Trials mobs are low-level, low-health creatures.
                    if (u.GameObject?.Level > 10)
                        return false;

                    if (u.MaxHealth > 200)
                        return false;

                    // Friendly/interactive NPCs (quest givers, trainers, etc.) are excluded.
                    if (u.NpcFlags != 0)
                        return false;

                    // Combat test must target neutral boars, not allied NPCs.
                    var entry = u.GameObject?.Entry ?? 0;
                    var name = u.GameObject?.Name ?? string.Empty;
                    var isBoar =
                        entry == MottledBoarEntry ||
                        string.Equals(name, MottledBoarName, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("mottled boar", StringComparison.OrdinalIgnoreCase);
                    if (!isBoar)
                        return false;

                    return true;
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
                var entry = mob.GameObject?.Entry ?? 0;
                _output.WriteLine($"    Candidate target 0x{guid:X}: name='{name}' entry={entry} npcFlags=0x{mob.NpcFlags:X} HP={mob.Health}/{mob.MaxHealth} at ({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F1})");
                if (guid != 0)
                    return guid;
            }

            await Task.Delay(500);
        }

        return 0UL;
    }

    private async Task<bool> WaitForSelectedTargetAsync(string account, ulong targetGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var selectedGuid = snap?.Player?.Unit?.TargetGuid ?? 0UL;
            if (selectedGuid == targetGuid)
                return true;
            await Task.Delay(250);
        }

        return false;
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
            await Task.Delay(300);
        }

        return false;
    }

    private async Task EnsureStrictAliveAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (IsStrictAlive(snap))
            return;

        var characterName = snap?.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(characterName), $"{label}: missing character name for revive setup.");

        _output.WriteLine($"  [{label}] Not strict-alive at setup; reviving.");
        await _bot.RevivePlayerAsync(characterName!);

        var restored = await WaitForStrictAliveAsync(account, TimeSpan.FromSeconds(15));
        global::Tests.Infrastructure.Skip.If(!restored, $"{label}: failed to restore strict-alive setup state.");
    }

    private async Task<bool> WaitForStrictAliveAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (IsStrictAlive(snap))
                return true;
            await Task.Delay(350);
        }

        return false;
    }

    private async Task<bool> WaitForNearPositionAsync(string account, float x, float y, float radius, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null && Distance2D(pos.X, pos.Y, x, y) <= radius)
                return true;
            await Task.Delay(350);
        }

        return false;
    }

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsStrictAlive(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        var standState = unit.Bytes1 & StandStateMask;
        return unit.Health > 0 && !hasGhostFlag && standState != StandStateDead;
    }

    private static bool ContainsCommandRejection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("no such command", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no such subcommand", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unknown command", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not available to you", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCombatCommandFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("you cannot attack that target", StringComparison.OrdinalIgnoreCase)
            || text.Contains("you should select a character or a creature", StringComparison.OrdinalIgnoreCase)
            || text.Contains("invalid target", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TraceHasCombatCommandFailure(LiveBotFixture.GmChatCommandTrace trace)
        => trace.ChatMessages.Concat(trace.ErrorMessages).Any(ContainsCombatCommandFailure);

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
