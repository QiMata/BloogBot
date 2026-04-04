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
    /// <summary>Ride the Deeprun Tram between Ironforge and Stormwind.</summary>
    Tram,
    /// <summary>Use hearthstone to teleport to bind point (10s cast).</summary>
    Hearthstone,
    /// <summary>Use class-specific teleport (Mage Teleport, 10s cast, capital city).</summary>
    ClassTeleport,
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
    public const uint MAP_DEEPRUN_TRAM = 369;   // Deeprun Tram instance

    // 5-Man Dungeons
    public const uint MAP_SFK = 33;            // Shadowfang Keep
    public const uint MAP_STOCKADE = 34;       // The Stockade
    public const uint MAP_DM = 36;             // Deadmines
    public const uint MAP_WC = 43;             // Wailing Caverns
    public const uint MAP_RFK = 47;            // Razorfen Kraul
    public const uint MAP_BFD = 48;            // Blackfathom Deeps
    public const uint MAP_GNOMEREGAN = 90;     // Gnomeregan
    public const uint MAP_ULDAMAN = 70;        // Uldaman
    public const uint MAP_SUNKEN_TEMPLE = 109; // Sunken Temple (Temple of Atal'Hakkar)
    public const uint MAP_RFD = 129;           // Razorfen Downs
    public const uint MAP_SM = 189;            // Scarlet Monastery
    public const uint MAP_ZF = 209;            // Zul'Farrak
    public const uint MAP_BRD = 230;           // Blackrock Depths
    public const uint MAP_SCHOLOMANCE = 289;   // Scholomance
    public const uint MAP_STRATHOLME = 329;    // Stratholme
    public const uint MAP_MARAUDON = 349;      // Maraudon
    public const uint MAP_RFC = 389;           // Ragefire Chasm
    public const uint MAP_DIRE_MAUL = 429;     // Dire Maul

    // Raids
    public const uint MAP_UBRS = 229;          // Upper Blackrock Spire (10-man)
    public const uint MAP_ONYXIA = 249;        // Onyxia's Lair (40-man)
    public const uint MAP_ZG = 309;            // Zul'Gurub (20-man)
    public const uint MAP_MC = 409;            // Molten Core (40-man)
    public const uint MAP_BWL = 469;           // Blackwing Lair (40-man)
    public const uint MAP_AQ20 = 509;          // Ruins of Ahn'Qiraj (20-man)
    public const uint MAP_AQ40 = 531;          // Temple of Ahn'Qiraj (40-man)
    public const uint MAP_NAXXRAMAS = 533;     // Naxxramas (40-man)

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

        // --- DEEPRUN TRAM (Alliance) ---
        // Ironforge entrance → Deeprun Tram instance
        new(MAP_EASTERN_KINGDOMS, new Position(-4838f, -1316f, 502f),
            MAP_DEEPRUN_TRAM, new Position(69f, 11f, -4.3f),
            TransitionType.Tram, DeeprunTram,
            EstimatedTransitTimeSec: 30f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        new(MAP_DEEPRUN_TRAM, new Position(69f, 11f, -4.3f),
            MAP_EASTERN_KINGDOMS, new Position(-4838f, -1316f, 502f),
            TransitionType.Tram, DeeprunTram,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        // Stormwind entrance → Deeprun Tram instance
        new(MAP_EASTERN_KINGDOMS, new Position(-8356f, 524f, 92f),
            MAP_DEEPRUN_TRAM, new Position(2489f, 18f, -4.3f),
            TransitionType.Tram, DeeprunTram,
            EstimatedTransitTimeSec: 30f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        new(MAP_DEEPRUN_TRAM, new Position(2489f, 18f, -4.3f),
            MAP_EASTERN_KINGDOMS, new Position(-8356f, 524f, 92f),
            TransitionType.Tram, DeeprunTram,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        // Tram transit within instance: Ironforge station ↔ Stormwind station
        new(MAP_DEEPRUN_TRAM, new Position(69f, 11f, -4.3f),
            MAP_DEEPRUN_TRAM, new Position(2489f, 18f, -4.3f),
            TransitionType.Tram, DeeprunTram,
            EstimatedTransitTimeSec: 30f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        new(MAP_DEEPRUN_TRAM, new Position(2489f, 18f, -4.3f),
            MAP_DEEPRUN_TRAM, new Position(69f, 11f, -4.3f),
            TransitionType.Tram, DeeprunTram,
            EstimatedTransitTimeSec: 30f,
            FactionRestriction: FlightPathData.Faction.Alliance),

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

        // Blackfathom Deeps — Ashenvale
        new(MAP_KALIMDOR, new Position(-4259f, 2180f, 5f),
            MAP_BFD, new Position(-153f, 106f, -40f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_BFD, new Position(-153f, 106f, -40f),
            MAP_KALIMDOR, new Position(-4259f, 2180f, 5f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Razorfen Kraul — The Barrens
        new(MAP_KALIMDOR, new Position(-4468f, -1659f, 82f),
            MAP_RFK, new Position(1942f, 1544f, 82f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_RFK, new Position(1942f, 1544f, 82f),
            MAP_KALIMDOR, new Position(-4468f, -1659f, 82f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Razorfen Downs — The Barrens
        new(MAP_KALIMDOR, new Position(-4658f, -2524f, 81f),
            MAP_RFD, new Position(2592f, 1107f, 52f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_RFD, new Position(2592f, 1107f, 52f),
            MAP_KALIMDOR, new Position(-4658f, -2524f, 81f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Gnomeregan — Dun Morogh
        new(MAP_EASTERN_KINGDOMS, new Position(-5163f, 925f, 257f),
            MAP_GNOMEREGAN, new Position(-332f, -2f, -152f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null), // Alliance-biased but accessible

        new(MAP_GNOMEREGAN, new Position(-332f, -2f, -152f),
            MAP_EASTERN_KINGDOMS, new Position(-5163f, 925f, 257f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // The Stockade — Stormwind City
        new(MAP_EASTERN_KINGDOMS, new Position(-8764f, 846f, 89f),
            MAP_STOCKADE, new Position(54f, 0f, -18f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        new(MAP_STOCKADE, new Position(54f, 0f, -18f),
            MAP_EASTERN_KINGDOMS, new Position(-8764f, 846f, 89f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: FlightPathData.Faction.Alliance),

        // Uldaman — Badlands
        new(MAP_EASTERN_KINGDOMS, new Position(-6066f, -2956f, 209f),
            MAP_ULDAMAN, new Position(-228f, 45f, -46f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_ULDAMAN, new Position(-228f, 45f, -46f),
            MAP_EASTERN_KINGDOMS, new Position(-6066f, -2956f, 209f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Zul'Farrak — Tanaris
        new(MAP_KALIMDOR, new Position(-6797f, -2891f, 8f),
            MAP_ZF, new Position(1213f, 841f, 9f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_ZF, new Position(1213f, 841f, 9f),
            MAP_KALIMDOR, new Position(-6797f, -2891f, 8f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Maraudon — Desolace
        new(MAP_KALIMDOR, new Position(-1185f, 2880f, 85f),
            MAP_MARAUDON, new Position(1013f, -458f, -44f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_MARAUDON, new Position(1013f, -458f, -44f),
            MAP_KALIMDOR, new Position(-1185f, 2880f, 85f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Sunken Temple (Temple of Atal'Hakkar) — Swamp of Sorrows
        new(MAP_EASTERN_KINGDOMS, new Position(-10176f, -3997f, -112f),
            MAP_SUNKEN_TEMPLE, new Position(-319f, 99f, -131f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_SUNKEN_TEMPLE, new Position(-319f, 99f, -131f),
            MAP_EASTERN_KINGDOMS, new Position(-10176f, -3997f, -112f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Blackrock Depths — Blackrock Mountain
        new(MAP_EASTERN_KINGDOMS, new Position(-7179f, -921f, 165f),
            MAP_BRD, new Position(458f, 27f, -70f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_BRD, new Position(458f, 27f, -70f),
            MAP_EASTERN_KINGDOMS, new Position(-7179f, -921f, 165f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Dire Maul — Feralas
        new(MAP_KALIMDOR, new Position(-3519f, 1120f, 161f),
            MAP_DIRE_MAUL, new Position(44f, -155f, -2f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_DIRE_MAUL, new Position(44f, -155f, -2f),
            MAP_KALIMDOR, new Position(-3519f, 1120f, 161f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Stratholme — Eastern Plaguelands
        new(MAP_EASTERN_KINGDOMS, new Position(3392f, -3379f, 143f),
            MAP_STRATHOLME, new Position(3395f, -3380f, 143f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_STRATHOLME, new Position(3395f, -3380f, 143f),
            MAP_EASTERN_KINGDOMS, new Position(3392f, -3379f, 143f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Scholomance — Western Plaguelands
        new(MAP_EASTERN_KINGDOMS, new Position(1269f, -2556f, 94f),
            MAP_SCHOLOMANCE, new Position(196f, 127f, 135f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_SCHOLOMANCE, new Position(196f, 127f, 135f),
            MAP_EASTERN_KINGDOMS, new Position(1269f, -2556f, 94f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // --- RAID PORTALS ---

        // Upper Blackrock Spire (10-man) — Blackrock Mountain
        new(MAP_EASTERN_KINGDOMS, new Position(-7524f, -1226f, 287f),
            MAP_UBRS, new Position(78f, -225f, 50f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_UBRS, new Position(78f, -225f, 50f),
            MAP_EASTERN_KINGDOMS, new Position(-7524f, -1226f, 287f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Zul'Gurub (20-man) — Stranglethorn Vale
        new(MAP_EASTERN_KINGDOMS, new Position(-11916f, -1218f, 92f),
            MAP_ZG, new Position(-11916f, -1221f, 92f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_ZG, new Position(-11916f, -1221f, 92f),
            MAP_EASTERN_KINGDOMS, new Position(-11916f, -1218f, 92f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Ruins of Ahn'Qiraj (AQ20) — Silithus
        new(MAP_KALIMDOR, new Position(-8410f, 1499f, 31f),
            MAP_AQ20, new Position(-8429f, 1512f, 31f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_AQ20, new Position(-8429f, 1512f, 31f),
            MAP_KALIMDOR, new Position(-8410f, 1499f, 31f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Molten Core (40-man) — entrance inside BRD (map 230)
        new(MAP_BRD, new Position(1096f, -467f, -105f),
            MAP_MC, new Position(1096f, -467f, -105f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_MC, new Position(1096f, -467f, -105f),
            MAP_BRD, new Position(1096f, -467f, -105f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Blackwing Lair (40-man) — entrance inside UBRS (map 229)
        new(MAP_UBRS, new Position(-7665f, -1102f, 400f),
            MAP_BWL, new Position(-7665f, -1100f, 400f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_BWL, new Position(-7665f, -1100f, 400f),
            MAP_UBRS, new Position(-7665f, -1102f, 400f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Onyxia's Lair (40-man) — Dustwallow Marsh
        new(MAP_KALIMDOR, new Position(-4707f, -3727f, 54f),
            MAP_ONYXIA, new Position(28f, -54f, -5f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_ONYXIA, new Position(28f, -54f, -5f),
            MAP_KALIMDOR, new Position(-4707f, -3727f, 54f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Temple of Ahn'Qiraj (AQ40) — Silithus
        new(MAP_KALIMDOR, new Position(-8233f, 2035f, 129f),
            MAP_AQ40, new Position(-8231f, 2035f, 129f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_AQ40, new Position(-8231f, 2035f, 129f),
            MAP_KALIMDOR, new Position(-8233f, 2035f, 129f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        // Naxxramas (40-man) — Eastern Plaguelands
        new(MAP_EASTERN_KINGDOMS, new Position(3131f, -3730f, 138f),
            MAP_NAXXRAMAS, new Position(3005f, -3434f, 304f),
            TransitionType.DungeonPortal, null,
            EstimatedTransitTimeSec: 0f,
            FactionRestriction: null),

        new(MAP_NAXXRAMAS, new Position(3005f, -3434f, 304f),
            MAP_EASTERN_KINGDOMS, new Position(3131f, -3730f, 138f),
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
