using BotRunner.Movement;
using BotRunner.Interfaces;
<<<<<<< HEAD
=======
using GameData.Core.Constants;
>>>>>>> cpp_physics_system
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: run to corpse using pathfinding and reclaim when eligible.
/// </summary>
public class RetrieveCorpseTask(IBotContext botContext, Position corpsePosition) : BotTask(botContext), IBotTask
{
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

<<<<<<< HEAD
    private readonly NavigationPath _navPath = new(botContext.Container.PathfindingClient);
=======
    // Corpse runback follows the navmesh path faithfully.
    // Disable probe heuristics/pruning so corners are not skipped into walls.
    // strictPathValidation is OFF because long outdoor corpse runs (460y+) have
    // segments where collision-based LOS rejects valid navmesh paths.
    private readonly NavigationPath _navPath = CreateCorpseNavPath(botContext);

    private static NavigationPath CreateCorpseNavPath(IBotContext ctx)
    {
        var player = ctx.ObjectManager.Player;
        var (radius, height) = player != null
            ? RaceDimensions.GetCapsuleForRace(player.Race, player.Gender)
            : (0.3064f, 2.0313f);
        return new NavigationPath(
            ctx.Container.PathfindingClient,
            enableProbeHeuristics: false,
            enableDynamicProbeSkipping: false,
            strictPathValidation: false,
            capsuleRadius: radius,
            capsuleHeight: height,
            nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(ctx.ObjectManager, start, end),
            race: player?.Race ?? 0,
            gender: player?.Gender ?? 0);
    }
>>>>>>> cpp_physics_system
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastReclaimAttempt = DateTime.MinValue;
    private DateTime _lastCooldownLog = DateTime.MinValue;
    private DateTime? _noPathSinceUtc;
<<<<<<< HEAD
    private float? _bestNoPathDistance2D;
    private bool _loggedPathfindingMode;
    private DateTime? _nonGhostSinceUtc;
    private DateTime _lastProbeAttemptUtc = DateTime.MinValue;
    private Position? _cachedProbeWaypoint;
    private DateTime _cachedProbeWaypointExpiresUtc = DateTime.MinValue;
    private Position? _lastRunbackPosition;
    private DateTime _lastRunbackSampleUtc = DateTime.MinValue;
    private DateTime _lastRunbackRecoveryUtc = DateTime.MinValue;
    private DateTime _preferProbeRoutingUntilUtc = DateTime.MinValue;
    private DateTime _lastWaypointDriveLogUtc = DateTime.MinValue;
    private DateTime _unstickManeuverUntilUtc = DateTime.MinValue;
    private DateTime _lastUnstickLogUtc = DateTime.MinValue;
    private DateTime _lastDetourLogUtc = DateTime.MinValue;
    private ControlBits _unstickControlBits = ControlBits.Nothing;
    private int _runbackNoDisplacementTicks;
    private int _runbackStaleForwardTicks;
    private int _runbackRecoveryCount;
    private Position? _lastDrivenWaypoint;
    private Position? _blockedWaypoint;
    private DateTime _blockedWaypointExpiresUtc = DateTime.MinValue;
    private Position? _detourTarget;
    private DateTime _detourUntilUtc = DateTime.MinValue;

    // Vanilla corpse reclaim interaction radius is roughly 39 yards.
    // Staying at 5y causes long ghost stalls when the graveyard drop is already within reclaim range.
    private const float RetrieveRange = 39f;
=======
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

    // MaNGOS CORPSE_RECLAIM_RADIUS = 39y (3D distance). We compute a dynamic 2D
    // approach distance from the current Z delta so the bot walks close enough in
    // 3D even in multi-level areas like Orgrimmar where the graveyard is 20+ yards
    // above/below the corpse.
    private const float ServerReclaimRadius3D = 39f;
    private const float ReclaimSafetyMargin = 5f; // stay 5y inside the 39y sphere
    private const float MinRetrieveRange2D = 5f;   // never stop further than this minimum
>>>>>>> cpp_physics_system
    private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan NoPathTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReclaimRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CooldownLogInterval = TimeSpan.FromSeconds(5);
<<<<<<< HEAD
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProbeWaypointTtl = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RunbackSampleInterval = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan RunbackRecoveryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PreferProbeRoutingDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan UnstickManeuverDuration = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan BlockedWaypointDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RunbackDetourDuration = TimeSpan.FromSeconds(6);
    private const float MaxCorpseZDeltaForNavigation = 120f;
    private const float MovementStepThreshold = 0.2f;
    private const float NearWaypointThreshold = 1.5f;
    private const float MinimumDriveWaypointDistance = 6f;
    private const float BlockedWaypointRadius = 2.5f;
    private const float BlockedWaypointZTolerance = 6f;
    private const float RunbackDetourDistance = 18f;
    private const float RunbackDetourReachDistance = 4f;
    private const int RunbackNoDisplacementThreshold = 8;
    private const int RunbackStaleForwardThreshold = 6;
    private const int MaxRunbackRecoveryAttempts = 8;
    private static readonly (float X, float Y)[] StartProbeOffsets =
    [
        (0f, 0f),
        (2f, 0f), (-2f, 0f), (0f, 2f), (0f, -2f),
        (4f, 4f), (-4f, 4f), (4f, -4f), (-4f, -4f),
        (6f, 0f), (-6f, 0f), (0f, 6f), (0f, -6f),
    ];
    private static readonly (float X, float Y)[] CorpseApproachOffsets =
    [
        (5f, 0f), (-5f, 0f), (0f, 5f), (0f, -5f),
        (10f, 0f), (-10f, 0f), (0f, 10f), (0f, -10f),
        (7f, 7f), (-7f, 7f), (7f, -7f), (-7f, -7f),
    ];
=======
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
>>>>>>> cpp_physics_system

    private static bool HasGhostFlag(IWoWLocalPlayer player)
    {
        try { return (((uint)player.PlayerFlags) & PlayerFlagGhost) != 0; }
        catch { return false; }
    }

    private static bool IsStandStateDeadFlag(IWoWLocalPlayer player)
    {
        try
        {
            var bytes1 = player.Bytes1;
            return bytes1 != null
                && bytes1.Length > 0
                && (bytes1[0] & StandStateMask) == StandStateDead;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStrictAlive(IWoWLocalPlayer player)
        => player.Health > 0 && !HasGhostFlag(player) && !IsStandStateDeadFlag(player);

    private static bool IsGhostState(IWoWLocalPlayer player)
    {
        if (HasGhostFlag(player))
            return true;

        try { return player.InGhostForm; }
        catch { return false; }
    }

    private static bool IsDeadOrGhostState(IWoWLocalPlayer player)
        => player.Health == 0 || HasGhostFlag(player) || IsStandStateDeadFlag(player) || IsGhostState(player);

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

<<<<<<< HEAD
=======
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

>>>>>>> cpp_physics_system
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

<<<<<<< HEAD
    private static bool IsWaypointApproximatelyEqual(Position a, Position b)
        => a.DistanceTo2D(b) <= BlockedWaypointRadius && MathF.Abs(a.Z - b.Z) <= BlockedWaypointZTolerance;

    private bool IsBlockedWaypoint(Position waypoint)
        => _blockedWaypoint != null
            && DateTime.UtcNow <= _blockedWaypointExpiresUtc
            && IsWaypointApproximatelyEqual(waypoint, _blockedWaypoint);

    private void TrackDrivenWaypoint(Position waypoint)
        => _lastDrivenWaypoint = CopyPosition(waypoint);

    private void ClearRunbackDetour()
    {
        _detourTarget = null;
        _detourUntilUtc = DateTime.MinValue;
    }

    private Position ResolveRunbackNavigationTarget(Position currentPosition, Position corpseNavTarget)
    {
        if (_detourTarget == null)
            return corpseNavTarget;

        if (DateTime.UtcNow > _detourUntilUtc)
        {
            ClearRunbackDetour();
            return corpseNavTarget;
        }

        var detourDistance2D = currentPosition.DistanceTo2D(_detourTarget);
        if (detourDistance2D <= RunbackDetourReachDistance)
        {
            ClearRunbackDetour();
            return corpseNavTarget;
        }

        if (DateTime.UtcNow - _lastDetourLogUtc >= TimeSpan.FromSeconds(2))
        {
            Log.Information("[RETRIEVE_CORPSE] Detour active toward ({X:F1}, {Y:F1}, {Z:F1}) detourDist={DetourDist:F1}",
                _detourTarget.X, _detourTarget.Y, _detourTarget.Z, detourDistance2D);
            _lastDetourLogUtc = DateTime.UtcNow;
        }

        return _detourTarget;
    }

    private void ScheduleRunbackDetour(Position currentPosition, Position corpseNavTarget, DateTime now)
    {
        var angleToCorpse = MathF.Atan2(corpseNavTarget.Y - currentPosition.Y, corpseNavTarget.X - currentPosition.X);
        var side = (_runbackRecoveryCount % 2 == 0) ? 1f : -1f;
        var detourAngle = angleToCorpse + side * (MathF.PI / 2f);
        _detourTarget = new Position(
            currentPosition.X + MathF.Cos(detourAngle) * RunbackDetourDistance,
            currentPosition.Y + MathF.Sin(detourAngle) * RunbackDetourDistance,
            currentPosition.Z);
        _detourUntilUtc = now + RunbackDetourDuration;
        _lastDetourLogUtc = DateTime.MinValue;
    }

    private bool TryGetOffsetApproachWaypoint(
        Position currentPosition,
        Position corpseNavTarget,
        uint mapId,
        out Position? waypoint,
        out Position? probeStart,
        out Position? probeTarget,
        out bool fromCache)
    {
        waypoint = null;
        probeStart = null;
        probeTarget = null;
        fromCache = false;

        // Keep moving toward a recently discovered probe waypoint between expensive probe rounds.
        if (_cachedProbeWaypoint != null
            && DateTime.UtcNow <= _cachedProbeWaypointExpiresUtc
            && !IsBlockedWaypoint(_cachedProbeWaypoint)
            && currentPosition.DistanceTo(_cachedProbeWaypoint) > 2f)
        {
            waypoint = _cachedProbeWaypoint;
            fromCache = true;
            return true;
        }

        if (DateTime.UtcNow - _lastProbeAttemptUtc < ProbeInterval)
            return false;

        _lastProbeAttemptUtc = DateTime.UtcNow;

        foreach (var (startOffsetX, startOffsetY) in StartProbeOffsets)
        {
            var adjustedStart = new Position(
                currentPosition.X + startOffsetX,
                currentPosition.Y + startOffsetY,
                currentPosition.Z);

            // Try direct corpse target first.
            {
                var directProbePath = new NavigationPath(Container.PathfindingClient);
                var directWaypoint = directProbePath.GetNextWaypoint(
                    adjustedStart,
                    corpseNavTarget,
                    mapId,
                    allowDirectFallback: false,
                    minWaypointDistance: MinimumDriveWaypointDistance);
                if (directWaypoint != null && !IsBlockedWaypoint(directWaypoint))
                {
                    _cachedProbeWaypoint = directWaypoint;
                    _cachedProbeWaypointExpiresUtc = DateTime.UtcNow + ProbeWaypointTtl;
                    waypoint = directWaypoint;
                    probeStart = adjustedStart;
                    probeTarget = corpseNavTarget;
                    return true;
                }
            }

            foreach (var (targetOffsetX, targetOffsetY) in CorpseApproachOffsets)
            {
                var approachTarget = new Position(
                    corpseNavTarget.X + targetOffsetX,
                    corpseNavTarget.Y + targetOffsetY,
                    corpseNavTarget.Z);

                // Use a fresh path object for probing so internal path-calc cooldown on the
                // main corpse path does not block alternate target attempts.
                var probePath = new NavigationPath(Container.PathfindingClient);
                var offsetWaypoint = probePath.GetNextWaypoint(
                    adjustedStart,
                    approachTarget,
                    mapId,
                    allowDirectFallback: false,
                    minWaypointDistance: MinimumDriveWaypointDistance);
                if (offsetWaypoint != null && !IsBlockedWaypoint(offsetWaypoint))
                {
                    _cachedProbeWaypoint = offsetWaypoint;
                    _cachedProbeWaypointExpiresUtc = DateTime.UtcNow + ProbeWaypointTtl;
                    waypoint = offsetWaypoint;
                    probeStart = adjustedStart;
                    probeTarget = approachTarget;
                    return true;
                }
            }
        }

        _cachedProbeWaypoint = null;
        _cachedProbeWaypointExpiresUtc = DateTime.MinValue;
        return false;
    }

=======
    private void TrackDrivenWaypoint(Position waypoint)
        => _lastDrivenWaypoint = CopyPosition(waypoint);

>>>>>>> cpp_physics_system
    private void ResetRunbackStallTracking(Position position)
    {
        _lastRunbackPosition = CopyPosition(position);
        _lastRunbackSampleUtc = DateTime.UtcNow;
        _runbackNoDisplacementTicks = 0;
        _runbackStaleForwardTicks = 0;
    }

<<<<<<< HEAD
    private bool ShouldRecoverRunbackStall(IWoWLocalPlayer player, out string reason)
=======
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
>>>>>>> cpp_physics_system
    {
        reason = string.Empty;
        var now = DateTime.UtcNow;

<<<<<<< HEAD
        // A recovery maneuver is already active; do not immediately trigger another one.
        if (_unstickControlBits != ControlBits.Nothing && now < _unstickManeuverUntilUtc)
        {
            ResetRunbackStallTracking(player.Position);
            return false;
        }
=======
        TrackRunbackProgress(corpseHorizontalDistance, now);
        if (activeWaypoint != null)
            TrackRunbackWaypointProgress(player.Position, activeWaypoint, now);
        else
            ResetWaypointProgressTracking();
>>>>>>> cpp_physics_system

        if (_lastRunbackPosition == null || _lastRunbackSampleUtc == DateTime.MinValue)
        {
            ResetRunbackStallTracking(player.Position);
<<<<<<< HEAD
=======
            ResetRunbackProgressTracking(corpseHorizontalDistance);
>>>>>>> cpp_physics_system
            return false;
        }

        if (now - _lastRunbackSampleUtc < RunbackSampleInterval)
            return false;

        var stepDistance = player.Position.DistanceTo(_lastRunbackPosition);
        var hasHorizontalIntent = HasHorizontalMovementIntent(player);
<<<<<<< HEAD
        var hasRunbackCommandIntent = hasHorizontalIntent || _lastDrivenWaypoint != null || _detourTarget != null;
=======
        var hasRunbackCommandIntent = hasHorizontalIntent || _lastDrivenWaypoint != null;
>>>>>>> cpp_physics_system
        var noDisplacement = stepDistance < MovementStepThreshold;

        if (noDisplacement)
            _runbackNoDisplacementTicks++;
        else
            _runbackNoDisplacementTicks = 0;

        if (hasHorizontalIntent && noDisplacement)
            _runbackStaleForwardTicks++;
        else
            _runbackStaleForwardTicks = 0;

<<<<<<< HEAD
        if (stepDistance >= 1f)
        {
            _runbackRecoveryCount = 0;
            _preferProbeRoutingUntilUtc = DateTime.MinValue;
=======
        // Only reset recovery count when making meaningful progress TOWARD the corpse,
        // not just any displacement. The jump+backward recovery maneuver moves the bot
        // ≥1y but doesn't bring it closer to the corpse — resetting on any movement
        // prevents the recovery counter from ever reaching MaxRunbackRecoveryAttempts.
        if (stepDistance >= 1f && corpseHorizontalDistance < _bestRunbackCorpseDistance2D - 5f)
        {
            _runbackRecoveryCount = 0;
>>>>>>> cpp_physics_system
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

<<<<<<< HEAD
        return false;
    }

    private bool RecoverRunbackStall(IWoWLocalPlayer player, Position corpseNavTarget, float corpseHorizontalDistance, string reason)
=======
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
>>>>>>> cpp_physics_system
    {
        var now = DateTime.UtcNow;
        if (now - _lastRunbackRecoveryUtc < RunbackRecoveryInterval)
            return false;

        _lastRunbackRecoveryUtc = now;
        _runbackRecoveryCount++;
<<<<<<< HEAD
        _preferProbeRoutingUntilUtc = now + PreferProbeRoutingDuration;

        if (_lastDrivenWaypoint != null)
        {
            _blockedWaypoint = CopyPosition(_lastDrivenWaypoint);
            _blockedWaypointExpiresUtc = now + BlockedWaypointDuration;
        }

        ScheduleRunbackDetour(player.Position, corpseNavTarget, now);

        ObjectManager.ForceStopImmediate();
        _navPath.Clear();
        _cachedProbeWaypoint = null;
        _cachedProbeWaypointExpiresUtc = DateTime.MinValue;
        _lastProbeAttemptUtc = DateTime.MinValue;
        _noPathSinceUtc = null;
        ResetRunbackStallTracking(player.Position);
        _unstickControlBits = ((_runbackRecoveryCount - 1) % 3) switch
        {
            0 => ControlBits.StrafeLeft,
            1 => ControlBits.StrafeRight,
            _ => ControlBits.Back
        };
        _unstickManeuverUntilUtc = now + UnstickManeuverDuration;
        _lastUnstickLogUtc = DateTime.MinValue;
        _lastDrivenWaypoint = null;

        Log.Warning("[RETRIEVE_CORPSE] Runback stall recovery #{Attempt}: {Reason} (distance2D={Distance2D:F1}). Cleared movement and rebuilding path.",
            _runbackRecoveryCount, reason, corpseHorizontalDistance);
=======

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
>>>>>>> cpp_physics_system

        if (_runbackRecoveryCount > MaxRunbackRecoveryAttempts)
        {
            Log.Warning("[RETRIEVE_CORPSE] Runback remained stalled after {Attempts} recoveries; aborting task.",
                _runbackRecoveryCount);
<<<<<<< HEAD
=======
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] stall_recovery_exceeded attempts={_runbackRecoveryCount} trace={GetNavigationTraceSummary()}");
>>>>>>> cpp_physics_system
            PopTask("RunbackStallRecoveryExceeded");
            return true;
        }

        return false;
    }

<<<<<<< HEAD
    private bool ExecuteUnstickManeuver(IWoWLocalPlayer player, Position corpseNavTarget, float corpseHorizontalDistance)
    {
        if (_unstickControlBits == ControlBits.Nothing)
            return false;

        var now = DateTime.UtcNow;
        if (now >= _unstickManeuverUntilUtc)
        {
            ObjectManager.StopMovement(_unstickControlBits);
            _unstickControlBits = ControlBits.Nothing;
            return false;
        }

        try
        {
            var facing = player.GetFacingForPosition(corpseNavTarget);
            ObjectManager.SetFacing(facing);
        }
        catch
        {
            // Keep maneuver active even if facing query transiently fails.
        }

        ObjectManager.StartMovement(_unstickControlBits);
        if (now - _lastUnstickLogUtc >= TimeSpan.FromSeconds(2))
        {
            Log.Information("[RETRIEVE_CORPSE] Unstick maneuver active: {Maneuver} distance2D={Distance2D:F1}",
                _unstickControlBits, corpseHorizontalDistance);
            _lastUnstickLogUtc = now;
        }

        return true;
    }

=======
>>>>>>> cpp_physics_system
    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
<<<<<<< HEAD
=======
            BotRunnerService.DiagLog($"[RETRIEVE_CORPSE] Update: PlayerUnavailable player={player != null} pos={player?.Position != null}");
>>>>>>> cpp_physics_system
            PopTask("PlayerUnavailable");
            return;
        }

        if (DateTime.UtcNow - _startTime > TaskTimeout)
        {
            Log.Warning("[RETRIEVE_CORPSE] Timed out navigating to corpse");
<<<<<<< HEAD
=======
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] timeout corpse=({corpsePosition.X:F1},{corpsePosition.Y:F1},{corpsePosition.Z:F1}) trace={GetNavigationTraceSummary()}");
>>>>>>> cpp_physics_system
            ObjectManager.StopAllMovement();
            PopTask("Timeout");
            return;
        }

        var corpseHorizontalDistance = player.Position.DistanceTo2D(corpsePosition);
        var corpseDeltaZ = MathF.Abs(player.Position.Z - corpsePosition.Z);
        var corpseNavTarget = BuildCorpseNavigationTarget(player.Position, corpsePosition);

<<<<<<< HEAD
        if (corpseHorizontalDistance > RetrieveRange)
        {
            if (ExecuteUnstickManeuver(player, corpseNavTarget, corpseHorizontalDistance))
                return;

=======
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
>>>>>>> cpp_physics_system
            if (!_loggedPathfindingMode)
            {
                Log.Information("[RETRIEVE_CORPSE] Using pathfinding toward corpse at ({X:F0}, {Y:F0}, {Z:F0})",
                    corpsePosition.X, corpsePosition.Y, corpsePosition.Z);
                _loggedPathfindingMode = true;
            }

            var now = DateTime.UtcNow;
<<<<<<< HEAD
            var preferProbeRouting = now <= _preferProbeRoutingUntilUtc;
            var runbackTarget = ResolveRunbackNavigationTarget(player.Position, corpseNavTarget);

            Position? waypoint = null;
            if (!preferProbeRouting)
            {
                waypoint = _navPath.GetNextWaypoint(
                    player.Position,
                    runbackTarget,
                    player.MapId,
                    allowDirectFallback: false,
                    minWaypointDistance: MinimumDriveWaypointDistance);
                if (waypoint != null && player.Position.DistanceTo2D(waypoint) <= NearWaypointThreshold)
                {
                    Log.Warning("[RETRIEVE_CORPSE] Main route waypoint is too close while corpse remains far (distance2D={Distance2D:F1}); forcing probe reroute.",
                        corpseHorizontalDistance);
                    _navPath.Clear();
                    waypoint = null;
                    preferProbeRouting = true;
                }

                if (waypoint != null && IsBlockedWaypoint(waypoint))
                {
                    Log.Warning("[RETRIEVE_CORPSE] Main route repeated blocked waypoint ({X:F1}, {Y:F1}, {Z:F1}); forcing probe reroute.",
                        waypoint.X, waypoint.Y, waypoint.Z);
                    _navPath.Clear();
                    waypoint = null;
                    preferProbeRouting = true;
                }
            }

            if (preferProbeRouting || waypoint == null)
            {
                if (TryGetOffsetApproachWaypoint(
                    player.Position,
                    runbackTarget,
                    player.MapId,
                    out var probeWaypoint,
                    out var probeStart,
                    out var probeTarget,
                    out var fromCache)
                    && probeWaypoint != null)
                {
                    if (!fromCache && probeTarget != null && DateTime.UtcNow - _lastCooldownLog >= CooldownLogInterval)
                    {
                        if (probeStart != null)
                        {
                            Log.Information("[RETRIEVE_CORPSE] No direct route; using probe start ({SX:F1}, {SY:F1}, {SZ:F1}) and target ({TX:F1}, {TY:F1}, {TZ:F1})",
                                probeStart.X, probeStart.Y, probeStart.Z,
                                probeTarget.X, probeTarget.Y, probeTarget.Z);
                        }
                        else
                        {
                            Log.Information("[RETRIEVE_CORPSE] No direct route; using probe target ({TX:F1}, {TY:F1}, {TZ:F1})",
                                probeTarget.X, probeTarget.Y, probeTarget.Z);
                        }
                        _lastCooldownLog = DateTime.UtcNow;
                    }

                    _noPathSinceUtc = null;
                    _bestNoPathDistance2D = null;
                    if (DateTime.UtcNow - _lastWaypointDriveLogUtc >= TimeSpan.FromSeconds(2))
                    {
                        var waypointDistance = player.Position.DistanceTo(probeWaypoint);
                        Log.Information("[RETRIEVE_CORPSE] Driving probe waypoint ({X:F1}, {Y:F1}, {Z:F1}) waypointDist={WaypointDist:F1} corpseDist2D={CorpseDist2D:F1}",
                            probeWaypoint.X, probeWaypoint.Y, probeWaypoint.Z, waypointDistance, corpseHorizontalDistance);
                        _lastWaypointDriveLogUtc = DateTime.UtcNow;
                    }
                    if (ShouldRecoverRunbackStall(player, out var probeStallReason)
                        && RecoverRunbackStall(player, corpseNavTarget, corpseHorizontalDistance, probeStallReason))
                    {
                        return;
                    }
                    TrackDrivenWaypoint(probeWaypoint);
                    ObjectManager.MoveToward(probeWaypoint);
                    return;
                }

                var noPathNow = DateTime.UtcNow;
                _noPathSinceUtc ??= noPathNow;

                // Keep no-path timeout focused on true stalls. As long as distance to corpse
                // keeps improving, continue driving direct runback movement and reset the stall timer.
                if (_bestNoPathDistance2D == null || corpseHorizontalDistance + 0.5f < _bestNoPathDistance2D.Value)
                {
                    _bestNoPathDistance2D = corpseHorizontalDistance;
                    _noPathSinceUtc = noPathNow;
                }
                else if (corpseHorizontalDistance < _bestNoPathDistance2D.Value)
                {
                    _bestNoPathDistance2D = corpseHorizontalDistance;
                }

                if (noPathNow - _lastCooldownLog >= CooldownLogInterval)
                {
                    var stalledSeconds = (int)(noPathNow - _noPathSinceUtc.Value).TotalSeconds;
                    Log.Warning("[RETRIEVE_CORPSE] No pathfinding route; driving fallback target ({X:F1}, {Y:F1}, {Z:F1}) stalledFor={Seconds}s distance2D={Distance2D:F1} zDelta={ZDelta:F1}",
                        runbackTarget.X, runbackTarget.Y, runbackTarget.Z, stalledSeconds, corpseHorizontalDistance, corpseDeltaZ);
                    _lastCooldownLog = noPathNow;
                }

                if (noPathNow - _noPathSinceUtc.Value > NoPathTimeout)
                {
                    Log.Warning("[RETRIEVE_CORPSE] No pathfinding route after {Seconds}s; aborting corpse run task.",
                        (int)NoPathTimeout.TotalSeconds);
=======
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
>>>>>>> cpp_physics_system
                    ObjectManager.StopAllMovement();
                    PopTask("NoPathTimeout");
                    return;
                }

<<<<<<< HEAD
                if (DateTime.UtcNow - _lastWaypointDriveLogUtc >= TimeSpan.FromSeconds(2))
                {
                    var fallbackDistance = player.Position.DistanceTo(runbackTarget);
                    Log.Information("[RETRIEVE_CORPSE] Driving fallback target ({X:F1}, {Y:F1}, {Z:F1}) waypointDist={WaypointDist:F1} corpseDist2D={CorpseDist2D:F1}",
                        runbackTarget.X, runbackTarget.Y, runbackTarget.Z, fallbackDistance, corpseHorizontalDistance);
                    _lastWaypointDriveLogUtc = DateTime.UtcNow;
                }
                if (ShouldRecoverRunbackStall(player, out var noPathStallReason)
                    && RecoverRunbackStall(player, corpseNavTarget, corpseHorizontalDistance, noPathStallReason))
                {
                    return;
                }
                TrackDrivenWaypoint(runbackTarget);
                ObjectManager.MoveToward(runbackTarget);
                return;
            }

            _cachedProbeWaypoint = null;
            _cachedProbeWaypointExpiresUtc = DateTime.MinValue;
            _noPathSinceUtc = null;
            _bestNoPathDistance2D = null;
            if (waypoint == null)
            {
                ObjectManager.StopAllMovement();
                return;
            }
            if (DateTime.UtcNow - _lastWaypointDriveLogUtc >= TimeSpan.FromSeconds(2))
            {
                var waypointDistance = player.Position.DistanceTo(waypoint);
                Log.Information("[RETRIEVE_CORPSE] Driving main waypoint ({X:F1}, {Y:F1}, {Z:F1}) waypointDist={WaypointDist:F1} corpseDist2D={CorpseDist2D:F1}",
                    waypoint.X, waypoint.Y, waypoint.Z, waypointDistance, corpseHorizontalDistance);
                _lastWaypointDriveLogUtc = DateTime.UtcNow;
            }
            if (ShouldRecoverRunbackStall(player, out var stallReason)
                && RecoverRunbackStall(player, corpseNavTarget, corpseHorizontalDistance, stallReason))
            {
                return;
            }
            TrackDrivenWaypoint(waypoint);
=======
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
>>>>>>> cpp_physics_system
            ObjectManager.MoveToward(waypoint);
            return;
        }

        _runbackRecoveryCount = 0;
<<<<<<< HEAD
        _preferProbeRoutingUntilUtc = DateTime.MinValue;
        _unstickControlBits = ControlBits.Nothing;
        _lastDrivenWaypoint = null;
        _noPathSinceUtc = null;
        _bestNoPathDistance2D = null;
        ClearRunbackDetour();
        ResetRunbackStallTracking(player.Position);
        ObjectManager.StopAllMovement();
=======
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
>>>>>>> cpp_physics_system

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
<<<<<<< HEAD
        Log.Information("[RETRIEVE_CORPSE] Sent reclaim request at ({X:F0}, {Y:F0}, {Z:F0}) distance2D={Distance2D:F1} zDelta={ZDelta:F1}",
            corpsePosition.X, corpsePosition.Y, corpsePosition.Z, corpseHorizontalDistance, corpseDeltaZ);
=======
        var dist3D = MathF.Sqrt(corpseHorizontalDistance * corpseHorizontalDistance + corpseDeltaZ * corpseDeltaZ);
        Log.Information("[RETRIEVE_CORPSE] Sent reclaim request dist2D={Distance2D:F1} zDelta={ZDelta:F1} dist3D={Dist3D:F1} retrieveRange={Range:F1}",
            corpseHorizontalDistance, corpseDeltaZ, dist3D, retrieveRange);
>>>>>>> cpp_physics_system
    }
}
