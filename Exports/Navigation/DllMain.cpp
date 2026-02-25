// DllMain.cpp - Refactored to use VMapManager2 directly
#include "Navigation.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "PhysicsEngine.h"
#include "PhysicsBridge.h"
#include "MapLoader.h"
#include "SceneQuery.h"
#include "DynamicObjectRegistry.h"

#define NOMINMAX
#include <windows.h>
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
