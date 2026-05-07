using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Pathfinding;
using PathfindingService.Repository;
using PathfindingService.RoutePacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace PathfindingService.Tests;

public class LongPathingRouteTests(NavigationFixture fixture, ITestOutputHelper output) : IClassFixture<NavigationFixture>
{
    private const uint Kalimdor = 1;
    private const uint EasternKingdoms = 0;
    private static readonly object ProductionRoutePackLock = new();
    private static StaticRoutePackCache? s_productionRoutePackCache;
    private static readonly (float Radius, float Height) TaurenMaleCapsule =
        RaceDimensions.GetCapsuleForRace(Race.Tauren, Gender.Male);
    private static readonly Position OrgrimmarUndercityZeppelinDeckApproachPoint =
        new(1338.1f, -4646.0f, 51.6f);
    private static readonly Position OrgrimmarUndercityZeppelinBoardingPoint =
        new(1320.142944f, -4653.158691f, 53.891945f);
    private static readonly Position OrgrimmarUndercityZeppelinDeckBoardingPoint =
        OrgrimmarUndercityZeppelinBoardingPoint;

    private readonly Navigation _navigation = fixture.Navigation;
    private readonly ITestOutputHelper _output = output;

    public static IEnumerable<object[]> CriticalWalkableLegs()
    {
        yield return
        [
            "orgrimmar_flight_master_tower_descent",
            Kalimdor,
            new Position(1677.0f, -4315.0f, 62.0f),
            new Position(1604.8f, -4425.6f, 10.36f),
            8.0f,
            0.0f,
            0,
            12.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];

        yield return
        [
            "orgrimmar_flight_master_to_zeppelin_tower_full_route",
            Kalimdor,
            new Position(1677.6f, -4315.7f, 61.2f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            0.0f,
            0,
            12.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];

        yield return
        [
            "orgrimmar_city_to_zeppelin_tower_lower_approach",
            Kalimdor,
            new Position(1604.8f, -4425.6f, 10.36f),
            new Position(1356.8f, -4501.3f, 29.44f),
            8.0f,
            0.0f,
            0,
            14.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];

        yield return
        [
            "orgrimmar_flight_master_tower_descent_live_stall_recovery",
            Kalimdor,
            new Position(1673.1f, -4333.5f, 53.7f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_flight_master_tower_hover_stall_exact_live_recovery",
            Kalimdor,
            new Position(1675.9f, -4334.5f, 55.9f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_city_support_stall_screenshot_recovery",
            Kalimdor,
            new Position(1605.1f, -4425.0f, 10.2f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_city_support_stall_exact_live_recovery",
            Kalimdor,
            new Position(1605.0f, -4425.3f, 10.1f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            2.5f,
            true
        ];

        yield return
        [
            "orgrimmar_city_live_vertical_replan_recovery",
            Kalimdor,
            new Position(1545.0f, -4434.5f, 11.1f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            2.5f,
            true
        ];

        yield return
        [
            "orgrimmar_city_hallway_live_wall_stall_recovery",
            Kalimdor,
            new Position(1518.2f, -4419.8f, 17.1f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_city_hallway_exit_live_stall_recovery",
            Kalimdor,
            new Position(1491.4f, -4417.3f, 23.3f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_city_hallway_exit_live_stall_recovery_corridor",
            Kalimdor,
            new Position(1491.4f, -4417.3f, 23.3f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            4.0f,
            false
        ];

        yield return
        [
            "orgrimmar_exterior_steep_incline_live_stall_recovery",
            Kalimdor,
            new Position(1381.0f, -4380.9f, 26.0f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            2.5f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_exterior_incline_live_stall_exact_recovery",
            Kalimdor,
            new Position(1381.3f, -4370.6f, 26.0f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            2.5f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_ramp",
            Kalimdor,
            new Position(1356.8f, -4501.3f, 29.44f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            0.0f,
            0,
            14.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery",
            Kalimdor,
            new Position(1354.0f, -4521.8f, 32.3f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            6.0f,
            2.5f,
            1.25f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery",
            Kalimdor,
            new Position(1357.2f, -4516.2f, 32.0f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            2.5f,
            2.5f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_support_rope_stall_exact_live_recovery",
            Kalimdor,
            new Position(1371.2f, -4439.5f, 29.9f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            7.0f,
            2.5f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_friction_recovery",
            Kalimdor,
            OrgrimmarUndercityZeppelinDeckApproachPoint,
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            0.0f,
            0,
            14.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery",
            Kalimdor,
            new Position(1337.2f, -4654.8f, 49.8f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            2.5f,
            2.5f,
            true
        ];

        yield return
        [
            "orgrimmar_durotar_hillside_slope_recovery",
            Kalimdor,
            new Position(1362.6f, -4472.3f, 27.4f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            0.0f,
            0,
            14.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_exterior_support_recovery",
            Kalimdor,
            new Position(1328.6f, -4535.2f, 26.8f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            0.0f,
            0,
            14.0f,
            7.0f,
            4.0f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery",
            Kalimdor,
            new Position(1342.4f, -4652.1f, 24.6f),
            OrgrimmarUndercityZeppelinBoardingPoint,
            8.0f,
            8.0f,
            0,
            10.0f,
            2.5f,
            2.5f,
            false
        ];

        yield return
        [
            "undercity_zeppelin_arrival_to_target",
            EasternKingdoms,
            new Position(2066.911377f, 290.113708f, 97.031593f),
            new Position(1584.07f, 241.987f, -52.1534f),
            8.0f,
            0.0f,
            0,
            26.0f,
            7.0f,
            float.PositiveInfinity,
            true
        ];
    }

    [Fact]
    public void OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments()
        => CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes(
            "orgrimmar_city_to_zeppelin_tower_lower_approach",
            Kalimdor,
            new Position(1604.8f, -4425.6f, 10.36f),
            new Position(1356.8f, -4501.3f, 29.44f),
            8.0f,
            0.0f,
            0,
            14.0f,
            7.0f,
            float.PositiveInfinity,
            true);

    [Theory]
    [MemberData(nameof(CriticalWalkableLegs))]
    public void CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes(
        string label,
        uint mapId,
        Position start,
        Position end,
        float maxSegmentLength,
        float maxWalkableValidationSegmentLength,
        int maxWalkableValidationChecks,
        float maxHeightJump,
        float minLineOfSightValidationSegmentLength,
        float maxResolvedWaypointZDelta,
        bool smoothPath)
    {
        Assert.True(TaurenMaleCapsule.Radius > 0.9f, $"Unexpected Tauren Male capsule radius {TaurenMaleCapsule.Radius:F4}");
        Assert.True(TaurenMaleCapsule.Height > 2.5f, $"Unexpected Tauren Male capsule height {TaurenMaleCapsule.Height:F4}");

        var traceCase = string.Equals(
            Environment.GetEnvironmentVariable("WWOW_TRACE_LONG_PATHING_CASES"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        var caseStopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (traceCase)
            Console.Error.WriteLine($"[LONGPATH-CASE] start {label}");

        var result = CalculateProductionValidatedPath(
            mapId,
            start.ToXYZ(),
            end.ToXYZ(),
            smoothPath: smoothPath,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        if (traceCase)
            Console.Error.WriteLine(
                $"[LONGPATH-CASE] finish {label} elapsedMs={caseStopwatch.ElapsedMilliseconds} result={result.Result} blocked={result.BlockedReason} corners={result.Path.Length}");

        var path = result.Path;
        if (string.Equals(Environment.GetEnvironmentVariable("WWOW_DUMP_LONG_PATHING_ROUTE"), "1", StringComparison.Ordinal))
            _output.WriteLine($"{label} result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        if (Distance2D(start.ToXYZ(), end.ToXYZ()) <= 0.25f)
        {
            Assert.True(
                path.Length >= 1,
                $"{label} should return the current location for an already-arrived route. result={result.Result} blocked={result.BlockedReason}");
            Assert.Null(result.BlockedSegmentIndex);
            return;
        }

        var routeMaxSegmentLength = result.Result.StartsWith("route_pack", StringComparison.Ordinal)
            ? MathF.Max(maxSegmentLength, 12.0f)
            : maxSegmentLength;

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            mapId,
            start.ToXYZ(),
            end.ToXYZ(),
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 10.0f,
            maxSegmentLength: routeMaxSegmentLength,
            maxHeightJump: maxHeightJump,
            maxWalkableValidationSegmentLength: maxWalkableValidationSegmentLength,
            maxWalkableValidationChecks: maxWalkableValidationChecks,
            minLineOfSightValidationSegmentLength: minLineOfSightValidationSegmentLength,
            maxLineOfSightValidationSegmentIndex: label == "orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery"
                ? 64
                : int.MaxValue,
            maxResolvedWaypointZDelta: maxResolvedWaypointZDelta,
            maxResolvedWaypointZDeltaCheckLimit: label is "orgrimmar_zeppelin_tower_exterior_support_recovery"
                or "orgrimmar_flight_master_tower_descent_live_stall_recovery"
                or "orgrimmar_flight_master_tower_hover_stall_exact_live_recovery"
                or "orgrimmar_city_support_stall_screenshot_recovery"
                or "orgrimmar_city_support_stall_exact_live_recovery"
                or "orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery"
                or "orgrimmar_zeppelin_support_rope_stall_exact_live_recovery"
                    ? 8
                    : int.MaxValue,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"{label} produced an invalid Tauren Male path on map {mapId}: {validationFailure} result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");
        if (label == "orgrimmar_zeppelin_tower_exterior_support_recovery")
        {
            var earlyProbeEnd = Math.Min(path.Length, 8);
            const float maxEarlySupportRise = 3.25f;
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                Assert.True(
                    path[i].Z <= start.Z + maxEarlySupportRise,
                    $"{label} waypoint {i} stayed projected into the exterior support model instead of resolving to the screenshot support layer. " +
                    $"startZ={start.Z:F1} waypoint=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}){Environment.NewLine}{FormatPath(path)}");
            }
        }
        else if (label == "orgrimmar_flight_master_tower_descent_live_stall_recovery")
        {
            var earlyProbeEnd = Math.Min(path.Length, 32);
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                var segmentHorizontal = Distance2D(path[i - 1], path[i]);
                var segmentDrop = path[i - 1].Z - path[i].Z;
                Assert.True(
                    segmentHorizontal >= 0.75f || segmentDrop <= 2.0f,
                    $"{label} routed through a stacked lower tower layer without a horizontal descent segment. " +
                    $"from=({path[i - 1].X:F1},{path[i - 1].Y:F1},{path[i - 1].Z:F1}) " +
                    $"to=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}) horizontal={segmentHorizontal:F1} drop={segmentDrop:F1}{Environment.NewLine}{FormatPath(path)}");
            }
        }
        else if (label == "orgrimmar_flight_master_tower_hover_stall_exact_live_recovery")
        {
            var earlyProbeEnd = Math.Min(path.Length, 32);
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                var segmentHorizontal = Distance2D(path[i - 1], path[i]);
                var segmentDrop = path[i - 1].Z - path[i].Z;
                Assert.True(
                    segmentHorizontal >= 0.75f || segmentDrop <= 2.0f,
                    $"{label} routed through a stacked lower tower layer without a horizontal descent segment. " +
                    $"from=({path[i - 1].X:F1},{path[i - 1].Y:F1},{path[i - 1].Z:F1}) " +
                    $"to=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}) horizontal={segmentHorizontal:F1} drop={segmentDrop:F1}{Environment.NewLine}{FormatPath(path)}");
            }
        }
        else if (label == "orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery")
        {
            var earlyProbeEnd = Math.Min(path.Length, 8);
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                var horizontalFromStart = Distance2D(start.ToXYZ(), path[i]);
                Assert.True(
                    horizontalFromStart >= 2.5f || path[i].Z <= start.Z + 0.75f,
                    $"{label} tried to climb to an overhead ramp/ceiling waypoint before moving horizontally out from the screenshot stall. " +
                    $"startZ={start.Z:F1} waypoint=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}) horizontal={horizontalFromStart:F1}{Environment.NewLine}{FormatPath(path)}");
            }
        }
        else if (label == "orgrimmar_zeppelin_tower_friction_recovery")
        {
            var earlyProbeEnd = Math.Min(path.Length, 8);
            var cumulativeHorizontal = 0.0f;
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                cumulativeHorizontal += Distance2D(path[i - 1], path[i]);
                Assert.True(
                    cumulativeHorizontal >= 3.0f || path[i].Z <= start.Z + 0.9f,
                    $"{label} projected an early waypoint onto the upper tower support before the Tauren capsule could reach it. " +
                    $"startZ={start.Z:F1} waypoint=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}) cumulativeHorizontal={cumulativeHorizontal:F1}{Environment.NewLine}{FormatPath(path)}");
            }
        }
        else if (label == "orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery")
        {
            var localReachabilityFailure = GetLocalPhysicsReachabilityFailure(mapId, path, int.MaxValue);
            Assert.True(
                localReachabilityFailure is null,
                $"{label} produced a route with a local physics movement break at the live stall: " +
                $"{localReachabilityFailure} result={result.Result}{Environment.NewLine}{FormatPath(path)}");
        }

        Assert.True(
            path.Length >= 3,
            $"{label} should be resolved by pathfinding corners, not a direct hand route. Got {path.Length} points.{Environment.NewLine}{FormatPath(path)}");

        if (!result.Result.StartsWith("route_pack", StringComparison.Ordinal) &&
            label is "orgrimmar_city_live_vertical_replan_recovery"
            or "orgrimmar_city_hallway_live_wall_stall_recovery"
            or "orgrimmar_exterior_steep_incline_live_stall_recovery")
        {
            var steepClimbFailure = GetSteepUphillSegmentFailure(mapId, path);
            Assert.True(
                steepClimbFailure is null,
                $"{label} produced a Tauren route with an unwalkable steep uphill segment: {steepClimbFailure} " +
                $"result={result.Result}{Environment.NewLine}{FormatPath(path)}");
        }
        else if (label == "orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery")
        {
            var steepClimbFailure = GetSteepUphillSegmentFailure(mapId, path);
            Assert.True(
                steepClimbFailure is null,
                $"{label} produced a Tauren route with an unwalkable steep uphill segment: {steepClimbFailure}{Environment.NewLine}{FormatPath(path)}");
        }
    }

    private NavigationPathResult CalculateProductionValidatedPath(
        uint mapId,
        XYZ start,
        XYZ end,
        bool smoothPath,
        float agentRadius,
        float agentHeight)
    {
        var routePackRequest = new StaticRoutePackRequest(
            mapId,
            start,
            end,
            Race.Tauren,
            Gender.Male,
            smoothPath,
            StaticRoutePackCache.DefaultRoutePolicy,
            DynamicOverlayCount: 0);

        if (GetProductionRoutePackCache().TryGetPath(
                routePackRequest,
                agentRadius,
                agentHeight,
                out var routePackPath,
                out _))
        {
            return routePackPath;
        }

        return _navigation.CalculateValidatedPath(
            mapId,
            start,
            end,
            smoothPath: smoothPath,
            agentRadius: agentRadius,
            agentHeight: agentHeight);
    }

    private StaticRoutePackCache GetProductionRoutePackCache()
    {
        lock (ProductionRoutePackLock)
        {
            if (s_productionRoutePackCache is not null)
                return s_productionRoutePackCache;

            var cache = new StaticRoutePackCache(
                StaticRoutePackCache.CreateDefaultSeeds(),
                new FixedSignatureProvider("test-nav-signature"),
                routeSeed =>
                {
                    var (radius, height) = routeSeed.Capsule;
                    return routeSeed.GenerationMode == StaticRoutePackGenerationMode.CorridorSeedPath
                        ? _navigation.CalculateRoutePackSeedPath(
                            routeSeed.MapId,
                            routeSeed.StartAnchor,
                            routeSeed.EndAnchor,
                            routeSeed.SmoothPath,
                            radius,
                            height)
                        : _navigation.CalculateStaticRoutePackPath(
                            routeSeed.MapId,
                            routeSeed.StartAnchor,
                            routeSeed.EndAnchor,
                            routeSeed.SmoothPath,
                            radius,
                            height);
                });

            foreach (var routeSeed in StaticRoutePackCache.CreateDefaultSeeds().Where(static routeSeed => routeSeed.WarmAtStartup))
            {
                if (!cache.WarmUp(routeSeed, new TestOutputLogger(_output)))
                    _output.WriteLine($"Route-pack seed '{routeSeed.Id}' was rejected by the static support contract and will fall back to normal navigation.");
            }

            s_productionRoutePackCache = cache;
            return cache;
        }
    }

    [Fact]
    public void OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute()
    {
        var start = new Position(1381.3f, -4370.6f, 26.0f);
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 10.0f,
            maxSegmentLength: 8.0f,
            maxHeightJump: 10.0f,
            maxWalkableValidationSegmentLength: 8.0f,
            maxWalkableValidationChecks: 0,
            minLineOfSightValidationSegmentLength: 2.5f,
            maxResolvedWaypointZDelta: 4.0f,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"Orgrimmar exterior incline exact live stall recovery produced an invalid Tauren Male path: " +
            $"{validationFailure} result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        Assert.Null(result.BlockedSegmentIndex);
    }

    [Fact]
    public void OrgrimmarZeppelinTowerUnderpassLiveStallExactRecovery_HasLocallyReachablePath()
    {
        var start = new Position(1357.2f, -4516.2f, 32.0f);
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.Null(result.BlockedSegmentIndex);

        var localReachabilityFailure = GetLocalPhysicsReachabilityFailure(Kalimdor, result.Path, int.MaxValue);
        Assert.True(
            localReachabilityFailure is null,
            $"Orgrimmar tower underpass exact live stall recovery produced a local physics movement break: " +
            $"{localReachabilityFailure} result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(result.Path)}");
    }

    [Fact]
    public void OrgrimmarZeppelinTowerBaseLiveReplanRecovery_HasWalkablePathfindingRoute()
    {
        var start = new Position(1342.4f, -4652.1f, 24.6f);
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            smoothPath: false,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 10.0f,
            maxSegmentLength: 8.0f,
            maxHeightJump: 10.0f,
            maxWalkableValidationSegmentLength: 8.0f,
            maxWalkableValidationChecks: 0,
            minLineOfSightValidationSegmentLength: 2.5f,
            maxResolvedWaypointZDelta: 2.5f,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"Orgrimmar tower-base live replan produced an invalid Tauren Male path: {validationFailure} " +
            $"result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        var steepClimbFailure = GetSteepUphillSegmentFailure(Kalimdor, path);
        Assert.True(
            steepClimbFailure is null,
            $"Orgrimmar tower-base live replan produced a Tauren route with an unwalkable steep uphill segment: " +
            $"{steepClimbFailure}{Environment.NewLine}{FormatPath(path)}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ(bool smoothPath)
    {
        var start = new Position(1342.4f, -4652.1f, 24.6f);
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinDeckBoardingPoint.ToXYZ(),
            smoothPath: smoothPath,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinDeckBoardingPoint.ToXYZ(),
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 4.0f,
            maxSegmentLength: 8.0f,
            maxHeightJump: 10.0f,
            maxWalkableValidationSegmentLength: 8.0f,
            maxWalkableValidationChecks: 0,
            minLineOfSightValidationSegmentLength: 2.5f,
            maxResolvedWaypointZDelta: 2.5f,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"Orgrimmar tower-base -> deck boarding route produced an invalid Tauren Male path " +
            $"(smooth={smoothPath}): {validationFailure} result={result.Result} blocked={result.BlockedReason}" +
            $"{Environment.NewLine}{FormatPath(path)}");

        Assert.True(
            path.Length >= 3,
            $"Orgrimmar tower-base -> deck boarding route should be resolved by navigation corners " +
            $"(smooth={smoothPath}), not a direct hand route. Got {path.Length} points.{Environment.NewLine}{FormatPath(path)}");

        var final = path[^1];
        var finalHorizontal = Distance2D(final, OrgrimmarUndercityZeppelinDeckBoardingPoint.ToXYZ());
        var finalVertical = MathF.Abs(final.Z - OrgrimmarUndercityZeppelinDeckBoardingPoint.Z);
        Assert.True(
            finalHorizontal <= 4.0f && finalVertical <= 2.0f,
            $"Orgrimmar tower-base -> deck boarding route stopped short of the upper deck boarding Z " +
            $"(smooth={smoothPath}): final=({final.X:F1},{final.Y:F1},{final.Z:F1}) " +
            $"target=({OrgrimmarUndercityZeppelinDeckBoardingPoint.X:F1}," +
            $"{OrgrimmarUndercityZeppelinDeckBoardingPoint.Y:F1}," +
            $"{OrgrimmarUndercityZeppelinDeckBoardingPoint.Z:F1}) " +
            $"dxy={finalHorizontal:F1} dz={finalVertical:F1}{Environment.NewLine}{FormatPath(path)}");

        var steepClimbFailure = GetSteepUphillSegmentFailure(Kalimdor, path);
        Assert.True(
            steepClimbFailure is null,
            $"Orgrimmar tower-base -> deck boarding route cut an unwalkable Tauren uphill corner " +
            $"(smooth={smoothPath}): {steepClimbFailure}{Environment.NewLine}{FormatPath(path)}");
    }

    [Fact]
    public void OrgrimmarZeppelinDeckBoardingPoint_StaysOnUpperDeckLayer()
    {
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            OrgrimmarUndercityZeppelinDeckApproachPoint.ToXYZ(),
            OrgrimmarUndercityZeppelinDeckBoardingPoint.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            OrgrimmarUndercityZeppelinDeckApproachPoint.ToXYZ(),
            OrgrimmarUndercityZeppelinDeckBoardingPoint.ToXYZ(),
            path,
            maxStartDistance: 4.0f,
            maxEndDistance: 4.0f,
            maxSegmentLength: 8.0f,
            maxHeightJump: 7.0f,
            maxWalkableValidationSegmentLength: 0.0f,
            maxWalkableValidationChecks: 0,
            minLineOfSightValidationSegmentLength: 2.5f,
            maxResolvedWaypointZDelta: 2.5f,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"Configured Orgrimmar zeppelin deck boarding point produced an invalid Tauren Male path: {validationFailure} " +
            $"result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        foreach (var waypoint in path)
        {
            Assert.True(
                waypoint.Z >= 50.0f,
                $"Configured Orgrimmar zeppelin boarding path dropped to a lower tower layer at " +
                $"({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1}).{Environment.NewLine}{FormatPath(path)}");
        }
    }

    [Fact]
    public void OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers()
    {
        var start = new Position(1677.6f, -4315.7f, 61.2f);
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start.ToXYZ(),
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        Assert.True(
            path.Length >= 3,
            $"Expected pathfinding to generate an inspectable Orgrimmar route, got {path.Length} points. " +
            $"result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        var failures = new List<string>();
        foreach (var blocker in OrgrimmarFlightMasterToZeppelinBlockers())
        {
            var closest = GetClosestRouteClearance(path, blocker);
            _output.WriteLine(
                $"{blocker.Name}: clearance={closest.Distance:F2}, required={blocker.ClearanceYards:F2}, " +
                $"segment={closest.SegmentIndex}, from=({closest.From.X:F1},{closest.From.Y:F1},{closest.From.Z:F1}), " +
                $"to=({closest.To.X:F1},{closest.To.Y:F1},{closest.To.Z:F1}), evidence={blocker.Evidence}");
            if (closest.Distance >= blocker.ClearanceYards)
                continue;

            failures.Add(
                $"- {blocker.Name}: clearance={closest.Distance:F2} required={blocker.ClearanceYards:F2} " +
                $"segment={closest.SegmentIndex}->{closest.SegmentIndex + 1} " +
                $"anchor=({blocker.Anchor.X:F2},{blocker.Anchor.Y:F2},{blocker.Anchor.Z:F2}) " +
                $"segmentFrom=({closest.From.X:F2},{closest.From.Y:F2},{closest.From.Z:F2}) " +
                $"segmentTo=({closest.To.X:F2},{closest.To.Y:F2},{closest.To.Z:F2}) " +
                $"evidence={blocker.Evidence}");
        }

        Assert.True(
            failures.Count == 0,
            $"Generated Orgrimmar flight-master -> zeppelin route clips known static blockers:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures) +
            $"{Environment.NewLine}{FormatPath(path)}");
    }

    [Fact]
    public void OrgrimmarFlightMasterToZeppelinApproachRoute_AvoidsKnownLiveBlockers()
    {
        var start = new Position(1677.6f, -4315.7f, 61.2f).ToXYZ();
        var end = OrgrimmarUndercityZeppelinDeckApproachPoint.ToXYZ();
        var result = CalculateProductionValidatedPath(
            Kalimdor,
            start,
            end,
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        Assert.True(
            path.Length >= 3,
            $"Expected pathfinding to generate an inspectable Orgrimmar approach route, got {path.Length} points. " +
            $"result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            start,
            end,
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 10.0f,
            maxSegmentLength: result.Result.StartsWith("route_pack", StringComparison.Ordinal) ? 12.0f : 8.0f,
            maxHeightJump: 12.0f,
            maxWalkableValidationSegmentLength: 8.0f,
            maxWalkableValidationChecks: 0,
            minLineOfSightValidationSegmentLength: 2.5f,
            maxResolvedWaypointZDelta: 4.0f,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"Generated Orgrimmar flight-master -> zeppelin approach route is not Tauren-walkable: " +
            $"{validationFailure} result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        AssertAvoidsOrgrimmarStaticBlockers(path);
    }

    [Fact]
    public void OrgrimmarStaticRoutePackSeeds_TargetGangplankAndDeferStartupWarmup()
    {
        var seeds = StaticRoutePackCache.CreateDefaultSeeds();
        var seed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_flight_master_to_undercity_zeppelin");
        var recoverySeed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_exterior_incline_to_undercity_zeppelin");
        var lowerRecoverySeed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_lower_incline_recovery_to_undercity_zeppelin");
        var hallwayRecoverySeed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_hallway_wall_stall_to_undercity_zeppelin");
        var orgrimmarSeeds = new[] { seed, recoverySeed, lowerRecoverySeed, hallwayRecoverySeed };
        Assert.Equal(StaticRoutePackGenerationMode.ValidatedPath, seed.GenerationMode);
        Assert.Equal(StaticRoutePackGenerationMode.CorridorSeedPath, recoverySeed.GenerationMode);
        Assert.Equal(StaticRoutePackGenerationMode.CorridorSeedPath, lowerRecoverySeed.GenerationMode);
        Assert.Equal(StaticRoutePackGenerationMode.ValidatedPath, hallwayRecoverySeed.GenerationMode);
        Assert.All(orgrimmarSeeds, routeSeed =>
        {
            Assert.Equal(OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(), routeSeed.EndAnchor);
            Assert.False(routeSeed.WarmAtStartup);
            Assert.True(routeSeed.WarmOnDemand);
            Assert.False(routeSeed.AllowsDynamicOverlay);
        });

        var cache = new StaticRoutePackCache(
            seeds,
            new FixedSignatureProvider("test-nav-signature"),
            routeSeed =>
            {
                var (radius, height) = routeSeed.Capsule;
                return routeSeed.GenerationMode == StaticRoutePackGenerationMode.CorridorSeedPath
                    ? _navigation.CalculateRoutePackSeedPath(
                        routeSeed.MapId,
                        routeSeed.StartAnchor,
                        routeSeed.EndAnchor,
                        routeSeed.SmoothPath,
                        radius,
                        height)
                    : _navigation.CalculateStaticRoutePackPath(
                        routeSeed.MapId,
                        routeSeed.StartAnchor,
                        routeSeed.EndAnchor,
                        routeSeed.SmoothPath,
                        radius,
                        height);
            });

        cache.WarmUpAll(new TestOutputLogger(_output));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath()
    {
        var lowerRecoverySeed = Assert.Single(
            StaticRoutePackCache.CreateDefaultSeeds(),
            static candidate => candidate.Id == "kalimdor_orgrimmar_lower_incline_recovery_to_undercity_zeppelin");
        NavigationPathResult? generatedResult = null;
        var cache = new StaticRoutePackCache(
            [lowerRecoverySeed],
            new FixedSignatureProvider("test-nav-signature"),
            routeSeed =>
            {
                var (radius, height) = routeSeed.Capsule;
                generatedResult = _navigation.CalculateRoutePackSeedPath(
                    routeSeed.MapId,
                    routeSeed.StartAnchor,
                    routeSeed.EndAnchor,
                    routeSeed.SmoothPath,
                    radius,
                    height);
                return generatedResult.Value;
            });
        var liveStart = new XYZ(1363.9f, -4378.2f, 26.1f);
        var request = new StaticRoutePackRequest(
            lowerRecoverySeed.MapId,
            liveStart,
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            lowerRecoverySeed.Race,
            lowerRecoverySeed.Gender,
            lowerRecoverySeed.SmoothPath,
            lowerRecoverySeed.RoutePolicy,
            DynamicOverlayCount: 0);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var warmed = cache.WarmUp(lowerRecoverySeed, new TestOutputLogger(_output));
        var hit = cache.TryGetPath(
            request,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var route,
            out var match);
        stopwatch.Stop();

        Assert.True(
            warmed,
            $"Lower-incline recovery route pack did not warm within {lowerRecoverySeed.EffectiveGenerationTimeout.TotalSeconds:F0}s. " +
            FormatGeneratedRoutePackFailure(generatedResult));
        Assert.True(hit, "Lower-incline recovery route pack warmed but did not match the live request.");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(35),
            $"Lower-incline recovery route-pack lookup should be bounded; elapsed={stopwatch.Elapsed}.");
        Assert.Equal(lowerRecoverySeed.Id, match.SeedId);
        Assert.StartsWith("route_pack", route.Result, StringComparison.Ordinal);
        Assert.Equal(liveStart, route.Path[0]);

        var endDistance = Distance2D(route.Path[^1], OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ());
        Assert.True(
            endDistance <= 10.0f,
            $"Lower-incline route pack ended too far from the gangplank target: dxy={endDistance:F1}{Environment.NewLine}{FormatPath(route.Path)}");

        var failure = GetLocalPhysicsReachabilityFailure(Kalimdor, route.Path, maxSegments: 16);
        Assert.True(
            failure is null,
            $"Lower-incline recovery route pack produced an early local-physics break: {failure}{Environment.NewLine}{FormatPath(route.Path)}");
    }

    [Fact]
    public void OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable()
    {
        var start = OrgrimmarUndercityZeppelinDeckApproachPoint.ToXYZ();
        var end = OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ();
        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start,
            end,
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        if (Distance2D(start, end) <= 0.25f)
        {
            Assert.True(result.Path.Length >= 1);
            Assert.Null(result.BlockedSegmentIndex);
            return;
        }

        var failure = GetLocalPhysicsReachabilityFailure(Kalimdor, result.Path, maxSegments: 12);
        Assert.True(
            failure is null,
            $"Orgrimmar tower friction recovery produced an early segment the local physics probe cannot traverse: {failure} " +
            $"result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(result.Path)}");
    }

    [Fact]
    public void OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer()
    {
        var seeds = StaticRoutePackCache.CreateDefaultSeeds();
        var cache = new StaticRoutePackCache(
            seeds,
            new FixedSignatureProvider("test-nav-signature"),
            routeSeed =>
            {
                var (radius, height) = routeSeed.Capsule;
                return routeSeed.GenerationMode == StaticRoutePackGenerationMode.CorridorSeedPath
                    ? _navigation.CalculateRoutePackSeedPath(
                        routeSeed.MapId,
                        routeSeed.StartAnchor,
                        routeSeed.EndAnchor,
                        routeSeed.SmoothPath,
                        radius,
                        height)
                        : _navigation.CalculateStaticRoutePackPath(
                            routeSeed.MapId,
                            routeSeed.StartAnchor,
                            routeSeed.EndAnchor,
                            routeSeed.SmoothPath,
                        radius,
                        height);
            });

        foreach (var routeSeed in seeds.Where(static routeSeed => routeSeed.WarmAtStartup))
            cache.WarmUp(routeSeed, new TestOutputLogger(_output));

        var seed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_flight_master_to_undercity_zeppelin");
        var request = new StaticRoutePackRequest(
            seed.MapId,
            OrgrimmarUndercityZeppelinDeckApproachPoint.ToXYZ(),
            seed.EndAnchor,
            seed.Race,
            seed.Gender,
            seed.SmoothPath,
            seed.RoutePolicy,
            DynamicOverlayCount: 0);

        if (!cache.TryGetPath(
                request,
                TaurenMaleCapsule.Radius,
                TaurenMaleCapsule.Height,
                out var route,
                out var match))
        {
            return;
        }

        var failure = GetLocalPhysicsReachabilityFailure(Kalimdor, route.Path, maxSegments: 8);
        Assert.True(
            failure is null,
            $"Route-pack seed '{match.SeedId}' attached the live lower-layer tower start to an unreachable suffix: {failure}" +
            $"{Environment.NewLine}{FormatPath(route.Path)}");
    }

    [Fact]
    public void OrgrimmarCitySupport_ForwardFrictionSegment_IsBlockedForTaurenCapsule()
    {
        var start = new XYZ(1605.0f, -4425.3f, 10.1f);
        var badActiveWaypoint = new XYZ(1598.8f, -4420.4f, 10.0f);

        var validation = ValidateWalkableSegment(
            Kalimdor,
            start,
            badActiveWaypoint,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out _,
            out _,
            out var travelFraction);

        Assert.Equal(
            SegmentValidationResult.BlockedGeometry,
            validation);
        Assert.True(
            travelFraction < 0.95f,
            $"The Tauren segment should not be accepted as near-complete through the Orgrimmar support: fraction={travelFraction:F3}");
    }

    [Fact]
    public void OrgrimmarCitySupport_FirstUphillStep_IsWalkableForTaurenCapsule()
    {
        var start = new XYZ(1605.0f, -4425.2f, 10.2f);
        var lowerStep = new XYZ(1606.1f, -4423.4f, 10.8f);

        var validation = ValidateWalkableSegment(
            Kalimdor,
            start,
            lowerStep,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out _,
            out _,
            out var travelFraction);

        Assert.Equal(
            SegmentValidationResult.Clear,
            validation);
        Assert.True(
            travelFraction >= 0.95f,
            $"The first Orgrimmar support step should be traversable for Tauren: fraction={travelFraction:F3}");
    }

    [Fact]
    public void OrgrimmarZeppelinSupport_FirstCompactStep_IsWalkableForTaurenCapsule()
    {
        var start = new XYZ(1371.1f, -4439.4f, 30.9f);
        var supportStep = new XYZ(1372.0f, -4441.6f, 30.7f);

        var validation = ValidateWalkableSegment(
            Kalimdor,
            start,
            supportStep,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out _,
            out _,
            out var travelFraction);

        Assert.Equal(
            SegmentValidationResult.Clear,
            validation);
        Assert.True(
            travelFraction >= 0.95f,
            $"The first Orgrimmar zeppelin support step should be traversable for Tauren: fraction={travelFraction:F3}");
    }

    [Fact]
    public void OrgrimmarBonfireOverlay_FallbackHullBlocksTaurenFrictionSegment()
    {
        ClearAllDynamicObjects();
        using var overlay = new RequestScopedDynamicObjectOverlay(new NativeDynamicObjectOverlayRegistry());

        var start = new XYZ(1596.7f, -4426.4f, 9.3f);
        var badActiveWaypoint = new XYZ(1590.7f, -4425.6f, 8.8f);
        try
        {
            var result = overlay.ExecuteWithOverlay(
                Kalimdor,
                [CreateOrgrimmarBonfireOverlay()],
                () => ValidateWalkableSegment(
                    Kalimdor,
                    start,
                    badActiveWaypoint,
                    TaurenMaleCapsule.Radius,
                    TaurenMaleCapsule.Height,
                    out _,
                    out _,
                    out var travelFraction));

            Assert.Equal(1, result.Summary.RegisteredCount);
            Assert.Equal(
                SegmentValidationResult.BlockedGeometry,
                result.Value);
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void OrgrimmarBonfireOverlay_ReroutesLiveFrictionRecoveryAroundFirePile()
    {
        ClearAllDynamicObjects();
        using var overlay = new RequestScopedDynamicObjectOverlay(new NativeDynamicObjectOverlayRegistry());

        var start = new XYZ(1596.7f, -4426.4f, 9.3f);
        var target = OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ();
        var bonfire = new XYZ(1592.37f, -4427.32f, 8.053f);

        try
        {
            var result = overlay.ExecuteWithOverlay(
                Kalimdor,
                [CreateOrgrimmarBonfireOverlay()],
                () => _navigation.CalculateValidatedPath(
                    Kalimdor,
                    start,
                    target,
                    smoothPath: true,
                    agentRadius: TaurenMaleCapsule.Radius,
                    agentHeight: TaurenMaleCapsule.Height));

            var path = result.Value.Path;
            Assert.Equal(1, result.Summary.RegisteredCount);
            Assert.True(path.Length >= 3, $"Expected an overlay-rerouted path, got:{Environment.NewLine}{FormatPath(path)}");

            var earlyProbeEnd = Math.Min(path.Length, 12);
            for (var i = 0; i + 1 < earlyProbeEnd; i++)
            {
                var distance = DistancePointToSegment2D(bonfire, path[i], path[i + 1]);
                Assert.True(
                    distance > TaurenMaleCapsule.Radius + 0.75f,
                    $"Path segment {i}->{i + 1} cut the Tauren corridor through the Orgrimmar bonfire overlay. " +
                    $"distance={distance:F2} bonfire=({bonfire.X:F1},{bonfire.Y:F1},{bonfire.Z:F1}){Environment.NewLine}{FormatPath(path)}");
            }
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    // Phase 3 walkable-snapped OG↔UC zeppelin off-mesh anchors. The original
    // screenshot-derived seeds at z=51-54 were the bot's recorded ground
    // positions when stalled, not the actual upper-platform / gangplank
    // elevations. Tile (1, 29, 40)'s walkable mesh floor is at z≈72 (per a
    // managed binary parse of the regenerated tile's polyMeshDetail verts),
    // so seeds below that elevation are silently dropped at Detour's
    // dtCreateNavMeshData::classifyOffMeshPoint height-check
    // (DetourNavMeshBuilder.cpp:344-348). Snapped to the closest existing
    // walkable detail verts in tile (1, 29, 40) — see tools/MmapGen/offmesh.txt
    // and memory note project_mmapgen_offmesh_axis_swap.md.
    private static readonly Position OrgrimmarZeppelinUpperPlatformWalkable =
        new(1330.66f, -4656.03f, 96.29f);
    private static readonly Position OrgrimmarZeppelinGangplankEndWalkable =
        new(1315.33f, -4650.00f, 98.54f);
    // Phase 5.3.4 pre-flight gate (PFS-OVERHAUL-005). Phase 5.3 FG verification
    // proved BoardingPosition (1320.14,-4653.16,53.89) is REAL walkable (C4
    // settled with dz=0.0) and ApproachPosition (1338.10,-4646.00,51.60) is REAL
    // walkable (C3 settled with dz=0.0), but C6/C7 proved no walkable polygon
    // exists at z=65/70 between them — the OG zeppelin tower's central pillar
    // blocks any direct ground walk, leaving offmesh edge #4 as the only path.
    private static readonly Position OrgrimmarFlightMasterTopGround =
        new(1677.0f, -4315.0f, 62.0f);
    private static readonly Position OrgrimmarZeppelinBoardingPositionGround =
        new(1320.142944f, -4653.158691f, 53.891945f);

    [Fact]
    public void OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink()
    {
        // Phase 3 proof gate (PFS-OVERHAUL-003). Two assertions:
        //
        // PROOF A — the regenerated tile (1, 29, 40) authoritatively encodes a
        //   bidirectional dtOffMeshConnection between the OG zeppelin tower's
        //   upper-platform anchor (1330.66, -4656.03, 96.29) and the
        //   gangplank-end anchor (1315.33, -4650.00, 98.54). Parsed directly
        //   from the on-disk .mmtile (wrapper + Detour payload), so the test
        //   is independent of the runtime path-query stack.
        //
        // PROOF B — a Detour path query across that boarding edge does not
        //   trip any of the six managed repair-phase counters in
        //   NavigationPerformanceMetrics. The freeze contract in
        //   docs/physics/PATHFINDING_OVERHAUL.md says the mesh is
        //   authoritative; this gates that claim.
        var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR")
            ?? throw new InvalidOperationException(
                "WWOW_DATA_DIR not set; NavigationFixture should auto-discover it.");
        var tilePath = System.IO.Path.Combine(dataDir, "mmaps", "0014029.mmtile");
        Assert.True(
            System.IO.File.Exists(tilePath),
            $"Expected regenerated OG-dock tile at {tilePath}. Run tools/MmapGen and "
            + "regenerate map 1 tiles 28-30 / 39-41 before this test.");

        var connections = ParseOffMeshConnectionsFromMmtile(tilePath);
        _output.WriteLine($"Tile {tilePath}: {connections.Count} off-mesh connection(s).");
        for (var i = 0; i < connections.Count; i++)
        {
            var c = connections[i];
            _output.WriteLine(
                $"  [{i}] A=({c.Pos[0]:F2},{c.Pos[1]:F2},{c.Pos[2]:F2}) "
                + $"B=({c.Pos[3]:F2},{c.Pos[4]:F2},{c.Pos[5]:F2}) "
                + $"rad={c.Rad:F2} flags=0x{c.Flags:X2} side={c.Side} userId={c.UserId}");
        }

        // On-disk Recast frame for off-mesh pos after the WWoW divergence in
        // TerrainBuilder.cpp::loadOffMeshConnections is (WoW_X, WoW_Z, WoW_Y) per
        // endpoint — same swap as solidVerts and as MapBuilder::getTileBounds's
        // bmin[0]/bmax[0] (= WoW X) and bmin[2]/bmax[2] (= WoW Y) axes.
        static (float A, float B, float C) ToOnDisk(float wowX, float wowY, float wowZ)
            => (wowX, wowZ, wowY);
        var upperPlatformOnDisk = ToOnDisk(
            OrgrimmarZeppelinUpperPlatformWalkable.X,
            OrgrimmarZeppelinUpperPlatformWalkable.Y,
            OrgrimmarZeppelinUpperPlatformWalkable.Z);
        var gangplankEndOnDisk = ToOnDisk(
            OrgrimmarZeppelinGangplankEndWalkable.X,
            OrgrimmarZeppelinGangplankEndWalkable.Y,
            OrgrimmarZeppelinGangplankEndWalkable.Z);
        const float Tolerance = 1.0f;

        OffMeshConnectionRecord? match = null;
        foreach (var c in connections)
        {
            var aMatchesUpperBStartsGangplank = NearOnDisk(c.Pos, 0, upperPlatformOnDisk, Tolerance)
                && NearOnDisk(c.Pos, 3, gangplankEndOnDisk, Tolerance);
            var aMatchesGangplankBStartsUpper = NearOnDisk(c.Pos, 0, gangplankEndOnDisk, Tolerance)
                && NearOnDisk(c.Pos, 3, upperPlatformOnDisk, Tolerance);
            if (aMatchesUpperBStartsGangplank || aMatchesGangplankBStartsUpper)
            {
                match = c;
                break;
            }
        }

        Assert.True(
            match.HasValue,
            $"Tile (map=1, tileX=29, tileY=40) does not contain an off-mesh "
            + $"connection between OG upper-platform anchor "
            + $"({OrgrimmarZeppelinUpperPlatformWalkable.X:F2},"
            + $"{OrgrimmarZeppelinUpperPlatformWalkable.Y:F2},"
            + $"{OrgrimmarZeppelinUpperPlatformWalkable.Z:F2}) and gangplank-end "
            + $"anchor ({OrgrimmarZeppelinGangplankEndWalkable.X:F2},"
            + $"{OrgrimmarZeppelinGangplankEndWalkable.Y:F2},"
            + $"{OrgrimmarZeppelinGangplankEndWalkable.Z:F2}). Found "
            + $"{connections.Count} other connection(s); see test output for "
            + "their endpoints. Likely cause: tools/MmapGen/offmesh.txt seed "
            + "missing or the tile was not regenerated since the seed was added.");

        Assert.True(
            (match!.Value.Flags & 0x01) != 0,
            $"OG↔UC zeppelin off-mesh connection must be bidirectional "
            + $"(DT_OFFMESH_CON_BIDIR=0x01); got flags=0x{match.Value.Flags:X2}.");

        _output.WriteLine(
            "PROOF A: bidirectional OG↔UC zeppelin off-mesh connection present in "
            + "tile (1, 29, 40).");

        // PROOF B — runtime traversal does not invoke any managed repair phase.
        var before = NavigationPerformanceMetrics.Snapshot;
        var pathResult = _navigation.CalculateValidatedPath(
            Kalimdor,
            OrgrimmarZeppelinUpperPlatformWalkable.ToXYZ(),
            OrgrimmarZeppelinGangplankEndWalkable.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var after = NavigationPerformanceMetrics.Snapshot;

        var dLongLOS  = after.LongLineOfSightRepairCount   - before.LongLineOfSightRepairCount;
        var dStaticW  = after.StaticWallRepairCount        - before.StaticWallRepairCount;
        var dSteepAff = after.SteepAffordanceRepairCount   - before.SteepAffordanceRepairCount;
        var dLocalPL  = after.LocalPhysicsLayerRepairCount - before.LocalPhysicsLayerRepairCount;
        var dSegVal   = after.SegmentValidationRepairCount - before.SegmentValidationRepairCount;
        var dDynOver  = after.DynamicOverlayRepairCount    - before.DynamicOverlayRepairCount;

        _output.WriteLine(
            $"PathResult: result={pathResult.Result} len={pathResult.Path.Length} "
            + $"blockedSeg={pathResult.BlockedSegmentIndex} "
            + $"blockedReason={pathResult.BlockedReason}");
        _output.WriteLine(
            $"Repair deltas: longLOS={dLongLOS} staticWall={dStaticW} "
            + $"steepAffordance={dSteepAff} localPhysicsLayer={dLocalPL} "
            + $"segValidation={dSegVal} dynamicOverlay={dDynOver}");

        Assert.True(
            pathResult.Path.Length > 0,
            "Expected a non-empty path result for the OG upper-platform → "
            + $"gangplank-end query; got result='{pathResult.Result}' "
            + $"length={pathResult.Path.Length}. If 'no_path', the off-mesh entry "
            + "exists in the tile but Detour cannot reach it from one of the "
            + "endpoints (likely cause: endpoint over a non-walkable poly — fix "
            + "at the mesh, not at query time).");

        Assert.Equal(0, dLongLOS);
        Assert.Equal(0, dStaticW);
        Assert.Equal(0, dSteepAff);
        Assert.Equal(0, dLocalPL);
        Assert.Equal(0, dSegVal);
        Assert.Equal(0, dDynOver);

        _output.WriteLine(
            "PROOF B: OG dock → zeppelin deck path query did not trip any managed "
            + "repair phase. The mesh is authoritative for this boarding edge.");
    }

    [Fact]
    public void OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut()
    {
        // PROOF C (Phase 4 navmesh-tuning gate). The Phase 3 off-mesh entries
        // are baked into tile (1, 29, 40) (PROOF A) and the upper-platform →
        // gangplank-end query traverses them without managed repair (PROOF B).
        // But the live CrossroadsToUndercity_UsesFlightAndZeppelin test at
        // 11m33s showed Detour preferred a 470-waypoint sea-level walk through
        // OG city (z=6-23) over the upper-platform off-mesh shortcut from the
        // OG flight master to the zeppelin-deck approach point.
        //
        // This gate path-queries from the OG flight master tower top
        // (1677.0, -4315.0, 62.0) to the zeppelin-deck approach point
        // (1338.1, -4646.0, 51.6) and asserts:
        //   1. The corridor is small (≤ 50 corners) — the prior sea-level walk
        //      had 470 corners.
        //   2. At least one corner sits at z ≥ 80, proving the path goes up
        //      onto the OG zeppelin tower's upper platform (where the off-mesh
        //      anchor is at z=96.29) rather than walking the dock at sea level.
        //   3. The Detour polygon corridor (queried via the test-only
        //      FindPathPolygonsForAgent export, PFS-OVERHAUL-004 H2b) contains
        //      at least one DT_POLYTYPE_OFFMESH_CONNECTION polygon. Assertions
        //      1+2 are corner-XYZ heuristics that cannot distinguish "Detour
        //      reached the off-mesh poly but kept walking past it" from
        //      "Detour never reached the off-mesh poly"; assertion 3 closes
        //      that gap. The freeze contract permits the helper as test-only
        //      diagnostic infrastructure.
        var start = new Position(1677.0f, -4315.0f, 62.0f);
        var end = OrgrimmarUndercityZeppelinDeckApproachPoint;

        var result = _navigation.CalculateValidatedPath(
            Kalimdor,
            start.ToXYZ(),
            end.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        _output.WriteLine(
            $"PROOF C diagnostics: result={result.Result} corners={path.Length} "
            + $"blockedSeg={result.BlockedSegmentIndex} blockedReason={result.BlockedReason}");

        // Polygon-list diagnostic — emit before asserts so it appears in test
        // output regardless of which assertion fires first.
        var polyResult = NavigationInterop.QueryPathPolygons(
            Kalimdor,
            start.ToXYZ(),
            end.ToXYZ(),
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height);
        _output.WriteLine(
            $"PROOF C polygon-list (FindPathPolygonsForAgent): success={polyResult.Success} "
            + $"totalPolyCount={polyResult.TotalPolyCount} written={polyResult.PolyRefs.Length} "
            + $"offMeshPolyCount={polyResult.OffMeshPolyCount}");
        if (polyResult.Success)
        {
            var headPoly = Math.Min(20, polyResult.PolyRefs.Length);
            for (var i = 0; i < headPoly; i++)
                _output.WriteLine(
                    $"  poly[{i:D3}] ref=0x{polyResult.PolyRefs[i]:X16} type={polyResult.PolyTypes[i]}");
            if (polyResult.PolyRefs.Length > headPoly)
                _output.WriteLine($"  ... {polyResult.PolyRefs.Length - headPoly} interior poly(s) elided ...");
        }

        Assert.True(
            path.Length > 0,
            $"Expected a non-empty path from OG flight master to approach point; "
            + $"got result='{result.Result}' length={path.Length} blocked='{result.BlockedReason}'.");

        var maxZ = path.Max(p => p.Z);
        var minZ = path.Min(p => p.Z);
        _output.WriteLine($"  zRange=[{minZ:F2}, {maxZ:F2}]");
        var headLimit = Math.Min(15, path.Length);
        for (var i = 0; i < headLimit; i++)
            _output.WriteLine($"  [{i:D3}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");
        if (path.Length > headLimit + 5)
        {
            _output.WriteLine($"  ... {path.Length - headLimit - 5} interior corner(s) elided ...");
            for (var i = path.Length - 5; i < path.Length; i++)
                _output.WriteLine($"  [{i:D3}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");
        }

        Assert.True(
            maxZ >= 80f,
            $"PROOF C (corner-Z heuristic): expected the path from OG flight master "
            + $"({start.X:F1},{start.Y:F1},{start.Z:F1}) to ApproachPosition "
            + $"({end.X:F1},{end.Y:F1},{end.Z:F1}) to traverse the OG zeppelin tower "
            + $"upper platform (off-mesh anchor at z=96.29). Observed maxZ={maxZ:F2} "
            + $"across {path.Length} corners — Detour is picking a sea-level walkable "
            + $"corridor through OG city instead. To unblock, tune one of: "
            + $"(a) raise off-mesh radius (currently 4.0) in tools/MmapGen/offmesh.txt; "
            + $"(b) add intermediate off-mesh anchors at flight-master-to-tower-top "
            + $"positions; (c) tighten capsule rules to make the sea-level dock "
            + $"non-walkable for Tauren. See PFS-OVERHAUL-004 'Off-mesh shortcut routing'.");

        Assert.True(
            path.Length <= 50,
            $"PROOF C (corner-count heuristic): expected ≤ 50 corners on the "
            + $"upper-platform-via-off-mesh path; observed {path.Length}. The prior "
            + $"sea-level corridor had ~470 corners and the bot only reached idx=119 "
            + $"in the live test's 11m33s window. If maxZ is high but corner count is "
            + $"also high, the path may be using the upper platform but failing to "
            + $"string-pull tightly across it.");

        Assert.True(
            polyResult.Success,
            $"PROOF C (polygon-list helper): FindPathPolygonsForAgent failed for the "
            + $"OG flight master → ApproachPosition query (mapId={Kalimdor}, "
            + $"start=({start.X:F1},{start.Y:F1},{start.Z:F1}), end=({end.X:F1},{end.Y:F1},{end.Z:F1})). "
            + $"This rules out a meaningful polygon-corridor diagnostic. Likely cause: "
            + $"findNearestPoly snapped neither endpoint, or findPath returned no polys. "
            + $"Check the [POLYLIST] stderr lines from Navigation.dll for the specific failure.");

        Assert.True(
            polyResult.ContainsOffMeshPoly,
            $"PROOF C (polygon-list helper, the load-bearing claim): expected Detour's "
            + $"corridor from OG flight master to ApproachPosition to traverse at least "
            + $"one DT_POLYTYPE_OFFMESH_CONNECTION polygon. Observed polyCount="
            + $"{polyResult.TotalPolyCount} with offMeshPolyCount=0. This means the "
            + $"off-mesh anchors baked into tile (1, 29, 40) are correct (PROOF A + B "
            + $"green) but Detour does not include them in its findPath result for this "
            + $"start/end pair — typically because the off-mesh START anchor sits in a "
            + $"polygon island unreachable from the flight-master walkable graph "
            + $"(Hypothesis 2 connectivity), or the off-mesh area cost is filtering it "
            + $"out (Hypothesis 3). Branch H2a (add an off-mesh anchor along the natural "
            + $"sea-level walk path in tile (1, 28, 40)) is the next experiment per the "
            + $"PFS-OVERHAUL-004 Phase 4 ranking.");

        _output.WriteLine(
            "PROOF C: OG flight-master → zeppelin approach corridor uses the "
            + "upper-platform off-mesh shortcut.");
    }

    [Fact]
    public void OrgrimmarUpperPlatformToGangplankEnd_PolygonListIncludesOffMeshConnection()
    {
        // Smoke test for the new test-only FindPathPolygonsForAgent C export
        // (PFS-OVERHAUL-004 H2b). Validates the export works on a route that
        // PROOF A + PROOF B already prove is correctly baked: the same upper-
        // platform ↔ gangplank-end pair the off-mesh entry was authored for.
        // The corridor between these two anchors MUST include at least one
        // off-mesh polygon — if this smoke test fails, the export is broken or
        // the tile lost its off-mesh entries, and PROOF C's polygon-list
        // assertion is meaningless until that's fixed.
        var start = OrgrimmarZeppelinUpperPlatformWalkable.ToXYZ();
        var end = OrgrimmarZeppelinGangplankEndWalkable.ToXYZ();

        var polyResult = NavigationInterop.QueryPathPolygons(
            Kalimdor, start, end,
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        _output.WriteLine(
            $"FindPathPolygonsForAgent smoke: success={polyResult.Success} "
            + $"totalPolyCount={polyResult.TotalPolyCount} written={polyResult.PolyRefs.Length} "
            + $"offMeshPolyCount={polyResult.OffMeshPolyCount}");
        for (var i = 0; i < polyResult.PolyRefs.Length; i++)
            _output.WriteLine(
                $"  poly[{i:D3}] ref=0x{polyResult.PolyRefs[i]:X16} type={polyResult.PolyTypes[i]}");

        Assert.True(
            polyResult.Success,
            "FindPathPolygonsForAgent returned false for the upper-platform → "
            + "gangplank-end smoke route. Either the export is missing from the "
            + "loaded Navigation.dll (rebuild + ensure Bot/Release/net8.0/Navigation.dll "
            + "is fresh) or the proof anchors are no longer on the navmesh. Check "
            + "[POLYLIST] lines in stderr.");

        Assert.True(
            polyResult.TotalPolyCount > 0,
            $"Expected polyCount > 0 for the smoke route; got {polyResult.TotalPolyCount}. "
            + "The export reported success but produced zero polygons — likely an internal "
            + "logic error in FindPathPolygonsForAgent.");

        Assert.True(
            polyResult.ContainsOffMeshPoly,
            $"Expected the corridor between upper-platform anchor "
            + $"({OrgrimmarZeppelinUpperPlatformWalkable.X:F2},"
            + $"{OrgrimmarZeppelinUpperPlatformWalkable.Y:F2},"
            + $"{OrgrimmarZeppelinUpperPlatformWalkable.Z:F2}) and gangplank-end anchor "
            + $"({OrgrimmarZeppelinGangplankEndWalkable.X:F2},"
            + $"{OrgrimmarZeppelinGangplankEndWalkable.Y:F2},"
            + $"{OrgrimmarZeppelinGangplankEndWalkable.Z:F2}) to include at least one "
            + $"DT_POLYTYPE_OFFMESH_CONNECTION polygon (the bidirectional anchor entry "
            + $"PROOF A confirms is in the tile). Observed polyCount="
            + $"{polyResult.TotalPolyCount} with offMeshPolyCount=0. If PROOF A is also "
            + $"failing the tile has lost its off-mesh entry; if PROOF A is green, the "
            + $"export is reading polygon types incorrectly.");
    }

    [Fact]
    public void OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection()
    {
        // Phase 4 H2d outcome (PFS-OVERHAUL-004). H2d instrumentation in
        // baseOffMeshLinks proved the off-mesh polys ARE linked at runtime
        // (dxz^2=0.00 for all 4 baked entries — the prior "links are dangling"
        // theory was wrong). The earlier H2c smoke test failed for a different
        // reason: the upper-platform ↔ gangplank-end pair are ~16 units apart
        // and Detour finds a 5-poly direct ground path of comparable cost,
        // correctly preferring it over the off-mesh hop.
        //
        // This assertion uses off-mesh entry #2's anchors — upper-platform
        // (z=96.29) → ApproachPosition (z=51.60). The ground walk between
        // them requires descending the entire OG ramp (hundreds of units);
        // the off-mesh hop is ~46 units. Detour MUST prefer the off-mesh.
        // If this test passes, the off-mesh-link infrastructure is fully
        // working end-to-end and the Phase 4 mesh-side claim is proven.
        // Touch the navmesh first so it is loaded before we count.
        var probe = NavigationInterop.QueryPathPolygons(
            Kalimdor,
            OrgrimmarZeppelinUpperPlatformWalkable.ToXYZ(),
            OrgrimmarZeppelinGangplankEndWalkable.ToXYZ(),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);
        _output.WriteLine($"H2d probe: success={probe.Success} totalPolyCount={probe.TotalPolyCount}");

        var counts = NavigationInterop.QueryOffMeshLinkCounts(Kalimdor);
        _output.WriteLine($"H2d off-mesh link counts: success={counts.Success} "
            + $"total={counts.Total} linked={counts.Linked}");

        Assert.True(counts.Success,
            "CountLinkedOffMeshPolysOnMap returned false. Either the export is "
            + "missing from the loaded Navigation.dll (rebuild + ensure "
            + "Bot/Release/net8.0/Navigation.dll is fresh) or no navMesh is "
            + "loaded for Kalimdor. Check [OMLINK] in stderr.");

        Assert.True(counts.Total >= 4,
            $"Expected at least 4 off-mesh polygons on Kalimdor (the four "
            + $"OG↔UC zeppelin entries baked into tile (1, 29, 40)); got "
            + $"total={counts.Total}. If 0, the navmesh wasn't fully loaded "
            + $"or the tile lost its off-mesh entries.");

        // The 4 OG↔UC zeppelin entries in tile (1, 29, 40) are all intra-tile
        // (side==0xff) so baseOffMeshLinks/connectExtOffMeshLinks(tile,tile,-1)
        // both fire and the runtime poly's firstLink chain is non-empty. Any
        // additional cross-tile entries (e.g., the prior H2a sea-level entry
        // in tile (28, 40) whose end lands in tile (29, 40)) may or may not
        // link depending on tile load order; that is tracked separately.
        Assert.True(counts.Linked >= 4,
            $"H2d GATE: expected at least the 4 OG↔UC intra-tile off-mesh "
            + $"polygons in tile (1, 29, 40) to be linked at runtime; got "
            + $"linked={counts.Linked} of total={counts.Total}. If linked < 4, "
            + $"the runtime baseOffMeshLinks / connectExtOffMeshLinks failed "
            + $"to snap the off-mesh anchors to a ground polygon. Check "
            + $"[OFFLINK] lines in stderr for the failing endpoint.");
    }

    [Fact]
    public void OrgrimmarFlightMasterToFrezzaSpawn_PathExists()
    {
        // Phase 5.3.5 pre-flight (PFS-OVERHAUL-005). With OG.ApproachPosition
        // re-anchored to Zeppelin Master Frezza's spawn (NPC 9564, map 1, z=53.63),
        // the walk leg now targets the upper-platform deck (one tier above the
        // city ground at z=51.6) instead of the prior wrong-tier z=51.6 anchor.
        // This probe asserts Detour can route from OG flight master to Frezza —
        // i.e., the wooden ramp from city ground up to the deck IS in the navmesh.
        // If RED, the deck isn't reachable via natural ground walk and the fix
        // is dead.
        var probe = NavigationInterop.QueryPathPolygons(
            Kalimdor,
            OrgrimmarFlightMasterTopGround.ToXYZ(),
            new XYZ(1331.11f, -4649.45f, 53.6269f),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        _output.WriteLine(
            $"Phase 5.3.5 flight-master→Frezza: success={probe.Success} "
            + $"totalPolyCount={probe.TotalPolyCount} "
            + $"offMeshPolyCount={probe.OffMeshPolyCount}");

        Assert.True(probe.Success,
            "QueryPathPolygons returned false for flight master → Frezza spawn. "
            + "Detour cannot reach the upper-platform deck via natural walk. "
            + "Either the wooden ramp is not bake-walkable in the current tile, "
            + "or Frezza's spawn coords land in a non-walkable polygon. Phase "
            + "5.3.5 ApproachPosition re-anchor is dead.");
    }

    [Fact]
    public void OrgrimmarLowerDeckStallToFrezza_PrintsCornerSequence()
    {
        // Phase 5.3.6 Cycle 5 diagnostic (PFS-OVERHAUL-006). Cycle 4's tighter
        // walk_arrived dz<=1.5 gate let the bot climb to z=51.60 (vs Cycle 2's
        // z=49.88) but it stalled at (1338.13, -4645.96, 51.60) at idx=2/6 of
        // the final leg, unable to traverse a 1.8y vertical step from waypoint
        // 1 (z=51.7) to waypoint 2 (z=53.5). [TRAVEL_WALK_STALL] count=2,
        // 6 path replans tried, no progress. Same "Detour-walkable but
        // physically-blocked" phenomenon as Phase 5.3.4 found between
        // ApproachPosition and BoardingPosition.
        //
        // This diagnostic prints Detour's corner sequence from the stall point
        // (1338.13,-4645.96,51.60) to Frezza (1331.11,-4649.45,53.63). If the
        // corners trend smoothly up (z=51→52→53→54), the issue is runtime
        // physics — capsule can't climb a step the navmesh considers walkable.
        // If the corners are sparse / non-monotonic / detour through a higher
        // platform, the issue is mesh-side topology.
        var corners = NavigationInterop.QueryPathCorners(
            Kalimdor,
            new XYZ(1338.13f, -4645.96f, 51.60f),
            new XYZ(1331.11f, -4649.45f, 53.6269f),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        Assert.True(corners.Success,
            "FindPathCornersForAgent returned false for stall→Frezza. "
            + "Detour can't even FIND a path from the stall point to Frezza, "
            + "meaning the stall point is on a polygon disconnected from Frezza's. "
            + "If RED, the lower ledge polygons are an island.");

        _output.WriteLine($"[CORNERS-STALL-TO-FREZZA] count={corners.CornerCount}");
        for (int i = 0; i < corners.CornerCount; i++)
        {
            var c = corners.Corners[i];
            _output.WriteLine($"  [{i:D3}] ({c.X:F2},{c.Y:F2},{c.Z:F2})");
        }
    }

    [Fact]
    public void OrgrimmarFlightMasterToFrezzaSpawn_PrintsCornerSequence()
    {
        // Phase 5.3.6 diagnostic (PFS-OVERHAUL-006). The 278-poly Detour path
        // exists per OrgrimmarFlightMasterToFrezzaSpawn_PathExists, but the live
        // sub-test ClimbOrgrimmarZeppelinTowerRampToFrezza shows the bot walks
        // WEST/NORTHWEST instead of SOUTHWEST and ends 267y NORTH of Frezza.
        // The corner-XYZ inspection (via FindPathCornersForAgent / findStraightPath)
        // reveals the actual route Detour serves to BotRunner: if the corners
        // themselves trend NORTH, the runtime path IS wrong-direction and the
        // mesh/cost story needs work; if the corners trend SOUTHWEST as
        // expected, the bot is corner-cutting/auto-completing waypoints
        // (the user's Facing-based completion hypothesis applies).
        var corners = NavigationInterop.QueryPathCorners(
            Kalimdor,
            OrgrimmarFlightMasterTopGround.ToXYZ(),
            new XYZ(1331.11f, -4649.45f, 53.6269f),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        Assert.True(corners.Success,
            "FindPathCornersForAgent returned false for FM → Frezza. Either "
            + "Navigation.dll is stale (rebuild + copy to "
            + "Tests/PathfindingService.Tests/bin/Release/net8.0/) or the new "
            + "FindPathCornersForAgent export is missing.");

        _output.WriteLine($"[CORNERS] count={corners.CornerCount}");
        for (int i = 0; i < corners.CornerCount; i++)
        {
            var c = corners.Corners[i];
            _output.WriteLine($"  [{i:D3}] ({c.X:F2},{c.Y:F2},{c.Z:F2})");
        }

        Assert.True(corners.CornerCount >= 2,
            $"Expected >= 2 corners (start, end) for FM → Frezza. Got "
            + $"{corners.CornerCount}. Detour returned an empty straight path.");
    }

    [Fact]
    public void OrgrimmarFrezzaSpawnToBoardingPosition_PathExists()
    {
        // Phase 5.3.5 pre-flight short-hop (PFS-OVERHAUL-005). Frezza's spawn
        // (1331.11,-4649.45,53.63) is on the upper-platform deck. BoardingPosition
        // (1320.14,-4653.16,53.89) is also on the deck. Both at z=53.6-53.9, ~12y
        // apart in XY. After the walk leg deposits the bot at Frezza, the Boarding
        // phase needs Detour to navigate this short hop. Asserts the two are
        // connected on the same deck level.
        var probe = NavigationInterop.QueryPathPolygons(
            Kalimdor,
            new XYZ(1331.11f, -4649.45f, 53.6269f),
            OrgrimmarZeppelinBoardingPositionGround.ToXYZ(),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        _output.WriteLine(
            $"Phase 5.3.5 Frezza→Boarding: success={probe.Success} "
            + $"totalPolyCount={probe.TotalPolyCount} "
            + $"offMeshPolyCount={probe.OffMeshPolyCount}");

        Assert.True(probe.Success,
            "QueryPathPolygons returned false for Frezza spawn → BoardingPosition. "
            + "If Frezza is on a separate Detour island from BoardingPosition, the "
            + "Boarding phase navigator cannot bridge them. Phase 5.3.5 needs "
            + "either to also re-anchor BoardingPosition to Frezza's coords (so "
            + "the walk leg ends at boarding directly) or to keep the Phase 5.3.4 "
            + "predicate gate.");
    }

    [Fact]
    public void OrgrimmarApproachToBoardingPosition_PathExistsAndDescribesOffMeshUsage()
    {
        // Phase 5.3.4 secondary pre-flight (PFS-OVERHAUL-005). The first
        // pre-flight (flight master → BoardingPosition) returned polyCount=281
        // offMeshPolyCount=0 — Detour found a long ground-only path. This
        // shorter probe (ApproachPosition → BoardingPosition, the final 18y
        // hop) tests whether Detour finds a path AT ALL between the walk-leg
        // endpoint and the boarding deck, and whether that path uses the
        // off-mesh edge. Two outcomes informative for option (iii) (Detour-
        // driven boarding navigation in TransportWaitingLogic):
        //   - polyCount > 0 and offMeshPolyCount >= 1: option (iii) works.
        //   - polyCount > 0 and offMeshPolyCount == 0: option (iii) finds the
        //     same kind of "Detour-walkable but physically-blocked" polygons
        //     the long path used; same stall problem.
        //   - success=false: no path at all; both options dead.
        var probe = NavigationInterop.QueryPathPolygons(
            Kalimdor,
            new XYZ(1338.10f, -4646.00f, 51.60f),
            OrgrimmarZeppelinBoardingPositionGround.ToXYZ(),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        _output.WriteLine(
            $"Phase 5.3.4 short-hop pre-flight: success={probe.Success} "
            + $"totalPolyCount={probe.TotalPolyCount} "
            + $"offMeshPolyCount={probe.OffMeshPolyCount} "
            + $"containsOffMesh={probe.ContainsOffMeshPoly}");

        // Diagnostic assertion only — a failure here just records the result;
        // the ASSERT_OUTCOME comment below is what the lead reads to decide.
        Assert.True(probe.Success,
            $"ASSERT_OUTCOME: short-hop probe Success was false — Detour cannot "
            + $"find ANY path from ApproachPosition (1338.10,-4646.00,51.60) to "
            + $"BoardingPosition (1320.14,-4653.16,53.89). Both options (i) and "
            + $"(iii) are dead. Phase 5.3.4 must use option (ii) (Boarding-phase "
            + $"Detour-driven nav with explicit off-mesh corridor selection) or "
            + $"defer to a deeper mesh-side fix.");
    }

    [Fact(Skip = "Phase 5.3.4 outcome: this gate is RED. Detour finds a 281-poly ground-only path "
        + "(offMeshPolyCount=0) from OG flight master to BoardingPosition. The walk-endpoint shift "
        + "(option i in the original Phase 5.3.4 plan) is dead because Detour will always prefer "
        + "the long ground path over offmesh edge #4. Kept in source as durable diagnostic "
        + "infrastructure documenting the rejected approach. Re-enable if a future session adds "
        + "area-cost bias or bake-time fixes that make off-mesh edges Detour-preferred over the "
        + "long ground detour. See Services/PathfindingService/TASKS.md '2026-05-07 Phase 5.3.4 "
        + "outcome' entry for the full reasoning.")]
    public void OrgrimmarFlightMasterToBoardingPosition_PathIncludesOffMeshConnection()
    {
        // Phase 5.3.4 pre-flight gate (PFS-OVERHAUL-005). The Phase 5 BotRunner
        // walk-leg currently ends at TransportStop.NavigationPosition (=
        // ApproachPosition for OG, z=51.60). The fix being designed shifts
        // leg.End to BoardingPosition (z=53.89) when WWOW_OFFMESH_NATIVE_BOARDING
        // is set so Detour MUST route via offmesh edge #4 (the only path: tower
        // central pillar blocks direct ground walk; FG verification C6/C7
        // confirmed no walkable polygon at z=60-70). This pre-flight asserts
        // Detour's findPath from city-ground flight master to BoardingPosition
        // includes at least one DT_POLYTYPE_OFFMESH_CONNECTION poly. If RED,
        // the walk-endpoint shift is dead and the lead must reconsider.
        var probe = NavigationInterop.QueryPathPolygons(
            Kalimdor,
            OrgrimmarFlightMasterTopGround.ToXYZ(),
            OrgrimmarZeppelinBoardingPositionGround.ToXYZ(),
            TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height);

        _output.WriteLine(
            $"Phase 5.3.4 pre-flight: success={probe.Success} "
            + $"totalPolyCount={probe.TotalPolyCount} "
            + $"offMeshPolyCount={probe.OffMeshPolyCount} "
            + $"containsOffMesh={probe.ContainsOffMeshPoly}");

        Assert.True(probe.Success,
            "QueryPathPolygons returned false for flight master → BoardingPosition. "
            + "Likely findPath could not reach the goal at all — meaning the "
            + "off-mesh edge #4's auto-snap from z=96.29 phantom to the z=65.65 "
            + "real walkable polygon at (1330.66,-4656.03) does NOT bridge to a "
            + "walkable polygon connected to OG city ground. Phase 5.3.4 "
            + "walk-endpoint shift cannot work. Reconsider option (ii) "
            + "(Detour-driven Boarding-phase navigation) or extend offmesh.txt "
            + "with an additional intra-tile entry whose START is on a confirmed "
            + "walkable polygon at z>=70.5.");

        Assert.True(probe.OffMeshPolyCount >= 1,
            $"Phase 5.3.4 PRE-FLIGHT GATE: expected Detour's path from OG flight "
            + $"master to BoardingPosition to traverse at least one off-mesh "
            + $"polygon (offmesh edge #4 is the only path through the OG zeppelin "
            + $"tower's central-pillar barrier per Phase 5.3 FG verification); "
            + $"got polyCount={probe.TotalPolyCount} offMeshPolyCount={probe.OffMeshPolyCount}. "
            + $"If 0, Detour found a ground-only path (likely a long detour around "
            + $"the city) that bypasses the off-mesh edge — meaning the "
            + $"walk-endpoint shift won't force off-mesh use. The lead must add "
            + $"area-cost bias on the alternate path or tighten the off-mesh "
            + $"radius before proceeding.");
    }

    private readonly record struct OffMeshConnectionRecord(
        float[] Pos, float Rad, ushort Poly, byte Flags, byte Side, uint UserId);

    private static bool NearOnDisk(float[] pos, int offset, (float A, float B, float C) target, float tolerance)
        => Math.Abs(pos[offset]     - target.A) <= tolerance
           && Math.Abs(pos[offset + 1] - target.B) <= tolerance
           && Math.Abs(pos[offset + 2] - target.C) <= tolerance;

    private static List<OffMeshConnectionRecord> ParseOffMeshConnectionsFromMmtile(string tilePath)
    {
        // Layout (see docs/physics/MMAP_FORMAT.md §2 and DetourNavMeshBuilder.cpp::createNavMeshData):
        //   [20-byte MMAP wrapper: mmapMagic, dtVersion, mmapVersion, payloadSize, usesLiquids]
        //   [Detour tile payload]
        //     [dtMeshHeader (100 bytes)]
        //     [verts]         align4(12 * vertCount)
        //     [polys]         align4(32 * polyCount)            — DT_VERTS_PER_POLYGON=6 ⇒ sizeof(dtPoly)=32
        //     [links]         align4(16 * maxLinkCount)         — DT_POLYREF64 ⇒ sizeof(dtLink)=16
        //     [detailMeshes]  align4(12 * detailMeshCount)      — sizeof(dtPolyDetail)=12 (with 2-byte trailing pad)
        //     [detailVerts]   align4(12 * detailVertCount)
        //     [detailTris]    align4(4  * detailTriCount)
        //     [bvNodes]       align4(16 * bvNodeCount)          — sizeof(dtBVNode)=16
        //     [offMeshCons]   align4(36 * offMeshConCount)      — sizeof(dtOffMeshConnection)=36
        //
        // Each dtOffMeshConnection (36 bytes):
        //   float pos[6]; float rad; ushort poly; byte flags; byte side; uint userId;
        //
        // Off-mesh flags & 0x01 == DT_OFFMESH_CON_BIDIR (Detour writes this from the
        // generator's offMeshConDir array — see DetourNavMeshBuilder.cpp:644).
        var bytes = System.IO.File.ReadAllBytes(tilePath);
        if (bytes.Length < 20 + 100)
            throw new System.IO.InvalidDataException($"Tile {tilePath} too small ({bytes.Length} bytes).");

        var span = new ReadOnlySpan<byte>(bytes);
        var mmapMagic = BitConverter.ToUInt32(span.Slice(0, 4));
        var dtVersion = BitConverter.ToUInt32(span.Slice(4, 4));
        var mmapVersion = BitConverter.ToUInt32(span.Slice(8, 4));
        if (mmapMagic != 0x4D4D4150u)
            throw new System.IO.InvalidDataException(
                $"Bad mmapMagic 0x{mmapMagic:X8} in {tilePath}; expected 0x4D4D4150 ('MMAP').");
        if (dtVersion != 7 || mmapVersion != 6)
            throw new System.IO.InvalidDataException(
                $"Bad mmtile versions dt={dtVersion}/mmap={mmapVersion} in {tilePath}; expected 7/6.");

        const int WrapperSize = 20;
        var p = WrapperSize;
        var polyCount       = BitConverter.ToInt32(span.Slice(p + 24, 4));
        var vertCount       = BitConverter.ToInt32(span.Slice(p + 28, 4));
        var maxLinkCount    = BitConverter.ToInt32(span.Slice(p + 32, 4));
        var detailMeshCount = BitConverter.ToInt32(span.Slice(p + 36, 4));
        var detailVertCount = BitConverter.ToInt32(span.Slice(p + 40, 4));
        var detailTriCount  = BitConverter.ToInt32(span.Slice(p + 44, 4));
        var bvNodeCount     = BitConverter.ToInt32(span.Slice(p + 48, 4));
        var offMeshConCount = BitConverter.ToInt32(span.Slice(p + 52, 4));

        static int Align4(int n) => (n + 3) & ~3;
        const int HeaderSize = 100;
        const int PolySize = 32;
        const int LinkSize = 16;
        const int DetailMeshSize = 12;
        const int BvNodeSize = 16;
        const int OffMeshSize = 36;

        var vertsSize        = Align4(12 * vertCount);
        var polysSize        = Align4(PolySize * polyCount);
        var linksSize        = Align4(LinkSize * maxLinkCount);
        var detailMeshesSize = Align4(DetailMeshSize * detailMeshCount);
        var detailVertsSize  = Align4(12 * detailVertCount);
        var detailTrisSize   = Align4(4 * detailTriCount);
        var bvTreeSize       = Align4(BvNodeSize * bvNodeCount);

        var offMeshOffset = p + HeaderSize + vertsSize + polysSize + linksSize
                            + detailMeshesSize + detailVertsSize + detailTrisSize + bvTreeSize;
        var requiredEnd = offMeshOffset + Align4(OffMeshSize * offMeshConCount);
        if (requiredEnd > bytes.Length)
            throw new System.IO.InvalidDataException(
                $"Computed off-mesh end {requiredEnd} exceeds tile size {bytes.Length} for {tilePath}. "
                + $"Counts: poly={polyCount} vert={vertCount} link={maxLinkCount} "
                + $"detailMesh={detailMeshCount} detailVert={detailVertCount} "
                + $"detailTri={detailTriCount} bv={bvNodeCount} offMesh={offMeshConCount}.");

        var conns = new List<OffMeshConnectionRecord>(offMeshConCount);
        for (var i = 0; i < offMeshConCount; i++)
        {
            var b = offMeshOffset + i * OffMeshSize;
            var pos = new float[6];
            for (var j = 0; j < 6; j++)
                pos[j] = BitConverter.ToSingle(span.Slice(b + j * 4, 4));
            var rad    = BitConverter.ToSingle(span.Slice(b + 24, 4));
            var poly   = BitConverter.ToUInt16(span.Slice(b + 28, 2));
            var flags  = span[b + 30];
            var side   = span[b + 31];
            var userId = BitConverter.ToUInt32(span.Slice(b + 32, 4));
            conns.Add(new OffMeshConnectionRecord(pos, rad, poly, flags, side, userId));
        }
        return conns;
    }

    private static string FormatPath(XYZ[] path)
    {
        if (path.Length == 0)
            return "Path: <empty>";

        var lines = new List<string>(path.Length + 1) { $"Path ({path.Length} points):" };
        for (var i = 0; i < path.Length; i++)
            lines.Add($"  [{i}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatGeneratedRoutePackFailure(NavigationPathResult? generatedResult)
    {
        if (!generatedResult.HasValue)
            return "Generator did not return a result.";

        var result = generatedResult.Value;
        return $"generated result={result.Result} blocked={result.BlockedReason} idx={result.BlockedSegmentIndex?.ToString() ?? "none"}" +
            $"{Environment.NewLine}{FormatPath(result.Path)}";
    }

    private static void AssertValidOrgrimmarRoute(XYZ start, XYZ end, XYZ[] path, string result)
    {
        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            start,
            end,
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 10.0f,
            maxSegmentLength: 12.0f,
            maxHeightJump: 10.0f,
            maxWalkableValidationSegmentLength: 8.0f,
            maxWalkableValidationChecks: 0,
            minLineOfSightValidationSegmentLength: 2.5f,
            maxResolvedWaypointZDelta: 4.0f,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);

        Assert.True(
            validationFailure is null,
            $"Route-pack path failed Orgrimmar route validation: {validationFailure} result={result}" +
            $"{Environment.NewLine}{FormatPath(path)}");
    }

    private static string? GetLocalPhysicsReachabilityFailure(uint mapId, XYZ[] path, int maxSegments)
    {
        if (path.Length < 2)
            return "path has fewer than two waypoints";

        var scanEnd = Math.Min(path.Length - 1, Math.Max(0, maxSegments));
        for (var i = 0; i < scanEnd; i++)
        {
            var from = path[i];
            var to = path[i + 1];
            if (Navigation.IsSegmentLocallyReachableForAgent(mapId, from, to, TaurenMaleCapsule.Radius, TaurenMaleCapsule.Height))
                continue;

            return $"segment {i}->{i + 1} from=({from.X:F1},{from.Y:F1},{from.Z:F1}) " +
                $"to=({to.X:F1},{to.Y:F1},{to.Z:F1})";
        }

        return null;
    }

    private static void AssertRouteLocallyReachable(XYZ[] path, string routeId)
    {
        var failure = GetLocalPhysicsReachabilityFailure(Kalimdor, path, int.MaxValue);
        Assert.True(
            failure is null,
            $"Route-pack seed '{routeId}' produced a cached route segment the local physics probe cannot traverse: " +
            $"{failure}{Environment.NewLine}{FormatPath(path)}");
    }

    private static void AssertAvoidsOrgrimmarStaticBlockers(XYZ[] path)
    {
        var failures = new List<string>();
        foreach (var blocker in OrgrimmarFlightMasterToZeppelinBlockers())
        {
            var closest = GetClosestRouteClearance(path, blocker);
            if (closest.Distance >= blocker.ClearanceYards)
                continue;

            failures.Add(
                $"- {blocker.Name}: clearance={closest.Distance:F2} required={blocker.ClearanceYards:F2} " +
                $"segment={closest.SegmentIndex}->{closest.SegmentIndex + 1}");
        }

        Assert.True(
            failures.Count == 0,
            $"Route-pack Orgrimmar route clips known static blockers:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures) +
            $"{Environment.NewLine}{FormatPath(path)}");
    }

    private static float Distance2D(XYZ from, XYZ to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static string? GetSteepUphillSegmentFailure(uint mapId, XYZ[] path)
    {
        for (var i = 0; i < path.Length - 1; i++)
        {
            var from = path[i];
            var to = path[i + 1];
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            if (dz <= 0.0f)
                continue;

            var horizontal = MathF.Sqrt((dx * dx) + (dy * dy));
            var slopeDegrees = horizontal > 0.01f
                ? MathF.Atan2(dz, horizontal) * (180.0f / MathF.PI)
                : 90.0f;
            if (slopeDegrees <= 45.0f || dz < 3.0f)
                continue;

            if (Navigation.IsSegmentLocallyReachableForAgent(
                    mapId,
                    from,
                    to,
                    TaurenMaleCapsule.Radius,
                    TaurenMaleCapsule.Height))
            {
                continue;
            }

            return $"segment {i}->{i + 1} slope={slopeDegrees:F1}deg " +
                   $"from=({from.X:F1},{from.Y:F1},{from.Z:F1}) " +
                   $"to=({to.X:F1},{to.Y:F1},{to.Z:F1})";
        }

        return null;
    }

    private static float DistancePointToSegment2D(XYZ point, XYZ start, XYZ end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lenSq = (dx * dx) + (dy * dy);
        if (lenSq <= 0.0001f)
            return Distance2D(point, start);

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lenSq;
        t = Math.Clamp(t, 0.0f, 1.0f);
        var closest = new XYZ(start.X + (dx * t), start.Y + (dy * t), start.Z);
        return Distance2D(point, closest);
    }

    private static IEnumerable<RouteBlocker> OrgrimmarFlightMasterToZeppelinBlockers()
    {
        const float taurenComfortBuffer = 2.25f;
        var taurenClearance = TaurenMaleCapsule.Radius + taurenComfortBuffer;

        yield return new RouteBlocker(
            "Orgrimmar lower flight-master bonfire",
            new XYZ(1665.50f, -4360.83f, 26.66f),
            MathF.Max(4.5f, taurenClearance),
            18.0f,
            34.0f,
            "mangos.gameobject guid=10975 entry=177026 display=4572 size=2.21128");

        yield return new RouteBlocker(
            "Orgrimmar bank-front palm/static model snag",
            new XYZ(1605.00f, -4425.20f, 10.20f),
            MathF.Max(4.0f, taurenClearance),
            4.0f,
            18.0f,
            "live blocker point reported at the bank-front palm/tree model");

        yield return new RouteBlocker(
            "Orgrimmar bank-front bonfire",
            new XYZ(1592.37f, -4427.32f, 8.05f),
            MathF.Max(4.5f, taurenClearance),
            3.0f,
            16.0f,
            "mangos.gameobject guid=10090 entry=177019 display=4572 size=2.21128");

        yield return new RouteBlocker(
            "Orgrimmar Z-hallway early-cut north corner",
            new XYZ(1513.20f, -4415.90f, 20.0f),
            MathF.Max(3.5f, taurenClearance),
            12.0f,
            30.0f,
            "live route corner where the turn is taken too early in the Z-shaped exit hallway");

        yield return new RouteBlocker(
            "Orgrimmar Z-hallway early-cut south corner",
            new XYZ(1415.30f, -4372.90f, 25.3f),
            MathF.Max(4.0f, taurenClearance),
            18.0f,
            36.0f,
            "live route corner where the diagonal smoothing slides off the right-angle hallway");

        yield return new RouteBlocker(
            "Orgrimmar exterior steep incline",
            new XYZ(1383.00f, -4385.00f, 28.0f),
            MathF.Max(6.0f, TaurenMaleCapsule.Radius + 4.0f),
            20.0f,
            42.0f,
            "live route tried to run directly uphill on a slope too steep to climb");

        yield return new RouteBlocker(
            "Orgrimmar exterior rope-line support snag",
            new XYZ(1371.10f, -4439.40f, 30.9f),
            MathF.Max(5.0f, TaurenMaleCapsule.Radius + 3.0f),
            22.0f,
            40.0f,
            "live blocker point at the 45-degree rope/support line outside Orgrimmar");
    }

    private static RouteClearance GetClosestRouteClearance(XYZ[] path, RouteBlocker blocker)
    {
        var best = new RouteClearance(
            SegmentIndex: -1,
            Distance: float.PositiveInfinity,
            From: default,
            To: default);

        for (var i = 0; i + 1 < path.Length; i++)
        {
            if (!SegmentOverlapsZBand(path[i], path[i + 1], blocker.MinZ, blocker.MaxZ))
                continue;

            var distance = DistancePointToSegment2D(blocker.Anchor, path[i], path[i + 1]);
            if (distance >= best.Distance)
                continue;

            best = new RouteClearance(i, distance, path[i], path[i + 1]);
        }

        return best;
    }

    private static bool SegmentOverlapsZBand(XYZ from, XYZ to, float minZ, float maxZ)
    {
        var segmentMinZ = MathF.Min(from.Z, to.Z);
        var segmentMaxZ = MathF.Max(from.Z, to.Z);
        return segmentMaxZ >= minZ && segmentMinZ <= maxZ;
    }

    private readonly record struct RouteBlocker(
        string Name,
        XYZ Anchor,
        float ClearanceYards,
        float MinZ,
        float MaxZ,
        string Evidence);

    private readonly record struct RouteClearance(
        int SegmentIndex,
        float Distance,
        XYZ From,
        XYZ To);

    private static DynamicObjectProto CreateOrgrimmarBonfireOverlay()
        => new()
        {
            Guid = 0x10090,
            DisplayId = 4572,
            X = 1592.37f,
            Y = -4427.32f,
            Z = 8.053f,
            Orientation = 0.087267f,
            Scale = 2.21128f,
            GoState = 1,
        };

    [DllImport("Navigation.dll", EntryPoint = "ValidateWalkableSegment", CallingConvention = CallingConvention.Cdecl)]
    private static extern SegmentValidationResult ValidateWalkableSegment(
        uint mapId,
        XYZ start,
        XYZ end,
        float radius,
        float height,
        out float resolvedEndZ,
        out float supportDelta,
        out float travelFraction);

    [DllImport("Navigation.dll", EntryPoint = "ClearAllDynamicObjects", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ClearAllDynamicObjects();

    private enum SegmentValidationResult : uint
    {
        Clear = 0,
        BlockedGeometry = 1,
        MissingSupport = 2,
        StepUpTooHigh = 3,
        StepDownTooFar = 4,
    }

    private sealed class FixedSignatureProvider(string signature) : INavigationDataSignatureProvider
    {
        public string GetSignature(uint mapId) => signature;
    }

    private sealed class TestOutputLogger(ITestOutputHelper output) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            output.WriteLine(formatter(state, exception));
            if (exception is not null)
                output.WriteLine(exception.ToString());
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
