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
    public static IReadOnlyList<Position>? GetWaypointsForMap(uint mapId) => mapId switch
    {
        389 => RagefireChasm,
        _ => null,
    };
}
