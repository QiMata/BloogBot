// DllMain.cpp - Refactored to use VMapManager2 directly
#include "Navigation.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "PhysicsEngine.h"
#include "PhysicsBridge.h"
#include "PhysicsGroundSnap.h"
#include "PhysicsShapeHelpers.h"
#include "MapLoader.h"
#include "SceneQuery.h"
#include "SceneCache.h"
#include "DynamicObjectRegistry.h"
#ifndef PHYSICS_DLL_ONLY
#include "DetourPathCorridor.h"
#endif
#include "VMapLog.h"

#include <algorithm>
#include <iostream>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <filesystem>
#include <vector>
#include <cstdio>
#include <csignal>
#include <cstdlib>
#include <cstring>

#if defined(_WIN32)
#define NOMINMAX
#include <windows.h>
#include <crtdbg.h>
#endif

#if !defined(_WIN32)
#ifndef __declspec
#define __declspec(x) __attribute__((visibility("default")))
#endif

// All __try/__except replaced with standard try/catch(...) to fix MSVC C2712.
// With /EHa, catch(...) catches both C++ and SEH exceptions.

#ifndef EXCEPTION_EXECUTE_HANDLER
#define EXCEPTION_EXECUTE_HANDLER 1
#endif

static inline unsigned long GetLastError()
{
    return 0;
}

static inline void OutputDebugStringA(const char*)
{
}
#endif

// Global mutex for ALL Navigation.dll operations.
// The entire C++ layer (PhysicsEngine, VMapManager, dtNavMeshQuery, DynamicObjectRegistry,
// SceneQuery) was designed for single-threaded use (one bot per process).
// PathfindingService runs 10+ bots through one process, so we serialize all operations.
// Each call takes microseconds-to-low-milliseconds, so contention is negligible.
// Recursive because ValidateWalkableSegment calls PhysicsStepV2Inner internally.
static std::recursive_mutex g_navigationMutex;

// CRT invalid parameter handler — logs and continues instead of aborting
static void NavigationInvalidParameterHandler(
    const wchar_t* expression,
    const wchar_t* function,
    const wchar_t* file,
    unsigned int line,
    uintptr_t pReserved)
{
    fprintf(stderr, "[Navigation.dll] CRT invalid parameter in %ls at %ls:%u\n",
            function ? function : L"(unknown)",
            file ? file : L"(unknown)",
            line);
}

// Global instances
static bool g_initialized = false;
static std::mutex g_initMutex;
static std::unique_ptr<MapLoader> g_mapLoader;
static VMAP::VMapManager2* g_vmapManager = nullptr;

static std::string ReadDataRootFromEnvironment()
{
    const char* env = std::getenv("WWOW_DATA_DIR");
    if (!env || !env[0])
        return {};

    std::string dataRoot = env;
    if (!dataRoot.empty() && dataRoot.back() != '/' && dataRoot.back() != '\\')
        dataRoot += '/';
    return dataRoot;
}

static void SetDataRootEnvironment(const std::string& root)
{
#if defined(_WIN32)
    SetEnvironmentVariableA("WWOW_DATA_DIR", root.c_str());
#else
    setenv("WWOW_DATA_DIR", root.c_str(), 1);
#endif
}

void InitializeAllSystems()
{
    std::lock_guard<std::mutex> lock(g_initMutex);

    if (g_initialized)
        return;

    // Each subsystem initializes independently so a failure in one doesn't
    // prevent the others from working.  The previous code had Navigation and
    // Physics in a single try/catch — if Navigation::Initialize() threw,
    // PhysicsEngine::Initialize() was skipped, leaving physics permanently
    // broken (groundZ=0 for every frame, no horizontal movement).

    // --- Data root ---
    std::string dataRoot = ReadDataRootFromEnvironment();

    // --- MapLoader (optional, for terrain data) ---
    try
    {
        g_mapLoader = std::make_unique<MapLoader>();
        std::vector<std::string> mapPaths;
        if (!dataRoot.empty())
            mapPaths.push_back(dataRoot + "maps/");
        mapPaths.push_back("maps/");

        for (const auto& path : mapPaths)
        {
            if (std::filesystem::exists(path))
            {
                if (g_mapLoader->Initialize(path))
                    break;
            }
        }
    }
    catch (...) {}

    // --- VMAP system ---
    try
    {
        std::vector<std::string> vmapPaths;
        if (!dataRoot.empty())
            vmapPaths.push_back(dataRoot + "vmaps/");
        vmapPaths.push_back("vmaps/");

        for (const auto& path : vmapPaths)
        {
            if (std::filesystem::exists(path))
            {
                g_vmapManager = static_cast<VMAP::VMapManager2*>(
                    VMAP::VMapFactory::createOrGetVMapManager());

                if (g_vmapManager)
                {
                    VMAP::VMapFactory::initialize();
                    g_vmapManager->setBasePath(path);
                    DynamicObjectRegistry::Instance()->LoadDisplayIdMapping(path);
                    break;
                }
            }
        }
    }
    catch (...) {}

    // --- Scenes directory ---
    try
    {
        if (SceneQuery::GetScenesDir().empty())
        {
            if (!dataRoot.empty())
                SceneQuery::SetScenesDir(dataRoot + "scenes/");
            else
                SceneQuery::SetScenesDir("scenes/");
        }
    }
    catch (...) {}

#ifndef PHYSICS_DLL_ONLY
    // --- Navigation (pathfinding — not needed in Physics.dll) ---
    try
    {
        Navigation::GetInstance()->Initialize();
    }
    catch (...) {}
#endif

    // --- Physics Engine ---
    try
    {
        PhysicsEngine::Instance()->Initialize();
    }
    catch (...) {}

    g_initialized = true;
}

// ===============================
// ESSENTIAL EXPORTS ONLY
// ===============================

static void PreloadMapInner(uint32_t mapId)
{
    if (!g_initialized)
        InitializeAllSystems();

    try
    {
#ifndef PHYSICS_DLL_ONLY
        auto* navigation = Navigation::GetInstance();
        if (navigation)
        {
            MMAP::MMapFactory::createOrGetMMapManager();
            navigation->GetQueryForMap(mapId);
        }
#endif
        SceneQuery::EnsureMapLoaded(mapId);
    }
    catch (...) {}
}

extern "C" __declspec(dllexport) void PreloadMap(uint32_t mapId)
{
    try
    {
        PreloadMapInner(mapId);
    }
    catch (...)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in PreloadMap\n");
        fprintf(stderr, "[Navigation.dll] SEH exception in PreloadMap (code=0x%08lx)\n",
                0);
    }
}

// Inject scene triangles into the SceneCache for a map.
// Called by the bot client after receiving scene data from SceneDataService.
// The triangles are added to (or replace) the existing SceneCache for this map.
extern "C" __declspec(dllexport) bool InjectSceneTriangles(
    uint32_t mapId,
    float minX, float minY, float maxX, float maxY,
    const SceneCache::InjectedTriangle* triangles,
    int triangleCount)
{
    try
    {
        if (!g_initialized)
            InitializeAllSystems();

        if (!triangles || triangleCount <= 0)
            return false;

        // Build a SceneCache from the injected triangles
        auto* cache = new SceneCache();
        cache->mapId = mapId;
        cache->InjectTriangles(minX, minY, maxX, maxY, triangles, triangleCount);

        // Replace or merge into the existing scene cache
        SceneQuery::SetSceneCache(mapId, cache);
        return true;
    }
    catch (...)
    {
        return false;
    }
}

// Query AABB terrain contacts for a region — used by SceneDataService.
struct ExportedAABBContact
{
    float pointX, pointY, pointZ;
    float normalX, normalY, normalZ;
    float v1X, v1Y, v1Z;
    float v2X, v2Y, v2Z;
    int walkable;
    uint32_t instanceId;
};

extern "C" __declspec(dllexport) int QueryTerrainAABBTriangles(
    uint32_t mapId,
    float minX, float minY, float minZ,
    float maxX, float maxY, float maxZ,
    ExportedAABBContact* outContacts,
    int maxContacts)
{
    try
    {
        if (!g_initialized)
            InitializeAllSystems();

        SceneQuery::EnsureMapLoaded(mapId);

        G3D::Vector3 boxMin(minX, minY, minZ);
        G3D::Vector3 boxMax(maxX, maxY, maxZ);
        std::vector<SceneQuery::AABBContact> contacts;
        SceneQuery::TestTerrainAABB(mapId, boxMin, boxMax, contacts);

        int count = std::min(static_cast<int>(contacts.size()), maxContacts);
        for (int i = 0; i < count; ++i)
        {
            const auto& c = contacts[i];
            outContacts[i].pointX = c.point.x;
            outContacts[i].pointY = c.point.y;
            outContacts[i].pointZ = c.point.z;
            outContacts[i].normalX = c.normal.x;
            outContacts[i].normalY = c.normal.y;
            outContacts[i].normalZ = c.normal.z;
            // Store full triangle vertices
            outContacts[i].v1X = c.triangleB.x;
            outContacts[i].v1Y = c.triangleB.y;
            outContacts[i].v1Z = c.triangleB.z;
            outContacts[i].v2X = c.triangleC.x;
            outContacts[i].v2Y = c.triangleC.y;
            outContacts[i].v2Z = c.triangleC.z;
            outContacts[i].walkable = c.walkable ? 1 : 0;
            outContacts[i].instanceId = c.instanceId;
        }

        return count;
    }
    catch (...)
    {
        return 0;
    }
}

extern "C" __declspec(dllexport) void ClearSceneCache(uint32_t mapId)
{
    try
    {
        SceneQuery::ClearSceneCache(mapId);
    }
    catch (...) {}
}

// No-op: kept as exported symbol for backward compat with test P/Invoke declarations.
// BG bots now load Physics.dll (PHYSICS_DLL_ONLY) which strips mmaps/VMAPs.
extern "C" __declspec(dllexport) void SetSceneSliceMode(bool) {}

extern "C" __declspec(dllexport) void SetSceneAutoloadEnabled(bool enabled)
{
    try
    {
        SceneQuery::SetSceneAutoloadEnabled(enabled);
    }
    catch (...) {}
}

// Set the data directory for all subsystems (MapLoader, VMapManager, SceneQuery).
// Must be called before PreloadMap. Used by SceneDataService and in-process bot
// physics to configure the data root when WWOW_DATA_DIR may not be set.
extern "C" __declspec(dllexport) void SetDataDirectory(const char* dataDir)
{
    try
    {
        if (!dataDir)
            return;

        std::string root(dataDir);
        if (!root.empty() && root.back() != '/' && root.back() != '\\')
            root += '/';

        // Set the native environment variable so InitializeAllSystems picks it up
        SetDataRootEnvironment(root);

        // If already initialized, update SceneQuery scenes dir directly
        SceneQuery::SetScenesDir(root + "scenes/");

        // Update VMapManager base path if already created
        if (g_vmapManager)
        {
            std::string vmapPath = root + "vmaps/";
            if (std::filesystem::exists(vmapPath))
                g_vmapManager->setBasePath(vmapPath);
        }

        // Update MapLoader if already created
        if (g_mapLoader)
        {
            std::string mapPath = root + "maps/";
            if (std::filesystem::exists(mapPath))
                g_mapLoader->Initialize(mapPath);
        }
    }
    catch (...)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in SetDataDirectory\n");
    }
}

#ifndef PHYSICS_DLL_ONLY
extern "C" __declspec(dllexport) XYZ* FindPath(uint32_t mapId, XYZ start, XYZ end, bool smoothPath, int* length)
{
    try
    {
        if (!g_initialized)
            InitializeAllSystems();

        std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

        auto* navigation = Navigation::GetInstance();
        if (navigation)
            return navigation->CalculatePath(mapId, start, end, smoothPath, length);

        if (length)
            *length = 0;
        return nullptr;
    }
    catch (...)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in FindPath\n");
        fprintf(stderr, "[Navigation.dll] SEH exception in FindPath (code=0x%08lx)\n",
                0);

        if (length)
            *length = 0;
        return nullptr;
    }
}

extern "C" __declspec(dllexport) void PathArrFree(XYZ* pathArr)
{
    delete[] pathArr;
}
#endif // PHYSICS_DLL_ONLY

// Removed legacy PhysicsStep export. Use PhysicsStepV2 only.

static PhysicsOutput MakePassthroughOutput(const PhysicsInput& input)
{
    PhysicsOutput output = {};
    output.x = input.x;
    output.y = input.y;
    output.z = input.z;
    output.orientation = input.orientation;
    output.pitch = input.pitch;
    output.vx = input.vx;
    output.vy = input.vy;
    output.vz = input.vz;
    output.moveFlags = input.moveFlags;
    output.groundZ = -100000.0f;
    output.liquidZ = -100000.0f;
    output.liquidType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
    return output;
}

static PhysicsOutput PhysicsStepV2Inner(const PhysicsInput& input)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    if (auto* physics = PhysicsEngine::Instance())
        return physics->StepV2(input, input.deltaTime);

    return MakePassthroughOutput(input);
}

extern "C" __declspec(dllexport) PhysicsOutput PhysicsStepV2(const PhysicsInput& input)
{
    try
    {
        return PhysicsStepV2Inner(input);
    }
    catch (...)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in PhysicsStepV2\n");
        fprintf(stderr, "[Navigation.dll] SEH exception in PhysicsStepV2 (code=0x%08lx)\n",
                0);
        return MakePassthroughOutput(input);
    }
}

extern "C" __declspec(dllexport) void SetPhysicsLogLevel(int level, uint32_t mask)
{
    gPhysLogLevel = level;
    if (mask != 0)
        gPhysLogMask = mask;
    fprintf(stdout, "[Navigation.dll] SetPhysicsLogLevel: level=%d mask=0x%x\n", gPhysLogLevel, gPhysLogMask);
    fflush(stdout);
}

extern "C" __declspec(dllexport) bool LineOfSight(uint32_t mapId, XYZ from, XYZ to)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    // Delegate to SceneQuery implementation
    return SceneQuery::LineOfSight(mapId, G3D::Vector3(from.X, from.Y, from.Z), G3D::Vector3(to.X, to.Y, to.Z));
}

extern "C" __declspec(dllexport) float GetGroundZ(uint32_t mapId, float x, float y, float z, float maxSearchDist)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    return SceneQuery::GetGroundZ(mapId, x, y, z, maxSearchDist);
}

// Möller–Trumbore segment-triangle intersection.
// Returns true if segment (p0→p1) intersects triangle (a, b, c).
static bool SegmentIntersectsTriangle(
    const G3D::Vector3& p0, const G3D::Vector3& p1,
    const G3D::Vector3& ta, const G3D::Vector3& tb, const G3D::Vector3& tc)
{
    const float eps = 1e-8f;
    const G3D::Vector3 dir = p1 - p0;
    const G3D::Vector3 e1  = tb - ta;
    const G3D::Vector3 e2  = tc - ta;
    const G3D::Vector3 h   = dir.cross(e2);
    const float a = e1.dot(h);
    if (fabsf(a) < eps) return false;           // segment parallel to triangle
    const float f = 1.0f / a;
    const G3D::Vector3 s = p0 - ta;
    const float u = f * s.dot(h);
    if (u < 0.0f || u > 1.0f) return false;
    const G3D::Vector3 q = s.cross(e1);
    const float v = f * dir.dot(q);
    if (v < 0.0f || u + v > 1.0f) return false;
    const float t = f * e2.dot(q);
    return t >= 0.0f && t <= 1.0f;             // hit within segment
}

enum class SegmentValidationCode : uint32_t
{
    Clear = 0,
    BlockedGeometry = 1,
    MissingSupport = 2,
    StepUpTooHigh = 3,
    StepDownTooFar = 4,
};

enum class SegmentAffordanceCode : uint32_t
{
    Walk = 0,
    StepUp = 1,
    SteepClimb = 2,
    Drop = 3,
    Cliff = 4,
    Vertical = 5,
    JumpGap = 6,
    SafeDrop = 7,
    UnsafeDrop = 8,
    Blocked = 9,
};

static float Clamp01(float value)
{
    return std::max(0.0f, std::min(1.0f, value));
}

static float SegmentSlopeAngleDeg(float horizontalDistance, float verticalDistance)
{
    const float absVertical = std::fabs(verticalDistance);
    if (horizontalDistance > 0.01f)
        return std::atan2(absVertical, horizontalDistance) * (180.0f / 3.14159265358979323846f);

    return absVertical > 0.5f ? 90.0f : 0.0f;
}

static float EstimateRunJumpDistance(float landingDeltaZ)
{
    const float discriminant =
        (PhysicsConstants::JUMP_VELOCITY * PhysicsConstants::JUMP_VELOCITY) -
        (PhysicsConstants::DOUBLE_GRAVITY * landingDeltaZ);

    if (discriminant < 0.0f)
        return 0.0f;

    const float flightTime =
        (PhysicsConstants::JUMP_VELOCITY + std::sqrt(discriminant)) * PhysicsConstants::INV_GRAVITY;
    return std::max(0.0f, flightTime * PhysicsConstants::BASE_RUN_SPEED);
}

static float ProbeEndSupportZ(uint32_t mapId, XYZ end, float radius)
{
    const float queryBaseZ = end.Z + PhysicsConstants::STEP_HEIGHT + 0.5f;
    const float queryDistance = PhysicsConstants::FALL_SAFE_DISTANCE + PhysicsConstants::STEP_HEIGHT + 2.0f;
    return SceneQuery::GetCapsuleSupportZ(
        mapId,
        end.X,
        end.Y,
        end.Z,
        queryBaseZ,
        queryDistance,
        radius);
}

static bool HasBlockingCapsuleOverlap(
    uint32_t mapId,
    float x,
    float y,
    float z,
    float radius,
    float height,
    float orientation)
{
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(x, y, z, radius, height);
    std::vector<SceneHit> overlaps;
    G3D::Vector3 playerFwd(std::cos(orientation), std::sin(orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, cap, G3D::Vector3(0, 0, 0), 0.0f, overlaps, playerFwd);

    for (const auto& hit : overlaps)
    {
        if (!hit.hit || !hit.startPenetrating)
            continue;

        const float penetrationDepth = std::max(0.0f, hit.penetrationDepth);
        const float maxAllowedPenDepth = std::max(0.05f, radius * 0.75f);
        if (penetrationDepth <= maxAllowedPenDepth)
            continue;

        if (std::fabs(hit.normal.z) >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z)
            continue;

        return true;
    }

    return false;
}

static bool ShouldAcceptNearCompleteSegment(float horizontalDistance, float completedFraction, float radius = 0.6f)
{
    if (horizontalDistance <= 0.01f)
        return false;

    const float clampedFraction = std::max(0.0f, std::min(1.0f, completedFraction));
    const float remainingDistance = horizontalDistance * (1.0f - clampedFraction);
    // Accept if remaining distance is trivially small — within 2x capsule radius
    // or under 0.5m absolute. No length restriction: near-complete is near-complete.
    const float threshold = std::max(0.5f, radius * 2.0f);
    return remainingDistance <= threshold;
}

static SegmentValidationCode FinalizeSimulatedSegment(
    uint32_t mapId,
    XYZ start,
    float x,
    float y,
    float currentZ,
    float orientation,
    float radius,
    float height,
    float horizontalDistance,
    float completedFraction,
    float* resolvedEndZ,
    float* supportDelta,
    float* travelFraction)
{
    const float queryDistance = PhysicsConstants::STEP_HEIGHT + PhysicsConstants::STEP_DOWN_HEIGHT + 0.5f;
    const float queryBaseZ = currentZ + PhysicsConstants::STEP_HEIGHT + 0.5f;
    const float supportZ = SceneQuery::GetCapsuleSupportZ(
        mapId,
        x,
        y,
        currentZ,
        queryBaseZ,
        queryDistance,
        radius);
    if (supportZ <= PhysicsConstants::INVALID_HEIGHT + 1.0f)
        return SegmentValidationCode::MissingSupport;

    const float deltaZ = supportZ - currentZ;
    if (deltaZ > PhysicsConstants::STEP_HEIGHT + 0.3f)
        return SegmentValidationCode::StepUpTooHigh;

    if (deltaZ < -PhysicsConstants::STEP_DOWN_HEIGHT - 0.5f)
        return SegmentValidationCode::StepDownTooFar;

    if (resolvedEndZ)
        *resolvedEndZ = supportZ;

    if (supportDelta)
        *supportDelta = supportZ - start.Z;

    if (travelFraction)
        *travelFraction = horizontalDistance > 0.01f
            ? std::max(0.0f, std::min(1.0f, completedFraction))
            : 1.0f;

    if (HasBlockingCapsuleOverlap(mapId, x, y, supportZ, radius, height, orientation))
    {
        if (ShouldAcceptNearCompleteSegment(horizontalDistance, completedFraction, radius))
            return SegmentValidationCode::Clear;

        return SegmentValidationCode::BlockedGeometry;
    }

    return SegmentValidationCode::Clear;
}

static SegmentValidationCode TryValidateWalkableSegmentWithPhysics(
    uint32_t mapId,
    XYZ start,
    XYZ end,
    float radius,
    float height,
    float horizontalDistance,
    float* resolvedEndZ,
    float* supportDelta,
    float* travelFraction)
{
    if (horizontalDistance <= 0.05f)
        return SegmentValidationCode::BlockedGeometry;

    constexpr float walkSpeed = 2.5f;
    constexpr float runSpeed = 7.0f;
    constexpr float runBackSpeed = 4.5f;
    constexpr float swimSpeed = 4.72f;
    constexpr float swimBackSpeed = 2.5f;
    constexpr float flightSpeed = 7.0f;
    constexpr float turnSpeed = 3.14159265f;
    constexpr float baseDt = 0.05f;
    constexpr float arrivalTolerance = 0.15f;
    constexpr int maxSteps = 96;

    PhysicsInput input{};
    input.moveFlags = MOVEFLAG_FORWARD;
    input.x = start.X;
    input.y = start.Y;
    input.z = start.Z;
    input.orientation = std::atan2(end.Y - start.Y, end.X - start.X);
    input.pitch = 0.0f;
    input.vx = 0.0f;
    input.vy = 0.0f;
    input.vz = 0.0f;
    input.walkSpeed = walkSpeed;
    input.runSpeed = runSpeed;
    input.runBackSpeed = runBackSpeed;
    input.swimSpeed = swimSpeed;
    input.swimBackSpeed = swimBackSpeed;
    input.flightSpeed = flightSpeed;
    input.turnSpeed = turnSpeed;
    input.transportGuid = 0;
    input.fallTime = 0;
    input.fallStartZ = PhysicsConstants::INVALID_HEIGHT;
    input.height = height;
    input.radius = radius;
    input.hasSplinePath = false;
    input.splineSpeed = 0.0f;
    input.splinePoints = nullptr;
    input.splinePointCount = 0;
    input.currentSplineIndex = 0;
    input.prevGroundZ = start.Z;
    input.prevGroundNx = 0.0f;
    input.prevGroundNy = 0.0f;
    input.prevGroundNz = 1.0f;
    input.pendingDepenX = 0.0f;
    input.pendingDepenY = 0.0f;
    input.pendingDepenZ = 0.0f;
    input.standingOnInstanceId = 0;
    input.standingOnLocalX = 0.0f;
    input.standingOnLocalY = 0.0f;
    input.standingOnLocalZ = 0.0f;
    input.nearbyObjects = nullptr;
    input.nearbyObjectCount = 0;
    input.mapId = mapId;
    input.deltaTime = baseDt;
    input.frameCounter = 0;
    input.physicsFlags = 0;
    input.stepUpBaseZ = PhysicsConstants::INVALID_HEIGHT;
    input.stepUpAge = 0;

    float bestRemaining = horizontalDistance;
    float bestX = start.X;
    float bestY = start.Y;
    float bestZ = start.Z;
    int stalledSteps = 0;

    for (int step = 0; step < maxSteps; ++step)
    {
        const float remainingX = end.X - input.x;
        const float remainingY = end.Y - input.y;
        const float remaining = std::sqrt((remainingX * remainingX) + (remainingY * remainingY));
        if (remaining <= arrivalTolerance)
        {
            const float completedFraction = 1.0f - (remaining / horizontalDistance);
            return FinalizeSimulatedSegment(
                mapId,
                start,
                input.x,
                input.y,
                input.z,
                input.orientation,
                radius,
                height,
                horizontalDistance,
                completedFraction,
                resolvedEndZ,
                supportDelta,
                travelFraction);
        }

        input.orientation = std::atan2(remainingY, remainingX);
        input.deltaTime = std::max(0.016f, std::min(baseDt, remaining / runSpeed));
        input.frameCounter = static_cast<uint32_t>(step + 1);
        input.moveFlags = MOVEFLAG_FORWARD;
        input.vx = 0.0f;
        input.vy = 0.0f;

        const PhysicsOutput output = PhysicsStepV2Inner(input);
        if (!std::isfinite(output.x) || !std::isfinite(output.y) || !std::isfinite(output.z))
            return SegmentValidationCode::BlockedGeometry;

        const float nextRemainingX = end.X - output.x;
        const float nextRemainingY = end.Y - output.y;
        const float nextRemaining = std::sqrt((nextRemainingX * nextRemainingX) + (nextRemainingY * nextRemainingY));
        const float progress = remaining - nextRemaining;

        if (nextRemaining < bestRemaining)
        {
            bestRemaining = nextRemaining;
            bestX = output.x;
            bestY = output.y;
            bestZ = output.z;
        }

        if (nextRemaining <= arrivalTolerance)
        {
            const float completedFraction = 1.0f - (nextRemaining / horizontalDistance);
            return FinalizeSimulatedSegment(
                mapId,
                start,
                output.x,
                output.y,
                output.z,
                input.orientation,
                radius,
                height,
                horizontalDistance,
                completedFraction,
                resolvedEndZ,
                supportDelta,
                travelFraction);
        }

        if (progress > 0.01f)
            stalledSteps = 0;
        else
            ++stalledSteps;

        if ((output.moveFlags & MOVEFLAG_FALLINGFAR) != 0 &&
            (start.Z - output.z) > PhysicsConstants::STEP_DOWN_HEIGHT + 0.25f)
        {
            if (resolvedEndZ)
                *resolvedEndZ = output.z;

            if (supportDelta)
                *supportDelta = output.z - start.Z;

            if (travelFraction)
                *travelFraction = std::max(0.0f, std::min(1.0f, 1.0f - (nextRemaining / horizontalDistance)));

            return SegmentValidationCode::StepDownTooFar;
        }

        if (stalledSteps >= 6 || (output.hitWall && output.blockedFraction < 0.05f && stalledSteps >= 3))
            break;

        input.x = output.x;
        input.y = output.y;
        input.z = output.z;
        input.orientation = output.orientation;
        input.pitch = output.pitch;
        input.vx = output.vx;
        input.vy = output.vy;
        input.vz = output.vz;
        input.moveFlags = output.moveFlags;
        input.prevGroundZ = output.groundZ;
        input.prevGroundNx = output.groundNx;
        input.prevGroundNy = output.groundNy;
        input.prevGroundNz = output.groundNz;
        input.pendingDepenX = output.pendingDepenX;
        input.pendingDepenY = output.pendingDepenY;
        input.pendingDepenZ = output.pendingDepenZ;
        input.standingOnInstanceId = output.standingOnInstanceId;
        input.standingOnLocalX = output.standingOnLocalX;
        input.standingOnLocalY = output.standingOnLocalY;
        input.standingOnLocalZ = output.standingOnLocalZ;
        input.fallTime = output.fallTime > 0.0f
            ? static_cast<uint32_t>(output.fallTime * 1000.0f)
            : 0u;
        input.fallStartZ = output.fallStartZ;
        input.currentSplineIndex = output.currentSplineIndex;
        input.stepUpBaseZ = output.stepUpBaseZ;
        input.stepUpAge = output.stepUpAge;
    }

    if (resolvedEndZ)
        *resolvedEndZ = bestZ;

    if (supportDelta)
        *supportDelta = bestZ - start.Z;

    if (travelFraction)
        *travelFraction = horizontalDistance > 0.01f
            ? std::max(0.0f, std::min(1.0f, 1.0f - (bestRemaining / horizontalDistance)))
            : 1.0f;

    return SegmentValidationCode::BlockedGeometry;
}

extern "C" __declspec(dllexport) uint32_t ValidateWalkableSegment(
    uint32_t mapId,
    XYZ start,
    XYZ end,
    float radius,
    float height,
    float* resolvedEndZ,
    float* supportDelta,
    float* travelFraction)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    if (resolvedEndZ)
        *resolvedEndZ = start.Z;

    if (supportDelta)
        *supportDelta = 0.0f;

    if (travelFraction)
        *travelFraction = 0.0f;

    const float dx = end.X - start.X;
    const float dy = end.Y - start.Y;
    const float horizontalDistance = std::sqrt((dx * dx) + (dy * dy));
    const float queryDistance = PhysicsConstants::STEP_HEIGHT + PhysicsConstants::STEP_DOWN_HEIGHT + 0.5f;

    float lastSupportZ = start.Z;
    auto validateSupport = [&](float x, float y, float currentZ, float orientation, float traveled) -> SegmentValidationCode
    {
        const float queryBaseZ = currentZ + PhysicsConstants::STEP_HEIGHT + 0.5f;
        const float groundZ = SceneQuery::GetCapsuleSupportZ(
            mapId,
            x,
            y,
            currentZ,
            queryBaseZ,
            queryDistance,
            radius);
        if (groundZ <= PhysicsConstants::INVALID_HEIGHT + 1.0f)
            return SegmentValidationCode::MissingSupport;

        const float deltaZ = groundZ - currentZ;
        if (deltaZ > PhysicsConstants::STEP_HEIGHT + 0.3f)
            return SegmentValidationCode::StepUpTooHigh;

        if (deltaZ < -PhysicsConstants::STEP_DOWN_HEIGHT - 0.5f)
            return SegmentValidationCode::StepDownTooFar;

        lastSupportZ = groundZ;

        if (resolvedEndZ)
            *resolvedEndZ = groundZ;

        if (supportDelta)
            *supportDelta = groundZ - start.Z;

        if (travelFraction)
        {
            const float completedFraction = horizontalDistance > 0.01f
                ? std::max(0.0f, std::min(1.0f, traveled / horizontalDistance))
                : 1.0f;
            *travelFraction = completedFraction;
        }

        if (traveled > 0.05f &&
            HasBlockingCapsuleOverlap(mapId, x, y, groundZ, radius, height, orientation))
        {
            const float completedFraction = horizontalDistance > 0.01f
                ? std::max(0.0f, std::min(1.0f, traveled / horizontalDistance))
                : 1.0f;
            if (ShouldAcceptNearCompleteSegment(horizontalDistance, completedFraction, radius))
                return SegmentValidationCode::Clear;

            return SegmentValidationCode::BlockedGeometry;
        }

        return SegmentValidationCode::Clear;
    };

    if (horizontalDistance <= 0.05f)
    {
        const float orientation = 0.0f;
        if (travelFraction)
            *travelFraction = 1.0f;

        if (HasBlockingCapsuleOverlap(mapId, start.X, start.Y, start.Z, radius, height, orientation))
            return static_cast<uint32_t>(SegmentValidationCode::BlockedGeometry);

        return static_cast<uint32_t>(SegmentValidationCode::Clear);
    }

    const G3D::Vector3 direction(dx / horizontalDistance, dy / horizontalDistance, 0.0f);
    const float orientation = std::atan2(direction.y, direction.x);
    constexpr float chunkSize = 0.25f;
    constexpr float minMeaningfulAdvance = 0.02f;

    float currentX = start.X;
    float currentY = start.Y;
    float currentZ = start.Z;
    float traveled = 0.0f;
    float remaining = horizontalDistance;

    const auto startSupportResult = validateSupport(currentX, currentY, currentZ, orientation, 0.0f);
    if (startSupportResult != SegmentValidationCode::Clear)
    {
        if (startSupportResult == SegmentValidationCode::BlockedGeometry)
        {
            const auto simulatedResult = TryValidateWalkableSegmentWithPhysics(
                mapId,
                start,
                end,
                radius,
                height,
                horizontalDistance,
                resolvedEndZ,
                supportDelta,
                travelFraction);
            return static_cast<uint32_t>(simulatedResult);
        }

        return static_cast<uint32_t>(startSupportResult);
    }

    currentZ = lastSupportZ;

    while (remaining > 0.05f)
    {
        const float requested = std::min(chunkSize, remaining);
        const float advanced = PhysicsGroundSnap::HorizontalSweepAdvance(
            mapId,
            currentX,
            currentY,
            currentZ,
            orientation,
            radius,
            height,
            direction,
            requested);

        if (advanced <= minMeaningfulAdvance)
        {
            const auto simulatedResult = TryValidateWalkableSegmentWithPhysics(
                mapId,
                start,
                end,
                radius,
                height,
                horizontalDistance,
                resolvedEndZ,
                supportDelta,
                travelFraction);
            return static_cast<uint32_t>(simulatedResult);
        }

        currentX += direction.x * advanced;
        currentY += direction.y * advanced;
        traveled += advanced;
        remaining -= advanced;

        const auto supportResult = validateSupport(currentX, currentY, currentZ, orientation, traveled);
        if (supportResult != SegmentValidationCode::Clear)
        {
            if (supportResult == SegmentValidationCode::BlockedGeometry)
            {
                const auto simulatedResult = TryValidateWalkableSegmentWithPhysics(
                    mapId,
                    start,
                    end,
                    radius,
                    height,
                    horizontalDistance,
                    resolvedEndZ,
                    supportDelta,
                    travelFraction);
                return static_cast<uint32_t>(simulatedResult);
            }

            return static_cast<uint32_t>(supportResult);
        }

        currentZ = lastSupportZ;
    }

    return static_cast<uint32_t>(SegmentValidationCode::Clear);
}

extern "C" __declspec(dllexport) uint32_t ClassifyPathSegmentAffordance(
    uint32_t mapId,
    XYZ start,
    XYZ end,
    float radius,
    float height,
    float* climbHeight,
    float* gapDistance,
    float* dropHeight,
    float* slopeAngleDeg,
    float* resolvedEndZ,
    uint32_t* validationCode)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    if (climbHeight)
        *climbHeight = 0.0f;
    if (gapDistance)
        *gapDistance = 0.0f;
    if (dropHeight)
        *dropHeight = 0.0f;
    if (slopeAngleDeg)
        *slopeAngleDeg = 0.0f;
    if (resolvedEndZ)
        *resolvedEndZ = start.Z;
    if (validationCode)
        *validationCode = static_cast<uint32_t>(SegmentValidationCode::BlockedGeometry);

    const float dx = end.X - start.X;
    const float dy = end.Y - start.Y;
    const float horizontalDistance = std::sqrt((dx * dx) + (dy * dy));

    SegmentValidationCode validation = SegmentValidationCode::Clear;
    float effectiveEndZ = end.Z;
    float verticalDelta = end.Z - start.Z;
    float climb = std::max(0.0f, verticalDelta);
    float drop = std::max(0.0f, -verticalDelta);
    float gap = 0.0f;

    const G3D::Vector3 losStart(start.X, start.Y, start.Z + (height * 0.5f));
    const G3D::Vector3 losEnd(end.X, end.Y, end.Z + (height * 0.5f));
    if (!SceneQuery::LineOfSight(mapId, losStart, losEnd))
        validation = SegmentValidationCode::BlockedGeometry;

    const float orientation = horizontalDistance > 0.01f
        ? std::atan2(dy, dx)
        : 0.0f;
    const int sampleCount = horizontalDistance > 0.05f
        ? std::max(1, std::min(8, static_cast<int>(std::ceil(horizontalDistance / 2.0f))))
        : 1;
    float previousSupportZ = start.Z;
    bool havePreviousSupport = false;
    float firstMissingFraction = -1.0f;

    for (int i = 0; i <= sampleCount; ++i)
    {
        const float t = sampleCount > 0
            ? static_cast<float>(i) / static_cast<float>(sampleCount)
            : 1.0f;
        const float sampleX = start.X + (dx * t);
        const float sampleY = start.Y + (dy * t);
        const float sampleZ = start.Z + ((end.Z - start.Z) * t);
        const XYZ sample(sampleX, sampleY, sampleZ);
        const float supportZ = ProbeEndSupportZ(mapId, sample, radius);

        if (supportZ <= PhysicsConstants::INVALID_HEIGHT + 1.0f)
        {
            if (validation == SegmentValidationCode::Clear)
                validation = SegmentValidationCode::MissingSupport;
            if (firstMissingFraction < 0.0f)
                firstMissingFraction = t;
            continue;
        }

        if (i == sampleCount)
            effectiveEndZ = supportZ;

        climb = std::max(climb, std::max(0.0f, supportZ - start.Z));
        drop = std::max(drop, std::max(0.0f, start.Z - supportZ));

        if (havePreviousSupport)
        {
            const float deltaFromPrevious = supportZ - previousSupportZ;
            if (validation == SegmentValidationCode::Clear && deltaFromPrevious > PhysicsConstants::STEP_HEIGHT + 0.3f)
                validation = SegmentValidationCode::StepUpTooHigh;
            else if (validation == SegmentValidationCode::Clear && deltaFromPrevious < -PhysicsConstants::STEP_DOWN_HEIGHT - 0.5f)
                validation = SegmentValidationCode::StepDownTooFar;
        }

        if (i > 0 &&
            validation == SegmentValidationCode::Clear &&
            HasBlockingCapsuleOverlap(mapId, sampleX, sampleY, supportZ, radius, height, orientation))
        {
            validation = SegmentValidationCode::BlockedGeometry;
        }

        previousSupportZ = supportZ;
        havePreviousSupport = true;
    }

    if (validation == SegmentValidationCode::MissingSupport)
    {
        gap = firstMissingFraction >= 0.0f
            ? horizontalDistance * (1.0f - Clamp01(firstMissingFraction))
            : horizontalDistance;
        if (gap < 0.25f)
            gap = horizontalDistance;
    }

    if (validationCode)
        *validationCode = static_cast<uint32_t>(validation);

    verticalDelta = effectiveEndZ - start.Z;
    const float slopeDeg = SegmentSlopeAngleDeg(horizontalDistance, verticalDelta);

    if (climbHeight)
        *climbHeight = climb;
    if (gapDistance)
        *gapDistance = gap;
    if (dropHeight)
        *dropHeight = drop;
    if (slopeAngleDeg)
        *slopeAngleDeg = slopeDeg;
    if (resolvedEndZ)
        *resolvedEndZ = effectiveEndZ;

    switch (validation)
    {
    case SegmentValidationCode::BlockedGeometry:
        return static_cast<uint32_t>(SegmentAffordanceCode::Blocked);

    case SegmentValidationCode::StepUpTooHigh:
        return static_cast<uint32_t>(SegmentAffordanceCode::Blocked);

    case SegmentValidationCode::StepDownTooFar:
        return static_cast<uint32_t>(
            drop > PhysicsConstants::FALL_SAFE_DISTANCE
                ? SegmentAffordanceCode::UnsafeDrop
                : SegmentAffordanceCode::SafeDrop);

    case SegmentValidationCode::MissingSupport:
    {
        if (effectiveEndZ <= PhysicsConstants::INVALID_HEIGHT + 1.0f)
            return static_cast<uint32_t>(SegmentAffordanceCode::Blocked);

        if (drop > PhysicsConstants::FALL_SAFE_DISTANCE)
            return static_cast<uint32_t>(SegmentAffordanceCode::UnsafeDrop);

        const float maxJumpDistance = EstimateRunJumpDistance(verticalDelta);
        if (gap <= maxJumpDistance + radius && climb <= (PhysicsConstants::JUMP_VELOCITY * PhysicsConstants::JUMP_VELOCITY / PhysicsConstants::DOUBLE_GRAVITY) + 0.25f)
            return static_cast<uint32_t>(SegmentAffordanceCode::JumpGap);

        return static_cast<uint32_t>(SegmentAffordanceCode::Blocked);
    }

    case SegmentValidationCode::Clear:
    default:
        break;
    }

    if (horizontalDistance < 0.5f && std::fabs(verticalDelta) > PhysicsConstants::STEP_HEIGHT)
        return static_cast<uint32_t>(SegmentAffordanceCode::Vertical);

    if (drop > PhysicsConstants::FALL_SAFE_DISTANCE)
        return static_cast<uint32_t>(SegmentAffordanceCode::UnsafeDrop);

    if (drop > 2.0f)
        return static_cast<uint32_t>(SegmentAffordanceCode::SafeDrop);

    if (slopeDeg > 45.0f && climb > 0.0f)
        return static_cast<uint32_t>(SegmentAffordanceCode::SteepClimb);

    if (climb > 1.0f || (slopeDeg > 15.0f && climb > 0.0f))
        return static_cast<uint32_t>(SegmentAffordanceCode::StepUp);

    return static_cast<uint32_t>(SegmentAffordanceCode::Walk);
}

// Check whether the line segment (x0,y0,z0)→(x1,y1,z1) intersects any triangle
// belonging to a registered dynamic object on the given map.
// Returns false when no dynamic objects are registered (fast path).
// Used by the pathfinding layer to detect when a freshly-generated path segment
// passes through a closed door or other registered dynamic obstacle.
extern "C" __declspec(dllexport) bool SegmentIntersectsDynamicObjectsDetailed(
    uint32_t mapId,
    float x0, float y0, float z0,
    float x1, float y1, float z1,
    uint32_t* blockingInstanceId,
    uint64_t* blockingGuid,
    uint32_t* blockingDisplayId);

extern "C" __declspec(dllexport) bool SegmentIntersectsDynamicObjects(
    uint32_t mapId,
    float x0, float y0, float z0,
    float x1, float y1, float z1)
{
    return SegmentIntersectsDynamicObjectsDetailed(
        mapId,
        x0, y0, z0,
        x1, y1, z1,
        nullptr,
        nullptr,
        nullptr);
}

extern "C" __declspec(dllexport) bool SegmentIntersectsDynamicObjectsDetailed(
    uint32_t mapId,
    float x0, float y0, float z0,
    float x1, float y1, float z1,
    uint32_t* blockingInstanceId,
    uint64_t* blockingGuid,
    uint32_t* blockingDisplayId)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    if (blockingInstanceId)
        *blockingInstanceId = 0;

    if (blockingGuid)
        *blockingGuid = 0;

    if (blockingDisplayId)
        *blockingDisplayId = 0;

    auto* reg = DynamicObjectRegistry::Instance();
    if (!reg || reg->Count() == 0)
        return false;

    const G3D::Vector3 p0(x0, y0, z0);
    const G3D::Vector3 p1(x1, y1, z1);
    return reg->FindFirstIntersectingObject(
        mapId,
        p0,
        p1,
        blockingInstanceId,
        blockingGuid,
        blockingDisplayId);
}

// ===============================
// SPATIAL QUERIES
// ===============================

#ifndef PHYSICS_DLL_ONLY
// Check if a point is on the navmesh (within searchRadius XZ, 200y vertical).
// Returns true if a walkable polygon is found near the given position.
// nearestX/Y/Z receive the closest point on the navmesh surface.
extern "C" __declspec(dllexport) bool IsPointOnNavmesh(
    uint32_t mapId,
    float x, float y, float z,
    float searchRadius,
    float* nearestX, float* nearestY, float* nearestZ)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    auto* nav = Navigation::GetInstance();
    const dtNavMeshQuery* query = nav->GetQueryForMap(mapId);
    if (!query) return false;

    float pos[3] = { y, z, x };  // WoW→Detour axis swap
    float ext[3] = { searchRadius, 200.0f, searchRadius };
    float nearest[3] = { 0.f, 0.f, 0.f };
    dtPolyRef polyRef = 0;

    dtQueryFilter filter;
    filter.setIncludeFlags(0x01);  // NAV_GROUND
    filter.setExcludeFlags(0);

    dtStatus st = query->findNearestPoly(pos, ext, &filter, &polyRef, nearest);
    if (dtStatusFailed(st) || polyRef == 0)
        return false;

    // Check the 2D distance from query point to nearest — must be within searchRadius
    float dx = nearest[0] - pos[0];
    float dz = nearest[2] - pos[2];
    if (dx * dx + dz * dz > searchRadius * searchRadius)
        return false;

    if (nearestX) *nearestX = nearest[2];  // Detour→WoW axis swap
    if (nearestY) *nearestY = nearest[0];
    if (nearestZ) *nearestZ = nearest[1];
    return true;
}

// Find the nearest walkable point within a search radius.
// Returns the area type of the found polygon (0=none/not found).
// area values match VMaNGOS NavMeshAreas: 1=ground, 2=ground_model,
// 3=steep_slope, 5=water_transition, 6=water, etc.
extern "C" __declspec(dllexport) uint32_t FindNearestWalkablePoint(
    uint32_t mapId,
    float x, float y, float z,
    float searchRadius,
    float* outX, float* outY, float* outZ)
{
    if (!g_initialized)
        InitializeAllSystems();

    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

    auto* nav = Navigation::GetInstance();
    const dtNavMeshQuery* query = nav->GetQueryForMap(mapId);
    if (!query) return 0;

    float pos[3] = { y, z, x };  // WoW→Detour axis swap
    float ext[3] = { searchRadius, 200.0f, searchRadius };
    float nearest[3] = { 0.f, 0.f, 0.f };
    dtPolyRef polyRef = 0;

    dtQueryFilter filter;
    filter.setIncludeFlags(0x01);  // NAV_GROUND
    filter.setExcludeFlags(0);

    dtStatus st = query->findNearestPoly(pos, ext, &filter, &polyRef, nearest);
    if (dtStatusFailed(st) || polyRef == 0)
        return 0;

    float dx = nearest[0] - pos[0];
    float dz = nearest[2] - pos[2];
    if (dx * dx + dz * dz > searchRadius * searchRadius)
        return 0;

    if (outX) *outX = nearest[2];  // Detour→WoW axis swap
    if (outY) *outY = nearest[0];
    if (outZ) *outZ = nearest[1];

    // Get the area type of the polygon
    const dtNavMesh* navMesh = nullptr;
    // Access the mesh from the query — Detour stores it as m_nav
    // Use getAttachedNavMesh helper
    unsigned char area = 0;
    navMesh = query->getAttachedNavMesh();
    if (navMesh)
    {
        const dtMeshTile* tile = nullptr;
        const dtPoly* poly = nullptr;
        if (dtStatusSucceed(navMesh->getTileAndPolyByRef(polyRef, &tile, &poly)))
            area = poly->getArea();
    }

    return static_cast<uint32_t>(area);
}
#endif // PHYSICS_DLL_ONLY (IsPointOnNavmesh + FindNearestWalkablePoint)

// ===============================
#ifndef PHYSICS_DLL_ONLY
// PATH CORRIDOR API
// ===============================
// Incremental, collision-aware path following using Detour's dtPathCorridor.
// Replaces the expensive ValidateWalkableSegment physics sweep pipeline.

static const int CORRIDOR_MAX_PATH = 740;   // matches PathFinder MAX_PATH_LENGTH
static const int CORRIDOR_MAX_CORNERS = 96; // enough for long paths via findStraightPath

struct CorridorInstance
{
    dtPathCorridor corridor;
    uint32_t       mapId;
    dtQueryFilter  filter;
    bool           valid;
};

static uint32_t g_nextCorridorHandle = 1;
static std::unordered_map<uint32_t, CorridorInstance*> g_corridors;

// Result struct returned by FindPathCorridor / CorridorUpdate.
// C# reads this via P/Invoke as a blittable struct.
#pragma pack(push, 1)
struct CorridorResult
{
    uint32_t handle;
    int      cornerCount;
    float    corners[CORRIDOR_MAX_CORNERS * 3]; // [x,y,z, x,y,z, ...]
    float    posX, posY, posZ;                  // corridor-constrained position
    int      blockedSegmentIndex;               // -1 when no dynamic overlay block was observed
    uint32_t blockingInstanceId;
    uint64_t blockingGuid;
    uint32_t blockingDisplayId;
    uint32_t flags;                             // bit 0: returned path was repaired around this block
};
#pragma pack(pop)

static constexpr uint32_t CORRIDOR_RESULT_FLAG_OVERLAY_REPAIRED = 0x00000001u;

static void ResetCorridorResult(CorridorResult& result, uint32_t handle = 0)
{
    result = {};
    result.handle = handle;
    result.blockedSegmentIndex = -1;
}

static dtNavMeshQuery* GetMutableQueryForMap(uint32_t mapId)
{
    // dtPathCorridor needs a non-const dtNavMeshQuery*.
    // Navigation::GetQueryForMap returns const, but the underlying object is mutable.
    return const_cast<dtNavMeshQuery*>(
        Navigation::GetInstance()->GetQueryForMap(mapId));
}

static bool HasActiveDynamicObjectOverlay()
{
    auto* registry = DynamicObjectRegistry::Instance();
    return registry != nullptr && registry->Count() > 0;
}

static void SetCorridorBlockMetadata(
    CorridorResult& result,
    int segmentIndex,
    uint32_t instanceId,
    uint64_t guid,
    uint32_t displayId,
    bool repaired)
{
    if (segmentIndex < 0)
        return;

    result.blockedSegmentIndex = segmentIndex;
    result.blockingInstanceId = instanceId;
    result.blockingGuid = guid;
    result.blockingDisplayId = displayId;
    if (repaired)
        result.flags |= CORRIDOR_RESULT_FLAG_OVERLAY_REPAIRED;
}

static bool TryFindDynamicOverlayBlockInResult(
    uint32_t mapId,
    const XYZ& start,
    const CorridorResult& result,
    int* outSegmentIndex,
    uint32_t* outInstanceId,
    uint64_t* outGuid,
    uint32_t* outDisplayId)
{
    auto* registry = DynamicObjectRegistry::Instance();
    if (!registry || registry->Count() == 0 || result.cornerCount <= 0)
        return false;

    XYZ segmentStart = start;
    for (int i = 0; i < result.cornerCount; ++i)
    {
        const XYZ segmentEnd(
            result.corners[i * 3 + 0],
            result.corners[i * 3 + 1],
            result.corners[i * 3 + 2]);

        uint32_t instanceId = 0;
        uint64_t guid = 0;
        uint32_t displayId = 0;
        if (registry->FindFirstIntersectingObject(
            mapId,
            G3D::Vector3(segmentStart.X, segmentStart.Y, segmentStart.Z),
            G3D::Vector3(segmentEnd.X, segmentEnd.Y, segmentEnd.Z),
            &instanceId,
            &guid,
            &displayId))
        {
            if (outSegmentIndex)
                *outSegmentIndex = i;
            if (outInstanceId)
                *outInstanceId = instanceId;
            if (outGuid)
                *outGuid = guid;
            if (outDisplayId)
                *outDisplayId = displayId;
            return true;
        }

        segmentStart = segmentEnd;
    }

    return false;
}

static uint32_t RegisterPassiveCorridorHandle(uint32_t mapId)
{
    auto* ci = new CorridorInstance();
    ci->mapId = mapId;
    ci->valid = false;

    uint32_t handle = g_nextCorridorHandle++;
    g_corridors[handle] = ci;
    return handle;
}

static void FillResultFromPointPath(
    CorridorResult& result,
    const XYZ& start,
    const XYZ* pathArr,
    int pathLength)
{
    if (!pathArr || pathLength <= 0)
        return;

    constexpr float StartDedupEpsilon = 0.25f;
    int firstCornerIndex = 0;
    if (pathLength > 0)
    {
        const float dx = pathArr[0].X - start.X;
        const float dy = pathArr[0].Y - start.Y;
        const float dz = pathArr[0].Z - start.Z;
        const float distanceSq = (dx * dx) + (dy * dy) + (dz * dz);
        if (distanceSq <= (StartDedupEpsilon * StartDedupEpsilon))
            firstCornerIndex = 1;
    }

    int cornerCount = 0;
    for (int i = firstCornerIndex; i < pathLength && cornerCount < CORRIDOR_MAX_CORNERS; ++i, ++cornerCount)
    {
        result.corners[cornerCount * 3 + 0] = pathArr[i].X;
        result.corners[cornerCount * 3 + 1] = pathArr[i].Y;
        result.corners[cornerCount * 3 + 2] = pathArr[i].Z;
    }

    result.cornerCount = cornerCount;
    result.posX = start.X;
    result.posY = start.Y;
    result.posZ = start.Z;
}

static int FillCorners(CorridorInstance* ci, CorridorResult& result)
{
    dtNavMeshQuery* query = GetMutableQueryForMap(ci->mapId);
    if (!query) return 0;

    unsigned char cornerFlags[CORRIDOR_MAX_CORNERS];
    dtPolyRef     cornerPolys[CORRIDOR_MAX_CORNERS];

    // findCorners writes Detour coords (Y,Z,X) into the buffer
    float detourCorners[CORRIDOR_MAX_CORNERS * 3];
    int n = ci->corridor.findCorners(
        detourCorners, cornerFlags, cornerPolys,
        CORRIDOR_MAX_CORNERS, query, &ci->filter);

    // Convert Detour (Y,Z,X) → WoW (X,Y,Z) for each corner
    for (int i = 0; i < n; i++)
    {
        float dY = detourCorners[i * 3 + 0]; // Detour[0] = WoW Y
        float dZ = detourCorners[i * 3 + 1]; // Detour[1] = WoW Z
        float dX = detourCorners[i * 3 + 2]; // Detour[2] = WoW X
        result.corners[i * 3 + 0] = dX;      // WoW X
        result.corners[i * 3 + 1] = dY;      // WoW Y
        result.corners[i * 3 + 2] = dZ;      // WoW Z
    }

    result.cornerCount = n;

    // Convert position Detour (Y,Z,X) → WoW (X,Y,Z)
    const float* pos = ci->corridor.getPos();
    result.posX = pos[2]; // Detour[2] = WoW X
    result.posY = pos[0]; // Detour[0] = WoW Y
    result.posZ = pos[1]; // Detour[1] = WoW Z

    return n;
}

/// Create a corridor from start to end on the given map.
/// Returns a CorridorResult with a handle (>0) and initial corners.
/// handle==0 means failure.
extern "C" __declspec(dllexport) CorridorResult FindPathCorridor(
    uint32_t mapId, XYZ start, XYZ end)
{
    CorridorResult result = {};
    ResetCorridorResult(result);
    std::unique_ptr<XYZ[]> overlayPath;

    try
    {
        if (!g_initialized)
            InitializeAllSystems();

        auto* navigation = Navigation::GetInstance();
        if (!navigation) { fprintf(stderr, "[CORRIDOR] no Navigation instance\n"); return result; }

        // Hold the corridor mutex for the ENTIRE operation.
        // dtNavMeshQuery is NOT thread-safe — concurrent findPath/findNearestPoly
        // on the same query object corrupts internal state and causes access violations.
        // With 10 bots on the same map, all sharing one query, this is fatal.
        std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

        if (HasActiveDynamicObjectOverlay())
        {
            int overlayPathLength = 0;
            overlayPath.reset(navigation->CalculatePath(mapId, start, end, true, &overlayPathLength));
            if (overlayPath != nullptr && overlayPathLength > 0)
            {
                FillResultFromPointPath(result, start, overlayPath.get(), overlayPathLength);
                const auto overlayBlock = navigation->GetLastOverlayRepairedSegment();
                SetCorridorBlockMetadata(
                    result,
                    overlayBlock.segmentIndex,
                    overlayBlock.blockingInstanceId,
                    overlayBlock.blockingGuid,
                    overlayBlock.blockingDisplayId,
                    true);
                result.handle = RegisterPassiveCorridorHandle(mapId);
                fprintf(stderr, "[CORRIDOR] overlay-aware native path reused smooth point-path: corners=%d handle=%u blockedIdx=%d display=%u guid=0x%llx\n",
                    result.cornerCount, result.handle, result.blockedSegmentIndex, result.blockingDisplayId,
                    (unsigned long long)result.blockingGuid);
                return result;
            }
        }

        dtNavMeshQuery* query = GetMutableQueryForMap(mapId);
        if (!query) { fprintf(stderr, "[CORRIDOR] no query for map %u\n", mapId); return result; }

        // Find start and end poly refs
        dtQueryFilter filter;
        filter.setIncludeFlags(0xFFFF);
        filter.setExcludeFlags(0);

        // WoW coords (X,Y,Z) → Detour coords (Y,Z,X) — MaNGOS mmaps use this convention
        float startPos[3] = { start.Y, start.Z, start.X };
        float endPos[3]   = { end.Y,   end.Z,   end.X   };
        float extents[3]  = { 4.0f, 5.0f, 4.0f };

        dtPolyRef startRef = 0, endRef = 0;
        float nearestStart[3], nearestEnd[3];

        dtStatus st = query->findNearestPoly(startPos, extents, &filter, &startRef, nearestStart);
        if (dtStatusFailed(st) || startRef == 0)
        {
            // Retry with larger extents
            float bigExtents[3] = { 8.0f, 200.0f, 8.0f };
            st = query->findNearestPoly(startPos, bigExtents, &filter, &startRef, nearestStart);
            if (dtStatusFailed(st) || startRef == 0)
            {
                fprintf(stderr, "[CORRIDOR] findNearestPoly failed for START (%.1f,%.1f,%.1f) st=0x%x\n",
                        start.X, start.Y, start.Z, st);
                return result;
            }
        }

        st = query->findNearestPoly(endPos, extents, &filter, &endRef, nearestEnd);
        if (dtStatusFailed(st) || endRef == 0)
        {
            float bigExtents[3] = { 8.0f, 200.0f, 8.0f };
            st = query->findNearestPoly(endPos, bigExtents, &filter, &endRef, nearestEnd);
            if (dtStatusFailed(st) || endRef == 0)
            {
                fprintf(stderr, "[CORRIDOR] findNearestPoly failed for END (%.1f,%.1f,%.1f) st=0x%x\n",
                        end.X, end.Y, end.Z, st);
                return result;
            }
        }

        fprintf(stderr, "[CORRIDOR] startRef=%llu endRef=%llu\n", (unsigned long long)startRef, (unsigned long long)endRef);

        // Find poly path via A*
        dtPolyRef polyPath[CORRIDOR_MAX_PATH];
        int polyCount = 0;
        st = query->findPath(startRef, endRef, nearestStart, nearestEnd,
                             &filter, polyPath, &polyCount, CORRIDOR_MAX_PATH);
        if (dtStatusFailed(st) || polyCount == 0)
        {
            fprintf(stderr, "[CORRIDOR] findPath failed: st=0x%x polyCount=%d\n", st, polyCount);
            return result;
        }
        fprintf(stderr, "[CORRIDOR] findPath OK: polyCount=%d partial=%s\n",
                polyCount, dtStatusDetail(st, DT_PARTIAL_RESULT) ? "yes" : "no");

        // Create corridor instance
        auto* ci = new CorridorInstance();
        ci->mapId = mapId;
        ci->filter = filter;
        ci->valid = true;

        if (!ci->corridor.init(CORRIDOR_MAX_PATH))
        {
            delete ci;
            return result;
        }

        ci->corridor.reset(startRef, nearestStart);
        ci->corridor.setCorridor(nearestEnd, polyPath, polyCount);

        // Use findStraightPath on the full poly path for the initial result.
        // This gives us all the string-pulled corners for the entire route,
        // not just the nearby ones from findCorners.
        float straightPath[CORRIDOR_MAX_CORNERS * 3];
        unsigned char straightFlags[CORRIDOR_MAX_CORNERS];
        dtPolyRef straightPolys[CORRIDOR_MAX_CORNERS];
        int straightCount = 0;

        query->findStraightPath(nearestStart, nearestEnd, polyPath, polyCount,
                                straightPath, straightFlags, straightPolys,
                                &straightCount, CORRIDOR_MAX_CORNERS);

        // Convert Detour (Y,Z,X) → WoW (X,Y,Z) for each corner
        for (int i = 0; i < straightCount; i++)
        {
            float dY = straightPath[i * 3 + 0]; // Detour[0] = WoW Y
            float dZ = straightPath[i * 3 + 1]; // Detour[1] = WoW Z
            float dX = straightPath[i * 3 + 2]; // Detour[2] = WoW X
            result.corners[i * 3 + 0] = dX;
            result.corners[i * 3 + 1] = dY;
            result.corners[i * 3 + 2] = dZ;
        }
        result.cornerCount = straightCount;

        // Position = corridor-snapped start in WoW coords
        result.posX = nearestStart[2]; // Detour[2] = WoW X
        result.posY = nearestStart[0]; // Detour[0] = WoW Y
        result.posZ = nearestStart[1]; // Detour[1] = WoW Z

        // Register corridor for future incremental updates
        // (already under g_navigationMutex from top of function)
        uint32_t handle = g_nextCorridorHandle++;
        g_corridors[handle] = ci;

        result.handle = handle;
        if (HasActiveDynamicObjectOverlay())
        {
            int segmentIndex = -1;
            uint32_t instanceId = 0;
            uint64_t guid = 0;
            uint32_t displayId = 0;
            if (TryFindDynamicOverlayBlockInResult(
                mapId,
                start,
                result,
                &segmentIndex,
                &instanceId,
                &guid,
                &displayId))
            {
                SetCorridorBlockMetadata(result, segmentIndex, instanceId, guid, displayId, false);
            }
        }
        fprintf(stderr, "[CORRIDOR] handle=%u corners=%d pos=(%.1f,%.1f,%.1f)\n",
                handle, straightCount, result.posX, result.posY, result.posZ);
    }
    catch (...)
    {
        fprintf(stderr, "[Navigation.dll] SEH exception in FindPathCorridor (code=0x%08lx)\n",
                0);
    }

    return result;
}

/// Feed the agent's current position into the corridor. The corridor slides
/// the position along the navmesh surface (collision-aware) and returns
/// updated waypoints.
extern "C" __declspec(dllexport) CorridorResult CorridorUpdate(
    uint32_t handle, XYZ agentPos)
{
    CorridorResult result = {};
    ResetCorridorResult(result, handle);

    try
    {
        // Hold the corridor mutex for the ENTIRE operation to prevent:
        // 1. Use-after-free: another thread calling CorridorDestroy while we use ci
        // 2. dtNavMeshQuery corruption: concurrent access to shared query object
        std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

        auto it = g_corridors.find(handle);
        if (it == g_corridors.end()) return result;
        CorridorInstance* ci = it->second;
        if (!ci || !ci->valid) return result;

        dtNavMeshQuery* query = GetMutableQueryForMap(ci->mapId);
        if (!query) return result;

        // WoW coords (X,Y,Z) → Detour coords (Y,Z,X)
        float npos[3] = { agentPos.Y, agentPos.Z, agentPos.X };

        // Move the corridor start to the agent's actual position.
        // movePosition calls moveAlongSurface internally — this is the
        // collision-aware slide that replaces ValidateWalkableSegment.
        ci->corridor.movePosition(npos, query, &ci->filter);

        // Periodically optimize the corridor path.
        // Visibility optimization shortcuts visible corners.
        float cornerBuf[CORRIDOR_MAX_CORNERS * 3];
        unsigned char flagBuf[CORRIDOR_MAX_CORNERS];
        dtPolyRef polyBuf[CORRIDOR_MAX_CORNERS];
        int nc = ci->corridor.findCorners(cornerBuf, flagBuf, polyBuf,
                                          CORRIDOR_MAX_CORNERS, query, &ci->filter);
        if (nc > 0)
            ci->corridor.optimizePathVisibility(&cornerBuf[0], 30.0f, query, &ci->filter);

        // Topology optimization fixes non-optimal corridors from drift.
        ci->corridor.optimizePathTopology(query, &ci->filter);

        FillCorners(ci, result);
    }
    catch (...)
    {
        fprintf(stderr, "[Navigation.dll] SEH exception in CorridorUpdate (code=0x%08lx)\n",
                0);
    }

    return result;
}

/// Move the corridor's target to a new destination (for moving targets).
extern "C" __declspec(dllexport) CorridorResult CorridorMoveTarget(
    uint32_t handle, XYZ newTarget)
{
    CorridorResult result = {};
    ResetCorridorResult(result, handle);

    try
    {
        std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);

        auto it = g_corridors.find(handle);
        if (it == g_corridors.end()) return result;
        CorridorInstance* ci = it->second;
        if (!ci || !ci->valid) return result;

        dtNavMeshQuery* query = GetMutableQueryForMap(ci->mapId);
        if (!query) return result;

        // WoW coords (X,Y,Z) → Detour coords (Y,Z,X)
        float npos[3] = { newTarget.Y, newTarget.Z, newTarget.X };
        ci->corridor.moveTargetPosition(npos, query, &ci->filter);

        FillCorners(ci, result);
    }
    catch (...)
    {
        fprintf(stderr, "[Navigation.dll] exception in CorridorMoveTarget\n");
    }

    return result;
}

/// Check if the corridor is still valid (poly refs haven't been invalidated).
extern "C" __declspec(dllexport) bool CorridorIsValid(uint32_t handle)
{
    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);
    auto it = g_corridors.find(handle);
    if (it == g_corridors.end()) return false;

    auto* ci = it->second;
    if (!ci || !ci->valid) return false;

    dtNavMeshQuery* query = GetMutableQueryForMap(ci->mapId);
    if (!query) return false;

    return ci->corridor.isValid(10, query, &ci->filter);
}

/// Destroy a corridor and free its resources.
extern "C" __declspec(dllexport) void CorridorDestroy(uint32_t handle)
{
    std::lock_guard<std::recursive_mutex> lock(g_navigationMutex);
    auto it = g_corridors.find(handle);
    if (it != g_corridors.end())
    {
        delete it->second;
        g_corridors.erase(it);
    }
}
#endif // PHYSICS_DLL_ONLY

#if defined(_WIN32)
// DLL Entry Point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    if (ul_reason_for_call == DLL_PROCESS_ATTACH)
    {
        SetConsoleOutputCP(CP_UTF8);

        // Install CRT invalid parameter handler to prevent abort() on null stream etc.
        _set_invalid_parameter_handler(NavigationInvalidParameterHandler);

        // Suppress CRT assertion dialogs — redirect to stderr instead of modal dialog
        _CrtSetReportMode(_CRT_ASSERT, _CRTDBG_MODE_FILE | _CRTDBG_MODE_DEBUG);
        _CrtSetReportFile(_CRT_ASSERT, _CRTDBG_FILE_STDERR);
        _CrtSetReportMode(_CRT_ERROR, _CRTDBG_MODE_FILE | _CRTDBG_MODE_DEBUG);
        _CrtSetReportFile(_CRT_ERROR, _CRTDBG_FILE_STDERR);

        // Suppress abort() from showing Windows Error Reporting dialog
        _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);

        // Suppress Windows Error Reporting dialog for this process
        SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    }
    else if (ul_reason_for_call == DLL_PROCESS_DETACH)
    {
        if (lpReserved == nullptr)  // FreeLibrary was called
        {
            PhysicsEngine::Destroy();
            VMAP::VMapFactory::clear();  // Clean up the factory
        }
    }
    return TRUE;
}
#endif
