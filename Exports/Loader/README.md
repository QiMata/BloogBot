# Loader - .NET 8 CLR Bootstrapper DLL
# Loader

A C++ DLL injection and .NET CLR hosting library for the BloogBot project. This library provides the bridge between native Windows processes and the managed .NET bot components.

## Overview

**Loader** is a native C++ dynamic-link library (DLL) that serves as the bridge between the native Windows environment and the managed .NET 8 runtime. When injected into a target process, it initializes the .NET runtime using the **hostfxr** API and loads a managed assembly, enabling .NET 8 code to execute within the host process context.
The Loader project is a native C++ dynamic link library (DLL) that implements CLR (Common Language Runtime) hosting to execute managed .NET code within a target process. It serves as the entry point for injecting BloogBot's managed components into World of Warcraft processes.

This component is essential for the **ForegroundBotRunner** architecture, where managed bot code must run inside an external process (WoW 1.12.1) to access its memory space directly.
## Key Features

## Purpose
- **CLR Hosting**: Hosts the .NET 4.0+ runtime within a native process
- **DLL Injection**: Designed for injection into target processes (e.g., WoW.exe)
- **Managed Code Execution**: Loads and executes the `WoWActivityMember.exe` assembly
- **Debug Support**: Includes debugging capabilities with debugger detection and wait functionality
- **Thread Safety**: Runs CLR hosting in a separate thread to avoid blocking the main process

The Loader DLL provides:

1. **CLR Hosting** - Bootstraps the .NET 8 runtime inside a native process using `hostfxr.dll`
2. **Assembly Loading** - Loads and executes a managed entry point via `load_assembly_and_get_function_pointer`
3. **Debug Support** - Allocates a console window for stdout/stderr and provides debugger attachment windows
4. **Thread Management** - Creates a dedicated thread for CLR operations to avoid blocking the host process

## Architecture

```
????????????????????????????????????????????????????????????????????
?                        Host Process (WoW.exe)                    ?
????????????????????????????????????????????????????????????????????
?                                                                  ?
?   ??????????????     ???????????????     ?????????????????????  ?
?   ? Loader.dll ??????? hostfxr.dll ??????? ForegroundBot     ?  ?
?   ?  (Native)  ?     ?  (.NET 8)   ?     ? Runner.dll        ?  ?
?   ??????????????     ???????????????     ?????????????????????  ?
?          ?                                         ?             ?
?          ?                                         ?             ?
?   ??????????????                          ????????????????????? ?
?   ?  Console   ?                          ?   Bot Logic       ? ?
?   ?  Window    ?                          ?   (Memory R/W)    ? ?
?   ??????????????                          ????????????????????? ?
?                                                                  ?
????????????????????????????????????????????????????????????????????
```
### Core Components

## Technical Details
1. **DllMain Entry Point**: Standard DLL entry point that initiates the CLR loading process
2. **CLR Hosting**: Uses the .NET hosting APIs to create and manage a CLR runtime instance
3. **Assembly Execution**: Loads and executes the managed bot assembly with specific entry points

### .NET 8 Hosting (hostfxr)
### Key Definitions

Unlike .NET Framework which used `ICLRRuntimeHost::ExecuteInDefaultAppDomain`, .NET 8 uses the **hostfxr** hosting API:

1. Load `hostfxr.dll` (from .NET installation or bundled)
2. Call `hostfxr_initialize_for_runtime_config` with `runtimeconfig.json`
3. Get `load_assembly_and_get_function_pointer` delegate
4. Load assembly and get function pointer to entry method
5. Call the entry point

### Build Configuration

| Property | Value |
|----------|-------|
| Project Type | Dynamic Library (DLL) |
| Platform Toolset | v143 (Visual Studio 2022) |
| Character Set | Unicode |
| C++ Standard | C++17 (Win32), C++20 (x64) |
| Target Platforms | Win32 (for 32-bit WoW), x64 |

### Output Locations

| Configuration | Output Path |
|---------------|-------------|
| Debug (Win32) | `..\..\Bot\Debug\net8.0\Loader.dll` |
| Release (Win32) | `..\Bot\Release\net8.0\Loader.dll` |

### Dependencies

- **Windows SDK 10.0** - Core Windows APIs
- **hostfxr.dll** - .NET 8 hosting library (from .NET installation or bundled)
- **ForegroundBotRunner.runtimeconfig.json** - Runtime configuration

### Key APIs Used

| API | Purpose |
|-----|---------|
| `hostfxr_initialize_for_runtime_config` | Initialize host context with runtime config |
| `hostfxr_get_runtime_delegate` | Get CLR delegate for assembly loading |
| `load_assembly_and_get_function_pointer` | Load assembly and get entry point |
| `hostfxr_close` | Clean up host context |
| `AllocConsole` | Creates debug console window |
| `_beginthreadex` | Creates CLR bootstrap thread |

## Configuration Constants

The following constants in `dllmain.cpp` control the loader behavior:

```cpp
#define MANAGED_ASSEMBLY_NAME      L"ForegroundBotRunner"
#define MANAGED_ASSEMBLY_DLL       L"ForegroundBotRunner.dll"
#define MANAGED_RUNTIME_CONFIG     L"ForegroundBotRunner.runtimeconfig.json"
#define MANAGED_TYPE_NAME          L"ForegroundBotRunner.Loader, ForegroundBotRunner"
#define MANAGED_METHOD_NAME        L"Load"
#define LOAD_DLL_FILE_NAME L"WoWActivityMember.exe"    // Target managed assembly
#define NAMESPACE_AND_CLASS L"WoWActivityMember.Loader" // Entry class
#define MAIN_METHOD L"Load"                             // Entry method
#define MAIN_METHOD_ARGS L"NONE"                        // Method arguments
```

## Execution Flow
## Technical Details

### 1. DLL Attachment (`DllMain`)
### CLR Version Support
- Configured for .NET 4.0+ (`FOR_DOTNET_4` defined)
- Falls back to legacy v2 runtime binding for compatibility with .NET 3.5 mixed-mode assemblies
- Uses `ICLRMetaHostPolicy` for runtime selection

```
DLL_PROCESS_ATTACH
    ?
    ?
Store module handle
    ?
    ?
Disable thread library calls
    ?
    ?
Create bootstrap thread (ThreadMain)
```
### Compilation Settings
- **Platform**: x86 and x64 support
- **Language Standard**: C++17 (Debug), C++20 (Release)
- **Character Set**: Unicode
- **Output**: Dynamic Link Library (.dll)
- **Target Platforms**: Windows 10+

### 2. .NET 8 Bootstrap (`ThreadMain`)
### Build Configurations

```
ThreadMain()
    ?
    ?
AllocConsole() - Create debug console
    ?
    ?
[DEBUG] Wait 10 seconds for debugger attachment
    ?
    ?
Find hostfxr.dll (local ? nethost ? system install)
    ?
    ?
Load hostfxr functions
    ?
    ?
hostfxr_initialize_for_runtime_config()
    ?
    ?
hostfxr_get_runtime_delegate(load_assembly_and_get_function_pointer)
    ?
    ?
load_assembly_and_get_function_pointer() - Get entry point
    ?
    ?
Call managed entry point
    ?
    ?
[Managed code runs in separate thread]
```
| Configuration | Platform | Output Directory | Features |
|---------------|----------|------------------|----------|
| Debug | x86/x64 | `..\..\Bot\Debug\net8.0` | Console output, debugger support |
| Release | x86/x64 | `..\..\Bot\Release\net8.0` | Optimized, no debug info |

### 3. DLL Detachment

```
DLL_PROCESS_DETACH
    ?
    ?
hostfxr_close() - Clean up host context
    ?
    ?
Unload hostfxr.dll
    ?
    ?
Wait for and close bootstrap thread
```

## Managed Entry Point Contract

The managed assembly must expose a static method matching the **ComponentEntryPoint** delegate:

```csharp
namespace ForegroundBotRunner
{
    public class Loader
    {
        // Signature: public delegate int ComponentEntryPoint(IntPtr args, int sizeBytes);
        public static int Load(IntPtr args, int sizeBytes)
        {
            // Start your application
            // Return 0 for success, non-zero for error
            return 0;
        }
    }
}
```
## Usage

**Requirements:**
- Method must be `public static`
- Signature: `int MethodName(IntPtr args, int sizeBytes)`
- Return 0 for success
- Type name must include assembly name: `"Namespace.Class, AssemblyName"`
### Injection Process
1. The DLL is injected into the target process (typically WoW.exe)
2. `DllMain` is called with `DLL_PROCESS_ATTACH`
3. `LoadClr()` function is invoked to start the CLR hosting process
4. A separate thread (`ThreadMain`) is created to handle CLR operations
5. The managed assembly `WoWActivityMember.exe` is located and loaded
6. The `WoWActivityMember.Loader.Load` method is executed

## Required Files
### Debug Mode Features
- Allocates a console for debug output
- Provides a 10-second window for debugger attachment
- Outputs debugger detection status
- Waits for manual debugger attachment if needed

For the loader to work, these files must be in the same directory:
## File Structure

```
Bot/Debug/net8.0/
??? Loader.dll                              # This native loader
??? ForegroundBotRunner.dll                 # Managed assembly
??? ForegroundBotRunner.runtimeconfig.json  # Runtime configuration
??? ForegroundBotRunner.deps.json           # Dependencies
??? hostfxr.dll                             # (Optional) Bundled runtime
??? [other dependencies...]
```

### runtimeconfig.json

Generated automatically by the .NET SDK. Example:

```json
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "8.0.0"
    },
    "rollForward": "LatestMinor"
  }
}
Exports/Loader/
??? Loader.vcxproj          # Visual Studio project file
??? Loader.vcxproj.filters  # Project filters
??? dllmain.cpp            # Main implementation
??? targetver.h            # Windows version targeting (shared)
??? README.md              # This file
```

## Debug Mode Features

When compiled in **Debug** configuration:

1. **Debugger Attachment Window**
   - Waits 10 seconds for debugger attachment
   - Reports attachment status

2. **Verbose Console Output**
   - All initialization steps logged
   - Error codes displayed with descriptions
   - hostfxr error callback enabled
## Dependencies

### Attaching a Debugger
### System Libraries
- `mscoree.lib` - .NET CLR hosting
- `kernel32.lib` - Windows API
- Standard C++ runtime libraries

1. Inject Loader.dll into target process
2. Wait for console: "Attach a debugger now..."
3. In Visual Studio: **Debug ? Attach to Process ? [WoW.exe]**
4. Check "Managed (.NET Core)" in code type
5. Set breakpoints in managed code
6. Wait for 10-second window to expire
### Headers
- `metahost.h` - .NET 4.0+ hosting APIs
- `CorError.h` - CLR error definitions
- `Windows.h` - Windows API
- `process.h` - Thread management

## Error Handling

### Common Errors

| Error Code | Description | Solution |
|------------|-------------|----------|
| `0x80008083` | Framework missing | Install .NET 8 Desktop Runtime (x86) |
| `0x80131522` | Type not found | Verify MANAGED_TYPE_NAME includes assembly |
| `0x80131523` | Method not found | Check method signature matches ComponentEntryPoint |
| Runtime config not found | Missing .runtimeconfig.json | Build ForegroundBotRunner project |
| hostfxr.dll not found | .NET not installed | Install .NET 8 or bundle hostfxr.dll |

### Finding hostfxr.dll

The loader searches in this order:
1. Same directory as Loader.dll
2. `runtime/` subdirectory
3. Via `nethost.dll` if present
4. System .NET installation (`C:\Program Files\dotnet\...`)

## Building

### Prerequisites

- Visual Studio 2022 with C++ Desktop Development workload
- Windows 10/11 SDK
- .NET 8 SDK (for building ForegroundBotRunner)

### Build Steps

1. Open `BloogBot.sln` in Visual Studio 2022
2. Select **Debug|Win32** configuration (for 32-bit WoW)
3. Build the Loader project
4. Build ForegroundBotRunner project
5. Verify both DLLs and runtimeconfig.json are in output

### Build Verification

```powershell
# Check output directory
ls Bot\Debug\net8.0\

# Should contain:
# - Loader.dll
# - ForegroundBotRunner.dll
# - ForegroundBotRunner.runtimeconfig.json
# - ForegroundBotRunner.deps.json
```

## Troubleshooting

### "Framework missing" error
- Install **.NET 8 Desktop Runtime (x86)** for 32-bit WoW
- Or bundle the runtime with the application
The loader includes comprehensive error handling for common CLR hosting failures:

### Console appears but managed code doesn't start
- Verify `ForegroundBotRunner.runtimeconfig.json` exists
- Check console output for specific error codes
- Ensure all dependencies are present
- CLR instance creation failures
- Runtime binding errors
- Assembly loading failures
- Execution domain errors

### Type or method not found
- Type name must be: `"Namespace.ClassName, AssemblyName"`
- Method signature must match: `public static int Load(IntPtr, int)`
Error messages are displayed via Windows message boxes with specific error codes for debugging.

### 32-bit vs 64-bit
- WoW 1.12.1 is **32-bit** - use Win32 configuration
- Loader.dll bitness must match target process
- .NET 8 x86 runtime must be installed
## Integration with BloogBot

## File Structure
This loader is a critical component of the BloogBot architecture:
- Enables managed C# bot logic to run within the WoW process
- Provides the bridge for memory reading/writing operations
- Allows real-time game state monitoring and bot decision making

```
Exports/Loader/
??? Loader.vcxproj           # Visual Studio C++ project
??? dllmain.cpp              # Main source - .NET 8 hosting
??? nethost_helpers.h        # hostfxr types and utilities
??? README.md                # This documentation
```
## Building

## Related Components
1. Open the solution in Visual Studio 2022
2. Ensure the Windows 10 SDK is installed
3. Select the desired configuration (Debug/Release) and platform (x86/x64)
4. Build the project

| Component | Relationship |
|-----------|--------------|
| **ForegroundBotRunner** | Managed assembly loaded by Loader |
| **FastCall.dll** | Native helper for x86 calling conventions |
| **WinProcessImports** | C# P/Invoke wrappers for injection |
| **Navigation.dll** | Native pathfinding/physics engine |
The output DLL will be placed in the `Bot\[Configuration]\net8.0` directory alongside other BloogBot components.

## Version History
## Notes

| Version | Changes |
|---------|---------|
| 1.0 | Initial .NET Framework 4.x implementation |
| 2.0 | **Complete rewrite for .NET 8 using hostfxr API** |
| 2.1 | Added nethost fallback and improved error diagnostics |
- This DLL must be compiled for the same architecture as the target process
- The managed assembly `WoWActivityMember.exe` must be present in the same directory
- CLR hosting is limited to one instance per process
- Thread termination is handled aggressively during DLL unload to prevent hangs

---
## Security Considerations

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
- This DLL is designed for game automation and requires injection into another process
- Use only with software you own or have explicit permission to modify
- Antivirus software may flag this as potentially unwanted due to injection capabilities