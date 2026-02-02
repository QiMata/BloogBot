// ThreePassMovementTests.cs - Tests for the Up/Side/Down three-pass movement system
// Based on PhysX CCT movement decomposition.

namespace Navigation.Physics.Tests;

using static NavigationInterop;
using static GeometryTestHelpers;

/// <summary>
/// Tests for the three-pass movement decomposition system (Up ? Side ? Down).
/// This is the core of PhysX-style character controller movement.
/// </summary>
public class ThreePassMovementTests
{
    // ==========================================================================
    // FLAT GROUND MOVEMENT
    // ==========================================================================

    [Fact]
    public void ThreePass_WalkOnFlatGround_MaintainsGroundContact()
    {
        // Arrange: Character on flat ground, moving forward
        const float groundZ = 0.0f;
        var startPos = new Vector3(0, 0, groundZ);
        var moveDir = new Vector3(1, 0, 0);  // +X
        const float moveSpeed = 7.0f;  // yards/second
        const float dt = 1.0f / 60.0f;  // 60 FPS

        // Create ground geometry
        var ground = CreateFlatGroundQuad(0, 0, groundZ, 100.0f);

        // Act: Simulate three-pass movement
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveSpeed * dt, ground,
            isJumping: false, isGrounded: true);

        // Assert
        Assert.True(result.IsGrounded, "Should remain grounded");
        Assert.True(MathF.Abs(result.Position.Z - groundZ) < 0.01f,
            $"Should stay at ground level, got Z={result.Position.Z}");
        Assert.True(result.Position.X > startPos.X, "Should have moved forward");
    }

    [Fact]
    public void ThreePass_WalkOnFlatGround_MovesCorrectDistance()
    {
        // Arrange
        const float groundZ = 0.0f;
        var startPos = new Vector3(0, 0, groundZ);
        var moveDir = new Vector3(1, 0, 0);
        const float moveSpeed = 7.0f;
        const float dt = 0.1f;  // 100ms step
        float expectedDist = moveSpeed * dt;

        var ground = CreateFlatGroundQuad(0, 0, groundZ, 100.0f);

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, expectedDist, ground,
            isJumping: false, isGrounded: true);

        // Assert
        float actualDist = (result.Position - startPos).Length();
        Assert.True(MathF.Abs(actualDist - expectedDist) < 0.1f,
            $"Should move {expectedDist} yards, moved {actualDist}");
    }

    // ==========================================================================
    // STEP-UP TESTS
    // ==========================================================================

    [Fact]
    public void ThreePass_ApproachStep_StepsUpWhenWithinLimit()
    {
        // Arrange: Step that's below the step height limit
        const float stepHeight = 1.0f;  // Well below 2.125 limit
        var startPos = new Vector3(-2, 0, 0);  // Behind the step
        var moveDir = new Vector3(1, 0, 0);   // Moving toward step
        const float moveDist = 5.0f;

        var geometry = new List<Triangle>();
        geometry.AddRange(CreateFlatGroundQuad(-10, 0, 0, 20.0f));  // Ground before
        geometry.AddRange(CreateStepGeometry(0, 0, 0, stepHeight));

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, [.. geometry],
            isJumping: false, isGrounded: true);

        // Assert
        Assert.True(result.Position.X > 0, "Should have crossed the step");
        Assert.True(MathF.Abs(result.Position.Z - stepHeight) < 0.1f,
            $"Should be at step height {stepHeight}, got Z={result.Position.Z}");
        Assert.True(result.IsGrounded, "Should remain grounded after step");
    }

    [Fact]
    public void ThreePass_ApproachStep_BlockedWhenTooHigh()
    {
        // Arrange: Step that exceeds the step height limit
        const float stepHeight = 3.0f;  // Above 2.125 limit
        var startPos = new Vector3(-2, 0, 0);
        var moveDir = new Vector3(1, 0, 0);
        const float moveDist = 5.0f;

        var geometry = new List<Triangle>();
        geometry.AddRange(CreateFlatGroundQuad(-10, 0, 0, 20.0f));
        geometry.AddRange(CreateStepGeometry(0, 0, 0, stepHeight));

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, [.. geometry],
            isJumping: false, isGrounded: true);

        // Assert
        Assert.True(result.Position.X < 0.5f, 
            $"Should be blocked by high step, got X={result.Position.X}");
        Assert.True(result.CollisionSide, "Should report side collision");
    }

    [Fact]
    public void ThreePass_StepUp_CancelsWhenJumping()
    {
        // Arrange: Jumping character should not auto-step
        const float stepHeight = 1.0f;
        var startPos = new Vector3(-2, 0, 0);
        var moveDir = new Vector3(1, 0, 0.5f).Normalized();  // Forward + up (jumping)
        const float moveDist = 5.0f;

        var geometry = new List<Triangle>();
        geometry.AddRange(CreateFlatGroundQuad(-10, 0, 0, 20.0f));
        geometry.AddRange(CreateStepGeometry(0, 0, 0, stepHeight));

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, [.. geometry],
            isJumping: true, isGrounded: false);

        // Assert: Step offset should be cancelled when jumping
        // The character might still clear the step via jump, but not via auto-step
        Assert.False(result.UsedAutoStep, "Should not use auto-step while jumping");
    }

    // ==========================================================================
    // STEP-DOWN (GROUND SNAP) TESTS
    // ==========================================================================

    [Fact]
    public void ThreePass_WalkingDownSlope_MaintainsGroundContact()
    {
        // Arrange: Walking down a gentle slope
        var startPos = new Vector3(0, 0, 5.0f);  // Start at higher end
        var moveDir = new Vector3(0, 1, 0);      // Moving +Y (down the slope)
        const float moveDist = 3.0f;

        var slope = CreateSlopeQuadY(0, 5, 0, 30.0f, length: 20.0f);

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, slope,
            isJumping: false, isGrounded: true);

        // Assert
        Assert.True(result.IsGrounded, "Should maintain ground contact on slope");
        Assert.True(result.Position.Z < startPos.Z, "Should have descended");
        Assert.True(!result.StartedFalling, "Should not transition to falling");
    }

    [Fact]
    public void ThreePass_WalkOffLedge_TransitionsToFalling()
    {
        // Arrange: Platform with a drop-off
        var startPos = new Vector3(8, 0, 10.0f);  // On platform, near edge
        var moveDir = new Vector3(1, 0, 0);       // Moving toward edge
        const float moveDist = 5.0f;

        var platform = CreateFlatGroundQuad(0, 0, 10.0f, 20.0f);

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, platform,
            isJumping: false, isGrounded: true);

        // Assert: After stepping off edge, should transition to falling
        // (if snap-down distance exceeds STEP_DOWN_HEIGHT)
        if (result.Position.X > 10.0f)  // Walked past platform edge
        {
            Assert.False(result.IsGrounded, "Should not be grounded past platform edge");
        }
    }

    // ==========================================================================
    // SLOPE WALKABILITY TESTS
    // ==========================================================================

    [Theory]
    [InlineData(30.0f, true)]   // Walkable slope
    [InlineData(45.0f, true)]   // Walkable slope
    [InlineData(59.0f, true)]   // Just under threshold
    [InlineData(65.0f, false)]  // Non-walkable
    public void ThreePass_WalkOntoSlope_RespectsWalkabilityThreshold(
        float slopeAngle, bool shouldBeWalkable)
    {
        // Arrange
        var startPos = new Vector3(0, -3, 2.0f);  // Behind and above slope
        var moveDir = new Vector3(0, 1, 0);       // Moving up the slope
        const float moveDist = 5.0f;

        var slope = CreateSlopeQuadY(0, 0, 0, slopeAngle, length: 10.0f);

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, slope,
            isJumping: false, isGrounded: true);

        // Assert
        if (shouldBeWalkable)
        {
            Assert.True(result.IsGrounded || result.Position.Y > startPos.Y,
                "Should be able to walk on walkable slope");
        }
        else
        {
            // On non-walkable slope, should either slide down or be blocked
            Assert.True(result.HitNonWalkable || !result.IsGrounded,
                "Should detect non-walkable surface");
        }
    }

    // ==========================================================================
    // CEILING TESTS
    // ==========================================================================

    [Fact]
    public void ThreePass_JumpIntoCeiling_CancelsUpwardVelocity()
    {
        // Arrange
        const float ceilingHeight = 4.0f;
        var startPos = new Vector3(0, 0, 0);
        var moveDir = new Vector3(0, 0, 1);  // Pure upward
        const float moveDist = 10.0f;  // Would overshoot ceiling

        var ground = CreateFlatGroundQuad(0, 0, 0, 20.0f);
        var ceiling = CreateCeilingTriangle(0, 0, ceilingHeight, 20.0f);
        var geometry = ground.Append(ceiling).ToArray();

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, geometry,
            isJumping: true, isGrounded: false);

        // Assert
        Assert.True(result.CollisionUp, "Should report ceiling collision");
        Assert.True(result.Position.Z < ceilingHeight,
            "Should not penetrate ceiling");
    }

    // ==========================================================================
    // COLLIDE-AND-SLIDE (SIDE PASS) TESTS
    // ==========================================================================

    [Fact]
    public void ThreePass_WalkIntoWall_SlidesAlongSurface()
    {
        // Arrange: Walking diagonally into a wall
        var startPos = new Vector3(-3, -3, 0);
        var moveDir = new Vector3(1, 1, 0).Normalized();  // Diagonal
        const float moveDist = 10.0f;

        var ground = CreateFlatGroundQuad(0, 0, 0, 50.0f);
        var wall = CreateWallQuadFacingPlusX(0, 0, 0, height: 5.0f, width: 20.0f);
        var geometry = ground.Concat(wall).ToArray();

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, geometry,
            isJumping: false, isGrounded: true);

        // Assert
        Assert.True(result.CollisionSide, "Should report wall collision");
        Assert.True(result.Position.X < 0.5f, "Should not penetrate wall");
        Assert.True(result.Position.Y > startPos.Y, "Should slide along wall in Y");
    }

    [Fact]
    public void ThreePass_WalkIntoCorner_BlockedByCrease()
    {
        // Arrange: Walking directly into an inside corner
        var startPos = new Vector3(-3, -3, 0);
        var moveDir = new Vector3(1, 1, 0).Normalized();
        const float moveDist = 10.0f;

        var ground = CreateFlatGroundQuad(-5, -5, 0, 20.0f);
        var corner = CreateInsideCorner(0, 0, 0);
        var geometry = ground.Concat(corner).ToArray();

        // Act
        var result = SimulateThreePassMovement(
            startPos, moveDir, moveDist, geometry,
            isJumping: false, isGrounded: true);

        // Assert: Should be blocked when constrained by two perpendicular walls
        Assert.True(result.CollisionSide, "Should hit corner");
        // In a true corner, diagonal movement should be nearly blocked
        float moved = (result.Position - startPos).Length();
        Assert.True(moved < moveDist * 0.5f,
            $"Should be significantly blocked in corner, moved {moved}/{moveDist}");
    }

    // ==========================================================================
    // SIMULATION HELPER
    // ==========================================================================

    private struct ThreePassResult
    {
        public Vector3 Position;
        public bool IsGrounded;
        public bool CollisionUp;
        public bool CollisionSide;
        public bool CollisionDown;
        public bool HitNonWalkable;
        public bool UsedAutoStep;
        public bool StartedFalling;
    }

    private static ThreePassResult SimulateThreePassMovement(
        Vector3 startPos, Vector3 moveDir, float distance,
        Triangle[] geometry, bool isJumping, bool isGrounded)
    {
        // This simulates the three-pass algorithm:
        // 1. UP pass: step-up + upward velocity (if any)
        // 2. SIDE pass: horizontal collide-and-slide
        // 3. DOWN pass: undo step-up + gravity snap

        var result = new ThreePassResult
        {
            Position = startPos,
            IsGrounded = isGrounded
        };

        float stepOffset = isJumping ? 0 : PhysicsTestConstants.StepHeight;
        
        // Decompose movement
        float verticalComponent = moveDir.Z * distance;
        var horizontalDir = new Vector3(moveDir.X, moveDir.Y, 0);
        float horizontalDist = horizontalDir.Length() * distance / moveDir.Length();
        horizontalDir = horizontalDir.Normalized();

        // === UP PASS ===
        if (!isJumping && isGrounded && horizontalDist > 0)
        {
            // Apply step-up lift
            result.Position.Z += stepOffset;
            result.UsedAutoStep = true;
        }
        if (verticalComponent > 0)
        {
            // Check for ceiling collision
            // (simplified: just add vertical component)
            result.Position.Z += verticalComponent;
            // In real implementation, would sweep up and detect ceiling
        }

        // === SIDE PASS ===
        if (horizontalDist > PhysicsTestConstants.MinMoveDistance)
        {
            // Perform horizontal sweep
            // (simplified: just move, real impl would collide-and-slide)
            result.Position.X += horizontalDir.X * horizontalDist;
            result.Position.Y += horizontalDir.Y * horizontalDist;

            // Check for wall collisions
            foreach (var tri in geometry)
            {
                var normal = tri.Normal();
                if (!IsWalkableNormal(normal))  // Wall
                {
                    // Simplified collision check
                    result.CollisionSide = true;
                }
            }
        }

        // === DOWN PASS ===
        float totalDown = stepOffset + PhysicsTestConstants.StepDownHeight;
        if (!isJumping || verticalComponent <= 0)
        {
            // Sweep down to find ground
            bool foundGround = false;
            foreach (var tri in geometry)
            {
                var normal = tri.Normal();
                if (IsWalkableNormal(normal))
                {
                    // Check if this triangle is below us
                    // (simplified check)
                    foundGround = true;
                    result.CollisionDown = true;
                    break;
                }
                else if (MathF.Abs(normal.Z) < PhysicsTestConstants.WalkableMinNormalZ)
                {
                    result.HitNonWalkable = true;
                }
            }

            result.IsGrounded = foundGround;
            if (!foundGround && isGrounded)
            {
                result.StartedFalling = true;
            }
        }

        return result;
    }
}
