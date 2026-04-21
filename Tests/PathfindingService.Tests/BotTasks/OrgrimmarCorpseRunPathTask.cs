using GameData.Core.Models;
using PathfindingService.Repository;
using Tests.Infrastructure.BotTasks;

namespace PathfindingService.Tests.BotTasks;

/// <summary>
/// Mirrors the live Orgrimmar corpse-run retrieval corridor used by BotRunner
/// so route regressions are caught before end-to-end corpse-run tests.
/// </summary>
public class OrgrimmarCorpseRunPathTask : TestBotTask
{
    private readonly Navigation _navigation;

    public OrgrimmarCorpseRunPathTask(Navigation navigation)
        : base("OrgrimmarCorpseRunPath")
    {
        _navigation = navigation;
        Timeout = TimeSpan.FromSeconds(10);
    }

    public override void Update()
    {
        const uint mapId = 1;
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
            Fail($"{validationFailure}\n{FormatPath(path)}");
            return;
        }

        if (path.Length < 3)
        {
            Fail($"Orgrimmar corpse-run path too short ({path.Length} points).\n{FormatPath(path)}");
            return;
        }

        Complete();
    }

    private static string FormatPath(XYZ[] path)
    {
        if (path.Length == 0)
            return "Path: <empty>";

        var lines = new List<string>(path.Length + 1) { $"Path ({path.Length} points):" };
        for (int i = 0; i < path.Length; i++)
            lines.Add($"  [{i}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");

        return string.Join(Environment.NewLine, lines);
    }
}
