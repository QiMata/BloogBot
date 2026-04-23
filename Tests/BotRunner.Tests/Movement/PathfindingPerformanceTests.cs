using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.Movement;

[Trait("Category", "RequiresInfrastructure")]
public class PathfindingPerformanceTests(ITestOutputHelper output)
{
    // ── Kalimdor (MapId=1) coordinates ──
    private const uint MapId = 1;
    private static readonly Position ValleyOfTrials = new(-620f, -4385f, 44f);
    private static readonly Position OrgrimmarBank = new(1627f, -4376f, 14.81f);
    private static readonly Position OrgrimmarFM = new(1676f, -4313f, 64.72f);
    private static readonly Position RazorHill = new(340f, -4686f, 19.54f);
    private static readonly Position Ratchet = new(-957f, -3755f, 5f);

    private const int PathfindingServicePort = 5001;

    #region Unit-Level Benchmarks (DelegatePathfindingClient)

    [Fact]
    [UnitTest]
    public void GetNextWaypoint_PerTickCost_Under1Ms()
    {
        // Arrange: pre-computed 10-waypoint path
        var waypoints = Enumerable.Range(0, 10)
            .Select(i => new Position(i * 10f, 0f, 0f))
            .ToArray();

        var pathfinding = new DelegatePathfindingClient((_, _, _, _) => waypoints);

        long tick = 0;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var destination = new Position(90f, 0f, 0f);

        // Prime the path with an initial call
        navPath.GetNextWaypoint(new Position(0f, 0f, 0f), destination, MapId, allowDirectFallback: false);

        // Act: simulate 1000 consecutive GetNextWaypoint calls
        const int iterations = 1000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            // Simulate slight movement along the path
            var currentPos = new Position((i % 90) * 1f, 0f, 0f);
            navPath.GetNextWaypoint(currentPos, destination, MapId, allowDirectFallback: false);
            tick += 50; // 50ms per tick
        }
        sw.Stop();

        var averageMs = (double)sw.ElapsedMilliseconds / iterations;
        output.WriteLine($"GetNextWaypoint: {iterations} calls in {sw.ElapsedMilliseconds}ms (avg {averageMs:F4}ms/call)");

        // Assert: average per-call time < 1ms
        Assert.True(averageMs < 1.0,
            $"Average per-call time was {averageMs:F4}ms, expected < 1ms");
    }

    [Fact]
    [UnitTest]
    public void GetNextWaypoint_CooldownPreventsExcessiveRecalculations()
    {
        // Arrange: track GetPath calls
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) =>
            new[] { start, new Position((start.X + end.X) / 2, 0f, 0f), end });

        long tick = 0;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var currentPos = new Position(0f, 0f, 0f);

        // Act: call GetNextWaypoint 100 times with slight destination changes each time
        // Destination changes by >10y each call (RECALCULATE_DISTANCE) but cooldown should suppress most
        for (int i = 0; i < 100; i++)
        {
            // Change destination slightly each time but within cooldown window
            var dest = new Position(100f + (i * 0.5f), 0f, 0f);
            navPath.GetNextWaypoint(currentPos, dest, MapId, allowDirectFallback: false);
            tick += 10; // Only 10ms between calls — well within 2000ms cooldown
        }

        var totalGetPathCalls = pathfinding.LegacyGetPathCalls + pathfinding.OverlayGetPathCalls;
        output.WriteLine($"GetPath was called {totalGetPathCalls} times out of 100 GetNextWaypoint calls");

        // Assert: cooldown prevents excessive recalculations
        Assert.True(totalGetPathCalls <= 5,
            $"GetPath was called {totalGetPathCalls} times, expected <= 5 (cooldown should suppress most recalculations)");
    }

    [Fact]
    [UnitTest]
    public void GetNextWaypoint_LOSStringPull_SkipsIntermediateWaypoints()
    {
        // Arrange: 10 waypoints in a straight line — LOS always clear
        var waypoints = Enumerable.Range(0, 10)
            .Select(i => new Position(i * 10f, 0f, 0f))
            .ToArray();

        var losCalled = false;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => waypoints,
            isInLineOfSight: (_, _, _) =>
            {
                losCalled = true;
                return true; // All pairs have clear LOS
            });

        long tick = 0;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var currentPos = new Position(0f, 0f, 0f);
        var destination = new Position(90f, 0f, 0f);

        // Act
        var waypoint = navPath.GetNextWaypoint(currentPos, destination, MapId, allowDirectFallback: false);

        output.WriteLine($"Returned waypoint: ({waypoint?.X}, {waypoint?.Y}, {waypoint?.Z})");
        output.WriteLine($"LOS was queried: {losCalled}");

        // Assert: string-pull should skip intermediate waypoints when LOS is clear
        Assert.NotNull(waypoint);
        // The returned waypoint should NOT be waypoint[1] (the immediate next)
        // because LOS string-pull should allow skipping ahead
        if (losCalled)
        {
            // If LOS was queried and all pairs had clear LOS, the waypoint should be
            // further along than the immediate next waypoint
            Assert.True(waypoint!.X > waypoints[1].X,
                $"Expected string-pull to skip past waypoint[1] (X={waypoints[1].X}), got X={waypoint.X}");
            output.WriteLine($"String-pull successfully skipped to X={waypoint.X} (past waypoint[1] at X={waypoints[1].X})");
        }
        else
        {
            // LOS might not be queried on the first call if the path is being calculated;
            // in that case, advance tick and call again
            tick += 100;
            waypoint = navPath.GetNextWaypoint(currentPos, destination, MapId, allowDirectFallback: false);
            output.WriteLine($"Second call returned waypoint: ({waypoint?.X}, {waypoint?.Y}, {waypoint?.Z}), LOS queried: {losCalled}");
            Assert.True(losCalled, "LOS should have been queried for string-pull optimization");
        }
    }

    [Fact]
    [UnitTest]
    public void NavigationMetrics_TracksPathDuration()
    {
        // Arrange
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) =>
        {
            // Simulate a small computation delay
            System.Threading.Thread.Sleep(1);
            return new[] { start, end };
        });

        long tick = 0;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var currentPos = new Position(0f, 0f, 0f);
        var destination = new Position(100f, 0f, 0f);

        // Act: trigger path calculation
        navPath.GetNextWaypoint(currentPos, destination, MapId, allowDirectFallback: false);

        // Assert: metrics should record the path duration
        var metrics = navPath.Metrics;
        output.WriteLine($"LastPathDurationMs: {metrics.LastPathDurationMs}");
        output.WriteLine($"PathsCalculated: {metrics.PathsCalculated}");

        Assert.True(metrics.PathsCalculated >= 1, "At least one path should have been calculated");
        Assert.True(metrics.LastPathDurationMs >= 0,
            $"LastPathDurationMs should be >= 0, got {metrics.LastPathDurationMs}");
    }

    [Fact]
    [UnitTest]
    public void GetNextWaypoint_StallDetection_TriggersRecalculation()
    {
        // Arrange: track recalculation via GetPath call count. Use a blocked
        // corner scenario that stays outside the acceptance radius so repeated
        // same-position samples hit the stalled-near-waypoint recovery path.
        var pathCallCount = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            {
                pathCallCount++;
                return
                [
                    new Position(10f, 0f, 0f),
                    new Position(10f, 10f, 0f)
                ];
            },
            isInLineOfSight: (_, from, to) => !(from.Y < 0.5f && to.Y >= 7f));

        long tick = 0;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var currentPos = new Position(4f, 0f, 0f);
        var destination = new Position(10f, 20f, 0f);

        // Initial path calculation
        navPath.GetNextWaypoint(currentPos, destination, MapId, allowDirectFallback: false, minWaypointDistance: 4f);
        var initialPathCalls = pathCallCount;
        output.WriteLine($"Initial path calls: {initialPathCalls}");

        // Act: simulate being stuck at the same position for many ticks.
        for (int i = 0; i < 30; i++)
        {
            tick += 100; // 100ms per tick
            navPath.GetNextWaypoint(currentPos, destination, MapId, allowDirectFallback: false, minWaypointDistance: 4f);
        }

        var postStallPathCalls = pathCallCount;
        output.WriteLine($"Post-stall path calls: {postStallPathCalls} (delta: {postStallPathCalls - initialPathCalls})");
        output.WriteLine($"RecalculationsTriggered: {navPath.Metrics.RecalculationsTriggered}");

        // Assert: stall detection should have triggered at least one recalculation
        Assert.True(navPath.Metrics.RecalculationsTriggered >= 1,
            $"Expected at least 1 recalculation from stall detection, got {navPath.Metrics.RecalculationsTriggered}");
    }

    #endregion

    #region Live Integration Benchmarks (PathfindingService on port 5001)

    private static bool IsPathfindingServiceAvailable()
    {
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", PathfindingServicePort);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PathfindingClient CreateLiveClient()
        => new("127.0.0.1", PathfindingServicePort, NullLogger.Instance);

    [SkippableFact]
    [IntegrationTest]
    [Trait(TestCategories.RequiresService, TestCategories.PathfindingService)]
    public void LivePath_ShortRoute_Under500ms()
    {
        global::Tests.Infrastructure.Skip.If(!IsPathfindingServiceAvailable(), "PathfindingService not available on port 5001");

        using var client = CreateLiveClient();
        var nearValley = new Position(-600f, -4370f, 44f);

        var sw = Stopwatch.StartNew();
        var path = client.GetPath(MapId, ValleyOfTrials, nearValley);
        sw.Stop();

        output.WriteLine($"Short route ({ValleyOfTrials} -> {nearValley}): {sw.ElapsedMilliseconds}ms, {path.Length} waypoints");

        Assert.True(path.Length > 0, "Path should have at least one waypoint");
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Short route took {sw.ElapsedMilliseconds}ms, expected < 500ms");
    }

    [SkippableFact]
    [IntegrationTest]
    [Trait(TestCategories.RequiresService, TestCategories.PathfindingService)]
    public void LivePath_MediumRoute_Under2s()
    {
        global::Tests.Infrastructure.Skip.If(!IsPathfindingServiceAvailable(), "PathfindingService not available on port 5001");

        using var client = CreateLiveClient();

        var sw = Stopwatch.StartNew();
        var path = client.GetPath(MapId, OrgrimmarBank, OrgrimmarFM);
        sw.Stop();

        output.WriteLine($"Medium route (Org bank -> Org FM): {sw.ElapsedMilliseconds}ms, {path.Length} waypoints");

        Assert.True(path.Length > 0, "Path should have at least one waypoint");
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Medium route took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
    }

    [SkippableFact]
    [IntegrationTest]
    [Trait(TestCategories.RequiresService, TestCategories.PathfindingService)]
    public void LivePath_LongRoute_Under5s()
    {
        global::Tests.Infrastructure.Skip.If(!IsPathfindingServiceAvailable(), "PathfindingService not available on port 5001");

        using var client = CreateLiveClient();

        var sw = Stopwatch.StartNew();
        var path = client.GetPath(MapId, ValleyOfTrials, OrgrimmarBank);
        sw.Stop();

        output.WriteLine($"Long route (Valley of Trials -> Org bank): {sw.ElapsedMilliseconds}ms, {path.Length} waypoints");

        Assert.True(path.Length > 0, "Path should have at least one waypoint");
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Long route took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    [SkippableFact]
    [IntegrationTest]
    [Trait(TestCategories.RequiresService, TestCategories.PathfindingService)]
    public void LivePath_ThroughputTest_10PathsUnder10s()
    {
        global::Tests.Infrastructure.Skip.If(!IsPathfindingServiceAvailable(), "PathfindingService not available on port 5001");

        using var client = CreateLiveClient();

        var routes = new (Position start, Position end, string name)[]
        {
            (ValleyOfTrials, RazorHill, "Valley->Razor Hill"),
            (RazorHill, ValleyOfTrials, "Razor Hill->Valley"),
            (OrgrimmarBank, OrgrimmarFM, "Org bank->Org FM"),
            (OrgrimmarFM, OrgrimmarBank, "Org FM->Org bank"),
            (RazorHill, Ratchet, "Razor Hill->Ratchet"),
            (Ratchet, RazorHill, "Ratchet->Razor Hill"),
            (ValleyOfTrials, new Position(-580f, -4350f, 40f), "Valley short 1"),
            (ValleyOfTrials, new Position(-650f, -4400f, 48f), "Valley short 2"),
            (RazorHill, new Position(300f, -4650f, 20f), "Razor Hill short 1"),
            (OrgrimmarBank, new Position(1650f, -4350f, 15f), "Org bank short"),
        };

        var totalSw = Stopwatch.StartNew();
        var timings = new long[routes.Length];

        for (int i = 0; i < routes.Length; i++)
        {
            var sw = Stopwatch.StartNew();
            var path = client.GetPath(MapId, routes[i].start, routes[i].end);
            sw.Stop();
            timings[i] = sw.ElapsedMilliseconds;
            output.WriteLine($"  [{i + 1}/10] {routes[i].name}: {sw.ElapsedMilliseconds}ms, {path.Length} waypoints");
        }

        totalSw.Stop();
        var avgMs = timings.Average();
        output.WriteLine($"Total: {totalSw.ElapsedMilliseconds}ms, Average: {avgMs:F1}ms");

        Assert.True(totalSw.ElapsedMilliseconds < 10_000,
            $"10 paths took {totalSw.ElapsedMilliseconds}ms, expected < 10000ms");
    }

    [SkippableFact]
    [IntegrationTest]
    [Trait(TestCategories.RequiresService, TestCategories.PathfindingService)]
    public void LiveGroundZ_BatchVsSingle_BatchIsFaster()
    {
        global::Tests.Infrastructure.Skip.If(!IsPathfindingServiceAvailable(), "PathfindingService not available on port 5001");

        using var client = CreateLiveClient();

        // Generate 20 positions in a grid near Valley of Trials
        var positions = new Position[20];
        for (int i = 0; i < 20; i++)
        {
            positions[i] = new Position(
                ValleyOfTrials.X + (i % 5) * 5f,
                ValleyOfTrials.Y + (i / 5) * 5f,
                ValleyOfTrials.Z);
        }

        // Batch query
        var batchSw = Stopwatch.StartNew();
        var batchResults = client.BatchGetGroundZ(MapId, positions);
        batchSw.Stop();

        // Individual queries
        var singleSw = Stopwatch.StartNew();
        var singleResults = new (float groundZ, bool found)[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            singleResults[i] = WoWSharpClient.Movement.NativeLocalPhysics.GetGroundZ(MapId, p.X, p.Y, p.Z, 50f);
        }
        singleSw.Stop();

        output.WriteLine($"Batch (20 positions): {batchSw.ElapsedMilliseconds}ms");
        output.WriteLine($"Individual (20 queries): {singleSw.ElapsedMilliseconds}ms");
        output.WriteLine($"Batch speedup: {(double)singleSw.ElapsedMilliseconds / Math.Max(1, batchSw.ElapsedMilliseconds):F2}x");

        Assert.Equal(positions.Length, batchResults.Length);
        Assert.True(batchSw.ElapsedMilliseconds <= singleSw.ElapsedMilliseconds,
            $"Batch ({batchSw.ElapsedMilliseconds}ms) should be faster than individual ({singleSw.ElapsedMilliseconds}ms)");
    }

    [SkippableFact]
    [IntegrationTest]
    [Trait(TestCategories.RequiresService, TestCategories.PathfindingService)]
    public void LiveLOS_RapidQueries_Under50msEach()
    {
        global::Tests.Infrastructure.Skip.If(!IsPathfindingServiceAvailable(), "PathfindingService not available on port 5001");

        using var client = CreateLiveClient();

        // 20 LOS queries between nearby points in Valley of Trials
        const int queryCount = 20;
        var timings = new long[queryCount];

        for (int i = 0; i < queryCount; i++)
        {
            var from = new Position(
                ValleyOfTrials.X + i * 2f,
                ValleyOfTrials.Y,
                ValleyOfTrials.Z);
            var to = new Position(
                ValleyOfTrials.X + i * 2f + 10f,
                ValleyOfTrials.Y + 5f,
                ValleyOfTrials.Z);

            var sw = Stopwatch.StartNew();
            var inLos = WoWSharpClient.Movement.NativeLocalPhysics.LineOfSight(MapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);
            sw.Stop();

            timings[i] = sw.ElapsedMilliseconds;
            output.WriteLine($"  LOS query [{i + 1}/{queryCount}]: {sw.ElapsedMilliseconds}ms (result={inLos})");
        }

        var avgMs = timings.Average();
        var maxMs = timings.Max();
        output.WriteLine($"LOS Average: {avgMs:F1}ms, Max: {maxMs}ms");

        Assert.True(avgMs < 50,
            $"Average LOS query time was {avgMs:F1}ms, expected < 50ms");
    }

    #endregion

    #region DelegatePathfindingClient (test double)

    /// <summary>
    /// Test double for PathfindingClient that delegates to provided functions.
    /// Copied from NavigationPathTests since the original is private.
    /// </summary>
    private sealed class DelegatePathfindingClient : PathfindingClient
    {
        private readonly Func<uint, Position, Position, bool, Position[]> _getPath;
        private readonly Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Position[]>? _getPathWithNearbyObjects;
        private readonly Func<uint, Position, float, (bool onNavmesh, Position nearestPoint)> _isPointOnNavmesh;
        private readonly Func<uint, Position, float, (uint areaType, Position nearestPoint)> _findNearestWalkablePoint;
        private readonly Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Race, Gender, PathfindingRouteResult>? _getPathResult;

        public DelegatePathfindingClient(
            Func<uint, Position, Position, bool, Position[]> getPath,
            Func<uint, Position, Position, bool>? isInLineOfSight = null,
            Func<uint, Position, float, (float, bool)>? getGroundZ = null,
            Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Position[]>? getPathWithNearbyObjects = null,
            Func<uint, Position, float, (bool onNavmesh, Position nearestPoint)>? isPointOnNavmesh = null,
            Func<uint, Position, float, (uint areaType, Position nearestPoint)>? findNearestWalkablePoint = null,
            Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Race, Gender, PathfindingRouteResult>? getPathResult = null)
        {
            _getPath = getPath;
            _getPathWithNearbyObjects = getPathWithNearbyObjects;
            _isPointOnNavmesh = isPointOnNavmesh ?? ((_, position, _) => (true, position));
            _getPathResult = getPathResult;

            // GroundZ + LOS now go straight to NativeLocalPhysics; install
            // per-test overrides so the production code under test still
            // observes the mocked behavior.
            var losDelegate = isInLineOfSight ?? ((_, _, _) => true);
            WoWSharpClient.Movement.NativeLocalPhysics.TestLineOfSightOverride =
                (mapId, fx, fy, fz, tx, ty, tz) => losDelegate(mapId, new Position(fx, fy, fz), new Position(tx, ty, tz));

            if (getGroundZ != null)
                WoWSharpClient.Movement.NativeLocalPhysics.TestGetGroundZOverride =
                    (mapId, x, y, z, maxDist) => getGroundZ(mapId, new Position(x, y, z), maxDist);
            else
                WoWSharpClient.Movement.NativeLocalPhysics.TestGetGroundZOverride =
                    (mapId, x, y, z, _) => (z, true);

            _findNearestWalkablePoint = findNearestWalkablePoint ?? ((_, position, _) => (1u, position));
        }

        public int LegacyGetPathCalls { get; private set; }
        public int OverlayGetPathCalls { get; private set; }
        public IReadOnlyList<DynamicObjectProto>? LastNearbyObjects { get; private set; }

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
        {
            LegacyGetPathCalls++;
            return _getPath(mapId, start, end, smoothPath);
        }

        public override Position[] GetPath(uint mapId, Position start, Position end, IReadOnlyList<DynamicObjectProto>? nearbyObjects, bool smoothPath = false, Race race = 0, Gender gender = 0)
        {
            OverlayGetPathCalls++;
            LastNearbyObjects = nearbyObjects?.ToArray();
            return _getPathWithNearbyObjects?.Invoke(mapId, start, end, nearbyObjects, smoothPath)
                ?? _getPath(mapId, start, end, smoothPath);
        }

        public override PathfindingRouteResult GetPathResult(uint mapId, Position start, Position end, IReadOnlyList<DynamicObjectProto>? nearbyObjects, bool smoothPath = false, Race race = 0, Gender gender = 0)
        {
            if (_getPathResult is not null)
                return _getPathResult(mapId, start, end, nearbyObjects, smoothPath, race, gender);

            Position[] corners;
            if (nearbyObjects != null)
            {
                OverlayGetPathCalls++;
                LastNearbyObjects = nearbyObjects.ToArray();
                corners = _getPathWithNearbyObjects?.Invoke(mapId, start, end, nearbyObjects, smoothPath)
                    ?? _getPath(mapId, start, end, smoothPath);
            }
            else
            {
                LegacyGetPathCalls++;
                corners = _getPath(mapId, start, end, smoothPath);
            }

            return new PathfindingRouteResult(
                Corners: corners,
                Result: corners.Length > 0 ? "native_path" : "no_path",
                RawCornerCount: (uint)corners.Length,
                BlockedSegmentIndex: null,
                BlockedReason: "none",
                MaxAffordance: PathSegmentAffordance.Walk,
                PathSupported: corners.Length > 0,
                StepUpCount: 0,
                DropCount: 0,
                CliffCount: 0,
                VerticalCount: 0,
                TotalZGain: 0f,
                TotalZLoss: 0f,
                MaxSlopeAngleDeg: 0f,
                JumpGapCount: 0,
                SafeDropCount: 0,
                UnsafeDropCount: 0,
                BlockedCount: 0,
                MaxClimbHeight: 0f,
                MaxGapDistance: 0f,
                MaxDropHeight: 0f);
        }

        public override (bool onNavmesh, Position nearestPoint) IsPointOnNavmesh(uint mapId, Position position, float searchRadius = 4.0f)
            => _isPointOnNavmesh(mapId, position, searchRadius);

        public override (uint areaType, Position nearestPoint) FindNearestWalkablePoint(uint mapId, Position position, float searchRadius = 8.0f)
            => _findNearestWalkablePoint(mapId, position, searchRadius);
    }

    #endregion
}
