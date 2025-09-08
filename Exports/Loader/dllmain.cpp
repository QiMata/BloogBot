#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <string>
#include <sstream>
#include <fstream>
#include <thread>
#include <vector>
#include <cassert>
#include <time.h>
#include <process.h>
#include <cstdio>
#include <cstdarg>
#include <stdint.h>
#include <stdio.h>
#include <iostream>
#include <io.h>
#include <fcntl.h>
#pragma comment(lib, "User32.lib")

static const wchar_t *g_defaultAssemblyName = L"ForegroundBotRunner.dll";
static const wchar_t *g_defaultRuntimeConfigName  = L"ForegroundBotRunner.runtimeconfig.json";
static const char    *g_entryTypeName      = "ForegroundBotRunner.MinimalLoader";
static const char    *g_entryMethodName    = "TestEntry";

static std::wstring g_logFilePath; 
static bool g_consoleAttached = false;
static bool g_quiet = false; 
static volatile LONG g_pauseTriggered = 0; // pause only once
static thread_local bool g_inVehLogging = false; // VEH reentrancy guard

// ------------ Console Management ------------
static void AllocateConsoleWindow()
{
    if (g_consoleAttached) return;
    
    // Always allocate a console for debugging - not dependent on environment variables
    if (!AllocConsole()) {
        // If AllocConsole fails, try to attach to parent console
        if (!AttachConsole(ATTACH_PARENT_PROCESS)) {
            // Still failed, try to attach to any existing console
            AttachConsole(GetCurrentProcessId());
        }
    }
    
    // Redirect stdout, stdin, stderr to console
    FILE* pCout;
    FILE* pCin;
    FILE* pCerr;
    
    freopen_s(&pCout, "CONOUT$", "w", stdout);
    freopen_s(&pCin, "CONIN$", "r", stdin);
    freopen_s(&pCerr, "CONOUT$", "w", stderr);
    
    // Make cout, wcout, cin, wcin, wcerr, cerr, wclog and clog
    // point to console as well
    std::ios::sync_with_stdio(true);
    std::wcout.clear();
    std::cout.clear();
    std::wcerr.clear();
    std::cerr.clear();
    std::wcin.clear();
    std::cin.clear();
    
    g_consoleAttached = true;
    
    // Set console title so we can easily identify it
    SetConsoleTitleA("BloogBot Loader.dll - DLL Injection Console");
    
    // Write immediate output so we know console is working
    std::cout << "======================================================" << std::endl;
    std::cout << "BLOOGBOT LOADER.DLL CONSOLE INITIALIZED" << std::endl;
    std::cout << "Process: " << GetCurrentProcessId() << std::endl;
    std::cout << "======================================================" << std::endl;
    std::cout.flush();
}

// ------------ Logging primitives ------------
static void AppendToFileRaw(const std::string& text)
{
    if (g_logFilePath.empty()) return;
    std::ofstream ofs(g_logFilePath, std::ios::app | std::ios::out);
    if (ofs.is_open())
    {
        SYSTEMTIME st; GetLocalTime(&st);
        char ts[48]; sprintf_s(ts, "%02d:%02d:%02d.%03d ", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
        ofs << ts << text;
        ofs.flush();
    }
}

static void AppendLog(const std::string &msg)
{
    // Always write to console if available
    if (g_consoleAttached) {
        std::cout << msg << std::endl;
        std::cout.flush();
    }
    
    // Also write to debug output
    OutputDebugStringA((msg + "\n").c_str());
    
    // And to log file
    AppendToFileRaw(msg + "\n");
    
    // Also show critical messages as message boxes for immediate visibility
    if (msg.find("ERROR") != std::string::npos || msg.find("SUCCESS") != std::string::npos) {
        MessageBoxA(nullptr, msg.c_str(), "Loader.dll", MB_OK | MB_TOPMOST);
    }
}

static void AppendLogW(const std::wstring &msg) { 
    AppendLog(std::string(msg.begin(), msg.end())); 
}

// ------------ hostfxr types ------------
using hostfxr_initialize_for_runtime_config_fn = int(*)(const wchar_t *runtimeConfigPath, void *parameters, void **hostContextHandle);
using hostfxr_get_runtime_delegate_fn = int(*)(void *hostContextHandle, int type, void **delegate);
using hostfxr_close_fn = int(*)(void *hostContextHandle);
using load_assembly_and_get_function_pointer_fn = int(*)(const wchar_t*,const wchar_t*,const wchar_t*,const wchar_t*,void*,void**);
#ifdef _M_IX86
using component_entrypoint_fn_stdcall = int(__stdcall *)(void*, int32_t);
using component_entrypoint_fn_cdecl   = int(__cdecl   *)(void*, int32_t);
#else
using component_entrypoint_fn_stdcall = int(*)(void*, int32_t); // x64 single calling convention
using component_entrypoint_fn_cdecl   = int(*)(void*, int32_t);
#endif

// ------------ Diagnostics helpers ------------
static void MaybePauseForDiagnostics(const char* where, DWORD code, PEXCEPTION_POINTERS info)
{
    if (GetEnvironmentVariableW(L"LOADER_PAUSE_ON_EXCEPTION", nullptr, 0) == 0) return;
    if (InterlockedCompareExchange(&g_pauseTriggered, 1, 0) != 0) return;
    char buf[256]; sprintf_s(buf, "[PAUSE] %s caught exception 0x%08X. Pausing...", where, code);
    AppendLog(buf);
    if (info && info->ExceptionRecord){ 
        sprintf_s(buf, "[PAUSE] ExceptionAddress=0x%p", info->ExceptionRecord->ExceptionAddress); 
        AppendLog(buf);
    }    
    AppendLog("Process sleeping (LOADER_PAUSE_ON_EXCEPTION set). Attach debugger or kill process.");
    MessageBoxA(nullptr, "Loader paused after exception (LOADER_PAUSE_ON_EXCEPTION).", "Loader", MB_OK | MB_ICONWARNING);
    while (true) Sleep(1000);
}

static LONG WINAPI VectoredHandler(PEXCEPTION_POINTERS info)
{
    if (!info || !info->ExceptionRecord) return EXCEPTION_CONTINUE_SEARCH;
    DWORD code = info->ExceptionRecord->ExceptionCode;
    // Ignore debug print exceptions to prevent recursion loop (DBG_PRINTEXCEPTION_C == 0x40010006)
    if (code == 0x40010006) return EXCEPTION_CONTINUE_SEARCH;
    if (g_inVehLogging) return EXCEPTION_CONTINUE_SEARCH; // prevent re-entrancy if any nested issue
    g_inVehLogging = true;
    char buf[256]; sprintf_s(buf, "[VEH] Exception 0x%08X at 0x%p", code, info->ExceptionRecord->ExceptionAddress);
    AppendLog(buf);
    MaybePauseForDiagnostics("VEH", code, info);
    g_inVehLogging = false;
    return EXCEPTION_CONTINUE_SEARCH;
}

// ------------ Utility functions ------------
static std::wstring ProbeFxrUnderRoot(const std::wstring& root, const std::wstring& relative)
{
    std::wstring dir = root + L"\\" + relative;
    WIN32_FIND_DATAW ffd{};
    std::wstring newest;
    std::wstring search = dir + L"\\*";
    HANDLE hFind = FindFirstFileW(search.c_str(), &ffd);
    if (hFind != INVALID_HANDLE_VALUE)
    {
        do
        {
            if ((ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) && ffd.cFileName[0] != L'.')
            {
                std::wstring candidate = ffd.cFileName;
                if (candidate.rfind(L"8.", 0) == 0 && candidate > newest)
                    newest = candidate;
            }
        } while (FindNextFileW(hFind, &ffd));
        FindClose(hFind);
    }
    if (newest.empty()) return L"";
    std::wstring fxr = dir + L"\\" + newest + L"\\hostfxr.dll";
    if (GetFileAttributesW(fxr.c_str()) != INVALID_FILE_ATTRIBUTES) return fxr;
    return L"";
}

static std::wstring GetHostFxrPath()
{
    wchar_t overrideBuf[MAX_PATH * 4];
    DWORD olen = GetEnvironmentVariableW(L"HOSTFXR_PATH", overrideBuf, _countof(overrideBuf));
    if (olen > 0 && olen < _countof(overrideBuf))
    {
        std::wstring o(overrideBuf, olen);
        AppendLogW(L"HOSTFXR_PATH override: " + o);
        if (GetFileAttributesW(o.c_str()) != INVALID_FILE_ATTRIBUTES) return o;
        AppendLog("HOSTFXR_PATH override invalid (file missing).");
    }

    const wchar_t* envRoots[] = { L"DOTNET_ROOT(x86)", L"DOTNET_ROOT" };
    for (auto envName : envRoots)
    {
        wchar_t buf[1024]; DWORD elen = GetEnvironmentVariableW(envName, buf, _countof(buf));
        if (elen > 0 && elen < _countof(buf))
        {
            std::wstring root(buf, elen);
            AppendLogW(std::wstring(envName) + L"=" + root);
            auto fxr = ProbeFxrUnderRoot(root, L"host\\fxr");
            if (!fxr.empty()) { AppendLogW(L"Using hostfxr (env root): " + fxr); return fxr; }
            auto legacy = ProbeFxrUnderRoot(root, L"shared\\Microsoft.NETCore.App");
            if (!legacy.empty()) { AppendLogW(L"(Fallback) Using hostfxr from shared unexpectedly: " + legacy); return legacy; }
            AppendLog("No hostfxr found under env root.");
        }
    }
#ifdef _WIN64
    const wchar_t* stdRoots[] = { L"C:\\Program Files\\dotnet", L"C:\\Program Files (x86)\\dotnet" };
#else
    const wchar_t* stdRoots[] = { L"C:\\Program Files (x86)\\dotnet", L"C:\\Program Files\\dotnet" };
#endif
    for (auto root : stdRoots)
    {
        auto fxr = ProbeFxrUnderRoot(root, L"host\\fxr");
        if (!fxr.empty()) { AppendLogW(L"Using hostfxr from standard root: " + fxr); return fxr; }
        AppendLogW(L"No 8.x hostfxr in: " + std::wstring(root) + L"\\host\\fxr");
    }

    AppendLog("Failed to locate hostfxr.dll (searched overrides, DOTNET_ROOT*, and standard roots).");
    return L"";
}

static std::wstring GetModuleDirectory(HMODULE hMod)
{ 
    wchar_t path[MAX_PATH]; 
    DWORD len = GetModuleFileNameW(hMod, path, MAX_PATH); 
    if (len == 0 || len == MAX_PATH) return L""; 
    std::wstring p(path, len); 
    size_t pos = p.find_last_of(L"\\/"); 
    return (pos == std::wstring::npos) ? L"" : p.substr(0, pos + 1);
} 

static bool FileExistsW(const std::wstring& p) { 
    return GetFileAttributesW(p.c_str()) != INVALID_FILE_ATTRIBUTES; 
}

static void ResolveAssemblyPaths(const std::wstring& baseDir, std::wstring& asmPath, std::wstring& runtimeCfg)
{ 
    wchar_t buf[2048]; 
    DWORD len = GetEnvironmentVariableW(L"FOREGROUNDBOT_DLL_PATH", buf, 2048); 
    if (len > 0 && len < 2048) { 
        std::wstring envPath(buf, len); 
        AppendLogW(L"FOREGROUNDBOT_DLL_PATH set: " + envPath); 
        if (envPath.size() > 4 && _wcsicmp(envPath.c_str() + envPath.size() - 4, L".dll") == 0) { 
            asmPath = envPath; 
            size_t slash = envPath.find_last_of(L"\\/"); 
            runtimeCfg = (slash != std::wstring::npos) ? envPath.substr(0, slash + 1) + g_defaultRuntimeConfigName : g_defaultRuntimeConfigName;
        } else { 
            if (!envPath.empty() && envPath.back() != L'\\' && envPath.back() != L'/') envPath.push_back(L'\\'); 
            asmPath = envPath + g_defaultAssemblyName; 
            runtimeCfg = envPath + g_defaultRuntimeConfigName; 
        } 
        AppendLogW(L"Using assembly path from env: " + asmPath); 
        AppendLogW(L"Using runtimeconfig path from env: " + runtimeCfg); 
        return;
    } 
    asmPath = baseDir + g_defaultAssemblyName; 
    runtimeCfg = baseDir + g_defaultRuntimeConfigName; 
    AppendLog("Using default co-located assembly path (env var not set)"); 
}

enum hostfxr_delegate_type_local { 
    hdt_com_activation = 0, 
    hdt_load_in_memory_assembly = 1, 
    hdt_winrt_activation = 2, 
    hdt_com_register = 3, 
    hdt_com_unregister = 4, 
    hdt_load_assembly_and_get_function_pointer = 5, 
    hdt_get_function_pointer = 6 
};

static void InitLogFile(const std::wstring& baseDir)
{
    wchar_t overrideBuf[MAX_PATH * 4]; 
    DWORD len = GetEnvironmentVariableW(L"LOADER_LOG_PATH", overrideBuf, _countof(overrideBuf));
    if (len > 0 && len < _countof(overrideBuf)) { 
        g_logFilePath.assign(overrideBuf, len); 
    }
    else { 
        SYSTEMTIME st; 
        GetLocalTime(&st); 
        wchar_t name[128]; 
        swprintf_s(name, 128, L"loader_full_%04d%02d%02d_%02d%02d%02d.txt", st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond); 
        
        // Use absolute path to solution root so we can easily find the log
        g_logFilePath = L"C:\\Users\\WowAdmin\\source\\repos\\sethrhod\\BloogBot\\" + std::wstring(name); 
    }
    
    AppendLog("================ LOADER LOG START ================");
    AppendLog("Loader using .NET 8 hostfxr runtime hosting");
    
    // Log process information
    char buf[512];
    sprintf_s(buf, "Process ID: %lu", GetCurrentProcessId());
    AppendLog(buf);
    
    wchar_t processPath[MAX_PATH];
    if (GetModuleFileNameW(NULL, processPath, MAX_PATH)) {
        AppendLogW(L"Host process: " + std::wstring(processPath));
    }
}

// ------------ Main host thread ------------
static unsigned __stdcall HostThread(void* param)
{
    HMODULE hSelf = (HMODULE)param; 
    
    // FIRST THING: Allocate console so we can see what's happening
    AllocateConsoleWindow();
    AppendLog("*** LOADER.DLL HOST THREAD STARTED ***");
    
    g_quiet = GetEnvironmentVariableW(L"LOADER_QUIET", nullptr, 0) > 0;
    std::wstring baseDir = GetModuleDirectory(hSelf); 
    if (baseDir.empty()) { 
        AppendLog("[FATAL] Could not resolve loader directory"); 
        return 1; 
    }
    
    InitLogFile(baseDir);
    AppendLog("[Loader] Host thread initializing with .NET 8 runtime...");
    AppendLogW(L"Loader BaseDir: " + baseDir);
    AppendLog(std::string("Process Arch: ") + (
#ifdef _WIN64
        "x64"
#else
        "x86"
#endif
    ));

    AddVectoredExceptionHandler(1, VectoredHandler);

    std::wstring assemblyPath, runtimeConfig; 
    ResolveAssemblyPaths(baseDir, assemblyPath, runtimeConfig); 
    
    AppendLogW(L"Checking assembly path: " + assemblyPath);
    if (!FileExistsW(assemblyPath)) { 
        AppendLog("ERROR: Missing ForegroundBotRunner.dll"); 
        AppendLogW(L"Tried: " + assemblyPath); 
        return 1;
    } 
    AppendLog("? Found ForegroundBotRunner.dll");
    
    AppendLogW(L"Checking runtime config: " + runtimeConfig);
    if (!FileExistsW(runtimeConfig)) { 
        AppendLog("ERROR: Missing ForegroundBotRunner.runtimeconfig.json"); 
        AppendLogW(L"Tried: " + runtimeConfig); 
        return 1;
    } 
    AppendLog("? Found ForegroundBotRunner.runtimeconfig.json");

    std::wstring hostfxrPath = GetHostFxrPath(); 
    AppendLogW(L"hostfxr path attempt: " + hostfxrPath); 
    if (hostfxrPath.empty()) { 
        AppendLog("ERROR: hostfxr.dll not found"); 
        return 1; 
    }
    
    AppendLogW(L"Loading hostfxr: " + hostfxrPath);
    HMODULE hostfxr = LoadLibraryW(hostfxrPath.c_str()); 
    if (!hostfxr) { 
        AppendLog("ERROR: Failed to load hostfxr.dll"); 
        return 1;
    } 
    AppendLog("? hostfxr loaded");

    auto init_f = (hostfxr_initialize_for_runtime_config_fn)GetProcAddress(hostfxr, "hostfxr_initialize_for_runtime_config");
    auto get_delegate_f = (hostfxr_get_runtime_delegate_fn)GetProcAddress(hostfxr, "hostfxr_get_runtime_delegate");
    auto close_f = (hostfxr_close_fn)GetProcAddress(hostfxr, "hostfxr_close");
    if (!init_f || !get_delegate_f || !close_f) { 
        AppendLog("ERROR: hostfxr exports missing"); 
        return 1; 
    }
    AppendLog("? All hostfxr function pointers resolved");

    AppendLogW(L"Initializing .NET runtime with config: " + runtimeConfig);
    void* hostContext = nullptr; 
    int rc = init_f(runtimeConfig.c_str(), nullptr, &hostContext); 
    char buf[128]; 
    sprintf_s(buf, "hostfxr_initialize_for_runtime_config result: %d", rc);
    AppendLog(buf);
    if (rc != 0 || !hostContext) { 
        sprintf_s(buf, "ERROR: hostfxr_initialize_for_runtime_config failed with code: %d", rc);
        AppendLog(buf); 
        return rc; 
    }
    AppendLog("? .NET runtime initialized");

    AppendLog("Getting load_assembly_and_get_function_pointer delegate...");
    void* loadAssemblyDelegate = nullptr; 
    rc = get_delegate_f(hostContext, hdt_load_assembly_and_get_function_pointer, &loadAssemblyDelegate); 
    sprintf_s(buf, "get_delegate result: %d", rc);
    AppendLog(buf);
    if (rc != 0 || !loadAssemblyDelegate) { 
        AppendLog("ERROR: Failed to get load_assembly_and_get_function_pointer delegate"); 
        close_f(hostContext); 
        return rc; 
    }
    AppendLog("? Load assembly delegate obtained");

    auto load_assembly_and_get_function_pointer = (load_assembly_and_get_function_pointer_fn)loadAssemblyDelegate;
    AppendLog("Attempting to load managed assembly and resolve entry point...");
    AppendLogW(L"Assembly path: " + assemblyPath); 
    AppendLog("Type name: ForegroundBotRunner.MinimalLoader"); 
    AppendLog("Method name: TestEntry");

    const wchar_t* typeNameQualified = L"ForegroundBotRunner.MinimalLoader, ForegroundBotRunner"; 
    component_entrypoint_fn_stdcall entry_std = nullptr; 
    
    AppendLog("Calling load_assembly_and_get_function_pointer...");
    rc = load_assembly_and_get_function_pointer(assemblyPath.c_str(), typeNameQualified, L"TestEntry", nullptr, nullptr, (void**)&entry_std); 
    sprintf_s(buf, "load_assembly_and_get_function_pointer result: %d (0x%08X)", rc, rc);
    AppendLog(buf);
    
    if (rc != 0 || !entry_std) { 
        AppendLog("ERROR: Failed to resolve managed entrypoint TestEntry"); 
        sprintf_s(buf, "HRESULT: 0x%08X", rc); 
        AppendLog(buf);
        
        // Additional diagnostics
        AppendLog("Possible causes:");
        AppendLog("1. Assembly not found or corrupted");
        AppendLog("2. Type 'ForegroundBotRunner.MinimalLoader' not found");
        AppendLog("3. Method 'TestEntry' not found or wrong signature");
        AppendLog("4. Assembly dependencies missing");
        AppendLog("5. .NET runtime version mismatch");
        
        close_f(hostContext); 
        return rc; 
    }
    AppendLog("? SUCCESS: Managed entry point resolved successfully!");

    // Log pointer value
    sprintf_s(buf, "Entry pointer: 0x%p", (void*)entry_std); 
    AppendLog(buf);

    AppendLog("=== INVOKING MANAGED ENTRY POINT ===");
    AppendLog("Calling ForegroundBotRunner.MinimalLoader.TestEntry...");
    
    try {
        int r = entry_std(nullptr, 0);
        sprintf_s(buf, "? SUCCESS: Managed entry point returned: %d", r);
        AppendLog(buf);
        
        if (r == 42) {
            AppendLog("? SUCCESS: Got expected return value 42 from stdcall entry");
        } else if (r == 43) {
            AppendLog("? SUCCESS: Got expected return value 43 from cdecl entry");
        } else {
            sprintf_s(buf, "WARNING: Unexpected return value: %d", r);
            AppendLog(buf);
        }
    }
    catch (...) {
        AppendLog("ERROR: Exception thrown during managed entry point execution");
    }

    AppendLog("Cleaning up .NET runtime...");
    close_f(hostContext); 
    AppendLog("? hostfxr context closed"); 
    AppendLog("=== SUCCESS: LOADER HOST THREAD FINISHED SUCCESSFULLY ==="); 
    
    // Keep console open for a bit so we can see the results
    AppendLog("Keeping console open for 30 seconds...");
    Sleep(30000);
    
    return 0;
}

BOOL WINAPI DllMain(HMODULE hDll, DWORD reason, LPVOID)
{ 
    if (reason == DLL_PROCESS_ATTACH) { 
        DisableThreadLibraryCalls(hDll); 
        
        // Create immediate multiple log files to confirm DLL is being loaded
        try {
            // Log to solution root
            std::ofstream logFile1("C:\\Users\\WowAdmin\\source\\repos\\sethrhod\\BloogBot\\LOADER_DLL_LOADED.txt", std::ios::app);
            if (logFile1.is_open()) {
                SYSTEMTIME st; GetLocalTime(&st);
                logFile1 << "[" << st.wHour << ":" << st.wMinute << ":" << st.wSecond << "] ";
                logFile1 << "Loader.dll DLL_PROCESS_ATTACH called in process " << GetCurrentProcessId() << std::endl;
                logFile1.close();
            }
            
            // Log to temp directory as backup
            std::ofstream logFile2("C:\\Temp\\LOADER_DLL_SUCCESS.txt", std::ios::app);
            if (logFile2.is_open()) {
                SYSTEMTIME st; GetLocalTime(&st);
                logFile2 << "[" << st.wHour << ":" << st.wMinute << ":" << st.wSecond << "] ";
                logFile2 << "SUCCESS: Loader.dll loaded in process " << GetCurrentProcessId() << std::endl;
                logFile2.close();
            }
            
            // Log to current directory as backup
            std::ofstream logFile3("LOADER_SUCCESS.txt", std::ios::app);
            if (logFile3.is_open()) {
                SYSTEMTIME st; GetLocalTime(&st);
                logFile3 << "[" << st.wHour << ":" << st.wMinute << ":" << st.wSecond << "] ";
                logFile3 << "SUCCESS: Loader.dll DllMain called in process " << GetCurrentProcessId() << std::endl;
                logFile3.close();
            }
        } catch (...) {
            // Ignore any errors in logging
        }
        
        // Return the module handle as a non-zero value to indicate success to LoadLibrary
        _beginthreadex(nullptr, 0, HostThread, hDll, 0, nullptr);
    } 
    return TRUE; 
}