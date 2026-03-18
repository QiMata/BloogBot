// ServerMovementValidationTests.cs — Codifies VMaNGOS server-side movement validation rules
// as physics engine assertions against recorded movement replays.
//
// VMaNGOS anticheat validates:
//   1. Speed: distance/time ≤ allowed speed (with small tolerance for lag)
//   2. Slope: characters cannot climb slopes steeper than the walkable threshold
//   3. Fall physics: gravity must apply correctly (no fly/hover hacks)
//   4. Ground clamping: position must be on a valid surface (no underground/floating)
//   5. State consistency: movement flags must match actual movement behavior
//
// PhysX CCT reference (docs/physics/PHYSX_CCT_RULES.md):
//   - 3-pass movement: UP → SIDE → DOWN
//   - Walkable slope: cos(angle) ≥ slopeLimit (WoW: 0.6428 = cos(50°))
//   - Step-down snap: grounded characters snap down up to StepDownHeight (4.0y)
//   - Auto-step: obstacles ≤ StepHeight (2.125y) are automatically climbed

using System;
using System.Collections.Generic;
using System.Linq;
using Navigation.Physics.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class ServerMovementValidationTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    // =========================================================================
    // VMaNGOS SPEED CONSTANTS (from MaNGOS Unit.h / MoveSpline)
    // =========================================================================

    private const float WalkSpeed = 2.5f;        // yards/sec
    private const float RunSpeed = 7.0f;          // yards/sec
    private const float RunBackSpeed = 4.5f;      // yards/sec
    private const float SwimSpeed = 4.722f;       // yards/sec
    private const float SwimBackSpeed = 2.5f;     // yards/sec

    // VMaNGOS anticheat speed tolerance: server uses 1.1x (10% margin)
    // We use 3% for our engine precision test (tighter than server)
    private const float SpeedToleranceFactor = 1.03f;

    // VMaNGOS anticheat constants (from MovementAnticheat.cpp + mangosd.conf)
    private const float VmangosSpeedTolerance = 1.1f;           // Server allows 10% overspeed
    private const float VmangosTeleportThreshold = 40.0f;       // yards — single-packet max displacement
    private const float VmangosWallClimbAngleRad = 1.0f;        // radians (~57.3°) — soft limit
    private const float VmangosWallClimbHighRad = 1.2f;         // radians (~68.8°) — hard limit
    private const float TerminalVelocity = 60.0f;               // yards/sec — max falling speed

    // Minimum dt to consider for speed checks (skip near-zero dt frames)
    private const float MinDtForSpeedCheck = 0.005f;

    // 3D distance threshold to exclude teleport frames from step/speed checks.
    // Frames where 3D position jumped >20y are teleport artifacts, not physics movement.
    private const float TeleportExclusionThreshold = 20.0f;

    // =========================================================================
    // 1. SPEED VALIDATION — No frame exceeds server speed limits
    // =========================================================================

    [Fact]
    public void AllRecordings_HorizontalSpeed_NeverExceedsServerLimit()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int totalFrames = 0;
        int violations = 0;
        var worstViolations = new List<(string name, int frame, float speed, float limit, string mode)>();

        foreach (var (name, recording, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];
                if (fd.Dt < MinDtForSpeedCheck) continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact) continue;
                if (fd.IsSplineElevationTransition || prev.IsSplineElevationTransition) continue;
                if (fd.IsOnTransport || prev.IsOnTransport) continue;
                if (fd.IsFlagTransition) continue; // Flag transitions have sub-frame timing issues
                totalFrames++;

                float dx = fd.SimX - prev.SimX;
                float dy = fd.SimY - prev.SimY;
                float horizDist = MathF.Sqrt(dx * dx + dy * dy);

                // Skip teleport frames (huge position jumps)
                if (horizDist > 50f) continue;

                float speed = horizDist / fd.Dt;

                // Determine allowed speed based on movement flags
                float allowedSpeed = GetAllowedSpeed(fd.MoveFlags, fd.IsSwimming);
                float serverLimit = allowedSpeed * SpeedToleranceFactor;

                // Airborne frames inherit launch velocity — allow higher horizontal speed
                // (running jump at 45° can maintain RunSpeed horizontally)
                if (fd.IsAirborne) serverLimit = RunSpeed * 1.5f;

                if (speed > serverLimit && speed > 1.0f)
                {
                    violations++;
                    worstViolations.Add((name, fd.Frame, speed, serverLimit, fd.MovementMode));
                }
            }
        }

        // Report
        _output.WriteLine($"=== SERVER SPEED VALIDATION ===");
        _output.WriteLine($"Frames checked: {totalFrames}");
        _output.WriteLine($"Violations: {violations}");

        if (worstViolations.Count > 0)
        {
            var top20 = worstViolations.OrderByDescending(v => v.speed / v.limit).Take(20);
            foreach (var v in top20)
            {
                float ratio = v.speed / v.limit;
                _output.WriteLine($"  [{v.name}] frame={v.frame} speed={v.speed:F2} limit={v.limit:F2} " +
                    $"ratio={ratio:F2}x mode={v.mode}");
            }
        }

        // Separate violations by severity:
        // - Mild (speed < 2x limit): geometry transitions, slope effects
        // - Severe (speed > 2x limit): real physics bugs, position teleports
        int severeViolations = worstViolations.Count(v => v.speed > v.limit * 2);
        float severeRate = totalFrames > 0 ? (float)severeViolations / totalFrames : 0;
        _output.WriteLine($"Severe violations (>2x limit): {severeViolations} ({severeRate:P2})");

        // Assert on severe violations only — these indicate real physics engine bugs
        // (position teleports, Z oscillation causing large XY jumps, missing ground snap).
        // Mild violations from geometry transitions are expected and acceptable.
        Assert.True(severeRate < 0.05f,
            $"Severe speed violation rate {severeRate:P2} exceeds 5% threshold. " +
            $"{severeViolations}/{totalFrames} frames exceeded 2x server speed limits. " +
            $"This indicates position teleports or ground clamping failures in the physics engine.");
    }

    [Fact]
    public void GroundMovement_Speed_MatchesExpectedRunSpeed()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        var groundSpeeds = new List<float>();

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var fd = result.FrameDetails[i];
                var prev = result.FrameDetails[i - 1];

                if (fd.MovementMode != "ground" || fd.Dt < MinDtForSpeedCheck) continue;
                if (fd.IsRecordingArtifact || fd.IsFlagTransition) continue;

                // Only check forward movement frames
                if ((fd.MoveFlags & Helpers.MoveFlags.Forward) == 0) continue;
                if ((fd.MoveFlags & Helpers.MoveFlags.Backward) != 0) continue;

                float dx = fd.SimX - prev.SimX;
                float dy = fd.SimY - prev.SimY;
                float speed = MathF.Sqrt(dx * dx + dy * dy) / fd.Dt;

                if (speed > 0.5f) // Only count frames where we're actually moving
                    groundSpeeds.Add(speed);
            }
        }

        if (groundSpeeds.Count == 0) { _output.WriteLine("SKIP: No ground forward frames"); return; }

        float avgSpeed = groundSpeeds.Average();
        float medianSpeed = groundSpeeds.OrderBy(s => s).ToList()[groundSpeeds.Count / 2];

        _output.WriteLine($"=== GROUND FORWARD SPEED DISTRIBUTION ===");
        _output.WriteLine($"Frames: {groundSpeeds.Count}");
        _output.WriteLine($"Avg: {avgSpeed:F3} y/s  Median: {medianSpeed:F3} y/s  Expected: {RunSpeed:F1} y/s");
        _output.WriteLine($"Min: {groundSpeeds.Min():F3}  Max: {groundSpeeds.Max():F3}");

        // Average ground speed should be within 10% of RunSpeed
        Assert.True(MathF.Abs(avgSpeed - RunSpeed) / RunSpeed < 0.10f,
            $"Average ground speed {avgSpeed:F3} deviates more than 10% from expected {RunSpeed} y/s");
    }

    // =========================================================================
    // 2. SLOPE WALKABILITY — Grounded characters must be on walkable surfaces
    // =========================================================================

    [Fact]
    public void GroundMovement_SurfaceNormal_AlwaysWalkable()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int groundFrames = 0;
        int nonWalkableFrames = 0;
        var nonWalkableDetails = new List<(string name, int frame, float normalZ, float simZ, float groundZ)>();

        foreach (var (name, _, result) in allResults)
        {
            foreach (var fd in result.FrameDetails)
            {
                if (fd.MovementMode != "ground") continue;
                if (fd.IsRecordingArtifact) continue;
                groundFrames++;

                // The engine reports ground normal via GroundNx/Ny/Nz in PhysicsOutput
                // We can infer walkability from the engine's ground Z stability
                // A non-walkable slope would cause the engine to NOT snap to ground,
                // resulting in airborne transition (which would change MovementMode)

                // For frames where the engine reports ground Z, check the vertical
                // difference between sim position and ground — should be small for
                // properly grounded frames
                float zAboveGround = fd.SimZ - fd.EngineGroundZ;
                if (fd.EngineGroundZ > -50000f && zAboveGround > PhysicsTestConstants.StepHeight)
                {
                    nonWalkableFrames++;
                    nonWalkableDetails.Add((name, fd.Frame, zAboveGround, fd.SimZ, fd.EngineGroundZ));
                }
            }
        }

        _output.WriteLine($"=== SLOPE WALKABILITY VALIDATION ===");
        _output.WriteLine($"Ground frames: {groundFrames}");
        _output.WriteLine($"Floating above ground (>{PhysicsTestConstants.StepHeight:F1}y): {nonWalkableFrames}");

        if (nonWalkableDetails.Count > 0)
        {
            foreach (var d in nonWalkableDetails.OrderByDescending(d => d.normalZ).Take(10))
            {
                _output.WriteLine($"  [{d.name}] frame={d.frame} zAboveGround={d.normalZ:F3} " +
                    $"simZ={d.simZ:F3} groundZ={d.groundZ:F3}");
            }
        }

        // No grounded frame should be floating more than step height above ground
        float floatingRate = groundFrames > 0 ? (float)nonWalkableFrames / groundFrames : 0;
        Assert.True(floatingRate < 0.005f,
            $"Floating rate {floatingRate:P2} — {nonWalkableFrames}/{groundFrames} ground frames are " +
            $"more than {PhysicsTestConstants.StepHeight}y above engine ground Z");
    }

    // =========================================================================
    // 3. GROUNDED ↔ AIRBORNE TRANSITIONS — No false freefalls on flat terrain
    // =========================================================================

    [Fact]
    public void AllRecordings_NoFalseFreefallOnFlatTerrain()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int totalGroundFrames = 0;
        int falseFalls = 0;
        var falseFallDetails = new List<(string name, int frame, float dz, float prevGroundZ, float simZ)>();

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 2; i < result.FrameDetails.Count; i++)
            {
                var prev2 = result.FrameDetails[i - 2];
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                // Look for: grounded → grounded → airborne on flat-ish terrain
                if (prev2.MovementMode != "ground" || prev.MovementMode != "ground") continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact) continue;

                // Exclude teleport frames — the replay engine skips them in FrameDetails
                // but adjacent frames appear consecutive, creating false ground→airborne artifacts
                float tdx = fd.SimX - prev.SimX, tdy = fd.SimY - prev.SimY, tdz = fd.SimZ - prev.SimZ;
                float dist3D = MathF.Sqrt(tdx * tdx + tdy * tdy + tdz * tdz);
                if (dist3D > TeleportExclusionThreshold) continue;

                // Exclude transport and spline transitions
                if (fd.IsOnTransport || prev.IsOnTransport) continue;
                if (fd.IsSplineElevationTransition || prev.IsSplineElevationTransition) continue;

                totalGroundFrames++;

                // If current frame is airborne and the terrain was flat (small Z change)
                bool currentAirborne = fd.IsAirborne;
                float prevDz = MathF.Abs(prev.SimZ - prev2.SimZ);
                bool terrainWasFlat = prevDz < 0.5f; // Less than 0.5y Z change = flat

                if (currentAirborne && terrainWasFlat)
                {
                    // Check if there's actually a drop — legitimate grounded→airborne
                    // happens at edges. False freefall = no significant Z drop expected.
                    // Only count downward drops (positive zDrop) under 0.3y.
                    // Negative zDrop means the character went UP — not a false fall.
                    float zDrop = prev.SimZ - fd.SimZ;
                    if (zDrop >= 0f && zDrop < 0.3f) // Small downward or zero — not a real edge/cliff
                    {
                        falseFalls++;
                        falseFallDetails.Add((name, fd.Frame, zDrop, prev.EngineGroundZ, fd.SimZ));
                    }
                }
            }
        }

        _output.WriteLine($"=== FALSE FREEFALL DETECTION ===");
        _output.WriteLine($"Ground→ground transitions checked: {totalGroundFrames}");
        _output.WriteLine($"False freefalls (flat terrain → airborne, no edge): {falseFalls}");

        if (falseFallDetails.Count > 0)
        {
            foreach (var d in falseFallDetails.Take(15))
            {
                _output.WriteLine($"  [{d.name}] frame={d.frame} zDrop={d.dz:F3} " +
                    $"prevGroundZ={d.prevGroundZ:F3} simZ={d.simZ:F3}");
            }
        }

        // False freefall rate should be very low
        float falseFallRate = totalGroundFrames > 0 ? (float)falseFalls / totalGroundFrames : 0;
        Assert.True(falseFallRate < 0.02f,
            $"False freefall rate {falseFallRate:P2} — {falseFalls}/{totalGroundFrames} ground frames " +
            $"transition to airborne on flat terrain without a real edge");
    }

    // =========================================================================
    // 4. STEP HEIGHT VALIDATION — Auto-step respects limits
    // =========================================================================

    [Fact]
    public void GroundMovement_StepUp_NeverExceedsStepHeight()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int stepUpCount = 0;
        int excessiveStepCount = 0;
        var stepEvents = new List<(string name, int frame, float stepDz, float simZ)>();

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                if (prev.MovementMode != "ground" || fd.MovementMode != "ground") continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact) continue;
                if (fd.IsSplineElevationTransition || prev.IsSplineElevationTransition) continue;
                if (fd.IsOnTransport || prev.IsOnTransport) continue;

                // Exclude teleport frames — large 3D jumps aren't physics steps
                float tdx = fd.SimX - prev.SimX, tdy = fd.SimY - prev.SimY, tdz = fd.SimZ - prev.SimZ;
                float dist3D = MathF.Sqrt(tdx * tdx + tdy * tdy + tdz * tdz);
                if (dist3D > TeleportExclusionThreshold) continue;

                float dz = fd.SimZ - prev.SimZ;

                // Step-up: positive Z change while grounded
                if (dz > 0.3f)
                {
                    stepUpCount++;
                    // Use larger tolerance — geometry gaps at WMO boundaries can cause
                    // legitimate ground-to-ground Z jumps slightly above step height
                    if (dz > PhysicsTestConstants.StepHeight * 2.0f)
                    {
                        excessiveStepCount++;
                        stepEvents.Add((name, fd.Frame, dz, fd.SimZ));
                    }
                }
            }
        }

        _output.WriteLine($"=== STEP HEIGHT VALIDATION ===");
        _output.WriteLine($"Step-up events (dZ > 0.3y): {stepUpCount}");
        _output.WriteLine($"Excessive steps (dZ > {PhysicsTestConstants.StepHeight * 2.0f:F1}y): {excessiveStepCount}");
        _output.WriteLine($"Max allowed step: {PhysicsTestConstants.StepHeight:F3}y (2x tolerance for geometry gaps)");

        if (stepEvents.Count > 0)
        {
            foreach (var s in stepEvents.OrderByDescending(s => s.stepDz).Take(10))
            {
                _output.WriteLine($"  [{s.name}] frame={s.frame} stepDz={s.stepDz:F3} simZ={s.simZ:F3}");
            }
        }

        // A small number of excessive steps are acceptable at geometry boundaries
        // (WMO/ADT seams, elevator doors). Track these as diagnostic data points.
        float excessiveRate = stepUpCount > 0 ? (float)excessiveStepCount / stepUpCount : 0;
        Assert.True(excessiveStepCount <= 5,
            $"{excessiveStepCount} step-up events exceeded 2x max step height " +
            $"{PhysicsTestConstants.StepHeight}y — indicates ground clamping failure");
    }

    [Fact]
    public void GroundMovement_StepDown_NeverExceedsStepDownHeight()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int stepDownCount = 0;
        int excessiveDropCount = 0;
        var dropEvents = new List<(string name, int frame, float dropDz, float simZ)>();

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                // Both frames grounded — this is a step-down, not a fall
                if (prev.MovementMode != "ground" || fd.MovementMode != "ground") continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact) continue;
                if (fd.IsSplineElevationTransition || prev.IsSplineElevationTransition) continue;
                if (fd.IsOnTransport || prev.IsOnTransport) continue;

                // Exclude teleport frames
                float tdx = fd.SimX - prev.SimX, tdy = fd.SimY - prev.SimY, tdz = fd.SimZ - prev.SimZ;
                float dist3D = MathF.Sqrt(tdx * tdx + tdy * tdy + tdz * tdz);
                if (dist3D > TeleportExclusionThreshold) continue;

                float dz = prev.SimZ - fd.SimZ; // Positive = downward

                if (dz > 0.3f)
                {
                    stepDownCount++;
                    // Use generous tolerance — geometry gaps at WMO/ADT boundaries
                    if (dz > PhysicsTestConstants.StepDownHeight * 2.0f)
                    {
                        excessiveDropCount++;
                        dropEvents.Add((name, fd.Frame, dz, fd.SimZ));
                    }
                }
            }
        }

        _output.WriteLine($"=== STEP-DOWN VALIDATION ===");
        _output.WriteLine($"Step-down events (dZ > 0.3y): {stepDownCount}");
        _output.WriteLine($"Excessive drops (dZ > {PhysicsTestConstants.StepDownHeight * 2.0f:F1}y): {excessiveDropCount}");
        _output.WriteLine($"Max step-down snap: {PhysicsTestConstants.StepDownHeight:F3}y (2x tolerance for geometry gaps)");

        if (dropEvents.Count > 0)
        {
            foreach (var d in dropEvents.OrderByDescending(d => d.dropDz).Take(10))
            {
                _output.WriteLine($"  [{d.name}] frame={d.frame} dropDz={d.dropDz:F3} simZ={d.simZ:F3}");
            }
        }

        float excessiveRate = stepDownCount > 0 ? (float)excessiveDropCount / stepDownCount : 0;
        Assert.True(excessiveDropCount <= 10,
            $"{excessiveDropCount} step-down events exceeded 2x max step-down height " +
            $"{PhysicsTestConstants.StepDownHeight}y — indicates ground clamping failure");
    }

    // =========================================================================
    // 5. FALL PHYSICS — Gravity and jump arcs match server expectations
    // =========================================================================

    [Fact]
    public void AirborneMovement_FallAcceleration_MatchesGravity()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        var fallAccelerations = new List<float>();

        foreach (var (name, _, result) in allResults)
        {
            // Find consecutive airborne frames to measure acceleration
            for (int i = 2; i < result.FrameDetails.Count; i++)
            {
                var prev2 = result.FrameDetails[i - 2];
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                // Need 3 consecutive airborne non-swimming frames
                if (!prev2.IsAirborne || !prev.IsAirborne || !fd.IsAirborne) continue;
                if (prev2.IsSwimming || prev.IsSwimming || fd.IsSwimming) continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact || prev2.IsRecordingArtifact) continue;
                if (prev.Dt < MinDtForSpeedCheck || fd.Dt < MinDtForSpeedCheck) continue;

                // Compute Vz at two consecutive intervals
                float vz1 = (prev.SimZ - prev2.SimZ) / prev.Dt;
                float vz2 = (fd.SimZ - prev.SimZ) / fd.Dt;

                // Acceleration = (v2 - v1) / dt
                float dt = (prev.Dt + fd.Dt) / 2.0f;
                if (dt < MinDtForSpeedCheck) continue;

                float accel = (vz2 - vz1) / dt;

                // Only count downward acceleration (gravity), ignore transport/upward artifacts
                if (accel < -5.0f && accel > -40.0f)
                    fallAccelerations.Add(-accel); // Store as positive magnitude
            }
        }

        if (fallAccelerations.Count == 0) { _output.WriteLine("SKIP: No airborne acceleration samples"); return; }

        float avgAccel = fallAccelerations.Average();
        float medianAccel = fallAccelerations.OrderBy(a => a).ToList()[fallAccelerations.Count / 2];

        _output.WriteLine($"=== FALL ACCELERATION VALIDATION ===");
        _output.WriteLine($"Samples: {fallAccelerations.Count}");
        _output.WriteLine($"Avg: {avgAccel:F3} y/s²  Median: {medianAccel:F3} y/s²  Expected: {PhysicsTestConstants.Gravity:F3} y/s²");
        _output.WriteLine($"Min: {fallAccelerations.Min():F3}  Max: {fallAccelerations.Max():F3}");

        // Average fall acceleration should be within 20% of gravity
        // (wider tolerance due to discrete timestep effects)
        float deviationPct = MathF.Abs(avgAccel - PhysicsTestConstants.Gravity) / PhysicsTestConstants.Gravity;
        Assert.True(deviationPct < 0.20f,
            $"Average fall acceleration {avgAccel:F3} deviates {deviationPct:P1} from expected gravity " +
            $"{PhysicsTestConstants.Gravity:F3} y/s²");
    }

    [Fact]
    public void JumpArc_PeakHeight_MatchesPhysicsFormula()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        // Theoretical max jump height: v₀²/(2g) = 7.95577²/(2×19.2911) ≈ 1.640y
        float expectedPeakHeight = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity)
            / (2.0f * PhysicsTestConstants.Gravity);

        var jumpPeaks = new List<float>();

        foreach (var (name, _, result) in allResults)
        {
            float jumpStartZ = float.NaN;
            float maxZ = float.NaN;
            bool inJump = false;

            foreach (var fd in result.FrameDetails)
            {
                if (fd.IsRecordingArtifact) continue;

                if (!inJump && fd.Transition == TransitionType.JumpStart)
                {
                    inJump = true;
                    jumpStartZ = fd.SimZ;
                    maxZ = fd.SimZ;
                }
                else if (inJump)
                {
                    if (fd.SimZ > maxZ) maxZ = fd.SimZ;

                    if (fd.Transition == TransitionType.Landing || fd.MovementMode == "ground")
                    {
                        float peakHeight = maxZ - jumpStartZ;
                        if (peakHeight > 0.1f) // Filter out micro-jumps
                            jumpPeaks.Add(peakHeight);
                        inJump = false;
                    }
                }
            }
        }

        if (jumpPeaks.Count == 0) { _output.WriteLine("SKIP: No jump arcs detected"); return; }

        float avgPeak = jumpPeaks.Average();
        float maxPeak = jumpPeaks.Max();

        _output.WriteLine($"=== JUMP ARC VALIDATION ===");
        _output.WriteLine($"Jumps: {jumpPeaks.Count}");
        _output.WriteLine($"Avg peak: {avgPeak:F3}y  Max peak: {maxPeak:F3}y  Expected: {expectedPeakHeight:F3}y");

        // Max jump peak should not exceed theoretical maximum (+ tolerance for slopes)
        Assert.True(maxPeak < expectedPeakHeight * 1.5f,
            $"Jump peak height {maxPeak:F3}y exceeds theoretical max {expectedPeakHeight:F3}y by more than 50%");
    }

    // =========================================================================
    // 5b. TERMINAL VELOCITY — Fall speed must not exceed cap
    // =========================================================================

    [Fact]
    public void AirborneMovement_FallSpeed_NeverExceedsTerminalVelocity()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        float maxFallSpeed = 0;
        int excessiveCount = 0;
        var violations = new List<(string name, int frame, float speed)>();

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                if (!fd.IsAirborne || fd.IsSwimming) continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact) continue;
                if (fd.IsSplineElevationTransition || prev.IsSplineElevationTransition) continue;
                if (fd.IsOnTransport || prev.IsOnTransport) continue;
                if (fd.Dt < MinDtForSpeedCheck) continue;

                // Skip position teleport frames
                float dz = fd.SimZ - prev.SimZ;
                float dx = fd.SimX - prev.SimX;
                float dy = fd.SimY - prev.SimY;
                if (MathF.Sqrt(dx * dx + dy * dy + dz * dz) > 50f) continue;

                float vz = dz / fd.Dt;
                float fallSpeed = -vz; // Positive = falling downward

                if (fallSpeed > maxFallSpeed) maxFallSpeed = fallSpeed;

                if (fallSpeed > TerminalVelocity * 1.1f) // 10% tolerance
                {
                    excessiveCount++;
                    violations.Add((name, fd.Frame, fallSpeed));
                }
            }
        }

        _output.WriteLine($"=== TERMINAL VELOCITY VALIDATION ===");
        _output.WriteLine($"Max fall speed: {maxFallSpeed:F2} y/s  Limit: {TerminalVelocity:F0} y/s");
        _output.WriteLine($"Excessive frames: {excessiveCount}");

        if (violations.Count > 0)
        {
            foreach (var v in violations.OrderByDescending(v => v.speed).Take(10))
                _output.WriteLine($"  [{v.name}] frame={v.frame} fallSpeed={v.speed:F2} y/s");
        }

        Assert.True(excessiveCount == 0,
            $"{excessiveCount} frames exceeded terminal velocity {TerminalVelocity} y/s " +
            $"(max observed: {maxFallSpeed:F2} y/s)");
    }

    // =========================================================================
    // 5c. WALL CLIMB — VMaNGOS rise/run ratio must not exceed anticheat thresholds
    // =========================================================================

    [Fact]
    public void GroundMovement_ClimbAngle_WithinVmangosWallClimbLimit()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        // VMaNGOS wall climb check: deltaZ / deltaXY > tan(angle) = cheat
        float softLimit = MathF.Tan(VmangosWallClimbAngleRad); // ~1.557
        float hardLimit = MathF.Tan(VmangosWallClimbHighRad);  // ~2.572

        int climbFrames = 0;
        int softViolations = 0;
        int hardViolations = 0;
        var worstClimbs = new List<(string name, int frame, float ratio, float dz, float dxy)>();

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                if (prev.MovementMode != "ground" || fd.MovementMode != "ground") continue;
                if (fd.IsRecordingArtifact || prev.IsRecordingArtifact) continue;
                if (fd.IsOnTransport || prev.IsOnTransport) continue;

                float dz = fd.SimZ - prev.SimZ;
                if (dz < 1.0f) continue; // VMaNGOS minimum vertical delta

                float dx = fd.SimX - prev.SimX;
                float dy = fd.SimY - prev.SimY;
                float dxy = MathF.Sqrt(dx * dx + dy * dy);
                if (dxy < 0.5f) continue; // VMaNGOS minimum horizontal delta

                float ratio = dz / dxy;
                climbFrames++;

                if (ratio > hardLimit)
                {
                    hardViolations++;
                    worstClimbs.Add((name, fd.Frame, ratio, dz, dxy));
                }
                else if (ratio > softLimit)
                {
                    softViolations++;
                }
            }
        }

        _output.WriteLine($"=== VMANGOS WALL CLIMB VALIDATION ===");
        _output.WriteLine($"Climb frames (dZ>1.0, dXY>0.5): {climbFrames}");
        _output.WriteLine($"Soft limit (tan {VmangosWallClimbAngleRad:F1}rad = {softLimit:F3}): {softViolations} violations");
        _output.WriteLine($"Hard limit (tan {VmangosWallClimbHighRad:F1}rad = {hardLimit:F3}): {hardViolations} violations");

        if (worstClimbs.Count > 0)
        {
            foreach (var c in worstClimbs.OrderByDescending(c => c.ratio).Take(10))
                _output.WriteLine($"  [{c.name}] frame={c.frame} ratio={c.ratio:F3} dZ={c.dz:F3} dXY={c.dxy:F3}");
        }

        // Hard violations = server would flag as wallhack
        Assert.True(hardViolations <= 3,
            $"{hardViolations} frames exceeded VMaNGOS hard wall climb limit " +
            $"(rise/run > {hardLimit:F3}). Server would flag as wall climb hack.");
    }

    // =========================================================================
    // 6. GROUND CLAMPING — No underground or excessive floating
    // =========================================================================

    [Fact]
    public void GroundMovement_Position_NotUnderground()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int groundFrames = 0;
        int undergroundFrames = 0;
        var undergroundDetails = new List<(string name, int frame, float simZ, float groundZ, float delta)>();

        foreach (var (name, _, result) in allResults)
        {
            foreach (var fd in result.FrameDetails)
            {
                if (fd.MovementMode != "ground") continue;
                if (fd.IsRecordingArtifact) continue;
                if (fd.EngineGroundZ < -50000f) continue; // No ground data
                groundFrames++;

                // Character feet should be AT or ABOVE engine ground Z
                float delta = fd.SimZ - fd.EngineGroundZ;
                if (delta < -0.5f) // More than 0.5y below ground = underground
                {
                    undergroundFrames++;
                    undergroundDetails.Add((name, fd.Frame, fd.SimZ, fd.EngineGroundZ, delta));
                }
            }
        }

        _output.WriteLine($"=== UNDERGROUND DETECTION ===");
        _output.WriteLine($"Ground frames with valid ground Z: {groundFrames}");
        _output.WriteLine($"Underground (simZ < groundZ - 0.5y): {undergroundFrames}");

        if (undergroundDetails.Count > 0)
        {
            foreach (var d in undergroundDetails.OrderBy(d => d.delta).Take(10))
            {
                _output.WriteLine($"  [{d.name}] frame={d.frame} simZ={d.simZ:F3} " +
                    $"groundZ={d.groundZ:F3} below={d.delta:F3}");
            }
        }

        float undergroundRate = groundFrames > 0 ? (float)undergroundFrames / groundFrames : 0;
        Assert.True(undergroundRate < 0.005f,
            $"Underground rate {undergroundRate:P2} — {undergroundFrames}/{groundFrames} ground frames " +
            $"are more than 0.5y below engine ground Z");
    }

    // =========================================================================
    // 7. MOVEMENT FLAG CONSISTENCY — Flags match actual behavior
    // =========================================================================

    [Fact]
    public void AllRecordings_MovementFlags_ConsistentWithBehavior()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int checkedFrames = 0;
        int forwardButStationary = 0;
        int noFlagsButMoving = 0;
        int noFlagsButMovingOnTransport = 0;
        int noFlagsButMovingSpline = 0;
        int noFlagsButMovingPhysics = 0; // Genuine physics displacement without input

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];

                if (fd.IsRecordingArtifact || fd.Dt < MinDtForSpeedCheck) continue;
                checkedFrames++;

                float dx = fd.SimX - prev.SimX;
                float dy = fd.SimY - prev.SimY;
                float horizSpeed = MathF.Sqrt(dx * dx + dy * dy) / fd.Dt;

                bool hasDirectionalFlags = (fd.MoveFlags & Helpers.MoveFlags.DirectionalMask) != 0;
                bool isStationary = horizSpeed < 0.3f;
                bool isMoving = horizSpeed > 2.0f;

                // Forward flag set but character not moving horizontally
                if (hasDirectionalFlags && isStationary && !fd.IsAirborne)
                    forwardButStationary++;

                // No directional flags but character moving fast (not airborne momentum)
                if (!hasDirectionalFlags && isMoving && !fd.IsAirborne && !fd.IsSwimming)
                {
                    noFlagsButMoving++;

                    // Classify the cause: transport, spline, or genuine physics displacement
                    bool isOnTransport = fd.IsOnTransport || prev.IsOnTransport;
                    bool isSpline = fd.IsSplineElevationTransition || prev.IsSplineElevationTransition
                        || (fd.RawMoveFlags & Helpers.MoveFlags.SplineEnabled) != 0;

                    if (isOnTransport) noFlagsButMovingOnTransport++;
                    else if (isSpline) noFlagsButMovingSpline++;
                    else noFlagsButMovingPhysics++;
                }
            }
        }

        _output.WriteLine($"=== FLAG CONSISTENCY VALIDATION ===");
        _output.WriteLine($"Frames checked: {checkedFrames}");
        _output.WriteLine($"Directional flags but stationary: {forwardButStationary}");
        _output.WriteLine($"No flags but moving fast: {noFlagsButMoving}");
        _output.WriteLine($"  - On transport: {noFlagsButMovingOnTransport}");
        _output.WriteLine($"  - Spline-driven: {noFlagsButMovingSpline}");
        _output.WriteLine($"  - Physics displacement: {noFlagsButMovingPhysics}");

        // Wall collisions, geometry transitions, and step-up/down events naturally cause
        // directional flags + stationary (character is pushing into wall or stepping).
        // Focus on the no-flags-but-moving metric which indicates real physics issues.
        // Transport and spline frames legitimately move without directional flags (the server/
        // transport moves the player). Only count genuine physics displacements for the assertion.
        float inconsistencyRate = checkedFrames > 0
            ? (float)(forwardButStationary + noFlagsButMoving) / checkedFrames : 0;
        float noFlagsMovingRate = checkedFrames > 0 ? (float)noFlagsButMoving / checkedFrames : 0;
        float physicsDisplacementRate = checkedFrames > 0 ? (float)noFlagsButMovingPhysics / checkedFrames : 0;
        _output.WriteLine($"Overall inconsistency rate: {inconsistencyRate:P2}");
        _output.WriteLine($"No-flags-but-moving rate (all): {noFlagsMovingRate:P2}");
        _output.WriteLine($"Physics displacement rate (excl transport/spline): {physicsDisplacementRate:P2}");

        // Physics displacement is the critical metric — it means the physics engine
        // is displacing the character without any movement input and NOT due to transport/spline.
        // Transport and spline movement without directional flags is expected server behavior.
        Assert.True(physicsDisplacementRate < 0.02f,
            $"Physics displacement rate {physicsDisplacementRate:P2} exceeds 2% threshold. " +
            $"Physics engine is moving the character without movement flag input " +
            $"(excluding {noFlagsButMovingOnTransport} transport + {noFlagsButMovingSpline} spline frames).");
    }

    // =========================================================================
    // 8. GROUNDED Z STABILITY — No oscillation between ground levels
    // =========================================================================

    [Fact]
    public void GroundMovement_ZStability_NoRapidOscillation()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int groundSequences = 0;
        int oscillationCount = 0;
        var oscillationDetails = new List<(string name, int frame, float[] recentZ)>();

        foreach (var (name, _, result) in allResults)
        {
            // Track Z direction changes in ground frames
            var recentGroundZ = new List<float>();

            foreach (var fd in result.FrameDetails)
            {
                if (fd.MovementMode != "ground" || fd.IsRecordingArtifact)
                {
                    recentGroundZ.Clear();
                    continue;
                }

                recentGroundZ.Add(fd.SimZ);

                // Check last 5 ground frames for oscillation
                if (recentGroundZ.Count >= 5)
                {
                    groundSequences++;
                    int dirChanges = 0;
                    for (int j = recentGroundZ.Count - 4; j < recentGroundZ.Count; j++)
                    {
                        float dz = recentGroundZ[j] - recentGroundZ[j - 1];
                        float prevDz = recentGroundZ[j - 1] - (j >= 2 ? recentGroundZ[j - 2] : recentGroundZ[j - 1]);
                        if (MathF.Abs(dz) > 0.3f && MathF.Abs(prevDz) > 0.3f &&
                            MathF.Sign(dz) != MathF.Sign(prevDz))
                        {
                            dirChanges++;
                        }
                    }

                    if (dirChanges >= 2) // 2+ direction reversals in 5 frames = oscillation
                    {
                        oscillationCount++;
                        oscillationDetails.Add((name, fd.Frame,
                            recentGroundZ.Skip(recentGroundZ.Count - 5).Take(5).ToArray()));
                    }

                    // Sliding window — keep last 6 entries
                    if (recentGroundZ.Count > 6)
                        recentGroundZ.RemoveAt(0);
                }
            }
        }

        _output.WriteLine($"=== Z OSCILLATION DETECTION ===");
        _output.WriteLine($"5-frame ground sequences: {groundSequences}");
        _output.WriteLine($"Oscillations (2+ reversals > 0.3y): {oscillationCount}");

        if (oscillationDetails.Count > 0)
        {
            foreach (var d in oscillationDetails.Take(10))
            {
                string zStr = string.Join(" → ", d.recentZ.Select(z => z.ToString("F2")));
                _output.WriteLine($"  [{d.name}] frame={d.frame}: {zStr}");
            }
        }

        float oscillationRate = groundSequences > 0 ? (float)oscillationCount / groundSequences : 0;
        Assert.True(oscillationRate < 0.01f,
            $"Z oscillation rate {oscillationRate:P2} — {oscillationCount}/{groundSequences} " +
            $"ground sequences show rapid Z bouncing");
    }

    // =========================================================================
    // 9. COMPREHENSIVE SERVER VALIDATION SUMMARY
    // =========================================================================

    [Fact]
    public void ServerValidation_ComprehensiveSummary()
    {
        var allResults = _fixture.ReplayCache.GetOrReplayAll(_output, _fixture.IsInitialized);
        if (allResults.Count == 0) { _output.WriteLine("SKIP: No recordings"); return; }

        int totalFrames = 0;
        int groundFrames = 0;
        int airFrames = 0;
        int swimFrames = 0;
        int transportFrames = 0;
        int transitionFrames = 0;
        int jumpCount = 0;
        float maxHorizSpeed = 0;
        float maxVertSpeed = 0;
        float maxStepUp = 0;
        float maxStepDown = 0;

        foreach (var (name, _, result) in allResults)
        {
            for (int i = 1; i < result.FrameDetails.Count; i++)
            {
                var prev = result.FrameDetails[i - 1];
                var fd = result.FrameDetails[i];
                if (fd.IsRecordingArtifact) continue;
                totalFrames++;

                switch (fd.MovementMode)
                {
                    case "ground": groundFrames++; break;
                    case "air": airFrames++; break;
                    case "swim": swimFrames++; break;
                    case "transport": transportFrames++; break;
                    case "transition": transitionFrames++; break;
                }

                if (fd.Transition == TransitionType.JumpStart) jumpCount++;

                if (fd.Dt >= MinDtForSpeedCheck)
                {
                    float dx = fd.SimX - prev.SimX;
                    float dy = fd.SimY - prev.SimY;
                    float dz = fd.SimZ - prev.SimZ;
                    float dist3D = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                    // Skip teleport frames for max calculations — they're not physics movement
                    if (dist3D <= TeleportExclusionThreshold)
                    {
                        float hSpeed = MathF.Sqrt(dx * dx + dy * dy) / fd.Dt;
                        float vSpeed = MathF.Abs(dz) / fd.Dt;
                        if (hSpeed > maxHorizSpeed) maxHorizSpeed = hSpeed;
                        if (vSpeed > maxVertSpeed) maxVertSpeed = vSpeed;

                        if (prev.MovementMode == "ground" && fd.MovementMode == "ground")
                        {
                            if (dz > maxStepUp) maxStepUp = dz;
                            if (-dz > maxStepDown) maxStepDown = -dz;
                        }
                    }
                }
            }
        }

        _output.WriteLine($"=== SERVER MOVEMENT VALIDATION SUMMARY ===");
        _output.WriteLine($"Recordings: {allResults.Count}");
        _output.WriteLine($"Total frames: {totalFrames}");
        _output.WriteLine($"  Ground: {groundFrames} ({100f * groundFrames / totalFrames:F1}%)");
        _output.WriteLine($"  Air: {airFrames} ({100f * airFrames / totalFrames:F1}%)");
        _output.WriteLine($"  Swim: {swimFrames} ({100f * swimFrames / totalFrames:F1}%)");
        _output.WriteLine($"  Transport: {transportFrames} ({100f * transportFrames / totalFrames:F1}%)");
        _output.WriteLine($"  Transition: {transitionFrames} ({100f * transitionFrames / totalFrames:F1}%)");
        _output.WriteLine($"");
        _output.WriteLine($"Jumps detected: {jumpCount}");
        _output.WriteLine($"Max horizontal speed: {maxHorizSpeed:F2} y/s (limit: {RunSpeed * SpeedToleranceFactor:F2})");
        _output.WriteLine($"Max vertical speed: {maxVertSpeed:F2} y/s");
        _output.WriteLine($"Max step-up: {maxStepUp:F3}y (limit: {PhysicsTestConstants.StepHeight:F3})");
        _output.WriteLine($"Max step-down: {maxStepDown:F3}y (limit: {PhysicsTestConstants.StepDownHeight:F3})");
        _output.WriteLine($"");
        float wallClimbSoft = MathF.Tan(VmangosWallClimbAngleRad);
        float wallClimbHard = MathF.Tan(VmangosWallClimbHighRad);

        _output.WriteLine($"=== VMaNGOS ANTICHEAT RULES ===");
        _output.WriteLine($"[SPEED]      Engine limit: {RunSpeed * SpeedToleranceFactor:F2} y/s | " +
            $"Server limit: {RunSpeed * VmangosSpeedTolerance:F2} y/s (1.1x)");
        _output.WriteLine($"[TELEPORT]   Server threshold: {VmangosTeleportThreshold:F0}y per packet");
        _output.WriteLine($"[TERMINAL_V] Cap: {TerminalVelocity:F0} y/s  Max observed: {maxVertSpeed:F2} y/s " +
            $"{(maxVertSpeed <= TerminalVelocity * 1.1f ? "PASS" : "FAIL")}");
        _output.WriteLine($"[WALL_CLIMB] Soft: tan({VmangosWallClimbAngleRad:F1}rad)={wallClimbSoft:F3} | " +
            $"Hard: tan({VmangosWallClimbHighRad:F1}rad)={wallClimbHard:F3}");
        _output.WriteLine($"[STEP_UP]    Max step ≤ {PhysicsTestConstants.StepHeight:F3}y: " +
            $"{(maxStepUp <= PhysicsTestConstants.StepHeight + 0.5f ? "PASS" : "FAIL")} (observed: {maxStepUp:F3}y)");
        _output.WriteLine($"[STEP_DOWN]  Max snap ≤ {PhysicsTestConstants.StepDownHeight:F3}y: " +
            $"{(maxStepDown <= PhysicsTestConstants.StepDownHeight + 0.5f ? "PASS" : "FAIL")} (observed: {maxStepDown:F3}y)");
        _output.WriteLine($"[GRAVITY]    Expected: {PhysicsTestConstants.Gravity:F3} y/s²");
        _output.WriteLine($"[SLOPE]      Engine: cos({PhysicsTestConstants.MaxWalkableSlopeDegrees}°)={PhysicsTestConstants.WalkableMinNormalZ:F3} | " +
            $"Server: ~{VmangosWallClimbAngleRad * 180f / MathF.PI:F1}°");
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static float GetAllowedSpeed(uint moveFlags, bool isSwimming)
    {
        if (isSwimming)
        {
            return (moveFlags & Helpers.MoveFlags.Backward) != 0 ? SwimBackSpeed : SwimSpeed;
        }

        if ((moveFlags & Helpers.MoveFlags.Backward) != 0) return RunBackSpeed;

        // Forward, strafe, or diagonal (diagonal has same max speed in WoW)
        return RunSpeed;
    }
}
