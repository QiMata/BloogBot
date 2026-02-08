using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BotRunner.Tests
{
    /// <summary>
    /// Utility class for detecting when WoW.exe process is ready for DLL injection.
    /// Provides methods to wait for process launch, window creation, and login screen.
    /// </summary>
    public static class WoWProcessDetector
    {
        // WoW 1.12.1 memory offset for LoginState (string at this address)
        private const int LOGIN_STATE_OFFSET = 0xB41478;

        // Process access rights needed for reading memory
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        // Minimum working set size (in bytes) for a fully initialized WoW client at login screen
        // WoW 1.12.1 typically reaches 150-200MB when fully loaded
        private const long MIN_WORKING_SET_SIZE = 100 * 1024 * 1024; // 100 MB

        // Working set growth rate threshold - if growth slows below this, client is likely done loading
        private const long GROWTH_THRESHOLD = 1 * 1024 * 1024; // 1 MB per check interval

        /// <summary>
        /// Wait for a WoW process to fully initialize and be ready for injection.
        /// Uses multiple detection methods: window handle, working set size, and memory stability.
        /// </summary>
        /// <param name="process">The WoW process to monitor</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="waitForLoginScreen">If true, also tries to verify login screen (requires admin)</param>
        /// <param name="logger">Optional action to log progress messages</param>
        /// <returns>True if process is ready, false if timeout or process exited</returns>
        public static async Task<bool> WaitForProcessReadyAsync(
            Process process,
            TimeSpan timeout,
            bool waitForLoginScreen = true,
            Action<string>? logger = null)
        {
            var stopwatch = Stopwatch.StartNew();

            logger?.Invoke($"Waiting for WoW process {process.Id} to be ready (timeout: {timeout.TotalSeconds}s)...");

            // Phase 1: Wait for process to not exit immediately
            await Task.Delay(500);
            if (process.HasExited)
            {
                logger?.Invoke($"Process exited immediately with code: {process.ExitCode}");
                return false;
            }

            // Phase 2: Wait for main window handle to be valid
            logger?.Invoke("Phase 2: Waiting for main window to be created...");
            while (stopwatch.Elapsed < timeout)
            {
                if (process.HasExited)
                {
                    logger?.Invoke($"Process exited with code: {process.ExitCode}");
                    return false;
                }

                process.Refresh(); // Refresh process info to get updated window handle

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    logger?.Invoke($"Main window detected (handle: 0x{process.MainWindowHandle:X})");
                    break;
                }

                await Task.Delay(250);
            }

            if (process.MainWindowHandle == IntPtr.Zero)
            {
                logger?.Invoke("Timeout waiting for main window");
                return false;
            }

            // Phase 3: Wait for working set to reach minimum size (client loading assets)
            logger?.Invoke($"Phase 3: Waiting for working set to reach {MIN_WORKING_SET_SIZE / (1024 * 1024)} MB...");
            while (stopwatch.Elapsed < timeout)
            {
                if (process.HasExited)
                {
                    logger?.Invoke($"Process exited with code: {process.ExitCode}");
                    return false;
                }

                process.Refresh();
                var workingSet = process.WorkingSet64;
                logger?.Invoke($"Current working set: {workingSet / (1024 * 1024)} MB");

                if (workingSet >= MIN_WORKING_SET_SIZE)
                {
                    logger?.Invoke($"Minimum working set reached ({workingSet / (1024 * 1024)} MB)");
                    break;
                }

                await Task.Delay(500);
            }

            // Phase 4: Wait for memory to stabilize (growth rate slows down when loading completes)
            logger?.Invoke("Phase 4: Waiting for memory to stabilize...");
            long lastWorkingSet = 0;
            int stableCount = 0;
            const int requiredStableChecks = 3; // Require 3 consecutive stable checks (1.5 seconds)

            while (stopwatch.Elapsed < timeout && stableCount < requiredStableChecks)
            {
                if (process.HasExited)
                {
                    logger?.Invoke($"Process exited with code: {process.ExitCode}");
                    return false;
                }

                process.Refresh();
                var currentWorkingSet = process.WorkingSet64;
                var growth = currentWorkingSet - lastWorkingSet;

                logger?.Invoke($"Working set: {currentWorkingSet / (1024 * 1024)} MB (growth: {growth / 1024} KB)");

                if (lastWorkingSet > 0 && Math.Abs(growth) < GROWTH_THRESHOLD)
                {
                    stableCount++;
                    logger?.Invoke($"Memory stable ({stableCount}/{requiredStableChecks})");
                }
                else
                {
                    stableCount = 0; // Reset if growth detected
                }

                lastWorkingSet = currentWorkingSet;
                await Task.Delay(500);
            }

            if (stableCount < requiredStableChecks)
            {
                logger?.Invoke("Warning: Memory did not fully stabilize, but continuing...");
            }

            // Phase 5: Optionally try to verify login screen state (requires admin privileges)
            if (waitForLoginScreen)
            {
                logger?.Invoke("Phase 5: Attempting to verify login screen state (may fail without admin)...");

                // Try to read LoginState, but don't fail if we can't (requires elevated privileges)
                bool canReadMemory = false;
                IntPtr testHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
                if (testHandle != IntPtr.Zero)
                {
                    canReadMemory = true;
                    CloseHandle(testHandle);
                }

                if (canReadMemory)
                {
                    // We can read memory - wait for login state
                    int attempts = 0;
                    const int maxAttempts = 20; // 10 seconds max for login state
                    while (stopwatch.Elapsed < timeout && attempts < maxAttempts)
                    {
                        if (process.HasExited)
                        {
                            logger?.Invoke($"Process exited with code: {process.ExitCode}");
                            return false;
                        }

                        var loginState = ReadLoginStateExternal(process.Id);
                        logger?.Invoke($"Current LoginState: '{loginState ?? "(null)"}'");

                        if (loginState == "login")
                        {
                            logger?.Invoke("Login screen detected - process is ready for injection");
                            return true;
                        }

                        if (!string.IsNullOrEmpty(loginState) && loginState != "connecting")
                        {
                            logger?.Invoke($"Login state '{loginState}' detected - process is ready for injection");
                            return true;
                        }

                        attempts++;
                        await Task.Delay(500);
                    }

                    logger?.Invoke("Timeout waiting for login screen state");
                    return false;
                }
                else
                {
                    logger?.Invoke("WARNING: Could not open process handle for memory reading");
                    logger?.Invoke("Proceeding based on working set stability (memory read requires admin)");
                }
            }

            logger?.Invoke("Process appears ready for injection (based on working set stability)");
            return true;
        }

        /// <summary>
        /// Read the LoginState string from an external WoW process.
        /// </summary>
        /// <param name="processId">The process ID of WoW.exe</param>
        /// <returns>The login state string, or null if unable to read</returns>
        public static string? ReadLoginStateExternal(int processId)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processId);
                if (processHandle == IntPtr.Zero)
                {
                    return null;
                }

                // Read up to 32 bytes from the LoginState address (it's a short string like "login", "charselect", etc.)
                var buffer = new byte[32];
                if (!ReadProcessMemory(processHandle, (IntPtr)LOGIN_STATE_OFFSET, buffer, buffer.Length, out int bytesRead))
                {
                    return null;
                }

                if (bytesRead == 0)
                {
                    return null;
                }

                // Convert to string (null-terminated ASCII)
                var result = Encoding.ASCII.GetString(buffer);
                var nullIndex = result.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    result = result.Substring(0, nullIndex);
                }

                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        /// <summary>
        /// Find an existing WoW process by name.
        /// </summary>
        /// <param name="processName">The process name (default: "WoW")</param>
        /// <returns>The first matching process, or null if none found</returns>
        public static Process? FindWoWProcess(string processName = "WoW")
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.FirstOrDefault();
        }

        /// <summary>
        /// Wait for a WoW process to appear.
        /// </summary>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="processName">The process name to look for (default: "WoW")</param>
        /// <returns>The WoW process if found, null if timeout</returns>
        public static async Task<Process?> WaitForWoWProcessAsync(TimeSpan timeout, string processName = "WoW")
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var process = FindWoWProcess(processName);
                if (process != null)
                {
                    return process;
                }
                await Task.Delay(250);
            }

            return null;
        }
    }
}
