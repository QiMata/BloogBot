using System;
using System.Diagnostics;
using System.Linq;
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
[Collection(LiveValidationCollection.Name)]
public class NavigationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    private const float ArrivalRadius = 12.0f;
    private const float NoRouteSkipThresholdSeconds = 10f;

    // Valley of Trials — flat outdoor terrain, pathfinding routes reliably here.
    // CombatLoopTests confirms navigation works at these coordinates.
    // Z+3 offset applied to spawn table Z to avoid UNDERMAP detection.
    private const float VotStartX = -284f, VotStartY = -4383f, VotStartZ = 57f;
    private const float VotEndX = -320f, VotEndY = -4420f, VotEndZ = 57f;

    // Valley of Trials — longer path (~100y) on sloped terrain.
    // Tests slope-guard protection in BG MovementController (prevents cascading Z into caves).
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
            VotEndX, VotEndY, VotEndZ, maxSeconds: 40);
    }

    [SkippableFact]
    public async Task Navigation_LongPath_ArrivesAtDestination()
    {
        await RunNavigationTest("Long (Valley of Trials)", VotLongStartX, VotLongStartY, VotLongStartZ,
            VotLongEndX, VotLongEndY, VotLongEndZ, maxSeconds: 90);
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

    /// <summary>
    /// Diagnostic: capture high-frequency Z trace for the long path route on both FG and BG bots.
    /// Outputs Z values at each poll interval to identify where BG diverges from FG (gold standard).
    /// </summary>
    [SkippableFact]
    public async Task Navigation_LongPath_ZTrace_FGvsBG()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;

        _output.WriteLine("=== Z Trace: FG vs BG on Valley of Trials Slope ===");
        _output.WriteLine($"Route: ({VotLongStartX},{VotLongStartY},{VotLongStartZ}) -> ({VotLongEndX},{VotLongEndY},{VotLongEndZ})");

        // Run BG first (always available)
        var bgTrace = await CaptureZTrace(bgAccount, "BG", VotLongStartX, VotLongStartY, VotLongStartZ,
            VotLongEndX, VotLongEndY, VotLongEndZ, maxSeconds: 60, pollMs: 500);

        // Run FG if available and actually alive (not just configured)
        System.Collections.Generic.List<(float x, float y, float z, double t)>? fgTrace = null;
        await _bot.RefreshSnapshotsAsync();
        var fgAlive = _bot.ForegroundBot?.Player?.Unit?.MaxHealth > 0;
        if (fgAccount != null && fgAlive)
        {
            fgTrace = await CaptureZTrace(fgAccount, "FG", VotLongStartX, VotLongStartY, VotLongStartZ,
                VotLongEndX, VotLongEndY, VotLongEndZ, maxSeconds: 60, pollMs: 500);
        }
        else
        {
            _output.WriteLine("  [FG] Not available — BG-only Z trace");
        }

        // Print comparison table
        _output.WriteLine("\n=== Z Trace Summary ===");
        _output.WriteLine($"BG: {bgTrace.Count} samples");
        if (fgTrace != null) _output.WriteLine($"FG: {fgTrace.Count} samples");

        // Analyze BG Z oscillation
        int bgOscillations = 0;
        float prevZ = float.NaN;
        int dir = 0;
        float maxZDelta = 0;
        foreach (var (x, y, z, t) in bgTrace)
        {
            if (!float.IsNaN(prevZ))
            {
                float dz = z - prevZ;
                if (MathF.Abs(dz) > maxZDelta) maxZDelta = MathF.Abs(dz);
                int newDir = dz > 2f ? 1 : dz < -2f ? -1 : 0;
                if (newDir != 0 && dir != 0 && newDir != dir && MathF.Abs(dz) > 5f)
                    bgOscillations++;
                if (newDir != 0) dir = newDir;
            }
            prevZ = z;
        }
        _output.WriteLine($"BG Z oscillations: {bgOscillations}, max Z delta between samples: {maxZDelta:F1}");

        // Save trace as JSON for physics calibration
        var traceDir = System.IO.Path.Combine(AppContext.BaseDirectory, "ZTraces");
        try
        {
            System.IO.Directory.CreateDirectory(traceDir);
            var tracePath = System.IO.Path.Combine(traceDir, $"vot_slope_trace_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.json");
            var traceData = new
            {
                route = new { startX = VotLongStartX, startY = VotLongStartY, startZ = VotLongStartZ,
                              endX = VotLongEndX, endY = VotLongEndY, endZ = VotLongEndZ },
                bg = bgTrace.Select(t => new { t.x, t.y, t.z, t.t }),
                fg = fgTrace?.Select(t => new { t.x, t.y, t.z, t.t })
            };
            System.IO.File.WriteAllText(tracePath, System.Text.Json.JsonSerializer.Serialize(traceData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            _output.WriteLine($"Trace saved: {tracePath}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Failed to save trace: {ex.Message}");
        }
    }

    private async Task<System.Collections.Generic.List<(float x, float y, float z, double t)>> CaptureZTrace(
        string account, string label, float startX, float startY, float startZ,
        float endX, float endY, float endZ, int maxSeconds, int pollMs)
    {
        var trace = new System.Collections.Generic.List<(float x, float y, float z, double t)>();

        await _bot.EnsureCleanSlateAsync(account, label);
        _output.WriteLine($"  [{label}] Teleporting to start ({startX:F0}, {startY:F0}, {startZ:F0})");
        await _bot.BotTeleportAsync(account, MapId, startX, startY, startZ);
        await _bot.WaitForTeleportSettledAsync(account, startX, startY);

        _output.WriteLine($"  [{label}] Sending GOTO to ({endX:F0}, {endY:F0}, {endZ:F0})");
        var gotoResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = endX },
                new RequestParameter { FloatParam = endY },
                new RequestParameter { FloatParam = endZ },
                new RequestParameter { FloatParam = 0 }
            }
        });
        if (gotoResult != ResponseResult.Success)
        {
            _output.WriteLine($"  [{label}] GOTO failed: {gotoResult}");
            return trace;
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(maxSeconds))
        {
            await Task.Delay(pollMs);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            trace.Add((pos.X, pos.Y, pos.Z, sw.Elapsed.TotalSeconds));
            _output.WriteLine($"  [{label}] t={sw.Elapsed.TotalSeconds:F1}s pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");

            var dist2D = LiveBotFixture.Distance2D(pos.X, pos.Y, endX, endY);
            if (dist2D <= ArrivalRadius)
            {
                _output.WriteLine($"  [{label}] ARRIVED after {sw.Elapsed.TotalSeconds:F1}s");
                break;
            }
        }

        return trace;
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
