using System.Linq;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

public class FishingPoolStagePlannerTests
{
    [Fact]
    public void SelectPoolUpdates_PrefersLocalDistinctChildPools()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180655, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 26.6f, 2619u, "child"),
            new(180655, 1, -969.8f, -3805.1f, 0f, 45.2f, 2627u, "child"),
            new(180582, 1, -975.7f, -3835.2f, 0f, 75.7f, 2626u, "child")
        ];

        var poolUpdates = FishingPoolStagePlanner.SelectPoolUpdates(
            spawns,
            localSpawnDistance: 50f,
            masterPoolEntry: 2628u,
            maxPoolUpdates: 8);

        Assert.Equal([2620u, 2619u, 2627u], poolUpdates);
    }

    [Fact]
    public void SelectPoolUpdates_FallsBackToMasterPoolWhenChildPoolsMissing()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -975.7f, -3835.2f, 0f, 75.7f, null, null)
        ];

        var poolUpdates = FishingPoolStagePlanner.SelectPoolUpdates(
            spawns,
            localSpawnDistance: 50f,
            masterPoolEntry: 2628u,
            maxPoolUpdates: 8);

        Assert.Equal([2628u], poolUpdates);
    }

    [Fact]
    public void SelectRemainingPoolUpdates_RemovesAlreadyAttemptedLocalPools()
    {
        var remainingPools = FishingPoolStagePlanner.SelectRemainingPoolUpdates(
            allPoolEntries: [2607u, 2608u, 2617u, 2618u, 2619u, 2620u, 2621u, 2626u, 2627u],
            attemptedPoolEntries: [2628u, 2617u, 2618u, 2619u, 2620u, 2621u, 2626u, 2627u]);

        Assert.Equal([2607u, 2608u], remainingPools);
    }

    [Fact]
    public void BuildPoolRefreshPlan_PutsNearbyChildrenFirstAndMasterLast()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 26.6f, 2619u, "child"),
            new(180655, 1, -969.8f, -3805.1f, 0f, 45.2f, 2627u, "child"),
            new(180655, 1, -975.7f, -3835.2f, 0f, 75.7f, 2626u, "child")
        ];

        var refreshPlan = FishingPoolStagePlanner.BuildPoolRefreshPlan(
            spawns,
            preferredSpawnDistance: 80f,
            masterPoolEntry: 2628u,
            maxPoolUpdates: 8);

        Assert.Equal([2620u, 2619u, 2627u, 2626u, 2628u], refreshPlan);
    }

    [Fact]
    public void BuildPoolRefreshPlan_IncludesSeventhNearbyRatchetChildBeforeMasterFallback()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 26.6f, 2619u, "child"),
            new(180655, 1, -969.8f, -3805.1f, 0f, 45.2f, 2627u, "child"),
            new(180582, 1, -1001.7f, -3733.5f, 0f, 61.6f, 2618u, "child"),
            new(180655, 1, -975.7f, -3835.2f, 0f, 73.0f, 2626u, "child"),
            new(180582, 1, -1012.0f, -3808.3f, 0f, 74.6f, 2617u, "child"),
            new(180655, 1, -872.8f, -3814.7f, 0f, 90.8f, 2621u, "child")
        ];

        var refreshPlan = FishingPoolStagePlanner.BuildPoolRefreshPlan(
            spawns,
            preferredSpawnDistance: 140f,
            masterPoolEntry: 2628u,
            maxPoolUpdates: 8);

        Assert.Equal([2620u, 2619u, 2627u, 2618u, 2626u, 2617u, 2621u, 2628u], refreshPlan);
    }

    [Fact]
    public void BuildPoolRefreshPlan_UsesNearStageDistanceToExcludeFarRatchetChildren()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 14.0f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 39.9f, 2619u, "child"),
            new(180655, 1, -969.8f, -3805.1f, 0f, 43.1f, 2627u, "child"),
            new(180655, 1, -1001.7f, -3733.5f, 0f, 61.6f, 2618u, "child"),
            new(180582, 1, -975.7f, -3835.2f, 0f, 73.0f, 2626u, "child"),
            new(180655, 1, -1012.0f, -3808.3f, 0f, 74.6f, 2617u, "child"),
            new(180582, 1, -872.8f, -3814.7f, 0f, 90.8f, 2621u, "child")
        ];

        var refreshPlan = FishingPoolStagePlanner.BuildPoolRefreshPlan(
            spawns,
            preferredSpawnDistance: 50f,
            masterPoolEntry: 2628u,
            maxPoolUpdates: 8);

        Assert.Equal([2620u, 2619u, 2627u, 2628u], refreshPlan);
    }

    [Fact]
    public void CreateSearchWaypoints_KeepsStageSearchLocalWhenNearbySpawnsExist()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180655, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 26.6f, 2619u, "child"),
            new(180655, 1, -1012.0f, -3808.3f, 0f, 65.9f, 2617u, "child")
        ];

        var waypoints = FishingPoolStagePlanner.CreateSearchWaypoints(
            spawns,
            stageX: -967.2f,
            stageY: -3760.0f,
            anchorZ: 5f,
            localSpawnDistance: 50f,
            waypointCount: 6,
            standoffDistance: 8f);

        Assert.Equal(2, waypoints.Count);
        Assert.Equal(5f, waypoints[0].z);
        Assert.All(waypoints, waypoint => Assert.True(waypoint.x > -990f));
    }

    [Fact]
    public void CreatePrioritizedSearchWaypoints_KeepsNearStageWaypointsAheadOfFarSpawnedPools()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 26.6f, 2619u, "child"),
            new(180655, 1, -1001.7f, -3733.5f, 0f, 61.6f, 2618u, "child"),
            new(180655, 1, -872.8f, -3814.7f, 0f, 90.8f, 2621u, "child")
        ];

        var waypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
            spawns,
            prioritizedPoolEntries: [2621u],
            stageX: -949.932f,
            stageY: -3766.883f,
            anchorZ: 5f,
            localSpawnDistance: 50f,
            waypointCount: 4,
            standoffDistance: 8f);

        Assert.Equal(3, waypoints.Count);
        Assert.True(waypoints[0].x < -940f);
        Assert.True(waypoints[1].x < -940f);
        Assert.True(waypoints[2].x > -940f);
    }

    [Fact]
    public void CreatePrioritizedSearchWaypoints_PrefersLocalSpawnedPoolWaypointsBeforeFarMatches()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 21.4f, 2620u, "child"),
            new(180582, 1, -1012.0f, -3808.3f, 0f, 74.6f, 2620u, "child"),
            new(180655, 1, -988.9f, -3775.5f, 0f, 26.6f, 2619u, "child")
        ];

        var waypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
            spawns,
            prioritizedPoolEntries: [2620u],
            stageX: -949.932f,
            stageY: -3766.883f,
            anchorZ: 5f,
            localSpawnDistance: 50f,
            waypointCount: 4,
            standoffDistance: 8f);

        Assert.Equal(2, waypoints.Count);
        Assert.True(waypoints[0].x > -980f);
        Assert.True(waypoints[1].x > -995f);
    }

    [Fact]
    public void CreatePrioritizedSearchWaypoints_KeepsCloserStageLocalProbeAheadOfFartherPrioritizedLocalSpawn()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -957.2f, -3778.9f, 0f, 14.0f, 2620u, "child"),
            new(180582, 1, -988.9f, -3775.5f, 0f, 39.9f, 2619u, "child"),
            new(180655, 1, -969.8f, -3805.1f, 0f, 43.1f, 2627u, "child"),
            new(180655, 1, -1001.7f, -3733.5f, 0f, 61.6f, 2618u, "child"),
            new(180582, 1, -1012.0f, -3808.3f, 0f, 74.6f, 2617u, "child"),
            new(180582, 1, -872.8f, -3814.7f, 0f, 90.8f, 2621u, "child")
        ];

        var waypoints = FishingPoolStagePlanner.CreatePrioritizedSearchWaypoints(
            spawns,
            prioritizedPoolEntries: [2617u, 2618u, 2621u, 2627u],
            stageX: -949.932f,
            stageY: -3766.883f,
            anchorZ: 3.949f,
            localSpawnDistance: 50f,
            waypointCount: 4,
            standoffDistance: 8f);

        Assert.True(waypoints.Count >= 3);
        Assert.InRange(waypoints[0].x, -954f, -952f);
        Assert.InRange(waypoints[0].y, -3773f, -3771f);
        Assert.InRange(waypoints[1].x, -970f, -968f);
        Assert.InRange(waypoints[1].y, -3772f, -3770f);
        Assert.InRange(waypoints[2].x, -960f, -958f);
        Assert.InRange(waypoints[2].y, -3786f, -3783f);
    }

    [Fact]
    public void CreateSearchWaypoints_CapsFarProbeTravelToKeepStageSearchLocal()
    {
        FishingPoolSpawn[] spawns =
        [
            new(180582, 1, -1012.0f, -3808.3f, 0f, 74.6f, 2617u, "child")
        ];

        var waypoints = FishingPoolStagePlanner.CreateSearchWaypoints(
            spawns,
            stageX: -949.932f,
            stageY: -3766.883f,
            anchorZ: 5f,
            localSpawnDistance: 50f,
            waypointCount: 4,
            standoffDistance: 8f);

        Assert.Single(waypoints);
        var travelDistance = MathF.Sqrt(
            ((waypoints[0].x - (-949.932f)) * (waypoints[0].x - (-949.932f))) +
            ((waypoints[0].y - (-3766.883f)) * (waypoints[0].y - (-3766.883f))));
        Assert.InRange(travelDistance, 19.5f, 20.5f);
    }
}
