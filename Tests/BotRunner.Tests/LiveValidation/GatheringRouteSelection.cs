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
    public const float ValleyCopperSearchRadius = 500f;
    public const int ValleyCopperQueryLimit = 64;

    // Durotar herb route staging — centered on the herb-dense area between
    // Razor Hill and Sen'jin Village.  Covers Peacebloom, Silverleaf, and Earthroot.
    public const float DurotarHerbRouteStartX = -500f;
    public const float DurotarHerbRouteStartY = -4800f;
    public const float DurotarHerbRouteStartZ = 38f;
    public const float DurotarHerbSearchRadius = 600f;
    public const int DurotarHerbQueryLimit = 64;

    public static List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> SelectValleyCopperVeinCandidates(
        IEnumerable<(uint entry, int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> spawns,
        uint nodeEntry,
        float maxDistance = ValleyCopperSearchRadius,
        int maxCandidates = int.MaxValue)
    {
        return SelectRouteCandidates(spawns, [nodeEntry], maxDistance, maxCandidates);
    }

    public static List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> SelectDurotarHerbCandidates(
        IEnumerable<(uint entry, int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> spawns,
        IReadOnlyCollection<uint> nodeEntries,
        float maxDistance = DurotarHerbSearchRadius,
        int maxCandidates = int.MaxValue)
    {
        return SelectRouteCandidates(spawns, nodeEntries, maxDistance, maxCandidates);
    }

    private static List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> SelectRouteCandidates(
        IEnumerable<(uint entry, int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> spawns,
        IReadOnlyCollection<uint> nodeEntries,
        float maxDistance,
        int maxCandidates)
    {
        return spawns
            .Where(spawn => nodeEntries.Contains(spawn.entry)
                            && spawn.map == DurotarMap
                            && spawn.distance2D <= maxDistance)
            .OrderBy(spawn => spawn.distance2D)
            .Take(maxCandidates)
            .Select(spawn => (spawn.map, spawn.x, spawn.y, spawn.z, spawn.distance2D, spawn.poolEntry, spawn.poolDescription))
            .ToList();
    }
}
