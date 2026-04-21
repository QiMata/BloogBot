using GameData.Core.Models;
using Pathfinding;
using PathfindingService.Repository;
using System;
using System.Diagnostics;

namespace PathfindingService;

public readonly record struct RouteAffordanceSummary(
    PathSegmentAffordance MaxAffordance,
    bool PathSupported,
    int StepUpCount,
    int DropCount,
    int CliffCount,
    int VerticalCount,
    int JumpGapCount,
    int SafeDropCount,
    int UnsafeDropCount,
    int BlockedCount,
    float TotalZGain,
    float TotalZLoss,
    float MaxSlopeAngleDeg,
    float MaxClimbHeight,
    float MaxGapDistance,
    float MaxDropHeight)
{
    public static RouteAffordanceSummary Empty => new(
        MaxAffordance: PathSegmentAffordance.Walk,
        PathSupported: false,
        StepUpCount: 0,
        DropCount: 0,
        CliffCount: 0,
        VerticalCount: 0,
        JumpGapCount: 0,
        SafeDropCount: 0,
        UnsafeDropCount: 0,
        BlockedCount: 0,
        TotalZGain: 0f,
        TotalZLoss: 0f,
        MaxSlopeAngleDeg: 0f,
        MaxClimbHeight: 0f,
        MaxGapDistance: 0f,
        MaxDropHeight: 0f);
}

public static class PathAffordanceClassifier
{
    private static readonly TimeSpan NativeClassificationBudget = TimeSpan.FromMilliseconds(750);
    private const float MaxNativeClassificationSegmentLength = 12.0f;
    private const float FallSafeDistance = 14.57f;

    public static RouteAffordanceSummary Summarize(
        uint mapId,
        XYZ[] path,
        float agentRadius,
        float agentHeight)
        => IsNativeSummaryEnabled()
            ? SummarizeCore(
                mapId,
                path,
                agentRadius,
                agentHeight,
                static (m, start, end, radius, height) =>
                    Navigation.ClassifySegmentAffordance(m, start, end, radius, height),
                useNativeBudget: true)
            : SummarizeCore(
                mapId,
                path,
                agentRadius,
                agentHeight,
                static (_, start, end, _, _) => ClassifyGeometricFallback(start, end),
                useNativeBudget: false);

    public static RouteAffordanceSummary Summarize(
        uint mapId,
        XYZ[] path,
        float agentRadius,
        float agentHeight,
        Func<uint, XYZ, XYZ, float, float, NativeSegmentAffordanceResult> classifySegment)
        => SummarizeCore(mapId, path, agentRadius, agentHeight, classifySegment, useNativeBudget: false);

    private static RouteAffordanceSummary SummarizeCore(
        uint mapId,
        XYZ[] path,
        float agentRadius,
        float agentHeight,
        Func<uint, XYZ, XYZ, float, float, NativeSegmentAffordanceResult> classifySegment,
        bool useNativeBudget)
    {
        if (path.Length < 2)
            return RouteAffordanceSummary.Empty;

        var stopwatch = useNativeBudget ? Stopwatch.StartNew() : null;
        var maxAffordance = PathSegmentAffordance.Walk;
        var stepUpCount = 0;
        var dropCount = 0;
        var cliffCount = 0;
        var verticalCount = 0;
        var jumpGapCount = 0;
        var safeDropCount = 0;
        var unsafeDropCount = 0;
        var blockedCount = 0;
        var totalZGain = 0f;
        var totalZLoss = 0f;
        var maxSlopeDeg = 0f;
        var maxClimbHeight = 0f;
        var maxGapDistance = 0f;
        var maxDropHeight = 0f;

        for (var i = 0; i < path.Length - 1; i++)
        {
            var native = ClassifySegmentWithinBudget(
                mapId,
                path[i],
                path[i + 1],
                agentRadius,
                agentHeight,
                classifySegment,
                stopwatch);
            var affordance = MapNativeAffordance(native.Affordance);
            if (GetSeverity(affordance) > GetSeverity(maxAffordance))
            {
                maxAffordance = affordance;
            }

            switch (affordance)
            {
                case PathSegmentAffordance.StepUp:
                    stepUpCount++;
                    break;
                case PathSegmentAffordance.SteepClimb:
                    stepUpCount++;
                    break;
                case PathSegmentAffordance.Drop:
                    dropCount++;
                    break;
                case PathSegmentAffordance.Cliff:
                    cliffCount++;
                    break;
                case PathSegmentAffordance.Vertical:
                    verticalCount++;
                    break;
                case PathSegmentAffordance.JumpGap:
                    jumpGapCount++;
                    break;
                case PathSegmentAffordance.SafeDrop:
                    dropCount++;
                    safeDropCount++;
                    break;
                case PathSegmentAffordance.UnsafeDrop:
                    cliffCount++;
                    unsafeDropCount++;
                    break;
                case PathSegmentAffordance.Blocked:
                    blockedCount++;
                    break;
            }

            totalZGain += MathF.Max(0f, native.ClimbHeight);
            totalZLoss += MathF.Max(0f, native.DropHeight);
            maxSlopeDeg = MathF.Max(maxSlopeDeg, native.SlopeAngleDeg);
            maxClimbHeight = MathF.Max(maxClimbHeight, native.ClimbHeight);
            maxGapDistance = MathF.Max(maxGapDistance, native.GapDistance);
            maxDropHeight = MathF.Max(maxDropHeight, native.DropHeight);
        }

        return new RouteAffordanceSummary(
            MaxAffordance: maxAffordance,
            PathSupported: blockedCount == 0 && cliffCount == 0 && unsafeDropCount == 0,
            StepUpCount: stepUpCount,
            DropCount: dropCount,
            CliffCount: cliffCount,
            VerticalCount: verticalCount,
            JumpGapCount: jumpGapCount,
            SafeDropCount: safeDropCount,
            UnsafeDropCount: unsafeDropCount,
            BlockedCount: blockedCount,
            TotalZGain: totalZGain,
            TotalZLoss: totalZLoss,
            MaxSlopeAngleDeg: maxSlopeDeg,
            MaxClimbHeight: maxClimbHeight,
            MaxGapDistance: maxGapDistance,
            MaxDropHeight: maxDropHeight);
    }

    private static NativeSegmentAffordanceResult ClassifySegmentWithinBudget(
        uint mapId,
        XYZ start,
        XYZ end,
        float agentRadius,
        float agentHeight,
        Func<uint, XYZ, XYZ, float, float, NativeSegmentAffordanceResult> classifySegment,
        Stopwatch? stopwatch)
    {
        var horizontalDistance = Distance2D(start, end);
        if (stopwatch is not null
            && (horizontalDistance > MaxNativeClassificationSegmentLength
                || stopwatch.Elapsed > NativeClassificationBudget))
        {
            return ClassifyGeometricFallback(start, end);
        }

        try
        {
            return classifySegment(mapId, start, end, agentRadius, agentHeight);
        }
        catch
        {
            return ClassifyGeometricFallback(start, end);
        }
    }

    private static NativeSegmentAffordanceResult ClassifyGeometricFallback(XYZ start, XYZ end)
    {
        var horizontalDistance = Distance2D(start, end);
        var dz = end.Z - start.Z;
        var absDz = MathF.Abs(dz);
        var slopeDeg = horizontalDistance > 0.01f
            ? MathF.Atan2(absDz, horizontalDistance) * (180f / MathF.PI)
            : (absDz > 0.5f ? 90f : 0f);
        var climbHeight = MathF.Max(0f, dz);
        var dropHeight = MathF.Max(0f, -dz);

        NativeSegmentAffordance affordance;
        if (horizontalDistance < 0.5f && absDz > 2f)
        {
            affordance = NativeSegmentAffordance.Vertical;
        }
        else if (dropHeight > FallSafeDistance)
        {
            affordance = NativeSegmentAffordance.UnsafeDrop;
        }
        else if (dropHeight > 2f)
        {
            affordance = NativeSegmentAffordance.SafeDrop;
        }
        else if (slopeDeg > 45f && climbHeight > 0f)
        {
            affordance = NativeSegmentAffordance.SteepClimb;
        }
        else if (climbHeight > 1f || (slopeDeg > 15f && climbHeight > 0f))
        {
            affordance = NativeSegmentAffordance.StepUp;
        }
        else
        {
            affordance = NativeSegmentAffordance.Walk;
        }

        return new NativeSegmentAffordanceResult(
            affordance,
            ValidationCode: 0,
            ClimbHeight: climbHeight,
            GapDistance: 0f,
            DropHeight: dropHeight,
            SlopeAngleDeg: slopeDeg,
            ResolvedEndZ: end.Z);
    }

    private static float Distance2D(XYZ start, XYZ end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static PathSegmentAffordance MapNativeAffordance(NativeSegmentAffordance affordance)
        => affordance switch
        {
            NativeSegmentAffordance.StepUp => PathSegmentAffordance.StepUp,
            NativeSegmentAffordance.SteepClimb => PathSegmentAffordance.SteepClimb,
            NativeSegmentAffordance.Drop => PathSegmentAffordance.Drop,
            NativeSegmentAffordance.Cliff => PathSegmentAffordance.Cliff,
            NativeSegmentAffordance.Vertical => PathSegmentAffordance.Vertical,
            NativeSegmentAffordance.JumpGap => PathSegmentAffordance.JumpGap,
            NativeSegmentAffordance.SafeDrop => PathSegmentAffordance.SafeDrop,
            NativeSegmentAffordance.UnsafeDrop => PathSegmentAffordance.UnsafeDrop,
            NativeSegmentAffordance.Blocked => PathSegmentAffordance.Blocked,
            _ => PathSegmentAffordance.Walk,
        };

    private static int GetSeverity(PathSegmentAffordance affordance)
        => affordance switch
        {
            PathSegmentAffordance.StepUp => 1,
            PathSegmentAffordance.SteepClimb => 2,
            PathSegmentAffordance.JumpGap => 3,
            PathSegmentAffordance.Drop => 4,
            PathSegmentAffordance.SafeDrop => 4,
            PathSegmentAffordance.Vertical => 5,
            PathSegmentAffordance.Cliff => 6,
            PathSegmentAffordance.UnsafeDrop => 7,
            PathSegmentAffordance.Blocked => 8,
            _ => 0,
        };

    private static bool IsNativeSummaryEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("WWOW_ENABLE_NATIVE_AFFORDANCE_SUMMARY");
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return raw.Trim() switch
        {
            "1" => true,
            _ when raw.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
            _ when raw.Equals("yes", StringComparison.OrdinalIgnoreCase) => true,
            _ when raw.Equals("on", StringComparison.OrdinalIgnoreCase) => true,
            _ => false,
        };
    }
}
