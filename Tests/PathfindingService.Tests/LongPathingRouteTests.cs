using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using System.Collections.Generic;

namespace PathfindingService.Tests;

public class LongPathingRouteTests(NavigationFixture fixture) : IClassFixture<NavigationFixture>
{
    private const uint Kalimdor = 1;
    private const uint EasternKingdoms = 0;
    private static readonly (float Radius, float Height) TaurenMaleCapsule =
        RaceDimensions.GetCapsuleForRace(Race.Tauren, Gender.Male);

    private readonly Navigation _navigation = fixture.Navigation;

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
            new Position(1320.0f, -4649.0f, 53.0f),
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
            "orgrimmar_city_support_stall_screenshot_recovery",
            Kalimdor,
            new Position(1605.1f, -4425.0f, 10.2f),
            new Position(1320.0f, -4649.0f, 53.0f),
            8.0f,
            8.0f,
            16,
            10.0f,
            2.5f,
            2.5f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_ramp",
            Kalimdor,
            new Position(1356.8f, -4501.3f, 29.44f),
            new Position(1320.0f, -4649.0f, 53.0f),
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
            new Position(1320.0f, -4649.0f, 53.0f),
            8.0f,
            8.0f,
            24,
            6.0f,
            2.5f,
            1.25f,
            true
        ];

        yield return
        [
            "orgrimmar_zeppelin_tower_friction_recovery",
            Kalimdor,
            new Position(1338.1f, -4646.0f, 51.6f),
            new Position(1320.0f, -4649.0f, 53.0f),
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
            new Position(1320.0f, -4649.0f, 53.0f),
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
            new Position(1320.0f, -4649.0f, 53.0f),
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
            "undercity_zeppelin_arrival_to_target",
            EasternKingdoms,
            new Position(2066.0f, 288.0f, 97.0f),
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
            maxResolvedWaypointZDelta: maxResolvedWaypointZDelta,
            maxResolvedWaypointZDeltaCheckLimit: label is "orgrimmar_zeppelin_tower_exterior_support_recovery"
                or "orgrimmar_city_support_stall_screenshot_recovery"
                or "orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery"
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
            const float maxEarlySupportRise = 1.25f;
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                Assert.True(
                    path[i].Z <= start.Z + maxEarlySupportRise,
                    $"{label} waypoint {i} stayed projected into the exterior support model instead of resolving to the screenshot support layer. " +
                    $"startZ={start.Z:F1} waypoint=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}){Environment.NewLine}{FormatPath(path)}");
            }
        }
        else if (label == "orgrimmar_city_support_stall_screenshot_recovery")
        {
            var liveBlockedPoint = new XYZ(1598.7f, -4420.8f, 10.6f);
            var earlyProbeEnd = Math.Min(path.Length, 8);
            for (var i = 1; i < earlyProbeEnd; i++)
            {
                Assert.True(
                    Distance2D(path[i], liveBlockedPoint) > 2.0f,
                    $"{label} routed back through the live screenshot support-friction point instead of around the game object. " +
                    $"waypoint=({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}) blocked=({liveBlockedPoint.X:F1},{liveBlockedPoint.Y:F1},{liveBlockedPoint.Z:F1}){Environment.NewLine}{FormatPath(path)}");
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

    private static float Distance2D(XYZ from, XYZ to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
