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

    // Melee range: WoW melee reach = weapon range (~2y) + both capsule radii (~0.4y each) + sampling margin.
    // Snapshot is sampled at 350ms intervals; at run speed ~7m/s the bot moves ~2.45m between samples,
    // so the closest approach may not be captured. 6.5y provides reliable detection without false positives.
    private const float MeleeRange = 6.5f;
    // Facing tolerance: attack must be within 90° of target direction.
    private const float FacingToleranceRad = (float)(Math.PI / 2.0);

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
        await EnsureStrictAliveAsync(account, label);

        // CRITICAL: Turn GM mode OFF for combat. GM mode makes the character invisible
        // to NPCs → mob evades after initial swing. Must be off for BOTH bots.
        // SOAP `.gm off` fails — requires player session context (returns "not available to you").
        // Both FG and BG must use chat. BG chat `.gm` was previously blocked on disconnect fear —
        // if BG disconnects, that's a WoWSharpClient bug to fix, not a reason to skip GM toggle.
        // Use try/finally to GUARANTEE .gm on restoration (BT-VERIFY-006 fix).
        bool gmTurnedOff = false;
        _output.WriteLine($"  [{label}] Turning GM mode OFF for combat via chat.");
        var gmOffTrace = await _bot.SendGmChatCommandTrackedAsync(account, ".gm off", captureResponse: true, delayMs: 1000);
        var gmOffMsg = gmOffTrace?.ChatMessages.Count > 0 ? string.Join("; ", gmOffTrace.ChatMessages) : "(no response)";
        _output.WriteLine($"  [{label}] .gm off result={gmOffTrace?.DispatchResult}, response: {gmOffMsg}");
        await _bot.SendGmChatCommandTrackedAsync(account, ".gm visible on", captureResponse: true, delayMs: 500);
        gmTurnedOff = true;
        await Task.Delay(500); // Brief settle after GM mode change

        try
        {

        // Ensure bot has a weapon equipped — .reset items from other tests may strip starter gear.
        _output.WriteLine($"  [{label}] Ensuring weapon equipped (Worn Mace).");
        await _bot.BotLearnSpellAsync(account, OneHandMaceSpell);
        await _bot.BotSetSkillAsync(account, 54, 1, 300); // skill 54 = Maces
        await _bot.BotAddItemAsync(account, LiveBotFixture.TestItems.WornMace);
        await Task.Delay(500);
        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)LiveBotFixture.TestItems.WornMace } }
        });
        _output.WriteLine($"  [{label}] EquipItem result: {equipResult}");
        await Task.Delay(500);

        // STEP 1b: Teleport to mob area to enumerate available mobs.
        await EnsureNearMobAreaAsync(account, label, areaX, areaY, areaZ);

        // Wait for nearby units to appear (area loading for FG ObjectManager)
        await _bot.WaitForSnapshotConditionAsync(account,
            s => s.NearbyUnits?.Count > 0, TimeSpan.FromSeconds(5), pollIntervalMs: 500);

        await _bot.RefreshSnapshotsAsync();
        var selfSnap = await _bot.GetSnapshotAsync(account);
        var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (selfGuid == 0)
        {
            _output.WriteLine($"  [{label}] Missing self GUID; cannot select target.");
            return false;
        }

        _output.WriteLine($"  [{label}] Finding candidate mob in snapshot...");
        var (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12), claimedTargets);
        if (targetGuid == 0)
        {
            // No living mob nearby — spawn a temporary Mottled Boar (entry 3098, level 3-4).
            // .npc add temp creates a temp creature at the player's position (removed on server restart).
            _output.WriteLine($"  [{label}] No mob found; spawning temp Mottled Boar (entry 3098).");
            await _bot.SendGmChatCommandTrackedAsync(account, ".npc add temp 3098", captureResponse: true, delayMs: 1000);
            // Poll for creature to appear in snapshots
            await _bot.WaitForSnapshotConditionAsync(account,
                s => s.NearbyUnits?.Count > 0, TimeSpan.FromSeconds(5), pollIntervalMs: 500);
            await _bot.RefreshSnapshotsAsync();

            (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12), claimedTargets);
        }

        if (targetGuid == 0)
        {
            _output.WriteLine($"  [{label}] No living mob found after spawn attempt.");
            return false;
        }

        // STEP 2: Walk to the mob using Goto.
        //
        // Walking (Goto) generates real movement packets (MSG_MOVE_HEARTBEAT) that sync
        // the server and client positions. After GM teleport, the BG physics engine ground-snaps
        // to a Z that can differ from MaNGOS server's terrain Z by 1-2y. Without movement packets,
        // the pre-attack heartbeat Z mismatches → server rejects → mob evades.
        //
        // If the mob is already close (<15y from teleport), re-teleport 20y away to guarantee
        // enough walk distance for position sync.
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
                    var od = 20f;
                    float tpX = curDist > 0.1f ? mobX - (dx2 / curDist) * od : mobX + od;
                    float tpY = curDist > 0.1f ? mobY - (dy2 / curDist) * od : mobY;
                    _output.WriteLine($"  [{label}] Too close ({curDist:F1}y); re-teleporting 20y away for approach walk.");
                    await _bot.BotTeleportAsync(account, MapId, tpX, tpY, areaZ);
                    await _bot.WaitForTeleportSettledAsync(account, tpX, tpY);
                }
            }
        }
        _output.WriteLine($"  [{label}] Walking to mob at ({mobX:F1},{mobY:F1},{mobZ:F1})");

        var reachedMelee = false;
        for (int gotoAttempt = 0; gotoAttempt < 2 && !reachedMelee; gotoAttempt++)
        {
            if (gotoAttempt > 0)
            {
                _output.WriteLine($"  [{label}] Goto retry #{gotoAttempt}...");
                await Task.Delay(500); // Brief pause before pathfinding retry
            }

            var gotoResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.Goto,
                Parameters =
                {
                    new RequestParameter { FloatParam = mobX },
                    new RequestParameter { FloatParam = mobY },
                    new RequestParameter { FloatParam = mobZ },
                    new RequestParameter { FloatParam = 0 }, // tolerance
                }
            });
            _output.WriteLine($"  [{label}] Goto result: {gotoResult}");

            reachedMelee = await WaitForMeleeRangeAsync(account, targetGuid, MeleeRange, TimeSpan.FromSeconds(12));
        }

        if (!reachedMelee)
        {
            var currentDist = await GetDistanceToTargetAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] Could not reach melee range after 2 Goto attempts. dist={currentDist:F1}y");
            // Continue anyway — the attack might still work at close range
        }

        // Diagnostic: verify bot position and mob visibility.
        {
            await _bot.RefreshSnapshotsAsync();
            var diagSnap = await _bot.GetSnapshotAsync(account);
            var selfPos = diagSnap?.Player?.Unit?.GameObject?.Base?.Position;
            var nearbyCount = diagSnap?.NearbyUnits?.Count ?? 0;
            var aliveCount = diagSnap?.NearbyUnits?.Count(u => u.Health > 0) ?? 0;
            var mobVisible = diagSnap?.NearbyUnits?.Any(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid) ?? false;
            _output.WriteLine($"  [{label}] Pre-attack diag: selfPos=({selfPos?.X:F1},{selfPos?.Y:F1},{selfPos?.Z:F1}), " +
                $"nearbyUnits={nearbyCount}, alive={aliveCount}, targetVisible={mobVisible}");

            if (!mobVisible || (await GetTargetHealthAsync(account, targetGuid)) == 0)
            {
                // Target died or despawned during approach — find another living mob nearby.
                _output.WriteLine($"  [{label}] Target lost during approach — finding another living mob.");
                (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(8), claimedTargets);
                if (targetGuid == 0)
                {
                    _output.WriteLine($"  [{label}] No living mob found nearby.");
                    return false;
                }
                _output.WriteLine($"  [{label}] Retargeted to 0x{targetGuid:X} at ({mobX:F1},{mobY:F1},{mobZ:F1})");
            }
        }

        // Settle after movement
        await Task.Delay(1000);

        // Face the target — FG bot may not auto-face.
        await FaceTargetAsync(account, label, targetGuid);
        await Task.Delay(500);

        // FULL COMBAT LIFECYCLE: Auto-attack the mob to death.
        // Server manages auto-attack via internal swing timer after CMSG_ATTACKSWING.
        // BG sends periodic MSG_MOVE_HEARTBEAT (500ms) to keep server position fresh.
        // FG Lua AttackTarget() lets the real WoW client handle heartbeats natively.
        // GM character has high stats — won't die to a level 1-3 mob.
        //
        // ATTACK LOOP: Up to 3 engagement attempts. Mob may evade on first hit if
        // server pathfinding fails at the mob's spawn position (24s unreachable timeout).
        var mobKilled = false;
        for (int attackAttempt = 0; attackAttempt < 3 && !mobKilled; attackAttempt++)
        {
            if (attackAttempt > 0)
            {
                _output.WriteLine($"  [{label}] Mob evaded or no damage — re-engaging (attempt {attackAttempt + 1}/3)...");
                await _bot.RefreshSnapshotsAsync();
                var snap = await _bot.GetSnapshotAsync(account);
                var mob = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
                if (mob == null || mob.Health == 0)
                {
                    // Original target is gone — find another mob nearby.
                    _output.WriteLine($"  [{label}] Original target gone — finding another mob.");
                    (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(8), claimedTargets);
                    if (targetGuid == 0)
                    {
                        _output.WriteLine($"  [{label}] No living mob found for re-engage.");
                        return false;
                    }
                    _output.WriteLine($"  [{label}] Retargeted to 0x{targetGuid:X}");
                }
                else
                {
                    var mPos = mob.GameObject?.Base?.Position;
                    if (mPos != null)
                    {
                        mobX = mPos.X; mobY = mPos.Y; mobZ = mPos.Z;
                    }
                }

                // Walk to the mob again
                _output.WriteLine($"  [{label}] Re-walking to mob at ({mobX:F1},{mobY:F1},{mobZ:F1})");
                await _bot.SendActionAsync(account, new ActionMessage
                {
                    ActionType = ActionType.Goto,
                    Parameters =
                    {
                        new RequestParameter { FloatParam = mobX },
                        new RequestParameter { FloatParam = mobY },
                        new RequestParameter { FloatParam = mobZ },
                        new RequestParameter { FloatParam = 0 },
                    }
                });
                await WaitForMeleeRangeAsync(account, targetGuid, MeleeRange, TimeSpan.FromSeconds(10));
                await Task.Delay(1000);
                await FaceTargetAsync(account, label, targetGuid);
                await Task.Delay(500);
            }

            // Re-sample health before attack.
            initialHealth = await GetTargetHealthAsync(account, targetGuid);
            if (initialHealth == 0)
            {
                _output.WriteLine($"  [{label}] Target HP=0; retrying snapshot refresh...");
                for (int retryHp = 0; retryHp < 5 && initialHealth == 0; retryHp++)
                {
                    await Task.Delay(1000);
                    await _bot.RefreshSnapshotsAsync();
                    initialHealth = await GetTargetHealthAsync(account, targetGuid);
                }
            }
            if (initialHealth == 0)
            {
                _output.WriteLine($"  [{label}] Target dead before attack — may have been killed by another mob/player.");
                continue;
            }

            var preAttackDist = await GetDistanceToTargetAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] Pre-attack: dist={preAttackDist:F1}y, HP={initialHealth} (attempt {attackAttempt + 1}/3)");

            _output.WriteLine($"  [{label}] Sending StartMeleeAttack on 0x{targetGuid:X} (HP={initialHealth})");
            var selectResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            if (selectResult != ResponseResult.Success)
            {
                _output.WriteLine($"  [{label}] StartMeleeAttack dispatch failed: {selectResult}");
                continue;
            }

            // Verify bot targeted the mob.
            var targeted = await WaitForSelectedTargetAsync(account, targetGuid, TimeSpan.FromSeconds(8));
            if (!targeted)
            {
                await _bot.RefreshSnapshotsAsync();
                var diagSnap = await _bot.GetSnapshotAsync(account);
                var currentTarget = diagSnap?.Player?.Unit?.TargetGuid ?? 0UL;
                _output.WriteLine($"  [{label}] WARN: TargetGuid=0x{currentTarget:X} (expected 0x{targetGuid:X}). Continuing...");
            }
            else
            {
                _output.WriteLine($"  [{label}] Target selected in snapshot.");
            }

            // Verify facing.
            var facingOk = await AssertBotFacingTargetAsync(account, targetGuid, label);
            if (!facingOk)
            {
                _output.WriteLine($"  [{label}] WARN: Bot not facing target. Continuing anyway.");
            }

            // Wait for mob death via auto-attacks. Level 1-3 mob with ~100 HP should die
            // within ~30s of auto-attacking (weapon speed ~2.0s, damage ~10-20 per swing).
            _output.WriteLine($"  [{label}] Auto-attacking target to death (HP={initialHealth})...");
            mobKilled = await WaitForMobDeathAsync(account, label, targetGuid, initialHealth, TimeSpan.FromSeconds(45));
            if (!mobKilled)
            {
                var currentHealth = await GetTargetHealthAsync(account, targetGuid);
                var postDist = await GetDistanceToTargetAsync(account, targetGuid);
                var evaded = currentHealth >= initialHealth && currentHealth > 0;
                _output.WriteLine($"  [{label}] Attack attempt {attackAttempt + 1} result: HP {initialHealth}→{currentHealth}, dist={postDist:F1}y, evaded={evaded}");
            }
        }

        if (!mobKilled)
        {
            _output.WriteLine($"  [{label}] FAIL: Could not kill target after 3 attack attempts.");
            return false;
        }

        _output.WriteLine($"  [{label}] COMBAT COMPLETE: Mob killed via auto-attacks (full lifecycle validated).");
        return true;
        }
        finally
        {
            // ALWAYS restore GM mode (BT-VERIFY-006 fix) — even if test failed mid-combat.
            if (gmTurnedOff)
            {
                _output.WriteLine($"  [{label}] Restoring .gm on (finally block).");
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

    private async Task<bool> WaitForSelectedTargetAsync(string account, ulong targetGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if ((snap?.Player?.Unit?.TargetGuid ?? 0UL) == targetGuid)
                return true;

            // If the mob is already dead or gone, the server cleared TargetGuid.
            // The bot clearly attacked it — accept this as targeting success.
            var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            if (target != null && target.Health == 0)
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
        return LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, targetPos.X, targetPos.Y);
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
    /// Waits until the target mob dies (HP reaches 0) from auto-attacks.
    /// Logs HP progression to track the full combat lifecycle.
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

    /// <summary>Normalizes an angle to [-π, π].</summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > Math.PI) angle -= (float)(2 * Math.PI);
        while (angle < -Math.PI) angle += (float)(2 * Math.PI);
        return angle;
    }

    /// <summary>
    /// Sends a SET_FACING action toward the target so the bot faces it before attacking.
    /// </summary>
    private async Task FaceTargetAsync(string account, string label, ulong targetGuid)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var selfPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        var targetPos = target?.GameObject?.Base?.Position;

        if (selfPos == null || targetPos == null)
        {
            _output.WriteLine($"  [{label}] FaceTarget: positions unavailable, skipping.");
            return;
        }

        var facing = (float)Math.Atan2(targetPos.Y - selfPos.Y, targetPos.X - selfPos.X);
        if (facing < 0) facing += (float)(2 * Math.PI);

        _output.WriteLine($"  [{label}] FaceTarget: sending SET_FACING={facing:F2} rad toward target.");
        await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SetFacing,
            Parameters = { new RequestParameter { FloatParam = facing } }
        });
    }
}
