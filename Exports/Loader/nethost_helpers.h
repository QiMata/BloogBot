// nethost_helpers.h - Helper types and functions for .NET 8 hosting
// Based on Microsoft's native hosting sample
#pragma once

#include <Windows.h>
#include <string>
#include <iostream>
#include <sstream>
#include <filesystem>

// Calling conventions - must be defined BEFORE typedefs
#ifndef HOSTFXR_CALLTYPE
#ifdef _WIN32
#define HOSTFXR_CALLTYPE __cdecl
#else
#define HOSTFXR_CALLTYPE
#endif
#endif

#ifndef CORECLR_DELEGATE_CALLTYPE
#ifdef _WIN32
#define CORECLR_DELEGATE_CALLTYPE __stdcall
#else
#define CORECLR_DELEGATE_CALLTYPE
#endif
#endif

// .NET hosting function pointer types
// These match the signatures in hostfxr.h and coreclr_delegates.h

// hostfxr function types
typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_initialize_for_runtime_config_fn)(
    const wchar_t* runtime_config_path,
    const void* parameters,
    void** host_context_handle);

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_get_runtime_delegate_fn)(
    const void* host_context_handle,
    int32_t type,
    void** delegate);

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_close_fn)(
    const void* host_context_handle);

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_set_error_writer_fn)(
    void* error_writer);

// coreclr delegate types
typedef int32_t(CORECLR_DELEGATE_CALLTYPE* load_assembly_and_get_function_pointer_fn)(
    const wchar_t* assembly_path,
    const wchar_t* type_name,
    const wchar_t* method_name,
    const wchar_t* delegate_type_name,
    void* reserved,
    void** delegate);

typedef int32_t(CORECLR_DELEGATE_CALLTYPE* component_entry_point_fn)(
    void* arg,
    int32_t arg_size_in_bytes);

// Delegate type enumeration
enum hostfxr_delegate_type
{
    hdt_com_activation = 0,
    hdt_load_in_memory_assembly = 1,
    hdt_winrt_activation = 2,
    hdt_com_register = 3,
    hdt_com_unregister = 4,
    hdt_load_assembly_and_get_function_pointer = 5,
    hdt_get_function_pointer = 6,
};

// Log levels for diagnostic output
enum class LogLevel
{
    Debug,
    Info,
    Warning,
    Error
};

// File-based logging for debugging when console isn't visible
inline std::wstring GetLoaderLogPath()
{
    wchar_t modulePath[MAX_PATH];
    if (GetModuleFileNameW(NULL, modulePath, MAX_PATH))
    {
        std::wstring path(modulePath);
        size_t lastSlash = path.find_last_of(L'\\');
        if (lastSlash != std::wstring::npos)
            return path.substr(0, lastSlash + 1) + L"loader_debug.log";
    }
    return L"C:\\loader_debug.log";
}

// Simple logging helper - writes to both console and file
inline void LogMessage(LogLevel level, const std::wstring& message)
{
    const wchar_t* prefix = L"";
    switch (level)
    {
    case LogLevel::Debug:   prefix = L"[DEBUG] "; break;
    case LogLevel::Info:    prefix = L"[INFO]  "; break;
    case LogLevel::Warning: prefix = L"[WARN]  "; break;
    case LogLevel::Error:   prefix = L"[ERROR] "; break;
    }

    // Console output
    std::wcout << prefix << message << std::endl;

    // File output for debugging
    static std::wstring logPath = GetLoaderLogPath();
    FILE* logFile = nullptr;
    if (_wfopen_s(&logFile, logPath.c_str(), L"a") == 0 && logFile)
    {
        fwprintf(logFile, L"%s%s\n", prefix, message.c_str());
        fclose(logFile);
    }
}

inline void LogMessage(LogLevel level, const std::string& message)
{
    std::wstring wmsg(message.begin(), message.end());
    LogMessage(level, wmsg);
}

// Error writer callback for hostfxr
inline void HOSTFXR_CALLTYPE hostfxr_error_writer(const wchar_t* message)
{
    std::wcerr << L"[hostfxr] " << message << std::endl;
}

// Find the hostfxr.dll path
// First tries to find it via nethost.dll, falls back to searching common locations
inline bool FindHostFxrPath(std::wstring& outPath, const std::wstring& baseDir)
{
    // Strategy 1: Look for hostfxr.dll in a known .NET runtime installation
    // For injected scenarios, we bundle the runtime with our app
    
    // Check if hostfxr.dll is next to our DLL (self-contained deployment)
    std::wstring localPath = baseDir + L"hostfxr.dll";
    if (std::filesystem::exists(localPath))
    {
        outPath = localPath;
        LogMessage(LogLevel::Info, L"Found hostfxr.dll at: " + outPath);
        return true;
    }
    
    // Check in a 'runtime' subdirectory
    std::wstring runtimePath = baseDir + L"runtime\\hostfxr.dll";
    if (std::filesystem::exists(runtimePath))
    {
        outPath = runtimePath;
        LogMessage(LogLevel::Info, L"Found hostfxr.dll at: " + outPath);
        return true;
    }
    
    // Strategy 2: Try to load nethost.dll and use get_hostfxr_path
    HMODULE nethostLib = LoadLibraryW(L"nethost.dll");
    if (!nethostLib)
    {
        // Try local nethost.dll
        std::wstring localNethost = baseDir + L"nethost.dll";
        nethostLib = LoadLibraryW(localNethost.c_str());
    }
    
    if (nethostLib)
    {
        typedef int32_t(HOSTFXR_CALLTYPE* get_hostfxr_path_fn)(
            wchar_t* buffer,
            size_t* buffer_size,
            const void* parameters);
        
        auto get_hostfxr_path = (get_hostfxr_path_fn)GetProcAddress(nethostLib, "get_hostfxr_path");
        if (get_hostfxr_path)
        {
            wchar_t buffer[MAX_PATH];
            size_t bufferSize = MAX_PATH;
            
            int32_t rc = get_hostfxr_path(buffer, &bufferSize, nullptr);
            if (rc == 0)
            {
                outPath = buffer;
                LogMessage(LogLevel::Info, L"Found hostfxr.dll via nethost: " + outPath);
                FreeLibrary(nethostLib);
                return true;
            }
        }
        FreeLibrary(nethostLib);
    }
    
    // Strategy 3: Search common .NET installation paths
    // For 32-bit processes (like WoW), prefer x86 .NET installation first
#ifdef _M_IX86
    const wchar_t* dotnetPaths[] = {
        L"C:\\Program Files (x86)\\dotnet\\host\\fxr\\",  // x86 first for 32-bit
        L"C:\\Program Files\\dotnet\\host\\fxr\\"
    };
#else
    const wchar_t* dotnetPaths[] = {
        L"C:\\Program Files\\dotnet\\host\\fxr\\",        // x64 first for 64-bit
        L"C:\\Program Files (x86)\\dotnet\\host\\fxr\\"
    };
#endif
    
    for (const auto& dotnetPath : dotnetPaths)
    {
        if (!std::filesystem::exists(dotnetPath))
            continue;
            
        // Find the highest version directory
        std::wstring highestVersion;
        for (const auto& entry : std::filesystem::directory_iterator(dotnetPath))
        {
            if (entry.is_directory())
            {
                std::wstring version = entry.path().filename().wstring();
                if (highestVersion.empty() || version > highestVersion)
                    highestVersion = version;
            }
        }
        
        if (!highestVersion.empty())
        {
            std::wstring fxrPath = std::wstring(dotnetPath) + highestVersion + L"\\hostfxr.dll";
            if (std::filesystem::exists(fxrPath))
            {
                outPath = fxrPath;
                LogMessage(LogLevel::Info, L"Found hostfxr.dll at: " + outPath);
                return true;
            }
        }
    }
    
    LogMessage(LogLevel::Error, L"Could not find hostfxr.dll");
    return false;
}

// Load hostfxr and get required function pointers
struct HostFxrFunctions
{
    hostfxr_initialize_for_runtime_config_fn initialize;
    hostfxr_get_runtime_delegate_fn get_delegate;
    hostfxr_close_fn close;
    hostfxr_set_error_writer_fn set_error_writer;
    HMODULE module;
    
    bool IsValid() const
    {
        return module != nullptr && initialize != nullptr && 
               get_delegate != nullptr && close != nullptr;
    }
    
    void Unload()
    {
        if (module)
        {
            FreeLibrary(module);
            module = nullptr;
        }
        initialize = nullptr;
        get_delegate = nullptr;
        close = nullptr;
        set_error_writer = nullptr;
    }
};

inline bool LoadHostFxr(const std::wstring& hostfxrPath, HostFxrFunctions& funcs)
{
    funcs.module = LoadLibraryW(hostfxrPath.c_str());
    if (!funcs.module)
    {
        DWORD error = GetLastError();
        std::wstringstream ss;
        ss << L"Failed to load hostfxr.dll. Error: " << error;
        LogMessage(LogLevel::Error, ss.str());
        return false;
    }
    
    funcs.initialize = (hostfxr_initialize_for_runtime_config_fn)
        GetProcAddress(funcs.module, "hostfxr_initialize_for_runtime_config");
    funcs.get_delegate = (hostfxr_get_runtime_delegate_fn)
        GetProcAddress(funcs.module, "hostfxr_get_runtime_delegate");
    funcs.close = (hostfxr_close_fn)
        GetProcAddress(funcs.module, "hostfxr_close");
    funcs.set_error_writer = (hostfxr_set_error_writer_fn)
        GetProcAddress(funcs.module, "hostfxr_set_error_writer");
    
    if (!funcs.IsValid())
    {
        LogMessage(LogLevel::Error, L"Failed to get hostfxr function pointers");
        funcs.Unload();
        return false;
    }
    
    // Set error writer for better diagnostics
    if (funcs.set_error_writer)
    {
        funcs.set_error_writer((void*)hostfxr_error_writer);
    }
    
    LogMessage(LogLevel::Info, L"Successfully loaded hostfxr functions");
    return true;
}
