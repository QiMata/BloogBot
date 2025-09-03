// PhysicsEngine.h - Enhanced with cylinder collision support
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
    constexpr float STEP_HEIGHT = 2.3f;
    constexpr float STEP_DOWN_HEIGHT = 4.0f;

    // Height constants
    constexpr float INVALID_HEIGHT = -200000.0f;
    constexpr float MAX_HEIGHT = 100000.0f;
    constexpr float DEFAULT_HEIGHT_SEARCH = 50.0f;

    // Player cylinder dimensions (based on WoW capsule collision)
    constexpr float PLAYER_RADIUS = 0.35f;  // Standard player collision radius
    constexpr float PLAYER_HEIGHT = 2.0f;   // Standard player height
}

class PhysicsEngine
{
public:
    static PhysicsEngine* Instance();
    static void Destroy();

    void Initialize();
    void Shutdown();

    PhysicsOutput Step(const PhysicsInput& input, float dt);

    // Legacy method kept for backward compatibility
    float GetHeight(uint32_t mapId, float x, float y, float z, bool checkVMap, float maxSearchDist);

private:
    static PhysicsEngine* s_instance;
    VMAP::VMapManager2* m_vmapManager;
    std::unique_ptr<MapLoader> m_mapLoader;
    Navigation* m_navigation;
    bool m_initialized;
    uint32_t m_currentMapId;

    // Step-down tracking to improve movement continuity
    bool m_lastStepWasDown;
    int m_framesSinceStepDown;

    // Cylinder collision parameters
    float m_playerRadius;
    float m_playerHeight;

    float m_lastValidSurfaceHeight = PhysicsConstants::INVALID_HEIGHT;
    int m_framesSinceLastSurface = 0;

    PhysicsEngine();

    // Simple movement state
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

    // Cylinder-based surface finding
    WalkableSurface FindWalkableSurfaceWithCylinder(uint32_t mapId, float x, float y, float currentZ,
        float maxStepUp, float maxStepDown);

    // Legacy ray-based surface finding (fallback)
    WalkableSurface FindWalkableSurface(uint32_t mapId, float x, float y, float currentZ,
        float maxStepUp, float maxStepDown);

    // Movement processing with cylinder collision
    void ProcessGroundMovementWithCylinder(const PhysicsInput& input, MovementState& state, float dt);
    void ProcessAirMovement(const PhysicsInput& input, MovementState& state, float dt);
    void ProcessSwimMovement(const PhysicsInput& input, MovementState& state, float dt);

    // Cylinder collision helpers
    bool CheckCylinderMovement(uint32_t mapId, const MovementState& currentState,
        float newX, float newY, float& outZ, G3D::Vector3& outNormal);
    bool ValidateCylinderPosition(uint32_t mapId, float x, float y, float z, float tolerance = 0.05f);

    // Separated slide movement logic with cylinder collision
    void AttemptSlideMovementWithCylinder(const PhysicsInput& input, MovementState& state,
        float moveX, float moveY, float moveDist);

    // Helper methods
    float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming);
    void ApplyGravity(MovementState& state, float dt);

    // Create player cylinder at position
    VMAP::Cylinder CreatePlayerCylinder(float x, float y, float z) const;
};