// PhysicsEngine.h - Stateless physics engine with singleton pattern for resource management
#pragma once

#include "PhysicsBridge.h"
#include <memory>
#include <cmath>
#include <vector>
#include "Vector3.h" // Needed for by-value usage of G3D::Vector3

// Forward declarations
namespace VMAP {
    class VMapManager2;
    struct Cylinder; // match actual declaration
}
namespace G3D {
    class Vector3; // still forward declare (already included but harmless)
}
class Navigation;
class MapLoader;

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

    // Slope walkability threshold (cos 60° = 0.5) - kept only as documentation default; configurable at runtime
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

    // Main physics step - completely stateless
    PhysicsOutput Step(const PhysicsInput& input, float dt);

    // New: modernized step using diagnostics-driven movement
    PhysicsOutput StepV2(const PhysicsInput& input, float dt);

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

    // New: expose MapLoader for read-only terrain queries
    MapLoader* GetMapLoader() const;

    // New: diagnostic summary for capsule sweep and terrain triangles
    struct SweepDiagnostics
    {
        // Combined summary (VMAP + ADT hits appended)
        size_t hitCount = 0;
        size_t penCount = 0;
        size_t nonPenCount = 0;
        size_t walkableNonPen = 0;
        float earliestNonPen = -1.0f;
        float hitMinZ = 0.0f;
        float hitMaxZ = 0.0f;
        size_t uniqueInstanceCount = 0;

        // VMAP-only summary
        size_t vmapHitCount = 0;
        size_t vmapPenCount = 0;
        size_t vmapNonPenCount = 0;
        size_t vmapWalkableNonPen = 0;
        float vmapEarliestNonPen = -1.0f;
        float vmapHitMinZ = 0.0f;
        float vmapHitMaxZ = 0.0f;
        size_t vmapUniqueInstanceCount = 0;

        // ADT terrain diagnostics
        size_t terrainTriCount = 0;
        float terrainMinZ = 0.0f;
        float terrainMaxZ = 0.0f;
        // ADT overlap hits (penetrating) summary
        size_t adtPenetratingHitCount = 0;
        float adtHitMinZ = 0.0f;
        float adtHitMaxZ = 0.0f;

        // Selected standing placement using inflated radius (+0.02)
        bool standFound = false;
        float standZ = 0.0f;
        enum class StandSource { None, VMAP, ADT };
        StandSource standSource = StandSource::None;

        // Movement manifold built from collective triangle hits
        struct ContactPlane {
            G3D::Vector3 normal; // unit normal
            G3D::Vector3 point;  // point on plane (world)
            bool walkable;       // normal.z >= walkable threshold
            bool penetrating;    // came from start penetration
            StandSource source = StandSource::None; // origin of plane (VMAP or ADT)
        };
        std::vector<ContactPlane> planes;       // all contact planes considered
        std::vector<ContactPlane> walkablePlanes; // subset of planes that are walkable
        G3D::Vector3 slideDir;                  // projected movement direction along the primary plane
        bool slideDirValid = false;             // whether slideDir was computed
        ContactPlane primaryPlane;              // the plane chosen for movement resolution
        bool hasPrimaryPlane = false;

        // Extended manifold diagnostics
        G3D::Vector3 intersectionLineDir;       // when two walkable planes found, their intersection direction
        bool hasIntersectionLine = false;       // true if intersectionLineDir was computed
        float xyReduction = 1.0f;               // horizontal reduction factor when sliding on slope
        float suggestedXYDist = 0.0f;           // suggested XY travel distance respecting manifold projection
        int constraintIterations = 0;           // number of projection iterations that would be used
        float slopeClampThresholdZ = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z; // threshold used for clamping

        // Continuous collision detection & depenetration diagnostics
        float minTOI = -1.0f;                   // minimum time of impact in [0,1]
        G3D::Vector3 depenetration;             // accumulated depenetration vector from penetrating contacts
        float depenetrationMagnitude = 0.0f;    // length of depenetration
        float suggestedSkinWidth = 0.0f;        // small inset to avoid immediate re-penetration

        // Liquid diagnostics (start/end of sweep)
        bool liquidStartHasLevel = false;
        float liquidStartLevel = 0.0f;
        uint32_t liquidStartType = 0u;
        bool liquidStartFromVmap = false;
        bool liquidStartSwimming = false;

        bool liquidEndHasLevel = false;
        float liquidEndLevel = 0.0f;
        uint32_t liquidEndType = 0u;
        bool liquidEndFromVmap = false;
        bool liquidEndSwimming = false;
    };

    // Compute capsule sweep diagnostics and terrain triangle stats within the swept AABB
    SweepDiagnostics ComputeCapsuleSweepDiagnostics(
        uint32_t mapId,
        float x,
        float y,
        float z,
        float radius,
        float height,
        const G3D::Vector3& moveDir,
        float intendedDist);

    // New: liquid evaluation result
    struct LiquidInfo {
        float level = 0.0f;
        uint32_t type = 0u;
        bool fromVmap = false;
        bool hasLevel = false;
        bool isSwimming = false;
    };

    // Evaluate liquid at a position and determine swimming (water-only), preferring VMAP
    LiquidInfo EvaluateLiquidAt(uint32_t mapId, float x, float y, float z) const;

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
    float GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType);

    // Movement processing (simplified authentic style)
    void ProcessGroundMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state,
        float dt, float speed, float cylinderRadius, float cylinderHeight);
    void ProcessAirMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state, float dt, float speed);
    void ProcessSwimMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state, float dt, float speed);

    // Helper methods
    float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming);
    void ApplyGravity(MovementState& state, float dt);

    // Phase 1 extracted helpers
    MovementIntent BuildMovementIntent(const PhysicsInput& input, float orientation) const;
    float QueryLiquidLevel(uint32_t mapId, float x, float y, float z, uint32_t& liquidType) const;
};