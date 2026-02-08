using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tests.Infrastructure;

/// <summary>
/// State of the WoW injection process.
/// </summary>
public enum InjectionState
{
    NotStarted,
    ProcessLaunched,
    MemoryAllocated,
    DllPathWritten,
    LoaderInjected,
    LoaderInitialized,
    ManagedCodeRunning,
    Failed,
    ProcessExited
}

/// <summary>
/// Result of a WoW process injection attempt.
/// </summary>
public record InjectionResult(
    bool Success,
    InjectionState FinalState,
    string? ErrorMessage = null,
    int? ProcessId = null,
    IntPtr ProcessHandle = default);

/// <summary>
/// Manages WoW client processes for integration testing.
/// Handles launching WoW and injecting Loader.dll to bootstrap .NET 8 runtime.
/// </summary>
public class WoWProcessManager : IDisposable
{
    private readonly ILogger<WoWProcessManager>? _logger;
    private readonly WoWProcessConfig _config;
    
    private Process? _wowProcess;
    private IntPtr _processHandle;
    private IntPtr _allocatedMemory;
    private InjectionState _state = InjectionState.NotStarted;
    
    private bool _disposed;

    public WoWProcessManager(WoWProcessConfig config, ILogger<WoWProcessManager>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    /// <summary>
    /// Current state of the injection process.
    /// </summary>
    public InjectionState State => _state;

    /// <summary>
    /// The WoW process ID if launched.
    /// </summary>
    public int? ProcessId => _wowProcess?.Id;

    /// <summary>
    /// Launches WoW.exe and injects Loader.dll.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the injection attempt</returns>
    public async Task<InjectionResult> LaunchAndInjectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate paths
            if (!File.Exists(_config.WoWExecutablePath))
            {
                return Fail($"WoW.exe not found at: {_config.WoWExecutablePath}");
            }

            if (!File.Exists(_config.LoaderDllPath))
            {
                return Fail($"Loader.dll not found at: {_config.LoaderDllPath}");
            }

            _logger?.LogInformation("Launching WoW from: {Path}", _config.WoWExecutablePath);

            // Step 1: Launch WoW process
            _wowProcess = LaunchWoWProcess();
            if (_wowProcess == null || _wowProcess.HasExited)
            {
                return Fail("Failed to launch WoW process");
            }
            _state = InjectionState.ProcessLaunched;
            _logger?.LogInformation("WoW launched with PID: {Pid}", _wowProcess.Id);

            // Wait a bit for the process to initialize
            await Task.Delay(_config.ProcessInitDelayMs, cancellationToken);

            if (_wowProcess.HasExited)
            {
                return Fail($"WoW process exited immediately with code: {_wowProcess.ExitCode}");
            }

            // Step 2: Open process handle with required access
            _processHandle = OpenProcess(
                ProcessAccessFlags.All,
                false,
                _wowProcess.Id);

            if (_processHandle == IntPtr.Zero)
            {
                return Fail($"Failed to open process handle. Error: {Marshal.GetLastWin32Error()}");
            }

            // Step 3: Allocate memory in target process for DLL path
            var dllPathBytes = Encoding.Unicode.GetBytes(_config.LoaderDllPath + "\0");
            _allocatedMemory = VirtualAllocEx(
                _processHandle,
                IntPtr.Zero,
                (uint)dllPathBytes.Length,
                AllocationType.Commit | AllocationType.Reserve,
                MemoryProtection.ReadWrite);

            if (_allocatedMemory == IntPtr.Zero)
            {
                return Fail($"Failed to allocate memory. Error: {Marshal.GetLastWin32Error()}");
            }
            _state = InjectionState.MemoryAllocated;
            _logger?.LogDebug("Allocated {Size} bytes at 0x{Address:X}", dllPathBytes.Length, _allocatedMemory);

            // Step 4: Write DLL path to allocated memory
            if (!WriteProcessMemory(
                _processHandle,
                _allocatedMemory,
                dllPathBytes,
                (uint)dllPathBytes.Length,
                out _))
            {
                return Fail($"Failed to write DLL path to process memory. Error: {Marshal.GetLastWin32Error()}");
            }
            _state = InjectionState.DllPathWritten;
            _logger?.LogDebug("Wrote DLL path: {Path}", _config.LoaderDllPath);

            // Step 5: Get LoadLibraryW address from kernel32
            var kernel32 = GetModuleHandle("kernel32.dll");
            var loadLibraryAddr = GetProcAddress(kernel32, "LoadLibraryW");

            if (loadLibraryAddr == IntPtr.Zero)
            {
                return Fail($"Failed to get LoadLibraryW address. Error: {Marshal.GetLastWin32Error()}");
            }

            // Step 6: Create remote thread to call LoadLibraryW with our DLL path
            var threadHandle = CreateRemoteThread(
                _processHandle,
                IntPtr.Zero,
                0,
                loadLibraryAddr,
                _allocatedMemory,
                0,
                out _);

            if (threadHandle == IntPtr.Zero)
            {
                return Fail($"Failed to create remote thread. Error: {Marshal.GetLastWin32Error()}");
            }
            _state = InjectionState.LoaderInjected;
            _logger?.LogInformation("Loader.dll injected successfully");

            // Step 7: Wait for injection to complete
            var waitResult = WaitForSingleObject(threadHandle, (uint)_config.InjectionTimeoutMs);
            CloseHandle(threadHandle);

            if (waitResult != 0) // WAIT_OBJECT_0
            {
                return Fail($"Injection thread did not complete in time. WaitResult: {waitResult}");
            }

            _state = InjectionState.LoaderInitialized;
            _logger?.LogInformation("Loader initialization complete");

            // Give the .NET 8 runtime time to initialize
            await Task.Delay(_config.RuntimeInitDelayMs, cancellationToken);

            if (_wowProcess.HasExited)
            {
                return Fail($"WoW process exited after injection with code: {_wowProcess.ExitCode}");
            }

            _state = InjectionState.ManagedCodeRunning;
            _logger?.LogInformation("Managed code should now be running");

            return new InjectionResult(
                Success: true,
                FinalState: _state,
                ProcessId: _wowProcess.Id,
                ProcessHandle: _processHandle);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during injection");
            return Fail($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Terminates the WoW process if running.
    /// </summary>
    public void TerminateProcess()
    {
        if (_wowProcess != null && !_wowProcess.HasExited)
        {
            _logger?.LogInformation("Terminating WoW process {Pid}", _wowProcess.Id);
            try
            {
                _wowProcess.Kill();
                _wowProcess.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error terminating WoW process");
            }
        }
        _state = InjectionState.ProcessExited;
    }

    /// <summary>
    /// Checks if the WoW process is still running.
    /// </summary>
    public bool IsProcessRunning => _wowProcess != null && !_wowProcess.HasExited;

    private Process? LaunchWoWProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _config.WoWExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(_config.WoWExecutablePath),
            UseShellExecute = false,
            CreateNoWindow = false
        };

        return Process.Start(startInfo);
    }

    private InjectionResult Fail(string message)
    {
        _logger?.LogError("Injection failed: {Message}", message);
        _state = InjectionState.Failed;
        return new InjectionResult(
            Success: false,
            FinalState: _state,
            ErrorMessage: message,
            ProcessId: _wowProcess?.Id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up allocated memory
        if (_allocatedMemory != IntPtr.Zero && _processHandle != IntPtr.Zero)
        {
            VirtualFreeEx(_processHandle, _allocatedMemory, 0, FreeType.Release);
            _allocatedMemory = IntPtr.Zero;
        }

        // Close process handle
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }

        // Terminate process if requested
        if (_config.TerminateOnDispose)
        {
            TerminateProcess();
        }

        _wowProcess?.Dispose();
        _wowProcess = null;
    }

    #region P/Invoke Declarations

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, FreeType dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF
    }

    [Flags]
    private enum AllocationType : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000
    }

    [Flags]
    private enum MemoryProtection : uint
    {
        ReadWrite = 0x04
    }

    [Flags]
    private enum FreeType : uint
    {
        Release = 0x8000
    }

    #endregion
}

/// <summary>
/// Configuration for WoW process management.
/// </summary>
public class WoWProcessConfig
{
    /// <summary>
    /// Path to WoW.exe executable.
    /// Override: WWOW_TEST_WOW_PATH
    /// </summary>
    public string WoWExecutablePath { get; init; } =
        Environment.GetEnvironmentVariable("WWOW_TEST_WOW_PATH") ?? @"C:\Games\WoW-1.12.1\WoW.exe";

    /// <summary>
    /// Path to Loader.dll.
    /// Override: WWOW_TEST_LOADER_PATH
    /// </summary>
    public string LoaderDllPath { get; init; } =
        Environment.GetEnvironmentVariable("WWOW_TEST_LOADER_PATH") ?? 
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Loader.dll");

    /// <summary>
    /// Delay after launching process before injection (ms).
    /// </summary>
    public int ProcessInitDelayMs { get; init; } = 2000;

    /// <summary>
    /// Timeout for injection thread completion (ms).
    /// </summary>
    public int InjectionTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// Delay after injection to allow .NET 8 runtime to initialize (ms).
    /// </summary>
    public int RuntimeInitDelayMs { get; init; } = 5000;

    /// <summary>
    /// Whether to terminate the WoW process when disposing the manager.
    /// </summary>
    public bool TerminateOnDispose { get; init; } = true;

    /// <summary>
    /// Creates a configuration from environment variables.
    /// </summary>
    public static WoWProcessConfig FromEnvironment() => new();
}
