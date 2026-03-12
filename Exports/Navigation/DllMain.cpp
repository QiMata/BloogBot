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
#include "DynamicObjectRegistry.h"

#define NOMINMAX
#include <windows.h>
#include <algorithm>
#include <iostream>
#include <memory>
#include <mutex>
#include <filesystem>
#include <vector>
#include <crtdbg.h>
#include <cstdio>
#include <csignal>
#include <cstdlib>

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

void InitializeAllSystems()
{
    std::lock_guard<std::mutex> lock(g_initMutex);

    if (g_initialized)
        return;

    try
    {
        // Get data root from environment variable if set.
        // Use Win32 GetEnvironmentVariableA (not _dupenv_s) because .NET's
        // Environment.SetEnvironmentVariable updates the process environment block
        // but NOT the CRT cache that _dupenv_s reads from.
        std::string dataRoot;
        {
            char buf[512] = {0};
            DWORD len = GetEnvironmentVariableA("WWOW_DATA_DIR", buf, sizeof(buf));
            if (len > 0 && len < sizeof(buf))
            {
                dataRoot = buf;
                if (!dataRoot.empty() && dataRoot.back() != '/' && dataRoot.back() != '\\')
                    dataRoot += '/';
            }
        }

        // Initialize MapLoader (optional, for terrain data)
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

        // Initialize VMAP system directly using VMapManager2
        std::vector<std::string> vmapPaths;
        if (!dataRoot.empty())
            vmapPaths.push_back(dataRoot + "vmaps/");
        vmapPaths.push_back("vmaps/");

        for (const auto& path : vmapPaths)
        {
            if (std::filesystem::exists(path))
            {
                // Get or create the VMapManager2 instance through factory
                g_vmapManager = static_cast<VMAP::VMapManager2*>(
                    VMAP::VMapFactory::createOrGetVMapManager());

                if (g_vmapManager)
                {
                    // Initialize factory and set base path
                    VMAP::VMapFactory::initialize();
                    g_vmapManager->setBasePath(path);
                    // Load displayId→model mapping for dynamic objects (elevators, doors)
                    DynamicObjectRegistry::Instance()->LoadDisplayIdMapping(path);
                    break;
                }
            }
        }

        // Set scenes/ directory for pre-cached collision data (if not already set).
        // Directory doesn't need to exist yet — EnsureMapLoaded() creates it on first extraction.
        // Don't overwrite if already configured (e.g. by test fixture via SetScenesDir export).
        if (SceneQuery::GetScenesDir().empty())
        {
            if (!dataRoot.empty())
                SceneQuery::SetScenesDir(dataRoot + "scenes/");
            else
                SceneQuery::SetScenesDir("scenes/");
        }

        // Initialize Navigation
        Navigation::GetInstance()->Initialize();

        // Initialize Physics Engine
        PhysicsEngine::Instance()->Initialize();

        g_initialized = true;
    }
    catch (...)
    {
        g_initialized = true; // Prevent retry
    }
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
        auto* navigation = Navigation::GetInstance();
        if (navigation)
        {
            MMAP::MMapFactory::createOrGetMMapManager();
            navigation->GetQueryForMap(mapId);
        }

        SceneQuery::EnsureMapLoaded(mapId);
    }
    catch (...) {}
}

extern "C" __declspec(dllexport) void PreloadMap(uint32_t mapId)
{
    __try
    {
        PreloadMapInner(mapId);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in PreloadMap\n");
        fprintf(stderr, "[Navigation.dll] SEH exception in PreloadMap (code=0x%08lx)\n",
                GetExceptionCode());
    }
}

extern "C" __declspec(dllexport) XYZ* FindPath(uint32_t mapId, XYZ start, XYZ end, bool smoothPath, int* length)
{
    __try
    {
        if (!g_initialized)
            InitializeAllSystems();

        auto* navigation = Navigation::GetInstance();
        if (navigation)
            return navigation->CalculatePath(mapId, start, end, smoothPath, length);

        if (length)
            *length = 0;
        return nullptr;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in FindPath\n");
        fprintf(stderr, "[Navigation.dll] SEH exception in FindPath (code=0x%08lx)\n",
                GetExceptionCode());

        if (length)
            *length = 0;
        return nullptr;
    }
}

extern "C" __declspec(dllexport) void PathArrFree(XYZ* pathArr)
{
    delete[] pathArr;
}

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

    if (auto* physics = PhysicsEngine::Instance())
        return physics->StepV2(input, input.deltaTime);

    return MakePassthroughOutput(input);
}

extern "C" __declspec(dllexport) PhysicsOutput PhysicsStepV2(const PhysicsInput& input)
{
    __try
    {
        return PhysicsStepV2Inner(input);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        OutputDebugStringA("[Navigation.dll] SEH exception in PhysicsStepV2\n");
        fprintf(stderr, "[Navigation.dll] SEH exception in PhysicsStepV2 (code=0x%08lx)\n",
                GetExceptionCode());
        return MakePassthroughOutput(input);
    }
}

extern "C" __declspec(dllexport) bool LineOfSight(uint32_t mapId, XYZ from, XYZ to)
{
    if (!g_initialized)
        InitializeAllSystems();

    // Delegate to SceneQuery implementation
    return SceneQuery::LineOfSight(mapId, G3D::Vector3(from.X, from.Y, from.Z), G3D::Vector3(to.X, to.Y, to.Z));
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
        if (penetrationDepth <= 0.02f)
            continue;

        if (std::fabs(hit.normal.z) >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z)
            continue;

        return true;
    }

    return false;
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
        const float groundZ = SceneQuery::GetGroundZ(mapId, x, y, queryBaseZ, queryDistance);
        if (groundZ <= PhysicsConstants::INVALID_HEIGHT + 1.0f)
            return SegmentValidationCode::MissingSupport;

        const float deltaZ = groundZ - currentZ;
        if (deltaZ > PhysicsConstants::STEP_HEIGHT + 0.1f)
            return SegmentValidationCode::StepUpTooHigh;

        if (deltaZ < -PhysicsConstants::STEP_DOWN_HEIGHT - 0.1f)
            return SegmentValidationCode::StepDownTooFar;

        lastSupportZ = groundZ;

        if (resolvedEndZ)
            *resolvedEndZ = groundZ;

        if (supportDelta)
            *supportDelta = groundZ - start.Z;

        if (travelFraction)
            *travelFraction = horizontalDistance > 0.01f
                ? std::max(0.0f, std::min(1.0f, traveled / horizontalDistance))
                : 1.0f;

        if (HasBlockingCapsuleOverlap(mapId, x, y, groundZ, radius, height, orientation))
            return SegmentValidationCode::BlockedGeometry;

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
    constexpr float chunkSize = 1.0f;

    float currentX = start.X;
    float currentY = start.Y;
    float currentZ = start.Z;
    float traveled = 0.0f;
    float remaining = horizontalDistance;

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

        if (advanced + 0.05f < requested)
        {
            if (travelFraction && horizontalDistance > 0.01f)
                *travelFraction = std::max(0.0f, std::min(1.0f, traveled / horizontalDistance));

            return static_cast<uint32_t>(SegmentValidationCode::BlockedGeometry);
        }

        currentX += direction.x * advanced;
        currentY += direction.y * advanced;
        traveled += advanced;
        remaining -= advanced;

        const auto supportResult = validateSupport(currentX, currentY, currentZ, orientation, traveled);
        if (supportResult != SegmentValidationCode::Clear)
            return static_cast<uint32_t>(supportResult);

        currentZ = lastSupportZ;
    }

    return static_cast<uint32_t>(SegmentValidationCode::Clear);
}

// Check whether the line segment (x0,y0,z0)→(x1,y1,z1) intersects any triangle
// belonging to a registered dynamic object on the given map.
// Returns false when no dynamic objects are registered (fast path).
// Used by the pathfinding layer to detect when a freshly-generated path segment
// passes through a closed door or other registered dynamic obstacle.
extern "C" __declspec(dllexport) bool SegmentIntersectsDynamicObjects(
    uint32_t mapId,
    float x0, float y0, float z0,
    float x1, float y1, float z1)
{
    if (!g_initialized)
        InitializeAllSystems();

    auto* reg = DynamicObjectRegistry::Instance();
    if (!reg || reg->Count() == 0) return false;

    // Build AABB of segment with a small radius pad so thin geometry is caught.
    const float pad = 0.5f;
    const G3D::AABox segBox(
        G3D::Vector3(std::min(x0, x1) - pad, std::min(y0, y1) - pad, std::min(z0, z1) - pad),
        G3D::Vector3(std::max(x0, x1) + pad, std::max(y0, y1) + pad, std::max(z0, z1) + pad));

    std::vector<CapsuleCollision::Triangle> tris;
    reg->QueryTriangles(mapId, segBox, tris);
    if (tris.empty()) return false;

    const G3D::Vector3 p0(x0, y0, z0);
    const G3D::Vector3 p1(x1, y1, z1);
    for (const auto& tri : tris)
    {
        const G3D::Vector3 ta(tri.a.x, tri.a.y, tri.a.z);
        const G3D::Vector3 tb(tri.b.x, tri.b.y, tri.b.z);
        const G3D::Vector3 tc(tri.c.x, tri.c.y, tri.c.z);
        if (SegmentIntersectsTriangle(p0, p1, ta, tb, tc))
            return true;
    }
    return false;
}

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
