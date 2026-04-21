using GameData.Core.Models;
using Pathfinding;
using PathfindingService;
using PathfindingService.Repository;

namespace PathfindingService.Tests;

public sealed class PathAffordanceClassifierTests
{
    [Fact]
    public void Summarize_UsesNativeClassificationsAndMetrics()
    {
        var path = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(1f, 0f, 1f),
            new XYZ(2f, 0f, 1f),
            new XYZ(3f, 0f, -2f),
            new XYZ(4f, 0f, -18f),
            new XYZ(5f, 0f, -18f),
        };
        var nativeResults = new[]
        {
            new NativeSegmentAffordanceResult(NativeSegmentAffordance.StepUp, 0, 1f, 0f, 0f, 16f, 1f),
            new NativeSegmentAffordanceResult(NativeSegmentAffordance.JumpGap, 2, 0f, 3.5f, 0f, 0f, 1f),
            new NativeSegmentAffordanceResult(NativeSegmentAffordance.SafeDrop, 4, 0f, 0f, 3f, 35f, -2f),
            new NativeSegmentAffordanceResult(NativeSegmentAffordance.UnsafeDrop, 4, 0f, 0f, 16f, 70f, -18f),
            new NativeSegmentAffordanceResult(NativeSegmentAffordance.Blocked, 1, 0f, 0f, 0f, 0f, -18f),
        };
        var callIndex = 0;

        var summary = PathAffordanceClassifier.Summarize(
            1,
            path,
            agentRadius: 0.6f,
            agentHeight: 2.0f,
            classifySegment: (_, _, _, _, _) => nativeResults[callIndex++]);

        Assert.Equal(path.Length - 1, callIndex);
        Assert.Equal(PathSegmentAffordance.Blocked, summary.MaxAffordance);
        Assert.False(summary.PathSupported);
        Assert.Equal(1, summary.StepUpCount);
        Assert.Equal(1, summary.JumpGapCount);
        Assert.Equal(1, summary.DropCount);
        Assert.Equal(1, summary.SafeDropCount);
        Assert.Equal(1, summary.CliffCount);
        Assert.Equal(1, summary.UnsafeDropCount);
        Assert.Equal(1, summary.BlockedCount);
        Assert.Equal(1f, summary.TotalZGain);
        Assert.Equal(19f, summary.TotalZLoss);
        Assert.Equal(70f, summary.MaxSlopeAngleDeg);
        Assert.Equal(1f, summary.MaxClimbHeight);
        Assert.Equal(3.5f, summary.MaxGapDistance);
        Assert.Equal(16f, summary.MaxDropHeight);
    }

    [Fact]
    public void Summarize_EmptyPath_IsUnsupportedWalk()
    {
        var summary = PathAffordanceClassifier.Summarize(
            1,
            [],
            agentRadius: 0.6f,
            agentHeight: 2.0f,
            classifySegment: (_, _, _, _, _) => throw new InvalidOperationException("should not classify"));

        Assert.Equal(PathSegmentAffordance.Walk, summary.MaxAffordance);
        Assert.False(summary.PathSupported);
        Assert.Equal(0, summary.BlockedCount);
    }
}
