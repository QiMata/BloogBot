using GameData.Core.Models;
using PathfindingService.Repository;
using System;

namespace PathfindingService.Tests;

public sealed class NavigationOverlayAwarePathTests
{
    private static readonly XYZ Start = new(0f, 0f, 0f);
    private static readonly XYZ End = new(10f, 0f, 0f);

    [Fact]
    public void CalculateValidatedPath_UsesOnlyRequestedRawNativeMode()
    {
        var alternateMid = new XYZ(5f, 3f, 0f);
        var smoothCalls = 0;
        var straightCalls = 0;
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) =>
            {
                if (smoothPath)
                {
                    smoothCalls++;
                    return [Start, End];
                }

                straightCalls++;
                return [Start, alternateMid, End];
            },
            (mapId, from, to) => true);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("raw_detour", result.Result);
        Assert.Equal("none", result.BlockedReason);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal([Start, End], result.Path);
        Assert.Equal([Start, End], result.RawPath);
        Assert.Equal(1, smoothCalls);
        Assert.Equal(0, straightCalls);
    }

    [Fact]
    public void CalculateValidatedPath_RecordsValidatedRequestWithoutManagedRepairMetrics()
    {
        NavigationPerformanceMetrics.ResetForTests();
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [Start, End],
            (mapId, from, to) => true);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);
        var stats = NavigationPerformanceMetrics.Snapshot;

        Assert.Equal("raw_detour", result.Result);
        Assert.Equal(1, stats.ValidatedPathRequests);
        Assert.Equal(0, stats.ManagedValidationRuns);
        Assert.Equal(0, stats.PathResolverAttempts);
        Assert.Equal(0, stats.DynamicOverlayRepairCount);
        Assert.Equal(0, stats.SegmentValidationRepairCount);
        Assert.Equal(0, stats.LocalPhysicsLayerRepairCount);
        Assert.Equal(0, stats.SteepAffordanceRepairCount);
        Assert.Equal(0, stats.StaticWallRepairCount);
        Assert.Equal(0, stats.LongLineOfSightRepairCount);
    }

    [Fact]
    public void CalculateValidatedPath_DoesNotProbeManagedSegmentBlockers()
    {
        var segmentProbeCalls = 0;
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => [Start, End],
            (mapId, from, to) =>
            {
                segmentProbeCalls++;
                return true;
            });

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("raw_detour", result.Result);
        Assert.Equal([Start, End], result.Path);
        Assert.Equal(0, segmentProbeCalls);
    }

    [Fact]
    public void CalculateValidatedPath_ReturnsNoPathWithoutManagedDetourCandidate()
    {
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => Array.Empty<XYZ>(),
            (mapId, from, to) => true);

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("no_path", result.Result);
        Assert.Equal("none", result.BlockedReason);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Empty(result.Path);
        Assert.Empty(result.RawPath);
    }

    [Fact]
    public void CalculateValidatedPath_PreservesNativeBlockedMetadataWithoutManagedRediscovery()
    {
        var rawPath = new[] { Start, new XYZ(5f, 3f, 0f), End };
        var segmentProbeCalls = 0;
        const string blockedReason = "dynamic_overlay,display=455,guid=0xA000000000000001";
        var navigation = new Navigation(
            (mapId, start, end, smoothPath) => new NativePathResolution(
                rawPath,
                BlockedSegmentIndex: 0,
                BlockedReason: blockedReason,
                WasRepairedAroundBlockedSegment: true),
            (mapId, from, to) =>
            {
                segmentProbeCalls++;
                return true;
            });

        var result = navigation.CalculateValidatedPath(1, Start, End, smoothPath: true);

        Assert.Equal("raw_detour", result.Result);
        Assert.Equal(blockedReason, result.BlockedReason);
        Assert.Equal(0, result.BlockedSegmentIndex);
        Assert.Equal(rawPath, result.Path);
        Assert.Equal(rawPath, result.RawPath);
        Assert.Equal(0, segmentProbeCalls);
    }
}
