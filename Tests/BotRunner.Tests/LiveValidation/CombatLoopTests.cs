using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Live melee-combat baseline for the dedicated COMBATTEST account.
///
/// COMBATTEST has GM level 6 for setup commands but never receives `.gm on`,
/// so faction data stays normal and mobs behave naturally.
///
/// Flow:
/// 1) Equip a weapon and learn its skill via GM setup commands
/// 2) Teleport near the Valley of Trials combat area
/// 3) Find a living nearby mob in snapshots
/// 4) Dispatch StartMeleeAttack from a real chase distance
/// 5) Assert the mob takes damage and dies
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class CombatLoopTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float MobAreaX = -284f;
    private const float MobAreaY = -4383f;
    private const float MobAreaZ = 57f;
    private const float MobAreaRadius = 80f;
    private const ulong CreatureGuidHighMask = 0xF000000000000000UL;
    private const ulong CreatureGuidHighPrefix = 0xF000000000000000UL;

    private static readonly HashSet<uint> AttackableCreatureEntries = [3098, 3108, 3124];
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
        var combatAccount = _bot.CombatTestAccountName;
        global::Tests.Infrastructure.Skip.If(combatAccount == null, "COMBATTEST bot not available - add COMBATTEST entry to StateManagerSettings.json");

        _output.WriteLine($"=== Combat Test Bot: {_bot.CombatTestCharacterName} ({combatAccount}) ===");
        _output.WriteLine("Using dedicated non-GM account (never receives .gm on -> no factionTemplate corruption)");

        var passed = await RunCombatScenarioAsync(combatAccount!, "COMBAT");
        Assert.True(passed, "COMBATTEST bot must approach, face, and auto-attack a mob to death.");
    }

    private const int MaxCombatAttempts = 3;

    private async Task<bool> RunCombatScenarioAsync(string account, string label)
    {
        await EnsureStrictAliveAsync(account, label);

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

        await EnsureNearMobAreaAsync(account, label);
        await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s.NearbyUnits?.Count > 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 500);

        await _bot.RefreshSnapshotsAsync();
        var selfSnap = await _bot.GetSnapshotAsync(account);
        var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (selfGuid == 0)
        {
            _output.WriteLine($"  [{label}] Missing self GUID.");
            return false;
        }

        var playerFlags = selfSnap?.Player?.PlayerFlags ?? 0;
        var isGmFlagSet = (playerFlags & 0x08) != 0;
        var factionTemplate = selfSnap?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
        _output.WriteLine(
            $"  [{label}] GM CHECK: playerFlags=0x{playerFlags:X8}, PLAYER_FLAGS_GM={(isGmFlagSet ? "SET (BAD!)" : "CLEAR (OK)")}, factionTemplate={factionTemplate} (expect 2=Orc)");
        if (isGmFlagSet)
        {
            _output.WriteLine($"  [{label}] ERROR: GM flag is set on COMBATTEST account - this should never happen.");
            return false;
        }

        // Retry loop: mobs can evade/despawn before the bot deals damage (environment issue).
        for (int attempt = 1; attempt <= MaxCombatAttempts; attempt++)
        {
            _output.WriteLine($"  [{label}] Finding candidate mob (attempt {attempt}/{MaxCombatAttempts})...");
            var (targetGuid, initialHealth, mobX, mobY, mobZ) = await FindLivingMobAsync(account, selfGuid, TimeSpan.FromSeconds(12));
            global::Tests.Infrastructure.Skip.If(targetGuid == 0, $"[{label}] No living mob found near Valley of Trials mob area.");
            _output.WriteLine($"  [{label}] Target: 0x{targetGuid:X} HP={initialHealth} at ({mobX:F1},{mobY:F1},{mobZ:F1})");

            var distanceToMob = await GetDistanceToTargetAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] Starting combat from {distanceToMob:F1}y away so StartMeleeAttack owns the approach.");

            initialHealth = await GetTargetHealthAsync(account, targetGuid);
            if (initialHealth == 0)
            {
                _output.WriteLine($"  [{label}] Target 0x{targetGuid:X} died during setup - retrying...");
                continue;
            }

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

            var mobKilled = await WaitForMobDeathAsync(account, label, targetGuid, initialHealth, TimeSpan.FromSeconds(90));
            if (mobKilled)
            {
                _output.WriteLine($"  [{label}] COMBAT COMPLETE: Mob killed via auto-attacks.");
                return true;
            }

            var currentHealth = await GetTargetHealthAsync(account, targetGuid);
            var postDist = await GetDistanceToTargetAsync(account, targetGuid);
            _output.WriteLine($"  [{label}] Attempt {attempt}: HP {initialHealth}->{currentHealth}, dist={postDist:F1}y");

            if (attempt < MaxCombatAttempts)
                _output.WriteLine($"  [{label}] Mob evaded/despawned before damage dealt — retrying with new target.");
        }

        _output.WriteLine($"  [{label}] FAIL: All {MaxCombatAttempts} combat attempts failed.");
        return false;
    }

    private async Task EnsureNearMobAreaAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return;

        var distance = LiveBotFixture.Distance2D(pos.X, pos.Y, MobAreaX, MobAreaY);
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

    private async Task<(ulong guid, uint health, float mobX, float mobY, float mobZ)> FindLivingMobAsync(
        string account,
        ulong selfGuid,
        TimeSpan timeout)
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
                    if ((guid & CreatureGuidHighMask) != CreatureGuidHighPrefix)
                        return false;
                    if (u.Health == 0 || u.MaxHealth == 0)
                        return false;
                    if (u.GameObject?.Level > 10)
                        return false;
                    if (u.MaxHealth > 500)
                        return false;
                    if (u.NpcFlags != 0)
                        return false;

                    var entry = u.GameObject?.Entry ?? 0;
                    return AttackableCreatureEntries.Contains(entry);
                })
                .OrderBy(u => GetCombatTargetPriority(u.GameObject?.Entry ?? 0))
                .ThenBy(u =>
                {
                    if (selfPos == null)
                        return float.MaxValue;
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
                _output.WriteLine(
                    $"    Candidate: 0x{guid:X} '{name}' entry={entry} HP={mob.Health}/{mob.MaxHealth} dist={dist:F1}y at ({mobPos?.X:F1},{mobPos?.Y:F1},{mobPos?.Z:F1})");
                return (guid, mob.Health, mobPos?.X ?? 0f, mobPos?.Y ?? 0f, mobPos?.Z ?? 0f);
            }

            if (sw.Elapsed.TotalSeconds < 2)
            {
                var allUnits = snap?.NearbyUnits ?? [];
                _output.WriteLine($"    [FindMob] {allUnits.Count} nearby units, 0 candidates. Self=0x{selfGuid:X}");
                foreach (var u in allUnits.Take(10))
                {
                    var g = u.GameObject?.Base?.Guid ?? 0UL;
                    var p = u.GameObject?.Base?.Position;
                    var n = u.GameObject?.Name ?? "?";
                    _output.WriteLine(
                        $"      0x{g:X} '{n}' L{u.GameObject?.Level} HP={u.Health}/{u.MaxHealth} npc={u.NpcFlags} entry={u.GameObject?.Entry} at ({p?.X:F1},{p?.Y:F1},{p?.Z:F1})");
                }
            }

            await Task.Delay(500);
        }

        return (0UL, 0, 0f, 0f, 0f);
    }

    private static int GetCombatTargetPriority(uint entry)
        => entry switch
        {
            3098 => 0, // Mottled Boar
            3108 => 1, // Vile Familiar
            3124 => 2, // Scorpid Worker
            _ => 99
        };

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

    private async Task<bool> WaitForMobDeathAsync(string account, string label, ulong targetGuid, uint initialHealth, TimeSpan timeout)
    {
        if (initialHealth == 0)
        {
            _output.WriteLine($"    [{label}] BUG: WaitForMobDeathAsync called with initialHealth=0.");
            return false;
        }

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

            if (target == null || currentHealth == 0)
            {
                if (!firstDamageConfirmed)
                {
                    _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: Mob disappeared or HP=0 but NO damage dealt. NOT a valid kill.");
                    return false;
                }

                _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: MOB KILLED (HP {initialHealth}->0) after {sw.Elapsed.TotalSeconds:F1}s");
                return true;
            }

            if (currentHealth != lastLoggedHp)
            {
                if (!firstDamageConfirmed)
                {
                    _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: FIRST HIT - HP {lastLoggedHp}->{currentHealth} (damage={lastLoggedHp - currentHealth})");
                    firstDamageConfirmed = true;
                }
                else
                {
                    _output.WriteLine($"    [{label}] t={sw.Elapsed.TotalSeconds:F1}s: HP {lastLoggedHp}->{currentHealth} (damage={lastLoggedHp - currentHealth})");
                }

                lastLoggedHp = currentHealth;
            }

            if (sw.Elapsed - lastDiagTime > TimeSpan.FromSeconds(5))
            {
                var dist = await GetDistanceToTargetAsync(account, targetGuid);
                var pflags = snap?.Player?.PlayerFlags ?? 0;
                var gmBit = (pflags & 0x08) != 0 ? " GM=ON!" : "";
                var fac = snap?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
                var mobFac = target.GameObject?.FactionTemplate ?? 0;
                _output.WriteLine(
                    $"    [{label}] t={sw.Elapsed.TotalSeconds:F0}s: HP={currentHealth}/{initialHealth}, dist={dist:F1}y, target=0x{diagTarget:X}, flags=0x{pflags:X}{gmBit}, playerFaction={fac}, mobFaction={mobFac}, firstHit={firstDamageConfirmed}");
                lastDiagTime = sw.Elapsed;

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
