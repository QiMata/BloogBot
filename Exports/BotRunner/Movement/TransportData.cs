using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Movement;

/// <summary>
/// Static database of vanilla 1.12.1 transports: elevators, boats, and zeppelins.
/// Follows the <see cref="Combat.FlightPathData"/> pattern.
/// Used by TransportWaitingLogic and CrossMapRouter for transport-aware pathfinding.
/// </summary>
public static class TransportData
{
    public enum TransportType { Elevator, Boat, Zeppelin }

    /// <summary>
    /// A named world waypoint that describes how to reach a transport stop.
    /// </summary>
    public record TransportApproachPoint(
        string Name,
        Position Position);

    /// <summary>
    /// A known waitable navmesh surface for a transport stop.
    /// </summary>
    public record TransportWaitSurface(
        string Name,
        ulong PolygonRef,
        int PolygonIndex,
        Position Center,
        float SampleRadius);

    /// <summary>
    /// A boarding/disembarking point for a transport.
    /// </summary>
    public record TransportStop(
        string Name,              // e.g. "Undercity Upper", "Undercity Lower"
        uint MapId,
        Position WaitPosition,    // Where to stand and wait for the transport
        float BoardingRadius,     // How close = "at the stop"
        Position? BoardingPosition = null, // Optional fixed world staging point near an offset transport deck
        Position? TransportBoardingOffset = null, // Optional post-attachment local-space point to stand on the transport model
        Position? ApproachPosition = null, // Optional generated-navigation staging point before final boarding
        TransportApproachPoint[]? ApproachRoute = null, // Optional ordered long-travel approach route
        TransportWaitSurface? WaitSurface = null) // Optional waitable navmesh surface for random standing points
    {
        public Position NavigationPosition => ApproachPosition ?? WaitPosition;

        public Position NavigationEndpoint =>
            ApproachRoute is { Length: > 0 }
                ? ApproachRoute[^1].Position
                : NavigationPosition;

        public Position ResolveWaitPosition(string? stableKey = null)
        {
            if (WaitSurface == null || string.IsNullOrWhiteSpace(stableKey))
                return NavigationPosition;

            var hashInput = $"{stableKey}|{WaitSurface.Name}|{WaitSurface.PolygonRef:X16}";
            var angle = ToUnitFloat(StableHash($"{hashInput}|angle")) * MathF.PI * 2f;
            var radius = MathF.Sqrt(ToUnitFloat(StableHash($"{hashInput}|radius")))
                * MathF.Max(0f, WaitSurface.SampleRadius);

            return new Position(
                WaitSurface.Center.X + MathF.Cos(angle) * radius,
                WaitSurface.Center.Y + MathF.Sin(angle) * radius,
                WaitSurface.Center.Z);
        }

        private static uint StableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            unchecked
            {
                var hash = offset;
                foreach (var c in value)
                {
                    hash ^= c;
                    hash *= prime;
                }

                return hash;
            }
        }

        private static float ToUnitFloat(uint value)
            => (value + 0.5f) / 4294967296f;
    }

    /// <summary>
    /// A transport definition with its stops.
    /// </summary>
    public record TransportDefinition(
        uint GameObjectEntry,
        uint DisplayId,
        string Name,
        TransportType Type,
        TransportStop[] Stops,
        float VerticalRange);     // Total Z travel distance (for transport detection)

    // =========================================================================
    // UNDERCITY ELEVATORS — 3 shafts connecting Ruins of Lordaeron to Undercity
    // =========================================================================
    // Data source: Dralrahgra_Undercity_2026-02-13_19-26-54.json recording
    // All on Map 0 (Eastern Kingdoms). DisplayId 455 = elevator car.
    // Z range: ~-43 to ~+60 (roughly 100y travel).
    // Door display ID 462 marks upper/lower stops.

    /// <summary>West shaft — near the main entrance from Tirisfal Glades courtyard.</summary>
    public static readonly TransportDefinition UndercityElevatorWest = new(
        GameObjectEntry: 20655,
        DisplayId: 455,
        Name: "Undercity Elevator (West)",
        Type: TransportType.Elevator,
        Stops:
        [
            new("Undercity Upper (West)", 0, new Position(1544.24f, 240.77f, 55.40f), BoardingRadius: 6f),
            new("Undercity Lower (West)", 0, new Position(1544.24f, 240.77f, -43.0f), BoardingRadius: 6f),
        ],
        VerticalRange: 98f);

    /// <summary>East shaft — southern passage.</summary>
    public static readonly TransportDefinition UndercityElevatorEast = new(
        GameObjectEntry: 20652,
        DisplayId: 455,
        Name: "Undercity Elevator (East)",
        Type: TransportType.Elevator,
        Stops:
        [
            new("Undercity Upper (East)", 0, new Position(1595.26f, 188.64f, 55.40f), BoardingRadius: 6f),
            new("Undercity Lower (East)", 0, new Position(1595.26f, 188.64f, -43.0f), BoardingRadius: 6f),
        ],
        VerticalRange: 98f);

    /// <summary>North shaft — northern passage.</summary>
    public static readonly TransportDefinition UndercityElevatorNorth = new(
        GameObjectEntry: 20649,
        DisplayId: 455,
        Name: "Undercity Elevator (North)",
        Type: TransportType.Elevator,
        Stops:
        [
            new("Undercity Upper (North)", 0, new Position(1596.15f, 291.80f, 55.40f), BoardingRadius: 6f),
            new("Undercity Lower (North)", 0, new Position(1596.15f, 291.80f, -43.0f), BoardingRadius: 6f),
        ],
        VerticalRange: 98f);

    // =========================================================================
    // THUNDER BLUFF ELEVATORS — 3 rise lifts connecting the mesa top to ground
    // =========================================================================
    // Positions from MaNGOS gameobject_template; not yet recording-validated.
    // Map 1 (Kalimdor). DisplayId 455 = elevator car.

    public static readonly TransportDefinition ThunderBluffElevatorMain = new(
        GameObjectEntry: 47296,
        DisplayId: 455,
        Name: "Thunder Bluff Elevator (Main)",
        Type: TransportType.Elevator,
        Stops:
        [
            new("Thunder Bluff Top (Main)", 1, new Position(-1247.0f, 50.0f, 127.0f), BoardingRadius: 6f),
            new("Thunder Bluff Bottom (Main)", 1, new Position(-1247.0f, 50.0f, -30.0f), BoardingRadius: 6f),
        ],
        VerticalRange: 157f);

    // =========================================================================
    // BOATS — cross-continent sea transports
    // =========================================================================
    // Boat routes connect Eastern Kingdoms ↔ Kalimdor and neutral ports.
    // Players wait at docks and board when the boat arrives.

    public static readonly TransportDefinition BoatMenethilAuberdine = new(
        GameObjectEntry: 176231,
        DisplayId: 3015,
        Name: "Boat: Menethil Harbor ↔ Auberdine",
        Type: TransportType.Boat,
        Stops:
        [
            new("Menethil Harbor Dock", 0, new Position(-3676.0f, -613.0f, 6.0f), BoardingRadius: 12f),
            new("Auberdine Dock", 1, new Position(6581.0f, 769.0f, 5.0f), BoardingRadius: 12f),
        ],
        VerticalRange: 0f);

    public static readonly TransportDefinition BoatMenethilTheramore = new(
        GameObjectEntry: 176310,
        DisplayId: 3015,
        Name: "Boat: Menethil Harbor ↔ Theramore",
        Type: TransportType.Boat,
        Stops:
        [
            new("Menethil Harbor Dock (South)", 0, new Position(-3664.0f, -582.0f, 6.0f), BoardingRadius: 12f),
            new("Theramore Dock", 1, new Position(-3814.0f, -4516.0f, 9.0f), BoardingRadius: 12f),
        ],
        VerticalRange: 0f);

    public static readonly TransportDefinition BoatBootyBayRatchet = new(
        GameObjectEntry: 20808,
        DisplayId: 3015,
        Name: "Boat: Booty Bay ↔ Ratchet",
        Type: TransportType.Boat,
        Stops:
        [
            new("Booty Bay Dock", 0, new Position(-14280.0f, 553.0f, 9.0f), BoardingRadius: 12f),
            new("Ratchet Dock", 1, new Position(-996.0f, -3827.0f, 6.0f), BoardingRadius: 12f),
        ],
        VerticalRange: 0f);

    public static readonly TransportDefinition BoatAuberdineTeldrassil = new(
        GameObjectEntry: 176244,
        DisplayId: 3015,
        Name: "Boat: Auberdine ↔ Teldrassil (Rut'theran Village)",
        Type: TransportType.Boat,
        Stops:
        [
            new("Auberdine Dock (North)", 1, new Position(6587.0f, 797.0f, 5.0f), BoardingRadius: 12f),
            new("Rut'theran Village Dock", 1, new Position(8642.0f, 837.0f, 23.0f), BoardingRadius: 12f),
        ],
        VerticalRange: 0f);

    // =========================================================================
    // ZEPPELINS — Horde air transports
    // =========================================================================

    public static readonly TransportDefinition ZeppelinUndercityOrgrimmar = new(
        GameObjectEntry: 164871,
        DisplayId: 3031,
        Name: "Zeppelin: Orgrimmar <-> Undercity",
        Type: TransportType.Zeppelin,
        Stops:
        [
            new(
                "Orgrimmar Zeppelin Tower",
                1,
                // org-uc-boarding.jpg /gps, aligned to the Orgrimmar -> Undercity gangplank.
                new Position(1320.142944f, -4653.158691f, 53.891945f),
                BoardingRadius: 12f,
                BoardingPosition: new Position(1320.142944f, -4653.158691f, 53.891945f),
                // DBC path 302 stops the model at (1318.107,-4658.047,71.860);
                // zepplin-riding.jpg captures a stable post-attachment center-deck transport-local offset.
                TransportBoardingOffset: new Position(-12.580913f, -7.983256f, -16.398277f),
                ApproachPosition: new Position(1320.142944f, -4653.158691f, 53.891945f),
                ApproachRoute:
                [
                    new(
                        "orgrimmar.windrider_tower.descent",
                        new Position(1604.8f, -4425.6f, 10.36f)),
                    new(
                        "orgrimmar.front_gate.hallway_exit",
                        new Position(1491.4f, -4417.3f, 23.3f)),
                    new(
                        "durotar.exterior_incline",
                        new Position(1381.3f, -4370.6f, 26.0f)),
                    new(
                        "orgrimmar.zeppelin_tower.lower_approach",
                        new Position(1356.8f, -4501.3f, 29.44f)),
                    new(
                        "orgrimmar.zeppelin_tower.base",
                        new Position(1342.4f, -4652.1f, 24.6f)),
                    new(
                        "orgrimmar.zeppelin_tower.frezza_deck",
                        new Position(1331.11f, -4649.45f, 53.6269f)),
                    new(
                        "orgrimmar.undercity_zeppelin.boarding_platform",
                        new Position(1320.142944f, -4653.158691f, 53.891945f)),
                ],
                WaitSurface: new TransportWaitSurface(
                    "OrgrimmarUndercityZeppelinBoardingPlatform",
                    PolygonRef: 0x1000015201B41UL,
                    PolygonIndex: 6977,
                    Center: new Position(1320.142944f, -4653.158691f, 53.891945f),
                    SampleRadius: 4.0f)),
            new(
                "Undercity Zeppelin Tower",
                0,
                // uc-org-boarding.jpg /gps, aligned to the Undercity -> Orgrimmar gangplank.
                new Position(2066.911377f, 290.113708f, 97.031593f),
                BoardingRadius: 12f,
                BoardingPosition: new Position(2066.911377f, 290.113708f, 97.031593f),
                TransportBoardingOffset: new Position(-12.580913f, -7.983256f, -16.398277f)),
        ],
        VerticalRange: 0f);

    public static readonly TransportDefinition ZeppelinUndercityGromgol = new(
        GameObjectEntry: 176495,
        DisplayId: 3031,
        Name: "Zeppelin: Grom'gol <-> Undercity",
        Type: TransportType.Zeppelin,
        Stops:
        [
            new("Grom'gol Zeppelin Tower", 0, new Position(-12411.0f, 213.0f, 32.0f), BoardingRadius: 15f),
            new("Undercity Zeppelin Tower (South)", 0, new Position(2066.0f, 286.0f, 97.0f), BoardingRadius: 15f),
        ],
        VerticalRange: 0f);

    public static readonly TransportDefinition ZeppelinOrgrimmarGromgol = new(
        GameObjectEntry: 175080,
        DisplayId: 3031,
        Name: "Zeppelin: Orgrimmar <-> Grom'gol",
        Type: TransportType.Zeppelin,
        Stops:
        [
            new("Orgrimmar Zeppelin Tower (South)", 1, new Position(1317.0f, -4652.0f, 53.0f), BoardingRadius: 15f),
            new("Grom'gol Zeppelin Tower (South)", 0, new Position(-12407.0f, 214.0f, 32.0f), BoardingRadius: 15f),
        ],
        VerticalRange: 0f);

    // =========================================================================
    // TRAMS — underground rail transports
    // =========================================================================

    public static readonly TransportDefinition DeeprunTram = new(
        GameObjectEntry: 176085,
        DisplayId: 1560,
        Name: "Deeprun Tram: Ironforge ↔ Stormwind",
        Type: TransportType.Boat, // Re-uses Boat type (scheduled transport with stops)
        Stops:
        [
            new("Ironforge Tram Station", 369, new Position(69.0f, 11.0f, -4.3f), BoardingRadius: 10f),
            new("Stormwind Tram Station", 369, new Position(2489.0f, 18.0f, -4.3f), BoardingRadius: 10f),
        ],
        VerticalRange: 0f);

    // =========================================================================
    // ALL TRANSPORTS — master list
    // =========================================================================

    public static readonly IReadOnlyList<TransportDefinition> AllTransports =
    [
        // Elevators
        UndercityElevatorWest,
        UndercityElevatorEast,
        UndercityElevatorNorth,
        ThunderBluffElevatorMain,
        // Boats
        BoatMenethilAuberdine,
        BoatMenethilTheramore,
        BoatBootyBayRatchet,
        BoatAuberdineTeldrassil,
        // Zeppelins
        ZeppelinUndercityOrgrimmar,
        ZeppelinUndercityGromgol,
        ZeppelinOrgrimmarGromgol,
        // Trams
        DeeprunTram,
    ];

    // =========================================================================
    // LOOKUP METHODS
    // =========================================================================

    /// <summary>
    /// Find the nearest transport to a world position within maxDistance.
    /// Returns null if no transport stop is nearby.
    /// </summary>
    public static TransportDefinition? FindNearestTransport(
        uint mapId, Position pos, float maxDistance = 50f)
    {
        TransportDefinition? best = null;
        float bestDist = maxDistance;

        foreach (var transport in AllTransports)
        {
            foreach (var stop in transport.Stops)
            {
                if (stop.MapId != mapId) continue;

                float dist = DistanceXY(pos, stop.WaitPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = transport;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Find the nearest stop of a specific transport to a world position.
    /// </summary>
    public static TransportStop? FindNearestStop(
        TransportDefinition transport, Position pos)
    {
        TransportStop? best = null;
        float bestDist = float.MaxValue;

        foreach (var stop in transport.Stops)
        {
            float dist = Distance3D(pos, stop.WaitPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = stop;
            }
        }

        return best;
    }

    /// <summary>
    /// Find transport by its MaNGOS gameobject entry.
    /// </summary>
    public static TransportDefinition? FindByEntry(uint gameObjectEntry)
    {
        return AllTransports.FirstOrDefault(t => t.GameObjectEntry == gameObjectEntry);
    }

    /// <summary>
    /// Find a transport by a packed transport GUID from object or movement state.
    /// </summary>
    public static TransportDefinition? FindByGuid(ulong guid)
    {
        return TransportObjectIdentity.FindTransportByGuid(guid);
    }

    /// <summary>
    /// Find transport by its display ID.
    /// </summary>
    public static IEnumerable<TransportDefinition> FindByDisplayId(uint displayId)
    {
        return AllTransports.Where(t => t.DisplayId == displayId);
    }

    /// <summary>
    /// Get the destination stop (the stop the player is NOT currently near).
    /// For a 2-stop transport, this is simply the other stop.
    /// </summary>
    public static TransportStop? GetDestinationStop(
        TransportDefinition transport, Position currentPos)
    {
        if (transport.Stops.Length < 2) return null;

        var nearestStop = FindNearestStop(transport, currentPos);
        return transport.Stops.FirstOrDefault(s => s != nearestStop);
    }

    /// <summary>
    /// Check if a path segment likely crosses an elevator (large Z delta near known elevator XY).
    /// Used to detect when a walkable path needs an elevator leg inserted.
    /// </summary>
    public static TransportDefinition? DetectElevatorCrossing(
        uint mapId, Position from, Position to, float minZDelta = 30f)
    {
        float zDelta = MathF.Abs(from.Z - to.Z);
        if (zDelta < minZDelta) return null;

        // Check if both endpoints are near any elevator shaft (horizontally)
        foreach (var transport in AllTransports)
        {
            if (transport.Type != TransportType.Elevator) continue;
            if (transport.VerticalRange < minZDelta) continue;

            // For elevator detection, check if the midpoint is near the shaft
            float midX = (from.X + to.X) / 2f;
            float midY = (from.Y + to.Y) / 2f;
            var midPos = new Position(midX, midY, from.Z);

            foreach (var stop in transport.Stops)
            {
                if (stop.MapId != mapId) continue;
                float dist = DistanceXY(midPos, stop.WaitPosition);
                if (dist < 30f) // Within 30y horizontally of elevator shaft
                    return transport;
            }
        }

        return null;
    }

    private static float DistanceXY(Position a, Position b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float Distance3D(Position a, Position b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
