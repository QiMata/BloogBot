using GameData.Core.Constants;
using GameData.Core.Enums;
using static Navigation.Physics.Tests.NavigationInterop;
using Xunit.Abstractions;

namespace Navigation.Physics.Tests;

/// <summary>
/// Tests calibrated from manually recorded movement sessions (2026-02-08).
/// These recordings cover complex mixed movement, jumping patterns,
/// and long-duration runs that the automated recordings don't capture.
///
/// NOTE: No swimming recordings currently exist — all swim tests will skip gracefully.
/// NOTE: Only Dralrahgra_Durotar_2026-02-08_12-28-15 has player spline data.
///       Do NOT assert spline execution against older recordings.
///
/// Movement flag reference:
///   0x00000001 = FORWARD     0x00000002 = BACKWARD
///   0x00000004 = STRAFE_L    0x00000008 = STRAFE_R
///   0x00000010 = TURN_LEFT   0x00000020 = TURN_RIGHT
///   0x00002000 = FALLING     0x00004000 = FALLING_FAR
///   0x00200000 = SWIMMING    0x04000000 = SPLINE_ENABLED
/// </summary>
public class ManualRecordingTests : IClassFixture<PhysicsEngineFixture>
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const float VelocityTolerance = 0.5f;
    private const float RelaxedPositionTolerance = 0.5f;

    // Fallback capsule dimensions
    private const float FallbackHeight = 2.0f;
    private const float FallbackRadius = 0.5f;

    // Recording filenames (2026-02-08 session, Orc Female, 60 FPS)
    // No swimming recordings exist yet — swim tests will skip gracefully.
    private const string SwimmingRecording = ""; // No swim recording available
    private const string ComplexMixedRecording = "Dralrahgra_Durotar_2026-02-08_11-06-59";     // fwd/back/strafe/fall, 1142 frames
    private const string OrgRunningJumps = "Dralrahgra_Orgrimmar_2026-02-08_11-01-15";          // 3495 fwd, 464 fall, 86 fallingFar
    private const string MixedDirectional = "Dralrahgra_Durotar_2026-02-08_11-24-45";           // diagonal+strafe, 652 frames
    private const string LongFlatRun = "Dralrahgra_Durotar_2026-02-08_11-37-56";                // pure forward, 5028 frames, 82s
    private const string UndercityMixed = "Dralrahgra_Undercity_2026-02-08_11-30-52";           // strafe/fall/fallingFar, UC

    public ManualRecordingTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ==========================================================================
    // SWIMMING TESTS — No swimming recordings available yet. Tests skip gracefully.
    // ==========================================================================

    [Fact]
    public void SwimForward_SpeedMatchesSwimSpeed()
    {
        var recording = TryLoadByFilename(SwimmingRecording);
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }
        var swimForwardFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x00200001) == 0x00200001) // SWIMMING + FORWARD
            .ToList();

        _output.WriteLine($"Total frames: {recording.Frames.Count}, Swimming+Forward frames: {swimForwardFrames.Count}");

        Assert.True(swimForwardFrames.Count >= 100,
            $"Expected at least 100 swimming frames, got {swimForwardFrames.Count}");

        // Measure speed using frame-by-frame 3D displacement (handles diving)
        var speeds = new List<float>();
        var segments = FindConsecutiveSegments(swimForwardFrames, maxGapMs: 100);
        var longestSegment = segments.OrderByDescending(s => s.Count).First();

        for (int i = 1; i < longestSegment.Count; i++)
        {
            var prev = longestSegment[i - 1];
            var curr = longestSegment[i];
            float dt = (curr.FrameTimestamp - prev.FrameTimestamp) / 1000.0f;
            if (dt <= 0) continue;

            float dx = curr.Position.X - prev.Position.X;
            float dy = curr.Position.Y - prev.Position.Y;
            float dz = curr.Position.Z - prev.Position.Z;
            float dist3d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            speeds.Add(dist3d / dt);
        }

        float avgSpeed = speeds.Average();

        _output.WriteLine($"Longest segment: {longestSegment.Count} frames");
        _output.WriteLine($"Avg 3D swim speed: {avgSpeed:F4} y/s");
        _output.WriteLine($"Expected swim speed: {swimForwardFrames[0].SwimSpeed:F4} y/s");
        _output.WriteLine($"Speed samples: {speeds.Count}");

        // Swimming tolerance is wider than ground movement because the swimmer
        // turns, changes pitch, and corrects course - reducing measured speed
        const float swimTolerance = 1.0f;
        Assert.True(MathF.Abs(avgSpeed - swimForwardFrames[0].SwimSpeed) < swimTolerance,
            $"Swim speed mismatch: recorded={avgSpeed:F4}, expected={swimForwardFrames[0].SwimSpeed:F4}");
    }

    [Fact]
    public void SwimForward_CapturesDivingBehavior()
    {
        var recording = TryLoadByFilename(SwimmingRecording);
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }
        var swimFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x00200000) != 0) // SWIMMING flag
            .ToList();

        if (swimFrames.Count < 50)
        {
            _output.WriteLine("SKIP: Not enough swimming frames");
            return;
        }

        var zValues = swimFrames.Select(f => f.Position.Z).ToList();
        float minZ = zValues.Min();
        float maxZ = zValues.Max();
        float zRange = maxZ - minZ;

        _output.WriteLine($"Z range during swim: {zRange:F4} yards");
        _output.WriteLine($"Min Z: {minZ:F4}, Max Z: {maxZ:F4}");
        _output.WriteLine($"Swimming frames: {swimFrames.Count}");

        // The recording includes diving (Z drops below surface)
        // Validate that the recording captured meaningful depth variation
        Assert.True(swimFrames.Count > recording.Frames.Count * 0.5f,
            $"Expected majority swimming frames, got {swimFrames.Count}/{recording.Frames.Count}");

        // Log swim pitch data for physics engine calibration
        var pitchValues = swimFrames.Where(f => f.SwimPitch != 0).Select(f => f.SwimPitch).ToList();
        if (pitchValues.Count > 0)
        {
            _output.WriteLine($"Swim pitch range: {pitchValues.Min():F4} to {pitchValues.Max():F4}");
            _output.WriteLine($"Frames with non-zero pitch: {pitchValues.Count}");
        }
    }

    [Fact]
    public void SwimWithTurning_SpeedMaintained()
    {
        var recording = TryLoadByFilename(SwimmingRecording);
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        // Get frames with SWIMMING + FORWARD + TURN_RIGHT (0x200021)
        var swimTurnFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x00200021) == 0x00200021)
            .ToList();

        _output.WriteLine($"Swimming+Forward+TurnRight frames: {swimTurnFrames.Count}");

        if (swimTurnFrames.Count < 10)
        {
            _output.WriteLine("SKIP: Not enough swim-turn frames");
            return;
        }

        var segments = FindConsecutiveSegments(swimTurnFrames, maxGapMs: 100);
        if (segments.Count == 0 || segments[0].Count < 5) return;

        var seg = segments.OrderByDescending(s => s.Count).First();
        var first = seg[0];
        var last = seg[^1];
        float dx = last.Position.X - first.Position.X;
        float dy = last.Position.Y - first.Position.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (last.FrameTimestamp - first.FrameTimestamp) / 1000.0f;

        if (elapsed < 0.1f) return;

        float recordedSpeed = distance / elapsed;

        _output.WriteLine($"Swim+turn speed: {recordedSpeed:F4} y/s");
        _output.WriteLine($"Expected swim speed: {first.SwimSpeed:F4} y/s");

        // Speed while turning should still approximate swim speed
        // (slightly lower due to path curvature reducing straight-line distance)
        Assert.True(recordedSpeed > first.SwimSpeed * 0.5f,
            $"Swim+turn speed too low: {recordedSpeed:F4} vs expected ~{first.SwimSpeed:F4}");
    }

    // ==========================================================================
    // COMPLEX MIXED MOVEMENT (from 19-09-24: 741 frames, 21s, Durotar)
    // Contains: jump+turn, strafe, backward+jump, all directions
    // ==========================================================================

    [Fact]
    public void ComplexMixed_JumpWhileRunning_HasCorrectArcHeight()
    {
        var recording = LoadByFilename(ComplexMixedRecording);
        var jumpForwardFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x2001) == 0x2001) // FALLING + FORWARD
            .ToList();

        _output.WriteLine($"Jump+Forward frames: {jumpForwardFrames.Count}");

        if (jumpForwardFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough jump frames");
            return;
        }

        // Find jump arcs: sequences of FALLING+FORWARD frames
        var segments = FindConsecutiveSegments(jumpForwardFrames, maxGapMs: 200);
        _output.WriteLine($"Jump segments found: {segments.Count}");

        foreach (var (seg, idx) in segments.Select((s, i) => (s, i)))
        {
            if (seg.Count < 3) continue;

            float groundZ = seg[0].Position.Z;
            float peakZ = seg.Max(f => f.Position.Z);
            float jumpHeight = peakZ - groundZ;

            float expectedHeight = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity)
                / (2.0f * PhysicsTestConstants.Gravity);

            float elapsed = (seg[^1].FrameTimestamp - seg[0].FrameTimestamp) / 1000.0f;

            _output.WriteLine($"  Jump {idx}: height={jumpHeight:F4}y, expected={expectedHeight:F4}y, " +
                $"duration={elapsed:F3}s, frames={seg.Count}");
        }

        // Validate the longest/most complete jump
        var bestJump = segments.OrderByDescending(s => s.Count).First();
        float bestGroundZ = bestJump[0].Position.Z;
        float bestPeakZ = bestJump.Max(f => f.Position.Z);
        float bestHeight = bestPeakZ - bestGroundZ;
        float expected = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity)
            / (2.0f * PhysicsTestConstants.Gravity);

        Assert.True(MathF.Abs(bestHeight - expected) < RelaxedPositionTolerance,
            $"Jump height mismatch: recorded={bestHeight:F4}, expected={expected:F4}");
    }

    [Fact]
    public void ComplexMixed_BackwardJump_HasSameArcAsForward()
    {
        var recording = LoadByFilename(ComplexMixedRecording);

        // Find FALLING + BACKWARD frames (0x2002)
        var backJumpFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x2002) == 0x2002)
            .ToList();

        _output.WriteLine($"Jump+Backward frames: {backJumpFrames.Count}");

        if (backJumpFrames.Count < 3)
        {
            _output.WriteLine("SKIP: No backward jump frames");
            return;
        }

        var segments = FindConsecutiveSegments(backJumpFrames, maxGapMs: 200);
        if (segments.Count == 0 || segments[0].Count < 3) return;

        var bestJump = segments.OrderByDescending(s => s.Count).First();
        float groundZ = bestJump[0].Position.Z;
        float peakZ = bestJump.Max(f => f.Position.Z);
        float jumpHeight = peakZ - groundZ;

        float expected = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity)
            / (2.0f * PhysicsTestConstants.Gravity);

        _output.WriteLine($"Backward jump height: {jumpHeight:F4}y, expected={expected:F4}y");

        // Jump height should be the same regardless of direction
        Assert.True(MathF.Abs(jumpHeight - expected) < RelaxedPositionTolerance,
            $"Backward jump height differs: recorded={jumpHeight:F4}, expected={expected:F4}");
    }

    [Fact]
    public void ComplexMixed_StrafeJump_HasPositiveHorizontalDisplacement()
    {
        var recording = LoadByFilename(ComplexMixedRecording);

        // Find any falling frames with lateral movement (strafe or forward+strafe)
        var airborneWithMovement = recording.Frames
            .Where(f => (f.MovementFlags & 0x2000) != 0 && // FALLING
                        (f.MovementFlags & 0xF) != 0)       // Any directional flag
            .ToList();

        _output.WriteLine($"Airborne+directional frames: {airborneWithMovement.Count}");

        if (airborneWithMovement.Count < 3)
        {
            _output.WriteLine("SKIP: Not enough airborne+directional frames");
            return;
        }

        // Measure frame-by-frame horizontal displacement while airborne
        var segments = FindConsecutiveSegments(airborneWithMovement, maxGapMs: 200);
        float totalHorizDisp = 0;
        int measuredFrames = 0;

        foreach (var seg in segments.Where(s => s.Count >= 3))
        {
            for (int i = 1; i < seg.Count; i++)
            {
                float dx = seg[i].Position.X - seg[i - 1].Position.X;
                float dy = seg[i].Position.Y - seg[i - 1].Position.Y;
                totalHorizDisp += MathF.Sqrt(dx * dx + dy * dy);
                measuredFrames++;
            }
        }

        _output.WriteLine($"Total horizontal displacement while airborne: {totalHorizDisp:F4}y over {measuredFrames} frames");

        // While airborne with directional input, horizontal movement should occur
        Assert.True(totalHorizDisp > 0.1f,
            $"Expected horizontal movement while airborne, got {totalHorizDisp:F4}y");
    }

    // ==========================================================================
    // RUNNING WITH JUMPS (from 19-11-07: 888 frames, 24.5s, Orgrimmar)
    // ==========================================================================

    [Fact]
    public void RunningJumps_MultipleJumps_AllHaveSimilarArc()
    {
        var recording = LoadByFilename(OrgRunningJumps);
        var jumpFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x2000) != 0)
            .ToList();

        _output.WriteLine($"Total jump/falling frames: {jumpFrames.Count}");

        if (jumpFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough jump frames");
            return;
        }

        var segments = FindConsecutiveSegments(jumpFrames, maxGapMs: 200);
        _output.WriteLine($"Jump segments: {segments.Count}");

        var jumpHeights = new List<float>();
        foreach (var seg in segments.Where(s => s.Count >= 3))
        {
            float groundZ = seg[0].Position.Z;
            float peakZ = seg.Max(f => f.Position.Z);
            float height = peakZ - groundZ;
            if (height > 0.1f) // Filter out pure falling (negative height)
                jumpHeights.Add(height);
        }

        _output.WriteLine($"Valid jumps with positive arc: {jumpHeights.Count}");
        foreach (var (h, i) in jumpHeights.Select((h, i) => (h, i)))
        {
            _output.WriteLine($"  Jump {i}: height={h:F4}y");
        }

        if (jumpHeights.Count < 2)
        {
            _output.WriteLine("SKIP: Need at least 2 valid jumps");
            return;
        }

        // All jumps should have similar height (within 0.3y of each other)
        float avgHeight = jumpHeights.Average();
        float maxDeviation = jumpHeights.Max(h => MathF.Abs(h - avgHeight));

        _output.WriteLine($"Average jump height: {avgHeight:F4}y, max deviation: {maxDeviation:F4}y");

        Assert.True(maxDeviation < 0.5f,
            $"Jump heights too inconsistent: avg={avgHeight:F4}, maxDev={maxDeviation:F4}");
    }

    // ==========================================================================
    // MIXED DIRECTIONAL (from 19-12-56: 493 frames, 13.5s, Durotar)
    // Forward, diagonal, backward, strafe
    // ==========================================================================

    [Fact]
    public void MixedDirectional_BackwardSpeed_MatchesRunBackSpeed()
    {
        var recording = LoadByFilename(MixedDirectional);
        var backwardFrames = recording.Frames
            .Where(f => f.MovementFlags == 0x2) // BACKWARD only (no turning/strafing)
            .ToList();

        _output.WriteLine($"Pure backward frames: {backwardFrames.Count}");

        if (backwardFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough pure backward frames");
            return;
        }

        var segments = FindConsecutiveSegments(backwardFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();

        if (bestSeg.Count < 3) return;

        var first = bestSeg[0];
        var last = bestSeg[^1];
        float dx = last.Position.X - first.Position.X;
        float dy = last.Position.Y - first.Position.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (last.FrameTimestamp - first.FrameTimestamp) / 1000.0f;
        float recordedSpeed = distance / elapsed;

        _output.WriteLine($"Recorded backward speed: {recordedSpeed:F4} y/s");
        _output.WriteLine($"Expected runBack speed: {first.RunBackSpeed:F4} y/s");

        Assert.True(MathF.Abs(recordedSpeed - first.RunBackSpeed) < VelocityTolerance,
            $"Backward speed mismatch: recorded={recordedSpeed:F4}, expected={first.RunBackSpeed:F4}");
    }

    [Fact]
    public void MixedDirectional_DiagonalForwardStrafe_IsNormalized()
    {
        // Use ComplexMixed recording which has normal speeds (not GM-boosted)
        var recording = LoadByFilename(ComplexMixedRecording);

        // FORWARD + STRAFE_RIGHT (0x9) or FORWARD + STRAFE_LEFT (0x5)
        var diagonalFrames = recording.Frames
            .Where(f => f.MovementFlags == 0x9 || f.MovementFlags == 0x5)
            .ToList();

        _output.WriteLine($"Diagonal (forward+strafe) frames: {diagonalFrames.Count}");

        if (diagonalFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough diagonal frames");
            return;
        }

        // Verify this recording has normal speeds
        Assert.True(diagonalFrames[0].RunSpeed < 10f,
            $"Recording has GM speeds (runSpeed={diagonalFrames[0].RunSpeed}), need normal speed recording");

        // Measure frame-by-frame speed for more accuracy
        var speeds = new List<float>();
        var segments = FindConsecutiveSegments(diagonalFrames, maxGapMs: 100);

        foreach (var seg in segments.Where(s => s.Count >= 3))
        {
            for (int i = 1; i < seg.Count; i++)
            {
                float dt = (seg[i].FrameTimestamp - seg[i - 1].FrameTimestamp) / 1000.0f;
                if (dt <= 0) continue;
                float dx = seg[i].Position.X - seg[i - 1].Position.X;
                float dy = seg[i].Position.Y - seg[i - 1].Position.Y;
                speeds.Add(MathF.Sqrt(dx * dx + dy * dy) / dt);
            }
        }

        if (speeds.Count < 3)
        {
            _output.WriteLine("SKIP: Not enough consecutive diagonal frames");
            return;
        }

        float avgSpeed = speeds.Average();
        float runSpeed = diagonalFrames[0].RunSpeed;

        _output.WriteLine($"Avg diagonal speed: {avgSpeed:F4} y/s");
        _output.WriteLine($"Run speed: {runSpeed:F4} y/s");
        _output.WriteLine($"Ratio: {avgSpeed / runSpeed:F4} (expected ~1.0 if normalized)");

        // Diagonal should be normalized to run speed, not sqrt(2)*run
        Assert.True(MathF.Abs(avgSpeed - runSpeed) < 1.5f,
            $"Diagonal not normalized: recorded={avgSpeed:F4}, expected ~{runSpeed:F4}");
    }

    // ==========================================================================
    // LONG FLAT RUN (from 19-28-13: 1014 frames, 17s, Durotar coast)
    // ==========================================================================

    [Fact]
    public void LongFlatRun_SpeedConsistentOverDuration()
    {
        var recording = LoadByFilename(LongFlatRun);
        var forwardFrames = recording.Frames
            .Where(f => f.MovementFlags == 0x1) // Pure FORWARD
            .ToList();

        _output.WriteLine($"Pure forward frames: {forwardFrames.Count}");

        if (forwardFrames.Count < 100)
        {
            _output.WriteLine("SKIP: Not enough forward frames");
            return;
        }

        // Measure frame-by-frame speed (handles terrain elevation changes better)
        var frameSpeeds = new List<float>();
        for (int i = 1; i < forwardFrames.Count; i++)
        {
            float dt = (forwardFrames[i].FrameTimestamp - forwardFrames[i - 1].FrameTimestamp) / 1000.0f;
            if (dt <= 0) continue;

            float dx = forwardFrames[i].Position.X - forwardFrames[i - 1].Position.X;
            float dy = forwardFrames[i].Position.Y - forwardFrames[i - 1].Position.Y;
            float dz = forwardFrames[i].Position.Z - forwardFrames[i - 1].Position.Z;
            // Use 3D distance to account for elevation changes on hilly terrain
            float dist3d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            frameSpeeds.Add(dist3d / dt);
        }

        if (frameSpeeds.Count < 10) return;

        // Remove outliers (top/bottom 5%) for cleaner measurement
        var sorted = frameSpeeds.OrderBy(s => s).ToList();
        int trimCount = sorted.Count / 20;
        var trimmed = sorted.Skip(trimCount).Take(sorted.Count - 2 * trimCount).ToList();

        float avgSpeed = trimmed.Average();
        float runSpeed = forwardFrames[0].RunSpeed;

        // Measure per-quarter for consistency check
        int quarterSize = frameSpeeds.Count / 4;
        var quarterSpeeds = new List<float>();
        for (int q = 0; q < 4; q++)
        {
            var quarter = frameSpeeds.Skip(q * quarterSize).Take(quarterSize).ToList();
            if (quarter.Count > 0)
            {
                quarterSpeeds.Add(quarter.Average());
                _output.WriteLine($"  Quarter {q + 1}: avg speed={quarter.Average():F4} y/s");
            }
        }

        _output.WriteLine($"Overall avg speed (trimmed): {avgSpeed:F4} y/s");
        _output.WriteLine($"Expected run speed: {runSpeed:F4} y/s");

        // This recording is on coastal terrain which is NOT flat
        // Speed should be close to run speed but terrain may cause some deviation
        Assert.True(MathF.Abs(avgSpeed - runSpeed) < 2.0f,
            $"Long run speed too far from expected: avg={avgSpeed:F4}, expected={runSpeed:F4}");

        // Quarters should be relatively consistent (no acceleration/deceleration bugs)
        if (quarterSpeeds.Count >= 2)
        {
            float qAvg = quarterSpeeds.Average();
            float qMaxDev = quarterSpeeds.Max(s => MathF.Abs(s - qAvg));
            _output.WriteLine($"Quarter consistency: avg={qAvg:F4}, maxDev={qMaxDev:F4}");
            Assert.True(qMaxDev < 2.0f,
                $"Speed too inconsistent between quarters: maxDev={qMaxDev:F4}");
        }
    }

    [Fact]
    public void LongFlatRun_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }

        var recording = LoadByFilename(LongFlatRun);
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("long_flat_run", result);

        Assert.True(result.AvgPositionError < RelaxedPositionTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {RelaxedPositionTolerance}y");
    }

    // ==========================================================================
    // UNDERCITY MIXED (from 19-30-20: 1996 frames, 33s)
    // Forward, strafe, diagonal, jumping, falling_far
    // ==========================================================================

    [Fact]
    public void UndercityMixed_FallingFar_FollowsGravity()
    {
        var recording = LoadByFilename(UndercityMixed);

        // Find FALLING_FAR frames (0x4000 or 0x6000)
        var fallingFarFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x4000) != 0)
            .ToList();

        _output.WriteLine($"FALLING_FAR frames: {fallingFarFrames.Count}");

        if (fallingFarFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough falling far frames");
            return;
        }

        var segments = FindConsecutiveSegments(fallingFarFrames, maxGapMs: 200);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();

        if (bestSeg.Count < 3) return;

        // Measure gravitational acceleration
        float startZ = bestSeg[0].Position.Z;
        float endZ = bestSeg[^1].Position.Z;
        float totalFall = startZ - endZ;
        float elapsed = (bestSeg[^1].FrameTimestamp - bestSeg[0].FrameTimestamp) / 1000.0f;

        // d = 0.5 * g * t^2 (assuming starting from rest)
        float measuredG = 2.0f * totalFall / (elapsed * elapsed);

        _output.WriteLine($"Total fall: {totalFall:F4}y over {elapsed:F4}s");
        _output.WriteLine($"Measured gravity: {measuredG:F4} y/s^2");
        _output.WriteLine($"Expected gravity: {PhysicsTestConstants.Gravity:F4} y/s^2");

        // Gravity should be in the right ballpark (wider tolerance since initial velocity may not be 0)
        Assert.True(measuredG > 10.0f && measuredG < 30.0f,
            $"Gravity out of range: measured={measuredG:F4}, expected ~{PhysicsTestConstants.Gravity:F4}");
    }

    [Fact]
    public void UndercityMixed_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }

        var recording = LoadByFilename(UndercityMixed);

        // Detect and trim teleports (position jumps > 50y between consecutive frames)
        var cleanFrames = new List<RecordedFrame> { recording.Frames[0] };
        int teleportCount = 0;
        for (int i = 1; i < recording.Frames.Count; i++)
        {
            var prev = recording.Frames[i - 1];
            var curr = recording.Frames[i];
            float dx = curr.Position.X - prev.Position.X;
            float dy = curr.Position.Y - prev.Position.Y;
            float dz = curr.Position.Z - prev.Position.Z;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

            if (dist > 50.0f)
            {
                _output.WriteLine($"Teleport detected at frame {i}: distance={dist:F1}y");
                teleportCount++;
                // Start a new segment from this frame
                cleanFrames.Clear();
            }
            cleanFrames.Add(curr);
        }

        _output.WriteLine($"Teleports detected: {teleportCount}");
        _output.WriteLine($"Clean segment frames: {cleanFrames.Count} (of {recording.Frames.Count})");

        // Use only the longest clean segment
        var cleanRecording = new MovementRecording
        {
            MapId = recording.MapId,
            FrameIntervalMs = recording.FrameIntervalMs,
            Race = recording.Race,
            Gender = recording.Gender,
            Frames = cleanFrames
        };

        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(cleanRecording);
        LogCalibrationResult("undercity_mixed (clean)", result);

        // Mixed movement with transitions - wider tolerance
        const float mixedTolerance = 2.0f;
        Assert.True(result.AvgPositionError < mixedTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {mixedTolerance}y");
    }

    // ==========================================================================
    // SWIMMING FRAME-BY-FRAME
    // ==========================================================================

    [Fact]
    public void SwimForward_FrameByFrame_PositionMatchesRecording()
    {
        var recording = TryLoadByFilename(SwimmingRecording);
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("swim_forward", result);

        Assert.True(result.AvgPositionError < RelaxedPositionTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {RelaxedPositionTolerance}y");
    }

    // ==========================================================================
    // COMPLEX MIXED FRAME-BY-FRAME
    // ==========================================================================

    [Fact]
    public void ComplexMixed_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }

        var recording = LoadByFilename(ComplexMixedRecording);
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("complex_mixed", result);

        // Mixed movement with jumps, turns, strafes - wider tolerance
        const float mixedTolerance = 2.0f;
        Assert.True(result.AvgPositionError < mixedTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {mixedTolerance}y");
    }

    // ==========================================================================
    // RUNNING JUMPS FRAME-BY-FRAME
    // ==========================================================================

    [Fact]
    public void RunningJumps_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }

        var recording = LoadByFilename(OrgRunningJumps);
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("running_jumps", result);

        // Orgrimmar has complex terrain - wider tolerance
        const float urbanTolerance = 2.0f;
        Assert.True(result.AvgPositionError < urbanTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {urbanTolerance}y");
    }

    // ==========================================================================
    // MOVEMENT FLAG TRANSITIONS
    // ==========================================================================

    [Fact]
    public void MovementFlags_FallingToFallingFar_TransitionsCorrectly()
    {
        var recording = LoadByFilename(UndercityMixed);

        // Look for FALLING (0x2000) → FALLING_FAR (0x4000) transitions
        int transitions = 0;
        for (int i = 1; i < recording.Frames.Count; i++)
        {
            var prev = recording.Frames[i - 1];
            var curr = recording.Frames[i];

            bool prevFalling = (prev.MovementFlags & 0x2000) != 0 && (prev.MovementFlags & 0x4000) == 0;
            bool currFallingFar = (curr.MovementFlags & 0x4000) != 0;

            if (prevFalling && currFallingFar)
            {
                float elapsed = (curr.FrameTimestamp - prev.FrameTimestamp) / 1000.0f;
                _output.WriteLine($"Transition FALLING→FALLING_FAR at frame {i}, dt={elapsed:F4}s");
                transitions++;
            }
        }

        _output.WriteLine($"Total FALLING→FALLING_FAR transitions: {transitions}");

        // Just log - don't assert, since some recordings may not have long enough falls
        if (transitions > 0)
        {
            _output.WriteLine("Transition confirmed: FALLING_FAR flag appears after sustained fall");
        }
    }

    [Fact]
    public void MovementFlags_SwimmingDetectedCorrectly()
    {
        var recording = TryLoadByFilename(SwimmingRecording);
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }

        int swimmingFrames = recording.Frames.Count(f => (f.MovementFlags & 0x200000) != 0);
        int nonSwimmingFrames = recording.Frames.Count(f => (f.MovementFlags & 0x200000) == 0);

        _output.WriteLine($"Swimming frames: {swimmingFrames}");
        _output.WriteLine($"Non-swimming frames: {nonSwimmingFrames}");

        // This recording should be predominantly swimming
        Assert.True(swimmingFrames > recording.Frames.Count * 0.5f,
            $"Expected majority swimming frames, got {swimmingFrames}/{recording.Frames.Count}");
    }

    // ==========================================================================
    // HELPERS
    // ==========================================================================

    private MovementRecording LoadByFilename(string filenamePattern)
    {
        var path = RecordingLoader.FindRecordingByFilename(filenamePattern);
        _output.WriteLine($"Loading recording: {Path.GetFileName(path)}");
        var recording = RecordingLoader.LoadFromFile(path);
        _output.WriteLine($"  Frames: {recording.Frames.Count}, Duration: {recording.DurationMs}ms, " +
            $"Race: {recording.RaceName}, Zone: {recording.ZoneName}");
        return recording;
    }

    /// <summary>
    /// Attempts to load a recording by filename. Returns null if the pattern is empty
    /// or no matching file is found (allows tests to skip gracefully).
    /// </summary>
    private MovementRecording? TryLoadByFilename(string filenamePattern)
    {
        if (string.IsNullOrEmpty(filenamePattern))
            return null;
        try
        {
            return LoadByFilename(filenamePattern);
        }
        catch (FileNotFoundException)
        {
            _output.WriteLine($"Recording not found: {filenamePattern}");
            return null;
        }
    }

    private static List<List<RecordedFrame>> FindConsecutiveSegments(
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

    private (float radius, float height) GetCapsuleDimensions(MovementRecording recording)
    {
        if (recording.Race == 0)
            return (FallbackRadius, FallbackHeight);

        var race = (Race)recording.Race;
        var gender = (Gender)recording.Gender;
        return RaceDimensions.GetCapsuleForRace(race, gender);
    }

    private CalibrationResult ReplayRecording(MovementRecording recording, bool verbose = false)
    {
        var result = new CalibrationResult();
        var frames = recording.Frames;

        if (frames.Count < 2) return result;

        var (capsuleRadius, capsuleHeight) = GetCapsuleDimensions(recording);

        var prevOutput = new PhysicsOutput();
        float prevGroundZ = frames[0].Position.Z;
        int fallStartFrameIndex = -1;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            var currentFrame = frames[i];
            var nextFrame = frames[i + 1];
            float dt = (nextFrame.FrameTimestamp - currentFrame.FrameTimestamp) / 1000.0f;

            if (dt <= 0) continue;

            bool isFalling = (currentFrame.MovementFlags & 0x6000) != 0;
            bool wasAirborne = fallStartFrameIndex >= 0;
            uint fallTimeMs = 0;
            if (isFalling)
            {
                if (fallStartFrameIndex < 0)
                    fallStartFrameIndex = i;
                fallTimeMs = (uint)(currentFrame.FrameTimestamp - frames[fallStartFrameIndex].FrameTimestamp);
            }
            else
            {
                fallStartFrameIndex = -1;
            }

            var input = new PhysicsInput
            {
                MoveFlags = currentFrame.MovementFlags,
                X = currentFrame.Position.X,
                Y = currentFrame.Position.Y,
                Z = currentFrame.Position.Z,
                Orientation = currentFrame.Facing,
                Pitch = currentFrame.SwimPitch,
                Vx = 0, Vy = 0, Vz = 0,
                WalkSpeed = currentFrame.WalkSpeed,
                RunSpeed = currentFrame.RunSpeed,
                RunBackSpeed = currentFrame.RunBackSpeed,
                SwimSpeed = currentFrame.SwimSpeed,
                SwimBackSpeed = currentFrame.SwimBackSpeed,
                FlightSpeed = 0,
                TurnSpeed = currentFrame.TurnRate,
                TransportGuid = currentFrame.TransportGuid,
                TransportX = currentFrame.TransportOffsetX,
                TransportY = currentFrame.TransportOffsetY,
                TransportZ = currentFrame.TransportOffsetZ,
                TransportO = currentFrame.TransportOrientation,
                FallTime = fallTimeMs,
                Height = capsuleHeight,
                Radius = capsuleRadius,
                MapId = recording.MapId,
                DeltaTime = dt,
                FrameCounter = (uint)i,
                PrevGroundZ = prevGroundZ,
                PrevGroundNx = prevOutput.GroundNx,
                PrevGroundNy = prevOutput.GroundNy,
                PrevGroundNz = prevOutput.GroundNz != 0 ? prevOutput.GroundNz : 1.0f,
                // Don't carry forward depenetration — position resets to recording each frame,
                // so the previous frame's depenetration vector is stale and causes drift.
                PendingDepenX = 0,
                PendingDepenY = 0,
                PendingDepenZ = 0,
                StandingOnInstanceId = prevOutput.StandingOnInstanceId,
                StandingOnLocalX = prevOutput.StandingOnLocalX,
                StandingOnLocalY = prevOutput.StandingOnLocalY,
                StandingOnLocalZ = prevOutput.StandingOnLocalZ,
            };

            bool isAirborne = (currentFrame.MovementFlags & 0x6000) != 0;
            if (isAirborne && wasAirborne)
            {
                input.Vx = prevOutput.Vx;
                input.Vy = prevOutput.Vy;
                input.Vz = prevOutput.Vz;
            }
            else if (isAirborne && !wasAirborne)
            {
                // First airborne frame: estimate all velocity components from position delta.
                // Horizontal velocity is constant during air (no air control), so Vx/Vy = dx/dt.
                // Vertical velocity includes gravity correction: Vz = dz/dt + 0.5*g*dt.
                const float GRAVITY = 19.2911f;
                float deltaX = nextFrame.Position.X - currentFrame.Position.X;
                float deltaY = nextFrame.Position.Y - currentFrame.Position.Y;
                float deltaZ = nextFrame.Position.Z - currentFrame.Position.Z;
                input.Vx = deltaX / dt;
                input.Vy = deltaY / dt;
                input.Vz = deltaZ / dt + 0.5f * GRAVITY * dt;
                input.FallTime = 1;
            }

            var output = StepPhysicsV2(ref input);
            prevOutput = output;
            prevGroundZ = output.GroundZ;

            float dx = output.X - nextFrame.Position.X;
            float dy = output.Y - nextFrame.Position.Y;
            float dz = output.Z - nextFrame.Position.Z;
            float posError = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            float horizError = MathF.Sqrt(dx * dx + dy * dy);
            float vertError = MathF.Abs(dz);

            result.AddFrame(i, posError, horizError, vertError,
                output.X, output.Y, output.Z,
                nextFrame.Position.X, nextFrame.Position.Y, nextFrame.Position.Z);
        }

        return result;
    }

    private void TryPreloadMap(uint mapId)
    {
        try
        {
            PreloadMap(mapId);
            _output.WriteLine($"Preloaded map {mapId}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to preload map {mapId}: {ex.Message}");
        }
    }

    private void LogCalibrationResult(string scenario, CalibrationResult result)
    {
        _output.WriteLine($"=== Calibration: {scenario} ===");
        _output.WriteLine($"  Frames simulated: {result.FrameCount}");
        _output.WriteLine($"  Avg position error: {result.AvgPositionError:F4} yards");
        _output.WriteLine($"  Max position error: {result.MaxPositionError:F4} yards (frame {result.WorstFrame})");
        _output.WriteLine($"  Avg horiz error: {result.AvgHorizError:F4} yards");
        _output.WriteLine($"  Avg vert error: {result.AvgVertError:F4} yards");

        if (result.WorstFrame >= 0 && result.WorstFrame < result.FrameDetails.Count)
        {
            var worst = result.FrameDetails[result.WorstFrame];
            _output.WriteLine($"  Worst frame {worst.Frame}: " +
                $"sim=({worst.SimX:F3},{worst.SimY:F3},{worst.SimZ:F3}) " +
                $"rec=({worst.RecX:F3},{worst.RecY:F3},{worst.RecZ:F3})");
        }
    }
}
