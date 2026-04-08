using BotRunner.Constants;
using BotRunner.Helpers;
using BotRunner.Movement;
using BotRunner.Interfaces;
using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: run to corpse using pathfinding and reclaim when eligible.
/// </summary>
public class RetrieveCorpseTask(IBotContext botContext, Position corpsePosition) : BotTask(botContext), IBotTask
{
    // Corpse runback follows the navmesh path faithfully.
    // Disable probe heuristics/pruning so corners are not skipped into walls.
    // strictPathValidation is OFF because long outdoor corpse runs (460y+) have
    // segments where collision-based LOS rejects valid navmesh paths.
    private readonly NavigationPath _navPath = NavigationPathFactory.CreateForCorpseRun(
        botContext.Container.PathfindingClient, botContext.ObjectManager.Player, botContext.ObjectManager);
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastReclaimAttempt = DateTime.MinValue;
    private DateTime _lastCooldownLog = DateTime.MinValue;
    private DateTime? _noPathSinceUtc;
    private DateTime _lastNoPathRecoveryKickUtc = DateTime.MinValue;
    private bool _loggedPathfindingMode;
    private DateTime? _nonGhostSinceUtc;
    private Position? _lastRunbackPosition;
    private DateTime _lastRunbackSampleUtc = DateTime.MinValue;
    private DateTime _lastRunbackRecoveryUtc = DateTime.MinValue;
    private DateTime _lastWaypointDriveLogUtc = DateTime.MinValue;
    private int _runbackNoDisplacementTicks;
    private int _runbackStaleForwardTicks;
    private int _runbackRecoveryCount;
    private float _bestRunbackCorpseDistance2D = float.MaxValue;
    private DateTime _lastRunbackProgressUtc = DateTime.MinValue;
    private Position? _trackedRunbackWaypoint;
    private float _bestRunbackWaypointDistance = float.MaxValue;
    private DateTime _lastRunbackWaypointProgressUtc = DateTime.MinValue;
    private Position? _lastDrivenWaypoint;
    private DateTime _runbackRecoveryHoldUntilUtc = DateTime.MinValue;
    private DateTime _lastTickDiagUtc = DateTime.MinValue;
    private bool _stoppedForRetrieval;
    private bool _triedSpiritHealer;

    // MaNGOS CORPSE_RECLAIM_RADIUS = 39y (3D distance). We compute a dynamic 2D
    // approach distance from the current Z delta so the bot walks close enough in
    // 3D even in multi-level areas like Orgrimmar where the graveyard is 20+ yards
    // above/below the corpse.
    private const float ServerReclaimRadius3D = 39f;
    private const float ReclaimSafetyMargin = 5f; // stay 5y inside the 39y sphere
    private const float MinRetrieveRange2D = 5f;   // never stop further than this minimum
    private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(BotTaskTimeouts.CorpseRetrievalMinutes);
    private static readonly TimeSpan NoPathTimeout = TimeSpan.FromSeconds(BotTaskTimeouts.NoPathTimeoutSec);
    private static readonly TimeSpan ReclaimRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CooldownLogInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RunbackSampleInterval = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan RunbackRecoveryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RunbackRecoveryHold = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan RunbackProgressTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RunbackWaypointProgressTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan NoPathRecoveryInterval = TimeSpan.FromSeconds(4);
    private const float MaxCorpseZDeltaForNavigation = 120f;
    private const float MovementStepThreshold = 0.2f;
    private const float RunbackProgressImprovementThreshold = 2f;
    private const float RunbackWaypointProgressImprovementThreshold = 0.75f;
    private const float RunbackWaypointIdentityRadius = 2f;
    private const float RunbackWaypointIdentityZTolerance = 4f;
    private const float NearWaypointThreshold = 1.5f;
    private const float MinimumDriveWaypointDistance = 3f;
    private const int RunbackNoDisplacementThreshold = 8;
    private const int RunbackNoIntentDisplacementThreshold = 12;
    private const int RunbackStaleForwardThreshold = 6;
    private const int MaxRunbackRecoveryAttempts = 6;
    private const int TraceSummaryWaypointLimit = 4;
    private const int TraceSummarySampleLimit = 3;

    private static bool HasGhostFlag(IWoWLocalPlayer player) => DeathStateDetection.HasGhostFlag(player);
    private static bool IsStandStateDeadFlag(IWoWLocalPlayer player) => DeathStateDetection.IsStandStateDead(player);
    private static bool IsStrictAlive(IWoWLocalPlayer player) => DeathStateDetection.IsStrictAlive(player);
    private static bool IsGhostState(IWoWLocalPlayer player) => DeathStateDetection.IsGhost(player);
    private static bool IsDeadOrGhostState(IWoWLocalPlayer player) => DeathStateDetection.IsDeadOrGhostBroad(player);

    private static bool HasHorizontalMovementIntent(IWoWLocalPlayer player)
    {
        try
        {
            var flags = player.MovementFlags;
            return flags.HasFlag(MovementFlags.MOVEFLAG_FORWARD)
                || flags.HasFlag(MovementFlags.MOVEFLAG_BACKWARD)
                || flags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT)
                || flags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT);
        }
        catch
        {
            return false;
        }
    }

    internal static string FormatNavigationTraceSummary(NavigationTraceSnapshot trace)
    {
        return $"plan={trace.PlanVersion} reason={trace.LastReplanReason ?? "none"} resolution={trace.LastResolution ?? "none"} " +
               $"idx={trace.CurrentWaypointIndex} active={FormatTracePosition(trace.ActiveWaypoint)} short={trace.IsShortRoute} " +
               $"smooth={trace.SmoothPath} overlay={trace.NearbyObjectCount} request={FormatTracePosition(trace.RequestedStart)}->{FormatTracePosition(trace.RequestedDestination)} " +
               $"service={FormatTracePath(trace.ServiceWaypoints, TraceSummaryWaypointLimit)} " +
               $"planned={FormatTracePath(trace.PlannedWaypoints, TraceSummaryWaypointLimit)} " +
               $"samples={FormatTraceSamples(trace.ExecutionSamples, TraceSummarySampleLimit)}";
    }

    private string GetNavigationTraceSummary()
        => FormatNavigationTraceSummary(_navPath.TraceSnapshot);

    private static string FormatTracePath(Position[] path, int limit)
    {
        if (path.Length == 0)
            return "[]";

        var displayCount = Math.Min(path.Length, limit);
        var parts = new string[displayCount + (path.Length > limit ? 1 : 0)];
        for (var i = 0; i < displayCount; i++)
            parts[i] = FormatTracePosition(path[i]);

        if (path.Length > limit)
            parts[^1] = $"+{path.Length - limit} more";

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string FormatTraceSamples(NavigationExecutionSample[] samples, int limit)
    {
        if (samples.Length == 0)
            return "[]";

        var startIndex = Math.Max(0, samples.Length - limit);
        var displayCount = samples.Length - startIndex;
        var parts = new string[displayCount + (startIndex > 0 ? 1 : 0)];
        var offset = 0;
        if (startIndex > 0)
        {
            parts[0] = $"+{startIndex} earlier";
            offset = 1;
        }

        for (var i = 0; i < displayCount; i++)
        {
            var sample = samples[startIndex + i];
            parts[offset + i] = $"p{sample.PlanVersion}:{sample.Resolution}:idx{sample.WaypointIndex}:{FormatTracePosition(sample.CurrentPosition)}->{FormatTracePosition(sample.ReturnedWaypoint)}";
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string FormatTracePosition(Position? position)
        => position == null
            ? "null"
            : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";

    private static Position CopyPosition(Position source) => new(source.X, source.Y, source.Z);

    private static Position BuildCorpseNavigationTarget(Position currentPosition, Position corpsePosition)
    {
        var corpseDeltaZ = MathF.Abs(currentPosition.Z - corpsePosition.Z);
        if (corpseDeltaZ <= MaxCorpseZDeltaForNavigation)
            return corpsePosition;

        // Some server builds can report stale corpse Z during ghost transitions.
        // Keep corpse X/Y authoritative while clamping target Z to current ghost Z
        // so pathfinding stays horizontal and reclaim gating can proceed by range.
        return new Position(corpsePosition.X, corpsePosition.Y, currentPosition.Z);
    }

    protected override NavigationTraceSnapshot? GetDiagnosticNavigationTraceSnapshot()
        => _navPath.TraceSnapshot;

    private void TrackDrivenWaypoint(Position waypoint)
        => _lastDrivenWaypoint = CopyPosition(waypoint);

    private void ResetRunbackStallTracking(Position position)
    {
        _lastRunbackPosition = CopyPosition(position);
        _lastRunbackSampleUtc = DateTime.UtcNow;
        _runbackNoDisplacementTicks = 0;
        _runbackStaleForwardTicks = 0;
    }

    private void ResetRunbackProgressTracking(float corpseHorizontalDistance)
    {
        _bestRunbackCorpseDistance2D = corpseHorizontalDistance;
        _lastRunbackProgressUtc = DateTime.UtcNow;
    }

    private void TrackRunbackProgress(float corpseHorizontalDistance, DateTime now)
    {
        if (_lastRunbackProgressUtc == DateTime.MinValue || _bestRunbackCorpseDistance2D == float.MaxValue)
        {
            _bestRunbackCorpseDistance2D = corpseHorizontalDistance;
            _lastRunbackProgressUtc = now;
            return;
        }

        if (corpseHorizontalDistance + RunbackProgressImprovementThreshold < _bestRunbackCorpseDistance2D)
        {
            _bestRunbackCorpseDistance2D = corpseHorizontalDistance;
            _lastRunbackProgressUtc = now;
        }
    }

    private void ResetWaypointProgressTracking()
    {
        _trackedRunbackWaypoint = null;
        _bestRunbackWaypointDistance = float.MaxValue;
        _lastRunbackWaypointProgressUtc = DateTime.MinValue;
    }

    private static bool IsSameRunbackWaypoint(Position a, Position b)
        => a.DistanceTo2D(b) <= RunbackWaypointIdentityRadius
            && MathF.Abs(a.Z - b.Z) <= RunbackWaypointIdentityZTolerance;

    private void TrackRunbackWaypointProgress(Position currentPosition, Position activeWaypoint, DateTime now)
    {
        var waypointDistance = currentPosition.DistanceTo(activeWaypoint);
        if (_trackedRunbackWaypoint == null || !IsSameRunbackWaypoint(_trackedRunbackWaypoint, activeWaypoint))
        {
            _trackedRunbackWaypoint = CopyPosition(activeWaypoint);
            _bestRunbackWaypointDistance = waypointDistance;
            _lastRunbackWaypointProgressUtc = now;
            return;
        }

        if (_lastRunbackWaypointProgressUtc == DateTime.MinValue || _bestRunbackWaypointDistance == float.MaxValue)
        {
            _bestRunbackWaypointDistance = waypointDistance;
            _lastRunbackWaypointProgressUtc = now;
            return;
        }

        if (waypointDistance + RunbackWaypointProgressImprovementThreshold < _bestRunbackWaypointDistance)
        {
            _bestRunbackWaypointDistance = waypointDistance;
            _lastRunbackWaypointProgressUtc = now;
        }
    }

    private bool ShouldRecoverRunbackStall(IWoWLocalPlayer player, float corpseHorizontalDistance, Position? activeWaypoint, out string reason)
    {
        reason = string.Empty;
        var now = DateTime.UtcNow;

        TrackRunbackProgress(corpseHorizontalDistance, now);
        if (activeWaypoint != null)
            TrackRunbackWaypointProgress(player.Position, activeWaypoint, now);
        else
            ResetWaypointProgressTracking();

        if (_lastRunbackPosition == null || _lastRunbackSampleUtc == DateTime.MinValue)
        {
            ResetRunbackStallTracking(player.Position);
            ResetRunbackProgressTracking(corpseHorizontalDistance);
            return false;
        }

        if (now - _lastRunbackSampleUtc < RunbackSampleInterval)
            return false;

        var stepDistance = player.Position.DistanceTo(_lastRunbackPosition);
        var hasHorizontalIntent = HasHorizontalMovementIntent(player);
        var hasRunbackCommandIntent = hasHorizontalIntent || _lastDrivenWaypoint != null;
        var noDisplacement = stepDistance < MovementStepThreshold;

        if (noDisplacement)
            _runbackNoDisplacementTicks++;
        else
            _runbackNoDisplacementTicks = 0;

        if (hasHorizontalIntent && noDisplacement)
            _runbackStaleForwardTicks++;
        else
            _runbackStaleForwardTicks = 0;

        // Only reset recovery count when making meaningful progress TOWARD the corpse,
        // not just any displacement. The jump+backward recovery maneuver moves the bot
        // ≥1y but doesn't bring it closer to the corpse — resetting on any movement
        // prevents the recovery counter from ever reaching MaxRunbackRecoveryAttempts.
        if (stepDistance >= 1f && corpseHorizontalDistance < _bestRunbackCorpseDistance2D - 5f)
        {
            _runbackRecoveryCount = 0;
        }

        _lastRunbackPosition = CopyPosition(player.Position);
        _lastRunbackSampleUtc = now;

        if (_runbackStaleForwardTicks >= RunbackStaleForwardThreshold)
        {
            reason = $"stale movement intent persisted for {_runbackStaleForwardTicks} samples with no displacement";
            return true;
        }

        if (hasRunbackCommandIntent && _runbackNoDisplacementTicks >= RunbackNoDisplacementThreshold)
        {
            reason = hasHorizontalIntent
                ? $"runback stalled for {_runbackNoDisplacementTicks} samples despite movement intent"
                : $"runback stalled for {_runbackNoDisplacementTicks} samples while driving waypoints with no horizontal movement flags";
            return true;
        }

        if (!hasRunbackCommandIntent && _runbackNoDisplacementTicks >= RunbackNoIntentDisplacementThreshold)
        {
            reason = $"runback stalled for {_runbackNoDisplacementTicks} samples with no movement intent";
            return true;
        }

        if (activeWaypoint != null
            && hasRunbackCommandIntent
            && _lastRunbackWaypointProgressUtc != DateTime.MinValue
            && now - _lastRunbackWaypointProgressUtc >= RunbackWaypointProgressTimeout
            && player.Position.DistanceTo(activeWaypoint) > NearWaypointThreshold + 1f)
        {
            var stalledSeconds = (int)(now - _lastRunbackWaypointProgressUtc).TotalSeconds;
            var currentWaypointDistance = player.Position.DistanceTo(activeWaypoint);
            reason = $"waypoint distance did not improve for {stalledSeconds}s (best={_bestRunbackWaypointDistance:F1}, current={currentWaypointDistance:F1})";
            return true;
        }

        if (hasRunbackCommandIntent
            && corpseHorizontalDistance > ServerReclaimRadius3D + 2f
            && _lastRunbackProgressUtc != DateTime.MinValue
            && now - _lastRunbackProgressUtc >= RunbackProgressTimeout)
        {
            var stalledSeconds = (int)(now - _lastRunbackProgressUtc).TotalSeconds;
            reason = $"corpse distance did not improve for {stalledSeconds}s (best2D={_bestRunbackCorpseDistance2D:F1}, current2D={corpseHorizontalDistance:F1})";
            return true;
        }

        return false;
    }

    private bool RecoverRunbackStall(IWoWLocalPlayer player, float corpseHorizontalDistance, string reason)
    {
        var now = DateTime.UtcNow;
        if (now - _lastRunbackRecoveryUtc < RunbackRecoveryInterval)
            return false;

        _lastRunbackRecoveryUtc = now;
        _runbackRecoveryCount++;

        ObjectManager.ForceStopImmediate();
        _navPath.Clear();
        _noPathSinceUtc = null;
        _lastNoPathRecoveryKickUtc = now;
        ResetRunbackStallTracking(player.Position);
        ResetRunbackProgressTracking(corpseHorizontalDistance);
        ResetWaypointProgressTracking();
        _lastDrivenWaypoint = null;
        _runbackRecoveryHoldUntilUtc = now + RunbackRecoveryHold;

        // Escalating recovery strategies to break collision deadlocks:
        // 1st: just clear path and replan (default above)
        // 2nd: jump + backward step to break server-side collision
        // 3rd+: alternate strafe direction + jump to escape lateral terrain snags
        if (_runbackRecoveryCount == 2)
        {
            ObjectManager.Turn180();
            ObjectManager.StartMovement(ControlBits.Front | ControlBits.Jump);
            _runbackRecoveryHoldUntilUtc = now + TimeSpan.FromMilliseconds(2500);
            Log.Information("[RETRIEVE_CORPSE] Stall recovery #{Attempt}: jump+backward to break collision deadlock", _runbackRecoveryCount);
        }
        else if (_runbackRecoveryCount >= 3)
        {
            // Alternate strafe left/right on odd/even attempts to escape lateral terrain snags.
            var strafeLeft = _runbackRecoveryCount % 2 != 0;
            var strafeBit = strafeLeft ? ControlBits.StrafeLeft : ControlBits.StrafeRight;
            ObjectManager.StartMovement(strafeBit | ControlBits.Jump);
            _runbackRecoveryHoldUntilUtc = now + TimeSpan.FromMilliseconds(2000);
            Log.Information("[RETRIEVE_CORPSE] Stall recovery #{Attempt}: {Direction}+jump to escape terrain snag",
                _runbackRecoveryCount, strafeLeft ? "strafe-left" : "strafe-right");
        }

        Log.Warning("[RETRIEVE_CORPSE] Runback stall recovery #{Attempt}: {Reason} (distance2D={Distance2D:F1}) trace={Trace}. Cleared movement and rebuilding path.",
            _runbackRecoveryCount, reason, corpseHorizontalDistance, GetNavigationTraceSummary());
        BotRunnerService.DiagLog(
            $"[RETRIEVE_CORPSE] stall_recovery attempt={_runbackRecoveryCount} reason={reason} distance2D={corpseHorizontalDistance:F1} trace={GetNavigationTraceSummary()}");

        if (_runbackRecoveryCount > MaxRunbackRecoveryAttempts)
        {
            Log.Warning("[RETRIEVE_CORPSE] Runback remained stalled after {Attempts} recoveries; aborting task.",
                _runbackRecoveryCount);
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] stall_recovery_exceeded attempts={_runbackRecoveryCount} trace={GetNavigationTraceSummary()}");
            PopTask("RunbackStallRecoveryExceeded");
            return true;
        }

        return false;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            BotRunnerService.DiagLog($"[RETRIEVE_CORPSE] Update: PlayerUnavailable player={player != null} pos={player?.Position != null}");
            PopTask("PlayerUnavailable");
            return;
        }

        if (DateTime.UtcNow - _startTime > TaskTimeout)
        {
            Log.Warning("[RETRIEVE_CORPSE] Timed out navigating to corpse");
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] timeout corpse=({corpsePosition.X:F1},{corpsePosition.Y:F1},{corpsePosition.Z:F1}) trace={GetNavigationTraceSummary()}");
            ObjectManager.StopAllMovement();
            PopTask("Timeout");
            return;
        }

        // P21.25: Spirit healer auto-navigation — if corpse is very far away (>200y)
        // and a spirit healer NPC is nearby (<50y), use spirit healer resurrection
        // instead of running back (accepts 25% durability loss + rez sickness).
        var corpseHorizontalDistance = player.Position.DistanceTo2D(corpsePosition);
        if (corpseHorizontalDistance > 200f && !_triedSpiritHealer)
        {
            var spiritHealer = ObjectManager.Units
                .FirstOrDefault(u => u.Name != null
                    && u.Name.Contains("Spirit Healer", StringComparison.OrdinalIgnoreCase)
                    && u.Position.DistanceTo(player.Position) < 50f);

            if (spiritHealer != null)
            {
                Log.Information("[RETRIEVE_CORPSE] Corpse is {Dist:F0}y away. Using nearby spirit healer instead.",
                    corpseHorizontalDistance);
                spiritHealer.Interact(); // CMSG_SPIRIT_HEALER_ACTIVATE
                _triedSpiritHealer = true;
                // Wait a tick for resurrection to process
                return;
            }
        }
        var corpseDeltaZ = MathF.Abs(player.Position.Z - corpsePosition.Z);
        var corpseNavTarget = BuildCorpseNavigationTarget(player.Position, corpsePosition);

        if (DateTime.UtcNow - _lastTickDiagUtc >= TimeSpan.FromSeconds(3))
        {
            var isGhostNow = IsGhostState(player);
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] tick pos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1}) corpse=({corpsePosition.X:F1},{corpsePosition.Y:F1},{corpsePosition.Z:F1}) dist2D={corpseHorizontalDistance:F1} zDelta={corpseDeltaZ:F1} ghost={isGhostNow} hp={player.Health}/{player.MaxHealth} flags=0x{(uint)player.PlayerFlags:X} noPath={_noPathSinceUtc != null} elapsed={(DateTime.UtcNow - _startTime).TotalSeconds:F0}s");
            _lastTickDiagUtc = DateTime.UtcNow;
        }

        // Dynamic 2D approach range: solve for max 2D dist such that 3D dist < (39 - 5) = 34y.
        // In flat terrain (zDelta≈0) this yields ~34y. In Orgrimmar (zDelta≈22y) → ~25y.
        // If zDelta alone exceeds the safe radius, walk as close as physically possible.
        var effectiveRadius = ServerReclaimRadius3D - ReclaimSafetyMargin;
        var maxDist2DSquared = effectiveRadius * effectiveRadius - corpseDeltaZ * corpseDeltaZ;
        var retrieveRange = maxDist2DSquared > 0
            ? MathF.Max(MathF.Sqrt(maxDist2DSquared), MinRetrieveRange2D)
            : MinRetrieveRange2D;

        if (corpseHorizontalDistance > retrieveRange)
        {
            _stoppedForRetrieval = false;
            if (!_loggedPathfindingMode)
            {
                Log.Information("[RETRIEVE_CORPSE] Using pathfinding toward corpse at ({X:F0}, {Y:F0}, {Z:F0})",
                    corpsePosition.X, corpsePosition.Y, corpsePosition.Z);
                _loggedPathfindingMode = true;
            }

            var now = DateTime.UtcNow;
            if (now < _runbackRecoveryHoldUntilUtc)
            {
                _lastDrivenWaypoint = null;
                ObjectManager.StopAllMovement();
                return;
            }

            var waypoint = _navPath.GetNextWaypoint(
                player.Position,
                corpseNavTarget,
                player.MapId,
                allowDirectFallback: false,
                minWaypointDistance: MinimumDriveWaypointDistance);

            if (waypoint == null)
            {
                _noPathSinceUtc ??= now;
                if (now - _lastCooldownLog >= CooldownLogInterval)
                {
                    var stalledSeconds = (int)(now - _noPathSinceUtc.Value).TotalSeconds;
                    Log.Warning("[RETRIEVE_CORPSE] No pathfinding route for corpse target ({X:F1}, {Y:F1}, {Z:F1}) stalledFor={Seconds}s distance2D={Distance2D:F1} zDelta={ZDelta:F1} trace={Trace}",
                        corpseNavTarget.X, corpseNavTarget.Y, corpseNavTarget.Z, stalledSeconds, corpseHorizontalDistance, corpseDeltaZ, GetNavigationTraceSummary());
                    BotRunnerService.DiagLog(
                        $"[RETRIEVE_CORPSE] no_path corpse=({corpseNavTarget.X:F1},{corpseNavTarget.Y:F1},{corpseNavTarget.Z:F1}) stalledFor={stalledSeconds}s distance2D={corpseHorizontalDistance:F1} zDelta={corpseDeltaZ:F1} trace={GetNavigationTraceSummary()}");
                    _lastCooldownLog = now;
                }

                if (now - _noPathSinceUtc.Value > NoPathTimeout)
                {
                    Log.Warning("[RETRIEVE_CORPSE] No pathfinding route after {Seconds}s; aborting corpse run task. trace={Trace}",
                        (int)NoPathTimeout.TotalSeconds, GetNavigationTraceSummary());
                    BotRunnerService.DiagLog(
                        $"[RETRIEVE_CORPSE] no_path_timeout seconds={(int)NoPathTimeout.TotalSeconds} trace={GetNavigationTraceSummary()}");
                    ObjectManager.StopAllMovement();
                    PopTask("NoPathTimeout");
                    return;
                }

                if (ShouldRecoverRunbackStall(player, corpseHorizontalDistance, null, out var noPathStallReason))
                {
                    RecoverRunbackStall(player, corpseHorizontalDistance, noPathStallReason);
                    return;
                }

                if (now - _lastNoPathRecoveryKickUtc >= NoPathRecoveryInterval)
                {
                    _lastNoPathRecoveryKickUtc = now;
                    if (RecoverRunbackStall(player, corpseHorizontalDistance, "pathfinding returned no route"))
                        return;
                }

                _lastDrivenWaypoint = null;
                ObjectManager.StopAllMovement();
                return;
            }

            _noPathSinceUtc = null;
            _lastNoPathRecoveryKickUtc = DateTime.MinValue;
            if (now - _lastWaypointDriveLogUtc >= TimeSpan.FromSeconds(2))
            {
                var waypointDistance = player.Position.DistanceTo(waypoint);
                var trace = _navPath.TraceSnapshot;
                Log.Information("[RETRIEVE_CORPSE] Driving path waypoint ({X:F1}, {Y:F1}, {Z:F1}) waypointDist={WaypointDist:F1} corpseDist2D={CorpseDist2D:F1} plan={Plan} idx={Index} reason={Reason}",
                    waypoint.X, waypoint.Y, waypoint.Z, waypointDistance, corpseHorizontalDistance, trace.PlanVersion, trace.CurrentWaypointIndex, trace.LastReplanReason ?? "none");
                BotRunnerService.DiagLog(
                    $"[RETRIEVE_CORPSE] waypoint=({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1}) wpDist={waypointDistance:F1} corpse2D={corpseHorizontalDistance:F1} plan={trace.PlanVersion} idx={trace.CurrentWaypointIndex} facing={player.Facing:F3} reason={trace.LastReplanReason ?? "none"}");
                _lastWaypointDriveLogUtc = now;
            }

            if (ShouldRecoverRunbackStall(player, corpseHorizontalDistance, waypoint, out var stallReason))
            {
                RecoverRunbackStall(player, corpseHorizontalDistance, stallReason);
                return;
            }

            TrackDrivenWaypoint(waypoint);
            // Drive the active waypoint continuously; MoveToward already normalizes direction flags.
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] MoveToward wp=({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1}) from=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1}) facing={player.Facing:F3}");
            ObjectManager.MoveToward(waypoint);
            return;
        }

        _runbackRecoveryCount = 0;
        _lastDrivenWaypoint = null;
        _noPathSinceUtc = null;
        _lastNoPathRecoveryKickUtc = DateTime.MinValue;
        _bestRunbackCorpseDistance2D = float.MaxValue;
        _lastRunbackProgressUtc = DateTime.MinValue;
        ResetWaypointProgressTracking();
        ResetRunbackStallTracking(player.Position);
        _runbackRecoveryHoldUntilUtc = DateTime.MinValue;

        // Only send ForceStopImmediate once when entering retrieve range.
        // Calling it every 100ms tick floods MSG_MOVE_STOP packets (~170 in 27s)
        // and triggers VMaNGOS anti-cheat disconnect.
        if (!_stoppedForRetrieval)
        {
            ObjectManager.ForceStopImmediate();
            _stoppedForRetrieval = true;
        }

        if (IsStrictAlive(player))
        {
            Log.Information("[RETRIEVE_CORPSE] Player no longer in ghost form; retrieval complete.");
            PopTask("AliveAfterRetrieve");
            return;
        }

        var isGhost = IsGhostState(player);
        if (!isGhost)
        {
            _nonGhostSinceUtc ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _nonGhostSinceUtc.Value > TimeSpan.FromSeconds(2))
            {
                var isDeadOrGhost = IsDeadOrGhostState(player);
                Log.Information("[RETRIEVE_CORPSE] Ghost state unavailable (deadOrGhost={DeadOrGhost}, health={Health}); yielding.",
                    isDeadOrGhost, player.Health);
                PopTask(isDeadOrGhost ? "GhostStateUnavailable" : "NoLongerDeadOrGhost");
            }
            return;
        }

        _nonGhostSinceUtc = null;

        var reclaimDelay = player.CorpseRecoveryDelaySeconds;
        if (reclaimDelay > 0)
        {
            if (DateTime.UtcNow - _lastCooldownLog >= CooldownLogInterval)
            {
                Log.Information("[RETRIEVE_CORPSE] Waiting for corpse reclaim cooldown: {Seconds}s remaining", reclaimDelay);
                _lastCooldownLog = DateTime.UtcNow;
            }
            return;
        }

        if (DateTime.UtcNow - _lastReclaimAttempt < ReclaimRetryInterval)
            return;

        _lastReclaimAttempt = DateTime.UtcNow;
        ObjectManager.RetrieveCorpse();
        var dist3D = MathF.Sqrt(corpseHorizontalDistance * corpseHorizontalDistance + corpseDeltaZ * corpseDeltaZ);
        Log.Information("[RETRIEVE_CORPSE] Sent reclaim request dist2D={Distance2D:F1} zDelta={ZDelta:F1} dist3D={Dist3D:F1} retrieveRange={Range:F1}",
            corpseHorizontalDistance, corpseDeltaZ, dist3D, retrieveRange);
    }
}
