// PhysicsThreePass.h - Three-pass movement decomposition system
// PhysX CCT-style UP ? SIDE ? DOWN movement decomposition.
#pragma once

#include "Vector3.h"
#include "PhysicsBridge.h"
#include "PhysicsCollideSlide.h"
#include <cstdint>

namespace PhysicsThreePass
{
    using SlideResult = PhysicsCollideSlide::SlideResult;

    // Decomposed movement vectors for 3-pass system
    struct DecomposedMovement
    {
        G3D::Vector3 upVector;          // Vertical upward component (step-up + jump)
        G3D::Vector3 sideVector;        // Horizontal/planar component
        G3D::Vector3 downVector;        // Vertical downward component (gravity + undo step)
        float stepOffset;               // Auto-step height to apply (may be cancelled)
        bool isMovingUp;                // True if vertical intent is upward (jumping)
        bool hasSideMovement;           // True if there's meaningful lateral motion
    };

    // Result of the 3-pass movement
    struct ThreePassResult
    {
        G3D::Vector3 finalPosition;     // Final position after all passes
        bool collisionUp;               // Hit something during UP pass
        bool collisionSide;             // Hit something during SIDE pass  
        bool collisionDown;             // Hit something during DOWN pass (landed)
        bool hitNonWalkable;            // Landed on or hit a non-walkable slope
        float actualStepUpDelta;        // How much we actually rose in UP pass
        G3D::Vector3 groundNormal;      // Normal of ground surface (if landed)
    };

    // Internal movement state for three-pass operations
    struct ThreePassState
    {
        float x, y, z;
        float vx, vy, vz;
        float orientation;
        float pitch;
        bool isGrounded;
        bool isSwimming;
        float fallTime;
        G3D::Vector3 groundNormal;
    };

    /// Decomposes a movement direction into up/side/down components.
    /// Handles step offset injection and cancellation based on movement intent.
    DecomposedMovement DecomposeMovement(
        const G3D::Vector3& direction,
        const G3D::Vector3& upDirection,
        float stepOffset,
        bool isJumping,
        bool standingOnMoving);

    /// Constrained climbing mode configuration
    struct ClimbingSensorConfig
    {
        bool enabled;           // Whether to use constrained climbing mode
        float sensorDistance;   // Forward sensor sweep distance (default: radius * 2)
        float maxClimbAngle;    // Maximum angle (in degrees) for climbable surfaces
    };

    /// Returns the default climbing sensor configuration
    ClimbingSensorConfig GetDefaultClimbingSensorConfig(float radius);

    /// Performs a forward sensor sweep to detect climbable geometry.
    /// Returns true if climbable geometry is detected ahead, meaning step-up should proceed.
    /// Returns false if there's nothing to climb, so step-up should be skipped.
    /// @param mapId The current map ID
    /// @param st Current movement state
    /// @param radius Capsule radius
    /// @param height Capsule height
    /// @param sideVector The horizontal movement direction
    /// @param stepOffset The step height being attempted
    /// @param config Climbing sensor configuration
    bool PerformClimbingSensorSweep(
        uint32_t mapId,
        const ThreePassState& st,
        float radius,
        float height,
        const G3D::Vector3& sideVector,
        float stepOffset,
        const ClimbingSensorConfig& config);

    /// Executes the UP pass: step-up lift + any upward movement intent
    SlideResult ExecuteUpPass(
        uint32_t mapId,
        ThreePassState& st,
        float radius,
        float height,
        const DecomposedMovement& decomposed,
        float& clampedStepOffset);

    /// Executes the SIDE pass: horizontal collide-and-slide
    SlideResult ExecuteSidePass(
        uint32_t mapId,
        ThreePassState& st,
        float radius,
        float height,
        const DecomposedMovement& decomposed);

    /// Executes the DOWN pass: undo step offset + downward movement + ground snap
    SlideResult ExecuteDownPass(
        uint32_t mapId,
        ThreePassState& st,
        float radius,
        float height,
        const DecomposedMovement& decomposed,
        float clampedStepOffset);

    /// Validates slope after the DOWN pass - checks if landed surface is walkable.
    bool ValidateSlopeAfterDownPass(
        const G3D::Vector3& contactNormal,
        float contactHeight,
        float originalBottomZ,
        float stepOffset);

    /// Enhanced slope validation using triangle height range.
    /// This provides more accurate slope validation by considering:
    ///   - Contact normal (slope angle)
    ///   - Triangle height span (terrain roughness)
    ///   - Step height constraints
    /// @param contactNormal Normal of the contact surface
    /// @param heightRange Height range data from contacted triangles
    /// @param stepOffset Maximum allowed step height
    /// @return true if the surface is walkable, false otherwise
    bool ValidateSlopeWithHeightRange(
        const G3D::Vector3& contactNormal,
        const PhysicsCollideSlide::TriangleHeightRange& heightRange,
        float stepOffset);

    /// Performs the complete 3-pass movement: UP ? SIDE ? DOWN
    ThreePassResult PerformThreePassMove(
        const PhysicsInput& input,
        ThreePassState& st,
        float radius,
        float height,
        const G3D::Vector3& moveDir,
        float distance,
        float dt,
        float stepOffsetOverride = -1.0f);

} // namespace PhysicsThreePass
