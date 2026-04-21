using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tests.LiveValidation;

internal sealed record FishingPoolSpawn(
    uint Entry,
    int Map,
    float X,
    float Y,
    float Z,
    float Distance2D,
    uint? PoolEntry,
    string? PoolDescription);

internal sealed record FishingSearchWaypoint(
    float X,
    float Y,
    float Z,
    float StageDistance2D,
    bool IsPrioritized);

internal static class FishingPoolStagePlanner
{
    private const float MaxStageProbeTravelDistance = 20f;

    public static IReadOnlyList<uint> BuildPoolRefreshPlan(
        IReadOnlyList<FishingPoolSpawn> spawns,
        float preferredSpawnDistance,
        uint masterPoolEntry,
        int maxPoolUpdates)
    {
        var childPools = SelectPoolUpdates(spawns, preferredSpawnDistance, masterPoolEntry, maxPoolUpdates);
        if (childPools.Count == 1 && childPools[0] == masterPoolEntry)
            return childPools;

        var refreshPlan = new List<uint>(maxPoolUpdates);
        refreshPlan.AddRange(childPools.Where(poolEntry => poolEntry != masterPoolEntry));
        refreshPlan.Add(masterPoolEntry);
        return refreshPlan
            .Distinct()
            .Take(maxPoolUpdates)
            .ToList();
    }

    public static IReadOnlyList<FishingPoolSpawn> MaterializeSpawns(
        IEnumerable<(uint entry, int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> spawns)
        => spawns
            .Select(spawn => new FishingPoolSpawn(
                spawn.entry,
                spawn.map,
                spawn.x,
                spawn.y,
                spawn.z,
                spawn.distance2D,
                spawn.poolEntry,
                spawn.poolDescription))
            .ToList();

    public static IReadOnlyList<uint> SelectRemainingPoolUpdates(
        IEnumerable<uint> allPoolEntries,
        IEnumerable<uint> attemptedPoolEntries)
    {
        var attempted = attemptedPoolEntries
            .Distinct()
            .ToHashSet();

        return allPoolEntries
            .Distinct()
            .Where(poolEntry => !attempted.Contains(poolEntry))
            .OrderBy(poolEntry => poolEntry)
            .ToList();
    }

    public static IReadOnlyList<uint> SelectPoolUpdates(
        IReadOnlyList<FishingPoolSpawn> spawns,
        float localSpawnDistance,
        uint masterPoolEntry,
        int maxPoolUpdates)
    {
        var localPools = spawns
            .Where(spawn => spawn.Distance2D <= localSpawnDistance)
            .Select(spawn => spawn.PoolEntry)
            .Where(poolEntry => poolEntry.HasValue)
            .Select(poolEntry => poolEntry!.Value)
            .Distinct()
            .Take(maxPoolUpdates)
            .ToList();

        if (localPools.Count > 0)
            return localPools;

        var anyPools = spawns
            .Select(spawn => spawn.PoolEntry)
            .Where(poolEntry => poolEntry.HasValue)
            .Select(poolEntry => poolEntry!.Value)
            .Distinct()
            .Take(maxPoolUpdates)
            .ToList();

        return anyPools.Count > 0 ? anyPools : [masterPoolEntry];
    }

    public static IReadOnlyList<(float x, float y, float z)> CreateSearchWaypoints(
        IReadOnlyList<FishingPoolSpawn> spawns,
        float stageX,
        float stageY,
        float anchorZ,
        float localSpawnDistance,
        int waypointCount,
        float standoffDistance)
    {
        return CreateWaypointCandidates(
                SelectWaypointSource(spawns, localSpawnDistance),
                stageX,
                stageY,
                anchorZ,
                standoffDistance,
                isPrioritized: false)
            .Select(waypoint => (waypoint.X, waypoint.Y, waypoint.Z))
            .Take(waypointCount)
            .ToList();
    }

    public static IReadOnlyList<(float x, float y, float z)> CreatePrioritizedSearchWaypoints(
        IReadOnlyList<FishingPoolSpawn> spawns,
        IReadOnlyCollection<uint> prioritizedPoolEntries,
        float stageX,
        float stageY,
        float anchorZ,
        float localSpawnDistance,
        int waypointCount,
        float standoffDistance)
    {
        var defaultWaypoints = CreateWaypointCandidates(
            SelectWaypointSource(spawns, localSpawnDistance),
            stageX,
            stageY,
            anchorZ,
            standoffDistance,
            isPrioritized: false);

        if (prioritizedPoolEntries.Count == 0)
        {
            return defaultWaypoints
                .Select(waypoint => (waypoint.X, waypoint.Y, waypoint.Z))
                .Take(waypointCount)
                .ToList();
        }

        var prioritizedSpawns = spawns
            .Where(spawn => spawn.PoolEntry.HasValue && prioritizedPoolEntries.Contains(spawn.PoolEntry.Value))
            .ToList();
        if (prioritizedSpawns.Count == 0)
        {
            return defaultWaypoints
                .Select(waypoint => (waypoint.X, waypoint.Y, waypoint.Z))
                .Take(waypointCount)
                .ToList();
        }

        var prioritizedLocalSpawns = prioritizedSpawns
            .Where(spawn => spawn.Distance2D <= localSpawnDistance)
            .ToList();
        if (prioritizedLocalSpawns.Count == 0 && defaultWaypoints.Count > 0)
        {
            var fallbackPrioritizedWaypoints = CreateWaypointCandidates(
                SelectWaypointSource(prioritizedSpawns, localSpawnDistance),
                stageX,
                stageY,
                anchorZ,
                standoffDistance,
                isPrioritized: true);

            return defaultWaypoints
                .Concat(fallbackPrioritizedWaypoints)
                .DistinctBy(waypoint => $"{waypoint.X:F1}:{waypoint.Y:F1}")
                .Select(waypoint => (waypoint.X, waypoint.Y, waypoint.Z))
                .Take(waypointCount)
                .ToList();
        }

        var prioritizedWaypoints = CreateWaypointCandidates(
            SelectWaypointSource(prioritizedLocalSpawns.Count > 0 ? prioritizedLocalSpawns : prioritizedSpawns, localSpawnDistance),
            stageX,
            stageY,
            anchorZ,
            standoffDistance,
            isPrioritized: true);

        return defaultWaypoints
            .Concat(prioritizedWaypoints)
            .OrderBy(waypoint => waypoint.StageDistance2D)
            .ThenByDescending(waypoint => waypoint.IsPrioritized)
            .DistinctBy(waypoint => $"{waypoint.X:F1}:{waypoint.Y:F1}")
            .Select(waypoint => (waypoint.X, waypoint.Y, waypoint.Z))
            .Take(waypointCount)
            .ToList();
    }

    private static IReadOnlyList<FishingPoolSpawn> SelectWaypointSource(
        IReadOnlyList<FishingPoolSpawn> spawns,
        float localSpawnDistance)
    {
        var localSpawns = spawns
            .Where(spawn => spawn.Distance2D <= localSpawnDistance)
            .ToList();
        return localSpawns.Count > 0
            ? localSpawns
            : spawns;
    }

    private static IReadOnlyList<FishingSearchWaypoint> CreateWaypointCandidates(
        IReadOnlyList<FishingPoolSpawn> spawns,
        float stageX,
        float stageY,
        float anchorZ,
        float standoffDistance,
        bool isPrioritized)
    {
        return spawns
            .OrderBy(spawn => Distance2D(stageX, stageY, spawn.X, spawn.Y))
            .GroupBy(spawn => $"{spawn.X:F1}:{spawn.Y:F1}")
            .Select(group =>
            {
                var spawn = group.First();
                var waypoint = CreateWaypoint(spawn, stageX, stageY, anchorZ, standoffDistance);
                return new FishingSearchWaypoint(
                    waypoint.x,
                    waypoint.y,
                    waypoint.z,
                    Distance2D(stageX, stageY, spawn.X, spawn.Y),
                    isPrioritized);
            })
            .DistinctBy(waypoint => $"{waypoint.X:F1}:{waypoint.Y:F1}")
            .ToList();
    }

    private static (float x, float y, float z) CreateWaypoint(
        FishingPoolSpawn spawn,
        float stageX,
        float stageY,
        float anchorZ,
        float standoffDistance)
    {
        var towardSpawnX = spawn.X - stageX;
        var towardSpawnY = spawn.Y - stageY;
        var planarDistance = MathF.Sqrt((towardSpawnX * towardSpawnX) + (towardSpawnY * towardSpawnY));
        if (planarDistance < 0.01f)
            return (stageX, stageY, anchorZ);

        var usableTravelDistance = Math.Max(0f, planarDistance - standoffDistance);
        var stepScale = Math.Min(usableTravelDistance, MaxStageProbeTravelDistance) / planarDistance;
        return (
            stageX + (towardSpawnX * stepScale),
            stageY + (towardSpawnY * stepScale),
            anchorZ);
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
