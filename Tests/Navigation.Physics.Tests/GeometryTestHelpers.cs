// GeometryTestHelpers.cs - Helper methods for constructing test geometry
// Provides synthetic triangle configurations to test specific physics behaviors.

namespace Navigation.Physics.Tests;

using static NavigationInterop;

/// <summary>
/// Helper class for constructing synthetic geometry for physics tests.
/// </summary>
public static class GeometryTestHelpers
{
    // ==========================================================================
    // FLAT GROUND HELPERS
    // ==========================================================================

    /// <summary>
    /// Creates a flat horizontal ground triangle at the specified height.
    /// The triangle is large (20x20 yards) and centered at (centerX, centerY, height).
    /// Normal points up (0, 0, 1).
    /// </summary>
    public static Triangle CreateFlatGroundTriangle(float centerX, float centerY, float height, float size = 20.0f)
    {
        float half = size / 2.0f;
        return new Triangle(
            new Vector3(centerX - half, centerY - half, height),  // A: back-left
            new Vector3(centerX + half, centerY - half, height),  // B: back-right
            new Vector3(centerX, centerY + half, height)          // C: front-center
        );
    }

    /// <summary>
    /// Creates a quad (two triangles) representing flat ground.
    /// Returns an array of 2 triangles forming a square.
    /// </summary>
    public static Triangle[] CreateFlatGroundQuad(float centerX, float centerY, float height, float size = 20.0f)
    {
        float half = size / 2.0f;
        var bl = new Vector3(centerX - half, centerY - half, height);
        var br = new Vector3(centerX + half, centerY - half, height);
        var tr = new Vector3(centerX + half, centerY + half, height);
        var tl = new Vector3(centerX - half, centerY + half, height);

        return
        [
            new Triangle(bl, br, tr),  // First triangle
            new Triangle(bl, tr, tl)   // Second triangle
        ];
    }

    // ==========================================================================
    // SLOPE HELPERS
    // ==========================================================================

    /// <summary>
    /// Creates a slope triangle with the specified angle from horizontal.
    /// The slope rises in the +Y direction.
    /// </summary>
    /// <param name="centerX">Center X position</param>
    /// <param name="centerY">Center Y position</param>
    /// <param name="baseHeight">Height at the bottom of the slope</param>
    /// <param name="angleDegrees">Slope angle in degrees (0 = flat, 90 = vertical wall)</param>
    /// <param name="length">Length of the slope in the Y direction</param>
    public static Triangle CreateSlopeTriangleY(
        float centerX, float centerY, float baseHeight,
        float angleDegrees, float length = 10.0f)
    {
        float angleRad = angleDegrees * MathF.PI / 180.0f;
        float rise = length * MathF.Tan(angleRad);

        float halfWidth = 5.0f;
        float halfLength = length / 2.0f;

        // Bottom edge
        var bl = new Vector3(centerX - halfWidth, centerY - halfLength, baseHeight);
        var br = new Vector3(centerX + halfWidth, centerY - halfLength, baseHeight);
        // Top vertex
        var top = new Vector3(centerX, centerY + halfLength, baseHeight + rise);

        return new Triangle(bl, br, top);
    }

    /// <summary>
    /// Creates a slope quad (two triangles) with the specified angle.
    /// The slope rises in the +Y direction.
    /// </summary>
    public static Triangle[] CreateSlopeQuadY(
        float centerX, float centerY, float baseHeight,
        float angleDegrees, float length = 10.0f, float width = 10.0f)
    {
        float angleRad = angleDegrees * MathF.PI / 180.0f;
        float rise = length * MathF.Tan(angleRad);

        float halfWidth = width / 2.0f;
        float halfLength = length / 2.0f;

        var bl = new Vector3(centerX - halfWidth, centerY - halfLength, baseHeight);
        var br = new Vector3(centerX + halfWidth, centerY - halfLength, baseHeight);
        var tr = new Vector3(centerX + halfWidth, centerY + halfLength, baseHeight + rise);
        var tl = new Vector3(centerX - halfWidth, centerY + halfLength, baseHeight + rise);

        return
        [
            new Triangle(bl, br, tr),
            new Triangle(bl, tr, tl)
        ];
    }

    /// <summary>
    /// Calculates the expected normal Z component for a slope of the given angle.
    /// For a 60° slope, normalZ = cos(60°) = 0.5
    /// </summary>
    public static float GetSlopeNormalZ(float angleDegrees)
    {
        float angleRad = angleDegrees * MathF.PI / 180.0f;
        return MathF.Cos(angleRad);
    }

    // ==========================================================================
    // STEP/STAIR HELPERS
    // ==========================================================================

    /// <summary>
    /// Creates geometry representing a single step up.
    /// Returns triangles for: bottom ground, step face (vertical), step top.
    /// </summary>
    /// <param name="stepX">X position of the step edge</param>
    /// <param name="centerY">Center Y position</param>
    /// <param name="baseHeight">Ground height before the step</param>
    /// <param name="stepHeight">Height of the step (should be ? StepHeight constant for walkable)</param>
    /// <param name="width">Width of the step geometry</param>
    public static Triangle[] CreateStepGeometry(
        float stepX, float centerY, float baseHeight,
        float stepHeight, float width = 4.0f)
    {
        float halfWidth = width / 2.0f;
        float depthBefore = 5.0f;  // Ground before step
        float depthAfter = 5.0f;   // Ground after step

        // Ground before step (behind stepX)
        var g1_bl = new Vector3(stepX - depthBefore, centerY - halfWidth, baseHeight);
        var g1_br = new Vector3(stepX - depthBefore, centerY + halfWidth, baseHeight);
        var g1_tr = new Vector3(stepX, centerY + halfWidth, baseHeight);
        var g1_tl = new Vector3(stepX, centerY - halfWidth, baseHeight);

        // Step face (vertical)
        var sf_bl = new Vector3(stepX, centerY - halfWidth, baseHeight);
        var sf_br = new Vector3(stepX, centerY + halfWidth, baseHeight);
        var sf_tr = new Vector3(stepX, centerY + halfWidth, baseHeight + stepHeight);
        var sf_tl = new Vector3(stepX, centerY - halfWidth, baseHeight + stepHeight);

        // Step top (after step)
        var st_bl = new Vector3(stepX, centerY - halfWidth, baseHeight + stepHeight);
        var st_br = new Vector3(stepX, centerY + halfWidth, baseHeight + stepHeight);
        var st_tr = new Vector3(stepX + depthAfter, centerY + halfWidth, baseHeight + stepHeight);
        var st_tl = new Vector3(stepX + depthAfter, centerY - halfWidth, baseHeight + stepHeight);

        return
        [
            // Ground before
            new Triangle(g1_bl, g1_tl, g1_tr),
            new Triangle(g1_bl, g1_tr, g1_br),
            // Step face
            new Triangle(sf_bl, sf_tl, sf_tr),
            new Triangle(sf_bl, sf_tr, sf_br),
            // Step top
            new Triangle(st_bl, st_tl, st_tr),
            new Triangle(st_bl, st_tr, st_br)
        ];
    }

    // ==========================================================================
    // WALL HELPERS
    // ==========================================================================

    /// <summary>
    /// Creates a vertical wall facing the +X direction (normal = (1, 0, 0)).
    /// </summary>
    public static Triangle CreateWallFacingPlusX(
        float wallX, float centerY, float baseHeight,
        float height = 5.0f, float width = 10.0f)
    {
        float halfWidth = width / 2.0f;

        var bl = new Vector3(wallX, centerY - halfWidth, baseHeight);
        var br = new Vector3(wallX, centerY + halfWidth, baseHeight);
        var top = new Vector3(wallX, centerY, baseHeight + height);

        return new Triangle(bl, br, top);
    }

    /// <summary>
    /// Creates a vertical wall quad facing the +X direction.
    /// </summary>
    public static Triangle[] CreateWallQuadFacingPlusX(
        float wallX, float centerY, float baseHeight,
        float height = 5.0f, float width = 10.0f)
    {
        float halfWidth = width / 2.0f;

        var bl = new Vector3(wallX, centerY - halfWidth, baseHeight);
        var br = new Vector3(wallX, centerY + halfWidth, baseHeight);
        var tr = new Vector3(wallX, centerY + halfWidth, baseHeight + height);
        var tl = new Vector3(wallX, centerY - halfWidth, baseHeight + height);

        return
        [
            new Triangle(bl, br, tr),
            new Triangle(bl, tr, tl)
        ];
    }

    // ==========================================================================
    // CORNER HELPERS
    // ==========================================================================

    /// <summary>
    /// Creates an inside corner (two perpendicular walls meeting).
    /// Useful for testing collide-and-slide corner behavior.
    /// </summary>
    public static Triangle[] CreateInsideCorner(
        float cornerX, float cornerY, float baseHeight,
        float height = 5.0f, float length = 10.0f)
    {
        // Wall along Y axis (facing +X)
        var w1_bl = new Vector3(cornerX, cornerY, baseHeight);
        var w1_br = new Vector3(cornerX, cornerY + length, baseHeight);
        var w1_tr = new Vector3(cornerX, cornerY + length, baseHeight + height);
        var w1_tl = new Vector3(cornerX, cornerY, baseHeight + height);

        // Wall along X axis (facing +Y)
        var w2_bl = new Vector3(cornerX, cornerY, baseHeight);
        var w2_br = new Vector3(cornerX + length, cornerY, baseHeight);
        var w2_tr = new Vector3(cornerX + length, cornerY, baseHeight + height);
        var w2_tl = new Vector3(cornerX, cornerY, baseHeight + height);

        return
        [
            // Wall 1 (facing +X)
            new Triangle(w1_bl, w1_br, w1_tr),
            new Triangle(w1_bl, w1_tr, w1_tl),
            // Wall 2 (facing +Y)
            new Triangle(w2_bl, w2_br, w2_tr),
            new Triangle(w2_bl, w2_tr, w2_tl)
        ];
    }

    // ==========================================================================
    // CEILING HELPERS
    // ==========================================================================

    /// <summary>
    /// Creates a ceiling triangle at the specified height.
    /// Normal points down (0, 0, -1).
    /// </summary>
    public static Triangle CreateCeilingTriangle(float centerX, float centerY, float height, float size = 20.0f)
    {
        float half = size / 2.0f;
        // Reversed winding for downward-facing normal
        return new Triangle(
            new Vector3(centerX - half, centerY + half, height),  // front-left
            new Vector3(centerX + half, centerY - half, height),  // back-right
            new Vector3(centerX - half, centerY - half, height)   // back-left
        );
    }

    // ==========================================================================
    // VALIDATION HELPERS
    // ==========================================================================

    /// <summary>
    /// Validates that a surface normal indicates a walkable slope.
    /// </summary>
    public static bool IsWalkableNormal(Vector3 normal) =>
        normal.Z >= PhysicsTestConstants.WalkableMinNormalZ;

    /// <summary>
    /// Validates that a surface normal indicates a ceiling.
    /// </summary>
    public static bool IsCeilingNormal(Vector3 normal) =>
        normal.Z <= PhysicsTestConstants.CeilingNormalZThreshold;

    /// <summary>
    /// Creates a capsule positioned with feet at the given coordinates.
    /// </summary>
    public static Capsule CreateCapsuleAtFeet(
        float x, float y, float feetZ,
        float radius = PhysicsTestConstants.DefaultCapsuleRadius,
        float height = PhysicsTestConstants.DefaultCapsuleHeight) =>
        Capsule.FromFeetPosition(x, y, feetZ, radius, height);
}
