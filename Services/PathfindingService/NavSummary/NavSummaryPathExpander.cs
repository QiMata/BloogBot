using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using System.Collections.Generic;

namespace PathfindingService.NavSummary;

public sealed record NavSummaryRouteMatch(
    string GraphId,
    string GraphSignature,
    int AnchorCount,
    int SegmentCount,
    float EstimatedCost);

public sealed record NavSummaryResolution(
    NavigationPathResult PathResult,
    NavSummaryRouteMatch Match);

public sealed class NavSummaryPathExpander(NavSummaryOptions options)
{
    private const float DuplicatePointEpsilon = 0.25f;

    public bool TryExpand(
        NavSummaryRoutePlan plan,
        NavSummaryRouteRequest request,
        Func<XYZ, XYZ, NavigationPathResult> detailedPathResolver,
        out NavSummaryResolution resolution,
        out string failureReason)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(detailedPathResolver);

        resolution = default!;
        failureReason = string.Empty;

        var routePoints = BuildRoutePoints(request.Start, plan.Anchors, request.End);
        if (routePoints.Count < 2)
        {
            failureReason = "not_enough_summary_points";
            return false;
        }

        var segmentCount = CountNonTrivialSegments(routePoints);
        if (segmentCount == 0)
        {
            failureReason = "no_nontrivial_summary_segments";
            return false;
        }

        if (segmentCount > options.MaxExpandedSegments)
        {
            failureReason = $"too_many_segments:{segmentCount}";
            return false;
        }

        var path = new List<XYZ>();
        var rawPath = new List<XYZ>();
        for (var i = 1; i < routePoints.Count; i++)
        {
            var segmentStart = routePoints[i - 1];
            var segmentEnd = routePoints[i];
            if (NavSummaryRoutePlanner.Distance3D(segmentStart, segmentEnd) <= DuplicatePointEpsilon)
                continue;

            var segment = detailedPathResolver(segmentStart, segmentEnd);
            if (!IsUsableSegment(segmentStart, segmentEnd, segment, out failureReason))
                return false;

            AppendDistinct(path, segment.Path);
            AppendDistinct(rawPath, segment.RawPath.Length > 0 ? segment.RawPath : segment.Path);
        }

        if (path.Count == 0)
        {
            failureReason = "expanded_path_empty";
            return false;
        }

        var result = new NavigationPathResult(
            path.ToArray(),
            rawPath.Count > 0 ? rawPath.ToArray() : path.ToArray(),
            "nav_summary_expanded",
            null,
            "none");
        var match = new NavSummaryRouteMatch(
            plan.LoadedGraph.Graph.Id,
            plan.LoadedGraph.Signature,
            plan.Anchors.Count,
            segmentCount,
            plan.EstimatedCost);
        resolution = new NavSummaryResolution(result, match);
        return true;
    }

    private bool IsUsableSegment(
        XYZ requestedStart,
        XYZ requestedEnd,
        NavigationPathResult segment,
        out string failureReason)
    {
        if (segment.Path.Length == 0)
        {
            failureReason = "segment_no_path";
            return false;
        }

        if (string.Equals(segment.Result, "no_path", StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "segment_result_no_path";
            return false;
        }

        if (segment.BlockedSegmentIndex.HasValue)
        {
            failureReason = $"segment_blocked:{segment.BlockedSegmentIndex.Value}";
            return false;
        }

        if (!string.Equals(segment.BlockedReason, "none", StringComparison.OrdinalIgnoreCase))
        {
            failureReason = $"segment_blocked_reason:{segment.BlockedReason}";
            return false;
        }

        if (NavSummaryRoutePlanner.Distance3D(segment.Path[0], requestedStart) > options.MaxDetailEndpointDistance)
        {
            failureReason = "segment_start_snap_too_far";
            return false;
        }

        if (NavSummaryRoutePlanner.Distance3D(segment.Path[^1], requestedEnd) > options.MaxDetailEndpointDistance)
        {
            failureReason = "segment_end_snap_too_far";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static List<XYZ> BuildRoutePoints(XYZ start, IReadOnlyList<XYZ> anchors, XYZ end)
    {
        var routePoints = new List<XYZ>(anchors.Count + 2);
        AppendDistinct(routePoints, [start]);
        AppendDistinct(routePoints, anchors);
        AppendDistinct(routePoints, [end]);
        return routePoints;
    }

    private static int CountNonTrivialSegments(IReadOnlyList<XYZ> routePoints)
    {
        var count = 0;
        for (var i = 1; i < routePoints.Count; i++)
        {
            if (NavSummaryRoutePlanner.Distance3D(routePoints[i - 1], routePoints[i]) > DuplicatePointEpsilon)
                count++;
        }

        return count;
    }

    private static void AppendDistinct(List<XYZ> target, IReadOnlyList<XYZ> points)
    {
        foreach (var point in points)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                continue;

            if (target.Count > 0 && NavSummaryRoutePlanner.Distance3D(target[^1], point) <= DuplicatePointEpsilon)
                continue;

            target.Add(point);
        }
    }
}
