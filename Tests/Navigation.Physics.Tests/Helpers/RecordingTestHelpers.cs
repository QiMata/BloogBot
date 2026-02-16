using GameData.Core.Constants;
using GameData.Core.Enums;
using static Navigation.Physics.Tests.NavigationInterop;
using Xunit.Abstractions;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// Shared utilities for physics recording tests.
/// </summary>
public static class RecordingTestHelpers
{
    // Fallback capsule dimensions for recordings without race/gender metadata
    public const float FallbackHeight = 2.0f;
    public const float FallbackRadius = 0.5f;

    /// <summary>
    /// Resolves capsule dimensions from a recording's race/gender metadata.
    /// Falls back to generic defaults for legacy recordings without race/gender.
    /// </summary>
    public static (float radius, float height) GetCapsuleDimensions(
        MovementRecording recording, ITestOutputHelper? output = null)
    {
        if (recording.Race == 0)
        {
            output?.WriteLine($"  [capsule] No race in recording, using fallback: r={FallbackRadius}, h={FallbackHeight}");
            return (FallbackRadius, FallbackHeight);
        }

        var race = (Race)recording.Race;
        var gender = (Gender)recording.Gender;
        var (radius, height) = RaceDimensions.GetCapsuleForRace(race, gender);
        output?.WriteLine($"  [capsule] Race={race}, Gender={gender} => r={radius:F4}, h={height:F4}");
        return (radius, height);
    }

    /// <summary>
    /// Loads a recording by filename pattern. Logs metadata to output.
    /// </summary>
    public static MovementRecording LoadByFilename(string filenamePattern, ITestOutputHelper output)
    {
        var path = RecordingLoader.FindRecordingByFilename(filenamePattern);
        output.WriteLine($"Loading recording: {Path.GetFileName(path)}");
        var recording = RecordingLoader.LoadFromFile(path);
        output.WriteLine($"  Frames: {recording.Frames.Count}, Duration: {recording.DurationMs}ms, " +
            $"Race: {recording.RaceName}, Zone: {recording.ZoneName}");
        return recording;
    }

    /// <summary>
    /// Attempts to load a recording by filename. Returns null if the pattern is empty
    /// or no matching file is found (allows tests to skip gracefully).
    /// </summary>
    public static MovementRecording? TryLoadByFilename(string filenamePattern, ITestOutputHelper output)
    {
        if (string.IsNullOrEmpty(filenamePattern))
            return null;
        try
        {
            return LoadByFilename(filenamePattern, output);
        }
        catch (FileNotFoundException)
        {
            output.WriteLine($"Recording not found: {filenamePattern}");
            return null;
        }
    }

    /// <summary>
    /// Loads all recordings from the recordings directory.
    /// Returns an empty list if the directory doesn't exist.
    /// </summary>
    public static List<(string name, MovementRecording rec)> LoadAllRecordings(ITestOutputHelper output)
    {
        string dir;
        try
        {
            dir = RecordingLoader.GetRecordingsDirectory();
        }
        catch (DirectoryNotFoundException)
        {
            output.WriteLine("Recordings directory not found");
            return [];
        }

        var results = new List<(string name, MovementRecording rec)>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var rec = RecordingLoader.LoadFromFile(file);
                if (rec.Frames.Count > 0)
                    results.Add((Path.GetFileNameWithoutExtension(file), rec));
            }
            catch (Exception ex)
            {
                output.WriteLine($"  Failed to load {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        output.WriteLine($"Loaded {results.Count} recordings from {dir}");
        return results;
    }

    /// <summary>
    /// Preloads map data for a given map ID, logging success or failure.
    /// </summary>
    public static void TryPreloadMap(uint mapId, ITestOutputHelper output)
    {
        try
        {
            PreloadMap(mapId);
            output.WriteLine($"Preloaded map {mapId}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Failed to preload map {mapId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs a calibration result to the test output.
    /// </summary>
    public static void LogCalibrationResult(string scenario, CalibrationResult result, ITestOutputHelper output)
    {
        output.WriteLine($"=== Calibration: {scenario} ===");
        var skipParts = new List<string>();
        if (result.TeleportCount > 0) skipParts.Add($"{result.TeleportCount} teleport(s)");
        if (result.TransportTransitionCount > 0) skipParts.Add($"{result.TransportTransitionCount} transport transition(s)");
        if (result.TransportFrameCount > 0) skipParts.Add($"{result.TransportFrameCount} transport frame(s) [no GO data]");
        var skipSuffix = skipParts.Count > 0 ? $" (skipped {string.Join(", ", skipParts)})" : "";
        output.WriteLine($"  Frames simulated: {result.FrameCount}{skipSuffix}");
        if (result.TransportSimulatedCount > 0)
            output.WriteLine($"  Transport frames simulated: {result.TransportSimulatedCount}");
        output.WriteLine($"  Avg position error: {result.AvgPositionError:F4} yards");
        output.WriteLine($"  P50: {result.P50:F4}y  P95: {result.P95:F4}y  P99: {result.P99:F4}y  Max: {result.MaxPositionError:F4}y");
        output.WriteLine($"  Avg horiz error: {result.AvgHorizError:F4} yards");
        output.WriteLine($"  Avg vert error: {result.AvgVertError:F4} yards");
        output.WriteLine($"  Steady-state: n={result.SteadyStateCount}  avg={result.SteadyStateAvg:F4}y  P99={result.SteadyStateP99:F4}y");
        if (result.ArtifactCount > 0 || result.SplineElevationCount > 0)
        {
            var clean = result.CleanFrames;
            if (clean.Count > 0)
            {
                var sorted = clean.Select(f => f.PosError).OrderBy(e => e).ToList();
                int p99Idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
                output.WriteLine($"  Recording artifacts: {result.ArtifactCount} Z-spike frames, {result.SplineElevationCount} SPLINE_ELEVATION transitions");
                output.WriteLine($"  Clean (excl artifacts): n={clean.Count}  avg={clean.Average(f => f.PosError):F4}y  p99={sorted[p99Idx]:F4}y  max={clean.Max(f => f.PosError):F4}y");
            }
        }

        if (result.WorstFrame >= 0 && result.WorstFrame < result.FrameDetails.Count)
        {
            var worst = result.FrameDetails[result.WorstFrame];
            output.WriteLine($"  Worst frame {worst.Frame}: " +
                $"sim=({worst.SimX:F3},{worst.SimY:F3},{worst.SimZ:F3}) " +
                $"rec=({worst.RecX:F3},{worst.RecY:F3},{worst.RecZ:F3}) " +
                $"mode={worst.MovementMode} flags=0x{worst.MoveFlags:X}→0x{worst.NextMoveFlags:X}");
        }

        // Top 10 worst steady-state frames with XY/Z breakdown
        var worstSS = result.SteadyStateFrames
            .OrderByDescending(f => f.PosError)
            .Take(10)
            .ToList();
        if (worstSS.Count > 0)
        {
            output.WriteLine("  --- TOP 10 WORST STEADY-STATE FRAMES ---");
            foreach (var f in worstSS)
            {
                output.WriteLine($"  #{f.Frame}: err={f.PosError:F4}y  dX={f.ErrorX:F4}  dY={f.ErrorY:F4}  dZ={f.ErrorZ:F4}  " +
                    $"horiz={f.HorizError:F4}  vert={f.VertError:F4}  mode={f.MovementMode}  " +
                    $"flags=0x{f.MoveFlags:X}  dt={f.Dt:F4}s  speed={f.RecordedSpeed:F2}  " +
                    $"sim=({f.SimX:F3},{f.SimY:F3},{f.SimZ:F3})  rec=({f.RecX:F3},{f.RecY:F3},{f.RecZ:F3})");
            }
        }

        // Transition-specific breakdown
        LogTransitionStats(result, output);

        // On-transport breakdown
        var transportStats = result.OnTransportStats();
        if (transportStats.count > 0)
        {
            output.WriteLine($"  [Transport] n={transportStats.count}  avg={transportStats.avg:F4}y  " +
                $"p99={transportStats.p99:F4}y  max={transportStats.max:F4}y");
        }

        // Movement mode breakdown
        LogMovementModeStats(result, output);
    }

    /// <summary>
    /// Logs per-transition-type error statistics.
    /// </summary>
    private static void LogTransitionStats(CalibrationResult result, ITestOutputHelper output)
    {
        var transitionTypes = new[]
        {
            TransitionType.JumpStart,
            TransitionType.Landing,
            TransitionType.WaterEntry,
            TransitionType.WaterExit,
            TransitionType.DirectionChange,
            TransitionType.SpeedChange,
            TransitionType.SurfaceStep,
            TransitionType.Other,
        };

        bool anyTransitions = false;
        foreach (var tt in transitionTypes)
        {
            var (count, avg, max, p99) = result.TransitionStats(tt);
            if (count > 0)
            {
                if (!anyTransitions)
                {
                    output.WriteLine("  --- Transition Breakdown ---");
                    anyTransitions = true;
                }
                output.WriteLine($"  [{tt}] n={count}  avg={avg:F4}y  p99={p99:F4}y  max={max:F4}y");
            }
        }
    }

    /// <summary>
    /// Logs error statistics broken down by movement mode (ground, air, swim, transport).
    /// </summary>
    private static void LogMovementModeStats(CalibrationResult result, ITestOutputHelper output)
    {
        if (result.FrameCount == 0) return;

        var modeGroups = result.FrameDetails
            .GroupBy(f => f.MovementMode)
            .OrderByDescending(g => g.Count());

        bool hasMultipleModes = modeGroups.Count() > 1;
        if (!hasMultipleModes) return;

        output.WriteLine("  --- Movement Mode Breakdown ---");
        foreach (var group in modeGroups)
        {
            var frames = group.ToList();
            float avg = frames.Average(f => f.PosError);
            float max = frames.Max(f => f.PosError);
            var sorted = frames.Select(f => f.PosError).OrderBy(e => e).ToList();
            int idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
            output.WriteLine($"  [{group.Key}] n={frames.Count}  avg={avg:F4}y  p99={sorted[idx]:F4}y  max={max:F4}y");
        }
    }

    /// <summary>
    /// Finds consecutive segments of frames within a maximum time gap.
    /// Used to identify coherent movement sequences (jump arcs, swim stretches, etc.)
    /// </summary>
    public static List<List<RecordedFrame>> FindConsecutiveSegments(
        List<RecordedFrame> frames, long maxGapMs = 100)
    {
        var segments = new List<List<RecordedFrame>>();
        if (frames.Count == 0) return segments;

        var current = new List<RecordedFrame> { frames[0] };

        for (int i = 1; i < frames.Count; i++)
        {
            long gap = frames[i].FrameTimestamp - frames[i - 1].FrameTimestamp;
            if (gap <= maxGapMs)
            {
                current.Add(frames[i]);
            }
            else
            {
                if (current.Count >= 2)
                    segments.Add(current);
                current = [frames[i]];
            }
        }

        if (current.Count >= 2)
            segments.Add(current);

        return segments;
    }

    /// <summary>
    /// Computes the trimmed mean, excluding the top and bottom percentile of values.
    /// </summary>
    public static float TrimmedMean(List<float> values, float trimFraction)
    {
        if (values.Count < 3) return values.Average();
        var sorted = values.OrderBy(v => v).ToList();
        int trimCount = Math.Max(1, (int)(sorted.Count * trimFraction));
        var trimmed = sorted.Skip(trimCount).Take(sorted.Count - 2 * trimCount).ToList();
        return trimmed.Count > 0 ? trimmed.Average() : values.Average();
    }

    /// <summary>
    /// Normalizes an angle to [-?, ?].
    /// </summary>
    public static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2 * MathF.PI;
        while (angle < -MathF.PI) angle += 2 * MathF.PI;
        return angle;
    }
}
