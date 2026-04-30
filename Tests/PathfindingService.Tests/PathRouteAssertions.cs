using GameData.Core.Models;
using System.Runtime.InteropServices;

namespace PathfindingService.Tests;

internal static class PathRouteAssertions
{
    private const string NavigationDll = "Navigation.dll";

    [DllImport(NavigationDll, EntryPoint = "ValidateWalkableSegment", CallingConvention = CallingConvention.Cdecl)]
    private static extern SegmentValidationResult ValidateWalkableSegment(
        uint mapId,
        XYZ start,
        XYZ end,
        float radius,
        float height,
        out float resolvedEndZ,
        out float supportDelta,
        out float travelFraction);

    [DllImport(NavigationDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LineOfSight(uint mapId, XYZ from, XYZ to);

    [DllImport(NavigationDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern float GetGroundZ(
        uint mapId,
        float x,
        float y,
        float z,
        float maxSearchDist);

    private enum SegmentValidationResult : uint
    {
        Clear = 0,
        BlockedGeometry = 1,
        MissingSupport = 2,
        StepUpTooHigh = 3,
        StepDownTooFar = 4,
    }

    public static string? GetValidationFailure(
        uint mapId,
        XYZ requestedStart,
        XYZ requestedEnd,
        XYZ[]? path,
        float maxStartDistance = 5.0f,
        float maxEndDistance = 5.0f,
        float maxSegmentLength = 50.0f,
        float maxHeightJump = 10.0f,
        float maxWalkableValidationSegmentLength = 20.0f,
        int maxWalkableValidationChecks = int.MaxValue,
        float minLineOfSightValidationSegmentLength = 0.0f,
        float maxResolvedWaypointZDelta = float.PositiveInfinity,
        int maxResolvedWaypointZDeltaCheckLimit = int.MaxValue,
        float agentRadius = 0.6f,
        float agentHeight = 2.0f)
    {
        if (path == null || path.Length == 0)
            return "CalculatePath returned empty path";

        if (path.Length < 2)
            return $"Path should have at least 2 waypoints, got {path.Length}";

        for (var i = 0; i < path.Length; i++)
        {
            if (!float.IsFinite(path[i].X) || !float.IsFinite(path[i].Y) || !float.IsFinite(path[i].Z))
                return $"Path waypoint {i} contains non-finite coordinates: {path[i]}";

            if (i > 0
                && i <= maxResolvedWaypointZDeltaCheckLimit
                && !float.IsPositiveInfinity(maxResolvedWaypointZDelta))
            {
                var groundZ = GetGroundZ(mapId, path[i].X, path[i].Y, path[i].Z, 4.0f);
                if (float.IsFinite(groundZ)
                    && groundZ > -100000f
                    && MathF.Abs(groundZ - path[i].Z) > maxResolvedWaypointZDelta)
                {
                    return $"Path waypoint {i} floats {path[i].Z - groundZ:F1}y from collision support: " +
                        $"waypoint={Format(path[i])} supportZ={groundZ:F3}";
                }
            }
        }

        var startDistance = Distance(path[0], requestedStart);
        if (startDistance > maxStartDistance)
            return $"Path start {Format(path[0])} too far from requested start {Format(requestedStart)}: {startDistance:F1}y";

        var endDistance = Distance(path[^1], requestedEnd);
        if (endDistance > maxEndDistance)
            return $"Path end {Format(path[^1])} too far from requested end {Format(requestedEnd)}: {endDistance:F1}y";

        var current = path[0];
        var walkableValidationChecks = 0;
        for (var i = 1; i < path.Length; i++)
        {
            var rawNext = path[i];
            var dx = rawNext.X - current.X;
            var dy = rawNext.Y - current.Y;
            var horizontal = MathF.Sqrt((dx * dx) + (dy * dy));
            if (horizontal < 0.001f && MathF.Abs(rawNext.Z - current.Z) < 0.001f)
                return $"Zero-length segment at index {i - 1}->{i}: {Format(current)} -> {Format(rawNext)}";

            if (horizontal > maxSegmentLength)
                return $"Segment {i - 1}->{i} horizontal distance {horizontal:F1}y exceeds max {maxSegmentLength}y";

            var effectiveNext = rawNext;
            if (minLineOfSightValidationSegmentLength > 0
                && horizontal >= minLineOfSightValidationSegmentLength
                && !LineOfSight(mapId, current, rawNext))
            {
                return $"Segment {i - 1}->{i} failed static line-of-sight from={Format(current)} to={Format(rawNext)}";
            }

            if (horizontal <= maxWalkableValidationSegmentLength
                && walkableValidationChecks < maxWalkableValidationChecks)
            {
                walkableValidationChecks++;
                var validation = ValidateWalkableSegment(
                    mapId,
                    current,
                    rawNext,
                    radius: agentRadius,
                    height: agentHeight,
                    out var resolvedEndZ,
                    out _,
                    out var travelFraction);

                if (validation is not SegmentValidationResult.Clear and not SegmentValidationResult.MissingSupport
                    && !IsInconsistentUphillStepDown(validation, current, rawNext))
                {
                    return $"Segment {i - 1}->{i} failed native walkability with {validation} " +
                        $"from={Format(current)} to={Format(rawNext)} travelFraction={travelFraction:F3}";
                }

                if (validation == SegmentValidationResult.Clear && float.IsFinite(resolvedEndZ))
                {
                    if (i <= maxResolvedWaypointZDeltaCheckLimit
                        && MathF.Abs(resolvedEndZ - rawNext.Z) > maxResolvedWaypointZDelta)
                    {
                        return $"Segment {i - 1}->{i} endpoint Z differs from native support by {resolvedEndZ - rawNext.Z:F1}y " +
                            $"from={Format(current)} to={Format(rawNext)} resolvedEndZ={resolvedEndZ:F3}";
                    }

                    effectiveNext = new XYZ(rawNext.X, rawNext.Y, resolvedEndZ);
                }
            }

            var effectiveDz = effectiveNext.Z - current.Z;
            if (MathF.Abs(effectiveDz) > maxHeightJump)
            {
                return $"Segment {i - 1}->{i} height change {effectiveDz:F1}y exceeds max {maxHeightJump}y " +
                    $"from={Format(current)} to={Format(effectiveNext)}";
            }

            current = effectiveNext;
        }

        return null;
    }

    private static float Distance(XYZ a, XYZ b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static bool IsInconsistentUphillStepDown(SegmentValidationResult validation, XYZ from, XYZ to)
        => validation == SegmentValidationResult.StepDownTooFar
            && to.Z >= from.Z - 0.25f;

    private static string Format(XYZ point) => $"({point.X:F1},{point.Y:F1},{point.Z:F1})";
}
