using BotRunner.Combat;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static BotRunner.Movement.TransportData;

namespace BotRunner.Movement;

/// <summary>
/// Type of transition between map zones or between maps.
/// </summary>
public enum TransitionType
{
    /// <summary>Walk on the same map (normal pathfinding).</summary>
    Walk,
    /// <summary>Ride an elevator (same map, large Z change).</summary>
    Elevator,
    /// <summary>Take a flight path (same continent, requires discovered nodes).</summary>
    FlightPath,
    /// <summary>Board a boat (typically cross-continent for Alliance).</summary>
    Boat,
    /// <summary>Board a zeppelin (typically cross-continent for Horde).</summary>
    Zeppelin,
    /// <summary>Enter/exit a dungeon portal.</summary>
    DungeonPortal,
}

/// <summary>
/// A single edge in the map transition graph.
/// Describes how to get from one (map, position) to another.
/// </summary>
public record MapTransition(
    uint FromMapId,
    Position FromPos,
    uint ToMapId,
    Position ToPos,
    TransitionType Type,
    TransportDefinition? Transport,
    float EstimatedTransitTimeSec,
    FlightPathData.Faction? FactionRestriction);

/// <summary>
/// Static graph of map-to-map and zone-to-zone transitions for vanilla 1.12.1.
/// Edges represent transports (boats, zeppelins, elevators), flight paths,
/// and dungeon portals. Used by <see cref="CrossMapRouter"/> to plan routes.
/// </summary>
public static class MapTransitionGraph
{
    // =========================================================================
    // MAP IDs
    // =========================================================================
    public const uint MAP_EASTERN_KINGDOMS = 0;
    public const uint MAP_KALIMDOR = 1;
    public const uint MAP_RFC = 389;           // Ragefire Chasm
    public const uint MAP_WC = 43;             // Wailing Caverns
    public const uint MAP_DM = 36;             // Deadmines
    public const uint MAP_SFK = 33;            // Shadowfang Keep
    public const uint MAP_BFD = 48;            // Blackfathom Deeps
    public const uint MAP_SM = 189;            // Scarlet Monastery
    public const uint MAP_BRD = 230;           // Blackrock Depths

    // =========================================================================
    // ALL TRANSITIONS
    // =========================================================================

    public static readonly IReadOnlyList<MapTransition> Transitions =
    [
        // --- BOATS (Alliance / Neutral) ---
        new(MAP_EASTERN_KINGDOMS, new Position(-3676f, -613f, 6f),
            MAP_KALIMDOR, new Position(6581f, 769f, 5f),
            TransitionType.Boat, BoatMenethilAuberdine,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: null), // Alliance-biased but neutral route

        new(MAP_KALIMDOR, new Position(6581f, 769f, 5f),
            MAP_EASTERN_KINGDOMS, new Position(-3676f, -613f, 6f),
            TransitionType.Boat, BoatMenethilAuberdine,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: null),

        new(MAP_EASTERN_KINGDOMS, new Position(-3664f, -582f, 6f),
            MAP_KALIMDOR, new Position(-3814f, -4516f, 9f),
            TransitionType.Boat, BoatMenethilTheramore,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: null),

        new(MAP_KALIMDOR, new Position(-3814f, -4516f, 9f),
            MAP_EASTERN_KINGDOMS, new Position(-3664f, -582f, 6f),
            TransitionType.Boat, BoatMenethilTheramore,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: null),

        new(MAP_EASTERN_KINGDOMS, new Position(-14280f, 553f, 9f),
            MAP_KALIMDOR, new Position(-996f, -3827f, 6f),
            TransitionType.Boat, BoatBootyBayRatchet,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: null), // Neutral

        new(MAP_KALIMDOR, new Position(-996f, -3827f, 6f),
            MAP_EASTERN_KINGDOMS, new Position(-14280f, 553f, 9f),
            TransitionType.Boat, BoatBootyBayRatchet,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: null),

        // Auberdine ↔ Teldrassil (same map, same continent)
        new(MAP_KALIMDOR, new Position(6587f, 797f, 5f),
            MAP_KALIMDOR, new Position(8642f, 837f, 23f),
            TransitionType.Boat, BoatAuberdineTeldrassil,
            EstimatedTransitTimeSec: 45f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        new(MAP_KALIMDOR, new Position(8642f, 837f, 23f),
            MAP_KALIMDOR, new Position(6587f, 797f, 5f),
            TransitionType.Boat, BoatAuberdineTeldrassil,
            EstimatedTransitTimeSec: 45f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        // --- ZEPPELINS (Horde) ---
        new(MAP_EASTERN_KINGDOMS, new Position(2066f, 288f, 97f),
            MAP_KALIMDOR, new Position(1320f, -4649f, 53f),
            TransitionType.Zeppelin, ZeppelinUndercityOrgrimmar,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: FlightPathData.Faction.Horde),

        new(MAP_KALIMDOR, new Position(1320f, -4649f, 53f),
            MAP_EASTERN_KINGDOMS, new Position(2066f, 288f, 97f),
            TransitionType.Zeppelin, ZeppelinUndercityOrgrimmar,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: FlightPathData.Faction.Horde),

        new(MAP_EASTERN_KINGDOMS, new Position(2066f, 286f, 97f),
            MAP_EASTERN_KINGDOMS, new Position(-12411f, 213f, 32f),
            TransitionType.Zeppelin, ZeppelinUndercityGromgol,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: FlightPathData.Faction.Horde),

        new(MAP_EASTERN_KINGDOMS, new Position(-12411f, 213f, 32f),
            MAP_EASTERN_KINGDOMS, new Position(2066f, 286f, 97f),
            TransitionType.Zeppelin, ZeppelinUndercityGromgol,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: FlightPathData.Faction.Horde),

        new(MAP_KALIMDOR, new Position(1317f, -4652f, 53f),
            MAP_EASTERN_KINGDOMS, new Position(-12407f, 214f, 32f),
            TransitionType.Zeppelin, ZeppelinOrgrimmarGromgol,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: FlightPathData.Faction.Horde),

        new(MAP_EASTERN_KINGDOMS, new Position(-12407f, 214f, 32f),
            MAP_KALIMDOR, new Position(1317f, -4652f, 53f),
            TransitionType.Zeppelin, ZeppelinOrgrimmarGromgol,
            EstimatedTransitTimeSec: 60f,
            FactionRestriction: FlightPathData.Faction.Horde),

        // --- DUNGEON PORTALS (Horde-accessible) ---
        // Ragefire Chasm — inside Orgrimmar cleft
        new(MAP_KALIMDOR, new Position(1811f, -4410f, -18f),
            MAP_RFC, new Position(3f, -11f, -18f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: FlightPathData.Faction.Horde),

        new(MAP_RFC, new Position(3f, -11f, -18f),
            MAP_KALIMDOR, new Position(1811f, -4410f, -18f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: FlightPathData.Faction.Horde),

        // Wailing Caverns — The Barrens
        new(MAP_KALIMDOR, new Position(-740f, -2214f, 16f),
            MAP_WC, new Position(-163f, 132f, -73f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_WC, new Position(-163f, 132f, -73f),
            MAP_KALIMDOR, new Position(-740f, -2214f, 16f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Scarlet Monastery — Tirisfal Glades
        new(MAP_EASTERN_KINGDOMS, new Position(2892f, -802f, 160f),
            MAP_SM, new Position(1089f, 1398f, 32f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_SM, new Position(1089f, 1398f, 32f),
            MAP_EASTERN_KINGDOMS, new Position(2892f, -802f, 160f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Shadowfang Keep — Silverpine Forest
        new(MAP_EASTERN_KINGDOMS, new Position(-234f, 1561f, 76f),
            MAP_SFK, new Position(-229f, 2108f, 76f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_SFK, new Position(-229f, 2108f, 76f),
            MAP_EASTERN_KINGDOMS, new Position(-234f, 1561f, 76f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Deadmines — Westfall
        new(MAP_EASTERN_KINGDOMS, new Position(-11208f, 1672f, 24f),
            MAP_DM, new Position(-16f, -383f, 62f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_DM, new Position(-16f, -383f, 62f),
            MAP_EASTERN_KINGDOMS, new Position(-11208f, 1672f, 24f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),
    ];

    // =========================================================================
    // QUERIES
    // =========================================================================

    /// <summary>
    /// Get all transitions departing from a specific map, optionally filtered by faction.
    /// </summary>
    public static IEnumerable<MapTransition> GetTransitionsFrom(
        uint mapId, FlightPathData.Faction? faction = null)
    {
        return Transitions.Where(t =>
            t.FromMapId == mapId &&
            (t.FactionRestriction == null || faction == null || t.FactionRestriction == faction));
    }

    /// <summary>
    /// Get all transitions arriving at a specific map.
    /// </summary>
    public static IEnumerable<MapTransition> GetTransitionsTo(
        uint mapId, FlightPathData.Faction? faction = null)
    {
        return Transitions.Where(t =>
            t.ToMapId == mapId &&
            (t.FactionRestriction == null || faction == null || t.FactionRestriction == faction));
    }

    /// <summary>
    /// Check if a direct transition exists between two maps.
    /// </summary>
    public static bool HasDirectTransition(uint fromMapId, uint toMapId, FlightPathData.Faction? faction = null)
    {
        return GetTransitionsFrom(fromMapId, faction).Any(t => t.ToMapId == toMapId);
    }

    /// <summary>
    /// Find the nearest transition point from a position on a given map.
    /// </summary>
    public static MapTransition? FindNearestTransition(
        uint mapId, Position pos, uint? targetMapId = null, FlightPathData.Faction? faction = null)
    {
        MapTransition? best = null;
        float bestDist = float.MaxValue;

        foreach (var t in GetTransitionsFrom(mapId, faction))
        {
            if (targetMapId.HasValue && t.ToMapId != targetMapId.Value) continue;

            float dx = pos.X - t.FromPos.X;
            float dy = pos.Y - t.FromPos.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = t;
            }
        }

        return best;
    }
}
