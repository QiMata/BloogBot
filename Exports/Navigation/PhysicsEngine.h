// PhysicsEngine.h - Stateless physics engine with singleton pattern for resource management
#pragma once

#include "PhysicsBridge.h"
#include <memory>
#include <cmath>
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

    // Configuration: walkable slope threshold (cosine of max slope angle)
    void SetWalkableCosMin(float cosMin);
    float GetWalkableCosMin() const;

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
        G3D::Vector3 groundNormal;
    };

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
            impactPoint = G3D::Vector3(0,0,0);
            impactNormal = G3D::Vector3(0,0,1);
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
            floorPoint = G3D::Vector3(0,0,0);
            floorNormal = G3D::Vector3(0,0,1);
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

    // Cylinder-based surface finding (simplified: single terrain + minimal vmap sampling)
    WalkableSurface FindWalkableSurfaceWithCylinder(uint32_t mapId, float x, float y, float currentZ,
        float maxStepUp, float maxStepDown, float cylinderRadius, float cylinderHeight);

    // Movement processing (simplified authentic style)
    void ProcessGroundMovementWithCylinder(const PhysicsInput& input, const MovementIntent& intent, MovementState& state,
        float dt, float speed, float cylinderRadius, float cylinderHeight);
    void ProcessAirMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state, float dt, float speed);
    void ProcessSwimMovement(const PhysicsInput& input, const MovementIntent& intent, MovementState& state, float dt, float speed);

    bool ValidateCylinderPosition(uint32_t mapId, float x, float y, float z,
        float tolerance, float cylinderRadius, float cylinderHeight);

    // New helpers for improved authenticity
    bool HasHeadClearance(uint32_t mapId, float x, float y, float newZ, float radius, float height);
    void AttemptWallSlide(const PhysicsInput& input, const MovementIntent& intent, MovementState& state,
        float dt, float speed, float radius, float height);

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
    void ResolveGroundAttachment(MovementState& st, const WalkableSurface& surf, float stepUpLimit, float stepDownLimit, float dt);
};