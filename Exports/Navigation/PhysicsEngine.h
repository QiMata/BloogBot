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

// WoW 1.12.1 Physics Constants — extracted from client binary (build 5875)
// and VMaNGOS server source. All addresses refer to WoW.exe unless noted.
namespace PhysicsConstants
{
    // =========================================================================
    // GRAVITY AND MOVEMENT
    // =========================================================================

    // WoW.exe 1.12.1 VA 0x0081DA58: 19.29110527 (0x419A542F as IEEE 754 float)
    // Pre-computed: GRAVITY/2 @ 0x0081DA60, 2*GRAVITY @ 0x0081DA64,
    //               1/GRAVITY @ 0x0080E020, -1/GRAVITY @ 0x0081DA5C
    constexpr float GRAVITY = 19.29110527f;

    // WoW.exe 0x7C626F: immediate 0xC0FE93D8 = -7.955547f (negative = upward)
    // Swimming jump: 0x7C6266: 0xC1118C48 = -9.096748f
    // Implied max jump height: v²/(2g) = 1.640 yards.
    constexpr float JUMP_VELOCITY = 7.955547f;
    constexpr float JUMP_VELOCITY_SWIMMING = 9.096748f;

    constexpr float WATER_LEVEL_DELTA = 2.0f;

    // Initial downward velocity when transitioning from grounded to freefall.
    // Small nudge ensures the character doesn't hover for one frame at vz=0.
    constexpr float FALL_START_VELOCITY = -0.1f;

    // WoW.exe VA 0x0087D894: 60.148003 (0x4270978E)
    // This is NOT a hardcoded constant — it's computed at init by SetTerminalVelocity()
    // (0x7C6160): termVel = param * STEP_HEIGHT_FACTOR(1.0936). Default param ≈ 55.0.
    constexpr float TERMINAL_VELOCITY = 60.14800262f;

    // Pre-computed gravity multiples stored as static floats in WoW.exe.
    constexpr float HALF_GRAVITY = GRAVITY * 0.5f;      // 9.64555f — VA 0x0081DA60
    constexpr float DOUBLE_GRAVITY = GRAVITY * 2.0f;    // 38.5822f — VA 0x0081DA64
    constexpr float INV_GRAVITY = 1.0f / GRAVITY;       // 0.05184f — VA 0x0080E020
    constexpr float NEG_INV_GRAVITY = -INV_GRAVITY;     //           — VA 0x0081DA5C

    // WoW.exe VA 0x0087D898: 7.0 (adjacent to TERMINAL_VELOCITY).
    // Used when MOVEFLAG_SAFE_FALL (0x20000000) is set — e.g. Slow Fall, Levitate.
    constexpr float SAFE_FALL_TERMINAL_VELOCITY = 7.0f;

    // =========================================================================
    // BASE MOVEMENT SPEEDS (VMaNGOS baseMoveSpeed[], yards/second)
    // Walk and Run confirmed in client @ 0x0081018C, 0x00810190.
    // =========================================================================
    constexpr float BASE_WALK_SPEED      = 2.5f;
    constexpr float BASE_RUN_SPEED       = 7.0f;
    constexpr float BASE_RUN_BACK_SPEED  = 4.5f;
    constexpr float BASE_SWIM_SPEED      = 4.722222f;  // server-side only
    constexpr float BASE_SWIM_BACK_SPEED = 2.5f;
    constexpr float BASE_TURN_RATE       = 3.141594f;   // pi rad/s

    // =========================================================================
    // FALL DAMAGE
    // =========================================================================
    // VA 0x0081DA80: 10.0 = FALL_DAMAGE_START_SPEED
    // VA 0x0081DA84: 11.111111 = 100/9 (damage scalar)
    // VA 0x0081DA88: 5.555555 = 50/9  (half scalar)
    // VA 0x0081DA8C: 10.0 = duplicate
    // VA 0x0081DA90: 5.0
    // Init functions (0x7C7BD0, 0x7C7C00): square these and store as globals.
    //   safe_fall_speed² = 10² = 100  (stored at ds:0xCF5D68)
    //   damage_scalar²   = (100/9)² ≈ 123.457 (stored at ds:0xCF5D7C)
    constexpr float FALL_DAMAGE_START_SPEED = 10.0f;
    constexpr float FALL_DAMAGE_SCALAR      = 11.111111f;  // 100/9
    // VMaNGOS formula: dmgPct = COEFF * (zDiff - safeFall) - OFFSET
    constexpr float FALL_SAFE_DISTANCE  = 14.57f;  // Min fall distance for damage
    constexpr float FALL_SAFE_TIME_MS   = 1229.0f; // Fall time from safe distance
    constexpr float FALL_DAMAGE_COEFF   = 0.018f;  // % max HP per yard
    constexpr float FALL_DAMAGE_OFFSET  = 0.2426f; // Damage formula intercept

    // =========================================================================
    // GROUND DETECTION
    // =========================================================================
    constexpr float GROUND_HEIGHT_TOLERANCE = 0.04f;

    // VA 0x0081DA74: 1.093600 (0x3F8BFB16) — used as terminal velocity factor.
    // SetTerminalVelocity(0x7C6160): termVel = param * 1.0936.
    constexpr float STEP_HEIGHT_FACTOR = 1.093600f;

    // CMovement constructor hardcodes step-up at +0xB4 as 0x4001C71C = 2.027778f.
    // Collision skin fraction at +0xB0 = 0.333333 (1/3 of bounding box).
    constexpr float STEP_HEIGHT = 2.027778f;
    constexpr float COLLISION_SKIN_FRACTION = 0.333333f;

    constexpr float STEP_DOWN_HEIGHT = 4.0f;    // max downward snap while grounded

    // =========================================================================
    // HEIGHT CONSTANTS
    // =========================================================================
    constexpr float INVALID_HEIGHT = -200000.0f;
    constexpr float MAX_HEIGHT = 100000.0f;
    constexpr float DEFAULT_HEIGHT_SEARCH = 50.0f;

    // =========================================================================
    // SLOPE WALKABILITY
    // =========================================================================

    // VA 0x0081DA54: sin(45°) = cos(45°) = 1/√2 = 0.70710677 (0x3F3504F3)
    // Used in collision response: normal scaling for slide planes.
    constexpr float SIN_45 = 0.70710677f;

    // cos(50°) = 0.642788 @ 0x0080DFFC. Slopes steeper than 50° non-walkable.
    constexpr float DEFAULT_WALKABLE_MIN_NORMAL_Z = 0.6428f;

    // tan(50°) = 1.191754 @ 0x0080E008. Also -tan(50°) @ 0x0080E010.
    // Used for cliff detection: if ground drops faster than this, enter freefall.
    // In collision sweep (0x633C7B): slopeLimit = boundingRadius * tan(50°).
    constexpr float WALKABLE_TAN_MAX_SLOPE = 1.19175363f;

    // √2 = 1.414214 @ 0x0080E00C. Used in grounded collision sweep (0x633D93):
    // AABB vertical contraction = collisionSkin * √2 for step-down detection.
    constexpr float SQRT_2 = 1.41421354f;

    // 1/720 = 0.001389 @ 0x0080DFEC. Added to collisionSkin when computing
    // slope-limited displacement threshold in grounded collision (0x633C83).
    constexpr float COLLISION_SKIN_EPSILON = 0.00138889f;

    // Speed² thresholds from movement extrapolation (0x616B7E, 0x616B8D):
    // dist²/dt² > 3600 (speed > 60 y/s) = teleport/illegal movement
    // dist²/dt² < 9 (speed < 3 y/s) = jitter, ignore displacement
    constexpr float TELEPORT_SPEED_SQ_THRESHOLD = 3600.0f;
    constexpr float JITTER_SPEED_SQ_THRESHOLD = 9.0f;

    // =========================================================================
    // TERRAIN NORMAL ESTIMATION
    // =========================================================================

    // XY offset for finite-difference terrain normal probes (4 GetGroundZ calls)
    constexpr float NORMAL_PROBE_OFFSET = 0.3f;

    // =========================================================================
    // STEP-UP TOLERANCES
    // =========================================================================

    // Extra penetration tolerance added to capsule radius during step-up promotion
    constexpr float STEP_UP_PEN_TOLERANCE_EXTRA = 0.05f;

    // Max Z above pre-step position for step-up candidate promotion
    constexpr float MAX_STEP_UP_ABOVE_PRE_STEP = 1.5f;

    // =========================================================================
    // NUMERICAL EPSILONS
    // =========================================================================

    // VA 0x00801360: 0.001 — milliseconds to seconds conversion.
    // WoW stores fall time as int32 ms, multiplies by this before physics.
    constexpr float MS_TO_SEC = 0.001f;

    // VA 0x008029D4: 2.384e-07 (0x34800000) — speed epsilon.
    // If abs(velocity) < this, velocity is considered zero.
    constexpr float SPEED_EPSILON = 2.384185791015625e-7f;

    // General-purpose vector magnitude / sweep distance epsilon.
    // Used throughout for "is this vector effectively zero?" checks.
    constexpr float VECTOR_EPSILON = 1e-6f;

    // Slightly larger epsilon for ground snap / candidate sorting where
    // floating-point noise is higher (multi-ray probes, height comparisons).
    constexpr float GROUND_SNAP_EPSILON = 1e-4f;

    // =========================================================================
    // COLLISION GEOMETRY
    // =========================================================================

    // Overlap normal Z filter — ignore overlaps whose normal Z exceeds this
    // (they're floor/ceiling contacts, not walls).
    constexpr float OVERLAP_NORMAL_Z_FILTER = 0.7f;

    // Max deferred depenetration applied per physics tick (prevents teleporting).
    constexpr float MAX_DEFERRED_DEPEN_PER_TICK = 0.05f;

    // Max overlap recovery iterations per tick.
    constexpr int MAX_OVERLAP_RECOVER_ITERATIONS = 4;

    // =========================================================================
    // WATER TRANSITION
    // =========================================================================

    // Velocity damping factor when entering water (halves horizontal + vertical).
    constexpr float WATER_ENTRY_VELOCITY_DAMP = 0.5f;

    // =========================================================================
    // MOVEMENT FLAG RESTRICTIONS (WoW.exe binary analysis)
    // =========================================================================
    // WoW restricts which movement flags are allowed based on current state.
    // When airborne (JUMPING or FALLINGFAR), directional input flags are ignored
    // by the physics — horizontal velocity is frozen at the moment of leaving ground.
    // When rooted (ROOT), all movement is blocked.
    // These masks define which bits are ALLOWED in each state.

    // Directional movement bits (FORWARD|BACKWARD|STRAFE_LEFT|STRAFE_RIGHT)
    constexpr uint32_t DIRECTIONAL_BITS = 0x0000000F;
    // Turn bits (TURN_LEFT|TURN_RIGHT)
    constexpr uint32_t TURN_BITS = 0x00000030;
    // Pitch bits (PITCH_UP|PITCH_DOWN)
    constexpr uint32_t PITCH_BITS = 0x000000C0;

    // While airborne (JUMPING or FALLINGFAR):
    //   - Directional input is IGNORED (velocity frozen from launch)
    //   - Turning is still allowed (camera/facing)
    //   - Pitch is still allowed (irrelevant unless swimming)
    constexpr uint32_t AIRBORNE_BLOCKED_BITS = DIRECTIONAL_BITS;

    // While rooted: all movement blocked, only turns allowed
    constexpr uint32_t ROOTED_BLOCKED_BITS = DIRECTIONAL_BITS | PITCH_BITS;

    // =========================================================================
    // TURN SPEED (WoW.exe binary analysis)
    // =========================================================================
    // VA 0x8012CC: 0.75f — when any directional flag (0x200F) is active,
    // turn rate is multiplied by this factor. Characters turn slower while moving.
    constexpr float MOVING_TURN_RATE_FACTOR = 0.75f;
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
        // Wall contact state — set by GroundMoveElevatedSweep from SIDE pass result
        bool wallHit = false;
        G3D::Vector3 wallHitNormal;
        float wallBlockedFraction = 1.0f;
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
    void ApplyGravity(MovementState& state, float dt, uint32_t moveFlags = 0);

    // Create player cylinder at position with specified dimensions
    VMAP::Cylinder CreatePlayerCylinder(float x, float y, float z,
        float radius, float height) const;

    // New helpers (non-const to allow calling non-const queries)
    G3D::Vector3 ComputeTerrainNormal(uint32_t mapId, float x, float y);

    // Phase 1 extracted helpers
    MovementIntent BuildMovementIntent(const PhysicsInput& input, float orientation) const;
    float QueryLiquidLevel(uint32_t mapId, float x, float y, float z, uint32_t& liquidType) const;

    // WoW.exe-style ground collision (replaces 3-pass for ground movement).
    // Matches CollisionStep at VA 0x633840: terrain query + wall overlap + step height.
    // Uses GetGroundZ for terrain height (like WoW's TestTerrain) and capsule overlap
    // for wall detection (approximates WoW's AABB sweep).
    void CollisionStepWoW(const PhysicsInput& input, const MovementIntent& intent,
                          MovementState& st, float radius, float height,
                          const G3D::Vector3& moveDir, float intendedDist, float dt, float moveSpeed);

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
        // Wall contact feedback (populated from SIDE pass result)
        G3D::Vector3 lastSideHitNormal; // contact normal of the wall hit during SIDE pass
        float sideBlockedFraction;      // fraction of side distance completed (0=fully blocked, 1=no block)
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