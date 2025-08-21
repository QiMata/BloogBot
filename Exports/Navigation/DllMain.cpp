#include "Navigation.h"
#include <windows.h>
#include <iostream>
#include "PhysicsBridge.h"
#include "PhysicsEngine.h"

// Flag to track if initialization has been performed
static bool g_initialized = false;

// Lazy initialization helper
static bool EnsureInitialized()
{
    if (g_initialized) return true;
    
    try
    {
        OutputDebugStringA("Navigation.dll: Starting lazy initialization...");
        
        if (auto* physics = PhysicsEngine::Instance()) 
        {
            physics->Initialize();
            OutputDebugStringA("Navigation.dll: PhysicsEngine initialized");
        }
        
        if (auto* navigation = Navigation::GetInstance()) 
        {
            navigation->Initialize();
            OutputDebugStringA("Navigation.dll: Navigation initialized");
        }
        
        g_initialized = true;
        OutputDebugStringA("Navigation.dll: Lazy initialization complete");
        return true;
    }
    catch (...)
    {
        OutputDebugStringA("Navigation.dll: Lazy initialization failed");
        return false;
    }
}

extern "C"
{
    __declspec(dllexport) XYZ* CalculatePath(uint32_t mapId, XYZ start, XYZ end, bool straightPath, int* length)
    {
        if (!EnsureInitialized() || !length) return nullptr;
        auto nav = Navigation::GetInstance();
        return nav ? nav->CalculatePath(mapId, start, end, straightPath, length) : nullptr;
    }

    __declspec(dllexport) void FreePathArr(XYZ* path)
    {
        if (!EnsureInitialized() || !path) return;
        if (auto* nav = Navigation::GetInstance())
            nav->FreePathArr(path);
    }

    __declspec(dllexport) bool LineOfSight(uint32_t mapId, XYZ from, XYZ to)
    {
        if (!EnsureInitialized()) return false;
        return Navigation::GetInstance()->IsLineOfSight(mapId, from, to);
    }

    __declspec(dllexport) NavPoly* CapsuleOverlap(uint32_t mapId, XYZ pos,
            float radius, float height,
            int* outCount)
    {
        if (!EnsureInitialized() || !outCount) return nullptr;

        std::vector<NavPoly> v;
        try
        {
            v = Navigation::GetInstance()->CapsuleOverlap(mapId, pos, radius, height);
        }
        catch (const std::exception& ex)
        {
            return nullptr;
        }

        *outCount = static_cast<int>(v.size());

        if (v.empty()) return nullptr;

        size_t bytes = v.size() * sizeof(NavPoly);

        auto* buf = static_cast<NavPoly*>(::CoTaskMemAlloc(bytes));
        if (!buf) return nullptr;

        std::memcpy(buf, v.data(), bytes);
        return buf;
    }

    __declspec(dllexport) void FreeNavPolyArr(NavPoly* p)
    {
        if (p) ::CoTaskMemFree(p);
    }
    
    __declspec(dllexport) PhysicsOutput __cdecl StepPhysics(const PhysicsInput* in, float dt)
    {
        if (!EnsureInitialized()) return PhysicsOutput{};
        return PhysicsEngine::Instance()->Step(*in, dt);
    }
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        OutputDebugStringA("Navigation.dll: DLL_PROCESS_ATTACH - lightweight init only");
        // Only lightweight initialization here - defer heavy work to first function call
        break;
        
    case DLL_PROCESS_DETACH:
        OutputDebugStringA("Navigation.dll: DLL_PROCESS_DETACH");
        if (g_initialized)
        {
            if (auto* navigation = Navigation::GetInstance()) 
                navigation->Release();
        }
        break;
        
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    }
    return TRUE;
}