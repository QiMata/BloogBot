using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BotRunner.Tests.LiveValidation;

internal static class GatheringRouteSelection
{
    private static readonly Regex PoolSpawnCoordinatePattern = new(
        @"\(Pool\s+(?<pool>\d+)\).*?\bX:(?<x>[+-]?\d+(?:\.\d+)?)\s+Y:(?<y>[+-]?\d+(?:\.\d+)?)\s+Z:(?<z>[+-]?\d+(?:\.\d+)?)\s+MapId:(?<map>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public const int DurotarMap = 1;

    // Valley of Trials route staging for the copper-vein pathing probe. Keep
    // this centered on a real copper-row ground surface; (-800,-4500) is on a
    // high terrain layer and `.go xyz` there causes vertical recovery noise.
    public const float ValleyCopperRouteStartX = -1000f;
    public const float ValleyCopperRouteStartY = -4500f;
    public const float ValleyCopperRouteStartZ = 28.5f;
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

    public static List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> PrioritizeSpawnedPools(
        IEnumerable<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> candidates,
        IReadOnlyCollection<uint> spawnedPoolEntries)
    {
        var materialized = candidates.ToList();
        if (materialized.Count == 0 || spawnedPoolEntries.Count == 0)
            return materialized;

        var spawned = spawnedPoolEntries.ToHashSet();
        return materialized
            .OrderBy(candidate => candidate.poolEntry.HasValue && spawned.Contains(candidate.poolEntry.Value) ? 0 : 1)
            .ThenBy(candidate => candidate.distance2D)
            .ToList();
    }

    public static List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> SelectActivePoolSpawnCandidates(
        IEnumerable<string> poolSpawnResponses,
        float originX,
        float originY,
        float maxDistance,
        int maxCandidates = int.MaxValue)
    {
        var candidates = new List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)>();
        var seen = new HashSet<(int map, int x, int y, int z, uint? poolEntry)>();

        foreach (var response in poolSpawnResponses.Where(response => !string.IsNullOrWhiteSpace(response)))
        {
            foreach (Match match in PoolSpawnCoordinatePattern.Matches(response))
            {
                if (!TryReadPoolSpawn(match, originX, originY, out var candidate))
                    continue;

                if (candidate.map != DurotarMap || candidate.distance2D > maxDistance)
                    continue;

                var key = (
                    candidate.map,
                    x: (int)MathF.Round(candidate.x * 10f),
                    y: (int)MathF.Round(candidate.y * 10f),
                    z: (int)MathF.Round(candidate.z * 10f),
                    candidate.poolEntry);
                if (seen.Add(key))
                    candidates.Add(candidate);
            }
        }

        return candidates
            .OrderBy(candidate => candidate.distance2D)
            .Take(maxCandidates)
            .ToList();
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

    private static bool TryReadPoolSpawn(
        Match match,
        float originX,
        float originY,
        out (int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription) candidate)
    {
        candidate = default;

        if (!uint.TryParse(match.Groups["pool"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var poolEntry)
            || !int.TryParse(match.Groups["map"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var map)
            || !float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        var distance2D = Distance2D(originX, originY, x, y);
        candidate = (map, x, y, z, distance2D, poolEntry, $"active pool spawn {poolEntry}");
        return true;
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
