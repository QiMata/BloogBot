using System;
using System.Collections.Generic;
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
/// Strategy:
///   1. Teleport to Orgrimmar (safe zone) for setup — learn spells, set skills
///   2. For mining, query Valley of Trials copper spawns and dispatch StartGatheringRoute
///      so GatheringRouteTask owns route optimization, movement, discovery, and gather
///   3. For herbalism, query Durotar herb spawns and dispatch StartGatheringRoute
///      so GatheringRouteTask owns route optimization, movement, discovery, and gather
///   4. Interact and verify gather success / skill progression metrics
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~GatheringProfessionTests" --configuration Release -v n
/// </summary>
[Collection(BgOnlyValidationCollection.Name)]
public class GatheringProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // --- Mining constants ---
    private const uint MiningApprentice = 2575;
    private const uint MiningGatherSpell = GatheringData.MINING_GATHER_SPELL;  // 2575 — profession spell with 3.2s channel
    private const uint MiningPick = 2901;
    private const uint CopperVeinEntry = 1731;

    // --- Herbalism constants ---
    private const uint HerbalismApprentice = 2366;
    private const uint HerbalismGatherSpell = GatheringData.HERBALISM_GATHER_SPELL;  // 2366 — same as profession spell
    private const uint PeacebloomEntry = 1617;
    private const uint SilverleafEntry = 1618;
    private const uint EarthrootEntry = 1619;

    // Orgrimmar (safe zone, no hostile mobs) for GM setup
    private const int OrgrimmarMap = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 31.2f;

    public GatheringProfessionTests(BgOnlyBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    // =====================================================================
    //  ROUTE LOADERS
    // =====================================================================

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
            "DB must have herb spawns near Durotar — no natural herb route candidates found.");
        _output.WriteLine(
            $"Selected {herbCandidates.Count} Durotar herb-route candidates " +
            $"with nearest route distance {herbCandidates[0].distance2D:F0}y.");
        return herbCandidates;
    }

    // =====================================================================
    //  SCENARIO RUNNERS
    // =====================================================================

    private async Task<bool> RunMiningScenarioAsync(string account, string label,
        IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> valleyCandidates)
    {
        await PrepareMining(account, label);

        await _bot.RefreshSnapshotsAsync();
        uint skillBefore = GetSkill(label, GatheringData.MINING_SKILL_ID);
        _output.WriteLine($"{label} initial mining skill: {skillBefore}");

        await StageAtValleyCopperRouteStartAsync(account, label);
        var bagBefore = CaptureBagItemCounts(label);
        var diagStart = DateTime.UtcNow;
        await _bot.SendActionAndWaitAsync(
            account,
            BuildGatheringRouteAction(MiningGatherSpell, [CopperVeinEntry], valleyCandidates),
            delayMs: 500);
        bool gathered = await WaitForGatheringRouteOutcomeAsync(
            label, GatheringData.MINING_SKILL_ID, skillBefore, bagBefore, diagStart,
            timeout: TimeSpan.FromMinutes(5));

        await _bot.RefreshSnapshotsAsync();
        uint skillAfter = GetSkill(label, GatheringData.MINING_SKILL_ID);
        _output.WriteLine($"{label} Results: gathered={gathered}, skill {skillBefore} -> {skillAfter}");

        if (skillAfter <= skillBefore)
            _output.WriteLine($"{label}: WARNING - Mining skill did not increase. RNG skill-up mechanic.");

        return gathered;
    }

    private async Task<bool> RunHerbalismScenarioAsync(string account, string label,
        IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> herbCandidates)
    {
        uint[] herbEntries = [PeacebloomEntry, SilverleafEntry, EarthrootEntry];

        await PrepareHerbalism(account, label);

        await _bot.RefreshSnapshotsAsync();
        uint skillBefore = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"{label} initial herbalism skill: {skillBefore}");

        await StageAtDurotarHerbRouteStartAsync(account, label);
        var bagBefore = CaptureBagItemCounts(label);
        var diagStart = DateTime.UtcNow;
        await _bot.SendActionAndWaitAsync(
            account,
            BuildGatheringRouteAction(HerbalismGatherSpell, herbEntries, herbCandidates),
            delayMs: 500);
        bool gathered = await WaitForGatheringRouteOutcomeAsync(
            label, GatheringData.HERBALISM_SKILL_ID, skillBefore, bagBefore, diagStart,
            timeout: TimeSpan.FromMinutes(5));

        await _bot.RefreshSnapshotsAsync();
        uint skillAfter = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"{label} Results: gathered={gathered}, skill {skillBefore} -> {skillAfter}");

        if (skillAfter <= skillBefore)
            _output.WriteLine($"{label}: WARNING - Herbalism skill did not increase. RNG skill-up mechanic.");

        return gathered;
    }

    // =====================================================================
    //  MINING TESTS
    // =====================================================================

    [SkippableFact]
    public async Task Mining_BG_GatherCopperVein()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);

        await _bot.RefreshSnapshotsAsync();
        uint skillBefore = GetSkill("BG", GatheringData.MINING_SKILL_ID);
        global::Tests.Infrastructure.Skip.If(skillBefore >= 300, $"BG mining skill already capped ({skillBefore}).");

        var valleyCandidates = await LoadValleyCopperCandidatesAsync();

        try
        {
            bool gathered = await RunMiningScenarioAsync(bgAccount, "BG", valleyCandidates);
            Assert.True(gathered,
                $"BG: Failed to gather Copper Vein on the Valley copper route ({valleyCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await ReturnToSafeZoneAsync(bgAccount, "BG");
        }
    }

    [SkippableFact]
    public async Task Mining_FG_GatherCopperVein()
    {
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(fgAccount == null, "FG bot not available.");
        global::Tests.Infrastructure.Skip.IfNot(await _bot.CheckFgActionableAsync(), "FG bot not actionable.");

        var valleyCandidates = await LoadValleyCopperCandidatesAsync();

        try
        {
            bool gathered = await RunMiningScenarioAsync(fgAccount!, "FG", valleyCandidates);
            Assert.True(gathered,
                $"FG: Failed to gather Copper Vein on the Valley copper route ({valleyCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await ReturnToSafeZoneAsync(fgAccount!, "FG");
        }
    }

    // =====================================================================
    //  HERBALISM TESTS
    // =====================================================================

    [SkippableFact]
    public async Task Herbalism_BG_GatherHerb()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);

        await _bot.RefreshSnapshotsAsync();
        uint skillBefore = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
        global::Tests.Infrastructure.Skip.If(skillBefore >= 300, $"BG herbalism skill already capped ({skillBefore}).");

        var herbCandidates = await LoadDurotarHerbCandidatesAsync();

        try
        {
            bool gathered = await RunHerbalismScenarioAsync(bgAccount, "BG", herbCandidates);
            Assert.True(gathered,
                $"BG: Failed to gather herb on the Durotar herb route ({herbCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await ReturnToSafeZoneAsync(bgAccount, "BG");
        }
    }

    [SkippableFact]
    public async Task Herbalism_FG_GatherHerb()
    {
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(fgAccount == null, "FG bot not available.");
        global::Tests.Infrastructure.Skip.IfNot(await _bot.CheckFgActionableAsync(), "FG bot not actionable.");

        var herbCandidates = await LoadDurotarHerbCandidatesAsync();

        try
        {
            bool gathered = await RunHerbalismScenarioAsync(fgAccount!, "FG", herbCandidates);
            Assert.True(gathered,
                $"FG: Failed to gather herb on the Durotar herb route ({herbCandidates.Count} candidates, confirmed by DB).");
        }
        finally
        {
            await ReturnToSafeZoneAsync(fgAccount!, "FG");
        }
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================
    /// <summary>
    /// Snapshot-driven mining setup: apply only missing preconditions.
    /// Self-targeting is applied before GM commands that require it (.learn, .setskill).
    /// </summary>
    private async Task PrepareMining(string account, string label)
    {
        await EnsureAliveAndAtSetupLocationAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var bagCount = GetBagItemCount(label);
        if (bagCount >= 12)
        {
            _output.WriteLine($"[{label}] Clearing bags (count={bagCount}) before mining setup");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await _bot.RefreshSnapshotsAsync();
        }

        // Self-target before GM commands that require it (.learn, .setskill).
        await EnsureSelfSelectionAsync(account);

        var currentMining = GetSkill(label, GatheringData.MINING_SKILL_ID);
        _output.WriteLine($"[{label}] Ensuring mining spells learned (current skill={currentMining})");
        await _bot.BotLearnSpellAsync(account, MiningApprentice);
        await _bot.BotLearnSpellAsync(account, MiningGatherSpell);
        if (currentMining < 1)
        {
            var setSkillTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".setskill {GatheringData.MINING_SKILL_ID} 1 300", captureResponse: true);
            AssertCommandSucceeded(setSkillTrace, label, $".setskill {GatheringData.MINING_SKILL_ID} 1 300");
        }

        await _bot.RefreshSnapshotsAsync();
        if (!HasBagItem(label, MiningPick))
        {
            _output.WriteLine($"[{label}] Adding Mining Pick ({MiningPick})");
            await _bot.BotAddItemAsync(account, MiningPick);
            await _bot.WaitForSnapshotConditionAsync(
                account,
                snap => snap?.Player?.BagContents?.Values.Any(v => v == MiningPick) == true,
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 300,
                progressLabel: $"{label} mining-pick-add");
        }

        await _bot.RefreshSnapshotsAsync();
        var skillCheck = GetSkill(label, GatheringData.MINING_SKILL_ID);
        var hasPick = HasBagItem(label, MiningPick);
        _output.WriteLine($"[{label}] Mining setup state: skill={skillCheck}, hasPick={hasPick}");
    }
    /// <summary>
    /// Snapshot-driven herbalism setup: apply only missing preconditions.
    /// Self-targeting is applied before GM commands that require it (.learn, .setskill).
    /// </summary>
    private async Task PrepareHerbalism(string account, string label)
    {
        await EnsureAliveAndAtSetupLocationAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var bagCount = GetBagItemCount(label);
        if (bagCount >= 12)
        {
            _output.WriteLine($"[{label}] Clearing bags (count={bagCount}) before herbalism setup");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await _bot.RefreshSnapshotsAsync();
        }

        // Self-target before GM commands that require it (.learn, .setskill).
        await EnsureSelfSelectionAsync(account);

        var currentHerbalism = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"[{label}] Ensuring herbalism spells learned (current skill={currentHerbalism})");
        await _bot.BotLearnSpellAsync(account, HerbalismApprentice);
        await _bot.BotLearnSpellAsync(account, HerbalismGatherSpell);
        if (currentHerbalism < 1)
        {
            var setSkillTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".setskill {GatheringData.HERBALISM_SKILL_ID} 1 300", captureResponse: true);
            AssertCommandSucceeded(setSkillTrace, label, $".setskill {GatheringData.HERBALISM_SKILL_ID} 1 300");
        }

        await _bot.RefreshSnapshotsAsync();
        var skillCheck = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"[{label}] Herbalism setup state: skill={skillCheck}");
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
        bool sawRouteExhausted = false;
        string recentDiagSummary = "none";

        while (DateTime.UtcNow < endTime)
        {
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();

            var skillNow = GetSkill(label, skillId);
            if (skillNow > initialSkill)
                return true;

            var bagCountsAfter = CaptureBagItemCounts(label);
            bool bagDelta = bagCountsAfter.Any(pair =>
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

    private async Task StageAtValleyCopperRouteStartAsync(string account, string label)
    {
        var characterName = label == "FG" ? _bot.FgCharacterName : _bot.BgCharacterName;
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            _output.WriteLine($"[{label}] Staging via named teleport ValleyOfTrials");
            await _bot.BotTeleportToNamedAsync(account, characterName, "ValleyOfTrials");
        }
        else
        {
            _output.WriteLine(
                $"[{label}] Staging at Valley copper route start " +
                $"({GatheringRouteSelection.ValleyCopperRouteStartX:F1}, {GatheringRouteSelection.ValleyCopperRouteStartY:F1}, {GatheringRouteSelection.ValleyCopperRouteStartZ:F1})");
            await _bot.BotTeleportAsync(
                account,
                GatheringRouteSelection.DurotarMap,
                GatheringRouteSelection.ValleyCopperRouteStartX,
                GatheringRouteSelection.ValleyCopperRouteStartY,
                GatheringRouteSelection.ValleyCopperRouteStartZ);
        }
        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);
    }

    private async Task StageAtDurotarHerbRouteStartAsync(string account, string label)
    {
        _output.WriteLine(
            $"[{label}] Staging at Durotar herb route start " +
            $"({GatheringRouteSelection.DurotarHerbRouteStartX:F1}, {GatheringRouteSelection.DurotarHerbRouteStartY:F1}, {GatheringRouteSelection.DurotarHerbRouteStartZ:F1})");
        await _bot.BotTeleportAsync(
            account,
            GatheringRouteSelection.DurotarMap,
            GatheringRouteSelection.DurotarHerbRouteStartX,
            GatheringRouteSelection.DurotarHerbRouteStartY,
            GatheringRouteSelection.DurotarHerbRouteStartZ);
        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);
    }

    private async Task EnsureAliveAndAtSetupLocationAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = GetSnapshot(label);
        var health = snap?.Player?.Unit?.Health ?? 0;
        var playerFlags = snap?.Player?.PlayerFlags ?? 0;
        var isGhost = (playerFlags & 0x10) != 0;

        if (health == 0 || isGhost)
        {
            var charName = label == "FG" ? _bot.FgCharacterName : _bot.BgCharacterName;
            if (!string.IsNullOrEmpty(charName))
            {
                _output.WriteLine($"[{label}] Reviving character before setup");
                await _bot.RevivePlayerAsync(charName);
                await _bot.WaitForSnapshotConditionAsync(
                    account, LiveBotFixture.IsStrictAlive,
                    TimeSpan.FromSeconds(5),
                    progressLabel: $"{label} revive");
                await _bot.RefreshSnapshotsAsync();
                snap = GetSnapshot(label);
            }
        }

        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var needsTeleport = true;
        if (pos != null)
        {
            var distToOrg = LiveBotFixture.Distance3D(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ);
            needsTeleport = distToOrg > 80f;
        }

        if (needsTeleport)
        {
            _output.WriteLine($"[{label}] Moving to safe setup zone (Orgrimmar)");
            await _bot.BotTeleportAsync(account, OrgrimmarMap, OrgX, OrgY, OrgZ);
            await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);
        }
    }

    /// <summary>
    /// Select self before GM commands like .setskill that require a target.
    /// Uses the proper .targetself chat command instead of the melee-attack workaround.
    /// Works for both BG (headless) and FG (injected) bots.
    /// </summary>
    private async Task EnsureSelfSelectionAsync(string account)
    {
        await _bot.BotSelectSelfAsync(account);
    }

    private int GetBagItemCount(string label)
        => GetSnapshot(label)?.Player?.BagContents?.Count ?? 0;

    private bool HasBagItem(string label, uint itemId)
    {
        var bags = GetSnapshot(label)?.Player?.BagContents;
        return bags != null && bags.Values.Any(v => v == itemId);
    }

    /// <summary>
    /// Best-effort return to safe zone (Orgrimmar) after test completion or failure.
    /// Prevents leaving the bot stranded at a remote gathering location.
    /// </summary>
    private async Task ReturnToSafeZoneAsync(string account, string label)
    {
        try
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null)
            {
                var distToOrg = LiveBotFixture.Distance3D(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ);
                if (distToOrg > 80f)
                {
                    _output.WriteLine($"[{label}] Cleanup: returning to Orgrimmar (dist={distToOrg:F0}y)");
                    await _bot.BotTeleportAsync(account, OrgrimmarMap, OrgX, OrgY, OrgZ);
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[{label}] Cleanup warning: safe zone return failed — {ex.Message}");
        }
    }

    private uint GetSkill(string label, uint skillId)
    {
        var snap = GetSnapshot(label);
        var skillMap = snap?.Player?.SkillInfo;
        if (skillMap != null && skillMap.TryGetValue(skillId, out uint level))
            return level;
        return 0;
    }

    private WoWActivitySnapshot? GetSnapshot(string label)
        => label == "FG" ? _bot.ForegroundBot : _bot.BackgroundBot;

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);
        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
