// =============================================================================
// Loader.dll - .NET 8 CLR Bootstrapper for DLL Injection
// 
// This DLL is injected into the WoW client process and bootstraps the .NET 8
// runtime using the hostfxr API. It then loads and executes the managed
// ForegroundBotRunner assembly.
//
// Key differences from .NET Framework hosting:
// - Uses hostfxr.dll instead of mscoree.dll
// - Requires a runtimeconfig.json file
// - Uses load_assembly_and_get_function_pointer delegate
// - Entry point must be a static method with specific signature
// =============================================================================

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <process.h>
#include <string>
#include <iostream>
#include <sstream>
#include <filesystem>

#include "nethost_helpers.h"

// Configuration - adjust these to match your managed assembly
#define MANAGED_ASSEMBLY_NAME      L"ForegroundBotRunner"
#define MANAGED_ASSEMBLY_DLL       L"ForegroundBotRunner.dll"
#define MANAGED_RUNTIME_CONFIG     L"ForegroundBotRunner.runtimeconfig.json"
#define MANAGED_TYPE_NAME          L"ForegroundBotRunner.Loader, ForegroundBotRunner"
#define MANAGED_METHOD_NAME        L"Load"

// Global state
HMODULE g_myDllModule = NULL;
HANDLE g_hThread = NULL;
void* g_hostContextHandle = NULL;
HostFxrFunctions g_hostfxr = {};
std::wstring g_baseDirectory;

// Forward declarations
unsigned __stdcall ThreadMain(void* pParam);
bool InitializeNetHost();
bool LoadAndRunManagedCode();
void Cleanup();

// Macro for showing message boxes (useful for debugging injection issues)
#define MB(s) MessageBoxW(NULL, s, L"Loader", MB_OK)
#define MB_ERROR(s) MessageBoxW(NULL, s, L"Loader Error", MB_OK | MB_ICONERROR)

// =============================================================================
// Main bootstrap thread
// =============================================================================
unsigned __stdcall ThreadMain(void* pParam)
{
    // Allocate console for debug output
    AllocConsole();
    FILE* fDummy;
    freopen_s(&fDummy, "CONOUT$", "w", stdout);
    freopen_s(&fDummy, "CONOUT$", "w", stderr);
    
    std::cout << "========================================" << std::endl;
    std::cout << "  WWoW Loader - .NET 8 CLR Bootstrap   " << std::endl;
    std::cout << "========================================" << std::endl;
    std::cout << std::endl;

#if _DEBUG
    std::cout << "[DEBUG] Attach a debugger now to the host process." << std::endl;
    std::cout << "[DEBUG] Waiting 10 seconds..." << std::endl;
    
    HANDLE hEvent = CreateEventW(nullptr, TRUE, FALSE, L"WWoWLoaderDebugEvent");
    WaitForSingleObject(hEvent, 10000);
    
    if (IsDebuggerPresent())
    {
        std::cout << "[DEBUG] Debugger attached." << std::endl;
    }
    else
    {
        std::cout << "[DEBUG] No debugger detected, continuing..." << std::endl;
    }
    
    CloseHandle(hEvent);
    std::cout << std::endl;
#endif

    // Initialize and run
    if (!InitializeNetHost())
    {
        MB_ERROR(L"Failed to initialize .NET host. Check console for details.");
        return 1;
    }
    
    if (!LoadAndRunManagedCode())
    {
        MB_ERROR(L"Failed to load managed code. Check console for details.");
        Cleanup();
        return 1;
    }
    
    // Note: LoadAndRunManagedCode spawns a thread and returns immediately
    // The managed code is now running in its own thread
    
    std::cout << "[Loader] Managed code loaded successfully." << std::endl;
    return 0;
}

// =============================================================================
// Initialize the .NET host
// =============================================================================
bool InitializeNetHost()
{
    LogMessage(LogLevel::Info, L"Initializing .NET 8 host...");
    
    // Get base directory (where Loader.dll is located)
    wchar_t modulePathBuffer[MAX_PATH];
    if (!GetModuleFileNameW(g_myDllModule, modulePathBuffer, MAX_PATH))
    {
        LogMessage(LogLevel::Error, L"Failed to get module path");
        return false;
    }
    
    g_baseDirectory = modulePathBuffer;
    size_t lastSlash = g_baseDirectory.find_last_of(L'\\');
    if (lastSlash != std::wstring::npos)
    {
        g_baseDirectory = g_baseDirectory.substr(0, lastSlash + 1);
    }
    
    LogMessage(LogLevel::Info, L"Base directory: " + g_baseDirectory);
    
    // Find hostfxr.dll
    std::wstring hostfxrPath;
    if (!FindHostFxrPath(hostfxrPath, g_baseDirectory))
    {
        LogMessage(LogLevel::Error, L"Could not locate hostfxr.dll");
        LogMessage(LogLevel::Info, L"Make sure .NET 8 runtime is installed or hostfxr.dll is in the same directory");
        return false;
    }
    
    // Load hostfxr functions
    if (!LoadHostFxr(hostfxrPath, g_hostfxr))
    {
        return false;
    }
    
    // Build path to runtimeconfig.json
    std::wstring runtimeConfigPath = g_baseDirectory + MANAGED_RUNTIME_CONFIG;
    
    if (!std::filesystem::exists(runtimeConfigPath))
    {
        LogMessage(LogLevel::Error, L"Runtime config not found: " + runtimeConfigPath);
        return false;
    }
    
    LogMessage(LogLevel::Info, L"Using runtime config: " + runtimeConfigPath);
    
    // Initialize the host context
    int32_t rc = g_hostfxr.initialize(runtimeConfigPath.c_str(), nullptr, &g_hostContextHandle);
    
    if (rc != 0 || g_hostContextHandle == nullptr)
    {
        std::wstringstream ss;
        ss << L"hostfxr_initialize_for_runtime_config failed. rc = 0x" << std::hex << rc;
        LogMessage(LogLevel::Error, ss.str());
        
        // Provide helpful diagnostics
        if (rc == 0x80008083) // FrameworkMissingFailure
        {
            LogMessage(LogLevel::Error, L"The required .NET runtime is not installed.");
            LogMessage(LogLevel::Info, L"Please install .NET 8 Desktop Runtime (x86 for 32-bit WoW)");
        }
        
        return false;
    }
    
    LogMessage(LogLevel::Info, L"Host context initialized successfully");
    return true;
}

// =============================================================================
// Load and execute managed code
// =============================================================================
bool LoadAndRunManagedCode()
{
    LogMessage(LogLevel::Info, L"Loading managed assembly...");
    
    // Get the load_assembly_and_get_function_pointer delegate
    load_assembly_and_get_function_pointer_fn load_assembly_and_get_function_pointer = nullptr;
    
    int32_t rc = g_hostfxr.get_delegate(
        g_hostContextHandle,
        hdt_load_assembly_and_get_function_pointer,
        (void**)&load_assembly_and_get_function_pointer);
    
    if (rc != 0 || load_assembly_and_get_function_pointer == nullptr)
    {
        std::wstringstream ss;
        ss << L"Failed to get load_assembly_and_get_function_pointer delegate. rc = 0x" << std::hex << rc;
        LogMessage(LogLevel::Error, ss.str());
        return false;
    }
    
    // Build path to managed assembly
    std::wstring assemblyPath = g_baseDirectory + MANAGED_ASSEMBLY_DLL;
    
    if (!std::filesystem::exists(assemblyPath))
    {
        LogMessage(LogLevel::Error, L"Managed assembly not found: " + assemblyPath);
        return false;
    }
    
    LogMessage(LogLevel::Info, L"Loading assembly: " + assemblyPath);
    LogMessage(LogLevel::Info, L"Type: " + std::wstring(MANAGED_TYPE_NAME));
    LogMessage(LogLevel::Info, L"Method: " + std::wstring(MANAGED_METHOD_NAME));
    
    // Load the assembly and get the entry point function pointer
    // The delegate type name is nullptr to use the default delegate type:
    // public delegate int ComponentEntryPoint(IntPtr args, int sizeBytes);
    component_entry_point_fn entry_point = nullptr;
    
    rc = load_assembly_and_get_function_pointer(
        assemblyPath.c_str(),
        MANAGED_TYPE_NAME,
        MANAGED_METHOD_NAME,
        nullptr,  // Use default delegate type (ComponentEntryPoint)
        nullptr,  // Reserved
        (void**)&entry_point);
    
    if (rc != 0 || entry_point == nullptr)
    {
        std::wstringstream ss;
        ss << L"Failed to load assembly and get function pointer. rc = 0x" << std::hex << rc;
        LogMessage(LogLevel::Error, ss.str());
        
        // Provide helpful diagnostics
        if (rc == 0x80131522) // TypeLoadException
        {
            LogMessage(LogLevel::Error, L"Could not find the specified type in the assembly.");
            LogMessage(LogLevel::Info, L"Verify that MANAGED_TYPE_NAME matches the full type name.");
        }
        else if (rc == 0x80131523) // MissingMethodException  
        {
            LogMessage(LogLevel::Error, L"Could not find the specified method.");
            LogMessage(LogLevel::Info, L"Verify the method signature matches: public static int Load(IntPtr args, int sizeBytes)");
        }
        
        return false;
    }
    
    LogMessage(LogLevel::Info, L"Calling managed entry point...");
    
    // Call the entry point
    // For our use case, we don't pass any arguments
    int32_t result = entry_point(nullptr, 0);
    
    std::wstringstream ss;
    ss << L"Managed entry point returned: " << result;
    LogMessage(LogLevel::Info, ss.str());
    
    return result == 0;
}

// =============================================================================
// Cleanup resources
// =============================================================================
void Cleanup()
{
    LogMessage(LogLevel::Info, L"Cleaning up...");
    
    if (g_hostContextHandle && g_hostfxr.close)
    {
        g_hostfxr.close(g_hostContextHandle);
        g_hostContextHandle = nullptr;
    }
    
    g_hostfxr.Unload();
}

// =============================================================================
// Initialize the CLR bootstrap thread
// =============================================================================
void StartLoader()
{
    g_hThread = (HANDLE)_beginthreadex(NULL, 0, ThreadMain, NULL, 0, NULL);
    
    if (g_hThread == NULL)
    {
        MB_ERROR(L"Failed to create bootstrap thread");
    }
}

// =============================================================================
// DLL Entry Point
// =============================================================================
BOOL WINAPI DllMain(HMODULE hDll, DWORD dwReason, LPVOID lpReserved)
{
    g_myDllModule = hDll;
    
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        // Disable thread library calls for performance
        DisableThreadLibraryCalls(hDll);
        // Start the loader in a new thread to avoid loader lock issues
        StartLoader();
        break;
        
    case DLL_PROCESS_DETACH:
        // Clean up the .NET host
        Cleanup();
        
        // Wait for and close the bootstrap thread
        if (g_hThread)
        {
            // Give the thread a chance to exit gracefully
            WaitForSingleObject(g_hThread, 1000);
            CloseHandle(g_hThread);
            g_hThread = NULL;
        }
        break;
    }
    
    return TRUE;
}
