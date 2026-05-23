using GameData.Core.Models;
using PathfindingService.Repository;
using Tests.Infrastructure;

namespace PathfindingService.Tests;

[Trait(TestCategories.Feature, TestCategories.Pathfinding)]
[Collection(NavigationCollection.Name)]
public sealed class SegmentValidationCacheTests
{
    private readonly NavigationFixture _fixture;

    public SegmentValidationCacheTests(NavigationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CalculateValidatedPath_DoesNotPopulateSegmentValidationCacheCounters_InRawMode()
    {
        Navigation.ResetSegmentValidationCacheCountersForTests();
        var start = new XYZ(-562.225f, -4189.092f, 70.789f);
        var end = new XYZ(-568.0f, -4180.0f, 70.789f);

        _ = _fixture.Navigation.CalculateValidatedPath(
            mapId: 1,
            start: start,
            end: end,
            smoothPath: true);

        var (hits, misses) = Navigation.GetLastCompletedSegmentValidationCacheCounters();
        Assert.Equal(0, hits);
        Assert.Equal(0, misses);
    }

    [Fact]
    public void CalculateValidatedPath_LeavesSegmentValidationCacheCountersZeroAcrossRepeatedCalls()
    {
        Navigation.ResetSegmentValidationCacheCountersForTests();
        var start = new XYZ(-562.225f, -4189.092f, 70.789f);
        var end = new XYZ(-568.0f, -4180.0f, 70.789f);

        _ = _fixture.Navigation.CalculateValidatedPath(1, start, end, smoothPath: true);
        var (firstHits, firstMisses) = Navigation.GetLastCompletedSegmentValidationCacheCounters();

        _ = _fixture.Navigation.CalculateValidatedPath(1, start, end, smoothPath: true);
        var (secondHits, secondMisses) = Navigation.GetLastCompletedSegmentValidationCacheCounters();

        Assert.Equal(0, firstHits);
        Assert.Equal(0, firstMisses);
        Assert.Equal(0, secondHits);
        Assert.Equal(0, secondMisses);
    }
}
