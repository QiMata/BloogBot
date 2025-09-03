// PhysicsEngine.cpp - Fixed step-down movement continuity
#include "PhysicsEngine.h"
#include "CylinderCollision.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "MapLoader.h"
#include "Navigation.h"
#include <algorithm>
#include <filesystem>
#include <iostream>
#include <iomanip>
#include <cfloat>

using namespace PhysicsConstants;

PhysicsEngine* PhysicsEngine::s_instance = nullptr;

PhysicsEngine* PhysicsEngine::Instance()
{
    if (!s_instance)
        s_instance = new PhysicsEngine();
    return s_instance;
}

void PhysicsEngine::Destroy()
{
    delete s_instance;
    s_instance = nullptr;
}

PhysicsEngine::PhysicsEngine()
    : m_vmapManager(nullptr), m_navigation(nullptr),
    m_initialized(false), m_currentMapId(UINT32_MAX),
    m_lastStepWasDown(false), m_framesSinceStepDown(0)
{
}

void PhysicsEngine::Initialize()
{
    if (m_initialized)
        return;

    std::cout << "[PhysicsEngine] Initializing..." << std::endl;

    // Initialize MapLoader for terrain data
    m_mapLoader = std::make_unique<MapLoader>();
    std::vector<std::string> mapPaths = { "maps/", "Data/maps/", "../Data/maps/" };

    for (const auto& path : mapPaths)
    {
        if (std::filesystem::exists(path))
        {
            if (m_mapLoader->Initialize(path))
            {
                std::cout << "[PhysicsEngine] MapLoader initialized with path: " << path << std::endl;
                break;
            }
        }
    }

    // Initialize VMAP system
    try
    {
        m_vmapManager = static_cast<VMAP::VMapManager2*>(
            VMAP::VMapFactory::createOrGetVMapManager());

        if (m_vmapManager)
        {
            VMAP::VMapFactory::initialize();

            std::vector<std::string> vmapPaths = { "vmaps/", "Data/vmaps/", "../Data/vmaps/" };
            for (const auto& path : vmapPaths)
            {
                if (std::filesystem::exists(path))
                {
                    m_vmapManager->setBasePath(path);
                    std::cout << "[PhysicsEngine] VMapManager initialized with path: " << path << std::endl;
                    break;
                }
            }
        }
    }
    catch (...)
    {
        std::cout << "[PhysicsEngine] Failed to initialize VMapManager" << std::endl;
        m_vmapManager = nullptr;
    }

    m_navigation = Navigation::GetInstance();
    m_initialized = true;
    std::cout << "[PhysicsEngine] Initialization complete" << std::endl;
}

void PhysicsEngine::Shutdown()
{
    std::cout << "[PhysicsEngine] Shutting down..." << std::endl;
    m_vmapManager = nullptr;
    m_mapLoader.reset();
    m_currentMapId = UINT32_MAX;
    m_initialized = false;
    m_lastStepWasDown = false;
    m_framesSinceStepDown = 0;
}

void PhysicsEngine::EnsureMapLoaded(uint32_t mapId)
{
    if (m_currentMapId != mapId && m_vmapManager)
    {
        if (!m_vmapManager->isMapInitialized(mapId))
        {
            std::cout << "[PhysicsEngine] Initializing map " << mapId << std::endl;
            m_vmapManager->initializeMap(mapId);
        }
        m_currentMapId = mapId;
    }
}

float PhysicsEngine::GetTerrainHeight(uint32_t mapId, float x, float y)
{
    if (!m_mapLoader || !m_mapLoader->IsInitialized())
        return INVALID_HEIGHT;

    return m_mapLoader->GetHeight(mapId, x, y);
}

float PhysicsEngine::GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType)
{
    // Try ADT data first
    if (m_mapLoader && m_mapLoader->IsInitialized())
    {
        float liquidLevel = m_mapLoader->GetLiquidLevel(mapId, x, y);
        if (liquidLevel > INVALID_HEIGHT)
        {
            liquidType = m_mapLoader->GetLiquidType(mapId, x, y);
            return liquidLevel;
        }
    }

    // Then try VMAP for WMO liquids
    if (m_vmapManager)
    {
        float liquidLevel, liquidFloor;
        uint32_t vmapLiquidType;
        if (m_vmapManager->GetLiquidLevel(mapId, x, y, z, 0xFF, liquidLevel, liquidFloor, vmapLiquidType))
        {
            liquidType = vmapLiquidType;
            return liquidLevel;
        }
    }

    return INVALID_HEIGHT;
}

PhysicsEngine::WalkableSurface PhysicsEngine::FindWalkableSurface(
    uint32_t mapId, float x, float y, float currentZ,
    float maxStepUp, float maxStepDown)
{
    WalkableSurface result;
    result.found = false;
    result.height = INVALID_HEIGHT;
    result.source = SurfaceSource::NONE;

    std::cout << "\n[FindWalkableSurface] pos(" << x << "," << y << ") currentZ:"
        << currentZ << " stepUp:" << maxStepUp << " stepDown:" << maxStepDown;

    // Add context about recent movement
    if (m_lastStepWasDown)
    {
        std::cout << " [RECENT STEP DOWN - frame " << m_framesSinceStepDown << "]";
    }
    std::cout << std::endl;

    // Ensure VMAP is ready
    if (m_vmapManager)
    {
        EnsureMapLoaded(mapId);
        const float GRID_SIZE = 533.33333f;
        const float MID = 32.0f * GRID_SIZE;
        int tileX = (int)((MID - y) / GRID_SIZE);
        int tileY = (int)((MID - x) / GRID_SIZE);
        m_vmapManager->loadMap(nullptr, mapId, tileX, tileY);
    }

    // Check terrain height
    float terrainZ = GetTerrainHeight(mapId, x, y);
    if (terrainZ > INVALID_HEIGHT)
    {
        float terrainDiff = terrainZ - currentZ;
        std::cout << "  Terrain height: " << terrainZ << " (diff: " << terrainDiff << ")" << std::endl;

        if (terrainDiff >= -maxStepDown && terrainDiff <= maxStepUp)
        {
            result.found = true;
            result.height = terrainZ;
            result.source = SurfaceSource::TERRAIN;
        }
    }

    // Check VMAP surfaces with improved search strategy
    if (m_vmapManager)
    {
        // Define search ranges based on step limits and recent movement
        struct SearchRange {
            float startZ;      // Where to start searching from
            float searchDist;  // How far to search
            const char* desc;
        };

        // Adjust search strategy based on recent step-down
        float extraSearchMargin = 0.0f;
        if (m_lastStepWasDown && m_framesSinceStepDown < 3)
        {
            // After stepping down, be more aggressive in finding ground
            extraSearchMargin = 2.0f;
        }

        // Improved search ranges for better ground detection
        SearchRange ranges[] = {
            // Main search from above - covers most cases
            {currentZ + maxStepUp + 2.0f, maxStepUp + maxStepDown + 4.0f + extraSearchMargin, "full range"},

            // Search immediately below feet - crucial for maintaining ground contact
            {currentZ + 0.5f, 2.0f + extraSearchMargin, "at feet"},

            // Extended search below for step-down scenarios
            {currentZ - 2.0f, 5.0f + extraSearchMargin, "below feet"},

            // Mid-range search for steps
            {currentZ + STEP_HEIGHT * 0.5f, STEP_HEIGHT, "mid step"}
        };

        float bestVmapZ = INVALID_HEIGHT;

        for (const auto& range : ranges)
        {
            float vmapZ = m_vmapManager->getHeight(mapId, x, y, range.startZ, range.searchDist);

            if (vmapZ > INVALID_HEIGHT)
            {
                float vmapDiff = vmapZ - currentZ;
                std::cout << "  VMAP search from " << range.desc << " found surface at "
                    << vmapZ << " (diff: " << vmapDiff << ")" << std::endl;

                // Is this within our step limits?
                if (vmapDiff >= -maxStepDown && vmapDiff <= maxStepUp)
                {
                    // Prefer the highest valid VMAP surface
                    if (bestVmapZ <= INVALID_HEIGHT || vmapZ > bestVmapZ)
                    {
                        bestVmapZ = vmapZ;
                    }
                }
            }
        }

        // Compare VMAP with terrain and select the best surface
        if (bestVmapZ > INVALID_HEIGHT)
        {
            // If we have both VMAP and terrain, prefer VMAP if it's higher
            // (player should walk on structures rather than terrain below them)
            if (!result.found || bestVmapZ > result.height)
            {
                result.found = true;
                result.height = bestVmapZ;
                result.source = SurfaceSource::VMAP;
            }
        }
    }

    if (result.found)
    {
        std::cout << "  Selected surface: " << result.height
            << " (source: " << (result.source == SurfaceSource::TERRAIN ? "TERRAIN" : "VMAP")
            << ")" << std::endl;
    }
    else
    {
        std::cout << "  No walkable surface found" << std::endl;
    }

    return result;
}

float PhysicsEngine::GetHeight(uint32_t mapId, float x, float y, float z, bool checkVMap, float maxSearchDist)
{
    // This method is now a simple wrapper that uses FindWalkableSurface
    // It's kept for backward compatibility

    if (!checkVMap)
    {
        return GetTerrainHeight(mapId, x, y);
    }

    // Use the new unified surface finding logic
    // For generic height queries, we search both up and down from the given Z
    WalkableSurface surface = FindWalkableSurface(mapId, x, y, z,
        maxSearchDist * 0.5f,  // Search up
        maxSearchDist);         // Search down

    if (surface.found)
    {
        return surface.height;
    }

    // Fall back to just terrain if no walkable surface found
    return GetTerrainHeight(mapId, x, y);
}

float PhysicsEngine::CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming)
{
    if (isSwimming)
        return input.swimSpeed;
    if (input.moveFlags & MOVEFLAG_WALK_MODE)
        return input.walkSpeed;
    if (input.moveFlags & MOVEFLAG_BACKWARD)
        return input.runBackSpeed;
    return input.runSpeed;
}

void PhysicsEngine::ApplyGravity(MovementState& state, float dt)
{
    state.vz -= GRAVITY * dt;
    if (state.vz < -54.0f)  // Terminal velocity
        state.vz = -54.0f;
}

void PhysicsEngine::ProcessGroundMovement(const PhysicsInput& input, MovementState& state, float dt)
{
    std::cout << "\n=== ProcessGroundMovement ===" << std::endl;
    std::cout << "Current pos: (" << state.x << ", " << state.y << ", " << state.z << ")" << std::endl;

    // Track step-down state
    if (m_framesSinceStepDown < 10)
    {
        std::cout << "Frames since step-down: " << m_framesSinceStepDown << std::endl;
    }

    float speed = CalculateMoveSpeed(input, false);
    std::cout << "Movement speed: " << speed << std::endl;

    // Handle jumping
    if (input.moveFlags & MOVEFLAG_JUMPING)
    {
        std::cout << "Jump initiated!" << std::endl;
        state.vz = JUMP_VELOCITY;
        state.isGrounded = false;
        state.fallTime = 0;
        m_lastStepWasDown = false;  // Reset step-down tracking
        return;
    }

    // Calculate movement direction
    float moveX = 0, moveY = 0;

    if (input.moveFlags & MOVEFLAG_FORWARD)
    {
        moveX = std::cos(state.orientation);
        moveY = std::sin(state.orientation);
    }
    else if (input.moveFlags & MOVEFLAG_BACKWARD)
    {
        moveX = -std::cos(state.orientation);
        moveY = -std::sin(state.orientation);
    }

    if (input.moveFlags & MOVEFLAG_STRAFE_LEFT)
    {
        moveX -= std::sin(state.orientation);
        moveY += std::cos(state.orientation);
    }
    else if (input.moveFlags & MOVEFLAG_STRAFE_RIGHT)
    {
        moveX += std::sin(state.orientation);
        moveY -= std::cos(state.orientation);
    }

    // Normalize diagonal movement
    float moveLength = std::sqrt(moveX * moveX + moveY * moveY);
    if (moveLength > 1.0f)
    {
        moveX /= moveLength;
        moveY /= moveLength;
    }

    // Calculate new position
    float newX = state.x + moveX * speed * dt;
    float newY = state.y + moveY * speed * dt;

    std::cout << "Desired pos: (" << newX << ", " << newY << ", ?)" << std::endl;

    // Track previous height for step detection
    float previousZ = state.z;

    // Find walkable surface at destination
    // Increase search range if we recently stepped down
    float searchUp = STEP_HEIGHT;
    float searchDown = STEP_DOWN_HEIGHT;

    if (m_lastStepWasDown && m_framesSinceStepDown < 3)
    {
        // After stepping down, be more generous with ground detection
        searchDown += 1.0f;
    }

    WalkableSurface destSurface = FindWalkableSurface(
        input.mapId, newX, newY, state.z,
        searchUp, searchDown);

    if (destSurface.found)
    {
        float heightDiff = destSurface.height - state.z;
        std::cout << "Found surface at destination: " << destSurface.height
            << " (diff: " << heightDiff << ")" << std::endl;

        // Determine step type
        auto stepResult = VMAP::CylinderHelpers::CheckStepHeight(
            state.z, destSurface.height, STEP_HEIGHT, STEP_DOWN_HEIGHT);

        switch (stepResult)
        {
        case VMAP::CylinderHelpers::STEP_UP:
            std::cout << "STEP_UP - Moving to new position" << std::endl;
            state.x = newX;
            state.y = newY;
            state.z = destSurface.height + GROUND_HEIGHT_TOLERANCE;
            m_lastStepWasDown = false;  // Reset step-down tracking
            break;

        case VMAP::CylinderHelpers::STEP_DOWN:
            std::cout << "STEP_DOWN - Moving to new position" << std::endl;
            state.x = newX;
            state.y = newY;
            // Use slightly larger tolerance after stepping down for stability
            state.z = destSurface.height + GROUND_HEIGHT_TOLERANCE * 2.0f;
            m_lastStepWasDown = true;
            m_framesSinceStepDown = 0;
            break;

        case VMAP::CylinderHelpers::STEP_BLOCKED:
            // Surface too high - try sliding
            std::cout << "STEP_BLOCKED - Surface too high, attempting slide" << std::endl;
            AttemptSlideMovement(input, state, moveX, moveY, speed * dt);
            break;

        case VMAP::CylinderHelpers::STEP_FALL:
            // Drop too far - but if we just stepped down, be more lenient
            if (m_lastStepWasDown && m_framesSinceStepDown < 2)
            {
                // Allow slightly larger drops right after a step-down
                if (heightDiff >= -(STEP_DOWN_HEIGHT + 1.0f))
                {
                    std::cout << "STEP_FALL (lenient) - Continuing step-down chain" << std::endl;
                    state.x = newX;
                    state.y = newY;
                    state.z = destSurface.height + GROUND_HEIGHT_TOLERANCE * 2.0f;
                    m_framesSinceStepDown = 0;  // Reset counter
                    break;
                }
            }

            std::cout << "STEP_FALL - Drop too far, starting fall" << std::endl;
            state.x = newX;
            state.y = newY;
            state.isGrounded = false;
            state.fallTime = 0;
            m_lastStepWasDown = false;
            break;
        }
    }
    else
    {
        std::cout << "No valid ground at destination - checking for edge" << std::endl;

        // No ground found - but if we just stepped down, try harder to find ground
        if (m_lastStepWasDown && m_framesSinceStepDown < 2)
        {
            // Try an extended search with more generous parameters
            std::cout << "Recent step-down detected - trying extended ground search" << std::endl;

            WalkableSurface extendedSearch = FindWalkableSurface(
                input.mapId, newX, newY, state.z,
                STEP_HEIGHT, STEP_DOWN_HEIGHT + 2.0f);

            if (extendedSearch.found)
            {
                float heightDiff = extendedSearch.height - state.z;
                if (heightDiff >= -(STEP_DOWN_HEIGHT + 2.0f))
                {
                    std::cout << "Extended search successful - continuing movement" << std::endl;
                    state.x = newX;
                    state.y = newY;
                    state.z = extendedSearch.height + GROUND_HEIGHT_TOLERANCE * 2.0f;
                    m_framesSinceStepDown = 0;
                }
                else
                {
                    std::cout << "Extended search found ground but too far down" << std::endl;
                    // Stay in place
                }
            }
            else
            {
                std::cout << "Extended search failed - stopping at edge" << std::endl;
                // Stay in place
            }
        }
        else
        {
            // Check if we're at an edge
            WalkableSurface currentSurface = FindWalkableSurface(
                input.mapId, state.x, state.y, state.z,
                1.0f, 1.0f);

            if (currentSurface.found && std::abs(currentSurface.height - state.z) < 1.0f)
            {
                std::cout << "At edge - stopping movement" << std::endl;
                // Stay in place (at edge)
            }
            else
            {
                std::cout << "No ground below either - possibly in air" << std::endl;
                // Move forward but start falling
                state.x = newX;
                state.y = newY;
                state.isGrounded = false;
                state.fallTime = 0;
                m_lastStepWasDown = false;
            }
        }
    }

    // Update frame counter for step-down tracking
    if (m_lastStepWasDown)
    {
        m_framesSinceStepDown++;
        if (m_framesSinceStepDown > 10)
        {
            m_lastStepWasDown = false;  // Reset after enough frames
        }
    }

    std::cout << "Final position: (" << state.x << ", " << state.y << ", " << state.z << ")" << std::endl;
    std::cout << "=== End ProcessGroundMovement ===\n" << std::endl;
}

void PhysicsEngine::AttemptSlideMovement(const PhysicsInput& input, MovementState& state,
    float moveX, float moveY, float moveDist)
{
    // Try sliding perpendicular to the movement direction
    float slideX = moveY * moveDist * 0.7f;
    float slideY = -moveX * moveDist * 0.7f;

    std::cout << "Attempting slide movement: (" << slideX << ", " << slideY << ")" << std::endl;

    // Try both slide directions
    for (int dir = 0; dir < 2; ++dir)
    {
        float testX = state.x + (dir == 0 ? slideX : -slideX);
        float testY = state.y + (dir == 0 ? slideY : -slideY);

        std::cout << "Testing slide direction " << dir << ": (" << testX << ", " << testY << ")" << std::endl;

        // Find surface at slide position
        WalkableSurface slideSurface = FindWalkableSurface(
            input.mapId, testX, testY, state.z,
            STEP_HEIGHT, STEP_DOWN_HEIGHT);

        if (slideSurface.found)
        {
            auto slideResult = VMAP::CylinderHelpers::CheckStepHeight(
                state.z, slideSurface.height, STEP_HEIGHT, STEP_DOWN_HEIGHT);

            if (slideResult == VMAP::CylinderHelpers::STEP_UP ||
                slideResult == VMAP::CylinderHelpers::STEP_DOWN)
            {
                std::cout << "Slide successful to height " << slideSurface.height << std::endl;
                state.x = testX;
                state.y = testY;
                state.z = slideSurface.height + GROUND_HEIGHT_TOLERANCE;

                // Track if this was a step-down
                if (slideResult == VMAP::CylinderHelpers::STEP_DOWN)
                {
                    m_lastStepWasDown = true;
                    m_framesSinceStepDown = 0;
                }
                else
                {
                    m_lastStepWasDown = false;
                }

                return;
            }
        }
    }

    std::cout << "Slide movement failed - staying in place" << std::endl;
}

void PhysicsEngine::ProcessAirMovement(const PhysicsInput& input, MovementState& state, float dt)
{
    std::cout << "\n=== ProcessAirMovement === Fall time: " << state.fallTime << std::endl;

    state.fallTime += dt;
    ApplyGravity(state, dt);

    // Reset step-down tracking when in air
    m_lastStepWasDown = false;
    m_framesSinceStepDown = 0;

    // Limited air control
    float speed = CalculateMoveSpeed(input, false) * 0.5f;  // Reduced air control

    if (input.moveFlags & MOVEFLAG_FORWARD)
    {
        state.x += std::cos(state.orientation) * speed * dt;
        state.y += std::sin(state.orientation) * speed * dt;
    }
    else if (input.moveFlags & MOVEFLAG_BACKWARD)
    {
        state.x -= std::cos(state.orientation) * speed * dt;
        state.y -= std::sin(state.orientation) * speed * dt;
    }

    state.z += state.vz * dt;

    // Check for landing
    WalkableSurface groundSurface = FindWalkableSurface(
        input.mapId, state.x, state.y, state.z,
        0.1f,  // Don't look for surfaces above us when falling
        DEFAULT_HEIGHT_SEARCH);  // Look far below

    if (state.vz <= 0 && groundSurface.found)
    {
        float distToGround = state.z - groundSurface.height;
        std::cout << "Air: Checking landing - ground at " << groundSurface.height
            << " dist: " << distToGround << std::endl;

        if (distToGround <= GROUND_HEIGHT_TOLERANCE)
        {
            std::cout << "Landing!" << std::endl;
            state.z = groundSurface.height + GROUND_HEIGHT_TOLERANCE;
            state.vz = 0;
            state.isGrounded = true;
            state.fallTime = 0;
        }
    }
}

void PhysicsEngine::ProcessSwimMovement(const PhysicsInput& input, MovementState& state, float dt)
{
    std::cout << "\n=== ProcessSwimMovement ===" << std::endl;

    // Reset step-down tracking when swimming
    m_lastStepWasDown = false;
    m_framesSinceStepDown = 0;

    float speed = input.swimSpeed;

    float moveZ = std::sin(state.pitch);
    float horizontalScale = std::cos(state.pitch);

    if (input.moveFlags & MOVEFLAG_FORWARD)
    {
        state.x += std::cos(state.orientation) * horizontalScale * speed * dt;
        state.y += std::sin(state.orientation) * horizontalScale * speed * dt;
        state.z += moveZ * speed * dt;
    }
    else if (input.moveFlags & MOVEFLAG_BACKWARD)
    {
        speed = input.swimBackSpeed;
        state.x -= std::cos(state.orientation) * horizontalScale * speed * dt;
        state.y -= std::sin(state.orientation) * horizontalScale * speed * dt;
        state.z -= moveZ * speed * dt;
    }

    state.vz = 0;  // No gravity while swimming
}

PhysicsOutput PhysicsEngine::Step(const PhysicsInput& input, float dt)
{
    PhysicsOutput output = {};

    // Passthrough if not initialized
    if (!m_initialized)
    {
        output.x = input.x;
        output.y = input.y;
        output.z = input.z;
        output.orientation = input.orientation;
        output.pitch = input.pitch;
        output.vx = input.vx;
        output.vy = input.vy;
        output.vz = input.vz;
        output.moveFlags = input.moveFlags;
        return output;
    }

    // Initialize state
    MovementState state{};
    state.x = input.x;
    state.y = input.y;
    state.z = input.z;
    state.orientation = input.orientation;
    state.pitch = input.pitch;
    state.vx = input.vx;
    state.vy = input.vy;
    state.vz = input.vz;
    state.fallTime = input.fallTime;

    std::cout << "\n========== Physics Step ==========" << std::endl;
    std::cout << "Input pos: (" << state.x << ", " << state.y << ", " << state.z << ")" << std::endl;

    // Get environment info using the new unified surface finding
    WalkableSurface currentSurface = FindWalkableSurface(
        input.mapId, state.x, state.y, state.z,
        STEP_HEIGHT,  // Look for surfaces we could step up to
        STEP_HEIGHT);  // And surfaces we could be standing on

    uint32_t liquidType = 0;
    float liquidLevel = GetLiquidHeight(input.mapId, state.x, state.y, state.z, liquidType);

    // Determine movement state
    float distToGround = INVALID_HEIGHT;
    if (currentSurface.found)
    {
        distToGround = state.z - currentSurface.height;
        std::cout << "Current surface at: " << currentSurface.height
            << " Distance: " << distToGround << std::endl;
    }

    // Improved ground detection with tolerance
    state.isGrounded = currentSurface.found &&
        distToGround >= -GROUND_HEIGHT_TOLERANCE * 2.0f &&  // More lenient below
        distToGround <= STEP_HEIGHT;

    std::cout << "Is grounded: " << (state.isGrounded ? "YES" : "NO") << std::endl;

    // Check swimming
    bool inWater = false;
    if (liquidLevel > INVALID_HEIGHT)
    {
        float playerHeight = input.height > 0 ? input.height : 2.0f;
        float swimmingThreshold = liquidLevel - playerHeight * 0.75f;
        inWater = state.z < swimmingThreshold;
        std::cout << "Liquid level: " << liquidLevel << " In water: " << (inWater ? "YES" : "NO") << std::endl;
    }

    state.isSwimming = inWater && !state.isGrounded;

    // Process movement
    if (state.isSwimming)
    {
        std::cout << "Processing swim movement" << std::endl;
        ProcessSwimMovement(input, state, dt);
    }
    else if (state.isGrounded)
    {
        std::cout << "Processing ground movement" << std::endl;
        ProcessGroundMovement(input, state, dt);
    }
    else
    {
        std::cout << "Processing air movement" << std::endl;
        ProcessAirMovement(input, state, dt);
    }

    // Apply knockback if present
    if (std::abs(input.vx) > 0.01f || std::abs(input.vy) > 0.01f)
    {
        std::cout << "Applying knockback: vx=" << input.vx << " vy=" << input.vy << std::endl;
        state.x += input.vx * dt;
        state.y += input.vy * dt;

        if (!state.isGrounded && std::abs(input.vz) > 0.01f)
            state.vz += input.vz;
    }

    // Clamp height
    state.z = std::max(-MAX_HEIGHT, std::min(MAX_HEIGHT, state.z));

    // Prepare output
    output.x = state.x;
    output.y = state.y;
    output.z = state.z;
    output.orientation = state.orientation;
    output.pitch = state.pitch;
    output.vx = (std::abs(input.vx) > 0.01f) ? input.vx : 0;
    output.vy = (std::abs(input.vy) > 0.01f) ? input.vy : 0;
    output.vz = (state.isGrounded || state.isSwimming) ? 0 : state.vz;
    output.fallTime = state.isSwimming ? 0 : state.fallTime;
    output.moveFlags = input.moveFlags;

    // Update flags
    if (state.isSwimming)
        output.moveFlags |= MOVEFLAG_SWIMMING;
    else
        output.moveFlags &= ~MOVEFLAG_SWIMMING;

    if (state.isGrounded)
    {
        output.moveFlags &= ~MOVEFLAG_JUMPING;
        output.moveFlags &= ~MOVEFLAG_FALLINGFAR;
    }
    else if (!state.isSwimming && state.vz < 0)
    {
        output.moveFlags |= MOVEFLAG_FALLINGFAR;
    }

    std::cout << "Output pos: (" << output.x << ", " << output.y << ", " << output.z << ")" << std::endl;
    std::cout << "==================================\n" << std::endl;

    return output;
}