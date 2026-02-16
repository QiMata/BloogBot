using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using Tests.Infrastructure.BotTasks;

namespace PathfindingService.Tests.BotTasks;

/// <summary>
/// BotTask that validates path segments don't have unreasonable jumps in height
/// or distance, ensuring the pathfinding produces walkable results.
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
        // Short path in Durotar for validation
        uint mapId = 1;
        var start = new XYZ(-616.25f, -4188.00f, 82.32f);
        var end = new XYZ(-620.0f, -4220.0f, 82.0f);

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
            Fail("CalculatePath returned empty path for short route");
            return;
        }

        // Validate no single segment exceeds reasonable max distance
        const float maxSegmentLength = 50.0f;
        const float maxHeightJump = 10.0f;

        for (int i = 1; i < path.Length; i++)
        {
            float dx = path[i].X - path[i - 1].X;
            float dy = path[i].Y - path[i - 1].Y;
            float dz = path[i].Z - path[i - 1].Z;
            float horizontal = MathF.Sqrt(dx * dx + dy * dy);

            if (horizontal > maxSegmentLength)
            {
                Fail($"Segment {i - 1}→{i} horizontal distance {horizontal:F1}y exceeds max {maxSegmentLength}y");
                return;
            }

            if (MathF.Abs(dz) > maxHeightJump)
            {
                Fail($"Segment {i - 1}→{i} height change {dz:F1}y exceeds max {maxHeightJump}y");
                return;
            }
        }

        Complete();
    }
}
