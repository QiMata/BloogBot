using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Live server validation of BG bot movement speed and Z stability.
/// Teleports to flat terrain, sends GOTO, polls snapshots, asserts speed and ground tracking.
///
/// Run: dotnet test --filter "FullyQualifiedName~MovementSpeedTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MovementSpeedTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor

    // Durotar road south of Razor Hill — flat open terrain, no buildings or cliffs.
    // The road sits at Z≈11-12 on ADT terrain. Use Z=12 so .go xyz lands on the road.
    private const float StartX = 285.0f;
    private const float StartY = -4740.0f;
    private const float StartZ = 12.0f;

    // Target ~50y north along the road — flat terrain, no obstacles.
    private const float TargetX = 285.0f;
    private const float TargetY = -4690.0f;
    private const float TargetZ = 12.0f;

    // Expected run speed for a level 1 character
    private const float ExpectedRunSpeed = 7.0f;

    // How often (in poll iterations) to teleport FG to shadow BG position
    private const int FgShadowEveryNPolls = 3;

    public MovementSpeedTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Teleport FG bot near a position so the user can observe BG behavior in the WoW client.
    /// Offset slightly (+2y X) so FG doesn't stack exactly on top of BG.
    /// Uses bot chat .go xyz — requires FG to be in-world and actionable.
    /// Silently swallows errors — FG shadowing is observational, never fails the test.
    /// </summary>
    private async Task ShadowFgToBgPositionAsync(float x, float y, float z, int mapId = MapId)
    {
        try
        {
            var fgAccount = _bot.FgAccountName;
            if (string.IsNullOrWhiteSpace(fgAccount) || !_bot.IsFgActionable) return;
            await _bot.BotTeleportAsync(fgAccount, mapId, x + 2f, y, z);
        }
        catch { /* observational only */ }
    }

    [SkippableFact]
    public async Task BG_FlatTerrain_MovesAtExpectedSpeed()
    {
        var bgAccount = _bot.BgAccountName;
        _output.WriteLine("=== BG Flat Terrain Movement Speed Test ===");
        _output.WriteLine($"BG character: {_bot.BgCharacterName} (account: {bgAccount})");
        _output.WriteLine($"Start: ({StartX}, {StartY}, {StartZ})  Target: ({TargetX}, {TargetY}, {TargetZ})");
        _output.WriteLine($"Expected speed: {ExpectedRunSpeed} y/s\n");

        // Teleport BG to start position (Z+3 to avoid undermap)
        await _bot.BotTeleportAsync(bgAccount!, MapId, StartX, StartY, StartZ);
        await _bot.WaitForTeleportSettledAsync(bgAccount!, StartX, StartY);
        // Teleport FG nearby so user can observe BG in the WoW client
        await ShadowFgToBgPositionAsync(StartX, StartY, StartZ);

        // Read start position from snapshot
        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(bgAccount!);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);

        float actualStartX = startPos!.X;
        float actualStartY = startPos.Y;
        float actualStartZ = startPos.Z;
        _output.WriteLine($"Actual start position: ({actualStartX:F2}, {actualStartY:F2}, {actualStartZ:F2})");

        // Send GOTO action
        var gotoResult = await _bot.SendActionAsync(bgAccount!, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = TargetX },
                new RequestParameter { FloatParam = TargetY },
                new RequestParameter { FloatParam = TargetZ },
                new RequestParameter { FloatParam = 3.0f } // arrival tolerance
            }
        });
        _output.WriteLine($"GOTO result: {gotoResult}\n");

        // Poll snapshots every 500ms for 15 seconds
        var samples = new List<(float elapsed, float x, float y, float z)>();
        var startTime = DateTime.UtcNow;

        _output.WriteLine($"{"Time",6} {"X",10} {"Y",12} {"Z",10} {"Dist",8} {"Speed",8}");
        _output.WriteLine(new string('-', 65));

        for (int i = 0; i < 30; i++) // 30 * 500ms = 15 seconds max
        {
            await Task.Delay(500);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(bgAccount!);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            float dx = pos.X - actualStartX;
            float dy = pos.Y - actualStartY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float speed = elapsed > 0.1f ? dist / elapsed : 0f;

            samples.Add((elapsed, pos.X, pos.Y, pos.Z));
            _output.WriteLine($"{elapsed,6:F1}s {pos.X,10:F2} {pos.Y,12:F2} {pos.Z,10:F2} {dist,8:F1} {speed,8:F2}");

            // Periodically teleport FG to shadow BG so user can observe
            if (i % FgShadowEveryNPolls == 0)
                await ShadowFgToBgPositionAsync(pos.X, pos.Y, pos.Z);

            // Stop early if we arrived
            float toTarget = MathF.Sqrt(
                (pos.X - TargetX) * (pos.X - TargetX) +
                (pos.Y - TargetY) * (pos.Y - TargetY));
            if (toTarget < 5.0f)
            {
                _output.WriteLine($"\nArrived at target (dist={toTarget:F1}y)");
                break;
            }
        }

        Assert.True(samples.Count >= 3, "Not enough position samples collected");

        // Compute overall speed from first to last sample
        var first = samples.First();
        var last = samples.Last();
        float totalDx = last.x - first.x;
        float totalDy = last.y - first.y;
        float totalDist = MathF.Sqrt(totalDx * totalDx + totalDy * totalDy);
        float totalTime = last.elapsed - first.elapsed;
        float avgSpeed = totalTime > 0.1f ? totalDist / totalTime : 0f;

        _output.WriteLine($"\nTotal distance: {totalDist:F1}y over {totalTime:F1}s = {avgSpeed:F2} y/s");
        _output.WriteLine($"Speed ratio: {avgSpeed / ExpectedRunSpeed:P0} of expected {ExpectedRunSpeed} y/s");

        // Assert speed is within reasonable range (50-150% of expected)
        Assert.True(avgSpeed > ExpectedRunSpeed * 0.5f,
            $"BG bot too slow: {avgSpeed:F2} y/s (expected >{ExpectedRunSpeed * 0.5f:F1})");
        Assert.True(avgSpeed < ExpectedRunSpeed * 1.5f,
            $"BG bot too fast: {avgSpeed:F2} y/s (expected <{ExpectedRunSpeed * 1.5f:F1})");
    }

    [SkippableFact]
    public async Task BG_FlatTerrain_ZStableWhileWalking()
    {
        var bgAccount = _bot.BgAccountName;
        _output.WriteLine("=== BG Flat Terrain Z Stability Test ===");

        // Teleport BG to start position, FG shadows nearby
        await _bot.BotTeleportAsync(bgAccount!, MapId, StartX, StartY, StartZ);
        await _bot.WaitForTeleportSettledAsync(bgAccount!, StartX, StartY);
        await ShadowFgToBgPositionAsync(StartX, StartY, StartZ);

        // Read start Z
        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(bgAccount!);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);
        float baseZ = startPos!.Z;
        _output.WriteLine($"Base Z after settle: {baseZ:F3}");

        // Send GOTO
        await _bot.SendActionAsync(bgAccount!, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = TargetX },
                new RequestParameter { FloatParam = TargetY },
                new RequestParameter { FloatParam = TargetZ },
                new RequestParameter { FloatParam = 3.0f }
            }
        });

        // Track Z values during walk
        var zValues = new List<float>();
        _output.WriteLine($"\n{"Time",6} {"Z",10} {"dZ",8}");
        _output.WriteLine(new string('-', 30));

        for (int i = 0; i < 20; i++) // 20 * 500ms = 10 seconds
        {
            await Task.Delay(500);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(bgAccount!);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            float dz = pos.Z - baseZ;
            zValues.Add(pos.Z);
            _output.WriteLine($"{(i + 1) * 0.5f,6:F1}s {pos.Z,10:F3} {dz,8:F3}");

            // Shadow FG to BG position for observation
            if (i % FgShadowEveryNPolls == 0)
                await ShadowFgToBgPositionAsync(pos.X, pos.Y, pos.Z);
        }

        Assert.True(zValues.Count >= 5, "Not enough Z samples collected");

        float minZ = zValues.Min();
        float maxZ = zValues.Max();
        float zRange = maxZ - minZ;
        _output.WriteLine($"\nZ range: {minZ:F3} to {maxZ:F3} (oscillation: {zRange:F3}y)");

        // Z should not oscillate more than 5y on mostly-flat terrain
        // (Valley of Trials has gentle hills; sinking/bouncing bugs produce 10+ y oscillation)
        Assert.True(zRange < 5.0f,
            $"Z oscillation too large: {zRange:F3}y (expected <5.0y) — possible sinking/bouncing");
    }

    [SkippableFact]
    public async Task DualClient_FlatWalk_SpeedComparison()
    {
        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        bool hasFg = _bot.IsFgActionable;

        global::Tests.Infrastructure.Skip.IfNot(hasFg, "FG client not available for dual-client comparison");

        _output.WriteLine("=== Dual-Client Flat Walk Speed Comparison ===");
        _output.WriteLine($"BG: {_bot.BgCharacterName}  FG: {_bot.FgCharacterName}\n");

        // Teleport both to start
        await Task.WhenAll(
            _bot.BotTeleportAsync(bgAccount!, MapId, StartX, StartY, StartZ),
            _bot.BotTeleportAsync(fgAccount!, MapId, StartX, StartY, StartZ));
        await _bot.WaitForTeleportSettledAsync(bgAccount!, StartX, StartY);

        // Read start positions
        await _bot.RefreshSnapshotsAsync();
        var bgStart = await _bot.GetSnapshotAsync(bgAccount!);
        var fgStart = await _bot.GetSnapshotAsync(fgAccount!);
        var bgStartPos = bgStart?.Player?.Unit?.GameObject?.Base?.Position;
        var fgStartPos = fgStart?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(bgStartPos);
        Assert.NotNull(fgStartPos);

        // Send GOTO to both
        var bgGoto = _bot.SendActionAsync(bgAccount!, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = TargetX },
                new RequestParameter { FloatParam = TargetY },
                new RequestParameter { FloatParam = TargetZ },
                new RequestParameter { FloatParam = 3.0f }
            }
        });
        var fgGoto = _bot.SendActionAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = TargetX },
                new RequestParameter { FloatParam = TargetY },
                new RequestParameter { FloatParam = TargetZ },
                new RequestParameter { FloatParam = 3.0f }
            }
        });
        await Task.WhenAll(bgGoto, fgGoto);

        // Poll both for 20 seconds
        _output.WriteLine($"{"Time",6} {"BG_Dist",10} {"BG_Z",8} {"FG_Dist",10} {"FG_Z",8} {"Dist_Gap",10}");
        _output.WriteLine(new string('-', 65));

        var bgSamples = new List<(float t, float dist, float z)>();
        var fgSamples = new List<(float t, float dist, float z)>();
        var startTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++) // 20 * 1s = 20 seconds
        {
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();

            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;

            var bgSnap = await _bot.GetSnapshotAsync(bgAccount!);
            var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;
            var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;

            float bgDist = 0f, bgZ = float.NaN;
            if (bgPos != null)
            {
                float dx = bgPos.X - bgStartPos!.X;
                float dy = bgPos.Y - bgStartPos.Y;
                bgDist = MathF.Sqrt(dx * dx + dy * dy);
                bgZ = bgPos.Z;
                bgSamples.Add((elapsed, bgDist, bgZ));
            }

            float fgDist = 0f, fgZ = float.NaN;
            if (fgPos != null)
            {
                float dx = fgPos.X - fgStartPos!.X;
                float dy = fgPos.Y - fgStartPos.Y;
                fgDist = MathF.Sqrt(dx * dx + dy * dy);
                fgZ = fgPos.Z;
                fgSamples.Add((elapsed, fgDist, fgZ));
            }

            float distGap = bgDist - fgDist;
            _output.WriteLine($"{elapsed,6:F1}s {bgDist,10:F1} {bgZ,8:F2} {fgDist,10:F1} {fgZ,8:F2} {distGap,10:F1}");
        }

        // Compute speeds
        if (bgSamples.Count >= 3 && fgSamples.Count >= 3)
        {
            float bgSpeed = bgSamples.Last().dist / bgSamples.Last().t;
            float fgSpeed = fgSamples.Last().dist / fgSamples.Last().t;
            float speedRatio = bgSpeed / fgSpeed;

            _output.WriteLine($"\nBG speed: {bgSpeed:F2} y/s  FG speed: {fgSpeed:F2} y/s  Ratio: {speedRatio:P0}");

            // BG should be within 30% of FG speed
            Assert.True(speedRatio > 0.7f && speedRatio < 1.3f,
                $"BG/FG speed mismatch: BG={bgSpeed:F2} FG={fgSpeed:F2} ratio={speedRatio:P0} (expected 70-130%)");
        }

        // Compare Z traces
        if (bgSamples.Count >= 3 && fgSamples.Count >= 3)
        {
            float avgBgZ = bgSamples.Average(s => s.z);
            float avgFgZ = fgSamples.Average(s => s.z);
            float zGap = avgBgZ - avgFgZ;
            _output.WriteLine($"Avg Z — BG: {avgBgZ:F2}  FG: {avgFgZ:F2}  Gap: {zGap:F2}y");

            // BG Z should not be consistently far from FG Z
            Assert.True(MathF.Abs(zGap) < 3.0f,
                $"BG/FG Z gap too large: {zGap:F2}y (expected <3.0y)");
        }
    }
}
