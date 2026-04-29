using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Clients;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
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

    private const int PathfindingServicePort = 5001;
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

    private const float UndercityNamedTeleportX = 1584.07f;
    private const float UndercityNamedTeleportY = 241.987f;
    private const float UndercityNamedTeleportZ = -52.1534f;
    private const float UndercityElevatorWestX = 1544.24f;
    private const float UndercityElevatorWestY = 240.77f;
    private const float UndercityElevatorUpperZ = 55.40f;
    private const uint UndercityElevatorWestEntry = 20655;
    private const float UndercityElevatorObjectXYTolerance = 8.0f;
    private const float UndercityElevatorLowerZ = -40.80f;
    private const float UndercityElevatorUpperExitX = 1552.10f;
    private const float UndercityElevatorUpperExitY = 242.20f;
    private const float UndercityElevatorUpperExitZ = 55.10f;
    private const float ElevatorArrivalZTolerance = 8.0f;
    private const float UndercityRouteRunSpeedYardsPerSecond = 7.0f;
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
            await StopResidualMovementAsync(accounts, "running jump");
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
        TestSkip.IfNot(_bot.IsPathfindingReady, "PathfindingService not available on port 5001.");

        var accounts = await EnsureDirectMovementAccountsAsync();
        await StagePairAtNamedUndercityAsync(accounts);
        var lowerRoute = ResolveUndercityLowerPathfindingRoute();
        Assert.True(lowerRoute.Count >= 2, "PathfindingService must return an approach route plus a lower elevator board target.");
        var lowerApproachRoute = lowerRoute.Take(lowerRoute.Count - 1).ToArray();
        var lowerBoardStart = new WorldPoint(
            UndercityElevatorLowerBoardStartX,
            UndercityElevatorLowerBoardStartY,
            UndercityElevatorLowerBoardStartZ);
        var recordingDir = await StartPairRecordingAsync(accounts);

        try
        {
            await WalkPairThroughUndercityLowerRouteAsync(
                accounts,
                lowerApproachRoute,
                "Undercity pathfinding lower approach",
                finalWaypointIsBoardStart: false,
                isBoardingLane: false);

            var elevatorAtLower = await WaitForElevatorAtStopAsync(
                accounts,
                UndercityElevatorWestEntry,
                UndercityElevatorLowerZ,
                "lower boarding",
                TimeSpan.FromSeconds(60),
                zTolerance: 1.5f);
            Assert.True(elevatorAtLower, "Undercity west elevator did not reach the lower stop before the ride probe.");

            await DrivePairToWaypointAsync(
                accounts,
                lowerBoardStart,
                "Undercity lower board start",
                xyToleranceYards: 4.0f,
                zToleranceYards: 1.0f,
                timeout: TimeSpan.FromSeconds(8),
                requireStoppedAtWaypoint: true);

            await DispatchPairAsync(
                accounts,
                MakeSetFacing(0f),
                MakeSetFacing(0f));
            var facingReady = await WaitForPairFacingAsync(accounts, 0f, "Undercity lower board facing", TimeSpan.FromSeconds(6));
            Assert.True(facingReady, "Both bots should face east from the lower Undercity board-start point before boarding.");

            PairTrace? trace = null;
            await DispatchBothAsync(accounts, () => MakeMovement(ActionType.StartMovement, ControlBits.Front));

            try
            {
                var bgReachedUpperExit = false;
                var fgReachedUpperExit = false;
                trace = await TracePairAsync(
                    accounts,
                    TimeSpan.FromSeconds(120),
                    pollIntervalMs: 100,
                    stopWhen: _ => bgReachedUpperExit && fgReachedUpperExit,
                    onSample: async sample =>
                    {
                        if (!bgReachedUpperExit && IsAtUpperElevatorExit(sample.Bg))
                        {
                            bgReachedUpperExit = true;
                            await _bot.SendActionAsync(accounts.Bg, MakeMovement(ActionType.StopMovement, ControlBits.Front));
                        }

                        if (!fgReachedUpperExit && IsAtUpperElevatorExit(sample.Fg))
                        {
                            fgReachedUpperExit = true;
                            await _bot.SendActionAsync(accounts.Fg, MakeMovement(ActionType.StopMovement, ControlBits.Front));
                        }
                    });
            }
            finally
            {
                await DispatchBothAsync(accounts, () => MakeMovement(ActionType.StopMovement, ControlBits.Front));
            }

            var completedTrace = trace ?? throw new InvalidOperationException("Elevator ride trace was not captured.");
            WriteTraceSummary("Undercity named-teleport lower route, elevator ride up, and disembark", completedTrace);
            AssertUndercityElevatorUpRide(completedTrace);
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
        TestSkip.If(string.IsNullOrWhiteSpace(_bot.BgCharacterName), "BG character name required for named Undercity teleport.");
        TestSkip.If(string.IsNullOrWhiteSpace(_bot.FgCharacterName), "FG character name required for named Undercity teleport.");
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

        if (IsMovingHorizontally(snap) && !IsOnTransport(snap))
        {
            await StopResidualMovementAsync(account, role, snap, $"{label} stage");
            await _bot.WaitForSnapshotConditionAsync(
                account,
                snapshot => !IsMovingHorizontally(snapshot) || IsOnTransport(snapshot),
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 500,
                progressLabel: $"{role} {label} stage stop residual movement");
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account);
        }

        var finalSnapshotSettled = IsNearStagePoint(snap, mapId, x, y, xyToleranceYards: 30f)
            && !IsMovingHorizontally(snap)
            && (zStable || !IsJumping(snap));

        _output.WriteLine(
            $"[STAGE] {role} {label}: settled={settled} finalSnapshotSettled={finalSnapshotSettled} zStable={zStable} finalZ={finalZ:F2} {DescribeSnapshot(snap)}");
        Assert.True(
            settled || finalSnapshotSettled || (acceptTransportSettled && IsOnTransport(snap)),
            $"{role} did not settle for {label}. {DescribeSnapshot(snap)}");
        Assert.True(finalZ > -500f, $"{role} staged below world for {label}. finalZ={finalZ:F2}");
    }

    private async Task StagePairAtNamedUndercityAsync(PairAccounts accounts)
    {
        await Task.WhenAll(
            StageAccountAtNamedUndercityAsync(accounts.Bg, _bot.BgCharacterName!, "BG"),
            StageAccountAtNamedUndercityAsync(accounts.Fg, _bot.FgCharacterName!, "FG"));

        await _bot.QuiesceAccountsAsync(
            new[] { accounts.Bg, accounts.Fg },
            "movement parity named Undercity stage",
            TimeSpan.FromSeconds(12));
    }

    private async Task StageAccountAtNamedUndercityAsync(string account, string characterName, string role)
    {
        await _bot.EnsureCleanSlateAsync(account, role, teleportToSafeZone: false);

        _output.WriteLine($"[CMD-SEND] [{role}] .tele name {characterName} undercity");
        await _bot.BotTeleportToNamedAsync(account, characterName, "undercity");

        var settled = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => IsNearPoint3D(
                snapshot,
                EasternKingdomsMapId,
                UndercityNamedTeleportX,
                UndercityNamedTeleportY,
                UndercityNamedTeleportZ,
                xyToleranceYards: 20f,
                zToleranceYards: 15f),
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 500,
            progressLabel: $"{role} named Undercity teleport");

        var gmOff = await _bot.SendGmChatCommandAndAwaitServerAckAsync(account, ".gm off");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        _output.WriteLine($"[STAGE] {role} named Undercity teleport: settled={settled} gmOff={gmOff} {DescribeSnapshot(snap)}");
        Assert.True(settled, $"{role} did not settle at `.tele name {characterName} undercity`. {DescribeSnapshot(snap)}");
        Assert.True(gmOff, $"{role} did not acknowledge `.gm off` after named Undercity teleport. {DescribeSnapshot(snap)}");

        if (IsMovingHorizontally(snap) && !IsOnTransport(snap))
        {
            await StopResidualMovementAsync(account, role, snap, "named Undercity teleport");
            await _bot.WaitForSnapshotConditionAsync(
                account,
                snapshot => !IsMovingHorizontally(snapshot) || IsOnTransport(snapshot),
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 500,
                progressLabel: $"{role} named Undercity teleport stop residual movement");
        }
    }

    private IReadOnlyList<WorldPoint> ResolveUndercityLowerPathfindingRoute()
    {
        using var client = new PathfindingClient(
            "127.0.0.1",
            PathfindingServicePort,
            NullLogger<PathfindingClient>.Instance);

        var start = new Position(
            UndercityNamedTeleportX,
            UndercityNamedTeleportY,
            UndercityNamedTeleportZ);
        var end = new Position(
            UndercityElevatorLowerBoardStartX,
            UndercityElevatorLowerBoardStartY,
            UndercityElevatorLowerBoardStartZ);

        var result = client.GetPathResult((uint)EasternKingdomsMapId, start, end, smoothPath: false);
        _output.WriteLine(
            $"[PATHFINDING] Undercity named teleport -> west lower board: result={result.Result} supported={result.PathSupported} corners={result.Corners.Length}/{result.RawCornerCount} blocked={result.BlockedReason} segment={result.BlockedSegmentIndex?.ToString() ?? "none"} maxAffordance={result.MaxAffordance} zGain={result.TotalZGain:F1} zLoss={result.TotalZLoss:F1} maxSlope={result.MaxSlopeAngleDeg:F1}");

        Assert.True(
            result.Corners.Length > 0,
            $"PathfindingService returned no route from named Undercity teleport to west lower board start. result={result.Result} blocked={result.BlockedReason}");
        var route = ToWorldPoints(result.Corners, end);
        for (var i = 0; i < route.Count; i++)
        {
            var point = route[i];
            _output.WriteLine($"[PATHFINDING]   {i + 1}/{route.Count}: ({point.X:F1},{point.Y:F1},{point.Z:F2})");
        }

        return route;
    }
    private static IReadOnlyList<WorldPoint> ToWorldPoints(IReadOnlyList<Position> corners, Position end)
    {
        var route = new List<WorldPoint>(corners.Count + 1);
        foreach (var corner in corners)
        {
            if (route.Count > 0
                && Distance2D(route[^1].X, route[^1].Y, corner.X, corner.Y) < 0.75f
                && MathF.Abs(route[^1].Z - corner.Z) < 0.25f)
            {
                continue;
            }

            route.Add(new WorldPoint(corner.X, corner.Y, corner.Z));
        }

        if (route.Count == 0
            || Distance2D(route[^1].X, route[^1].Y, end.X, end.Y) > 1.0f
            || MathF.Abs(route[^1].Z - end.Z) > 1.0f)
        {
            route.Add(new WorldPoint(end.X, end.Y, end.Z));
        }

        return route;
    }

    private async Task WalkPairThroughUndercityLowerRouteAsync(
        PairAccounts accounts,
        IReadOnlyList<WorldPoint> waypoints,
        string routeLabel,
        bool finalWaypointIsBoardStart,
        bool isBoardingLane)
    {
        for (var i = 0; i < waypoints.Count; i++)
        {
            var waypoint = waypoints[i];
            var isBoardStart = finalWaypointIsBoardStart && i == waypoints.Count - 1;
            var label = $"{routeLabel} waypoint {i + 1}/{waypoints.Count}";

            await DrivePairToWaypointAsync(
                accounts,
                waypoint,
                label,
                xyToleranceYards: UndercityLowerRouteXYTolerance(waypoint, isBoardStart, isBoardingLane),
                zToleranceYards: UndercityLowerRouteZTolerance(waypoint, isBoardStart, isBoardingLane),
                timeout: TimeSpan.FromSeconds(35),
                requireStoppedAtWaypoint: isBoardStart || i == waypoints.Count - 1);
        }
    }

    private static float UndercityLowerRouteXYTolerance(WorldPoint waypoint, bool isBoardStart, bool isBoardingLane)
        => isBoardStart ? 1.5f : isBoardingLane ? 1.5f : waypoint.Z <= -43.05f ? 1.5f : 3f;

    private static float UndercityLowerRouteZTolerance(WorldPoint waypoint, bool isBoardStart, bool isBoardingLane)
        => isBoardStart ? 2f : isBoardingLane ? 2f : waypoint.X >= 1550f ? 3f : 2f;

    private async Task DrivePairToWaypointAsync(
        PairAccounts accounts,
        WorldPoint point,
        string label,
        float xyToleranceYards,
        float zToleranceYards,
        TimeSpan timeout,
        bool requireStoppedAtWaypoint)
    {
        PairSnapshot snapshots = default!;
        bool[] results = [false, false];

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            results = await Task.WhenAll(
                DriveAccountToWaypointAsync(accounts.Bg, "BG", point, label, xyToleranceYards, zToleranceYards, timeout, stopAfterReach: requireStoppedAtWaypoint),
                DriveAccountToWaypointAsync(accounts.Fg, "FG", point, label, xyToleranceYards, zToleranceYards, timeout, stopAfterReach: requireStoppedAtWaypoint));

            snapshots = await CapturePairSnapshotAsync(accounts);
            var attemptLabel = attempt == 1 ? label : $"{label} retry {attempt}";
            _output.WriteLine($"[ROUTE] {attemptLabel}: BG {DescribeSnapshot(snapshots.Bg)} | FG {DescribeSnapshot(snapshots.Fg)}");

            if (IsOnTransport(snapshots.Bg) || IsOnTransport(snapshots.Fg))
                break;

            if (!requireStoppedAtWaypoint && results[0] && results[1])
                return;

            if (results[0] && results[1]
                && await WaitForPairStoppedAtWaypointAsync(
                    accounts,
                    point,
                    label,
                    xyToleranceYards + 1.5f,
                    zToleranceYards + 1f))
            {
                return;
            }
        }

        snapshots = await CapturePairSnapshotAsync(accounts);
        Assert.True(
            results[0] && results[1],
            $"{label} did not settle for both bots. BG {DescribeSnapshot(snapshots.Bg)} | FG {DescribeSnapshot(snapshots.Fg)}");

        Assert.Fail($"{label} did not stop at the waypoint before the next route leg. BG {DescribeSnapshot(snapshots.Bg)} | FG {DescribeSnapshot(snapshots.Fg)}");
    }

    private async Task<bool> WaitForPairStoppedAtWaypointAsync(
        PairAccounts accounts,
        WorldPoint point,
        string label,
        float xyToleranceYards,
        float zToleranceYards)
    {
        var results = await Task.WhenAll(
            _bot.WaitForSnapshotConditionAsync(
                accounts.Bg,
                snapshot => IsRouteStoppedAtCheckpoint(snapshot, point, xyToleranceYards, zToleranceYards),
                TimeSpan.FromSeconds(6),
                pollIntervalMs: 100,
                progressLabel: $"BG {label} stopped"),
            _bot.WaitForSnapshotConditionAsync(
                accounts.Fg,
                snapshot => IsRouteStoppedAtCheckpoint(snapshot, point, xyToleranceYards, zToleranceYards),
                TimeSpan.FromSeconds(6),
                pollIntervalMs: 100,
                progressLabel: $"FG {label} stopped"));

        var snapshots = await CapturePairSnapshotAsync(accounts);
        if (results[0] && results[1])
            return true;

        _output.WriteLine($"[ROUTE] {label} stop drift: BG {DescribeSnapshot(snapshots.Bg)} | FG {DescribeSnapshot(snapshots.Fg)}");
        return false;
    }

    private async Task<bool> DriveAccountToWaypointAsync(
        string account,
        string role,
        WorldPoint point,
        string label,
        float xyToleranceYards,
        float zToleranceYards,
        TimeSpan timeout,
        bool stopAfterReach)
    {
        await _bot.RefreshSnapshotsAsync();
        var start = await _bot.GetSnapshotAsync(account);
        if (IsRouteStoppedAtCheckpoint(
            start,
            point,
            xyToleranceYards + 1.5f,
            zToleranceYards + 1f))
        {
            return true;
        }

        var facing = FacingTo(start, point);
        var facingResult = await _bot.SendActionAsync(account, MakeSetFacing(facing));
        Assert.Equal(ResponseResult.Success, facingResult);
        var startResult = await _bot.SendActionAsync(account, MakeMovement(ActionType.StartMovement, ControlBits.Front));
        Assert.Equal(ResponseResult.Success, startResult);
        var pulseCutoff = RouteMovementPulseCutoff(start, point, xyToleranceYards);
        var pulseTimer = new Stopwatch();
        var reached = false;

        try
        {
            reached = await _bot.WaitForSnapshotConditionAsync(
                account,
                snapshot =>
                {
                    if (IsRouteCheckpointSettled(snapshot, point, xyToleranceYards, zToleranceYards))
                        return true;

                    if (!pulseTimer.IsRunning && HasRouteMovementStarted(start, snapshot))
                        pulseTimer.Start();

                    return pulseTimer.IsRunning && pulseTimer.Elapsed >= pulseCutoff;
                },
                timeout,
                pollIntervalMs: 25,
                progressLabel: $"{role} {label}");
            return reached;
        }
        finally
        {
            if (stopAfterReach || !reached)
            {
                var stopResult = await _bot.SendActionAsync(account, MakeMovement(ActionType.StopMovement, ControlBits.Front));
                Assert.Equal(ResponseResult.Success, stopResult);
            }
        }
    }

    private async Task<bool> WaitForElevatorAtStopAsync(
        PairAccounts accounts,
        uint elevatorEntry,
        float stopZ,
        string stopLabel,
        TimeSpan timeout,
        float zTolerance = 6f)
    {
        Game.GameObjectSnapshot? bgElevator = null;
        Game.GameObjectSnapshot? fgElevator = null;

        var reached = await Task.WhenAll(
            _bot.WaitForSnapshotConditionAsync(
                accounts.Bg,
                snapshot =>
                {
                    bgElevator = FindElevatorAtStop(snapshot, elevatorEntry, stopZ, zTolerance);
                    return bgElevator != null;
                },
                timeout,
                pollIntervalMs: 500,
                progressLabel: $"west Undercity elevator {elevatorEntry} {stopLabel} stop BG"),
            _bot.WaitForSnapshotConditionAsync(
                accounts.Fg,
                snapshot =>
                {
                    fgElevator = FindElevatorAtStop(snapshot, elevatorEntry, stopZ, zTolerance);
                    return fgElevator != null;
                },
                timeout,
                pollIntervalMs: 500,
                progressLabel: $"west Undercity elevator {elevatorEntry} {stopLabel} stop FG"));

        if (reached[0] && reached[1])
            _output.WriteLine(
                $"[ELEVATOR] West Undercity elevator {elevatorEntry} reached {stopLabel} stop: BG sees {DescribeTransport(bgElevator)} | FG sees {DescribeTransport(fgElevator)}");

        return reached[0] && reached[1];
    }

    private async Task<bool> WaitForPairFacingAsync(
        PairAccounts accounts,
        float expectedFacing,
        string label,
        TimeSpan timeout)
        => await WaitForPairFacingAsync(accounts, expectedFacing, expectedFacing, label, timeout);

    private async Task<bool> WaitForPairFacingAsync(
        PairAccounts accounts,
        float bgExpectedFacing,
        float fgExpectedFacing,
        string label,
        TimeSpan timeout)
    {
        var results = await Task.WhenAll(
            _bot.WaitForSnapshotConditionAsync(
                accounts.Bg,
                snapshot => IsFacing(snapshot, bgExpectedFacing, toleranceRadians: 0.15f),
                timeout,
                pollIntervalMs: 100,
                progressLabel: $"BG {label}"),
            _bot.WaitForSnapshotConditionAsync(
                accounts.Fg,
                snapshot => IsFacing(snapshot, fgExpectedFacing, toleranceRadians: 0.15f),
                timeout,
                pollIntervalMs: 100,
                progressLabel: $"FG {label}"));

        var snapshots = await CapturePairSnapshotAsync(accounts);
        _output.WriteLine($"[ROUTE] {label}: BG {DescribeSnapshot(snapshots.Bg)} | FG {DescribeSnapshot(snapshots.Fg)}");
        return results[0] && results[1];
    }

    private async Task<bool> WaitForPairOnTransportAsync(
        PairAccounts accounts,
        string label,
        TimeSpan timeout,
        uint? transportEntry = null)
    {
        var results = await Task.WhenAll(
            _bot.WaitForSnapshotConditionAsync(
                accounts.Bg,
                snapshot => transportEntry.HasValue
                    ? IsOnTransportEntry(snapshot, transportEntry.Value)
                    : IsOnTransport(snapshot),
                timeout,
                pollIntervalMs: 250,
                progressLabel: $"BG {label}"),
            _bot.WaitForSnapshotConditionAsync(
                accounts.Fg,
                snapshot => transportEntry.HasValue
                    ? IsOnTransportEntry(snapshot, transportEntry.Value)
                    : IsOnTransport(snapshot),
                timeout,
                pollIntervalMs: 250,
                progressLabel: $"FG {label}"));

        var snapshots = await CapturePairSnapshotAsync(accounts);
        _output.WriteLine($"[ELEVATOR] {label}: BG {DescribeSnapshot(snapshots.Bg)} | FG {DescribeSnapshot(snapshots.Fg)}");
        return results[0] && results[1];
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

    private async Task StopResidualMovementAsync(PairAccounts accounts, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snapshots = await CapturePairSnapshotAsync(accounts);

        await Task.WhenAll(
            StopResidualMovementAsync(accounts.Bg, "BG", snapshots.Bg, label),
            StopResidualMovementAsync(accounts.Fg, "FG", snapshots.Fg, label));

        await Task.WhenAll(
            _bot.WaitForSnapshotConditionAsync(
                accounts.Bg,
                snapshot => !IsMovingHorizontally(snapshot) || IsOnTransport(snapshot),
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 500,
                progressLabel: $"{label} BG stop residual movement"),
            _bot.WaitForSnapshotConditionAsync(
                accounts.Fg,
                snapshot => !IsMovingHorizontally(snapshot) || IsOnTransport(snapshot),
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 500,
                progressLabel: $"{label} FG stop residual movement"));
    }

    private async Task StopResidualMovementAsync(string account, string role, WoWActivitySnapshot? snapshot, string label)
    {
        if (!IsMovingHorizontally(snapshot) || IsOnTransport(snapshot))
            return;

        var pos = PositionOf(snapshot);
        if (pos == null)
            return;

        var result = await _bot.SendActionAsync(
            account,
            MakeGoto(pos.X, pos.Y, pos.Z, stopDistance: 50f));
        _output.WriteLine($"[CLEANUP] {role} {label}: stop residual movement via arrived Goto => {result}");
    }

    private async Task DispatchBothAsync(PairAccounts accounts, Func<ActionMessage> actionFactory)
    {
        var results = await Task.WhenAll(
            _bot.SendActionAsync(accounts.Bg, actionFactory()),
            _bot.SendActionAsync(accounts.Fg, actionFactory()));
        Assert.All(results, result => Assert.Equal(ResponseResult.Success, result));
    }

    private async Task DispatchPairAsync(PairAccounts accounts, ActionMessage bgAction, ActionMessage fgAction)
    {
        var results = await Task.WhenAll(
            _bot.SendActionAsync(accounts.Bg, bgAction),
            _bot.SendActionAsync(accounts.Fg, fgAction));
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
        Func<PairSample, bool>? stopWhen = null,
        Func<PairSample, Task>? onSample = null)
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

            if (onSample != null)
                await onSample(sample);

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

    private static void AssertUndercityElevatorUpRide(PairTrace trace)
    {
        AssertElevatorUpRide("BG", trace.Bg);
        AssertElevatorUpRide("FG", trace.Fg);
        Assert.True(IsAtUpperElevatorExit(trace.LastBg), $"BG did not disembark at the upper Undercity elevator exit. {DescribeSnapshot(trace.LastBg)}");
        Assert.True(IsAtUpperElevatorExit(trace.LastFg), $"FG did not disembark at the upper Undercity elevator exit. {DescribeSnapshot(trace.LastFg)}");
    }

    private static void AssertElevatorUpRide(string role, TraceMetrics metrics)
    {
        Assert.True(metrics.TransportSamples >= 2, DescribeFailure(role, "Undercity elevator boarding", metrics));
        Assert.True(metrics.MinZ <= UndercityElevatorLowerZ + ElevatorArrivalZTolerance, DescribeFailure(role, "lower-stop ride start", metrics));
        Assert.True(metrics.MaxZ >= UndercityElevatorUpperExitZ - ElevatorArrivalZTolerance, DescribeFailure(role, "upper-stop ride arrival", metrics));
        Assert.True(metrics.VerticalRange >= 80f, DescribeFailure(role, "full lower-to-upper elevator ride", metrics));
        Assert.True(metrics.FinalDistanceTo(UndercityElevatorUpperExitX, UndercityElevatorUpperExitY) <= 10f,
            DescribeFailure(role, "upper elevator disembark XY", metrics));
    }

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
        var transportEntry = TransportEntryOf(transportGuid);
        var transportEntryText = transportEntry == 0 ? "none" : transportEntry.ToString();
        var facingText = snapshot.MovementData == null
            ? "?"
            : NormalizeFacing(snapshot.MovementData.Facing).ToString("F3");
        return $"pos=({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F2}) facing={facingText} map={snapshot.CurrentMapId} screen={snapshot.ScreenState} conn={snapshot.ConnectionState} transition={snapshot.IsMapTransition} flags=0x{(uint)flags:X} transport=0x{transportGuid:X} entry={transportEntryText} current={snapshot.CurrentAction?.ActionType.ToString() ?? "null"} previous={snapshot.PreviousAction?.ActionType.ToString() ?? "null"}";
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

    private static ActionMessage MakeSetFacing(float facing)
        => new()
        {
            ActionType = ActionType.SetFacing,
            Parameters =
            {
                new RequestParameter { FloatParam = NormalizeFacing(facing) }
            }
        };

    private static ActionMessage MakeMovement(ActionType actionType, ControlBits bits)
        => new()
        {
            ActionType = actionType,
            Parameters =
            {
                new RequestParameter { IntParam = (int)bits }
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

    private static bool IsMovingHorizontally(WoWActivitySnapshot? snapshot)
        => (FlagsOf(snapshot) & MovementFlags.MOVEFLAG_MASK_XZ) != 0;

    private static bool IsOnTransport(WoWActivitySnapshot? snapshot)
        => (snapshot?.MovementData?.TransportGuid ?? 0UL) != 0
            || (FlagsOf(snapshot) & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;

    private static bool IsOnTransportEntry(WoWActivitySnapshot? snapshot, uint expectedEntry)
    {
        var transportGuid = snapshot?.MovementData?.TransportGuid ?? 0UL;
        return transportGuid != 0
            && TransportEntryOf(transportGuid) == expectedEntry;
    }

    private static uint TransportEntryOf(ulong transportGuid)
        => transportGuid == 0 ? 0 : (uint)((transportGuid >> 24) & 0xFFFFFF);

    private static bool IsRouteCheckpointSettled(
        WoWActivitySnapshot? snapshot,
        WorldPoint point,
        float xyToleranceYards,
        float zToleranceYards)
        => IsNearPoint3D(snapshot, EasternKingdomsMapId, point.X, point.Y, point.Z, xyToleranceYards, zToleranceYards)
            && !IsJumping(snapshot)
            && !IsOnTransport(snapshot);

    private static bool IsRouteStoppedAtCheckpoint(
        WoWActivitySnapshot? snapshot,
        WorldPoint point,
        float xyToleranceYards,
        float zToleranceYards)
        => IsRouteStopped(snapshot)
            && IsNearPoint3D(snapshot, EasternKingdomsMapId, point.X, point.Y, point.Z, xyToleranceYards, zToleranceYards);

    private static bool IsRouteStopped(WoWActivitySnapshot? snapshot)
        => !IsMovingHorizontally(snapshot) && !IsJumping(snapshot) && !IsOnTransport(snapshot);

    private static TimeSpan RouteMovementPulseCutoff(
        WoWActivitySnapshot? start,
        WorldPoint point,
        float xyToleranceYards)
    {
        var startPosition = PositionOf(start);
        if (startPosition == null)
            return TimeSpan.FromSeconds(1);

        var distance = Distance2D(startPosition.X, startPosition.Y, point.X, point.Y);
        var stopLead = MathF.Min(3f, MathF.Max(0.5f, xyToleranceYards * 0.5f));
        var stopDistance = MathF.Max(0.5f, distance - stopLead);
        return TimeSpan.FromSeconds(stopDistance / UndercityRouteRunSpeedYardsPerSecond);
    }

    private static bool HasRouteMovementStarted(WoWActivitySnapshot? start, WoWActivitySnapshot? snapshot)
    {
        if (IsMovingHorizontally(snapshot))
            return true;

        var startPosition = PositionOf(start);
        var currentPosition = PositionOf(snapshot);
        return startPosition != null
            && currentPosition != null
            && Distance2D(startPosition.X, startPosition.Y, currentPosition.X, currentPosition.Y) >= 0.5f;
    }

    private static bool IsFacing(WoWActivitySnapshot? snapshot, float expectedFacing, float toleranceRadians)
        => snapshot?.MovementData != null
            && FacingDelta(snapshot.MovementData.Facing, expectedFacing) <= toleranceRadians;

    private static float FacingDelta(float a, float b)
    {
        var delta = MathF.Abs(NormalizeFacing(a) - NormalizeFacing(b));
        var twoPi = MathF.PI * 2f;
        return MathF.Min(delta, twoPi - delta);
    }

    private static float FacingTo(WoWActivitySnapshot? snapshot, WorldPoint point)
    {
        var pos = PositionOf(snapshot);
        if (pos == null)
            return 0f;

        return NormalizeFacing(MathF.Atan2(point.Y - pos.Y, point.X - pos.X));
    }

    private static float NormalizeFacing(float facing)
    {
        var twoPi = MathF.PI * 2f;
        var normalized = facing % twoPi;
        return normalized < 0f ? normalized + twoPi : normalized;
    }

    private static bool IsAtUpperElevatorExit(WoWActivitySnapshot? snapshot)
        => IsNearPoint3D(
                snapshot,
                EasternKingdomsMapId,
                UndercityElevatorUpperExitX,
                UndercityElevatorUpperExitY,
                UndercityElevatorUpperExitZ,
                xyToleranceYards: 10f,
                zToleranceYards: ElevatorArrivalZTolerance)
            && !IsOnTransport(snapshot);

    private static Game.GameObjectSnapshot? FindElevatorAtStop(
        WoWActivitySnapshot? snapshot,
        uint elevatorEntry,
        float stopZ,
        float zTolerance)
        => snapshot?.MovementData?.NearbyGameObjects?
            .Where(go =>
                go != null
                && go.Entry == elevatorEntry
                && go.Position != null
                && Distance2D(go.Position.X, go.Position.Y, UndercityElevatorWestX, UndercityElevatorWestY) <= UndercityElevatorObjectXYTolerance
                && MathF.Abs(go.Position.Z - stopZ) <= zTolerance)
            .OrderBy(go => MathF.Abs(go.Position!.Z - stopZ))
            .FirstOrDefault();

    private static string DescribeTransport(Game.GameObjectSnapshot? transport)
    {
        if (transport?.Position == null)
            return "none";

        var pos = transport.Position;
        return $"{transport.Entry}:{transport.Name ?? "?"}:display={transport.DisplayId}:type={transport.GameObjectType} state={transport.GoState} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})";
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

    private static bool IsNearPoint3D(
        WoWActivitySnapshot? snapshot,
        int expectedMapId,
        float expectedX,
        float expectedY,
        float expectedZ,
        float xyToleranceYards,
        float zToleranceYards)
    {
        var pos = PositionOf(snapshot);
        return pos != null
            && snapshot?.CurrentMapId == expectedMapId
            && Distance2D(pos.X, pos.Y, expectedX, expectedY) <= xyToleranceYards
            && MathF.Abs(pos.Z - expectedZ) <= zToleranceYards;
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

    private sealed record WorldPoint(float X, float Y, float Z);

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
