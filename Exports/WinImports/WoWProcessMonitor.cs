using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinImports;

/// <summary>
/// Monitors a WoW process from an external process to detect client state.
/// Used by tests and StateManager to wait for the WoW client to be ready before injection.
/// </summary>
public class WoWProcessMonitor : IDisposable
{
    // WoW 1.12.1.5875 Memory Offsets (from Offsets.cs)
    private const int LoginStateOffset = 0xB41478;
    private const int CharacterCountOffset = 0x00B42140;
    private const int ManagerBaseOffset = 0x00B41414;
    private const int PlayerGuidOffset = 0xc0;

    // Known login state strings
    public const string LoginStateLogin = "login";
    public const string LoginStateCharSelect = "charselect";
    public const string LoginStateConnecting = "connecting";

    private readonly int _processId;
    private IntPtr _processHandle;
    private bool _disposed;

    /// <summary>
    /// Creates a new WoW process monitor for the specified process ID.
    /// </summary>
    public WoWProcessMonitor(int processId)
    {
        _processId = processId;
        _processHandle = IntPtr.Zero;
    }

    /// <summary>
    /// Creates a new WoW process monitor for the specified process.
    /// </summary>
    public WoWProcessMonitor(Process process) : this(process.Id)
    {
    }

    /// <summary>
    /// Opens a handle to the WoW process for memory reading.
    /// </summary>
    private bool EnsureProcessHandle()
    {
        if (_processHandle != IntPtr.Zero)
            return true;

        _processHandle = WinProcessImports.OpenProcess(
            WinProcessImports.PROCESS_VM_READ | WinProcessImports.PROCESS_QUERY_INFORMATION,
            false,
            _processId);

        return _processHandle != IntPtr.Zero;
    }

    /// <summary>
    /// Reads a string from the WoW process memory at the specified address.
    /// </summary>
    public string? ReadString(int address, int maxLength = 64)
    {
        if (!EnsureProcessHandle())
            return null;

        try
        {
            var buffer = new byte[maxLength];
            if (!WinProcessImports.ReadProcessMemory(_processHandle, new IntPtr(address), buffer, maxLength, out int bytesRead))
                return null;

            if (bytesRead == 0)
                return null;

            // Find null terminator
            int nullIndex = Array.IndexOf(buffer, (byte)0);
            if (nullIndex < 0)
                nullIndex = bytesRead;

            return Encoding.ASCII.GetString(buffer, 0, nullIndex);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an integer from the WoW process memory at the specified address.
    /// </summary>
    public int? ReadInt(int address)
    {
        if (!EnsureProcessHandle())
            return null;

        try
        {
            var buffer = new byte[4];
            if (!WinProcessImports.ReadProcessMemory(_processHandle, new IntPtr(address), buffer, 4, out int bytesRead))
                return null;

            if (bytesRead != 4)
                return null;

            return BitConverter.ToInt32(buffer, 0);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an unsigned long from the WoW process memory at the specified address.
    /// </summary>
    public ulong? ReadULong(int address)
    {
        if (!EnsureProcessHandle())
            return null;

        try
        {
            var buffer = new byte[8];
            if (!WinProcessImports.ReadProcessMemory(_processHandle, new IntPtr(address), buffer, 8, out int bytesRead))
                return null;

            if (bytesRead != 8)
                return null;

            return BitConverter.ToUInt64(buffer, 0);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the current login state string from the WoW client.
    /// Returns null if unable to read, or the state string (e.g., "login", "charselect", "connecting").
    /// </summary>
    public string? GetLoginState()
    {
        return ReadString(LoginStateOffset);
    }

    /// <summary>
    /// Gets the number of characters on the current account (valid at character select).
    /// </summary>
    public int? GetCharacterCount()
    {
        return ReadInt(CharacterCountOffset);
    }

    /// <summary>
    /// Gets the player GUID from the object manager. Non-zero when logged into a character.
    /// </summary>
    public ulong? GetPlayerGuid()
    {
        var managerBase = ReadInt(ManagerBaseOffset);
        if (managerBase == null || managerBase == 0)
            return null;

        return ReadULong(managerBase.Value + PlayerGuidOffset);
    }

    /// <summary>
    /// Returns true if the WoW client is at the login screen.
    /// </summary>
    public bool IsAtLoginScreen()
    {
        var state = GetLoginState();
        return state == LoginStateLogin;
    }

    /// <summary>
    /// Returns true if the WoW client is at the character select screen.
    /// </summary>
    public bool IsAtCharacterSelect()
    {
        var state = GetLoginState();
        return state == LoginStateCharSelect;
    }

    /// <summary>
    /// Returns true if the WoW client is logged in (has a valid player GUID).
    /// </summary>
    public bool IsLoggedIn()
    {
        var guid = GetPlayerGuid();
        return guid.HasValue && guid.Value != 0;
    }

    /// <summary>
    /// Returns true if the WoW client appears to be initialized and ready for interaction.
    /// This checks if we can successfully read the login state from memory.
    /// </summary>
    public bool IsClientInitialized()
    {
        var state = GetLoginState();
        return !string.IsNullOrEmpty(state);
    }

    /// <summary>
    /// Waits for the WoW client to reach the login screen.
    /// </summary>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="pollInterval">How often to check (default 500ms)</param>
    /// <param name="progress">Optional callback for progress updates</param>
    /// <returns>WaitResult indicating success or failure reason</returns>
    public async Task<WaitResult> WaitForLoginScreenAsync(
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        Action<WaitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        var sw = Stopwatch.StartNew();
        var lastState = "";

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if process is still running
            try
            {
                var process = Process.GetProcessById(_processId);
                if (process.HasExited)
                {
                    return new WaitResult(false, "WoW process exited unexpectedly", lastState);
                }
            }
            catch (ArgumentException)
            {
                return new WaitResult(false, "WoW process not found", lastState);
            }

            // Try to read login state
            var state = GetLoginState();
            if (!string.IsNullOrEmpty(state))
            {
                lastState = state;
                progress?.Invoke(new WaitProgress(sw.Elapsed, state, "Reading login state"));

                if (state == LoginStateLogin)
                {
                    return new WaitResult(true, "Login screen detected", state);
                }
            }
            else
            {
                progress?.Invoke(new WaitProgress(sw.Elapsed, null, "Waiting for client initialization..."));
            }

            await Task.Delay(interval, cancellationToken);
        }

        return new WaitResult(false, $"Timeout waiting for login screen (last state: {lastState})", lastState);
    }

    /// <summary>
    /// Waits for the WoW client to be ready (either at login screen or character select).
    /// </summary>
    public async Task<WaitResult> WaitForClientReadyAsync(
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        Action<WaitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        var sw = Stopwatch.StartNew();
        var lastState = "";

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if process is still running
            try
            {
                var process = Process.GetProcessById(_processId);
                if (process.HasExited)
                {
                    return new WaitResult(false, "WoW process exited unexpectedly", lastState);
                }
            }
            catch (ArgumentException)
            {
                return new WaitResult(false, "WoW process not found", lastState);
            }

            // Try to read login state
            var state = GetLoginState();
            if (!string.IsNullOrEmpty(state))
            {
                lastState = state;
                progress?.Invoke(new WaitProgress(sw.Elapsed, state, $"Current state: {state}"));

                if (state == LoginStateLogin || state == LoginStateCharSelect)
                {
                    return new WaitResult(true, $"Client ready at {state}", state);
                }
            }
            else
            {
                progress?.Invoke(new WaitProgress(sw.Elapsed, null, "Waiting for client initialization..."));
            }

            await Task.Delay(interval, cancellationToken);
        }

        return new WaitResult(false, $"Timeout waiting for client ready (last state: {lastState})", lastState);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_processHandle != IntPtr.Zero)
        {
            WinProcessImports.CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WoWProcessMonitor()
    {
        Dispose();
    }
}

/// <summary>
/// Result of a wait operation.
/// </summary>
public record WaitResult(bool Success, string Message, string? LastState);

/// <summary>
/// Progress update during a wait operation.
/// </summary>
public record WaitProgress(TimeSpan Elapsed, string? CurrentState, string Message);
