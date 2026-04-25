using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Combat;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Gathering profession integration tests (Mining + Herbalism).
///
/// Shodan migration shape:
///   1. Launch FG + BG + SHODAN with Gathering.config.json.
///   2. Stage profession loadout and route location through fixture helpers.
///   3. Dispatch only ActionType.StartGatheringRoute to the BotRunner target.
///   4. Assert gather success via snapshots and task diagnostics.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class GatheringProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint MiningGatherSpell = GatheringData.MINING_GATHER_SPELL;
    private const uint MiningPick = 2901;
    private const uint CopperVeinEntry = 1731;
    private const int MiningMinimumLevel = 20;
    private const int MiningRouteRequiredSkill = 1;

    private const uint HerbalismGatherSpell = GatheringData.HERBALISM_GATHER_SPELL;
    private const uint PeacebloomEntry = 1617;
    private const uint SilverleafEntry = 1618;
    private const uint EarthrootEntry = 1619;
    private const int HerbalismMinimumLevel = 20;
    private const int HerbalismRouteRequiredSkill = 15;

    public GatheringProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Mining_BG_GatherCopperVein()
    {
        await EnsureGatheringSettingsAsync();
        var target = ResolveTarget(isForeground: false);
        var valleyCandidates = await LoadValleyCopperCandidatesAsync();

        try
        {
            var gathered = await RunMiningScenarioAsync(target, valleyCandidates);
            Assert.True(gathered,
                $"{target.RoleLabel}: Failed to gather Copper Vein on the Valley copper route " +
                $"({valleyCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await _bot.ReturnBotRunnerToOrgrimmarSafeZoneAsync(target.AccountName, target.RoleLabel);
        }
    }

    [SkippableFact]
    public async Task Mining_FG_GatherCopperVein()
    {
        await EnsureGatheringSettingsAsync();
        var target = ResolveTarget(isForeground: true);
        var valleyCandidates = await LoadValleyCopperCandidatesAsync();

        try
        {
            var gathered = await RunMiningScenarioAsync(target, valleyCandidates);
            Assert.True(gathered,
                $"{target.RoleLabel}: Failed to gather Copper Vein on the Valley copper route " +
                $"({valleyCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await _bot.ReturnBotRunnerToOrgrimmarSafeZoneAsync(target.AccountName, target.RoleLabel);
        }
    }

    [SkippableFact]
    public async Task Herbalism_BG_GatherHerb()
    {
        await EnsureGatheringSettingsAsync();
        var target = ResolveTarget(isForeground: false);
        var herbCandidates = await LoadDurotarHerbCandidatesAsync();

        try
        {
            var gathered = await RunHerbalismScenarioAsync(target, herbCandidates);
            Assert.True(gathered,
                $"{target.RoleLabel}: Failed to gather herb on the Durotar herb route " +
                $"({herbCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await _bot.ReturnBotRunnerToOrgrimmarSafeZoneAsync(target.AccountName, target.RoleLabel);
        }
    }

    [SkippableFact]
    public async Task Herbalism_FG_GatherHerb()
    {
        await EnsureGatheringSettingsAsync();
        var target = ResolveTarget(isForeground: true);
        var herbCandidates = await LoadDurotarHerbCandidatesAsync();

        try
        {
            var gathered = await RunHerbalismScenarioAsync(target, herbCandidates);
            Assert.True(gathered,
                $"{target.RoleLabel}: Failed to gather herb on the Durotar herb route " +
                $"({herbCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await _bot.ReturnBotRunnerToOrgrimmarSafeZoneAsync(target.AccountName, target.RoleLabel);
        }
    }

    private async Task<string> EnsureGatheringSettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Gathering.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Gathering.config.json.");

        return settingsPath;
    }

    private LiveBotFixture.BotRunnerActionTarget ResolveTarget(bool isForeground)
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no gathering action dispatch.");

        foreach (var target in targets)
        {
            var action = target.IsForeground == isForeground
                ? "stage route + dispatch StartGatheringRoute"
                : "idle for this test method";
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: {action}.");
        }

        var selected = targets.FirstOrDefault(target => target.IsForeground == isForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(selected.AccountName),
            isForeground ? "FG bot not available/actionable." : "BG bot not available.");

        return selected;
    }

    private async Task<IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)>> LoadValleyCopperCandidatesAsync()
    {
        var valleyCandidates = GatheringRouteSelection.SelectValleyCopperVeinCandidates(
            await _bot.QueryGameObjectSpawnsNearAsync(
                [CopperVeinEntry],
                GatheringRouteSelection.DurotarMap,
                GatheringRouteSelection.ValleyCopperRouteStartX,
                GatheringRouteSelection.ValleyCopperRouteStartY,
                GatheringRouteSelection.ValleyCopperSearchRadius,
                limit: GatheringRouteSelection.ValleyCopperQueryLimit),
            CopperVeinEntry);
        Assert.True(valleyCandidates.Count > 0,
            "DB must have copper vein spawns near Valley of Trials.");
        _output.WriteLine(
            $"Selected {valleyCandidates.Count} Valley copper-route candidates " +
            $"with nearest route distance {valleyCandidates[0].distance2D:F0}y.");
        return valleyCandidates;
    }

    private async Task<IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)>> LoadDurotarHerbCandidatesAsync()
    {
        uint[] herbEntries = [PeacebloomEntry, SilverleafEntry, EarthrootEntry];
        var herbCandidates = GatheringRouteSelection.SelectDurotarHerbCandidates(
            await _bot.QueryGameObjectSpawnsNearAsync(
                herbEntries,
                GatheringRouteSelection.DurotarMap,
                GatheringRouteSelection.DurotarHerbRouteStartX,
                GatheringRouteSelection.DurotarHerbRouteStartY,
                GatheringRouteSelection.DurotarHerbSearchRadius,
                limit: GatheringRouteSelection.DurotarHerbQueryLimit),
            herbEntries);
        Assert.True(herbCandidates.Count > 0,
            "DB must have herb spawns near Durotar; no natural herb route candidates found.");
        _output.WriteLine(
            $"Selected {herbCandidates.Count} Durotar herb-route candidates " +
            $"with nearest route distance {herbCandidates[0].distance2D:F0}y.");
        return herbCandidates;
    }

    private async Task<bool> RunMiningScenarioAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> valleyCandidates)
    {
        await StageMiningLoadoutAsync(target.AccountName, target.RoleLabel);

        await _bot.RefreshSnapshotsAsync();
        var skillBefore = GetSkill(target.RoleLabel, GatheringData.MINING_SKILL_ID);
        _output.WriteLine($"{target.RoleLabel} initial mining skill: {skillBefore}");

        var activeCandidates = await _bot.RefreshAndPrioritizeGatheringPoolsWithShodanAsync(
            _bot.ShodanAccountName!,
            $"{target.RoleLabel} mining",
            valleyCandidates,
            GatheringRouteSelection.ValleyCopperRouteStartX,
            GatheringRouteSelection.ValleyCopperRouteStartY,
            GatheringRouteSelection.ValleyCopperSearchRadius);

        var staged = await _bot.StageBotRunnerAtValleyCopperRouteStartAsync(target.AccountName, target.RoleLabel);
        if (!staged)
        {
            _output.WriteLine($"[{target.RoleLabel}] Valley copper route stage did not settle.");
            return false;
        }

        var bagBefore = CaptureBagItemCounts(target.RoleLabel);
        var diagStart = DateTime.UtcNow;
        var dispatchResult = await _bot.SendActionAsync(
            target.AccountName,
            BuildGatheringRouteAction(MiningGatherSpell, [CopperVeinEntry], activeCandidates));
        Assert.Equal(ResponseResult.Success, dispatchResult);

        var gathered = await WaitForGatheringRouteOutcomeAsync(
            target.RoleLabel,
            GatheringData.MINING_SKILL_ID,
            skillBefore,
            bagBefore,
            diagStart,
            timeout: TimeSpan.FromMinutes(5));

        await _bot.RefreshSnapshotsAsync();
        var skillAfter = GetSkill(target.RoleLabel, GatheringData.MINING_SKILL_ID);
        _output.WriteLine($"{target.RoleLabel} Results: gathered={gathered}, skill {skillBefore} -> {skillAfter}");

        if (skillAfter <= skillBefore)
            _output.WriteLine($"{target.RoleLabel}: WARNING - Mining skill did not increase. RNG skill-up mechanic.");

        return gathered;
    }

    private async Task<bool> RunHerbalismScenarioAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> herbCandidates)
    {
        uint[] herbEntries = [PeacebloomEntry, SilverleafEntry, EarthrootEntry];

        await StageHerbalismLoadoutAsync(target.AccountName, target.RoleLabel);

        await _bot.RefreshSnapshotsAsync();
        var skillBefore = GetSkill(target.RoleLabel, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"{target.RoleLabel} initial herbalism skill: {skillBefore}");

        var activeCandidates = await _bot.RefreshAndPrioritizeGatheringPoolsWithShodanAsync(
            _bot.ShodanAccountName!,
            $"{target.RoleLabel} herbalism",
            herbCandidates,
            GatheringRouteSelection.DurotarHerbRouteStartX,
            GatheringRouteSelection.DurotarHerbRouteStartY,
            GatheringRouteSelection.DurotarHerbSearchRadius);

        var staged = await _bot.StageBotRunnerAtDurotarHerbRouteStartAsync(target.AccountName, target.RoleLabel);
        if (!staged)
        {
            _output.WriteLine($"[{target.RoleLabel}] Durotar herb route stage did not settle.");
            return false;
        }

        var bagBefore = CaptureBagItemCounts(target.RoleLabel);
        var diagStart = DateTime.UtcNow;
        var dispatchResult = await _bot.SendActionAsync(
            target.AccountName,
            BuildGatheringRouteAction(HerbalismGatherSpell, herbEntries, activeCandidates));
        Assert.Equal(ResponseResult.Success, dispatchResult);

        var gathered = await WaitForGatheringRouteOutcomeAsync(
            target.RoleLabel,
            GatheringData.HERBALISM_SKILL_ID,
            skillBefore,
            bagBefore,
            diagStart,
            timeout: TimeSpan.FromMinutes(5));

        await _bot.RefreshSnapshotsAsync();
        var skillAfter = GetSkill(target.RoleLabel, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"{target.RoleLabel} Results: gathered={gathered}, skill {skillBefore} -> {skillAfter}");

        if (skillAfter <= skillBefore)
            _output.WriteLine($"{target.RoleLabel}: WARNING - Herbalism skill did not increase. RNG skill-up mechanic.");

        return gathered;
    }

    private async Task StageMiningLoadoutAsync(string account, string label)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { MiningGatherSpell },
            skillsToSet: new[] { new LiveBotFixture.SkillDirective(GatheringData.MINING_SKILL_ID, MiningRouteRequiredSkill, 300) },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(MiningPick, 1) },
            levelTo: MiningMinimumLevel);

        var ready = await _bot.WaitForSnapshotConditionAsync(
            account,
            snap => snap.Player?.SkillInfo.TryGetValue(GatheringData.MINING_SKILL_ID, out var skill) == true
                    && skill >= MiningRouteRequiredSkill
                    && snap.Player.BagContents.Values.Any(itemId => itemId == MiningPick)
                    && (snap.Player.Unit?.GameObject?.Level ?? 0) >= MiningMinimumLevel,
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 300,
            progressLabel: $"{label} mining-loadout");
        Assert.True(ready, $"{label}: mining skill, level, and pick should be staged before StartGatheringRoute.");
    }

    private async Task StageHerbalismLoadoutAsync(string account, string label)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { HerbalismGatherSpell },
            skillsToSet: new[] { new LiveBotFixture.SkillDirective(GatheringData.HERBALISM_SKILL_ID, HerbalismRouteRequiredSkill, 300) },
            levelTo: HerbalismMinimumLevel);

        var ready = await _bot.WaitForSnapshotConditionAsync(
            account,
            snap => snap.Player?.SkillInfo.TryGetValue(GatheringData.HERBALISM_SKILL_ID, out var skill) == true
                    && skill >= HerbalismRouteRequiredSkill
                    && (snap.Player.Unit?.GameObject?.Level ?? 0) >= HerbalismMinimumLevel,
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 300,
            progressLabel: $"{label} herbalism-loadout");
        Assert.True(ready, $"{label}: herbalism skill and level should be staged before StartGatheringRoute.");
    }

    private ActionMessage BuildGatheringRouteAction(
        uint gatherSpellId,
        IReadOnlyCollection<uint> nodeEntries,
        IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> routeCandidates,
        int maxRouteLoops = 2)
    {
        var action = new ActionMessage
        {
            ActionType = ActionType.StartGatheringRoute,
            Parameters =
            {
                new RequestParameter { IntParam = (int)gatherSpellId },
                new RequestParameter { StringParam = string.Join(",", nodeEntries.OrderBy(entry => entry)) },
                new RequestParameter { IntParam = maxRouteLoops }
            }
        };

        foreach (var candidate in routeCandidates)
        {
            action.Parameters.Add(new RequestParameter { FloatParam = candidate.x });
            action.Parameters.Add(new RequestParameter { FloatParam = candidate.y });
            action.Parameters.Add(new RequestParameter { FloatParam = candidate.z });
        }

        return action;
    }

    private Dictionary<uint, int> CaptureBagItemCounts(string label)
    {
        var counts = new Dictionary<uint, int>();
        var bagContents = GetSnapshot(label)?.Player?.BagContents;
        if (bagContents == null)
            return counts;

        foreach (var itemId in bagContents.Values)
        {
            counts.TryGetValue(itemId, out var count);
            counts[itemId] = count + 1;
        }

        return counts;
    }

    private async Task<bool> WaitForGatheringRouteOutcomeAsync(
        string label,
        uint skillId,
        uint initialSkill,
        IReadOnlyDictionary<uint, int> bagCountsBefore,
        DateTime diagStartUtc,
        TimeSpan timeout)
    {
        var endTime = DateTime.UtcNow + timeout;
        var sawRouteExhausted = false;
        var recentDiagSummary = "none";

        while (DateTime.UtcNow < endTime)
        {
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();

            var skillNow = GetSkill(label, skillId);
            if (skillNow > initialSkill)
                return true;

            var bagCountsAfter = CaptureBagItemCounts(label);
            var bagDelta = bagCountsAfter.Any(pair =>
                pair.Value > bagCountsBefore.GetValueOrDefault(pair.Key, 0));
            if (bagDelta)
                return true;

            var diagLines = LiveBotFixture.ReadRecentBotRunnerDiagnosticLines(
                ["GatheringRouteTask", "gather_success", "route_complete_no_nodes"],
                minWriteUtc: diagStartUtc,
                maxLines: 10);
            recentDiagSummary = diagLines.Count == 0 ? "none" : string.Join(" || ", diagLines);

            if (diagLines.Any(line => line.Contains("gather_success", StringComparison.OrdinalIgnoreCase)
                                      || line.Contains("pop reason=gather_success", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (diagLines.Any(line => line.Contains("route_complete_no_nodes", StringComparison.OrdinalIgnoreCase)))
                sawRouteExhausted = true;
        }

        if (sawRouteExhausted)
            _output.WriteLine($"[{label}] Gathering route exhausted all candidates without a visible node. diag={recentDiagSummary}");
        else
            _output.WriteLine($"[{label}] Gathering route timed out. diag={recentDiagSummary}");
        return false;
    }

    private uint GetSkill(string label, uint skillId)
    {
        var skillMap = GetSnapshot(label)?.Player?.SkillInfo;
        if (skillMap != null && skillMap.TryGetValue(skillId, out var level))
            return level;
        return 0;
    }

    private WoWActivitySnapshot? GetSnapshot(string label)
        => label == "FG" ? _bot.ForegroundBot : _bot.BackgroundBot;

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
