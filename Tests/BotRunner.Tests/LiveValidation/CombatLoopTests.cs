using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Live melee-combat tests using the dedicated COMBATTEST account.
///
/// COMBATTEST never receives `.gm on`, so faction data stays normal and mobs
/// engage naturally. Two test variants:
///
/// 1) FG combat: COMBATTEST is the FG bot (injected WoW.exe) — gold standard.
/// 2) BG combat: COMBATTEST is the BG bot (headless), with the FG bot (TESTBOT1)
///    positioned nearby as a GM camera so the human can observe.
///
/// Flow for both:
/// 1) Prep in Orgrimmar safe zone — equip weapon, learn skill
/// 2) Teleport COMBATTEST to Valley of Trials mob area
/// 3) (BG only) Position FG observer nearby, facing COMBATTEST
/// 4) Find a living mob, dispatch StartMeleeAttack
/// 5) Assert the mob takes damage and dies
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class CombatLoopTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float MobAreaX = -284f;
    private const float MobAreaY = -4383f;
    private const float MobAreaZ = 60f; // Z+3 offset from spawn table (~57) to avoid UNDERMAP detection
    private const float MobAreaRadius = 80f;

    // Observer offset: 15y behind the mob area so FG camera sees the fight
    private const float ObserverX = MobAreaX + 15f;
    private const float ObserverY = MobAreaY;
    private const float ObserverZ = MobAreaZ;

    private static readonly HashSet<uint> AttackableCreatureEntries = [3098, 3101, 3124];
    private const uint OneHandMaceSpell = 198;
    private const int MaxCombatAttempts = 3;

    public CombatLoopTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Resolves the path to a per-test settings file.
    /// Settings files are in LiveValidation/Settings/ and copied to test output.
    /// </summary>
    private static string ResolveTestSettingsPath(string settingsFileName)
    {
        // First check the output directory (files copied by CopyToOutputDirectory)
        var outputPath = Path.Combine(AppContext.BaseDirectory, "LiveValidation", "Settings", settingsFileName);
        if (File.Exists(outputPath))
            return outputPath;

        // Fallback: walk up from output dir to find the source file
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Tests", "BotRunner.Tests", "LiveValidation", "Settings", settingsFileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Test settings file not found: {settingsFileName}");
    }

    /// <summary>
    /// FG combat: COMBATTEST as the FG (injected) bot fights a mob.
    /// Gold standard — real WoW client, native functions, visible in-game.
    /// Restarts StateManager with CombatFg.settings.json (COMBATTEST=Foreground).
    /// </summary>
    [SkippableFact]
    public async Task Combat_FG_AutoAttacksMob_DealsDamageInMeleeRange()
    {
        var settingsPath = ResolveTestSettingsPath("CombatFg.settings.json");
        _output.WriteLine($"Restarting with FG combat settings: {settingsPath}");
        await _bot.EnsureSettingsAsync(settingsPath);
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready after restart");

        var combatAccount = _bot.CombatTestAccountName;
        Assert.True(combatAccount != null, "COMBATTEST account not found after restart with CombatFg.settings.json");
        Assert.True(string.Equals(combatAccount, _bot.FgAccountName, StringComparison.OrdinalIgnoreCase),
            $"COMBATTEST ({combatAccount}) should be FG bot (FG={_bot.FgAccountName}) after restart with CombatFg.settings.json");

        _output.WriteLine($"=== FG Combat Test: {_bot.CombatTestCharacterName} ({combatAccount}) ===");
        _output.WriteLine("COMBATTEST on FG (injected WoW.exe) — gold standard combat validation");

        var killed = await RunCombatScenarioAsync(combatAccount!, observerAccount: null);
        Assert.True(killed, "FG COMBATTEST must approach, face, and auto-attack a mob to death.");
    }

    /// <summary>
    /// BG combat: COMBATTEST as the BG (headless) bot fights a mob.
    /// FG bot (TESTBOT1) positioned nearby with GM on as visual observer.
    /// Restarts StateManager with CombatBg.settings.json (COMBATTEST=Background).
    /// </summary>
    [SkippableFact]
    public async Task Combat_BG_AutoAttacksMob_WithFgObserver()
    {
        var settingsPath = ResolveTestSettingsPath("CombatBg.settings.json");
        _output.WriteLine($"Restarting with BG combat settings: {settingsPath}");
        await _bot.EnsureSettingsAsync(settingsPath);
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready after restart");

        var combatAccount = _bot.CombatTestAccountName;
        Assert.True(combatAccount != null, "COMBATTEST account not found after restart with CombatBg.settings.json");
        Assert.True(!string.Equals(combatAccount, _bot.FgAccountName, StringComparison.OrdinalIgnoreCase),
            $"COMBATTEST ({combatAccount}) should NOT be the FG bot after restart with CombatBg.settings.json");

        _output.WriteLine($"=== BG Combat Test: {_bot.CombatTestCharacterName} ({combatAccount}) ===");
        _output.WriteLine($"FG observer: {_bot.FgAccountName} (GM on, camera)");

        var killed = await RunCombatScenarioAsync(combatAccount!, observerAccount: _bot.FgAccountName);
        Assert.True(killed, "BG COMBATTEST must approach, face, and auto-attack a mob to death.");
    }

    // ---- Core Combat Scenario ----

    /// <summary>
    /// Shared combat flow. If observerAccount is non-null, that bot is positioned
    /// nearby with GM on as a visual camera.
    /// </summary>
    private async Task<bool> RunCombatScenarioAsync(string combatAccount, string? observerAccount)
    {
        // --- Phase 1: Prep in Orgrimmar safe zone ---
        _output.WriteLine("\n--- Phase 1: Prep (Orgrimmar safe zone) ---");
        await _bot.EnsureStrictAliveAsync(combatAccount, "COMBAT");

        await _bot.BotTeleportAsync(combatAccount, LiveBotFixture.SafeZoneMap,
            LiveBotFixture.SafeZoneX, LiveBotFixture.SafeZoneY, LiveBotFixture.SafeZoneZ);
        await Task.Delay(1500);

        _output.WriteLine("  Equipping weapon (Worn Mace) and learning 1H Mace skill...");
        await _bot.BotLearnSpellAsync(combatAccount, OneHandMaceSpell);
        await _bot.BotSetSkillAsync(combatAccount, 54, 1, 300);
        await _bot.BotAddItemAsync(combatAccount, LiveBotFixture.TestItems.WornMace);
        await Task.Delay(500);
        await _bot.SendActionAsync(combatAccount, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)LiveBotFixture.TestItems.WornMace } }
        });
        await Task.Delay(500);

        // --- Phase 2: Teleport to mob area ---
        _output.WriteLine("\n--- Phase 2: Teleport to Valley of Trials mob area ---");
        await _bot.BotTeleportAsync(combatAccount, MapId, MobAreaX, MobAreaY, MobAreaZ);
        var arrived = await WaitForNearPositionAsync(combatAccount, MobAreaX, MobAreaY, MobAreaRadius, TimeSpan.FromSeconds(12));
        Assert.True(arrived, "COMBATTEST failed to arrive near mob area after teleport.");

        // Force nearby creatures to respawn
        await _bot.SendGmChatCommandAsync(combatAccount, ".respawn");
        await Task.Delay(2000);

        // --- Phase 3: Position FG observer (BG test only) ---
        if (observerAccount != null)
            await PositionFgObserverAsync(combatAccount, observerAccount);

        // --- Phase 4: Verify no GM flag on COMBATTEST ---
        await _bot.RefreshSnapshotsAsync();
        var selfSnap = await _bot.GetSnapshotAsync(combatAccount);
        var selfGuid = selfSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        Assert.True(selfGuid != 0, "COMBATTEST missing self GUID in snapshot.");

        var playerFlags = selfSnap?.Player?.PlayerFlags ?? 0;
        var isGmFlagSet = (playerFlags & 0x08) != 0;
        var factionTemplate = selfSnap?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
        _output.WriteLine(
            $"  GM CHECK: playerFlags=0x{playerFlags:X8}, GM={(isGmFlagSet ? "SET (BAD!)" : "CLEAR (OK)")}, factionTemplate={factionTemplate}");
        Assert.False(isGmFlagSet, "GM flag is set on COMBATTEST — this should never happen.");

        // Wait for nearby units to stream in
        await _bot.WaitForSnapshotConditionAsync(
            combatAccount,
            s => s.NearbyUnits?.Count > 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 500);

        // --- Phase 5: Combat loop ---
        _output.WriteLine("\n--- Phase 5: Combat ---");
        for (int attempt = 1; attempt <= MaxCombatAttempts; attempt++)
        {
            _output.WriteLine($"  Finding candidate mob (attempt {attempt}/{MaxCombatAttempts})...");
            var (targetGuid, initialHealth, mobX, mobY, mobZ) =
                await FindLivingMobAsync(combatAccount, selfGuid, TimeSpan.FromSeconds(20));
            Assert.True(targetGuid != 0,
                "No living mob found near Valley of Trials after 20s. " +
                "Mobs should always be present — detection or ObjectManager bug.");
            _output.WriteLine($"  Target: 0x{targetGuid:X} HP={initialHealth} at ({mobX:F1},{mobY:F1},{mobZ:F1})");

            var distanceToMob = await GetDistanceToTargetAsync(combatAccount, targetGuid);
            _output.WriteLine($"  Starting combat from {distanceToMob:F1}y away.");

            initialHealth = await GetTargetHealthAsync(combatAccount, targetGuid);
            if (initialHealth == 0)
            {
                _output.WriteLine($"  Target 0x{targetGuid:X} died during setup — retrying...");
                continue;
            }

            // Re-face observer toward the mob
            if (observerAccount != null)
                await FaceBotTowardAsync(observerAccount, mobX, mobY);

            _output.WriteLine($"  Sending StartMeleeAttack on 0x{targetGuid:X} (HP={initialHealth})");
            var attackResult = await _bot.SendActionAsync(combatAccount, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            if (attackResult != ResponseResult.Success)
            {
                _output.WriteLine($"  StartMeleeAttack dispatch failed: {attackResult}");
                Assert.Fail($"StartMeleeAttack dispatch failed: {attackResult}");
            }

            var killed = await WaitForMobDeathAsync(combatAccount, targetGuid, initialHealth, TimeSpan.FromSeconds(90));
            if (killed)
            {
                _output.WriteLine("  COMBAT COMPLETE: Mob killed via auto-attacks.");
                return true;
            }

            var currentHealth = await GetTargetHealthAsync(combatAccount, targetGuid);
            var postDist = await GetDistanceToTargetAsync(combatAccount, targetGuid);
            _output.WriteLine($"  Attempt {attempt}: HP {initialHealth}->{currentHealth}, dist={postDist:F1}y — retrying.");
        }

        _output.WriteLine($"  FAIL: All {MaxCombatAttempts} combat attempts failed.");
        return false;
    }

    // ---- FG Observer Helpers ----

    /// <summary>
    /// Teleport the observer bot near the combat area with GM on so the human
    /// can watch the fight in the WoW client window.
    /// </summary>
    private async Task PositionFgObserverAsync(string combatAccount, string observerAccount)
    {
        Assert.True(_bot.IsFgActionable,
            "FG observer bot is required for BG combat test but is not actionable (crashed or not alive). " +
            "FG observability is a hard requirement — if the FG bot isn't running, the BG test fails.");

        _output.WriteLine("\n--- Phase 3: Position FG observer ---");

        // Ensure GM mode is on for observer (so mobs ignore it)
        await _bot.SendGmChatCommandAsync(observerAccount, ".gm on");
        await Task.Delay(500);

        // Teleport observer to offset position
        await _bot.BotTeleportAsync(observerAccount, MapId, ObserverX, ObserverY, ObserverZ);
        await Task.Delay(1500);

        // Face toward the combat bot
        await FaceBotTowardTargetBotAsync(observerAccount, combatAccount);

        _output.WriteLine($"  [FG-OBSERVER] {observerAccount} at ({ObserverX:F1},{ObserverY:F1},{ObserverZ:F1}), GM on, facing combat bot.");
    }

    /// <summary>Face a bot toward another bot's current position.</summary>
    private async Task FaceBotTowardTargetBotAsync(string facingAccount, string targetAccount)
    {
        await _bot.RefreshSnapshotsAsync();
        var targetSnap = await _bot.GetSnapshotAsync(targetAccount);
        var targetPos = targetSnap?.Player?.Unit?.GameObject?.Base?.Position;
        if (targetPos != null)
            await FaceBotTowardAsync(facingAccount, targetPos.X, targetPos.Y);
    }

    /// <summary>Face a bot toward a world position.</summary>
    private async Task FaceBotTowardAsync(string account, float targetX, float targetY)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null) return;

        var facing = (float)Math.Atan2(targetY - pos.Y, targetX - pos.X);
        if (facing < 0) facing += (float)(2 * Math.PI);

        await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SetFacing,
            Parameters = { new RequestParameter { FloatParam = facing } }
        });
    }

    // ---- Mob Finding & Combat Helpers ----

    private async Task<(ulong guid, uint health, float mobX, float mobY, float mobZ)> FindLivingMobAsync(
        string account, ulong selfGuid, TimeSpan timeout)
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
                _output.WriteLine(
                    $"    Candidate: 0x{guid:X} '{name}' entry={entry} HP={mob.Health}/{mob.MaxHealth} dist={dist:F1}y at ({mobPos?.X:F1},{mobPos?.Y:F1},{mobPos?.Z:F1})");
                return (guid, mob.Health, mobPos?.X ?? 0f, mobPos?.Y ?? 0f, mobPos?.Z ?? 0f);
            }

            if (sw.Elapsed.TotalSeconds < 4)
            {
                var allUnits = snap?.NearbyUnits ?? [];
                _output.WriteLine($"    [FindMob] {allUnits.Count} nearby units, 0 candidates. Self=0x{selfGuid:X}");
                foreach (var u in allUnits.Take(15))
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
            3101 => 1, // Vile Familiar
            3124 => 2, // Scorpid Worker
            _ => 99
        };

    private async Task<float> GetDistanceToTargetAsync(string account, ulong targetGuid)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var selfPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        var targetPos = target?.GameObject?.Base?.Position;
        if (selfPos == null || targetPos == null) return float.MaxValue;
        return LiveBotFixture.Distance2D(selfPos.X, selfPos.Y, targetPos.X, targetPos.Y);
    }

    private async Task<uint> GetTargetHealthAsync(string account, ulong targetGuid)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var target = snap?.NearbyUnits?.FirstOrDefault(u => (u.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return target?.Health ?? 0;
    }

    private async Task<bool> WaitForMobDeathAsync(string account, ulong targetGuid, uint initialHealth, TimeSpan timeout)
    {
        if (initialHealth == 0)
        {
            _output.WriteLine("    BUG: WaitForMobDeathAsync called with initialHealth=0.");
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
                    _output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: Mob disappeared but NO damage dealt. NOT a valid kill.");
                    return false;
                }
                _output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: MOB KILLED (HP {initialHealth}->0) after {sw.Elapsed.TotalSeconds:F1}s");
                return true;
            }

            if (currentHealth != lastLoggedHp)
            {
                if (!firstDamageConfirmed)
                {
                    _output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: FIRST HIT - HP {lastLoggedHp}->{currentHealth} (damage={lastLoggedHp - currentHealth})");
                    firstDamageConfirmed = true;
                }
                else
                {
                    _output.WriteLine($"    t={sw.Elapsed.TotalSeconds:F1}s: HP {lastLoggedHp}->{currentHealth} (damage={lastLoggedHp - currentHealth})");
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
                    $"    t={sw.Elapsed.TotalSeconds:F0}s: HP={currentHealth}/{initialHealth}, dist={dist:F1}y, target=0x{diagTarget:X}, flags=0x{pflags:X}{gmBit}, playerFac={fac}, mobFac={mobFac}, firstHit={firstDamageConfirmed}");
                lastDiagTime = sw.Elapsed;

                if (diagTarget == 0 && currentHealth >= initialHealth)
                {
                    _output.WriteLine($"    EVADE detected: target cleared, HP={currentHealth}>={initialHealth}");
                    return false;
                }
            }

            await Task.Delay(400);
        }

        _output.WriteLine($"    TIMEOUT after {timeout.TotalSeconds}s. HP={lastLoggedHp}/{initialHealth}, firstHit={firstDamageConfirmed}");
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
            if (pos != null && LiveBotFixture.Distance2D(pos.X, pos.Y, x, y) <= radius)
                return true;
            await Task.Delay(350);
        }
        return false;
    }
}
