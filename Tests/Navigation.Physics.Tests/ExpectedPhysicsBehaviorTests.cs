// ExpectedPhysicsBehaviorTests.cs - Documents and validates expected physics behavior
// These tests define the "contract" for how physics should behave in specific scenarios.

namespace Navigation.Physics.Tests;

using static NavigationInterop;

/// <summary>
/// Tests that document and validate expected physics behavior.
/// Each test represents a specific scenario with mathematically predicted outcomes.
/// 
/// These tests can run without map data by using known physics equations.
/// They serve as a specification for the physics engine.
/// </summary>
public class ExpectedPhysicsBehaviorTests
{
    // ==========================================================================
    // GRAVITY AND FALLING
    // ==========================================================================

    [Fact]
    public void Falling_FromRest_AccelerationMatchesGravity()
    {
        // Physics: v = g * t, d = 0.5 * g * t²
        const float dt = 1.0f / 60.0f;
        const float g = PhysicsTestConstants.Gravity;

        // After 1 frame of falling:
        float v1 = g * dt;  // velocity after 1 frame
        float d1 = 0.5f * g * dt * dt;  // distance fallen in 1 frame

        // After 10 frames:
        float t10 = 10 * dt;
        float v10 = g * t10;
        float d10 = 0.5f * g * t10 * t10;

        // After 1 second (60 frames):
        float t60 = 1.0f;
        float v60 = g * t60;  // ~19.29 yards/second
        float d60 = 0.5f * g * t60 * t60;  // ~9.65 yards

        // Assert expected values
        Assert.True(MathF.Abs(v60 - g) < 0.01f, $"After 1 second, velocity should be {g}, got {v60}");
        Assert.True(MathF.Abs(d60 - 9.65f) < 0.1f, $"After 1 second, should fall ~9.65 yards, got {d60}");
    }

    [Theory]
    [InlineData(1, 0.0027f)]   // 1 frame: 0.0027 yards
    [InlineData(10, 0.27f)]    // 10 frames: 0.27 yards  
    [InlineData(30, 2.41f)]    // 30 frames (0.5s): 2.41 yards
    [InlineData(60, 9.65f)]    // 60 frames (1s): 9.65 yards
    [InlineData(120, 38.58f)]  // 120 frames (2s): 38.58 yards
    public void Falling_DistanceAfterFrames_MatchesEquation(int frames, float expectedDistance)
    {
        const float dt = 1.0f / 60.0f;
        const float g = PhysicsTestConstants.Gravity;

        float t = frames * dt;
        float calculatedDistance = 0.5f * g * t * t;

        Assert.True(MathF.Abs(calculatedDistance - expectedDistance) < 0.1f,
            $"After {frames} frames ({t}s), expected {expectedDistance} yards, calculated {calculatedDistance}");
    }

    // ==========================================================================
    // JUMP PHYSICS
    // ==========================================================================

    [Fact]
    public void Jump_MaxHeight_CalculatedCorrectly()
    {
        // Max height = v0² / (2g)
        float v0 = PhysicsTestConstants.JumpVelocity;
        float g = PhysicsTestConstants.Gravity;

        float maxHeight = (v0 * v0) / (2 * g);

        // Expected: ~1.64 yards (about 5 feet)
        Assert.True(MathF.Abs(maxHeight - 1.64f) < 0.05f,
            $"Max jump height should be ~1.64 yards, got {maxHeight}");
    }

    [Fact]
    public void Jump_TimeToApex_CalculatedCorrectly()
    {
        // Time to apex = v0 / g
        float v0 = PhysicsTestConstants.JumpVelocity;
        float g = PhysicsTestConstants.Gravity;

        float timeToApex = v0 / g;

        // Expected: ~0.41 seconds
        Assert.True(MathF.Abs(timeToApex - 0.41f) < 0.02f,
            $"Time to apex should be ~0.41s, got {timeToApex}");
    }

    [Fact]
    public void Jump_TotalAirTime_CalculatedCorrectly()
    {
        // Total air time = 2 * v0 / g
        float v0 = PhysicsTestConstants.JumpVelocity;
        float g = PhysicsTestConstants.Gravity;

        float totalAirTime = 2 * v0 / g;

        // Expected: ~0.82 seconds
        Assert.True(MathF.Abs(totalAirTime - 0.82f) < 0.03f,
            $"Total air time should be ~0.82s, got {totalAirTime}");
    }

    [Fact]
    public void Jump_FrameByFrame_HeightProgression()
    {
        const float dt = 1.0f / 60.0f;
        float v0 = PhysicsTestConstants.JumpVelocity;
        float g = PhysicsTestConstants.Gravity;

        // Simulate jump frame by frame
        var heights = new List<(int frame, float height, float velocity)>();
        
        float z = 0;
        float vz = v0;

        for (int frame = 0; vz > 0 || z > 0; frame++)
        {
            heights.Add((frame, z, vz));
            
            // Euler integration
            vz -= g * dt;
            z += vz * dt;

            if (frame > 100) break;  // Safety
        }

        // Find apex
        var apex = heights.MaxBy(h => h.height);

        // Verify apex is at expected time (~25 frames)
        int expectedApexFrame = (int)(v0 / g / dt);
        Assert.True(MathF.Abs(apex.frame - expectedApexFrame) <= 2,
            $"Apex should be at frame ~{expectedApexFrame}, got frame {apex.frame}");

        // Verify apex height
        float expectedApexHeight = (v0 * v0) / (2 * g);
        Assert.True(MathF.Abs(apex.height - expectedApexHeight) < 0.1f,
            $"Apex height should be ~{expectedApexHeight}, got {apex.height}");
    }

    // ==========================================================================
    // GROUND MOVEMENT
    // ==========================================================================

    [Theory]
    [InlineData(7.0f, 1.0f, 7.0f)]     // Run speed: 7 yards/s * 1s = 7 yards
    [InlineData(7.0f, 0.5f, 3.5f)]     // Half second
    [InlineData(2.5f, 1.0f, 2.5f)]     // Walk speed
    [InlineData(7.0f, 1.0f/60.0f, 0.1167f)]  // One frame
    public void GroundMovement_DistanceOverTime_IsLinear(float speed, float time, float expectedDistance)
    {
        float calculatedDistance = speed * time;

        Assert.True(MathF.Abs(calculatedDistance - expectedDistance) < 0.01f,
            $"At {speed} yards/s for {time}s, expected {expectedDistance}, got {calculatedDistance}");
    }

    [Fact]
    public void GroundMovement_OneFrame_AtRunSpeed()
    {
        const float runSpeed = 7.0f;
        const float dt = 1.0f / 60.0f;

        float distancePerFrame = runSpeed * dt;

        // ~0.117 yards per frame at run speed
        Assert.True(MathF.Abs(distancePerFrame - 0.1167f) < 0.001f,
            $"Should move ~0.117 yards per frame, got {distancePerFrame}");
    }

    // ==========================================================================
    // RUNNING JUMP DISTANCE
    // ==========================================================================

    [Fact]
    public void RunningJump_HorizontalDistance_Calculated()
    {
        float v0 = PhysicsTestConstants.JumpVelocity;
        float g = PhysicsTestConstants.Gravity;
        float runSpeed = 7.0f;

        // Time in air
        float airTime = 2 * v0 / g;

        // Horizontal distance (assuming no air control changes speed)
        float horizontalDistance = runSpeed * airTime;

        // Expected: ~5.76 yards
        Assert.True(MathF.Abs(horizontalDistance - 5.76f) < 0.1f,
            $"Running jump distance should be ~5.76 yards, got {horizontalDistance}");
    }

    // ==========================================================================
    // SLOPE PHYSICS
    // ==========================================================================

    [Theory]
    [InlineData(0, 1.0f, true)]     // Flat: cos(0) = 1.0
    [InlineData(30, 0.866f, true)]  // 30 deg: cos(30) = 0.866
    [InlineData(45, 0.707f, true)]  // 45 deg: cos(45) = 0.707
    [InlineData(60, 0.5f, true)]    // 60 deg: cos(60) = 0.5 (boundary)
    [InlineData(65, 0.423f, false)] // 65 deg: cos(65) = 0.423
    [InlineData(90, 0.0f, false)]   // 90 deg: cos(90) = 0 (wall)
    public void SlopeWalkability_NormalZMatchesAngle(float angleDegrees, float expectedNormalZ, bool shouldBeWalkable)
    {
        float angleRad = angleDegrees * MathF.PI / 180.0f;
        float calculatedNormalZ = MathF.Cos(angleRad);

        Assert.True(MathF.Abs(calculatedNormalZ - expectedNormalZ) < 0.01f,
            $"cos({angleDegrees}°) should be {expectedNormalZ}, got {calculatedNormalZ}");

        bool isWalkable = calculatedNormalZ >= PhysicsTestConstants.WalkableMinNormalZ;
        Assert.Equal(shouldBeWalkable, isWalkable);
    }

    // ==========================================================================
    // STEP HEIGHT SCENARIOS
    // ==========================================================================

    [Theory]
    [InlineData(0.5f, true)]   // Half yard step - climbable
    [InlineData(1.0f, true)]   // 1 yard step - climbable
    [InlineData(1.5f, true)]   // 1.5 yard step - climbable
    [InlineData(2.0f, true)]   // 2 yard step - climbable
    [InlineData(2.125f, true)] // Exactly at limit - climbable
    [InlineData(2.2f, false)]  // Just over - NOT climbable
    [InlineData(3.0f, false)]  // 3 yard step - NOT climbable
    public void StepHeight_Climbability_DependsOnHeight(float stepHeight, bool shouldBeClimbable)
    {
        bool isClimbable = stepHeight <= PhysicsTestConstants.StepHeight;
        Assert.Equal(shouldBeClimbable, isClimbable);
    }

    // ==========================================================================
    // COLLISION SLIDING
    // ==========================================================================

    [Fact]
    public void WallSlide_45DegreeAngle_PreservesHalfSpeed()
    {
        // Moving diagonally into wall at 45°
        // Slide velocity = original * cos(45°) = 0.707
        var moveDir = new Vector3(1, 1, 0).Normalized();
        var wallNormal = new Vector3(1, 0, 0);

        // Compute slide: v_slide = v - (v·n)n
        float vDotN = Vector3.Dot(moveDir, wallNormal);
        var slide = moveDir - wallNormal * vDotN;

        float slideSpeed = slide.Length();
        float expectedSlideSpeed = MathF.Cos(45 * MathF.PI / 180);

        Assert.True(MathF.Abs(slideSpeed - expectedSlideSpeed) < 0.01f,
            $"45° slide should preserve {expectedSlideSpeed:F3} of speed, got {slideSpeed:F3}");
    }

    [Theory]
    [InlineData(0, 1.0f)]    // Parallel to wall: full speed
    [InlineData(30, 0.866f)] // 30° to wall
    [InlineData(45, 0.707f)] // 45° to wall
    [InlineData(60, 0.5f)]   // 60° to wall
    [InlineData(90, 0.0f)]   // Head-on: zero slide
    public void WallSlide_SpeedPreservation_ByAngle(float angleDegrees, float expectedSpeedRatio)
    {
        float angleRad = angleDegrees * MathF.PI / 180.0f;
        float calculatedRatio = MathF.Cos(angleRad);

        Assert.True(MathF.Abs(calculatedRatio - expectedSpeedRatio) < 0.01f,
            $"At {angleDegrees}°, should preserve {expectedSpeedRatio} of speed, calculated {calculatedRatio}");
    }

    // ==========================================================================
    // FRAME TIMING
    // ==========================================================================

    [Fact]
    public void FrameTiming_60FPS_CorrectDeltaTime()
    {
        const float targetFPS = 60.0f;
        const float expectedDt = 1.0f / targetFPS;

        Assert.True(MathF.Abs(expectedDt - 0.01667f) < 0.0001f,
            $"60 FPS delta time should be ~0.01667, got {expectedDt}");
    }

    [Fact]
    public void FrameTiming_DistancePerFrame_AtRunSpeed()
    {
        const float runSpeed = 7.0f;  // yards/second
        const float dt = 1.0f / 60.0f;

        float distancePerFrame = runSpeed * dt;

        // Document expected movement per frame
        // ~0.117 yards = ~0.35 feet = ~4.2 inches per frame
        Assert.True(distancePerFrame > 0.1f && distancePerFrame < 0.15f,
            $"Should move 0.1-0.15 yards per frame at run speed, got {distancePerFrame}");
    }

    // ==========================================================================
    // COMBINED SCENARIOS
    // ==========================================================================

    [Fact]
    public void Scenario_JumpOverGap_MinimumGapClearable()
    {
        float v0 = PhysicsTestConstants.JumpVelocity;
        float g = PhysicsTestConstants.Gravity;
        float runSpeed = 7.0f;

        // Maximum horizontal distance from running jump
        float airTime = 2 * v0 / g;
        float maxJumpDistance = runSpeed * airTime;

        // A player should be able to clear at least a 5-yard gap
        Assert.True(maxJumpDistance >= 5.0f,
            $"Should be able to jump at least 5 yards, can jump {maxJumpDistance:F2}");
    }

    [Fact]
    public void Scenario_FallDamage_HeightThreshold()
    {
        // In WoW, fall damage typically starts around 10-15 yards
        // Fatal fall is around 50+ yards
        // This documents the expected fall distances for reference

        float g = PhysicsTestConstants.Gravity;

        // Time to fall 10 yards: d = 0.5 * g * t² => t = sqrt(2d/g)
        float timeToFall10 = MathF.Sqrt(2 * 10 / g);  // ~1.02 seconds
        float timeToFall50 = MathF.Sqrt(2 * 50 / g);  // ~2.28 seconds

        // Velocity at impact
        float impactVelocity10 = g * timeToFall10;  // ~19.6 yards/s
        float impactVelocity50 = g * timeToFall50;  // ~43.9 yards/s

        // Document for reference
        Assert.True(timeToFall10 > 0.5f && timeToFall10 < 1.5f, 
            $"10 yard fall time: {timeToFall10:F2}s");
        Assert.True(impactVelocity50 > impactVelocity10, 
            "Higher falls have greater impact velocity");
    }
}
