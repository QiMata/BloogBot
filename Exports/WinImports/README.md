# WinProcessImports - Windows API P/Invoke Library
# WinProcessImports

A .NET 8 library providing Windows API P/Invoke declarations for process and memory management operations. This library serves as a foundational component for the BloogBot project, enabling managed C# code to interact with native Windows APIs for process manipulation, memory allocation, and DLL injection.

## Overview

**WinProcessImports** is a .NET class library providing Platform Invocation Services (P/Invoke) declarations for Windows API functions. It serves as the foundation for process manipulation, memory access, and DLL injection operations in the WWoW system.
WinProcessImports is a lightweight wrapper around essential Windows kernel32.dll functions, providing type-safe managed interfaces for process operations commonly used in game automation and system interaction scenarios.

This library enables managed C# code to interact with low-level Windows APIs from `kernel32.dll`, making it essential for the **ForegroundBotRunner** injection pipeline and process management.
## Key Features

## Purpose
- **Process Management**: Create, open, and manage Windows processes
- **Memory Operations**: Allocate, write, and manage memory in external processes
- **DLL Operations**: Load libraries and retrieve function addresses
- **Thread Management**: Create and manage remote threads
- **Console Operations**: Allocate consoles for debugging
- **Type Safety**: Strongly-typed enumerations and structures for Windows constants

WinProcessImports provides:
## Core Functionality

1. **Process Management** - Create, open, and manage Windows processes
2. **Memory Operations** - Allocate, read, write, and free virtual memory
3. **Thread Control** - Create and synchronize remote threads
4. **Module Management** - Load DLLs and resolve function addresses
5. **Console Management** - Allocate console windows for debug output
### Process Operations
- `CreateProcess()` - Create new processes with full parameter control
- `OpenProcess()` - Open existing processes for manipulation
- `CloseProcess()` - Safe process handle cleanup with error handling

## Architecture
### Memory Management
- `VirtualAllocEx()` - Allocate memory in remote processes
- `WriteProcessMemory()` - Write data to remote process memory
- `VirtualFreeEx()` - Free allocated memory in remote processes

```
???????????????????????????????????????????????????????????????????
?                     Managed C# Code                              ?
?         (ForegroundBotRunner, Bootstrapper, etc.)               ?
???????????????????????????????????????????????????????????????????
?                                                                  ?
?                    WinProcessImports                             ?
?                  (P/Invoke Declarations)                         ?
?                                                                  ?
?   ????????????????  ????????????????  ????????????????????????  ?
?   ?   Process    ?  ?    Memory    ?  ?      Thread          ?  ?
?   ?  Management  ?  ?  Operations  ?  ?     Control          ?  ?
?   ?              ?  ?              ?  ?                      ?  ?
?   ? CreateProcess?  ?VirtualAllocEx?  ?CreateRemoteThread    ?  ?
?   ? OpenProcess  ?  ?WriteProcess  ?  ?WaitForSingleObject   ?  ?
?   ? CloseHandle  ?  ?  Memory      ?  ?                      ?  ?
?   ????????????????  ?VirtualFreeEx ?  ????????????????????????  ?
?                     ????????????????                             ?
?                            ?                                     ?
????????????????????????????????????????????????????????????????????
                             ?
                             ?
???????????????????????????????????????????????????????????????????
?                      kernel32.dll                                ?
?                   (Windows System DLL)                           ?
???????????????????????????????????????????????????????????????????
```
### DLL and Threading
- `LoadLibrary()` / `LoadLibraryEx()` - Load dynamic libraries
- `GetProcAddress()` / `GetModuleHandle()` - Retrieve function addresses
- `CreateRemoteThread()` - Execute code in remote processes
- `WaitForSingleObject()` - Wait for thread completion

## Technical Details
### Utility Functions
- `SetDllDirectory()` - Configure DLL search paths
- `AllocConsole()` - Create debug consoles

### Build Configuration
## Technical Specifications

| Property | Value |
|----------|-------|
| Target Framework | .NET 8.0 |
| Output Type | Class Library |
| Nullable | Enabled |
| Implicit Usings | Enabled |
| Unsafe Blocks | Allowed |
- **Target Framework**: .NET 8.0
- **Language Features**: C# 12 with implicit usings and nullable reference types
- **Unsafe Code**: Enabled for native interop operations
- **Platform**: AnyCPU with x64 support
- **Output**: Class library (.dll)

### Output Location
## Project Configuration

```
Bot/{Configuration}/net8.0/WinProcessImports.dll
```

### Dependencies

**None** - This is a pure P/Invoke declaration library with no external package dependencies.

## API Reference

### Process Management

#### CreateProcess
Creates a new process and its primary thread.

```csharp
[DllImport("kernel32.dll")]
public static extern bool CreateProcess(
    string lpApplicationName,
    string lpCommandLine,
    IntPtr lpProcessAttributes,
    IntPtr lpThreadAttributes,
    bool bInheritHandles,
    ProcessCreationFlag dwCreationFlags,
    IntPtr lpEnvironment,
    string lpCurrentDirectory,
    ref STARTUPINFO lpStartupInfo,
    out PROCESS_INFORMATION lpProcessInformation);
```

#### OpenProcess
Opens an existing process with the specified access rights.

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr OpenProcess(
    uint processAccess, 
    bool bInheritHandle, 
    int processId);
```

#### CloseHandle
Closes an open object handle.

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool CloseHandle(IntPtr hObject);
```

#### CloseProcess (Helper)
Safe wrapper for closing process handles with error handling.

```csharp
public static void CloseProcess(IntPtr processHandle)
{
    if (processHandle != IntPtr.Zero)
    {
        if (!CloseHandle(processHandle))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
}
```

### Memory Operations

#### VirtualAllocEx
Allocates memory in the virtual address space of a target process.

```csharp
[DllImport("kernel32.dll")]
public static extern IntPtr VirtualAllocEx(
    IntPtr hProcess,
    IntPtr dwAddress,
    int nSize,
    MemoryAllocationType dwAllocationType,
    MemoryProtectionType dwProtect);
```

#### WriteProcessMemory
Writes data to memory in a target process.

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool WriteProcessMemory(
    IntPtr hProcess, 
    IntPtr lpBaseAddress,
    byte[] lpBuffer, 
    uint nSize, 
    out IntPtr lpNumberOfBytesWritten);
```

#### VirtualFreeEx
Frees previously allocated memory in a target process.

```csharp
[DllImport("kernel32.dll")]
public static extern bool VirtualFreeEx(
    IntPtr hProcess,
    IntPtr dwAddress,
    int nSize,
    MemoryFreeType dwFreeType);
```

### Thread Operations

#### CreateRemoteThread
Creates a thread that runs in the virtual address space of another process.

```csharp
[DllImport("kernel32.dll")]
public static extern IntPtr CreateRemoteThread(
    IntPtr hProcess, 
    IntPtr lpThreadAttributes,
    uint dwStackSize, 
    IntPtr lpStartAddress, 
    IntPtr lpParameter, 
    uint dwCreationFlags, 
    out IntPtr lpThreadId);
```

#### WaitForSingleObject
Waits until the specified object is signaled or the timeout interval elapses.

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
```

### Module Operations

#### LoadLibrary / LoadLibraryEx
Loads a DLL into the calling process's address space.

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr LoadLibrary(string lpFileName);

[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern IntPtr LoadLibraryEx(
    string lpFileName,
    IntPtr hFile,
    LoadLibraryFlags dwFlags);
```

#### GetModuleHandle
Retrieves a handle to a loaded module.

```csharp
[DllImport("kernel32.dll")]
public static extern IntPtr GetModuleHandle(string lpModuleName);
```

#### GetProcAddress
Retrieves the address of an exported function from a DLL.

```csharp
[DllImport("kernel32.dll")]
public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
```

#### SetDllDirectory
Sets the search path for DLL loading.

```csharp
[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool SetDllDirectory(string lpPathName);
```

### Console Operations

#### AllocConsole
Allocates a new console for the calling process.

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool AllocConsole();
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <BaseOutputPath>..\..\Bot</BaseOutputPath>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

## Enumerations
## Data Structures

### MemoryAllocationType
### Core Structures
- `STARTUPINFO` - Process startup configuration
- `PROCESS_INFORMATION` - Process creation results

```csharp
public enum MemoryAllocationType
{
    MEM_COMMIT  = 0x1000,  // Commits physical storage
    MEM_RESERVE = 0x2000   // Reserves address space
}
```
### Enumerations
- `LoadLibraryFlags` - Library loading options
- `MemoryAllocationType` - Memory allocation types (MEM_COMMIT, MEM_RESERVE)
- `MemoryProtectionType` - Memory protection settings (PAGE_EXECUTE_READWRITE)
- `MemoryFreeType` - Memory deallocation options (MEM_RELEASE)
- `ProcessCreationFlag` - Process creation flags

### MemoryProtectionType
## Usage Examples

### Creating a Process
```csharp
public enum MemoryProtectionType
{
    PAGE_EXECUTE_READWRITE = 0x40  // Enables execute, read, and write access
}
```

### MemoryFreeType
var startupInfo = new WinProcessImports.STARTUPINFO();
var processInfo = new WinProcessImports.PROCESS_INFORMATION();

```csharp
public enum MemoryFreeType
{
    MEM_RELEASE = 0x8000  // Releases the entire region
}
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

### ProcessCreationFlag

### Memory Allocation in Remote Process
```csharp
public enum ProcessCreationFlag
{
    CREATE_DEFAULT_ERROR_MODE = 0x04000000  // New process uses default error mode
}
```

### LoadLibraryFlags
IntPtr processHandle = WinProcessImports.OpenProcess(
    processAccess: 0x1F0FFF, // PROCESS_ALL_ACCESS
    bInheritHandle: false,
    processId: targetProcessId
);

```csharp
[Flags]
public enum LoadLibraryFlags : uint
{
    None                          = 0,
    LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008  // Use alternate DLL search path
}
IntPtr allocatedMemory = WinProcessImports.VirtualAllocEx(
    hProcess: processHandle,
    dwAddress: IntPtr.Zero,
    nSize: 1024,
    dwAllocationType: WinProcessImports.MemoryAllocationType.MEM_COMMIT | WinProcessImports.MemoryAllocationType.MEM_RESERVE,
    dwProtect: WinProcessImports.MemoryProtectionType.PAGE_EXECUTE_READWRITE
);
```

## Structures

### STARTUPINFO

Process startup configuration:

### Safe Process Cleanup
```csharp
public struct STARTUPINFO
try 
{
    public uint cb;              // Structure size
    public string lpReserved;
    public string lpDesktop;     // Desktop name
    public string lpTitle;       // Window title
    public uint dwX, dwY;        // Window position
    public uint dwXSize, dwYSize; // Window size
    public uint dwXCountChars, dwYCountChars; // Console buffer size
    public uint dwFillAttribute; // Console fill attribute
    public uint dwFlags;         // Startup flags
    public short wShowWindow;    // Show window command
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;     // Standard input handle
    public IntPtr hStdOutput;    // Standard output handle
    public IntPtr hStdError;     // Standard error handle
    WinProcessImports.CloseProcess(processHandle);
}
```

### PROCESS_INFORMATION

New process information:

```csharp
public struct PROCESS_INFORMATION
catch (System.ComponentModel.Win32Exception ex)
{
    public IntPtr hProcess;   // Process handle
    public IntPtr hThread;    // Primary thread handle
    public uint dwProcessId;  // Process ID
    public uint dwThreadId;   // Thread ID
    // Handle cleanup errors
    Console.WriteLine($"Failed to close process: {ex.Message}");
}
```

## Usage Examples

### DLL Injection Pattern

The typical DLL injection workflow used by ForegroundBotRunner:

```csharp
// 1. Open target process
IntPtr hProcess = WinProcessImports.OpenProcess(
    0x001F0FFF,  // PROCESS_ALL_ACCESS
    false, 
    targetProcessId);

// 2. Allocate memory for DLL path
string dllPath = @"C:\Bot\Loader.dll";
byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
IntPtr allocMem = WinProcessImports.VirtualAllocEx(
    hProcess,
    IntPtr.Zero,
    pathBytes.Length,
    WinProcessImports.MemoryAllocationType.MEM_COMMIT | 
    WinProcessImports.MemoryAllocationType.MEM_RESERVE,
    WinProcessImports.MemoryProtectionType.PAGE_EXECUTE_READWRITE);

// 3. Write DLL path to target process
int bytesWritten = 0;
WinProcessImports.WriteProcessMemory(
    hProcess, 
    allocMem, 
    pathBytes, 
    pathBytes.Length, 
    ref bytesWritten);

// 4. Get LoadLibraryW address
IntPtr kernel32 = WinProcessImports.GetModuleHandle("kernel32.dll");
IntPtr loadLibAddr = WinProcessImports.GetProcAddress(kernel32, "LoadLibraryW");

// 5. Create remote thread to load DLL
IntPtr hThread = WinProcessImports.CreateRemoteThread(
    hProcess,
    IntPtr.Zero,
    0,
    loadLibAddr,
    allocMem,
    0,
    IntPtr.Zero);

// 6. Wait for injection to complete
WinProcessImports.WaitForSingleObject(hThread, 5000);

// 7. Cleanup
WinProcessImports.VirtualFreeEx(hProcess, allocMem, 0, 
    WinProcessImports.MemoryFreeType.MEM_RELEASE);
WinProcessImports.CloseHandle(hThread);
WinProcessImports.CloseHandle(hProcess);
```

### Process Creation
## Integration with BloogBot

```csharp
var startupInfo = new WinProcessImports.STARTUPINFO();
startupInfo.cb = (uint)Marshal.SizeOf<WinProcessImports.STARTUPINFO>();
This library is a critical dependency for several BloogBot components:

WinProcessImports.CreateProcess(
    @"C:\Games\WoW\WoW.exe",
    null,
    IntPtr.Zero,
    IntPtr.Zero,
    false,
    WinProcessImports.ProcessCreationFlag.CREATE_DEFAULT_ERROR_MODE,
    IntPtr.Zero,
    @"C:\Games\WoW",
    ref startupInfo,
    out WinProcessImports.PROCESS_INFORMATION processInfo);
```
- **PathfindingService**: Uses process APIs for game memory access
- **Loader**: Leverages DLL injection capabilities
- **Bot Services**: Requires memory management for game state reading

## File Structure
## Error Handling

```
Exports/WinImports/
??? WinProcessImports.csproj    # .NET 8 class library project
??? WinProcessImports.cs        # All P/Invoke declarations
??? README.md                   # This documentation
```
The library includes comprehensive error handling:
- Win32 error code propagation through `Marshal.GetLastWin32Error()`
- Structured exception handling in `CloseProcess()` helper method
- SetLastError support for all P/Invoke declarations

## Security Considerations

?? **Important**: These APIs provide powerful low-level system access:

- **Process Access**: Can read/write memory of other processes
- **Code Injection**: Can execute code in other processes
- **System Stability**: Incorrect use can crash processes or the system
?? **Important Security Notes**:
- This library enables process injection and memory manipulation
- Intended for legitimate game automation and testing purposes
- Use only with software you own or have explicit permission to modify
- Antivirus software may flag applications using these APIs
- Requires elevated privileges for some operations

**Guidelines**:
- Only use on systems you own or have permission to modify
- Test thoroughly in isolated environments
- Never use for malicious purposes
- Be aware of antivirus/anti-cheat detection
## Dependencies

## Error Handling
- **System.Runtime.InteropServices** - P/Invoke marshaling
- **System.ComponentModel** - Win32Exception handling
- **.NET 8 Runtime** - Modern .NET runtime features

Many APIs set `SetLastError`. Use `Marshal.GetLastWin32Error()` to retrieve error codes:
## Building

```csharp
IntPtr handle = WinProcessImports.OpenProcess(access, false, pid);
if (handle == IntPtr.Zero)
{
    int error = Marshal.GetLastWin32Error();
    throw new Win32Exception(error);
}
1. Ensure .NET 8.0 SDK is installed
2. Open the solution in Visual Studio 2022 or use CLI:
   ```bash
   dotnet build Exports/WinImports/WinProcessImports.csproj
   ```
3. Output will be placed in `..\..\Bot\` directory

## Related Components
## Compatibility

| Component | Relationship |
|-----------|--------------|
| **ForegroundBotRunner** | Uses for DLL injection and process management |
| **Loader.dll** | DLL that gets injected using these APIs |
| **FastCall.dll** | Loaded after injection using LoadLibrary |
- **Windows Versions**: Windows 10+ (due to kernel32.dll dependencies)
- **Architectures**: x86, x64, AnyCPU
- **Runtime**: .NET 8.0+

## References
## Contributing

- [Windows Process Security and Access Rights](https://docs.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights)
- [Memory Protection Constants](https://docs.microsoft.com/en-us/windows/win32/memory/memory-protection-constants)
- [P/Invoke Tutorial](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
When extending this library:
1. Follow existing P/Invoke patterns
2. Include proper error handling with `SetLastError = true`
3. Use appropriate marshaling attributes
4. Document security implications of new APIs
5. Ensure compatibility with the BloogBot architecture

---
## License

*This component is part of the WWoW (Westworld of Warcraft) simulation platform. See [ARCHITECTURE.md](../../ARCHITECTURE.md) for system-wide documentation.*
This library is part of the BloogBot project. Use in accordance with project licensing terms.