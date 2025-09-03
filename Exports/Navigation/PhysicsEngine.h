// PhysicsEngine.h - Fixed with step-down tracking
#pragma once

#include "PhysicsBridge.h"
#include <memory>
#include <cmath>

// Forward declarations
namespace VMAP {
    class VMapManager2;
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

    // Ground detection - slightly increased tolerance for better stability
    constexpr float GROUND_HEIGHT_TOLERANCE = 0.1f;  // Increased from 0.05f
    constexpr float STEP_HEIGHT = 2.3f;
    constexpr float STEP_DOWN_HEIGHT = 4.0f;

    // Height constants
    constexpr float INVALID_HEIGHT = -200000.0f;
    constexpr float MAX_HEIGHT = 100000.0f;
    constexpr float DEFAULT_HEIGHT_SEARCH = 50.0f;
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
        VMAP
    };

    struct WalkableSurface
    {
        bool found;
        float height;
        SurfaceSource source;
    };

    // Core height/collision methods
    void EnsureMapLoaded(uint32_t mapId);
    float GetTerrainHeight(uint32_t mapId, float x, float y);
    float GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType);

    // Unified surface finding method with improved step-down handling
    WalkableSurface FindWalkableSurface(uint32_t mapId, float x, float y, float currentZ,
        float maxStepUp, float maxStepDown);

    // Movement processing
    void ProcessGroundMovement(const PhysicsInput& input, MovementState& state, float dt);
    void ProcessAirMovement(const PhysicsInput& input, MovementState& state, float dt);
    void ProcessSwimMovement(const PhysicsInput& input, MovementState& state, float dt);

    // Separated slide movement logic
    void AttemptSlideMovement(const PhysicsInput& input, MovementState& state,
        float moveX, float moveY, float moveDist);

    // Helper methods
    float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming);
    void ApplyGravity(MovementState& state, float dt);
};