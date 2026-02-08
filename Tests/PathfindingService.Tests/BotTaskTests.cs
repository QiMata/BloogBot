using PathfindingService.Tests.BotTasks;
using Tests.Infrastructure;

namespace PathfindingService.Tests;

/// <summary>
/// Runs pathfinding BotTasks as xUnit tests.
/// These require Navigation.dll and mmaps data to be available.
/// </summary>
[Trait(TestCategories.Feature, TestCategories.Pathfinding)]
public class PathfindingBotTaskTests : IClassFixture<NavigationFixture>
{
    private readonly NavigationFixture _fixture;

    public PathfindingBotTaskTests(NavigationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PathCalculation_ShouldReturnValidWaypointPath()
    {
        var task = new PathCalculationTask(_fixture.Navigation);
        task.Update();
        task.AssertSuccess();
    }

    [Fact]
    public void PathSegmentValidation_ShouldProduceWalkableSegments()
    {
        var task = new PathSegmentValidationTask(_fixture.Navigation);
        task.Update();
        task.AssertSuccess();
    }
}
