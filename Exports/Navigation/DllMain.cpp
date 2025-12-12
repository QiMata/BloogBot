// DllMain.cpp - Refactored to use VMapManager2 directly
#include "Navigation.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "PhysicsEngine.h"
#include "PhysicsBridge.h"
#include "MapLoader.h"

#define NOMINMAX
#include <windows.h>
#include <iostream>
#include <memory>
#include <mutex>
#include <filesystem>
#include <vector>

// Global instances
static VMAP::VMapManager2* g_vmapManager = nullptr;  // Direct pointer to VMapManager2
static std::unique_ptr<MapLoader> g_mapLoader = nullptr;
static bool g_initialized = false;
static std::mutex g_initMutex;

void InitializeAllSystems()
{
    std::lock_guard<std::mutex> lock(g_initMutex);

    if (g_initialized)
        return;

    try
    {
        // Initialize MapLoader (optional, for terrain data)
        g_mapLoader = std::make_unique<MapLoader>();
        std::vector<std::string> mapPaths = { "maps/" };

        for (const auto& path : mapPaths)
        {
            if (std::filesystem::exists(path))
            {
                if (g_mapLoader->Initialize(path))
                    break;
            }
        }

        // Initialize VMAP system directly using VMapManager2
        std::vector<std::string> vmapPaths = { "vmaps/" };
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
                    break;
                }
            }
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

extern "C" __declspec(dllexport) void PreloadMap(uint32_t mapId)
{
    if (!g_initialized)
        InitializeAllSystems();

    // Preload VMAP data directly using VMapManager2
    if (g_vmapManager)
    {
        try
        {
            // Initialize the map if not already done
            if (!g_vmapManager->isMapInitialized(mapId))
            {
                g_vmapManager->initializeMap(mapId);
            }
        }
        catch (...) {}
    }

    // Preload navigation mesh
    try
    {
        auto* navigation = Navigation::GetInstance();
        if (navigation)
        {
            MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();
            navigation->GetQueryForMap(mapId);
        }
    }
    catch (...) {}
}

extern "C" __declspec(dllexport) XYZ* FindPath(uint32_t mapId, XYZ start, XYZ end, bool smoothPath, int* length)
{
    if (!g_initialized)
        InitializeAllSystems();

    auto* navigation = Navigation::GetInstance();
    if (navigation)
        return navigation->CalculatePath(mapId, start, end, smoothPath, length);

    *length = 0;
    return nullptr;
}

extern "C" __declspec(dllexport) void PathArrFree(XYZ* pathArr)
{
    delete[] pathArr;
}

extern "C" __declspec(dllexport) PhysicsOutput PhysicsStep(const PhysicsInput& input)
{
    if (!g_initialized)
        InitializeAllSystems();

    if (auto* physics = PhysicsEngine::Instance())
        return physics->Step(input, input.deltaTime);

    // Return passthrough if physics isn't available
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

extern "C" __declspec(dllexport) PhysicsOutput PhysicsStepV2(const PhysicsInput& input)
{
    if (!g_initialized)
        InitializeAllSystems();

    if (auto* physics = PhysicsEngine::Instance())
        return physics->StepV2(input, input.deltaTime);

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

extern "C" __declspec(dllexport) bool LineOfSight(uint32_t mapId, XYZ from, XYZ to)
{
    if (!g_initialized)
        InitializeAllSystems();

    // First, use VMAP system to check LOS against WMO/M2 geometry
    bool vmapClear = true;
    if (g_vmapManager)
    {
        try
        {
            // Ensure map is initialized
            if (!g_vmapManager->isMapInitialized(mapId))
                g_vmapManager->initializeMap(mapId);

            // VMapManager2::isInLineOfSight returns true if there is LOS (no obstruction)
            vmapClear = g_vmapManager->isInLineOfSight(mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z, false);
        }
        catch (...) {}
    }

    // If VMAP says blocked, no LOS
    if (!vmapClear)
        return false;

    // Terrain (ADT) raycast: sample triangles along the segment and test intersections
    if (g_mapLoader)
    {
        try
        {
            // Build world-space AABB around the ray segment in XY
            float minX = std::min(from.X, to.X);
            float minY = std::min(from.Y, to.Y);
            float maxX = std::max(from.X, to.X);
            float maxY = std::max(from.Y, to.Y);

            // Gather terrain triangles overlapped by XY AABB
            std::vector<MapFormat::TerrainTriangle> tris;
            if (g_mapLoader->GetTerrainTriangles(mapId, minX, minY, maxX, maxY, tris))
            {
                // Ray in world space
                G3D::Vector3 rayStart(from.X, from.Y, from.Z);
                G3D::Vector3 rayEnd(to.X, to.Y, to.Z);
                G3D::Vector3 dir = (rayEnd - rayStart);
                float len = dir.magnitude();
                if (len > 1e-6f)
                {
                    dir = dir * (1.0f / len);

                    // For each triangle, test ray intersection; any hit before end blocks LOS
                    for (const auto& t : tris)
                    {
                        // Construct triangle vertices
                        G3D::Vector3 a(t.ax, t.ay, t.az);
                        G3D::Vector3 b(t.bx, t.by, t.bz);
                        G3D::Vector3 c(t.cx, t.cy, t.cz);

                        // Moller-Trumbore style test
                        G3D::Vector3 edge1 = b - a;
                        G3D::Vector3 edge2 = c - a;
                        G3D::Vector3 pvec = dir.cross(edge2);
                        float det = edge1.dot(pvec);
                        if (std::fabs(det) < 1e-7f)
                            continue; // Parallel
                        float invDet = 1.0f / det;
                        G3D::Vector3 tvec = rayStart - a;
                        float u = tvec.dot(pvec) * invDet;
                        if (u < 0.0f || u > 1.0f) continue;
                        G3D::Vector3 qvec = tvec.cross(edge1);
                        float v = dir.dot(qvec) * invDet;
                        if (v < 0.0f || u + v > 1.0f) continue;
                        float tHit = edge2.dot(qvec) * invDet;
                        if (tHit >= 0.0f && tHit <= len)
                        {
                            // Intersection along the segment -> blocked LOS
                            return false;
                        }
                    }
                }
            }
        }
        catch (...) {}
    }

    // LOS clear
    return true;
}

// DLL Entry Point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    if (ul_reason_for_call == DLL_PROCESS_ATTACH)
    {
        SetConsoleOutputCP(CP_UTF8);
    }
    else if (ul_reason_for_call == DLL_PROCESS_DETACH)
    {
        if (lpReserved == nullptr)  // FreeLibrary was called
        {
            // Don't delete g_vmapManager as it's managed by the factory
            g_vmapManager = nullptr;
            g_mapLoader.reset();
            PhysicsEngine::Destroy();
            VMAP::VMapFactory::clear();  // Clean up the factory
        }
    }
    return TRUE;
}