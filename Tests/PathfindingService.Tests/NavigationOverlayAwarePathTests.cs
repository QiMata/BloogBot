using GameData.Core.Models;
using PathfindingService.Repository;
using System;

namespace PathfindingService.Tests;

public class NavigationOverlayAwarePathTests
{
    private static readonly XYZ Start = new(0f, 0f, 0f);
    private static readonly XYZ End = new(10f, 0f, 0f);

    [Fact]
    public void CalculateValidatedPath_UsesAlternateMode_WhenPreferredPathIsBlocked()
    {
        var alternateMid = new XYZ(5f, 3f, 0f);
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) =>
            {
                if (start.Equals(Start) && end.Equals(End) && smoothPath)
                    return [Start, End];

                if (start.Equals(Start) && end.Equals(End) && !smoothPath)
                    return [Start, alternateMid, End];

                return Array.Empty<XYZ>();
            },
            (mapId, from, to) => IntersectsFlatCorridor(from, to));

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("native_path_alternate_mode", result.Result);
        Assert.Equal("none", result.BlockedReason);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal([Start, alternateMid, End], result.Path);
        Assert.Equal([Start, alternateMid, End], result.RawPath);
    }

    [Fact]
    public void CalculateValidatedPath_RepairsBlockedPath_WithDetourCandidate()
    {
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [start, end],
            (mapId, from, to) => IntersectsFlatCorridor(from, to));

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("repaired_dynamic_overlay", result.Result);
        Assert.Equal("dynamic_overlay", result.BlockedReason);
        Assert.Equal(0, result.BlockedSegmentIndex);
        Assert.Equal([Start, End], result.RawPath);
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
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [start, end],
            (mapId, from, to) => SegmentCrossesBlockedSpan(from, to));

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("blocked_by_dynamic_overlay", result.Result);
        Assert.Equal("dynamic_overlay", result.BlockedReason);
        Assert.Equal(0, result.BlockedSegmentIndex);
        Assert.Empty(result.Path);
        Assert.Equal([Start, End], result.RawPath);
    }

    [Fact]
    public void CalculateValidatedPath_PreservesDetailedDynamicOverlayReason_WhenResolverProvidesIdentity()
    {
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [start, end],
            (mapId, from, to) => true,
            (mapId, from, to) => "dynamic_overlay,display=455,guid=0xA000000000000001");

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("blocked_by_dynamic_overlay", result.Result);
        Assert.Equal("dynamic_overlay,display=455,guid=0xA000000000000001", result.BlockedReason);
        Assert.Equal(0, result.BlockedSegmentIndex);
        Assert.Empty(result.Path);
        Assert.Equal([Start, End], result.RawPath);
    }

    [Fact]
    public void CalculateValidatedPath_TrustsNativeRepairedOverlayMetadata_WithoutManagedRediscovery()
    {
        var repairedPath = new[] { Start, new XYZ(5f, 3f, 0f), End };
        var segmentProbeCalls = 0;
        const string blockedReason = "dynamic_overlay,display=455,guid=0xA000000000000001";
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => new NativePathResolution(
                repairedPath,
                BlockedSegmentIndex: 0,
                BlockedReason: blockedReason,
                WasRepairedAroundBlockedSegment: true),
            (mapId, from, to) =>
            {
                segmentProbeCalls++;
                return true;
            });

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("repaired_dynamic_overlay", result.Result);
        Assert.Equal(blockedReason, result.BlockedReason);
        Assert.Equal(0, result.BlockedSegmentIndex);
        Assert.Equal(repairedPath, result.Path);
        Assert.Equal(repairedPath, result.RawPath);
        Assert.Equal(0, segmentProbeCalls);
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
