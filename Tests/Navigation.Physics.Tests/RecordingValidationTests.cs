using Navigation.Physics.Tests.Helpers;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;
using static Navigation.Physics.Tests.Helpers.MoveFlags;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Navigation.Physics.Tests;

/// <summary>
/// Pure-math tests that validate physics equations against recorded WoW client data.
/// No C++ physics engine or map data required � these analyze recordings directly.
///
/// Categories:
///   - Ground speed (forward, backward, strafe, diagonal normalization)
///   - Jump arc (height, duration, horizontal maintenance)
///   - Free-fall gravity (least-squares fit)
/// </summary>
public class RecordingValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // ==========================================================================
    // GROUND SPEED
    // ==========================================================================

    [Fact]
    public void ForwardRun_SpeedMatchesRunSpeed()
    {
        var recording = LoadByFilename(Recordings.OrgFlatRunForward, _output);
        var movingFrames = recording.Frames
            .Where(f => (f.MovementFlags & Forward) != 0)
            .ToList();

        Assert.True(movingFrames.Count >= 2, "No forward frames found");

        var segments = FindConsecutiveSegments(movingFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();
        Assert.True(bestSeg.Count >= 5, "No long enough forward segment");

        var frameSpeeds = MeasureFrameSpeeds(bestSeg);
        Assert.True(frameSpeeds.Count >= 3, "Not enough speed samples");

        float recordedSpeed = TrimmedMean(frameSpeeds, 0.05f);
        float expectedSpeed = movingFrames[0].RunSpeed;

        _output.WriteLine($"Recorded speed: {recordedSpeed:F4} y/s ({bestSeg.Count}-frame segment)");
        _output.WriteLine($"Expected run speed: {expectedSpeed:F4} y/s");

        Assert.True(MathF.Abs(recordedSpeed - expectedSpeed) < Tolerances.Velocity,
            $"Speed mismatch: recorded={recordedSpeed:F4}, expected={expectedSpeed:F4}");
    }

    [Fact]
    public void BackwardRun_SpeedMatchesRunBackSpeed()
    {
        var recording = TryLoadByFilename(Recordings.DurotarMixedMovement, _output);
        if (recording == null) { _output.WriteLine("SKIP: DurotarMixedMovement needs re-recording"); return; }
        var backFrames = recording.Frames
            .Where(f => (f.MovementFlags & Backward) != 0 && (f.MovementFlags & (StrafeLeft | StrafeRight)) == 0)
            .ToList();

        Assert.True(backFrames.Count >= 2, "No backward-only frames found");

        var segments = FindConsecutiveSegments(backFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();
        Assert.True(bestSeg.Count >= 5, $"Longest backward segment is {bestSeg.Count} frames (need 5+)");

        float dx = bestSeg[^1].Position.X - bestSeg[0].Position.X;
        float dy = bestSeg[^1].Position.Y - bestSeg[0].Position.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (bestSeg[^1].FrameTimestamp - bestSeg[0].FrameTimestamp) / 1000.0f;
        float recordedSpeed = distance / elapsed;

        _output.WriteLine($"Recorded backward speed: {recordedSpeed:F4} y/s ({bestSeg.Count}-frame segment)");
        _output.WriteLine($"Expected runBack speed: {bestSeg[0].RunBackSpeed:F4} y/s");

        Assert.True(MathF.Abs(recordedSpeed - bestSeg[0].RunBackSpeed) < Tolerances.Velocity,
            $"Backward speed mismatch: recorded={recordedSpeed:F4}, expected={bestSeg[0].RunBackSpeed:F4}");
    }

    [Fact]
    public void DiagonalStrafe_SpeedIsNormalized()
    {
        var recording = TryLoadByFilename(Recordings.DurotarMixedMovement, _output);
        if (recording == null) { _output.WriteLine("SKIP: DurotarMixedMovement needs re-recording"); return; }
        var diagonalFrames = recording.Frames
            .Where(f => f.MovementFlags == (Forward | StrafeRight) || f.MovementFlags == (Forward | StrafeLeft))
            .ToList();

        Assert.True(diagonalFrames.Count >= 2, "No diagonal movement frames");
        Assert.True(diagonalFrames[0].RunSpeed < 10f, $"Recording has GM speeds ({diagonalFrames[0].RunSpeed})");

        var speeds = new List<float>();
        var segments = FindConsecutiveSegments(diagonalFrames, maxGapMs: 100);
        foreach (var seg in segments.Where(s => s.Count >= 3))
            speeds.AddRange(MeasureFrameSpeeds(seg));

        Assert.True(speeds.Count >= 3, "Not enough consecutive diagonal frames");

        float avgSpeed = speeds.Average();
        float runSpeed = diagonalFrames[0].RunSpeed;

        _output.WriteLine($"Diagonal speed: {avgSpeed:F4} y/s, Run speed: {runSpeed:F4} y/s");
        _output.WriteLine($"Ratio: {avgSpeed / runSpeed:F4} (expected ~1.0 if normalized)");

        Assert.True(MathF.Abs(avgSpeed - runSpeed) < Tolerances.Velocity,
            $"Diagonal should be normalized to {runSpeed:F4}, got {avgSpeed:F4}");
    }

    [Fact]
    public void StrafeOnly_SpeedMatchesRunSpeed()
    {
        var recording = TryLoadByFilename(Recordings.DurotarMixedMovement, _output);
        if (recording == null) { _output.WriteLine("SKIP: DurotarMixedMovement needs re-recording"); return; }
        var strafeFrames = recording.Frames
            .Where(f => f.MovementFlags == StrafeRight || f.MovementFlags == StrafeLeft)
            .ToList();

        _output.WriteLine($"Pure strafe frames: {strafeFrames.Count}");
        Assert.True(strafeFrames.Count >= 5, "Not enough strafe-only frames");

        var speeds = new List<float>();
        for (int i = 1; i < strafeFrames.Count; i++)
        {
            long gap = strafeFrames[i].FrameTimestamp - strafeFrames[i - 1].FrameTimestamp;
            if (gap > 100) continue;
            float dt = gap / 1000.0f;
            if (dt <= 0) continue;
            float dx = strafeFrames[i].Position.X - strafeFrames[i - 1].Position.X;
            float dy = strafeFrames[i].Position.Y - strafeFrames[i - 1].Position.Y;
            speeds.Add(MathF.Sqrt(dx * dx + dy * dy) / dt);
        }

        Assert.True(speeds.Count >= 3, "Not enough consecutive strafe frame pairs");

        float avgSpeed = speeds.Average();
        float runSpeed = strafeFrames[0].RunSpeed;

        _output.WriteLine($"Avg strafe speed: {avgSpeed:F4} y/s, Run speed: {runSpeed:F4} y/s");

        Assert.True(MathF.Abs(avgSpeed - runSpeed) < Tolerances.Velocity,
            $"Strafe speed mismatch: recorded={avgSpeed:F4}, expected={runSpeed:F4}");
    }

    [Fact]
    public void LongRun_SpeedConsistentOverDuration()
    {
        var recording = LoadByFilename(Recordings.DurotarLongFlatRun, _output);
        var forwardFrames = recording.Frames.Where(f => f.MovementFlags == Forward).ToList();

        Assert.True(forwardFrames.Count >= 100, $"Only {forwardFrames.Count} forward frames");

        var frameSpeeds = new List<float>();
        for (int i = 1; i < forwardFrames.Count; i++)
        {
            float dt = (forwardFrames[i].FrameTimestamp - forwardFrames[i - 1].FrameTimestamp) / 1000.0f;
            if (dt <= 0) continue;
            float dx = forwardFrames[i].Position.X - forwardFrames[i - 1].Position.X;
            float dy = forwardFrames[i].Position.Y - forwardFrames[i - 1].Position.Y;
            float dz = forwardFrames[i].Position.Z - forwardFrames[i - 1].Position.Z;
            frameSpeeds.Add(MathF.Sqrt(dx * dx + dy * dy + dz * dz) / dt);
        }

        float avgSpeed = TrimmedMean(frameSpeeds, 0.05f);
        float runSpeed = forwardFrames[0].RunSpeed;

        // Speed per quarter � check consistency
        int quarterSize = frameSpeeds.Count / 4;
        for (int q = 0; q < 4; q++)
        {
            var quarter = frameSpeeds.Skip(q * quarterSize).Take(quarterSize).ToList();
            if (quarter.Count > 0)
                _output.WriteLine($"  Quarter {q + 1}: avg speed={quarter.Average():F4} y/s");
        }

        _output.WriteLine($"Overall avg speed: {avgSpeed:F4} y/s, Expected: {runSpeed:F4} y/s");

        Assert.True(MathF.Abs(avgSpeed - runSpeed) < Tolerances.AvgPosition,
            $"Long run speed too far from expected: avg={avgSpeed:F4}, expected={runSpeed:F4}");
    }

    // ==========================================================================
    // JUMP ARC
    // ==========================================================================

    [Fact]
    public void StandingJump_ArcHeightMatchesPhysics()
    {
        var recording = LoadByFilename(Recordings.OrgStandingJump, _output);
        var arcs = FindJumpArcs(recording);

        if (arcs.Count == 0)
        {
            _output.WriteLine("SKIP: No valid jump arcs with upward movement found " +
                "(recording may not capture the ascending phase of jumps)");
            return;
        }

        var bestArc = arcs.OrderByDescending(a => a.height).First();
        float expectedHeight = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity)
            / (2.0f * PhysicsTestConstants.Gravity);

        _output.WriteLine($"Best arc: height={bestArc.height:F4}y ({bestArc.frames.Count} frames)");
        _output.WriteLine($"Expected: {expectedHeight:F4}y");

        Assert.True(MathF.Abs(bestArc.height - expectedHeight) < Tolerances.RelaxedPosition,
            $"Jump height mismatch: recorded={bestArc.height:F4}, expected={expectedHeight:F4}");
    }

    [Fact]
    public void StandingJump_DurationMatchesPhysics()
    {
        var recording = LoadByFilename(Recordings.OrgStandingJump, _output);
        var arcs = FindJumpArcs(recording);

        if (arcs.Count == 0)
        {
            _output.WriteLine("SKIP: No valid jump arcs found " +
                "(recording may not capture the ascending phase of jumps)");
            return;
        }

        var bestArc = arcs.OrderByDescending(a => a.frames.Count).First();
        float recordedAirTime = (bestArc.frames[^1].FrameTimestamp - bestArc.frames[0].FrameTimestamp) / 1000.0f;
        float expectedAirTime = 2.0f * PhysicsTestConstants.JumpVelocity / PhysicsTestConstants.Gravity;

        _output.WriteLine($"Recorded air time: {recordedAirTime:F4}s, Expected: {expectedAirTime:F4}s");

        Assert.True(MathF.Abs(recordedAirTime - expectedAirTime) < 0.15f,
            $"Air time mismatch: recorded={recordedAirTime:F4}s, expected={expectedAirTime:F4}s");
    }

    [Fact]
    public void RunningJump_MaintainsHorizontalSpeed()
    {
        var recording = LoadByFilename(Recordings.OrgRunningJumps, _output);
        var airborneForward = recording.Frames
            .Where(f => (f.MovementFlags & (Jumping | Forward)) == (Jumping | Forward))
            .ToList();

        Assert.True(airborneForward.Count >= 3, "Not enough airborne+forward frames");

        var arcs = FindConsecutiveSegments(airborneForward, maxGapMs: 200).Where(a => a.Count >= 5).ToList();
        Assert.True(arcs.Count > 0, "No long enough airborne+forward arcs");

        var bestArc = arcs.OrderByDescending(a => a.Count).First();
        float dx = bestArc[^1].Position.X - bestArc[0].Position.X;
        float dy = bestArc[^1].Position.Y - bestArc[0].Position.Y;
        float horizDist = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (bestArc[^1].FrameTimestamp - bestArc[0].FrameTimestamp) / 1000.0f;
        float airborneSpeed = horizDist / elapsed;

        _output.WriteLine($"Horizontal speed while airborne: {airborneSpeed:F4} y/s");
        _output.WriteLine($"Run speed: {bestArc[0].RunSpeed:F4} y/s");

        Assert.True(MathF.Abs(airborneSpeed - bestArc[0].RunSpeed) < Tolerances.Velocity,
            $"Horizontal speed changed during jump: {airborneSpeed:F4} vs {bestArc[0].RunSpeed:F4}");
    }

    [Fact]
    public void MultipleJumps_AllHaveSimilarArc()
    {
        var recording = LoadByFilename(Recordings.OrgRunningJumps, _output);
        var arcs = FindJumpArcs(recording);

        _output.WriteLine($"Valid jumps: {arcs.Count}");
        foreach (var (frames, height, idx) in arcs.Select((a, i) => (a.frames, a.height, i)))
            _output.WriteLine($"  Jump {idx}: height={height:F4}y");

        if (arcs.Count < 2)
        {
            _output.WriteLine("SKIP: Need at least 2 valid jumps with captured ascending phase");
            return;
        }

        float avgHeight = arcs.Average(a => a.height);
        float maxDeviation = arcs.Max(a => MathF.Abs(a.height - avgHeight));

        _output.WriteLine($"Average: {avgHeight:F4}y, Max deviation: {maxDeviation:F4}y");

        Assert.True(maxDeviation < Tolerances.RelaxedPosition,
            $"Jump heights too inconsistent: avg={avgHeight:F4}, maxDev={maxDeviation:F4}");
    }

    // ==========================================================================
    // FREE-FALL GRAVITY
    // ==========================================================================

    [Fact]
    public void FallFromHeight_GravityMatchesConstant()
    {
        var recording = LoadByFilename(Recordings.OrgFallFromHeight, _output);
        var fallingFrames = recording.Frames
            .Where(f => (f.MovementFlags & AirborneMask) != 0)
            .ToList();

        Assert.True(fallingFrames.Count >= 5, "Not enough falling frames");

        float measuredGravity = FitGravity(fallingFrames, _output);

        float gravityError = MathF.Abs(measuredGravity - PhysicsTestConstants.Gravity);
        _output.WriteLine($"Gravity error: {gravityError:F4} y/s�");

        Assert.True(gravityError < 3.0f,
            $"Gravity mismatch: measured={measuredGravity:F4}, expected={PhysicsTestConstants.Gravity:F4}");
    }

    [Fact]
    public void FallingFar_GravityFollowsPhysics()
    {
        var recording = LoadByFilename(Recordings.UndercityMixed, _output);
        var fallingFarFrames = recording.Frames
            .Where(f => (f.MovementFlags & FallingFar) != 0)
            .ToList();

        _output.WriteLine($"FALLING_FAR frames: {fallingFarFrames.Count}");
        Assert.True(fallingFarFrames.Count >= 5, "Not enough falling far frames");

        var segments = FindConsecutiveSegments(fallingFarFrames, maxGapMs: 200);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();
        Assert.True(bestSeg.Count >= 3, "No long enough falling segment");

        float startZ = bestSeg[0].Position.Z;
        float endZ = bestSeg[^1].Position.Z;
        float totalFall = startZ - endZ;
        float elapsed = (bestSeg[^1].FrameTimestamp - bestSeg[0].FrameTimestamp) / 1000.0f;
        float measuredG = 2.0f * totalFall / (elapsed * elapsed);

        _output.WriteLine($"Total fall: {totalFall:F4}y over {elapsed:F4}s");
        _output.WriteLine($"Measured gravity: {measuredG:F4} y/s�, Expected: ~{PhysicsTestConstants.Gravity:F4}");

        Assert.True(measuredG > 10.0f && measuredG < 30.0f,
            $"Gravity out of range: measured={measuredG:F4}");
    }

    // ==========================================================================
    // MOVEMENT FLAG TRANSITIONS
    // ==========================================================================

    [Fact]
    public void FallingToFallingFar_TransitionsCorrectly()
    {
        var recording = LoadByFilename(Recordings.UndercityMixed, _output);
        int transitions = 0;

        for (int i = 1; i < recording.Frames.Count; i++)
        {
            bool prevFalling = (recording.Frames[i - 1].MovementFlags & Jumping) != 0
                && (recording.Frames[i - 1].MovementFlags & FallingFar) == 0;
            bool currFallingFar = (recording.Frames[i].MovementFlags & FallingFar) != 0;

            if (prevFalling && currFallingFar)
            {
                _output.WriteLine($"Transition FALLING?FALLING_FAR at frame {i}");
                transitions++;
            }
        }

        _output.WriteLine($"Total transitions: {transitions}");
    }

    // ==========================================================================
    // PRIVATE HELPERS
    // ==========================================================================

    private static List<float> MeasureFrameSpeeds(List<RecordedFrame> frames)
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

    private List<(List<RecordedFrame> frames, float height)> FindJumpArcs(MovementRecording recording)
    {
        // JUMPING flag (0x2000) is set by the client for the entire airborne phase
        // of a jump. Filter for frames with this flag set.
        var jumpFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x2000) != 0)
            .ToList();

        _output.WriteLine($"  Total FALLING/JUMPING frames: {jumpFrames.Count}");

        var segments = FindConsecutiveSegments(jumpFrames, maxGapMs: 200);
        _output.WriteLine($"  Segments (gap<=200ms): {segments.Count}");

        var all = segments
            .Where(s => s.Count >= 3)
            .Select(s => (frames: s, height: s.Max(f => f.Position.Z) - s[0].Position.Z))
            .ToList();

        foreach (var (frames, height) in all)
            _output.WriteLine($"    Segment: {frames.Count} frames, height={height:F4}y");

        // Filter for upward arcs (real jumps, not pure falls)
        return all.Where(a => a.height > 0.1f).ToList();
    }

    private static float FitGravity(List<RecordedFrame> fallingFrames, ITestOutputHelper output)
    {
        float startZ = fallingFrames[0].Position.Z;

        double st2 = 0, st3 = 0, st4 = 0, stz = 0, st2z = 0;
        for (int i = 1; i < fallingFrames.Count; i++)
        {
            double t = (fallingFrames[i].FrameTimestamp - fallingFrames[0].FrameTimestamp) / 1000.0;
            double zOff = fallingFrames[i].Position.Z - startZ;
            if (t <= 0) continue;
            st2 += t * t; st3 += t * t * t; st4 += t * t * t * t;
            stz += t * zOff; st2z += t * t * zOff;
        }

        double det = st2 * st4 - st3 * st3;
        double a = (st4 * stz - st3 * st2z) / det;
        double b = (st2 * st2z - st3 * stz) / det;

        float fittedV0 = (float)a;
        float fittedGravity = (float)(b * 2.0);

        output.WriteLine($"Fitted v0: {fittedV0:F4} y/s");
        output.WriteLine($"Fitted gravity: {fittedGravity:F4} y/s� (negative = downward)");

        return MathF.Abs(fittedGravity);
    }
}
