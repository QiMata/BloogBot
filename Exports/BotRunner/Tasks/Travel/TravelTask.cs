using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Cross-world travel task. Decomposes a travel objective into observable
/// walk, taxi, and transport legs, then executes those legs sequentially.
/// </summary>
public class TravelTask : BotTask, IBotTask
{
    private const int MaxReplans = 3;
    private const float DefaultArrivalRadius = 5.0f;
    private const float WalkLegArrivalRadius = 15.0f;
    // A plain (non-transport) walk leg only "arrives" when the bot is within the
    // 2D radius AND on roughly the same vertical layer as leg.End. Without this,
    // a destination directly above the bot (deck / tower / upper floor) within
    // WalkLegArrivalRadius horizontally falsely completes the leg at the base —
    // e.g. OG zeppelin tower: Frezza (z=53.6) is ~14.5y due-south of the Grunt
    // base (z=24), so the base is 2D<=15y from Frezza yet ~30y below it. The leg
    // would complete at the base and dump the bot into the Standard-policy
    // route-exhausted fallback, which cannot drive the long climb. 6y matches the
    // transport vertical tolerance: enough for capsule height + slope/ground-Z
    // noise, tight enough to reject a floor/deck layer mismatch. (2026-06-01)
    private const float WalkLegVerticalArrivalTolerance = 6.0f;
    private const float WalkLegTransportArrivalRadius = 4.0f;
    private const float WalkLegTransportVerticalArrivalTolerance = 6.0f;
    // Phase 5.3.6 (PFS-OVERHAUL-006): under WWOW_OFFMESH_NATIVE_BOARDING, the
    // walk leg must end on the same deck tier as the configured boarding zone.
    // The legacy 6y tolerance accepted arrival on the LOWER spiral coil
    // (z≈50) where the bot is XY-close but vertically wrong — boarding
    // cascade then can't bridge the 3y Z gap because Detour's path from there
    // leads through the OG tower's central pillar (Phase 5.3.4
    // "Detour-walkable but physically-blocked" geometry). 1.5y matches
    // NavigationPath's WAYPOINT_VERTICAL_REACH_TOLERANCE=1.25f plus a quantum,
    // forcing same-deck arrival before walk_arrived fires.
    private const float WalkLegNativeOffMeshTransportVerticalArrivalTolerance = 1.5f;
    private const float TransportNearbyObjectRadius = 80.0f;
    private const float FlightMasterSearchRadius = 60.0f;
    private const float FlightArrivalRadius = 10.0f;
    private const float FlightArrivalStableDistance = 1.0f;
    private const int FlightArrivalStableTicks = 3;
    private const int FlightActivationMaxAttempts = 5;
    private const double FlightActivationRetrySeconds = 3.0;
    private const double FlightTimeoutSeconds = 360.0;
    private const double TransportTimeoutSeconds = 480.0;
    private const float WalkLegStallMovementYards = 1.5f;
    private const float WalkLegStallActiveWaypointProgressYards = 1.0f;
    private const float WalkLegStallFallbackMovementYards = 6.0f;
    private const double WalkLegStallRecoverySeconds = 5.0;
    private const double WalkLegStallRecoveryCooldownSeconds = 3.0;
    private const float TransportExitVerticalArrivalTolerance = 25.0f;
    private const uint NpcFlagFlightMaster = (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER;

    private readonly uint _targetMapId;
    private readonly TravelOptions _options;
    private readonly Func<DateTime> _utcNow;
    private Position _targetPosition;
    private float _arrivalRadius;

    private List<RouteLeg>? _route;
    private int _currentLegIndex;
    private bool _initialized;
    private int _replanCount;
    private int _activeLegIndex = -1;
    private DateTime _lastUpdateUtc = DateTime.MinValue;
    private DateTime _legStartUtc = DateTime.MinValue;

    private bool _flightActivated;
    private bool _flightDeparted;
    private int _flightActivationAttempts;
    private DateTime _nextFlightActivationUtc = DateTime.MinValue;
    private Position? _flightActivationStart;
    private Position? _lastFlightArrivalCandidate;
    private int _flightArrivalStableTicks;
    private int _lastWalkTraceLegIndex = -1;
    private int _lastWalkTracePlanVersion = -1;
    private int _lastWalkTraceWaypointIndex = -1;
    private int _lastWalkTraceStuckGeneration = -1;
    private string? _lastWalkTraceResolution;
    private DateTime _lastWalkTraceEmitUtc = DateTime.MinValue;
    private Position? _walkProgressAnchor;
    private uint _walkProgressAnchorMapId;
    private DateTime _walkProgressAnchorUtc = DateTime.MinValue;
    private int _walkProgressAnchorPlanVersion = -1;
    private int _walkProgressAnchorWaypointIndex = -1;
    private float _walkProgressAnchorActiveWaypointDistance = float.NaN;
    private DateTime _lastWalkStallRecoveryUtc = DateTime.MinValue;
    private int _walkStallRecoveryCount;
    private string? _lastFlightTraceSignature;
    private DateTime _lastFlightTraceEmitUtc = DateTime.MinValue;
    private string? _lastTransportTraceSignature;
    private DateTime _lastTransportTraceEmitUtc = DateTime.MinValue;
    private Position? _transportBoardingProgressAnchor;
    private uint _transportBoardingProgressAnchorMapId;
    private DateTime _transportBoardingProgressAnchorUtc = DateTime.MinValue;
    private DateTime _lastTransportBoardingStallRecoveryUtc = DateTime.MinValue;
    private int _transportBoardingStallRecoveryCount;
    private Position? _scheduledTransportBoardingTarget;
    private float? _scheduledTransportBoardingFacing;

    private TransportWaitingLogic? _transportLogic;

    public TravelTask(
        IBotContext context,
        uint targetMapId,
        Position targetPos,
        TravelOptions? options = null,
        float arrivalRadius = DefaultArrivalRadius,
        Func<DateTime>? utcNowProvider = null)
        : base(context)
    {
        _targetMapId = targetMapId;
        _targetPosition = targetPos;
        _options = options ?? new TravelOptions();
        _arrivalRadius = arrivalRadius > 0f ? arrivalRadius : DefaultArrivalRadius;
        _utcNow = utcNowProvider ?? (() => DateTime.UtcNow);
        _lastUpdateUtc = _utcNow();
        _legStartUtc = _lastUpdateUtc;
    }

    internal uint TargetMapId => _targetMapId;
    internal Position TargetPosition => _targetPosition;
    internal float ArrivalRadius => _arrivalRadius;

    internal bool MatchesTarget(uint targetMapId, Position targetPosition, float arrivalRadius)
        => _targetMapId == targetMapId
            && _targetPosition.DistanceTo2D(targetPosition) <= 0.5f
            && Math.Abs(_targetPosition.Z - targetPosition.Z) <= 0.5f
            && Math.Abs(_arrivalRadius - arrivalRadius) <= 0.1f;

    internal void Retarget(Position targetPosition, float arrivalRadius)
    {
        _targetPosition = targetPosition;
        _arrivalRadius = arrivalRadius > 0f ? arrivalRadius : DefaultArrivalRadius;
        ResetRouteState();
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null)
            return;

        var now = _utcNow();
        var elapsedSec = Math.Clamp((float)(now - _lastUpdateUtc).TotalSeconds, 0.05f, 2.0f);
        _lastUpdateUtc = now;

        if (player.MapId == _targetMapId)
        {
            var dist = player.Position.DistanceTo(_targetPosition);
            var horizontalDist = player.Position.DistanceTo2D(_targetPosition);
            var verticalDelta = Math.Abs(player.Position.Z - _targetPosition.Z);
            // Arrival requires HORIZONTAL proximity AND being on roughly the same
            // vertical layer. A 3D-radius-only check falsely "arrives" below a target
            // that is directly ABOVE the bot — OG zeppelin: at z41 the bot is ~8y
            // horizontal but ~12.5y below Frezza (z53.6), i.e. ~15y 3D <= _arrivalRadius,
            // so the task popped travel_complete 12.5y under the deck and went Idle.
            if (horizontalDist <= _arrivalRadius && verticalDelta <= WalkLegVerticalArrivalTolerance)
            {
                Logger.LogInformation(
                    "[TravelTask] Arrived at destination ({X:F0},{Y:F0},{Z:F0}) distance {Dist:F1}y",
                    _targetPosition.X,
                    _targetPosition.Y,
                    _targetPosition.Z,
                    dist);
                BotContext.AddDiagnosticMessage($"[TRAVEL_COMPLETE] map={_targetMapId} dist={dist:F1}");
                ObjectManager.StopAllMovement();
                PopTask("travel_complete");
                return;
            }
        }

        if (!_initialized)
        {
            EmitImmediateTravelDiagnostic(
                $"[TRAVEL_EXEC] init enter map={player.MapId} pos={FormatCompactPosition(player.Position)} " +
                $"targetMap={_targetMapId} target={FormatCompactPosition(_targetPosition)}");
            PlanRoute();
            EmitImmediateTravelDiagnostic(
                $"[TRAVEL_EXEC] init exit routeLegs={_route?.Count ?? 0} currentLeg={_currentLegIndex}");
            _initialized = true;
        }

        if (_route == null || _currentLegIndex >= _route.Count)
        {
            if (player.MapId == _targetMapId)
            {
                TryNavigateToward(_targetPosition);
            }
            else if (_replanCount < MaxReplans)
            {
                _replanCount++;
                PlanRoute();
            }
            else
            {
                Logger.LogWarning("[TravelTask] Failed to reach target after {Replans} replans. Giving up.", MaxReplans);
                BotContext.AddDiagnosticMessage($"[TRAVEL_FAILED] replans={MaxReplans}");
                ObjectManager.StopAllMovement();
                PopTask("travel_failed");
            }

            return;
        }

        var leg = _route[_currentLegIndex];
        if (_activeLegIndex != _currentLegIndex)
            EnterLeg(_currentLegIndex, leg);

        switch (leg.Type)
        {
            case TransitionType.Walk:
                ExecuteWalkLeg(player, leg, now);
                break;
            case TransitionType.FlightPath:
                ExecuteFlightPathLeg(player, leg, now);
                break;
            case TransitionType.Elevator:
            case TransitionType.Boat:
            case TransitionType.Zeppelin:
                ExecuteTransportLeg(player, leg, elapsedSec, now);
                break;
            case TransitionType.DungeonPortal:
                ExecutePortalLeg(player, leg);
                break;
            default:
                CompleteCurrentLeg($"unsupported_type:{leg.Type}");
                break;
        }
    }

    private void PlanRoute()
    {
        var player = ObjectManager.Player;
        if (player == null)
            return;

        try
        {
            EmitImmediateTravelDiagnostic(
                $"[TRAVEL_EXEC] plan-route enter fromMap={player.MapId} from={FormatCompactPosition(player.Position)} " +
                $"toMap={_targetMapId} to={FormatCompactPosition(_targetPosition)}");
            var router = new CrossMapRouter();
            _route = router.PlanRoute(
                player.MapId,
                player.Position,
                _targetMapId,
                _targetPosition,
                ToFlightPathFaction(_options.PlayerFaction),
                _options.AllowFlightPath ? _options.DiscoveredFlightNodes : null);
            _currentLegIndex = 0;
            _activeLegIndex = -1;

            if (_route.Count == 0)
            {
                Logger.LogWarning(
                    "[TravelTask] No route found from map {FromMap} to map {ToMap}",
                    player.MapId,
                    _targetMapId);
            }
            else
            {
                Logger.LogInformation(
                    "[TravelTask] Planned {LegCount} legs from ({X:F0},{Y:F0}) to ({TX:F0},{TY:F0})",
                    _route.Count,
                    player.Position.X,
                    player.Position.Y,
                    _targetPosition.X,
                    _targetPosition.Y);
                EmitTravelDiagnostic($"[TRAVEL_PLAN] legs={_route.Count} {DescribeRoute(_route)}");
            }

            EmitImmediateTravelDiagnostic(
                $"[TRAVEL_EXEC] plan-route exit legs={_route?.Count ?? 0} currentLeg={_currentLegIndex}");
        }
        catch (Exception ex)
        {
            EmitImmediateTravelDiagnostic(
                $"[TRAVEL_EXEC] plan-route error type={ex.GetType().Name} message={ex.Message}");
            Logger.LogError(ex, "[TravelTask] Route planning failed");
            _route = null;
        }
    }

    private void ExecuteWalkLeg(IWoWLocalPlayer player, RouteLeg leg, DateTime now)
    {
        if (player.MapId != leg.MapId)
        {
            CompleteCurrentLeg("walk_map_changed");
            return;
        }

        if (TryGetWalkLegArrival(
            player.Position,
            leg,
            out var walkDist,
            out var walkVerticalDelta,
            out var walkArrivalRadius,
            out var walkArrivalTarget))
        {
            ObjectManager.StopAllMovement();
            CompleteCurrentLeg(
                $"walk_arrived target={walkArrivalTarget} dist={walkDist:F1} dz={walkVerticalDelta:F1} radius={walkArrivalRadius:F1}");
            return;
        }

        EmitImmediateTravelDiagnostic(
            $"[TRAVEL_EXEC] walk-nav enter leg={_currentLegIndex} map={player.MapId} " +
            $"player={FormatCompactPosition(player.Position)} target={FormatCompactPosition(leg.End)}");
        var navigationStopwatch = Stopwatch.StartNew();
        var navigated = TryNavigateToward(
            leg.End,
            allowDirectFallback: false,
            routePolicy: NavigationRoutePolicy.LongTravel);
        navigationStopwatch.Stop();
        EmitImmediateTravelDiagnostic(
            $"[TRAVEL_EXEC] walk-nav exit leg={_currentLegIndex} nav={navigated} " +
            $"elapsedMs={navigationStopwatch.ElapsedMilliseconds}");
        EmitWalkNavigationTrace(player, leg, navigated, now);
        ObserveWalkLegProgress(player, leg, navigated, now);
    }

    private void ExecuteFlightPathLeg(IWoWLocalPlayer player, RouteLeg leg, DateTime now)
    {
        if (!leg.FlightEndNodeId.HasValue)
        {
            FailOrReplan("flight_missing_destination_node");
            return;
        }

        if (player.MapId != leg.MapId)
        {
            CompleteCurrentLeg("flight_map_changed");
            return;
        }

        if (!_flightActivated)
        {
            if (player.Position.DistanceTo2D(leg.Start) > WalkLegArrivalRadius)
            {
                TryNavigateToward(leg.Start);
                return;
            }

            if (now < _nextFlightActivationUtc)
                return;

            var flightMaster = FindNearestFlightMaster(player.Position);
            if (flightMaster == null || player.Position.DistanceTo2D(flightMaster.Position) > FlightMasterSearchRadius)
            {
                FailOrReplan("flight_master_not_found");
                return;
            }

            _flightActivationAttempts++;
            var activated = ObjectManager
                .ActivateFlightAsync(flightMaster.Guid, leg.FlightEndNodeId.Value, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (!activated)
            {
                if (_flightActivationAttempts >= FlightActivationMaxAttempts)
                {
                    FailOrReplan($"flight_activate_failed node={leg.FlightEndNodeId.Value}");
                    return;
                }

                _nextFlightActivationUtc = now.AddSeconds(FlightActivationRetrySeconds);
                EmitTravelDiagnostic(
                    $"[TRAVEL_FLIGHT_RETRY] attempt={_flightActivationAttempts} dest={leg.FlightEndNodeId.Value}");
                return;
            }

            _flightActivated = true;
            _flightDeparted = false;
            _flightActivationStart = new Position(player.Position.X, player.Position.Y, player.Position.Z);
            _legStartUtc = now;
            ObjectManager.StopAllMovement();
            EmitTravelDiagnostic(
                $"[TRAVEL_FLIGHT] activated {leg.FlightStartNodeId}->{leg.FlightEndNodeId}");
            return;
        }

        var onTransport = IsOnTransport(player);
        if (onTransport
            || (_flightActivationStart != null && player.Position.DistanceTo2D(_flightActivationStart) > 25f))
        {
            _flightDeparted = true;
        }

        var distanceToArrival = player.Position.DistanceTo2D(leg.End);
        var inFlight = ObjectManager.IsInFlight;
        var airborne = IsAirborne(player);
        var readyToLand = _flightDeparted
            && distanceToArrival <= FlightArrivalRadius
            && !onTransport
            && !inFlight
            && !airborne;

        if (readyToLand)
        {
            if (_lastFlightArrivalCandidate != null
                && player.Position.DistanceTo(_lastFlightArrivalCandidate) <= FlightArrivalStableDistance)
            {
                _flightArrivalStableTicks++;
            }
            else
            {
                _flightArrivalStableTicks = 1;
            }

            _lastFlightArrivalCandidate = new Position(player.Position.X, player.Position.Y, player.Position.Z);
            if (_flightArrivalStableTicks >= FlightArrivalStableTicks)
            {
                CompleteCurrentLeg($"flight_arrived dist={distanceToArrival:F1} stable={_flightArrivalStableTicks}");
                return;
            }
        }
        else
        {
            _lastFlightArrivalCandidate = null;
            _flightArrivalStableTicks = 0;
            EmitFlightStateTrace(player, leg, now, distanceToArrival, onTransport, inFlight, airborne);
        }

        if ((now - _legStartUtc).TotalSeconds > FlightTimeoutSeconds)
            FailOrReplan($"flight_timeout dest={leg.FlightEndNodeId.Value}");
    }

    private bool CanCompleteWalkLeg(RouteLeg leg, float verticalDelta)
    {
        if (!WalkLegHandsOffToTransport(leg))
            return verticalDelta <= WalkLegVerticalArrivalTolerance;

        var tolerance = TransportWaitingLogic.IsNativeOffMeshBoardingEnabled()
            ? WalkLegNativeOffMeshTransportVerticalArrivalTolerance
            : WalkLegTransportVerticalArrivalTolerance;
        return verticalDelta <= tolerance;
    }

    private bool TryGetWalkLegArrival(
        Position currentPosition,
        RouteLeg leg,
        out float distance,
        out float verticalDelta,
        out float arrivalRadius,
        out string target)
    {
        distance = currentPosition.DistanceTo2D(leg.End);
        verticalDelta = Math.Abs(currentPosition.Z - leg.End.Z);
        arrivalRadius = GetWalkLegArrivalRadius(leg);
        target = "end";
        if (distance <= arrivalRadius && CanCompleteWalkLeg(leg, verticalDelta))
            return true;

        var boardStop = GetNextTransportBoardStop(leg);
        var boardingPosition = boardStop?.BoardingPosition;
        if (boardingPosition == null)
            return false;

        var boardingDistance = currentPosition.DistanceTo2D(boardingPosition);
        var boardingVerticalDelta = Math.Abs(currentPosition.Z - boardingPosition.Z);
        var boardingArrivalRadius = Math.Min(boardStop!.BoardingRadius, WalkLegTransportArrivalRadius);
        var boardingVerticalTolerance = TransportWaitingLogic.IsNativeOffMeshBoardingEnabled()
            ? WalkLegNativeOffMeshTransportVerticalArrivalTolerance
            : WalkLegTransportVerticalArrivalTolerance;
        if (boardingDistance > boardingArrivalRadius
            || boardingVerticalDelta > boardingVerticalTolerance)
        {
            return false;
        }

        distance = boardingDistance;
        verticalDelta = boardingVerticalDelta;
        arrivalRadius = boardingArrivalRadius;
        target = "transport_boarding";
        return true;
    }

    private float GetWalkLegArrivalRadius(RouteLeg leg)
    {
        if (!WalkLegHandsOffToTransport(leg))
            return WalkLegArrivalRadius;

        // Phase 5.3.5+: when native off-mesh boarding is active, the walk leg
        // is judged against the configured boarding zone rather than a narrow
        // generic transport radius. The bot's natural ramp-top arrival point
        // sits within BoardingRadius but can fall outside the legacy 4y
        // WalkLegTransportArrivalRadius. Use BoardingRadius so the handoff
        // happens when the bot is actually inside the transport's usable front
        // boarding corridor.
        if (TransportWaitingLogic.IsNativeOffMeshBoardingEnabled())
        {
            var nextLeg = (_route != null && _currentLegIndex + 1 < _route.Count)
                ? _route[_currentLegIndex + 1]
                : null;
            var radius = nextLeg?.BoardStop?.BoardingRadius;
            if (radius.HasValue && radius.Value > 0f)
                return radius.Value;
        }

        return WalkLegTransportArrivalRadius;
    }

    private TransportData.TransportStop? GetNextTransportBoardStop(RouteLeg leg)
    {
        if (!WalkLegHandsOffToTransport(leg) || _route == null)
            return null;

        return _route[_currentLegIndex + 1].BoardStop;
    }

    private bool WalkLegHandsOffToTransport(RouteLeg leg)
    {
        if (_route == null || _currentLegIndex + 1 >= _route.Count)
            return false;

        var nextLeg = _route[_currentLegIndex + 1];
        return nextLeg.Start.DistanceTo2D(leg.End) <= WalkLegArrivalRadius
            && IsTransportTransition(nextLeg.Type);
    }

    private static bool IsTransportTransition(TransitionType type)
        => type is TransitionType.Boat
            or TransitionType.Elevator
            or TransitionType.Tram
            or TransitionType.Zeppelin;

    private void ExecuteTransportLeg(IWoWLocalPlayer player, RouteLeg leg, float elapsedSec, DateTime now)
    {
        if (leg.Transport == null || leg.BoardStop == null || leg.ExitStop == null)
        {
            FailOrReplan("transport_missing_definition");
            return;
        }

        if (player.MapId != leg.MapId && !IsCrossMapTransportLeg(leg))
        {
            CompleteCurrentLeg("transport_map_changed");
            return;
        }

        _transportLogic ??= new TransportWaitingLogic(leg.Transport, leg.BoardStop, leg.ExitStop);
        var nearbyObjects = PathfindingOverlayBuilder.BuildNearbyObjects(
            ObjectManager,
            player.Position,
            leg.End,
            maxDistance: TransportNearbyObjectRadius);
        var waypoint = _transportLogic.Update(
            player.Position,
            player.TransportGuid,
            nearbyObjects,
            elapsedSec,
            player.MapId,
            IsOnTransport(player));
        EmitTransportStateTrace(player, leg, nearbyObjects, waypoint, now);

        if (_transportLogic.MissedBoardingAttempt)
        {
            ResetScheduledTransportBoardingCommit();
            ObjectManager.StopAllMovement();
            EmitTravelDiagnostic(
                $"[TRAVEL_TRANSPORT_MISSED_BOARDING] leg={_currentLegIndex} type={leg.Type} " +
                $"player={FormatCompactPosition(player.Position)} board={FormatCompactPosition(leg.BoardStop.WaitPosition)}");
            return;
        }

        if (_transportLogic.CurrentPhase == TransportPhase.Riding && waypoint == null)
        {
            ObjectManager.StopAllMovement();
            return;
        }

        if (_transportLogic.CurrentPhase == TransportPhase.Complete)
        {
            if (HasReachedTransportExit(player, leg, out var exitDistance, out var exitVerticalDelta))
            {
                var reason = leg.Type == TransitionType.Elevator
                    ? "elevator_complete"
                    : $"transport_arrived dist={exitDistance:F1} dz={exitVerticalDelta:F1}";
                CompleteCurrentLeg(reason);
                return;
            }

            FailOrReplan("transport_completed_without_arrival");
            return;
        }

        if (waypoint != null)
        {
            if (ShouldDirectMoveOnScheduledTransport(player, leg))
            {
                ResetTransportBoardingProgressMarkers();
                DirectMoveOnScheduledTransport(player, waypoint);
            }
            else if (ShouldDirectCommitToConfiguredScheduledTransportBoarding(player, leg, waypoint))
            {
                DirectBoardScheduledTransport(player, waypoint);
                ObserveTransportBoardingProgress(player, waypoint, navigated: true, now);
            }
            else if (ShouldNavigateToConfiguredScheduledTransportBoarding(player, leg, waypoint))
            {
                var navigated = TryNavigateToward(
                    waypoint,
                    allowDirectFallback: false,
                    routePolicy: NavigationRoutePolicy.LongTravel);
                ObserveTransportBoardingProgress(player, waypoint, navigated, now);
            }
            else if (ShouldDirectBoardScheduledTransport(leg))
            {
                ResetTransportBoardingProgressMarkers();
                DirectBoardScheduledTransport(player, waypoint);
            }
            else if (player.Position.DistanceTo2D(waypoint) <= 1.0f)
            {
                ResetTransportBoardingProgressMarkers();
                ObjectManager.StopAllMovement();
            }
            else
            {
                ResetTransportBoardingProgressMarkers();
                TryNavigateToward(waypoint, allowDirectFallback: true);
            }
        }
        else
        {
            ResetTransportBoardingProgressMarkers();
        }

        if ((now - _legStartUtc).TotalSeconds > TransportTimeoutSeconds)
            FailOrReplan($"transport_timeout type={leg.Type}");
    }

    private void ExecutePortalLeg(IWoWLocalPlayer player, RouteLeg leg)
    {
        var portalDist = player.Position.DistanceTo(leg.Start);
        if (portalDist > 5f)
        {
            TryNavigateToward(leg.Start, allowDirectFallback: true);
            return;
        }

        if (player.MapId != leg.MapId)
            CompleteCurrentLeg("portal_map_transition");
    }

    private void EnterLeg(int index, RouteLeg leg)
    {
        _activeLegIndex = index;
        _legStartUtc = _utcNow();
        _flightActivated = false;
        _flightDeparted = false;
        _flightActivationAttempts = 0;
        _nextFlightActivationUtc = DateTime.MinValue;
        _flightActivationStart = null;
        _lastFlightArrivalCandidate = null;
        _flightArrivalStableTicks = 0;
        _transportLogic = null;
        ResetScheduledTransportBoardingCommit();
        ClearNavigation();
        ResetWalkTraceMarkers();
        ResetWalkProgressMarkers();
        ResetFlightTraceMarkers();
        ResetTransportTraceMarkers();
        ResetTransportBoardingProgressMarkers();

        EmitTravelDiagnostic(
            $"[TRAVEL_LEG] start index={index} type={leg.Type} map={leg.MapId} end=({leg.End.X:F1},{leg.End.Y:F1},{leg.End.Z:F1})");
    }

    private void CompleteCurrentLeg(string reason)
    {
        EmitTravelDiagnostic($"[TRAVEL_LEG] complete index={_currentLegIndex} reason={reason}");
        _currentLegIndex++;
        _activeLegIndex = -1;
        _transportLogic = null;
        ResetScheduledTransportBoardingCommit();
        _lastFlightArrivalCandidate = null;
        _flightArrivalStableTicks = 0;
        ClearNavigation();
        ResetWalkTraceMarkers();
        ResetWalkProgressMarkers();
        ResetFlightTraceMarkers();
        ResetTransportTraceMarkers();
        ResetTransportBoardingProgressMarkers();
    }

    private void FailOrReplan(string reason)
    {
        EmitTravelDiagnostic($"[TRAVEL_REPLAN] reason={reason} count={_replanCount}");
        if (_replanCount < MaxReplans)
        {
            _replanCount++;
            ResetRouteState();
            return;
        }

        Logger.LogWarning("[TravelTask] Giving up after route failure: {Reason}", reason);
        ObjectManager.StopAllMovement();
        PopTask($"travel_failed:{reason}");
    }

    private void ResetRouteState()
    {
        _route = null;
        _currentLegIndex = 0;
        _activeLegIndex = -1;
        _initialized = false;
        _flightActivated = false;
        _flightDeparted = false;
        _flightActivationAttempts = 0;
        _nextFlightActivationUtc = DateTime.MinValue;
        _flightActivationStart = null;
        _lastFlightArrivalCandidate = null;
        _flightArrivalStableTicks = 0;
        _transportLogic = null;
        ResetScheduledTransportBoardingCommit();
        ClearNavigation();
        ResetWalkTraceMarkers();
        ResetWalkProgressMarkers();
        ResetFlightTraceMarkers();
        ResetTransportTraceMarkers();
        ResetTransportBoardingProgressMarkers();
    }

    private void ObserveWalkLegProgress(IWoWLocalPlayer player, RouteLeg leg, bool navigated, DateTime now)
    {
        if (player.MapId != leg.MapId || IsOnTransport(player))
        {
            ResetWalkProgressMarkers();
            return;
        }

        var currentPosition = player.Position;
        var trace = GetNavigationTraceSnapshot();
        var activeWaypointDistance = GetActiveWaypointDistance(currentPosition, trace);
        var anchor = _walkProgressAnchor;
        if (anchor == null || _walkProgressAnchorMapId != player.MapId)
        {
            RecordWalkProgressAnchor(currentPosition, player.MapId, now, trace, activeWaypointDistance);
            return;
        }

        var moved = currentPosition.DistanceTo2D(anchor);
        var movedEnoughForWaypointProgress = moved > WalkLegStallMovementYards;
        // PFS-OVERHAUL-006 Phase 5.3.7: plan-version churn alone is not progress.
        // NavigationPath's stalled_near_waypoint detector force-replans when the
        // bot stops at a corner; with the authoritative navmesh, replans are
        // idempotent and the bot can churn through 250+ replans at the same
        // physical pos. Earlier this branch reset the stall anchor on every
        // plan-version bump, hiding the underlying stuck state from the 5s
        // recovery + 20s test stuck guard. Require physical movement before
        // a plan-version change counts as forward progress.
        var planVersionChanged = trace != null
            && trace.PlanVersion != _walkProgressAnchorPlanVersion
            && movedEnoughForWaypointProgress;
        var waypointProgressed = movedEnoughForWaypointProgress
            && trace != null
            && trace.CurrentWaypointIndex > _walkProgressAnchorWaypointIndex;
        var activeWaypointProgressed =
            movedEnoughForWaypointProgress
            &&
            float.IsFinite(activeWaypointDistance)
            && float.IsFinite(_walkProgressAnchorActiveWaypointDistance)
            && _walkProgressAnchorActiveWaypointDistance - activeWaypointDistance > WalkLegStallActiveWaypointProgressYards;
        var fallbackPositionProgressed =
            moved > WalkLegStallFallbackMovementYards;

        if (planVersionChanged || waypointProgressed || activeWaypointProgressed || fallbackPositionProgressed)
        {
            RecordWalkProgressAnchor(currentPosition, player.MapId, now, trace, activeWaypointDistance);
            return;
        }

        if (now - _walkProgressAnchorUtc < TimeSpan.FromSeconds(WalkLegStallRecoverySeconds))
            return;

        if (now - _lastWalkStallRecoveryUtc < TimeSpan.FromSeconds(WalkLegStallRecoveryCooldownSeconds))
            return;

        var replanned = NavPath?.RecalculateAfterMovementStall(currentPosition, leg.End, player.MapId) == true;
        _lastWalkStallRecoveryUtc = now;
        _walkStallRecoveryCount++;
        ObjectManager.StopAllMovement();
        var physicsFrozen = ObjectManager.PhysicsFrozenDebugInfo;
        EmitTravelDiagnostic(
            $"[TRAVEL_WALK_STALL] leg={_currentLegIndex} count={_walkStallRecoveryCount} nav={navigated} replanned={replanned} " +
            $"anchor={FormatCompactPosition(anchor)} current={FormatCompactPosition(currentPosition)} moved={moved:F1} " +
            $"plan={trace?.PlanVersion ?? -1} idx={trace?.CurrentWaypointIndex ?? -1} " +
            $"activeDist={activeWaypointDistance:F1}->{_walkProgressAnchorActiveWaypointDistance:F1} " +
            $"target={FormatCompactPosition(leg.End)}"
            + (physicsFrozen != null ? $" physics[{physicsFrozen}]" : ""));

        var updatedTrace = GetNavigationTraceSnapshot();
        RecordWalkProgressAnchor(
            currentPosition,
            player.MapId,
            now,
            updatedTrace,
            GetActiveWaypointDistance(currentPosition, updatedTrace));
    }

    private void RecordWalkProgressAnchor(
        Position position,
        uint mapId,
        DateTime now,
        NavigationTraceSnapshot? trace,
        float activeWaypointDistance)
    {
        _walkProgressAnchor = new Position(position.X, position.Y, position.Z);
        _walkProgressAnchorMapId = mapId;
        _walkProgressAnchorUtc = now;
        _walkProgressAnchorPlanVersion = trace?.PlanVersion ?? -1;
        _walkProgressAnchorWaypointIndex = trace?.CurrentWaypointIndex ?? -1;
        _walkProgressAnchorActiveWaypointDistance = activeWaypointDistance;
    }

    private void ObserveTransportBoardingProgress(
        IWoWLocalPlayer player,
        Position boardingTarget,
        bool navigated,
        DateTime now)
    {
        if (IsOnTransport(player))
        {
            ResetTransportBoardingProgressMarkers();
            return;
        }

        var currentPosition = player.Position;
        var anchor = _transportBoardingProgressAnchor;
        if (anchor == null || _transportBoardingProgressAnchorMapId != player.MapId)
        {
            RecordTransportBoardingProgressAnchor(currentPosition, player.MapId, now);
            return;
        }

        var moved = currentPosition.DistanceTo2D(anchor);
        var targetProgress = anchor.DistanceTo2D(boardingTarget) - currentPosition.DistanceTo2D(boardingTarget);
        if (targetProgress > WalkLegStallActiveWaypointProgressYards
            || moved > WalkLegStallFallbackMovementYards)
        {
            RecordTransportBoardingProgressAnchor(currentPosition, player.MapId, now);
            return;
        }

        if (now - _transportBoardingProgressAnchorUtc < TimeSpan.FromSeconds(WalkLegStallRecoverySeconds))
            return;

        if (now - _lastTransportBoardingStallRecoveryUtc < TimeSpan.FromSeconds(WalkLegStallRecoveryCooldownSeconds))
            return;

        var replanned = NavPath?.RecalculateAfterMovementStall(currentPosition, boardingTarget, player.MapId) == true;
        _lastTransportBoardingStallRecoveryUtc = now;
        _transportBoardingStallRecoveryCount++;
        ObjectManager.StopAllMovement();
        EmitTravelDiagnostic(
            $"[TRAVEL_TRANSPORT_BOARDING_STALL] count={_transportBoardingStallRecoveryCount} nav={navigated} replanned={replanned} " +
            $"anchor={FormatCompactPosition(anchor)} current={FormatCompactPosition(currentPosition)} moved={moved:F1} " +
            $"targetProgress={targetProgress:F1} target={FormatCompactPosition(boardingTarget)}");

        RecordTransportBoardingProgressAnchor(currentPosition, player.MapId, now);
    }

    private void RecordTransportBoardingProgressAnchor(Position position, uint mapId, DateTime now)
    {
        _transportBoardingProgressAnchor = new Position(position.X, position.Y, position.Z);
        _transportBoardingProgressAnchorMapId = mapId;
        _transportBoardingProgressAnchorUtc = now;
    }

    private static float GetActiveWaypointDistance(Position currentPosition, NavigationTraceSnapshot? trace)
    {
        if (trace?.ActiveWaypoint == null)
            return float.NaN;

        return currentPosition.DistanceTo2D(trace.ActiveWaypoint);
    }

    private void EmitFlightStateTrace(
        IWoWLocalPlayer player,
        RouteLeg leg,
        DateTime now,
        float distanceToArrival,
        bool onTransport,
        bool inFlight,
        bool airborne)
    {
        if (!_flightActivated)
            return;

        var signature =
            $"departed={_flightDeparted};transport={onTransport};inFlight={inFlight};mounted={player.IsMounted};airborne={airborne}";
        if (string.Equals(_lastFlightTraceSignature, signature, StringComparison.Ordinal)
            && now - _lastFlightTraceEmitUtc < TimeSpan.FromSeconds(10))
            return;

        _lastFlightTraceSignature = signature;
        _lastFlightTraceEmitUtc = now;
        EmitTravelDiagnostic(
            $"[TRAVEL_FLIGHT_WAIT] leg={_currentLegIndex} {signature} dist={distanceToArrival:F1} " +
            $"player={FormatCompactPosition(player.Position)} target={FormatCompactPosition(leg.End)}");
    }

    private void EmitTransportStateTrace(
        IWoWLocalPlayer player,
        RouteLeg leg,
        IReadOnlyList<DynamicObjectProto> nearbyObjects,
        Position? waypoint,
        DateTime now)
    {
        if (_transportLogic == null)
            return;

        var transport = leg.Transport;
        var transportDisplayId = transport?.DisplayId ?? 0u;
        var transportIdentity = transport == null
            ? "none"
            : $"{transport.GameObjectEntry}:{transport.DisplayId}:{transport.Name}";
        DynamicObjectProto[] displayObjects = transportDisplayId == 0
            ? Array.Empty<DynamicObjectProto>()
            : nearbyObjects
                .Where(obj => obj.DisplayId == transportDisplayId)
                .OrderBy(obj => Distance2D(player.Position, obj))
                .ToArray();
        DynamicObjectProto[] matchingObjects = transport == null
            ? Array.Empty<DynamicObjectProto>()
            : displayObjects
                .Where(obj => TransportObjectIdentity.MatchesTransport(obj, transport))
                .ToArray();
        var nearestObject = matchingObjects.FirstOrDefault();
        var nearestDisplayObject = displayObjects.FirstOrDefault();
        var nearestText = TransportObjectIdentity.Format(nearestObject);
        var nearestDisplayText = TransportObjectIdentity.Format(nearestDisplayObject);
        var transportOffset = IsOnTransport(player) ? player.Position : null;
        var configuredBoardOffset = leg.BoardStop?.TransportBoardingOffset;
        var signature =
            $"phase={_transportLogic.CurrentPhase};transport=0x{player.TransportGuid:X};" +
            $"map={player.MapId};expected={transportIdentity};wp={FormatCompactPosition(waypoint)};" +
            $"offset={FormatCompactPosition(transportOffset)};" +
            $"boardOffset={FormatCompactPosition(configuredBoardOffset)};" +
            $"near={matchingObjects.Length};displayNear={displayObjects.Length};" +
            $"nearest={nearestText};nearestDisplay={nearestDisplayText}";

        if (string.Equals(_lastTransportTraceSignature, signature, StringComparison.Ordinal)
            && now - _lastTransportTraceEmitUtc < TimeSpan.FromSeconds(10))
            return;

        _lastTransportTraceSignature = signature;
        _lastTransportTraceEmitUtc = now;
        EmitTravelDiagnostic(
            $"[TRAVEL_TRANSPORT] leg={_currentLegIndex} {signature} player={FormatCompactPosition(player.Position)} " +
            $"board={FormatCompactPosition(leg.BoardStop?.WaitPosition)} exit={FormatCompactPosition(leg.ExitStop?.WaitPosition)}");
    }

    private void EmitWalkNavigationTrace(IWoWLocalPlayer player, RouteLeg leg, bool navigated, DateTime now)
    {
        var trace = GetNavigationTraceSnapshot();
        if (trace == null)
            return;

        var stuckGeneration = ObjectManager.MovementStuckRecoveryGeneration;
        var resolution = trace.LastResolution ?? "none";
        var waypointEmitThreshold = trace.PlannedWaypoints.Length >= 100 ? 5 : 20;
        var shouldEmit =
            _lastWalkTraceLegIndex != _currentLegIndex
            || _lastWalkTracePlanVersion != trace.PlanVersion
            || Math.Abs(_lastWalkTraceWaypointIndex - trace.CurrentWaypointIndex) >= waypointEmitThreshold
            || _lastWalkTraceStuckGeneration != stuckGeneration
            || !string.Equals(_lastWalkTraceResolution, resolution, StringComparison.Ordinal)
            || now - _lastWalkTraceEmitUtc >= TimeSpan.FromSeconds(10);

        if (!shouldEmit)
            return;

        _lastWalkTraceLegIndex = _currentLegIndex;
        _lastWalkTracePlanVersion = trace.PlanVersion;
        _lastWalkTraceWaypointIndex = trace.CurrentWaypointIndex;
        _lastWalkTraceStuckGeneration = stuckGeneration;
        _lastWalkTraceResolution = resolution;
        _lastWalkTraceEmitUtc = now;

        var decision = trace.RouteDecision;
        EmitTravelDiagnostic(
            $"[TRAVEL_WALK_NAV] leg={_currentLegIndex} nav={navigated} stuck={stuckGeneration} " +
            $"plan={trace.PlanVersion} smooth={trace.SmoothPath} reason={trace.LastReplanReason ?? "none"} " +
            $"resolution={resolution} idx={trace.CurrentWaypointIndex} afford={decision.MaxAffordance} " +
            $"agent={trace.Race}/{trace.Gender} capsule=({trace.CapsuleRadius:F3},{trace.CapsuleHeight:F3}) " +
            $"alt={decision.AlternateSelected}/{decision.AlternateEvaluated} overlay={trace.UsedNearbyObjectOverlay}:{trace.NearbyObjectCount} " +
            $"player={FormatCompactPosition(player.Position)} target={FormatCompactPosition(leg.End)} " +
            $"active={FormatCompactPosition(trace.ActiveWaypoint)} " +
            $"window={FormatCompactPathWindow(trace.PlannedWaypoints, trace.CurrentWaypointIndex, 3, 7)} " +
            $"samples={FormatCompactExecutionSamples(trace.ExecutionSamples, 3)}");
    }

    private void EmitTravelDiagnostic(string message)
    {
        BotContext.AddDiagnosticMessage(message);
        BotRunnerService.DiagLog(message);
    }

    private void EmitImmediateTravelDiagnostic(string message)
    {
        BotContext.AddImmediateDiagnostic(message);
    }

    private void ResetWalkTraceMarkers()
    {
        _lastWalkTraceLegIndex = -1;
        _lastWalkTracePlanVersion = -1;
        _lastWalkTraceWaypointIndex = -1;
        _lastWalkTraceStuckGeneration = -1;
        _lastWalkTraceResolution = null;
        _lastWalkTraceEmitUtc = DateTime.MinValue;
    }

    private void ResetWalkProgressMarkers()
    {
        _walkProgressAnchor = null;
        _walkProgressAnchorMapId = 0;
        _walkProgressAnchorUtc = DateTime.MinValue;
        _walkProgressAnchorPlanVersion = -1;
        _walkProgressAnchorWaypointIndex = -1;
        _walkProgressAnchorActiveWaypointDistance = float.NaN;
        _lastWalkStallRecoveryUtc = DateTime.MinValue;
        _walkStallRecoveryCount = 0;
    }

    private void ResetFlightTraceMarkers()
    {
        _lastFlightTraceSignature = null;
        _lastFlightTraceEmitUtc = DateTime.MinValue;
    }

    private void ResetTransportTraceMarkers()
    {
        _lastTransportTraceSignature = null;
        _lastTransportTraceEmitUtc = DateTime.MinValue;
    }

    private void ResetTransportBoardingProgressMarkers()
    {
        _transportBoardingProgressAnchor = null;
        _transportBoardingProgressAnchorMapId = 0;
        _transportBoardingProgressAnchorUtc = DateTime.MinValue;
        _lastTransportBoardingStallRecoveryUtc = DateTime.MinValue;
        _transportBoardingStallRecoveryCount = 0;
    }

    private static float Distance2D(Position position, DynamicObjectProto obj)
    {
        var dx = position.X - obj.X;
        var dy = position.Y - obj.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static string FormatCompactPosition(Position? position)
        => position == null
            ? "none"
            : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";

    private static string FormatCompactPath(Position[] path, int limit)
    {
        if (path.Length == 0)
            return "[]";

        var count = Math.Min(path.Length, limit);
        var parts = new string[count + (path.Length > limit ? 1 : 0)];
        for (var i = 0; i < count; i++)
            parts[i] = FormatCompactPosition(path[i]);

        if (path.Length > limit)
            parts[^1] = $"+{path.Length - limit}";

        return $"[{string.Join(" ", parts)}]";
    }

    private static string FormatCompactPathWindow(Position[] path, int activeIndex, int before, int after)
    {
        if (path.Length == 0)
            return "[]";

        var center = Math.Clamp(activeIndex, 0, path.Length - 1);
        var start = Math.Max(0, center - Math.Max(0, before));
        var end = Math.Min(path.Length - 1, center + Math.Max(0, after));
        var parts = new List<string>((end - start) + 1 + (start > 0 ? 1 : 0) + (end < path.Length - 1 ? 1 : 0));

        if (start > 0)
            parts.Add($"+{start}");

        for (var i = start; i <= end; i++)
        {
            var marker = i == center ? "*" : "";
            parts.Add($"{marker}{i}:{FormatCompactPosition(path[i])}");
        }

        if (end < path.Length - 1)
            parts.Add($"+{path.Length - end - 1}");

        return $"[{string.Join(" ", parts)}]";
    }

    private static string FormatCompactExecutionSamples(NavigationExecutionSample[] samples, int limit)
    {
        if (samples.Length == 0)
            return "[]";

        var start = Math.Max(0, samples.Length - Math.Max(1, limit));
        var parts = new List<string>(samples.Length - start + (start > 0 ? 1 : 0));
        if (start > 0)
            parts.Add($"+{start}");

        for (var i = start; i < samples.Length; i++)
        {
            var sample = samples[i];
            var fallback = sample.UsedDirectFallback ? ":fb" : "";
            parts.Add(
                $"p{sample.PlanVersion}:i{sample.WaypointIndex}:{sample.Resolution}{fallback}:d{sample.DistanceToWaypoint:F1}->{FormatCompactPosition(sample.ReturnedWaypoint)}");
        }

        return $"[{string.Join(" ", parts)}]";
    }

    private IWoWUnit? FindNearestFlightMaster(Position playerPosition)
        => ObjectManager.Units
            .Where(unit => ((uint)unit.NpcFlags & NpcFlagFlightMaster) != 0 && unit.Health > 0)
            .OrderBy(unit => unit.Position.DistanceTo2D(playerPosition))
            .FirstOrDefault();

    private static bool IsOnTransport(IWoWLocalPlayer player)
        => player.TransportGuid != 0
            || (player.MovementFlags & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;

    private static bool IsCrossMapTransportLeg(RouteLeg leg)
        => leg.Transport != null
            && leg.BoardStop != null
            && leg.ExitStop != null
            && leg.Transport.Type != TransportData.TransportType.Elevator
            && leg.BoardStop.MapId != leg.ExitStop.MapId;

    private bool ShouldDirectBoardScheduledTransport(RouteLeg leg)
        => _transportLogic?.CurrentPhase == TransportPhase.Boarding
            && leg.Transport?.Type != TransportData.TransportType.Elevator
            && !TransportWaitingLogic.IsNativeOffMeshBoardingEnabled();

    private static bool ShouldDirectMoveOnScheduledTransport(IWoWLocalPlayer player, RouteLeg leg)
        => IsOnTransport(player)
            && leg.Transport?.Type != TransportData.TransportType.Elevator;

    private static bool ShouldNavigateToConfiguredScheduledTransportBoarding(
        IWoWLocalPlayer player,
        RouteLeg leg,
        Position waypoint)
    {
        if (IsOnTransport(player) || leg.Transport?.Type == TransportData.TransportType.Elevator)
            return false;

        // Phase 5.3.5: previously this predicate was suppressed when native
        // off-mesh boarding was enabled to avoid the legacy "BoardingPosition
        // nudge" short-circuit. But the natural cascade fallback uses the
        // Standard route policy, which enables dynamic probe-skipping and
        // corner-cutting in NavigationPath.AdvanceReachableWaypoints — and
        // that lets the bot skip ahead off the OG zeppelin tower's wooden
        // ramp before fully cresting onto the upper-platform deck (live test
        // showed snag at z=51.6, 2y short of the upper boarding deck).
        // predicate routes the boarding nav through LongTravel policy
        // (EnableDynamicProbeSkipping=false, RequireVerticalWaypointArrival=
        // true, TightenDenseWaypointAcceptance=true) so the bot follows
        // every corner the navmesh returns and won't shortcut over the
        // platform-edge geometry.

        var boardingPosition = leg.BoardStop?.BoardingPosition;
        if (boardingPosition == null)
            return false;

        if (IsDifferentScheduledTransportBoardingTarget(boardingPosition, waypoint))
            return false;

        return IsDifferentScheduledTransportBoardingTarget(player.Position, boardingPosition);
    }

    private static bool ShouldDirectCommitToConfiguredScheduledTransportBoarding(
        IWoWLocalPlayer player,
        RouteLeg leg,
        Position waypoint)
    {
        if (IsOnTransport(player) || leg.Transport?.Type == TransportData.TransportType.Elevator)
            return false;

        if (TransportWaitingLogic.IsNativeOffMeshBoardingEnabled())
            return false;

        var boardingPosition = leg.BoardStop?.BoardingPosition;
        if (boardingPosition == null)
            return false;

        if (IsDifferentScheduledTransportBoardingTarget(boardingPosition, waypoint))
            return false;

        return player.Position.DistanceTo2D(boardingPosition) <= WalkLegTransportArrivalRadius
            && IsDifferentScheduledTransportBoardingTarget(player.Position, boardingPosition);
    }

    private void DirectMoveOnScheduledTransport(IWoWLocalPlayer player, Position waypoint)
    {
        if (player.Position.DistanceTo2D(waypoint) <= 1.0f)
        {
            ObjectManager.StopAllMovement();
            return;
        }

        ObjectManager.MoveToward(waypoint, player.GetFacingForPosition(waypoint));
    }

    private void DirectBoardScheduledTransport(IWoWLocalPlayer player, Position waypoint)
    {
        if (_scheduledTransportBoardingTarget == null
            || !_scheduledTransportBoardingFacing.HasValue
            || IsDifferentScheduledTransportBoardingTarget(_scheduledTransportBoardingTarget, waypoint))
        {
            _scheduledTransportBoardingTarget = waypoint;
            _scheduledTransportBoardingFacing = player.GetFacingForPosition(waypoint);
        }

        if (player.Position.DistanceTo2D(_scheduledTransportBoardingTarget) <= 0.75f)
        {
            ObjectManager.StopAllMovement();
            return;
        }

        ObjectManager.MoveToward(_scheduledTransportBoardingTarget, _scheduledTransportBoardingFacing.Value);
    }

    private static bool IsDifferentScheduledTransportBoardingTarget(Position? current, Position next)
        => current == null
            || current.DistanceTo2D(next) > 0.5f
            || Math.Abs(current.Z - next.Z) > 0.5f;

    private void ResetScheduledTransportBoardingCommit()
    {
        _scheduledTransportBoardingTarget = null;
        _scheduledTransportBoardingFacing = null;
    }

    private static bool HasReachedTransportExit(
        IWoWLocalPlayer player,
        RouteLeg leg,
        out float distance,
        out float verticalDelta)
    {
        distance = float.PositiveInfinity;
        verticalDelta = float.PositiveInfinity;

        if (leg.ExitStop == null || player.MapId != leg.ExitStop.MapId)
            return false;

        distance = player.Position.DistanceTo2D(leg.ExitStop.WaitPosition);
        verticalDelta = Math.Abs(player.Position.Z - leg.ExitStop.WaitPosition.Z);

        if (leg.Type == TransitionType.Elevator)
            return distance <= WalkLegArrivalRadius;

        return distance <= Math.Max(leg.ExitStop.BoardingRadius, WalkLegArrivalRadius)
            && verticalDelta <= TransportExitVerticalArrivalTolerance;
    }

    private static bool IsAirborne(IWoWLocalPlayer player)
        => (player.MovementFlags & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;

    private static FlightPathData.Faction ToFlightPathFaction(TravelFaction faction)
        => faction switch
        {
            TravelFaction.Alliance => FlightPathData.Faction.Alliance,
            _ => FlightPathData.Faction.Horde,
        };

    private static string DescribeRoute(IReadOnlyList<RouteLeg> route)
        => string.Join(" -> ", route.Select(DescribeLeg));

    private static string DescribeLeg(RouteLeg leg)
        => leg.Type switch
        {
            TransitionType.FlightPath => $"FlightPath({leg.FlightStartNodeId}->{leg.FlightEndNodeId})",
            TransitionType.Zeppelin or TransitionType.Boat or TransitionType.Elevator =>
                $"{leg.Type}({leg.Transport?.Name ?? "unknown"})",
            _ => leg.Type.ToString(),
        };
}
