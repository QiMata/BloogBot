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
/// Movement parity tests: FG (gold standard) and BG (headless physics) walk identical paths.
/// Both characters MUST be the same race/gender for identical capsule dimensions.
/// Records per-frame transform data and flags per-frame speed anomalies (collision/stuck).
///
/// Run: dotnet test --filter "MovementParityTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MovementParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    private const float ExpectedRunSpeed = 7.0f;
    // ~20Hz polling. Actual game runs at 60fps (16.7ms) but snapshot IPC adds latency.
    private const int PollIntervalMs = 50;
    // FG updates position in ~500ms bursts (only when WoW client sends movement packets).
    // Use a rolling window to detect true stuck conditions across packet boundaries.
    // FG updates position in ~500ms bursts (only when WoW client sends movement packets).
    // Use a 1.2s rolling window spanning at least 2 packet intervals to detect true stuck.
    private const float FgStuckWindowSec = 1.2f;
    private const float BgStuckWindowSec = 0.20f;   // BG updates every 50ms tick
    // Minimum displacement over the window period before flagging stuck.
    // At 7 y/s over 1.2s = 8.4y; flag if < 10% (0.84y) — only fires on genuine wall collisions.
    private const float FgMinWindowDisplacement = ExpectedRunSpeed * FgStuckWindowSec * 0.10f;
    // At 7 y/s over 0.20s = 1.4y; flag if < 20% (0.28y).
    private const float BgMinWindowDisplacement = ExpectedRunSpeed * BgStuckWindowSec * 0.20f;

    public MovementParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_FlatPath()
    {
        await RunParityTest(
            name: "Valley of Trials — Flat Path",
            startX: -284f, startY: -4383f, startZ: 57f,
            targetX: -320f, targetY: -4420f, targetZ: 57f,
            maxSeconds: 20);
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_HillPath()
    {
        // Start from the road, walk toward the cave entrance (uphill then down)
        await RunParityTest(
            name: "Valley of Trials — Hill Path",
            startX: -284f, startY: -4383f, startZ: 57f,
            targetX: -254f, targetY: -4340f, targetZ: 55f,
            maxSeconds: 20);
    }

    [SkippableFact]
    public async Task Parity_Durotar_RoadPath()
    {
        // Road near Razor Hill — relatively flat, long straight
        await RunParityTest(
            name: "Durotar — Road Path",
            startX: -500f, startY: -4800f, startZ: 38f,
            targetX: -460f, targetY: -4760f, targetZ: 38f,
            maxSeconds: 20);
    }

    /// <summary>
    /// Core parity test runner. Teleports both bots, sends GOTO, records transforms,
    /// detects per-frame anomalies, and reports parity metrics.
    /// </summary>
    private async Task RunParityTest(
        string name,
        float startX, float startY, float startZ,
        float targetX, float targetY, float targetZ,
        int maxSeconds)
    {
        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        var hasFg = await _bot.CheckFgActionableAsync(requireTeleportProbe: false);
        global::Tests.Infrastructure.Skip.IfNot(hasFg,
            "FG client required — this test compares FG (gold standard) with BG (headless physics)");

        // Both characters MUST be Orc Warrior (configured in StateManagerSettings.json).
        // Same race/gender = identical capsule dimensions (radius=0.3064, height=2.0313).
        // If this changes, update the settings so both match.
        _output.WriteLine($"=== {name} ===");
        _output.WriteLine($"FG: {_bot.FgCharacterName} (TESTBOT1=Orc Warrior)");
        _output.WriteLine($"BG: {_bot.BgCharacterName} (TESTBOT2=Orc Warrior)");

        float routeDist = Distance2D(startX, startY, targetX, targetY);
        _output.WriteLine($"Route: ({startX},{startY},{startZ}) -> ({targetX},{targetY},{targetZ}) = {routeDist:F1}y\n");

        // --- TELEPORT ---
        await Task.WhenAll(
            _bot.BotTeleportAsync(bgAccount!, MapId, startX, startY, startZ),
            _bot.BotTeleportAsync(fgAccount!, MapId, startX, startY, startZ));

        var bgSettled = await _bot.WaitForTeleportSettledAsync(bgAccount!, startX, startY, timeoutMs: 8000);
        var fgSettled = await _bot.WaitForTeleportSettledAsync(fgAccount!, startX, startY, timeoutMs: 8000);

        await _bot.RefreshSnapshotsAsync();
        var fgStart = await _bot.GetSnapshotAsync(fgAccount!);
        var bgStart = await _bot.GetSnapshotAsync(bgAccount!);
        var fgStartPos = fgStart?.Player?.Unit?.GameObject?.Base?.Position;
        var bgStartPos = bgStart?.Player?.Unit?.GameObject?.Base?.Position;

        _output.WriteLine($"[SETUP] BG settled={bgSettled} pos=({bgStartPos?.X:F1},{bgStartPos?.Y:F1},{bgStartPos?.Z:F2})");
        _output.WriteLine($"[SETUP] FG settled={fgSettled} pos=({fgStartPos?.X:F1},{fgStartPos?.Y:F1},{fgStartPos?.Z:F2})");
        Assert.NotNull(fgStartPos);

        // Allow physics to snap to ground
        await Task.Delay(2000);

        // --- GOTO ---
        var bgGoto = _bot.SendActionAsync(bgAccount!, MakeGoto(targetX, targetY, targetZ));
        var fgGoto = _bot.SendActionAsync(fgAccount!, MakeGoto(targetX, targetY, targetZ));
        await Task.WhenAll(bgGoto, fgGoto);
        _output.WriteLine($"[ACTION] BG GOTO={bgGoto.Result}  FG GOTO={fgGoto.Result}\n");

        // --- POLL TRANSFORMS ---
        var fgSamples = new List<TransformSample>();
        var bgSamples = new List<TransformSample>();
        var fgAnomalies = new List<string>();
        var bgAnomalies = new List<string>();
        var startTime = DateTime.UtcNow;
        int maxPolls = maxSeconds * (1000 / PollIntervalMs);
        bool fgArrived = false, bgArrived = false;

        // Print header
        _output.WriteLine($"{"t",7} | {"FG_X",8} {"FG_Y",8} {"FG_Z",7} {"Flg",5} {"FGspd",5} | {"BG_X",8} {"BG_Y",8} {"BG_Z",7} {"Flg",5} {"BGspd",5} | {"dXY",5} {"dZ",6} {"note",4}");
        _output.WriteLine(new string('-', 120));

        for (int i = 0; i < maxPolls; i++)
        {
            await Task.Delay(PollIntervalMs);
            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;

            await _bot.RefreshSnapshotsAsync();
            var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
            var bgSnap = await _bot.GetSnapshotAsync(bgAccount!);

            var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;

            float fgX = fgPos?.X ?? float.NaN, fgY = fgPos?.Y ?? float.NaN, fgZ = fgPos?.Z ?? float.NaN;
            float bgX = bgPos?.X ?? float.NaN, bgY = bgPos?.Y ?? float.NaN, bgZ = bgPos?.Z ?? float.NaN;
            uint fgFlags = fgSnap?.Player?.Unit?.MovementFlags ?? 0;
            uint bgFlags = bgSnap?.Player?.Unit?.MovementFlags ?? 0;

            // Per-frame speed calculation (frame-to-frame for display)
            float fgFrameSpeed = 0f, bgFrameSpeed = 0f;
            string note = "";

            if (!float.IsNaN(fgX) && fgSamples.Count > 0)
            {
                var prev = fgSamples[^1];
                float dt = elapsed - prev.T;
                if (dt > 0.001f)
                {
                    float frameDist = Distance2D(fgX, fgY, prev.X, prev.Y);
                    fgFrameSpeed = frameDist / dt;
                }

                // FG stuck detection: rolling window over 650ms (spans packet intervals).
                // FG only updates position when WoW sends movement packets (~500ms bursts),
                // so per-frame checks at 50ms produce false positives.
                var windowSample = fgSamples.LastOrDefault(s => elapsed - s.T >= FgStuckWindowSec);
                if (windowSample != null && (fgFlags & 0x1) != 0)
                {
                    float windowDist = Distance2D(fgX, fgY, windowSample.X, windowSample.Y);
                    float windowDt = elapsed - windowSample.T;
                    if (windowDt >= FgStuckWindowSec && windowDist < FgMinWindowDisplacement)
                    {
                        var msg = $"[FG STUCK] t={elapsed:F2}s pos=({fgX:F1},{fgY:F1},{fgZ:F1}) window={windowDt:F2}s disp={windowDist:F3}y flags=0x{fgFlags:X}";
                        fgAnomalies.Add(msg);
                        note = "FG!";
                    }
                }
            }

            if (!float.IsNaN(bgX) && bgSamples.Count > 0)
            {
                var prev = bgSamples[^1];
                float dt = elapsed - prev.T;
                if (dt > 0.001f)
                {
                    float frameDist = Distance2D(bgX, bgY, prev.X, prev.Y);
                    bgFrameSpeed = frameDist / dt;
                }

                // BG stuck detection: shorter window (150ms) since BG updates every tick.
                var windowSample = bgSamples.LastOrDefault(s => elapsed - s.T >= BgStuckWindowSec);
                if (windowSample != null && (bgFlags & 0x1) != 0)
                {
                    float windowDist = Distance2D(bgX, bgY, windowSample.X, windowSample.Y);
                    float windowDt = elapsed - windowSample.T;
                    if (windowDt >= BgStuckWindowSec && windowDist < BgMinWindowDisplacement)
                    {
                        var msg = $"[BG STUCK] t={elapsed:F2}s pos=({bgX:F1},{bgY:F1},{bgZ:F1}) window={windowDt:F2}s disp={windowDist:F3}y flags=0x{bgFlags:X}";
                        bgAnomalies.Add(msg);
                        note += "BG!";
                    }
                }
            }

            if (!float.IsNaN(fgX)) fgSamples.Add(new TransformSample(elapsed, fgX, fgY, fgZ, fgFlags));
            if (!float.IsNaN(bgX)) bgSamples.Add(new TransformSample(elapsed, bgX, bgY, bgZ, bgFlags));

            float dXY = (!float.IsNaN(fgX) && !float.IsNaN(bgX)) ? Distance2D(fgX, fgY, bgX, bgY) : float.NaN;
            float dZ = (!float.IsNaN(fgZ) && !float.IsNaN(bgZ)) ? bgZ - fgZ : float.NaN;

            // Print every 10th sample (~500ms) or anomalous frames
            if (i % 10 == 0 || note.Length > 0)
            {
                _output.WriteLine(
                    $"{elapsed,7:F2}s | {fgX,8:F1} {fgY,8:F1} {fgZ,7:F2} {fgFlags,5:X} {fgFrameSpeed,5:F1} | " +
                    $"{bgX,8:F1} {bgY,8:F1} {bgZ,7:F2} {bgFlags,5:X} {bgFrameSpeed,5:F1} | " +
                    $"{(float.IsNaN(dXY) ? "  n/a" : $"{dXY,5:F1}")} " +
                    $"{(float.IsNaN(dZ) ? "   n/a" : $"{dZ,6:F2}")} {note}");
            }

            if (!float.IsNaN(fgX) && Distance2D(fgX, fgY, targetX, targetY) < 5f) fgArrived = true;
            if (!float.IsNaN(bgX) && Distance2D(bgX, bgY, targetX, targetY) < 5f) bgArrived = true;
            if (fgArrived && bgArrived) break;
        }

        // --- SUMMARY ---
        _output.WriteLine($"\n=== RESULTS: {name} ===");
        _output.WriteLine($"Samples: FG={fgSamples.Count} BG={bgSamples.Count}  Arrived: FG={fgArrived} BG={bgArrived}");

        PrintBotSummary("FG", fgSamples);
        PrintBotSummary("BG", bgSamples);

        // Per-frame anomalies
        if (fgAnomalies.Count > 0)
        {
            _output.WriteLine($"\n--- FG Anomalies ({fgAnomalies.Count} stuck frames) ---");
            foreach (var a in fgAnomalies.Take(20))
                _output.WriteLine($"  {a}");
            if (fgAnomalies.Count > 20)
                _output.WriteLine($"  ... and {fgAnomalies.Count - 20} more");
        }
        if (bgAnomalies.Count > 0)
        {
            _output.WriteLine($"\n--- BG Anomalies ({bgAnomalies.Count} stuck frames) ---");
            foreach (var a in bgAnomalies.Take(20))
                _output.WriteLine($"  {a}");
            if (bgAnomalies.Count > 20)
                _output.WriteLine($"  ... and {bgAnomalies.Count - 20} more");
        }

        // Parity comparison
        if (fgSamples.Count >= 2 && bgSamples.Count >= 2)
        {
            var deltas = new List<(float t, float dXY, float dZ)>();
            foreach (var fg in fgSamples)
            {
                var bg = bgSamples.MinBy(b => MathF.Abs(b.T - fg.T));
                if (bg != null && MathF.Abs(bg.T - fg.T) < 1.0f)
                    deltas.Add((fg.T, Distance2D(fg.X, fg.Y, bg.X, bg.Y), bg.Z - fg.Z));
            }

            if (deltas.Count > 0)
            {
                _output.WriteLine($"\n--- Parity ---");
                _output.WriteLine($"XY: avg={deltas.Average(d => d.dXY):F1}y  max={deltas.Max(d => d.dXY):F1}y");
                _output.WriteLine($" Z: avg={deltas.Average(d => MathF.Abs(d.dZ)):F2}y  max={deltas.Max(d => MathF.Abs(d.dZ)):F2}y");
            }
        }

        // Speed segment analysis — flag 1-second windows where speed drops below 50% expected
        PrintSpeedSegments("FG", fgSamples, fgAnomalies);
        PrintSpeedSegments("BG", bgSamples, bgAnomalies);

        Assert.True(fgSamples.Count >= 3 || bgSamples.Count >= 3,
            "Neither bot produced enough position samples");
    }

    private void PrintBotSummary(string label, List<TransformSample> samples)
    {
        if (samples.Count < 2) { _output.WriteLine($"{label}: no data"); return; }

        var first = samples.First();
        var last = samples.Last();
        float dist = Distance2D(first.X, first.Y, last.X, last.Y);
        float time = last.T - first.T;
        float speed = time > 0.1f ? dist / time : 0f;
        float minZ = samples.Min(s => s.Z);
        float maxZ = samples.Max(s => s.Z);

        _output.WriteLine($"{label}: {dist:F1}y in {time:F1}s = {speed:F2} y/s  Z=[{minZ:F2}..{maxZ:F2}] (range {maxZ - minZ:F2}y)");
    }

    /// <summary>
    /// Break samples into 1-second windows and flag segments where speed drops below 50% expected.
    /// This detects collision, pathfinding obstacles, and route deviations.
    /// </summary>
    private void PrintSpeedSegments(string label, List<TransformSample> samples, List<string> anomalies)
    {
        if (samples.Count < 4) return;

        var slowSegments = new List<string>();
        float windowSec = 1.0f;
        float slowThreshold = ExpectedRunSpeed * 0.50f; // 3.5 y/s

        int windowStart = 0;
        for (int j = 1; j < samples.Count; j++)
        {
            float dt = samples[j].T - samples[windowStart].T;
            if (dt >= windowSec)
            {
                float segDist = Distance2D(samples[j].X, samples[j].Y,
                    samples[windowStart].X, samples[windowStart].Y);
                float segSpeed = segDist / dt;

                // Only flag if FORWARD flag was set (bot intended to move)
                bool hadForward = samples.Skip(windowStart).Take(j - windowStart)
                    .Any(s => (s.MoveFlags & 0x1) != 0);

                if (segSpeed < slowThreshold && hadForward)
                {
                    slowSegments.Add(
                        $"  t={samples[windowStart].T:F1}-{samples[j].T:F1}s  speed={segSpeed:F1}y/s  pos=({samples[j].X:F1},{samples[j].Y:F1},{samples[j].Z:F1})");
                }
                windowStart = j;
            }
        }

        if (slowSegments.Count > 0)
        {
            _output.WriteLine($"\n--- {label} Slow Segments (<{slowThreshold:F1} y/s over 1s windows) ---");
            foreach (var seg in slowSegments.Take(15))
                _output.WriteLine(seg);
            if (slowSegments.Count > 15)
                _output.WriteLine($"  ... and {slowSegments.Count - 15} more");
        }
    }

    private static ActionMessage MakeGoto(float x, float y, float z)
        => new()
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = x },
                new RequestParameter { FloatParam = y },
                new RequestParameter { FloatParam = z },
                new RequestParameter { FloatParam = 3.0f }
            }
        };

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private sealed record TransformSample(float T, float X, float Y, float Z, uint MoveFlags);
}
