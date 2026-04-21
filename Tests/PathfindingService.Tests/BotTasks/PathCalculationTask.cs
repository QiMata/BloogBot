using GameData.Core.Models;
using PathfindingService.Repository;
using Tests.Infrastructure.BotTasks;

namespace PathfindingService.Tests.BotTasks;

/// <summary>
/// BotTask that requests a path between two known positions on Kalimdor (map 1)
/// and validates the result has walkable waypoints with correct start/end proximity.
/// </summary>
public class PathCalculationTask : TestBotTask
{
    private readonly Navigation _navigation;

    public PathCalculationTask(Navigation navigation)
        : base("PathCalculation")
    {
        _navigation = navigation;
        Timeout = TimeSpan.FromSeconds(10);
    }

    public override void Update()
    {
        uint mapId = 1;
        var start = new XYZ(1177.8f, -4464.2f, 21.4f);
        var end = new XYZ(1629.4f, -4373.4f, 31.3f);

        XYZ[] path;
        try
        {
            path = _navigation.CalculatePath(mapId, start, end, smoothPath: true);
        }
        catch (Exception ex)
        {
            Fail($"CalculatePath threw: {ex.Message}");
            return;
        }

        var validationFailure = PathRouteAssertions.GetValidationFailure(
            mapId,
            start,
            end,
            path,
            maxStartDistance: 10.0f,
            maxEndDistance: 12.0f,
            maxSegmentLength: 200.0f,
            maxHeightJump: 25.0f);
        if (validationFailure is not null)
        {
            Fail(validationFailure);
            return;
        }

        Complete();
    }
}
