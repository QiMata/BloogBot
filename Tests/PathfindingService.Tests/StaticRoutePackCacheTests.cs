using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Models;
using PathfindingService.Repository;
using PathfindingService.RoutePacks;

namespace PathfindingService.Tests;

public sealed class StaticRoutePackCacheTests
{
    private static readonly StaticRoutePackSeed Seed = new(
        Id: "test_main_path",
        MapId: 1,
        StartAnchor: new XYZ(0f, 0f, 0f),
        EndAnchor: new XYZ(20f, 0f, 0f),
        Race: Race.Tauren,
        Gender: Gender.Male,
        SmoothPath: true,
        RoutePolicy: StaticRoutePackCache.DefaultRoutePolicy,
        AllowsDynamicOverlay: false,
        StartAnchorRadius: 4f,
        EndAnchorRadius: 4f,
        CorridorProjectionRadius: 3f,
        MaxProjectionZDrift: 2f,
        MaxSegmentLength: 12f);

    private static readonly (float Radius, float Height) TaurenMaleCapsule =
        RaceDimensions.GetCapsuleForRace(Race.Tauren, Gender.Male);

    [Fact]
    public void WarmUp_GeneratesRoutePackFromNavigationOutput()
    {
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(10f, 0f, 0f),
            new XYZ(20f, 0f, 0f),
        };
        var provider = new MutableSignatureProvider("sig-a");
        var generatorCalls = 0;
        StaticRoutePackSeed? observedSeed = null;
        var cache = new StaticRoutePackCache(
            [Seed],
            provider,
            seed =>
            {
                generatorCalls++;
                observedSeed = seed;
                return new NavigationPathResult(generatedPath, generatedPath, "native_path", null, "none");
            },
            SegmentAlwaysSupported);

        Assert.True(cache.WarmUp(Seed));

        var hit = cache.TryGetPath(
            CreateRequest(Seed.StartAnchor, Seed.EndAnchor),
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var cached,
            out var match);

        Assert.True(hit);
        Assert.Equal(1, generatorCalls);
        Assert.Equal(Seed, observedSeed);
        Assert.Equal("route_pack_main_path", cached.Result);
        Assert.Equal("none", cached.BlockedReason);
        Assert.Equal(generatedPath, cached.Path);
        Assert.Equal("test_main_path", match.SeedId);
    }

    [Fact]
    public void TryGetPath_StartProjectedOntoCorridor_ReturnsGeneratedSuffix()
    {
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(10f, 0f, 0f),
            new XYZ(20f, 0f, 0f),
        };
        var cache = CreateWarmedCache(generatedPath, new MutableSignatureProvider("sig-a"));
        var recoveryStart = new XYZ(9f, 1f, 0f);

        var hit = cache.TryGetPath(
            CreateRequest(recoveryStart, Seed.EndAnchor),
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var cached,
            out var match);

        Assert.True(hit);
        Assert.Equal("route_pack_suffix", cached.Result);
        Assert.Equal(0, match.StartSegmentIndex);
        Assert.InRange(match.StartDistanceFromCorridor, 0.9f, 1.1f);
        Assert.Equal(recoveryStart, cached.Path[0]);
        Assert.Equal(new XYZ(9f, 0f, 0f), cached.Path[1]);
        Assert.Equal(new XYZ(10f, 0f, 0f), cached.Path[2]);
        Assert.Equal(new XYZ(20f, 0f, 0f), cached.Path[^1]);
    }

    [Fact]
    public void TryGetPath_UnsupportedProjectionCanAttachToNextWalkableCorridorPoint()
    {
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(10f, 0f, 0f),
            new XYZ(20f, 0f, 0f),
        };
        var recoveryStart = new XYZ(9f, 1f, 0f);
        var cache = CreateWarmedCache(
            generatedPath,
            new MutableSignatureProvider("sig-a"),
            segmentProbe: (_, _, to, _, _) => to.X >= 10f);

        var hit = cache.TryGetPath(
            CreateRequest(recoveryStart, Seed.EndAnchor),
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var cached,
            out var match);

        Assert.True(hit);
        Assert.Equal("route_pack_suffix", cached.Result);
        Assert.Equal(1, match.StartSegmentIndex);
        Assert.Equal(recoveryStart, cached.Path[0]);
        Assert.Equal(new XYZ(10f, 0f, 0f), cached.Path[1]);
        Assert.DoesNotContain(new XYZ(9f, 0f, 0f), cached.Path);
    }

    [Fact]
    public void TryGetPath_VerticalProjectionAttachmentUsesDownstreamPoint()
    {
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(5f, 0f, 2f),
            new XYZ(10f, 0f, 2f),
            new XYZ(20f, 0f, 0f),
        };
        var recoveryStart = new XYZ(5f, 0f, 0f);
        var cache = CreateWarmedCache(generatedPath, new MutableSignatureProvider("sig-a"));

        var hit = cache.TryGetPath(
            CreateRequest(recoveryStart, Seed.EndAnchor),
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var cached,
            out var match);

        Assert.True(hit);
        Assert.Equal("route_pack_suffix", cached.Result);
        Assert.Equal(2, match.StartSegmentIndex);
        Assert.Equal(recoveryStart, cached.Path[0]);
        Assert.Equal(new XYZ(10f, 0f, 2f), cached.Path[1]);
        Assert.DoesNotContain(new XYZ(5f, 0f, 2f), cached.Path);
    }

    [Fact]
    public void TryGetPath_UnsupportedCorridorAttachmentBypassesRoutePack()
    {
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(10f, 0f, 0f),
            new XYZ(20f, 0f, 0f),
        };
        var cache = CreateWarmedCache(
            generatedPath,
            new MutableSignatureProvider("sig-a"),
            segmentProbe: (_, _, _, _, _) => false);

        Assert.False(TryHit(cache, CreateRequest(new XYZ(9f, 1f, 0f), Seed.EndAnchor)));
    }

    [Fact]
    public void TryGetPath_UnsafeEarlierSuffixFallsThroughToExactRecoverySeed()
    {
        var recoveryStart = new XYZ(10f, 1f, 0f);
        var upperSeed = Seed with
        {
            Id = "upper_corridor",
            StartAnchor = new XYZ(0f, 0f, 10f),
        };
        var recoverySeed = Seed with
        {
            Id = "exact_recovery",
            StartAnchor = recoveryStart,
        };
        var cache = new StaticRoutePackCache(
            [upperSeed, recoverySeed],
            new MutableSignatureProvider("sig-a"),
            seed =>
            {
                XYZ[] generatedPath = seed.Id switch
                {
                    "upper_corridor" =>
                    [
                        seed.StartAnchor,
                        new XYZ(10f, 0f, 10f),
                        seed.EndAnchor,
                    ],
                    _ =>
                    [
                        seed.StartAnchor,
                        new XYZ(15f, 0.5f, 0f),
                        seed.EndAnchor,
                    ],
                };
                return new NavigationPathResult(generatedPath, generatedPath, "native_path", null, "none");
            },
            SegmentAlwaysSupported);

        Assert.True(cache.WarmUp(upperSeed));
        Assert.True(cache.WarmUp(recoverySeed));

        var hit = cache.TryGetPath(
            CreateRequest(recoverySeed, recoveryStart, recoverySeed.EndAnchor),
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out var cached,
            out var match);

        Assert.True(hit);
        Assert.Equal("route_pack_main_path", cached.Result);
        Assert.Equal(recoverySeed.Id, match.SeedId);
        Assert.Equal(recoveryStart, cached.Path[0]);
        Assert.DoesNotContain(new XYZ(10f, 0f, 10f), cached.Path);
    }

    [Fact]
    public void TryGetPath_MismatchedKeyOrDynamicOverlay_DoesNotReuseRoutePack()
    {
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(10f, 0f, 0f),
            new XYZ(20f, 0f, 0f),
        };
        var provider = new MutableSignatureProvider("sig-a");
        var cache = CreateWarmedCache(generatedPath, provider);

        Assert.True(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor)));
        Assert.False(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor) with { Race = Race.Orc }));
        Assert.False(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor) with { Gender = Gender.Female }));
        Assert.False(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor) with { SmoothPath = false }));
        Assert.False(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor) with { RoutePolicy = "strict" }));
        Assert.False(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor) with { DynamicOverlayCount = 1 }));

        provider.Signature = "sig-b";
        Assert.False(TryHit(cache, CreateRequest(Seed.StartAnchor, Seed.EndAnchor)));
    }

    [Fact]
    public void TryGetPath_DynamicOverlayFarFromCorridorCanReuseButNearOverlayBypasses()
    {
        var overlayAwareSeed = Seed with { AllowsDynamicOverlay = true };
        var generatedPath = new[]
        {
            new XYZ(0f, 0f, 0f),
            new XYZ(10f, 0f, 0f),
            new XYZ(20f, 0f, 0f),
        };
        var cache = CreateWarmedCache(generatedPath, new MutableSignatureProvider("sig-a"), overlayAwareSeed);

        var farOverlayRequest = CreateRequest(overlayAwareSeed, overlayAwareSeed.StartAnchor, overlayAwareSeed.EndAnchor) with
        {
            DynamicOverlayCount = 1,
            DynamicObjects =
            [
                new StaticRoutePackDynamicObject(123, new XYZ(50f, 50f, 0f), 1f)
            ],
        };
        var nearOverlayRequest = CreateRequest(overlayAwareSeed, overlayAwareSeed.StartAnchor, overlayAwareSeed.EndAnchor) with
        {
            DynamicOverlayCount = 1,
            DynamicObjects =
            [
                new StaticRoutePackDynamicObject(123, new XYZ(10f, 0.5f, 0f), 1f)
            ],
        };

        Assert.True(TryHit(cache, farOverlayRequest));
        Assert.False(TryHit(cache, nearOverlayRequest));
    }

    private static StaticRoutePackCache CreateWarmedCache(
        XYZ[] generatedPath,
        MutableSignatureProvider provider,
        StaticRoutePackSeed? seedOverride = null,
        Func<uint, XYZ, XYZ, float, float, bool>? segmentProbe = null)
    {
        var seed = seedOverride ?? Seed;
        var cache = new StaticRoutePackCache(
            [seed],
            provider,
            _ => new NavigationPathResult(generatedPath, generatedPath, "native_path", null, "none"),
            segmentProbe ?? SegmentAlwaysSupported);
        Assert.True(cache.WarmUp(seed));
        return cache;
    }

    private static bool TryHit(StaticRoutePackCache cache, StaticRoutePackRequest request)
        => cache.TryGetPath(
            request,
            TaurenMaleCapsule.Radius,
            TaurenMaleCapsule.Height,
            out _,
            out _);

    private static StaticRoutePackRequest CreateRequest(XYZ start, XYZ end)
        => CreateRequest(Seed, start, end);

    private static StaticRoutePackRequest CreateRequest(StaticRoutePackSeed seed, XYZ start, XYZ end)
        => new(
            MapId: seed.MapId,
            Start: start,
            End: end,
            Race: seed.Race,
            Gender: seed.Gender,
            SmoothPath: seed.SmoothPath,
            RoutePolicy: seed.RoutePolicy,
            DynamicOverlayCount: 0);

    private static bool SegmentAlwaysSupported(uint mapId, XYZ from, XYZ to, float radius, float height) => true;

    private sealed class MutableSignatureProvider(string signature) : INavigationDataSignatureProvider
    {
        public string Signature { get; set; } = signature;

        public string GetSignature(uint mapId) => Signature;
    }
}
