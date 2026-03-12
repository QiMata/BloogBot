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
        float maxWalkableValidationSegmentLength = 20.0f)
    {
        if (path == null || path.Length == 0)
            return "CalculatePath returned empty path";

        if (path.Length < 2)
            return $"Path should have at least 2 waypoints, got {path.Length}";

        for (var i = 0; i < path.Length; i++)
        {
            if (!float.IsFinite(path[i].X) || !float.IsFinite(path[i].Y) || !float.IsFinite(path[i].Z))
                return $"Path waypoint {i} contains non-finite coordinates: {path[i]}";
        }

        var startDistance = Distance(path[0], requestedStart);
        if (startDistance > maxStartDistance)
            return $"Path start ({path[0]}) too far from requested start ({requestedStart}): {startDistance:F1}y";

        var endDistance = Distance(path[^1], requestedEnd);
        if (endDistance > maxEndDistance)
            return $"Path end ({path[^1]}) too far from requested end ({requestedEnd}): {endDistance:F1}y";

        var current = path[0];
        for (var i = 1; i < path.Length; i++)
        {
            var rawNext = path[i];
            var dx = rawNext.X - current.X;
            var dy = rawNext.Y - current.Y;
            var horizontal = MathF.Sqrt((dx * dx) + (dy * dy));
            if (horizontal < 0.001f && MathF.Abs(rawNext.Z - current.Z) < 0.001f)
                return $"Zero-length segment at index {i - 1}->{i}: {current} -> {rawNext}";

            if (horizontal > maxSegmentLength)
                return $"Segment {i - 1}->{i} horizontal distance {horizontal:F1}y exceeds max {maxSegmentLength}y";

            var effectiveNext = rawNext;
            if (horizontal <= maxWalkableValidationSegmentLength)
            {
                var validation = ValidateWalkableSegment(
                    mapId,
                    current,
                    rawNext,
                    radius: 0.6f,
                    height: 2.0f,
                    out var resolvedEndZ,
                    out _,
                    out var travelFraction);

                if (validation is not SegmentValidationResult.Clear and not SegmentValidationResult.MissingSupport)
                {
                    return $"Segment {i - 1}->{i} failed native walkability with {validation} " +
                        $"from={current} to={rawNext} travelFraction={travelFraction:F3}";
                }

                if (validation == SegmentValidationResult.Clear && float.IsFinite(resolvedEndZ))
                    effectiveNext = new XYZ(rawNext.X, rawNext.Y, resolvedEndZ);
            }

            var effectiveDz = effectiveNext.Z - current.Z;
            if (MathF.Abs(effectiveDz) > maxHeightJump)
            {
                return $"Segment {i - 1}->{i} height change {effectiveDz:F1}y exceeds max {maxHeightJump}y " +
                    $"from={current} to={effectiveNext}";
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
}
