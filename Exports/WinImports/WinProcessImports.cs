using System;
using System.Runtime.InteropServices;

public static class WinProcessImports
{
    public const uint TH32CS_SNAPMODULE   = 0x00000008;
    public const uint TH32CS_SNAPMODULE32 = 0x00000010;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryEx(
        string lpFileName,
        IntPtr hFile,
        LoadLibraryFlags dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetDllDirectory(string lpPathName);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    // Canonical P/Invoke declarations for process injection (typed-enum variants).
    // VirtualAllocEx, WriteProcessMemory, CreateRemoteThread are declared below (lines 74+).

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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    public static void CloseProcess(IntPtr processHandle)
    {
        if (processHandle != IntPtr.Zero)
        {
            // Close the process using its handle
            if (!CloseHandle(processHandle))
            {
                // Handle the case where the close operation fails
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    public static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr dwAddress,
        int nSize,
        MemoryAllocationType dwAllocationType,
        MemoryProtectionType dwProtect);

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        ref int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    public static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttribute,
        IntPtr dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        IntPtr lpThreadId);

    [DllImport("kernel32.dll")]
    public static extern bool VirtualFreeEx(
        IntPtr hProcess,
        IntPtr dwAddress,
        int nSize,
        MemoryFreeType dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

    // Window enumeration for process window detection (avoids Process.GetProcessById access denied)
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    // Additional safe injection functions
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool IsWow64Process(IntPtr hProcess, out bool Wow64Process);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    // Process access rights
    public const uint PROCESS_CREATE_THREAD = 0x0002;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_OPERATION = 0x0008;
    public const uint PROCESS_VM_WRITE = 0x0020;
    public const uint PROCESS_VM_READ = 0x0010;

    // Wait results
    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;
    public const uint WAIT_FAILED = 0xFFFFFFFF;

    public enum LoadLibraryFlags : uint
    {
        DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
        LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
        LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
        LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
        LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
        LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
    }

    public enum ProcessCreationFlag : uint
    {
        ZERO_FLAG = 0x00000000,
        CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
        CREATE_DEFAULT_ERROR_MODE = 0x04000000,
        CREATE_NEW_CONSOLE = 0x00000010,
        CREATE_NEW_PROCESS_GROUP = 0x00000200,
        CREATE_NO_WINDOW = 0x08000000,
        CREATE_PROTECTED_PROCESS = 0x00040000,
        CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
        CREATE_SEPARATE_WOW_VDM = 0x00001000,
        CREATE_SHARED_WOW_VDM = 0x00001000,
        CREATE_SUSPENDED = 0x00000004,
        CREATE_UNICODE_ENVIRONMENT = 0x00000400,
        DEBUG_ONLY_THIS_PROCESS = 0x00000002,
        DEBUG_PROCESS = 0x00000001,
        DETACHED_PROCESS = 0x00000008,
        EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
        INHERIT_PARENT_AFFINITY = 0x00010000
    }

    public enum MemoryAllocationType : uint
    {
        MEM_COMMIT = 0x00001000,
        MEM_RESERVE = 0x00002000,
        MEM_DECOMMIT = 0x00004000,
        MEM_RELEASE = 0x00008000,
        MEM_FREE = 0x00010000,
        MEM_PRIVATE = 0x00020000,
        MEM_MAPPED = 0x00040000,
        MEM_RESET = 0x00080000,
        MEM_TOP_DOWN = 0x00100000,
        MEM_WRITE_WATCH = 0x00200000,
        MEM_PHYSICAL = 0x00400000,
        MEM_LARGE_PAGES = 0x20000000,
        MEM_4MB_PAGES = 0x80000000u
    }

    public enum MemoryProtectionType
    {
        PAGE_EXECUTE = 0x10,
        PAGE_EXECUTE_READ = 0x20,
        PAGE_EXECUTE_READWRITE = 0x40,
        PAGE_EXECUTE_WRITECOPY = 0x80,
        PAGE_NOACCESS = 0x01,
        PAGE_READONLY = 0x02,
        PAGE_READWRITE = 0x04,
        PAGE_WRITECOPY = 0x08,
        PAGE_GUARD = 0x100,
        PAGE_NOCACHE = 0x200,
        PAGE_WRITECOMBINE = 0x400
    }

    public enum MemoryFreeType
    {
        MEM_DECOMMIT = 0x4000,
        MEM_RELEASE = 0x8000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public uint cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    // Safe injection helper methods
    public static class SafeInjection
    {
        public static bool InjectDllSafely(int processId, string dllPath, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // Open process with proper access rights
                var processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    processId);

                if (processHandle == IntPtr.Zero)
                {
                    errorMessage = $"Failed to open process {processId}. Error: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                try
                {
                    // Check if target process is 32-bit or 64-bit
                    IsWow64Process(processHandle, out bool isWow64);
                    bool isTarget32Bit = Environment.Is64BitOperatingSystem && isWow64;
                    bool isCurrent32Bit = !Environment.Is64BitProcess;

                    if (isTarget32Bit != isCurrent32Bit)
                    {
                        errorMessage = $"Architecture mismatch. Target process: {(isTarget32Bit ? "32-bit" : "64-bit")}, Current process: {(isCurrent32Bit ? "32-bit" : "64-bit")}";

                        return false;
                    }

                    // Verify DLL exists and can be loaded
                    if (!System.IO.File.Exists(dllPath))
                    {
                        errorMessage = $"DLL file not found: {dllPath}";
                        return false;
                    }

                    // Allocate memory for DLL path (Unicode)
                    var dllPathBytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
                    var allocSize = (uint)dllPathBytes.Length;

                    var allocatedMemory = VirtualAllocEx(
                        processHandle,
                        IntPtr.Zero,
                        (int)allocSize,
                        MemoryAllocationType.MEM_COMMIT | MemoryAllocationType.MEM_RESERVE,
                        MemoryProtectionType.PAGE_READWRITE);

                    if (allocatedMemory == IntPtr.Zero)
                    {
                        errorMessage = $"Failed to allocate memory in target process. Error: {Marshal.GetLastWin32Error()}";
                        return false;
                    }

                    try
                    {
                        // Write DLL path to allocated memory
                        int bytesWritten = 0;
                        bool writeResult = WriteProcessMemory(processHandle, allocatedMemory, dllPathBytes, (int)allocSize, ref bytesWritten);

                        if (!writeResult || bytesWritten != allocSize)
                        {
                            errorMessage = $"Failed to write DLL path to target process. Error: {Marshal.GetLastWin32Error()}";
                            return false;
                        }

                        // Get LoadLibraryW address
                        var kernel32 = GetModuleHandle("kernel32.dll");
                        var loadLibraryAddr = GetProcAddress(kernel32, "LoadLibraryW");

                        if (loadLibraryAddr == IntPtr.Zero)
                        {
                            errorMessage = $"Failed to get LoadLibraryW address. Error: {Marshal.GetLastWin32Error()}";
                            return false;
                        }

                        // Create remote thread
                        var remoteThread = CreateRemoteThread(
                            processHandle, IntPtr.Zero, IntPtr.Zero, loadLibraryAddr, allocatedMemory, 0, IntPtr.Zero);

                        if (remoteThread == IntPtr.Zero)
                        {
                            errorMessage = $"Failed to create remote thread. Error: {Marshal.GetLastWin32Error()}";
                            return false;
                        }

                        try
                        {
                            // Wait for injection to complete
                            var waitResult = WaitForSingleObject(remoteThread, 30000);

                            if (waitResult == WAIT_TIMEOUT)
                            {
                                errorMessage = "DLL injection timed out after 30 seconds";
                                return false;
                            }
                            else if (waitResult == WAIT_FAILED)
                            {
                                errorMessage = $"Wait failed. Error: {Marshal.GetLastWin32Error()}";
                                return false;
                            }

                            // Check thread exit code
                            if (GetExitCodeThread(remoteThread, out uint exitCode))
                            {
                                if (exitCode == 0)
                                {
                                    errorMessage = "LoadLibraryW returned NULL - DLL injection failed. Check DLL dependencies and architecture.";
                                    return false;
                                }
                                // Success - exitCode contains the module handle
                                return true;
                            }
                            else
                            {
                                errorMessage = $"Failed to get thread exit code. Error: {Marshal.GetLastWin32Error()}";
                                return false;
                            }
                        }
                        finally
                        {
                            CloseHandle(remoteThread);
                        }
                    }
                    finally
                    {
                        VirtualFreeEx(processHandle, allocatedMemory, 0, MemoryFreeType.MEM_RELEASE);
                    }
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Exception during injection: {ex.Message}";
                return false;
            }
        }
    }
}
