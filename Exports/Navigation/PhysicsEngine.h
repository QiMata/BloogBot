// PhysicsEngine.h - Stateless physics engine with singleton pattern for resource management
#pragma once

#include "PhysicsBridge.h"
#include "MapLoader.h"
#include <memory>
#include <cmath>
#include <array>
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
        uint32_t supportInstanceId = 0;
        G3D::Vector3 supportLocalPoint = G3D::Vector3(0, 0, 0);
        // Wall contact state — set by CollisionStepWoW from AABB overlap
        bool wallHit = false;
        G3D::Vector3 wallHitNormal;
        float wallBlockedFraction = 1.0f;
        bool groundedWallState = false;
        uint32_t environmentFlags = 0;
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
    // Performs the elevated-origin horizontal sweep followed by a downward probe and Z smoothing.
    // Handles the no-horizontal-movement case by calling PerformVerticalPlacementOrFall.
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

    // [DELETED] Old 3-pass system (PhysX CCT style) replaced by CollisionStepWoW
    // matching WoW.exe VA 0x633840 AABB-based collision.
};

namespace WoWCollision
{
    struct CheckWalkableResult
    {
        bool walkable = false;
        bool walkableState = false;
        bool groundedWallFlagAfter = false;
    };

    struct SelectedContactThresholdGateResult
    {
        float normalZ = 0.0f;
        bool currentPositionInsidePrism = false;
        bool projectedPositionInsidePrism = false;
        bool thresholdSensitive = false;
        bool wouldUseDirectPair = false;
    };

    struct SelectorSupportPlane
    {
        G3D::Vector3 normal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float planeDistance = 0.0f;
    };

    struct SelectorPointStrip
    {
        std::array<G3D::Vector3, 15> points{};
        std::array<uint32_t, 15> sourceIndices{};
        uint32_t count = 0;
    };

    struct SelectorCandidateValidationTrace
    {
        float inputBestRatio = 0.0f;
        float candidateBestRatio = 0.0f;
        float outputBestRatio = 0.0f;
        uint32_t firstPassAllBelowLooseThreshold = 0;
        uint32_t rebuildExecuted = 0;
        uint32_t rebuildSucceeded = 0;
        uint32_t secondPassAllBelowStrictThreshold = 0;
        uint32_t improvedBestRatio = 0;
        uint32_t finalStripCount = 0;
    };

    struct SelectorCandidateRecord
    {
        SelectorSupportPlane filterPlane{};
        std::array<G3D::Vector3, 3> points{};
    };

    struct SelectorSourceScanWindow
    {
        int32_t rowMin = 0;
        int32_t columnMin = 0;
        int32_t rowMax = -1;
        int32_t columnMax = -1;
        int32_t pointStartIndex = 0;
        int32_t rowAdvancePointCount = 0;
    };

    struct SelectorRecordEvaluationTrace
    {
        float inputBestRatio = 0.0f;
        float outputBestRatio = 0.0f;
        float selectedBestRatio = 0.0f;
        uint32_t recordCount = 0;
        uint32_t dotRejectedCount = 0;
        uint32_t clipRejectedCount = 0;
        uint32_t validationRejectedCount = 0;
        uint32_t validationAcceptedCount = 0;
        uint32_t updatedBestRatio = 0;
        uint32_t selectedRecordIndex = 0xFFFFFFFFu;
        uint32_t selectedStripCount = 0;
    };

    struct SelectorSourceRankingTrace
    {
        float inputBestRatio = 0.0f;
        float outputBestRatio = 0.0f;
        uint32_t dotRejectedCount = 0;
        uint32_t builderRejectedCount = 0;
        uint32_t evaluatorRejectedCount = 0;
        uint32_t acceptedSourceCount = 0;
        uint32_t overwriteCount = 0;
        uint32_t appendCount = 0;
        uint32_t bestRatioUpdatedCount = 0;
        uint32_t swappedBestToFront = 0;
        uint32_t finalCandidateCount = 0;
        uint32_t selectedSourceIndex = 0xFFFFFFFFu;
    };

    struct SelectorDirectionRankingTrace
    {
        float inputBestRatio = 0.0f;
        float outputBestRatio = 0.0f;
        float reportedBestRatio = 0.0f;
        uint32_t dotRejectedCount = 0;
        uint32_t builderRejectedCount = 0;
        uint32_t evaluatorRejectedCount = 0;
        uint32_t acceptedDirectionCount = 0;
        uint32_t overwriteCount = 0;
        uint32_t appendCount = 0;
        uint32_t bestRatioUpdatedCount = 0;
        uint32_t swappedBestToFront = 0;
        uint32_t zeroClampedOutput = 0;
        uint32_t finalCandidateCount = 0;
        uint32_t selectedDirectionIndex = 0xFFFFFFFFu;
        uint32_t selectedRecordIndex = 0xFFFFFFFFu;
    };

    struct SelectorTriangleEdgeDirectionTrace
    {
        float bestScore = 0.0f;
        uint32_t zeroLengthRejectedCount = 0;
        uint32_t pointToLineScoredCount = 0;
        uint32_t planeScoredCount = 0;
        uint32_t selectedEdgeIndex = 0xFFFFFFFFu;
    };

    struct SelectorTwoCandidateWorkingVectorTrace
    {
        uint32_t returnedSelectedNormal = 0;
        uint32_t returnedNegatedFirstCandidate = 0;
        uint32_t returnedConstructedVector = 0;
        uint32_t rejectedByLineZGate = 0;
        uint32_t rejectedBySelectedPlaneDotGate = 0;
        uint32_t rejectedByFootprintMismatch = 0;
        uint32_t orientationNegated = 0;
        uint32_t selectedEdgeIndex = 0xFFFFFFFFu;
        G3D::Vector3 lineDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 edgeDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 workingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
    };

    struct SelectorAlternatePairTrace
    {
        uint32_t usedNegatedInputWorkingVector = 0;
        uint32_t usedNegatedFirstCandidate = 0;
        uint32_t usedTwoCandidateBuilder = 0;
        uint32_t usedSelectedContactNormal = 0;
        uint32_t normalizedHorizontal = 0;
        float horizontalMagnitude = 0.0f;
        float denominator = 0.0f;
        float scale = 0.0f;
        G3D::Vector3 workingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
    };

    struct SelectorPair
    {
        float first = 0.0f;
        float second = 0.0f;
    };

    struct TerrainQueryPairPayload
    {
        float first = 0.0f;
        float second = 0.0f;
    };

    struct TerrainQueryChunkSpan
    {
        int32_t cellMinX = 0;
        int32_t cellMaxX = 0;
        int32_t cellMinY = 0;
        int32_t cellMaxY = 0;
        int32_t chunkMinX = 0;
        int32_t chunkMaxX = 0;
        int32_t chunkMinY = 0;
        int32_t chunkMaxY = 0;
    };

    struct TerrainQueryChunkCoordinate
    {
        int32_t primary = 0;
        int32_t secondary = 0;
    };

    struct TerrainQueryMergedQueryTrace
    {
        G3D::Vector3 queryBoundsMin = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 queryBoundsMax = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 mergedBoundsMin = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 mergedBoundsMax = G3D::Vector3(0.0f, 0.0f, 0.0f);
        uint32_t cacheContainsBoundsMin = 0u;
        uint32_t cacheContainsBoundsMax = 0u;
        uint32_t reusedCachedQuery = 0u;
        uint32_t builtMergedBounds = 0u;
        uint32_t builtQueryMask = 0u;
        uint32_t queryInvoked = 0u;
        uint32_t queryDispatchSucceeded = 0u;
        uint32_t returnedSuccess = 0u;
        uint32_t queryMask = 0u;
    };

    struct TerrainQuerySelectedContactContainerTrace
    {
        TerrainQueryMergedQueryTrace mergedQuery{};
        uint32_t reusedExistingContainer = 0u;
        uint32_t copiedQueryResults = 0u;
        uint32_t returnedSuccess = 0u;
        uint32_t outputContactCount = 0u;
    };

    enum TerrainQueryEntryDispatchAction : uint32_t
    {
        TERRAIN_QUERY_ENTRY_SKIP = 0,
        TERRAIN_QUERY_ENTRY_DISPATCH = 1,
        TERRAIN_QUERY_ENTRY_ABORT = 2,
    };

    struct SelectorPairConsumerTrace
    {
        float requestedDistance = 0.0f;
        int32_t selectedIndex = -1;
        uint32_t selectedCount = 0;
        uint32_t directionRankingAccepted = 0;
        uint32_t directGateAccepted = 0;
        uint32_t directGateState = 0;
        uint32_t alternateUnitZState = 0;
        uint32_t returnedDirectPair = 0;
        uint32_t returnedAlternatePair = 0;
        uint32_t returnedZeroPair = 0;
        uint32_t preservedInputMove = 0;
        uint32_t zeroedMoveOnRankingFailure = 0;
        int32_t returnCode = 0;
        G3D::Vector3 inputMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 outputMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        SelectorPair outputPair{};
    };

    struct SelectorTriangleSourceWrapperTrace
    {
        uint32_t supportPlaneInitCount = 0u;
        uint32_t validationPlaneInitCount = 0u;
        uint32_t scratchPointZeroCount = 0u;
        uint32_t usedOverridePosition = 0u;
        uint32_t terrainQueryInvoked = 0u;
        uint32_t terrainQuerySucceeded = 0u;
        uint32_t returnedSuccess = 0u;
        uint32_t queryFailureZeroedOutput = 0u;
        G3D::Vector3 selectedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 testPoint = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 candidateDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float initialBestRatio = 0.0f;
        float inputBestRatio = 0.0f;
        float reportedBestRatio = 0.0f;
    };

    struct SelectorTriangleSourceVariableTransactionTrace
    {
        uint32_t supportPlaneInitCount = 0u;
        uint32_t validationPlaneInitCount = 0u;
        uint32_t scratchPointZeroCount = 0u;
        uint32_t usedOverridePosition = 0u;
        uint32_t terrainQueryInvoked = 0u;
        uint32_t terrainQuerySucceeded = 0u;
        uint32_t terrainQueryReusedCachedQuery = 0u;
        uint32_t terrainQueryBuiltMergedBounds = 0u;
        uint32_t terrainQueryBuiltQueryMask = 0u;
        uint32_t rankingInvoked = 0u;
        uint32_t rankingAccepted = 0u;
        uint32_t zeroClampedOutput = 0u;
        uint32_t returnedSuccess = 0u;
        uint32_t queryFailureZeroedOutput = 0u;
        G3D::Vector3 selectedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 projectedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 testPoint = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 candidateDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float initialBestRatio = 0.0f;
        float rankingReportedBestRatio = 0.0f;
        float outputReportedBestRatio = 0.0f;
        uint32_t rankingCandidateCount = 0u;
        int32_t rankingSelectedRecordIndex = -1;
        uint32_t terrainQueryMask = 0u;
    };

    struct SelectorBvhNodeRecord
    {
        uint16_t controlWord = 0;
        uint16_t lowChildIndex = 0xFFFFu;
        uint16_t highChildIndex = 0xFFFFu;
        uint16_t leafTriangleCount = 0;
        uint32_t leafTriangleStartIndex = 0;
        float splitCoordinate = 0.0f;
    };

    struct SelectorBvhChildTraversal
    {
        uint32_t axis = 0u;
        float splitCoordinate = 0.0f;
        uint32_t lowChildIndex = 0xFFFFFFFFu;
        uint32_t highChildIndex = 0xFFFFFFFFu;
        uint32_t visitLow = 0u;
        uint32_t visitHigh = 0u;
        G3D::Vector3 lowBoundsMin = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 lowBoundsMax = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 highBoundsMin = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 highBoundsMax = G3D::Vector3(0.0f, 0.0f, 0.0f);
    };

    struct SelectorObjectRouterEntryRecord
    {
        G3D::Vector3 boundsMin = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 boundsMax = G3D::Vector3(0.0f, 0.0f, 0.0f);
        uint64_t nodeToken = 0u;
        uint32_t nodeEnabled = 0u;
        uint32_t callbackReturn = 0u;
    };

    struct SelectorObjectRouterTrace
    {
        uint32_t overlapRejectedCount = 0u;
        uint32_t nodeRejectedCount = 0u;
        uint32_t dispatchedCount = 0u;
        uint32_t accumulatorUpdatedCount = 0u;
        uint32_t result = 0u;
    };

    struct SelectorObjectNoCallbackState
    {
        uint32_t hitResult = 0u;
        uint32_t recordCount = 0u;
        uint32_t outputFlags = 0u;
    };

    struct SelectorLeafQueueMutationTrace
    {
        uint32_t skippedByMask = 0u;
        uint32_t overflowed = 0u;
        uint32_t pendingEnqueued = 0u;
        uint32_t visitedBitSet = 0u;
        uint32_t predicateRejected = 0u;
        uint32_t acceptedEnqueued = 0u;
        uint32_t stateByteBefore = 0u;
        uint32_t stateByteAfter = 0u;
        uint32_t pendingCountAfter = 0u;
        uint32_t acceptedCountAfter = 0u;
    };

    struct SelectorNodeTraversalRecord
    {
        uint64_t traversalBaseToken = 0u;
        uint64_t extraNodeToken = 0u;
        uint64_t stateBytesToken = 0u;
        uint64_t vertexBufferToken = 0u;
        uint64_t triangleIndexToken = 0u;
    };

    struct SelectorNodeTraversalPayload
    {
        G3D::Vector3 queryBoundsMin = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 queryBoundsMax = G3D::Vector3(0.0f, 0.0f, 0.0f);
        uint32_t callbackMaskWord = 0u;
        uint32_t acceptedCount = 0u;
        uint64_t traversalBaseToken = 0u;
        uint64_t extraNodeToken = 0u;
        uint64_t stateBytesToken = 0u;
        uint64_t vertexBufferToken = 0u;
        uint64_t triangleIndexToken = 0u;
    };

    enum SelectorAlternateWorkingVectorMode : uint32_t
    {
        SELECTOR_ALTERNATE_VECTOR_NEGATED_FIRST_CANDIDATE = 0,
        SELECTOR_ALTERNATE_VECTOR_TWO_CANDIDATE_BUILDER = 1,
        SELECTOR_ALTERNATE_VECTOR_SELECTED_CONTACT_NORMAL = 2,
    };

    enum GroundedWallResolutionBranch : uint32_t
    {
        GROUNDED_WALL_BRANCH_NONE = 0,
        GROUNDED_WALL_BRANCH_HORIZONTAL = 1,
        GROUNDED_WALL_BRANCH_WALKABLE_SELECTED_VERTICAL = 2,
        GROUNDED_WALL_BRANCH_NON_WALKABLE_VERTICAL = 3,
    };

    struct GroundedWallResolutionTrace
    {
        uint32_t queryContactCount = 0;
        uint32_t candidateCount = 0;
        uint32_t selectedContactIndex = 0xFFFFFFFFu;
        uint32_t selectedInstanceId = 0;
        uint32_t rawWalkable = 0;
        uint32_t walkableWithoutState = 0;
        uint32_t walkableWithState = 0;
        uint32_t groundedWallStateBefore = 0;
        uint32_t groundedWallStateAfter = 0;
        uint32_t usedPositionReorientation = 0;
        uint32_t usedWalkableSelectedContact = 0;
        uint32_t usedNonWalkableVertical = 0;
        uint32_t usedUphillDiscard = 0;
        uint32_t usedPrimaryAxisFallback = 0;
        uint32_t branchKind = GROUNDED_WALL_BRANCH_NONE;
        G3D::Vector3 selectedPoint = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 selectedNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 orientedNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 primaryAxis = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 mergedWallNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 finalWallNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 horizontalProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 branchProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 finalProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float rawOpposeScore = 0.0f;
        float orientedOpposeScore = 0.0f;
        float requested2D = 0.0f;
        float horizontalResolved2D = 0.0f;
        float slopedResolved2D = 0.0f;
        float finalResolved2D = 0.0f;
        float blockedFraction = 1.0f;
        uint32_t selectedCurrentPositionInsidePrism = 0;
        uint32_t selectedProjectedPositionInsidePrism = 0;
        uint32_t selectedThresholdSensitiveStandard = 0;
        uint32_t selectedThresholdSensitiveRelaxed = 0;
        uint32_t selectedWouldUseDirectPairStandard = 0;
        uint32_t selectedWouldUseDirectPairRelaxed = 0;
        G3D::Vector3 selectedThresholdPoint = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float selectedThresholdNormalZ = 0.0f;
    };

    CheckWalkableResult CheckWalkable(const SceneQuery::AABBContact& contact,
                                      const G3D::Vector3& position,
                                      float collisionRadius,
                                      float boundingHeight,
                                      bool useStandardWalkableThreshold,
                                      bool groundedWallFlagBefore);

    SelectedContactThresholdGateResult EvaluateSelectedContactThresholdGate(const SceneQuery::AABBContact& contact,
                                                                           const G3D::Vector3& currentPosition,
                                                                           const G3D::Vector3& projectedPosition,
                                                                           bool useStandardWalkableThreshold);

    bool IsPointInsideAabbInclusive(const G3D::Vector3& boundsMin,
                                    const G3D::Vector3& boundsMax,
                                    const G3D::Vector3& point);

    bool DoAabbsOverlapInclusive(const G3D::Vector3& boundsMinA,
                                 const G3D::Vector3& boundsMaxA,
                                 const G3D::Vector3& boundsMinB,
                                 const G3D::Vector3& boundsMaxB);

    uint32_t BuildAabbOutcode(const G3D::Vector3& point,
                              const G3D::Vector3& boundsMin,
                              const G3D::Vector3& boundsMax);

    bool TriangleSharesAabbOutcodeReject(uint32_t firstOutcode,
                                         uint32_t secondOutcode,
                                         uint32_t thirdOutcode);

    bool TriangleSharesSelectorPlaneOutcodeReject(uint32_t firstOutcode,
                                                  uint32_t secondOutcode,
                                                  uint32_t thirdOutcode);

    uint32_t CountTrianglesPassingAabbOutcodeReject(const uint16_t* triangleIndices,
                                                    uint32_t triangleCount,
                                                    const uint32_t* vertexOutcodes,
                                                    uint32_t vertexOutcodeCount);

    uint32_t BuildTerrainQueryMask(bool modelPropertyFlagSet,
                                   uint32_t movementFlags,
                                   float field20Value,
                                   bool rootTreeFlagSet,
                                   bool childTreeFlagSet);

    bool IsTerrainQueryPayloadEnabled(uint32_t movementFlags,
                                      const TerrainQueryPairPayload& payload);

    bool ShouldRunDynamicCallbackProducer(bool callbackPresent,
                                          uint32_t movementFlags);

    bool ShouldVisitTerrainQueryStampedEntry(uint32_t entryVisitStamp,
                                             uint32_t currentVisitStamp);

    uint32_t BeginTerrainQueryProducerPass(uint32_t currentVisitStamp,
                                           std::vector<SelectorCandidateRecord>& ioRecords);

    void BuildTerrainQueryChunkSpan(const G3D::Vector3& worldBoundsMin,
                                    const G3D::Vector3& worldBoundsMax,
                                    TerrainQueryChunkSpan& outSpan);

    uint32_t EnumerateTerrainQueryChunkCoordinates(const TerrainQueryChunkSpan& span,
                                                   std::vector<TerrainQueryChunkCoordinate>& outCoordinates);

    uint32_t BuildOptionalSelectorChildDispatchMask(const uint32_t* childPresenceFlags,
                                                    uint32_t childCount,
                                                    uint32_t movementFlags);

    TerrainQueryEntryDispatchAction EvaluateTerrainQueryEntryDispatch(bool entryFlagMaskedOut,
                                                                     bool alreadyVisited,
                                                                     bool hasSourceGeometry,
                                                                     uint32_t movementFlags,
                                                                     const TerrainQueryPairPayload& payload,
                                                                     bool traversalAllowsDispatch,
                                                                     const G3D::Vector3& entryBoundsMin,
                                                                     const G3D::Vector3& entryBoundsMax,
                                                                     const G3D::Vector3& queryBoundsMin,
                                                                     const G3D::Vector3& queryBoundsMax);

    bool ShouldDispatchDynamicTerrainQueryEntry(bool entryFlagEnabled,
                                                bool alreadyVisited,
                                                bool callbackSucceeded,
                                                const G3D::Vector3& entryBoundsMin,
                                                const G3D::Vector3& entryBoundsMax,
                                                const G3D::Vector3& queryBoundsMin,
                                                const G3D::Vector3& queryBoundsMax);

    void BuildTerrainQueryBounds(const G3D::Vector3& projectedPosition,
                                 float collisionRadius,
                                 float boundingHeight,
                                 G3D::Vector3& outBoundsMin,
                                 G3D::Vector3& outBoundsMax);

    uint32_t CopyTerrainQueryWalkableContactsAndPairs(const SceneQuery::AABBContact* inputContacts,
                                                      const TerrainQueryPairPayload* inputPairs,
                                                      uint32_t inputCount,
                                                      std::vector<SceneQuery::AABBContact>& outContacts,
                                                      std::vector<TerrainQueryPairPayload>& outPairs);

    void AppendTerrainQueryPairPayloadRange(uint32_t previousRecordCount,
                                            uint32_t currentRecordCount,
                                            const TerrainQueryPairPayload& payload,
                                            std::vector<TerrainQueryPairPayload>& ioPairs);

    void ZeroTerrainQueryPairPayloadRange(uint32_t previousRecordCount,
                                          uint32_t currentRecordCount,
                                          std::vector<TerrainQueryPairPayload>& ioPairs);

    void MergeAabbBounds(const G3D::Vector3& boundsMinA,
                         const G3D::Vector3& boundsMaxA,
                         const G3D::Vector3& boundsMinB,
                         const G3D::Vector3& boundsMaxB,
                         G3D::Vector3& outBoundsMin,
                         G3D::Vector3& outBoundsMax);

    void AddScalarToVector3(G3D::Vector3& ioVector, float scalar);

    void SubtractScalarFromVector3(G3D::Vector3& ioVector, float scalar);

    void BuildTerrainQueryCacheMissBounds(const G3D::Vector3& projectedPosition,
                                          float collisionRadius,
                                          float boundingHeight,
                                          const G3D::Vector3& cachedBoundsMin,
                                          const G3D::Vector3& cachedBoundsMax,
                                          G3D::Vector3& outBoundsMin,
                                          G3D::Vector3& outBoundsMax);

    bool EvaluateTerrainQueryMergedQueryTransaction(const G3D::Vector3& projectedPosition,
                                                    float collisionRadius,
                                                    float boundingHeight,
                                                    const G3D::Vector3& cachedBoundsMin,
                                                    const G3D::Vector3& cachedBoundsMax,
                                                    bool modelPropertyFlagSet,
                                                    uint32_t movementFlags,
                                                    float field20Value,
                                                    bool rootTreeFlagSet,
                                                    bool childTreeFlagSet,
                                                    bool queryDispatchSucceeded,
                                                    TerrainQueryMergedQueryTrace& outTrace);

    bool EvaluateTerrainQuerySelectedContactContainerTransaction(
        const G3D::Vector3& projectedPosition,
        float collisionRadius,
        float boundingHeight,
        const G3D::Vector3& cachedBoundsMin,
        const G3D::Vector3& cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint32_t movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        const SceneQuery::AABBContact* existingContacts,
        const TerrainQueryPairPayload* existingPairs,
        uint32_t existingCount,
        const SceneQuery::AABBContact* queryContacts,
        const TerrainQueryPairPayload* queryPairs,
        uint32_t queryCount,
        bool queryDispatchSucceeded,
        std::vector<SceneQuery::AABBContact>& outContacts,
        std::vector<TerrainQueryPairPayload>& outPairs,
        TerrainQuerySelectedContactContainerTrace& outTrace);

    void NegatePlane(const G3D::Vector3& normal,
                     float planeDistance,
                     SelectorSupportPlane& outPlane);

    void BuildPlaneFromNormalAndPoint(const G3D::Vector3& normal,
                                      const G3D::Vector3& point,
                                      SelectorSupportPlane& outPlane);

    void BuildObjectLocalQueryBounds(const G3D::Vector3& worldBoundsMin,
                                     const G3D::Vector3& worldBoundsMax,
                                     const G3D::Vector3& objectPosition,
                                     G3D::Vector3& outLocalBoundsMin,
                                     G3D::Vector3& outLocalBoundsMax);

    bool BuildPlaneFromTrianglePoints(const G3D::Vector3& point0,
                                      const G3D::Vector3& point1,
                                      const G3D::Vector3& point2,
                                      SelectorSupportPlane& outPlane);

    bool BuildSelectorHullSourceGeometry(const G3D::Vector3* supportPoints,
                                         uint32_t supportPointCount,
                                         SelectorSupportPlane* outPlanes,
                                         uint32_t planeCount,
                                         G3D::Vector3* outPoints,
                                         uint32_t outPointCount,
                                         G3D::Vector3* outAnchorPoint0,
                                         G3D::Vector3* outAnchorPoint1);

    bool TransformSelectorSupportPointBuffer(const G3D::Vector3* inputPoints,
                                             uint32_t pointCount,
                                             const std::array<G3D::Vector3, 3>& transformBasisRows,
                                             const G3D::Vector3& translation,
                                             G3D::Vector3* outPoints,
                                             uint32_t outPointCount);

    uint32_t BuildSelectorObjectCallbackMask(uint32_t movementFlags);

    bool ShouldResolveSelectorObjectNode(bool selectorEnabled,
                                         bool nodeEnabled,
                                         bool allowInactiveNode);

    const void* ResolveSelectorObjectNodePointer(bool selectorEnabled,
                                                 const void* nodePointer,
                                                 bool nodeEnabled,
                                                 bool allowInactiveNode);

    uint32_t EvaluateSelectorObjectRouterEntries(const SelectorObjectRouterEntryRecord* entries,
                                                 uint32_t entryCount,
                                                 bool selectorEnabled,
                                                 const G3D::Vector3& queryBoundsMin,
                                                 const G3D::Vector3& queryBoundsMax,
                                                 SelectorObjectRouterTrace* outTrace);

    bool ShouldUseSelectorObjectCallback(uint64_t callbackToken);

    void FinalizeSelectorObjectNoCallbackState(uint32_t inputHitResult,
                                               uint32_t inputRecordCount,
                                               uint32_t inputOutputFlags,
                                               SelectorObjectNoCallbackState& outState);

    uint32_t EvaluateSelectorLeafQueueMutation(uint32_t triangleIndex,
                                               uint32_t stateMaskByte,
                                               bool predicateRejected,
                                               uint32_t& ioOverflowFlags,
                                               uint16_t* pendingIds,
                                               uint32_t pendingIdCapacity,
                                               uint32_t& ioPendingCount,
                                               uint16_t* acceptedIds,
                                               uint32_t acceptedIdCapacity,
                                               uint32_t& ioAcceptedCount,
                                               uint8_t* stateBytes,
                                               uint32_t stateByteCount,
                                               SelectorLeafQueueMutationTrace* outTrace);

    bool BuildSelectorNodeTraversalPayload(const SelectorNodeTraversalRecord& node,
                                           const G3D::Vector3* querySupportPoints,
                                           uint32_t supportPointCount,
                                           uint32_t callbackMask,
                                           SelectorNodeTraversalPayload& outPayload);

    void BuildSelectorSupportPointBounds(const G3D::Vector3* points,
                                         uint32_t pointCount,
                                         G3D::Vector3& outBoundsMin,
                                         G3D::Vector3& outBoundsMax);

    bool BuildSelectorDynamicObjectHullSourceGeometry(const SelectorSupportPlane* sourcePlanes,
                                                      uint32_t planeCount,
                                                      const G3D::Vector3& objectBoundsMin,
                                                      const G3D::Vector3& objectBoundsMax,
                                                      const G3D::Vector3* localSupportPoints,
                                                      uint32_t supportPointCount,
                                                      const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                      const G3D::Vector3& translation,
                                                      SelectorSupportPlane* outPlanes,
                                                      uint32_t outPlaneCount,
                                                      G3D::Vector3* outPoints,
                                                      uint32_t outPointCount,
                                                      G3D::Vector3* outAnchorPoint0,
                                                      G3D::Vector3* outAnchorPoint1);

    bool BuildSelectorBvhChildTraversal(const SelectorBvhNodeRecord& node,
                                        const G3D::Vector3& boundsMin,
                                        const G3D::Vector3& boundsMax,
                                        SelectorBvhChildTraversal& outTraversal);

    void TranslateSelectorSourceGeometry(const G3D::Vector3& translation,
                                         SelectorSupportPlane* ioPlanes,
                                         uint32_t planeCount,
                                         G3D::Vector3* ioPoints,
                                         uint32_t pointCount,
                                         G3D::Vector3* ioAnchorPoint0,
                                         G3D::Vector3* ioAnchorPoint1);

    uint32_t BuildSelectorSourcePlaneOutcode(const SelectorSupportPlane* planes,
                                             uint32_t planeCount,
                                             const G3D::Vector3& point);

    uint32_t EvaluateSelectorSourceAabbCull(const SelectorSupportPlane* planes,
                                            uint32_t planeCount,
                                            const G3D::Vector3& boundsMin,
                                            const G3D::Vector3& boundsMax);

    uint32_t EvaluateSelectorHullTransformedBoundsCull(const SelectorSupportPlane* planes,
                                                       uint32_t planeCount,
                                                       const G3D::Vector3& localBoundsMin,
                                                       const G3D::Vector3& localBoundsMax,
                                                       const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                       const G3D::Vector3& translation);

    uint32_t EvaluateSelectorHullPointWithMargin(const SelectorSupportPlane* planes,
                                                 uint32_t planeCount,
                                                 const G3D::Vector3& point,
                                                 float margin);

    uint32_t EvaluateSelectorHullPointEpsilon(const SelectorSupportPlane* planes,
                                              uint32_t planeCount,
                                              const G3D::Vector3& point);

    uint32_t CountSelectorSourceTrianglesPassingPlaneOutcodes(const SelectorSupportPlane* planes,
                                                              uint32_t planeCount,
                                                              const G3D::Vector3* samplePoints,
                                                              uint32_t samplePointCount);

    bool BuildSelectorSourceScanWindow(int32_t cellRowIndex,
                                       int32_t cellColumnIndex,
                                       int32_t queryRowMin,
                                       int32_t queryColumnMin,
                                       int32_t queryRowMax,
                                       int32_t queryColumnMax,
                                       SelectorSourceScanWindow& outWindow);

    uint32_t BuildLocalBoundsAabbOutcode(const G3D::Vector3& localBoundsMin,
                                         const G3D::Vector3& localBoundsMax,
                                         const G3D::Vector3& point);

    bool EvaluateTriangleLocalBoundsAabbReject(const G3D::Vector3& localBoundsMin,
                                               const G3D::Vector3& localBoundsMax,
                                               const G3D::Vector3& point0,
                                               const G3D::Vector3& point1,
                                               const G3D::Vector3& point2);

    uint32_t BuildSelectorSourceSubcellMask(uint32_t rowIndex,
                                            uint32_t columnIndex);

    bool IsSelectorSourceSubcellMaskedOut(uint32_t rowIndex,
                                          uint32_t columnIndex,
                                          uint32_t cellMaskFlags);

    void BuildTranslatedTriangleSelectorRecord(const G3D::Vector3& localPoint0,
                                               const G3D::Vector3& localPoint1,
                                               const G3D::Vector3& localPoint2,
                                               const G3D::Vector3& translation,
                                               bool useApproximatePlaneBuildPath,
                                               SelectorCandidateRecord& outRecord);

    uint32_t AppendSelectorSourceTriangleCandidateRecords(const SelectorSupportPlane* planes,
                                                          uint32_t planeCount,
                                                          const G3D::Vector3* samplePoints,
                                                          uint32_t samplePointCount,
                                                          const G3D::Vector3& translation,
                                                          bool useApproximatePlaneBuildPath,
                                                          std::vector<SelectorCandidateRecord>& ioRecords);

    uint32_t AppendSelectorSourceScanWindowCandidateRecords(const SelectorSupportPlane* planes,
                                                            uint32_t planeCount,
                                                            const G3D::Vector3* pointGrid,
                                                            uint32_t pointGridPointCount,
                                                            const SelectorSourceScanWindow& scanWindow,
                                                            uint32_t cellMaskFlags,
                                                            const G3D::Vector3& translation,
                                                            bool useApproximatePlaneBuildPath,
                                                            std::vector<SelectorCandidateRecord>& ioRecords);

    uint32_t AppendLocalBoundsScanWindowTriangleCandidateRecords(const G3D::Vector3& localBoundsMin,
                                                                 const G3D::Vector3& localBoundsMax,
                                                                 const G3D::Vector3* pointGrid,
                                                                 uint32_t pointGridPointCount,
                                                                 const SelectorSourceScanWindow& scanWindow,
                                                                 uint32_t cellMaskFlags,
                                                                 const G3D::Vector3& translation,
                                                                 bool useApproximatePlaneBuildPath,
                                                                 std::vector<SelectorCandidateRecord>& ioRecords);

    void AppendSelectorQuadRecordPair(const G3D::Vector3& basePoint,
                                      const G3D::Vector3& firstEdge,
                                      const G3D::Vector3& secondEdge,
                                      const G3D::Vector3& normal,
                                      std::vector<SelectorCandidateRecord>& ioRecords);

    uint32_t BuildAabbBoundarySelectorCandidateRecords(const G3D::Vector3& boundaryMin,
                                                       const G3D::Vector3& boundaryMax,
                                                       const G3D::Vector3& queryBoundsMin,
                                                       const G3D::Vector3& queryBoundsMax,
                                                       std::vector<SelectorCandidateRecord>& outRecords);

    void BuildTransformedTriangleSelectorRecord(const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                const G3D::Vector3& localNormal,
                                                const G3D::Vector3& point0,
                                                const G3D::Vector3& point1,
                                                const G3D::Vector3& point2,
                                                SelectorCandidateRecord& outRecord);

    void TransformWorldPointToTransportLocal(const G3D::Vector3& worldPoint,
                                             const G3D::Vector3& transportPosition,
                                             float transportOrientation,
                                             G3D::Vector3& outLocalPoint);

    void TransformWorldVectorToTransportLocal(const G3D::Vector3& worldVector,
                                              float transportOrientation,
                                              G3D::Vector3& outLocalVector);

    void BuildTransportLocalPlane(const G3D::Vector3& worldNormal,
                                  const G3D::Vector3& worldPoint,
                                  const G3D::Vector3& transportPosition,
                                  float transportOrientation,
                                  SelectorSupportPlane& outPlane);

    void TransformSelectorCandidateRecordToTransportLocal(const SelectorCandidateRecord& worldRecord,
                                                          const G3D::Vector3& transportPosition,
                                                          float transportOrientation,
                                                          SelectorCandidateRecord& outLocalRecord);

    void TransformSelectorCandidateRecordBufferToTransportLocal(uint32_t transportGuidLow,
                                                                uint32_t transportGuidHigh,
                                                                const G3D::Vector3& transportPosition,
                                                                float transportOrientation,
                                                                SelectorCandidateRecord* ioRecords,
                                                                uint32_t recordCount);

    void InitializeSelectorSupportPlane(SelectorSupportPlane& outPlane);

    float ClampSelectorReportedBestRatio(float bestRatio);

    bool FinalizeSelectorTriangleSourceWrapper(bool hasOverridePosition,
                                               bool terrainQuerySucceeded,
                                               float inputBestRatio,
                                               float& outReportedBestRatio);

    void InitializeSelectorTriangleSourceWrapperSeeds(G3D::Vector3& outTestPoint,
                                                      G3D::Vector3& outCandidateDirection,
                                                      float& outBestRatio);

    bool EvaluateSelectorTriangleSourceWrapperTransaction(const G3D::Vector3& defaultPosition,
                                                          const G3D::Vector3* overridePosition,
                                                          bool terrainQuerySucceeded,
                                                          float inputBestRatio,
                                                          SelectorTriangleSourceWrapperTrace& outTrace);

    bool EvaluateSelectorTriangleSourceVariableTransaction(const G3D::Vector3& defaultPosition,
                                                           const G3D::Vector3* overridePosition,
                                                           const G3D::Vector3& projectedPosition,
                                                           uint32_t supportPlaneInitCount,
                                                           uint32_t validationPlaneInitCount,
                                                           uint32_t scratchPointZeroCount,
                                                           const G3D::Vector3& testPoint,
                                                           const G3D::Vector3& candidateDirection,
                                                           float initialBestRatio,
                                                           float collisionRadius,
                                                           float boundingHeight,
                                                           const G3D::Vector3& cachedBoundsMin,
                                                           const G3D::Vector3& cachedBoundsMax,
                                                           bool modelPropertyFlagSet,
                                                           uint32_t movementFlags,
                                                           float field20Value,
                                                           bool rootTreeFlagSet,
                                                           bool childTreeFlagSet,
                                                           bool queryDispatchSucceeded,
                                                           bool rankingAccepted,
                                                           uint32_t rankingCandidateCount,
                                                           int32_t rankingSelectedRecordIndex,
                                                           float rankingReportedBestRatio,
                                                           SelectorTriangleSourceVariableTransactionTrace& outTrace);

    void BuildSelectorSupportPlanes(const G3D::Vector3& position,
                                    float verticalOffset,
                                    float horizontalRadius,
                                    std::array<SelectorSupportPlane, 9>& outPlanes);

    void BuildSelectorNeighborhood(const G3D::Vector3& position,
                                   float verticalOffset,
                                   float horizontalRadius,
                                   std::array<G3D::Vector3, 9>& outPoints,
                                   std::array<uint8_t, 32>& outSelectorIndices);

    float EvaluateSelectorPlaneRatio(const G3D::Vector3& candidatePoint,
                                     const SelectorSupportPlane& plane,
                                     const G3D::Vector3& testPoint);

    void ClipSelectorPointStripAgainstPlane(const SelectorSupportPlane& plane,
                                            uint32_t clipPlaneIndex,
                                            SelectorPointStrip& ioStrip);

    bool ClipSelectorPointStripAgainstPlanePrefix(const SelectorSupportPlane* planes,
                                                  uint32_t planeCount,
                                                  SelectorPointStrip& ioStrip);

    bool ClipSelectorPointStripExcludingPlane(const std::array<SelectorSupportPlane, 9>& planes,
                                              uint32_t excludedPlaneIndex,
                                              SelectorPointStrip& ioStrip);

    bool ValidateSelectorPointStripCandidate(const SelectorPointStrip& strip,
                                             const G3D::Vector3& testPoint,
                                             const std::array<SelectorSupportPlane, 9>& planes,
                                             uint32_t planeIndex,
                                             float& inOutBestRatio,
                                             SelectorCandidateValidationTrace* outTrace = nullptr);

    bool BuildSelectorCandidatePlaneRecord(const std::array<G3D::Vector3, 9>& points,
                                           const std::array<uint8_t, 3>& selectorIndices,
                                           const G3D::Vector3& translation,
                                           const SelectorSupportPlane& sourcePlane,
                                           std::array<SelectorSupportPlane, 4>& outPlanes);

    bool BuildSelectorCandidateQuadPlaneRecord(const std::array<G3D::Vector3, 9>& points,
                                               const std::array<uint8_t, 4>& selectorIndices,
                                               const G3D::Vector3& translation,
                                               const SelectorSupportPlane& sourcePlane,
                                               std::array<SelectorSupportPlane, 5>& outPlanes);

    bool HasSelectorCandidateWithUnitZ(const SelectorSupportPlane* candidates,
                                       uint32_t candidateCount);

    bool HasSelectorCandidateWithNegativeDiagonalZ(const SelectorSupportPlane* candidates,
                                                   uint32_t candidateCount);

    bool IsSelectorContactWithinAlternateWorkingVectorBand(float normalZ);

    SelectorAlternateWorkingVectorMode EvaluateSelectorAlternateWorkingVectorMode(float selectedNormalZ,
                                                                                  uint32_t candidateCount);

    bool EvaluateSelectorPlaneFootprintMismatch(const G3D::Vector3& position,
                                                float collisionRadius,
                                                const SelectorSupportPlane& selectedPlane);

    void BuildSelectorPlaneIntersectionPoint(const SelectorSupportPlane& selectedPlane,
                                             const SelectorSupportPlane& firstCandidatePlane,
                                             const SelectorSupportPlane& secondCandidatePlane,
                                             G3D::Vector3& outPoint);

    void BuildSelectorTriangleEdgeDirection(const SelectorCandidateRecord& selectedRecord,
                                            const G3D::Vector3& intersectionPoint,
                                            const G3D::Vector3& lineDirection,
                                            G3D::Vector3& outDirection,
                                            SelectorTriangleEdgeDirectionTrace* outTrace = nullptr);

    void BuildSelectorTwoCandidateWorkingVector(const G3D::Vector3& position,
                                                float collisionRadius,
                                                const SelectorCandidateRecord& selectedRecord,
                                                const SelectorSupportPlane& firstCandidatePlane,
                                                const SelectorSupportPlane& secondCandidatePlane,
                                                G3D::Vector3& outVector,
                                                SelectorTwoCandidateWorkingVectorTrace* outTrace = nullptr);

    void BuildSelectorAlternatePair(const G3D::Vector3& position,
                                    float collisionRadius,
                                    const SelectorCandidateRecord& selectedRecord,
                                    const SelectorSupportPlane* candidatePlanes,
                                    uint32_t candidateCount,
                                    const G3D::Vector3& inputMove,
                                    float windowStartScalar,
                                    float windowEndScalar,
                                    SelectorPair& outPair,
                                    SelectorAlternatePairTrace* outTrace = nullptr);

    bool EvaluateSelectorAlternateUnitZFallbackGate(float airborneTimeScalar,
                                                    float elapsedTimeScalar,
                                                    float horizontalSpeedScale,
                                                    float requestedDistance);

    float ComputeJumpTimeScalar(uint32_t movementFlags,
                                float verticalSpeed);

    bool EvaluateSelectorPairFollowupGate(float windowStartScalar,
                                          float windowSpanScalar,
                                          const G3D::Vector3& moveVector,
                                          bool alternateUnitZState,
                                          uint32_t movementFlags,
                                          float verticalSpeed,
                                          float horizontalSpeedScale);

    float ComputeVerticalTravelTimeScalar(float verticalDistance,
                                          bool preferEarlierPositiveRoot,
                                          uint32_t movementFlags,
                                          float verticalSpeed);

    float EvaluateSelectorPairWindowAdjustment(float windowSpanScalar,
                                               float windowStartScalar,
                                               G3D::Vector3& moveVector,
                                               float* outMoveMagnitude,
                                               bool alternateUnitZState,
                                               float horizontalReferenceMagnitude,
                                               uint32_t movementFlags,
                                               float verticalSpeed,
                                               float horizontalSpeedScale,
                                               float referenceZ,
                                               float positionZ);

    void EvaluateSelectorPairConsumer(float requestedDistance,
                                      const G3D::Vector3& inputMove,
                                      bool directionRankingAccepted,
                                      int32_t selectedIndex,
                                      uint32_t selectedCount,
                                      bool directGateAccepted,
                                      bool hasNegativeDiagonalCandidate,
                                      bool alternateUnitZFallbackGateAccepted,
                                      bool hasUnitZCandidate,
                                      const SelectorPair& directPair,
                                      const SelectorPair& alternatePair,
                                      SelectorPairConsumerTrace& outTrace);

    bool EvaluateSelectorCandidateRecordSet(const SelectorCandidateRecord* records,
                                            uint32_t recordCount,
                                            const G3D::Vector3& testPoint,
                                            const SelectorSupportPlane* clipPlanes,
                                            uint32_t clipPlaneCount,
                                            const std::array<SelectorSupportPlane, 9>& validationPlanes,
                                            uint32_t validationPlaneIndex,
                                            float& inOutBestRatio,
                                            uint32_t& outBestRecordIndex,
                                            SelectorRecordEvaluationTrace* outTrace = nullptr);

    bool EvaluateSelectorTriangleSourceRanking(const SelectorCandidateRecord* records,
                                               uint32_t recordCount,
                                               const G3D::Vector3& testPoint,
                                               const G3D::Vector3& candidateDirection,
                                               const std::array<G3D::Vector3, 9>& points,
                                               const std::array<SelectorSupportPlane, 9>& supportPlanes,
                                               const std::array<uint8_t, 32>& selectorIndices,
                                               std::array<SelectorSupportPlane, 5>& ioBestCandidates,
                                               uint32_t& ioCandidateCount,
                                               float& ioBestRatio,
                                               SelectorSourceRankingTrace* outTrace = nullptr);

    bool EvaluateSelectorDirectionRanking(const SelectorCandidateRecord* records,
                                          uint32_t recordCount,
                                          const G3D::Vector3& testPoint,
                                          const G3D::Vector3& candidateDirection,
                                          const std::array<G3D::Vector3, 9>& points,
                                          const std::array<SelectorSupportPlane, 9>& supportPlanes,
                                          const std::array<uint8_t, 32>& selectorIndices,
                                          std::array<SelectorSupportPlane, 5>& ioBestCandidates,
                                          uint32_t& ioCandidateCount,
                                          float& ioBestRatio,
                                          float& outReportedBestRatio,
                                          uint32_t& ioBestRecordIndex,
                                          SelectorDirectionRankingTrace* outTrace = nullptr);

    bool ResolveGroundedWallContacts(const std::vector<SceneQuery::AABBContact>& slideContacts,
                                     const G3D::Vector3& currentPosition,
                                     const G3D::Vector3& requestedMove,
                                     float collisionRadius,
                                     float boundingHeight,
                                     bool& groundedWallState,
                                     G3D::Vector3& outResolvedMove,
                                     G3D::Vector3& outWallNormal,
                                     float& outBlockedFraction,
                                     GroundedWallResolutionTrace* outTrace = nullptr);
}
