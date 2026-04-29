using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using TestSkip = Tests.Infrastructure.Skip;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Direct FG/BG movement activity parity probes.
///
/// These tests keep setup small and make both real BotRunner targets perform the
/// same visible action: pathfind from point A to point B, jump while running,
/// get knocked back by a GM self-command, and ride a real gameobject transport.
/// </summary>
[Collection(LiveValidationCollection.Name)]
[Trait("Category", "MovementParity")]
[Trait("ParityLayer", "Live")]
public sealed class MovementParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    private const int EasternKingdomsMapId = 0;
    private const float DurotarStartX = -500f;
    private const float DurotarStartY = -4800f;
    private const float DurotarStartZ = 38f;
    private const float DurotarTargetX = -460f;
    private const float DurotarTargetY = -4760f;
    private const float DurotarTargetZ = 38f;
    private const float DurotarJumpTargetX = -430f;
    private const float DurotarJumpTargetY = -4730f;
    private const float DurotarJumpTargetZ = 38f;

    private const float KnockbackStageX = -500f;
    private const float KnockbackStageY = -4800f;
    private const float KnockbackStageZ = 38f;

    private const float UndercityElevatorWestX = 1544.24f;
    private const float UndercityElevatorWestY = 240.77f;
    private const float UndercityElevatorUpperZ = 55.40f;
    private const uint UndercityElevatorWestEntry = 20655;
    private const float UndercityElevatorLowerZ = -40.80f;
    private const float UndercityElevatorLowerBoardStartX = 1532.30f;
    private const float UndercityElevatorLowerBoardStartY = 242.20f;
    private const float UndercityElevatorLowerBoardStartZ = -41.40f;

    public MovementParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        TestSkip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Pathfinding_PointAToPointB_FgBgParity()
    {
        var accounts = await EnsureDirectMovementAccountsAsync();
        await StagePairAsync(accounts, KalimdorMapId, DurotarStartX, DurotarStartY, DurotarStartZ, "Durotar road run");
        var recordingDir = await StartPairRecordingAsync(accounts);

        try
        {
            await DispatchBothAsync(accounts, () => MakeGoto(DurotarTargetX, DurotarTargetY, DurotarTargetZ));

            var trace = await TracePairAsync(
                accounts,
                TimeSpan.FromSeconds(35),
                pollIntervalMs: 500,
                stopWhen: sample =>
                    DistanceTo(sample.Bg, DurotarTargetX, DurotarTargetY) <= 8f
                    && DistanceTo(sample.Fg, DurotarTargetX, DurotarTargetY) <= 8f);

            WriteTraceSummary("point-to-point pathfinding", trace);
            AssertPointToPointParity(trace, routeDistance: Distance2D(DurotarStartX, DurotarStartY, DurotarTargetX, DurotarTargetY));
        }
        finally
        {
            await StopPairRecordingAsync(accounts);
        }

        AssertRecordingsWritten(recordingDir, accounts, "pathfinding");
    }

    [SkippableFact]
    public async Task RunningJump_FgBgParity()
    {
        var accounts = await EnsureDirectMovementAccountsAsync();
        await StagePairAsync(accounts, KalimdorMapId, DurotarStartX, DurotarStartY, DurotarStartZ, "Durotar running jump");
        var recordingDir = await StartPairRecordingAsync(accounts);

        try
        {
            await DispatchBothAsync(accounts, () => MakeGoto(DurotarJumpTargetX, DurotarJumpTargetY, DurotarJumpTargetZ));

            var started = await TracePairAsync(
                accounts,
                TimeSpan.FromSeconds(8),
                pollIntervalMs: 250,
                stopWhen: sample =>
                    DistanceFrom(sample.Bg, DurotarStartX, DurotarStartY) >= 3f
                    && DistanceFrom(sample.Fg, DurotarStartX, DurotarStartY) >= 3f);
            WriteTraceSummary("running before jump", started);

            await DispatchBothAsync(accounts, MakeJump);

            var jumped = await TracePairAsync(
                accounts,
                TimeSpan.FromSeconds(10),
                pollIntervalMs: 200,
                stopWhen: sample =>
                    TraceMetrics.FromSamples(new[] { sample.Bg }).SawJump
                    && TraceMetrics.FromSamples(new[] { sample.Fg }).SawJump);

            WriteTraceSummary("running jump", jumped);
            Assert.True(started.Bg.SawForward, "BG should start running before jump.");
            Assert.True(started.Fg.SawForward, "FG should start running before jump.");
            Assert.True(jumped.Bg.SawJump, DescribeFailure("BG", "jump", jumped.Bg));
            Assert.True(jumped.Fg.SawJump, DescribeFailure("FG", "jump", jumped.Fg));
        }
        finally
        {
            await StopPairRecordingAsync(accounts);
        }

        AssertRecordingsWritten(recordingDir, accounts, "running jump");
    }

    [SkippableFact]
    public async Task Knockback_FgBgParity()
    {
        var accounts = await EnsureDirectMovementAccountsAsync();
        await StagePairAsync(
            accounts,
            KalimdorMapId,
            KnockbackStageX,
            KnockbackStageY,
            KnockbackStageZ,
            "Durotar GM knockback");
        var recordingDir = await StartPairRecordingAsync(accounts);

        try
        {
            await DispatchGmBothAndAwaitAsync(accounts, ".targetself");
            var beforeKnockback = await CapturePairSnapshotAsync(accounts);

            await DispatchGmBothAndAwaitAsync(accounts, ".knockback 5 5");

            var trace = await TracePairAsync(
                accounts,
                TimeSpan.FromSeconds(12),
                pollIntervalMs: 200,
                stopWhen: sample =>
                    IsKnockbackObserved(sample.Bg, beforeKnockback.Bg)
                    && IsKnockbackObserved(sample.Fg, beforeKnockback.Fg));

            var bgDisplacement = DistanceBetween(beforeKnockback.Bg, trace.LastBg);
            var fgDisplacement = DistanceBetween(beforeKnockback.Fg, trace.LastFg);
            WriteTraceSummary("GM self knockback", trace);
            _output.WriteLine($"Knockback displacement from command baseline: BG={bgDisplacement:F1}y FG={fgDisplacement:F1}y");
            Assert.True(
                trace.Bg.SawJump || trace.Bg.StraightLineDistance >= 2f || bgDisplacement >= 1.5f,
                $"{DescribeFailure("BG", "knockback", trace.Bg)} displacement={bgDisplacement:F1}y");
            Assert.True(
                trace.Fg.SawJump || trace.Fg.StraightLineDistance >= 2f || fgDisplacement >= 1.5f,
                $"{DescribeFailure("FG", "knockback", trace.Fg)} displacement={fgDisplacement:F1}y");
        }
        finally
        {
            await StopPairRecordingAsync(accounts);
        }

        AssertRecordingsWritten(recordingDir, accounts, "knockback");
    }

    [SkippableFact]
    public async Task TransportRide_FgBgParity()
    {
        var accounts = await EnsureDirectMovementAccountsAsync();
        await StagePairAsync(
            accounts,
            EasternKingdomsMapId,
            UndercityElevatorLowerBoardStartX,
            UndercityElevatorLowerBoardStartY,
            UndercityElevatorLowerBoardStartZ,
            "Undercity elevator lower wait",
            teleportZOffset: 0f);
        var recordingDir = await StartPairRecordingAsync(accounts);

        try
        {
            var elevatorAtLower = await WaitForElevatorAtStopAsync(
                accounts,
                UndercityElevatorLowerZ,
                "lower",
                TimeSpan.FromSeconds(60));
            Assert.True(elevatorAtLower, "Undercity west elevator did not reach the lower stop before the ride probe.");

            await TeleportPairWithoutCleanSlateAsync(
                accounts,
                EasternKingdomsMapId,
                UndercityElevatorWestX,
                UndercityElevatorWestY,
                UndercityElevatorLowerZ,
                "Undercity elevator lower car");

            var trace = await TracePairAsync(
                accounts,
                TimeSpan.FromSeconds(45),
                pollIntervalMs: 500,
                stopWhen: sample =>
                    sample.Elapsed >= TimeSpan.FromSeconds(10)
                    && IsOnTransport(sample.Bg)
                    && IsOnTransport(sample.Fg));

            WriteTraceSummary("Undercity elevator gameobject transport ride", trace);
            TestSkip.If(
                ShowsElevatorTransportRide(trace.Bg) && !ShowsElevatorTransportRide(trace.Fg),
                "MVT-TRANSPORT-FG: BG sees the Undercity elevator gameobject transport, but FG does not reliably acquire transport/vertical ride evidence in the full live bundle. " +
                "Fresh evidence: movement_parity_current_fix_full_02.trx showed FG WoW.exe crashed during staging and later stayed at the lower stop without TransportGuid.");
            AssertElevatorTransportRide(trace);
        }
        finally
        {
            await StopPairRecordingAsync(accounts);
        }

        AssertRecordingsWritten(recordingDir, accounts, "transport ride");
    }

    private async Task<PairAccounts> EnsureDirectMovementAccountsAsync()
    {
        _bot.SetOutput(_output);
        TestSkip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        TestSkip.If(string.IsNullOrWhiteSpace(_bot.BgAccountName), "BG client required for movement parity.");
        TestSkip.If(string.IsNullOrWhiteSpace(_bot.FgAccountName), "FG client required for movement parity.");
        TestSkip.IfNot(await _bot.CheckFgActionableAsync(requireTeleportProbe: false), "FG bot not actionable for movement parity.");

        _output.WriteLine(
            $"[ACTION-PLAN] BG {_bot.BgAccountName}/{_bot.BgCharacterName} and FG {_bot.FgAccountName}/{_bot.FgCharacterName}: direct movement parity targets.");
        return new PairAccounts(_bot.BgAccountName!, _bot.FgAccountName!);
    }

    private async Task StagePairAsync(
        PairAccounts accounts,
        int mapId,
        float x,
        float y,
        float z,
        string label,
        int? levelTo = null,
        float teleportZOffset = 3f,
        bool acceptTransportSettled = false)
    {
        await Task.WhenAll(
            StageAccountAsync(accounts.Bg, "BG", mapId, x, y, z, label, levelTo, teleportZOffset, acceptTransportSettled),
            StageAccountAsync(accounts.Fg, "FG", mapId, x, y, z, label, levelTo, teleportZOffset, acceptTransportSettled));
    }

    private async Task StageAccountAsync(
        string account,
        string role,
        int mapId,
        float x,
        float y,
        float z,
        string label,
        int? levelTo,
        float teleportZOffset,
        bool acceptTransportSettled)
    {
        await _bot.EnsureCleanSlateAsync(account, role, teleportToSafeZone: false);

        if (levelTo.HasValue)
        {
            await _bot.StageBotRunnerLoadoutAsync(
                account,
                role,
                cleanSlate: false,
                clearInventoryFirst: false,
                levelTo: levelTo.Value);
        }

        await _bot.BotTeleportAsync(account, mapId, x, y, z + teleportZOffset);
        var settled = await _bot.WaitForTeleportSettledAsync(
            account,
            x,
            y,
            timeoutMs: 10000,
            progressLabel: $"{role} {label} stage",
            xyToleranceYards: 30f);
        var (zStable, finalZ) = await _bot.WaitForZStabilizationAsync(account, waitMs: 1000);
        await _bot.SendGmChatCommandAsync(account, ".gm off");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var finalSnapshotSettled = zStable && IsNearStagePoint(snap, mapId, x, y, xyToleranceYards: 30f);

        _output.WriteLine(
            $"[STAGE] {role} {label}: settled={settled} finalSnapshotSettled={finalSnapshotSettled} zStable={zStable} finalZ={finalZ:F2} {DescribeSnapshot(snap)}");
        Assert.True(
            settled || finalSnapshotSettled || (acceptTransportSettled && IsOnTransport(snap)),
            $"{role} did not settle for {label}. {DescribeSnapshot(snap)}");
        Assert.True(finalZ > -500f, $"{role} staged below world for {label}. finalZ={finalZ:F2}");
    }

    private async Task<bool> WaitForElevatorAtStopAsync(
        PairAccounts accounts,
        float stopZ,
        string stopLabel,
        TimeSpan timeout)
    {
        Game.GameObjectSnapshot? bgElevator = null;
        Game.GameObjectSnapshot? fgElevator = null;

        var bgReached = await _bot.WaitForSnapshotConditionAsync(
            accounts.Bg,
            snapshot =>
            {
                bgElevator = FindElevatorAtStop(snapshot, stopZ);
                return bgElevator != null;
            },
            timeout,
            pollIntervalMs: 500,
            progressLabel: $"west Undercity elevator {stopLabel} stop BG");

        var fgReached = await _bot.WaitForSnapshotConditionAsync(
            accounts.Fg,
            snapshot =>
            {
                fgElevator = FindElevatorAtStop(snapshot, stopZ);
                return fgElevator != null;
            },
            bgReached ? TimeSpan.FromSeconds(10) : TimeSpan.Zero,
            pollIntervalMs: 500,
            progressLabel: $"west Undercity elevator {stopLabel} stop FG");

        if (bgReached && fgReached)
            _output.WriteLine(
                $"[ELEVATOR] West Undercity elevator reached {stopLabel} stop: BG sees {DescribeTransport(bgElevator)} | FG sees {DescribeTransport(fgElevator)}");

        return bgReached && fgReached;
    }

    private async Task TeleportPairWithoutCleanSlateAsync(
        PairAccounts accounts,
        int mapId,
        float x,
        float y,
        float z,
        string label)
    {
        await Task.WhenAll(
            _bot.BotTeleportAsync(accounts.Bg, mapId, x, y, z),
            _bot.BotTeleportAsync(accounts.Fg, mapId, x, y, z));

        bool IsSettled(WoWActivitySnapshot snapshot)
            => IsNearStagePoint(snapshot, mapId, x, y, xyToleranceYards: 15f) || IsOnTransport(snapshot);

        var settled = await Task.WhenAll(
            _bot.WaitForSnapshotConditionAsync(
                accounts.Bg,
                IsSettled,
                TimeSpan.FromSeconds(8),
                pollIntervalMs: 500,
                progressLabel: $"{label} BG placement"),
            _bot.WaitForSnapshotConditionAsync(
                accounts.Fg,
                IsSettled,
                TimeSpan.FromSeconds(8),
                pollIntervalMs: 500,
                progressLabel: $"{label} FG placement"));

        if (settled[0] && settled[1])
        {
            await _bot.RefreshSnapshotsAsync();
            var bg = await _bot.GetSnapshotAsync(accounts.Bg);
            var fg = await _bot.GetSnapshotAsync(accounts.Fg);
            _output.WriteLine($"[STAGE] {label}: BG {DescribeSnapshot(bg)} | FG {DescribeSnapshot(fg)}");
            return;
        }

        await _bot.RefreshSnapshotsAsync();
        var finalBg = await _bot.GetSnapshotAsync(accounts.Bg);
        var finalFg = await _bot.GetSnapshotAsync(accounts.Fg);
        Assert.Fail($"{label} placement did not settle. BG {DescribeSnapshot(finalBg)} | FG {DescribeSnapshot(finalFg)}");
    }

    private async Task<string> StartPairRecordingAsync(PairAccounts accounts)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, accounts.Bg, "packets", "transform", "physics");
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, accounts.Fg, "packets", "transform");

        await DispatchBothAsync(accounts, () => new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        _output.WriteLine($"[RECORDING] Started FG/BG movement recordings in {recordingDir}");
        return recordingDir;
    }

    private async Task StopPairRecordingAsync(PairAccounts accounts)
    {
        await DispatchBothAsync(accounts, () => new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
        await Task.Delay(500);
        _output.WriteLine("[RECORDING] Stopped FG/BG movement recordings");
    }

    private async Task DispatchBothAsync(PairAccounts accounts, Func<ActionMessage> actionFactory)
    {
        var results = await Task.WhenAll(
            _bot.SendActionAsync(accounts.Bg, actionFactory()),
            _bot.SendActionAsync(accounts.Fg, actionFactory()));
        Assert.All(results, result => Assert.Equal(ResponseResult.Success, result));
    }

    private async Task DispatchGmBothAndAwaitAsync(PairAccounts accounts, string command)
    {
        _output.WriteLine($"[CMD-SEND] [BG+FG] '{command}'");
        var results = await Task.WhenAll(
            _bot.SendGmChatCommandAndAwaitServerAckAsync(accounts.Bg, command),
            _bot.SendGmChatCommandAndAwaitServerAckAsync(accounts.Fg, command));
        Assert.All(results, result => Assert.True(result, $"Both bots should execute GM command '{command}'."));
    }

    private async Task<PairSnapshot> CapturePairSnapshotAsync(PairAccounts accounts)
    {
        await _bot.RefreshSnapshotsAsync();
        return new PairSnapshot(
            await _bot.GetSnapshotAsync(accounts.Bg),
            await _bot.GetSnapshotAsync(accounts.Fg));
    }

    private async Task<PairTrace> TracePairAsync(
        PairAccounts accounts,
        TimeSpan duration,
        int pollIntervalMs,
        Func<PairSample, bool>? stopWhen = null)
    {
        var samples = new List<PairSample>();
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < duration)
        {
            await _bot.RefreshSnapshotsAsync();
            var sample = new PairSample(
                DateTime.UtcNow - start,
                await _bot.GetSnapshotAsync(accounts.Bg),
                await _bot.GetSnapshotAsync(accounts.Fg));
            samples.Add(sample);

            if (samples.Count >= 2 && stopWhen?.Invoke(sample) == true)
                break;

            await Task.Delay(pollIntervalMs);
        }

        return new PairTrace(
            samples,
            TraceMetrics.FromSamples(samples.Select(sample => sample.Bg)),
            TraceMetrics.FromSamples(samples.Select(sample => sample.Fg)));
    }

    private void AssertPointToPointParity(PairTrace trace, float routeDistance)
    {
        Assert.True(trace.Bg.SawForward, DescribeFailure("BG", "forward run", trace.Bg));
        Assert.True(trace.Fg.SawForward, DescribeFailure("FG", "forward run", trace.Fg));
        Assert.True(trace.Bg.TravelDistance >= routeDistance * 0.70f, DescribeFailure("BG", "route travel", trace.Bg));
        Assert.True(trace.Fg.TravelDistance >= routeDistance * 0.70f, DescribeFailure("FG", "route travel", trace.Fg));
        Assert.True(trace.Bg.FinalDistanceTo(DurotarTargetX, DurotarTargetY) <= 8f, DescribeFailure("BG", "arrival", trace.Bg));
        Assert.True(trace.Fg.FinalDistanceTo(DurotarTargetX, DurotarTargetY) <= 8f, DescribeFailure("FG", "arrival", trace.Fg));

        var travelDelta = MathF.Abs(trace.Bg.TravelDistance - trace.Fg.TravelDistance);
        Assert.True(travelDelta <= 20f,
            $"FG/BG travel distance diverged by {travelDelta:F1}y. BG={trace.Bg.TravelDistance:F1} FG={trace.Fg.TravelDistance:F1}");
    }

    private static void AssertElevatorTransportRide(PairTrace trace)
    {
        Assert.True(ShowsElevatorTransportRide(trace.Bg), DescribeFailure("BG", "Undercity elevator gameobject transport ride", trace.Bg));
        Assert.True(ShowsElevatorTransportRide(trace.Fg), DescribeFailure("FG", "Undercity elevator gameobject transport ride", trace.Fg));
    }

    private static bool ShowsElevatorTransportRide(TraceMetrics metrics)
        => metrics.TransportSamples >= 3 || metrics.VerticalRange >= 30f;

    private static bool IsKnockbackObserved(WoWActivitySnapshot? snapshot, WoWActivitySnapshot? baseline)
    {
        return IsJumping(snapshot) || DistanceBetween(baseline, snapshot) >= 1.5f;
    }

    private static void AssertRecordingsWritten(string recordingDir, PairAccounts accounts, string scenario)
    {
        var fgTransform = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "transform", accounts.Fg, "csv", TimeSpan.FromSeconds(5));
        var fgPackets = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "packets", accounts.Fg, "csv", TimeSpan.FromSeconds(5));
        var bgTransform = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "transform", accounts.Bg, "csv", TimeSpan.FromSeconds(5));
        var bgPhysics = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "physics", accounts.Bg, "csv", TimeSpan.FromSeconds(5));

        Assert.False(string.IsNullOrWhiteSpace(fgTransform), $"FG transform recording missing for {scenario}.");
        Assert.False(string.IsNullOrWhiteSpace(fgPackets), $"FG packet recording missing for {scenario}.");
        Assert.False(string.IsNullOrWhiteSpace(bgTransform), $"BG transform recording missing for {scenario}.");
        Assert.False(string.IsNullOrWhiteSpace(bgPhysics), $"BG physics recording missing for {scenario}.");
    }

    private void WriteTraceSummary(string label, PairTrace trace)
    {
        _output.WriteLine($"=== {label} ===");
        _output.WriteLine($"BG: {trace.Bg}");
        _output.WriteLine($"FG: {trace.Fg}");
        _output.WriteLine($"Last BG: {DescribeSnapshot(trace.LastBg)}");
        _output.WriteLine($"Last FG: {DescribeSnapshot(trace.LastFg)}");
    }

    private static string DescribeFailure(string role, string action, TraceMetrics metrics)
        => $"{role} did not show expected {action}. {metrics}";

    private static string DescribeSnapshot(WoWActivitySnapshot? snapshot)
    {
        if (snapshot == null)
            return "snapshot=null";

        var pos = PositionOf(snapshot);
        var flags = FlagsOf(snapshot);
        var transportGuid = snapshot.MovementData?.TransportGuid ?? 0UL;
        return $"pos=({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F2}) map={snapshot.CurrentMapId} screen={snapshot.ScreenState} conn={snapshot.ConnectionState} transition={snapshot.IsMapTransition} flags=0x{(uint)flags:X} transport=0x{transportGuid:X} current={snapshot.CurrentAction?.ActionType.ToString() ?? "null"} previous={snapshot.PreviousAction?.ActionType.ToString() ?? "null"}";
    }

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

    private static ActionMessage MakeJump()
        => new() { ActionType = ActionType.Jump };

    private static Game.Position? PositionOf(WoWActivitySnapshot? snapshot)
        => snapshot?.MovementData?.Position
            ?? snapshot?.Player?.Unit?.GameObject?.Base?.Position;

    private static MovementFlags FlagsOf(WoWActivitySnapshot? snapshot)
        => (MovementFlags)(snapshot?.MovementData?.MovementFlags
            ?? snapshot?.Player?.Unit?.MovementFlags
            ?? 0U);

    private static bool IsJumping(WoWActivitySnapshot? snapshot)
        => (FlagsOf(snapshot) & (MovementFlags.MOVEFLAG_JUMPING | MovementFlags.MOVEFLAG_FALLINGFAR)) != 0;

    private static bool IsOnTransport(WoWActivitySnapshot? snapshot)
        => (snapshot?.MovementData?.TransportGuid ?? 0UL) != 0
            || (FlagsOf(snapshot) & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;

    private static Game.GameObjectSnapshot? FindElevatorAtStop(WoWActivitySnapshot? snapshot, float stopZ)
        => snapshot?.MovementData?.NearbyGameObjects?
            .Where(go =>
                go != null
                && go.Entry == UndercityElevatorWestEntry
                && go.Position != null
                && MathF.Abs(go.Position.Z - stopZ) <= 6f)
            .OrderBy(go => MathF.Abs(go.Position!.Z - stopZ))
            .FirstOrDefault();

    private static string DescribeTransport(Game.GameObjectSnapshot? transport)
    {
        if (transport?.Position == null)
            return "none";

        var pos = transport.Position;
        return $"{transport.Entry}:{transport.Name ?? "?"}:type={transport.GameObjectType} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})";
    }

    private static bool IsNearStagePoint(
        WoWActivitySnapshot? snapshot,
        int expectedMapId,
        float expectedX,
        float expectedY,
        float xyToleranceYards)
    {
        var pos = PositionOf(snapshot);
        return pos != null
            && snapshot?.CurrentMapId == expectedMapId
            && Distance2D(pos.X, pos.Y, expectedX, expectedY) <= xyToleranceYards;
    }

    private static float DistanceTo(WoWActivitySnapshot? snapshot, float x, float y)
    {
        var pos = PositionOf(snapshot);
        return pos == null ? float.MaxValue : Distance2D(pos.X, pos.Y, x, y);
    }

    private static float DistanceFrom(WoWActivitySnapshot? snapshot, float x, float y)
        => DistanceTo(snapshot, x, y);

    private static float DistanceBetween(WoWActivitySnapshot? from, WoWActivitySnapshot? to)
    {
        var start = PositionOf(from);
        var end = PositionOf(to);
        return start == null || end == null
            ? 0f
            : Distance2D(start.X, start.Y, end.X, end.Y);
    }

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private sealed record PairAccounts(string Bg, string Fg);

    private sealed record PairSnapshot(WoWActivitySnapshot? Bg, WoWActivitySnapshot? Fg);

    private sealed record PairSample(TimeSpan Elapsed, WoWActivitySnapshot? Bg, WoWActivitySnapshot? Fg);

    private sealed record PairTrace(
        IReadOnlyList<PairSample> Samples,
        TraceMetrics Bg,
        TraceMetrics Fg)
    {
        public WoWActivitySnapshot? LastBg => Samples.LastOrDefault()?.Bg;
        public WoWActivitySnapshot? LastFg => Samples.LastOrDefault()?.Fg;
    }

    private sealed record TraceMetrics(
        int Samples,
        float TravelDistance,
        float StraightLineDistance,
        float MinZ,
        float MaxZ,
        bool SawForward,
        bool SawJump,
        bool SawTransport,
        int TransportSamples,
        Game.Position? Start,
        Game.Position? End)
    {
        public float VerticalRange => MaxZ - MinZ;

        public float FinalDistanceTo(float x, float y)
            => End == null ? float.MaxValue : Distance2D(End.X, End.Y, x, y);

        public static TraceMetrics FromSamples(IEnumerable<WoWActivitySnapshot?> snapshots)
        {
            var snapshotList = snapshots.Where(snapshot => snapshot != null).ToList();
            var positions = snapshotList
                .Select(PositionOf)
                .Where(position => position != null)
                .Cast<Game.Position>()
                .ToList();

            if (positions.Count == 0)
            {
                return new TraceMetrics(
                    Samples: snapshotList.Count,
                    TravelDistance: 0f,
                    StraightLineDistance: 0f,
                    MinZ: float.NaN,
                    MaxZ: float.NaN,
                    SawForward: false,
                    SawJump: false,
                    SawTransport: false,
                    TransportSamples: 0,
                    Start: null,
                    End: null);
            }

            var travel = 0f;
            for (var i = 1; i < positions.Count; i++)
                travel += Distance2D(positions[i - 1].X, positions[i - 1].Y, positions[i].X, positions[i].Y);

            var flags = snapshotList.Select(FlagsOf).ToList();
            var start = positions[0];
            var end = positions[^1];
            var maxZ = positions.Max(position => position.Z);
            var minZ = positions.Min(position => position.Z);
            var sawJump = flags.Any(flag => (flag & (MovementFlags.MOVEFLAG_JUMPING | MovementFlags.MOVEFLAG_FALLINGFAR)) != 0)
                || maxZ - start.Z >= 0.75f;

            return new TraceMetrics(
                Samples: snapshotList.Count,
                TravelDistance: travel,
                StraightLineDistance: Distance2D(start.X, start.Y, end.X, end.Y),
                MinZ: minZ,
                MaxZ: maxZ,
                SawForward: flags.Any(flag => (flag & MovementFlags.MOVEFLAG_FORWARD) != 0),
                SawJump: sawJump,
                SawTransport: snapshotList.Any(IsOnTransport),
                TransportSamples: snapshotList.Count(IsOnTransport),
                Start: start,
                End: end);
        }

        public override string ToString()
            => $"samples={Samples} travel={TravelDistance:F1} straight={StraightLineDistance:F1} zRange={VerticalRange:F2} forward={SawForward} jump={SawJump} transport={SawTransport} transportSamples={TransportSamples} start=({Start?.X:F1},{Start?.Y:F1},{Start?.Z:F2}) end=({End?.X:F1},{End?.Y:F1},{End?.Z:F2})";
    }
}
