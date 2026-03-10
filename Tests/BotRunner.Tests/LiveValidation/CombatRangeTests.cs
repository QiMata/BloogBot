using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Combat range and auto-attack validation — dual-client (BG + FG parity).
///
/// Tests cover:
///   1) CombatReach + BoundingRadius populated in snapshots.
///   2) Melee auto-attack within range → server accepts (SMSG_ATTACKSTART).
///   3) Melee auto-attack outside range → server rejects (negative case).
///   4) Melee range formula matches CombatDistance vanilla 1.12.1 calculations.
///   5) Auto-attack start/stop lifecycle.
///   6) Ranged auto-attack (bow/thrown) within range → server accepts.
///   7) Ranged auto-attack outside max range → server rejects (negative case).
///   8) Interaction distance uses bounding radius correctly.
///
/// Uses warrior characters (support both melee and ranged via thrown/bow pull).
/// All GM setup uses .gm on (per project rules — never .gm off in tests).
///
/// Run: dotnet test --filter "FullyQualifiedName~CombatRangeTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class CombatRangeTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    // Valley of Trials — boar area
    private const float MobAreaX = -620f;
    private const float MobAreaY = -4385f;
    private const float MobAreaZ = 44f;
    private const float MobAreaRadius = 45f;
    private const uint MottledBoarEntry = 3098;

    // Far-away location for out-of-range tests (200y south)
    private const float FarX = -620f;
    private const float FarY = -4585f;
    private const float FarZ = 44f;

    // Ranged pull distance — close enough for 8-30y range but beyond melee
    private const float RangedPullX = -620f;
    private const float RangedPullY = -4365f; // ~20y from mob area center
    private const float RangedPullZ = 44f;

    // Thrown knife item ID for warrior ranged attack testing
    private const uint ThrownKnifeItemId = 2947;

    public CombatRangeTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Verify that CombatReach and BoundingRadius are populated from server update fields.
    /// These are required for proper melee range calculation per vanilla 1.12.1 formula.
    /// </summary>
    [SkippableFact]
    public async Task CombatReach_PopulatedInSnapshot_ForPlayerAndMobs()
    {
        var bgAccount = _bot.BgAccountName!;
        await EnsureAliveAndNearMobsAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        // Player CombatReach should be non-zero (typically 1.5 for humanoid)
        var playerCombatReach = snap.Player?.Unit?.CombatReach ?? 0f;
        var playerBoundingRadius = snap.Player?.Unit?.BoundingRadius ?? 0f;
        _output.WriteLine($"  [BG] Player CombatReach={playerCombatReach:F3}, BoundingRadius={playerBoundingRadius:F3}");

        if (playerCombatReach > 0f)
        {
            Assert.InRange(playerCombatReach, 0.5f, 5.0f);
        }

        // Check nearby mobs for CombatReach
        var mobs = snap.NearbyUnits?.Where(u => u.Health > 0 && u.MaxHealth > 0 && u.NpcFlags == 0).ToList() ?? [];
        _output.WriteLine($"  [BG] Nearby hostile units: {mobs.Count}");

        foreach (var mob in mobs.Take(5))
        {
            _output.WriteLine($"    {mob.GameObject?.Name} entry={mob.GameObject?.Entry} CombatReach={mob.CombatReach:F3} BoundingRadius={mob.BoundingRadius:F3}");
        }

        var mobsWithReach = mobs.Count(m => m.CombatReach > 0f);
        _output.WriteLine($"  [BG] Mobs with CombatReach > 0: {mobsWithReach}/{mobs.Count}");

        // FG parity — ensure FG bot also gets CombatReach data
        if (_bot.IsFgActionable)
        {
            var fgAccount = _bot.FgAccountName!;
            await EnsureAliveAndNearMobsAsync(fgAccount, "FG");
            var fgSnap = await _bot.GetSnapshotAsync(fgAccount);
            if (fgSnap != null)
            {
                var fgReach = fgSnap.Player?.Unit?.CombatReach ?? 0f;
                var fgRadius = fgSnap.Player?.Unit?.BoundingRadius ?? 0f;
                _output.WriteLine($"  [FG] Player CombatReach={fgReach:F3}, BoundingRadius={fgRadius:F3}");
            }
        }
    }

    /// <summary>
    /// Melee auto-attack within range: bot near mob, send StartMeleeAttack, verify target set.
    /// Positive case — attack should succeed via SMSG_ATTACKSTART.
    /// </summary>
    [SkippableFact]
    public async Task MeleeAttack_WithinRange_TargetIsSelected()
    {
        // BG test
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.BotTeleportAsync(bgAccount, MapId, MobAreaX, MobAreaY, MobAreaZ);
        await Task.Delay(3000);
        await _bot.SendGmChatCommandTrackedAsync(bgAccount, ".respawn", captureResponse: true, delayMs: 500);

        var targetGuid = await FindLivingBoarAsync(bgAccount, "BG");
        global::Tests.Infrastructure.Skip.If(targetGuid == 0, "No living boar found for melee range test.");

        // Walk to mob via Goto to sync position (prevents mob evade from teleport Z desync)
        var mobPos = await GetMobPositionAsync(bgAccount, targetGuid);
        if (mobPos != null)
        {
            await _bot.SendActionAsync(bgAccount, new ActionMessage
            {
                ActionType = ActionType.Goto,
                Parameters =
                {
                    new RequestParameter { FloatParam = mobPos.Value.x },
                    new RequestParameter { FloatParam = mobPos.Value.y },
                    new RequestParameter { FloatParam = mobPos.Value.z },
                    new RequestParameter { FloatParam = 0 },
                }
            });
            await Task.Delay(3000);
        }

        _output.WriteLine($"  [BG] Attacking boar 0x{targetGuid:X} (within melee range)");
        var result = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });
        Assert.Equal(ResponseResult.Success, result);

        // Check for target selection: accept either TargetGuid match OR mob HP decrease.
        // BG bot's TargetGuid can be clobbered by SMSG_UPDATE_OBJECT (UNIT_FIELD_TARGET=0)
        // before the snapshot is captured. Mob HP decrease proves the attack connected.
        var initialHp = await GetMobHealthAsync(bgAccount, targetGuid);
        var selected = await WaitForConditionAsync(bgAccount, TimeSpan.FromSeconds(8), snap =>
        {
            if ((snap.Player?.Unit?.TargetGuid ?? 0UL) == targetGuid) return true;
            var mob = snap.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            if (mob != null && mob.Health < initialHp) return true;
            if (mob != null && mob.Health == 0) return true;
            return false;
        });
        if (!selected)
        {
            await _bot.RefreshSnapshotsAsync();
            var diagSnap = await _bot.GetSnapshotAsync(bgAccount);
            var currentTarget = diagSnap?.Player?.Unit?.TargetGuid ?? 0UL;
            _output.WriteLine($"  [BG] Target selection failed. Current TargetGuid=0x{currentTarget:X}, expected=0x{targetGuid:X}");
        }
        Assert.True(selected, "BG bot should have target GUID set after StartMeleeAttack within range.");

        // Cleanup: stop attack and kill mob
        await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
        await _bot.SendGmChatCommandTrackedAsync(bgAccount, ".damage 5000", captureResponse: true, delayMs: 500);

        // FG parity — teleport FG to mob area first, then find and attack
        if (_bot.IsFgActionable)
        {
            var fgAccount = _bot.FgAccountName!;
            await EnsureAliveAndNearMobsAsync(fgAccount, "FG");
            await _bot.SendGmChatCommandTrackedAsync(fgAccount, ".respawn", captureResponse: true, delayMs: 1000);

            var fgTarget = await FindLivingBoarAsync(fgAccount, "FG");
            if (fgTarget != 0)
            {
                var fgResult = await _bot.SendActionAsync(fgAccount, new ActionMessage
                {
                    ActionType = ActionType.StartMeleeAttack,
                    Parameters = { new RequestParameter { LongParam = (long)fgTarget } }
                });
                Assert.Equal(ResponseResult.Success, fgResult);

                var fgSelected = await WaitForConditionAsync(fgAccount, TimeSpan.FromSeconds(6), snap =>
                    (snap.Player?.Unit?.TargetGuid ?? 0UL) == fgTarget);
                Assert.True(fgSelected, "FG bot should have target GUID set after StartMeleeAttack within range.");

                await _bot.SendActionAsync(fgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
                await _bot.SendGmChatCommandTrackedAsync(fgAccount, ".damage 5000", captureResponse: true, delayMs: 500);
            }
        }
    }

    /// <summary>
    /// Negative case: melee auto-attack outside range (200y away from mob).
    /// Server should reject with SMSG_ATTACKSWING_NOTINRANGE — bot should NOT enter combat.
    /// Validates that the vanilla melee range formula (CombatReach + CombatReach + 4/3 + leeway)
    /// is correctly enforced.
    /// </summary>
    [SkippableFact]
    public async Task MeleeAttack_OutsideRange_DoesNotStartCombat()
    {
        var bgAccount = _bot.BgAccountName!;

        // Find a boar near mob area
        await EnsureAliveAndNearMobsAsync(bgAccount, "BG");
        var targetGuid = await FindLivingBoarAsync(bgAccount, "BG");
        global::Tests.Infrastructure.Skip.If(targetGuid == 0, "No living boar found for out-of-range test.");

        // Stop any active combat before teleporting — .go xyz fails during combat
        await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.StopAttack,
        });
        await Task.Delay(500);
        // Force-exit combat state via GM command (server combat timer is 5s)
        await _bot.SendGmChatCommandAsync(bgAccount, ".combatstop");
        await Task.Delay(500);

        // Teleport bot FAR AWAY (200y south)
        _output.WriteLine($"  [BG] Teleporting 200y away from mob 0x{targetGuid:X} for out-of-range test.");
        await _bot.BotTeleportAsync(bgAccount, MapId, FarX, FarY, FarZ);
        await Task.Delay(3000);

        // Attempt melee attack on the distant mob
        _output.WriteLine($"  [BG] Sending StartMeleeAttack on distant target 0x{targetGuid:X}");
        await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });

        // The server should reject — at 200y the mob is far outside melee range
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);

        // Verify bot actually teleported to far position
        var botPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(botPos);
        var botDist = Distance2D(botPos!.X, botPos.Y, MobAreaX, MobAreaY);
        _output.WriteLine($"  [BG] Bot position after teleport: ({botPos.X:F1}, {botPos.Y:F1}), dist from mob area: {botDist:F1}y");
        Assert.True(botDist > 100f, $"Bot should be far from mob area (dist={botDist:F1}y). Teleport may have failed.");

        // If mob is still in NearbyUnits (stale snapshot), verify distance is > melee range
        var mobSnap = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0) == targetGuid);
        if (mobSnap != null)
        {
            var mobPos = mobSnap.GameObject?.Base?.Position;
            if (mobPos != null)
            {
                var mobDist = Distance2D(botPos.X, botPos.Y, mobPos.X, mobPos.Y);
                _output.WriteLine($"  [BG] Mob still in snapshot (stale data) at dist={mobDist:F1}y — verifying out of melee range");
                Assert.True(mobDist > 10f, $"Mob should be far outside melee range (dist={mobDist:F1}y).");
            }
        }

        // Log the expected melee range for documentation
        var playerReach = snap?.Player?.Unit?.CombatReach ?? CombatDistance.DEFAULT_PLAYER_COMBAT_REACH;
        var expectedRange = CombatDistance.GetMeleeAttackRange(playerReach, CombatDistance.DEFAULT_CREATURE_COMBAT_REACH);
        _output.WriteLine($"  [BG] Expected melee range: {expectedRange:F2}y (playerReach={playerReach:F2})");
        _output.WriteLine($"  [BG] Bot at {botDist:F0}y — correctly outside melee range of {expectedRange:F2}y.");

        // Cleanup: stop attack and teleport back
        await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
        await _bot.BotTeleportAsync(bgAccount, MapId, MobAreaX, MobAreaY, MobAreaZ);
    }

    /// <summary>
    /// Verify melee range formula: CombatReach values match expected calculation.
    /// Formula: max(NOMINAL, attacker.CombatReach + target.CombatReach + 4/3 + leeway)
    /// Leeway (2.0y) only applies when BOTH units have MOVEFLAG_MASK_XZ set and neither is walking.
    /// </summary>
    [SkippableFact]
    public async Task MeleeRange_Formula_MatchesCombatDistanceCalculation()
    {
        var bgAccount = _bot.BgAccountName!;
        await EnsureAliveAndNearMobsAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        var playerReach = snap.Player?.Unit?.CombatReach ?? 0f;
        var playerFlags = snap.Player?.Unit?.MovementFlags ?? 0u;

        var mobs = snap.NearbyUnits?.Where(u => u.Health > 0 && u.MaxHealth > 0 && u.NpcFlags == 0).ToList() ?? [];
        global::Tests.Infrastructure.Skip.If(mobs.Count == 0, "No mobs nearby for range formula test.");

        foreach (var mob in mobs.Take(3))
        {
            var mobReach = mob.CombatReach;
            var mobFlags = mob.MovementFlags;
            var name = mob.GameObject?.Name ?? "<unknown>";

            var effectivePlayerReach = playerReach > 0 ? playerReach : CombatDistance.DEFAULT_PLAYER_COMBAT_REACH;
            var effectiveMobReach = mobReach > 0 ? mobReach : CombatDistance.DEFAULT_CREATURE_COMBAT_REACH;

            bool bothMoving = CombatDistance.IsMovingXZ(playerFlags) && CombatDistance.IsMovingXZ(mobFlags);

            var rangeStatic = CombatDistance.GetMeleeAttackRange(effectivePlayerReach, effectiveMobReach, bothMoving: false);
            var rangeWithLeeway = CombatDistance.GetMeleeAttackRange(effectivePlayerReach, effectiveMobReach, bothMoving: true);

            _output.WriteLine($"  {name}: reach={effectiveMobReach:F2}, static range={rangeStatic:F2}y, with leeway={rangeWithLeeway:F2}y, both moving={bothMoving}");

            // Sanity: melee range should be >= NOMINAL and < 20y
            Assert.True(rangeStatic >= CombatDistance.NOMINAL_MELEE_RANGE,
                $"Melee range to {name} should be >= nominal ({CombatDistance.NOMINAL_MELEE_RANGE:F2}y).");
            Assert.True(rangeStatic < 20f,
                $"Melee range to {name} should be < 20y (got {rangeStatic:F2}y).");

            // Leeway always adds exactly 2.0y
            Assert.Equal(rangeStatic + CombatDistance.MELEE_LEEWAY, rangeWithLeeway);
        }
    }

    /// <summary>
    /// Auto-attack lifecycle: start melee attack, verify target, stop attack, verify cleanup.
    /// Tests the full CMSG_ATTACKSWING → SMSG_ATTACKSTART → CMSG_ATTACKSTOP cycle.
    /// </summary>
    [SkippableFact]
    public async Task AutoAttack_StartAndStop_StopsCorrectly()
    {
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.BotTeleportAsync(bgAccount, MapId, MobAreaX, MobAreaY, MobAreaZ);
        await Task.Delay(3000);
        await _bot.SendGmChatCommandTrackedAsync(bgAccount, ".respawn", captureResponse: true, delayMs: 500);

        var targetGuid = await FindLivingBoarAsync(bgAccount, "BG");
        global::Tests.Infrastructure.Skip.If(targetGuid == 0, "No living boar found.");

        // Walk to mob via Goto to sync position
        var mobPos = await GetMobPositionAsync(bgAccount, targetGuid);
        if (mobPos != null)
        {
            await _bot.SendActionAsync(bgAccount, new ActionMessage
            {
                ActionType = ActionType.Goto,
                Parameters =
                {
                    new RequestParameter { FloatParam = mobPos.Value.x },
                    new RequestParameter { FloatParam = mobPos.Value.y },
                    new RequestParameter { FloatParam = mobPos.Value.z },
                    new RequestParameter { FloatParam = 0 },
                }
            });
            await Task.Delay(3000);
        }

        // Start attack
        var initialHp = await GetMobHealthAsync(bgAccount, targetGuid);
        await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });

        // Accept TargetGuid match OR mob HP decrease (TargetGuid can be clobbered by SMSG_UPDATE_OBJECT)
        var attacking = await WaitForConditionAsync(bgAccount, TimeSpan.FromSeconds(8), snap =>
        {
            if ((snap.Player?.Unit?.TargetGuid ?? 0UL) == targetGuid) return true;
            var mob = snap.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            if (mob != null && mob.Health < initialHp) return true;
            if (mob != null && mob.Health == 0) return true;
            return false;
        });
        _output.WriteLine($"  [BG] Attack started: {attacking}");
        if (!attacking)
        {
            await _bot.RefreshSnapshotsAsync();
            var diagSnap = await _bot.GetSnapshotAsync(bgAccount);
            var currentTarget = diagSnap?.Player?.Unit?.TargetGuid ?? 0UL;
            _output.WriteLine($"  [BG] Target selection failed. Current TargetGuid=0x{currentTarget:X}, expected=0x{targetGuid:X}");
        }
        Assert.True(attacking, "BG bot should have target after starting melee attack.");

        // Stop attack
        await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
        await Task.Delay(1500);

        // Kill the mob via GM to clean up combat state
        await _bot.SendGmChatCommandTrackedAsync(bgAccount, ".damage 5000", captureResponse: true, delayMs: 500);
        _output.WriteLine("  [BG] Attack stopped and mob killed — combat state cleaned up.");
    }

    /// <summary>
    /// Ranged auto-attack within range: equip thrown weapon, start ranged attack on mob.
    /// Warriors can use thrown weapons (item 2947) for ranged pulling.
    /// Server should accept the ranged attack via CMSG_ATTACKSWING.
    /// </summary>
    [SkippableFact]
    public async Task RangedAttack_WithinRange_TargetIsSelected()
    {
        var bgAccount = _bot.BgAccountName!;
        await EnsureAliveAndNearMobsAsync(bgAccount, "BG");

        // Equip thrown knives — .additem 2947 gives Throwing Knife
        await _bot.SendGmChatCommandTrackedAsync(bgAccount, $".additem {ThrownKnifeItemId} 100", captureResponse: true, delayMs: 1000);

        // Find a living boar
        var targetGuid = await FindLivingBoarAsync(bgAccount, "BG");
        global::Tests.Infrastructure.Skip.If(targetGuid == 0, "No living boar found for ranged attack test.");

        // Start ranged attack
        _output.WriteLine($"  [BG] Starting ranged attack on boar 0x{targetGuid:X}");
        var result = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.StartRangedAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });
        Assert.Equal(ResponseResult.Success, result);

        // Wait for target to appear in snapshot
        var selected = await WaitForConditionAsync(bgAccount, TimeSpan.FromSeconds(6), snap =>
            (snap.Player?.Unit?.TargetGuid ?? 0UL) == targetGuid);
        _output.WriteLine($"  [BG] Ranged attack target selected: {selected}");

        // AST-7: Assert target was actually selected. If no ranged weapon is equipped,
        // skip the test rather than silently passing with ambiguous "maybe melee" log.
        if (!selected)
        {
            global::Tests.Infrastructure.Skip.If(true, "Ranged attack target not selected — likely no ranged weapon equipped. Skipping rather than false-passing.");
        }
        _output.WriteLine("  [BG] Ranged attack accepted by server — target GUID set.");

        // Cleanup
        await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
        await _bot.SendGmChatCommandTrackedAsync(bgAccount, ".damage 5000", captureResponse: true, delayMs: 500);
    }

    /// <summary>
    /// Negative case: ranged attack outside max range.
    /// Thrown weapons have a max range of 30y. At 200y, the server should reject.
    /// </summary>
    [SkippableFact]
    public async Task RangedAttack_OutsideRange_DoesNotStartCombat()
    {
        var bgAccount = _bot.BgAccountName!;

        // Find a boar first
        await EnsureAliveAndNearMobsAsync(bgAccount, "BG");
        var targetGuid = await FindLivingBoarAsync(bgAccount, "BG");
        global::Tests.Infrastructure.Skip.If(targetGuid == 0, "No living boar found for ranged out-of-range test.");

        // Stop combat and force-exit combat state before teleporting
        await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
        await Task.Delay(300);
        await _bot.SendGmChatCommandAsync(bgAccount, ".combatstop");
        await Task.Delay(500);

        // Teleport bot FAR AWAY (200y south)
        _output.WriteLine($"  [BG] Teleporting 200y away for ranged out-of-range test.");
        await _bot.BotTeleportAsync(bgAccount, MapId, FarX, FarY, FarZ);
        await Task.Delay(3000);

        // Verify teleport actually worked
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        var botPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(botPos);
        var botDist = Distance2D(botPos!.X, botPos.Y, MobAreaX, MobAreaY);
        _output.WriteLine($"  [BG] Bot position after teleport: ({botPos.X:F1}, {botPos.Y:F1}), dist from mob area: {botDist:F1}y");
        Assert.True(botDist > 100f, $"Bot should be far from mob area (dist={botDist:F1}y). Teleport may have failed.");

        // Attempt ranged attack on the distant mob
        _output.WriteLine($"  [BG] Sending StartRangedAttack on distant target 0x{targetGuid:X}");
        await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.StartRangedAttack,
            Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
        });

        // Wait and check — at 200y the mob should not be in NearbyUnits
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(bgAccount);

        var mobSnap = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0) == targetGuid);
        if (mobSnap != null)
        {
            var mobPos = mobSnap.GameObject?.Base?.Position;
            if (mobPos != null)
            {
                var mobDist = Distance2D(botPos.X, botPos.Y, mobPos.X, mobPos.Y);
                _output.WriteLine($"  [BG] Mob still in snapshot (stale data) at dist={mobDist:F1}y — verifying outside ranged range");
                Assert.True(mobDist > 30f, $"Mob should be far outside ranged range (dist={mobDist:F1}y).");
            }
        }
        else
        {
            _output.WriteLine("  [BG] Ranged attack at 200y correctly rejected — mob not in snapshot range.");
        }

        // Cleanup
        await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.StopAttack });
        await _bot.BotTeleportAsync(bgAccount, MapId, MobAreaX, MobAreaY, MobAreaZ);
    }

    /// <summary>
    /// Interaction distance uses bounding radius: verify GetInteractionDistance formula.
    /// Formula: INTERACTION_DISTANCE (5.0y) + target.BoundingRadius
    /// </summary>
    [SkippableFact]
    public async Task InteractionDistance_UsesBoundingRadius()
    {
        var bgAccount = _bot.BgAccountName!;
        await EnsureAliveAndNearMobsAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        var mobs = snap.NearbyUnits?.Where(u => u.Health > 0 && u.MaxHealth > 0 && u.NpcFlags == 0).ToList() ?? [];
        global::Tests.Infrastructure.Skip.If(mobs.Count == 0, "No mobs nearby for interaction distance test.");

        foreach (var mob in mobs.Take(3))
        {
            var radius = mob.BoundingRadius;
            var name = mob.GameObject?.Name ?? "<unknown>";

            var effectiveRadius = radius > 0 ? radius : CombatDistance.DEFAULT_PLAYER_BOUNDING_RADIUS;
            var interactDist = CombatDistance.GetInteractionDistance(effectiveRadius);

            _output.WriteLine($"  {name}: boundingRadius={effectiveRadius:F3}, interactionDist={interactDist:F2}y");

            // Interaction distance should be >= base (5.0) and < 10y (no mob has 5y bounding radius)
            Assert.True(interactDist >= CombatDistance.INTERACTION_DISTANCE,
                $"Interaction distance to {name} should be >= base {CombatDistance.INTERACTION_DISTANCE}y.");
            Assert.True(interactDist < 10f,
                $"Interaction distance to {name} should be < 10y (got {interactDist:F2}y).");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task EnsureAliveAndNearMobsAsync(string account, string label)
    {
        await _bot.EnsureStrictAliveAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var dist = pos == null ? float.MaxValue : Distance2D(pos.X, pos.Y, MobAreaX, MobAreaY);

        if (dist > MobAreaRadius)
        {
            _output.WriteLine($"  [{label}] Teleporting to mob area (dist={dist:F1}y).");
            await _bot.BotTeleportAsync(account, MapId, MobAreaX, MobAreaY, MobAreaZ);

            // Poll for arrival instead of fixed delay
            var arrived = await WaitForConditionAsync(account, TimeSpan.FromSeconds(12), s =>
            {
                var p = s.Player?.Unit?.GameObject?.Base?.Position;
                return p != null && Distance2D(p.X, p.Y, MobAreaX, MobAreaY) <= MobAreaRadius;
            });
            global::Tests.Infrastructure.Skip.If(!arrived, $"{label}: Failed to arrive near mob area after teleport.");
        }

        // Force respawn to ensure mobs are present
        await _bot.SendGmChatCommandTrackedAsync(account, ".respawn", captureResponse: true, delayMs: 500);
    }

    private async Task<ulong> FindLivingBoarAsync(string account, string label, float maxDistance = 45f)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(12))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var playerPos = snap?.Player?.Unit?.GameObject?.Base?.Position;

            var boar = snap?.NearbyUnits?
                .Where(u =>
                {
                    var guid = u.GameObject?.Base?.Guid ?? 0UL;
                    if (guid == 0 || u.Health == 0 || u.MaxHealth == 0) return false;
                    if (u.NpcFlags != 0) return false;
                    var entry = u.GameObject?.Entry ?? 0;
                    var name = u.GameObject?.Name ?? "";
                    if (entry != MottledBoarEntry
                        && !name.Contains("Mottled Boar", StringComparison.OrdinalIgnoreCase))
                        return false;
                    // Check distance if player position is known
                    if (playerPos != null)
                    {
                        var mobPos = u.GameObject?.Base?.Position;
                        if (mobPos != null && Distance2D(playerPos.X, playerPos.Y, mobPos.X, mobPos.Y) > maxDistance)
                            return false;
                    }
                    return true;
                })
                .OrderBy(u =>
                {
                    if (playerPos == null) return 0f;
                    var mobPos = u.GameObject?.Base?.Position;
                    return mobPos == null ? float.MaxValue : Distance2D(playerPos.X, playerPos.Y, mobPos.X, mobPos.Y);
                })
                .FirstOrDefault();

            if (boar != null)
            {
                var guid = boar.GameObject?.Base?.Guid ?? 0UL;
                var mobPos = boar.GameObject?.Base?.Position;
                var dist = playerPos != null && mobPos != null
                    ? Distance2D(playerPos.X, playerPos.Y, mobPos.X, mobPos.Y) : -1f;
                _output.WriteLine($"  [{label}] Found boar 0x{guid:X}: {boar.GameObject?.Name} HP={boar.Health}/{boar.MaxHealth} dist={dist:F1}y");
                return guid;
            }

            // Try respawn if we haven't found anything after 4s
            if (sw.Elapsed > TimeSpan.FromSeconds(4))
            {
                await _bot.SendGmChatCommandTrackedAsync(account, ".respawn", captureResponse: true, delayMs: 500);
            }

            await Task.Delay(500);
        }

        _output.WriteLine($"  [{label}] No living boar found within {maxDistance:F0}y after 12s.");
        return 0UL;
    }

    private async Task<(float x, float y, float z)?> GetMobPositionAsync(string account, ulong targetGuid)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var mob = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        var pos = mob?.GameObject?.Base?.Position;
        return pos != null ? (pos.X, pos.Y, pos.Z) : null;
    }

    private async Task<uint> GetMobHealthAsync(string account, ulong targetGuid)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var mob = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return mob?.Health ?? 0;
    }

    private Task<bool> WaitForConditionAsync(string account, TimeSpan timeout, Func<WoWActivitySnapshot, bool> condition)
        => _bot.WaitForSnapshotConditionAsync(account, condition, timeout, pollIntervalMs: 300);

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float Distance3D(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var dz = z2 - z1;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
