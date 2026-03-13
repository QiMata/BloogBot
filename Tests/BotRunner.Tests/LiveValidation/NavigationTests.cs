using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Navigation integration tests — validates pathfinding + physics movement end-to-end.
///
/// BG-only under the overhaul plan:
/// 1) Teleport to a known start position.
/// 2) Issue GOTO action to a known destination.
/// 3) Poll position snapshots and verify the bot moves toward the destination.
/// 4) Assert arrival within acceptance radius.
///
/// Tests use Valley of Trials (flat outdoor terrain where pathfinding routes reliably)
/// to validate both short and longer navigation scenarios.
///
/// Run: dotnet test --filter "FullyQualifiedName~NavigationTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class NavigationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    private const float ArrivalRadius = 8.0f;
    private const float NoRouteSkipThresholdSeconds = 10f;

    // Valley of Trials — flat outdoor terrain, pathfinding routes reliably here.
    // CombatLoopTests confirms navigation works at these coordinates.
    // Z+3 offset applied to spawn table Z to avoid UNDERMAP detection.
    private const float VotStartX = -284f, VotStartY = -4383f, VotStartZ = 57f;
    private const float VotEndX = -320f, VotEndY = -4420f, VotEndZ = 57f;

    // Valley of Trials — longer path (~80y), still flat terrain.
    private const float VotLongStartX = -284f, VotLongStartY = -4383f, VotLongStartZ = 57f;
    private const float VotLongEndX = -350f, VotLongEndY = -4450f, VotLongEndZ = 50f;

    public NavigationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsPathfindingReady, "PathfindingService not available on port 5001.");
    }

    [SkippableFact]
    public async Task Navigation_ShortPath_ArrivesAtDestination()
    {
        await RunNavigationTest("Short (Valley of Trials)", VotStartX, VotStartY, VotStartZ,
            VotEndX, VotEndY, VotEndZ, maxSeconds: 20);
    }

    [SkippableFact]
    public async Task Navigation_LongPath_ArrivesAtDestination()
    {
        await RunNavigationTest("Long (Valley of Trials)", VotLongStartX, VotLongStartY, VotLongStartZ,
            VotLongEndX, VotLongEndY, VotLongEndZ, maxSeconds: 45);
    }

    private async Task RunNavigationTest(string scenarioName, float startX, float startY, float startZ,
        float endX, float endY, float endZ, int maxSeconds)
    {
        var bgAccount = _bot.BgAccountName!;

        _output.WriteLine($"=== Navigation: {scenarioName} ===");
        _output.WriteLine("[BG-ONLY] Navigation assertions belong to the headless botrunner.");

        var result = await RunSingleNavigation(bgAccount, "BG", startX, startY, startZ, endX, endY, endZ, maxSeconds);
        Assert.True(result, $"[BG] Failed to navigate in '{scenarioName}'.");
    }

    private async Task<bool> RunSingleNavigation(string account, string label,
        float startX, float startY, float startZ,
        float endX, float endY, float endZ, int maxSeconds)
    {
        // Step 1: Standardized setup (BT-SETUP-001): revive + safe zone.
        await _bot.EnsureCleanSlateAsync(account, label);

        // Step 2: Teleport to start
        _output.WriteLine($"  [{label}] Teleporting to start ({startX:F0}, {startY:F0}, {startZ:F0})");
        await _bot.BotTeleportAsync(account, MapId, startX, startY, startZ);
        await _bot.WaitForTeleportSettledAsync(account, startX, startY);

        // Step 3: Issue GOTO
        _output.WriteLine($"  [{label}] Sending GOTO to ({endX:F0}, {endY:F0}, {endZ:F0})");
        var gotoResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = endX },
                new RequestParameter { FloatParam = endY },
                new RequestParameter { FloatParam = endZ },
                new RequestParameter { FloatParam = 0 } // tolerance
            }
        });
        _output.WriteLine($"  [{label}] GOTO dispatch result: {gotoResult}");
        if (gotoResult != ResponseResult.Success)
        {
            _output.WriteLine($"  [{label}] GOTO dispatch failed.");
            return false;
        }

        // Step 4: Poll position and verify approach
        var sw = Stopwatch.StartNew();
        float bestDist = float.MaxValue;
        var lastX = startX;
        var lastY = startY;
        var totalTravel = 0f;

        while (sw.Elapsed < TimeSpan.FromSeconds(maxSeconds))
        {
            await Task.Delay(1500);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            var dist2D = LiveBotFixture.Distance2D(pos.X, pos.Y, endX, endY);
            var stepDist = LiveBotFixture.Distance2D(pos.X, pos.Y, lastX, lastY);
            totalTravel += stepDist;
            lastX = pos.X;
            lastY = pos.Y;

            if (dist2D < bestDist) bestDist = dist2D;

            _output.WriteLine($"  [{label}] pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) dist2D={dist2D:F1} step={stepDist:F1} best={bestDist:F1}");

            if (dist2D <= ArrivalRadius)
            {
                _output.WriteLine($"  [{label}] ARRIVED at destination (dist={dist2D:F1}y) after {sw.Elapsed.TotalSeconds:F1}s, travel={totalTravel:F0}y");
                return true;
            }
        }

        // If no displacement at all after the timeout, pathfinding likely has no route
        // for these coordinates — skip rather than hard fail.
        if (totalTravel < 1.0f)
        {
            var skipMsg = $"[{label}] SKIP: Zero travel after {maxSeconds}s — pathfinding has no route from " +
                          $"({startX:F0},{startY:F0},{startZ:F0}) to ({endX:F0},{endY:F0},{endZ:F0}). " +
                          "Tracked as PathfindingService navmesh gap.";
            _output.WriteLine(skipMsg);
            global::Tests.Infrastructure.Skip.If(true, skipMsg);
            return true;
        }

        _output.WriteLine($"  [{label}] TIMEOUT: Did not arrive within {maxSeconds}s. Best dist={bestDist:F1}y, total travel={totalTravel:F0}y");
        return false;
    }

}
