using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed movement-speed validation. SHODAN stages the BG action
/// target on a proven Durotar road route; the BotRunner target receives only
/// the Goto action under test.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MovementSpeedTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor

    // Durotar road — proven walkable from MovementParityTests.WindingPath
    private const float StartX = -500.0f;
    private const float StartY = -4800.0f;
    private const float StartZ = 41.0f; // Z+3 from terrain ~38

    // Durotar winding path target — 141y with terrain variation
    // Proven target from MovementParityTests.WindingPath
    private const float TargetX = -400.0f;
    private const float TargetY = -4700.0f;
    private const float TargetZ = 45.0f; // Z+3 from terrain ~42

    // Expected run speed for a level 1 character
    private const float ExpectedRunSpeed = 7.0f;

    public MovementSpeedTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task BG_Durotar_WindingPathSpeed()
    {
        var target = await EnsureMovementSpeedSettingsAndTargetAsync();

        _output.WriteLine("=== BG Movement: Durotar Winding Path (141y) ===");
        _output.WriteLine($"Start: ({StartX}, {StartY}, {StartZ})");
        _output.WriteLine($"Target: ({TargetX}, {TargetY}, {TargetZ})");

        float straightLineDist = MathF.Sqrt(
            (TargetX - StartX) * (TargetX - StartX) +
            (TargetY - StartY) * (TargetY - StartY));
        _output.WriteLine($"Straight-line distance: {straightLineDist:F0}y\n");

        await StageMovementStartAsync(target);

        // Read actual start position
        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);
        _output.WriteLine($"Actual start: ({startPos!.X:F1}, {startPos.Y:F1}, {startPos.Z:F1})\n");

        // Send GOTO to target
        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = TargetX },
                new RequestParameter { FloatParam = TargetY },
                new RequestParameter { FloatParam = TargetZ },
                new RequestParameter { FloatParam = 5.0f } // arrival tolerance
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        // Poll snapshots — expect ~141y / 7 y/s ≈ 20s, give 45s for pathing overhead
        var samples = new List<(float t, float x, float y, float z, float dist2d, float speed)>();
        var startTime = DateTime.UtcNow;
        bool arrived = false;

        _output.WriteLine($"{"Time",6} {"X",9} {"Y",11} {"Z",8} {"Dist",7} {"Speed",7} {"ToGoal",8}");
        _output.WriteLine(new string('-', 70));

        for (int i = 0; i < 90; i++) // 90 * 500ms = 45s max
        {
            await Task.Delay(500);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            float dx = pos.X - startPos.X;
            float dy = pos.Y - startPos.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float speed = elapsed > 0.5f ? dist / elapsed : 0f;
            float toGoal = MathF.Sqrt(
                (pos.X - TargetX) * (pos.X - TargetX) +
                (pos.Y - TargetY) * (pos.Y - TargetY));

            samples.Add((elapsed, pos.X, pos.Y, pos.Z, dist, speed));
            _output.WriteLine($"{elapsed,6:F1}s {pos.X,9:F1} {pos.Y,11:F1} {pos.Z,8:F1} {dist,7:F1} {speed,7:F2} {toGoal,8:F1}");

            if (toGoal < 8.0f)
            {
                arrived = true;
                _output.WriteLine($"\nArrived! (dist to goal: {toGoal:F1}y)");
                break;
            }
        }

        // --- Assertions ---
        Assert.True(samples.Count >= 5, "Not enough position samples");

        // 1. Speed: should average at least 50% of run speed over the whole path
        var last = samples.Last();
        float avgSpeed = last.t > 1f ? last.dist2d / last.t : 0f;
        _output.WriteLine($"\nAvg speed: {avgSpeed:F2} y/s (expected ≥{ExpectedRunSpeed * 0.5f:F1})");
        Assert.True(avgSpeed > ExpectedRunSpeed * 0.5f,
            $"Too slow: {avgSpeed:F2} y/s (min {ExpectedRunSpeed * 0.5f:F1})");

        // 2. Z stability: no sample should be below -10 (undermap detection)
        float minZ = samples.Min(s => s.z);
        _output.WriteLine($"Min Z: {minZ:F1} (must be > -10)");
        Assert.True(minZ > -10f,
            $"Bot fell through map: min Z = {minZ:F1}");

        // 3. Arrival: bot should reach the target area
        Assert.True(arrived,
            $"Bot did not arrive at target within 45s. Last dist: {samples.Last().dist2d:F1}y, " +
            $"speed: {avgSpeed:F2} y/s");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureMovementSpeedSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG movement-speed action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no movement dispatch.");

        return target;
    }

    private async Task StageMovementStartAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            MapId,
            StartX,
            StartY,
            StartZ,
            "Durotar winding-path speed start");
        if (staged)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} movement-speed staged");
            return;
        }

        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.Fail(
            $"Expected {target.RoleLabel} {target.AccountName} to reach movement-speed start. " +
            $"finalMap={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }
}
