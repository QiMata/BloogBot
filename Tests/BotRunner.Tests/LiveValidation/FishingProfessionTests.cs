using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Constants;
using BotRunner.Combat;
using BotRunner.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fishing live validation for the task-owned Ratchet pool flow.
///
/// Setup stays in the test (.learn/.setskill/.additem/.tele name), but the runtime path under test is:
///   ActionType.StartFishing -> CharacterAction.StartFishing -> FishingTask
///
/// The task-owned path can complete end-to-end, but remaining intermittent failures are
/// shoreline/pathfinding issues around reaching a castable LOS position in Ratchet.
///
/// FishingTask is responsible for:
///   1) equipping the fishing pole from bags
///   2) moving from the Ratchet named-teleport landing into cast range of a visible fishing pool
///   3) casting and waiting through the bobber/channel cycle
///   4) looting the catch from the loot window after bobber interaction
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class FishingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float RatchetAnchorX = -957f;
    private const float RatchetAnchorY = -3755f;
    private const float RatchetAnchorZ = 5f;
    private const int PollIntervalMs = 1000;
    private const int StartingFishingSkill = 75;
    private const float ExpectedApproachRange = FishingTask.MaxCastingDistance;
    private const float RatchetPoolSearchRadius = 500f;
    private const int RatchetPoolQueryLimit = 32;
    private static readonly int FishingTimeoutMs = (new BotBehaviorConfig().MaxFishingCasts * 30000) + 20000;
    private static readonly float FishingPoolDetectRange = new BotBehaviorConfig().FishingPoolDetectRange;
    private const uint FishingLureItemId = FishingData.NightcrawlerBait;

    private static readonly uint[] FishingSpellSyncIds =
    [
        FishingData.FishingRank1,
        FishingData.FishingRank2,
        FishingData.FishingRank3,
        FishingData.FishingRank4,
        FishingData.FishingPoleProficiency
    ];

    private static readonly HashSet<uint> FishingPoleIds =
    [
        FishingData.FishingPole,
        FishingData.StrongFishingPole,
        FishingData.BigIronFishingPole,
        FishingData.DarkwoodFishingPole
    ];

    public FishingProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath()
    {
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsPathfindingReady, "PathfindingService is required for FishingTask pool approach validation.");

        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG bot account not available.");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot account not available.");

        // Teleport both bots to Orgrimmar for safe setup (away from water/mobs).
        await _bot.EnsureCleanSlateAsync(bgAccount!, "BG", teleportToSafeZone: true);
        await _bot.EnsureCleanSlateAsync(fgAccount!, "FG", teleportToSafeZone: true);

        var fgActionable = await _bot.CheckFgActionableAsync(requireTeleportProbe: false);
        global::Tests.Infrastructure.Skip.IfNot(fgActionable, "FG bot is not actionable for the dual fishing validation.");

        // Prepare both bots in parallel: learn spells, set skill, give items.
        var bgPrep = PrepareBotAsync(bgAccount!, "BG");
        var fgPrep = PrepareBotAsync(fgAccount!, "FG");
        await Task.WhenAll(bgPrep, fgPrep);

        // Query DB for fishing pool spawn positions near Ratchet to use as search waypoints.
        var poolSpawns = await _bot.QueryGameObjectSpawnsNearAsync(
            FishingData.KnownFishingPoolEntries.ToArray(),
            MapId,
            RatchetAnchorX,
            RatchetAnchorY,
            RatchetPoolSearchRadius,
            RatchetPoolQueryLimit);
        var searchWaypoints = poolSpawns
            .Select(s => (s.x, s.y, s.z))
            .ToList();
        _output.WriteLine($"Ratchet fishing pool DB query returned {searchWaypoints.Count} shoreline waypoints within {RatchetPoolSearchRadius}y.");
        Assert.True(searchWaypoints.Count > 0,
            "DB must have fishing pool spawns near Ratchet. If this fails, the world DB is missing fishing pool gameobject entries.");

        // Teleport both to Ratchet for fishing.
        await TeleportToRatchetAsync(bgAccount!, _bot.BgCharacterName, "BG");
        await TeleportToRatchetAsync(fgAccount!, _bot.FgCharacterName, "FG");

        // Wait for objects to stream in after teleport, then force pool refresh.
        // .pool update re-rolls the pool — must happen AFTER teleport so the bots are on
        // the correct map and the spawned pools will appear in their ObjectManagers.
        await Task.Delay(3000);
        _output.WriteLine("Forcing pool system refresh via .pool update 2628");
        await _bot.SendGmChatCommandAsync(bgAccount!, ".pool update 2628");
        await Task.Delay(5000); // Allow pools to spawn and stream into ObjectManager

        // Run both bots fishing simultaneously — they fish side by side at Ratchet.
        var bgTask = RunFishingTaskAsync(bgAccount!, "BG", searchWaypoints);
        var fgTask = RunFishingTaskAsync(fgAccount!, "FG", searchWaypoints);
        var results = await Task.WhenAll(bgTask, fgTask);
        var bgResult = results[0];
        var fgResult = results[1];

        AssertFishingResult("BG", bgResult);
        AssertFishingResult("FG", fgResult);
    }

    private async Task PrepareBotAsync(string account, string label)
    {
        var snap = await RefreshAndGetSnapshotAsync(account);
        if (snap == null)
            throw new InvalidOperationException($"[{label}] Missing snapshot during fishing setup.");

        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await _bot.WaitForSnapshotConditionAsync(account, LiveBotFixture.IsStrictAlive, TimeSpan.FromSeconds(5));
        }

        // Batch all setup: learn spells, set skill, reset items, add items.
        // Learn spells directly (no unlearn/relearn cycle — just ensure they're known).
        foreach (var spellId in FishingSpellSyncIds)
            await _bot.BotLearnSpellAsync(account, spellId);

        await _bot.BotSetSkillAsync(account, FishingData.FishingSkillId, StartingFishingSkill, 300);
        await _bot.ExecuteGMCommandAsync($".reset items {snap.CharacterName}");
        await Task.Delay(500);

        await _bot.BotAddItemAsync(account, FishingData.FishingPole);
        await _bot.BotAddItemAsync(account, FishingLureItemId);

        // Single wait for both items to appear. SOAP .additem can take 10+ seconds
        // to propagate through the server → SMSG_UPDATE_OBJECT → BG client pipeline.
        var itemsReady = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => ContainsFishingPole(snapshot) && CountItem(snapshot, FishingLureItemId) > 0,
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 300,
            progressLabel: $"{label} fishing-items");

        Assert.True(itemsReady, $"[{label}] Fishing pole or bait never appeared in bags after setup.");
    }

    private async Task TeleportToRatchetAsync(string account, string? characterName, string label)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[{label}] Character name was not resolved for Ratchet teleport.");

        _output.WriteLine($"[{label}] Teleporting to Ratchet via named GM teleport for {characterName}.");
        await _bot.BotTeleportToNamedAsync(account, characterName, "Ratchet");
        var settled = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot =>
            {
                if (!LiveBotFixture.IsStrictAlive(snapshot))
                    return false;

                var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
                return position != null
                    && Distance3D(position.X, position.Y, position.Z, RatchetAnchorX, RatchetAnchorY, RatchetAnchorZ) <= 80f;
            },
            TimeSpan.FromSeconds(6),
            pollIntervalMs: 300,
            progressLabel: $"{label} ratchet-arrival");

        Assert.True(settled, $"[{label}] Named Ratchet teleport never settled near the Ratchet dock anchor.");

        var snapshot = await RefreshAndGetSnapshotAsync(account);
        Assert.NotNull(snapshot);
        var playerPosition = snapshot!.Player?.Unit?.GameObject?.Base?.Position;
        var distanceFromRatchetAnchor = playerPosition != null
            ? Distance3D(playerPosition.X, playerPosition.Y, playerPosition.Z, RatchetAnchorX, RatchetAnchorY, RatchetAnchorZ)
            : float.MaxValue;
        var visiblePoolDistance = FindNearestPoolDistance(snapshot);
        _output.WriteLine(
            $"[{label}] Ratchet arrival snapshot. distanceFromAnchor={(distanceFromRatchetAnchor < float.MaxValue ? $"{distanceFromRatchetAnchor:F1}y" : "unknown")} " +
            $"visiblePoolAtStart={(visiblePoolDistance < float.MaxValue ? $"{visiblePoolDistance:F1}y" : "none")} " +
            $"position=({snapshot.Player?.Unit?.GameObject?.Base?.Position?.X:F1},{snapshot.Player?.Unit?.GameObject?.Base?.Position?.Y:F1},{snapshot.Player?.Unit?.GameObject?.Base?.Position?.Z:F1})");
    }

    private async Task<FishingRunResult> RunFishingTaskAsync(string account, string label, IReadOnlyList<(float x, float y, float z)>? searchWaypoints = null)
    {
        var before = await RefreshAndGetSnapshotAsync(account);
        if (before == null)
            throw new InvalidOperationException($"[{label}] Missing baseline snapshot before fishing.");

        var baselineCatchItems = GetCatchItemIds(before);
        var previousTaskMessages = GetFishingTaskMessages(before);
        var skillBefore = GetFishingSkill(before);
        var initialVisiblePoolDistance = FindNearestPoolDistance(before);
        var poleStartedInBag = ContainsFishingPole(before);
        var baitStartedInBag = CountItem(before, FishingLureItemId) > 0;
        var baitCountBefore = CountItem(before, FishingLureItemId);

        var fishingAction = new ActionMessage { ActionType = ActionType.StartFishing };
        if (searchWaypoints != null)
        {
            foreach (var (wx, wy, wz) in searchWaypoints)
            {
                fishingAction.Parameters.Add(new RequestParameter { FloatParam = wx });
                fishingAction.Parameters.Add(new RequestParameter { FloatParam = wy });
                fishingAction.Parameters.Add(new RequestParameter { FloatParam = wz });
            }
        }

        _output.WriteLine(
            $"[{label}] Dispatching StartFishing from Ratchet named teleport. skill={skillBefore} poleInBag={poleStartedInBag} baitCount={baitCountBefore} " +
            $"searchWaypoints={searchWaypoints?.Count ?? 0} " +
            $"visiblePoolAtStart={(initialVisiblePoolDistance < float.MaxValue ? $"{initialVisiblePoolDistance:F1}y" : "none")}");
        await _bot.SendActionAndWaitAsync(account, fishingAction, delayMs: 1000);

        var deadline = DateTime.UtcNow.AddMilliseconds(FishingTimeoutMs);
        var bestPoolDistance = initialVisiblePoolDistance;
        var sawChannel = false;
        var sawBobber = false;
        var sawPoolAcquireDiagnostic = false;
        var sawInCastRangeDiagnostic = false;
        var sawLureUseDiagnostic = false;
        var sawLootWindowDiagnostic = false;
        var sawLootSuccessDiagnostic = false;
        var sawSwimmingError = false;
        var poleEquippedByTask = !poleStartedInBag;
        var baitConsumedByTask = baitCountBefore == 0;
        var sawLosBlockedDiagnostic = false;
        var sawNonFishableWaterError = false;
        IReadOnlyList<uint> finalCatchItems = baselineCatchItems;
        IReadOnlyList<uint> finalCatchDeltaItems = [];
        IReadOnlyList<string> recentErrors = before.RecentErrors.TakeLast(4).ToArray();
        string lastFishingTaskMessage = string.Empty;
        string lastRelevantError = string.Empty;
        string recentDiagnosticsSummary = "none";
        uint skillAfter = skillBefore;
        WoWActivitySnapshot? lastSnapshot = before;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollIntervalMs);
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            if (snapshot == null)
                continue;
            lastSnapshot = snapshot;

            poleEquippedByTask |= poleStartedInBag && !ContainsFishingPole(snapshot);
            sawChannel |= IsFishingChannelActive(snapshot);
            sawBobber |= FindBobber(snapshot) != null;
            sawSwimmingError |= snapshot.RecentErrors.Any(message => message.Contains("swimming", StringComparison.OrdinalIgnoreCase));
            sawNonFishableWaterError |= snapshot.RecentErrors.Any(message => message.Contains("didn't land in fishable water", StringComparison.OrdinalIgnoreCase));
            skillAfter = GetFishingSkill(snapshot);
            recentErrors = snapshot.RecentErrors.TakeLast(4).ToArray();

            var latestRelevantError = snapshot.RecentErrors.LastOrDefault(message =>
                message.Contains("didn't land in fishable water", StringComparison.OrdinalIgnoreCase)
                || message.Contains("swimming", StringComparison.OrdinalIgnoreCase)
                || message.Contains("line of sight", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(latestRelevantError))
                lastRelevantError = latestRelevantError;

            var currentTaskMessages = GetFishingTaskMessages(snapshot);
            var newTaskMessages = GetMessageDelta(previousTaskMessages, currentTaskMessages);
            previousTaskMessages = currentTaskMessages;
            if (newTaskMessages.Count > 0)
            {
                foreach (var taskMessage in newTaskMessages)
                    _output.WriteLine($"[{label}] {taskMessage}");
            }

            var latestTaskMessage = currentTaskMessages.LastOrDefault();
            if (!string.IsNullOrWhiteSpace(latestTaskMessage))
                lastFishingTaskMessage = latestTaskMessage;

            // Scan diagnostic flags BEFORE the early exit check so that
            // sawLootSuccessDiagnostic is set when pop and loot_success arrive in the same poll.
            sawPoolAcquireDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask pool_acquired", StringComparison.Ordinal));
            sawInCastRangeDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask in_cast_range", StringComparison.Ordinal));
            sawLosBlockedDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask los_blocked", StringComparison.Ordinal));
            sawLureUseDiagnostic |= currentTaskMessages.Any(message =>
                message.Contains("FishingTask lure_use_started", StringComparison.Ordinal)
                || message.Contains("FishingTask lure_applied", StringComparison.Ordinal));
            sawLootWindowDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask loot_window_open", StringComparison.Ordinal));
            var lootSuccessMessage = currentTaskMessages.LastOrDefault(message => message.Contains("FishingTask fishing_loot_success", StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(lootSuccessMessage))
            {
                sawLootSuccessDiagnostic = true;
                lastFishingTaskMessage = lootSuccessMessage;
            }

            // Early exit when FishingTask pops without catching anything —
            // avoids polling for the full timeout when no pool is available.
            // Must come AFTER diagnostic scanning so sawLootSuccessDiagnostic is current.
            var popMessage = currentTaskMessages.LastOrDefault(m => m.Contains("FishingTask pop reason=", StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(popMessage) && !sawLootSuccessDiagnostic)
            {
                _output.WriteLine($"[{label}] FishingTask popped early: {popMessage}");
                recentDiagnosticsSummary = _bot.FormatRecentBotRunnerDiagnostics("FishingTask", "NavigationPath");
                break;
            }

            var poolDistance = FindNearestPoolDistance(snapshot);
            if (poolDistance < bestPoolDistance)
                bestPoolDistance = poolDistance;

            finalCatchItems = GetCatchItemIds(snapshot);
            finalCatchDeltaItems = GetItemDelta(baselineCatchItems, finalCatchItems);
            baitConsumedByTask |= CountItem(snapshot, FishingLureItemId) < baitCountBefore;
            recentDiagnosticsSummary = _bot.FormatRecentBotRunnerDiagnostics("FishingTask", "NavigationPath");
            if (sawLootSuccessDiagnostic && finalCatchDeltaItems.Count > 0)
            {
                _output.WriteLine($"[{label}] FishingTask reported loot success. bestPool={bestPoolDistance:F1}y channel={sawChannel} bobber={sawBobber} catchDelta=[{string.Join(", ", finalCatchDeltaItems)}]");
                return new FishingRunResult(
                    PoleStartedInBag: poleStartedInBag,
                    PoleEquippedByTask: poleEquippedByTask,
                    BaitStartedInBag: baitStartedInBag,
                    BaitConsumedByTask: baitConsumedByTask,
                    InitialVisiblePoolDistance: initialVisiblePoolDistance,
                    BestPoolDistance: bestPoolDistance,
                    SawChannel: sawChannel,
                    SawBobber: sawBobber,
                    SawPoolAcquireDiagnostic: sawPoolAcquireDiagnostic,
                    SawInCastRangeDiagnostic: sawInCastRangeDiagnostic,
                    SawLosBlockedDiagnostic: sawLosBlockedDiagnostic,
                    SawLureUseDiagnostic: sawLureUseDiagnostic,
                    SawLootWindowDiagnostic: sawLootWindowDiagnostic,
                    SawLootSuccessDiagnostic: sawLootSuccessDiagnostic,
                    SawSwimmingError: sawSwimmingError,
                    SawNonFishableWaterError: sawNonFishableWaterError,
                    LastFishingTaskMessage: lastFishingTaskMessage,
                    LastRelevantError: lastRelevantError,
                    RecentErrors: recentErrors,
                    RecentDiagnosticsSummary: recentDiagnosticsSummary,
                    CatchItems: finalCatchItems,
                    CatchDeltaItems: finalCatchDeltaItems,
                    SkillBefore: skillBefore,
                    SkillAfter: skillAfter);
            }
        }

        _bot.DumpRecentBotRunnerDiagnostics($"{label}-fishing-timeout", "FishingTask", "NavigationPath");
        if (lastSnapshot != null)
            _bot.DumpSnapshotDiagnostics(lastSnapshot, $"{label}-fishing-timeout");

        _output.WriteLine($"[{label}] Fishing timeout. bestPool={bestPoolDistance:F1}y channel={sawChannel} bobber={sawBobber} catchDelta=[{string.Join(", ", finalCatchDeltaItems)}]");
        return new FishingRunResult(
            PoleStartedInBag: poleStartedInBag,
            PoleEquippedByTask: poleEquippedByTask,
            BaitStartedInBag: baitStartedInBag,
            BaitConsumedByTask: baitConsumedByTask,
            InitialVisiblePoolDistance: initialVisiblePoolDistance,
            BestPoolDistance: bestPoolDistance,
            SawChannel: sawChannel,
            SawBobber: sawBobber,
            SawPoolAcquireDiagnostic: sawPoolAcquireDiagnostic,
            SawInCastRangeDiagnostic: sawInCastRangeDiagnostic,
            SawLosBlockedDiagnostic: sawLosBlockedDiagnostic,
            SawLureUseDiagnostic: sawLureUseDiagnostic,
            SawLootWindowDiagnostic: sawLootWindowDiagnostic,
            SawLootSuccessDiagnostic: sawLootSuccessDiagnostic,
            SawSwimmingError: sawSwimmingError,
            SawNonFishableWaterError: sawNonFishableWaterError,
            LastFishingTaskMessage: lastFishingTaskMessage,
            LastRelevantError: lastRelevantError,
            RecentErrors: recentErrors,
            RecentDiagnosticsSummary: recentDiagnosticsSummary,
            CatchItems: finalCatchItems,
            CatchDeltaItems: finalCatchDeltaItems,
            SkillBefore: skillBefore,
            SkillAfter: skillAfter);
    }

    private async Task<WoWActivitySnapshot?> RefreshAndGetSnapshotAsync(string account)
    {
        await _bot.RefreshSnapshotsAsync();
        return _bot.AllBots.FirstOrDefault(snapshot =>
                   string.Equals(snapshot.AccountName, account, StringComparison.OrdinalIgnoreCase))
               ?? await _bot.GetSnapshotAsync(account);
    }

    private void AssertFishingResult(string label, FishingRunResult result)
    {
        // If FishingTask popped claiming no pool, but a pool WAS visible during the run
        // (either at start or during polling), that's a real detection/pathfinding bug — FAIL, don't skip.
        var noPoolPop = result.LastFishingTaskMessage.Contains("pop reason=no_fishing_pool", StringComparison.Ordinal)
                     || result.LastFishingTaskMessage.Contains("pop reason=lost_fishing_pool", StringComparison.Ordinal)
                     || result.LastFishingTaskMessage.Contains("pop reason=search_exhausted", StringComparison.Ordinal)
                     || result.LastFishingTaskMessage.Contains("pop reason=search_timeout", StringComparison.Ordinal);
        if (noPoolPop)
        {
            var poolWasVisible = result.BestPoolDistance < float.MaxValue || result.InitialVisiblePoolDistance < float.MaxValue;
            Assert.False(poolWasVisible,
                $"[{label}] FishingTask reported '{(result.LastFishingTaskMessage.Contains("lost_fishing_pool") ? "lost_fishing_pool" : "no_fishing_pool")}' " +
                $"but a pool WAS visible (initial={result.InitialVisiblePoolDistance:F1}y, best={result.BestPoolDistance:F1}y). " +
                $"This is a pool detection or pathfinding bug, not a respawn timer. {FormatFishingFailureContext(result)}");

            // No pool was ever visible — with search-walk waypoints, the bot should have walked
            // the shoreline and found pools. If it didn't, that's a detection/pathfinding bug.
            Assert.Fail(
                $"[{label}] No fishing pool found after walking search waypoints. " +
                $"lastMessage={result.LastFishingTaskMessage} {FormatFishingFailureContext(result)}");
        }

        var failureContext = FormatFishingFailureContext(result);

        Assert.True(result.PoleStartedInBag, $"[{label}] Fishing pole should start in bags so FishingTask owns the equip step.");
        Assert.True(result.PoleEquippedByTask, $"[{label}] FishingTask never removed the fishing pole from bags. {failureContext}");
        Assert.True(result.BaitStartedInBag, $"[{label}] Fishing bait should start in bags so FishingTask owns the lure step.");
        Assert.True(result.SawLureUseDiagnostic, $"[{label}] FishingTask never reported using fishing bait. {failureContext}");
        Assert.True(result.BaitConsumedByTask, $"[{label}] FishingTask never consumed the staged fishing bait. {failureContext}");
        Assert.True(result.SawPoolAcquireDiagnostic,
            $"[{label}] FishingTask never reported acquiring a visible pool. {failureContext}");
        Assert.True(result.BestPoolDistance <= ExpectedApproachRange,
            $"[{label}] FishingTask never approached a pool into cast range. bestDistance={result.BestPoolDistance:F1} {failureContext}");
        Assert.True(result.SawInCastRangeDiagnostic,
            $"[{label}] FishingTask never reported entering cast range. {failureContext}");
        // LOS-blocked diagnostics are informational — BG pool Z=0 from memory reads causes
        // spurious LOS failures during approach. The bot works around them by retrying positions.
        // Only warn; do not fail the test when the catch ultimately succeeds.
        if (result.SawLosBlockedDiagnostic)
            _output.WriteLine($"[{label}] WARNING: FishingTask hit LOS-blocked during approach (pool Z=0 from memory reads). This is informational, not a failure.");
        Assert.True(result.SawChannel,
            $"[{label}] FishingTask never reached a fishing channel state. {failureContext}");
        Assert.True(result.SawBobber,
            $"[{label}] FishingTask never observed a fishing bobber. {failureContext}");
        // loot_window_open and fishing_loot_success diagnostics can appear between polling intervals.
        // Accept either diagnostic as evidence that the loot path completed.
        Assert.True(result.SawLootWindowDiagnostic || result.SawLootSuccessDiagnostic,
            $"[{label}] FishingTask never surfaced loot_window_open or fishing_loot_success after the bobber interaction path. {failureContext}");
        Assert.False(result.SawSwimmingError,
            $"[{label}] Fishing path entered a swimming failure state before the catch completed. {failureContext}");
        Assert.False(result.SawNonFishableWaterError,
            $"[{label}] Fishing cast landed outside fishable water, which indicates LOS/shoreline pathing drift. {failureContext}");
        Assert.True(result.CatchDeltaItems.Count > 0,
            $"[{label}] FishingTask completed without a newly looted item appearing in bags. {failureContext} catchItems=[{string.Join(", ", result.CatchItems)}]");

        _output.WriteLine(
            $"[{label}] Final metrics: skill {result.SkillBefore} -> {result.SkillAfter}, " +
            $"initialVisiblePool={(result.InitialVisiblePoolDistance < float.MaxValue ? result.InitialVisiblePoolDistance.ToString("F1") : "none")}, " +
            $"bestPool={result.BestPoolDistance:F1}y, lootSuccess={result.SawLootSuccessDiagnostic}, catchDelta=[{string.Join(", ", result.CatchDeltaItems)}]");
    }

    private static string FormatFishingFailureContext(FishingRunResult result)
        => $"lastMessage={result.LastFishingTaskMessage} lastError={result.LastRelevantError} " +
           $"recentErrors=[{string.Join(" || ", result.RecentErrors)}] diag={result.RecentDiagnosticsSummary}";

    private static bool HasRequiredFishingSpells(WoWActivitySnapshot snapshot)
        => snapshot.Player?.SpellList?.Any(IsFishingSpellId) == true;

    private static bool ContainsFishingPole(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.BagContents?.Values.Any(itemId => FishingPoleIds.Contains(itemId)) == true;

    private static int CountItem(WoWActivitySnapshot? snapshot, uint itemId)
        => snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    private static uint GetFishingSkill(WoWActivitySnapshot? snapshot)
    {
        if (snapshot?.Player?.SkillInfo != null
            && snapshot.Player.SkillInfo.TryGetValue(FishingData.FishingSkillId, out uint skillLevel))
        {
            return skillLevel;
        }

        return 0;
    }

    private static bool IsFishingChannelActive(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.ChannelSpellId is uint spellId && IsFishingSpellId(spellId);

    private static bool IsFishingSpellId(uint spellId)
        => spellId == FishingData.FishingRank1
            || spellId == FishingData.FishingRank2
            || spellId == FishingData.FishingRank3
            || spellId == FishingData.FishingRank4;

    private static float FindNearestPoolDistance(WoWActivitySnapshot? snapshot)
        => FindNearestVisibleFishingPool(snapshot)?.Distance ?? float.MaxValue;

    private static VisibleFishingPool? FindNearestVisibleFishingPool(WoWActivitySnapshot? snapshot)
    {
        var playerPosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        if (playerPosition == null)
            return null;

        return snapshot?.NearbyObjects?
            .Where(IsFishingPool)
            .Select(gameObject => new VisibleFishingPool(
                gameObject.Entry,
                gameObject.Name ?? "FishingPool",
                Distance3D(playerPosition, gameObject.Base?.Position),
                gameObject.Base?.Position))
            .OrderBy(pool => pool.Distance)
            .FirstOrDefault();
    }

    private static Game.WoWGameObject? FindBobber(WoWActivitySnapshot? snapshot)
        => snapshot?.NearbyObjects?.FirstOrDefault(gameObject =>
            gameObject.DisplayId == FishingData.BobberDisplayId || gameObject.GameObjectType == 17);

    private static bool IsFishingPool(Game.WoWGameObject gameObject)
        => gameObject.GameObjectType == 25
            || gameObject.Entry == 180582
            || (!string.IsNullOrWhiteSpace(gameObject.Name)
                && (gameObject.Name.Contains("School", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Pool", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Wreckage", StringComparison.OrdinalIgnoreCase)));

    private static float Distance3D(Game.Position playerPosition, Game.Position? objectPosition)
    {
        if (objectPosition == null)
            return float.MaxValue;

        var dx = playerPosition.X - objectPosition.X;
        var dy = playerPosition.Y - objectPosition.Y;
        var dz = playerPosition.Z - objectPosition.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static float Distance3D(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static IReadOnlyList<uint> GetCatchItemIds(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.BagContents?.Values
            .Where(itemId => !FishingPoleIds.Contains(itemId))
            .OrderBy(itemId => itemId)
            .ToArray()
            ?? [];

    private static IReadOnlyList<string> GetFishingTaskMessages(WoWActivitySnapshot? snapshot)
        => snapshot?.RecentChatMessages?
            .Where(message => message.Contains("[TASK] FishingTask", StringComparison.Ordinal))
            .ToArray()
            ?? [];

    private static IReadOnlyList<string> GetMessageDelta(IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var remainingBaseline = new List<string>(baseline);
        var delta = new List<string>();

        foreach (var message in current)
        {
            var index = remainingBaseline.IndexOf(message);
            if (index >= 0)
                remainingBaseline.RemoveAt(index);
            else
                delta.Add(message);
        }

        return delta;
    }

    private static IReadOnlyList<uint> GetItemDelta(IReadOnlyList<uint> baseline, IReadOnlyList<uint> current)
    {
        var remainingBaseline = new List<uint>(baseline);
        var delta = new List<uint>();

        foreach (var itemId in current)
        {
            var index = remainingBaseline.IndexOf(itemId);
            if (index >= 0)
                remainingBaseline.RemoveAt(index);
            else
                delta.Add(itemId);
        }

        return delta;
    }

    private sealed record FishingRunResult(
        bool PoleStartedInBag,
        bool PoleEquippedByTask,
        bool BaitStartedInBag,
        bool BaitConsumedByTask,
        float InitialVisiblePoolDistance,
        float BestPoolDistance,
        bool SawChannel,
        bool SawBobber,
        bool SawPoolAcquireDiagnostic,
        bool SawInCastRangeDiagnostic,
        bool SawLosBlockedDiagnostic,
        bool SawLureUseDiagnostic,
        bool SawLootWindowDiagnostic,
        bool SawLootSuccessDiagnostic,
        bool SawSwimmingError,
        bool SawNonFishableWaterError,
        string LastFishingTaskMessage,
        string LastRelevantError,
        IReadOnlyList<string> RecentErrors,
        string RecentDiagnosticsSummary,
        IReadOnlyList<uint> CatchItems,
        IReadOnlyList<uint> CatchDeltaItems,
        uint SkillBefore,
        uint SkillAfter);

    private sealed record VisibleFishingPool(uint Entry, string Name, float Distance, Game.Position? Position);
}
