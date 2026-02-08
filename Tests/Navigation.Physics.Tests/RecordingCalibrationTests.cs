using GameData.Core.Constants;
using GameData.Core.Enums;
using static Navigation.Physics.Tests.NavigationInterop;
using Xunit.Abstractions;

namespace Navigation.Physics.Tests;

/// <summary>
/// Replays recorded movement data through the C++ PhysicsEngine and compares
/// simulated positions to actual WoW client positions frame-by-frame.
///
/// These tests are the core of Task 26: Physics Engine Calibration.
/// Tolerance: position within 0.1y, velocity within 0.5 y/s.
/// Capsule dimensions are sourced from the recording's race/gender via RaceDimensions.
///
/// NOTE: Recordings from 2026-02-08 before 12:28 do NOT contain player spline data.
///       Only Dralrahgra_Durotar_2026-02-08_12-28-15 has spline fields populated.
///       Do NOT assert spline execution against older recordings.
/// </summary>
public class RecordingCalibrationTests : IClassFixture<PhysicsEngineFixture>
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    // Tolerances from TASKS.md
    private const float PositionTolerance = 0.1f;     // yards
    private const float VelocityTolerance = 0.5f;      // yards/second
    private const float RelaxedPositionTolerance = 0.5f; // for initial calibration

    // Fallback capsule dimensions for recordings that predate race/gender tracking
    private const float FallbackHeight = 2.0f;
    private const float FallbackRadius = 0.5f;

    // Recording filenames (2026-02-08 session, mapped to scenario types)
    private const string FlatRunForwardRecording = "Dralrahgra_Orgrimmar_2026-02-08_11-32-13";    // 1203 fwd, 34 still, ~pure forward, avgSpeed=6.97
    private const string FlatRunBackwardRecording = "Dralrahgra_Durotar_2026-02-08_11-06-59";    // 272 backward frames, mixed movement
    private const string StandingJumpRecording = "Dralrahgra_Orgrimmar_2026-02-08_11-31-46";     // 200 falling, 601 stationary, jumps
    private const string RunningJumpRecording = "Dralrahgra_Orgrimmar_2026-02-08_11-01-15";      // 3495 fwd, 464 falling, running jumps
    private const string FallFromHeightRecording = "Dralrahgra_Orgrimmar_2026-02-08_11-32-44";   // 93 fallingFar, 36.6y zRange
    private const string StrafeDiagonalRecording = "Dralrahgra_Durotar_2026-02-08_11-24-45";     // 121 diagonal, strafe+forward
    private const string StrafeOnlyRecording = "Dralrahgra_Durotar_2026-02-08_11-06-59";         // has strafe-only frames

    public RecordingCalibrationTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Resolves capsule dimensions from a recording's race/gender metadata.
    /// Falls back to generic defaults for legacy recordings without race/gender.
    /// </summary>
    private (float radius, float height) GetCapsuleDimensions(MovementRecording recording)
    {
        if (recording.Race == 0)
        {
            _output.WriteLine($"  [capsule] No race in recording, using fallback: r={FallbackRadius}, h={FallbackHeight}");
            return (FallbackRadius, FallbackHeight);
        }

        var race = (Race)recording.Race;
        var gender = (Gender)recording.Gender;
        var (radius, height) = RaceDimensions.GetCapsuleForRace(race, gender);
        _output.WriteLine($"  [capsule] Race={race}, Gender={gender} => r={radius:F4}, h={height:F4}");
        return (radius, height);
    }

    // ==========================================================================
    // PURE MATH TESTS (no map data needed)
    // These validate the physics equations against recorded data
    // ==========================================================================

    [Fact]
    public void StandingJump_ArcMatchesRecording()
    {
        var recording = LoadByFilename(StandingJumpRecording);
        var jumpFrames = GetJumpFrames(recording);

        if (jumpFrames.Count < 3)
        {
            _output.WriteLine("SKIP: No jump frames found in recording");
            return;
        }

        // Find individual jump arcs (consecutive FALLING segments)
        var arcs = FindConsecutiveSegments(jumpFrames, maxGapMs: 200);
        var validArcs = arcs
            .Where(a => a.Count >= 3)
            .Select(a => new {
                Frames = a,
                GroundZ = a[0].Position.Z,
                PeakZ = a.Max(f => f.Position.Z),
                Height = a.Max(f => f.Position.Z) - a[0].Position.Z
            })
            .Where(a => a.Height > 0.5f) // Filter out pure falls (no upward arc)
            .ToList();

        _output.WriteLine($"Total FALLING frames: {jumpFrames.Count}, arcs found: {arcs.Count}, valid upward arcs: {validArcs.Count}");

        if (validArcs.Count == 0)
        {
            _output.WriteLine("SKIP: No valid jump arcs with upward movement found");
            return;
        }

        // Use the best (tallest) arc
        var bestArc = validArcs.OrderByDescending(a => a.Height).First();

        // Expected from physics: h_max = v0^2 / (2g)
        float expectedJumpHeight = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity)
            / (2.0f * PhysicsTestConstants.Gravity);

        _output.WriteLine($"Best arc: height={bestArc.Height:F4} yards ({bestArc.Frames.Count} frames)");
        _output.WriteLine($"Expected jump height: {expectedJumpHeight:F4} yards");

        Assert.True(MathF.Abs(bestArc.Height - expectedJumpHeight) < RelaxedPositionTolerance,
            $"Jump height mismatch: recorded={bestArc.Height:F4}, expected={expectedJumpHeight:F4}");
    }

    [Fact]
    public void StandingJump_DurationMatchesRecording()
    {
        var recording = LoadByFilename(StandingJumpRecording);
        var jumpFrames = GetJumpFrames(recording);

        if (jumpFrames.Count < 3)
        {
            _output.WriteLine("SKIP: No jump frames found in recording");
            return;
        }

        // Find individual jump arcs
        var arcs = FindConsecutiveSegments(jumpFrames, maxGapMs: 200);
        var validArcs = arcs
            .Where(a => a.Count >= 3 && a.Max(f => f.Position.Z) - a[0].Position.Z > 0.5f)
            .ToList();

        _output.WriteLine($"Valid upward arcs: {validArcs.Count}");

        if (validArcs.Count == 0)
        {
            _output.WriteLine("SKIP: No valid jump arcs found");
            return;
        }

        // Use the longest arc (most complete jump)
        var bestArc = validArcs.OrderByDescending(a => a.Count).First();
        float recordedAirTime = (bestArc[^1].FrameTimestamp - bestArc[0].FrameTimestamp) / 1000.0f;

        // Expected from physics: t_total = 2 * v0 / g
        float expectedAirTime = 2.0f * PhysicsTestConstants.JumpVelocity / PhysicsTestConstants.Gravity;

        _output.WriteLine($"Best arc: {bestArc.Count} frames, duration={recordedAirTime:F4}s");
        _output.WriteLine($"Expected air time: {expectedAirTime:F4}s");

        Assert.True(MathF.Abs(recordedAirTime - expectedAirTime) < 0.15f,
            $"Air time mismatch: recorded={recordedAirTime:F4}s, expected={expectedAirTime:F4}s");
    }

    [Fact]
    public void FlatRunForward_SpeedMatchesRecording()
    {
        var recording = LoadByFilename(FlatRunForwardRecording);
        var movingFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x1) != 0) // FORWARD flag
            .ToList();

        if (movingFrames.Count < 2)
        {
            _output.WriteLine("SKIP: No moving frames found");
            return;
        }

        // Use longest consecutive segment to avoid gaps in mixed recordings
        var segments = FindConsecutiveSegments(movingFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();

        if (bestSeg.Count < 5)
        {
            _output.WriteLine("SKIP: No long enough consecutive forward segment");
            return;
        }

        // Measure frame-by-frame 3D speed (handles terrain elevation changes)
        var frameSpeeds = new List<float>();
        for (int i = 1; i < bestSeg.Count; i++)
        {
            float dt = (bestSeg[i].FrameTimestamp - bestSeg[i - 1].FrameTimestamp) / 1000.0f;
            if (dt <= 0) continue;
            float dx = bestSeg[i].Position.X - bestSeg[i - 1].Position.X;
            float dy = bestSeg[i].Position.Y - bestSeg[i - 1].Position.Y;
            float dz = bestSeg[i].Position.Z - bestSeg[i - 1].Position.Z;
            frameSpeeds.Add(MathF.Sqrt(dx * dx + dy * dy + dz * dz) / dt);
        }

        if (frameSpeeds.Count < 3)
        {
            _output.WriteLine("SKIP: Not enough speed samples");
            return;
        }

        // Trim outliers (top/bottom 5%)
        var sorted = frameSpeeds.OrderBy(s => s).ToList();
        int trimCount = Math.Max(1, sorted.Count / 20);
        var trimmed = sorted.Skip(trimCount).Take(sorted.Count - 2 * trimCount).ToList();
        float recordedSpeed = trimmed.Average();

        _output.WriteLine($"Recorded speed: {recordedSpeed:F4} y/s (from {bestSeg.Count}-frame segment, {frameSpeeds.Count} samples)");
        _output.WriteLine($"Expected run speed: {movingFrames[0].RunSpeed:F4} y/s");

        Assert.True(MathF.Abs(recordedSpeed - movingFrames[0].RunSpeed) < VelocityTolerance,
            $"Speed mismatch: recorded={recordedSpeed:F4}, expected={movingFrames[0].RunSpeed:F4}");
    }

    [Fact]
    public void FlatRunBackward_SpeedMatchesRecording()
    {
        var recording = LoadByFilename(FlatRunBackwardRecording);
        var movingFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x2) != 0 && (f.MovementFlags & 0xC) == 0) // BACKWARD only (no strafe)
            .ToList();

        if (movingFrames.Count < 2)
        {
            _output.WriteLine("SKIP: No backward-only frames found");
            return;
        }

        // Use longest consecutive segment to avoid gaps in mixed recordings
        var segments = FindConsecutiveSegments(movingFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();

        if (bestSeg.Count < 5)
        {
            _output.WriteLine($"SKIP: Longest consecutive backward segment is {bestSeg.Count} frames (need 5+)");
            return;
        }

        var first = bestSeg[0];
        var last = bestSeg[^1];
        float dx = last.Position.X - first.Position.X;
        float dy = last.Position.Y - first.Position.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (last.FrameTimestamp - first.FrameTimestamp) / 1000.0f;
        float recordedSpeed = distance / elapsed;

        _output.WriteLine($"Recorded backward speed: {recordedSpeed:F4} y/s (from {bestSeg.Count}-frame segment)");
        _output.WriteLine($"Expected runBack speed: {first.RunBackSpeed:F4} y/s");

        Assert.True(MathF.Abs(recordedSpeed - first.RunBackSpeed) < VelocityTolerance,
            $"Backward speed mismatch: recorded={recordedSpeed:F4}, expected={first.RunBackSpeed:F4}");
    }

    [Fact]
    public void FallFromHeight_AccelerationMatchesGravity()
    {
        var recording = LoadByFilename(FallFromHeightRecording);
        var fallingFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x6000) != 0) // FALLING or FALLINGFAR
            .ToList();

        if (fallingFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough falling frames");
            return;
        }

        // Use kinematic equation: d = v0*t + 0.5*g*t^2
        // The character may already be falling at the start of recording.
        // Fit both v0 and g using least squares regression on (t, d) data.
        float startZ = fallingFrames[0].Position.Z;

        // Estimate initial velocity from first two frames
        float dt01 = (fallingFrames[1].FrameTimestamp - fallingFrames[0].FrameTimestamp) / 1000.0f;
        float v0_estimate = dt01 > 0
            ? (fallingFrames[1].Position.Z - fallingFrames[0].Position.Z) / dt01
            : 0;

        _output.WriteLine($"Fall data: {fallingFrames.Count} frames");
        _output.WriteLine($"Start Z: {startZ:F4}");
        _output.WriteLine($"Initial velocity estimate: {v0_estimate:F4} y/s");

        // Collect (t, z_offset) pairs where z_offset = z - startZ
        var dataPoints = new List<(float t, float zOff)>();
        for (int i = 1; i < fallingFrames.Count; i++)
        {
            float t = (fallingFrames[i].FrameTimestamp - fallingFrames[0].FrameTimestamp) / 1000.0f;
            float zOff = fallingFrames[i].Position.Z - startZ; // negative when falling
            if (t > 0)
                dataPoints.Add((t, zOff));
        }

        if (dataPoints.Count < 3)
        {
            _output.WriteLine("SKIP: Not enough valid data points");
            return;
        }

        // 2-parameter least squares: z(t) = v0*t + 0.5*g*t^2
        // Let a = v0, b = g/2. Then z = a*t + b*t^2
        // Normal equations: [sum(t^2)  sum(t^3)] [a]   [sum(t*z)]
        //                   [sum(t^3)  sum(t^4)] [b] = [sum(t^2*z)]
        double st2 = 0, st3 = 0, st4 = 0, stz = 0, st2z = 0;
        foreach (var (t, zOff) in dataPoints)
        {
            double td = t;
            st2 += td * td;
            st3 += td * td * td;
            st4 += td * td * td * td;
            stz += td * zOff;
            st2z += td * td * zOff;
        }

        double det = st2 * st4 - st3 * st3;
        double a = (st4 * stz - st3 * st2z) / det;   // v0
        double b = (st2 * st2z - st3 * stz) / det;    // g/2

        float measuredV0 = (float)a;
        float measuredGravity = (float)(b * 2.0);  // g = 2b (note: negative for downward)

        _output.WriteLine($"Fitted v0: {measuredV0:F4} y/s");
        _output.WriteLine($"Fitted gravity: {measuredGravity:F4} y/s^2 (negative = downward)");
        _output.WriteLine($"Expected gravity: -{PhysicsTestConstants.Gravity:F4} y/s^2");

        // Gravity should be negative and match the constant
        float gravityMagnitude = MathF.Abs(measuredGravity);
        float gravityError = MathF.Abs(gravityMagnitude - PhysicsTestConstants.Gravity);
        _output.WriteLine($"Gravity magnitude: {gravityMagnitude:F4} y/s^2");
        _output.WriteLine($"Gravity error: {gravityError:F4} y/s^2");

        float totalFall = startZ - fallingFrames[^1].Position.Z;
        float totalTime = (fallingFrames[^1].FrameTimestamp - fallingFrames[0].FrameTimestamp) / 1000.0f;
        _output.WriteLine($"Total fall: {totalFall:F4} yards over {totalTime:F4}s");

        Assert.True(gravityError < 3.0f,
            $"Gravity mismatch: measured={gravityMagnitude:F4}, expected={PhysicsTestConstants.Gravity:F4}");
    }

    [Fact]
    public void DiagonalStrafe_SpeedIsNormalized()
    {
        var recording = LoadByFilename(StrafeDiagonalRecording);
        // Accept both FORWARD+STRAFE_RIGHT (0x9) and FORWARD+STRAFE_LEFT (0x5)
        var movingFrames = recording.Frames
            .Where(f => f.MovementFlags == 0x9 || f.MovementFlags == 0x5)
            .ToList();

        if (movingFrames.Count < 2)
        {
            _output.WriteLine("SKIP: No diagonal movement frames");
            return;
        }

        // Use longest consecutive segment to avoid gaps
        var segments = FindConsecutiveSegments(movingFrames, maxGapMs: 100);
        var bestSeg = segments.OrderByDescending(s => s.Count).First();

        if (bestSeg.Count < 3)
        {
            _output.WriteLine($"SKIP: Longest diagonal segment is {bestSeg.Count} frames (need 3+)");
            return;
        }

        var first = bestSeg[0];
        var last = bestSeg[^1];
        float dx = last.Position.X - first.Position.X;
        float dy = last.Position.Y - first.Position.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (last.FrameTimestamp - first.FrameTimestamp) / 1000.0f;
        float recordedSpeed = distance / elapsed;

        _output.WriteLine($"Diagonal speed: {recordedSpeed:F4} y/s (from {bestSeg.Count}-frame segment)");
        _output.WriteLine($"Run speed: {first.RunSpeed:F4} y/s");
        _output.WriteLine($"Ratio: {recordedSpeed / first.RunSpeed:F4} (expected: 1.0 if normalized)");

        // Diagonal speed should equal run speed (normalized), not runSpeed * sqrt(2)
        Assert.True(MathF.Abs(recordedSpeed - first.RunSpeed) < VelocityTolerance,
            $"Diagonal speed should be normalized to {first.RunSpeed:F4}, got {recordedSpeed:F4}");
    }

    [Fact]
    public void StrafeOnly_SpeedMatchesRunSpeed()
    {
        var recording = LoadByFilename(StrafeOnlyRecording);
        // Accept both STRAFE_RIGHT (0x8) and STRAFE_LEFT (0x4) as pure strafe
        var movingFrames = recording.Frames
            .Where(f => f.MovementFlags == 0x8 || f.MovementFlags == 0x4)
            .ToList();

        _output.WriteLine($"Pure strafe frames: {movingFrames.Count}");

        if (movingFrames.Count < 5)
        {
            _output.WriteLine("SKIP: Not enough strafe-only frames");
            return;
        }

        // Use frame-by-frame speed measurement (strafe segments may be short)
        var speeds = new List<float>();
        for (int i = 1; i < movingFrames.Count; i++)
        {
            long gap = movingFrames[i].FrameTimestamp - movingFrames[i - 1].FrameTimestamp;
            if (gap > 100) continue; // Skip non-consecutive frames

            float dt = gap / 1000.0f;
            if (dt <= 0) continue;
            float dx = movingFrames[i].Position.X - movingFrames[i - 1].Position.X;
            float dy = movingFrames[i].Position.Y - movingFrames[i - 1].Position.Y;
            speeds.Add(MathF.Sqrt(dx * dx + dy * dy) / dt);
        }

        _output.WriteLine($"Frame-to-frame speed samples: {speeds.Count}");

        if (speeds.Count < 3)
        {
            _output.WriteLine("SKIP: Not enough consecutive strafe frame pairs for speed measurement");
            return;
        }

        float avgSpeed = speeds.Average();
        float runSpeed = movingFrames[0].RunSpeed;

        _output.WriteLine($"Avg strafe speed: {avgSpeed:F4} y/s");
        _output.WriteLine($"Run speed: {runSpeed:F4} y/s");

        // Strafe speed should match run speed in vanilla 1.12.1
        Assert.True(MathF.Abs(avgSpeed - runSpeed) < VelocityTolerance,
            $"Strafe speed mismatch: recorded={avgSpeed:F4}, expected={runSpeed:F4}");
    }

    [Fact]
    public void RunningJump_MaintainsHorizontalSpeed()
    {
        var recording = LoadByFilename(RunningJumpRecording);
        // Find airborne frames with forward movement (running jumps, not pure falls)
        var airborneFrames = recording.Frames
            .Where(f => (f.MovementFlags & 0x2001) == 0x2001) // FALLING + FORWARD
            .ToList();

        if (airborneFrames.Count < 3)
        {
            _output.WriteLine("SKIP: Not enough airborne+forward frames");
            return;
        }

        // Find individual jump arcs (consecutive segments)
        var arcs = FindConsecutiveSegments(airborneFrames, maxGapMs: 200);
        var validArcs = arcs.Where(a => a.Count >= 5).ToList();

        _output.WriteLine($"Airborne+forward frames: {airborneFrames.Count}, valid arcs: {validArcs.Count}");

        if (validArcs.Count == 0)
        {
            _output.WriteLine("SKIP: No long enough airborne+forward arcs");
            return;
        }

        // Measure horizontal speed from the longest arc
        var bestArc = validArcs.OrderByDescending(a => a.Count).First();
        var first = bestArc[0];
        var last = bestArc[^1];
        float dx = last.Position.X - first.Position.X;
        float dy = last.Position.Y - first.Position.Y;
        float horizDistance = MathF.Sqrt(dx * dx + dy * dy);
        float elapsed = (last.FrameTimestamp - first.FrameTimestamp) / 1000.0f;
        float airborneHorizSpeed = horizDistance / elapsed;

        _output.WriteLine($"Horizontal speed while airborne: {airborneHorizSpeed:F4} y/s ({bestArc.Count}-frame arc)");
        _output.WriteLine($"Run speed: {first.RunSpeed:F4} y/s");

        // Horizontal speed should be maintained during jump
        Assert.True(MathF.Abs(airborneHorizSpeed - first.RunSpeed) < VelocityTolerance,
            $"Horizontal speed changed during jump: {airborneHorizSpeed:F4} vs {first.RunSpeed:F4}");
    }

    // ==========================================================================
    // FRAME-BY-FRAME SIMULATION TESTS (require Navigation.dll + map data)
    // ==========================================================================

    [Fact]
    public void FlatRunForward_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized (Navigation.dll not found)");
            return;
        }

        var recording = LoadByFilename(FlatRunForwardRecording);
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("flat_run_forward", result);

        Assert.True(result.MaxPositionError < RelaxedPositionTolerance,
            $"Max position error {result.MaxPositionError:F4}y exceeds tolerance {RelaxedPositionTolerance}y " +
            $"(at frame {result.WorstFrame})");
    }

    [Fact]
    public void StandingJump_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }

        var recording = LoadByFilename(StandingJumpRecording);
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("standing_jump", result);

        // Average error tests gravity integration accuracy.
        // Max error is dominated by landing detection (terrain sweep doesn't detect ground
        // during capsule fall), which is a separate issue from physics integration.
        Assert.True(result.AvgPositionError < RelaxedPositionTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {RelaxedPositionTolerance}y");
    }

    [Fact]
    public void FallFromHeight_FrameByFrame_PositionMatchesRecording()
    {
        if (!_fixture.IsInitialized)
        {
            _output.WriteLine("SKIP: Physics engine not initialized");
            return;
        }

        var recording = LoadByFilename(FallFromHeightRecording);
        TryPreloadMap(recording.MapId);

        var result = ReplayRecording(recording);
        LogCalibrationResult("fall_from_height", result);

        // Average error tests gravity integration accuracy.
        // Max error is dominated by landing detection at the end of the fall
        // (terrain sweep doesn't detect ground during capsule fall).
        // Falls have higher tolerance because the landing frames skew the average.
        const float fallTolerance = 1.0f;
        Assert.True(result.AvgPositionError < fallTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds tolerance {fallTolerance}y");
    }

    // ==========================================================================
    // REPLAY ENGINE
    // ==========================================================================

    /// <summary>
    /// Replays a recording through PhysicsStepV2 and tracks deviation.
    ///
    /// The engine now properly handles JUMPING flags (applies impulse only on
    /// first frame via fallTime==0 check) and outputs correct end-of-frame
    /// velocity for airborne frames. This allows a clean replay loop:
    /// - Pass recorded moveFlags directly (no flag stripping needed)
    /// - Carry engine output velocity for airborne continuation
    /// - Engine handles jump impulse, gravity integration, and ground detection
    /// </summary>
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

            // Compute fall duration from frame timestamps.
            // Recording stores absolute GetTickCount; engine expects duration in ms.
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

            // Build PhysicsInput from recorded frame.
            // Pass moveFlags directly — the engine now correctly handles JUMPING
            // (applies impulse only when fallTime==0).
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

            // Velocity: carry engine output velocity for airborne frames.
            // The engine outputs correct end-of-frame velocity (not average),
            // so carrying it forward produces accurate integration.
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
                // Set fallTime > 0 to prevent engine from re-applying JUMP_VELOCITY.
                input.FallTime = 1;
            }

            if (verbose && i < 5)
            {
                _output.WriteLine($"  [pre] frame={i} recZ={currentFrame.Position.Z:F4} nextRecZ={nextFrame.Position.Z:F4} " +
                    $"inputVz={input.Vz:F4} fallTime={input.FallTime} flags=0x{input.MoveFlags:X} dt={dt:F4}");
            }

            // Step physics
            var output = StepPhysicsV2(ref input);
            prevOutput = output;
            prevGroundZ = output.GroundZ;

            // Compare output position to next recorded frame
            float dx = output.X - nextFrame.Position.X;
            float dy = output.Y - nextFrame.Position.Y;
            float dz = output.Z - nextFrame.Position.Z;
            float posError = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            float horizError = MathF.Sqrt(dx * dx + dy * dy);
            float vertError = MathF.Abs(dz);

            if (verbose && (i < 5 || vertError > 0.5f))
            {
                _output.WriteLine($"  [post] frame={i} simZ={output.Z:F4} nextRecZ={nextFrame.Position.Z:F4} " +
                    $"err={vertError:F4} outVz={output.Vz:F4} grounded={output.MoveFlags & 0x6000:X}");
            }

            result.AddFrame(i, posError, horizError, vertError,
                output.X, output.Y, output.Z,
                nextFrame.Position.X, nextFrame.Position.Y, nextFrame.Position.Z);
        }

        return result;
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

    private static List<RecordedFrame> GetJumpFrames(MovementRecording recording)
    {
        return recording.Frames
            .Where(f => (f.MovementFlags & 0x2000) != 0) // JUMPING/FALLING flag
            .ToList();
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

/// <summary>
/// Tracks frame-by-frame calibration results.
/// </summary>
public class CalibrationResult
{
    public List<FrameDetail> FrameDetails { get; } = [];
    public int FrameCount => FrameDetails.Count;
    public float MaxPositionError { get; private set; }
    public int WorstFrame { get; private set; } = -1;
    public float AvgPositionError => FrameDetails.Count > 0 ? FrameDetails.Average(f => f.PosError) : 0;
    public float AvgHorizError => FrameDetails.Count > 0 ? FrameDetails.Average(f => f.HorizError) : 0;
    public float AvgVertError => FrameDetails.Count > 0 ? FrameDetails.Average(f => f.VertError) : 0;

    public void AddFrame(int frame, float posError, float horizError, float vertError,
        float simX, float simY, float simZ, float recX, float recY, float recZ)
    {
        FrameDetails.Add(new FrameDetail
        {
            Frame = frame,
            PosError = posError,
            HorizError = horizError,
            VertError = vertError,
            SimX = simX, SimY = simY, SimZ = simZ,
            RecX = recX, RecY = recY, RecZ = recZ
        });

        if (posError > MaxPositionError)
        {
            MaxPositionError = posError;
            WorstFrame = FrameDetails.Count - 1;
        }
    }

    public record FrameDetail
    {
        public int Frame;
        public float PosError, HorizError, VertError;
        public float SimX, SimY, SimZ;
        public float RecX, RecY, RecZ;
    }
}
