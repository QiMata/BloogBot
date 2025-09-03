# WinProcessImports

A .NET 8 library providing Windows API P/Invoke declarations for process and memory management operations. This library serves as a foundational component for the BloogBot project, enabling managed C# code to interact with native Windows APIs for process manipulation, memory allocation, and DLL injection.

## Overview

WinProcessImports is a lightweight wrapper around essential Windows kernel32.dll functions, providing type-safe managed interfaces for process operations commonly used in game automation and system interaction scenarios.

## Key Features

- **Process Management**: Create, open, and manage Windows processes
- **Memory Operations**: Allocate, write, and manage memory in external processes
- **DLL Operations**: Load libraries and retrieve function addresses
- **Thread Management**: Create and manage remote threads
- **Console Operations**: Allocate consoles for debugging
- **Type Safety**: Strongly-typed enumerations and structures for Windows constants

## Core Functionality

### Process Operations
- `CreateProcess()` - Create new processes with full parameter control
- `OpenProcess()` - Open existing processes for manipulation
- `CloseProcess()` - Safe process handle cleanup with error handling

### Memory Management
- `VirtualAllocEx()` - Allocate memory in remote processes
- `WriteProcessMemory()` - Write data to remote process memory
- `VirtualFreeEx()` - Free allocated memory in remote processes

### DLL and Threading
- `LoadLibrary()` / `LoadLibraryEx()` - Load dynamic libraries
- `GetProcAddress()` / `GetModuleHandle()` - Retrieve function addresses
- `CreateRemoteThread()` - Execute code in remote processes
- `WaitForSingleObject()` - Wait for thread completion

### Utility Functions
- `SetDllDirectory()` - Configure DLL search paths
- `AllocConsole()` - Create debug consoles

## Technical Specifications

- **Target Framework**: .NET 8.0
- **Language Features**: C# 12 with implicit usings and nullable reference types
- **Unsafe Code**: Enabled for native interop operations
- **Platform**: AnyCPU with x64 support
- **Output**: Class library (.dll)

## Project Configuration

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <BaseOutputPath>..\..\Bot</BaseOutputPath>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

## Data Structures

### Core Structures
- `STARTUPINFO` - Process startup configuration
- `PROCESS_INFORMATION` - Process creation results

### Enumerations
- `LoadLibraryFlags` - Library loading options
- `MemoryAllocationType` - Memory allocation types (MEM_COMMIT, MEM_RESERVE)
- `MemoryProtectionType` - Memory protection settings (PAGE_EXECUTE_READWRITE)
- `MemoryFreeType` - Memory deallocation options (MEM_RELEASE)
- `ProcessCreationFlag` - Process creation flags

## Usage Examples

### Creating a Process
```csharp
var startupInfo = new WinProcessImports.STARTUPINFO();
var processInfo = new WinProcessImports.PROCESS_INFORMATION();

bool success = WinProcessImports.CreateProcess(
    applicationName: "notepad.exe",
    commandLine: null,
    lpProcessAttributes: IntPtr.Zero,
    lpThreadAttributes: IntPtr.Zero,
    bInheritHandles: false,
    dwCreationFlags: WinProcessImports.ProcessCreationFlag.CREATE_DEFAULT_ERROR_MODE,
    lpEnvironment: IntPtr.Zero,
    lpCurrentDirectory: null,
    lpStartupInfo: ref startupInfo,
    lpProcessInformation: out processInfo
);
```

### Memory Allocation in Remote Process
```csharp
IntPtr processHandle = WinProcessImports.OpenProcess(
    processAccess: 0x1F0FFF, // PROCESS_ALL_ACCESS
    bInheritHandle: false,
    processId: targetProcessId
);

IntPtr allocatedMemory = WinProcessImports.VirtualAllocEx(
    hProcess: processHandle,
    dwAddress: IntPtr.Zero,
    nSize: 1024,
    dwAllocationType: WinProcessImports.MemoryAllocationType.MEM_COMMIT | WinProcessImports.MemoryAllocationType.MEM_RESERVE,
    dwProtect: WinProcessImports.MemoryProtectionType.PAGE_EXECUTE_READWRITE
);
```

### Safe Process Cleanup
```csharp
try 
{
    WinProcessImports.CloseProcess(processHandle);
}
catch (System.ComponentModel.Win32Exception ex)
{
    // Handle cleanup errors
    Console.WriteLine($"Failed to close process: {ex.Message}");
}
```

## Integration with BloogBot

This library is a critical dependency for several BloogBot components:

- **PathfindingService**: Uses process APIs for game memory access
- **Loader**: Leverages DLL injection capabilities
- **Bot Services**: Requires memory management for game state reading

## Error Handling

The library includes comprehensive error handling:
- Win32 error code propagation through `Marshal.GetLastWin32Error()`
- Structured exception handling in `CloseProcess()` helper method
- SetLastError support for all P/Invoke declarations

## Security Considerations

?? **Important Security Notes**:
- This library enables process injection and memory manipulation
- Intended for legitimate game automation and testing purposes
- Use only with software you own or have explicit permission to modify
- Antivirus software may flag applications using these APIs
- Requires elevated privileges for some operations

## Dependencies

- **System.Runtime.InteropServices** - P/Invoke marshaling
- **System.ComponentModel** - Win32Exception handling
- **.NET 8 Runtime** - Modern .NET runtime features

## Building

1. Ensure .NET 8.0 SDK is installed
2. Open the solution in Visual Studio 2022 or use CLI:
   ```bash
   dotnet build Exports/WinImports/WinProcessImports.csproj
   ```
3. Output will be placed in `..\..\Bot\` directory

## Compatibility

- **Windows Versions**: Windows 10+ (due to kernel32.dll dependencies)
- **Architectures**: x86, x64, AnyCPU
- **Runtime**: .NET 8.0+

## Contributing

When extending this library:
1. Follow existing P/Invoke patterns
2. Include proper error handling with `SetLastError = true`
3. Use appropriate marshaling attributes
4. Document security implications of new APIs
5. Ensure compatibility with the BloogBot architecture

## License

This library is part of the BloogBot project. Use in accordance with project licensing terms.