using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using Tests.Infrastructure.BotTasks;

namespace PathfindingService.Tests.BotTasks;

/// <summary>
/// BotTask that requests a path between two known positions on Kalimdor (map 1)
/// and validates the result has waypoints with correct start/end proximity.
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
        // Durotar: Valley of Trials to Sen'jin Village
        uint mapId = 1;
        var start = new XYZ(-616.25f, -4188.00f, 82.32f);
        var end = new XYZ(-829.95f, -4930.76f, 21.97f);

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

        if (path == null || path.Length == 0)
        {
            Fail("CalculatePath returned empty path");
            return;
        }

        if (path.Length < 2)
        {
            Fail($"Path should have at least 2 waypoints, got {path.Length}");
            return;
        }

        // Verify start is near our requested start
        float startDist = Distance(path[0], start);
        if (startDist > 5.0f)
        {
            Fail($"Path start ({path[0]}) too far from requested start ({start}): {startDist:F1}y");
            return;
        }

        // Verify end is near our requested end
        float endDist = Distance(path[^1], end);
        if (endDist > 5.0f)
        {
            Fail($"Path end ({path[^1]}) too far from requested end ({end}): {endDist:F1}y");
            return;
        }

        // Verify no zero-length segments
        for (int i = 1; i < path.Length; i++)
        {
            float segLen = Distance(path[i], path[i - 1]);
            if (segLen < 0.001f)
            {
                Fail($"Zero-length segment at index {i}: {path[i - 1]} → {path[i]}");
                return;
            }
        }

        Complete();
    }

    private static float Distance(XYZ a, XYZ b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
