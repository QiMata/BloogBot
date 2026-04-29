using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
    private const float FlightMasterSearchRadius = 60.0f;
    private const float FlightArrivalRadius = 200.0f;
    private const int FlightActivationMaxAttempts = 5;
    private const double FlightActivationRetrySeconds = 3.0;
    private const double FlightTimeoutSeconds = 360.0;
    private const double TransportTimeoutSeconds = 300.0;
    private const uint NpcFlagFlightMaster = 0x2000;

    private readonly uint _targetMapId;
    private readonly TravelOptions _options;
    private Position _targetPosition;
    private float _arrivalRadius;

    private List<RouteLeg>? _route;
    private int _currentLegIndex;
    private bool _initialized;
    private int _replanCount;
    private int _activeLegIndex = -1;
    private DateTime _lastUpdateUtc = DateTime.UtcNow;
    private DateTime _legStartUtc = DateTime.UtcNow;

    private bool _flightActivated;
    private bool _flightDeparted;
    private int _flightActivationAttempts;
    private DateTime _nextFlightActivationUtc = DateTime.MinValue;
    private Position? _flightActivationStart;

    private TransportWaitingLogic? _transportLogic;

    public TravelTask(
        IBotContext context,
        uint targetMapId,
        Position targetPos,
        TravelOptions? options = null,
        float arrivalRadius = DefaultArrivalRadius)
        : base(context)
    {
        _targetMapId = targetMapId;
        _targetPosition = targetPos;
        _options = options ?? new TravelOptions();
        _arrivalRadius = arrivalRadius > 0f ? arrivalRadius : DefaultArrivalRadius;
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

        var now = DateTime.UtcNow;
        var elapsedSec = Math.Clamp((float)(now - _lastUpdateUtc).TotalSeconds, 0.05f, 2.0f);
        _lastUpdateUtc = now;

        if (player.MapId == _targetMapId)
        {
            var dist = player.Position.DistanceTo(_targetPosition);
            if (dist <= _arrivalRadius)
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
            PlanRoute();
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
                ExecuteWalkLeg(player, leg);
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
                BotContext.AddDiagnosticMessage($"[TRAVEL_PLAN] legs={_route.Count} {DescribeRoute(_route)}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[TravelTask] Route planning failed");
            _route = null;
        }
    }

    private void ExecuteWalkLeg(IWoWLocalPlayer player, RouteLeg leg)
    {
        if (player.MapId != leg.MapId)
        {
            CompleteCurrentLeg("walk_map_changed");
            return;
        }

        var walkDist = player.Position.DistanceTo2D(leg.End);
        if (walkDist <= WalkLegArrivalRadius)
        {
            ObjectManager.StopAllMovement();
            CompleteCurrentLeg($"walk_arrived dist={walkDist:F1}");
            return;
        }

        TryNavigateToward(leg.End);
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
                BotContext.AddDiagnosticMessage(
                    $"[TRAVEL_FLIGHT_RETRY] attempt={_flightActivationAttempts} dest={leg.FlightEndNodeId.Value}");
                return;
            }

            _flightActivated = true;
            _flightDeparted = false;
            _flightActivationStart = new Position(player.Position.X, player.Position.Y, player.Position.Z);
            _legStartUtc = now;
            ObjectManager.StopAllMovement();
            BotContext.AddDiagnosticMessage(
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
        if (_flightDeparted && distanceToArrival <= FlightArrivalRadius && !onTransport)
        {
            CompleteCurrentLeg($"flight_arrived dist={distanceToArrival:F1}");
            return;
        }

        if ((now - _legStartUtc).TotalSeconds > FlightTimeoutSeconds)
            FailOrReplan($"flight_timeout dest={leg.FlightEndNodeId.Value}");
    }

    private void ExecuteTransportLeg(IWoWLocalPlayer player, RouteLeg leg, float elapsedSec, DateTime now)
    {
        if (player.MapId != leg.MapId)
        {
            CompleteCurrentLeg("transport_map_changed");
            return;
        }

        if (leg.Transport == null || leg.BoardStop == null || leg.ExitStop == null)
        {
            FailOrReplan("transport_missing_definition");
            return;
        }

        _transportLogic ??= new TransportWaitingLogic(leg.Transport, leg.BoardStop, leg.ExitStop);
        var nearbyObjects = PathfindingOverlayBuilder.BuildNearbyObjects(ObjectManager, player.Position, leg.End);
        var waypoint = _transportLogic.Update(player.Position, player.TransportGuid, nearbyObjects, elapsedSec);

        if (_transportLogic.CurrentPhase == TransportPhase.Riding && waypoint == null)
        {
            ObjectManager.StopAllMovement();
            return;
        }

        if (_transportLogic.CurrentPhase == TransportPhase.Complete)
        {
            if (leg.Type == TransitionType.Elevator && player.Position.DistanceTo2D(leg.End) <= WalkLegArrivalRadius)
            {
                CompleteCurrentLeg("elevator_complete");
                return;
            }

            FailOrReplan("transport_completed_without_arrival");
            return;
        }

        if (waypoint != null)
        {
            if (player.Position.DistanceTo2D(waypoint) <= 1.0f)
                ObjectManager.StopAllMovement();
            else
                TryNavigateToward(waypoint, allowDirectFallback: true);
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
        _legStartUtc = DateTime.UtcNow;
        _flightActivated = false;
        _flightDeparted = false;
        _flightActivationAttempts = 0;
        _nextFlightActivationUtc = DateTime.MinValue;
        _flightActivationStart = null;
        _transportLogic = null;
        ClearNavigation();

        BotContext.AddDiagnosticMessage(
            $"[TRAVEL_LEG] start index={index} type={leg.Type} map={leg.MapId} end=({leg.End.X:F1},{leg.End.Y:F1},{leg.End.Z:F1})");
    }

    private void CompleteCurrentLeg(string reason)
    {
        BotContext.AddDiagnosticMessage($"[TRAVEL_LEG] complete index={_currentLegIndex} reason={reason}");
        _currentLegIndex++;
        _activeLegIndex = -1;
        _transportLogic = null;
        ClearNavigation();
    }

    private void FailOrReplan(string reason)
    {
        BotContext.AddDiagnosticMessage($"[TRAVEL_REPLAN] reason={reason} count={_replanCount}");
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
        _transportLogic = null;
        ClearNavigation();
    }

    private IWoWUnit? FindNearestFlightMaster(Position playerPosition)
        => ObjectManager.Units
            .Where(unit => ((uint)unit.NpcFlags & NpcFlagFlightMaster) != 0 && unit.Health > 0)
            .OrderBy(unit => unit.Position.DistanceTo2D(playerPosition))
            .FirstOrDefault();

    private static bool IsOnTransport(IWoWLocalPlayer player)
        => player.TransportGuid != 0
            || (player.MovementFlags & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;

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
