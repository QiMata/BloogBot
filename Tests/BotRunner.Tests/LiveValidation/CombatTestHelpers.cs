using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shared constants and helpers for combat tests (BG and FG variants).
/// </summary>
internal static class CombatTestHelpers
{
    public const int MapId = 1;
    public const float MobAreaX = -284f;
    public const float MobAreaY = -4383f;
    public const float MobAreaZ = 60f; // Z+3 offset from spawn table (~57) to avoid UNDERMAP detection
    public const float MobAreaRadius = 80f;

    // Observer offset: 15y behind the mob area so FG camera sees the fight
    public const float ObserverX = MobAreaX + 15f;
    public const float ObserverY = MobAreaY;
    public const float ObserverZ = MobAreaZ;

    public static readonly HashSet<uint> AttackableCreatureEntries = [3098, 3101, 3124];
    public const uint OneHandMaceSpell = 198;
    public const int MaxCombatAttempts = 3;

    public static int GetCombatTargetPriority(uint entry)
        => entry switch
        {
            3098 => 0, // Mottled Boar
            3101 => 1, // Vile Familiar
            3124 => 2, // Scorpid Worker
            _ => 99
        };

    public static async Task<bool> RunCombatScenarioAsync(
        LiveBotFixture bot, ITestOutputHelper output,
        string combatAccount, string? observerAccount)
    {
        // --- Phase 1: Prep in Orgrimmar safe zone ---
        output.WriteLine("\n--- Phase 1: Prep (Orgrimmar safe zone) ---");
        await bot.EnsureStrictAliveAsync(combatAccount, "COMBAT");

        await bot.BotTeleportAsync(combatAccount, LiveBotFixture.SafeZoneMap,
            LiveBotFixture.SafeZoneX, LiveBotFixture.SafeZoneY, LiveBotFixture.SafeZoneZ);
        await Task.Delay(1500);

        output.WriteLine("  Equipping weapon (Worn Mace) and learning 1H Mace skill...");
        await bot.BotLearnSpellAsync(combatAccount, OneHandMaceSpell);
        await bot.BotSetSkillAsync(combatAccount, 54, 1, 300);
        await bot.BotAddItemAsync(combatAccount, LiveBotFixture.TestItems.WornMace);
        await Task.Delay(500);
        await bot.SendActionAsync(combatAccount, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)LiveBotFixture.TestItems.WornMace } }
        });
        await Task.Delay(500);

        // --- Phase 2: Teleport to mob area ---
        output.WriteLine("\n--- Phase 2: Teleport to Valley of Trials mob area ---");
        await bot.BotTeleportAsync(combatAccount, MapId, MobAreaX, MobAreaY, MobAreaZ);
        var arrived = await WaitForNearPositionAsync(bot, combatAccount, MobAreaX, MobAreaY, MobAreaRadius, TimeSpan.FromSeconds(12));
        if (!arrived) return false;

        // Force nearby creatures to respawn
        await bot.SendGmChatCommandAsync(combatAccount, ".respawn");
        await Task.Delay(2000);

        // --- Phase 3: Position FG observer (BG test only) ---
        if (observerAccount != null)
            await PositionFgObserverAsync(bot, output, combatAccount, observerAccount);

        // --- Phase 4: Verify no GM flag on COMBATTEST ---
        await bot.RefreshSnapshotsAsync();
        var selfSnap = await bot.GetSnapshotAsync(combatAccount);
        var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (selfGuid == 0) return false;

        var playerFlags = selfSnap?.Player?.PlayerFlags ?? 0;
        var isGmFlagSet = (playerFlags & 0x08) != 0;
        var factionTemplate = selfSnap?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
        output.WriteLine(
            $"  GM CHECK: playerFlags=0x{playerFlags:X8}, GM={(isGmFlagSet ? "SET (BAD!)" : "CLEAR (OK)")}, factionTemplate={factionTemplate}");
        if (isGmFlagSet) return false;

        // Wait for nearby units to stream in
        await bot.WaitForSnapshotConditionAsync(
            combatAccount,
            s => s.NearbyUnits?.Count > 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 500);

        // --- Phase 5: Combat loop ---
        output.WriteLine("\n--- Phase 5: Combat ---");
        for (int attempt = 1; attempt <= MaxCombatAttempts; attempt++)
        {
            output.WriteLine($"  Finding candidate mob (attempt {attempt}/{MaxCombatAttempts})...");
            var (targetGuid, initialHealth, mobX, mobY, mobZ) =
                await FindLivingMobAsync(bot, output, combatAccount, selfGuid, TimeSpan.FromSeconds(20));
            if (targetGuid == 0) return false;

            output.WriteLine($"  Target: 0x{targetGuid:X} HP={initialHealth} at ({mobX:F1},{mobY:F1},{mobZ:F1})");

            var distanceToMob = await GetDistanceToTargetAsync(bot, combatAccount, targetGuid);
            output.WriteLine($"  Starting combat from {distanceToMob:F1}y away.");

            initialHealth = await GetTargetHealthAsync(bot, combatAccount, targetGuid);
            if (initialHealth == 0)
            {
                output.WriteLine($"  Target 0x{targetGuid:X} died during setup — retrying...");
                continue;
            }

            // Re-face observer toward the mob
            if (observerAccount != null)
                await FaceBotTowardAsync(bot, observerAccount, mobX, mobY);

            output.WriteLine($"  Sending StartMeleeAttack on 0x{targetGuid:X} (HP={initialHealth})");
            var attackResult = await bot.SendActionAsync(combatAccount, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            if (attackResult != ResponseResult.Success)
            {
                output.WriteLine($"  StartMeleeAttack dispatch failed: {attackResult}");
                return false;
            }

            var killed = await WaitForMobDeathAsync(bot, output, combatAccount, targetGuid, initialHealth, TimeSpan.FromSeconds(90));
            if (killed)
            {
                output.WriteLine("  COMBAT COMPLETE: Mob killed via auto-attacks.");
                return true;
            }

            var currentHealth = await GetTargetHealthAsync(bot, combatAccount, targetGuid);
            var postDist = await GetDistanceToTargetAsync(bot, combatAccount, targetGuid);
            output.WriteLine($"  Attempt {attempt}: HP {initialHealth}->{currentHealth}, dist={postDist:F1}y — retrying.");
        }

        output.WriteLine($"  FAIL: All {MaxCombatAttempts} combat attempts failed.");
        return false;
    }

    // ---- FG Observer Helpers ----

    private static async Task PositionFgObserverAsync(
        LiveBotFixture bot, ITestOutputHelper output,
        string combatAccount, string observerAccount)
    {
        output.WriteLine("\n--- Phase 3: Position FG observer ---");

        // Ensure GM mode is on for observer (so mobs ignore it)
        await bot.SendGmChatCommandAsync(observerAccount, ".gm on");
        await Task.Delay(500);

        // Teleport observer to offset position
        await bot.BotTeleportAsync(observerAccount, MapId, ObserverX, ObserverY, ObserverZ);
        await Task.Delay(1500);

        // Face toward the combat bot
        await FaceBotTowardTargetBotAsync(bot, observerAccount, combatAccount);

        output.WriteLine($"  [FG-OBSERVER] {observerAccount} at ({ObserverX:F1},{ObserverY:F1},{ObserverZ:F1}), GM on, facing combat bot.");
    }

    private static async Task FaceBotTowardTargetBotAsync(
        LiveBotFixture bot, string facingAccount, string targetAccount)
    {
        await bot.RefreshSnapshotsAsync();
        var targetSnap = await bot.GetSnapshotAsync(targetAccount);
        var targetPos = targetSnap?.Player?.Unit?.GameObject?.Base?.Position;
        if (targetPos != null)
            await FaceBotTowardAsync(bot, facingAccount, targetPos.X, targetPos.Y);
    }

    private static async Task FaceBotTowardAsync(
        LiveBotFixture bot, string account, float targetX, float targetY)
    {
        var snap = await bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null) return;

        var facing = (float)Math.Atan2(targetY - pos.Y, targetX - pos.X);
        if (facing < 0) facing += (float)(2 * Math.PI);

        await bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SetFacing,
            Parameters = { new RequestParameter { FloatParam = facing } }
        });
    }

    // ---- Mob Finding & Combat Helpers ----

    private static async Task<(ulong guid, uint health, float mobX, float mobY, float mobZ)> FindLivingMobAsync(
        LiveBotFixture bot, ITestOutputHelper output,
        string account, ulong selfGuid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await bot.RefreshSnapshotsAsync();
            var snap = await bot.GetSnapshotAsync(account);
            var selfPos = snap?.Player?.Unit?.GameObject?.Base?.Position;

            var candidates = snap?.NearbyUnits?
                .Where(u =>
                {
                    var guid = u.GameObject?.Base?.Guid ?? 0UL;
                    if (guid == 0 || guid == selfGuid) return false;
                    if (u.Health == 0 || u.MaxHealth == 0) return false;
                    if (u.GameObject?.Level > 10) return false;
                    if (u.MaxHealth > 500) return false;
                    if (u.NpcFlags != 0) return false;
                    var entry = u.GameObject?.Entry ?? 0;
                    return AttackableCreatureEntries.Contains(entry);
                })
                .OrderBy(u => GetCombatTargetPriority(u.GameObject?.Entry ?? 0))
                .ThenBy(u =>
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
                var mobPos = mob.GameObject?.Base?.Position;
                var name = string.IsNullOrWhiteSpace(mob.GameObject?.Name) ? "<unknown>" : mob.GameObject.Name;
                var entry = mob.GameObject?.Entry ?? 0;
                var dist = selfPos != null && mobPos != null
                    ? LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, mobPos.X, mobPos.Y)
                    : -1f;
                output.WriteLine(
                    $"    Candidate: 0x{guid:X} '{name}' entry={entry} HP={mob.Health}/{mob.MaxHealth} dist={dist:F1}y at ({mobPos?.X:F1},{mobPos?.Y:F1},{mobPos?.Z:F1})");
                return (guid, mob.Health, mobPos?.X ?? 0f, mobPos?.Y ?? 0f, mobPos?.Z ?? 0f);
            }

            if (sw.Elapsed.TotalSeconds < 4)
            {
                var allUnits = snap?.NearbyUnits ?? [];
                output.WriteLine($"    [FindMob] {allUnits.Count} nearby units, 0 candidates. Self=0x{selfGuid:X}");
                foreach (var u in allUnits.Take(15))
                {
                    var g = u.GameObject?.Base?.Guid ?? 0UL;
                    var p = u.GameObject?.Base?.Position;
                    var n = u.GameObject?.Name ?? "?";
                    output.WriteLine(
                        $"      0x{g:X} '{n}' L{u.GameObject?.Level} HP={u.Health}/{u.MaxHealth} npc={u.NpcFlags} entry={u.GameObject?.Entry} at ({p?.X:F1},{p?.Y:F1},{p?.Z:F1})");
                }
            }

            await Task.Delay(500);
        }

        return (0UL, 0, 0f, 0f, 0f);
    }

    private static async Task<float> GetDistanceToTargetAsync(
        LiveBotFixture bot, string account, ulong targetGuid)
    {
        var snap = await bot.GetSnapshotAsync(account);
        var selfPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        var targetPos = target?.GameObject?.Base?.Position;
        if (selfPos == null || targetPos == null) return float.MaxValue;
        return LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, targetPos.X, targetPos.Y);
    }

    private static async Task<uint> GetTargetHealthAsync(
        LiveBotFixture bot, string account, ulong targetGuid)
    {
        var snap = await bot.GetSnapshotAsync(account);
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return target?.Health ?? 0;
    }

    private static async Task<bool> WaitForMobDeathAsync(
        LiveBotFixture bot, ITestOutputHelper output,
        string account, ulong targetGuid, uint initialHealth, TimeSpan timeout)
    {
        if (initialHealth == 0)
        {
            output.WriteLine("    BUG: WaitForMobDeathAsync called with initialHealth=0.");
            return false;
        }

        var sw = Stopwatch.StartNew();
        var lastDiagTime = TimeSpan.Zero;
        uint lastLoggedHp = initialHealth;
        bool firstDamageConfirmed = false;

        while (sw.Elapsed < timeout)
        {
            await bot.RefreshSnapshotsAsync();
            var snap = await bot.GetSnapshotAsync(account);
            var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            var currentHealth = target?.Health ?? 0;
            var diagTarget = snap?.Player?.Unit?.TargetGuid ?? 0UL;

            if (target == null || currentHealth == 0)
            {
                if (!firstDamageConfirmed)
                {
                    output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: Mob disappeared but NO damage dealt. NOT a valid kill.");
                    return false;
                }
                output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: MOB KILLED (HP {initialHealth}->0) after {sw.Elapsed.TotalSeconds:F1}s");
                return true;
            }

            if (currentHealth != lastLoggedHp)
            {
                if (!firstDamageConfirmed)
                {
                    output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: FIRST HIT - HP {lastLoggedHp}->{currentHealth} (damage={lastLoggedHp - currentHealth})");
                    firstDamageConfirmed = true;
                }
                else
                {
                    output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: HP {lastLoggedHp}->{currentHealth} (damage={lastLoggedHp - currentHealth})");
                }
                lastLoggedHp = currentHealth;
            }

            if (sw.Elapsed - lastDiagTime > TimeSpan.FromSeconds(5))
            {
                var dist = await GetDistanceToTargetAsync(bot, account, targetGuid);
                var pflags = snap?.Player?.PlayerFlags ?? 0;
                var gmBit = (pflags & 0x08) != 0 ? " GM=ON!" : "";
                var fac = snap?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
                var mobFac = target.GameObject?.FactionTemplate ?? 0;
                output.WriteLine(
                    $"    t={sw.Elapsed.TotalSeconds:F0}s: HP={currentHealth}/{initialHealth}, dist={dist:F1}y, target=0x{diagTarget:X}, flags=0x{pflags:X}{gmBit}, playerFac={fac}, mobFac={mobFac}, firstHit={firstDamageConfirmed}");
                lastDiagTime = sw.Elapsed;

                if (diagTarget == 0 && currentHealth >= initialHealth)
                {
                    output.WriteLine($"    EVADE detected: target cleared, HP={currentHealth}>={initialHealth}");
                    return false;
                }
            }

            await Task.Delay(400);
        }

        output.WriteLine($"    TIMEOUT after {timeout.TotalSeconds}s. HP={lastLoggedHp}/{initialHealth}, firstHit={firstDamageConfirmed}");
        return false;
    }

    public static async Task<bool> WaitForNearPositionAsync(
        LiveBotFixture bot, string account, float x, float y, float radius, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await bot.RefreshSnapshotsAsync();
            var snap = await bot.GetSnapshotAsync(account);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null && LiveBotFixture.Distance2D(pos.X, pos.Y, x, y) <= radius)
                return true;
            await Task.Delay(350);
        }
        return false;
    }
}
