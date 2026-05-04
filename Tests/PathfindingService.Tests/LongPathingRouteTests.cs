using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using PathfindingService.Repository;
using PathfindingService.RoutePacks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace PathfindingService.Tests;

public class LongPathingRouteTests(NavigationFixture fixture, ITestOutputHelper output) : IClassFixture<NavigationFixture>
{
    private const uint Kalimdor = 1;
    private const uint EasternKingdoms = 0;
    private static readonly (float Radius, float Height) TaurenMaleCapsule =
        RaceDimensions.GetCapsuleForRace(Race.Tauren, Gender.Male);
    private static readonly Position OrgrimmarUndercityZeppelinBoardingPoint =
        new(1320.142944f, -4653.158691f, 53.891945f);
    private static readonly Position OrgrimmarUndercityZeppelinDeckBoardingPoint =
        new(1320.142944f, -4653.158691f, 53.891945f);

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
            2.5f,
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
            new Position(1338.1f, -4646.0f, 51.6f),
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
            2.5f,
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

        var result = _navigation.CalculateValidatedPath(
            mapId,
            start.ToXYZ(),
            end.ToXYZ(),
            smoothPath: smoothPath,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;
        if (string.Equals(Environment.GetEnvironmentVariable("WWOW_DUMP_LONG_PATHING_ROUTE"), "1", StringComparison.Ordinal))
            _output.WriteLine($"{label} result={result.Result} blocked={result.BlockedReason}{Environment.NewLine}{FormatPath(path)}");

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            mapId,
            start.ToXYZ(),
            end.ToXYZ(),
            path,
            maxStartDistance: 8.0f,
            maxEndDistance: 10.0f,
            maxSegmentLength: maxSegmentLength,
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
            const float maxEarlySupportRise = 2.0f;
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

        Assert.True(
            path.Length >= 3,
            $"{label} should be resolved by pathfinding corners, not a direct hand route. Got {path.Length} points.{Environment.NewLine}{FormatPath(path)}");

        if (label is "orgrimmar_city_live_vertical_replan_recovery"
            or "orgrimmar_city_hallway_live_wall_stall_recovery"
            or "orgrimmar_exterior_steep_incline_live_stall_recovery")
        {
            var steepClimbFailure = GetSteepUphillSegmentFailure(path);
            Assert.True(
                steepClimbFailure is null,
                $"{label} produced a Tauren route with an unwalkable steep uphill segment: {steepClimbFailure}{Environment.NewLine}{FormatPath(path)}");
        }
        else if (label == "orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery")
        {
            var steepClimbFailure = GetSteepUphillSegmentFailure(path);
            Assert.True(
                steepClimbFailure is null,
                $"{label} produced a Tauren route with an unwalkable steep uphill segment: {steepClimbFailure}{Environment.NewLine}{FormatPath(path)}");
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

        var steepClimbFailure = GetSteepUphillSegmentFailure(path);
        Assert.True(
            steepClimbFailure is null,
            $"Orgrimmar exterior incline exact live stall recovery cut an unwalkable Tauren uphill corner: " +
            $"{steepClimbFailure}{Environment.NewLine}{FormatPath(path)}");
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

        var steepClimbFailure = GetSteepUphillSegmentFailure(path);
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

        var steepClimbFailure = GetSteepUphillSegmentFailure(path);
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
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
            OrgrimmarUndercityZeppelinDeckBoardingPoint.ToXYZ(),
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        var path = result.Path;

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            Kalimdor,
            OrgrimmarUndercityZeppelinBoardingPoint.ToXYZ(),
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
    public void OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor()
    {
        var seeds = StaticRoutePackCache.CreateDefaultSeeds();
        var seed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_flight_master_to_undercity_zeppelin");
        var recoverySeed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_exterior_incline_to_undercity_zeppelin");
        var lowerRecoverySeed = Assert.Single(seeds, static candidate =>
            candidate.Id == "kalimdor_orgrimmar_lower_incline_recovery_to_undercity_zeppelin");
        var cache = new StaticRoutePackCache(
            seeds,
            new FixedSignatureProvider("test-nav-signature"),
            routeSeed =>
            {
                var (radius, height) = routeSeed.Capsule;
                return _navigation.CalculateValidatedPath(
                    routeSeed.MapId,
                    routeSeed.StartAnchor,
                    routeSeed.EndAnchor,
                    routeSeed.SmoothPath,
                    radius,
                    height);
            });

        foreach (var routeSeed in seeds)
            Assert.True(cache.WarmUp(routeSeed));

        var fullRequest = new StaticRoutePackRequest(
            seed.MapId,
            seed.StartAnchor,
            seed.EndAnchor,
            seed.Race,
            seed.Gender,
            seed.SmoothPath,
            seed.RoutePolicy,
            DynamicOverlayCount: 0);

        Assert.True(cache.TryGetPath(
            fullRequest,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var fullRoute,
            out var fullMatch));
        Assert.Equal("route_pack_main_path", fullRoute.Result);
        Assert.Equal(seed.Id, fullMatch.SeedId);

        AssertValidOrgrimmarRoute(seed.StartAnchor, seed.EndAnchor, fullRoute.Path, fullRoute.Result);
        AssertAvoidsOrgrimmarStaticBlockers(fullRoute.Path);

        var recoveryStart = recoverySeed.StartAnchor;
        var suffixRequest = fullRequest with { Start = recoveryStart };

        Assert.True(cache.TryGetPath(
            suffixRequest,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var suffixRoute,
            out var suffixMatch));
        Assert.Equal("route_pack_main_path", suffixRoute.Result);
        Assert.Equal(recoverySeed.Id, suffixMatch.SeedId);

        AssertValidOrgrimmarRoute(recoveryStart, seed.EndAnchor, suffixRoute.Path, suffixRoute.Result);

        var lowerRecoveryStart = lowerRecoverySeed.StartAnchor;
        var lowerRecoveryRequest = fullRequest with { Start = lowerRecoveryStart };

        Assert.True(cache.TryGetPath(
            lowerRecoveryRequest,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var lowerRecoveryRoute,
            out var lowerRecoveryMatch));
        Assert.Equal("route_pack_main_path", lowerRecoveryRoute.Result);
        Assert.Equal(lowerRecoverySeed.Id, lowerRecoveryMatch.SeedId);
        Assert.Equal(lowerRecoveryStart, lowerRecoveryRoute.Path[0]);

        var firstLegHorizontal = Distance2D(lowerRecoveryRoute.Path[0], lowerRecoveryRoute.Path[1]);
        var firstLegClimb = lowerRecoveryRoute.Path[1].Z - lowerRecoveryRoute.Path[0].Z;
        Assert.True(
            firstLegHorizontal >= 1.25f || firstLegClimb <= 0.75f,
            $"Route-pack lower-incline recovery should not snap vertically onto a cached upper layer. " +
            $"seed={lowerRecoveryMatch.SeedId} firstLegHorizontal={firstLegHorizontal:F2} firstLegClimb={firstLegClimb:F2}");

        AssertValidOrgrimmarRoute(lowerRecoveryStart, seed.EndAnchor, lowerRecoveryRoute.Path, lowerRecoveryRoute.Result);
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

    private static string FormatPath(XYZ[] path)
    {
        if (path.Length == 0)
            return "Path: <empty>";

        var lines = new List<string>(path.Length + 1) { $"Path ({path.Length} points):" };
        for (var i = 0; i < path.Length; i++)
            lines.Add($"  [{i}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");

        return string.Join(Environment.NewLine, lines);
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

    private static string? GetSteepUphillSegmentFailure(XYZ[] path)
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
}
