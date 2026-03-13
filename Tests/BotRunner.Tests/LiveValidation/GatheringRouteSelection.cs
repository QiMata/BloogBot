using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tests.LiveValidation;

internal static class GatheringRouteSelection
{
    public const int DurotarMap = 1;

    // Valley of Trials route staging for the copper-vein pathing probe.
    public const float ValleyCopperRouteStartX = -800f;
    public const float ValleyCopperRouteStartY = -4500f;
    public const float ValleyCopperRouteStartZ = 31f;
    public const float ValleyCopperSearchRadius = 260f;
    public const int ValleyCopperQueryLimit = 32;

    public static List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> SelectValleyCopperVeinCandidates(
        IEnumerable<(uint entry, int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> spawns,
        uint nodeEntry,
        float maxDistance = ValleyCopperSearchRadius,
        int maxCandidates = int.MaxValue)
    {
        return spawns
            .Where(spawn => spawn.entry == nodeEntry
                            && spawn.map == DurotarMap
                            && spawn.distance2D <= maxDistance)
            .OrderBy(spawn => spawn.distance2D)
            .Take(maxCandidates)
            .Select(spawn => (spawn.map, spawn.x, spawn.y, spawn.z, spawn.distance2D, spawn.poolEntry, spawn.poolDescription))
            .ToList();
    }
}
