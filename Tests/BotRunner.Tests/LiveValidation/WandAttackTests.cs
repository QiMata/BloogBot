using System;
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
/// Wand attack live validation.
///
/// The test launches TRMAF5 + TRMAB5 + SHODAN with Wand.config.json, stages
/// each mage BotRunner target with a wand loadout through StageBotRunnerLoadoutAsync,
/// stages that same target near natural Durotar mobs through a fixture helper,
/// and dispatches only ActionType.EquipItem / ActionType.StartWandAttack from
/// the test body. SHODAN remains director-only.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class WandAttackTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint TorchlightWand = 5240;       // Wand, required_level=0 in local MaNGOS item_template.
    private const uint WandProficiencySpell = 5009; // Wands.
    private const uint ShootSpell = 5019;           // Shoot.
    private const uint WandsSkillId = 228;
    private const uint RangedSlot = 17;
    private const uint MottledBoarEntry = 3098;
    private const string MottledBoarName = "Mottled Boar";

    private const uint UnitFlagInCombat = 0x00080000;
    private const float MaxWandTargetDistance = 30f;

    private const ulong CreatureGuidHighMask = 0xF000000000000000UL;
    private const ulong CreatureGuidHighPrefix = 0xF000000000000000UL;

    public WandAttackTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Wand_ShootTarget_DealsDamage()
    {
        var wandSettingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Wand.config.json");

        await _bot.EnsureSettingsAsync(wandSettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(wandSettingsPath);

        var targets = _bot.ResolveBotRunnerActionTargets(foregroundFirst: true);
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no wand dispatch.");
        foreach (var target in targets)
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
                $"stage Torchlight Wand, dispatch EquipItem({TorchlightWand}), StartWandAttack, StopAttack.");

        _output.WriteLine("[PARITY] Running wand scenarios sequentially so each role gets a fresh staged target.");

        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            var passed = await RunWandScenario(target.AccountName, target.RoleLabel, index);

            Assert.True(
                passed,
                $"{target.RoleLabel} bot ({target.AccountName}/{target.CharacterName}): " +
                "wand attack should engage a staged Durotar target.");
        }
    }

    private async Task<bool> RunWandScenario(string account, string label, int stageIndex)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { WandProficiencySpell, ShootSpell },
            skillsToSet: new[] { new LiveBotFixture.SkillDirective(WandsSkillId, 1, 300) },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(TorchlightWand, 1) });

        if (!await WaitForBagItemAsync(account, TorchlightWand, TimeSpan.FromSeconds(8)))
        {
            _output.WriteLine($"  [{label}] Torchlight Wand never observed in bags after staging; aborting.");
            return false;
        }

        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)TorchlightWand } }
        });
        _output.WriteLine($"  [{label}] Equip wand result: {equipResult}");
        Assert.Equal(ResponseResult.Success, equipResult);

        var wandEquipped = await WaitForRangedEquippedAsync(account, TimeSpan.FromSeconds(8));
        if (!wandEquipped)
        {
            var equipSnap = await _bot.GetSnapshotAsync(account);
            _output.WriteLine($"  [{label}] Wand not observed in ranged slot after EquipItem.");
            _bot.DumpSnapshotDiagnostics(equipSnap, label);
            return false;
        }

        Game.WoWUnit? target = null;
        for (var attempt = 0; attempt < 3 && target == null; attempt++)
        {
            var effectiveStageIndex = stageIndex + attempt;
            var staged = await _bot.StageBotRunnerAtDurotarMobAreaAsync(account, label, effectiveStageIndex);
            if (!staged)
            {
                _output.WriteLine(
                    $"  [{label}] Durotar mob-area stage {effectiveStageIndex} did not settle with nearby units.");
                continue;
            }

            await _bot.RefreshSnapshotsAsync();
            var stageSnap = await _bot.GetSnapshotAsync(account);
            target = FindWandTarget(stageSnap);
            if (target == null)
                _output.WriteLine($"  [{label}] No living wand-range boar found at stage {effectiveStageIndex}; trying next stage.");
        }

        if (target == null)
        {
            _output.WriteLine($"  [{label}] No living wand-range boar found after all Durotar mob stages.");
            var snap = await _bot.GetSnapshotAsync(account);
            _bot.DumpSnapshotDiagnostics(snap, label);
            return false;
        }

        var targetGuid = target.GameObject?.Base?.Guid ?? 0UL;
        var targetHealthBefore = target.Health;
        var targetName = target.GameObject?.Name ?? "(unnamed)";
        _output.WriteLine(
            $"  [{label}] Wand target: {targetName} GUID=0x{targetGuid:X} HP={target.Health}/{target.MaxHealth}");

        try
        {
            var wandResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.StartWandAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            _output.WriteLine($"  [{label}] StartWandAttack result: {wandResult}");
            Assert.Equal(ResponseResult.Success, wandResult);

            var engaged = await WaitForWandEngagementAsync(
                account,
                label,
                targetGuid,
                targetHealthBefore,
                TimeSpan.FromSeconds(20));

            if (!engaged)
            {
                var finalSnap = await _bot.GetSnapshotAsync(account);
                _output.WriteLine($"  [{label}] Wand attack did not produce combat or target damage.");
                _bot.DumpSnapshotDiagnostics(finalSnap, label);
            }

            return engaged;
        }
        finally
        {
            await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StopAttack });
            await Task.Delay(300);
        }
    }

    private Game.WoWUnit? FindWandTarget(WoWActivitySnapshot? snap)
    {
        var playerPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (playerPos == null)
            return null;

        var candidates = snap?.NearbyUnits?
            .Where(IsLivingCreatureTarget)
            .Select(unit =>
            {
                var pos = unit.GameObject?.Base?.Position;
                var distance = pos == null
                    ? float.MaxValue
                    : LiveBotFixture.Distance2D(playerPos.X, playerPos.Y, pos.X, pos.Y);
                return (Unit: unit, Distance: distance);
            })
            .Where(candidate => candidate.Distance <= MaxWandTargetDistance)
            .OrderBy(candidate => candidate.Distance)
            .ToArray() ?? Array.Empty<(Game.WoWUnit Unit, float Distance)>();

        foreach (var candidate in candidates.Take(5))
        {
            _output.WriteLine(
                $"    [WAND-TARGET] {candidate.Unit.GameObject?.Name ?? "(unnamed)"} " +
                $"GUID=0x{candidate.Unit.GameObject?.Base?.Guid ?? 0UL:X} " +
                $"HP={candidate.Unit.Health}/{candidate.Unit.MaxHealth} dist={candidate.Distance:F1}");
        }

        if (candidates.Length > 0)
            return candidates[0].Unit;

        LogNearbyUnitsForWand(snap, playerPos.X, playerPos.Y);
        return null;
    }

    private static bool IsLivingCreatureTarget(Game.WoWUnit unit)
    {
        var guid = unit.GameObject?.Base?.Guid ?? 0UL;
        if (guid == 0 || (guid & CreatureGuidHighMask) != CreatureGuidHighPrefix)
            return false;

        if (unit.Health == 0 || unit.MaxHealth == 0)
            return false;

        if (unit.GameObject?.Level > 10 || unit.MaxHealth > 200)
            return false;

        if (unit.NpcFlags != 0)
            return false;

        var entry = unit.GameObject?.Entry ?? 0;
        var name = unit.GameObject?.Name ?? string.Empty;
        return entry == MottledBoarEntry
            || string.Equals(name, MottledBoarName, StringComparison.OrdinalIgnoreCase)
            || name.Contains("mottled boar", StringComparison.OrdinalIgnoreCase);
    }

    private void LogNearbyUnitsForWand(WoWActivitySnapshot? snap, float playerX, float playerY)
    {
        foreach (var unit in snap?.NearbyUnits?.Take(12) ?? Enumerable.Empty<Game.WoWUnit>())
        {
            var pos = unit.GameObject?.Base?.Position;
            var distance = pos == null
                ? float.NaN
                : LiveBotFixture.Distance2D(playerX, playerY, pos.X, pos.Y);
            _output.WriteLine(
                $"    [WAND-NEARBY] {unit.GameObject?.Name ?? "(unnamed)"} " +
                $"entry={unit.GameObject?.Entry ?? 0} guid=0x{unit.GameObject?.Base?.Guid ?? 0UL:X} " +
                $"HP={unit.Health}/{unit.MaxHealth} level={unit.GameObject?.Level ?? 0} " +
                $"npcFlags={unit.NpcFlags} dist={distance:F1}");
        }
    }

    private async Task<bool> WaitForWandEngagementAsync(
        string account,
        string label,
        ulong targetGuid,
        uint targetHealthBefore,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var unitFlags = snap?.Player?.Unit?.UnitFlags ?? 0;
            var inCombat = (unitFlags & UnitFlagInCombat) != 0;
            var selectedTargetGuid = snap?.Player?.Unit?.TargetGuid ?? 0UL;
            var target = snap?.NearbyUnits?.FirstOrDefault(
                unit => (unit.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
            var targetDamagedOrGone = target == null || target.Health < targetHealthBefore;

            if (inCombat || targetDamagedOrGone)
            {
                _output.WriteLine(
                    $"  [{label}] Wand engagement after {sw.ElapsedMilliseconds}ms: " +
                    $"inCombat={inCombat}, selected=0x{selectedTargetGuid:X}, " +
                    $"targetHealth={target?.Health.ToString() ?? "(gone)"}/{targetHealthBefore}");
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private async Task<bool> WaitForRangedEquippedAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (snap?.Player?.Inventory.TryGetValue(RangedSlot, out var wandGuid) == true && wandGuid != 0)
                return true;

            await Task.Delay(200);
        }

        return false;
    }

    private async Task<bool> WaitForBagItemAsync(string account, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (snap?.Player?.BagContents?.Values.Any(id => id == itemId) == true)
                return true;

            await Task.Delay(200);
        }

        return false;
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }
}
