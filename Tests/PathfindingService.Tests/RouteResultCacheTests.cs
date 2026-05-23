using GameData.Core.Models;
using PathfindingService.Repository;
using PathfindingService.RouteCaching;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PathfindingService.Tests;

public sealed class RouteResultCacheTests
{
    [Fact]
    public void RouteAlgorithmSignature_TracksStaticRoutePackAndReachabilityContract()
    {
        Assert.Contains("RouteResultCache.v10", RouteResultCache.RouteAlgorithmSignature, StringComparison.Ordinal);
        Assert.Contains("StaticRoutePack.v10", RouteResultCache.RouteAlgorithmSignature, StringComparison.Ordinal);
        Assert.Contains("RawNativeRuntime.v1", RouteResultCache.RouteAlgorithmSignature, StringComparison.Ordinal);
        Assert.Contains("TransportStaging.v1", RouteResultCache.RouteAlgorithmSignature, StringComparison.Ordinal);
        Assert.Contains("BakedMMapOnlyOverlayDefault.v3", RouteResultCache.RouteAlgorithmSignature, StringComparison.Ordinal);
    }

    [Fact]
    public void GetOrAdd_ReusesQuantizedStaticRequest()
    {
        var calls = 0;
        var cache = new RouteResultCache(new RouteResultCacheOptions(
            PositiveTtl: TimeSpan.FromMinutes(1),
            HorizontalQuantization: 0.25f,
            VerticalQuantization: 0.25f));

        var first = cache.GetOrAdd(CreateRequest(), () =>
        {
            calls++;
            return Success(marker: 1);
        });
        var second = cache.GetOrAdd(CreateRequest(start: new XYZ(0.06f, 0.06f, 0f)), () =>
        {
            calls++;
            return Success(marker: 2);
        });

        Assert.Equal(RouteResultCacheStatus.Miss, first.Status);
        Assert.Equal(RouteResultCacheStatus.Hit, second.Status);
        Assert.Equal(1, calls);
        Assert.Equal(1f, second.Result.PathResult.Path[0].X);

        var stats = cache.Snapshot;
        Assert.Equal(1L, stats.MissCount);
        Assert.Equal(1L, stats.HitCount);
        Assert.Equal(1, stats.EntryCount);
    }

    [Fact]
    public void GetOrAdd_BypassesDynamicOverlayRequests()
    {
        var calls = 0;
        var cache = new RouteResultCache();
        var request = CreateRequest(dynamicOverlaySignature: "dynamic:1");

        var first = cache.GetOrAdd(request, () =>
        {
            calls++;
            return Success(marker: 1);
        });
        var second = cache.GetOrAdd(request, () =>
        {
            calls++;
            return Success(marker: 2);
        });

        Assert.Equal(RouteResultCacheStatus.Bypassed, first.Status);
        Assert.Equal(RouteResultCacheStatus.Bypassed, second.Status);
        Assert.Equal(2, calls);
        Assert.Equal(2f, second.Result.PathResult.Path[0].X);

        var stats = cache.Snapshot;
        Assert.Equal(2L, stats.BypassCount);
        Assert.Equal(0, stats.EntryCount);
    }

    [Fact]
    public async Task GetOrAdd_CoalescesConcurrentEquivalentStaticRequests()
    {
        var calls = 0;
        using var factoryEntered = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        var cache = new RouteResultCache();
        var request = CreateRequest();

        var firstTask = Task.Run(() => cache.GetOrAdd(request, () =>
        {
            Interlocked.Increment(ref calls);
            factoryEntered.Set();
            Assert.True(releaseFactory.Wait(TimeSpan.FromSeconds(5)));
            return Success();
        }));

        Assert.True(factoryEntered.Wait(TimeSpan.FromSeconds(5)));

        var secondTask = Task.Run(() => cache.GetOrAdd(request, () =>
        {
            Interlocked.Increment(ref calls);
            return Success(marker: 2);
        }));

        await Task.Delay(100);
        releaseFactory.Set();
        var lookups = await Task.WhenAll(firstTask, secondTask);

        Assert.Contains(lookups, static lookup => lookup.Status == RouteResultCacheStatus.Miss);
        Assert.Contains(lookups, static lookup => lookup.Status == RouteResultCacheStatus.Coalesced);
        Assert.Equal(1, calls);

        var stats = cache.Snapshot;
        Assert.Equal(1L, stats.MissCount);
        Assert.Equal(1L, stats.CoalescedCount);
        Assert.Equal(1, stats.EntryCount);
    }

    [Fact]
    public async Task GetOrAdd_NegativeEntriesUseShortTtlAndExpire()
    {
        var calls = 0;
        var cache = new RouteResultCache(new RouteResultCacheOptions(
            PositiveTtl: TimeSpan.FromMinutes(1),
            NegativeTtl: TimeSpan.FromMilliseconds(30)));
        var request = CreateRequest();

        var first = cache.GetOrAdd(request, () =>
        {
            calls++;
            return NoPath();
        });
        var second = cache.GetOrAdd(request, () =>
        {
            calls++;
            return Success(marker: 2);
        });

        await Task.Delay(80);

        var third = cache.GetOrAdd(request, () =>
        {
            calls++;
            return Success(marker: 3);
        });

        Assert.Equal(RouteResultCacheStatus.Miss, first.Status);
        Assert.Equal(RouteResultCacheStatus.Hit, second.Status);
        Assert.Equal(RouteResultCacheStatus.Miss, third.Status);
        Assert.Empty(second.Result.PathResult.Path);
        Assert.Equal(3f, third.Result.PathResult.Path[0].X);
        Assert.Equal(2, calls);

        var stats = cache.Snapshot;
        Assert.Equal(1L, stats.StoredNegativeCount);
        Assert.Equal(1L, stats.ExpiredCount);
    }

    private static RouteResultCacheRequest CreateRequest(
        XYZ? start = null,
        string dynamicOverlaySignature = RouteResultCache.StaticOverlaySignature)
        => new(
            MapId: 1,
            Start: start ?? new XYZ(0f, 0f, 0f),
            End: new XYZ(10f, 0f, 0f),
            Race: 6,
            Gender: 0,
            AgentRadius: 1.0247f,
            AgentHeight: 2.625f,
            SmoothPath: true,
            RoutePolicy: "default",
            NavDataSignature: "navsig-a",
            RouteAlgorithmSignature: RouteResultCache.RouteAlgorithmSignature,
            DynamicOverlaySignature: dynamicOverlaySignature);

    private static RouteComputationResult Success(int marker = 1)
    {
        var path = new[]
        {
            new XYZ(marker, 0f, 0f),
            new XYZ(10f, 0f, 0f),
        };
        return new RouteComputationResult(
            new NavigationPathResult(path, path.ToArray(), "native_path", null, "none"));
    }

    private static RouteComputationResult NoPath()
        => new(new NavigationPathResult([], [], "no_path", null, "none"));
}
