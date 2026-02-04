// PhysicsTestConstants.cs - WoW Character Controller Physics Constants for Testing
// These values should match the C++ PhysicsConstants namespace exactly.

namespace Navigation.Physics.Tests;

/// <summary>
/// Physics constants that must match the C++ PhysicsEngine implementation.
/// Reference: Exports/Navigation/PhysicsEngine.h
/// </summary>
public static class PhysicsTestConstants
{
    // ==========================================================================
    // GRAVITY AND MOVEMENT
    // ==========================================================================

    /// <summary>
    /// WoW gravity in yards/second² (19.2911 is the authentic vanilla value)
    /// </summary>
    public const float Gravity = 19.2911f;

    /// <summary>
    /// Initial vertical velocity when jumping (yards/second)
    /// </summary>
    public const float JumpVelocity = 7.95577f;

    /// <summary>
    /// Water surface detection offset
    /// </summary>
    public const float WaterLevelDelta = 2.0f;

    // ==========================================================================
    // GROUND DETECTION
    // ==========================================================================

    /// <summary>
    /// Tolerance for considering a character "on ground" (yards)
    /// </summary>
    public const float GroundHeightTolerance = 0.04f;

    /// <summary>
    /// Maximum height the character can automatically step up (yards)
    /// WoW vanilla client allows approximately 2.1-2.2 unit step-ups
    /// </summary>
    public const float StepHeight = 2.125f;

    /// <summary>
    /// Maximum downward distance to snap to ground while remaining grounded (yards)
    /// </summary>
    public const float StepDownHeight = 4.0f;

    // ==========================================================================
    // HEIGHT CONSTANTS
    // ==========================================================================

    /// <summary>
    /// Sentinel value indicating invalid height
    /// </summary>
    public const float InvalidHeight = -200000.0f;

    /// <summary>
    /// Maximum valid height in the game world
    /// </summary>
    public const float MaxHeight = 100000.0f;

    /// <summary>
    /// Default vertical search distance for ground queries
    /// </summary>
    public const float DefaultHeightSearch = 50.0f;

    // ==========================================================================
    // SLOPE WALKABILITY
    // ==========================================================================

    /// <summary>
    /// Minimum Z component of surface normal to be considered walkable.
    /// cos(60°) = 0.5, meaning slopes steeper than 60° are not walkable.
    /// </summary>
    public const float WalkableMinNormalZ = 0.5f;

    /// <summary>
    /// Maximum walkable slope angle in degrees (60°)
    /// </summary>
    public const float MaxWalkableSlopeDegrees = 60.0f;

    // ==========================================================================
    // CAPSULE DIMENSIONS (Standard WoW Character)
    // ==========================================================================

    /// <summary>
    /// Default capsule radius for a standard character (yards)
    /// </summary>
    public const float DefaultCapsuleRadius = 0.5f;

    /// <summary>
    /// Default capsule height for a standard character (yards)
    /// </summary>
    public const float DefaultCapsuleHeight = 2.0f;

    // ==========================================================================
    // COLLIDE-AND-SLIDE
    // ==========================================================================

    /// <summary>
    /// Maximum iterations for collide-and-slide algorithm
    /// PhysX CCT uses 10 as the default
    /// </summary>
    public const int MaxSlideIterations = 10;

    /// <summary>
    /// Minimum distance threshold to consider movement (yards)
    /// </summary>
    public const float MinMoveDistance = 0.001f;

    // ==========================================================================
    // CEILING DETECTION
    // ==========================================================================

    /// <summary>
    /// Normal Z threshold for ceiling surfaces (downward-facing)
    /// cos(120°) = -0.5
    /// </summary>
    public const float CeilingNormalZThreshold = -0.5f;

    // ==========================================================================
    // CONTACT TOLERANCES
    // ==========================================================================

    /// <summary>
    /// Epsilon for numerical comparisons
    /// </summary>
    public const float Epsilon = 1e-6f;

    /// <summary>
    /// Larger epsilon for less precise comparisons
    /// </summary>
    public const float LargeEpsilon = 1e-4f;

    /// <summary>
    /// Touch epsilon for contact detection
    /// </summary>
    public const float TouchEpsilon = 1e-3f;
}
