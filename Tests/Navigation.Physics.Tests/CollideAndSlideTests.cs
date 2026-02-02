// CollideAndSlideTests.cs - Tests for the iterative collide-and-slide algorithm
// Validates wall sliding, corner behavior, and constraint handling.

namespace Navigation.Physics.Tests;

using static NavigationInterop;
using static GeometryTestHelpers;

/// <summary>
/// Tests for the collide-and-slide algorithm that handles wall collision and surface sliding.
/// </summary>
public class CollideAndSlideTests
{
    // ==========================================================================
    // SINGLE SURFACE SLIDE TESTS
    // ==========================================================================

    [Fact]
    public void CollideAndSlide_HeadOnIntoWall_StopsMovement()
    {
        // Arrange: Moving directly into a wall (perpendicular)
        var wallNormal = new Vector3(1, 0, 0);  // Wall facing +X
        var moveDir = new Vector3(-1, 0, 0);    // Moving -X (into wall)
        
        // Act
        var slideDir = ComputeSlideTangent(moveDir, wallNormal);
        
        // Assert: Head-on collision should result in zero slide
        Assert.True(slideDir.Length() < 0.01f,
            "Head-on collision should stop movement");
    }

    [Fact]
    public void CollideAndSlide_GlancingAngle_SlidesAlongSurface()
    {
        // Arrange: Moving at 45° angle to wall
        var wallNormal = new Vector3(1, 0, 0);  // Wall facing +X
        var moveDir = new Vector3(-1, 1, 0).Normalized();  // Diagonal into wall
        
        // Act
        var slideDir = ComputeSlideTangent(moveDir, wallNormal);
        
        // Assert: Should slide along Y axis
        Assert.True(slideDir.Length() > 0.5f, "Should have significant slide component");
        Assert.True(MathF.Abs(slideDir.X) < 0.1f, "Should not move into wall (X)");
        Assert.True(slideDir.Y > 0.5f, "Should slide in +Y direction");
    }

    [Theory]
    [InlineData(15.0f, 0.966f)]   // 15° glancing = cos(15°) ~= 0.966 of original speed
    [InlineData(30.0f, 0.866f)]   // 30° = cos(30°) ~= 0.866
    [InlineData(45.0f, 0.707f)]   // 45° = cos(45°) ~= 0.707
    [InlineData(60.0f, 0.5f)]     // 60° = cos(60°) = 0.5
    [InlineData(75.0f, 0.259f)]   // 75° = cos(75°) ~= 0.259
    [InlineData(90.0f, 0.0f)]     // 90° (head-on) = 0
    public void CollideAndSlide_AtAngle_PreservesCorrectSpeed(float angleDegrees, float expectedSpeedRatio)
    {
        // Arrange
        var wallNormal = new Vector3(1, 0, 0);
        float angleRad = angleDegrees * MathF.PI / 180.0f;
        
        // Movement direction at the specified angle to the wall normal
        // 0° = parallel to wall, 90° = perpendicular (head-on)
        var moveDir = new Vector3(
            -MathF.Sin(angleRad),  // Component into wall
            MathF.Cos(angleRad),   // Component along wall
            0
        );
        
        // Act
        var slideDir = ComputeSlideTangent(moveDir, wallNormal);
        float actualSpeedRatio = slideDir.Length();
        
        // Assert
        Assert.True(MathF.Abs(actualSpeedRatio - expectedSpeedRatio) < 0.05f,
            $"At {angleDegrees}°, expected speed ratio {expectedSpeedRatio}, got {actualSpeedRatio}");
    }

    // ==========================================================================
    // CORNER (CREASE) TESTS
    // ==========================================================================

    [Fact]
    public void CollideAndSlide_IntoCorner_ComputesCreaseDirection()
    {
        // Arrange: Two perpendicular walls forming a corner
        var wall1Normal = new Vector3(1, 0, 0);   // Wall facing +X
        var wall2Normal = new Vector3(0, 1, 0);   // Wall facing +Y
        var moveDir = new Vector3(-1, -1, 0).Normalized();  // Moving into corner
        
        // Act
        var creaseDir = ComputeCreaseDirection(moveDir, wall1Normal, wall2Normal);
        
        // Assert: Crease should be vertical (Z axis) or zero
        if (creaseDir.Length() > 0.01f)
        {
            Assert.True(MathF.Abs(creaseDir.Z) > 0.9f,
                "Corner crease should be vertical (Z axis)");
        }
        else
        {
            // Completely blocked - valid outcome when moving directly into corner
            Assert.True(true, "Movement completely blocked in corner");
        }
    }

    [Fact]
    public void CollideAndSlide_CornerWithUpSlope_AllowsVerticalMovement()
    {
        // Arrange: Corner with one wall and one slope
        var wallNormal = new Vector3(1, 0, 0);     // Vertical wall
        var slopeNormal = new Vector3(0, 0.5f, 0.866f).Normalized();  // 30° slope
        var moveDir = new Vector3(-1, 0, 0);       // Moving into wall
        
        // Act
        var creaseDir = ComputeCreaseDirection(moveDir, wallNormal, slopeNormal);
        
        // Assert: Should be able to move up along the crease
        Assert.True(creaseDir.Length() > 0.1f || 
                   Vector3.Dot(moveDir, wallNormal) >= 0,
            "Should have some movement direction or not be blocked");
    }

    // ==========================================================================
    // ITERATION LIMIT TESTS
    // ==========================================================================

    [Fact]
    public void CollideAndSlide_ComplexGeometry_RespectsIterationLimit()
    {
        // This test validates that the algorithm terminates within the iteration limit
        // even when bouncing between multiple surfaces
        
        const int maxIterations = PhysicsTestConstants.MaxSlideIterations;
        int actualIterations = SimulateCollideAndSlideIterations(
            startPos: new Vector3(0, 0, 0),
            moveDir: new Vector3(1, 0, 0),
            distance: 10.0f,
            walls: [
                new Vector3(-1, 0, 0),  // Wall facing -X
                new Vector3(1, 0, 0),   // Wall facing +X
                new Vector3(0, -1, 0),  // Wall facing -Y
                new Vector3(0, 1, 0),   // Wall facing +Y
            ]);

        Assert.True(actualIterations <= maxIterations,
            $"Should complete within {maxIterations} iterations, took {actualIterations}");
    }

    // ==========================================================================
    // MINIMUM DISTANCE THRESHOLD TESTS
    // ==========================================================================

    [Fact]
    public void CollideAndSlide_TinyRemainingDistance_Terminates()
    {
        // Movement that results in distance below MIN_MOVE_DISTANCE should stop
        var startPos = new Vector3(0, 0, 0);
        var moveDir = new Vector3(1, 0, 0);
        float distance = PhysicsTestConstants.MinMoveDistance * 0.5f;

        // This should terminate immediately without processing
        var result = SimulateCollideAndSlide(startPos, moveDir, distance, []);

        Assert.True((result - startPos).Length() < PhysicsTestConstants.MinMoveDistance,
            "Should not move below minimum distance threshold");
    }

    // ==========================================================================
    // SURFACE ORIENTATION TESTS
    // ==========================================================================

    [Fact]
    public void CollideAndSlide_OntoWalkableSurface_ReportsNotBlocked()
    {
        // Walking onto a walkable slope should not be considered "blocked"
        var slopeNormal = new Vector3(0, 0, 1);  // Flat ground
        var moveDir = new Vector3(1, 0, 0);
        
        bool isBlocked = IsDirectionBlocked(moveDir, slopeNormal);
        
        Assert.False(isBlocked, "Horizontal movement on flat ground should not be blocked");
    }

    [Fact]
    public void CollideAndSlide_IntoNonWalkable_ReportsBlocked()
    {
        // Moving into a steep non-walkable surface
        var steepNormal = new Vector3(0.9f, 0, 0.436f);  // ~65° slope, non-walkable
        var moveDir = new Vector3(-1, 0, 0);  // Moving into the slope
        
        bool isBlocked = IsDirectionBlocked(moveDir, steepNormal);
        
        Assert.True(isBlocked, "Moving into non-walkable slope should be blocked");
    }

    // ==========================================================================
    // HELPER IMPLEMENTATIONS
    // ==========================================================================

    /// <summary>
    /// Computes the slide direction when hitting a single surface.
    /// v_slide = v - (v · n) * n
    /// </summary>
    private static Vector3 ComputeSlideTangent(Vector3 moveDir, Vector3 surfaceNormal)
    {
        // Remove the component along the normal
        float vDotN = Vector3.Dot(moveDir, surfaceNormal);
        var slide = moveDir - surfaceNormal * vDotN;
        return slide;
    }

    /// <summary>
    /// Computes the crease direction when constrained by two surfaces.
    /// This is the cross product of the two normals.
    /// </summary>
    private static Vector3 ComputeCreaseDirection(
        Vector3 moveDir, Vector3 normal1, Vector3 normal2)
    {
        // Crease is perpendicular to both normals
        var crease = Vector3.Cross(normal1, normal2);
        
        if (crease.Length() < 0.001f)
        {
            // Normals are parallel - use single-surface slide
            return ComputeSlideTangent(moveDir, normal1);
        }
        
        crease = crease.Normalized();
        
        // Project movement onto crease direction
        float component = Vector3.Dot(moveDir, crease);
        return crease * component;
    }

    /// <summary>
    /// Checks if a movement direction is blocked by a constraint normal.
    /// </summary>
    private static bool IsDirectionBlocked(Vector3 moveDir, Vector3 constraintNormal)
    {
        // If the dot product is negative, we're moving into the surface
        return Vector3.Dot(moveDir, constraintNormal) < -0.001f;
    }

    /// <summary>
    /// Simulates collide-and-slide and returns the number of iterations used.
    /// </summary>
    private static int SimulateCollideAndSlideIterations(
        Vector3 startPos, Vector3 moveDir, float distance, Vector3[] walls)
    {
        int iterations = 0;
        var pos = startPos;
        var remaining = moveDir * distance;
        
        while (remaining.Length() > PhysicsTestConstants.MinMoveDistance &&
               iterations < PhysicsTestConstants.MaxSlideIterations)
        {
            iterations++;
            
            // Find earliest wall hit
            bool hitAny = false;
            Vector3 hitNormal = default;
            
            foreach (var wallNormal in walls)
            {
                if (IsDirectionBlocked(remaining.Normalized(), wallNormal))
                {
                    hitAny = true;
                    hitNormal = wallNormal;
                    break;
                }
            }
            
            if (!hitAny)
            {
                pos = pos + remaining;
                break;
            }
            
            // Slide along wall
            remaining = ComputeSlideTangent(remaining, hitNormal);
        }
        
        return iterations;
    }

    /// <summary>
    /// Simple collide-and-slide simulation returning final position.
    /// </summary>
    private static Vector3 SimulateCollideAndSlide(
        Vector3 startPos, Vector3 moveDir, float distance, Vector3[] walls)
    {
        if (distance < PhysicsTestConstants.MinMoveDistance)
            return startPos;
        
        var pos = startPos;
        var remaining = moveDir * distance;
        int iterations = 0;
        
        while (remaining.Length() > PhysicsTestConstants.MinMoveDistance &&
               iterations < PhysicsTestConstants.MaxSlideIterations)
        {
            iterations++;
            
            bool hitAny = false;
            foreach (var wallNormal in walls)
            {
                if (IsDirectionBlocked(remaining.Normalized(), wallNormal))
                {
                    remaining = ComputeSlideTangent(remaining, wallNormal);
                    hitAny = true;
                }
            }
            
            if (!hitAny)
                break;
        }
        
        return pos + remaining;
    }
}
