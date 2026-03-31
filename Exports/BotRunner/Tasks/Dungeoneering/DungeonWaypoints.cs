using GameData.Core.Models;
using System.Collections.Generic;

namespace BotRunner.Tasks.Dungeoneering;

/// <summary>
/// Pre-defined waypoint routes for dungeon crawling.
/// Waypoints are ordered for a leader to navigate through the dungeon,
/// visiting each encounter area in sequence.
/// </summary>
public static class DungeonWaypoints
{
    /// <summary>
    /// Ragefire Chasm (map 389) — linear dungeon under Orgrimmar.
    /// Route follows the main corridor from entrance to final boss area.
    ///
    /// Encounters (in order):
    ///   1. Ragefire Troggs (entrance corridor)
    ///   2. Oggleflint (first boss area, left branch)
    ///   3. Taragaman the Hungerer (central lava chamber)
    ///   4. Jergosh the Invoker (rear chamber)
    ///   5. Bazzalan (deep end, optional)
    /// </summary>
    public static IReadOnlyList<Position> RagefireChasm { get; } =
    [
        // Entrance area — just inside the portal (ground at Z≈-16.6)
        new Position(3f, -11f, -16f),

        // First corridor — earthborers and molten elementals
        new Position(-23f, -61f, -21f),
        new Position(-70f, -33f, -18f),

        // Approaching Oggleflint — descending into trogg caves
        new Position(-106f, -38f, -30f),
        new Position(-130f, -35f, -33f),

        // Oggleflint's area (first boss)
        new Position(-148f, 28f, -39f),

        // Ascending past Oggleflint toward lava chambers
        new Position(-177f, 75f, -22f),
        new Position(-209f, 56f, -14f),

        // Searing Blade territory — approaching Taragaman
        new Position(-223f, 87f, -25f),

        // Taragaman the Hungerer's platform (second boss)
        new Position(-245f, 150f, -19f),

        // Deep corridor toward Jergosh and Bazzalan
        new Position(-270f, 97f, -25f),
        new Position(-300f, 154f, -25f),
        new Position(-340f, 214f, -21f),

        // Jergosh the Invoker area (third boss)
        new Position(-377f, 209f, -22f),

        // Bazzalan's alcove (final boss, upper ledge)
        new Position(-385f, 146f, 8f),
    ];

    /// <summary>
    /// Get dungeon waypoints by map ID.
    /// Returns null if no waypoint data exists for the given dungeon.
    /// </summary>
    /// <summary>
    /// Wailing Caverns (map 43) — branching dungeon in the Barrens.
    /// Route follows the main path hitting bosses in order:
    ///   1. Lady Anacondra (serpent cave, left branch)
    ///   2. Lord Cobrahn (fang chamber)
    ///   3. Kresh (turtle, pools area)
    ///   4. Lord Pythas (vine terrace)
    ///   5. Skum (deep pool)
    ///   6. Lord Serpentis (upper terrace)
    ///   7. Verdan the Everliving (final chamber)
    ///   8. Mutanus the Devourer (escort event, after Disciple of Naralex)
    /// </summary>
    public static IReadOnlyList<Position> WailingCaverns { get; } =
    [
        // Entrance — inside the instance portal
        new Position(-163f, 132f, -73f),

        // First corridor — deviate raptors and serpents
        new Position(-120f, 160f, -78f),
        new Position(-82f, 194f, -91f),

        // Lady Anacondra's area (first boss)
        new Position(-37f, 233f, -89f),

        // Path toward Lord Cobrahn
        new Position(-67f, 265f, -87f),
        new Position(-84f, 320f, -87f),

        // Lord Cobrahn's chamber (second boss)
        new Position(-107f, 347f, -85f),

        // Kresh area — pools
        new Position(-128f, 393f, -95f),
        new Position(-115f, 441f, -92f),

        // Lord Pythas — vine terrace
        new Position(-66f, 478f, -87f),
        new Position(-38f, 514f, -82f),

        // Skum — deep pool area
        new Position(4f, 549f, -83f),

        // Lord Serpentis — upper terrace
        new Position(-19f, 600f, -73f),

        // Verdan the Everliving — final chamber
        new Position(-36f, 639f, -68f),
    ];

    /// <summary>
    /// Shadowfang Keep (map 33) — linear multi-floor keep.
    /// </summary>
    public static IReadOnlyList<Position> ShadowfangKeep { get; } =
    [
        // Courtyard entrance
        new Position(-229f, 2108f, 76f),

        // Stable area — wolves and worgen
        new Position(-227f, 2130f, 80f),
        new Position(-233f, 2173f, 80f),

        // Inner keep — ascending staircases
        new Position(-193f, 2192f, 80f),
        new Position(-175f, 2164f, 97f),

        // Commander Springvale area
        new Position(-146f, 2168f, 127f),

        // Upper floors — ascending through the keep
        new Position(-141f, 2184f, 147f),
        new Position(-155f, 2207f, 167f),

        // Arugal's chamber (final boss)
        new Position(-148f, 2220f, 191f),
    ];

    public static IReadOnlyList<Position>? GetWaypointsForMap(uint mapId) => mapId switch
    {
        389 => RagefireChasm,
        43 => WailingCaverns,
        33 => ShadowfangKeep,
        _ => null,
    };
}
