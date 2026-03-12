using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Constants;
using BotRunner.Combat;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fishing live validation for the task-owned Ratchet pool flow.
///
/// Setup stays in the test (.learn/.setskill/.additem/.tele), but the runtime path under test is:
///   ActionType.StartFishing -> CharacterAction.StartFishing -> FishingTask
///
/// FishingTask is responsible for:
///   1) equipping the fishing pole from bags
///   2) moving into cast range of a visible Ratchet fishing pool
///   3) casting and waiting through the bobber/channel cycle
///   4) looting the catch from the loot window after bobber interaction
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class FishingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float RatchetAnchorX = -995f;
    private const float RatchetAnchorY = -3850f;
    private const float RatchetAnchorZ = 4f;
    private const float RatchetPoolSearchRadius = 250f;
    private const int PollIntervalMs = 1000;
    private const float ExpectedApproachRange = 18f;
    private const float MaxLandingDelta = 3f;
    private static readonly int FishingTimeoutMs = (new BotBehaviorConfig().MaxFishingCasts * 30000) + 20000;
    private static readonly float FishingPoolDetectRange = new BotBehaviorConfig().FishingPoolDetectRange;

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

    private static readonly RatchetStageCandidate[] RatchetStageCandidates =
    [
        new("DockEastNorthLegacy", -985.7f, -3827f, 5.7f),
        new("DockEastNorth", -986f, -3826f, 5.7f),
        new("DockEastMid", -985f, -3821f, 5.7f),
        new("DockEastApproach", -987f, -3818f, 5.7f),
        new("DockEastWestBridge", -989f, -3822f, 5.7f),
        new("DockWestNorth", -994f, -3828f, 5.7f),
        new("DockWestMid", -994f, -3834f, 5.7f),
        new("DockWestSouth", -994f, -3839f, 5.7f),
        new("DockMidNorth", -992f, -3828f, 5.7f),
        new("DockMidSouth", -992f, -3839f, 5.7f),
        new("DockCenter", -992f, -3834f, 5.7f)
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

        await _bot.EnsureCleanSlateAsync(bgAccount!, "BG");
        await _bot.EnsureCleanSlateAsync(fgAccount!, "FG");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.IfNot(fgActionable, "FG bot is not actionable for the dual fishing validation.");

        await PrepareBotAsync(bgAccount!, "BG");
        await PrepareBotAsync(fgAccount!, "FG");

        await LogRatchetPoolDiagnosticsAsync();
        var bgStage = await ResolveRatchetStageAsync(bgAccount!, "BG");
        var fgStage = await ResolveRatchetStageAsync(fgAccount!, "FG");

        var bgResult = await RunFishingTaskAsync(bgAccount!, "BG", bgStage);
        var fgResult = await RunFishingTaskAsync(fgAccount!, "FG", fgStage);

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

        await ForceFishingSpellSyncAsync(account, label);
        await _bot.BotSetSkillAsync(account, FishingData.FishingSkillId, 1, 300);
        await Task.Delay(500);

        await _bot.ExecuteGMCommandAsync($".reset items {snap.CharacterName}");
        await Task.Delay(1000);

        await _bot.BotAddItemAsync(account, FishingData.FishingPole);
        var polePresent = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => ContainsFishingPole(snapshot),
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} fishing-pole-added");

        Assert.True(polePresent, $"[{label}] Fishing pole never appeared in bags after setup.");
    }

    private async Task LogRatchetPoolDiagnosticsAsync()
    {
        var poolSpawns = await _bot.QueryGameObjectSpawnsNearAsync(
            FishingData.KnownFishingPoolEntries,
            MapId,
            RatchetAnchorX,
            RatchetAnchorY,
            RatchetPoolSearchRadius,
            limit: 10);

        if (poolSpawns.Count == 0)
        {
            _output.WriteLine("[STAGE] No Ratchet-area fishing pool spawns were returned by the world DB.");
            return;
        }

        foreach (var pool in poolSpawns.Take(5))
            _output.WriteLine($"[STAGE] DB pool entry={pool.entry} pos=({pool.x:F1}, {pool.y:F1}, {pool.z:F1}) dist={pool.distance2D:F1}y from Ratchet anchor.");
    }

    private async Task<FishingStage> ResolveRatchetStageAsync(string account, string label)
    {
        var attempts = new List<string>(RatchetStageCandidates.Length);
        FishingStage? selectedStage = null;
        foreach (var candidate in RatchetStageCandidates)
        {
            _output.WriteLine($"[{label}] Probing Ratchet stage {candidate.Name} at ({candidate.X:F1}, {candidate.Y:F1}, {candidate.Z:F1}).");
            await _bot.BotTeleportAsync(account, MapId, candidate.X, candidate.Y, candidate.Z);
            var (stableLanding, finalZ) = await _bot.WaitForZStabilizationAsync(account, waitMs: 4000);
            var landingDelta = Math.Abs(finalZ - candidate.Z);

            var snapshot = await RefreshAndGetSnapshotAsync(account);
            var visiblePool = FindNearestVisibleFishingPool(snapshot);
            var poolDistance = visiblePool?.Distance ?? float.MaxValue;

            var summary = $"candidate={candidate.Name} stable={stableLanding} finalZ={finalZ:F1} deltaZ={landingDelta:F1} visiblePool={(visiblePool != null ? $"{visiblePool.Entry}:{visiblePool.Name}" : "none")} distance={(poolDistance < float.MaxValue ? poolDistance.ToString("F1") : "n/a")}";
            attempts.Add(summary);
            _output.WriteLine($"[{label}] {summary}");

            if (!stableLanding || landingDelta > MaxLandingDelta)
                continue;

            if (visiblePool == null
                || poolDistance <= ExpectedApproachRange
                || poolDistance > FishingPoolDetectRange)
            {
                continue;
            }

            var stage = new FishingStage(candidate.Name, candidate.X, candidate.Y, candidate.Z, visiblePool.Entry, visiblePool.Name, poolDistance);
            if (selectedStage == null || stage.InitialVisiblePoolDistance < selectedStage.InitialVisiblePoolDistance)
                selectedStage = stage;
        }

        if (selectedStage != null)
        {
            _output.WriteLine($"[{label}] Restaging on selected Ratchet dock point {selectedStage.StageName} at ({selectedStage.StageX:F1}, {selectedStage.StageY:F1}, {selectedStage.StageZ:F1}).");
            await _bot.BotTeleportAsync(account, MapId, selectedStage.StageX, selectedStage.StageY, selectedStage.StageZ);
            await _bot.WaitForZStabilizationAsync(account, waitMs: 4000);
            return selectedStage;
        }

        Assert.Fail($"[{label}] No stable Ratchet stage exposed a visible pool between {ExpectedApproachRange:F1}y and {FishingPoolDetectRange:F1}y. Attempts: {string.Join(" | ", attempts)}");
        throw new InvalidOperationException("unreachable");
    }

    private async Task<FishingRunResult> RunFishingTaskAsync(string account, string label, FishingStage stage)
    {
        var before = await RefreshAndGetSnapshotAsync(account);
        if (before == null)
            throw new InvalidOperationException($"[{label}] Missing baseline snapshot before fishing.");

        var baselineCatchItems = GetCatchItemIds(before);
        var previousTaskMessages = GetFishingTaskMessages(before);
        var skillBefore = GetFishingSkill(before);
        var initialPoolDistance = FindNearestPoolDistance(before);
        var poleStartedInBag = ContainsFishingPole(before);

        _output.WriteLine($"[{label}] Dispatching StartFishing from Ratchet stage {stage.StageName}. skill={skillBefore} poleInBag={poleStartedInBag} nearestPool={initialPoolDistance:F1}y targetPool={stage.VisiblePoolEntry}:{stage.VisiblePoolName}");
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.StartFishing
        }, delayMs: 1000);

        var deadline = DateTime.UtcNow.AddMilliseconds(FishingTimeoutMs);
        var bestPoolDistance = initialPoolDistance;
        var sawChannel = false;
        var sawBobber = false;
        var sawPoolAcquireDiagnostic = false;
        var sawInCastRangeDiagnostic = false;
        var sawLootWindowDiagnostic = false;
        var sawLootSuccessDiagnostic = false;
        var sawSwimmingError = false;
        var poleEquippedByTask = !poleStartedInBag;
        IReadOnlyList<uint> finalCatchItems = baselineCatchItems;
        IReadOnlyList<uint> finalCatchDeltaItems = [];
        string lastFishingTaskMessage = string.Empty;
        uint skillAfter = skillBefore;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollIntervalMs);
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            if (snapshot == null)
                continue;

            poleEquippedByTask |= poleStartedInBag && !ContainsFishingPole(snapshot);
            sawChannel |= IsFishingChannelActive(snapshot);
            sawBobber |= FindBobber(snapshot) != null;
            sawSwimmingError |= snapshot.RecentErrors.Any(message => message.Contains("swimming", StringComparison.OrdinalIgnoreCase));
            skillAfter = GetFishingSkill(snapshot);

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

            sawPoolAcquireDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask pool_acquired", StringComparison.Ordinal));
            sawInCastRangeDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask in_cast_range", StringComparison.Ordinal));
            sawLootWindowDiagnostic |= currentTaskMessages.Any(message => message.Contains("FishingTask loot_window_open", StringComparison.Ordinal));
            var lootSuccessMessage = currentTaskMessages.LastOrDefault(message => message.Contains("FishingTask fishing_loot_success", StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(lootSuccessMessage))
            {
                sawLootSuccessDiagnostic = true;
                lastFishingTaskMessage = lootSuccessMessage;
            }

            var poolDistance = FindNearestPoolDistance(snapshot);
            if (poolDistance < bestPoolDistance)
                bestPoolDistance = poolDistance;

            finalCatchItems = GetCatchItemIds(snapshot);
            finalCatchDeltaItems = GetItemDelta(baselineCatchItems, finalCatchItems);
            if (sawLootSuccessDiagnostic && finalCatchDeltaItems.Count > 0)
            {
                _output.WriteLine($"[{label}] FishingTask reported loot success. stage={stage.StageName} bestPool={bestPoolDistance:F1}y channel={sawChannel} bobber={sawBobber} catchDelta=[{string.Join(", ", finalCatchDeltaItems)}]");
                return new FishingRunResult(
                    StageName: stage.StageName,
                    PoleStartedInBag: poleStartedInBag,
                    PoleEquippedByTask: poleEquippedByTask,
                    InitialPoolDistance: initialPoolDistance,
                    BestPoolDistance: bestPoolDistance,
                    SawChannel: sawChannel,
                    SawBobber: sawBobber,
                    SawPoolAcquireDiagnostic: sawPoolAcquireDiagnostic,
                    SawInCastRangeDiagnostic: sawInCastRangeDiagnostic,
                    SawLootWindowDiagnostic: sawLootWindowDiagnostic,
                    SawLootSuccessDiagnostic: sawLootSuccessDiagnostic,
                    SawSwimmingError: sawSwimmingError,
                    LastFishingTaskMessage: lastFishingTaskMessage,
                    CatchItems: finalCatchItems,
                    CatchDeltaItems: finalCatchDeltaItems,
                    SkillBefore: skillBefore,
                    SkillAfter: skillAfter);
            }
        }

        _output.WriteLine($"[{label}] Fishing timeout. stage={stage.StageName} bestPool={bestPoolDistance:F1}y channel={sawChannel} bobber={sawBobber} catchDelta=[{string.Join(", ", finalCatchDeltaItems)}]");
        return new FishingRunResult(
            StageName: stage.StageName,
            PoleStartedInBag: poleStartedInBag,
            PoleEquippedByTask: poleEquippedByTask,
            InitialPoolDistance: initialPoolDistance,
            BestPoolDistance: bestPoolDistance,
            SawChannel: sawChannel,
            SawBobber: sawBobber,
            SawPoolAcquireDiagnostic: sawPoolAcquireDiagnostic,
            SawInCastRangeDiagnostic: sawInCastRangeDiagnostic,
            SawLootWindowDiagnostic: sawLootWindowDiagnostic,
            SawLootSuccessDiagnostic: sawLootSuccessDiagnostic,
            SawSwimmingError: sawSwimmingError,
            LastFishingTaskMessage: lastFishingTaskMessage,
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
        Assert.True(result.PoleStartedInBag, $"[{label}] Fishing pole should start in bags so FishingTask owns the equip step.");
        Assert.True(result.PoleEquippedByTask, $"[{label}] FishingTask never removed the fishing pole from bags.");
        Assert.True(result.InitialPoolDistance > ExpectedApproachRange,
            $"[{label}] Ratchet stage did not start outside the pool casting window. distance={result.InitialPoolDistance:F1}");
        Assert.True(result.InitialPoolDistance <= FishingPoolDetectRange,
            $"[{label}] Ratchet stage started outside FishingTask detect range. distance={result.InitialPoolDistance:F1} detectRange={FishingPoolDetectRange:F1}");
        Assert.True(result.SawPoolAcquireDiagnostic,
            $"[{label}] FishingTask never reported acquiring a visible pool. lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.BestPoolDistance <= ExpectedApproachRange,
            $"[{label}] FishingTask never approached a pool into cast range. bestDistance={result.BestPoolDistance:F1} lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.SawInCastRangeDiagnostic,
            $"[{label}] FishingTask never reported entering cast range. lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.SawChannel,
            $"[{label}] FishingTask never reached a fishing channel state. lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.SawBobber,
            $"[{label}] FishingTask never observed a fishing bobber. lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.SawLootWindowDiagnostic || result.SawLootSuccessDiagnostic,
            $"[{label}] FishingTask never surfaced a loot-window-open or loot-success diagnostic. lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.SawLootSuccessDiagnostic,
            $"[{label}] FishingTask never reported fishing_loot_success. lastMessage={result.LastFishingTaskMessage}");
        Assert.False(result.SawSwimmingError,
            $"[{label}] Fishing path entered a swimming failure state before the catch completed. lastMessage={result.LastFishingTaskMessage}");
        Assert.True(result.CatchDeltaItems.Count > 0,
            $"[{label}] FishingTask completed without a newly looted item appearing in bags. lastMessage={result.LastFishingTaskMessage} catchItems=[{string.Join(", ", result.CatchItems)}]");

        _output.WriteLine($"[{label}] Final metrics: stage={result.StageName}, skill {result.SkillBefore} -> {result.SkillAfter}, bestPool={result.BestPoolDistance:F1}y, lootSuccess={result.SawLootSuccessDiagnostic}, catchDelta=[{string.Join(", ", result.CatchDeltaItems)}]");
    }

    private async Task ForceFishingSpellSyncAsync(string account, string label)
    {
        _output.WriteLine($"[{label}] Forcing fishing spell sync (.unlearn -> .learn).");

        foreach (var spellId in FishingSpellSyncIds)
        {
            await _bot.BotUnlearnSpellAsync(account, spellId);
            await Task.Delay(250);
        }

        foreach (var spellId in FishingSpellSyncIds)
        {
            await _bot.BotLearnSpellAsync(account, spellId);
            await Task.Delay(250);
        }

        var synced = await _bot.WaitForSnapshotConditionAsync(
            account,
            HasRequiredFishingSpells,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} fishing-spell-sync");

        Assert.True(synced, $"[{label}] Fishing spell sync failed.");
    }

    private static bool HasRequiredFishingSpells(WoWActivitySnapshot snapshot)
        // FG chat reliably confirms the learn path, but FG snapshots do not always surface
        // Fishing Pole Proficiency in SpellList. The live contract is the task-owned equip/
        // cast/loot path, so the setup gate only requires a castable fishing spell here.
        => snapshot.Player?.SpellList?.Any(IsFishingSpellId) == true;

    private static bool ContainsFishingPole(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.BagContents?.Values.Any(itemId => FishingPoleIds.Contains(itemId)) == true;

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

        return snapshot.NearbyObjects?
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
        string StageName,
        bool PoleStartedInBag,
        bool PoleEquippedByTask,
        float InitialPoolDistance,
        float BestPoolDistance,
        bool SawChannel,
        bool SawBobber,
        bool SawPoolAcquireDiagnostic,
        bool SawInCastRangeDiagnostic,
        bool SawLootWindowDiagnostic,
        bool SawLootSuccessDiagnostic,
        bool SawSwimmingError,
        string LastFishingTaskMessage,
        IReadOnlyList<uint> CatchItems,
        IReadOnlyList<uint> CatchDeltaItems,
        uint SkillBefore,
        uint SkillAfter);

    private sealed record FishingStage(
        string StageName,
        float StageX,
        float StageY,
        float StageZ,
        uint VisiblePoolEntry,
        string VisiblePoolName,
        float InitialVisiblePoolDistance);

    private sealed record RatchetStageCandidate(string Name, float X, float Y, float Z);

    private sealed record VisibleFishingPool(uint Entry, string Name, float Distance, Game.Position? Position);
}
