using BotRunner.Movement;
using BotRunner.Interfaces;
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

    private readonly NavigationPath _navPath = new(botContext.Container.PathfindingClient);
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastReclaimAttempt = DateTime.MinValue;
    private DateTime _lastCooldownLog = DateTime.MinValue;
    private DateTime? _noPathSinceUtc;
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
    private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan NoPathTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReclaimRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CooldownLogInterval = TimeSpan.FromSeconds(5);
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

    private void ResetRunbackStallTracking(Position position)
    {
        _lastRunbackPosition = CopyPosition(position);
        _lastRunbackSampleUtc = DateTime.UtcNow;
        _runbackNoDisplacementTicks = 0;
        _runbackStaleForwardTicks = 0;
    }

    private bool ShouldRecoverRunbackStall(IWoWLocalPlayer player, out string reason)
    {
        reason = string.Empty;
        var now = DateTime.UtcNow;

        // A recovery maneuver is already active; do not immediately trigger another one.
        if (_unstickControlBits != ControlBits.Nothing && now < _unstickManeuverUntilUtc)
        {
            ResetRunbackStallTracking(player.Position);
            return false;
        }

        if (_lastRunbackPosition == null || _lastRunbackSampleUtc == DateTime.MinValue)
        {
            ResetRunbackStallTracking(player.Position);
            return false;
        }

        if (now - _lastRunbackSampleUtc < RunbackSampleInterval)
            return false;

        var stepDistance = player.Position.DistanceTo(_lastRunbackPosition);
        var hasHorizontalIntent = HasHorizontalMovementIntent(player);
        var hasRunbackCommandIntent = hasHorizontalIntent || _lastDrivenWaypoint != null || _detourTarget != null;
        var noDisplacement = stepDistance < MovementStepThreshold;

        if (noDisplacement)
            _runbackNoDisplacementTicks++;
        else
            _runbackNoDisplacementTicks = 0;

        if (hasHorizontalIntent && noDisplacement)
            _runbackStaleForwardTicks++;
        else
            _runbackStaleForwardTicks = 0;

        if (stepDistance >= 1f)
        {
            _runbackRecoveryCount = 0;
            _preferProbeRoutingUntilUtc = DateTime.MinValue;
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

        return false;
    }

    private bool RecoverRunbackStall(IWoWLocalPlayer player, Position corpseNavTarget, float corpseHorizontalDistance, string reason)
    {
        var now = DateTime.UtcNow;
        if (now - _lastRunbackRecoveryUtc < RunbackRecoveryInterval)
            return false;

        _lastRunbackRecoveryUtc = now;
        _runbackRecoveryCount++;
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

        if (_runbackRecoveryCount > MaxRunbackRecoveryAttempts)
        {
            Log.Warning("[RETRIEVE_CORPSE] Runback remained stalled after {Attempts} recoveries; aborting task.",
                _runbackRecoveryCount);
            PopTask("RunbackStallRecoveryExceeded");
            return true;
        }

        return false;
    }

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

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            PopTask("PlayerUnavailable");
            return;
        }

        if (DateTime.UtcNow - _startTime > TaskTimeout)
        {
            Log.Warning("[RETRIEVE_CORPSE] Timed out navigating to corpse");
            ObjectManager.StopAllMovement();
            PopTask("Timeout");
            return;
        }

        var corpseHorizontalDistance = player.Position.DistanceTo2D(corpsePosition);
        var corpseDeltaZ = MathF.Abs(player.Position.Z - corpsePosition.Z);
        var corpseNavTarget = BuildCorpseNavigationTarget(player.Position, corpsePosition);

        if (corpseHorizontalDistance > RetrieveRange)
        {
            if (ExecuteUnstickManeuver(player, corpseNavTarget, corpseHorizontalDistance))
                return;

            if (!_loggedPathfindingMode)
            {
                Log.Information("[RETRIEVE_CORPSE] Using pathfinding toward corpse at ({X:F0}, {Y:F0}, {Z:F0})",
                    corpsePosition.X, corpsePosition.Y, corpsePosition.Z);
                _loggedPathfindingMode = true;
            }

            var now = DateTime.UtcNow;
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
                    ObjectManager.StopAllMovement();
                    PopTask("NoPathTimeout");
                    return;
                }

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
            ObjectManager.MoveToward(waypoint);
            return;
        }

        _runbackRecoveryCount = 0;
        _preferProbeRoutingUntilUtc = DateTime.MinValue;
        _unstickControlBits = ControlBits.Nothing;
        _lastDrivenWaypoint = null;
        _noPathSinceUtc = null;
        _bestNoPathDistance2D = null;
        ClearRunbackDetour();
        ResetRunbackStallTracking(player.Position);
        ObjectManager.StopAllMovement();

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
        Log.Information("[RETRIEVE_CORPSE] Sent reclaim request at ({X:F0}, {Y:F0}, {Z:F0}) distance2D={Distance2D:F1} zDelta={ZDelta:F1}",
            corpsePosition.X, corpsePosition.Y, corpsePosition.Z, corpseHorizontalDistance, corpseDeltaZ);
    }
}
