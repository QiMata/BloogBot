// PhysicsEngine.h - Stateless physics engine with singleton pattern for resource management
#pragma once

#include "PhysicsBridge.h"
#include <memory>
#include <cmath>
#include "CylinderCollision.h"

// Forward declarations
namespace VMAP {
    class VMapManager2;
    class Cylinder;
}
namespace G3D {
    class Vector3;
}
class Navigation;
class MapLoader;

// WoW 1.12.1 Physics Constants
namespace PhysicsConstants
{
    // Gravity and movement
    constexpr float GRAVITY = 19.2911f;
    constexpr float JUMP_VELOCITY = 7.95577f;
    constexpr float WATER_LEVEL_DELTA = 2.0f;

    // Ground detection
    constexpr float GROUND_HEIGHT_TOLERANCE = 0.1f;
    constexpr float STEP_HEIGHT = 2.8f;
    constexpr float STEP_DOWN_HEIGHT = 4.0f;

    // Height constants
    constexpr float INVALID_HEIGHT = -200000.0f;
    constexpr float MAX_HEIGHT = 100000.0f;
    constexpr float DEFAULT_HEIGHT_SEARCH = 50.0f;
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
    };

    // Surface information
    enum class SurfaceSource
    {
        NONE,
        TERRAIN,
        VMAP,
        CYLINDER
    };

    struct WalkableSurface
    {
        bool found;
        float height;
        SurfaceSource source;
        G3D::Vector3 normal;
    };

    // Core height/collision methods
    void EnsureMapLoaded(uint32_t mapId);
    float GetTerrainHeight(uint32_t mapId, float x, float y);
    float GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType);

    // Cylinder-based surface finding - now takes radius and height as parameters
    WalkableSurface FindWalkableSurfaceWithCylinder(uint32_t mapId, float x, float y, float currentZ,
        float maxStepUp, float maxStepDown, float cylinderRadius, float cylinderHeight);

    // Movement processing with cylinder collision - now takes radius and height
    void ProcessGroundMovementWithCylinder(const PhysicsInput& input, MovementState& state,
        float dt, float cylinderRadius, float cylinderHeight);
    void ProcessAirMovement(const PhysicsInput& input, MovementState& state, float dt);
    void ProcessSwimMovement(const PhysicsInput& input, MovementState& state, float dt);

    // Cylinder collision helpers - now take dimensions as parameters
    bool CheckCylinderMovement(uint32_t mapId, const MovementState& currentState,
        float newX, float newY, float& outZ, G3D::Vector3& outNormal,
        float cylinderRadius, float cylinderHeight);
    bool ValidateCylinderPosition(uint32_t mapId, float x, float y, float z,
        float tolerance, float cylinderRadius, float cylinderHeight);

    // Slide movement logic with cylinder collision
    void AttemptSlideMovementWithCylinder(const PhysicsInput& input, MovementState& state,
        float moveX, float moveY, float moveDist, float cylinderRadius, float cylinderHeight);

    // Helper methods
    float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming);
    void ApplyGravity(MovementState& state, float dt);

    // Create player cylinder at position with specified dimensions
    VMAP::Cylinder CreatePlayerCylinder(float x, float y, float z,
        float radius, float height) const;
};