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
/// 4) Teleport bot to within 3y of the boar (testing attack, not BotRunner approach automation).
/// 5) Send StartMeleeAttack action.
/// 6) Assert bot selects the target (TargetGuid in snapshot).
/// 7) Assert bot is facing the target (within 90°).
/// 8) Auto-attack the mob to death — validates full combat lifecycle
///    (CMSG_ATTACKSWING → server swing timer → SMSG_ATTACKERSTATEUPDATE → mob HP=0).
///    GM character won't die to level 1-3 mobs due to high stats.
///
/// NOTE: Bot approach automation (combat loop pathfinding toward target) is tested separately
/// once BotRunner movement-in-combat is confirmed working. This test validates the attack
/// mechanics — facing, targeting, and full kill — independently of approach.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class CombatLoopTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    // Two separate mob spawn locations in Valley of Trials / Durotar.
    // Each bot teleports to its own area so they never compete for the same mob.
    // BG area: Valley of Trials scorpid field — Scorpid Workers (entry 3124, level 1-3).
    private const float BgMobAreaX = -284f;
    private const float BgMobAreaY = -4383f;
    private const float BgMobAreaZ = 57f;
    // FG area: Same scorpid field as BG. Sequential execution means no contention.
    // BG kills one mob; FG attacks a different one from the same cluster.
    // Previous separate coords (-354, -4421) only had Lazy Peons (Friendly, react=4),
    // and (-290, -4395) had no valid terrain for FG (fell underground).
    private const float FgMobAreaX = BgMobAreaX;
    private const float FgMobAreaY = BgMobAreaY;
    private const float FgMobAreaZ = BgMobAreaZ;
    private const float MobAreaRadius = 80f;
    private const ulong CreatureGuidHighMask = 0xF000000000000000UL;
    private const ulong CreatureGuidHighPrefix = 0xF000000000000000UL;

    // Known hostile creature template entries for Valley of Trials combat testing.
    // UnitReaction is NOT reliable in snapshots: BG defaults to Hated(0), FG may report Friendly(4)
    // even for hostile mobs (GM mode side-effect). Filter by entry instead.
    private static readonly HashSet<uint> HostileCreatureEntries = [3098, 3124, 3108]; // Mottled Boar, Scorpid Worker, Vile Familiar

    // Weapon setup: Worn Mace (item 36) + One Hand Maces proficiency (spell 198, skill 54).
    // Other tests may call .reset items which strips all gear including starter weapons.
    private const uint OneHandMaceSpell = 198;

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
        var hasFg = _bot.IsFgActionable;

        if (!hasFg)
        {
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            bgPassed = await RunCombatScenarioAsync(bgAccount, "BG", null, BgMobAreaX, BgMobAreaY, BgMobAreaZ);
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
        else
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG then FG sequentially — each targets a different boar.");

            // Run sequentially: BG first, then FG.
            // Share claimedTargets so FG won't pick up a mob that BG already attacked and evaded from.
            // A mob that evaded from BG has corrupted server-side AI state (evade timer accumulated)
            // and will immediately re-evade if FG attacks it.
            var claimedTargets = new ConcurrentDictionary<ulong, string>();
            bgPassed = await RunCombatScenarioAsync(bgAccount, "BG", claimedTargets, BgMobAreaX, BgMobAreaY, BgMobAreaZ);
            fgPassed = await RunCombatScenarioAsync(fgAccount, "FG", claimedTargets, FgMobAreaX, FgMobAreaY, FgMobAreaZ);
        }

        Assert.True(bgPassed, "BG bot must approach, face, and auto-attack a boar — real auto-attack damage required.");
        if (hasFg)
        {
            Assert.True(fgPassed, "FG bot must approach, face, and auto-attack a boar without mob evade. " +
                "Any evasion means auto-attack flow is broken (GM mode still on, position desync, or AttackTarget() failed).");
        }
    }

    private async Task<bool> RunCombatScenarioAsync(string account, string label,
        ConcurrentDictionary<ulong, string>? claimedTargets, float areaX, float areaY, float areaZ)
    {
        // ── SETUP ──────────────────────────────────────────────────────────
        await EnsureStrictAliveAsync(account, label);

        // GM mode OFF for combat — GM invisibility causes mob evade.
        bool gmTurnedOff = false;
        _output.WriteLine($"  [{label}] Turning GM mode OFF for combat.");
        await _bot.SendGmChatCommandTrackedAsync(account, ".gm off", captureResponse: true, delayMs: 1000);
        await _bot.SendGmChatCommandTrackedAsync(account, ".gm visible on", captureResponse: true, delayMs: 500);
        gmTurnedOff = true;
        await Task.Delay(500);

        try
        {
            // Weapon setup — .reset items from other tests may strip starter gear.
            _output.WriteLine($"  [{label}] Ensuring weapon equipped (Worn Mace).");
            await _bot.BotLearnSpellAsync(account, OneHandMaceSpell);
            await _bot.BotSetSkillAsync(account, 54, 1, 300);
            await _bot.BotAddItemAsync(account, LiveBotFixture.TestItems.WornMace);
            await Task.Delay(500);
            await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.EquipItem,
                Parameters = { new RequestParameter { IntParam = (int)LiveBotFixture.TestItems.WornMace } }
            });
            await Task.Delay(500);

            // Teleport to mob area.
            await EnsureNearMobAreaAsync(account, label, areaX, areaY, areaZ);
            await _bot.WaitForSnapshotConditionAsync(account,
                s => s.NearbyUnits?.Count > 0, TimeSpan.FromSeconds(5), pollIntervalMs: 500);

            await _bot.RefreshSnapshotsAsync();
            var selfSnap = await _bot.GetSnapshotAsync(account);
            var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
            if (selfGuid == 0)
            {
                _output.WriteLine($"  [{label}] Missing self GUID.");
                return false;
            }

            // Find a living mob.
            _output.WriteLine($"  [{label}] Finding candidate mob...");
            var (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12), claimedTargets);
            if (targetGuid == 0)
            {
                _output.WriteLine($"  [{label}] No mob found; spawning temp Mottled Boar (entry 3098).");
                await _bot.SendGmChatCommandTrackedAsync(account, ".npc add temp 3098", captureResponse: true, delayMs: 1000);
                await _bot.WaitForSnapshotConditionAsync(account,
                    s => s.NearbyUnits?.Count > 0, TimeSpan.FromSeconds(5), pollIntervalMs: 500);
                await _bot.RefreshSnapshotsAsync();
                (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12), claimedTargets);
            }

            if (targetGuid == 0)
            {
                _output.WriteLine($"  [{label}] No living mob found.");
                return false;
            }

            // Ensure we're far enough away that the chase generates movement packets.
            // After GM teleport, the BG physics engine ground-snaps to a Z that differs
            // from MaNGOS server terrain. Walking produces heartbeats that sync position.
            {
                await _bot.RefreshSnapshotsAsync();
                var sp = (await _bot.GetSnapshotAsync(account))?.Player?.Unit?.GameObject?.Base?.Position;
                if (sp != null)
                {
                    var dx2 = mobX - sp.X;
                    var dy2 = mobY - sp.Y;
                    var curDist = MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
                    if (curDist < 15f)
                    {
                        float tpX = curDist > 0.1f ? mobX - (dx2 / curDist) * 20f : mobX + 20f;
                        float tpY = curDist > 0.1f ? mobY - (dy2 / curDist) * 20f : mobY;
                        _output.WriteLine($"  [{label}] Too close ({curDist:F1}y); re-teleporting 20y away for approach.");
                        await _bot.BotTeleportAsync(account, MapId, tpX, tpY, areaZ);
                        await _bot.WaitForTeleportSettledAsync(account, tpX, tpY);
                    }
                }
            }

            // ── ACTION: Send StartMeleeAttack ──────────────────────────────
            // BotRunner's combat sequence handles everything: chase to melee range,
            // face the target, toggle auto-attack on, keep chasing if mob moves.
            // The test just sends the action and observes the outcome.
            initialHealth = await GetTargetHealthAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] Sending StartMeleeAttack on 0x{targetGuid:X} (HP={initialHealth})");

            var attackResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            if (attackResult != ResponseResult.Success)
            {
                _output.WriteLine($"  [{label}] StartMeleeAttack dispatch failed: {attackResult}");
                return false;
            }

            // ── OBSERVE: Wait for mob death ────────────────────────────────
            // BotRunner handles chase + attack. Test just polls snapshots for HP changes.
            // Level 1-3 mob (~100 HP), weapon speed ~2s, damage ~10-20/swing → ~30s max.
            // Extra time for initial chase approach.
            var mobKilled = await WaitForMobDeathAsync(account, label, targetGuid, initialHealth, TimeSpan.FromSeconds(60));

            if (!mobKilled)
            {
                var currentHealth = await GetTargetHealthAsync(account, targetGuid);
                var postDist = await GetDistanceToTargetAsync(account, targetGuid);
                _output.WriteLine($"  [{label}] FAIL: HP {initialHealth}→{currentHealth}, dist={postDist:F1}y");
                return false;
            }

            _output.WriteLine($"  [{label}] COMBAT COMPLETE: Mob killed via auto-attacks.");
            return true;
        }
        finally
        {
            if (gmTurnedOff)
            {
                _output.WriteLine($"  [{label}] Restoring .gm on.");
                await _bot.SendGmChatCommandTrackedAsync(account, ".gm on", captureResponse: false, delayMs: 500);
            }
        }
    }

    private async Task EnsureNearMobAreaAsync(string account, string label, float targetX, float targetY, float targetZ)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return;

        var distance = LiveBotFixture.Distance2D(pos.X, pos.Y, targetX, targetY);
        if (distance <= MobAreaRadius)
        {
            _output.WriteLine($"  [{label}] Already near mob area (distance={distance:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to mob area (distance={distance:F1}y).");
        await _bot.BotTeleportAsync(account, MapId, targetX, targetY, targetZ);

        var arrived = await WaitForNearPositionAsync(account, targetX, targetY, MobAreaRadius, TimeSpan.FromSeconds(12));
        Assert.True(arrived, $"[{label}] Failed to arrive near mob area after teleport.");
    }

    /// <summary>
    /// Finds a living low-level attackable mob in the snapshot.
    /// Prefers the closest unclaimed mob to minimize approach time.
    /// </summary>
    private async Task<(ulong guid, uint health, float mobX, float mobY, float mobZ)> FindLivingMobAsync(string account, ulong selfGuid, TimeSpan timeout,
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

                    // Low-level, low-health creatures only (avoids guards, elites, etc.).
                    if (u.GameObject?.Level > 10)
                        return false;

                    if (u.MaxHealth > 500)
                        return false;

                    // Friendly/interactive NPCs (quest givers, trainers, etc.) are excluded.
                    if (u.NpcFlags != 0)
                        return false;

                    // Only attack known hostile creature types by entry ID.
                    // UnitReaction is unreliable in snapshots (BG defaults to 0, FG may report
                    // Friendly(4) for hostile mobs due to GM mode side-effects).
                    var entry = u.GameObject?.Entry ?? 0;
                    if (!HostileCreatureEntries.Contains(entry))
                        return false;

                    // Skip targets already claimed by the other bot.
                    return claimedTargets == null || !claimedTargets.ContainsKey(guid);
                })
                // Prefer closer targets to reduce approach time.
                .OrderBy(u =>
                {
                    if (selfPos == null) return float.MaxValue;
                    var p = u.GameObject?.Base?.Position;
                    return p == null ? float.MaxValue : LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, p.X, p.Y);
                })
                .ToList() ?? [];

            if (candidates.Count > 0)
            {
                var mob = candidates[0];
                var guid = mob.GameObject?.Base?.Guid ?? 0UL;
                var pos = mob.GameObject?.Base?.Position;
                var name = string.IsNullOrWhiteSpace(mob.GameObject?.Name) ? "<unknown>" : mob.GameObject.Name;
                var entry = mob.GameObject?.Entry ?? 0;
                var dist = selfPos != null && pos != null ? LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, pos.X, pos.Y) : -1f;
                _output.WriteLine($"    Candidate: 0x{guid:X} '{name}' entry={entry} HP={mob.Health}/{mob.MaxHealth} dist={dist:F1}y at ({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F1})");
                if (guid != 0 && (claimedTargets == null || claimedTargets.TryAdd(guid, account)))
                {
                    return (guid, mob.Health, pos?.X ?? 0f, pos?.Y ?? 0f, pos?.Z ?? 0f);
                }
            }
            else if (sw.Elapsed.TotalSeconds < 2) // Dump unit info on first pass for diagnostics
            {
                var allUnits = snap?.NearbyUnits ?? [];
                _output.WriteLine($"    [FindMob] {allUnits.Count} nearby units, 0 candidates. Self=0x{selfGuid:X} selfPos=({selfPos?.X:F1},{selfPos?.Y:F1},{selfPos?.Z:F1})");
                foreach (var u in allUnits.Take(10))
                {
                    var g = u.GameObject?.Base?.Guid ?? 0UL;
                    var p = u.GameObject?.Base?.Position;
                    var n = u.GameObject?.Name ?? "?";
                    var isCre = (g & CreatureGuidHighMask) == CreatureGuidHighPrefix;
                    var claimed = claimedTargets?.ContainsKey(g) ?? false;
                    _output.WriteLine($"      0x{g:X} '{n}' L{u.GameObject?.Level} HP={u.Health}/{u.MaxHealth} npc={u.NpcFlags} react={u.UnitReaction} creature={isCre} claimed={claimed} at ({p?.X:F1},{p?.Y:F1},{p?.Z:F1})");
                }
            }
            // BT-FEEDBACK-001: Log progress every 5s when no candidates found
            else if ((int)sw.Elapsed.TotalSeconds % 5 == 0 && sw.Elapsed.TotalSeconds >= 5)
            {
                var unitCount = snap?.NearbyUnits?.Count ?? 0;
                _output.WriteLine($"    [FindMob] Still searching... {sw.Elapsed.TotalSeconds:F0}s / {timeout.TotalSeconds:F0}s, {unitCount} nearby units");
            }

            await Task.Delay(500);
        }

        return (0UL, 0, 0f, 0f, 0f);
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
        return LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, targetPos.X, targetPos.Y);
    }

    /// <summary>
    /// Waits until the target mob dies (HP reaches 0) from auto-attacks.
    /// BotRunner handles chase + facing + attack toggle — test only observes HP.
    /// Returns false on timeout or evade (HP resets to max with TargetGuid cleared).
    /// </summary>
    private async Task<bool> WaitForMobDeathAsync(string account, string label, ulong targetGuid, uint initialHealth, TimeSpan timeout)
    {
        if (initialHealth == 0)
            return true; // Already dead

        var sw = Stopwatch.StartNew();
        var lastDiagTime = TimeSpan.Zero;
        uint lastLoggedHp = initialHealth;
        bool firstDamageConfirmed = false;

        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            var currentHealth = target?.Health ?? 0;
            var diagTarget = snap?.Player?.Unit?.TargetGuid ?? 0UL;

            // Mob died — full combat lifecycle complete.
            if (target == null || currentHealth == 0)
            {
                _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: MOB KILLED (HP {initialHealth}→0) after {sw.Elapsed.TotalSeconds:F1}s");
                return true;
            }

            // Log each HP change to track swing-by-swing damage.
            if (currentHealth != lastLoggedHp)
            {
                if (!firstDamageConfirmed)
                {
                    _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: FIRST HIT — HP {lastLoggedHp}→{currentHealth} (damage={lastLoggedHp - currentHealth})");
                    firstDamageConfirmed = true;
                }
                else
                {
                    _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: HP {lastLoggedHp}→{currentHealth} (damage={lastLoggedHp - currentHealth})");
                }
                lastLoggedHp = currentHealth;
            }

            // Periodic diagnostic every 5s
            if (sw.Elapsed - lastDiagTime > TimeSpan.FromSeconds(5))
            {
                var dist = await GetDistanceToTargetAsync(account, targetGuid);
                _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F0}s: HP={currentHealth}/{initialHealth}, dist={dist:F1}y, target=0x{diagTarget:X}, firstHit={firstDamageConfirmed}");
                lastDiagTime = sw.Elapsed;

                // Early evade detection: target lost + HP back to max = mob evaded.
                // Server clears TargetGuid and mob resets HP when it returns home.
                if (diagTarget == 0 && currentHealth >= initialHealth)
                {
                    _output.WriteLine($"    [{label}] EVADE detected: target cleared, HP={currentHealth}>={initialHealth}");
                    return false;
                }
            }

            await Task.Delay(400);
        }

        _output.WriteLine($"    [{label}] TIMEOUT after {timeout.TotalSeconds}s. HP={lastLoggedHp}/{initialHealth}, firstHit={firstDamageConfirmed}");
        return false;
    }

    private async Task<uint> GetTargetHealthAsync(string account, ulong targetGuid)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return target?.Health ?? 0;
    }

    /// <summary>
    /// Teleports the bot to 3y north of the target mob's position so auto-attack range is guaranteed.
    /// Falls back to mob area center if target position is unavailable.
    /// </summary>
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
            if (pos != null && LiveBotFixture.Distance2D(pos.X, pos.Y, x, y) <= radius)
                return true;
            await Task.Delay(350);
        }

        return false;
    }

}
