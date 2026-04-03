using System;
using System.Collections.Generic;
using System.IO;
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
    private enum RatchetFishingStageKind
    {
        PacketCapture,
        Parity
    }

    private readonly record struct RatchetFishingStageCandidate(
        string Name,
        float X,
        float Y,
        float Z,
        RatchetFishingStageKind Kind);

    private enum RatchetFishingStageReadiness
    {
        VisiblePoolReady,
        LocalChildSpawnedButInvisible,
        NoLocalChildSpawned
    }

    private readonly record struct RatchetFishingStageAttempt(
        RatchetFishingStageCandidate Stage,
        RatchetFishingStageReadiness Readiness,
        IReadOnlyList<uint> SpawnedLocalPoolEntries);

    private readonly record struct RatchetFishingPoolRefreshResult(
        RatchetFishingStageReadiness Readiness,
        IReadOnlyList<uint> SpawnedLocalPoolEntries);

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float RatchetAnchorX = -957f;
    private const float RatchetAnchorY = -3755f;
    private const float RatchetAnchorZ = 5f;
    private const float RatchetPacketCaptureX = -949.932f;
    private const float RatchetPacketCaptureY = -3766.883f;
    private const float RatchetPacketCaptureZ = 3.949f;
    private const float RatchetParityStageX = -967.2f;
    private const float RatchetParityStageY = -3760.0f;
    private const float RatchetParityStageZ = 4.4f;
    private const int PollIntervalMs = 1000;
    private const int StartingFishingSkill = 75;
    private const float ExpectedApproachRange = FishingTask.MaxCastingDistance;
    private const uint RatchetMasterPoolEntry = 2628;
    private const float RatchetLocalWaypointSearchRadius = 140f;
    private const float RatchetLocalWaypointMaxSpawnDistance = 50f;
    private const int RatchetLocalWaypointQueryLimit = 16;
    private const int RatchetLocalWaypointCount = 8;
    private const int RatchetPoolRefreshLimit = 8;
    private static readonly int FishingTimeoutMs = (new BotBehaviorConfig().MaxFishingCasts * 30000) + 20000;
    private static readonly float FishingPoolDetectRange = new BotBehaviorConfig().FishingPoolDetectRange;
    private const uint FishingLureItemId = FishingData.NightcrawlerBait;

    private static readonly uint[] RatchetLocalChildPoolEntries =
    [
        2617u,
        2618u,
        2619u,
        2620u,
        2621u,
        2626u,
        2627u
    ];

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
    public async Task Fishing_CaptureForegroundPackets_RatchetStagingCast()
    {
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot account not available.");

        var fgActionable = await _bot.CheckFgActionableAsync(requireTeleportProbe: false);
        global::Tests.Infrastructure.Skip.IfNot(fgActionable, "FG bot is not actionable for the foreground fishing packet capture.");

        await _bot.EnsureCleanSlateAsync(fgAccount!, "FG", teleportToSafeZone: true);
        await PrepareBotAsync(fgAccount!, "FG");
        var (stage, searchWaypoints) = await PrepareRatchetFishingStageAsync(fgAccount!, _bot.FgCharacterName, "FG");
        _output.WriteLine(
            $"[FG] Foreground packet capture using Ratchet stage '{stage.Name}' " +
            $"at ({stage.X:F1},{stage.Y:F1},{stage.Z:F1}).");

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, fgAccount!, "packets", "transform");

        var startRecording = await _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        Assert.Equal(ResponseResult.Success, startRecording);

        FishingRunResult result;
        try
        {
            result = await RunFishingTaskAsync(fgAccount!, "FG", searchWaypoints);
        }
        finally
        {
            var stopRecording = await _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
            _output.WriteLine($"Foreground packet capture stop: {stopRecording}");
            await Task.Delay(500);
        }

        AssertFishingResult("FG", result);
        var stages = AssertFishingPacketTraceRecorded("FG", fgAccount!, result);
        Assert.Equal(10, stages.CastSpell.Size);
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

        // Keep prep serialized. The injected client is less tolerant of a burst of
        // interleaved GM chat commands from both bots during fishing setup.
        await PrepareBotAsync(fgAccount!, "FG");
        await PrepareBotAsync(bgAccount!, "BG");

        var fgResult = await RunStagedFishingTaskWithPacketRecordingAsync(fgAccount!, _bot.FgCharacterName, "FG");
        var bgResult = await RunStagedFishingTaskWithPacketRecordingAsync(bgAccount!, _bot.BgCharacterName, "BG");

#if false
            var searchWaypoints = Array.Empty<(float x, float y, float z)>();

        // Force pool refresh — must happen AFTER teleport so the bots are on
        // the correct map and the spawned pools will appear in their ObjectManagers.
        // Wait for nearby objects to populate first.
        await _bot.WaitForNearbyUnitsPopulatedAsync(bgAccount!, timeoutMs: 5000, progressLabel: "BG post-teleport-stream");
        _output.WriteLine("Forcing pool system refresh via .pool update 2628");
        await _bot.SendGmChatCommandAsync(bgAccount!, ".pool update 2628");
        // Poll for game objects to appear after pool refresh
        await _bot.WaitForSnapshotConditionAsync(
            bgAccount!,
            snap => snap?.NearbyObjects?.Any() == true,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 500,
            progressLabel: "pool-refresh-objects");

        // Run both bots fishing simultaneously — they fish side by side at Ratchet.
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, bgAccount!, "packets", "transform", "physics");
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, fgAccount!, "packets", "transform");

        var startRecordingResults = await Task.WhenAll(
            _bot.SendActionAsync(bgAccount!, new ActionMessage { ActionType = ActionType.StartPhysicsRecording }),
            _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.StartPhysicsRecording }));
        Assert.All(startRecordingResults, result => Assert.Equal(ResponseResult.Success, result));

        FishingRunResult[] results;
        try
        {
            var bgTask = RunFishingTaskAsync(bgAccount!, "BG", searchWaypoints);
            var fgTask = RunFishingTaskAsync(fgAccount!, "FG", searchWaypoints);
            results = await Task.WhenAll(bgTask, fgTask);
        }
        finally
        {
            var stopRecordingResults = await Task.WhenAll(
                _bot.SendActionAsync(bgAccount!, new ActionMessage { ActionType = ActionType.StopPhysicsRecording }),
                _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.StopPhysicsRecording }));
            _output.WriteLine($"Fishing recording stop: BG={stopRecordingResults[0]} FG={stopRecordingResults[1]}");
            await Task.Delay(500);
        }

#endif

        AssertFishingResult("BG", bgResult);
        AssertFishingResult("FG", fgResult);

        var bgPacketStages = AssertFishingPacketTraceRecorded("BG", bgAccount!, bgResult);
        var fgPacketStages = AssertFishingPacketTraceRecorded("FG", fgAccount!, fgResult);
        AssertFishingPacketParity(bgPacketStages, fgPacketStages);
    }

    private async Task<FishingRunResult> RunStagedFishingTaskWithPacketRecordingAsync(string account, string? characterName, string label)
    {
        var (stage, searchWaypoints) = await PrepareRatchetFishingStageAsync(account, characterName, label);
        _output.WriteLine(
            $"[{label}] Fishing task starting from Ratchet stage '{stage.Name}' " +
            $"at ({stage.X:F1},{stage.Y:F1},{stage.Z:F1}).");

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, account, "packets", "transform", "physics");

        var startRecording = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        Assert.Equal(ResponseResult.Success, startRecording);

        try
        {
            return await RunFishingTaskAsync(account, label, searchWaypoints);
        }
        finally
        {
            var stopRecording = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
            _output.WriteLine($"[{label}] Fishing recording stop: {stopRecording}");
            await Task.Delay(500);
        }
    }

    private async Task<(RatchetFishingStageCandidate stage, List<(float x, float y, float z)> searchWaypoints)> PrepareRatchetFishingStageAsync(
        string account,
        string? characterName,
        string label)
    {
        var poolCommandAccount = await ResolveRatchetPoolCommandAccountAsync(account, label);
        var stageAttempts = new List<RatchetFishingStageAttempt>();
        foreach (var stage in GetRatchetFishingStageCandidates(label))
        {
            await TeleportToRatchetFishingStageAsync(account, characterName, label, stage);
            var (searchWaypoints, stageSpawns) = await LoadRatchetFishingStagePlanAsync(label, stage.X, stage.Y);
            var refreshResult = await RefreshRatchetFishingPoolsAsync(
                poolCommandAccount,
                account,
                label,
                stage.X,
                stage.Y,
                stageSpawns,
                searchWaypoints,
                $"{label} {stage.Name}-stream",
                requireVisiblePool: false);
            var prioritizedSearchWaypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
                stageSpawns,
                refreshResult.SpawnedLocalPoolEntries,
                stage.X,
                stage.Y,
                RatchetAnchorZ,
                RatchetLocalWaypointMaxSpawnDistance,
                RatchetLocalWaypointCount,
                standoffDistance: 8f).ToList();
            if (refreshResult.Readiness != RatchetFishingStageReadiness.NoLocalChildSpawned)
            {
                if (stageAttempts.Count > 0)
                {
                    _output.WriteLine(
                        $"[{label}] Falling back to Ratchet stage '{stage.Name}' after pool-visibility failures at: " +
                        string.Join(" | ", stageAttempts.Select(FormatStageAttempt)));
                }

                if (refreshResult.Readiness == RatchetFishingStageReadiness.LocalChildSpawnedButInvisible)
                {
                    _output.WriteLine(
                        $"[{label}] Proceeding from Ratchet stage '{stage.Name}' even though no pool is visible yet, " +
                        $"because local child pools [{string.Join(", ", refreshResult.SpawnedLocalPoolEntries)}] reported spawned objects.");
                }

                if (!prioritizedSearchWaypoints.SequenceEqual(searchWaypoints))
                {
                    _output.WriteLine(
                        $"[{label}] Prioritized search waypoints from spawned local pools [{string.Join(", ", refreshResult.SpawnedLocalPoolEntries)}]: " +
                        string.Join(" | ", prioritizedSearchWaypoints.Select(waypoint => $"({waypoint.x:F1},{waypoint.y:F1},{waypoint.z:F1})")));
                }

                return (stage, prioritizedSearchWaypoints);
            }

            stageAttempts.Add(new RatchetFishingStageAttempt(stage, refreshResult.Readiness, refreshResult.SpawnedLocalPoolEntries));
            _output.WriteLine(
                $"[{label}] Ratchet stage '{stage.Name}' did not become ready. " +
                $"result={refreshResult.Readiness} spawnedLocalPools=[{string.Join(", ", refreshResult.SpawnedLocalPoolEntries)}]");
        }

        var masterChildSpawns = await _bot.QueryMasterPoolChildSpawnsAsync(RatchetMasterPoolEntry);
        var activationProbes = await ProbeRatchetMasterPoolActivationAsync(poolCommandAccount, label, masterChildSpawns);
        var directProbeLocalPools = activationProbes
            .Where(probe => probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Spawned)
            .Select(probe => probe.Site.PoolEntry)
            .Distinct()
            .OrderBy(poolEntry => poolEntry)
            .ToArray();
        if (directProbeLocalPools.Length > 0)
        {
            var fallbackStage = GetRatchetFishingStageCandidates(label).First();
            await TeleportToRatchetFishingStageAsync(account, characterName, label, fallbackStage);
            var (fallbackSearchWaypoints, fallbackStageSpawns) = await LoadRatchetFishingStagePlanAsync(label, fallbackStage.X, fallbackStage.Y);
            var prioritizedFallbackSearchWaypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
                fallbackStageSpawns,
                directProbeLocalPools,
                fallbackStage.X,
                fallbackStage.Y,
                RatchetAnchorZ,
                RatchetLocalWaypointMaxSpawnDistance,
                RatchetLocalWaypointCount,
                standoffDistance: 8f).ToList();

            _output.WriteLine(
                $"[{label}] Proceeding from Ratchet direct-probe fallback using local child pools [{string.Join(", ", directProbeLocalPools)}] " +
                $"and stage '{fallbackStage.Name}'.");
            if (!prioritizedFallbackSearchWaypoints.SequenceEqual(fallbackSearchWaypoints))
            {
                _output.WriteLine(
                    $"[{label}] Direct-probe prioritized search waypoints: " +
                    string.Join(" | ", prioritizedFallbackSearchWaypoints.Select(waypoint => $"({waypoint.x:F1},{waypoint.y:F1},{waypoint.z:F1})")));
            }

            await LogRatchetFishingStageAsync(
                account,
                label,
                prioritizedFallbackSearchWaypoints,
                requireVisiblePool: false);
            return (fallbackStage, prioritizedFallbackSearchWaypoints);
        }

        throw new Xunit.Sdk.XunitException(BuildRatchetPoolBlockerMessage(label, stageAttempts, activationProbes));
    }

    private async Task<string> ResolveRatchetPoolCommandAccountAsync(string targetAccount, string label)
    {
        var bgAccount = _bot.BgAccountName;
        if (string.IsNullOrWhiteSpace(bgAccount)
            || string.Equals(bgAccount, targetAccount, StringComparison.OrdinalIgnoreCase))
        {
            return targetAccount;
        }

        await _bot.EnsureStrictAliveAsync(bgAccount, $"{label} BG pool-command sender");
        await TeleportToRatchetAsync(bgAccount, _bot.BgCharacterName, $"{label} BG pool-command sender");
        _output.WriteLine($"[{label}] Using BG account '{bgAccount}' as Ratchet pool-command sender.");
        return bgAccount;
    }

    private Task TeleportToRatchetFishingStageAsync(
        string account,
        string? characterName,
        string label,
        RatchetFishingStageCandidate stage)
    {
        // Packet parity asserts the fishing cast/channel/bobber/loot sequence, not
        // identical shoreline approach geometry. Prefer the proven packet-capture
        // dock for both bots now that it can complete end-to-end; keep parity staging
        // only as a fallback when local pool visibility does not come up there.
        return stage.Kind == RatchetFishingStageKind.PacketCapture
            ? TeleportToRatchetPacketCaptureAsync(account, characterName, label)
            : TeleportToRatchetParityStagingAsync(account, characterName, label);
    }

    private static IReadOnlyList<RatchetFishingStageCandidate> GetRatchetFishingStageCandidates(string label)
        =>
        [
            new("packet-capture", RatchetPacketCaptureX, RatchetPacketCaptureY, RatchetPacketCaptureZ, RatchetFishingStageKind.PacketCapture),
            new("parity", RatchetParityStageX, RatchetParityStageY, RatchetParityStageZ, RatchetFishingStageKind.Parity)
        ];

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

        _output.WriteLine($"[{label}] Teleporting to Ratchet anchor for {characterName}.");
        await _bot.BotTeleportAsync(account, MapId, RatchetAnchorX, RatchetAnchorY, RatchetAnchorZ + 3f);
        var settled = await _bot.WaitForTeleportSettledAsync(
            account,
            RatchetAnchorX,
            RatchetAnchorY,
            timeoutMs: 8000,
            progressLabel: $"{label} ratchet-arrival");
        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

        Assert.True(settled, $"[{label}] Ratchet teleport never settled near the Ratchet dock anchor.");

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

    private async Task TeleportToRatchetPacketCaptureAsync(string account, string? characterName, string label)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[{label}] Character name was not resolved for the Ratchet packet-capture teleport.");

        _output.WriteLine($"[{label}] Teleporting to Ratchet packet-capture staging for {characterName}.");
        await _bot.BotTeleportAsync(account, MapId, RatchetPacketCaptureX, RatchetPacketCaptureY, RatchetPacketCaptureZ + 2f);
        var settled = await _bot.WaitForTeleportSettledAsync(
            account,
            RatchetPacketCaptureX,
            RatchetPacketCaptureY,
            timeoutMs: 8000,
            progressLabel: $"{label} ratchet-packet-capture");
        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

        Assert.True(settled, $"[{label}] Ratchet packet-capture teleport never settled near the staging pose.");

        var snapshot = await RefreshAndGetSnapshotAsync(account);
        Assert.NotNull(snapshot);
        _output.WriteLine(
            $"[{label}] Packet-capture staging snapshot: position=({snapshot!.Player?.Unit?.GameObject?.Base?.Position?.X:F1}," +
            $"{snapshot.Player?.Unit?.GameObject?.Base?.Position?.Y:F1},{snapshot.Player?.Unit?.GameObject?.Base?.Position?.Z:F1})");
    }

    private async Task TeleportToRatchetParityStagingAsync(string account, string? characterName, string label)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[{label}] Character name was not resolved for the Ratchet parity staging teleport.");

        _output.WriteLine($"[{label}] Teleporting to Ratchet parity staging for {characterName}.");
        await _bot.BotTeleportAsync(account, MapId, RatchetParityStageX, RatchetParityStageY, RatchetParityStageZ + 2f);
        var settled = await _bot.WaitForTeleportSettledAsync(
            account,
            RatchetParityStageX,
            RatchetParityStageY,
            timeoutMs: 8000,
            progressLabel: $"{label} ratchet-parity-stage");
        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

        Assert.True(settled, $"[{label}] Ratchet parity staging teleport never settled near the cast-ready pose.");

        var snapshot = await RefreshAndGetSnapshotAsync(account);
        Assert.NotNull(snapshot);
        _output.WriteLine(
            $"[{label}] Parity staging snapshot: position=({snapshot!.Player?.Unit?.GameObject?.Base?.Position?.X:F1}," +
            $"{snapshot.Player?.Unit?.GameObject?.Base?.Position?.Y:F1},{snapshot.Player?.Unit?.GameObject?.Base?.Position?.Z:F1})");
    }

    private Task<(List<(float x, float y, float z)> waypoints, IReadOnlyList<FishingPoolSpawn> spawns)> LoadRatchetFishingStagePlanAsync(string label)
    {
        var (stageX, stageY, _) = GetRatchetFishingStagePosition(label);
        return LoadRatchetFishingStagePlanAsync(label, stageX, stageY);
    }

    private async Task<(List<(float x, float y, float z)> waypoints, IReadOnlyList<FishingPoolSpawn> spawns)> LoadRatchetFishingStagePlanAsync(string label, float stageX, float stageY)
    {
        var rawSpawns = await _bot.QueryGameObjectSpawnsNearAsync(
            FishingData.KnownFishingPoolEntries.ToArray(),
            MapId,
            stageX,
            stageY,
            RatchetLocalWaypointSearchRadius,
            limit: RatchetLocalWaypointQueryLimit);
        var spawns = FishingPoolStagePlanner.MaterializeSpawns(rawSpawns);

        var waypoints = FishingPoolStagePlanner.CreateSearchWaypoints(
            spawns,
            stageX,
            stageY,
            RatchetAnchorZ,
            RatchetLocalWaypointMaxSpawnDistance,
            RatchetLocalWaypointCount,
            standoffDistance: 8f).ToList();

        Assert.NotEmpty(waypoints);
        _output.WriteLine(
            $"[{label}] Ratchet fishing search waypoints: " +
            string.Join(" | ", waypoints.Select(waypoint => $"({waypoint.x:F1},{waypoint.y:F1},{waypoint.z:F1})")));
        _output.WriteLine(
            $"[{label}] Ratchet DB pool candidates: " +
            string.Join(" | ", spawns.Select(spawn => $"entry={spawn.Entry} pool={spawn.PoolEntry?.ToString() ?? "none"} dist={spawn.Distance2D:F1}")));

        return (waypoints, spawns);
    }

    private async Task<RatchetFishingPoolRefreshResult> RefreshRatchetFishingPoolsAsync(
        string commandAccount,
        string account,
        string label,
        float stageX,
        float stageY,
        IReadOnlyList<FishingPoolSpawn> stageSpawns,
        IReadOnlyList<(float x, float y, float z)> searchWaypoints,
        string progressLabel,
        bool requireVisiblePool = true)
    {
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: progressLabel);
        var childSpawnStates = await _bot.QueryMasterPoolChildSpawnStatesAsync(RatchetMasterPoolEntry);
        var localChildSpawnStates = childSpawnStates
            .Where(state => RatchetLocalChildPoolEntries.Contains(state.PoolEntry))
            .OrderBy(state => state.PoolEntry)
            .ThenBy(state => state.Guid)
            .ToArray();
        _output.WriteLine(
            $"[{label}] Local Ratchet child spawn states: " +
            string.Join(" | ", localChildSpawnStates.Select(FormatRatchetChildSpawnState)));

        var respawnableLocalChildSpawnStates = localChildSpawnStates
            .Where(state => state.HasRespawnTimer)
            .ToArray();
        if (respawnableLocalChildSpawnStates.Length > 0)
        {
            var clearedRespawns = await _bot.ClearFishingPoolRespawnTimersAsync(MapId, stageX, stageY, RatchetLocalWaypointSearchRadius);
            _output.WriteLine($"[{label}] Cleared {clearedRespawns} nearby fishing-pool respawn timers.");

            var respawnTrace = await _bot.SendGmChatCommandTrackedAsync(
                commandAccount,
                ".respawn",
                captureResponse: true,
                delayMs: 1000);
            var respawnResponses = respawnTrace.ChatMessages.Concat(respawnTrace.ErrorMessages).ToArray();
            _output.WriteLine(
                $"[{label}] nearby .respawn after clearing timers via {commandAccount}: " +
                $"{(respawnResponses.Length > 0 ? string.Join(" || ", respawnResponses) : respawnTrace.DispatchResult.ToString())}");

            var visiblePoolAfterRespawn = await WaitForVisibleFishingPoolAsync(account, TimeSpan.FromSeconds(4));
            if (visiblePoolAfterRespawn?.Position != null)
            {
                _output.WriteLine(
                    $"[{label}] Visible fishing pool after natural respawn: " +
                    $"({visiblePoolAfterRespawn.Position.X:F1},{visiblePoolAfterRespawn.Position.Y:F1},{visiblePoolAfterRespawn.Position.Z:F1}) " +
                    $"distance={visiblePoolAfterRespawn.Distance:F1}y");
                return new RatchetFishingPoolRefreshResult(
                    RatchetFishingStageReadiness.VisiblePoolReady,
                    respawnableLocalChildSpawnStates
                        .Select(state => state.PoolEntry)
                        .Distinct()
                        .OrderBy(poolEntry => poolEntry)
                        .ToArray());
            }
        }
        else
        {
            _output.WriteLine(
                $"[{label}] No local Ratchet child-pool rows currently have respawn timers; skipping natural respawn and treating pool selection separately.");
        }

        var poolRefreshPlan = FishingPoolStagePlanner.BuildPoolRefreshPlan(
            stageSpawns,
            RatchetLocalWaypointMaxSpawnDistance,
            RatchetMasterPoolEntry,
            RatchetPoolRefreshLimit);
        _output.WriteLine(
            $"[{label}] Pool refresh plan (near-stage child pools first, master fallback last): {string.Join(", ", poolRefreshPlan)}");

        var spawnedLocalChildPools = new HashSet<uint>();
        var visiblePoolBeforeRefresh = await WaitForVisibleFishingPoolAsync(account, TimeSpan.FromSeconds(2));
        if (visiblePoolBeforeRefresh?.Position != null)
        {
            _output.WriteLine(
                $"[{label}] Visible fishing pool already active before pool refresh: " +
                $"({visiblePoolBeforeRefresh.Position.X:F1},{visiblePoolBeforeRefresh.Position.Y:F1},{visiblePoolBeforeRefresh.Position.Z:F1}) distance={visiblePoolBeforeRefresh.Distance:F1}y");
            return new RatchetFishingPoolRefreshResult(
                RatchetFishingStageReadiness.VisiblePoolReady,
                Array.Empty<uint>());
        }

        for (var attempt = 0; attempt < poolRefreshPlan.Count; attempt++)
        {
            var poolEntry = poolRefreshPlan[attempt];
            if (poolEntry == RatchetMasterPoolEntry && spawnedLocalChildPools.Count > 0)
            {
                _output.WriteLine(
                    $"[{label}] Skipping master pool fallback because local child pools already reported spawned objects: [{string.Join(", ", spawnedLocalChildPools.OrderBy(entry => entry))}]");
                break;
            }

            var trace = await _bot.SendGmChatCommandTrackedAsync(
                commandAccount,
                $".pool update {poolEntry}",
                captureResponse: true,
                delayMs: 750);
            var responses = trace.ChatMessages.Concat(trace.ErrorMessages).ToArray();
            _output.WriteLine(
                $"[{label}] pool refresh attempt={attempt + 1}/{poolRefreshPlan.Count} entry={poolEntry} pre-update status via {commandAccount}: " +
                $"{(responses.Length > 0 ? string.Join(" || ", responses) : trace.DispatchResult.ToString())}");

            if (poolEntry != RatchetMasterPoolEntry)
            {
                var (activationState, _) = await QueryPoolSpawnStateAsync(
                    commandAccount,
                    label,
                    poolEntry,
                    $"refresh attempt={attempt + 1}/{poolRefreshPlan.Count}",
                    responses);
                if (activationState == FishingPoolActivationState.Spawned
                    && RatchetLocalChildPoolEntries.Contains(poolEntry))
                {
                    spawnedLocalChildPools.Add(poolEntry);
                }
            }

            var visiblePool = await WaitForVisibleFishingPoolAsync(account, TimeSpan.FromSeconds(4));
            if (visiblePool?.Position != null)
            {
                _output.WriteLine(
                    $"[{label}] Visible fishing pool after refresh entry {poolEntry}: " +
                    $"({visiblePool.Position.X:F1},{visiblePool.Position.Y:F1},{visiblePool.Position.Z:F1}) distance={visiblePool.Distance:F1}y");
                return new RatchetFishingPoolRefreshResult(
                    RatchetFishingStageReadiness.VisiblePoolReady,
                    spawnedLocalChildPools.OrderBy(poolEntry => poolEntry).ToArray());
            }
        }

        var visiblePoolReady = await LogRatchetFishingStageAsync(
            account,
            label,
            searchWaypoints,
            requireVisiblePool: requireVisiblePool && spawnedLocalChildPools.Count == 0);
        if (visiblePoolReady)
        {
            return new RatchetFishingPoolRefreshResult(
                RatchetFishingStageReadiness.VisiblePoolReady,
                spawnedLocalChildPools.OrderBy(poolEntry => poolEntry).ToArray());
        }

        return new RatchetFishingPoolRefreshResult(
            spawnedLocalChildPools.Count > 0
                ? RatchetFishingStageReadiness.LocalChildSpawnedButInvisible
                : RatchetFishingStageReadiness.NoLocalChildSpawned,
            spawnedLocalChildPools.OrderBy(poolEntry => poolEntry).ToArray());
    }

    private static string FormatRatchetChildSpawnState(LiveBotFixture.PoolGameObjectSpawnState state)
        => $"pool={state.PoolEntry} guid={state.Guid} entry={state.Entry} " +
           $"pos=({state.X:F1},{state.Y:F1},{state.Z:F1}) " +
           $"respawn={(state.HasRespawnTimer ? state.RespawnAtUtc?.ToString("O") : "none")}";

    private async Task<bool> LogRatchetFishingStageAsync(
        string account,
        string label,
        IReadOnlyList<(float x, float y, float z)> searchWaypoints,
        bool requireVisiblePool = true)
    {
        var snapshot = await RefreshAndGetSnapshotAsync(account);
        var playerPosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var visiblePool = FindNearestVisibleFishingPool(snapshot)
            ?? await WaitForVisibleFishingPoolAsync(account, TimeSpan.FromSeconds(8));

        if (visiblePool?.Position != null)
        {
            _output.WriteLine(
                $"[{label}] Staged fishing pool visible at ({visiblePool.Position.X:F1},{visiblePool.Position.Y:F1},{visiblePool.Position.Z:F1}) " +
                $"distance={visiblePool.Distance:F1}y");
            return true;
        }

        LogNearbyObjectSummary(account, snapshot);
        _output.WriteLine(
            $"[{label}] No immediate pool visible at staged start. " +
            $"position=({playerPosition?.X:F1},{playerPosition?.Y:F1},{playerPosition?.Z:F1}) " +
            $"searchWaypoints={searchWaypoints.Count}");

        Assert.True(!requireVisiblePool || visiblePool?.Position != null,
            $"[{label}] No visible fishing pool appeared after refreshing the nearby Ratchet pool set.");
        return false;
    }

    private async Task<IReadOnlyList<FishingPoolActivationProbe>> ProbeRatchetMasterPoolActivationAsync(
        string commandAccount,
        string label,
        IReadOnlyList<(uint poolEntry, string? poolDescription, uint entry, int map, float x, float y, float z)> childSpawns)
    {
        var sites = FishingPoolActivationAnalyzer.MaterializeSites(
            childSpawns,
            RatchetLocalChildPoolEntries,
            RatchetPacketCaptureX,
            RatchetPacketCaptureY,
            RatchetParityStageX,
            RatchetParityStageY);
        if (sites.Count == 0)
        {
            _output.WriteLine($"[{label}] No child-pool rows were returned for Ratchet master pool {RatchetMasterPoolEntry}.");
            return Array.Empty<FishingPoolActivationProbe>();
        }

        _output.WriteLine(
            $"[{label}] Barrens master pool {RatchetMasterPoolEntry} child sites: " +
            string.Join(" | ", sites.Select(site =>
                $"{site.PoolEntry}@({site.X:F1},{site.Y:F1},{site.Z:F1}) distFG={site.DistanceToPacketCapture:F1} distBG={site.DistanceToParity:F1} local={site.IsLocalRatchetChild}")));

        var responsesByPool = new Dictionary<uint, IReadOnlyList<string>>();
        foreach (var site in sites)
        {
            var trace = await _bot.SendGmChatCommandTrackedAsync(
                commandAccount,
                $".pool update {site.PoolEntry}",
                captureResponse: true,
                delayMs: 750);
            var responses = trace.ChatMessages.Concat(trace.ErrorMessages).ToArray();
            _output.WriteLine(
                $"[{label}] master-pool activation probe entry={site.PoolEntry} local={site.IsLocalRatchetChild} pre-update status via {commandAccount}: " +
                $"{(responses.Length > 0 ? string.Join(" || ", responses) : trace.DispatchResult.ToString())}");

            var (_, evidenceResponses) = await QueryPoolSpawnStateAsync(
                commandAccount,
                label,
                site.PoolEntry,
                $"direct probe entry={site.PoolEntry} local={site.IsLocalRatchetChild}",
                responses);
            responsesByPool[site.PoolEntry] = evidenceResponses;
        }

        var probes = FishingPoolActivationAnalyzer.MaterializeProbes(sites, responsesByPool);
        _output.WriteLine(
            $"[{label}] Barrens child-pool activation summary: {FishingPoolActivationAnalyzer.FormatSummary(probes)}");
        return probes;
    }

    private async Task<(FishingPoolActivationState State, IReadOnlyList<string> Responses)> QueryPoolSpawnStateAsync(
        string commandAccount,
        string label,
        uint poolEntry,
        string operationLabel,
        IReadOnlyList<string>? updateResponses = null)
    {
        var trace = await _bot.SendGmChatCommandTrackedAsync(
            commandAccount,
            $".pool spawns {poolEntry}",
            captureResponse: true,
            delayMs: 750);
        var spawnResponses = trace.ChatMessages.Concat(trace.ErrorMessages).ToArray();
        var evidenceResponses = (updateResponses ?? Array.Empty<string>())
            .Concat(spawnResponses)
            .ToArray();
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(poolEntry, evidenceResponses);
        var classifiedFromSpawnRowsOnly = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(poolEntry, spawnResponses);
        var usedUpdateCountEvidence = state == FishingPoolActivationState.Spawned
            && classifiedFromSpawnRowsOnly != FishingPoolActivationState.Spawned
            && updateResponses?.Count > 0;
        _output.WriteLine(
            $"[{label}] {operationLabel} post-update .pool spawns entry={poolEntry} via {commandAccount}: " +
            $"{(spawnResponses.Length > 0 ? string.Join(" || ", spawnResponses) : $"{trace.DispatchResult} (no active spawns reported)")}" +
            $" => {state}" +
            $"{(usedUpdateCountEvidence ? " (including .pool update count evidence)" : string.Empty)}");
        return (state, evidenceResponses);
    }

    private static string BuildRatchetPoolBlockerMessage(
        string label,
        IReadOnlyList<RatchetFishingStageAttempt> stageAttempts,
        IReadOnlyList<FishingPoolActivationProbe> activationProbes)
    {
        var blockerKind = FishingPoolActivationAnalyzer.DetermineBlockerKind(
            stageAttempts.Any(attempt => attempt.Readiness == RatchetFishingStageReadiness.LocalChildSpawnedButInvisible),
            activationProbes);
        var blockerText = blockerKind switch
        {
            FishingPoolBlockerKind.LocalPoolSpawnedButInvisible =>
                "A local Ratchet child pool reported a spawned object during the staged refresh, but no pool became visible from the staged dock positions. The blocker is now local pool visibility/streaming or shoreline approach, not master-pool selection.",
            FishingPoolBlockerKind.MasterPoolSelectedNonLocal =>
                "Barrens master pool 2628 is currently spawning non-local child pools instead of the local Ratchet children.",
            FishingPoolBlockerKind.LocalPoolSpawnedOnlyOnDirectProbe =>
                "Direct child-pool probes can spawn local Ratchet pools, but the staged refresh path still never surfaced a visible pool from either Ratchet dock position.",
            _ =>
                "No child pool under Barrens master pool 2628 reported a spawned object during the activation probe."
        };

        return
            $"[{label}] {blockerText} " +
            $"stageAttempts={string.Join(" | ", stageAttempts.Select(FormatStageAttempt))} " +
            $"activation={FishingPoolActivationAnalyzer.FormatSummary(activationProbes)}";
    }

    private static string FormatStageAttempt(RatchetFishingStageAttempt attempt)
    {
        var spawnedPools = attempt.SpawnedLocalPoolEntries.Count > 0
            ? string.Join(",", attempt.SpawnedLocalPoolEntries)
            : "none";
        return
            $"{attempt.Stage.Name}@({attempt.Stage.X:F1},{attempt.Stage.Y:F1},{attempt.Stage.Z:F1})" +
            $"=>{attempt.Readiness}[{spawnedPools}]";
    }

    private static (float x, float y, float z) GetRatchetFishingStagePosition(string label)
        => string.Equals(label, "FG", StringComparison.OrdinalIgnoreCase)
            ? (RatchetPacketCaptureX, RatchetPacketCaptureY, RatchetPacketCaptureZ)
            : (RatchetParityStageX, RatchetParityStageY, RatchetParityStageZ);

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
                || message.Contains("line of sight", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cast failed", StringComparison.OrdinalIgnoreCase));
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

    private FishingPacketStages AssertFishingPacketTraceRecorded(string label, string account, FishingRunResult result)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        var packetTracePath = PacketTraceArtifactHelper.WaitForPacketTrace(recordingDir, account, TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(packetTracePath), $"[{label}] Expected packet trace sidecar for {account}.");

        var packets = PacketTraceArtifactHelper.LoadPacketCsv(packetTracePath!);
        Assert.NotEmpty(packets);

        var gameObjectUse = FindFirstPacket(packets, "Send", "CMSG_GAMEOBJ_USE");
        Assert.NotNull(gameObjectUse);

        var customAnim = FindLastPacketBefore(packets, "Recv", "SMSG_GAMEOBJECT_CUSTOM_ANIM", gameObjectUse!.Index);
        Assert.NotNull(customAnim);

        var lootResponse = FindFirstPacket(packets, "Recv", "SMSG_LOOT_RESPONSE", gameObjectUse.Index + 1);
        Assert.NotNull(lootResponse);

        var channelStart = FindLastPacketBefore(packets, "Recv", "MSG_CHANNEL_START", customAnim!.Index);
        Assert.NotNull(channelStart);

        var spellGo = FindLastPacketBefore(packets, "Recv", "SMSG_SPELL_GO", channelStart!.Index);
        Assert.NotNull(spellGo);

        var castSpell = FindLastPacketBefore(packets, "Send", "CMSG_CAST_SPELL", spellGo!.Index);
        Assert.NotNull(castSpell);

        var channelUpdate = FindFirstPacket(packets, "Recv", "MSG_CHANNEL_UPDATE", channelStart.Index + 1);
        Assert.NotNull(channelUpdate);
        Assert.True(channelUpdate!.Index > channelStart.Index,
            $"[{label}] Channel update must occur after channel start.");

        var stages = new FishingPacketStages(
            CastSpell: castSpell,
            SpellGo: spellGo,
            ChannelStart: channelStart,
            ChannelUpdate: channelUpdate,
            CustomAnim: customAnim,
            GameObjectUse: gameObjectUse,
            LootResponse: lootResponse);

        Assert.True(result.SawBobber,
            $"[{label}] Fishing packet trace was captured, but the snapshot run never observed the bobber stage. {FormatFishingFailureContext(result)}");
        Assert.True(stages.CastToSpellGoMs >= 0, $"[{label}] Spell GO preceded the cast packet.");
        Assert.True(stages.SpellGoToChannelStartMs >= 0, $"[{label}] Channel start preceded spell GO.");
        Assert.True(stages.ChannelStartToCustomAnimMs >= 0, $"[{label}] Custom anim preceded channel start.");
        Assert.True(stages.CustomAnimToGameObjectUseMs >= 0, $"[{label}] Game object use preceded custom anim.");
        Assert.True(stages.GameObjectUseToLootResponseMs >= 0, $"[{label}] Loot response preceded game object use.");
        Assert.True(stages.CustomAnimToGameObjectUseMs <= 1000,
            $"[{label}] Bobber auto-interact lagged too long after the bite signal ({stages.CustomAnimToGameObjectUseMs}ms).");

        _output.WriteLine(
            $"[{label}] Fishing packet trace {Path.GetFileName(packetTracePath)}: " +
            $"cast={stages.CastSpell.ElapsedMs}ms spellGo={stages.SpellGo.ElapsedMs}ms channelStart={stages.ChannelStart.ElapsedMs}ms " +
            $"channelUpdate={stages.ChannelUpdate.ElapsedMs}ms customAnim={stages.CustomAnim.ElapsedMs}ms " +
            $"gameObjUse={stages.GameObjectUse.ElapsedMs}ms lootResponse={stages.LootResponse.ElapsedMs}ms");

        return stages;
    }

    private void AssertFishingPacketParity(FishingPacketStages bgStages, FishingPacketStages fgStages)
    {
        var castToSpellGoDeltaMs = Math.Abs(bgStages.CastToSpellGoMs - fgStages.CastToSpellGoMs);
        var spellGoToChannelStartDeltaMs = Math.Abs(bgStages.SpellGoToChannelStartMs - fgStages.SpellGoToChannelStartMs);
        var customAnimToUseDeltaMs = Math.Abs(bgStages.CustomAnimToGameObjectUseMs - fgStages.CustomAnimToGameObjectUseMs);

        _output.WriteLine(
            $"Fishing packet parity: cast->spellGo delta={castToSpellGoDeltaMs}ms " +
            $"spellGo->channelStart delta={spellGoToChannelStartDeltaMs}ms " +
            $"customAnim->use delta={customAnimToUseDeltaMs}ms");

        Assert.True(castToSpellGoDeltaMs <= 750,
            $"Fishing cast->spellGo timing diverged too far between BG and FG ({castToSpellGoDeltaMs}ms).");
        Assert.True(spellGoToChannelStartDeltaMs <= 750,
            $"Fishing spellGo->channelStart timing diverged too far between BG and FG ({spellGoToChannelStartDeltaMs}ms).");
        Assert.True(customAnimToUseDeltaMs <= 500,
            $"Fishing customAnim->gameObjUse timing diverged too far between BG and FG ({customAnimToUseDeltaMs}ms).");
    }

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

    private async Task<VisibleFishingPool?> WaitForVisibleFishingPoolAsync(string account, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        WoWActivitySnapshot? lastSnapshot = null;
        do
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            lastSnapshot = snapshot;
            var pool = FindNearestVisibleFishingPool(snapshot);
            if (pool?.Position != null)
                return pool;

            await Task.Delay(500);
        }
        while (DateTime.UtcNow < deadline);

        LogNearbyObjectSummary(account, lastSnapshot);
        return null;
    }

    private static Game.WoWGameObject? FindBobber(WoWActivitySnapshot? snapshot)
        => snapshot?.NearbyObjects?.FirstOrDefault(gameObject =>
            gameObject.DisplayId == FishingData.BobberDisplayId || gameObject.GameObjectType == 17);

    private static bool IsFishingPool(Game.WoWGameObject gameObject)
        => gameObject.GameObjectType == 25
            || FishingData.KnownFishingPoolEntries.Contains(gameObject.Entry)
            || (!string.IsNullOrWhiteSpace(gameObject.Name)
                && (gameObject.Name.Contains("School", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Pool", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Debris", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Swarm", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Wreckage", StringComparison.OrdinalIgnoreCase)));

    private void LogNearbyObjectSummary(string account, WoWActivitySnapshot? snapshot)
    {
        var playerPosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var nearbyObjects = snapshot?.NearbyObjects?
            .Take(8)
            .Select(gameObject =>
            {
                var position = gameObject.Base?.Position;
                var distance = playerPosition != null && position != null
                    ? Distance3D(playerPosition, position)
                    : float.MaxValue;

                return $"{gameObject.Entry}:{gameObject.Name ?? "?"}:type={gameObject.GameObjectType}:dist={(distance < float.MaxValue ? $"{distance:F1}" : "n/a")}";
            })
            .ToArray()
            ?? [];

        _output.WriteLine(
            $"[{account}] No fishing pool detected. nearbyObjectCount={snapshot?.NearbyObjects?.Count ?? 0} " +
            $"objects=[{string.Join(", ", nearbyObjects)}]");
    }

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

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
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

    private static PacketTraceArtifactHelper.PacketTraceRow? FindFirstPacket(
        IReadOnlyList<PacketTraceArtifactHelper.PacketTraceRow> packets,
        string direction,
        string opcodeName,
        int minIndex = 0)
        => packets.FirstOrDefault(packet =>
            packet.Index >= minIndex
            && string.Equals(packet.Direction, direction, StringComparison.OrdinalIgnoreCase)
            && string.Equals(packet.OpcodeName, opcodeName, StringComparison.Ordinal));

    private static PacketTraceArtifactHelper.PacketTraceRow? FindLastPacketBefore(
        IReadOnlyList<PacketTraceArtifactHelper.PacketTraceRow> packets,
        string direction,
        string opcodeName,
        int maxExclusiveIndex)
        => packets.LastOrDefault(packet =>
            packet.Index < maxExclusiveIndex
            && string.Equals(packet.Direction, direction, StringComparison.OrdinalIgnoreCase)
            && string.Equals(packet.OpcodeName, opcodeName, StringComparison.Ordinal));

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

    private sealed record FishingPacketStages(
        PacketTraceArtifactHelper.PacketTraceRow CastSpell,
        PacketTraceArtifactHelper.PacketTraceRow SpellGo,
        PacketTraceArtifactHelper.PacketTraceRow ChannelStart,
        PacketTraceArtifactHelper.PacketTraceRow ChannelUpdate,
        PacketTraceArtifactHelper.PacketTraceRow CustomAnim,
        PacketTraceArtifactHelper.PacketTraceRow GameObjectUse,
        PacketTraceArtifactHelper.PacketTraceRow LootResponse)
    {
        internal long CastToSpellGoMs => SpellGo.ElapsedMs - CastSpell.ElapsedMs;
        internal long SpellGoToChannelStartMs => ChannelStart.ElapsedMs - SpellGo.ElapsedMs;
        internal long ChannelStartToCustomAnimMs => CustomAnim.ElapsedMs - ChannelStart.ElapsedMs;
        internal long CustomAnimToGameObjectUseMs => GameObjectUse.ElapsedMs - CustomAnim.ElapsedMs;
        internal long GameObjectUseToLootResponseMs => LootResponse.ElapsedMs - GameObjectUse.ElapsedMs;
    }

    private sealed record VisibleFishingPool(uint Entry, string Name, float Distance, Game.Position? Position);
}
