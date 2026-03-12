using GameData.Core.Models;
using PathfindingService.Repository;
using Tests.Infrastructure.BotTasks;

namespace PathfindingService.Tests.BotTasks;

/// <summary>
/// BotTask that validates short-route path output stays within deterministic
/// segment bounds after carrying forward native-grounded segment endpoints.
/// </summary>
public class PathSegmentValidationTask : TestBotTask
{
    private readonly Navigation _navigation;

    public PathSegmentValidationTask(Navigation navigation)
        : base("PathSegmentValidation")
    {
        _navigation = navigation;
        Timeout = TimeSpan.FromSeconds(10);
    }

    public override void Update()
    {
        uint mapId = 1;
        var start = new XYZ(-562.225f, -4189.092f, 70.789f);
        var end = new XYZ(-568.0f, -4210.0f, 70.789f);

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
            maxStartDistance: 5.0f,
            maxEndDistance: 8.0f,
            maxSegmentLength: 50.0f,
            maxHeightJump: 11.0f);
        if (validationFailure is not null)
        {
            Fail(validationFailure);
            return;
        }

        Complete();
    }
}
