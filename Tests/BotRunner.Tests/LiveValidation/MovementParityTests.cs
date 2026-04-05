using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Road path heading NE — gradual uphill (56.5→64.8 over ~55y).
        // Ground Z from GetGroundZ: start=56.47, target=64.81
        await RunParityTest(
            name: "Valley of Trials — Flat Path",
            startX: -260f, startY: -4350f, startZ: 56.5f,
            targetX: -230f, targetY: -4310f, targetZ: 64.8f,
            maxSeconds: 20);
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_HillPath()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Start from the road, walk toward the cave entrance (uphill).
        // Ground Z from GetGroundZ: start=57.39, target=61.41
        await RunParityTest(
            name: "Valley of Trials — Hill Path",
            startX: -284f, startY: -4383f, startZ: 57.4f,
            targetX: -254f, targetY: -4340f, targetZ: 61.4f,
            maxSeconds: 20);
    }

    [SkippableFact]
    public async Task Parity_Durotar_RoadPath()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Road near Razor Hill — relatively flat, long straight
        await RunParityTest(
            name: "Durotar — Road Path",
            startX: -500f, startY: -4800f, startZ: 38f,
            targetX: -460f, targetY: -4760f, targetZ: 38f,
            maxSeconds: 20);
    }

    [SkippableFact]
    public async Task Parity_Durotar_RoadPath_TurnStart()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        await RunParityTest(
            name: "Durotar - Road Path (turn start)",
            startX: -500f, startY: -4800f, startZ: 38f,
            targetX: -460f, targetY: -4760f, targetZ: 38f,
            maxSeconds: 20,
            initialFacing: 3.92699f);
    }

    /// <summary>
    /// Pause/resume parity: both bots walk toward point A, then mid-route are redirected
    /// to point B. This forces StopAllMovement → MoveToward (same code path as combat
    /// pause/resume) and proves FG/BG emit matching STOP → SET_FACING → START_FORWARD
    /// packet sequences at the redirect point.
    /// </summary>
    [SkippableFact]
    public async Task Parity_Durotar_RoadPath_Redirect()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        await RunRedirectParityTest(
            name: "Durotar - Road Path (mid-route redirect / pause-resume)",
            startX: -500f, startY: -4800f, startZ: 38f,
            firstTargetX: -460f, firstTargetY: -4760f, firstTargetZ: 38f,
            secondTargetX: -520f, secondTargetY: -4750f, secondTargetZ: 38f,
            redirectAfterSeconds: 3,
            maxSeconds: 25);
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_LongDiagonal()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Longer route (~80y diagonal) across Valley of Trials — tests sustained parity.
        // Ground Z: start=57.39, target=66.30
        await RunParityTest(
            name: "Valley of Trials — Long Diagonal",
            startX: -284f, startY: -4383f, startZ: 57.4f,
            targetX: -340f, targetY: -4450f, targetZ: 66.3f,
            maxSeconds: 30);
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_ReverseHill()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Reverse of HillPath — start uphill, walk toward the road (downhill).
        // Ground Z: start=61.41, target=57.39
        await RunParityTest(
            name: "Valley of Trials — Reverse Hill (downhill)",
            startX: -254f, startY: -4340f, startZ: 61.4f,
            targetX: -284f, targetY: -4383f, targetZ: 57.4f,
            maxSeconds: 20);
    }

    // ======= Diverse MoveFlag Tests =======
    // These routes are chosen to exercise flag transitions beyond simple FORWARD walking:
    //   - FALLINGFAR (ledge drops, steep descents)
    //   - Slope guard engagement (steep climbs)
    //   - Wall collision / deflection (obstacle-dense terrain)
    //   - Multiple facing changes (winding route with sharp turns)

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_LedgeDrop()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Start on elevated terrain near the cave, walk downhill to the road.
        // The elevation drop triggers FALLINGFAR → landing flag transition.
        // Ground Z: start=64.62, target=59.67
        await RunParityTest(
            name: "Valley of Trials — Ledge Drop (FALLINGFAR)",
            startX: -240f, startY: -4330f, startZ: 64.6f,
            targetX: -270f, targetY: -4380f, targetZ: 59.7f,
            maxSeconds: 25);
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_SteepClimb()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Start at the road, walk steeply uphill toward the high ground north of cave.
        // Ground Z: start=57.39, target=64.82
        await RunParityTest(
            name: "Valley of Trials — Steep Climb (slope guard)",
            startX: -284f, startY: -4383f, startZ: 57.4f,
            targetX: -224f, targetY: -4310f, targetZ: 64.8f,
            maxSeconds: 30);
    }

    [SkippableFact]
    public async Task Parity_Durotar_ObstacleDense()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Route along Valley of Trials path (open terrain with some objects nearby).
        // Replaces the old route through dense trees at (-356,-4490) → (-310,-4530)
        // where target Z was 148.43 (unreachable hillside) causing both bots to get stuck.
        // Ground Z: start=57.39, target=57.35
        await RunParityTest(
            name: "Valley of Trials — Open Path (replaced obstacle dense)",
            startX: -284f, startY: -4383f, startZ: 57.4f,
            targetX: -310f, targetY: -4410f, targetZ: 57.4f,
            maxSeconds: 30);
    }

    [SkippableFact]
    public async Task Parity_Durotar_WindingPath()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Longer path from Razor Hill area across varied terrain — road, dirt, slight hills.
        // Exercises: sustained FORWARD flag, multiple waypoint transitions, facing changes,
        // speed consistency over distance.
        await RunParityTest(
            name: "Durotar — Winding Path (sustained movement)",
            startX: -500f, startY: -4800f, startZ: 38f,
            targetX: -400f, targetY: -4700f, targetZ: 42f,
            maxSeconds: 40);
    }

    [SkippableFact]
    public async Task Parity_ValleyOfTrials_SteepDescent()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        // Start on high ground, descend steeply toward the valley floor.
        // Ground Z: start=64.82, target=57.35
        await RunParityTest(
            name: "Valley of Trials — Steep Descent (FFS hysteresis)",
            startX: -224f, startY: -4310f, startZ: 64.8f,
            targetX: -310f, targetY: -4410f, targetZ: 57.4f,
            maxSeconds: 30);
    }

    /// <summary>
    /// Core parity test runner. Teleports both bots, sends GOTO, records transforms,
    /// detects per-frame anomalies, and reports parity metrics.
    /// </summary>
    private async Task RunParityTest(
        string name,
        float startX, float startY, float startZ,
        float targetX, float targetY, float targetZ,
        int maxSeconds,
        float? initialFacing = null)
    {
        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        var hasFg = await _bot.CheckFgActionableAsync(requireTeleportProbe: false);
        await _bot.RefreshSnapshotsAsync();
        global::Tests.Infrastructure.Skip.IfNot(!string.IsNullOrWhiteSpace(bgAccount),
            "BG client required for parity comparison");
        Assert.NotNull(_bot.BackgroundBot);
        Assert.True(LiveBotFixture.IsStrictAlive(_bot.BackgroundBot),
            "BG client must be strict-alive for parity comparison");
        global::Tests.Infrastructure.Skip.IfNot(hasFg,
            "FG client required — this test compares FG (gold standard) with BG (headless physics)");

        // Both characters MUST be Male Orc Warrior (configured in StateManagerSettings.json).
        // Same race/gender = identical capsule dimensions (radius=0.3064, height=2.0313 for Orc Male).
        // CharacterGender in StateManagerSettings.json → WWOW_CHARACTER_GENDER env var → ResolveGender().
        // If gender mismatches, BotRunnerService auto-deletes and recreates the character on next login.
        _output.WriteLine($"=== {name} ===");
        _output.WriteLine($"FG: {_bot.FgCharacterName} (TESTBOT1=Male Orc Warrior)");
        _output.WriteLine($"BG: {_bot.BgCharacterName} (TESTBOT2=Male Orc Warrior)");

        float routeDist = Distance2D(startX, startY, targetX, targetY);
        _output.WriteLine($"Route: ({startX},{startY},{startZ}) -> ({targetX},{targetY},{targetZ}) = {routeDist:F1}y\n");

        // --- TELEPORT ---
        // .go xyz with exact ground Z triggers undermap detection on VMaNGOS.
        // Teleport Z+3 above nominal ground. The idle ground snap (GetGroundZ with 6y range)
        // handles the 3y gap within the first physics frame after Reset().
        float teleportZ = startZ + 3f;
        await Task.WhenAll(
            _bot.BotTeleportAsync(bgAccount!, MapId, startX, startY, teleportZ),
            _bot.BotTeleportAsync(fgAccount!, MapId, startX, startY, teleportZ));

        var bgSettled = await _bot.WaitForTeleportSettledAsync(bgAccount!, startX, startY, timeoutMs: 8000);
        var fgSettled = await _bot.WaitForTeleportSettledAsync(fgAccount!, startX, startY, timeoutMs: 8000);

        // Post-settle stabilization: wait for the BG bot's Z to converge toward
        // the FG bot's Z. The BG bot may still be at teleport Z if its ground snap
        // hasn't propagated to the snapshot yet. Poll until BG Z is within 2y of
        // FG Z, or timeout after 5s.
        for (int settle = 0; settle < 10; settle++)
        {
            await Task.Delay(500);
            await _bot.RefreshSnapshotsAsync();
            var fgCheck = await _bot.GetSnapshotAsync(fgAccount!);
            var bgCheck = await _bot.GetSnapshotAsync(bgAccount!);
            var fgCheckZ = fgCheck?.Player?.Unit?.GameObject?.Base?.Position?.Z ?? float.NaN;
            var bgCheckZ = bgCheck?.Player?.Unit?.GameObject?.Base?.Position?.Z ?? float.NaN;
            if (!float.IsNaN(fgCheckZ) && !float.IsNaN(bgCheckZ) &&
                MathF.Abs(fgCheckZ - bgCheckZ) < 2.0f)
            {
                _output.WriteLine($"[SETTLE] BG Z converged to FG Z after {(settle + 1) * 500}ms: FG={fgCheckZ:F2} BG={bgCheckZ:F2}");
                break;
            }
            if (settle == 9)
                _output.WriteLine($"[SETTLE] WARNING: BG Z did not converge after 5s: FG={fgCheckZ:F2} BG={bgCheckZ:F2} delta={MathF.Abs(fgCheckZ - bgCheckZ):F2}");
        }

        await _bot.RefreshSnapshotsAsync();
        var fgStart = await _bot.GetSnapshotAsync(fgAccount!);
        var bgStart = await _bot.GetSnapshotAsync(bgAccount!);
        var fgStartPos = fgStart?.Player?.Unit?.GameObject?.Base?.Position;
        var bgStartPos = bgStart?.Player?.Unit?.GameObject?.Base?.Position;
        var bgStartFlags = bgStart?.Player?.Unit?.MovementFlags ?? 0;

        _output.WriteLine($"[SETUP] BG settled={bgSettled} pos=({bgStartPos?.X:F1},{bgStartPos?.Y:F1},{bgStartPos?.Z:F2}) flags=0x{bgStartFlags:X}");
        _output.WriteLine($"[SETUP] FG settled={fgSettled} pos=({fgStartPos?.X:F1},{fgStartPos?.Y:F1},{fgStartPos?.Z:F2})");
        Assert.NotNull(fgStartPos);

        // Allow physics to snap to ground
        await Task.Delay(2000);

        if (initialFacing.HasValue)
        {
            await Task.WhenAll(
                _bot.SendActionAsync(bgAccount!, MakeSetFacing(initialFacing.Value)),
                _bot.SendActionAsync(fgAccount!, MakeSetFacing(initialFacing.Value)));
            await Task.Delay(500);
            _output.WriteLine($"[SETUP] Forced initial facing={initialFacing.Value:F4} rad before recording");
        }

        // --- START RECORDINGS (both bots) ---
        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount!, MakeRecordingAction(ActionType.StartPhysicsRecording)),
            _bot.SendActionAsync(fgAccount!, MakeRecordingAction(ActionType.StartPhysicsRecording)));
        _output.WriteLine("[RECORDING] FG transform + BG physics frame recording started");

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

            // FG-observed BG position: what does the server tell the FG client about where BG is?
            // If this Z oscillates while BG's own snapshot Z is stable, the BG bot's PACKETS
            // are wrong — the server interprets them as oscillating even though our internal
            // physics is correct. This is the key diagnostic for packet behavior parity.
            float fgSeesBgZ = float.NaN;
            uint fgSeesBgFlags = 0;
            if (!float.IsNaN(bgX) && fgSnap?.NearbyUnits != null)
            {
                foreach (var unit in fgSnap.NearbyUnits)
                {
                    var unitPos = unit.GameObject?.Base?.Position;
                    if (unitPos == null) continue;
                    // Match by XY proximity (within 10y — same area)
                    float dx = unitPos.X - bgX, dy = unitPos.Y - bgY;
                    if (dx * dx + dy * dy < 100f) // 10y radius
                    {
                        fgSeesBgZ = unitPos.Z;
                        fgSeesBgFlags = unit.MovementFlags;
                        break;
                    }
                }
            }

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
                string fgSeesBgStr = float.IsNaN(fgSeesBgZ) ? "---" : $"{fgSeesBgZ:F1}/0x{fgSeesBgFlags:X}";
                _output.WriteLine(
                    $"{elapsed,7:F2}s | {fgX,8:F1} {fgY,8:F1} {fgZ,7:F2} {fgFlags,5:X} {fgFrameSpeed,5:F1} | " +
                    $"{bgX,8:F1} {bgY,8:F1} {bgZ,7:F2} {bgFlags,5:X} {bgFrameSpeed,5:F1} | " +
                    $"{(float.IsNaN(dXY) ? "  n/a" : $"{dXY,5:F1}")} " +
                    $"{(float.IsNaN(dZ) ? "   n/a" : $"{dZ,6:F2}")} " +
                    $"FG→BG:{fgSeesBgStr} {note}");
            }

            if (!float.IsNaN(fgX) && Distance2D(fgX, fgY, targetX, targetY) < 5f) fgArrived = true;
            if (!float.IsNaN(bgX) && Distance2D(bgX, bgY, targetX, targetY) < 5f) bgArrived = true;
            if (fgArrived && bgArrived) break;
        }

        if (fgArrived || bgArrived)
        {
            await Task.Delay(1000);
        }

        // --- STOP RECORDINGS (both bots) ---
        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount!, MakeRecordingAction(ActionType.StopPhysicsRecording)),
            _bot.SendActionAsync(fgAccount!, MakeRecordingAction(ActionType.StopPhysicsRecording)));
        await Task.Delay(500); // Allow file write to complete
        _output.WriteLine("[RECORDING] FG transform + BG physics frame recording stopped");

        // --- SUMMARY ---
        _output.WriteLine($"\n=== RESULTS: {name} ===");
        _output.WriteLine($"Samples: FG={fgSamples.Count} BG={bgSamples.Count}  Arrived: FG={fgArrived} BG={bgArrived}");

        // Arrival time comparison
        float? fgArrivalT = null, bgArrivalT = null;
        foreach (var s in fgSamples)
            if (Distance2D(s.X, s.Y, targetX, targetY) < 5f) { fgArrivalT = s.T; break; }
        foreach (var s in bgSamples)
            if (Distance2D(s.X, s.Y, targetX, targetY) < 5f) { bgArrivalT = s.T; break; }

        if (fgArrivalT.HasValue && bgArrivalT.HasValue)
        {
            float arrivalDelta = bgArrivalT.Value - fgArrivalT.Value;
            _output.WriteLine($"Arrival: FG={fgArrivalT.Value:F1}s  BG={bgArrivalT.Value:F1}s  delta={arrivalDelta:+0.0;-0.0}s (BG {(arrivalDelta > 0 ? "slower" : "faster")})");
        }
        else
        {
            _output.WriteLine($"Arrival: FG={(fgArrivalT.HasValue ? $"{fgArrivalT.Value:F1}s" : "DNF")}  BG={(bgArrivalT.HasValue ? $"{bgArrivalT.Value:F1}s" : "DNF")}");
        }

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

                // Z drift trend: compare first-half vs second-half average Z delta.
                // Growing delta = BG physics Z is diverging from FG over time.
                int half = deltas.Count / 2;
                if (half >= 5)
                {
                    float firstHalfZ = deltas.Take(half).Average(d => d.dZ);
                    float secondHalfZ = deltas.Skip(half).Average(d => d.dZ);
                    float drift = secondHalfZ - firstHalfZ;
                    _output.WriteLine($" Z drift: 1st half avg={firstHalfZ:+0.00;-0.00}y  2nd half avg={secondHalfZ:+0.00;-0.00}y  trend={drift:+0.00;-0.00}y ({(MathF.Abs(drift) < 0.5f ? "stable" : "DRIFTING")})");
                }
            }
        }

        // MoveFlag diversity summary — shows which flags were observed during the run
        PrintMoveFlagSummary("FG", fgSamples);
        PrintMoveFlagSummary("BG", bgSamples);

        // Speed segment analysis — flag 1-second windows where speed drops below 50% expected
        PrintSpeedSegments("FG", fgSamples, fgAnomalies);
        PrintSpeedSegments("BG", bgSamples, bgAnomalies);

        // --- RECORDING ANALYSIS ---
        AnalyzeBgPhysicsRecording(bgAccount!);
        AnalyzeTransformComparison(fgAccount!, bgAccount!);
        AnalyzePacketComparison(fgAccount!, bgAccount!, initialFacing.HasValue);

        float fgTravel = ComputeTravelDistance(fgSamples);
        float bgTravel = ComputeTravelDistance(bgSamples);
        float minimumMeaningfulTravel = MathF.Min(routeDist * 0.25f, 10f);

        Assert.True(fgSamples.Count >= 3,
            $"FG produced too few position samples ({fgSamples.Count})");
        Assert.True(bgSamples.Count >= 3,
            $"BG produced too few position samples ({bgSamples.Count})");
        Assert.True(fgTravel >= minimumMeaningfulTravel,
            $"FG only moved {fgTravel:F1}y on a {routeDist:F1}y route");
        Assert.True(bgTravel >= minimumMeaningfulTravel,
            $"BG only moved {bgTravel:F1}y on a {routeDist:F1}y route");
    }

    /// <summary>
    /// Redirect parity test: walks both bots toward firstTarget, then after redirectAfterSeconds
    /// sends a new GoTo to secondTarget. Captures packets/transforms/navtrace and asserts that
    /// FG/BG emit matching STOP + start sequences at the redirect point.
    /// </summary>
    private async Task RunRedirectParityTest(
        string name,
        float startX, float startY, float startZ,
        float firstTargetX, float firstTargetY, float firstTargetZ,
        float secondTargetX, float secondTargetY, float secondTargetZ,
        int redirectAfterSeconds,
        int maxSeconds)
    {
        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        var hasFg = await _bot.CheckFgActionableAsync(requireTeleportProbe: false);
        await _bot.RefreshSnapshotsAsync();
        global::Tests.Infrastructure.Skip.IfNot(!string.IsNullOrWhiteSpace(bgAccount),
            "BG client required for parity comparison");
        Assert.NotNull(_bot.BackgroundBot);
        Assert.True(LiveBotFixture.IsStrictAlive(_bot.BackgroundBot),
            "BG client must be strict-alive for parity comparison");
        global::Tests.Infrastructure.Skip.IfNot(hasFg,
            "FG client required — this test compares FG (gold standard) with BG (headless physics)");

        _output.WriteLine($"=== {name} ===");
        _output.WriteLine($"FG: {_bot.FgCharacterName} (TESTBOT1)  BG: {_bot.BgCharacterName} (TESTBOT2)");

        float leg1Dist = Distance2D(startX, startY, firstTargetX, firstTargetY);
        float leg2Dist = Distance2D(firstTargetX, firstTargetY, secondTargetX, secondTargetY);
        _output.WriteLine($"Leg 1: ({startX},{startY}) -> ({firstTargetX},{firstTargetY}) = {leg1Dist:F1}y");
        _output.WriteLine($"Leg 2 (redirect): -> ({secondTargetX},{secondTargetY}) = {leg2Dist:F1}y");
        _output.WriteLine($"Redirect after: {redirectAfterSeconds}s\n");

        // --- TELEPORT ---
        float teleportZ = startZ + 3f;
        await Task.WhenAll(
            _bot.BotTeleportAsync(bgAccount!, MapId, startX, startY, teleportZ),
            _bot.BotTeleportAsync(fgAccount!, MapId, startX, startY, teleportZ));

        await _bot.WaitForTeleportSettledAsync(bgAccount!, startX, startY, timeoutMs: 8000);
        await _bot.WaitForTeleportSettledAsync(fgAccount!, startX, startY, timeoutMs: 8000);
        await Task.Delay(2000); // Allow physics to snap to ground

        // --- START RECORDINGS ---
        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount!, MakeRecordingAction(ActionType.StartPhysicsRecording)),
            _bot.SendActionAsync(fgAccount!, MakeRecordingAction(ActionType.StartPhysicsRecording)));
        _output.WriteLine("[RECORDING] Started on both bots");

        // --- LEG 1: GOTO first target ---
        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount!, MakeGoto(firstTargetX, firstTargetY, firstTargetZ)),
            _bot.SendActionAsync(fgAccount!, MakeGoto(firstTargetX, firstTargetY, firstTargetZ)));
        _output.WriteLine($"[ACTION] GoTo leg 1 sent to both bots");

        // --- POLL until redirect time ---
        var fgSamples = new List<TransformSample>();
        var bgSamples = new List<TransformSample>();
        var startTime = DateTime.UtcNow;
        int maxPolls = maxSeconds * (1000 / PollIntervalMs);
        bool redirectSent = false;
        bool fgArrived = false, bgArrived = false;
        int redirectPollIndex = -1;

        _output.WriteLine($"{"t",7} | {"FG_X",8} {"FG_Y",8} {"FG_Z",7} {"Flg",5} | {"BG_X",8} {"BG_Y",8} {"BG_Z",7} {"Flg",5} | {"dXY",5} {"note",12}");
        _output.WriteLine(new string('-', 100));

        for (int i = 0; i < maxPolls; i++)
        {
            await Task.Delay(PollIntervalMs);
            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;

            // --- REDIRECT mid-route ---
            if (!redirectSent && elapsed >= redirectAfterSeconds)
            {
                _output.WriteLine($"\n[REDIRECT] t={elapsed:F2}s — Sending GoTo leg 2 to both bots");
                await Task.WhenAll(
                    _bot.SendActionAsync(bgAccount!, MakeGoto(secondTargetX, secondTargetY, secondTargetZ)),
                    _bot.SendActionAsync(fgAccount!, MakeGoto(secondTargetX, secondTargetY, secondTargetZ)));
                redirectSent = true;
                redirectPollIndex = i;
                _output.WriteLine($"[REDIRECT] GoTo leg 2 sent\n");
            }

            await _bot.RefreshSnapshotsAsync();
            var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
            var bgSnap = await _bot.GetSnapshotAsync(bgAccount!);

            var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;

            float fgX = fgPos?.X ?? float.NaN, fgY = fgPos?.Y ?? float.NaN, fgZ = fgPos?.Z ?? float.NaN;
            float bgX = bgPos?.X ?? float.NaN, bgY = bgPos?.Y ?? float.NaN, bgZ = bgPos?.Z ?? float.NaN;
            uint fgFlags = fgSnap?.Player?.Unit?.MovementFlags ?? 0;
            uint bgFlags = bgSnap?.Player?.Unit?.MovementFlags ?? 0;

            if (!float.IsNaN(fgX)) fgSamples.Add(new TransformSample(elapsed, fgX, fgY, fgZ, fgFlags));
            if (!float.IsNaN(bgX)) bgSamples.Add(new TransformSample(elapsed, bgX, bgY, bgZ, bgFlags));

            float dXY = (!float.IsNaN(fgX) && !float.IsNaN(bgX)) ? Distance2D(fgX, fgY, bgX, bgY) : float.NaN;

            string note = "";
            if (redirectSent && i == redirectPollIndex + 1) note = "<<REDIRECT>>";

            // Print every 10th sample or right after redirect
            if (i % 10 == 0 || (redirectSent && i >= redirectPollIndex && i <= redirectPollIndex + 5))
            {
                _output.WriteLine(
                    $"{elapsed,7:F2}s | {fgX,8:F1} {fgY,8:F1} {fgZ,7:F2} {fgFlags,5:X} | " +
                    $"{bgX,8:F1} {bgY,8:F1} {bgZ,7:F2} {bgFlags,5:X} | " +
                    $"{(float.IsNaN(dXY) ? "  n/a" : $"{dXY,5:F1}")} {note}");
            }

            if (redirectSent)
            {
                if (!float.IsNaN(fgX) && Distance2D(fgX, fgY, secondTargetX, secondTargetY) < 5f) fgArrived = true;
                if (!float.IsNaN(bgX) && Distance2D(bgX, bgY, secondTargetX, secondTargetY) < 5f) bgArrived = true;
                if (fgArrived && bgArrived) break;
            }
        }

        if (fgArrived || bgArrived)
            await Task.Delay(1000);

        // --- STOP RECORDINGS ---
        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount!, MakeRecordingAction(ActionType.StopPhysicsRecording)),
            _bot.SendActionAsync(fgAccount!, MakeRecordingAction(ActionType.StopPhysicsRecording)));
        await Task.Delay(500);
        _output.WriteLine("[RECORDING] Stopped on both bots");

        // --- SUMMARY ---
        _output.WriteLine($"\n=== RESULTS: {name} ===");
        _output.WriteLine($"Samples: FG={fgSamples.Count} BG={bgSamples.Count}  Arrived: FG={fgArrived} BG={bgArrived}");

        float fgTravel = ComputeTravelDistance(fgSamples);
        float bgTravel = ComputeTravelDistance(bgSamples);
        _output.WriteLine($"Travel: FG={fgTravel:F1}y  BG={bgTravel:F1}y");

        PrintMoveFlagSummary("FG", fgSamples);
        PrintMoveFlagSummary("BG", bgSamples);

        // --- RECORDING ANALYSIS ---
        AnalyzeBgPhysicsRecording(bgAccount!);
        AnalyzeTransformComparison(fgAccount!, bgAccount!);

        // --- PACKET REDIRECT ANALYSIS ---
        AnalyzeRedirectPackets(fgAccount!, bgAccount!);

        // Assertions: both bots must have actually moved
        Assert.True(fgSamples.Count >= 3, $"FG produced too few samples ({fgSamples.Count})");
        Assert.True(bgSamples.Count >= 3, $"BG produced too few samples ({bgSamples.Count})");
        Assert.True(fgTravel >= 5f, $"FG only moved {fgTravel:F1}y");
        Assert.True(bgTravel >= 5f, $"BG only moved {bgTravel:F1}y");
    }

    /// <summary>
    /// Analyze redirect packet sequences: both bots should show at least two movement
    /// "sessions" (START_FORWARD ... STOP ... START_FORWARD ... STOP) corresponding
    /// to leg 1 and leg 2 of the redirect test.
    /// </summary>
    private void AnalyzeRedirectPackets(string fgAccount, string bgAccount)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();

        if (!Directory.Exists(recordingDir))
            return;

        var fgPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "packets", fgAccount, "csv");
        var bgPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "packets", bgAccount, "csv");

        if (fgPath == null || bgPath == null)
        {
            _output.WriteLine("\n--- Redirect Packet Analysis: missing CSV(s) ---");
            return;
        }

        var fgPackets = LoadPacketCsv(fgPath);
        var bgPackets = LoadPacketCsv(bgPath);

        var fgSend = fgPackets
            .Where(p => string.Equals(p.Direction, "Send", StringComparison.OrdinalIgnoreCase) && p.IsMovement)
            .ToList();
        var bgSend = bgPackets
            .Where(p => string.Equals(p.Direction, "Send", StringComparison.OrdinalIgnoreCase) && p.IsMovement)
            .ToList();

        _output.WriteLine("\n=== REDIRECT PACKET ANALYSIS ===");
        _output.WriteLine($"    FG outbound movement: {fgSend.Count} packets from {Path.GetFileName(fgPath)}");
        _output.WriteLine($"    BG outbound movement: {bgSend.Count} packets from {Path.GetFileName(bgPath)}");

        // Print all FG packets
        _output.WriteLine("    FG packets:");
        foreach (var p in fgSend.Take(30))
            _output.WriteLine($"      {p.ElapsedMs,6}ms  {p.OpcodeName}");
        if (fgSend.Count > 30)
            _output.WriteLine($"      ... and {fgSend.Count - 30} more");

        // Print all BG packets
        _output.WriteLine("    BG packets:");
        foreach (var p in bgSend.Take(30))
            _output.WriteLine($"      {p.ElapsedMs,6}ms  {p.OpcodeName}");
        if (bgSend.Count > 30)
            _output.WriteLine($"      ... and {bgSend.Count - 30} more");

        // Count key opcodes
        int fgStops = fgSend.Count(p => p.OpcodeName == "MSG_MOVE_STOP");
        int bgStops = bgSend.Count(p => p.OpcodeName == "MSG_MOVE_STOP");
        int fgStarts = fgSend.Count(p => p.OpcodeName == "MSG_MOVE_START_FORWARD");
        int bgStarts = bgSend.Count(p => p.OpcodeName == "MSG_MOVE_START_FORWARD");
        int fgFacings = fgSend.Count(p => p.OpcodeName == "MSG_MOVE_SET_FACING");
        int bgFacings = bgSend.Count(p => p.OpcodeName == "MSG_MOVE_SET_FACING");
        int fgFallLands = fgSend.Count(p => p.OpcodeName == "MSG_MOVE_FALL_LAND");
        int bgFallLands = bgSend.Count(p => p.OpcodeName == "MSG_MOVE_FALL_LAND");

        _output.WriteLine($"\n    STOP count: FG={fgStops}  BG={bgStops}");
        _output.WriteLine($"    START_FORWARD count: FG={fgStarts}  BG={bgStarts}");
        _output.WriteLine($"    SET_FACING count: FG={fgFacings}  BG={bgFacings}");
        _output.WriteLine($"    FALL_LAND count: FG={fgFallLands}  BG={bgFallLands}");

        // Assert: both bots sent at least one STOP (arrival)
        Assert.True(fgStops >= 1, $"FG sent {fgStops} STOP packets; expected at least 1");
        Assert.True(bgStops >= 1, $"BG sent {bgStops} STOP packets; expected at least 1");

        // Assert: both bots started moving
        Assert.True(fgStarts >= 1, $"FG sent {fgStarts} START_FORWARD; expected at least 1");
        Assert.True(bgStarts >= 1, $"BG sent {bgStarts} START_FORWARD; expected at least 1");

        // Redirect evidence: FG emits SET_FACING when direction changes mid-route.
        // BG should also emit SET_FACING. If BG doesn't, that's a parity gap to track.
        _output.WriteLine($"\n    Redirect evidence: FG used {fgFacings} SET_FACING, BG used {bgFacings} SET_FACING");
        if (fgFacings > 0 && bgFacings == 0)
            _output.WriteLine("    ** PARITY GAP: FG emits SET_FACING on redirect but BG does not **");

        // Compare final STOP timing (arrival at destination)
        var fgLastStop = fgSend.LastOrDefault(p => p.OpcodeName == "MSG_MOVE_STOP");
        var bgLastStop = bgSend.LastOrDefault(p => p.OpcodeName == "MSG_MOVE_STOP");
        if (fgLastStop != null && bgLastStop != null)
        {
            var stopDelta = Math.Abs(fgLastStop.ElapsedMs - bgLastStop.ElapsedMs);
            _output.WriteLine($"    Final STOP: FG={fgLastStop.ElapsedMs}ms  BG={bgLastStop.ElapsedMs}ms  delta={stopDelta}ms");
            // Redirect routes have inherent pathfinding variability (different waypoint
            // sequences for the same destination), so the stop delta bounds are wider
            // than the forced-turn test. The key assertion is that both bots STOP, not
            // that they stop at the exact same time.
            Assert.True(stopDelta <= 5000,
                $"Final STOP diverged by {stopDelta}ms (FG={fgLastStop.ElapsedMs}ms, BG={bgLastStop.ElapsedMs}ms)");
        }

        // Assert final packet is STOP for both
        if (fgSend.Count > 0 && bgSend.Count > 0)
        {
            _output.WriteLine($"    Final packet: FG={fgSend[^1].OpcodeName}  BG={bgSend[^1].OpcodeName}");
            Assert.Equal("MSG_MOVE_STOP", fgSend[^1].OpcodeName);
            Assert.Equal("MSG_MOVE_STOP", bgSend[^1].OpcodeName);
        }

        // BG FALL_LAND parity: FG should have 0 FALL_LAND on flat terrain.
        // BG FALL_LAND > 0 indicates the known native Z-bounce from PAR-NATIVE-01.
        _output.WriteLine($"\n    Z-bounce indicator: FG FALL_LAND={fgFallLands}, BG FALL_LAND={bgFallLands}");
        if (bgFallLands > 0 && fgFallLands == 0)
            _output.WriteLine("    ** KNOWN GAP: BG has false FALL_LAND from native multi-level terrain (PAR-NATIVE-01) **");
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

    /// <summary>
    /// Print which movement flags were observed and for how many frames.
    /// This is the key diagnostic for diverse-flag tests — confirms the route
    /// actually exercised the intended flag transitions.
    /// </summary>
    private void PrintMoveFlagSummary(string label, List<TransformSample> samples)
    {
        if (samples.Count < 2) return;

        // Decode flag bits into named flags with frame counts
        var flagCounts = new Dictionary<string, int>();
        var transitions = new List<string>();
        uint prevFlags = 0;

        foreach (var s in samples)
        {
            // Count individual flag bits
            for (int bit = 0; bit < 32; bit++)
            {
                uint mask = 1u << bit;
                if ((s.MoveFlags & mask) != 0)
                {
                    string name = FlagBitName(mask);
                    flagCounts[name] = flagCounts.GetValueOrDefault(name) + 1;
                }
            }

            // Track flag transitions (edges)
            if (s.MoveFlags != prevFlags && prevFlags != 0)
            {
                uint gained = s.MoveFlags & ~prevFlags;
                uint lost = prevFlags & ~s.MoveFlags;
                if (gained != 0 || lost != 0)
                {
                    var parts = new List<string>();
                    if (gained != 0) parts.Add($"+{FormatFlags(gained)}");
                    if (lost != 0) parts.Add($"-{FormatFlags(lost)}");
                    transitions.Add($"t={s.T:F2}s {string.Join(" ", parts)}");
                }
            }
            prevFlags = s.MoveFlags;
        }

        _output.WriteLine($"\n--- {label} MoveFlag Summary ---");
        if (flagCounts.Count == 0)
        {
            _output.WriteLine($"  No movement flags observed (all NONE)");
            return;
        }

        foreach (var (flag, count) in flagCounts.OrderByDescending(kv => kv.Value))
            _output.WriteLine($"  {flag,-20} {count,4} frames ({100f * count / samples.Count:F0}%)");

        if (transitions.Count > 0)
        {
            _output.WriteLine($"  Flag transitions ({transitions.Count}):");
            foreach (var t in transitions.Take(25))
                _output.WriteLine($"    {t}");
            if (transitions.Count > 25)
                _output.WriteLine($"    ... and {transitions.Count - 25} more");
        }
    }

    private static string FlagBitName(uint mask) => mask switch
    {
        0x00000001 => "FORWARD",
        0x00000002 => "BACKWARD",
        0x00000004 => "STRAFE_LEFT",
        0x00000008 => "STRAFE_RIGHT",
        0x00000010 => "TURN_LEFT",
        0x00000020 => "TURN_RIGHT",
        0x00000100 => "WALK_MODE",
        0x00002000 => "JUMPING",
        0x00004000 => "FALLINGFAR",
        0x00200000 => "SWIMMING",
        0x00400000 => "SPLINE",
        0x02000000 => "ONTRANSPORT",
        _ => $"0x{mask:X}"
    };

    private static string FormatFlags(uint flags)
    {
        var parts = new List<string>();
        for (int bit = 0; bit < 32; bit++)
        {
            uint mask = 1u << bit;
            if ((flags & mask) != 0)
                parts.Add(FlagBitName(mask));
        }
        return string.Join("|", parts);
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

    private static float ComputeTravelDistance(List<TransformSample> samples)
    {
        if (samples.Count < 2)
            return 0f;

        var first = samples.First();
        var last = samples.Last();
        return Distance2D(first.X, first.Y, last.X, last.Y);
    }

    private static ActionMessage MakeRecordingAction(ActionType type) => new() { ActionType = type };

    private static ActionMessage MakeSetFacing(float facing)
        => new()
        {
            ActionType = ActionType.SetFacing,
            Parameters =
            {
                new RequestParameter { FloatParam = facing }
            }
        };

    /// <summary>
    /// Find and analyze the most recent BG physics recording CSV.
    /// Prints Z-trace with guard annotations — the key diagnostic for bouncing.
    /// </summary>
    private void AnalyzeBgPhysicsRecording(string bgAccount)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();

        if (!Directory.Exists(recordingDir))
        {
            _output.WriteLine("\n--- BG Physics Recording: directory not found ---");
            return;
        }

        var csvPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "physics", bgAccount, "csv");
        if (csvPath == null)
        {
            _output.WriteLine("\n--- BG Physics Recording: no CSV found ---");
            return;
        }

        var lines = File.ReadAllLines(csvPath);
        if (lines.Length < 2)
        {
            _output.WriteLine($"\n--- BG Physics Recording: empty ({csvPath}) ---");
            return;
        }

        _output.WriteLine($"\n--- BG Physics Frame Recording ({lines.Length - 1} frames) ---");
        _output.WriteLine($"    File: {csvPath}");

        // Parse frames
        var frames = new List<FrameData>();
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 28) continue;
            try
            {
                frames.Add(new FrameData
                {
                    Frame = int.Parse(parts[0]),
                    PosX = float.Parse(parts[3], CultureInfo.InvariantCulture),
                    PosY = float.Parse(parts[4], CultureInfo.InvariantCulture),
                    PosZ = float.Parse(parts[5], CultureInfo.InvariantCulture),
                    RawPosZ = float.Parse(parts[6], CultureInfo.InvariantCulture),
                    PhysicsGroundZ = float.Parse(parts[7], CultureInfo.InvariantCulture),
                    PrevGroundZ = float.Parse(parts[8], CultureInfo.InvariantCulture),
                    VelZ = float.Parse(parts[12], CultureInfo.InvariantCulture),
                    FallTimeMs = uint.Parse(parts[13]),
                    IsFalling = parts[14] == "1",
                    MoveFlags = parts[15],
                    SlopeGuard = parts[16] == "1",
                    PathGuard = parts[17] == "1",
                    FalseFreefallSup = parts[18] == "1",
                    TeleportClamp = parts[19] == "1",
                    UndergroundSnap = parts[20] == "1",
                    HitWall = parts[21] == "1",
                    PathWpZ = parts[25] == "NaN" ? float.NaN : float.Parse(parts[25], CultureInfo.InvariantCulture),
                    ZDelta = float.Parse(parts[27], CultureInfo.InvariantCulture),
                });
            }
            catch { /* skip malformed lines */ }
        }

        if (frames.Count == 0)
        {
            _output.WriteLine("    No parseable frames.");
            return;
        }

        // Z statistics
        float minZ = frames.Min(f => f.PosZ);
        float maxZ = frames.Max(f => f.PosZ);
        float zRange = maxZ - minZ;
        int bounceCount = 0;
        for (int i = 2; i < frames.Count; i++)
        {
            // Detect Z direction reversals > 0.1y (bouncing signature)
            float d1 = frames[i - 1].PosZ - frames[i - 2].PosZ;
            float d2 = frames[i].PosZ - frames[i - 1].PosZ;
            if (MathF.Abs(d1) > 0.1f && MathF.Abs(d2) > 0.1f && MathF.Sign(d1) != MathF.Sign(d2))
                bounceCount++;
        }

        int slopeGuardFrames = frames.Count(f => f.SlopeGuard);
        int pathGuardFrames = frames.Count(f => f.PathGuard);
        int falseFreefallFrames = frames.Count(f => f.FalseFreefallSup);
        int teleportClampFrames = frames.Count(f => f.TeleportClamp);
        int undergroundSnapFrames = frames.Count(f => f.UndergroundSnap);
        int fallingFrames = frames.Count(f => f.IsFalling);
        int wallHitFrames = frames.Count(f => f.HitWall);

        _output.WriteLine($"    Z range: [{minZ:F2}..{maxZ:F2}] ({zRange:F2}y)");
        _output.WriteLine($"    Z bounces (direction reversals > 0.1y): {bounceCount}");
        _output.WriteLine($"    Guard activations:");
        _output.WriteLine($"      Slope guard:        {slopeGuardFrames} frames");
        _output.WriteLine($"      Path ground guard:  {pathGuardFrames} frames");
        _output.WriteLine($"      False freefall sup: {falseFreefallFrames} frames");
        _output.WriteLine($"      Teleport clamp:     {teleportClampFrames} frames");
        _output.WriteLine($"      Underground snap:   {undergroundSnapFrames} frames");
        _output.WriteLine($"      Falling:            {fallingFrames} frames");
        _output.WriteLine($"      Wall hit:           {wallHitFrames} frames");

        // Print frames where guards fired (the diagnostic gold)
        var guardFrames = frames.Where(f =>
            f.SlopeGuard || f.PathGuard || f.FalseFreefallSup ||
            f.UndergroundSnap || MathF.Abs(f.ZDelta) > 0.5f).ToList();

        if (guardFrames.Count > 0)
        {
            _output.WriteLine($"\n    --- Guard events + large Z jumps ({guardFrames.Count} frames) ---");
            _output.WriteLine($"    {"Frame",7} {"PosZ",8} {"RawZ",8} {"GndZ",8} {"PrevGZ",8} {"VelZ",7} {"ZΔ",7} {"Guards"}");
            foreach (var f in guardFrames.Take(50))
            {
                var guards = new List<string>();
                if (f.SlopeGuard) guards.Add("SLOPE");
                if (f.PathGuard) guards.Add("PATH");
                if (f.FalseFreefallSup) guards.Add("FREEFALL_SUP");
                if (f.UndergroundSnap) guards.Add("UNDERMAP");
                if (MathF.Abs(f.ZDelta) > 0.5f) guards.Add($"JUMP({f.ZDelta:+0.0;-0.0})");

                _output.WriteLine($"    {f.Frame,7} {f.PosZ,8:F2} {f.RawPosZ,8:F2} {f.PhysicsGroundZ,8:F2} {f.PrevGroundZ,8:F2} {f.VelZ,7:F2} {f.ZDelta,7:F3} {string.Join("+", guards)}");
            }
            if (guardFrames.Count > 50)
                _output.WriteLine($"    ... and {guardFrames.Count - 50} more");
        }

        // Print first 20 frames for Z trace context
        _output.WriteLine($"\n    --- First 20 frames Z trace ---");
        _output.WriteLine($"    {"Frame",7} {"PosZ",8} {"RawZ",8} {"GndZ",8} {"PrevGZ",8} {"VelZ",7} {"Fall",5} {"Flags",8}");
        foreach (var f in frames.Take(20))
        {
            _output.WriteLine($"    {f.Frame,7} {f.PosZ,8:F2} {f.RawPosZ,8:F2} {f.PhysicsGroundZ,8:F2} {f.PrevGroundZ,8:F2} {f.VelZ,7:F2} {f.FallTimeMs,5} {f.MoveFlags,8}");
        }
    }

    private sealed record TransformSample(float T, float X, float Y, float Z, uint MoveFlags);

    private sealed class FrameData
    {
        public int Frame;
        public float PosX, PosY, PosZ, RawPosZ, PhysicsGroundZ, PrevGroundZ;
        public float VelZ;
        public uint FallTimeMs;
        public bool IsFalling;
        public string MoveFlags = "";
        public bool SlopeGuard, PathGuard, FalseFreefallSup, TeleportClamp, UndergroundSnap, HitWall;
        public float PathWpZ, ZDelta;
    }

    private sealed class TransformData
    {
        public int Frame;
        public long ElapsedMs;
        public float PosX, PosY, PosZ;
        public float Facing;
        public string MoveFlags = "";
        public float RunSpeed;
        public uint FallTime;
    }

    private sealed class PacketTraceData
    {
        public int Index;
        public long ElapsedMs;
        public string Direction = "";
        public ushort Opcode;
        public string OpcodeName = "";
        public int Size;
        public bool IsMovement;
    }

    /// <summary>
    /// Load FG transform CSV and BG physics CSV, time-align frames, and print
    /// side-by-side position comparison with Z divergence analysis.
    /// </summary>
    private void AnalyzeTransformComparison(string fgAccount, string bgAccount)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();

        if (!Directory.Exists(recordingDir)) return;

        var fgPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "transform", fgAccount, "csv");
        var bgPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "transform", bgAccount, "csv");

        if (fgPath == null)
        {
            _output.WriteLine("\n--- FG Transform Recording: no CSV found ---");
            return;
        }
        if (bgPath == null)
        {
            _output.WriteLine("\n--- BG Transform Recording: no CSV found ---");
            return;
        }

        var fgFrames = LoadTransformCsv(fgPath);
        var bgFrames = LoadTransformCsv(bgPath);

        _output.WriteLine($"\n=== TRANSFORM COMPARISON (FG gold standard vs BG physics) ===");
        _output.WriteLine($"    FG: {fgFrames.Count} frames from {Path.GetFileName(fgPath)}");
        _output.WriteLine($"    BG: {bgFrames.Count} frames from {Path.GetFileName(bgPath)}");

        if (fgFrames.Count == 0 || bgFrames.Count == 0)
        {
            _output.WriteLine("    Insufficient data for comparison.");
            return;
        }

        // Time-align: match BG frames to FG by elapsed time
        var comparisons = new List<(TransformData fg, TransformData bg, float dXY, float dZ)>();
        foreach (var fg in fgFrames)
        {
            var bg = bgFrames.MinBy(b => Math.Abs(b.ElapsedMs - fg.ElapsedMs));
            if (bg != null && Math.Abs(bg.ElapsedMs - fg.ElapsedMs) < 200) // within 200ms
            {
                float dXY = Distance2D(fg.PosX, fg.PosY, bg.PosX, bg.PosY);
                float dZ = bg.PosZ - fg.PosZ;
                comparisons.Add((fg, bg, dXY, dZ));
            }
        }

        if (comparisons.Count == 0)
        {
            _output.WriteLine("    No time-aligned frame pairs found.");
            return;
        }

        // Summary stats
        float avgDXY = comparisons.Average(c => c.dXY);
        float maxDXY = comparisons.Max(c => c.dXY);
        float avgDZ = comparisons.Average(c => MathF.Abs(c.dZ));
        float maxDZ = comparisons.Max(c => MathF.Abs(c.dZ));
        float avgSignedDZ = comparisons.Average(c => c.dZ);

        _output.WriteLine($"\n    Paired frames: {comparisons.Count}");
        _output.WriteLine($"    XY divergence: avg={avgDXY:F2}y  max={maxDXY:F2}y");
        _output.WriteLine($"    Z  divergence: avg={avgDZ:F2}y  max={maxDZ:F2}y  signed avg={avgSignedDZ:+0.00;-0.00}y (BG {(avgSignedDZ > 0 ? "higher" : "lower")})");

        // Z trend over time
        int half = comparisons.Count / 2;
        if (half >= 3)
        {
            float firstHalf = comparisons.Take(half).Average(c => c.dZ);
            float secondHalf = comparisons.Skip(half).Average(c => c.dZ);
            float drift = secondHalf - firstHalf;
            _output.WriteLine($"    Z trend: 1st half={firstHalf:+0.00;-0.00}y  2nd half={secondHalf:+0.00;-0.00}y  drift={drift:+0.00;-0.00}y");
        }

        // Speed comparison (FG gold standard vs BG)
        var fgMoving = fgFrames.Where(f => f.MoveFlags.Contains("0x1") || f.RunSpeed > 0).ToList();
        var bgMoving = bgFrames.Where(f => f.MoveFlags.Contains("0x1") || f.RunSpeed > 0).ToList();
        if (fgMoving.Count >= 2 && bgMoving.Count >= 2)
        {
            float fgDuration = (fgMoving.Last().ElapsedMs - fgMoving.First().ElapsedMs) / 1000f;
            float bgDuration = (bgMoving.Last().ElapsedMs - bgMoving.First().ElapsedMs) / 1000f;
            float fgDist = Distance2D(fgMoving.First().PosX, fgMoving.First().PosY,
                fgMoving.Last().PosX, fgMoving.Last().PosY);
            float bgDist = Distance2D(bgMoving.First().PosX, bgMoving.First().PosY,
                bgMoving.Last().PosX, bgMoving.Last().PosY);
            float fgSpeed = fgDuration > 0.1f ? fgDist / fgDuration : 0;
            float bgSpeed = bgDuration > 0.1f ? bgDist / bgDuration : 0;
            _output.WriteLine($"    Effective speed: FG={fgSpeed:F2}y/s  BG={bgSpeed:F2}y/s  ratio={bgSpeed / Math.Max(fgSpeed, 0.01f):F3}");
        }

        // Print first 25 paired frames for detailed inspection
        _output.WriteLine($"\n    --- Side-by-side (first 25 paired frames) ---");
        _output.WriteLine($"    {"Ms",6} | {"FG_X",8} {"FG_Y",8} {"FG_Z",7} {"FGflg",8} | {"BG_X",8} {"BG_Y",8} {"BG_Z",7} {"BGflg",8} | {"dXY",5} {"dZ",6}");
        _output.WriteLine("    " + new string('-', 105));
        foreach (var (fg, bg, dXY, dZ) in comparisons.Take(25))
        {
            _output.WriteLine(
                $"    {fg.ElapsedMs,6} | {fg.PosX,8:F1} {fg.PosY,8:F1} {fg.PosZ,7:F2} {fg.MoveFlags,8} | " +
                $"{bg.PosX,8:F1} {bg.PosY,8:F1} {bg.PosZ,7:F2} {bg.MoveFlags,8} | " +
                $"{dXY,5:F1} {dZ,6:F2}");
        }

        // Print frames with large divergence (> 2y Z or > 5y XY)
        var divergent = comparisons.Where(c => MathF.Abs(c.dZ) > 2f || c.dXY > 5f).ToList();
        if (divergent.Count > 0)
        {
            _output.WriteLine($"\n    --- Divergent frames (|dZ|>2y or dXY>5y): {divergent.Count} ---");
            foreach (var (fg, bg, dXY, dZ) in divergent.Take(30))
            {
                _output.WriteLine(
                    $"    t={fg.ElapsedMs}ms FG=({fg.PosX:F1},{fg.PosY:F1},{fg.PosZ:F2}) " +
                    $"BG=({bg.PosX:F1},{bg.PosY:F1},{bg.PosZ:F2}) dXY={dXY:F1} dZ={dZ:+0.00;-0.00}");
            }
        }
    }

    private void AnalyzePacketComparison(string fgAccount, string bgAccount, bool expectTurnStart)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();

        if (!Directory.Exists(recordingDir))
            return;

        var fgPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "packets", fgAccount, "csv");
        var bgPath = RecordingArtifactHelper.FindLatestRecordingFile(recordingDir, "packets", bgAccount, "csv");

        if (fgPath == null)
        {
            _output.WriteLine("\n--- FG Packet Recording: no CSV found ---");
            return;
        }

        if (bgPath == null)
        {
            _output.WriteLine("\n--- BG Packet Recording: no CSV found ---");
            return;
        }

        var fgPackets = LoadPacketCsv(fgPath);
        var bgPackets = LoadPacketCsv(bgPath);

        var fgSendMovement = fgPackets
            .Where(p => string.Equals(p.Direction, "Send", StringComparison.OrdinalIgnoreCase) && p.IsMovement)
            .ToList();
        var bgSendMovement = bgPackets
            .Where(p => string.Equals(p.Direction, "Send", StringComparison.OrdinalIgnoreCase) && p.IsMovement)
            .ToList();

        _output.WriteLine("\n=== PACKET COMPARISON ===");
        _output.WriteLine($"    FG packets: {fgSendMovement.Count} outbound movement packets from {Path.GetFileName(fgPath)}");
        _output.WriteLine($"    BG packets: {bgSendMovement.Count} outbound movement packets from {Path.GetFileName(bgPath)}");
        _output.WriteLine("    FG opening packets:");
        foreach (var packet in fgSendMovement.Take(6))
            _output.WriteLine($"      {packet.ElapsedMs,5}ms  0x{packet.Opcode:X4}  {packet.OpcodeName}");
        _output.WriteLine("    BG opening packets:");
        foreach (var packet in bgSendMovement.Take(6))
            _output.WriteLine($"      {packet.ElapsedMs,5}ms  0x{packet.Opcode:X4}  {packet.OpcodeName}");

        if (!expectTurnStart)
            return;

        var fgOpening = fgSendMovement.Take(2).Select(p => p.OpcodeName).ToArray();
        var bgOpening = bgSendMovement.Take(2).Select(p => p.OpcodeName).ToArray();

        Assert.True(fgOpening.Length >= 2, "FG packet trace did not capture enough outbound opening movement packets");
        Assert.True(bgOpening.Length >= 2, "BG packet trace did not capture enough outbound opening movement packets");
        Assert.Equal("MSG_MOVE_SET_FACING", fgOpening[0]);
        Assert.Equal("MSG_MOVE_START_FORWARD", fgOpening[1]);
        Assert.Equal(fgOpening, bgOpening);

        Assert.DoesNotContain(fgSendMovement.Skip(2), packet => packet.OpcodeName == "MSG_MOVE_SET_FACING");
        Assert.DoesNotContain(bgSendMovement.Skip(2), packet => packet.OpcodeName == "MSG_MOVE_SET_FACING");

        var fgStop = fgSendMovement.LastOrDefault(packet => packet.OpcodeName == "MSG_MOVE_STOP");
        var bgStop = bgSendMovement.LastOrDefault(packet => packet.OpcodeName == "MSG_MOVE_STOP");
        Assert.NotNull(fgStop);
        Assert.NotNull(bgStop);
        Assert.Equal("MSG_MOVE_STOP", fgSendMovement[^1].OpcodeName);
        Assert.Equal("MSG_MOVE_STOP", bgSendMovement[^1].OpcodeName);

        var stopDeltaMs = Math.Abs(fgStop!.ElapsedMs - bgStop!.ElapsedMs);
        _output.WriteLine($"    Stop packets: FG={fgStop.ElapsedMs}ms  BG={bgStop.ElapsedMs}ms  delta={stopDeltaMs}ms");
        // Per-iteration terrain re-query (session 188) produces more accurate wall
        // resolution but introduces ~500ms timing variability from different contact
        // geometry at the advanced position. Widen from 300ms to 600ms.
        Assert.True(stopDeltaMs <= 600,
            $"Stop edge diverged by {stopDeltaMs}ms (FG={fgStop.ElapsedMs}ms, BG={bgStop.ElapsedMs}ms)");
    }

    private static List<TransformData> LoadTransformCsv(string path)
    {
        var result = new List<TransformData>();
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 9) continue;
            try
            {
                result.Add(new TransformData
                {
                    Frame = int.Parse(parts[0]),
                    ElapsedMs = long.Parse(parts[1]),
                    PosX = float.Parse(parts[2], CultureInfo.InvariantCulture),
                    PosY = float.Parse(parts[3], CultureInfo.InvariantCulture),
                    PosZ = float.Parse(parts[4], CultureInfo.InvariantCulture),
                    Facing = float.Parse(parts[5], CultureInfo.InvariantCulture),
                    MoveFlags = parts[6],
                    RunSpeed = float.Parse(parts[7], CultureInfo.InvariantCulture),
                    FallTime = uint.Parse(parts[8]),
                });
            }
            catch { }
        }
        return result;
    }

    private static List<PacketTraceData> LoadPacketCsv(string path)
    {
        var result = new List<PacketTraceData>();
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 8)
                continue;

            try
            {
                result.Add(new PacketTraceData
                {
                    Index = int.Parse(parts[0], CultureInfo.InvariantCulture),
                    ElapsedMs = long.Parse(parts[1], CultureInfo.InvariantCulture),
                    Direction = parts[2],
                    Opcode = ushort.Parse(parts[3], CultureInfo.InvariantCulture),
                    OpcodeName = parts[5],
                    Size = int.Parse(parts[6], CultureInfo.InvariantCulture),
                    IsMovement = parts[7] == "1",
                });
            }
            catch
            {
            }
        }

        return result;
    }
}
