using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;
using static Navigation.Physics.Tests.Helpers.MoveFlags;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Navigation.Physics.Tests;

/// <summary>
/// Analysis tests that scan ALL available recordings for physics-relevant patterns.
/// No C++ engine required — these provide ground-truth data for calibration.
///
/// Categories:
///   - Swimming: finds recordings with SWIMMING flag, measures speed/pitch
///   - Collision: detects wall hits (speed drops), direction deflection (wall slide)
///   - Slope: identifies steep terrain, slope rejection candidates
///   - Free-fall: fits gravity from long falls, checks for terminal velocity
///   - Recording health: validates data quality across all recordings
/// </summary>
public class RecordingAnalysisTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // ==========================================================================
    // SWIMMING
    // ==========================================================================

    [Fact]
    public void Swimming_FindRecordingsWithSwimmingFrames()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        int totalWithSwimming = 0;
        foreach (var (name, rec) in recordings)
        {
            int swimFrames = rec.Frames.Count(f => (f.MovementFlags & Swimming) != 0);
            if (swimFrames > 0)
            {
                int swimFwd = rec.Frames.Count(f => (f.MovementFlags & (Swimming | Forward)) == (Swimming | Forward));
                int swimBack = rec.Frames.Count(f => (f.MovementFlags & (Swimming | Backward)) == (Swimming | Backward));
                var pitchValues = rec.Frames.Where(f => (f.MovementFlags & Swimming) != 0 && f.SwimPitch != 0)
                    .Select(f => f.SwimPitch).ToList();

                _output.WriteLine($"  {name}: {swimFrames} swim (fwd={swimFwd}, back={swimBack}, pitch_nonzero={pitchValues.Count})");
                if (pitchValues.Count > 0)
                    _output.WriteLine($"    Pitch range: {pitchValues.Min():F4} to {pitchValues.Max():F4}");
                totalWithSwimming++;
            }
        }

        _output.WriteLine($"\nRecordings with SWIMMING: {totalWithSwimming}/{recordings.Count}");
        if (totalWithSwimming == 0)
        {
            _output.WriteLine("\n*** No swimming recordings found. ***");
            _output.WriteLine("Record: /say rec swim_test -> swim -> /say rec");
        }
    }

    [Fact]
    public void Swimming_SpeedAndPitchAnalysis()
    {
        var recordings = LoadAllRecordings(_output);
        var swimRecordings = recordings.Where(r => r.rec.Frames.Count(f => (f.MovementFlags & Swimming) != 0) > 50).ToList();

        if (swimRecordings.Count == 0) { _output.WriteLine("SKIP: No recordings with 50+ swim frames"); return; }

        foreach (var (name, rec) in swimRecordings)
        {
            _output.WriteLine($"=== {name} ===");
            var swimForwardFrames = rec.Frames
                .Where(f => (f.MovementFlags & (Swimming | Forward)) == (Swimming | Forward)).ToList();

            if (swimForwardFrames.Count < 10) { _output.WriteLine("  Not enough swim+forward frames"); continue; }

            var speeds3D = new List<float>();
            foreach (var seg in FindConsecutiveSegments(swimForwardFrames, maxGapMs: 100).Where(s => s.Count >= 3))
            {
                for (int i = 1; i < seg.Count; i++)
                {
                    float dt = (seg[i].FrameTimestamp - seg[i - 1].FrameTimestamp) / 1000.0f;
                    if (dt <= 0) continue;
                    float dx = seg[i].Position.X - seg[i - 1].Position.X;
                    float dy = seg[i].Position.Y - seg[i - 1].Position.Y;
                    float dz = seg[i].Position.Z - seg[i - 1].Position.Z;
                    speeds3D.Add(MathF.Sqrt(dx * dx + dy * dy + dz * dz) / dt);
                }
            }

            if (speeds3D.Count < 5) continue;
            float avgSpeed = TrimmedMean(speeds3D, 0.05f);
            float expectedSwimSpeed = swimForwardFrames[0].SwimSpeed;

            _output.WriteLine($"  Avg 3D speed: {avgSpeed:F4} y/s, Expected: {expectedSwimSpeed:F4} y/s");
            Assert.True(MathF.Abs(avgSpeed - expectedSwimSpeed) < 1.5f,
                $"Swim speed mismatch: measured={avgSpeed:F4}, expected={expectedSwimSpeed:F4}");
        }
    }

    // ==========================================================================
    // COLLISION / WALL SLIDE
    // ==========================================================================

    [Fact]
    public void Collision_DetectWallCollisionFrames()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        int totalCollisionFrames = 0;
        foreach (var (name, rec) in recordings)
        {
            var collisionFrames = new List<(int index, float expected, float actual, float ratio)>();

            for (int i = 1; i < rec.Frames.Count; i++)
            {
                var prev = rec.Frames[i - 1];
                var curr = rec.Frames[i];
                uint flags = prev.MovementFlags;
                if ((flags & (Jumping | FallingFar | Swimming)) != 0) continue;
                if ((flags & DirectionalMask) == 0) continue;

                float dt = (curr.FrameTimestamp - prev.FrameTimestamp) / 1000.0f;
                if (dt <= 0) continue;

                float dx = curr.Position.X - prev.Position.X;
                float dy = curr.Position.Y - prev.Position.Y;
                float actualSpeed = MathF.Sqrt(dx * dx + dy * dy) / dt;
                float expectedSpeed = (flags & Backward) != 0 ? prev.RunBackSpeed : prev.RunSpeed;
                float ratio = expectedSpeed > 0 ? actualSpeed / expectedSpeed : 1.0f;

                if (ratio < 0.5f && expectedSpeed > 1.0f)
                    collisionFrames.Add((i, expectedSpeed, actualSpeed, ratio));
            }

            if (collisionFrames.Count > 0)
            {
                _output.WriteLine($"  {name}: {collisionFrames.Count} collision frames");
                foreach (var (idx, exp, act, ratio) in collisionFrames.OrderBy(c => c.ratio).Take(5))
                {
                    var f = rec.Frames[idx];
                    _output.WriteLine($"    Frame {idx}: expected={exp:F2} actual={act:F2} ratio={ratio:F3} " +
                        $"flags=0x{rec.Frames[idx - 1].MovementFlags:X8} pos=({f.Position.X:F1},{f.Position.Y:F1},{f.Position.Z:F1})");
                }
                totalCollisionFrames += collisionFrames.Count;
            }
        }

        _output.WriteLine($"\nTotal collision frames: {totalCollisionFrames}");
    }

    [Fact]
    public void Collision_DetectDirectionDeflection()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        int totalDeflections = 0;
        foreach (var (name, rec) in recordings)
        {
            var deflections = new List<(int index, float angleDiffDeg)>();
            for (int i = 1; i < rec.Frames.Count; i++)
            {
                if (rec.Frames[i - 1].MovementFlags != Forward) continue;
                float dt = (rec.Frames[i].FrameTimestamp - rec.Frames[i - 1].FrameTimestamp) / 1000.0f;
                if (dt <= 0) continue;

                float dx = rec.Frames[i].Position.X - rec.Frames[i - 1].Position.X;
                float dy = rec.Frames[i].Position.Y - rec.Frames[i - 1].Position.Y;
                if (MathF.Sqrt(dx * dx + dy * dy) < 0.01f) continue;

                float moveAngle = MathF.Atan2(dy, dx);
                float facing = rec.Frames[i - 1].Facing;
                float expectedAngle = MathF.Atan2(MathF.Sin(facing), MathF.Cos(facing));
                float diff = MathF.Abs(NormalizeAngle(moveAngle - expectedAngle)) * 180.0f / MathF.PI;

                if (diff > 15.0f) deflections.Add((i, diff));
            }

            if (deflections.Count > 0)
            {
                _output.WriteLine($"  {name}: {deflections.Count} deflection frames");
                foreach (var (idx, diff) in deflections.OrderByDescending(d => d.angleDiffDeg).Take(3))
                    _output.WriteLine($"    Frame {idx}: deflection={diff:F1} degrees");
                totalDeflections += deflections.Count;
            }
        }

        _output.WriteLine($"\nTotal deflection frames: {totalDeflections}");
    }

    // ==========================================================================
    // SLOPE
    // ==========================================================================

    [Fact]
    public void Slope_DetectSteepTerrainFrames()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        foreach (var (name, rec) in recordings)
        {
            var slopeFrames = new List<(int index, float grade, float speed)>();
            for (int i = 1; i < rec.Frames.Count; i++)
            {
                uint flags = rec.Frames[i - 1].MovementFlags;
                if ((flags & (Jumping | FallingFar | Swimming)) != 0) continue;
                if ((flags & DirectionalMask) == 0) continue;

                float dt = (rec.Frames[i].FrameTimestamp - rec.Frames[i - 1].FrameTimestamp) / 1000.0f;
                if (dt <= 0) continue;

                float dx = rec.Frames[i].Position.X - rec.Frames[i - 1].Position.X;
                float dy = rec.Frames[i].Position.Y - rec.Frames[i - 1].Position.Y;
                float dz = rec.Frames[i].Position.Z - rec.Frames[i - 1].Position.Z;
                float horizDist = MathF.Sqrt(dx * dx + dy * dy);
                if (horizDist < 0.01f) continue;

                float grade = dz / horizDist;
                if (MathF.Abs(grade) > 0.3f)
                    slopeFrames.Add((i, grade, horizDist / dt));
            }

            if (slopeFrames.Count > 0)
            {
                float maxAngle = MathF.Atan(slopeFrames.Max(s => MathF.Abs(s.grade))) * 180.0f / MathF.PI;
                int uphill = slopeFrames.Count(s => s.grade > 0);
                int downhill = slopeFrames.Count(s => s.grade < 0);
                _output.WriteLine($"  {name}: {slopeFrames.Count} slope frames (up={uphill}, down={downhill}, maxAngle={maxAngle:F1} degrees)");

                var rejected = slopeFrames.Where(s => s.speed < 2.0f && s.grade > 0.5f).ToList();
                if (rejected.Count > 0)
                    _output.WriteLine($"    Slope REJECTION candidates: {rejected.Count} frames");
            }
        }
    }

    // ==========================================================================
    // FREE-FALL
    // ==========================================================================

    [Fact]
    public void FreeFall_AnalyzeGravityFromLongFalls()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        foreach (var (name, rec) in recordings)
        {
            var fallingFrames = rec.Frames.Where(f => (f.MovementFlags & FallingFar) != 0).ToList();
            if (fallingFrames.Count < 10) continue;

            var longFalls = FindConsecutiveSegments(fallingFrames, maxGapMs: 100)
                .Where(s => s.Count >= 10 && s[0].Position.Z - s[^1].Position.Z > 3.0f)
                .ToList();

            if (longFalls.Count == 0) continue;

            _output.WriteLine($"  {name}: {longFalls.Count} long fall(s)");
            foreach (var (seg, idx) in longFalls.Select((s, i) => (s, i)))
            {
                float totalFall = seg[0].Position.Z - seg[^1].Position.Z;
                float elapsed = (seg[^1].FrameTimestamp - seg[0].FrameTimestamp) / 1000.0f;
                if (elapsed < 0.3f) continue;

                float fittedGravity = FitGravityFromSegment(seg);

                _output.WriteLine($"    Fall {idx}: {totalFall:F2}y over {elapsed:F3}s, gravity={fittedGravity:F4} y/s^2");
                Assert.True(MathF.Abs(fittedGravity - PhysicsTestConstants.Gravity) < 3.0f,
                    $"Gravity mismatch in fall {idx}: fitted={fittedGravity:F4}, expected={PhysicsTestConstants.Gravity:F4}");
            }
        }
    }

    [Fact]
    public void FreeFall_CheckForTerminalVelocity()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        float maxObservedFallSpeed = 0;
        string maxFallRecording = "";

        foreach (var (name, rec) in recordings)
        {
            var fallingFrames = rec.Frames.Where(f => (f.MovementFlags & AirborneMask) != 0).ToList();
            foreach (var seg in FindConsecutiveSegments(fallingFrames, maxGapMs: 100).Where(s => s.Count >= 20))
            {
                float elapsed = (seg[^1].FrameTimestamp - seg[0].FrameTimestamp) / 1000.0f;
                if (elapsed < 1.0f) continue;

                int lastQuarterStart = seg.Count * 3 / 4;
                var lateVelocities = new List<float>();
                for (int i = lastQuarterStart + 1; i < seg.Count; i++)
                {
                    float dt = (seg[i].FrameTimestamp - seg[i - 1].FrameTimestamp) / 1000.0f;
                    if (dt <= 0) continue;
                    lateVelocities.Add(MathF.Abs((seg[i].Position.Z - seg[i - 1].Position.Z) / dt));
                }

                if (lateVelocities.Count < 3) continue;
                float lateMean = lateVelocities.Average();
                if (lateMean > maxObservedFallSpeed)
                {
                    maxObservedFallSpeed = lateMean;
                    maxFallRecording = name;
                }

                _output.WriteLine($"  {name}: {elapsed:F2}s fall, late speed={lateMean:F2} y/s");
            }
        }

        if (maxObservedFallSpeed > 0)
            _output.WriteLine($"\nMax fall speed: {maxObservedFallSpeed:F2} y/s ({maxFallRecording})");
        else
            _output.WriteLine("No long falls found. Jump off a tall building with /say rec active!");
    }

    // ==========================================================================
    // RECORDING HEALTH
    // ==========================================================================

    [Fact]
    public void RecordingHealth_AllRecordingsHaveValidSpeeds()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        foreach (var (name, rec) in recordings)
        {
            var first = rec.Frames.FirstOrDefault();
            if (first == null) continue;

            if (first.RunSpeed <= 0 || first.WalkSpeed <= 0)
                _output.WriteLine($"  WARNING {name}: RunSpeed={first.RunSpeed:F2}, WalkSpeed={first.WalkSpeed:F2}");

            int bad = rec.Frames.Count(f => !float.IsFinite(f.Position.X) || !float.IsFinite(f.Position.Y) || !float.IsFinite(f.Position.Z));
            if (bad > 0) _output.WriteLine($"  WARNING {name}: {bad} frames with NaN/Inf");

            if (rec.Race == 0) _output.WriteLine($"  WARNING {name}: No race metadata");
        }

        _output.WriteLine($"\nChecked {recordings.Count} recordings");
    }

    [Fact]
    public void RecordingHealth_FlagDistribution()
    {
        var recordings = LoadAllRecordings(_output);
        if (recordings.Count == 0) { _output.WriteLine("SKIP: No recordings found"); return; }

        foreach (var (name, rec) in recordings)
        {
            var flags = new Dictionary<string, int>
            {
                ["Forward"] = rec.Frames.Count(f => (f.MovementFlags & Forward) != 0),
                ["Backward"] = rec.Frames.Count(f => (f.MovementFlags & Backward) != 0),
                ["StrafeL"] = rec.Frames.Count(f => (f.MovementFlags & StrafeLeft) != 0),
                ["StrafeR"] = rec.Frames.Count(f => (f.MovementFlags & StrafeRight) != 0),
                ["Falling"] = rec.Frames.Count(f => (f.MovementFlags & Jumping) != 0),
                ["FallingFar"] = rec.Frames.Count(f => (f.MovementFlags & FallingFar) != 0),
                ["Swimming"] = rec.Frames.Count(f => (f.MovementFlags & Swimming) != 0),
                ["Stationary"] = rec.Frames.Count(f => f.MovementFlags == 0)
            };

            var active = flags.Where(kv => kv.Value > 0).ToList();
            if (active.Count > 0)
                _output.WriteLine($"  {name} ({rec.Frames.Count}): {string.Join(", ", active.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
    }

    // ==========================================================================
    // PRIVATE HELPERS
    // ==========================================================================

    private static float FitGravityFromSegment(List<RecordedFrame> seg)
    {
        float startZ = seg[0].Position.Z;
        double st2 = 0, st3 = 0, st4 = 0, stz = 0, st2z = 0;

        for (int i = 1; i < seg.Count; i++)
        {
            double t = (seg[i].FrameTimestamp - seg[0].FrameTimestamp) / 1000.0;
            double zOff = seg[i].Position.Z - startZ;
            st2 += t * t; st3 += t * t * t; st4 += t * t * t * t;
            stz += t * zOff; st2z += t * t * zOff;
        }

        double det = st2 * st4 - st3 * st3;
        if (Math.Abs(det) < 1e-10) return 0;

        double b = (st2 * st2z - st3 * stz) / det;
        return (float)(-b * 2.0);
    }
}
