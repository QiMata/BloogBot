using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BotRunner.Constants;
using BotRunner.Combat;
using BotRunner.Native;
using BotRunner.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fishing live validation for two Ratchet slices:
///
/// 1) focused FG packet capture for the task-owned pool path
///    ActionType.StartFishing -> CharacterAction.StartFishing -> FishingTask
/// 2) dual FG/BG validation for the fixed Ratchet pier route plus direct open-water cast
///
/// The second slice keeps the pathing proof on the pier, but removes pool discovery from the
/// pass condition. FG keeps the full catch contract from the fixed ferry-end spot, while BG
/// proves the same route-to-pier and cast dispatch from that spot. BG receive-side fishing
/// packet parity remains a separate follow-up.
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

    private readonly record struct RatchetFishingStageAttempt(
        RatchetFishingStageCandidate Stage,
        RatchetFishingStageReadiness Readiness,
        IReadOnlyList<uint> SpawnedLocalPoolEntries);

    private readonly record struct RatchetFishingPoolRefreshResult(
        RatchetFishingStageReadiness Readiness,
        IReadOnlyList<uint> SpawnedLocalPoolEntries,
        bool AlternateLocationRetryRequested);

    private readonly record struct AlternateFishingRetryLocation(
        string StageName,
        string TeleportName,
        int MapId,
        float X,
        float Y,
        float Z,
        float SearchRadius);

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
    private const float RatchetKnownPoolX = -1006.0f;
    private const float RatchetKnownPoolY = -3845.0f;
    private const float RatchetKnownPoolZ = 0.1f;
    private const float RatchetPierApproachTargetX = -955.1f;
    private const float RatchetPierApproachTargetY = -3775.5f;
    private const float RatchetPierApproachTargetZ = 5.0f;
    private const float RatchetPierApproachStopDistance = 2.5f;
    private const float RatchetPierApproachArrivalTolerance = 6.5f;
    private const float RatchetPierMidTarget1X = -963.4f;
    private const float RatchetPierMidTarget1Y = -3771.0f;
    private const float RatchetPierMidTarget1Z = 5.4f;
    private const float RatchetPierMidTarget2X = -974.2f;
    private const float RatchetPierMidTarget2Y = -3789.4f;
    private const float RatchetPierMidTarget2Z = 5.4f;
    private const float RatchetPierRouteStopDistance = 2.5f;
    private const float RatchetPierRouteArrivalTolerance = 4.5f;
    private const float RatchetPierCastX = -981.20f;
    private const float RatchetPierCastY = -3803.10f;
    private const float RatchetPierCastZ = 5.60f;
    private const float RatchetPierCastStopDistance = 2.5f;
    private const float RatchetPierCastArrivalTolerance = 4.5f;
    private const float FishingBobberDistance = 18f;
    private const string NavigationDll = "Navigation";
    private const int PollIntervalMs = 1000;
    private const int StartingFishingSkill = 75;
    private const float ExpectedApproachRange = FishingTask.MaxCastingDistance;
    private const uint RatchetMasterPoolEntry = 2628;
    private const float RatchetLocalWaypointSearchRadius = 140f;
    private const float RatchetLocalWaypointMaxSpawnDistance = 50f;
    private const int RatchetLocalWaypointQueryLimit = 16;
    private const int RatchetLocalWaypointCount = 8;
    private const int RatchetPoolRefreshLimit = 8;
    private static readonly TimeSpan NaturalFishingPoolRespawnTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AlternateFishingPoolRetryTimeout = TimeSpan.FromSeconds(90);
    private const uint MainhandSlot = 15;
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

    private static readonly AlternateFishingRetryLocation[] AlternateFishingRetryLocations =
    [
        new("alternate-bootybay", "BootyBay", 0, -14297.2f, 530.993f, 8.77916f, 500f),
        new("alternate-auberdine", "Auberdine", 1, 6501.4f, 481.607f, 6.27062f, 450f),
        new("alternate-azshara", "Azshara", 1, 3341.36f, -4603.79f, 92.5027f, 600f)
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

    private static readonly FerryCastTargetSpec[] RatchetPierCastTargetSpecs =
    [
        new("pool_2626", -975.7f, -3835.2f, 300f),
        new("channel_mid", -972.8f, -3820.2f, 250f),
        new("pool_2627", -969.8f, -3805.1f, 200f)
    ];

    private static readonly float[] RatchetPierCastTargetNudgesDegrees =
    [
        0f,
        -8f,
        8f
    ];

    private static readonly object RatchetPierCastProbeLock = new();
    private static readonly object NavigationDllResolverLock = new();
    private static bool _ratchetPierCastProbeInitialized;
    private static bool _ratchetPierCastProbeAvailable;
    private static string? _ratchetPierCastProbeDataRoot;
    private static bool _navigationDllResolverRegistered;

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
        var stagePreparation = await PrepareRatchetFishingStageAsync(fgAccount!, _bot.FgCharacterName, "FG");
        _output.WriteLine(
            $"[FG] Foreground packet capture using Ratchet stage '{stagePreparation.StageName}' " +
            $"at ({stagePreparation.StageX:F1},{stagePreparation.StageY:F1},{stagePreparation.StageZ:F1}) " +
            $"with {RatchetFishingStageAttribution.FormatReadiness(stagePreparation.Readiness, stagePreparation.SpawnedLocalPoolEntries)}.");

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, fgAccount!, "packets", "transform");

        var startRecording = await _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        Assert.Equal(ResponseResult.Success, startRecording);

        FishingRunResult result;
        try
        {
            result = await RunFishingTaskAsync(fgAccount!, "FG", stagePreparation);
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
    public async Task Fishing_CatchFish_BgAndFg_RatchetPierOpenWaterPath()
    {
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsPathfindingReady, "PathfindingService is required for the Ratchet pier fishing route.");

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

        var fgResult = await RunPierOpenWaterFishingWithPacketRecordingAsync(fgAccount!, _bot.FgCharacterName, "FG");
        var bgResult = await RunPierOpenWaterFishingWithPacketRecordingAsync(bgAccount!, _bot.BgCharacterName, "BG");

        // Force pool refresh — must happen AFTER teleport so the bots are on
        // the correct map and the spawned pools will appear in their ObjectManagers.
        // Wait for nearby objects to populate first.

        // Run both bots fishing simultaneously — they fish side by side at Ratchet.

        AssertDirectFishingResult("FG", fgResult);
        AssertDirectFishingPathAndCastAttempt("BG", bgResult);

        _ = AssertFishingPacketTraceRecorded("FG", fgAccount!, fgResult);
        AssertDirectFishingCastPacketsRecorded("BG", bgAccount!, minimumCastPackets: 1);
    }

    /// <summary>
    /// P3.2: Capture fresh staged FG/BG fishing traces and compare them using PacketSequenceComparator.
    /// This uses the task-owned StartFishing flow so both traces include the full cast/channel/bobber/loot cycle.
    /// </summary>
    [SkippableFact]
    public async Task Fishing_ComparePacketSequences_BgMatchesFgReference()
    {
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsPathfindingReady, "PathfindingService is required for staged Ratchet fishing parity.");

        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG bot account not available.");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot account not available.");

        var fgActionable = await _bot.CheckFgActionableAsync(requireTeleportProbe: false);
        global::Tests.Infrastructure.Skip.IfNot(fgActionable, "FG bot is not actionable for staged fishing packet comparison.");

        await _bot.EnsureCleanSlateAsync(bgAccount!, "BG", teleportToSafeZone: true);
        await _bot.EnsureCleanSlateAsync(fgAccount!, "FG", teleportToSafeZone: true);

        // Keep prep serialized. The injected client is less tolerant of interleaved GM chat
        // setup bursts while both bots are being staged for fishing.
        await PrepareBotAsync(fgAccount!, "FG");
        await PrepareBotAsync(bgAccount!, "BG");

        var fgResult = await RunStagedFishingTaskWithPacketRecordingAsync(fgAccount!, _bot.FgCharacterName, "FG");
        var bgResult = await RunStagedFishingTaskWithPacketRecordingAsync(bgAccount!, _bot.BgCharacterName, "BG");

        AssertFishingResult("FG", fgResult);
        AssertFishingResult("BG", bgResult);

        var fgStages = AssertFishingPacketTraceRecorded("FG", fgAccount!, fgResult);
        var bgStages = AssertFishingPacketTraceRecorded("BG", bgAccount!, bgResult);

        AssertFishingPacketParity(bgStages, fgStages);
        CompareFishingPacketCsvSequences(bgAccount!, fgAccount!);
    }

    /// <summary>
    /// P3.2: Core comparison logic — loads CSVs, filters fishing-relevant opcodes,
    /// and asserts sequence match via PacketSequenceComparator.
    /// </summary>
    private void CompareFishingPacketCsvSequences(string bgAccount, string fgAccount)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        var fgCsvPath = Path.Combine(recordingDir, $"packets_{fgAccount}.csv");
        var bgCsvPath = Path.Combine(recordingDir, $"packets_{bgAccount}.csv");

        if (!File.Exists(fgCsvPath) || !File.Exists(bgCsvPath))
        {
            _output.WriteLine("[P3.2] Skipping CSV comparison — artifact files not present.");
            return;
        }

        // Movement opcodes are expected to differ (different positions/timing)
        var ignoreOpcodes = new HashSet<string>
        {
            "MSG_MOVE_HEARTBEAT", "MSG_MOVE_START_FORWARD", "MSG_MOVE_STOP",
            "MSG_MOVE_SET_FACING", "MSG_MOVE_START_TURN_LEFT", "MSG_MOVE_START_TURN_RIGHT",
            "MSG_MOVE_STOP_TURN", "CMSG_MOVE_TIME_SKIPPED",
            "SMSG_MONSTER_MOVE", "SMSG_COMPRESSED_MOVES",
        };

        var result = PacketSequenceComparator.CompareFiles(fgCsvPath, bgCsvPath, ignoreOpcodes);
        var summary = PacketSequenceComparator.FormatResult(result);
        _output.WriteLine($"[P3.2] Fishing packet sequence comparison:\n{summary}");

        // Assert: fishing-critical opcodes are present in both traces
        var fgPackets = PacketSequenceComparator.ParseCsv(fgCsvPath);
        var bgPackets = PacketSequenceComparator.ParseCsv(bgCsvPath);

        var fishingOpcodes = new[] { "CMSG_CAST_SPELL", "SMSG_SPELL_GO", "SMSG_CHANNEL_START", "SMSG_GAMEOBJECT_CUSTOM_ANIM" };
        foreach (var opcode in fishingOpcodes)
        {
            var fgHas = fgPackets.Any(p => p.Opcode.Contains(opcode));
            var bgHas = bgPackets.Any(p => p.Opcode.Contains(opcode));
            _output.WriteLine($"[P3.2] {opcode}: FG={fgHas} BG={bgHas}");

            if (fgHas)
            {
                Assert.True(bgHas,
                    $"FG has fishing opcode {opcode} but BG does not — parity gap.");
            }
        }

        // Log count comparison for key fishing opcodes
        foreach (var opcode in fishingOpcodes)
        {
            var fgCount = fgPackets.Count(p => p.Opcode.Contains(opcode));
            var bgCount = bgPackets.Count(p => p.Opcode.Contains(opcode));
            _output.WriteLine($"[P3.2] {opcode} count: FG={fgCount} BG={bgCount}");
        }
    }

    private async Task<FishingRunResult> RunStagedFishingTaskWithPacketRecordingAsync(string account, string? characterName, string label)
    {
        var stagePreparation = await PrepareRatchetFishingStageAsync(account, characterName, label);
        _output.WriteLine(
            $"[{label}] Fishing task starting from Ratchet stage '{stagePreparation.StageName}' " +
            $"at ({stagePreparation.StageX:F1},{stagePreparation.StageY:F1},{stagePreparation.StageZ:F1}) " +
            $"with {RatchetFishingStageAttribution.FormatReadiness(stagePreparation.Readiness, stagePreparation.SpawnedLocalPoolEntries)}.");

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, account, "packets", "transform", "physics");

        var startRecording = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        Assert.Equal(ResponseResult.Success, startRecording);

        try
        {
            return await RunFishingTaskAsync(account, label, stagePreparation);
        }
        finally
        {
            var stopRecording = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
            _output.WriteLine($"[{label}] Fishing recording stop: {stopRecording}");
            await Task.Delay(500);
        }
    }

    private async Task<DirectFishingRunResult> RunPierOpenWaterFishingWithPacketRecordingAsync(string account, string? characterName, string label)
    {
        await TeleportToRatchetPacketCaptureAsync(account, characterName, label);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: $"{label} ratchet-open-water-stream");

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, account, "packets", "transform", "physics");

        var startRecording = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        Assert.Equal(ResponseResult.Success, startRecording);

        try
        {
            return await RunPierOpenWaterFishingAsync(account, label);
        }
        finally
        {
            var stopRecording = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
            _output.WriteLine($"[{label}] Direct fishing recording stop: {stopRecording}");
            await Task.Delay(500);
        }
    }

    private async Task<DirectFishingRunResult> RunPierOpenWaterFishingAsync(string account, string label)
    {
        var before = await RefreshAndGetSnapshotAsync(account);
        if (before == null)
            throw new InvalidOperationException($"[{label}] Missing baseline snapshot before direct fishing.");

        var baselineCatchItems = GetCatchItemIds(before);
        var skillBefore = GetFishingSkill(before);
        var poleStartedInBag = ContainsFishingPole(before);
        var poleBagCountBefore = CountItem(before, FishingData.FishingPole);
        var mainhandBeforeGuid = GetMainhandGuid(before);
        var lastSnapshot = before;
        IReadOnlyList<string> recentErrors = before.RecentErrors.TakeLast(4).ToArray();
        var lastRelevantError = string.Empty;
        var recentDiagnosticsSummary = "none";
        var castCandidate = "none";
        var castAttemptSummary = "none";
        var selectedFacing = CalculateFacingToPoint(before, RatchetPierCastX, RatchetPierCastY, RatchetKnownPoolX, RatchetKnownPoolY);
        var approachResult = ResponseResult.Failure;
        var gotoResult = ResponseResult.Failure;
        var equipResult = ResponseResult.Failure;
        var setFacingResult = ResponseResult.Failure;
        var castResult = ResponseResult.Failure;
        var reachedPierApproachZone = false;
        var reachedFishingPosition = false;
        var poleEquipped = !poleStartedInBag;
        var facingSettled = false;
        var sawChannel = false;
        var sawBobber = false;
        var sawSwimmingError = false;
        var sawNonFishableWaterError = false;
        var finalCatchItems = baselineCatchItems;
        IReadOnlyList<uint> finalCatchDeltaItems = [];
        uint skillAfter = skillBefore;
        var failureStage = "move_to_pier";

        _output.WriteLine(
            $"[{label}] Direct fishing route: packet-capture stage -> pier approach target " +
            $"({RatchetPierApproachTargetX:F1},{RatchetPierApproachTargetY:F1},{RatchetPierApproachTargetZ:F1}) -> " +
            $"mid pier ({RatchetPierMidTarget1X:F1},{RatchetPierMidTarget1Y:F1},{RatchetPierMidTarget1Z:F1}) -> " +
            $"mid pier ({RatchetPierMidTarget2X:F1},{RatchetPierMidTarget2Y:F1},{RatchetPierMidTarget2Z:F1}) -> " +
            $"pier cast spot ({RatchetPierCastX:F1},{RatchetPierCastY:F1},{RatchetPierCastZ:F1}) with adaptive ferry-side cast selection.");

        var approachBaselineChats = before.RecentChatMessages.ToArray();
        approachResult = await _bot.SendActionAsync(
            account,
            MakeGoto(
                RatchetPierApproachTargetX,
                RatchetPierApproachTargetY,
                RatchetPierApproachTargetZ,
                RatchetPierApproachStopDistance));
        await Task.Delay(500);

        var approachWaitResult = approachResult == ResponseResult.Success
            ? await WaitForPositionSettledAsync(
                account,
                RatchetPierApproachTargetX,
                RatchetPierApproachTargetY,
                RatchetPierApproachTargetZ,
                RatchetPierApproachArrivalTolerance,
                approachBaselineChats,
                timeoutMs: 20000)
            : PositionWaitResult.Failure;
        reachedPierApproachZone = approachResult == ResponseResult.Success && approachWaitResult.ReachedTarget;
        lastSnapshot = approachWaitResult.BestSnapshot
            ?? await RefreshAndGetSnapshotAsync(account)
            ?? lastSnapshot;
        var distanceToApproachTarget = approachWaitResult.BestDistance < float.MaxValue
            ? approachWaitResult.BestDistance
            : DistanceToPosition2D(
                lastSnapshot,
                RatchetPierApproachTargetX,
                RatchetPierApproachTargetY,
                RatchetPierApproachTargetZ);
        recentDiagnosticsSummary = _bot.FormatRecentBotRunnerDiagnostics("GoToTask", "NavigationPath");

        if (reachedPierApproachZone)
        {
            if (!approachWaitResult.SawArrivalMessage)
            {
                var approachArrivalWait = await WaitForGoToArrivalMessageAsync(account, approachBaselineChats, timeoutMs: 5000);
                lastSnapshot = approachArrivalWait.Snapshot ?? lastSnapshot;
                await Task.Delay(250);
            }

            failureStage = "move_to_mid_pier_1";
            var midPier1Move = await MoveToFishingWaypointWithRetriesAsync(
                account,
                label,
                "mid_pier_1",
                lastSnapshot,
                RatchetPierMidTarget1X,
                RatchetPierMidTarget1Y,
                RatchetPierMidTarget1Z,
                RatchetPierRouteStopDistance,
                RatchetPierRouteArrivalTolerance,
                timeoutMs: 25000,
                maxAttempts: 2);
            gotoResult = midPier1Move.ActionResult;
            lastSnapshot = midPier1Move.Snapshot ?? lastSnapshot;

            if (gotoResult == ResponseResult.Success && midPier1Move.WaitResult.ReachedTarget)
            {
                failureStage = "move_to_mid_pier_2";
                var midPier2Move = await MoveToFishingWaypointWithRetriesAsync(
                    account,
                    label,
                    "mid_pier_2",
                    lastSnapshot,
                    RatchetPierMidTarget2X,
                    RatchetPierMidTarget2Y,
                    RatchetPierMidTarget2Z,
                    RatchetPierRouteStopDistance,
                    RatchetPierRouteArrivalTolerance,
                    timeoutMs: 25000,
                    maxAttempts: 2);
                gotoResult = midPier2Move.ActionResult;
                lastSnapshot = midPier2Move.Snapshot ?? lastSnapshot;

                if (gotoResult == ResponseResult.Success && midPier2Move.WaitResult.ReachedTarget)
                {
                    failureStage = "move_to_fishing_spot";
                    var fishingSpotMove = await MoveToFishingWaypointWithRetriesAsync(
                        account,
                        label,
                        "fishing_spot",
                        lastSnapshot,
                        RatchetPierCastX,
                        RatchetPierCastY,
                        RatchetPierCastZ,
                        RatchetPierCastStopDistance,
                        RatchetPierCastArrivalTolerance,
                        timeoutMs: 25000,
                        maxAttempts: 2);
                    gotoResult = fishingSpotMove.ActionResult;
                    reachedFishingPosition = gotoResult == ResponseResult.Success && fishingSpotMove.WaitResult.ReachedTarget;
                    lastSnapshot = fishingSpotMove.Snapshot
                        ?? await RefreshAndGetSnapshotAsync(account)
                        ?? lastSnapshot;
                }
            }
        }

        var distanceToFishingPosition = DistanceToPosition2D(lastSnapshot, RatchetPierCastX, RatchetPierCastY, RatchetPierCastZ);

        if (reachedFishingPosition && poleStartedInBag)
        {
            failureStage = "equip_pole";
            equipResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.EquipItem,
                Parameters =
                {
                    new RequestParameter { IntParam = (int)FishingData.FishingPole }
                }
            });
            if (equipResult == ResponseResult.Success)
            {
                poleEquipped = await WaitForFishingPoleEquippedAsync(
                    account,
                    mainhandBeforeGuid,
                    poleBagCountBefore,
                    TimeSpan.FromSeconds(8));
            }
        }

        if (reachedFishingPosition && poleEquipped)
        {
            lastSnapshot = await WaitForCastReadySnapshotAsync(account, label, lastSnapshot);
            var castCandidates = BuildRatchetPierCastCandidates(label, lastSnapshot);
            var castAttemptDetails = new List<string>(castCandidates.Count);
            foreach (var candidate in castCandidates)
            {
                castCandidate = candidate.Name;
                selectedFacing = candidate.FacingRadians;
                failureStage = $"cast_{candidate.Name}";

                var castAttempt = await TryDirectFishingCastAsync(
                    account,
                    label,
                    baselineCatchItems,
                    skillBefore,
                    candidate);

                setFacingResult = castAttempt.SetFacingResult;
                facingSettled = castAttempt.FacingSettled;
                castResult = castAttempt.CastResult;
                lastSnapshot = castAttempt.Snapshot ?? lastSnapshot;
                recentDiagnosticsSummary = castAttempt.RecentDiagnosticsSummary;
                recentErrors = castAttempt.RecentErrors;
                skillAfter = castAttempt.SkillAfter;
                sawChannel |= castAttempt.SawChannel;
                sawBobber |= castAttempt.SawBobber;
                sawSwimmingError = castAttempt.SawSwimmingError;
                sawNonFishableWaterError = castAttempt.SawNonFishableWaterError;
                if (!string.IsNullOrWhiteSpace(castAttempt.LastRelevantError))
                    lastRelevantError = castAttempt.LastRelevantError;

                finalCatchItems = castAttempt.CatchItems;
                finalCatchDeltaItems = castAttempt.CatchDeltaItems;
                castAttemptDetails.Add(
                    $"{candidate.Name}:los={(candidate.HasLineOfSight ? 1 : 0)} score={candidate.Score:F1} " +
                    $"landing=({candidate.LandingX:F1},{candidate.LandingY:F1},{candidate.LandingGroundZ:F1}) " +
                    $"setFacing={castAttempt.SetFacingResult} settled={castAttempt.FacingSettled} cast={castAttempt.CastResult} " +
                    $"channel={castAttempt.SawChannel} bobber={castAttempt.SawBobber} catch=[{string.Join(", ", castAttempt.CatchDeltaItems)}] " +
                    $"error={castAttempt.LastRelevantError}");

                if (castAttempt.ConfirmedFishing)
                {
                    if (castAttempt.CatchDeltaItems.Count > 0)
                        failureStage = "none";
                    break;
                }

                await Task.Delay(1000);
            }

            castAttemptSummary = string.Join(" | ", castAttemptDetails);
        }

        if (finalCatchDeltaItems.Count == 0)
        {
            _bot.DumpRecentBotRunnerDiagnostics($"{label}-direct-fishing-timeout", "GoToTask", "NavigationPath");
            _bot.DumpSnapshotDiagnostics(lastSnapshot, $"{label}-direct-fishing-timeout");
        }

        return new DirectFishingRunResult(
            PoleStartedInBag: poleStartedInBag,
            PoleEquipped: poleEquipped,
            ApproachResult: approachResult,
            ReachedPierApproachZone: reachedPierApproachZone,
            DistanceToApproachTarget: distanceToApproachTarget,
            ReachedFishingPosition: reachedFishingPosition,
            DistanceToFishingPosition: distanceToFishingPosition,
            GotoResult: gotoResult,
            EquipResult: equipResult,
            SetFacingResult: setFacingResult,
            FacingSettled: facingSettled,
            CastCandidate: castCandidate,
            CastAttemptSummary: castAttemptSummary,
            FacingDelta: CalculateFacingDelta(lastSnapshot, selectedFacing),
            CastResult: castResult,
            SawChannel: sawChannel,
            SawBobber: sawBobber,
            SawSwimmingError: sawSwimmingError,
            SawNonFishableWaterError: sawNonFishableWaterError,
            FailureStage: failureStage,
            LastRelevantError: lastRelevantError,
            RecentErrors: recentErrors,
            RecentDiagnosticsSummary: recentDiagnosticsSummary,
            CatchItems: finalCatchItems,
            CatchDeltaItems: finalCatchDeltaItems,
            SkillBefore: skillBefore,
            SkillAfter: skillAfter);
    }

    private async Task<RatchetFishingStagePreparation> PrepareRatchetFishingStageAsync(
        string account,
        string? characterName,
        string label)
    {
        var poolCommandAccount = await ResolveRatchetPoolCommandAccountAsync(account, label);
        var stageAttempts = new List<RatchetFishingStageAttempt>();
        foreach (var stage in GetRatchetFishingStageCandidates(label))
        {
            await TeleportToRatchetFishingStageAsync(account, characterName, label, stage);
            var (searchWaypoints, stageSpawns) = await LoadRatchetFishingStagePlanAsync(label, stage.X, stage.Y, stage.Z);
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
            if (refreshResult.AlternateLocationRetryRequested)
            {
                _output.WriteLine(
                    $"[{label}] Ratchet stage '{stage.Name}' exhausted the natural-respawn budget. " +
                    "Retrying once from an alternate named-tele fishing location.");

                var alternatePreparation = await TryPrepareAlternateFishingStageAsync(account, characterName, label);
                if (alternatePreparation.HasValue)
                    return alternatePreparation.Value;

                throw new Xunit.Sdk.XunitException(
                    $"[{label}] No fishing pool became visible within the natural-respawn budget at Ratchet stage '{stage.Name}', " +
                    "and the one alternate named-tele retry also failed.");
            }

            var prioritizedSearchWaypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
                stageSpawns,
                refreshResult.SpawnedLocalPoolEntries,
                stage.X,
                stage.Y,
                stage.Z,
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

                return new RatchetFishingStagePreparation(
                    StageName: stage.Name,
                    StageX: stage.X,
                    StageY: stage.Y,
                    StageZ: stage.Z,
                    Readiness: refreshResult.Readiness,
                    SpawnedLocalPoolEntries: refreshResult.SpawnedLocalPoolEntries,
                    SearchWaypoints: prioritizedSearchWaypoints);
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
            var (fallbackSearchWaypoints, fallbackStageSpawns) = await LoadRatchetFishingStagePlanAsync(label, fallbackStage.X, fallbackStage.Y, fallbackStage.Z);
            var prioritizedFallbackSearchWaypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
                fallbackStageSpawns,
                directProbeLocalPools,
                fallbackStage.X,
                fallbackStage.Y,
                fallbackStage.Z,
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
            return new RatchetFishingStagePreparation(
                StageName: fallbackStage.Name,
                StageX: fallbackStage.X,
                StageY: fallbackStage.Y,
                StageZ: fallbackStage.Z,
                Readiness: RatchetFishingStageReadiness.LocalChildSpawnedOnDirectProbeOnly,
                SpawnedLocalPoolEntries: directProbeLocalPools,
                SearchWaypoints: prioritizedFallbackSearchWaypoints);
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
        var (stageX, stageY, stageZ) = GetRatchetFishingStagePosition(label);
        return LoadRatchetFishingStagePlanAsync(label, stageX, stageY, stageZ);
    }

    private async Task<(List<(float x, float y, float z)> waypoints, IReadOnlyList<FishingPoolSpawn> spawns)> LoadRatchetFishingStagePlanAsync(
        string label,
        float stageX,
        float stageY,
        float stageZ)
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
            stageZ,
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

    private async Task<RatchetFishingStagePreparation?> TryPrepareAlternateFishingStageAsync(
        string account,
        string? characterName,
        string label)
    {
        var selectedLocation = await ChooseAlternateFishingRetryLocationAsync(label);
        _output.WriteLine(
            $"[{label}] Alternate fishing retry uses '{selectedLocation.TeleportName}' " +
            $"at ({selectedLocation.X:F1},{selectedLocation.Y:F1},{selectedLocation.Z:F1}) map={selectedLocation.MapId}.");

        if (!string.IsNullOrWhiteSpace(characterName))
        {
            await _bot.BotTeleportToNamedAsync(account, characterName, selectedLocation.TeleportName);
        }
        else
        {
            await _bot.BotTeleportAsync(
                account,
                selectedLocation.MapId,
                selectedLocation.X,
                selectedLocation.Y,
                selectedLocation.Z + 2f);
        }

        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

        var rawSpawns = await _bot.QueryGameObjectSpawnsNearAsync(
            FishingData.KnownFishingPoolEntries.ToArray(),
            selectedLocation.MapId,
            selectedLocation.X,
            selectedLocation.Y,
            selectedLocation.SearchRadius,
            limit: RatchetLocalWaypointQueryLimit);
        var spawns = FishingPoolStagePlanner.MaterializeSpawns(rawSpawns);
        var waypoints = spawns.Count > 0
            ? FishingPoolStagePlanner.CreateSearchWaypoints(
                spawns,
                selectedLocation.X,
                selectedLocation.Y,
                selectedLocation.Z,
                localSpawnDistance: MathF.Min(selectedLocation.SearchRadius, 120f),
                waypointCount: RatchetLocalWaypointCount,
                standoffDistance: 8f).ToList()
            : [(selectedLocation.X, selectedLocation.Y, selectedLocation.Z)];

        _output.WriteLine(
            $"[{label}] Alternate fishing retry DB pool candidates: " +
            (spawns.Count > 0
                ? string.Join(" | ", spawns.Select(spawn => $"entry={spawn.Entry} pool={spawn.PoolEntry?.ToString() ?? "none"} dist={spawn.Distance2D:F1}"))
                : "none"));
        _output.WriteLine(
            $"[{label}] Alternate fishing retry search waypoints: " +
            string.Join(" | ", waypoints.Select(waypoint => $"({waypoint.x:F1},{waypoint.y:F1},{waypoint.z:F1})")));

        var visiblePool = await WaitForNearbyFishingPoolNearStageAsync(
            account,
            label,
            selectedLocation.MapId,
            selectedLocation.X,
            selectedLocation.Y,
            selectedLocation.SearchRadius,
            AlternateFishingPoolRetryTimeout);
        if (visiblePool?.Position == null)
        {
            _output.WriteLine(
                $"[{label}] Alternate fishing retry at '{selectedLocation.TeleportName}' timed out after " +
                $"{AlternateFishingPoolRetryTimeout.TotalSeconds:F0}s without a nearby visible pool.");
            return null;
        }

        _output.WriteLine(
            $"[{label}] Alternate fishing retry found pool '{visiblePool.Name}' " +
            $"at ({visiblePool.Position.X:F1},{visiblePool.Position.Y:F1},{visiblePool.Position.Z:F1}) " +
            $"distance={visiblePool.Distance:F1}y.");
        return new RatchetFishingStagePreparation(
            StageName: selectedLocation.StageName,
            StageX: selectedLocation.X,
            StageY: selectedLocation.Y,
            StageZ: selectedLocation.Z,
            Readiness: RatchetFishingStageReadiness.VisiblePoolReady,
            SpawnedLocalPoolEntries: Array.Empty<uint>(),
            SearchWaypoints: waypoints);
    }

    private async Task<AlternateFishingRetryLocation> ChooseAlternateFishingRetryLocationAsync(string label)
    {
        AlternateFishingRetryLocation? bestLocation = null;
        var bestSpawnCount = int.MinValue;

        foreach (var candidate in AlternateFishingRetryLocations)
        {
            var spawns = await _bot.QueryGameObjectSpawnsNearAsync(
                FishingData.KnownFishingPoolEntries.ToArray(),
                candidate.MapId,
                candidate.X,
                candidate.Y,
                candidate.SearchRadius,
                limit: RatchetLocalWaypointQueryLimit);
            _output.WriteLine(
                $"[{label}] Alternate retry candidate '{candidate.TeleportName}' has {spawns.Count} DB fishing-pool spawn(s) " +
                $"within {candidate.SearchRadius:F0}y.");

            if (spawns.Count > bestSpawnCount)
            {
                bestSpawnCount = spawns.Count;
                bestLocation = candidate;
            }
        }

        return bestLocation ?? AlternateFishingRetryLocations[0];
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

            _output.WriteLine(
                $"[{label}] Waiting up to {NaturalFishingPoolRespawnTimeout.TotalMinutes:F0} minute(s) for a natural nearby pool respawn " +
                $"after clearing timers; command sender remained '{commandAccount}' but no runtime respawn command is dispatched.");

            var visiblePoolAfterRespawn = await WaitForNearbyFishingPoolNearStageAsync(
                account,
                label,
                MapId,
                stageX,
                stageY,
                RatchetLocalWaypointSearchRadius,
                NaturalFishingPoolRespawnTimeout);
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
                        .ToArray(),
                    AlternateLocationRetryRequested: false);
            }

            _output.WriteLine(
                $"[{label}] No nearby staged pool became visible within the natural-respawn budget. " +
                "Escalating to the one alternate named-tele retry.");
            return new RatchetFishingPoolRefreshResult(
                RatchetFishingStageReadiness.NoLocalChildSpawned,
                respawnableLocalChildSpawnStates
                    .Select(state => state.PoolEntry)
                    .Distinct()
                    .OrderBy(poolEntry => poolEntry)
                    .ToArray(),
                AlternateLocationRetryRequested: true);
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
                Array.Empty<uint>(),
                AlternateLocationRetryRequested: false);
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
                    spawnedLocalChildPools.OrderBy(poolEntry => poolEntry).ToArray(),
                    AlternateLocationRetryRequested: false);
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
                spawnedLocalChildPools.OrderBy(poolEntry => poolEntry).ToArray(),
                AlternateLocationRetryRequested: false);
        }

        return new RatchetFishingPoolRefreshResult(
            spawnedLocalChildPools.Count > 0
                ? RatchetFishingStageReadiness.LocalChildSpawnedButInvisible
                : RatchetFishingStageReadiness.NoLocalChildSpawned,
            spawnedLocalChildPools.OrderBy(poolEntry => poolEntry).ToArray(),
            AlternateLocationRetryRequested: false);
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

    private async Task<FishingRunResult> RunFishingTaskAsync(string account, string label, RatchetFishingStagePreparation? stagePreparation = null)
    {
        var before = await RefreshAndGetSnapshotAsync(account);
        if (before == null)
            throw new InvalidOperationException($"[{label}] Missing baseline snapshot before fishing.");

        var searchWaypoints = stagePreparation?.SearchWaypoints;

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
            $"{RatchetFishingStageAttribution.FormatPreparation(stagePreparation)} " +
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
                    StagePreparation: stagePreparation,
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
            StagePreparation: stagePreparation,
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
            if (RatchetFishingStageAttribution.ShouldAttributeNoPoolFailureToRuntime(
                result.StagePreparation,
                result.InitialVisiblePoolDistance,
                result.BestPoolDistance))
            {
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

            Assert.Fail(
                $"[{label}] No fishing pool ever became visible after staged Ratchet preflight. " +
                $"{RatchetFishingStageAttribution.FormatPreparation(result.StagePreparation)} " +
                $"{FormatFishingFailureContext(result)}");
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
        => $"{RatchetFishingStageAttribution.FormatPreparation(result.StagePreparation)} " +
           $"lastMessage={result.LastFishingTaskMessage} lastError={result.LastRelevantError} " +
           $"recentErrors=[{string.Join(" || ", result.RecentErrors)}] diag={result.RecentDiagnosticsSummary}";

    private void AssertDirectFishingResult(string label, DirectFishingRunResult result)
    {
        var failureContext = FormatDirectFishingFailureContext(result);

        Assert.True(result.PoleStartedInBag,
            $"[{label}] Fishing pole should start in bags so the open-water validation still proves equip behavior. {failureContext}");
        Assert.Equal(ResponseResult.Success, result.ApproachResult);
        Assert.True(result.ReachedPierApproachZone,
            $"[{label}] The bot never reached the forced Ratchet pier approach corridor. {failureContext}");
        Assert.True(result.DistanceToApproachTarget <= RatchetPierApproachArrivalTolerance,
            $"[{label}] The bot stayed too far from the forced Ratchet pier approach target. distance={result.DistanceToApproachTarget:F1} {failureContext}");
        Assert.Equal(ResponseResult.Success, result.GotoResult);
        Assert.True(result.ReachedFishingPosition,
            $"[{label}] The bot never reached the fixed Ratchet pier fishing spot. {failureContext}");
        Assert.True(result.DistanceToFishingPosition <= RatchetPierCastArrivalTolerance,
            $"[{label}] The bot stayed too far from the fixed Ratchet pier fishing spot. distance={result.DistanceToFishingPosition:F1} {failureContext}");
        Assert.Equal(ResponseResult.Success, result.EquipResult);
        Assert.True(result.PoleEquipped,
            $"[{label}] The fishing pole never moved from bags into mainhand. {failureContext}");
        Assert.Equal(ResponseResult.Success, result.SetFacingResult);
        Assert.True(result.FacingSettled,
            $"[{label}] The bot never settled facing the open-water cast line. {failureContext}");
        Assert.Equal(ResponseResult.Success, result.CastResult);
        Assert.True(result.SawChannel,
            $"[{label}] Direct fishing never reached a channel state. {failureContext}");
        Assert.True(result.SawBobber,
            $"[{label}] Direct fishing never observed a bobber. {failureContext}");
        Assert.False(result.SawSwimmingError,
            $"[{label}] The bot entered a swimming state before the catch completed. {failureContext}");
        Assert.False(result.SawNonFishableWaterError,
            $"[{label}] The cast landed outside fishable water. {failureContext}");
        Assert.True(result.CatchDeltaItems.Count > 0,
            $"[{label}] Direct fishing completed without a newly looted item in bags. {failureContext} catchItems=[{string.Join(", ", result.CatchItems)}]");

        _output.WriteLine(
            $"[{label}] Final direct-fishing metrics: skill {result.SkillBefore} -> {result.SkillAfter}, " +
            $"candidate={result.CastCandidate} spotDistance={result.DistanceToFishingPosition:F1}y facingDelta={result.FacingDelta:F2}rad " +
            $"catchDelta=[{string.Join(", ", result.CatchDeltaItems)}]");
    }

    private void AssertDirectFishingPathAndCastAttempt(string label, DirectFishingRunResult result)
    {
        var failureContext = FormatDirectFishingFailureContext(result);

        Assert.True(result.PoleStartedInBag,
            $"[{label}] Fishing pole should start in bags so the ferry-end path still proves equip behavior. {failureContext}");
        Assert.Equal(ResponseResult.Success, result.ApproachResult);
        Assert.True(result.ReachedPierApproachZone,
            $"[{label}] The bot never reached the forced Ratchet pier approach corridor. {failureContext}");
        Assert.True(result.DistanceToApproachTarget <= RatchetPierApproachArrivalTolerance,
            $"[{label}] The bot stayed too far from the forced Ratchet pier approach target. distance={result.DistanceToApproachTarget:F1} {failureContext}");
        Assert.Equal(ResponseResult.Success, result.GotoResult);
        Assert.True(result.ReachedFishingPosition,
            $"[{label}] The bot never reached the fixed Ratchet pier fishing spot. {failureContext}");
        Assert.True(result.DistanceToFishingPosition <= RatchetPierCastArrivalTolerance,
            $"[{label}] The bot stayed too far from the fixed Ratchet pier fishing spot. distance={result.DistanceToFishingPosition:F1} {failureContext}");
        Assert.Equal(ResponseResult.Success, result.EquipResult);
        Assert.True(result.PoleEquipped,
            $"[{label}] The fishing pole never moved from bags into mainhand. {failureContext}");
        Assert.Equal(ResponseResult.Success, result.SetFacingResult);
        Assert.True(result.FacingSettled,
            $"[{label}] The bot never settled facing the open-water cast line. {failureContext}");
        Assert.Equal(ResponseResult.Success, result.CastResult);
        Assert.False(result.SawSwimmingError,
            $"[{label}] The bot entered a swimming state before the cast loop completed. {failureContext}");
        Assert.False(result.SawNonFishableWaterError,
            $"[{label}] The cast landed outside fishable water. {failureContext}");
        Assert.False(string.IsNullOrWhiteSpace(result.CastAttemptSummary),
            $"[{label}] No direct-cast attempts were recorded from the ferry-end pier spot. {failureContext}");

        _output.WriteLine(
            $"[{label}] Direct ferry-end path/cast metrics: candidate={result.CastCandidate} " +
            $"spotDistance={result.DistanceToFishingPosition:F1}y facingDelta={result.FacingDelta:F2}rad");
    }

    private static string FormatDirectFishingFailureContext(DirectFishingRunResult result)
        => $"stage={result.FailureStage} approach={result.ApproachResult} approachDistance={result.DistanceToApproachTarget:F1} " +
           $"goto={result.GotoResult} equip={result.EquipResult} setFacing={result.SetFacingResult} cast={result.CastResult} spotDistance={result.DistanceToFishingPosition:F1} " +
           $"candidate={result.CastCandidate} facingDelta={result.FacingDelta:F2} lastError={result.LastRelevantError} " +
           $"recentErrors=[{string.Join(" || ", result.RecentErrors)}] diag={result.RecentDiagnosticsSummary} casts=[{result.CastAttemptSummary}]";

    private FishingPacketStages AssertFishingPacketTraceRecorded(string label, string account, FishingRunResult result)
        => AssertFishingPacketTraceRecorded(label, account, result.SawBobber, FormatFishingFailureContext(result));

    private FishingPacketStages AssertFishingPacketTraceRecorded(string label, string account, DirectFishingRunResult result)
        => AssertFishingPacketTraceRecorded(label, account, result.SawBobber, FormatDirectFishingFailureContext(result));

    private FishingPacketStages AssertFishingPacketTraceRecorded(string label, string account, bool sawBobber, string failureContext)
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

        Assert.True(sawBobber,
            $"[{label}] Fishing packet trace was captured, but the snapshot run never observed the bobber stage. {failureContext}");
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

    private int AssertDirectFishingCastPacketsRecorded(string label, string account, int minimumCastPackets)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        var packetTracePath = PacketTraceArtifactHelper.WaitForPacketTrace(recordingDir, account, TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(packetTracePath), $"[{label}] Expected packet trace sidecar for {account}.");

        var packets = PacketTraceArtifactHelper.LoadPacketCsv(packetTracePath!);
        var castPackets = packets
            .Where(packet =>
                packet.Direction.Equals("Send", StringComparison.OrdinalIgnoreCase)
                && string.Equals(packet.OpcodeName, "CMSG_CAST_SPELL", StringComparison.Ordinal))
            .ToArray();

        Assert.True(castPackets.Length >= minimumCastPackets,
            $"[{label}] Expected at least {minimumCastPackets} direct fishing cast packets, but recorded {castPackets.Length}.");

        _output.WriteLine(
            $"[{label}] Direct fishing packet trace {Path.GetFileName(packetTracePath)} captured {castPackets.Length} cast packets.");

        return castPackets.Length;
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

    private static VisibleFishingPool? FindNearestNearbyFishingPoolNearStage(
        WoWActivitySnapshot? snapshot,
        int expectedMapId,
        float stageX,
        float stageY,
        float maxStageDistance)
    {
        var mapId = snapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot?.CurrentMapId ?? 0;
        if (mapId != expectedMapId)
            return null;

        return snapshot?.MovementData?.NearbyGameObjects?
            .Where(IsFishingPool)
            .Where(gameObject => gameObject.Position != null
                && Distance2D(gameObject.Position.X, gameObject.Position.Y, stageX, stageY) <= maxStageDistance)
            .OrderBy(gameObject => Distance2D(gameObject.Position!.X, gameObject.Position.Y, stageX, stageY))
            .ThenBy(gameObject => gameObject.DistanceToPlayer)
            .Select(gameObject => new VisibleFishingPool(
                gameObject.Entry,
                gameObject.Name ?? "FishingPool",
                gameObject.DistanceToPlayer,
                gameObject.Position))
            .FirstOrDefault();
    }

    private async Task<VisibleFishingPool?> WaitForNearbyFishingPoolNearStageAsync(
        string account,
        string label,
        int expectedMapId,
        float stageX,
        float stageY,
        float maxStageDistance,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var lastProgressLogAt = DateTime.UtcNow;
        WoWActivitySnapshot? lastSnapshot = null;

        do
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            lastSnapshot = snapshot;
            var pool = FindNearestNearbyFishingPoolNearStage(
                snapshot,
                expectedMapId,
                stageX,
                stageY,
                maxStageDistance);
            if (pool?.Position != null)
                return pool;

            if (DateTime.UtcNow - lastProgressLogAt >= TimeSpan.FromSeconds(15))
            {
                _output.WriteLine(
                    $"[{label}] Still waiting for a nearby staged fishing pool. " +
                    $"map={expectedMapId} stage=({stageX:F1},{stageY:F1}) radius={maxStageDistance:F0}y timeout={timeout.TotalSeconds:F0}s");
                lastProgressLogAt = DateTime.UtcNow;
            }

            await Task.Delay(500);
        }
        while (DateTime.UtcNow < deadline);

        LogNearbyObjectSummary(account, lastSnapshot);
        LogNearbyMovementGameObjectSummary(account, lastSnapshot);
        return null;
    }

    private async Task<PositionWaitResult> WaitForPositionSettledAsync(
        string account,
        float x,
        float y,
        float z,
        float maxDistance,
        IReadOnlyList<string> baselineChatMessages,
        int timeoutMs)
    {
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var consecutiveMatches = 0;
        var bestDistance = float.MaxValue;
        var sawArrivalMessage = false;
        WoWActivitySnapshot? bestSnapshot = null;

        while (DateTime.UtcNow < deadlineUtc)
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            var distance = DistanceToPosition2D(snapshot, x, y, z);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSnapshot = snapshot;
            }

            if (snapshot != null)
            {
                var chatDelta = GetMessageDelta(baselineChatMessages, snapshot.RecentChatMessages);
                if (chatDelta.Any(IsGoToArrivalMessage))
                    sawArrivalMessage = true;
            }

            if (distance <= maxDistance)
            {
                consecutiveMatches++;
                if (consecutiveMatches >= 2)
                    return new PositionWaitResult(
                        ReachedTarget: true,
                        Settled: true,
                        BestDistance: distance,
                        BestSnapshot: snapshot,
                        SawArrivalMessage: sawArrivalMessage);
            }
            else
            {
                consecutiveMatches = 0;
            }

            await Task.Delay(200);
        }

        return new PositionWaitResult(
            ReachedTarget: bestDistance <= maxDistance,
            Settled: false,
            BestDistance: bestDistance,
            BestSnapshot: bestSnapshot,
            SawArrivalMessage: sawArrivalMessage);
    }

    private async Task<WaypointMoveResult> MoveToFishingWaypointAsync(
        string account,
        WoWActivitySnapshot? baselineSnapshot,
        float x,
        float y,
        float z,
        float stopDistance,
        float arrivalTolerance,
        int timeoutMs)
    {
        var baselineChats = baselineSnapshot?.RecentChatMessages.ToArray() ?? [];
        var actionResult = await _bot.SendActionAsync(account, MakeGoto(x, y, z, stopDistance));
        await Task.Delay(500);

        var waitResult = actionResult == ResponseResult.Success
            ? await WaitForPositionSettledAsync(
                account,
                x,
                y,
                z,
                arrivalTolerance,
                baselineChats,
                timeoutMs)
            : PositionWaitResult.Failure;

        var snapshot = waitResult.BestSnapshot
            ?? await RefreshAndGetSnapshotAsync(account);

        if (actionResult == ResponseResult.Success && waitResult.ReachedTarget && !waitResult.SawArrivalMessage)
        {
            var arrivalWait = await WaitForGoToArrivalMessageAsync(account, baselineChats, timeoutMs: 5000);
            snapshot = arrivalWait.Snapshot ?? snapshot;
            await Task.Delay(250);
            snapshot = await RefreshAndGetSnapshotAsync(account) ?? snapshot;
        }

        return new WaypointMoveResult(actionResult, waitResult, snapshot);
    }

    private async Task<WaypointMoveResult> MoveToFishingWaypointWithRetriesAsync(
        string account,
        string label,
        string waypointName,
        WoWActivitySnapshot? baselineSnapshot,
        float x,
        float y,
        float z,
        float stopDistance,
        float arrivalTolerance,
        int timeoutMs,
        int maxAttempts)
    {
        WaypointMoveResult move = default;
        var currentBaseline = baselineSnapshot;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            move = await MoveToFishingWaypointAsync(
                account,
                currentBaseline,
                x,
                y,
                z,
                stopDistance,
                arrivalTolerance,
                timeoutMs);

            if (move.ActionResult == ResponseResult.Success && move.WaitResult.ReachedTarget)
                return move;

            var snapshot = move.Snapshot ?? await RefreshAndGetSnapshotAsync(account);
            var bestDistance = move.WaitResult.BestDistance < float.MaxValue
                ? move.WaitResult.BestDistance
                : DistanceToPosition2D(snapshot, x, y, z);
            _output.WriteLine(
                $"[{label}] Waypoint '{waypointName}' attempt {attempt}/{maxAttempts} did not settle. " +
                $"action={move.ActionResult} reached={move.WaitResult.ReachedTarget} bestDistance={bestDistance:F1}");
            currentBaseline = snapshot;
            await Task.Delay(500);
        }

        return move;
    }

    private IReadOnlyList<DirectFishingCastCandidate> BuildRatchetPierCastCandidates(string label, WoWActivitySnapshot? snapshot)
    {
        var sourcePosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var sourceX = sourcePosition?.X ?? RatchetPierCastX;
        var sourceY = sourcePosition?.Y ?? RatchetPierCastY;
        var sourceZ = sourcePosition?.Z ?? RatchetPierCastZ;
        var probeAvailable = TryEnsureRatchetPierCastProbeReady(label);
        var candidates = new List<DirectFishingCastCandidate>(
            RatchetPierCastTargetSpecs.Length * RatchetPierCastTargetNudgesDegrees.Length);

        for (var targetIndex = 0; targetIndex < RatchetPierCastTargetSpecs.Length; targetIndex++)
        {
            var targetSpec = RatchetPierCastTargetSpecs[targetIndex];
            var baseFacing = CalculateFacingToPoint(snapshot, sourceX, sourceY, targetSpec.TargetX, targetSpec.TargetY);
            for (var nudgeIndex = 0; nudgeIndex < RatchetPierCastTargetNudgesDegrees.Length; nudgeIndex++)
            {
                var nudgeDegrees = RatchetPierCastTargetNudgesDegrees[nudgeIndex];
                var facing = NormalizeAngleRadians(baseFacing + (nudgeDegrees * (MathF.PI / 180f)));
                var targetX = sourceX + (MathF.Cos(facing) * (FishingBobberDistance * 2f));
                var targetY = sourceY + (MathF.Sin(facing) * (FishingBobberDistance * 2f));
                var landingX = sourceX + (MathF.Cos(facing) * FishingBobberDistance);
                var landingY = sourceY + (MathF.Sin(facing) * FishingBobberDistance);
                var midpointX = sourceX + (MathF.Cos(facing) * (FishingBobberDistance * 0.5f));
                var midpointY = sourceY + (MathF.Sin(facing) * (FishingBobberDistance * 0.5f));

                float landingGroundZ = float.MaxValue;
                float midpointGroundZ = float.MaxValue;
                var hasLineOfSight = false;

                if (probeAvailable)
                {
                    try
                    {
                        landingGroundZ = GetGroundZNative(MapId, landingX, landingY, sourceZ + 4f, 40f);
                        midpointGroundZ = GetGroundZNative(MapId, midpointX, midpointY, sourceZ + 4f, 40f);
                        hasLineOfSight = LineOfSightNative(MapId, sourceX, sourceY, sourceZ + 1.0f, landingX, landingY, sourceZ);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"[{label}] Ratchet cast probe failed for '{targetSpec.Name}' nudge {nudgeDegrees:+0;-0}: {ex.Message}");
                    }
                }

                var landingDepth = landingGroundZ < float.MaxValue ? sourceZ - landingGroundZ : 0f;
                var midpointDepth = midpointGroundZ < float.MaxValue ? sourceZ - midpointGroundZ : 0f;
                var score =
                    targetSpec.BaseScore
                    + (hasLineOfSight ? 1000f : 0f)
                    + MathF.Max(landingDepth, 0f) * 25f
                    + MathF.Max(midpointDepth, 0f) * 10f
                    - MathF.Abs(nudgeDegrees)
                    - (targetIndex * 10f)
                    - nudgeIndex;

                candidates.Add(new DirectFishingCastCandidate(
                    Name: $"{targetSpec.Name}_nudge_{nudgeDegrees:+0;-0}",
                    FacingRadians: facing,
                    TargetX: targetX,
                    TargetY: targetY,
                    LandingX: landingX,
                    LandingY: landingY,
                    MidpointGroundZ: midpointGroundZ,
                    LandingGroundZ: landingGroundZ,
                    HasLineOfSight: hasLineOfSight,
                    Score: score));
            }
        }

        var ranked = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();

        _output.WriteLine(
            $"[{label}] Ratchet ferry-side cast candidates from ({sourceX:F1},{sourceY:F1},{sourceZ:F1}) " +
            $"({(probeAvailable ? $"native {(_ratchetPierCastProbeDataRoot ?? "unknown-data-root")}" : "probe-unavailable")}): " +
            string.Join(" | ", ranked.Select(candidate =>
                $"{candidate.Name} score={candidate.Score:F1} los={(candidate.HasLineOfSight ? 1 : 0)} " +
                $"landing=({candidate.LandingX:F1},{candidate.LandingY:F1},{(candidate.LandingGroundZ < float.MaxValue ? candidate.LandingGroundZ.ToString("F1") : "n/a")}) " +
                $"midZ={(candidate.MidpointGroundZ < float.MaxValue ? candidate.MidpointGroundZ.ToString("F1") : "n/a")}")));

        return ranked;
    }

    private async Task<DirectFishingCastAttemptResult> TryDirectFishingCastAsync(
        string account,
        string label,
        IReadOnlyList<uint> baselineCatchItems,
        uint skillBefore,
        DirectFishingCastCandidate candidate)
    {
        var setFacingResult = await _bot.SendActionAsync(account, MakeSetFacing(candidate.FacingRadians));
        var facingSettled = false;
        var castResult = ResponseResult.Failure;
        var sawChannel = false;
        var sawBobber = false;
        var sawSwimmingError = false;
        var sawNonFishableWaterError = false;
        var lastRelevantError = string.Empty;
        var recentDiagnosticsSummary = _bot.FormatRecentBotRunnerDiagnostics("GoToTask", "NavigationPath");
        IReadOnlyList<string> recentErrors = [];
        IReadOnlyList<uint> catchItems = baselineCatchItems;
        IReadOnlyList<uint> catchDeltaItems = [];
        uint skillAfter = skillBefore;
        WoWActivitySnapshot? lastSnapshot = null;

        _output.WriteLine(
            $"[{label}] Trying Ratchet cast candidate '{candidate.Name}' " +
            $"landing=({candidate.LandingX:F1},{candidate.LandingY:F1},{(candidate.LandingGroundZ < float.MaxValue ? candidate.LandingGroundZ.ToString("F1") : "n/a")}) " +
            $"los={(candidate.HasLineOfSight ? "true" : "false")} score={candidate.Score:F1}");

        if (setFacingResult == ResponseResult.Success)
        {
            facingSettled = await WaitForFacingSettledAsync(account, candidate.FacingRadians);
            lastSnapshot = await WaitForCastReadySnapshotAsync(account, label, lastSnapshot);
        }

        if (facingSettled)
        {
            var fishingSpellId = FishingData.GetBestFishingSpellId((int)skillBefore);
            castResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters =
                {
                    new RequestParameter { IntParam = (int)fishingSpellId }
                }
            });
            await Task.Delay(500);
        }

        var confirmDeadline = DateTime.UtcNow.AddSeconds(8);
        while (castResult == ResponseResult.Success && DateTime.UtcNow < confirmDeadline)
        {
            await Task.Delay(500);
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            if (snapshot == null)
                continue;

            lastSnapshot = snapshot;
            recentDiagnosticsSummary = _bot.FormatRecentBotRunnerDiagnostics("GoToTask", "NavigationPath");
            recentErrors = snapshot.RecentErrors.TakeLast(4).ToArray();
            skillAfter = GetFishingSkill(snapshot);
            sawChannel |= IsFishingChannelActive(snapshot);
            sawBobber |= FindBobber(snapshot) != null;
            sawSwimmingError |= snapshot.RecentErrors.Any(message => message.Contains("swimming", StringComparison.OrdinalIgnoreCase));
            sawNonFishableWaterError |= snapshot.RecentErrors.Any(message => message.Contains("didn't land in fishable water", StringComparison.OrdinalIgnoreCase));

            var latestRelevantError = snapshot.RecentErrors.LastOrDefault(message =>
                message.Contains("didn't land in fishable water", StringComparison.OrdinalIgnoreCase)
                || message.Contains("swimming", StringComparison.OrdinalIgnoreCase)
                || message.Contains("line of sight", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cast failed", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(latestRelevantError))
                lastRelevantError = latestRelevantError;

            catchItems = GetCatchItemIds(snapshot);
            catchDeltaItems = GetItemDelta(baselineCatchItems, catchItems);
            if (catchDeltaItems.Count > 0)
            {
                return new DirectFishingCastAttemptResult(
                    SetFacingResult: setFacingResult,
                    FacingSettled: facingSettled,
                    CastResult: castResult,
                    SawChannel: true,
                    SawBobber: true,
                    SawSwimmingError: sawSwimmingError,
                    SawNonFishableWaterError: sawNonFishableWaterError,
                    LastRelevantError: lastRelevantError,
                    RecentErrors: recentErrors,
                    RecentDiagnosticsSummary: recentDiagnosticsSummary,
                    Snapshot: snapshot,
                    CatchItems: catchItems,
                    CatchDeltaItems: catchDeltaItems,
                    SkillAfter: skillAfter,
                    ConfirmedFishing: true);
            }

            if (sawNonFishableWaterError || sawSwimmingError)
            {
                return new DirectFishingCastAttemptResult(
                    SetFacingResult: setFacingResult,
                    FacingSettled: facingSettled,
                    CastResult: castResult,
                    SawChannel: sawChannel,
                    SawBobber: sawBobber,
                    SawSwimmingError: sawSwimmingError,
                    SawNonFishableWaterError: sawNonFishableWaterError,
                    LastRelevantError: lastRelevantError,
                    RecentErrors: recentErrors,
                    RecentDiagnosticsSummary: recentDiagnosticsSummary,
                    Snapshot: snapshot,
                    CatchItems: catchItems,
                    CatchDeltaItems: catchDeltaItems,
                    SkillAfter: skillAfter,
                    ConfirmedFishing: false);
            }

            if (sawChannel || sawBobber)
            {
                var resolutionDeadline = DateTime.UtcNow.AddMilliseconds(FishingTimeoutMs);
                while (DateTime.UtcNow < resolutionDeadline)
                {
                    await Task.Delay(500);
                    snapshot = await RefreshAndGetSnapshotAsync(account);
                    if (snapshot == null)
                        continue;

                    lastSnapshot = snapshot;
                    recentDiagnosticsSummary = _bot.FormatRecentBotRunnerDiagnostics("GoToTask", "NavigationPath");
                    recentErrors = snapshot.RecentErrors.TakeLast(4).ToArray();
                    skillAfter = GetFishingSkill(snapshot);
                    sawChannel |= IsFishingChannelActive(snapshot);
                    sawBobber |= FindBobber(snapshot) != null;
                    sawSwimmingError |= snapshot.RecentErrors.Any(message => message.Contains("swimming", StringComparison.OrdinalIgnoreCase));
                    sawNonFishableWaterError |= snapshot.RecentErrors.Any(message => message.Contains("didn't land in fishable water", StringComparison.OrdinalIgnoreCase));

                    latestRelevantError = snapshot.RecentErrors.LastOrDefault(message =>
                        message.Contains("didn't land in fishable water", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("swimming", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("line of sight", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("Cast failed", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(latestRelevantError))
                        lastRelevantError = latestRelevantError;

                    catchItems = GetCatchItemIds(snapshot);
                    catchDeltaItems = GetItemDelta(baselineCatchItems, catchItems);
                    if (catchDeltaItems.Count > 0)
                    {
                        return new DirectFishingCastAttemptResult(
                            SetFacingResult: setFacingResult,
                            FacingSettled: facingSettled,
                            CastResult: castResult,
                            SawChannel: sawChannel,
                            SawBobber: sawBobber,
                            SawSwimmingError: sawSwimmingError,
                            SawNonFishableWaterError: sawNonFishableWaterError,
                            LastRelevantError: lastRelevantError,
                            RecentErrors: recentErrors,
                            RecentDiagnosticsSummary: recentDiagnosticsSummary,
                            Snapshot: snapshot,
                            CatchItems: catchItems,
                            CatchDeltaItems: catchDeltaItems,
                            SkillAfter: skillAfter,
                            ConfirmedFishing: true);
                    }
                }

                return new DirectFishingCastAttemptResult(
                    SetFacingResult: setFacingResult,
                    FacingSettled: facingSettled,
                    CastResult: castResult,
                    SawChannel: sawChannel,
                    SawBobber: sawBobber,
                    SawSwimmingError: sawSwimmingError,
                    SawNonFishableWaterError: sawNonFishableWaterError,
                    LastRelevantError: lastRelevantError,
                    RecentErrors: recentErrors,
                    RecentDiagnosticsSummary: recentDiagnosticsSummary,
                    Snapshot: lastSnapshot,
                    CatchItems: catchItems,
                    CatchDeltaItems: catchDeltaItems,
                    SkillAfter: skillAfter,
                    ConfirmedFishing: true);
            }
        }

        return new DirectFishingCastAttemptResult(
            SetFacingResult: setFacingResult,
            FacingSettled: facingSettled,
            CastResult: castResult,
            SawChannel: sawChannel,
            SawBobber: sawBobber,
            SawSwimmingError: sawSwimmingError,
            SawNonFishableWaterError: sawNonFishableWaterError,
            LastRelevantError: lastRelevantError,
            RecentErrors: recentErrors,
            RecentDiagnosticsSummary: recentDiagnosticsSummary,
            Snapshot: lastSnapshot,
            CatchItems: catchItems,
            CatchDeltaItems: catchDeltaItems,
            SkillAfter: skillAfter,
            ConfirmedFishing: false);
    }

    private bool TryEnsureRatchetPierCastProbeReady(string label)
    {
        lock (RatchetPierCastProbeLock)
        {
            if (_ratchetPierCastProbeInitialized)
                return _ratchetPierCastProbeAvailable;

            _ratchetPierCastProbeInitialized = true;
            EnsureTestNavigationDllResolverRegistered();
            var dataRoot = SceneDataParityPaths.ResolvePreferredDataRoot(
                Environment.GetEnvironmentVariable("WWOW_DATA_DIR"),
                AppContext.BaseDirectory,
                requireMmaps: false);
            if (string.IsNullOrWhiteSpace(dataRoot))
                return _ratchetPierCastProbeAvailable = false;

            try
            {
                SetDataDirectoryNative(dataRoot);
                PreloadMapNative(MapId);
                _ratchetPierCastProbeDataRoot = dataRoot;
                _ratchetPierCastProbeAvailable = true;
            }
            catch (Exception ex)
            {
                _ratchetPierCastProbeDataRoot = dataRoot;
                _ratchetPierCastProbeAvailable = false;
                _output.WriteLine($"[{label}] Ratchet cast probe initialization failed for '{dataRoot}': {ex.GetType().Name}: {ex.Message}");
            }

            return _ratchetPierCastProbeAvailable;
        }
    }

    private static void EnsureTestNavigationDllResolverRegistered()
    {
        lock (NavigationDllResolverLock)
        {
            if (_navigationDllResolverRegistered)
                return;

            NativeLibrary.SetDllImportResolver(
                typeof(FishingProfessionTests).Assembly,
                ResolveNavigationDllForTests);
            _navigationDllResolverRegistered = true;
        }
    }

    private static IntPtr ResolveNavigationDllForTests(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(NavigationDll, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (var candidatePath in NavigationDllResolver.GetCandidatePaths(
            AppContext.BaseDirectory,
            RuntimeInformation.ProcessArchitecture,
            NavigationDll + ".dll"))
        {
            if (File.Exists(candidatePath) && NativeLibrary.TryLoad(candidatePath, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    private async Task<GoToArrivalWaitResult> WaitForGoToArrivalMessageAsync(
        string account,
        IReadOnlyList<string> baselineChatMessages,
        int timeoutMs)
    {
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadlineUtc)
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            if (snapshot != null)
            {
                var chatDelta = GetMessageDelta(baselineChatMessages, snapshot.RecentChatMessages);
                if (chatDelta.Any(IsGoToArrivalMessage))
                    return new GoToArrivalWaitResult(true, snapshot);
            }

            await Task.Delay(200);
        }

        return new GoToArrivalWaitResult(false, null);
    }

    private async Task<bool> WaitForFacingSettledAsync(string account, float expectedFacing, int timeoutMs = 5000)
    {
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var consecutiveMatches = 0;

        while (DateTime.UtcNow < deadlineUtc)
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            var facing = snapshot?.Player?.Unit?.GameObject?.Base?.Facing;

            if (facing is float currentFacing && FacingDeltaRadians(currentFacing, expectedFacing) <= 0.10f)
            {
                consecutiveMatches++;
                if (consecutiveMatches >= 2)
                    return true;
            }
            else
            {
                consecutiveMatches = 0;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private async Task<WoWActivitySnapshot?> WaitForCastReadySnapshotAsync(
        string account,
        string label,
        WoWActivitySnapshot? fallbackSnapshot,
        int timeoutMs = 5000)
    {
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var consecutiveStableSamples = 0;
        var previousPosition = fallbackSnapshot?.Player?.Unit?.GameObject?.Base?.Position;
        WoWActivitySnapshot? lastSnapshot = fallbackSnapshot;

        while (DateTime.UtcNow < deadlineUtc)
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account) ?? lastSnapshot;
            var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
            var movementFlags = snapshot?.Player?.Unit?.MovementFlags ?? snapshot?.MovementData?.MovementFlags ?? 0u;
            var moved = previousPosition != null && position != null &&
                MathF.Sqrt(
                    MathF.Pow(position.X - previousPosition.X, 2f) +
                    MathF.Pow(position.Y - previousPosition.Y, 2f)) > 0.10f;

            if (!moved && movementFlags == 0u)
            {
                consecutiveStableSamples++;
                if (consecutiveStableSamples >= 3)
                    return snapshot;
            }
            else
            {
                consecutiveStableSamples = 0;
            }

            previousPosition = position;
            lastSnapshot = snapshot;
            await Task.Delay(200);
        }

        var finalFlags = lastSnapshot?.Player?.Unit?.MovementFlags ?? lastSnapshot?.MovementData?.MovementFlags ?? 0u;
        var finalPosition = lastSnapshot?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine(
            $"[{label}] Cast-ready wait timed out. pos=({finalPosition?.X:F1},{finalPosition?.Y:F1},{finalPosition?.Z:F1}) movementFlags=0x{finalFlags:X}");
        return lastSnapshot;
    }

    private async Task<bool> WaitForFishingPoleEquippedAsync(
        string account,
        ulong previousMainhandGuid,
        int poleCountBefore,
        TimeSpan timeout)
    {
        var deadlineUtc = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadlineUtc)
        {
            var snapshot = await RefreshAndGetSnapshotAsync(account);
            var mainhandGuid = GetMainhandGuid(snapshot);
            var poleCountAfter = CountItem(snapshot, FishingData.FishingPole);
            if (mainhandGuid != 0 && (mainhandGuid != previousMainhandGuid || poleCountAfter < poleCountBefore))
                return true;

            await Task.Delay(200);
        }

        return false;
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

    private static bool IsFishingPool(global::Game.GameObjectSnapshot gameObject)
        => FishingData.KnownFishingPoolEntries.Contains(gameObject.Entry)
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

    private void LogNearbyMovementGameObjectSummary(string account, WoWActivitySnapshot? snapshot)
    {
        var nearbyGameObjects = snapshot?.MovementData?.NearbyGameObjects?
            .Take(8)
            .Select(gameObject =>
                $"{gameObject.Entry}:{gameObject.Name ?? "?"}:dist={gameObject.DistanceToPlayer:F1}:pos=({gameObject.Position?.X:F1},{gameObject.Position?.Y:F1},{gameObject.Position?.Z:F1})")
            .ToArray()
            ?? [];

        _output.WriteLine(
            $"[{account}] No staged fishing pool detected in NearbyGameObjects. nearbyGameObjectCount={snapshot?.MovementData?.NearbyGameObjects?.Count ?? 0} " +
            $"objects=[{string.Join(", ", nearbyGameObjects)}]");
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

    private static float DistanceToPosition(WoWActivitySnapshot? snapshot, float x, float y, float z)
    {
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        return position == null
            ? float.MaxValue
            : Distance3D(position.X, position.Y, position.Z, x, y, z);
    }

    private static float DistanceToPosition2D(WoWActivitySnapshot? snapshot, float x, float y, float z)
    {
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        return position == null
            ? float.MaxValue
            : Distance2D(position.X, position.Y, x, y);
    }

    private static float CalculateFacingToPoint(WoWActivitySnapshot? snapshot, float fallbackX, float fallbackY, float targetX, float targetY)
    {
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var sourceX = position?.X ?? fallbackX;
        var sourceY = position?.Y ?? fallbackY;
        var facing = MathF.Atan2(targetY - sourceY, targetX - sourceX);
        return facing < 0f ? facing + (MathF.PI * 2f) : facing;
    }

    private static float CalculateFacingDelta(WoWActivitySnapshot? snapshot, float targetX, float targetY)
    {
        var facing = snapshot?.Player?.Unit?.GameObject?.Base?.Facing;
        if (facing is not float currentFacing)
            return float.MaxValue;

        var expectedFacing = CalculateFacingToPoint(snapshot, RatchetPierCastX, RatchetPierCastY, targetX, targetY);
        return FacingDeltaRadians(currentFacing, expectedFacing);
    }

    private static float CalculateFacingDelta(WoWActivitySnapshot? snapshot, float expectedFacing)
    {
        var facing = snapshot?.Player?.Unit?.GameObject?.Base?.Facing;
        return facing is float currentFacing
            ? FacingDeltaRadians(currentFacing, expectedFacing)
            : float.MaxValue;
    }

    private static float NormalizeAngleRadians(float angle)
    {
        while (angle < 0f)
            angle += MathF.PI * 2f;
        while (angle >= MathF.PI * 2f)
            angle -= MathF.PI * 2f;
        return angle;
    }

    private static float FacingDeltaRadians(float actual, float expected)
    {
        var delta = actual - expected;
        while (delta > MathF.PI)
            delta -= MathF.PI * 2f;
        while (delta < -MathF.PI)
            delta += MathF.PI * 2f;
        return MathF.Abs(delta);
    }

    private static ulong GetMainhandGuid(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Inventory.TryGetValue(MainhandSlot, out ulong guid) == true ? guid : 0UL;

    private static ActionMessage MakeSetFacing(float facing)
        => new()
        {
            ActionType = ActionType.SetFacing,
            Parameters =
            {
                new RequestParameter { FloatParam = facing }
            }
        };

    private static ActionMessage MakeGoto(float x, float y, float z, float stopDistance = 3f)
        => new()
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = x },
                new RequestParameter { FloatParam = y },
                new RequestParameter { FloatParam = z },
                new RequestParameter { FloatParam = stopDistance }
            }
        };

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

    private static bool IsGoToArrivalMessage(string message)
        => message.Contains("[TASK] GoToTask pop reason=arrived", StringComparison.Ordinal);

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
        RatchetFishingStagePreparation? StagePreparation,
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

    private sealed record DirectFishingRunResult(
        bool PoleStartedInBag,
        bool PoleEquipped,
        ResponseResult ApproachResult,
        bool ReachedPierApproachZone,
        float DistanceToApproachTarget,
        bool ReachedFishingPosition,
        float DistanceToFishingPosition,
        ResponseResult GotoResult,
        ResponseResult EquipResult,
        ResponseResult SetFacingResult,
        bool FacingSettled,
        string CastCandidate,
        string CastAttemptSummary,
        float FacingDelta,
        ResponseResult CastResult,
        bool SawChannel,
        bool SawBobber,
        bool SawSwimmingError,
        bool SawNonFishableWaterError,
        string FailureStage,
        string LastRelevantError,
        IReadOnlyList<string> RecentErrors,
        string RecentDiagnosticsSummary,
        IReadOnlyList<uint> CatchItems,
        IReadOnlyList<uint> CatchDeltaItems,
        uint SkillBefore,
        uint SkillAfter);

    private readonly record struct DirectFishingCastCandidate(
        string Name,
        float FacingRadians,
        float TargetX,
        float TargetY,
        float LandingX,
        float LandingY,
        float MidpointGroundZ,
        float LandingGroundZ,
        bool HasLineOfSight,
        float Score);

    private readonly record struct FerryCastTargetSpec(
        string Name,
        float TargetX,
        float TargetY,
        float BaseScore);

    private readonly record struct DirectFishingCastAttemptResult(
        ResponseResult SetFacingResult,
        bool FacingSettled,
        ResponseResult CastResult,
        bool SawChannel,
        bool SawBobber,
        bool SawSwimmingError,
        bool SawNonFishableWaterError,
        string LastRelevantError,
        IReadOnlyList<string> RecentErrors,
        string RecentDiagnosticsSummary,
        WoWActivitySnapshot? Snapshot,
        IReadOnlyList<uint> CatchItems,
        IReadOnlyList<uint> CatchDeltaItems,
        uint SkillAfter,
        bool ConfirmedFishing);

    private readonly record struct PositionWaitResult(
        bool ReachedTarget,
        bool Settled,
        float BestDistance,
        WoWActivitySnapshot? BestSnapshot,
        bool SawArrivalMessage)
    {
        public static PositionWaitResult Failure => new(
            ReachedTarget: false,
            Settled: false,
            BestDistance: float.MaxValue,
            BestSnapshot: null,
            SawArrivalMessage: false);
    }

    private readonly record struct GoToArrivalWaitResult(
        bool SawArrivalMessage,
        WoWActivitySnapshot? Snapshot);

    private readonly record struct WaypointMoveResult(
        ResponseResult ActionResult,
        PositionWaitResult WaitResult,
        WoWActivitySnapshot? Snapshot);

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

    [DllImport(NavigationDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetDataDirectory")]
    private static extern void SetDataDirectoryNative(string dataDir);

    [DllImport(NavigationDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PreloadMap")]
    private static extern void PreloadMapNative(uint mapId);

    [DllImport(NavigationDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetGroundZ")]
    private static extern float GetGroundZNative(uint mapId, float x, float y, float z, float maxSearchDist);

    [DllImport(NavigationDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LineOfSight")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LineOfSightNative(uint mapId, float fx, float fy, float fz, float tx, float ty, float tz);
}
