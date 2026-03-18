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
        // Entrance area — just inside the portal
        new Position(3f, -11f, -18f),

        // First corridor heading south
        new Position(-15f, -40f, -20f),
        new Position(-35f, -72f, -22f),

        // First open area with trogg packs
        new Position(-55f, -100f, -22f),
        new Position(-60f, -135f, -22f),

        // Oggleflint area (left fork)
        new Position(-45f, -165f, -23f),
        new Position(-28f, -190f, -22f),

        // Central lava chamber approach — Taragaman the Hungerer
        new Position(-20f, -220f, -22f),
        new Position(-35f, -255f, -21f),
        new Position(-55f, -280f, -20f),

        // Taragaman's platform
        new Position(-40f, -310f, -19f),

        // Jergosh the Invoker area (rear cavern)
        new Position(-20f, -345f, -19f),
        new Position(-5f, -375f, -18f),

        // Bazzalan's alcove (deep end)
        new Position(10f, -400f, -18f),
        new Position(0f, -420f, -17f),
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
