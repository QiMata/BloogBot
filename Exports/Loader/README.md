# Loader

A C++ DLL injection and .NET CLR hosting library for the BloogBot project. This library provides the bridge between native Windows processes and the managed .NET bot components.

## Overview

The Loader project is a native C++ dynamic link library (DLL) that implements CLR (Common Language Runtime) hosting to execute managed .NET code within a target process. It serves as the entry point for injecting BloogBot's managed components into World of Warcraft processes.

## Key Features

- **CLR Hosting**: Hosts the .NET 4.0+ runtime within a native process
- **DLL Injection**: Designed for injection into target processes (e.g., WoW.exe)
- **Managed Code Execution**: Loads and executes the `WoWActivityMember.exe` assembly
- **Debug Support**: Includes debugging capabilities with debugger detection and wait functionality
- **Thread Safety**: Runs CLR hosting in a separate thread to avoid blocking the main process

## Architecture

### Core Components

1. **DllMain Entry Point**: Standard DLL entry point that initiates the CLR loading process
2. **CLR Hosting**: Uses the .NET hosting APIs to create and manage a CLR runtime instance
3. **Assembly Execution**: Loads and executes the managed bot assembly with specific entry points

### Key Definitions

```cpp
#define LOAD_DLL_FILE_NAME L"WoWActivityMember.exe"    // Target managed assembly
#define NAMESPACE_AND_CLASS L"WoWActivityMember.Loader" // Entry class
#define MAIN_METHOD L"Load"                             // Entry method
#define MAIN_METHOD_ARGS L"NONE"                        // Method arguments
```

## Technical Details

### CLR Version Support
- Configured for .NET 4.0+ (`FOR_DOTNET_4` defined)
- Falls back to legacy v2 runtime binding for compatibility with .NET 3.5 mixed-mode assemblies
- Uses `ICLRMetaHostPolicy` for runtime selection

### Compilation Settings
- **Platform**: x86 and x64 support
- **Language Standard**: C++17 (Debug), C++20 (Release)
- **Character Set**: Unicode
- **Output**: Dynamic Link Library (.dll)
- **Target Platforms**: Windows 10+

### Build Configurations

| Configuration | Platform | Output Directory | Features |
|---------------|----------|------------------|----------|
| Debug | x86/x64 | `..\..\Bot\Debug\net8.0` | Console output, debugger support |
| Release | x86/x64 | `..\..\Bot\Release\net8.0` | Optimized, no debug info |

## Usage

### Injection Process
1. The DLL is injected into the target process (typically WoW.exe)
2. `DllMain` is called with `DLL_PROCESS_ATTACH`
3. `LoadClr()` function is invoked to start the CLR hosting process
4. A separate thread (`ThreadMain`) is created to handle CLR operations
5. The managed assembly `WoWActivityMember.exe` is located and loaded
6. The `WoWActivityMember.Loader.Load` method is executed

### Debug Mode Features
- Allocates a console for debug output
- Provides a 10-second window for debugger attachment
- Outputs debugger detection status
- Waits for manual debugger attachment if needed

## File Structure

```
Exports/Loader/
??? Loader.vcxproj          # Visual Studio project file
??? Loader.vcxproj.filters  # Project filters
??? dllmain.cpp            # Main implementation
??? targetver.h            # Windows version targeting (shared)
??? README.md              # This file
```

## Dependencies

### System Libraries
- `mscoree.lib` - .NET CLR hosting
- `kernel32.lib` - Windows API
- Standard C++ runtime libraries

### Headers
- `metahost.h` - .NET 4.0+ hosting APIs
- `CorError.h` - CLR error definitions
- `Windows.h` - Windows API
- `process.h` - Thread management

## Error Handling

The loader includes comprehensive error handling for common CLR hosting failures:

- CLR instance creation failures
- Runtime binding errors
- Assembly loading failures
- Execution domain errors

Error messages are displayed via Windows message boxes with specific error codes for debugging.

## Integration with BloogBot

This loader is a critical component of the BloogBot architecture:
- Enables managed C# bot logic to run within the WoW process
- Provides the bridge for memory reading/writing operations
- Allows real-time game state monitoring and bot decision making

## Building

1. Open the solution in Visual Studio 2022
2. Ensure the Windows 10 SDK is installed
3. Select the desired configuration (Debug/Release) and platform (x86/x64)
4. Build the project

The output DLL will be placed in the `Bot\[Configuration]\net8.0` directory alongside other BloogBot components.

## Notes

- This DLL must be compiled for the same architecture as the target process
- The managed assembly `WoWActivityMember.exe` must be present in the same directory
- CLR hosting is limited to one instance per process
- Thread termination is handled aggressively during DLL unload to prevent hangs

## Security Considerations

- This DLL is designed for game automation and requires injection into another process
- Use only with software you own or have explicit permission to modify
- Antivirus software may flag this as potentially unwanted due to injection capabilities