using GameData.Core.Models;
using PathfindingService.Repository;
using System;

namespace PathfindingService.Tests;

public class NavigationOverlayAwarePathTests
{
    private static readonly XYZ Start = new(0f, 0f, 0f);
    private static readonly XYZ End = new(10f, 0f, 0f);

    // Mid point sits before the blocked corridor (x=3..7), so segment 0 (Start→Mid) is clear
    // and segment 1 (Mid→End) crosses the corridor and gets blocked.
    private static readonly XYZ Mid = new(2f, 0f, 0f);

    [Fact]
    public void CalculateValidatedPath_UsesAlternateMode_WhenPreferredPathIsBlocked()
    {
        // Preferred (smooth) path: [Start, Mid, End] — segment 1 (Mid→End) is blocked.
        // Segment-0-skip does NOT apply because the blocked segment is at index 1.
        // Alternate (non-smooth) path: [Start, alternateMid, End] — all segments clear.
        var alternateMid = new XYZ(5f, 3f, 0f);
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) =>
            {
                if (start.Equals(Start) && end.Equals(End) && smoothPath)
                    return [Start, Mid, End];

                if (start.Equals(Start) && end.Equals(End) && !smoothPath)
                    return [Start, alternateMid, End];

                return Array.Empty<XYZ>();
            },
            (mapId, from, to) => IntersectsFlatCorridor(from, to)
                ? Navigation.SegmentBlockReason.DynamicOverlay
                : Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("native_path_alternate_mode", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal([Start, alternateMid, End], result.Path);
        Assert.Equal([Start, alternateMid, End], result.RawPath);
    }

    [Fact]
    public void CalculateValidatedPath_Segment0Skip_ReturnsPathWhenOnlySegment0IsBlocked()
    {
        // 2-point path [Start, End] where segment 0 is blocked.
        // The segment-0-skip logic accepts the path because the player is already
        // standing at Start, so Start→End is traversable by definition.
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [start, end],
            (mapId, from, to) => IntersectsFlatCorridor(from, to)
                ? Navigation.SegmentBlockReason.DynamicOverlay
                : Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("native_path_seg0skip", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal([Start, End], result.Path);
    }

    [Fact]
    public void CalculateValidatedPath_RepairsBlockedPath_WithDetourCandidate()
    {
        // 3-point path [Start, Mid, End] — segment 1 (Mid→End) crosses the corridor.
        // Segment 0 is clear, so seg0skip does not apply.  Repair should find a detour.
        // Sub-path requests (repair legs) return direct 2-point paths so repair candidates
        // aren't polluted with the Mid waypoint.
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) =>
                start.Equals(Start) && end.Equals(End) ? [Start, Mid, End] : [start, end],
            (mapId, from, to) => IntersectsFlatCorridor(from, to)
                ? Navigation.SegmentBlockReason.DynamicOverlay
                : Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("repaired_dynamic_overlay", result.Result);
        Assert.Equal(1, result.BlockedSegmentIndex);
        Assert.Equal([Start, Mid, End], result.RawPath);
        Assert.True(result.Path.Length >= 3, $"Expected repaired detour path, got {result.Path.Length} waypoints");
        Assert.Equal(Start, result.Path[0]);
        Assert.Equal(End, result.Path[^1]);

        for (var i = 0; i < result.Path.Length - 1; i++)
        {
            Assert.False(
                IntersectsFlatCorridor(result.Path[i], result.Path[i + 1]),
                $"Repaired segment {i}->{i + 1} still intersects the blocked corridor");
        }
    }

    [Fact]
    public void CalculateValidatedPath_ReturnsBlockedResult_WhenNoRepairCandidateWorks()
    {
        // 3-point path [Start, Mid, End] — segment 1 (Mid→End) crosses the wide blocked span.
        // SegmentCrossesBlockedSpan blocks all repair candidates too.
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [start, Mid, end],
            (mapId, from, to) => SegmentCrossesBlockedSpan(from, to)
                ? Navigation.SegmentBlockReason.DynamicOverlay
                : Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("blocked_by_dynamic_overlay", result.Result);
        Assert.Equal(1, result.BlockedSegmentIndex);
        Assert.Empty(result.Path);
        Assert.Equal([Start, Mid, End], result.RawPath);
    }

    [Fact]
    public void CalculateValidatedPath_RepairsCapsuleValidation_WithSegmentValidationResult()
    {
        // 3-point path [Start, Mid, End] — segment 1 (Mid→End) crosses the corridor.
        // Sub-path requests (repair legs) return direct 2-point paths.
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) =>
                start.Equals(Start) && end.Equals(End) ? [Start, Mid, End] : [start, end],
            (mapId, from, to) => IntersectsFlatCorridor(from, to)
                ? Navigation.SegmentBlockReason.CapsuleValidation
                : Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("repaired_segment_validation", result.Result);
        Assert.Equal(1, result.BlockedSegmentIndex);
        Assert.True(result.Path.Length >= 3);
    }

    [Fact]
    public void CalculateValidatedPath_ReturnsSpecificStepDownReason_WhenSegmentFailsWalkability()
    {
        // 3-point path [Start, Mid, End] — segment 1 (Mid→End) crosses the wide blocked span.
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [start, Mid, end],
            (mapId, from, to) => SegmentCrossesBlockedSpan(from, to)
                ? Navigation.SegmentBlockReason.StepDownLimit
                : Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("blocked_by_step_down_limit", result.Result);
        Assert.Equal(1, result.BlockedSegmentIndex);
        Assert.Empty(result.Path);
    }

    [Fact]
    public void CalculateValidatedPath_CarriesGroundedSegmentEnds_AcrossValidation()
    {
        var groundedMid = new XYZ(5f, 0f, 5f);
        var rawMid = new XYZ(5f, 0f, 1f);
        var rawEnd = new XYZ(10f, 0f, 2f);
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [Start, rawMid, rawEnd],
            (mapId, from, to) =>
            {
                if (to.Equals(rawMid))
                    return new Navigation.SegmentEvaluation(groundedMid, Navigation.SegmentBlockReason.None);

                return MathF.Abs(from.Z - groundedMid.Z) < 0.01f
                    ? new Navigation.SegmentEvaluation(rawEnd, Navigation.SegmentBlockReason.None)
                    : new Navigation.SegmentEvaluation(rawEnd, Navigation.SegmentBlockReason.StepUpLimit);
            });

        var result = navigation.CalculateValidatedPath(1, Start, rawEnd, smoothPath: true);

        Assert.Equal("native_path", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal([Start, rawMid, rawEnd], result.Path);
    }

    [Fact]
    public void CalculateValidatedPath_LongStraightRequest_UsesAlternateSmoothModeFirst()
    {
        var start = new XYZ(0f, 0f, 0f);
        var end = new XYZ(250f, 0f, 0f);
        var smoothPath = new XYZ(125f, 5f, 0f);
        var requestedModes = new List<bool>();
        var navigation = new Navigation(
            (mapId, from, to, useSmoothPath) =>
            {
                requestedModes.Add(useSmoothPath);
                return useSmoothPath
                    ? [start, smoothPath, end]
                    : throw new InvalidOperationException("Long straight mode should not run when the alternate smooth route is already usable.");
            },
            (mapId, from, to) => Navigation.SegmentBlockReason.None);

        var result = navigation.CalculateValidatedPath(1, start, end, smoothPath: false);

        Assert.Equal("native_path_alternate_mode", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal([true], requestedModes);
        Assert.Equal([start, smoothPath, end], result.Path);
    }

    private static bool IntersectsFlatCorridor(XYZ from, XYZ to)
    {
        for (var i = 0; i <= 32; i++)
        {
            var t = i / 32f;
            var x = from.X + ((to.X - from.X) * t);
            var y = from.Y + ((to.Y - from.Y) * t);
            if (x >= 3f && x <= 7f && MathF.Abs(y) <= 1f)
                return true;
        }

        return false;
    }

    private static bool SegmentCrossesBlockedSpan(XYZ from, XYZ to)
    {
        var minX = MathF.Min(from.X, to.X);
        var maxX = MathF.Max(from.X, to.X);
        return minX <= 7f && maxX >= 3f;
    }
}
