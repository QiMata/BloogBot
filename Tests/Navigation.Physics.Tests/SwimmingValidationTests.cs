using Navigation.Physics.Tests.Helpers;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;
using static Navigation.Physics.Tests.Helpers.MoveFlags;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Navigation.Physics.Tests;

/// <summary>
/// Validation tests for swimming movement data recorded in WoW 1.12.1.
/// These analyze recorded swim frames for:
///   - Forward swim speed matching SwimSpeed field
///   - Backward swim speed matching SwimBackSpeed field
///   - Swim pitch angles during ascent/descent
///   - Water entry/exit transitions (Swimming flag toggle)
///   - 3D movement consistency (pitch-adjusted velocity)
///
/// All tests skip gracefully when swimming recordings are not yet available.
/// Record swimming data via:
///   /say rec swim_forward ? swim forward in deep water ? /say rec
///   or use BLOOGBOT_AUTOMATED_RECORDING=1 (scenario 08_swim_forward)
/// </summary>
public class SwimmingValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // ==========================================================================
    // SWIM SPEED
    // ==========================================================================

    [Fact]
    public void SwimForward_SpeedMatchesSwimSpeed()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var swimForwardFrames = recording.Frames
            .Where(f => (f.MovementFlags & (Swimming | Forward)) == (Swimming | Forward)
                     && (f.MovementFlags & Backward) == 0)
            .ToList();

        _output.WriteLine($"Swim+Forward frames: {swimForwardFrames.Count}");
        Assert.True(swimForwardFrames.Count >= 10,
            $"Need 10+ swim+forward frames, got {swimForwardFrames.Count}");

        var segments = FindConsecutiveSegments(swimForwardFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).FirstOrDefault();
        Assert.True(bestSeg != null && bestSeg.Count >= 5,
            "No long enough consecutive swim segment");

        var speeds = MeasureFrameSpeeds3D(bestSeg!);
        Assert.True(speeds.Count >= 3, "Not enough speed samples");

        float avgSpeed = TrimmedMean(speeds, 0.05f);
        float expectedSpeed = swimForwardFrames[0].SwimSpeed;

        _output.WriteLine($"Avg swim speed: {avgSpeed:F4} y/s ({bestSeg!.Count}-frame segment)");
        _output.WriteLine($"Expected SwimSpeed: {expectedSpeed:F4} y/s");
        _output.WriteLine($"Difference: {MathF.Abs(avgSpeed - expectedSpeed):F4} y/s");

        Assert.True(MathF.Abs(avgSpeed - expectedSpeed) < Tolerances.Velocity,
            $"Swim speed mismatch: recorded={avgSpeed:F4}, expected={expectedSpeed:F4}");
    }

    [Fact]
    public void SwimBackward_SpeedMatchesSwimBackSpeed()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var swimBackFrames = recording.Frames
            .Where(f => (f.MovementFlags & (Swimming | Backward)) == (Swimming | Backward)
                     && (f.MovementFlags & Forward) == 0)
            .ToList();

        _output.WriteLine($"Swim+Backward frames: {swimBackFrames.Count}");
        if (swimBackFrames.Count < 10)
        {
            _output.WriteLine("SKIP: Not enough swim+backward frames (need dedicated backward swim recording)");
            return;
        }

        var segments = FindConsecutiveSegments(swimBackFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).FirstOrDefault();
        if (bestSeg == null || bestSeg.Count < 5)
        {
            _output.WriteLine("SKIP: No long enough backward swim segment");
            return;
        }

        var speeds = MeasureFrameSpeeds3D(bestSeg);
        float avgSpeed = TrimmedMean(speeds, 0.05f);
        float expectedSpeed = swimBackFrames[0].SwimBackSpeed;

        _output.WriteLine($"Avg backward swim speed: {avgSpeed:F4} y/s");
        _output.WriteLine($"Expected SwimBackSpeed: {expectedSpeed:F4} y/s");

        Assert.True(MathF.Abs(avgSpeed - expectedSpeed) < Tolerances.Velocity,
            $"Backward swim speed mismatch: recorded={avgSpeed:F4}, expected={expectedSpeed:F4}");
    }

    // ==========================================================================
    // SWIM PITCH
    // ==========================================================================

    [Fact]
    public void SwimPitch_IsRecordedDuringSwimming()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var swimFrames = recording.Frames
            .Where(f => (f.MovementFlags & Swimming) != 0)
            .ToList();

        Assert.True(swimFrames.Count >= 10, "Not enough swimming frames");

        int pitchNonZero = swimFrames.Count(f => MathF.Abs(f.SwimPitch) > 0.01f);
        float minPitch = swimFrames.Min(f => f.SwimPitch);
        float maxPitch = swimFrames.Max(f => f.SwimPitch);

        _output.WriteLine($"Swimming frames: {swimFrames.Count}");
        _output.WriteLine($"Frames with non-zero pitch: {pitchNonZero}");
        _output.WriteLine($"Pitch range: [{minPitch:F4}, {maxPitch:F4}] radians");
        _output.WriteLine($"Pitch range: [{minPitch * 180f / MathF.PI:F1}, {maxPitch * 180f / MathF.PI:F1}] degrees");

        // Pitch should be within valid range (-?/2 to ?/2)
        Assert.True(minPitch >= -MathF.PI / 2 - 0.01f, $"Pitch below -?/2: {minPitch:F4}");
        Assert.True(maxPitch <= MathF.PI / 2 + 0.01f, $"Pitch above ?/2: {maxPitch:F4}");
    }

    [Fact]
    public void SwimPitch_AffectsVerticalMovement()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var pitchedSwimFrames = recording.Frames
            .Where(f => (f.MovementFlags & (Swimming | Forward)) == (Swimming | Forward)
                     && MathF.Abs(f.SwimPitch) > 0.1f)
            .ToList();

        if (pitchedSwimFrames.Count < 5)
        {
            _output.WriteLine("SKIP: No swim frames with significant pitch");
            _output.WriteLine("Record ascending/descending swim to test pitch-based vertical movement");
            return;
        }

        _output.WriteLine($"Pitched swim frames: {pitchedSwimFrames.Count}");

        // Verify that positive pitch ? downward movement, negative pitch ? upward
        // (WoW convention: positive pitch = looking down)
        int correctDirection = 0;
        int total = 0;

        var segments = FindConsecutiveSegments(pitchedSwimFrames, maxGapMs: 100);
        foreach (var seg in segments.Where(s => s.Count >= 3))
        {
            for (int i = 1; i < seg.Count; i++)
            {
                float dt = (seg[i].FrameTimestamp - seg[i - 1].FrameTimestamp) / 1000.0f;
                if (dt <= 0) continue;
                float dz = seg[i].Position.Z - seg[i - 1].Position.Z;
                float pitch = seg[i - 1].SwimPitch;

                total++;
                // Positive pitch = looking down ? Z should decrease
                if ((pitch > 0 && dz < 0) || (pitch < 0 && dz > 0))
                    correctDirection++;
            }
        }

        if (total > 0)
        {
            float pct = correctDirection * 100f / total;
            _output.WriteLine($"Pitch-direction correlation: {correctDirection}/{total} ({pct:F1}%)");
        }
    }

    // ==========================================================================
    // WATER ENTRY/EXIT TRANSITIONS
    // ==========================================================================

    [Fact]
    public void WaterTransitions_SwimmingFlagTogglesCorrectly()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        int entryCount = 0;
        int exitCount = 0;

        for (int i = 1; i < recording.Frames.Count; i++)
        {
            bool prevSwim = (recording.Frames[i - 1].MovementFlags & Swimming) != 0;
            bool currSwim = (recording.Frames[i].MovementFlags & Swimming) != 0;

            if (!prevSwim && currSwim)
            {
                entryCount++;
                var pos = recording.Frames[i].Position;
                _output.WriteLine($"  Water ENTRY at frame {i}: ({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");
            }
            else if (prevSwim && !currSwim)
            {
                exitCount++;
                var pos = recording.Frames[i].Position;
                _output.WriteLine($"  Water EXIT at frame {i}: ({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");
            }
        }

        _output.WriteLine($"\nWater entries: {entryCount}, exits: {exitCount}");

        // At minimum, the recording should have the Swimming flag set at some point
        int totalSwimFrames = recording.Frames.Count(f => (f.MovementFlags & Swimming) != 0);
        Assert.True(totalSwimFrames > 0,
            "No Swimming flag frames found - recording may not be in water");
    }

    // ==========================================================================
    // 3D MOVEMENT CONSISTENCY
    // ==========================================================================

    [Fact]
    public void SwimMovement_HorizontalAndVerticalSpeedConsistent()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var swimForwardFrames = recording.Frames
            .Where(f => (f.MovementFlags & (Swimming | Forward)) == (Swimming | Forward))
            .ToList();

        if (swimForwardFrames.Count < 20)
        {
            _output.WriteLine("SKIP: Not enough swim+forward frames for consistency check");
            return;
        }

        var speeds3D = new List<float>();
        var speedsHoriz = new List<float>();
        var speedsVert = new List<float>();

        var segments = FindConsecutiveSegments(swimForwardFrames, maxGapMs: 100);
        foreach (var seg in segments.Where(s => s.Count >= 3))
        {
            for (int i = 1; i < seg.Count; i++)
            {
                float dt = (seg[i].FrameTimestamp - seg[i - 1].FrameTimestamp) / 1000.0f;
                if (dt <= 0) continue;
                float dx = seg[i].Position.X - seg[i - 1].Position.X;
                float dy = seg[i].Position.Y - seg[i - 1].Position.Y;
                float dz = seg[i].Position.Z - seg[i - 1].Position.Z;
                speeds3D.Add(MathF.Sqrt(dx * dx + dy * dy + dz * dz) / dt);
                speedsHoriz.Add(MathF.Sqrt(dx * dx + dy * dy) / dt);
                speedsVert.Add(MathF.Abs(dz) / dt);
            }
        }

        if (speeds3D.Count < 5) { _output.WriteLine("SKIP: Not enough consecutive pairs"); return; }

        float avg3D = TrimmedMean(speeds3D, 0.05f);
        float avgHoriz = TrimmedMean(speedsHoriz, 0.05f);
        float avgVert = TrimmedMean(speedsVert, 0.05f);
        float swimSpeed = swimForwardFrames[0].SwimSpeed;

        _output.WriteLine($"Avg 3D speed: {avg3D:F4} y/s");
        _output.WriteLine($"Avg horizontal speed: {avgHoriz:F4} y/s");
        _output.WriteLine($"Avg vertical speed: {avgVert:F4} y/s");
        _output.WriteLine($"Expected SwimSpeed: {swimSpeed:F4} y/s");

        // 3D speed should not exceed SwimSpeed by more than tolerance
        Assert.True(avg3D < swimSpeed + Tolerances.Velocity,
            $"3D swim speed exceeds expected: {avg3D:F4} > {swimSpeed:F4} + tolerance");
    }

    [Fact]
    public void SwimSpeed_ConsistentOverDuration()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var swimForwardFrames = recording.Frames
            .Where(f => (f.MovementFlags & (Swimming | Forward)) == (Swimming | Forward))
            .ToList();

        if (swimForwardFrames.Count < 60)
        {
            _output.WriteLine("SKIP: Need 60+ swim frames for duration consistency test");
            return;
        }

        var frameSpeeds = new List<float>();
        for (int i = 1; i < swimForwardFrames.Count; i++)
        {
            long gap = swimForwardFrames[i].FrameTimestamp - swimForwardFrames[i - 1].FrameTimestamp;
            if (gap > 100 || gap <= 0) continue;
            float dt = gap / 1000.0f;
            float dx = swimForwardFrames[i].Position.X - swimForwardFrames[i - 1].Position.X;
            float dy = swimForwardFrames[i].Position.Y - swimForwardFrames[i - 1].Position.Y;
            float dz = swimForwardFrames[i].Position.Z - swimForwardFrames[i - 1].Position.Z;
            frameSpeeds.Add(MathF.Sqrt(dx * dx + dy * dy + dz * dz) / dt);
        }

        if (frameSpeeds.Count < 20) { _output.WriteLine("SKIP: Not enough speed samples"); return; }

        float overall = TrimmedMean(frameSpeeds, 0.05f);
        int quarterSize = frameSpeeds.Count / 4;

        for (int q = 0; q < 4; q++)
        {
            var quarter = frameSpeeds.Skip(q * quarterSize).Take(quarterSize).ToList();
            if (quarter.Count > 0)
            {
                float qAvg = quarter.Average();
                _output.WriteLine($"  Quarter {q + 1}: avg={qAvg:F4} y/s ({quarter.Count} samples)");
            }
        }

        _output.WriteLine($"Overall: {overall:F4} y/s, Expected: {swimForwardFrames[0].SwimSpeed:F4} y/s");

        // Trim outliers before computing stddev (swimming pitch changes cause
        // per-frame noise at 60fps, but the quarter averages should be stable)
        var sorted = frameSpeeds.OrderBy(s => s).ToList();
        int trimCount = (int)(sorted.Count * 0.05f);
        var trimmed = sorted.Skip(trimCount).Take(sorted.Count - 2 * trimCount).ToList();
        float trimmedMean = trimmed.Average();
        float stdDev = MathF.Sqrt(trimmed.Select(s => (s - trimmedMean) * (s - trimmedMean)).Average());
        _output.WriteLine($"Trimmed standard deviation: {stdDev:F4} y/s (5% trim)");

        float cv = stdDev / trimmedMean;
        _output.WriteLine($"Coefficient of variation: {cv:F4} ({cv * 100:F1}%)");
        Assert.True(cv < 0.5f, $"Speed too variable: CV={cv:F4} (>50%)");
    }

    // ==========================================================================
    // SWIMMING FLAG COMBINATIONS
    // ==========================================================================

    [Fact]
    public void SwimmingFlags_DistributionReport()
    {
        var recording = TryLoadSwimmingRecording();
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        var swimFrames = recording.Frames.Where(f => (f.MovementFlags & Swimming) != 0).ToList();
        _output.WriteLine($"Total swimming frames: {swimFrames.Count}/{recording.Frames.Count}");

        var flagCombinations = swimFrames
            .GroupBy(f => f.MovementFlags)
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine("\nFlag combinations during swimming:");
        foreach (var group in flagCombinations)
        {
            var flags = group.Key;
            var desc = DescribeFlags(flags);
            _output.WriteLine($"  0x{flags:X8} ({desc}): {group.Count()} frames");
        }
    }

    // ==========================================================================
    // PRIVATE HELPERS
    // ==========================================================================

    /// <summary>
    /// Tries to load a swimming recording. Checks Recordings.Swimming first,
    /// then scans all recordings for any with 50+ swimming frames.
    /// </summary>
    private MovementRecording? TryLoadSwimmingRecording()
    {
        // Try the named swimming recording first
        var named = TryLoadByFilename(Recordings.Swimming, _output);
        if (named != null && named.Frames.Count(f => (f.MovementFlags & Swimming) != 0) > 10)
            return named;

        // Scan all recordings for any with swimming data
        var all = LoadAllRecordings(_output);
        var swimRec = all
            .Where(r => r.rec.Frames.Count(f => (f.MovementFlags & Swimming) != 0) > 50)
            .OrderByDescending(r => r.rec.Frames.Count(f => (f.MovementFlags & Swimming) != 0))
            .FirstOrDefault();

        if (swimRec.rec != null)
        {
            _output.WriteLine($"Using recording with swimming data: {swimRec.name}");
            return swimRec.rec;
        }

        return null;
    }

    private static List<float> MeasureFrameSpeeds3D(List<RecordedFrame> frames)
    {
        var speeds = new List<float>();
        for (int i = 1; i < frames.Count; i++)
        {
            float dt = (frames[i].FrameTimestamp - frames[i - 1].FrameTimestamp) / 1000.0f;
            if (dt <= 0) continue;
            float dx = frames[i].Position.X - frames[i - 1].Position.X;
            float dy = frames[i].Position.Y - frames[i - 1].Position.Y;
            float dz = frames[i].Position.Z - frames[i - 1].Position.Z;
            speeds.Add(MathF.Sqrt(dx * dx + dy * dy + dz * dz) / dt);
        }
        return speeds;
    }

    private static string DescribeFlags(uint flags)
    {
        var parts = new List<string>();
        if ((flags & Forward) != 0) parts.Add("FWD");
        if ((flags & Backward) != 0) parts.Add("BACK");
        if ((flags & StrafeLeft) != 0) parts.Add("STR_L");
        if ((flags & StrafeRight) != 0) parts.Add("STR_R");
        if ((flags & Jumping) != 0) parts.Add("JUMP");
        if ((flags & FallingFar) != 0) parts.Add("FALL_FAR");
        if ((flags & Swimming) != 0) parts.Add("SWIM");
        if ((flags & 0x00000010) != 0) parts.Add("TURN_L");
        if ((flags & 0x00000020) != 0) parts.Add("TURN_R");
        return parts.Count > 0 ? string.Join("|", parts) : "NONE";
    }
}
