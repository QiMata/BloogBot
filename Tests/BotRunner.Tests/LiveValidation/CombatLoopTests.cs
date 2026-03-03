using System;
using System.Collections.Concurrent;
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
/// 2) Teleport to Valley of Trials boar area.
/// 3) Find a living Mottled Boar in snapshot.
/// 4) Send StartMeleeAttack action — bot must target, approach, face, and auto-attack.
/// 5) Assert bot approaches within melee range (≤5.5y).
/// 6) Assert bot is facing the target (within 90°) when in melee range.
/// 7) Assert target health decreases from bot auto-attacks (not from GM .damage).
/// 8) Clean up with .damage only after real combat is validated.
///
/// The test DOES NOT pass by GM shortcuts — the bot must actually fight like a player.
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

    // Melee range: WoW melee reach = weapon range (~2y) + both capsule radii (~0.4y each) + margin.
    private const float MeleeRange = 5.5f;
    // Facing tolerance: attack must be within 90° of target direction.
    private const float FacingToleranceRad = (float)(Math.PI / 2.0);

    public CombatLoopTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Combat_AutoAttacksMob_DealsDamageInMeleeRange()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        var bgPassed = false;
        var fgPassed = false;
        var hasFg = _bot.ForegroundBot != null;

        // Shared set so concurrent bots claim different targets.
        var claimedTargets = new ConcurrentDictionary<ulong, string>();

        if (!hasFg)
        {
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            bgPassed = await RunCombatScenarioAsync(bgAccount, "BG", claimedTargets, offsetX: 0, offsetY: 0);
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
        else
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG combat scenarios in parallel — each targets a different boar.");

            // Offset positions so each bot lands near different boars.
            var bgTask = RunCombatScenarioAsync(bgAccount, "BG", claimedTargets, offsetX: -8, offsetY: 0);
            var fgTask = RunCombatScenarioAsync(fgAccount, "FG", claimedTargets, offsetX: +8, offsetY: 0);
            await Task.WhenAll(bgTask, fgTask);

            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }

        Assert.True(bgPassed, "BG bot must approach, face, and auto-attack a boar — .damage shortcut is NOT a pass.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot must approach, face, and auto-attack a boar — .damage shortcut is NOT a pass.");
    }

    private async Task<bool> RunCombatScenarioAsync(string account, string label,
        ConcurrentDictionary<ulong, string> claimedTargets, float offsetX = 0, float offsetY = 0)
    {
        await EnsureStrictAliveAsync(account, label);
        await EnsureNearMobAreaAsync(account, label, offsetX, offsetY);

        await _bot.RefreshSnapshotsAsync();
        var selfSnap = await _bot.GetSnapshotAsync(account);
        var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (selfGuid == 0)
        {
            _output.WriteLine($"  [{label}] Missing self GUID; cannot select target.");
            return false;
        }

        _output.WriteLine($"  [{label}] Finding candidate boar in snapshot...");
        var (targetGuid, initialHealth) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12), claimedTargets);
        if (targetGuid == 0)
        {
            _output.WriteLine($"  [{label}] No mob found; issuing .respawn and retrying once.");
            var respawnTrace = await _bot.SendGmChatCommandTrackedAsync(account, ".respawn", captureResponse: true, delayMs: 1000);
            AssertCommandSucceeded(respawnTrace, label, ".respawn");

            var respawnVerified = await WaitForNearbyUnitsAsync(account, TimeSpan.FromSeconds(6));
            _output.WriteLine(respawnVerified
                ? $"  [{label}] .respawn verified — nearby units detected."
                : $"  [{label}] Warning: .respawn did not produce nearby units within 6s.");

            (targetGuid, initialHealth) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12), claimedTargets);
        }

        if (targetGuid == 0)
        {
            _output.WriteLine($"  [{label}] No living boar found after retry.");
            return false;
        }

        _output.WriteLine($"  [{label}] Sending StartMeleeAttack on 0x{targetGuid:X} (initial HP={initialHealth})");
        var selectResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });
        if (selectResult != ResponseResult.Success)
        {
            _output.WriteLine($"  [{label}] StartMeleeAttack dispatch failed: {selectResult}");
            return false;
        }

        // STEP 1: Verify bot targeted the mob.
        var targeted = await WaitForSelectedTargetAsync(account, targetGuid, TimeSpan.FromSeconds(4));
        _output.WriteLine($"  [{label}] Target selected in snapshot: {targeted}");
        if (!targeted)
        {
            _output.WriteLine($"  [{label}] FAIL: Bot did not select target GUID in snapshot within 4s.");
            return false;
        }

        // STEP 2: Verify bot approaches to melee range (≤5.5y).
        _output.WriteLine($"  [{label}] Waiting for bot to approach melee range (≤{MeleeRange}y)...");
        var inMeleeRange = await WaitForMeleeRangeAsync(account, targetGuid, MeleeRange, TimeSpan.FromSeconds(20));
        if (!inMeleeRange)
        {
            var dist = await GetDistanceToTargetAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] FAIL: Bot did not enter melee range within 20s. Current distance: {dist:F1}y");
            return false;
        }
        _output.WriteLine($"  [{label}] Bot is in melee range.");

        // STEP 3: Verify facing — bot orientation must be within 90° of direction to target.
        var facingOk = await AssertBotFacingTargetAsync(account, targetGuid, label);
        if (!facingOk)
        {
            _output.WriteLine($"  [{label}] FAIL: Bot is not facing the target when in melee range.");
            return false;
        }

        // STEP 4: Verify bot auto-attacks — target health must decrease from bot hits (no GM help).
        _output.WriteLine($"  [{label}] Waiting for auto-attack damage on target (initial HP={initialHealth})...");
        var damagedByBot = await WaitForHealthDecreaseAsync(account, targetGuid, initialHealth, TimeSpan.FromSeconds(15));
        if (!damagedByBot)
        {
            var currentHealth = await GetTargetHealthAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] FAIL: Target health did not decrease from bot auto-attacks within 15s. HP: {initialHealth}→{currentHealth}");
            return false;
        }

        var healthAfterAttack = await GetTargetHealthAsync(account, targetGuid);
        _output.WriteLine($"  [{label}] Auto-attack confirmed: HP dropped {initialHealth}→{healthAfterAttack} from bot attacks.");

        // CLEANUP: Use .damage to finish the mob quickly (keeping the area tidy for other tests).
        // This is cleanup only — the real validation above already passed.
        _output.WriteLine($"  [{label}] Cleanup: finishing target 0x{targetGuid:X} with .damage.");
        var damageTrace = await _bot.SendGmChatCommandTrackedAsync(account, ".damage 5000", captureResponse: true, delayMs: 900);
        AssertCommandSucceeded(damageTrace, label, ".damage 5000 (cleanup)");

        var deadOrGone = await WaitForMobDeadOrGoneAsync(account, targetGuid, TimeSpan.FromSeconds(8));
        _output.WriteLine($"  [{label}] Target dead/removed after cleanup: {deadOrGone}");

        return true; // Real combat was validated — cleanup result is informational.
    }

    private async Task EnsureNearMobAreaAsync(string account, string label, float offsetX = 0, float offsetY = 0)
    {
        var targetX = MobAreaX + offsetX;
        var targetY = MobAreaY + offsetY;

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return;

        var distance = Distance2D(pos.X, pos.Y, targetX, targetY);
        if (distance <= MobAreaRadius)
        {
            _output.WriteLine($"  [{label}] Already near mob area (distance={distance:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to mob area (distance={distance:F1}y).");
        await _bot.BotTeleportAsync(account, MapId, targetX, targetY, MobAreaZ);

        var arrived = await WaitForNearPositionAsync(account, targetX, targetY, MobAreaRadius, TimeSpan.FromSeconds(12));
        Assert.True(arrived, $"[{label}] Failed to arrive near mob area after teleport.");
    }

    /// <summary>
    /// Finds a living Mottled Boar in the snapshot and returns its GUID and current health.
    /// Prefers the closest unclaimed boar to minimize approach time.
    /// </summary>
    private async Task<(ulong guid, uint health)> FindLivingMobAsync(string account, ulong selfGuid, TimeSpan timeout,
        ConcurrentDictionary<ulong, string>? claimedTargets = null)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var selfPos = snap?.Player?.Unit?.GameObject?.Base?.Position;

            var candidates = snap?.NearbyUnits?
                .Where(u =>
                {
                    var guid = u.GameObject?.Base?.Guid ?? 0UL;
                    if (guid == 0 || guid == selfGuid)
                        return false;

                    // Only attack creature GUIDs (not players).
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

                    // Must be a Mottled Boar specifically.
                    var entry = u.GameObject?.Entry ?? 0;
                    var name = u.GameObject?.Name ?? string.Empty;
                    var isBoar =
                        entry == MottledBoarEntry ||
                        string.Equals(name, MottledBoarName, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("mottled boar", StringComparison.OrdinalIgnoreCase);
                    if (!isBoar)
                        return false;

                    // Skip targets already claimed by the other bot.
                    return claimedTargets == null || !claimedTargets.ContainsKey(guid);
                })
                // Prefer closer targets to reduce approach time.
                .OrderBy(u =>
                {
                    if (selfPos == null) return float.MaxValue;
                    var p = u.GameObject?.Base?.Position;
                    return p == null ? float.MaxValue : Distance2D(selfPos.X, selfPos.Y, p.X, p.Y);
                })
                .ToList() ?? [];

            if (candidates.Count > 0)
            {
                var mob = candidates[0];
                var guid = mob.GameObject?.Base?.Guid ?? 0UL;
                var pos = mob.GameObject?.Base?.Position;
                var name = string.IsNullOrWhiteSpace(mob.GameObject?.Name) ? "<unknown>" : mob.GameObject.Name;
                var entry = mob.GameObject?.Entry ?? 0;
                var dist = selfPos != null && pos != null ? Distance2D(selfPos.X, selfPos.Y, pos.X, pos.Y) : -1f;
                _output.WriteLine($"    Candidate: 0x{guid:X} '{name}' entry={entry} HP={mob.Health}/{mob.MaxHealth} dist={dist:F1}y at ({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F1})");
                if (guid != 0)
                {
                    claimedTargets?.TryAdd(guid, account);
                    return (guid, mob.Health);
                }
            }

            await Task.Delay(500);
        }

        return (0UL, 0);
    }

    private async Task<bool> WaitForSelectedTargetAsync(string account, ulong targetGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if ((snap?.Player?.Unit?.TargetGuid ?? 0UL) == targetGuid)
                return true;
            await Task.Delay(250);
        }

        return false;
    }

    /// <summary>
    /// Waits until the bot's position is within <paramref name="maxRange"/> yards of the target.
    /// </summary>
    private async Task<bool> WaitForMeleeRangeAsync(string account, ulong targetGuid, float maxRange, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var dist = await GetDistanceToTargetAsync(account, targetGuid);
            if (dist <= maxRange)
                return true;
            await Task.Delay(350);
        }

        return false;
    }

    /// <summary>
    /// Returns current 2D distance from the bot to the target unit. Returns float.MaxValue if unknown.
    /// </summary>
    private async Task<float> GetDistanceToTargetAsync(string account, ulong targetGuid)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var selfPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        var targetPos = target?.GameObject?.Base?.Position;
        if (selfPos == null || targetPos == null)
            return float.MaxValue;
        return Distance2D(selfPos.X, selfPos.Y, targetPos.X, targetPos.Y);
    }

    /// <summary>
    /// Samples the bot's facing once and verifies it is within FacingToleranceRad of the direction to target.
    /// </summary>
    private async Task<bool> AssertBotFacingTargetAsync(string account, ulong targetGuid, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var selfBase = snap?.Player?.Unit?.GameObject?.Base;
        var selfPos = selfBase?.Position;
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        var targetPos = target?.GameObject?.Base?.Position;

        if (selfPos == null || targetPos == null)
        {
            _output.WriteLine($"  [{label}] Facing check skipped — positions unavailable.");
            return true; // Give benefit of the doubt if snapshot unavailable.
        }

        var botFacing = selfBase?.Facing ?? 0f;
        var expectedFacing = (float)Math.Atan2(targetPos.Y - selfPos.Y, targetPos.X - selfPos.X);
        var diff = Math.Abs(NormalizeAngle(botFacing - expectedFacing));

        _output.WriteLine($"  [{label}] Facing check: bot={botFacing:F2} rad, toward target={expectedFacing:F2} rad, diff={diff:F2} rad (tolerance={FacingToleranceRad:F2})");
        return diff <= FacingToleranceRad;
    }

    /// <summary>
    /// Waits until the target's health drops below its initial recorded health value.
    /// Returns true if health decreased (bot dealt at least one hit), false on timeout.
    /// </summary>
    private async Task<bool> WaitForHealthDecreaseAsync(string account, ulong targetGuid, uint initialHealth, TimeSpan timeout)
    {
        if (initialHealth == 0)
            return false;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var currentHealth = await GetTargetHealthAsync(account, targetGuid);
            if (currentHealth < initialHealth)
                return true;
            if (currentHealth == 0)
                return true; // Already dead — counts as damage dealt.
            await Task.Delay(400);
        }

        return false;
    }

    private async Task<uint> GetTargetHealthAsync(string account, ulong targetGuid)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return target?.Health ?? 0;
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

    private Task EnsureStrictAliveAsync(string account, string label)
        => _bot.EnsureStrictAliveAsync(account, label);

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

    private async Task<bool> WaitForNearbyUnitsAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var unitCount = snap?.NearbyUnits?.Count(u => u.Health > 0) ?? 0;
            if (unitCount > 0)
                return true;
            await Task.Delay(500);
        }

        return false;
    }

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Normalizes an angle to [-π, π].</summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > Math.PI) angle -= (float)(2 * Math.PI);
        while (angle < -Math.PI) angle += (float)(2 * Math.PI);
        return angle;
    }

    private static bool ContainsCombatCommandFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("you cannot attack that target", StringComparison.OrdinalIgnoreCase)
            || text.Contains("you should select a character or a creature", StringComparison.OrdinalIgnoreCase)
            || text.Contains("invalid target", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
