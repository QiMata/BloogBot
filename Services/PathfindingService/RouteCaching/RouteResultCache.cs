using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace PathfindingService.RouteCaching;

public enum RouteResultCacheStatus
{
    Bypassed,
    Hit,
    Miss,
    Coalesced,
    Expired,
}

public sealed record RouteResultCacheOptions(
    int MaxEntries = 2048,
    TimeSpan? PositiveTtl = null,
    TimeSpan? NegativeTtl = null,
    float HorizontalQuantization = 0.25f,
    float VerticalQuantization = 0.25f,
    TimeSpan? SlowRequestThreshold = null)
{
    public TimeSpan EffectivePositiveTtl => PositiveTtl ?? TimeSpan.FromMinutes(5);
    public TimeSpan EffectiveNegativeTtl => NegativeTtl ?? TimeSpan.FromSeconds(15);
    public TimeSpan EffectiveSlowRequestThreshold => SlowRequestThreshold ?? TimeSpan.FromSeconds(25);
}

public readonly record struct RouteComputationResult(
    NavigationPathResult PathResult,
    object? MatchMetadata = null);

public readonly record struct RouteResultCacheRequest(
    uint MapId,
    XYZ Start,
    XYZ End,
    uint Race,
    uint Gender,
    float AgentRadius,
    float AgentHeight,
    bool SmoothPath,
    string RoutePolicy,
    string NavDataSignature,
    string RouteAlgorithmSignature,
    string DynamicOverlaySignature)
{
    public bool IsStaticOverlay
        => string.Equals(DynamicOverlaySignature, RouteResultCache.StaticOverlaySignature, StringComparison.Ordinal);
}

public readonly record struct RouteResultCacheLookup(
    RouteResultCacheStatus Status,
    RouteComputationResult Result);

public readonly record struct RouteResultCacheSnapshot(
    long HitCount,
    long MissCount,
    long CoalescedCount,
    long BypassCount,
    long ExpiredCount,
    long InvalidatedCount,
    long StoredPositiveCount,
    long StoredNegativeCount,
    long SlowRequestCount,
    int EntryCount,
    int InFlightCount);

public sealed class RouteResultCache
{
    public const string StaticOverlaySignature = "static";
    public const string RouteAlgorithmSignature =
    "PathfindingService.RouteResultCache.v10|Navigation.CalculateRawPath|StaticRoutePack.v10|RawNativeRuntime.v1|TransportStaging.v1|BakedMMapOnlyOverlayDefault.v3";

    private readonly RouteResultCacheOptions _options;
    private readonly ConcurrentDictionary<RouteResultCacheKey, RouteResultCacheEntry> _entries = new();
    private readonly ConcurrentDictionary<RouteResultCacheKey, Lazy<RouteResultCacheEntry>> _inFlight = new();
    private long _hitCount;
    private long _missCount;
    private long _coalescedCount;
    private long _bypassCount;
    private long _expiredCount;
    private long _invalidatedCount;
    private long _storedPositiveCount;
    private long _storedNegativeCount;
    private long _slowRequestCount;
    private long _trimSequence;

    public RouteResultCache(RouteResultCacheOptions? options = null)
    {
        _options = options ?? new RouteResultCacheOptions();
        if (_options.MaxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxEntries must be positive.");
        if (_options.HorizontalQuantization <= 0f || !float.IsFinite(_options.HorizontalQuantization))
            throw new ArgumentOutOfRangeException(nameof(options), "Horizontal quantization must be finite and positive.");
        if (_options.VerticalQuantization <= 0f || !float.IsFinite(_options.VerticalQuantization))
            throw new ArgumentOutOfRangeException(nameof(options), "Vertical quantization must be finite and positive.");
    }

    public RouteResultCacheSnapshot Snapshot => new(
        Interlocked.Read(ref _hitCount),
        Interlocked.Read(ref _missCount),
        Interlocked.Read(ref _coalescedCount),
        Interlocked.Read(ref _bypassCount),
        Interlocked.Read(ref _expiredCount),
        Interlocked.Read(ref _invalidatedCount),
        Interlocked.Read(ref _storedPositiveCount),
        Interlocked.Read(ref _storedNegativeCount),
        Interlocked.Read(ref _slowRequestCount),
        _entries.Count,
        _inFlight.Count);

    public RouteResultCacheLookup GetOrAdd(
        RouteResultCacheRequest request,
        Func<RouteComputationResult> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (!request.IsStaticOverlay)
        {
            Interlocked.Increment(ref _bypassCount);
            return new RouteResultCacheLookup(
                RouteResultCacheStatus.Bypassed,
                Clone(valueFactory()));
        }

        var key = CreateKey(request);
        var now = DateTime.UtcNow;
        if (_entries.TryGetValue(key, out var existing))
        {
            if (existing.ExpiresAtUtc > now)
            {
                Interlocked.Increment(ref _hitCount);
                existing.Touch(now);
                return new RouteResultCacheLookup(
                    RouteResultCacheStatus.Hit,
                    Clone(existing.Result));
            }

            if (_entries.TryRemove(key, out _))
                Interlocked.Increment(ref _expiredCount);
        }

        var lazy = new Lazy<RouteResultCacheEntry>(
            () => CreateEntry(valueFactory),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var winner = _inFlight.GetOrAdd(key, lazy);
        if (!ReferenceEquals(winner, lazy))
        {
            Interlocked.Increment(ref _coalescedCount);
            var coalescedEntry = winner.Value;
            return new RouteResultCacheLookup(
                RouteResultCacheStatus.Coalesced,
                Clone(coalescedEntry.Result));
        }

        try
        {
            Interlocked.Increment(ref _missCount);
            var entry = lazy.Value;
            _entries[key] = entry;
            if (entry.IsNegative)
                Interlocked.Increment(ref _storedNegativeCount);
            else
                Interlocked.Increment(ref _storedPositiveCount);

            TrimIfNeeded();
            return new RouteResultCacheLookup(
                RouteResultCacheStatus.Miss,
                Clone(entry.Result));
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }

    public void InvalidateAll()
    {
        var count = _entries.Count;
        _entries.Clear();
        if (count > 0)
            Interlocked.Add(ref _invalidatedCount, count);
    }

    private RouteResultCacheEntry CreateEntry(Func<RouteComputationResult> valueFactory)
    {
        var startedAt = DateTime.UtcNow;
        var result = Clone(valueFactory());
        var elapsed = DateTime.UtcNow - startedAt;
        if (elapsed >= _options.EffectiveSlowRequestThreshold)
            Interlocked.Increment(ref _slowRequestCount);

        var isNegative = IsNegative(result.PathResult);
        var ttl = isNegative ? _options.EffectiveNegativeTtl : _options.EffectivePositiveTtl;
        return new RouteResultCacheEntry(result, startedAt.Add(ttl), isNegative, startedAt);
    }

    private RouteResultCacheKey CreateKey(RouteResultCacheRequest request)
        => new(
            request.MapId,
            Quantize(request.Start.X, _options.HorizontalQuantization),
            Quantize(request.Start.Y, _options.HorizontalQuantization),
            Quantize(request.Start.Z, _options.VerticalQuantization),
            Quantize(request.End.X, _options.HorizontalQuantization),
            Quantize(request.End.Y, _options.HorizontalQuantization),
            Quantize(request.End.Z, _options.VerticalQuantization),
            request.Race,
            request.Gender,
            Quantize(request.AgentRadius, 0.0001f),
            Quantize(request.AgentHeight, 0.0001f),
            request.SmoothPath,
            request.RoutePolicy,
            request.NavDataSignature,
            request.RouteAlgorithmSignature,
            request.DynamicOverlaySignature);

    private void TrimIfNeeded()
    {
        if (_entries.Count <= _options.MaxEntries)
            return;

        // Keep trimming rare and cheap enough for the request path.
        if (Interlocked.Increment(ref _trimSequence) % 16 != 1)
            return;

        var excess = _entries.Count - _options.MaxEntries;
        if (excess <= 0)
            return;

        foreach (var key in _entries
            .OrderBy(static pair => pair.Value.LastAccessUtc)
            .Take(excess)
            .Select(static pair => pair.Key))
        {
            if (_entries.TryRemove(key, out _))
                Interlocked.Increment(ref _invalidatedCount);
        }
    }

    private static bool IsNegative(NavigationPathResult result)
        => result.Path.Length == 0
            || string.Equals(result.Result, "no_path", StringComparison.OrdinalIgnoreCase)
            || result.BlockedSegmentIndex.HasValue
            || !string.Equals(result.BlockedReason, "none", StringComparison.OrdinalIgnoreCase);

    private static long Quantize(float value, float quantum)
    {
        if (!float.IsFinite(value))
            return 0;

        return (long)MathF.Round(value / quantum, MidpointRounding.AwayFromZero);
    }

    private static RouteComputationResult Clone(RouteComputationResult result)
        => new(Clone(result.PathResult), result.MatchMetadata);

    private static NavigationPathResult Clone(NavigationPathResult result)
        => new(
            result.Path.ToArray(),
            result.RawPath.ToArray(),
            result.Result,
            result.BlockedSegmentIndex,
            result.BlockedReason);

    private readonly record struct RouteResultCacheKey(
        uint MapId,
        long StartX,
        long StartY,
        long StartZ,
        long EndX,
        long EndY,
        long EndZ,
        uint Race,
        uint Gender,
        long AgentRadius,
        long AgentHeight,
        bool SmoothPath,
        string RoutePolicy,
        string NavDataSignature,
        string RouteAlgorithmSignature,
        string DynamicOverlaySignature);

    private sealed class RouteResultCacheEntry(
        RouteComputationResult result,
        DateTime expiresAtUtc,
        bool isNegative,
        DateTime lastAccessUtc)
    {
        private long _lastAccessTicks = lastAccessUtc.Ticks;

        public RouteComputationResult Result { get; } = result;
        public DateTime ExpiresAtUtc { get; } = expiresAtUtc;
        public bool IsNegative { get; } = isNegative;
        public DateTime LastAccessUtc => new(Interlocked.Read(ref _lastAccessTicks), DateTimeKind.Utc);

        public void Touch(DateTime utcNow)
            => Interlocked.Exchange(ref _lastAccessTicks, utcNow.Ticks);
    }
}
