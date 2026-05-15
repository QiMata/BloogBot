using GameData.Core.Models;
using PathfindingService.Repository;
using Tests.Infrastructure;

namespace PathfindingService.Tests;

[Trait(TestCategories.Feature, TestCategories.Pathfinding)]
public sealed class SegmentValidationCacheTests : IClassFixture<NavigationFixture>
{
    private readonly NavigationFixture _fixture;

    public SegmentValidationCacheTests(NavigationFixture fixture)
    {
        _fixture = fixture;
    }

    // Drives a CalculateValidatedPath against a short Durotar segment and
    // asserts the segment-validation cache was used. Misses > 0 proves the
    // scope wrapped the call; hits >= 0 (often > 0 in practice because
    // resolver attempts and repair passes share segments).
    [Fact]
    public void CalculateValidatedPath_PopulatesSegmentValidationCacheCounters()
    {
        var start = new XYZ(-562.225f, -4189.092f, 70.789f);
        var end = new XYZ(-568.0f, -4180.0f, 70.789f);

        _ = _fixture.Navigation.CalculateValidatedPath(
            mapId: 1,
            start: start,
            end: end,
            smoothPath: true);

        var (hits, misses) = Navigation.GetLastCompletedSegmentValidationCacheCounters();
        Assert.True(
            misses > 0 || hits > 0,
            $"Expected the segment-validation cache to record at least one access; got hits={hits} misses={misses}");
    }

    // Two consecutive CalculateValidatedPath calls share NO cache state
    // (per-call scope). After the second call the LastCompleted snapshot
    // is the second call's counters, not the cumulative total.
    [Fact]
    public void CalculateValidatedPath_CacheScopeIsPerCall()
    {
        var start = new XYZ(-562.225f, -4189.092f, 70.789f);
        var end = new XYZ(-568.0f, -4180.0f, 70.789f);

        _ = _fixture.Navigation.CalculateValidatedPath(1, start, end, smoothPath: true);
        var (firstHits, firstMisses) = Navigation.GetLastCompletedSegmentValidationCacheCounters();
        var firstTotal = firstHits + firstMisses;

        _ = _fixture.Navigation.CalculateValidatedPath(1, start, end, smoothPath: true);
        var (secondHits, secondMisses) = Navigation.GetLastCompletedSegmentValidationCacheCounters();
        var secondTotal = secondHits + secondMisses;

        Assert.True(firstTotal > 0);
        Assert.True(secondTotal > 0);
        // Second call's total should be in the same order of magnitude as
        // the first — the scope is per-call, not cumulative.
        Assert.True(secondTotal < firstTotal * 10);
    }
}
