using System;
using System.Threading;

namespace PathfindingService.Repository;

public readonly record struct NavigationPerformanceSnapshot(
    long ValidatedPathRequests,
    long ValidatedPathTotalMs,
    long ValidatedPathMaxMs,
    long SlowValidatedPathCount,
    long PathResolverAttempts,
    long SmoothPathResolverAttempts,
    long StraightPathResolverAttempts,
    long PathResolverTotalMs,
    long PathResolverMaxMs,
    long NativeFindPathAttempts,
    long SmoothNativeFindPathAttempts,
    long StraightNativeFindPathAttempts,
    long NativeFindPathTotalMs,
    long NativeFindPathMaxMs,
    long NativeFindPathReturnedPathCount,
    long NativeFindPathEmptyCount,
    long SlowNativeFindPathCount,
    long CorridorQueryAttempts,
    long CorridorQueryTotalMs,
    long CorridorQueryMaxMs,
    long ManagedValidationRuns,
    long ManagedValidationTotalMs,
    long ManagedValidationMaxMs,
    long SlowManagedValidationCount,
    long LongLineOfSightRepairCount,
    long StaticWallRepairCount,
    long SteepAffordanceRepairCount,
    long LocalPhysicsLayerRepairCount,
    long SegmentValidationRepairCount,
    long DynamicOverlayRepairCount,
    long BlockedPathResults,
    long NoPathResults)
{
    public double AverageValidatedPathMs => Average(ValidatedPathTotalMs, ValidatedPathRequests);
    public double AveragePathResolverMs => Average(PathResolverTotalMs, PathResolverAttempts);
    public double AverageNativeFindPathMs => Average(NativeFindPathTotalMs, NativeFindPathAttempts);
    public double AverageCorridorQueryMs => Average(CorridorQueryTotalMs, CorridorQueryAttempts);
    public double AverageManagedValidationMs => Average(ManagedValidationTotalMs, ManagedValidationRuns);

    private static double Average(long total, long count) => count == 0 ? 0d : (double)total / count;
}

public static class NavigationPerformanceMetrics
{
    private static readonly TimeSpan SlowValidatedPathThreshold = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SlowNativeFindPathThreshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SlowManagedValidationThreshold = TimeSpan.FromSeconds(1);

    private static long _validatedPathRequests;
    private static long _validatedPathTotalMs;
    private static long _validatedPathMaxMs;
    private static long _slowValidatedPathCount;
    private static long _pathResolverAttempts;
    private static long _smoothPathResolverAttempts;
    private static long _straightPathResolverAttempts;
    private static long _pathResolverTotalMs;
    private static long _pathResolverMaxMs;
    private static long _nativeFindPathAttempts;
    private static long _smoothNativeFindPathAttempts;
    private static long _straightNativeFindPathAttempts;
    private static long _nativeFindPathTotalMs;
    private static long _nativeFindPathMaxMs;
    private static long _nativeFindPathReturnedPathCount;
    private static long _nativeFindPathEmptyCount;
    private static long _slowNativeFindPathCount;
    private static long _corridorQueryAttempts;
    private static long _corridorQueryTotalMs;
    private static long _corridorQueryMaxMs;
    private static long _managedValidationRuns;
    private static long _managedValidationTotalMs;
    private static long _managedValidationMaxMs;
    private static long _slowManagedValidationCount;
    private static long _longLineOfSightRepairCount;
    private static long _staticWallRepairCount;
    private static long _steepAffordanceRepairCount;
    private static long _localPhysicsLayerRepairCount;
    private static long _segmentValidationRepairCount;
    private static long _dynamicOverlayRepairCount;
    private static long _blockedPathResults;
    private static long _noPathResults;

    public static NavigationPerformanceSnapshot Snapshot => new(
        Interlocked.Read(ref _validatedPathRequests),
        Interlocked.Read(ref _validatedPathTotalMs),
        Interlocked.Read(ref _validatedPathMaxMs),
        Interlocked.Read(ref _slowValidatedPathCount),
        Interlocked.Read(ref _pathResolverAttempts),
        Interlocked.Read(ref _smoothPathResolverAttempts),
        Interlocked.Read(ref _straightPathResolverAttempts),
        Interlocked.Read(ref _pathResolverTotalMs),
        Interlocked.Read(ref _pathResolverMaxMs),
        Interlocked.Read(ref _nativeFindPathAttempts),
        Interlocked.Read(ref _smoothNativeFindPathAttempts),
        Interlocked.Read(ref _straightNativeFindPathAttempts),
        Interlocked.Read(ref _nativeFindPathTotalMs),
        Interlocked.Read(ref _nativeFindPathMaxMs),
        Interlocked.Read(ref _nativeFindPathReturnedPathCount),
        Interlocked.Read(ref _nativeFindPathEmptyCount),
        Interlocked.Read(ref _slowNativeFindPathCount),
        Interlocked.Read(ref _corridorQueryAttempts),
        Interlocked.Read(ref _corridorQueryTotalMs),
        Interlocked.Read(ref _corridorQueryMaxMs),
        Interlocked.Read(ref _managedValidationRuns),
        Interlocked.Read(ref _managedValidationTotalMs),
        Interlocked.Read(ref _managedValidationMaxMs),
        Interlocked.Read(ref _slowManagedValidationCount),
        Interlocked.Read(ref _longLineOfSightRepairCount),
        Interlocked.Read(ref _staticWallRepairCount),
        Interlocked.Read(ref _steepAffordanceRepairCount),
        Interlocked.Read(ref _localPhysicsLayerRepairCount),
        Interlocked.Read(ref _segmentValidationRepairCount),
        Interlocked.Read(ref _dynamicOverlayRepairCount),
        Interlocked.Read(ref _blockedPathResults),
        Interlocked.Read(ref _noPathResults));

    public static void ResetForTests()
    {
        Interlocked.Exchange(ref _validatedPathRequests, 0);
        Interlocked.Exchange(ref _validatedPathTotalMs, 0);
        Interlocked.Exchange(ref _validatedPathMaxMs, 0);
        Interlocked.Exchange(ref _slowValidatedPathCount, 0);
        Interlocked.Exchange(ref _pathResolverAttempts, 0);
        Interlocked.Exchange(ref _smoothPathResolverAttempts, 0);
        Interlocked.Exchange(ref _straightPathResolverAttempts, 0);
        Interlocked.Exchange(ref _pathResolverTotalMs, 0);
        Interlocked.Exchange(ref _pathResolverMaxMs, 0);
        Interlocked.Exchange(ref _nativeFindPathAttempts, 0);
        Interlocked.Exchange(ref _smoothNativeFindPathAttempts, 0);
        Interlocked.Exchange(ref _straightNativeFindPathAttempts, 0);
        Interlocked.Exchange(ref _nativeFindPathTotalMs, 0);
        Interlocked.Exchange(ref _nativeFindPathMaxMs, 0);
        Interlocked.Exchange(ref _nativeFindPathReturnedPathCount, 0);
        Interlocked.Exchange(ref _nativeFindPathEmptyCount, 0);
        Interlocked.Exchange(ref _slowNativeFindPathCount, 0);
        Interlocked.Exchange(ref _corridorQueryAttempts, 0);
        Interlocked.Exchange(ref _corridorQueryTotalMs, 0);
        Interlocked.Exchange(ref _corridorQueryMaxMs, 0);
        Interlocked.Exchange(ref _managedValidationRuns, 0);
        Interlocked.Exchange(ref _managedValidationTotalMs, 0);
        Interlocked.Exchange(ref _managedValidationMaxMs, 0);
        Interlocked.Exchange(ref _slowManagedValidationCount, 0);
        Interlocked.Exchange(ref _longLineOfSightRepairCount, 0);
        Interlocked.Exchange(ref _staticWallRepairCount, 0);
        Interlocked.Exchange(ref _steepAffordanceRepairCount, 0);
        Interlocked.Exchange(ref _localPhysicsLayerRepairCount, 0);
        Interlocked.Exchange(ref _segmentValidationRepairCount, 0);
        Interlocked.Exchange(ref _dynamicOverlayRepairCount, 0);
        Interlocked.Exchange(ref _blockedPathResults, 0);
        Interlocked.Exchange(ref _noPathResults, 0);
    }

    internal static void RecordValidatedPath(TimeSpan elapsed, NavigationPathResult result)
    {
        var elapsedMs = ToMilliseconds(elapsed);
        Interlocked.Increment(ref _validatedPathRequests);
        Interlocked.Add(ref _validatedPathTotalMs, elapsedMs);
        UpdateMax(ref _validatedPathMaxMs, elapsedMs);
        if (elapsed >= SlowValidatedPathThreshold)
            Interlocked.Increment(ref _slowValidatedPathCount);

        if (string.Equals(result.Result, "no_path", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _noPathResults);
        if (result.BlockedSegmentIndex.HasValue ||
            !string.Equals(result.BlockedReason, "none", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _blockedPathResults);
        }
    }

    internal static void RecordPathResolverAttempt(bool smoothPath, TimeSpan elapsed)
    {
        var elapsedMs = ToMilliseconds(elapsed);
        Interlocked.Increment(ref _pathResolverAttempts);
        Interlocked.Add(ref _pathResolverTotalMs, elapsedMs);
        UpdateMax(ref _pathResolverMaxMs, elapsedMs);
        if (smoothPath)
            Interlocked.Increment(ref _smoothPathResolverAttempts);
        else
            Interlocked.Increment(ref _straightPathResolverAttempts);
    }

    internal static void RecordNativeFindPath(bool smoothPath, TimeSpan elapsed, int length)
    {
        var elapsedMs = ToMilliseconds(elapsed);
        Interlocked.Increment(ref _nativeFindPathAttempts);
        Interlocked.Add(ref _nativeFindPathTotalMs, elapsedMs);
        UpdateMax(ref _nativeFindPathMaxMs, elapsedMs);
        if (smoothPath)
            Interlocked.Increment(ref _smoothNativeFindPathAttempts);
        else
            Interlocked.Increment(ref _straightNativeFindPathAttempts);
        if (length > 0)
            Interlocked.Increment(ref _nativeFindPathReturnedPathCount);
        else
            Interlocked.Increment(ref _nativeFindPathEmptyCount);
        if (elapsed >= SlowNativeFindPathThreshold)
            Interlocked.Increment(ref _slowNativeFindPathCount);
    }

    internal static void RecordCorridorQuery(TimeSpan elapsed)
    {
        var elapsedMs = ToMilliseconds(elapsed);
        Interlocked.Increment(ref _corridorQueryAttempts);
        Interlocked.Add(ref _corridorQueryTotalMs, elapsedMs);
        UpdateMax(ref _corridorQueryMaxMs, elapsedMs);
    }

    internal static void RecordManagedValidation(TimeSpan elapsed)
    {
        var elapsedMs = ToMilliseconds(elapsed);
        Interlocked.Increment(ref _managedValidationRuns);
        Interlocked.Add(ref _managedValidationTotalMs, elapsedMs);
        UpdateMax(ref _managedValidationMaxMs, elapsedMs);
        if (elapsed >= SlowManagedValidationThreshold)
            Interlocked.Increment(ref _slowManagedValidationCount);
    }

    internal static void RecordLongLineOfSightRepair(int count) => AddPositive(ref _longLineOfSightRepairCount, count);
    internal static void RecordStaticWallRepair(int count) => AddPositive(ref _staticWallRepairCount, count);
    internal static void RecordSteepAffordanceRepair(int count) => AddPositive(ref _steepAffordanceRepairCount, count);
    internal static void RecordLocalPhysicsLayerRepair(int count) => AddPositive(ref _localPhysicsLayerRepairCount, count);
    internal static void RecordSegmentValidationRepair(int count) => AddPositive(ref _segmentValidationRepairCount, count);
    internal static void RecordDynamicOverlayRepair() => Interlocked.Increment(ref _dynamicOverlayRepairCount);

    private static void AddPositive(ref long target, int count)
    {
        if (count > 0)
            Interlocked.Add(ref target, count);
    }

    private static long ToMilliseconds(TimeSpan elapsed)
        => Math.Max(0L, (long)Math.Ceiling(elapsed.TotalMilliseconds));

    private static void UpdateMax(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value > current)
        {
            var original = Interlocked.CompareExchange(ref target, value, current);
            if (original == current)
                return;

            current = original;
        }
    }
}
