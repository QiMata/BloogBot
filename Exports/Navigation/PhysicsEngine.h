// PhysicsEngine.h - Stateless physics engine with singleton pattern for resource management
#pragma once

#include "PhysicsBridge.h"
#include "MapLoader.h"
#include <memory>
#include <cmath>
#include <vector>
#include "Vector3.h" // Needed for by-value usage of G3D::Vector3
#include "SceneQuery.h"

// Forward declarations
namespace VMAP {
    class VMapManager2;
    struct Cylinder; // match actual declaration
}
namespace G3D {
    class Vector3; // still forward declare (already included but harmless)
}
class Navigation;

// WoW 1.12.1 Physics Constants (values adjusted to more closely reflect retail client behaviour)
namespace PhysicsConstants
{
    // Gravity and movement
    constexpr float GRAVITY = 19.2911f;          // kept same
    constexpr float JUMP_VELOCITY = 7.95577f;    // kept same
    constexpr float WATER_LEVEL_DELTA = 2.0f;

    // Ground detection (authentic vanilla: client allows ~2.1-2.2 unit step ups; testing shows 2.125f safe)
    constexpr float GROUND_HEIGHT_TOLERANCE = 0.04f; // tighter tolerance (remove hover)
    constexpr float STEP_HEIGHT = 2.125f;            // maximum upward auto step (was 2.1f)
    constexpr float STEP_DOWN_HEIGHT = 4.0f;         // maximum downward snap while still considered grounded (was 3.0f, vanilla allows larger safe drops)

    // Height constants
    constexpr float INVALID_HEIGHT = -200000.0f;
    constexpr float MAX_HEIGHT = 100000.0f;
    constexpr float DEFAULT_HEIGHT_SEARCH = 50.0f;

    // Legacy smoothing / acceleration tuning (kept for compatibility but mostly bypassed now)
    constexpr float GROUND_ACCEL = 40.0f;        // no longer used (instant ground velocity)
    constexpr float GROUND_DECEL = 30.0f;        // no longer used
    constexpr float AIR_ACCEL = 5.0f;            // mild air control (full directional)
    constexpr float AIR_DECEL = 0.0f;            // no passive damping
    constexpr float SWIM_ACCEL = 20.0f;          // retained
    constexpr float SWIM_DECEL = 0.0f;           // no passive damping in water
    constexpr float MIN_GROUND_SNAP_EPS = 0.02f; // smaller jitter ignore
    // Removed time-based snap speeds (instant snap like client); constants kept to avoid compile issues if referenced
    constexpr float MAX_GROUND_SNAP_UP_SPEED = 9999.0f;
    constexpr float MAX_GROUND_SNAP_DOWN_SPEED = 9999.0f;

    // Slope walkability threshold: cos(60°) = 0.5 per CMaNGOS walkableSlopeAngle default
    // 50-60° = NAV_AREA_GROUND_STEEP (walkable for mobs, not players in navmesh)
    // Recordings confirm: sustained walking at ~53°, airborne transitions at ~64°
    constexpr float DEFAULT_WALKABLE_MIN_NORMAL_Z = 0.5f;
}

class PhysicsEngine
{
public:
    // Singleton pattern for resource management
    static PhysicsEngine* Instance();
    static void Destroy();

    void Initialize();
    void Shutdown();

    // New: modernized step using diagnostics-driven movement
    PhysicsOutput StepV2(const PhysicsInput& input, float dt);

    // Configuration: walkable slope threshold (cosine of max slope angle)
    void SetWalkableCosMin(float cosMin);
    float GetWalkableCosMin() const;

    // Surface information
    enum class SurfaceSource
    {
        NONE,
        TERRAIN,
        VMAP
    };

    struct WalkableSurface
    {
        bool found;
        float height;
        SurfaceSource source;
        G3D::Vector3 normal;
    };

    // Perform sweeps via `SceneQuery` directly (no passthrough here).
    // Liquid evaluation is provided by `SceneQuery` directly.

private:
    PhysicsEngine();
    ~PhysicsEngine();

    // Delete copy constructor and assignment operator
    PhysicsEngine(const PhysicsEngine&) = delete;
    PhysicsEngine& operator=(const PhysicsEngine&) = delete;

    static PhysicsEngine* s_instance;

    VMAP::VMapManager2* m_vmapManager;
    std::unique_ptr<MapLoader> m_mapLoader;
    bool m_initialized;

    // Tunables
    float m_walkableCosMin; // cosine of max slope angle considered walkable

    // Movement state (created fresh each Step call)
    struct MovementState
    {
        float x, y, z;
        float vx, vy, vz;
        float orientation;
        float pitch;
        bool isGrounded;
        bool isSwimming;
        float fallTime;
        float fallStartZ = -200000.0f;  // Z when bot left the ground (-200000 = not falling)
        G3D::Vector3 groundNormal;
        // Support ramp plane (for smoothing step transitions)
        bool rampActive = false;
        G3D::Vector3 rampN; // plane normal (upward)
        float rampD = 0.0f; // plane constant (n.x*x + n.y*y + n.z*z + d = 0)
        G3D::Vector3 rampStart; // previous ground point
        G3D::Vector3 rampEnd;   // new stepped point
        G3D::Vector3 rampDir;   // horizontal movement direction used to form plane
        float rampLength = 0.0f; // horizontal distance along rampDir between start/end
    };

    // Added physics query result structs
    struct HitResult
    {
        bool blocking;            // true if this hit blocks movement
        float toi;                // time/distance of impact along sweep (0..sweepDist)
        float penetrationDepth;   // penetration depth if starting overlapping
        G3D::Vector3 impactPoint; // world-space contact point
        G3D::Vector3 impactNormal;// world-space contact normal (unit length)
        void Reset()
        {
            blocking = false;
            toi = 0.0f;
            penetrationDepth = 0.0f;
            impactPoint = G3D::Vector3(0, 0, 0);
            impactNormal = G3D::Vector3(0, 0, 1);
        }
    };

    struct FloorResult
    {
        bool hasFloor;            // any floor detected
        bool walkable;            // floor satisfies walkable slope criteria
        float floorDist;          // distance from query origin to floor (vertical or along sweep)
        float lineDist;           // parametric distance along cast line (e.g. ray length fraction)
        G3D::Vector3 floorPoint;  // world-space point on floor
        G3D::Vector3 floorNormal; // floor surface normal
        void Reset()
        {
            hasFloor = false;
            walkable = false;
            floorDist = 0.0f;
            lineDist = 0.0f;
            floorPoint = G3D::Vector3(0, 0, 0);
            floorNormal = G3D::Vector3(0, 0, 1);
        }
    };

    // Phase 1: helper describing player directional input & actions
    struct MovementIntent
    {
        G3D::Vector3 dir;      // normalized planar desired direction (xy, z=0)
        bool hasInput;         // any movement key
        bool jumpRequested;    // jump flag present
    };

    // Phase 2 movement mode
    enum class MovementMode { Ground, Air, Swim };

    // Core height/collision methods
    void EnsureMapLoaded(uint32_t mapId);
    float GetTerrainHeight(uint32_t mapId, float x, float y);
    float GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType);

    // Movement processing (simplified authentic style)
    void ProcessGroundMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state,
        float dt, float speed, float cylinderRadius, float cylinderHeight);
    void ProcessAirMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state, float dt, float speed);
    void ProcessSwimMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state, float dt, float speed);

    // Helper methods
    float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming);
    void ApplyGravity(MovementState& state, float dt);

    // Create player cylinder at position with specified dimensions
    VMAP::Cylinder CreatePlayerCylinder(float x, float y, float z,
        float radius, float height) const;

    // New helpers (non-const to allow calling non-const queries)
    G3D::Vector3 ComputeTerrainNormal(uint32_t mapId, float x, float y);

    // Phase 1 extracted helpers
    MovementIntent BuildMovementIntent(const PhysicsInput& input, float orientation) const;
    float QueryLiquidLevel(uint32_t mapId, float x, float y, float z, uint32_t& liquidType) const;

    bool TryDownwardStepSnap(const PhysicsInput& input,
                              MovementState& st,
                              float radius,
                              float height);

    // Helper that tries vertical placement via downward snap; if it fails, starts falling and processes air movement.
    // Returns true if grounded (snapped), false if transitioned to air.
    bool PerformVerticalPlacementOrFall(const PhysicsInput& input,
                                        const MovementIntent& intent,
                                        MovementState& st,
                                        float radius,
                                        float height,
                                        float dt,
                                        float moveSpeed,
                                        const char* reasonLog);

    // Performs the elevated-origin horizontal sweep followed by a downward probe and Z smoothing.
    // Handles the no-horizontal-movement case by calling PerformVerticalPlacementOrFall.
    void GroundMoveElevatedSweep(const PhysicsInput& input,
                                 const MovementIntent& intent,
                                 MovementState& st,
                                 float radius,
                                 float height,
                                 const G3D::Vector3& moveDir,
                                 float intendedDist,
                                 float dt,
                                 float moveSpeed);

    // Generic helpers to support the sweep -> depenetrate workflow
    // Computes a small horizontal depenetration vector from current overlaps, optionally filtering by walkable surfaces.
    // Returns the applied XY push magnitude.
    float ApplyHorizontalDepenetration(const PhysicsInput& input,
                                       MovementState& st,
                                       float radius,
                                       float height,
                                       bool walkableOnly);

    // Computes a vertical depenetration push to resolve upward-facing penetrating contacts close to feet or head.
    // Positive values push up, negative push down. Returns the applied Z delta.
    float ApplyVerticalDepenetration(const PhysicsInput& input,
                                     MovementState& st,
                                     float radius,
                                     float height);

    // Performs a horizontal capsule sweep along `dir` for `dist` and returns earliest blocking side hit distance.
    // If no blocking side hit, returns `dist`.
    float HorizontalSweepAdvance(const PhysicsInput& input,
                                 const MovementState& st,
                                 float radius,
                                 float height,
                                 const G3D::Vector3& dir,
                                 float dist);

    // Performs a vertical sweep down for up to `maxDown` and snaps to a walkable if found; returns true if snapped.
    bool VerticalSweepSnapDown(const PhysicsInput& input,
                               MovementState& st,
                               float radius,
                               float height,
                               float maxDown);

    // Attempts to step up within `maxUp` distance to a walkable surface based on upward sweep from current position.
    // Returns true if Z was snapped up and ground state set.
    bool TryStepUpSnap(const PhysicsInput& input,
                       MovementState& st,
                       float radius,
                       float height,
                       float maxUp);

    // Applies slide movement along a surface normal source using remaining distance.
    // Computes slide ratio from impact angle, sweeps along slide direction to avoid re-penetration,
    // advances position, and performs horizontal depenetration.
    void ApplySlideMovement(const PhysicsInput& input,
                            MovementState& st,
                            float radius,
                            float height,
                            const G3D::Vector3& dirN,
                            const G3D::Vector3& slideSourceN,
                            float remaining);

    // Handles the case where there is no horizontal movement: logs diagnostics and performs vertical placement or fall.
    void HandleNoHorizontalMovement(const PhysicsInput& input,
                                    const MovementIntent& intent,
                                    MovementState& st,
                                    float radius,
                                    float height,
                                    const G3D::Vector3& dirN,
                                    float dist,
                                    float dt,
                                    float moveSpeed);

    // Computes an averaged horizontal start-overlap slide normal at the current capsule position.
    // Returns true if a valid horizontal slide normal was found and writes it to outSlideN.
    bool ComputeStartOverlapSlideNormal(const PhysicsInput& input,
                                        const MovementState& st,
                                        float radius,
                                        float height,
                                        const G3D::Vector3& dirN,
                                        G3D::Vector3& outSlideN);

    // Logs impact diagnostics for slide source normal and returns computed slide ratio [0,1].
    float LogSlideImpactAndComputeRatio(const G3D::Vector3& dirN,
                                        const G3D::Vector3& slideSourceN,
                                        float dist,
                                        float advance);

    // =========================================================================
    // Phase 1: Iterative Collide-and-Slide System
    // =========================================================================

    // Result of a single CollideAndSlide pass
    struct SlideResult
    {
        G3D::Vector3 finalPosition;     // Position after all iterations
        G3D::Vector3 finalVelocity;     // Remaining velocity direction (may be zero)
        float distanceMoved;            // Total distance actually moved
        float distanceRemaining;        // Distance that couldn't be traveled
        int iterations;                 // Number of iterations used
        bool hitWall;                   // True if blocked by non-walkable surface
        bool hitCorner;                 // True if constrained by multiple surfaces (corner)
        G3D::Vector3 lastHitNormal;     // Normal of the last surface hit
    };

    // Constraint plane accumulated during slide iterations
    struct ConstraintPlane
    {
        G3D::Vector3 normal;            // Unit normal pointing away from surface
        float penetrationDepth;         // How far we penetrated (for depenetration)
        bool isWalkable;                // True if slope is walkable
    };

    // Maximum iterations for collide-and-slide per pass
    // ?? CRITICAL: Must be 10, not 4. Lower values cause stuck issues in complex geometry.
    // PhysX CCT uses 10 as the default (see PHYSX_CCT_RULES.md Section 1.1 & 15.2).
    static constexpr int MAX_SLIDE_ITERATIONS = 10;

    // Minimum distance to consider movement (avoids infinite loops)
    static constexpr float MIN_MOVE_DISTANCE = 0.001f;

    // Performs iterative collide-and-slide movement along a direction.
    // Returns the result containing final position and remaining distance.
    // This handles multiple bounces off surfaces and corner detection.
    SlideResult CollideAndSlide(const PhysicsInput& input,
                                MovementState& st,
                                float radius,
                                float height,
                                const G3D::Vector3& moveDir,
                                float distance,
                                bool horizontalOnly = true);

    // Computes the slide direction when hitting a single surface.
    // Returns the tangent direction along the surface, or zero vector if fully blocked.
    G3D::Vector3 ComputeSlideTangent(const G3D::Vector3& moveDir,
                                     const G3D::Vector3& surfaceNormal) const;

    // Computes the crease direction when constrained by two surfaces (corner case).
    // Returns the direction along the intersection of two planes, or zero if invalid.
    G3D::Vector3 ComputeCreaseDirection(const G3D::Vector3& moveDir,
                                        const G3D::Vector3& normal1,
                                        const G3D::Vector3& normal2) const;

    // Checks if a movement direction is blocked by a constraint normal.
    // Returns true if the direction opposes the normal (would move into the surface).
    bool IsDirectionBlocked(const G3D::Vector3& moveDir,
                            const G3D::Vector3& constraintNormal) const;

    // =========================================================================
    // Phase 2: Three-Pass Movement Decomposition (Up ? Side ? Down)
    // Based on PhysX CCT SweepTest::moveCharacter pattern
    // =========================================================================

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
        enum class NonWalkableSource : uint8_t { None = 0, Side = 1, Down = 2 };
        NonWalkableSource nonWalkableSource = NonWalkableSource::None;
        G3D::Vector3 sideHitNormal = G3D::Vector3(0, 0, 1); // valid if nonWalkableSource==Side
        float actualStepUpDelta;        // How much we actually rose in UP pass
        G3D::Vector3 groundNormal;      // Normal of ground surface (if landed)
    };

    // Decomposes a movement direction into up/side/down components.
    // Handles step offset injection and cancellation based on movement intent.
    // @param direction: The full desired movement vector
    // @param upDirection: The world up vector (typically 0,0,1)
    // @param stepOffset: Maximum step-up height (may be cancelled if jumping)
    // @param isJumping: True if player is actively jumping (cancels step offset)
    // @param standingOnMoving: True if standing on a moving platform
    DecomposedMovement DecomposeMovement(const G3D::Vector3& direction,
                                         const G3D::Vector3& upDirection,
                                         float stepOffset,
                                         bool isJumping,
                                         bool standingOnMoving) const;

    // Performs the complete 3-pass movement: UP ? SIDE ? DOWN
    // This is the main entry point for PhysX-style ground movement.
    // @param input: Physics input state
    // @param st: Current movement state (modified in place)
    // @param radius: Capsule radius
    // @param height: Capsule height
    // @param moveDir: Desired movement direction (may include vertical component)
    // @param distance: Desired movement distance
    // @param dt: Delta time for this frame
    ThreePassResult PerformThreePassMove(const PhysicsInput& input,
                                         MovementState& st,
                                         float radius,
                                         float height,
                                         const G3D::Vector3& moveDir,
                                         float distance,
                                         float dt,
                                         float stepOffsetOverride = -1.0f);

    // Executes the UP pass: step-up lift + any upward movement intent
    // Returns the collision flags and actual distance moved up.
    // @param decomposed: The decomposed movement vectors
    // @param clampedStepOffset: Output - step offset clamped by actual up movement
    SlideResult ExecuteUpPass(const PhysicsInput& input,
                              MovementState& st,
                              float radius,
                              float height,
                              const DecomposedMovement& decomposed,
                              float& clampedStepOffset);

    // Executes the SIDE pass: horizontal collide-and-slide
    // Returns the collision flags and slide result.
    SlideResult ExecuteSidePass(const PhysicsInput& input,
                                MovementState& st,
                                float radius,
                                float height,
                                const DecomposedMovement& decomposed);

    // Executes the DOWN pass: undo step offset + downward movement + ground snap
    // Returns the collision flags and whether we landed on walkable ground.
    // @param clampedStepOffset: The step offset to undo (from UP pass)
    SlideResult ExecuteDownPass(const PhysicsInput& input,
                                MovementState& st,
                                float radius,
                                float height,
                                const DecomposedMovement& decomposed,
                                float clampedStepOffset);

    // Validates slope after the DOWN pass - checks if landed surface is walkable.
    // Sets hitNonWalkable flag if slope exceeds walkable threshold.
    // @param contactNormal: The ground contact normal from DOWN pass
    // @param originalBottomZ: The original Z position of character's feet
    // @param stepOffset: The step offset that was applied
    bool ValidateSlopeAfterDownPass(const G3D::Vector3& contactNormal,
                                    float contactHeight,
                                    float originalBottomZ,
                                    float stepOffset) const;
};