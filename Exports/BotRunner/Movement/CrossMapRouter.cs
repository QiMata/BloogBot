using BotRunner.Clients;
using BotRunner.Combat;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static BotRunner.Movement.TransportData;

namespace BotRunner.Movement;

/// <summary>
/// A single leg of a cross-map route. Each leg is either a walk, transport ride,
/// flight path, or dungeon portal crossing.
/// </summary>
public record RouteLeg(
    TransitionType Type,
    uint MapId,
    Position Start,
    Position End,
    TransportDefinition? Transport,
    TransportStop? BoardStop,
    TransportStop? ExitStop,
    uint? FlightStartNodeId,
    uint? FlightEndNodeId,
    float EstimatedTimeSec);

/// <summary>
/// High-level route planner: given (startMap, startPos) → (destMap, destPos),
/// produces a sequence of <see cref="RouteLeg"/>s.
/// Uses <see cref="MapTransitionGraph"/> for cross-map transitions and
/// <see cref="TransportData"/> for elevator/transport detection.
/// </summary>
public class CrossMapRouter
{
    private const float WALK_SPEED = 7.0f; // Run speed for time estimates

    /// <summary>
    /// Plan a route from start to destination, potentially crossing maps.
    /// Returns an ordered list of legs to execute.
    /// </summary>
    public List<RouteLeg> PlanRoute(
        uint startMapId, Position startPos,
        uint destMapId, Position destPos,
        FlightPathData.Faction faction,
        IReadOnlyCollection<uint>? discoveredFlightNodes = null)
    {
        // Same map — check for elevator crossing or simple walk
        if (startMapId == destMapId)
            return PlanSameMapRoute(startMapId, startPos, destPos);

        // Cross-map — find transition chain
        return PlanCrossMapRoute(startMapId, startPos, destMapId, destPos, faction, discoveredFlightNodes);
    }

    // =========================================================================
    // SAME-MAP ROUTING
    // =========================================================================

    private List<RouteLeg> PlanSameMapRoute(uint mapId, Position start, Position end)
    {
        // Check if an elevator crossing is needed
        var elevator = TransportData.DetectElevatorCrossing(mapId, start, end);
        if (elevator != null)
            return PlanElevatorRoute(mapId, start, end, elevator);

        // Simple walk
        float dist = Distance3D(start, end);
        return
        [
            new RouteLeg(TransitionType.Walk, mapId, start, end,
                Transport: null, BoardStop: null, ExitStop: null,
                FlightStartNodeId: null, FlightEndNodeId: null,
                EstimatedTimeSec: dist / WALK_SPEED)
        ];
    }

    private static List<RouteLeg> PlanElevatorRoute(
        uint mapId, Position start, Position end, TransportDefinition elevator)
    {
        var legs = new List<RouteLeg>();

        // Determine which stop we start near and which we end near
        var boardStop = FindNearestStop(elevator, start);
        var exitStop = GetDestinationStop(elevator, start);

        if (boardStop == null || exitStop == null)
        {
            // Fallback: simple walk (elevator stops not resolved)
            float dist = Distance3D(start, end);
            legs.Add(new RouteLeg(TransitionType.Walk, mapId, start, end,
                null, null, null, null, null, dist / WALK_SPEED));
            return legs;
        }

        // Leg 1: Walk to elevator boarding point
        float walkDist1 = Distance3D(start, boardStop.WaitPosition);
        if (walkDist1 > 2f)
        {
            legs.Add(new RouteLeg(TransitionType.Walk, mapId, start, boardStop.WaitPosition,
                null, null, null, null, null, walkDist1 / WALK_SPEED));
        }

        // Leg 2: Ride elevator
        float elevatorTime = elevator.VerticalRange / 10f; // ~10y/sec estimate
        legs.Add(new RouteLeg(TransitionType.Elevator, mapId,
            boardStop.WaitPosition, exitStop.WaitPosition,
            elevator, boardStop, exitStop, null, null, elevatorTime));

        // Leg 3: Walk from elevator exit to destination
        float walkDist2 = Distance3D(exitStop.WaitPosition, end);
        if (walkDist2 > 2f)
        {
            legs.Add(new RouteLeg(TransitionType.Walk, mapId, exitStop.WaitPosition, end,
                null, null, null, null, null, walkDist2 / WALK_SPEED));
        }

        return legs;
    }

    // =========================================================================
    // CROSS-MAP ROUTING
    // =========================================================================

    private List<RouteLeg> PlanCrossMapRoute(
        uint startMapId, Position startPos,
        uint destMapId, Position destPos,
        FlightPathData.Faction faction,
        IReadOnlyCollection<uint>? discoveredFlightNodes)
    {
        // Try direct transition first
        var direct = MapTransitionGraph.FindNearestTransition(
            startMapId, startPos, targetMapId: destMapId, faction: faction);

        if (direct != null)
            return BuildTransitionRoute(startMapId, startPos, destMapId, destPos, direct);

        // Try 1-hop: start → intermediate → dest
        var oneHop = FindOneHopRoute(startMapId, startPos, destMapId, destPos, faction);
        if (oneHop != null)
            return oneHop;

        // No route found
        return [];
    }

    private List<RouteLeg> BuildTransitionRoute(
        uint startMapId, Position startPos,
        uint destMapId, Position destPos,
        MapTransition transition)
    {
        var legs = new List<RouteLeg>();

        // Leg 1: Walk to the transition departure point
        float walkDist1 = Distance3D(startPos, transition.FromPos);
        if (walkDist1 > 5f)
        {
            legs.Add(new RouteLeg(TransitionType.Walk, startMapId, startPos, transition.FromPos,
                null, null, null, null, null, walkDist1 / WALK_SPEED));
        }

        // Leg 2: The transition itself (boat, zeppelin, dungeon portal)
        TransportStop? boardStop = null;
        TransportStop? exitStop = null;
        if (transition.Transport != null)
        {
            boardStop = FindNearestStop(transition.Transport, transition.FromPos);
            exitStop = GetDestinationStop(transition.Transport, transition.FromPos);
        }

        legs.Add(new RouteLeg(transition.Type, startMapId,
            transition.FromPos, transition.ToPos,
            transition.Transport, boardStop, exitStop,
            null, null, transition.EstimatedTransitTimeSec));

        // Leg 3: Walk from arrival to destination
        float walkDist2 = Distance3D(transition.ToPos, destPos);
        if (walkDist2 > 5f)
        {
            legs.Add(new RouteLeg(TransitionType.Walk, destMapId, transition.ToPos, destPos,
                null, null, null, null, null, walkDist2 / WALK_SPEED));
        }

        return legs;
    }

    private List<RouteLeg>? FindOneHopRoute(
        uint startMapId, Position startPos,
        uint destMapId, Position destPos,
        FlightPathData.Faction faction)
    {
        // Get all transitions from start map
        var fromStart = MapTransitionGraph.GetTransitionsFrom(startMapId, faction).ToList();

        float bestCost = float.MaxValue;
        List<RouteLeg>? bestRoute = null;

        foreach (var leg1 in fromStart)
        {
            // Check if leg1's destination map has a transition to the final dest map
            var leg2 = MapTransitionGraph.FindNearestTransition(
                leg1.ToMapId, leg1.ToPos, targetMapId: destMapId, faction: faction);

            if (leg2 == null) continue;

            // Estimate total cost
            float walk1 = Distance3D(startPos, leg1.FromPos) / WALK_SPEED;
            float transit1 = leg1.EstimatedTransitTimeSec;
            float walk2 = Distance3D(leg1.ToPos, leg2.FromPos) / WALK_SPEED;
            float transit2 = leg2.EstimatedTransitTimeSec;
            float walk3 = Distance3D(leg2.ToPos, destPos) / WALK_SPEED;
            float totalCost = walk1 + transit1 + walk2 + transit2 + walk3;

            if (totalCost < bestCost)
            {
                bestCost = totalCost;

                // Build the route
                var route = new List<RouteLeg>();

                // Walk to first transition
                if (walk1 > 5f / WALK_SPEED)
                {
                    route.Add(new RouteLeg(TransitionType.Walk, startMapId, startPos, leg1.FromPos,
                        null, null, null, null, null, walk1));
                }

                // First transition
                route.Add(MakeTransitionLeg(leg1));

                // Walk between transitions
                if (walk2 > 5f / WALK_SPEED)
                {
                    route.Add(new RouteLeg(TransitionType.Walk, leg1.ToMapId, leg1.ToPos, leg2.FromPos,
                        null, null, null, null, null, walk2));
                }

                // Second transition
                route.Add(MakeTransitionLeg(leg2));

                // Walk to destination
                if (walk3 > 5f / WALK_SPEED)
                {
                    route.Add(new RouteLeg(TransitionType.Walk, destMapId, leg2.ToPos, destPos,
                        null, null, null, null, null, walk3));
                }

                bestRoute = route;
            }
        }

        return bestRoute;
    }

    private static RouteLeg MakeTransitionLeg(MapTransition t)
    {
        TransportStop? board = null;
        TransportStop? exit = null;
        if (t.Transport != null)
        {
            board = FindNearestStop(t.Transport, t.FromPos);
            exit = GetDestinationStop(t.Transport, t.FromPos);
        }

        return new RouteLeg(t.Type, t.FromMapId, t.FromPos, t.ToPos,
            t.Transport, board, exit, null, null, t.EstimatedTransitTimeSec);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static float Distance3D(Position a, Position b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
