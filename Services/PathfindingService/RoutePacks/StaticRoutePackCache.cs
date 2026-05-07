using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using PathfindingService.Repository;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PathfindingService.RoutePacks;

public interface INavigationDataSignatureProvider
{
    string GetSignature(uint mapId);
}

public enum StaticRoutePackGenerationMode
{
    ValidatedPath,
    CorridorSeedPath
}

public sealed class FileSystemNavigationDataSignatureProvider : INavigationDataSignatureProvider
{
    private readonly ConcurrentDictionary<uint, string> _signatures = new();

    public string GetSignature(uint mapId)
        => _signatures.GetOrAdd(mapId, ComputeSignature);

    private static string ComputeSignature(uint mapId)
    {
        var root = ResolveDataRoot();
        var mmapsPath = Path.Combine(root, "mmaps");
        var mapPrefix = mapId.ToString("D3", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        builder.Append("routepack-navsig-v1|");
        builder.Append(Path.GetFullPath(root));
        builder.Append("|map=");
        builder.Append(mapPrefix);

        if (Directory.Exists(mmapsPath))
        {
            foreach (var file in Directory.EnumerateFiles(mmapsPath, $"{mapPrefix}*.mmtile").OrderBy(static f => f, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                builder.Append('|');
                builder.Append(info.Name);
                builder.Append(':');
                builder.Append(info.Length);
                builder.Append(':');
                builder.Append(info.LastWriteTimeUtc.Ticks);
            }
        }

        AppendFileFingerprint(builder, Path.Combine(root, "config.json"));
        AppendFileFingerprint(builder, Path.Combine(AppContext.BaseDirectory, "Navigation.dll"));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static void AppendFileFingerprint(StringBuilder builder, string path)
    {
        builder.Append('|');
        builder.Append(Path.GetFileName(path));
        builder.Append('=');

        if (!File.Exists(path))
        {
            builder.Append("missing");
            return;
        }

        var info = new FileInfo(path);
        builder.Append(info.Length);
        builder.Append(':');
        builder.Append(info.LastWriteTimeUtc.Ticks);
    }

    private static string ResolveDataRoot()
    {
        var dataRoot = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataRoot))
            return dataRoot;

        return AppContext.BaseDirectory;
    }
}

public sealed record StaticRoutePackSeed(
    string Id,
    uint MapId,
    XYZ StartAnchor,
    XYZ EndAnchor,
    Race Race,
    Gender Gender,
    bool SmoothPath,
    string RoutePolicy,
    bool AllowsDynamicOverlay,
    float StartAnchorRadius,
    float EndAnchorRadius,
    float CorridorProjectionRadius,
    float MaxProjectionZDrift,
    float MaxSegmentLength,
    bool WarmAtStartup = true,
    bool WarmOnDemand = false,
    int SchemaVersion = 1,
    TimeSpan? GenerationTimeout = null,
    StaticRoutePackGenerationMode GenerationMode = StaticRoutePackGenerationMode.ValidatedPath,
    float MinSuffixRemainingDistance = 0f)
{
    public TimeSpan EffectiveGenerationTimeout => GenerationTimeout ?? TimeSpan.FromSeconds(30);

    public (float Radius, float Height) Capsule
        => RaceDimensions.GetCapsuleForRace(Race, Gender);
}

public readonly record struct StaticRoutePackRequest(
    uint MapId,
    XYZ Start,
    XYZ End,
    Race Race,
    Gender Gender,
    bool SmoothPath,
    string RoutePolicy,
    int DynamicOverlayCount,
    IReadOnlyList<StaticRoutePackDynamicObject>? DynamicObjects = null);

public readonly record struct StaticRoutePackDynamicObject(
    uint DisplayId,
    XYZ Position,
    float Scale);

public readonly record struct StaticRoutePackMatch(
    string SeedId,
    string Result,
    int StartSegmentIndex,
    float StartDistanceFromCorridor,
    string NavDataSignature);

public sealed class StaticRoutePackCache
{
    public const string DefaultRoutePolicy = "default";
    public const string RouteAlgorithmSignature = "PathfindingService.StaticRoutePack.v10";

    private const float PointEpsilon = 0.25f;
    private const float CapsuleTolerance = 0.001f;
    private const int MaxAttachmentProbePoints = 12;
    private const int MaxSuffixSupportValidationSegments = 12;
    private static readonly TimeSpan FailedWarmUpRetryInterval = TimeSpan.FromMinutes(5);
    private readonly IReadOnlyList<StaticRoutePackSeed> _seeds;
    private readonly INavigationDataSignatureProvider _signatureProvider;
    private readonly Func<StaticRoutePackSeed, NavigationPathResult> _routeGenerator;
    private readonly Func<uint, XYZ, XYZ, float, float, bool> _segmentProbe;
    private readonly bool _usesDefaultSegmentProbe;
    private readonly ConcurrentDictionary<StaticRoutePackKey, StaticRoutePack> _packs = new();
    private readonly ConcurrentDictionary<StaticRoutePackKey, DateTime> _failedWarmUps = new();

    public StaticRoutePackCache(
        IEnumerable<StaticRoutePackSeed> seeds,
        INavigationDataSignatureProvider signatureProvider,
        Func<StaticRoutePackSeed, NavigationPathResult> routeGenerator,
        Func<uint, XYZ, XYZ, float, float, bool>? segmentProbe = null)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        _seeds = seeds.ToArray();
        _signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
        _routeGenerator = routeGenerator ?? throw new ArgumentNullException(nameof(routeGenerator));
        _usesDefaultSegmentProbe = segmentProbe is null;
        _segmentProbe = segmentProbe ?? DefaultSegmentProbe;
    }

    public int Count => _packs.Count;

    public static IReadOnlyList<StaticRoutePackSeed> CreateDefaultSeeds()
    {
        var orgrimmarUndercityGangplank = new XYZ(1320.142944f, -4653.158691f, 53.891945f);
        return
        [
            new StaticRoutePackSeed(
                Id: "kalimdor_orgrimmar_lower_incline_recovery_to_undercity_zeppelin",
                MapId: 1,
                StartAnchor: new XYZ(1363.9f, -4377.8f, 26.1f),
                EndAnchor: orgrimmarUndercityGangplank,
                Race: Race.Tauren,
                Gender: Gender.Male,
                SmoothPath: true,
                RoutePolicy: DefaultRoutePolicy,
                AllowsDynamicOverlay: false,
                StartAnchorRadius: 4.0f,
                EndAnchorRadius: 10.0f,
                CorridorProjectionRadius: 4.0f,
                MaxProjectionZDrift: 4.0f,
                MaxSegmentLength: 12.0f,
                WarmAtStartup: false,
                WarmOnDemand: true,
                GenerationMode: StaticRoutePackGenerationMode.CorridorSeedPath),
            new StaticRoutePackSeed(
                Id: "kalimdor_orgrimmar_exterior_incline_to_undercity_zeppelin",
                MapId: 1,
                StartAnchor: new XYZ(1381.3f, -4370.6f, 26.0f),
                EndAnchor: orgrimmarUndercityGangplank,
                Race: Race.Tauren,
                Gender: Gender.Male,
                SmoothPath: true,
                RoutePolicy: DefaultRoutePolicy,
                AllowsDynamicOverlay: false,
                StartAnchorRadius: 4.0f,
                EndAnchorRadius: 10.0f,
                CorridorProjectionRadius: 6.0f,
                MaxProjectionZDrift: 5.0f,
                MaxSegmentLength: 12.0f,
                WarmAtStartup: false,
                WarmOnDemand: true,
                GenerationMode: StaticRoutePackGenerationMode.CorridorSeedPath),
            new StaticRoutePackSeed(
                Id: "kalimdor_orgrimmar_hallway_wall_stall_to_undercity_zeppelin",
                MapId: 1,
                StartAnchor: new XYZ(1518.2f, -4419.8f, 17.1f),
                EndAnchor: orgrimmarUndercityGangplank,
                Race: Race.Tauren,
                Gender: Gender.Male,
                SmoothPath: true,
                RoutePolicy: DefaultRoutePolicy,
                AllowsDynamicOverlay: false,
                StartAnchorRadius: 4.0f,
                EndAnchorRadius: 10.0f,
                CorridorProjectionRadius: 5.0f,
                MaxProjectionZDrift: 5.0f,
                MaxSegmentLength: 12.0f,
                WarmAtStartup: false,
                WarmOnDemand: true),
            new StaticRoutePackSeed(
                Id: "kalimdor_orgrimmar_flight_master_to_undercity_zeppelin",
                MapId: 1,
                StartAnchor: new XYZ(1677.6f, -4315.7f, 61.2f),
                EndAnchor: orgrimmarUndercityGangplank,
                Race: Race.Tauren,
                Gender: Gender.Male,
                SmoothPath: true,
                RoutePolicy: DefaultRoutePolicy,
                AllowsDynamicOverlay: false,
                StartAnchorRadius: 8.0f,
                EndAnchorRadius: 10.0f,
                CorridorProjectionRadius: 6.0f,
                MaxProjectionZDrift: 5.0f,
                MaxSegmentLength: 12.0f,
                WarmAtStartup: false,
                WarmOnDemand: true)
        ];
    }

    public void WarmUpAll(ILogger? logger = null)
    {
        foreach (var seed in _seeds)
        {
            if (!seed.WarmAtStartup)
            {
                logger?.LogWarning(
                    "[ROUTE_PACK] seed={SeedId} startup warmup skipped; route-pack remains unavailable until explicitly warmed",
                    seed.Id);
                continue;
            }

            WarmUp(seed, logger);
        }
    }

    public bool WarmUp(StaticRoutePackSeed seed, ILogger? logger = null)
    {
        var key = CreateKey(seed);
        if (_packs.ContainsKey(key))
            return true;

        try
        {
            if (!TryGenerateRoute(seed, logger, out var generated))
            {
                _failedWarmUps[key] = DateTime.UtcNow;
                return false;
            }

            if (!TryCreatePack(seed, key, generated, out var pack, out var reason))
            {
                _failedWarmUps[key] = DateTime.UtcNow;
                logger?.LogWarning(
                    "[ROUTE_PACK] seed={SeedId} skipped reason={Reason} result={Result} corners={Corners} rawCorners={RawCorners}",
                    seed.Id,
                    reason,
                    generated.Result,
                    generated.Path.Length,
                    generated.RawPath.Length);
                return false;
            }

            _packs[key] = pack;
            _failedWarmUps.TryRemove(key, out _);
            logger?.LogInformation(
                "[ROUTE_PACK] seed={SeedId} warmed map={MapId} policy={Policy} race={Race} gender={Gender} corners={Corners} rawCorners={RawCorners} navSig={NavSig}",
                seed.Id,
                seed.MapId,
                seed.RoutePolicy,
                seed.Race,
                seed.Gender,
                pack.Path.Length,
                pack.RawPath.Length,
                TruncateSignature(key.NavDataSignature));
            return true;
        }
        catch (Exception ex)
        {
            _failedWarmUps[key] = DateTime.UtcNow;
            logger?.LogWarning(ex, "[ROUTE_PACK] seed={SeedId} warmup failed", seed.Id);
            return false;
        }
    }

    private bool TryGenerateRoute(
        StaticRoutePackSeed seed,
        ILogger? logger,
        out NavigationPathResult generated)
    {
        generated = default;
        var timeout = seed.EffectiveGenerationTimeout;
        if (timeout <= TimeSpan.Zero)
        {
            generated = _routeGenerator(seed);
            return true;
        }

        var task = Task.Run(() => _routeGenerator(seed));
        if (task.Wait(timeout))
        {
            generated = task.GetAwaiter().GetResult();
            return true;
        }

        logger?.LogWarning(
            "[ROUTE_PACK] seed={SeedId} generation timed out after {TimeoutMs}ms; route-pack remains unavailable",
            seed.Id,
            (long)timeout.TotalMilliseconds);
        return false;
    }

    public bool TryGetPath(
        StaticRoutePackRequest request,
        float agentRadius,
        float agentHeight,
        out NavigationPathResult result,
        out StaticRoutePackMatch match)
    {
        result = default;
        match = default;

        foreach (var seed in _seeds)
        {
            if (!CanSeedHandleRequest(seed, request, agentRadius, agentHeight))
                continue;

            var key = CreateKey(seed);
            if (!_packs.ContainsKey(key) &&
                seed.WarmOnDemand &&
                ShouldAttemptOnDemandWarmUp(key))
            {
                WarmUp(seed);
            }

            if (!_packs.TryGetValue(key, out var pack))
                continue;

            if (!AreDynamicOverlaysCompatible(seed, pack.Path, request))
                continue;

            if (!TryBuildPathFromPack(seed, pack, request, agentRadius, agentHeight, out result, out match))
                continue;

            return true;
        }

        return false;
    }

    private bool ShouldAttemptOnDemandWarmUp(StaticRoutePackKey key)
    {
        if (!_failedWarmUps.TryGetValue(key, out var lastFailureUtc))
            return true;

        return DateTime.UtcNow - lastFailureUtc >= FailedWarmUpRetryInterval;
    }

    private bool TryCreatePack(
        StaticRoutePackSeed seed,
        StaticRoutePackKey key,
        NavigationPathResult generated,
        out StaticRoutePack pack,
        out string reason)
    {
        pack = default!;
        reason = "none";

        var path = Sanitize(generated.Path);
        var rawPath = Sanitize(generated.RawPath.Length > 0 ? generated.RawPath : generated.Path);
        if (path.Length < 2)
        {
            reason = "empty_path";
            return false;
        }

        if (generated.BlockedSegmentIndex.HasValue || !string.Equals(generated.BlockedReason, "none", StringComparison.OrdinalIgnoreCase))
        {
            reason = FormatBlockedGeneratedPathReason(generated);
            return false;
        }

        if (Distance3D(path[0], seed.StartAnchor) > seed.StartAnchorRadius)
        {
            reason = "start_anchor_mismatch";
            return false;
        }

        if (Distance3D(path[^1], seed.EndAnchor) > seed.EndAnchorRadius)
        {
            reason = "end_anchor_mismatch";
            return false;
        }

        for (var i = 0; i + 1 < path.Length; i++)
        {
            if (Distance2D(path[i], path[i + 1]) > seed.MaxSegmentLength)
            {
                reason = $"segment_too_long:{i}";
                return false;
            }
        }

        if (!TryFindUnsupportedSegment(seed, path, out var unsupportedSegment, out var unsupportedReason))
        {
            reason = $"unsupported_segment:{unsupportedSegment}:{unsupportedReason}";
            return false;
        }

        pack = new StaticRoutePack(seed, key, path, rawPath, generated.Result);
        return true;
    }

    private bool TryFindUnsupportedSegment(
        StaticRoutePackSeed seed,
        IReadOnlyList<XYZ> path,
        out int unsupportedSegment,
        out string unsupportedReason)
    {
        unsupportedSegment = -1;
        unsupportedReason = "none";
        var (agentRadius, agentHeight) = seed.Capsule;
        var scanEnd = Math.Min(path.Count - 1, MaxSuffixSupportValidationSegments);
        for (var i = 0; i < scanEnd; i++)
        {
            if (IsInternalRouteSegmentSupported(seed.MapId, path[i], path[i + 1], agentRadius, agentHeight))
                continue;

            unsupportedSegment = i;
            unsupportedReason = FormatSegment(path[i], path[i + 1]);
            return false;
        }

        return true;
    }

    private bool CanSeedHandleRequest(
        StaticRoutePackSeed seed,
        StaticRoutePackRequest request,
        float agentRadius,
        float agentHeight)
    {
        if (request.MapId != seed.MapId ||
            request.Race != seed.Race ||
            request.Gender != seed.Gender ||
            request.SmoothPath != seed.SmoothPath ||
            !string.Equals(request.RoutePolicy, seed.RoutePolicy, StringComparison.Ordinal))
        {
            return false;
        }

        var (seedRadius, seedHeight) = seed.Capsule;
        if (MathF.Abs(seedRadius - agentRadius) > CapsuleTolerance ||
            MathF.Abs(seedHeight - agentHeight) > CapsuleTolerance)
        {
            return false;
        }

        return Distance3D(request.End, seed.EndAnchor) <= seed.EndAnchorRadius;
    }

    private static bool AreDynamicOverlaysCompatible(
        StaticRoutePackSeed seed,
        IReadOnlyList<XYZ> path,
        StaticRoutePackRequest request)
    {
        if (request.DynamicOverlayCount <= 0)
            return true;

        if (!seed.AllowsDynamicOverlay ||
            request.DynamicObjects is not { Count: > 0 } dynamicObjects ||
            dynamicObjects.Count != request.DynamicOverlayCount)
        {
            return false;
        }

        foreach (var dynamicObject in dynamicObjects)
        {
            if (dynamicObject.DisplayId == 0 ||
                !float.IsFinite(dynamicObject.Position.X) ||
                !float.IsFinite(dynamicObject.Position.Y) ||
                !float.IsFinite(dynamicObject.Position.Z))
            {
                return false;
            }

            var objectClearance = seed.Capsule.Radius + MathF.Max(1.5f, dynamicObject.Scale * 3.0f);
            if (DistancePointToPath2D(dynamicObject.Position, path, zTolerance: 8.0f) <= objectClearance)
                return false;
        }

        return true;
    }

    private bool TryBuildPathFromPack(
        StaticRoutePackSeed seed,
        StaticRoutePack pack,
        StaticRoutePackRequest request,
        float agentRadius,
        float agentHeight,
        out NavigationPathResult result,
        out StaticRoutePackMatch match)
    {
        result = default;
        match = default;

        if (Distance3D(request.Start, seed.StartAnchor) <= PointEpsilon &&
            Distance3D(request.End, seed.EndAnchor) <= PointEpsilon)
        {
            if (!IsSuffixSupported(seed.MapId, pack.Path, agentRadius, agentHeight, validateFirstSegmentWithAttachmentProbe: false))
                return false;

            result = new NavigationPathResult(
                pack.Path.ToArray(),
                pack.RawPath.ToArray(),
                "route_pack_main_path",
                null,
                "none");
            match = new StaticRoutePackMatch(
                seed.Id,
                result.Result,
                StartSegmentIndex: 0,
                StartDistanceFromCorridor: 0f,
                pack.Key.NavDataSignature);
            return true;
        }

        if (!TryFindCorridorProjection(seed, pack.Path, request.Start, out var projection))
            return false;

        if (seed.MinSuffixRemainingDistance > 0f &&
            Distance2D(request.Start, seed.EndAnchor) < seed.MinSuffixRemainingDistance)
        {
            return false;
        }

        if (projection.Distance2D > seed.CorridorProjectionRadius ||
            MathF.Abs(request.Start.Z - projection.Point.Z) > seed.MaxProjectionZDrift)
        {
            return false;
        }

        foreach (var candidate in EnumerateSupportedAttachments(
                     seed,
                     pack.Path,
                     request.Start,
                     projection,
                     agentRadius,
                     agentHeight))
        {
            var suffix = new List<XYZ>(pack.Path.Length - candidate.AttachmentIndex + 2);
            AppendDistinct(suffix, request.Start);
            AppendDistinct(suffix, candidate.AttachmentPoint);
            for (var i = candidate.AttachmentIndex + 1; i < pack.Path.Length; i++)
                AppendDistinct(suffix, pack.Path[i]);

            if (suffix.Count < 2)
                continue;

            if (!IsSuffixSupported(seed.MapId, suffix, agentRadius, agentHeight, validateFirstSegmentWithAttachmentProbe: true))
                continue;

            result = new NavigationPathResult(
                suffix.ToArray(),
                suffix.ToArray(),
                "route_pack_suffix",
                null,
                "none");
            match = new StaticRoutePackMatch(
                seed.Id,
                result.Result,
                candidate.AttachmentIndex,
                projection.Distance2D,
                pack.Key.NavDataSignature);
            return true;
        }

        return false;
    }

    private IEnumerable<RoutePackAttachmentCandidate> EnumerateSupportedAttachments(
        StaticRoutePackSeed seed,
        IReadOnlyList<XYZ> path,
        XYZ start,
        CorridorProjection projection,
        float agentRadius,
        float agentHeight)
    {
        var seen = new HashSet<int>();

        if (IsPlausibleAttachmentStep(start, projection.Point) &&
            IsSegmentSupported(seed.MapId, start, projection.Point, agentRadius, agentHeight))
        {
            seen.Add(projection.SegmentIndex);
            yield return new RoutePackAttachmentCandidate(projection.SegmentIndex, projection.Point);
        }

        var firstCandidate = Math.Clamp(projection.SegmentIndex + 1, 0, path.Count - 1);
        var lastCandidate = Math.Min(path.Count - 1, firstCandidate + MaxAttachmentProbePoints);
        for (var i = firstCandidate; i <= lastCandidate; i++)
        {
            var candidate = path[i];
            if (!seen.Add(i))
                continue;

            if (!CanUseAttachmentCandidate(seed, start, candidate, agentRadius, agentHeight))
                continue;

            yield return new RoutePackAttachmentCandidate(i, candidate);
        }

        var lastBacktrackCandidate = Math.Max(0, projection.SegmentIndex - MaxAttachmentProbePoints);
        for (var i = projection.SegmentIndex; i >= lastBacktrackCandidate; i--)
        {
            var candidate = path[i];
            if (!seen.Add(i))
                continue;

            if (!CanUseAttachmentCandidate(seed, start, candidate, agentRadius, agentHeight))
                continue;

            yield return new RoutePackAttachmentCandidate(i, candidate);
        }
    }

    private bool CanUseAttachmentCandidate(
        StaticRoutePackSeed seed,
        XYZ start,
        XYZ candidate,
        float agentRadius,
        float agentHeight)
    {
        if (!IsPlausibleAttachmentStep(start, candidate) ||
            Distance2D(start, candidate) > seed.MaxSegmentLength ||
            MathF.Abs(start.Z - candidate.Z) > seed.MaxProjectionZDrift)
        {
            return false;
        }

        return IsSegmentSupported(seed.MapId, start, candidate, agentRadius, agentHeight);
    }

    private bool IsSuffixSupported(
        uint mapId,
        IReadOnlyList<XYZ> suffix,
        float agentRadius,
        float agentHeight,
        bool validateFirstSegmentWithAttachmentProbe)
    {
        var scanEnd = Math.Min(suffix.Count - 1, MaxSuffixSupportValidationSegments);
        for (var i = 0; i < scanEnd; i++)
        {
            var supported = i == 0 && validateFirstSegmentWithAttachmentProbe
                ? IsSegmentSupported(mapId, suffix[i], suffix[i + 1], agentRadius, agentHeight)
                : IsInternalRouteSegmentSupported(mapId, suffix[i], suffix[i + 1], agentRadius, agentHeight);
            if (!supported)
                return false;
        }

        return true;
    }

    private static bool IsPlausibleAttachmentStep(XYZ from, XYZ to)
    {
        var climb = to.Z - from.Z;
        if (climb <= 0.75f)
            return true;

        return Distance2D(from, to) >= 1.25f;
    }

    private bool IsSegmentSupported(
        uint mapId,
        XYZ from,
        XYZ to,
        float agentRadius,
        float agentHeight)
    {
        if (Distance2D(from, to) <= PointEpsilon && MathF.Abs(from.Z - to.Z) <= PointEpsilon)
            return true;

        return _segmentProbe(mapId, from, to, agentRadius, agentHeight);
    }

    private bool IsInternalRouteSegmentSupported(
        uint mapId,
        XYZ from,
        XYZ to,
        float agentRadius,
        float agentHeight)
    {
        if (!_usesDefaultSegmentProbe)
            return IsSegmentSupported(mapId, from, to, agentRadius, agentHeight);

        return Navigation.IsSegmentLocallyReachableForAgent(mapId, from, to, agentRadius, agentHeight);
    }

    private StaticRoutePackKey CreateKey(StaticRoutePackSeed seed)
        => new(
            seed.Id,
            seed.SchemaVersion,
            seed.MapId,
            _signatureProvider.GetSignature(seed.MapId),
            RouteAlgorithmSignature,
            seed.Race,
            seed.Gender,
            seed.SmoothPath,
            seed.RoutePolicy);

    private static bool TryFindCorridorProjection(
        StaticRoutePackSeed seed,
        IReadOnlyList<XYZ> path,
        XYZ point,
        out CorridorProjection projection)
    {
        projection = default;
        if (path.Count < 2)
            return false;

        var best = new CorridorProjection(0, path[0], Distance2D(point, path[0]));
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var candidate = ProjectPointToSegment2D(point, path[i], path[i + 1], i);
            if (candidate.Distance2D < best.Distance2D)
                best = candidate;
        }

        if (Distance3D(point, seed.StartAnchor) <= seed.StartAnchorRadius &&
            Distance2D(point, path[0]) <= best.Distance2D + seed.StartAnchorRadius)
        {
            best = new CorridorProjection(0, path[0], Distance2D(point, path[0]));
        }

        projection = best;
        return true;
    }

    private static CorridorProjection ProjectPointToSegment2D(XYZ point, XYZ from, XYZ to, int segmentIndex)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lenSq = (dx * dx) + (dy * dy);
        if (lenSq <= 0.0001f)
            return new CorridorProjection(segmentIndex, from, Distance2D(point, from));

        var t = (((point.X - from.X) * dx) + ((point.Y - from.Y) * dy)) / lenSq;
        t = Math.Clamp(t, 0f, 1f);
        var projected = new XYZ(
            from.X + (dx * t),
            from.Y + (dy * t),
            from.Z + ((to.Z - from.Z) * t));
        return new CorridorProjection(segmentIndex, projected, Distance2D(point, projected));
    }

    private static float DistancePointToPath2D(XYZ point, IReadOnlyList<XYZ> path, float zTolerance)
    {
        if (path.Count == 0)
            return float.PositiveInfinity;

        var best = float.PositiveInfinity;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var minZ = MathF.Min(path[i].Z, path[i + 1].Z) - zTolerance;
            var maxZ = MathF.Max(path[i].Z, path[i + 1].Z) + zTolerance;
            if (point.Z < minZ || point.Z > maxZ)
                continue;

            var projection = ProjectPointToSegment2D(point, path[i], path[i + 1], i);
            best = MathF.Min(best, projection.Distance2D);
        }

        return best;
    }

    private static bool DefaultSegmentProbe(uint mapId, XYZ from, XYZ to, float agentRadius, float agentHeight)
    {
        try
        {
            return Navigation.IsSegmentWalkableForAgent(mapId, from, to, agentRadius, agentHeight) &&
                Navigation.IsSegmentLocallyReachableForAgent(mapId, from, to, agentRadius, agentHeight);
        }
        catch
        {
            return false;
        }
    }

    private static XYZ[] Sanitize(XYZ[] path)
        => path.Where(static p => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)).ToArray();

    private static void AppendDistinct(List<XYZ> path, XYZ point)
    {
        if (path.Count > 0 && Distance3D(path[^1], point) <= PointEpsilon)
            return;

        path.Add(point);
    }

    private static float Distance2D(XYZ from, XYZ to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float Distance3D(XYZ from, XYZ to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static string TruncateSignature(string signature)
        => signature.Length <= 12 ? signature : signature[..12];

    private static string FormatBlockedGeneratedPathReason(NavigationPathResult generated)
    {
        var index = generated.BlockedSegmentIndex;
        var reason = $"blocked:{generated.BlockedReason}:idx={index?.ToString(CultureInfo.InvariantCulture) ?? "none"}";
        if (index is not int blockedIndex ||
            blockedIndex < 0 ||
            blockedIndex + 1 >= generated.Path.Length)
        {
            return reason;
        }

        return $"{reason}:from={FormatPoint(generated.Path[blockedIndex])}:to={FormatPoint(generated.Path[blockedIndex + 1])}";
    }

    private static string FormatPoint(XYZ point)
        => FormattableString.Invariant($"({point.X:F1},{point.Y:F1},{point.Z:F1})");

    private static string FormatSegment(XYZ from, XYZ to)
        => FormattableString.Invariant($"from={FormatPoint(from)}:to={FormatPoint(to)}");

    private readonly record struct StaticRoutePackKey(
        string SeedId,
        int SchemaVersion,
        uint MapId,
        string NavDataSignature,
        string RouteAlgorithmSignature,
        Race Race,
        Gender Gender,
        bool SmoothPath,
        string RoutePolicy);

    private sealed record StaticRoutePack(
        StaticRoutePackSeed Seed,
        StaticRoutePackKey Key,
        XYZ[] Path,
        XYZ[] RawPath,
        string SourceResult);

    private readonly record struct CorridorProjection(
        int SegmentIndex,
        XYZ Point,
        float Distance2D);

    private readonly record struct RoutePackAttachmentCandidate(
        int AttachmentIndex,
        XYZ AttachmentPoint);
}
