# WinProcessImports - Windows API P/Invoke Library

## Overview

**WinProcessImports** is a .NET class library providing Platform Invocation Services (P/Invoke) declarations for Windows API functions. It serves as the foundation for process manipulation, memory access, and DLL injection operations in the WWoW system.

This library enables managed C# code to interact with low-level Windows APIs from `kernel32.dll`, making it essential for the **ForegroundBotRunner** injection pipeline and process management.

## Purpose

WinProcessImports provides:

1. **Process Management** - Create, open, and manage Windows processes
2. **Memory Operations** - Allocate, read, write, and free virtual memory
3. **Thread Control** - Create and synchronize remote threads
4. **Module Management** - Load DLLs and resolve function addresses
5. **Console Management** - Allocate console windows for debug output

## Architecture

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

## Technical Details

### Build Configuration

| Property | Value |
|----------|-------|
| Target Framework | .NET 8.0 |
| Output Type | Class Library |
| Nullable | Enabled |
| Implicit Usings | Enabled |
| Unsafe Blocks | Allowed |

### Output Location

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
```

## Enumerations

### MemoryAllocationType

```csharp
public enum MemoryAllocationType
{
    MEM_COMMIT  = 0x1000,  // Commits physical storage
    MEM_RESERVE = 0x2000   // Reserves address space
}
```

### MemoryProtectionType

```csharp
public enum MemoryProtectionType
{
    PAGE_EXECUTE_READWRITE = 0x40  // Enables execute, read, and write access
}
```

### MemoryFreeType

```csharp
public enum MemoryFreeType
{
    MEM_RELEASE = 0x8000  // Releases the entire region
}
```

### ProcessCreationFlag

```csharp
public enum ProcessCreationFlag
{
    CREATE_DEFAULT_ERROR_MODE = 0x04000000  // New process uses default error mode
}
```

### LoadLibraryFlags

```csharp
[Flags]
public enum LoadLibraryFlags : uint
{
    None                          = 0,
    LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008  // Use alternate DLL search path
}
```

## Structures

### STARTUPINFO

Process startup configuration:

```csharp
public struct STARTUPINFO
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
}
```

### PROCESS_INFORMATION

New process information:

```csharp
public struct PROCESS_INFORMATION
{
    public IntPtr hProcess;   // Process handle
    public IntPtr hThread;    // Primary thread handle
    public uint dwProcessId;  // Process ID
    public uint dwThreadId;   // Thread ID
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

```csharp
var startupInfo = new WinProcessImports.STARTUPINFO();
startupInfo.cb = (uint)Marshal.SizeOf<WinProcessImports.STARTUPINFO>();

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

## File Structure

```
Exports/WinImports/
??? WinProcessImports.csproj    # .NET 8 class library project
??? WinProcessImports.cs        # All P/Invoke declarations
??? README.md                   # This documentation
```

## Security Considerations

?? **Important**: These APIs provide powerful low-level system access:

- **Process Access**: Can read/write memory of other processes
- **Code Injection**: Can execute code in other processes
- **System Stability**: Incorrect use can crash processes or the system

**Guidelines**:
- Only use on systems you own or have permission to modify
- Test thoroughly in isolated environments
- Never use for malicious purposes
- Be aware of antivirus/anti-cheat detection

## Error Handling

Many APIs set `SetLastError`. Use `Marshal.GetLastWin32Error()` to retrieve error codes:

```csharp
IntPtr handle = WinProcessImports.OpenProcess(access, false, pid);
if (handle == IntPtr.Zero)
{
    int error = Marshal.GetLastWin32Error();
    throw new Win32Exception(error);
}
```

## Related Components

| Component | Relationship |
|-----------|--------------|
| **ForegroundBotRunner** | Uses for DLL injection and process management |
| **Loader.dll** | DLL that gets injected using these APIs |
| **FastCall.dll** | Loaded after injection using LoadLibrary |

## References

- [Windows Process Security and Access Rights](https://docs.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights)
- [Memory Protection Constants](https://docs.microsoft.com/en-us/windows/win32/memory/memory-protection-constants)
- [P/Invoke Tutorial](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform. See [ARCHITECTURE.md](../../ARCHITECTURE.md) for system-wide documentation.*
