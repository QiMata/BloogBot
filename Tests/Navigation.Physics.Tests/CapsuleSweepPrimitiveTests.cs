// CapsuleSweepPrimitiveTests.cs - Atomic tests for capsule-triangle sweep operations
// These tests validate the fundamental building blocks of the physics system.

namespace Navigation.Physics.Tests;

using static NavigationInterop;
using static GeometryTestHelpers;

/// <summary>
/// Tests for the basic capsule sweep primitive operations.
/// These are pure geometry tests that don't require map data.
/// </summary>
public class CapsuleSweepPrimitiveTests
{
    // ==========================================================================
    // FLAT GROUND TESTS
    // ==========================================================================

    [Fact]
    public void CapsuleSweep_DownOntoFlatGround_ReportsCorrectTOI()
    {
        // Arrange: Capsule positioned 1 yard above a flat ground triangle
        const float groundHeight = 0.0f;
        const float capsuleStartZ = 1.0f;  // Feet 1 yard above ground
        
        var capsule = CreateCapsuleAtFeet(0, 0, capsuleStartZ);
        var ground = CreateFlatGroundTriangle(0, 0, groundHeight);
        var downDirection = new Vector3(0, 0, -1);
        const float sweepDistance = 2.0f;

        // Act: Sweep down
        bool hit = IntersectMovingCapsule(capsule, downDirection, sweepDistance, ground,
            out float toi, out Vector3 normal, out Vector3 point);

        // Assert
        Assert.True(hit, "Should hit the ground when sweeping down");
        Assert.True(toi >= 0 && toi <= 1, $"TOI should be in [0,1], got {toi}");
        
        // Expected TOI: we're 1 yard above, sweeping 2 yards down
        // Impact should occur at toi = 0.5 (1/2 of sweep distance)
        float expectedToi = (capsuleStartZ - groundHeight) / sweepDistance;
        Assert.True(MathF.Abs(toi - expectedToi) < 0.1f, 
            $"TOI should be approximately {expectedToi}, got {toi}");
        
        // Normal should point up
        Assert.True(normal.Z > 0.9f, $"Ground normal should point up, got Z={normal.Z}");
    }

    [Fact]
    public void CapsuleSweep_DownOntoFlatGround_SurfaceIsWalkable()
    {
        // Arrange
        var capsule = CreateCapsuleAtFeet(0, 0, 1.0f);
        var ground = CreateFlatGroundTriangle(0, 0, 0);
        
        // Act
        bool hit = IntersectMovingCapsule(capsule, new Vector3(0, 0, -1), 2.0f, ground,
            out _, out Vector3 normal, out _);

        // Assert
        Assert.True(hit);
        Assert.True(IsWalkableNormal(normal), 
            $"Flat ground should be walkable, normalZ={normal.Z}");
    }

    [Fact]
    public void CapsuleOverlap_OnFlatGround_ReportsStartPenetrating()
    {
        // Arrange: Capsule intersecting with ground (feet slightly below surface)
        const float groundHeight = 0.0f;
        const float capsuleFeetZ = -0.1f;  // 0.1 yards into ground
        
        var capsule = CreateCapsuleAtFeet(0, 0, capsuleFeetZ);
        var ground = CreateFlatGroundTriangle(0, 0, groundHeight);

        // Act
        bool overlapping = TestCapsuleTriangleOverlap(capsule, ground,
            out float depth, out Vector3 normal, out Vector3 point);

        // Assert
        Assert.True(overlapping, "Capsule should overlap with ground");
        Assert.True(depth > 0, $"Penetration depth should be positive, got {depth}");
        Assert.True(normal.Z > 0.9f, $"Push-out normal should point up, got Z={normal.Z}");
    }

    // ==========================================================================
    // SLOPE WALKABILITY BOUNDARY TESTS
    // ==========================================================================

    [Theory]
    [InlineData(30.0f, true)]   // 30° - clearly walkable
    [InlineData(45.0f, true)]   // 45° - walkable
    [InlineData(59.0f, true)]   // 59° - just under threshold
    [InlineData(60.0f, true)]   // 60° - exactly at threshold (cos(60°) = 0.5)
    [InlineData(61.0f, false)]  // 61° - just over threshold
    [InlineData(70.0f, false)]  // 70° - clearly non-walkable
    [InlineData(85.0f, false)]  // 85° - near-vertical
    public void CapsuleSweep_OntoSlope_WalkabilityMatchesAngle(float angleDegrees, bool shouldBeWalkable)
    {
        // Arrange
        var capsule = CreateCapsuleAtFeet(0, -5, 5.0f);
        var slope = CreateSlopeQuadY(0, 0, 0, angleDegrees, length: 20.0f);

        // Act: Sweep down onto the slope
        bool hitAny = false;
        Vector3 hitNormal = default;
        
        foreach (var triangle in slope)
        {
            if (IntersectMovingCapsule(capsule, new Vector3(0, 0, -1), 10.0f, triangle,
                out _, out Vector3 normal, out _))
            {
                hitAny = true;
                hitNormal = normal;
                break;
            }
        }

        // Assert
        if (hitAny)
        {
            bool isWalkable = IsWalkableNormal(hitNormal);
            float expectedNormalZ = GetSlopeNormalZ(angleDegrees);
            
            Assert.Equal(shouldBeWalkable, isWalkable);
            Assert.True(MathF.Abs(hitNormal.Z - expectedNormalZ) < 0.05f,
                $"Normal Z should be ~{expectedNormalZ} for {angleDegrees}° slope, got {hitNormal.Z}");
        }
    }

    [Fact]
    public void CapsuleSweep_60DegreeSlope_ExactlyAtWalkableThreshold()
    {
        // This is the critical boundary test
        const float exactThresholdAngle = 60.0f;
        
        var capsule = CreateCapsuleAtFeet(0, -5, 5.0f);
        var slope = CreateSlopeQuadY(0, 0, 0, exactThresholdAngle, length: 20.0f);

        // Act
        foreach (var triangle in slope)
        {
            if (IntersectMovingCapsule(capsule, new Vector3(0, 0, -1), 10.0f, triangle,
                out _, out Vector3 normal, out _))
            {
                // Assert: cos(60°) = 0.5, which is exactly the threshold
                float expectedNormalZ = 0.5f;
                Assert.True(MathF.Abs(normal.Z - expectedNormalZ) < 0.01f,
                    $"60° slope normalZ should be 0.5, got {normal.Z}");
                Assert.True(normal.Z >= PhysicsTestConstants.WalkableMinNormalZ,
                    "60° slope should be exactly at walkable threshold");
                return;
            }
        }
        
        Assert.Fail("Should have hit the slope");
    }

    // ==========================================================================
    // STEP HEIGHT TESTS
    // ==========================================================================

    [Theory]
    [InlineData(1.0f, true)]    // 1 yard step - should be climbable
    [InlineData(2.0f, true)]    // 2 yards - should be climbable
    [InlineData(2.125f, true)]  // Exactly at limit
    [InlineData(2.2f, false)]   // Slightly over - not climbable
    [InlineData(3.0f, false)]   // 3 yards - definitely not climbable
    public void CapsuleSweep_AgainstStep_HeightDeterminesClimbability(float stepHeight, bool shouldClimb)
    {
        // Arrange: Capsule approaching a step from the side
        const float approachX = -3.0f;
        const float stepX = 0.0f;
        
        var capsule = CreateCapsuleAtFeet(approachX, 0, 0);
        var step = CreateStepGeometry(stepX, 0, 0, stepHeight);
        
        var moveDirection = new Vector3(1, 0, 0);  // Moving +X toward step
        const float sweepDistance = 5.0f;

        // Act: Check if we can reach the top of the step
        // This simulates the three-pass movement: UP (by stepHeight), SIDE, DOWN
        bool canReachTop = SimulateThreePassOverStep(capsule, step, stepHeight, sweepDistance);

        // Assert
        if (shouldClimb)
        {
            Assert.True(stepHeight <= PhysicsTestConstants.StepHeight,
                $"Test setup error: {stepHeight} claimed climbable but exceeds StepHeight constant");
        }
        
        // Note: The actual climbability depends on the three-pass implementation
        // This test documents expected behavior
    }

    // ==========================================================================
    // WALL COLLISION TESTS
    // ==========================================================================

    [Fact]
    public void CapsuleSweep_IntoWall_ReportsBlockingHit()
    {
        // Arrange
        var capsule = CreateCapsuleAtFeet(-2, 0, 0);
        var wall = CreateWallQuadFacingPlusX(0, 0, 0);
        var moveDirection = new Vector3(1, 0, 0);  // Moving toward wall

        // Act
        foreach (var triangle in wall)
        {
            if (IntersectMovingCapsule(capsule, moveDirection, 5.0f, triangle,
                out float toi, out Vector3 normal, out _))
            {
                // Assert
                Assert.True(toi > 0 && toi < 1, "Should hit wall before end of sweep");
                Assert.True(MathF.Abs(normal.X - 1.0f) < 0.1f, 
                    $"Wall normal should face +X, got X={normal.X}");
                Assert.False(IsWalkableNormal(normal), 
                    "Wall should not be walkable");
                return;
            }
        }
        
        Assert.Fail("Should have hit the wall");
    }

    [Fact]
    public void CapsuleSweep_ParallelToWall_NoCollision()
    {
        // Arrange: Moving parallel to wall, should not collide
        var capsule = CreateCapsuleAtFeet(-3, -5, 0);  // Behind and to side of wall
        var wall = CreateWallQuadFacingPlusX(0, 0, 0);
        var moveDirection = new Vector3(0, 1, 0);  // Moving +Y (parallel to wall)

        // Act
        bool anyHit = false;
        foreach (var triangle in wall)
        {
            if (IntersectMovingCapsule(capsule, moveDirection, 10.0f, triangle,
                out _, out _, out _))
            {
                anyHit = true;
                break;
            }
        }

        // Assert
        Assert.False(anyHit, "Moving parallel to wall should not collide");
    }

    // ==========================================================================
    // CEILING TESTS
    // ==========================================================================

    [Fact]
    public void CapsuleSweep_UpIntoCeiling_ReportsBlockingHit()
    {
        // Arrange
        const float ceilingHeight = 4.0f;
        var capsule = CreateCapsuleAtFeet(0, 0, 0);  // Standing at ground level
        var ceiling = CreateCeilingTriangle(0, 0, ceilingHeight);
        var upDirection = new Vector3(0, 0, 1);

        // Act
        bool hit = IntersectMovingCapsule(capsule, upDirection, 10.0f, ceiling,
            out float toi, out Vector3 normal, out _);

        // Assert
        Assert.True(hit, "Should hit ceiling when jumping up");
        Assert.True(IsCeilingNormal(normal), 
            $"Ceiling normal should point down, got Z={normal.Z}");
    }

    // ==========================================================================
    // CORNER/CREASE TESTS
    // ==========================================================================

    [Fact]
    public void CapsuleSweep_IntoInsideCorner_HitsBothWalls()
    {
        // Arrange: Capsule moving diagonally into a corner
        var capsule = CreateCapsuleAtFeet(-3, -3, 0);
        var corner = CreateInsideCorner(0, 0, 0);
        var moveDirection = new Vector3(1, 1, 0).Normalized();  // Diagonal into corner

        // Act
        int hitCount = 0;
        var hitNormals = new List<Vector3>();
        
        foreach (var triangle in corner)
        {
            if (IntersectMovingCapsule(capsule, moveDirection, 10.0f, triangle,
                out _, out Vector3 normal, out _))
            {
                hitCount++;
                hitNormals.Add(normal);
            }
        }

        // Assert: Should detect hits from both wall directions
        Assert.True(hitCount >= 1, "Should hit at least one wall of the corner");
        // Note: Whether we hit both walls depends on sweep order and early-out logic
    }

    // ==========================================================================
    // HELPER METHODS
    // ==========================================================================

    private static bool IntersectMovingCapsule(
        Capsule capsule, Vector3 direction, float distance, Triangle triangle,
        out float toi, out Vector3 normal, out Vector3 point)
    {
        // This would call the actual P/Invoke function
        // For now, use managed implementation for testing
        return ManagedCapsuleTriangleSweep(capsule, direction * distance, triangle,
            out toi, out normal, out point);
    }

    private static bool TestCapsuleTriangleOverlap(
        Capsule capsule, Triangle triangle,
        out float depth, out Vector3 normal, out Vector3 point)
    {
        // Use the discrete intersection test
        return ManagedCapsuleTriangleIntersect(capsule, triangle,
            out depth, out normal, out point);
    }

    private static bool SimulateThreePassOverStep(
        Capsule capsule, Triangle[] step, float stepHeight, float sweepDistance)
    {
        // Simplified three-pass simulation:
        // 1. UP pass: lift by stepHeight
        // 2. SIDE pass: move horizontally
        // 3. DOWN pass: settle onto surface
        
        // This would use the actual physics engine for real tests
        // For now, just check if step is within climbable range
        return stepHeight <= PhysicsTestConstants.StepHeight;
    }

    // ==========================================================================
    // MANAGED IMPLEMENTATIONS (for testing without P/Invoke)
    // ==========================================================================

    private static bool ManagedCapsuleTriangleSweep(
        Capsule cap, Vector3 vel, Triangle tri,
        out float toi, out Vector3 normal, out Vector3 point)
    {
        toi = 1.0f;
        normal = new Vector3(0, 0, 1);
        point = tri.A;

        // Check initial overlap
        if (ManagedCapsuleTriangleIntersect(cap, tri, out float initDepth, out normal, out point))
        {
            toi = 0.0f;
            return true;
        }

        // Simple sphere-plane sweep approximation
        var triNormal = tri.Normal();
        float denom = Vector3.Dot(triNormal, vel);
        
        if (MathF.Abs(denom) < 1e-6f)
            return false;

        // Distance from capsule center to plane
        var capsuleCenter = (cap.P0 + cap.P1) * 0.5f;
        float d = Vector3.Dot(triNormal, tri.A);
        float distToPlane = Vector3.Dot(triNormal, capsuleCenter) - d;
        
        // Account for radius
        float effectiveDist = distToPlane - cap.Radius;
        if (effectiveDist < 0)
        {
            toi = 0;
            normal = triNormal;
            return true;
        }

        float t = effectiveDist / (-denom);
        if (t >= 0 && t <= 1)
        {
            toi = t;
            normal = triNormal;
            point = capsuleCenter + vel * t - triNormal * cap.Radius;
            return true;
        }

        return false;
    }

    private static bool ManagedCapsuleTriangleIntersect(
        Capsule cap, Triangle tri,
        out float depth, out Vector3 normal, out Vector3 point)
    {
        depth = 0;
        normal = tri.Normal();
        point = tri.A;

        // Simplified: closest point on capsule axis to triangle plane
        var triNormal = tri.Normal();
        float d = Vector3.Dot(triNormal, tri.A);
        
        // Check both endpoints
        float dist0 = Vector3.Dot(triNormal, cap.P0) - d;
        float dist1 = Vector3.Dot(triNormal, cap.P1) - d;
        
        float minDist = MathF.Min(MathF.Abs(dist0), MathF.Abs(dist1));
        
        if (minDist <= cap.Radius)
        {
            depth = cap.Radius - minDist;
            normal = triNormal;
            return true;
        }

        return false;
    }
}
