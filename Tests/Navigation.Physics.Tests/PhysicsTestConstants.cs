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
    /// WoW gravity in yards/second� (19.2911 is the authentic vanilla value)
    /// </summary>
    public const float Gravity = 19.2911f;

    /// <summary>
    /// Initial vertical velocity when jumping (yards/second).
    /// Computed inline in client as sqrt(2*g*maxJumpHeight), NOT a static constant.
    /// </summary>
    public const float JumpVelocity = 7.9535f;

    /// <summary>
    /// Water surface detection offset
    /// </summary>
    public const float WaterLevelDelta = 2.0f;

    /// <summary>
    /// Initial downward velocity when transitioning from grounded to freefall.
    /// Small nudge to prevent one-frame hover at vz=0.
    /// </summary>
    public const float FallStartVelocity = -0.1f;

    /// <summary>
    /// Terminal fall velocity (max vertical speed during free-fall).
    /// Address 0x0087D894 in 1.12.1.
    /// </summary>
    public const float TerminalVelocity = 60.148f;

    /// <summary>
    /// Terminal velocity with Safe Fall effect active.
    /// Address 0x0087D898 (adjacent to TerminalVelocity).
    /// </summary>
    public const float SafeFallTerminalVelocity = 7.0f;

    // ==========================================================================
    // BASE MOVEMENT SPEEDS (VMaNGOS baseMoveSpeed[])
    // ==========================================================================

    public const float BaseWalkSpeed = 2.5f;
    public const float BaseRunSpeed = 7.0f;
    public const float BaseRunBackSpeed = 4.5f;
    public const float BaseSwimSpeed = 4.722222f;    // server-side only
    public const float BaseSwimBackSpeed = 2.5f;
    public const float BaseTurnRate = 3.141594f;      // π rad/s

    // ==========================================================================
    // FALL DAMAGE
    // ==========================================================================

    /// <summary>Safe fall distance before damage starts (yards)</summary>
    public const float FallSafeDistance = 14.57f;

    /// <summary>Minimum fall time before damage (ms)</summary>
    public const float FallSafeTimeMs = 1229.0f;

    /// <summary>Fall damage coefficient: dmgPct = coeff * (zDiff - safeFall) - offset</summary>
    public const float FallDamageCoeff = 0.018f;

    /// <summary>Fall damage offset constant</summary>
    public const float FallDamageOffset = 0.2426f;

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
    /// cos(50°) = 0.6428, per WoW 1.12.1 client (0x0080DFFC). Slopes steeper than 50° are non-walkable.
    /// </summary>
    public const float WalkableMinNormalZ = 0.6428f;

    /// <summary>
    /// Maximum walkable slope angle in degrees (50° per WoW client)
    /// </summary>
    public const float MaxWalkableSlopeDegrees = 50.0f;

    /// <summary>
    /// tan(50°) — max Z-drop per unit horizontal distance on walkable slope.
    /// Used for cliff detection.
    /// </summary>
    public const float WalkableTanMaxSlope = 1.1918f;

    // ==========================================================================
    // TERRAIN NORMAL ESTIMATION
    // ==========================================================================

    /// <summary>
    /// XY offset for finite-difference terrain normal probes
    /// </summary>
    public const float NormalProbeOffset = 0.3f;

    // ==========================================================================
    // STEP-UP TOLERANCES
    // ==========================================================================

    /// <summary>
    /// Extra penetration tolerance added to capsule radius during step-up promotion
    /// </summary>
    public const float StepUpPenToleranceExtra = 0.05f;

    /// <summary>
    /// Max Z above pre-step position for step-up candidate promotion
    /// </summary>
    public const float MaxStepUpAbovePreStep = 1.5f;

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
    /// cos(120�) = -0.5
    /// </summary>
    public const float CeilingNormalZThreshold = -0.5f;

    // ==========================================================================
    // CONTACT TOLERANCES
    // ==========================================================================

    /// <summary>
    /// Epsilon for numerical comparisons (VECTOR_EPSILON in C++)
    /// </summary>
    public const float Epsilon = 1e-6f;

    /// <summary>
    /// Larger epsilon for ground snap / candidate sorting (GROUND_SNAP_EPSILON in C++)
    /// </summary>
    public const float LargeEpsilon = 1e-4f;

    /// <summary>
    /// Touch epsilon for contact detection
    /// </summary>
    public const float TouchEpsilon = 1e-3f;

    // ==========================================================================
    // COLLISION GEOMETRY
    // ==========================================================================

    /// <summary>
    /// Overlap normal Z filter — ignore overlaps whose normal Z exceeds this
    /// </summary>
    public const float OverlapNormalZFilter = 0.7f;

    /// <summary>
    /// Max deferred depenetration applied per physics tick
    /// </summary>
    public const float MaxDeferredDepenPerTick = 0.05f;

    /// <summary>
    /// Max overlap recovery iterations per tick
    /// </summary>
    public const int MaxOverlapRecoverIterations = 4;

    // ==========================================================================
    // WATER TRANSITION
    // ==========================================================================

    /// <summary>
    /// Velocity damping factor when entering water
    /// </summary>
    public const float WaterEntryVelocityDamp = 0.5f;
}
