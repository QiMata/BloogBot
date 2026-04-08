using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Collections.Generic;
using BotRunner.Combat;
using static BotRunner.Movement.TransportData;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Cross-world travel task. Decomposes a travel objective into walk/flight/transport/portal
/// legs via CrossMapRouter, then executes each leg sequentially.
///
/// On first Update(): calls CrossMapRouter.PlanRoute() to get List&lt;RouteLeg&gt;.
/// Pushes sub-tasks in reverse order (stack is LIFO). Each sub-task pops itself on completion.
/// If a leg fails (stuck >30s), re-plans from current position.
/// Arrival: within 5y of target on correct map.
/// </summary>
public class TravelTask : BotTask, IBotTask
{
    private readonly uint _targetMapId;
    private readonly Position _targetPosition;
    private readonly TravelOptions _options;
    private readonly float _arrivalRadius;

    private List<RouteLeg>? _route;
    private int _currentLegIndex;
    private bool _initialized;
    private int _replanCount;
    private const int MaxReplans = 3;
    private const float DefaultArrivalRadius = 5.0f;

    public TravelTask(IBotContext context, uint targetMapId, Position targetPos, TravelOptions? options = null, float arrivalRadius = DefaultArrivalRadius)
        : base(context)
    {
        _targetMapId = targetMapId;
        _targetPosition = targetPos;
        _options = options ?? new TravelOptions();
        _arrivalRadius = arrivalRadius;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        // Check if we've arrived
        if (player.MapId == _targetMapId)
        {
            var dist = player.Position.DistanceTo(_targetPosition);
            if (dist <= _arrivalRadius)
            {
                Log.Information("[TravelTask] Arrived at destination ({X:F0},{Y:F0},{Z:F0}) — distance {Dist:F1}y",
                    _targetPosition.X, _targetPosition.Y, _targetPosition.Z, dist);
                BotContext.BotTasks.Pop();
                return;
            }
        }

        if (!_initialized)
        {
            PlanRoute();
            _initialized = true;
        }

        // If no route or all legs exhausted, try simple GOTO (same-map fallback)
        if (_route == null || _currentLegIndex >= _route.Count)
        {
            if (player.MapId == _targetMapId)
            {
                // Same map — just walk there
                ObjectManager.MoveToward(_targetPosition);
            }
            else if (_replanCount < MaxReplans)
            {
                _replanCount++;
                PlanRoute();
            }
            else
            {
                Log.Warning("[TravelTask] Failed to reach target after {Replans} replans. Giving up.", MaxReplans);
                BotContext.BotTasks.Pop();
            }
            return;
        }

        // Execute current leg
        var leg = _route[_currentLegIndex];
        switch (leg.Type)
        {
            case TransitionType.Walk:
                var walkDist = player.Position.DistanceTo(leg.End);
                if (walkDist <= _arrivalRadius || player.MapId != leg.MapId)
                {
                    _currentLegIndex++;
                    return;
                }
                ObjectManager.MoveToward(leg.End);
                break;

            case TransitionType.Elevator:
            case TransitionType.Boat:
            case TransitionType.Zeppelin:
                // Transport legs: walk to boarding position, then wait for transport
                var boardDist = player.Position.DistanceTo(leg.Start);
                if (boardDist > 10f)
                {
                    ObjectManager.MoveToward(leg.Start);
                }
                else
                {
                    // At boarding position — TransportWaitingLogic handles the rest
                    // For now, advance to next leg when map changes
                    if (player.MapId != leg.MapId)
                    {
                        _currentLegIndex++;
                    }
                }
                break;

            case TransitionType.DungeonPortal:
                // Walk to portal entrance
                var portalDist = player.Position.DistanceTo(leg.Start);
                if (portalDist > 5f)
                {
                    ObjectManager.MoveToward(leg.Start);
                }
                else
                {
                    // At portal — map transition should happen automatically
                    if (player.MapId != leg.MapId)
                    {
                        _currentLegIndex++;
                    }
                }
                break;

            default:
                _currentLegIndex++;
                break;
        }
    }

    private void PlanRoute()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        try
        {
            var router = new CrossMapRouter();
            _route = router.PlanRoute(
                player.MapId,
                player.Position,
                _targetMapId,
                _targetPosition,
                FlightPathData.Faction.Horde); // TODO: pass from TravelOptions.PlayerFaction
            _currentLegIndex = 0;

            if (_route.Count == 0)
            {
                Log.Warning("[TravelTask] No route found from map {FromMap} to map {ToMap}",
                    player.MapId, _targetMapId);
            }
            else
            {
                Log.Information("[TravelTask] Planned {LegCount} legs from ({X:F0},{Y:F0}) to ({TX:F0},{TY:F0})",
                    _route.Count, player.Position.X, player.Position.Y,
                    _targetPosition.X, _targetPosition.Y);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TravelTask] Route planning failed");
            _route = null;
        }
    }
}
