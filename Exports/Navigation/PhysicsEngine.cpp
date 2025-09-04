// PhysicsEngine.cpp - Stateless physics engine with cylinder collision support
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
using namespace VMAP;

PhysicsEngine* PhysicsEngine::s_instance = nullptr;

PhysicsEngine::PhysicsEngine()
    : m_vmapManager(nullptr), 
    m_initialized(false)
{
}

PhysicsEngine::~PhysicsEngine()
{
    Shutdown();
}

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

void PhysicsEngine::Initialize()
{
    if (m_initialized)
        return;

    std::cout << "[PhysicsEngine] Initializing with cylinder collision support..." << std::endl;

    // Initialize MapLoader for terrain data
    m_mapLoader = std::make_unique<MapLoader>();
    std::vector<std::string> mapPaths = { "maps/", "Data/maps/", "../Data/maps/" };

    auto pathIt = mapPaths.begin();
    while (pathIt != mapPaths.end())
    {
        if (std::filesystem::exists(*pathIt))
        {
            if (m_mapLoader->Initialize(*pathIt))
            {
                std::cout << "[PhysicsEngine] MapLoader initialized with path: " << *pathIt << std::endl;
                break;
            }
        }
        ++pathIt;
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
            auto vmapPathIt = vmapPaths.begin();
            while (vmapPathIt != vmapPaths.end())
            {
                if (std::filesystem::exists(*vmapPathIt))
                {
                    m_vmapManager->setBasePath(*vmapPathIt);
                    std::cout << "[PhysicsEngine] VMapManager initialized with path: " << *vmapPathIt << std::endl;
                    break;
                }
                ++vmapPathIt;
            }
        }
    }
    catch (...)
    {
        std::cout << "[PhysicsEngine] Failed to initialize VMapManager" << std::endl;
        m_vmapManager = nullptr;
    }

    m_initialized = true;
    std::cout << "[PhysicsEngine] Initialization complete" << std::endl;
}

void PhysicsEngine::Shutdown()
{
    std::cout << "[PhysicsEngine] Shutting down..." << std::endl;
    m_vmapManager = nullptr;
    m_mapLoader.reset();
    m_initialized = false;
}

void PhysicsEngine::EnsureMapLoaded(uint32_t mapId)
{
    if (m_vmapManager)
    {
        if (!m_vmapManager->isMapInitialized(mapId))
        {
            std::cout << "[PhysicsEngine] Initializing map " << mapId << std::endl;
            m_vmapManager->initializeMap(mapId);
        }
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

Cylinder PhysicsEngine::CreatePlayerCylinder(float x, float y, float z, float radius, float height) const
{
    return Cylinder(G3D::Vector3(x, y, z), G3D::Vector3(0, 0, 1), radius, height);
}

PhysicsEngine::WalkableSurface PhysicsEngine::FindWalkableSurfaceWithCylinder(
    uint32_t mapId, float x, float y, float currentZ,
    float maxStepUp, float maxStepDown, float cylinderRadius, float cylinderHeight)
{
    WalkableSurface result;
    result.found = false;
    result.height = INVALID_HEIGHT;
    result.source = SurfaceSource::NONE;
    result.normal = G3D::Vector3(0, 0, 1);

    std::cout << "\n[FindWalkableSurfaceWithCylinder] pos(" << x << "," << y << ") currentZ:"
        << currentZ << " stepUp:" << maxStepUp << " stepDown:" << maxStepDown
        << " radius:" << cylinderRadius << " height:" << cylinderHeight << std::endl;

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

    // Collect ALL valid surfaces within range
    struct SurfaceCandidate
    {
        float height;
        SurfaceSource source;
        float priority;
    };
    std::vector<SurfaceCandidate> candidates;

    // Check terrain height
    float terrainZ = GetTerrainHeight(mapId, x, y);
    if (terrainZ > INVALID_HEIGHT)
    {
        float terrainDiff = terrainZ - currentZ;
        std::cout << "  Terrain height: " << terrainZ << " (diff: " << terrainDiff << ")" << std::endl;

        if (terrainDiff >= -(maxStepDown + GROUND_HEIGHT_TOLERANCE) &&
            terrainDiff <= (maxStepUp + GROUND_HEIGHT_TOLERANCE))
        {
            SurfaceCandidate candidate;
            candidate.height = terrainZ;
            candidate.source = SurfaceSource::TERRAIN;

            // Calculate priority based on movement preference
            if (terrainDiff > 0.1f && terrainDiff <= maxStepUp)
                candidate.priority = 1.0f;
            else if (std::abs(terrainDiff) <= 0.1f)
                candidate.priority = 2.0f;
            else if (terrainDiff < -0.1f && terrainDiff >= -maxStepDown)
                candidate.priority = 3.0f;
            else
                candidate.priority = 4.0f;

            candidates.push_back(candidate);
        }
    }

    // Use cylinder collision for VMAP surfaces
    if (m_vmapManager)
    {
        // Search multiple heights to find ALL surfaces
        std::vector<float> foundHeights;

        // Search from above current position
        float searchOffset = -maxStepDown;
        while (searchOffset <= maxStepUp)
        {
            float searchZ = currentZ + searchOffset + maxStepUp;
            float cylHeight = m_vmapManager->GetCylinderHeight(
                mapId, x, y, searchZ,
                cylinderRadius, cylinderHeight, 5.0f);

            if (cylHeight > INVALID_HEIGHT)
            {
                // Check if we already have this height
                bool isDuplicate = false;
                auto heightIt = foundHeights.begin();
                while (heightIt != foundHeights.end())
                {
                    if (std::abs(*heightIt - cylHeight) < 0.05f)
                    {
                        isDuplicate = true;
                        break;
                    }
                    ++heightIt;
                }

                if (!isDuplicate)
                {
                    foundHeights.push_back(cylHeight);
                    float heightDiff = cylHeight - currentZ;
                    std::cout << "  Found VMAP surface at " << cylHeight
                        << " (diff: " << heightDiff << ")" << std::endl;

                    // Check if within step range
                    if (heightDiff >= -(maxStepDown + GROUND_HEIGHT_TOLERANCE) &&
                        heightDiff <= (maxStepUp + GROUND_HEIGHT_TOLERANCE))
                    {
                        SurfaceCandidate candidate;
                        candidate.height = cylHeight;
                        candidate.source = SurfaceSource::VMAP;

                        // Calculate priority - prefer stepping up
                        if (heightDiff > 0.1f && heightDiff <= maxStepUp)
                            candidate.priority = 0.5f;
                        else if (std::abs(heightDiff) <= 0.1f)
                            candidate.priority = 1.5f;
                        else if (heightDiff < -0.1f && heightDiff >= -GROUND_HEIGHT_TOLERANCE)
                            candidate.priority = 2.5f;
                        else if (heightDiff < -GROUND_HEIGHT_TOLERANCE && heightDiff >= -maxStepDown)
                            candidate.priority = 3.5f;
                        else
                            candidate.priority = 4.5f;

                        candidates.push_back(candidate);
                    }
                }
            }
            searchOffset += 0.5f;
        }

        // Also try FindCylinderWalkableSurface
        Cylinder testCylinder = CreatePlayerCylinder(x, y, currentZ, cylinderRadius, cylinderHeight);
        float cylinderSurfaceHeight = INVALID_HEIGHT;
        G3D::Vector3 cylinderNormal;

        bool foundCylinderSurface = m_vmapManager->FindCylinderWalkableSurface(
            mapId, testCylinder, currentZ, maxStepUp + 1.0f, maxStepDown + 1.0f,
            cylinderSurfaceHeight, cylinderNormal);

        if (foundCylinderSurface && cylinderSurfaceHeight > INVALID_HEIGHT)
        {
            float heightDiff = cylinderSurfaceHeight - currentZ;
            bool isWalkable = cylinderNormal.z >= 0.65f;

            if (isWalkable &&
                heightDiff >= -(maxStepDown + GROUND_HEIGHT_TOLERANCE) &&
                heightDiff <= (maxStepUp + GROUND_HEIGHT_TOLERANCE))
            {
                // Check for duplicate
                bool isDuplicate = false;
                auto candIt = candidates.begin();
                while (candIt != candidates.end())
                {
                    if (candIt->source == SurfaceSource::CYLINDER &&
                        std::abs(candIt->height - cylinderSurfaceHeight) < 0.05f)
                    {
                        isDuplicate = true;
                        break;
                    }
                    ++candIt;
                }

                if (!isDuplicate)
                {
                    std::cout << "  Found cylinder surface at " << cylinderSurfaceHeight
                        << " (diff: " << heightDiff << ")" << std::endl;

                    SurfaceCandidate candidate;
                    candidate.height = cylinderSurfaceHeight;
                    candidate.source = SurfaceSource::CYLINDER;

                    if (heightDiff > 0.1f && heightDiff <= maxStepUp)
                        candidate.priority = 0.6f;
                    else if (std::abs(heightDiff) <= 0.1f)
                        candidate.priority = 1.6f;
                    else if (heightDiff < -0.1f && heightDiff >= -maxStepDown)
                        candidate.priority = 3.6f;
                    else
                        candidate.priority = 4.6f;

                    candidates.push_back(candidate);
                }
            }
        }
    }

    // Select the best surface based on priority
    if (!candidates.empty())
    {
        std::cout << "  Evaluating " << candidates.size() << " candidate surfaces:" << std::endl;

        // Sort by priority (lower is better)
        std::sort(candidates.begin(), candidates.end(),
            [](const SurfaceCandidate& a, const SurfaceCandidate& b) {
                if (std::abs(a.priority - b.priority) > 0.01f)
                    return a.priority < b.priority;
                return a.height > b.height;
            });

        // Log the candidates
        auto candIt = candidates.begin();
        while (candIt != candidates.end())
        {
            const char* sourceStr = (candIt->source == SurfaceSource::TERRAIN) ? "TERRAIN" :
                (candIt->source == SurfaceSource::VMAP) ? "VMAP" : "CYLINDER";
            std::cout << "    Height: " << candIt->height << " Priority: " << candIt->priority
                << " Source: " << sourceStr << std::endl;
            ++candIt;
        }

        // Select the best candidate
        const auto& best = candidates[0];
        result.found = true;
        result.height = best.height;
        result.source = best.source;

        std::cout << "  Selected best surface at " << result.height
            << " with priority " << best.priority << std::endl;
    }

    if (result.found)
    {
        const char* sourceStr = "UNKNOWN";
        if (result.source == SurfaceSource::TERRAIN) sourceStr = "TERRAIN";
        else if (result.source == SurfaceSource::VMAP) sourceStr = "VMAP";
        else if (result.source == SurfaceSource::CYLINDER) sourceStr = "CYLINDER";

        std::cout << "  Final selection: " << result.height
            << " (source: " << sourceStr << ")" << std::endl;
    }
    else
    {
        std::cout << "  No walkable surface found" << std::endl;
    }

    return result;
}

bool PhysicsEngine::CheckCylinderMovement(uint32_t mapId, const MovementState& currentState,
    float newX, float newY, float& outZ, G3D::Vector3& outNormal,
    float cylinderRadius, float cylinderHeight)
{
    WalkableSurface surface = FindWalkableSurfaceWithCylinder(
        mapId, newX, newY, currentState.z, STEP_HEIGHT, STEP_DOWN_HEIGHT,
        cylinderRadius, cylinderHeight);

    if (surface.found)
    {
        outZ = surface.height;
        outNormal = surface.normal;
        return true;
    }

    return false;
}

bool PhysicsEngine::ValidateCylinderPosition(uint32_t mapId, float x, float y, float z,
    float tolerance, float cylinderRadius, float cylinderHeight)
{
    if (!m_vmapManager)
        return true;

    Cylinder testCylinder = CreatePlayerCylinder(x, y, z, cylinderRadius, cylinderHeight);
    return m_vmapManager->CanCylinderFitAtPosition(mapId, testCylinder, tolerance);
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
    if (state.vz < -54.0f)
        state.vz = -54.0f;
}

void PhysicsEngine::ProcessGroundMovementWithCylinder(const PhysicsInput& input, MovementState& state,
    float dt, float cylinderRadius, float cylinderHeight)
{
    std::cout << "\n=== ProcessGroundMovementWithCylinder ===" << std::endl;
    std::cout << "Current pos: (" << state.x << ", " << state.y << ", " << state.z << ")" << std::endl;

    float speed = CalculateMoveSpeed(input, false);
    std::cout << "Movement speed: " << speed << std::endl;

    // Handle jumping
    if (input.moveFlags & MOVEFLAG_JUMPING)
    {
        std::cout << "Jump initiated!" << std::endl;
        state.vz = JUMP_VELOCITY;
        state.isGrounded = false;
        state.fallTime = 0;
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

    // Find walkable surface at destination
    WalkableSurface destSurface = FindWalkableSurfaceWithCylinder(
        input.mapId, newX, newY, state.z,
        STEP_HEIGHT, STEP_DOWN_HEIGHT, cylinderRadius, cylinderHeight);

    if (destSurface.found)
    {
        float heightDiff = destSurface.height - state.z;
        std::cout << "Found surface at destination: " << destSurface.height
            << " (diff: " << heightDiff << ")" << std::endl;

        // Update position with proper ground snapping
        state.x = newX;
        state.y = newY;
        state.z = destSurface.height;

        std::cout << "Moved to new position at height " << state.z << std::endl;
    }
    else
    {
        // No movement if no valid surface found
        std::cout << "No valid surface at destination - staying in place" << std::endl;
    }

    std::cout << "Final position: (" << state.x << ", " << state.y << ", " << state.z << ")" << std::endl;
    std::cout << "=== End ProcessGroundMovementWithCylinder ===\n" << std::endl;
}

void PhysicsEngine::AttemptSlideMovementWithCylinder(const PhysicsInput& input, MovementState& state,
    float moveX, float moveY, float moveDist, float cylinderRadius, float cylinderHeight)
{
    // Try sliding perpendicular to the movement direction
    float slideX = moveY * moveDist * 0.7f;
    float slideY = -moveX * moveDist * 0.7f;

    std::cout << "Attempting cylinder-based slide movement: (" << slideX << ", " << slideY << ")" << std::endl;

    // Try both slide directions
    int dir = 0;
    while (dir < 2)
    {
        float testX = state.x + (dir == 0 ? slideX : -slideX);
        float testY = state.y + (dir == 0 ? slideY : -slideY);

        std::cout << "Testing slide direction " << dir << ": (" << testX << ", " << testY << ")" << std::endl;

        // Find surface at slide position
        WalkableSurface slideSurface = FindWalkableSurfaceWithCylinder(
            input.mapId, testX, testY, state.z,
            STEP_HEIGHT, STEP_DOWN_HEIGHT, cylinderRadius, cylinderHeight);

        if (slideSurface.found)
        {
            float heightDiff = slideSurface.height - state.z;

            if (std::abs(heightDiff) <= STEP_HEIGHT)
            {
                std::cout << "Cylinder slide successful to height " << slideSurface.height << std::endl;
                state.x = testX;
                state.y = testY;
                state.z = slideSurface.height;
                return;
            }
        }

        ++dir;
    }

    std::cout << "Cylinder slide movement failed - staying in place" << std::endl;
}

void PhysicsEngine::ProcessAirMovement(const PhysicsInput& input, MovementState& state, float dt)
{
    std::cout << "\n=== ProcessAirMovement === Fall time: " << state.fallTime << std::endl;

    state.fallTime += dt;
    ApplyGravity(state, dt);

    // Limited air control
    float speed = CalculateMoveSpeed(input, false) * 0.5f;

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

    // Get cylinder dimensions from input or use defaults
    float cylinderRadius = input.radius;
    float cylinderHeight = input.height;

    // Check for landing using cylinder collision
    WalkableSurface groundSurface = FindWalkableSurfaceWithCylinder(
        input.mapId, state.x, state.y, state.z,
        0.1f,
        DEFAULT_HEIGHT_SEARCH,
        cylinderRadius, cylinderHeight);

    if (state.vz <= 0 && groundSurface.found)
    {
        float distToGround = state.z - groundSurface.height;
        std::cout << "Air: Checking landing - ground at " << groundSurface.height
            << " dist: " << distToGround << std::endl;

        // Land if we're close enough to the ground
        if (distToGround <= GROUND_HEIGHT_TOLERANCE * 2.0f)
        {
            std::cout << "Landing!" << std::endl;
            state.z = groundSurface.height;
            state.vz = 0;
            state.isGrounded = true;
            state.fallTime = 0;
        }
    }
}

void PhysicsEngine::ProcessSwimMovement(const PhysicsInput& input, MovementState& state, float dt)
{
    std::cout << "\n=== ProcessSwimMovement ===" << std::endl;

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

    // Get player dimensions from input or use defaults
    float cylinderRadius = input.radius;
    float cylinderHeight = input.height;

    // Initialize state (fresh for each call - no persistent state)
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

    std::cout << "\n========== Physics Step (Stateless) ==========" << std::endl;
    std::cout << "Input pos: (" << state.x << ", " << state.y << ", " << state.z << ")" << std::endl;
    std::cout << "Player cylinder: radius=" << cylinderRadius << " height=" << cylinderHeight << std::endl;

    // Get environment info using cylinder collision
    WalkableSurface currentSurface = FindWalkableSurfaceWithCylinder(
        input.mapId, state.x, state.y, state.z,
        STEP_HEIGHT,
        STEP_DOWN_HEIGHT,
        cylinderRadius, cylinderHeight);

    uint32_t liquidType = 0;
    float liquidLevel = GetLiquidHeight(input.mapId, state.x, state.y, state.z, liquidType);

    // Determine movement state with improved ground detection
    float distToGround = INVALID_HEIGHT;
    if (currentSurface.found)
    {
        distToGround = state.z - currentSurface.height;
        std::cout << "Current surface at: " << currentSurface.height
            << " Distance: " << distToGround << std::endl;

        // We're grounded if we found a surface within step range
        state.isGrounded = (distToGround >= -GROUND_HEIGHT_TOLERANCE - 1.0f &&
            distToGround <= STEP_HEIGHT);

        // If we found a surface very close below us, snap to it
        if (distToGround > 0 && distToGround < 2.0f)
        {
            std::cout << "Snapping down to surface from distance " << distToGround << std::endl;
            state.z = currentSurface.height;
            state.isGrounded = true;
            distToGround = 0;
        }
    }
    else
    {
        state.isGrounded = false;
        std::cout << "No surface found - not grounded" << std::endl;
    }

    std::cout << "Is grounded: " << (state.isGrounded ? "YES" : "NO") << std::endl;

    // Check swimming
    bool inWater = false;
    if (liquidLevel > INVALID_HEIGHT)
    {
        float swimmingThreshold = liquidLevel - cylinderHeight * 0.75f;
        inWater = state.z < swimmingThreshold;
        std::cout << "Liquid level: " << liquidLevel << " In water: " << (inWater ? "YES" : "NO") << std::endl;
    }

    state.isSwimming = inWater && !state.isGrounded;

    // Process movement with cylinder collision
    if (state.isSwimming)
    {
        std::cout << "Processing swim movement" << std::endl;
        ProcessSwimMovement(input, state, dt);
    }
    else if (state.isGrounded)
    {
        std::cout << "Processing ground movement with cylinder collision" << std::endl;

        // For idle/stationary, ensure we stay properly grounded
        if ((input.moveFlags & (MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD | MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT)) == 0)
        {
            // Not moving - just ensure we're on the ground
            if (currentSurface.found)
            {
                state.z = currentSurface.height;
                std::cout << "Idle - snapped to ground at " << state.z << std::endl;
            }
        }
        else
        {
            ProcessGroundMovementWithCylinder(input, state, dt, cylinderRadius, cylinderHeight);
        }
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
    std::cout << "Movement Flags: " << output.moveFlags << std::endl;
    std::cout << "==========================================\n" << std::endl;

    return output;
}