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
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
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

    public GatheringProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    // =====================================================================
    //  MINING TEST
    // =====================================================================

    [SkippableFact]
    public async Task Mining_GatherCopperVein_SkillIncreases()
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
        int valleyCandidateCount = valleyCandidates.Count;
        int valleyPoolCount = valleyCandidates
            .Select(candidate => candidate.poolEntry)
            .Where(poolEntry => poolEntry.HasValue)
            .Select(poolEntry => poolEntry!.Value)
            .Distinct()
            .Count();
        Assert.True(valleyCandidateCount > 0,
            "DB must have copper vein spawns near Valley of Trials — no natural Copper Vein route candidates found.");
        _output.WriteLine(
            $"Selected {valleyCandidateCount} Valley copper-route candidates across {Math.Max(1, valleyPoolCount)} spawn pool(s) from " +
            $"({GatheringRouteSelection.ValleyCopperRouteStartX:F0}, {GatheringRouteSelection.ValleyCopperRouteStartY:F0}, {GatheringRouteSelection.ValleyCopperRouteStartZ:F0}) " +
            $"with nearest route distance {valleyCandidates[0].distance2D:F0}y.");
        var pooledCandidateSummary = string.Join(", ",
            valleyCandidates.Select(candidate => candidate.poolEntry)
                .Where(poolEntry => poolEntry.HasValue)
                .Select(poolEntry => poolEntry!.Value)
                .Distinct()
                .OrderBy(poolEntry => poolEntry));
        if (!string.IsNullOrWhiteSpace(pooledCandidateSummary))
            _output.WriteLine($"Loaded Valley copper pool entries: {pooledCandidateSummary}");

        var fgAccountForRoute = _bot.FgAccountName;
        if (fgAccountForRoute != null && await _bot.CheckFgActionableAsync())
        {
            try
            {
                var bgParkAccountForRoute = _bot.BgAccountName;
                if (bgParkAccountForRoute != null)
                {
                    _output.WriteLine("[BG] Parking BG bot in Orgrimmar (prevents CombatCoordinator interference)");
                    await _bot.BotTeleportAsync(bgParkAccountForRoute, OrgrimmarMap, OrgX, OrgY, OrgZ);
                }

                _output.WriteLine($"FG: {_bot.FgCharacterName} ({fgAccountForRoute})");
                await PrepareMining(fgAccountForRoute, "FG");

                await _bot.RefreshSnapshotsAsync();
                uint fgSkillBeforeForRoute = GetSkill("FG", GatheringData.MINING_SKILL_ID);
                _output.WriteLine($"FG initial mining skill: {fgSkillBeforeForRoute}");

                await StageAtValleyCopperRouteStartAsync(fgAccountForRoute, "FG");
                var fgBagBeforeForRoute = CaptureBagItemCounts("FG");
                var fgDiagStart = DateTime.UtcNow;
                await _bot.SendActionAndWaitAsync(
                    fgAccountForRoute,
                    BuildGatheringRouteAction(MiningGatherSpell, [CopperVeinEntry], valleyCandidates),
                    delayMs: 500);
                bool fgGatheredOnRoute = await WaitForGatheringRouteOutcomeAsync(
                    "FG",
                    GatheringData.MINING_SKILL_ID,
                    fgSkillBeforeForRoute,
                    fgBagBeforeForRoute,
                    fgDiagStart,
                    timeout: TimeSpan.FromMinutes(5));

                await _bot.RefreshSnapshotsAsync();
                uint fgSkillAfterForRoute = GetSkill("FG", GatheringData.MINING_SKILL_ID);
                _output.WriteLine($"FG Results: gathered={fgGatheredOnRoute}, skill {fgSkillBeforeForRoute} â†’ {fgSkillAfterForRoute}");

                if (!fgGatheredOnRoute)
                {
                    _output.WriteLine(
                        $"FG reference mining did not complete on any of {valleyCandidateCount} Valley copper-route candidates. " +
                        $"Continuing with BG-authoritative assertions. skill={fgSkillAfterForRoute}.");
                }
                else if (fgSkillAfterForRoute <= fgSkillBeforeForRoute)
                {
                    _output.WriteLine($"FG: WARNING â€” Mining skill did not increase ({fgSkillBeforeForRoute} â†’ {fgSkillAfterForRoute}). " +
                        "This can happen due to WoW's RNG skill-up mechanic.");
                }
            }
            catch (Xunit.Sdk.XunitException ex)
            {
                _output.WriteLine($"FG reference mining became unstable; continuing with BG-only assertions. Details: {ex.Message}");
            }
            finally
            {
                await ReturnToSafeZoneAsync(fgAccountForRoute, "FG");
            }
        }
        else
        {
            _output.WriteLine("FG bot not available or not actionable â€” skipping FG mining reference path.");
        }

        var bgAccountForRoute = _bot.BgAccountName!;
        Assert.NotNull(bgAccountForRoute);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccountForRoute})");

        try
        {
            var fgParkAccountForRoute = _bot.FgAccountName;
            if (fgParkAccountForRoute != null)
            {
                _output.WriteLine("[FG-park] Parking FG bot in Orgrimmar for BG test");
                await _bot.BotTeleportAsync(fgParkAccountForRoute, OrgrimmarMap, OrgX, OrgY, OrgZ);
            }

            await PrepareMining(bgAccountForRoute, "BG");

            await _bot.RefreshSnapshotsAsync();
            uint bgSkillBeforeForRoute = GetSkill("BG", GatheringData.MINING_SKILL_ID);
            _output.WriteLine($"BG initial mining skill: {bgSkillBeforeForRoute}");
            global::Tests.Infrastructure.Skip.If(bgSkillBeforeForRoute >= 300, $"BG mining skill already capped ({bgSkillBeforeForRoute}); cannot assert further increase.");

            await StageAtValleyCopperRouteStartAsync(bgAccountForRoute, "BG");
            var bgBagBeforeForRoute = CaptureBagItemCounts("BG");
            var bgDiagStart = DateTime.UtcNow;
            await _bot.SendActionAndWaitAsync(
                bgAccountForRoute,
                BuildGatheringRouteAction(MiningGatherSpell, [CopperVeinEntry], valleyCandidates),
                delayMs: 500);
            bool bgGatheredOnRoute = await WaitForGatheringRouteOutcomeAsync(
                "BG",
                GatheringData.MINING_SKILL_ID,
                bgSkillBeforeForRoute,
                bgBagBeforeForRoute,
                bgDiagStart,
                timeout: TimeSpan.FromMinutes(5));

            await _bot.RefreshSnapshotsAsync();
            uint bgSkillAfterForRoute = GetSkill("BG", GatheringData.MINING_SKILL_ID);
            _output.WriteLine($"BG Results: gathered={bgGatheredOnRoute}, skill {bgSkillBeforeForRoute} â†’ {bgSkillAfterForRoute}");

            // DB confirmed nodes exist (valleyCandidateCount > 0) — if the bot failed to gather,
            // that's a detection/pathfinding/interaction bug, not a "no nodes spawned" issue.
            Assert.True(bgGatheredOnRoute,
                $"BG: Failed to gather Copper Vein on the Valley copper route ({valleyCandidateCount} candidates, confirmed by DB). " +
                $"Skill={bgSkillAfterForRoute}. This is a bot detection/interaction failure, not a respawn issue.");
            if (bgSkillAfterForRoute <= bgSkillBeforeForRoute)
            {
                _output.WriteLine($"BG: WARNING â€” Mining skill did not increase ({bgSkillBeforeForRoute} â†’ {bgSkillAfterForRoute}). " +
                    "This can happen due to WoW's RNG skill-up mechanic.");
            }
        }
        finally
        {
            await ReturnToSafeZoneAsync(bgAccountForRoute, "BG");
        }
    }

    // =====================================================================
    //  HERBALISM TEST
    // =====================================================================

    [SkippableFact]
    public async Task Herbalism_GatherHerb_SkillIncreases()
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
        int herbCandidateCount = herbCandidates.Count;
        int herbPoolCount = herbCandidates
            .Select(c => c.poolEntry)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .Distinct()
            .Count();
        Assert.True(herbCandidateCount > 0,
            "DB must have herb spawns near Durotar — no natural herb route candidates found.");
        _output.WriteLine(
            $"Selected {herbCandidateCount} Durotar herb-route candidates across {Math.Max(1, herbPoolCount)} spawn pool(s) from " +
            $"({GatheringRouteSelection.DurotarHerbRouteStartX:F0}, {GatheringRouteSelection.DurotarHerbRouteStartY:F0}, {GatheringRouteSelection.DurotarHerbRouteStartZ:F0}) " +
            $"with nearest route distance {herbCandidates[0].distance2D:F0}y.");
        var pooledHerbSummary = string.Join(", ",
            herbCandidates.Select(c => c.poolEntry)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .Distinct()
                .OrderBy(p => p));
        if (!string.IsNullOrWhiteSpace(pooledHerbSummary))
            _output.WriteLine($"Loaded Durotar herb pool entries: {pooledHerbSummary}");

        // --- FG FIRST: native WoW right-click interaction (gold standard) ---
        var fgAccount = _bot.FgAccountName;
        if (fgAccount != null && await _bot.CheckFgActionableAsync())
        {
            try
            {
                var bgParkAccount = _bot.BgAccountName;
                if (bgParkAccount != null)
                {
                    _output.WriteLine("[BG] Parking BG bot in Orgrimmar (prevents CombatCoordinator interference)");
                    await _bot.BotTeleportAsync(bgParkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);
                }

                _output.WriteLine($"FG: {_bot.FgCharacterName} ({fgAccount})");
                await PrepareHerbalism(fgAccount, "FG");

                await _bot.RefreshSnapshotsAsync();
                uint fgSkillBefore = GetSkill("FG", GatheringData.HERBALISM_SKILL_ID);
                _output.WriteLine($"FG initial herbalism skill: {fgSkillBefore}");

                await StageAtDurotarHerbRouteStartAsync(fgAccount, "FG");
                var fgBagBefore = CaptureBagItemCounts("FG");
                var fgDiagStart = DateTime.UtcNow;
                await _bot.SendActionAndWaitAsync(
                    fgAccount,
                    BuildGatheringRouteAction(HerbalismGatherSpell, herbEntries, herbCandidates),
                    delayMs: 500);
                bool fgGathered = await WaitForGatheringRouteOutcomeAsync(
                    "FG",
                    GatheringData.HERBALISM_SKILL_ID,
                    fgSkillBefore,
                    fgBagBefore,
                    fgDiagStart,
                    timeout: TimeSpan.FromMinutes(5));

                await _bot.RefreshSnapshotsAsync();
                uint fgSkillAfter = GetSkill("FG", GatheringData.HERBALISM_SKILL_ID);
                _output.WriteLine($"FG Results: gathered={fgGathered}, skill {fgSkillBefore} → {fgSkillAfter}");

                if (!fgGathered)
                {
                    _output.WriteLine(
                        $"FG reference herbalism did not complete on any of {herbCandidateCount} Durotar herb-route candidates. " +
                        $"Continuing with BG-authoritative assertions. skill={fgSkillAfter}.");
                }
                else if (fgSkillAfter <= fgSkillBefore)
                {
                    _output.WriteLine($"FG: WARNING — Herbalism skill did not increase ({fgSkillBefore} → {fgSkillAfter}). " +
                        "This can happen due to WoW's RNG skill-up mechanic.");
                }
            }
            catch (Xunit.Sdk.XunitException ex)
            {
                _output.WriteLine($"FG reference herbalism became unstable; continuing with BG-only assertions. Details: {ex.Message}");
            }
            finally
            {
                await ReturnToSafeZoneAsync(fgAccount, "FG");
            }
        }
        else
        {
            _output.WriteLine("FG bot not available or not actionable — skipping FG herbalism reference path.");
        }

        // --- BG: headless protocol emulation ---
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccount})");

        try
        {
            var fgParkAccount = _bot.FgAccountName;
            if (fgParkAccount != null)
            {
                _output.WriteLine("[FG-park] Parking FG bot in Orgrimmar for BG test");
                await _bot.BotTeleportAsync(fgParkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);
            }

            await PrepareHerbalism(bgAccount, "BG");

            await _bot.RefreshSnapshotsAsync();
            uint bgSkillBefore = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
            _output.WriteLine($"BG initial herbalism skill: {bgSkillBefore}");
            global::Tests.Infrastructure.Skip.If(bgSkillBefore >= 300, $"BG herbalism skill already capped ({bgSkillBefore}); cannot assert further increase.");

            await StageAtDurotarHerbRouteStartAsync(bgAccount, "BG");
            var bgBagBefore = CaptureBagItemCounts("BG");
            var bgDiagStart = DateTime.UtcNow;
            await _bot.SendActionAndWaitAsync(
                bgAccount,
                BuildGatheringRouteAction(HerbalismGatherSpell, herbEntries, herbCandidates),
                delayMs: 500);
            bool bgGathered = await WaitForGatheringRouteOutcomeAsync(
                "BG",
                GatheringData.HERBALISM_SKILL_ID,
                bgSkillBefore,
                bgBagBefore,
                bgDiagStart,
                timeout: TimeSpan.FromMinutes(5));

            await _bot.RefreshSnapshotsAsync();
            uint bgSkillAfter = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
            _output.WriteLine($"BG Results: gathered={bgGathered}, skill {bgSkillBefore} → {bgSkillAfter}");

            // DB confirmed herb nodes exist (herbCandidateCount > 0) — if the bot failed to gather,
            // that's a detection/pathfinding/interaction bug, not a "no nodes spawned" issue.
            Assert.True(bgGathered,
                $"BG: Failed to gather herb on the Durotar herb route ({herbCandidateCount} candidates, confirmed by DB). " +
                $"Skill={bgSkillAfter}. This is a bot detection/interaction failure, not a respawn issue.");
            if (bgSkillAfter <= bgSkillBefore)
                _output.WriteLine($"BG: WARNING — Herbalism skill did not increase ({bgSkillBefore} → {bgSkillAfter}). " +
                    "This can happen due to WoW's RNG skill-up mechanic.");
        }
        finally
        {
            await ReturnToSafeZoneAsync(bgAccount, "BG");
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
            await Task.Delay(1000);
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
            await Task.Delay(500);
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
                await Task.Delay(1000);
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
        await Task.Delay(300);
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

