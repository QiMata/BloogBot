using System;
using System.Collections.Generic;
using System.Linq;
using Navigation.Physics.Tests.Helpers;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;

namespace Navigation.Physics.Tests;

/// <summary>
/// Frame-by-frame replay tests that feed each recorded frame through the C++ PhysicsEngine
/// (StepV2) and compare the engine's predicted next position against the actual recorded
/// next position.
///
/// Teleport frames (>50y position jump) are automatically skipped by the ReplayEngine.
///
/// These require Navigation.dll + map data. If not available, tests skip gracefully.
/// </summary>
[Collection("PhysicsEngine")]
public class PhysicsReplayTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    // ==========================================================================
    // GROUND MOVEMENT
    // ==========================================================================

    [Fact]
    public void FlatRunForward_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgFlatRunForward);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    [Fact]
    public void LongFlatRun_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.DurotarLongFlatRun);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    // ==========================================================================
    // JUMPS AND AIRBORNE
    // ==========================================================================

    [Fact]
    public void StandingJump_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgStandingJump);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    [Fact]
    public void RunningJumps_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgRunningJumps);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    // ==========================================================================
    // FREE-FALL
    // ==========================================================================

    [Fact]
    public void FallFromHeight_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgFallFromHeight);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    // ==========================================================================
    // MIXED MOVEMENT
    // ==========================================================================

    [Fact]
    public void ComplexMixed_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.DurotarMixedMovement);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    // UndercityMixed removed: recording from 2026-02-08 has no NearbyGameObjects data.
    // The Undercity floor at Z=55.2 IS the elevator door GO — without GO data, there's no floor.
    // Use ElevatorRide recording (2026-02-12) which captures doors and elevators properly.

    // ==========================================================================
    // TRANSPORT / ELEVATOR
    // ==========================================================================

    [Fact]
    public void ElevatorRide_FrameByFrame_PositionMatchesRecording()
    {
        var result = _fixture.ReplayCache.GetOrReplay(Recordings.UndercityElevator, _output, _fixture.IsInitialized);
        if (result.FrameCount == 0) return;

        LogCalibrationResult("elevator_ride", result, _output);

        int transportFrames = result.FrameDetails.Count(f => f.IsOnTransport);
        _output.WriteLine($"  Transport: {result.TransportTransitionCount} transitions, " +
            $"{transportFrames} on-transport frames simulated, " +
            $"{result.TransportFrameCount} transport frames skipped");

        AssertPrecision(result, Tolerances.TransportAvg, Tolerances.TransportP99);
    }

    [Fact]
    public void ElevatorRideV2_FrameByFrame_PositionMatchesRecording()
    {
        var result = _fixture.ReplayCache.GetOrReplay(Recordings.UndercityElevatorV2, _output, _fixture.IsInitialized);
        if (result.FrameCount == 0) return;

        LogCalibrationResult("elevator_ride_v2", result, _output);

        int transportFrames = result.FrameDetails.Count(f => f.IsOnTransport);
        _output.WriteLine($"  Transport: {result.TransportTransitionCount} transitions, " +
            $"{transportFrames} on-transport frames simulated, " +
            $"{result.TransportFrameCount} transport frames skipped");

        AssertPrecision(result, Tolerances.TransportAvg, Tolerances.TransportP99);
    }

    // ==========================================================================
    // SWIMMING
    // ==========================================================================

    [Fact]
    public void SwimForward_FrameByFrame_PositionMatchesRecording()
    {
        var result = _fixture.ReplayCache.GetOrReplay(Recordings.Swimming, _output, _fixture.IsInitialized);
        if (result.FrameCount == 0) return;

        LogCalibrationResult("swim_forward", result, _output);
        AssertPrecision(result, Tolerances.AvgPosition, Tolerances.P99Position);
    }

    // ==========================================================================
    // AGGREGATE DRIFT GATES (NPT-MISS-003)
    // ==========================================================================

    /// <summary>
    /// NPT-MISS-003: Hard regression gate across ALL recordings.
    /// Computes aggregate clean-frame metrics and fails if any threshold is exceeded.
    /// Excludes recording artifacts and SPLINE_ELEVATION transitions.
    /// Reports top offenders with recording name, frame index, and XYZ error vector.
    /// </summary>
    [Fact]
    public void AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings found or engine not initialized"); return; }

        // Collect clean frames from all recordings (exclude artifacts + SPLINE_ELEVATION)
        var allClean = new List<CalibrationResult.FrameDetail>();
        foreach (var (name, _, result) in allResults)
        {
            var clean = result.CleanFrames;
            _output.WriteLine($"  {name}: {clean.Count} clean / {result.FrameCount} total " +
                $"(artifacts={result.ArtifactCount}, splineElev={result.SplineElevationCount})");
            allClean.AddRange(clean);
        }

        Assert.True(allClean.Count > 0, "No clean frames across all recordings — check data availability");

        // Compute aggregate metrics
        var sorted = allClean.Select(f => f.PosError).OrderBy(e => e).ToList();
        float avgError = allClean.Average(f => f.PosError);
        int p99Idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
        float p99Error = sorted[p99Idx];
        float worstError = sorted[^1];

        _output.WriteLine($"\n=== AGGREGATE CLEAN METRICS (n={allClean.Count}) ===");
        _output.WriteLine($"  avg={avgError:F4}y  p99={p99Error:F4}y  worst={worstError:F4}y");
        _output.WriteLine($"  Thresholds: avg<{Tolerances.AggregateCleanAvg}  p99<{Tolerances.AggregateCleanP99}  worst<{Tolerances.WorstCleanFrame}");

        // Report top 10 worst clean frames for triage
        var top10 = allClean.OrderByDescending(f => f.PosError).Take(10).ToList();
        _output.WriteLine($"\n  Top 10 worst clean frames:");
        foreach (var f in top10)
        {
            _output.WriteLine(
                $"    [{f.RecordingName}] frame={f.Frame,5} err={f.PosError:F3}y " +
                $"dX={f.ErrorX:+0.000;-0.000} dY={f.ErrorY:+0.000;-0.000} dZ={f.ErrorZ:+0.000;-0.000} " +
                $"mode={f.MovementMode,-10} flags=0x{f.MoveFlags:X8}");
        }

        // Hard assertions — fail the build on drift regression
        Assert.True(avgError < Tolerances.AggregateCleanAvg,
            $"Aggregate clean-frame avg error {avgError:F4}y exceeds {Tolerances.AggregateCleanAvg}y threshold. " +
            $"Worst: [{top10[0].RecordingName}] frame {top10[0].Frame} err={top10[0].PosError:F3}y");

        Assert.True(p99Error < Tolerances.AggregateCleanP99,
            $"Aggregate clean-frame P99 error {p99Error:F4}y exceeds {Tolerances.AggregateCleanP99}y threshold. " +
            $"Worst: [{top10[0].RecordingName}] frame {top10[0].Frame} err={top10[0].PosError:F3}y");

        Assert.True(worstError < Tolerances.WorstCleanFrame,
            $"Worst clean frame error {worstError:F4}y exceeds {Tolerances.WorstCleanFrame}y threshold. " +
            $"Frame: [{top10[0].RecordingName}] frame {top10[0].Frame} " +
            $"sim=({top10[0].SimX:F3},{top10[0].SimY:F3},{top10[0].SimZ:F3}) " +
            $"rec=({top10[0].RecX:F3},{top10[0].RecY:F3},{top10[0].RecZ:F3})");
    }

    // ==========================================================================
    // TRANSITION ANALYSIS
    // ==========================================================================

    [Fact]
    public void TransitionAnalysis_AllRecordings_ReportsPerTransitionErrors()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings found or engine not initialized"); return; }

        // Aggregate transition stats across all recordings
        var allFrames = new List<CalibrationResult.FrameDetail>();

        foreach (var (name, _, result) in allResults)
        {
            LogCalibrationResult(name, result, _output);
            allFrames.AddRange(result.FrameDetails);
            _output.WriteLine("");
        }

        // Print aggregate transition stats
        _output.WriteLine("=== AGGREGATE TRANSITION ANALYSIS ===");
        _output.WriteLine($"Total frames analyzed: {allFrames.Count}");

        var transitionTypes = new[]
        {
            TransitionType.None,
            TransitionType.JumpStart,
            TransitionType.Landing,
            TransitionType.WaterEntry,
            TransitionType.WaterExit,
            TransitionType.DirectionChange,
            TransitionType.SpeedChange,
            TransitionType.Other,
        };

        foreach (var tt in transitionTypes)
        {
            var frames = allFrames.Where(f => f.Transition == tt).ToList();
            if (frames.Count == 0) continue;

            float avg = frames.Average(f => f.PosError);
            float max = frames.Max(f => f.PosError);
            var sorted = frames.Select(f => f.PosError).OrderBy(e => e).ToList();
            int p99Idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
            int p95Idx = Math.Clamp((int)(sorted.Count * 95 / 100.0), 0, sorted.Count - 1);
            _output.WriteLine($"  [{tt,-16}] n={frames.Count,5}  avg={avg:F4}y  p95={sorted[p95Idx]:F4}y  p99={sorted[p99Idx]:F4}y  max={max:F4}y");
        }

        // Aggregate mode breakdown
        _output.WriteLine("  --- Movement Mode Aggregate ---");
        var modeGroups = allFrames.GroupBy(f => f.MovementMode).OrderByDescending(g => g.Count());
        foreach (var group in modeGroups)
        {
            var frames = group.ToList();
            float avg = frames.Average(f => f.PosError);
            float max = frames.Max(f => f.PosError);
            var sorted = frames.Select(f => f.PosError).OrderBy(e => e).ToList();
            int p99Idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
            _output.WriteLine($"  [{group.Key,-12}] n={frames.Count,5}  avg={avg:F4}y  p99={sorted[p99Idx]:F4}y  max={max:F4}y");
        }

        // Summary of artifacts
        int totalArtifacts = allFrames.Count(f => f.IsRecordingArtifact);
        int totalSplineElev = allFrames.Count(f => f.IsSplineElevationTransition);
        _output.WriteLine($"\n  Recording artifacts: {totalArtifacts} Z-spike frames, {totalSplineElev} SPLINE_ELEVATION transitions");
        var cleanFrames = allFrames.Where(f => !f.IsRecordingArtifact && !f.IsSplineElevationTransition).ToList();
        if (cleanFrames.Count > 0)
        {
            var sorted = cleanFrames.Select(f => f.PosError).OrderBy(e => e).ToList();
            int p99Idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
            int p95Idx = Math.Clamp((int)(sorted.Count * 95 / 100.0), 0, sorted.Count - 1);
            _output.WriteLine($"  Clean metrics (excl artifacts): n={cleanFrames.Count}  avg={cleanFrames.Average(f => f.PosError):F4}y  " +
                $"p95={sorted[p95Idx]:F4}y  p99={sorted[p99Idx]:F4}y  max={sorted.Last():F4}y");
            // Clean mode breakdown
            _output.WriteLine("  --- Clean Movement Mode Breakdown ---");
            foreach (var group in cleanFrames.GroupBy(f => f.MovementMode).OrderByDescending(g => g.Count()))
            {
                var mframes = group.ToList();
                var msorted = mframes.Select(f => f.PosError).OrderBy(e => e).ToList();
                int mp99 = Math.Clamp((int)(msorted.Count * 99 / 100.0), 0, msorted.Count - 1);
                _output.WriteLine($"    [{group.Key,-12}] n={mframes.Count,5}  avg={mframes.Average(f => f.PosError):F4}y  p99={msorted[mp99]:F4}y  max={msorted.Last():F4}y");
            }
        }

        // Top 20 worst frames across all recordings
        _output.WriteLine("\n  === TOP 20 WORST FRAMES ===");
        var worstFrames = allFrames.OrderByDescending(f => f.PosError).Take(20).ToList();
        foreach (var f in worstFrames)
        {
            string artifacts = "";
            if (f.IsRecordingArtifact) artifacts += " [Z-SPIKE]";
            if (f.IsSplineElevationTransition) artifacts += " [SPLINE_ELEV]";
            _output.WriteLine($"  [{f.RecordingName}] frame={f.Frame,5} err={f.PosError:F3}y hErr={f.HorizError:F3}y vErr={f.VertError:F3}y " +
                $"mode={f.MovementMode,-10} trans={f.Transition,-16}{artifacts}");
            _output.WriteLine($"    raw=0x{f.RawMoveFlags:X8}→0x{f.RawNextMoveFlags:X8}  cleaned=0x{f.MoveFlags:X8}→0x{f.NextMoveFlags:X8}  " +
                $"dt={f.Dt:F4}  groundZ={f.EngineGroundZ:F3}");
            _output.WriteLine($"    sim=({f.SimX:F3},{f.SimY:F3},{f.SimZ:F3}) rec=({f.RecX:F3},{f.RecY:F3},{f.RecZ:F3}) " +
                $"dX={f.ErrorX:F3} dY={f.ErrorY:F3} dZ={f.ErrorZ:F3}");
        }

        // Top 10 worst CLEAN frames (no artifacts)
        _output.WriteLine("\n  === TOP 10 WORST CLEAN FRAMES (no artifacts) ===");
        var worstClean = cleanFrames.OrderByDescending(f => f.PosError).Take(10).ToList();
        foreach (var f in worstClean)
        {
            _output.WriteLine($"  [{f.RecordingName}] frame={f.Frame,5} err={f.PosError:F3}y hErr={f.HorizError:F3}y vErr={f.VertError:F3}y " +
                $"mode={f.MovementMode,-10} trans={f.Transition,-16}");
            _output.WriteLine($"    raw=0x{f.RawMoveFlags:X8}→0x{f.RawNextMoveFlags:X8}  dt={f.Dt:F4}  groundZ={f.EngineGroundZ:F3}");
            _output.WriteLine($"    sim=({f.SimX:F3},{f.SimY:F3},{f.SimZ:F3}) rec=({f.RecX:F3},{f.RecY:F3},{f.RecZ:F3}) " +
                $"dX={f.ErrorX:F3} dY={f.ErrorY:F3} dZ={f.ErrorZ:F3}");
        }

        // Top 10 worst per mode (non-transport)
        foreach (var mode in new[] { "ground", "air", "swim", "transition" })
        {
            var modeFrames = cleanFrames.Where(f => f.MovementMode == mode).OrderByDescending(f => f.PosError).Take(10).ToList();
            if (modeFrames.Count == 0) continue;
            _output.WriteLine($"\n  === TOP 10 WORST CLEAN {mode.ToUpper()} FRAMES ===");
            foreach (var f in modeFrames)
            {
                _output.WriteLine($"  [{f.RecordingName}] frame={f.Frame,5} err={f.PosError:F3}y hErr={f.HorizError:F3}y vErr={f.VertError:F3}y " +
                    $"trans={f.Transition,-16}");
                _output.WriteLine($"    raw=0x{f.RawMoveFlags:X8}→0x{f.RawNextMoveFlags:X8}  dt={f.Dt:F4}  groundZ={f.EngineGroundZ:F3}");
                _output.WriteLine($"    sim=({f.SimX:F3},{f.SimY:F3},{f.SimZ:F3}) rec=({f.RecX:F3},{f.RecY:F3},{f.RecZ:F3}) " +
                    $"dX={f.ErrorX:F3} dY={f.ErrorY:F3} dZ={f.ErrorZ:F3}");
            }
        }

        // Verify landing frames are within tolerance
        var landingFrames = allFrames.Where(f => f.Transition == TransitionType.Landing).ToList();
        if (landingFrames.Count > 0)
        {
            float landingAvg = landingFrames.Average(f => f.PosError);
            _output.WriteLine($"\n  Landing assertion: avg={landingAvg:F4}y (tolerance: {Tolerances.P99Position}y)");
            Assert.True(landingAvg < Tolerances.P99Position,
                $"Landing transition avg error {landingAvg:F4}y exceeds {Tolerances.P99Position}y tolerance");
        }
    }

    // ==========================================================================
    // STEP TRANSITION ANALYSIS
    // ==========================================================================

    [Fact]
    public void StepTransitionAnalysis_IdentifiesStepUpDownErrors()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings found or engine not initialized"); return; }

        var stepEvents = new List<StepEvent>();
        var groundErrors = new List<(float pos, float horiz, float vert)>();

        foreach (var (name, recording, result) in allResults)
        {

            for (int i = 0; i < result.FrameDetails.Count; i++)
            {
                var fd = result.FrameDetails[i];
                if (fd.MovementMode != "ground") continue;
                groundErrors.Add((fd.PosError, fd.HorizError, fd.VertError));

                // Find step transitions: Z changes > 0.3y between consecutive ground frames
                if (i > 0 && fd.Frame > 0 && fd.Frame < recording.Frames.Count - 1)
                {
                    var prevFd = result.FrameDetails[i - 1];
                    if (prevFd.MovementMode != "ground") continue;

                    float recDz = fd.RecZ - prevFd.RecZ;
                    if (MathF.Abs(recDz) > 0.3f)
                    {
                        stepEvents.Add(new StepEvent
                        {
                            RecordingName = name,
                            Frame = fd.Frame,
                            RecDz = recDz,
                            SimDz = fd.SimZ - prevFd.SimZ,
                            Error = fd.PosError,
                            VertError = fd.VertError,
                            RecZ = fd.RecZ,
                            SimZ = fd.SimZ,
                            IsArtifact = fd.IsRecordingArtifact || fd.IsSplineElevationTransition,
                            Flags = fd.MoveFlags,
                        });
                    }
                }
            }
        }

        // Report step events
        _output.WriteLine($"\n=== STEP TRANSITION ANALYSIS ===");
        _output.WriteLine($"Total ground frames: {groundErrors.Count}");
        _output.WriteLine($"Step transitions (|dZ|>0.3y): {stepEvents.Count}");

        var cleanSteps = stepEvents.Where(s => !s.IsArtifact).ToList();
        var ups = cleanSteps.Where(s => s.RecDz > 0).OrderByDescending(s => s.Error).ToList();
        var downs = cleanSteps.Where(s => s.RecDz < 0).OrderByDescending(s => s.Error).ToList();

        _output.WriteLine($"  Step-ups (clean): {ups.Count}  avg err={SafeAvg(ups.Select(s => s.Error)):F4}y  max={SafeMax(ups.Select(s => s.Error)):F4}y");
        _output.WriteLine($"  Step-downs (clean): {downs.Count}  avg err={SafeAvg(downs.Select(s => s.Error)):F4}y  max={SafeMax(downs.Select(s => s.Error)):F4}y");

        _output.WriteLine($"\n--- Top 15 worst step-up errors ---");
        foreach (var s in ups.Take(15))
        {
            _output.WriteLine($"  [{s.RecordingName}] frame={s.Frame} recDz={s.RecDz:+0.000;-0.000} simDz={s.SimDz:+0.000;-0.000} " +
                $"err={s.Error:F3}y vErr={s.VertError:F3}y recZ={s.RecZ:F3} simZ={s.SimZ:F3} flags=0x{s.Flags:X8}");
        }

        _output.WriteLine($"\n--- Top 15 worst step-down errors ---");
        foreach (var s in downs.Take(15))
        {
            _output.WriteLine($"  [{s.RecordingName}] frame={s.Frame} recDz={s.RecDz:+0.000;-0.000} simDz={s.SimDz:+0.000;-0.000} " +
                $"err={s.Error:F3}y vErr={s.VertError:F3}y recZ={s.RecZ:F3} simZ={s.SimZ:F3} flags=0x{s.Flags:X8}");
        }

        // Ground error distribution analysis (investigate 0.1191y pattern)
        _output.WriteLine($"\n=== GROUND ERROR DISTRIBUTION ===");
        if (groundErrors.Count > 0)
        {
            var posErrors = groundErrors.Select(e => e.pos).OrderBy(e => e).ToList();
            _output.WriteLine($"  n={posErrors.Count}  avg={posErrors.Average():F4}  median={posErrors[posErrors.Count / 2]:F4}");
            _output.WriteLine($"  Horiz: avg={groundErrors.Average(e => e.horiz):F4}  Vert: avg={groundErrors.Average(e => e.vert):F4}");

            // Histogram of error buckets
            float[] buckets = [0.001f, 0.005f, 0.01f, 0.02f, 0.05f, 0.1f, 0.115f, 0.12f, 0.125f, 0.15f, 0.2f, 0.5f, 1.0f, 5.0f];
            int prev = 0;
            foreach (float b in buckets)
            {
                int count = posErrors.Count(e => e <= b);
                int inBucket = count - prev;
                float pct = 100.0f * count / posErrors.Count;
                _output.WriteLine($"  <= {b:F3}y: {count,6} ({pct:F1}%)  [+{inBucket} in bucket]");
                prev = count;
            }

            // Horiz vs Vert breakdown for the 0.100-0.125y error band
            var midBand = groundErrors.Where(e => e.pos >= 0.100f && e.pos <= 0.125f).ToList();
            if (midBand.Count > 0)
            {
                _output.WriteLine($"\n--- 0.100-0.125y error band breakdown ({midBand.Count} frames) ---");
                _output.WriteLine($"  Horiz: avg={midBand.Average(e => e.horiz):F4}  max={midBand.Max(e => e.horiz):F4}");
                _output.WriteLine($"  Vert:  avg={midBand.Average(e => e.vert):F4}  max={midBand.Max(e => e.vert):F4}");
                int horizDominant = midBand.Count(e => e.horiz > e.vert);
                int vertDominant = midBand.Count(e => e.vert > e.horiz);
                _output.WriteLine($"  Horiz-dominant: {horizDominant} ({100f * horizDominant / midBand.Count:F0}%)  Vert-dominant: {vertDominant} ({100f * vertDominant / midBand.Count:F0}%)");
            }

            // Look for concentration around specific error values
            _output.WriteLine($"\n--- Error value frequency (rounded to 0.001y) ---");
            var valueFreq = groundErrors
                .Select(e => MathF.Round(e.pos, 3))
                .GroupBy(e => e)
                .OrderByDescending(g => g.Count())
                .Take(20);
            foreach (var g in valueFreq)
            {
                _output.WriteLine($"  err={g.Key:F3}y: {g.Count()} frames ({100.0f * g.Count() / groundErrors.Count:F1}%)");
            }
        }
    }

    private record StepEvent
    {
        public string RecordingName = "";
        public int Frame;
        public float RecDz, SimDz;
        public float Error, VertError;
        public float RecZ, SimZ;
        public bool IsArtifact;
        public uint Flags;
    }

    private static float SafeAvg(IEnumerable<float> values) => values.Any() ? values.Average() : 0;
    private static float SafeMax(IEnumerable<float> values) => values.Any() ? values.Max() : 0;

    // ==========================================================================
    // GROUND SURFACE DIAGNOSTICS
    // ==========================================================================

    [Fact]
    public void GroundSurfaceDiagnostics_ProbeWorstFrameLocations()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings found or engine not initialized"); return; }

        // Collect frames with >0.8y ground-mode error from cached results
        var groundErrors = new List<(string Name, int Frame, CalibrationResult.FrameDetail Detail,
            RecordedFrame Current, RecordedFrame Next)>();

        foreach (var (name, recording, result) in allResults)
        {

            for (int i = 0; i < result.FrameDetails.Count; i++)
            {
                var fd = result.FrameDetails[i];
                if (fd.PosError > 0.8f && fd.MovementMode is "ground" or "transition" or "transport")
                {
                    // Get the raw recording frames
                    var current = recording.Frames[fd.Frame];
                    var next = recording.Frames[fd.Frame + 1];
                    groundErrors.Add((name, fd.Frame, fd, current, next));
                }
            }
        }

        // VMAP diagnostics for all maps used (may not be available if DLL wasn't rebuilt)
        var mapIds = allResults.Select(r => r.recording.MapId).Distinct();
        foreach (uint mid in mapIds)
        {
            try
            {
                int vmapInfo = GetVmapDiagnostics(mid);
                _output.WriteLine($"VMAP map={mid}: instances={vmapInfo} ({(vmapInfo >= 0 ? "OK" : vmapInfo == -1 ? "NOT LOADED" : vmapInfo == -2 ? "NO VMAPMANAGER" : "NO MAPTREE")})");
            }
            catch (EntryPointNotFoundException)
            {
                _output.WriteLine($"VMAP map={mid}: GetVmapDiagnostics not available in this DLL build");
            }
        }
        _output.WriteLine("");

        _output.WriteLine($"=== GROUND SURFACE DIAGNOSTICS: {groundErrors.Count} frames with >0.8y error ===\n");

        foreach (var (name, frameIdx, detail, current, next) in groundErrors.OrderByDescending(e => e.Detail.PosError))
        {
            float x = current.Position.X;
            float y = current.Position.Y;
            float z = current.Position.Z;
            float nextZ = next.Position.Z;
            uint mapId = detail.IsOnTransport ? 0u : GetMapIdFromRecording(name, allResults);

            _output.WriteLine($"--- {name} frame={frameIdx} err={detail.PosError:F3}y ---");
            _output.WriteLine($"  Flags: 0x{current.MovementFlags:X8} -> 0x{next.MovementFlags:X8}  trans={detail.Transition}");
            _output.WriteLine($"  Rec pos: ({x:F3}, {y:F3}, {z:F3}) -> ({next.Position.X:F3}, {next.Position.Y:F3}, {nextZ:F3})");
            _output.WriteLine($"  Sim pos: ({detail.SimX:F3}, {detail.SimY:F3}, {detail.SimZ:F3})");
            _output.WriteLine($"  dZ(rec): {nextZ - z:F3}  dZ(err): {detail.ErrorZ:F3}");

            // ADT-only terrain height
            float adtZ = NavigationInterop.GetTerrainHeight(mapId, x, y);
            _output.WriteLine($"  ADT terrain Z:  {adtZ:F3}");

            // VMAP+ADT ground Z from multiple probe heights
            _output.WriteLine($"  GetGroundZ probes (VMAP+ADT):");
            float[] probeHeights = [z + 10, z + 5, z + 2, z + 1, z + 0.5f, z, z - 1, z - 2, z - 5];
            foreach (float probeZ in probeHeights)
            {
                float groundZ = NavigationInterop.GetGroundZ(mapId, x, y, probeZ, 15.0f);
                string marker = "";
                if (MathF.Abs(groundZ - z) < 0.1f) marker = " <-- matches current Z";
                if (MathF.Abs(groundZ - nextZ) < 0.1f) marker = " <-- matches next Z";
                _output.WriteLine($"    probe Z={probeZ:F1} -> groundZ={groundZ:F3}{marker}");
            }

            // Terrain triangles in small area around position
            var triangles = new NavigationInterop.TerrainTriangle[64];
            int triCount = NavigationInterop.QueryTerrainTriangles(mapId,
                x - 1, y - 1, x + 1, y + 1, triangles, 64);
            _output.WriteLine($"  ADT triangles in 2x2 box: {triCount}");

            // Show Z range of nearby terrain triangles
            if (triCount > 0)
            {
                float minTriZ = float.MaxValue, maxTriZ = float.MinValue;
                for (int t = 0; t < triCount; t++)
                {
                    var tri = triangles[t];
                    float[] zVals = [tri.Az, tri.Bz, tri.Cz];
                    foreach (float tz in zVals)
                    {
                        if (tz < minTriZ) minTriZ = tz;
                        if (tz > maxTriZ) maxTriZ = tz;
                    }
                }
                _output.WriteLine($"  ADT tri Z range: [{minTriZ:F3}, {maxTriZ:F3}]");
            }

            // Capsule sweep downward to find all walkable surfaces
            try
            {
                var capsuleHeight = 2.0108f;
                var capsuleRadius = 0.3060f;
                var capsule = NavigationInterop.Capsule.FromFeetPosition(x, y, z + 5, capsuleRadius, capsuleHeight);
                var dir = new NavigationInterop.Vector3(0, 0, -1);
                var fwd = new NavigationInterop.Vector3(MathF.Cos(current.Facing), MathF.Sin(current.Facing), 0);
                var hits = new SceneHit[32];
                int hitCount = SweepCapsuleForDiagnostics(mapId, capsule, dir, 15.0f, hits, 32, fwd);

                _output.WriteLine($"  Capsule sweep (from Z={z + 5:F1}, 15y down): {hitCount} hits");
                for (int h = 0; h < hitCount && h < 10; h++)
                {
                    var hit = hits[h];
                    string walkable = hit.NormalZ >= 0.57f ? "WALKABLE" : "wall";
                    string pen = hit.StartPenetrating ? "PEN" : "   ";
                    _output.WriteLine($"    [{h}] {pen} dist={hit.Distance:F3} pt=({hit.PointX:F2},{hit.PointY:F2},{hit.PointZ:F2}) " +
                        $"nrm=({hit.NormalX:F3},{hit.NormalY:F3},{hit.NormalZ:F3}) inst={hit.InstanceId} {walkable}");
                }
            }
            catch (EntryPointNotFoundException)
            {
                _output.WriteLine("  Capsule sweep: SweepCapsule not available in this DLL build");
            }

            _output.WriteLine("");
        }
    }

    private static uint GetMapIdFromRecording(string name,
        List<(string name, MovementRecording recording, CalibrationResult result)> recordings)
    {
        var match = recordings.FirstOrDefault(r => r.name == name);
        return match.recording?.MapId ?? 0;
    }

    // P/Invoke-compatible SceneHit for diagnostics
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SceneHit
    {
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]
        public bool Hit;
        public float Distance;
        public float Time;
        public float PenetrationDepth;
        public float NormalX, NormalY, NormalZ;
        public float PointX, PointY, PointZ;
        public int TriIndex;
        public float BaryU, BaryV, BaryW;
        public uint InstanceId;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]
        public bool StartPenetrating;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]
        public bool NormalFlipped;
        public byte FeatureType;
        public uint PhysMaterialId;
        public float StaticFriction, DynamicFriction, Restitution;
        public byte CapsuleRegion;
    }

    [System.Runtime.InteropServices.DllImport("Navigation.dll", EntryPoint = "GetVmapDiagnostics",
        CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int GetVmapDiagnostics(uint mapId);

    [System.Runtime.InteropServices.DllImport("Navigation.dll", EntryPoint = "SweepCapsule",
        CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int SweepCapsuleForDiagnostics(
        uint mapId,
        in NavigationInterop.Capsule capsule,
        in NavigationInterop.Vector3 direction,
        float distance,
        [System.Runtime.InteropServices.Out] SceneHit[] hits,
        int maxHits,
        in NavigationInterop.Vector3 playerForward);

    // ==========================================================================
    // PRIVATE HELPERS
    // ==========================================================================

    private CalibrationResult ReplayAndAssert(string recordingName)
    {
        var result = _fixture.ReplayCache.GetOrReplay(recordingName, _output, _fixture.IsInitialized);
        LogCalibrationResult(recordingName, result, _output);
        return result;
    }

    [Fact]
    public void UndercityGroundZ_DiagnosticProbe()
    {
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Not initialized"); return; }
        TryPreloadMap(0, _output); // Eastern Kingdoms

        // UndercityMixed worst frame: sim=(1552.671,240.151,57.610) rec=(1552.678,240.147,55.242)
        float recX = 1552.678f, recY = 240.147f, recZ = 55.242f;
        float simZ = 57.610f;

        _output.WriteLine($"=== Undercity GetGroundZ Diagnostic ===");
        _output.WriteLine($"Recorded position: ({recX}, {recY}, {recZ})");
        _output.WriteLine($"Simulated Z: {simZ}");

        // Query GetGroundZ from various heights to see what floors exist
        float[] queryHeights = { recZ - 2, recZ - 1, recZ, recZ + 0.5f, recZ + 1, recZ + 2, simZ, simZ + 1, simZ + 2, 65, 70, 80 };
        foreach (float qz in queryHeights)
        {
            float gz = NavigationInterop.GetGroundZ(0, recX, recY, qz, 10.0f);
            _output.WriteLine($"  GetGroundZ(z={qz:F2}, maxDist=10) = {gz:F4}");
        }

        // Query ADT terrain height (no VMAP, just terrain interpolation)
        float adtZ = NavigationInterop.GetTerrainHeight(0, recX, recY);
        _output.WriteLine($"  ADT TerrainHeight = {adtZ:F4}");

        // Also test nearby points to see if multi-ray would help
        _output.WriteLine($"\n=== Multi-ray probe at recZ+0.5={recZ + 0.5:F2} ===");
        float probeR = 0.256f; // capsule radius
        float[,] offsets = {
            {0, 0}, {probeR, 0}, {-probeR, 0}, {0, probeR}, {0, -probeR},
            {probeR*2, 0}, {-probeR*2, 0}, {0, probeR*2}, {0, -probeR*2}
        };
        for (int i = 0; i < offsets.GetLength(0); i++)
        {
            float ox = offsets[i, 0], oy = offsets[i, 1];
            float gz = NavigationInterop.GetGroundZ(0, recX + ox, recY + oy, recZ + 0.5f, 4.0f);
            _output.WriteLine($"  Probe[{i}] offset=({ox:F3},{oy:F3}) → Z={gz:F4}");
        }

        // Also probe from simZ+0.5 to see what the old queryZ=input.z+2.0 would find
        _output.WriteLine($"\n=== Multi-ray probe at simZ={simZ:F2} (old queryZ=input.z+2) ===");
        for (int i = 0; i < offsets.GetLength(0); i++)
        {
            float ox = offsets[i, 0], oy = offsets[i, 1];
            float gz = NavigationInterop.GetGroundZ(0, recX + ox, recY + oy, simZ, 4.0f);
            _output.WriteLine($"  Probe[{i}] offset=({ox:F3},{oy:F3}) → Z={gz:F4}");
        }

    }

    [Fact]
    public void OrgrimmarGroundZ_WorstFrameDiagnostic()
    {
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Not initialized"); return; }
        TryPreloadMap(1, _output); // Kalimdor

        // Worst ground frames from TransitionAnalysis — engine groundZ is 0.3-0.52y BELOW recording
        // All Orgrimmar (map 1), all pure vertical error, all negative dZ (sim below rec)
        var probePositions = new (int frame, float recX, float recY, float recZ, float simZ)[]
        {
            (1727, 1637.264f, -4374.140f, 29.369f, 28.850f),  // sim 0.52 BELOW (worst)
            (1785, 1637.267f, -4373.962f, 29.359f, 28.851f),  // sim 0.51 BELOW
            (2227, 1671.257f, -4356.295f, 29.856f, 29.443f),  // sim 0.41 BELOW
            (998,  1651.753f, -4374.463f, 24.705f, 24.299f),  // sim 0.41 BELOW
            (1425, 1660.734f, -4332.938f, 61.669f, 61.266f),  // sim 0.40 BELOW
            (839,  1625.772f, -4380.119f, 29.320f, 28.921f),  // sim 0.40 BELOW
        };

        foreach (var (frame, recX, recY, recZ, simZ) in probePositions)
        {
            _output.WriteLine($"\n=== Frame {frame}: rec=({recX:F3},{recY:F3},{recZ:F3}) sim_z={simZ:F3} err={MathF.Abs(simZ - recZ):F3} ===");
            // Probe GetGroundZ from multiple heights
            float[] queryHeights = { recZ - 1, recZ, recZ + 0.5f, recZ + 1, recZ + 2, simZ, simZ + 1 };
            foreach (float qz in queryHeights)
            {
                float gz = NavigationInterop.GetGroundZ(1, recX, recY, qz, 6.0f);
                _output.WriteLine($"  GetGroundZ(z={qz:F2}, maxDist=6) = {gz:F4}  err_to_rec={MathF.Abs(gz - recZ):F4}");
            }
            // ADT terrain
            float adtZ = NavigationInterop.GetTerrainHeight(1, recX, recY);
            _output.WriteLine($"  ADT terrain = {adtZ:F4}");
        }
    }

    [Fact]
    public void GroundZ_SceneCacheVsVmap_Diagnostic()
    {
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Not initialized"); return; }
        TryPreloadMap(1, _output); // Kalimdor

        // Worst ground frames from replay — engine groundZ is 0.4-0.9y BELOW recording
        var probePositions = new (int frame, float recX, float recY, float recZ, float simZ)[]
        {
            (1727, 1637.264f, -4374.140f, 29.369f, 28.850f),
            (2227, 1671.257f, -4356.295f, 29.856f, 29.443f),
            (998,  1651.753f, -4374.463f, 24.705f, 24.299f),
            (1425, 1660.734f, -4332.938f, 61.669f, 61.266f),
            (839,  1625.772f, -4380.119f, 29.320f, 28.921f),
        };

        _output.WriteLine("Comparing scene cache vs raw VMAP getHeight at worst-error positions:");
        _output.WriteLine("(VMAP init may take 30-60s on first call)\n");

        foreach (var (frame, recX, recY, recZ, simZ) in probePositions)
        {
            // Query from recorded Z and recorded Z + 2 (MaNGOS style)
            foreach (float qz in new[] { recZ, recZ + 2.0f })
            {
                float bestZ = NavigationInterop.GetGroundZBypassCache(
                    1, recX, recY, qz, 10.0f,
                    out float vmapZ, out float adtZ, out float bihZ, out float sceneCacheZ);

                _output.WriteLine($"Frame {frame} (qz={qz:F2}): scene={sceneCacheZ:F3} vmap={vmapZ:F3} adt={adtZ:F3} bih={bihZ:F3} best={bestZ:F3} rec={recZ:F3} gap={MathF.Abs(bestZ - recZ):F3}");
            }

            // Enumerate ALL surfaces at this position (no Z filter)
            var zValues = new float[32];
            var instanceIds = new uint[32];
            int surfaceCount = NavigationInterop.EnumerateAllSurfacesAt(1, recX, recY, zValues, instanceIds, 32);
            _output.WriteLine($"  ALL surfaces at ({recX:F3},{recY:F3}): {surfaceCount} found");
            for (int i = 0; i < surfaceCount; i++)
                _output.WriteLine($"    surface[{i}]: Z={zValues[i]:F4} instanceId={instanceIds[i]} err_to_rec={MathF.Abs(zValues[i] - recZ):F4}");

            _output.WriteLine("");
        }
    }

    private void AssertPrecision(CalibrationResult result, float avgTolerance, float p99Tolerance)
    {
        if (result.FrameCount == 0) return;

        // Avg error across ALL frames (catches systematic issues including transitions)
        Assert.True(result.AvgPositionError < avgTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds {avgTolerance}y tolerance");

        // P99 of steady-state frames (excludes flag transitions which have inherent
        // sub-frame timing imprecision). This measures spatial precision of continuous movement.
        Assert.True(result.SteadyStateP99 < p99Tolerance,
            $"Steady-state P99 position error {result.SteadyStateP99:F4}y exceeds {p99Tolerance}y tolerance " +
            $"(overall P99={result.P99:F4}y, max={result.MaxPositionError:F4}y)");
    }

    // ==========================================================================
    // WMO DOODAD EXTRACTION
    // ==========================================================================

    [Fact]
    public void ExtractWmoDoodads_FromMpq()
    {
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Not initialized"); return; }

        // Locate WoW client Data directory
        string[] possiblePaths = [
            @"D:\World of Warcraft\Data",
            @"C:\World of Warcraft\Data",
            @"E:\World of Warcraft\Data",
        ];
        string? mpqDataDir = possiblePaths.FirstOrDefault(System.IO.Directory.Exists);
        if (mpqDataDir == null)
        {
            _output.WriteLine("SKIP: WoW client Data directory not found");
            return;
        }

        // Find vmaps directory
        string baseDir = AppContext.BaseDirectory;
        string vmapsDir = System.IO.Path.Combine(baseDir, "vmaps");
        if (!System.IO.Directory.Exists(vmapsDir))
        {
            _output.WriteLine($"SKIP: vmaps directory not found at {vmapsDir}");
            return;
        }

        _output.WriteLine($"MPQ Data: {mpqDataDir}");
        _output.WriteLine($"vmaps:    {vmapsDir}");

        int result = NavigationInterop.ExtractWmoDoodads(mpqDataDir, vmapsDir);
        _output.WriteLine($"Result: {result} .doodads files written");
        Assert.True(result >= 0, $"ExtractWmoDoodads failed with result {result}");
        Assert.True(result > 0, "No .doodads files were extracted");

        // Verify some key files exist
        string[] expectedWmos = ["Orgrimmar.wmo.doodads", "Stormwind.wmo.doodads"];
        foreach (var name in expectedWmos)
        {
            // Case-insensitive search
            var match = System.IO.Directory.GetFiles(vmapsDir, "*.doodads")
                .FirstOrDefault(f => System.IO.Path.GetFileName(f)
                    .Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                _output.WriteLine($"  Found: {System.IO.Path.GetFileName(match)}");
            else
                _output.WriteLine($"  Missing: {name} (may not match vmaps naming)");
        }
    }
}
