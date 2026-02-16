using Navigation.Physics.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;

namespace Navigation.Physics.Tests.Diagnostics;

/// <summary>
/// Diagnostic tests that analyze per-frame error patterns across all recordings.
/// These are not pass/fail tests — they dump detailed analysis to help identify
/// systematic error sources in the physics engine.
/// </summary>
[Collection("PhysicsEngine")]
public class ErrorPatternDiagnosticTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void DiagnoseAllRecordings_DumpErrorPatterns()
    {
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Physics engine not initialized"); return; }

        var recordings = new (string name, string file)[]
        {
            ("FlatRunForward", Recordings.OrgFlatRunForward),
            ("LongFlatRun", Recordings.DurotarLongFlatRun),
            ("StandingJump", Recordings.OrgStandingJump),
            ("RunningJumps", Recordings.OrgRunningJumps),
            ("FallFromHeight", Recordings.OrgFallFromHeight),
            ("ComplexMixed", Recordings.DurotarMixedMovement),
            ("SwimForward", Recordings.Swimming),
            ("ElevatorRide", Recordings.UndercityElevator),
        };

        var allFramesByMode = new Dictionary<string, List<CalibrationResult.FrameDetail>>();

        foreach (var (name, file) in recordings)
        {
            var result = _fixture.ReplayCache.GetOrReplay(file, _output, _fixture.IsInitialized);
            if (result.FrameCount == 0) continue;

            _output.WriteLine($"\n{'='} {name} ({result.FrameCount} frames) {'='}");
            LogCalibrationResult(name, result, _output);

            // Group by movement mode
            var byMode = result.FrameDetails.GroupBy(f => f.MovementMode).OrderBy(g => g.Key);
            foreach (var group in byMode)
            {
                var frames = group.ToList();
                float avg = frames.Average(f => f.PosError);
                float p50 = Percentile(frames.Select(f => f.PosError).ToList(), 50);
                float p95 = Percentile(frames.Select(f => f.PosError).ToList(), 95);
                float p99 = Percentile(frames.Select(f => f.PosError).ToList(), 99);
                float avgH = frames.Average(f => f.HorizError);
                float avgV = frames.Average(f => f.VertError);

                _output.WriteLine($"  [{group.Key}] n={frames.Count}  avg={avg:F4}y  P50={p50:F4}  P95={p95:F4}  P99={p99:F4}  avgH={avgH:F4}  avgV={avgV:F4}");

                if (!allFramesByMode.ContainsKey(group.Key))
                    allFramesByMode[group.Key] = [];
                allFramesByMode[group.Key].AddRange(frames);
            }

            // Dump worst 10 frames with full context
            var worst = result.FrameDetails.OrderByDescending(f => f.PosError).Take(10).ToList();
            _output.WriteLine($"  --- Worst 10 frames ---");
            foreach (var f in worst)
            {
                string flags = FormatMoveFlags(f.MoveFlags);
                string nextFlags = f.IsFlagTransition ? $" -> {FormatMoveFlags(f.NextMoveFlags)}" : "";
                _output.WriteLine($"    F{f.Frame:D4} err={f.PosError:F4}y  " +
                    $"dX={f.ErrorX:F4} dY={f.ErrorY:F4} dZ={f.ErrorZ:F4}  " +
                    $"dt={f.Dt * 1000:F1}ms  mode={f.MovementMode}  " +
                    $"flags={flags}{nextFlags}  " +
                    $"speed={f.RecordedSpeed:F2}  orient={f.Orientation:F3}  " +
                    $"gndZ={f.EngineGroundZ:F2}");
            }

            // Analyze error vs dt correlation
            AnalyzeDtCorrelation(name, result.FrameDetails, _output);

            // Analyze transition frame errors
            AnalyzeTransitions(name, result.FrameDetails, _output);

            // Analyze error direction bias
            AnalyzeErrorBias(name, result.FrameDetails, _output);
        }

        // Cross-recording mode summary
        _output.WriteLine("\n=== CROSS-RECORDING MODE SUMMARY ===");
        foreach (var (mode, frames) in allFramesByMode.OrderBy(kv => kv.Key))
        {
            float avg = frames.Average(f => f.PosError);
            float p50 = Percentile(frames.Select(f => f.PosError).ToList(), 50);
            float p95 = Percentile(frames.Select(f => f.PosError).ToList(), 95);
            float p99 = Percentile(frames.Select(f => f.PosError).ToList(), 99);
            float max = frames.Max(f => f.PosError);
            float avgH = frames.Average(f => f.HorizError);
            float avgV = frames.Average(f => f.VertError);
            _output.WriteLine($"  [{mode}] n={frames.Count}  avg={avg:F4}  P50={p50:F4}  P95={p95:F4}  P99={p99:F4}  max={max:F4}  H={avgH:F4}  V={avgV:F4}");
        }

        // Ground frames: analyze error vs slope (ground normal Z)
        if (allFramesByMode.TryGetValue("ground", out var groundFrames))
        {
            _output.WriteLine("\n=== GROUND: ERROR vs SLOPE ===");
            // Bucket by ground Z normal
            var buckets = new (string label, float minNz, float maxNz)[]
            {
                ("flat (nz>0.99)", 0.99f, 1.01f),
                ("gentle (0.95<nz<0.99)", 0.95f, 0.99f),
                ("moderate (0.85<nz<0.95)", 0.85f, 0.95f),
                ("steep (0.70<nz<0.85)", 0.70f, 0.85f),
            };

            // For ground frames, ground normal comes from engine output
            // We can't directly access it from FrameDetail, but we can infer from
            // the recording Z delta whether the character is on a slope
            var slopeFrames = groundFrames.Where(f => f.Dt > 0 && f.RecordedSpeed > 0.5f).ToList();
            if (slopeFrames.Count > 0)
            {
                // Use Z change rate as slope proxy
                var flat = slopeFrames.Where(f => MathF.Abs(f.RecZ - f.SimZ) < 0.01f && MathF.Abs(f.ErrorZ) < 0.05f).ToList();
                var sloped = slopeFrames.Except(flat).ToList();

                _output.WriteLine($"  Flat-ish ground: n={flat.Count}  avg={SafeAvg(flat):F4}  P95={SafeP95(flat):F4}");
                _output.WriteLine($"  Sloped ground:   n={sloped.Count}  avg={SafeAvg(sloped):F4}  P95={SafeP95(sloped):F4}");
            }
        }

        // Air frames: analyze horizontal vs vertical error ratio
        if (allFramesByMode.TryGetValue("air", out var airFrames))
        {
            _output.WriteLine("\n=== AIR: HORIZONTAL vs VERTICAL ERROR ===");
            var rising = airFrames.Where(f => f.EngineVz > 0).ToList();
            var falling = airFrames.Where(f => f.EngineVz <= 0).ToList();
            _output.WriteLine($"  Rising (vz>0):  n={rising.Count}  avgH={SafeAvgH(rising):F4}  avgV={SafeAvgV(rising):F4}");
            _output.WriteLine($"  Falling (vz<=0): n={falling.Count}  avgH={SafeAvgH(falling):F4}  avgV={SafeAvgV(falling):F4}");

            // Check if error grows over time during continuous air sequences
            _output.WriteLine("  Error drift during continuous air:");
            var continuousAir = new List<CalibrationResult.FrameDetail>();
            foreach (var f in airFrames.OrderBy(f => f.Frame))
            {
                if (continuousAir.Count > 0 && f.Frame != continuousAir[^1].Frame + 1)
                {
                    if (continuousAir.Count >= 5)
                        DumpAirSequence(continuousAir, _output);
                    continuousAir.Clear();
                }
                continuousAir.Add(f);
            }
            if (continuousAir.Count >= 5)
                DumpAirSequence(continuousAir, _output);
        }

        // Transition frames: error by transition type
        var transitions = allFramesByMode.Values.SelectMany(f => f).Where(f => f.IsFlagTransition).ToList();
        if (transitions.Count > 0)
        {
            _output.WriteLine("\n=== TRANSITION FRAMES BY TYPE ===");
            var byType = transitions.GroupBy(f => $"{FormatMoveFlags(f.MoveFlags)} -> {FormatMoveFlags(f.NextMoveFlags)}");
            foreach (var group in byType.OrderByDescending(g => g.Average(f => f.PosError)).Take(15))
            {
                var frames = group.ToList();
                _output.WriteLine($"  {group.Key}: n={frames.Count}  avg={frames.Average(f => f.PosError):F4}  max={frames.Max(f => f.PosError):F4}");
            }
        }
    }

    private static void AnalyzeDtCorrelation(string name, List<CalibrationResult.FrameDetail> frames, ITestOutputHelper output)
    {
        if (frames.Count < 10) return;

        // Bucket by dt
        var shortDt = frames.Where(f => f.Dt < 0.017f).ToList();    // < 17ms
        var normalDt = frames.Where(f => f.Dt >= 0.017f && f.Dt <= 0.020f).ToList(); // 17-20ms
        var longDt = frames.Where(f => f.Dt > 0.020f).ToList();     // > 20ms

        output.WriteLine($"  --- dt correlation ---");
        if (shortDt.Count > 0) output.WriteLine($"    dt<17ms:  n={shortDt.Count}  avg={shortDt.Average(f => f.PosError):F4}");
        if (normalDt.Count > 0) output.WriteLine($"    17-20ms:  n={normalDt.Count}  avg={normalDt.Average(f => f.PosError):F4}");
        if (longDt.Count > 0) output.WriteLine($"    dt>20ms:  n={longDt.Count}  avg={longDt.Average(f => f.PosError):F4}");
    }

    private static void AnalyzeTransitions(string name, List<CalibrationResult.FrameDetail> frames, ITestOutputHelper output)
    {
        var transitions = frames.Where(f => f.IsFlagTransition).ToList();
        var steady = frames.Where(f => !f.IsFlagTransition).ToList();

        if (transitions.Count == 0) return;

        output.WriteLine($"  --- transitions vs steady-state ---");
        output.WriteLine($"    Transition: n={transitions.Count}  avg={transitions.Average(f => f.PosError):F4}  max={transitions.Max(f => f.PosError):F4}");
        output.WriteLine($"    Steady:     n={steady.Count}  avg={steady.Average(f => f.PosError):F4}  max={steady.Max(f => f.PosError):F4}");
    }

    private static void AnalyzeErrorBias(string name, List<CalibrationResult.FrameDetail> frames, ITestOutputHelper output)
    {
        if (frames.Count < 10) return;

        float avgErrX = frames.Average(f => f.ErrorX);
        float avgErrY = frames.Average(f => f.ErrorY);
        float avgErrZ = frames.Average(f => f.ErrorZ);

        output.WriteLine($"  --- error bias (mean signed error) ---");
        output.WriteLine($"    meanX={avgErrX:F5}  meanY={avgErrY:F5}  meanZ={avgErrZ:F5}");
    }

    private static void DumpAirSequence(List<CalibrationResult.FrameDetail> seq, ITestOutputHelper output)
    {
        float firstErr = seq[0].PosError;
        float lastErr = seq[^1].PosError;
        float avgErr = seq.Average(f => f.PosError);
        output.WriteLine($"    air seq F{seq[0].Frame}-F{seq[^1].Frame} ({seq.Count} frames): " +
            $"first={firstErr:F4} last={lastErr:F4} avg={avgErr:F4} " +
            $"avgH={seq.Average(f => f.HorizError):F4} avgV={seq.Average(f => f.VertError):F4}");
    }

    private static string FormatMoveFlags(uint flags)
    {
        var parts = new List<string>();
        if ((flags & 0x01) != 0) parts.Add("FWD");
        if ((flags & 0x02) != 0) parts.Add("BWD");
        if ((flags & 0x04) != 0) parts.Add("SL");
        if ((flags & 0x08) != 0) parts.Add("SR");
        if ((flags & 0x10) != 0) parts.Add("TL");
        if ((flags & 0x20) != 0) parts.Add("TR");
        if ((flags & 0x100) != 0) parts.Add("WALK");
        if ((flags & 0x2000) != 0) parts.Add("JUMP");
        if ((flags & 0x4000) != 0) parts.Add("FALL");
        if ((flags & 0x200000) != 0) parts.Add("SWIM");
        if ((flags & 0x4000000) != 0) parts.Add("SPLINE_ELEV");
        if (parts.Count == 0) parts.Add("IDLE");
        return string.Join("|", parts);
    }

    private static float Percentile(List<float> values, int pct)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int idx = Math.Clamp((int)(sorted.Count * pct / 100.0), 0, sorted.Count - 1);
        return sorted[idx];
    }

    private static float SafeAvg(List<CalibrationResult.FrameDetail> frames) =>
        frames.Count > 0 ? frames.Average(f => f.PosError) : 0;

    private static float SafeP95(List<CalibrationResult.FrameDetail> frames) =>
        frames.Count > 0 ? Percentile(frames.Select(f => f.PosError).ToList(), 95) : 0;

    private static float SafeAvgH(List<CalibrationResult.FrameDetail> frames) =>
        frames.Count > 0 ? frames.Average(f => f.HorizError) : 0;

    private static float SafeAvgV(List<CalibrationResult.FrameDetail> frames) =>
        frames.Count > 0 ? frames.Average(f => f.VertError) : 0;
}
