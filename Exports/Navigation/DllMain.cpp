// DllMain.cpp - Refactored to use VMapManager2 directly
#include "Navigation.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "PhysicsEngine.h"
#include "PhysicsBridge.h"
#include "MapLoader.h"
#include "SceneQuery.h"

#define NOMINMAX
#include <windows.h>
#include <iostream>
#include <memory>
#include <mutex>
#include <filesystem>
#include <vector>

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
        // Get data root from environment variable if set
        std::string dataRoot;
        char* envDataRoot = nullptr;
        size_t envSize = 0;
        if (_dupenv_s(&envDataRoot, &envSize, "BLOOGBOT_DATA_DIR") == 0 && envDataRoot != nullptr)
        {
            dataRoot = envDataRoot;
            free(envDataRoot);
            if (!dataRoot.empty() && dataRoot.back() != '/' && dataRoot.back() != '\\')
                dataRoot += '/';
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

    // Preload navigation mesh
    try
    {
        auto* navigation = Navigation::GetInstance();
        if (navigation)
        {
            MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();
            
            navigation->GetQueryForMap(mapId);
        }

        SceneQuery::EnsureMapLoaded(mapId);
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

// Removed legacy PhysicsStep export. Use PhysicsStepV2 only.

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

    // Delegate to SceneQuery implementation
    return SceneQuery::LineOfSight(mapId, G3D::Vector3(from.X, from.Y, from.Z), G3D::Vector3(to.X, to.Y, to.Z));
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
            PhysicsEngine::Destroy();
            VMAP::VMapFactory::clear();  // Clean up the factory
        }
    }
    return TRUE;
}