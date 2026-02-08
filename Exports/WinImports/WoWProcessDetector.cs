using System.Diagnostics;

namespace WinImports;

/// <summary>
/// Static helper methods for detecting WoW process readiness.
/// This wraps WoWProcessMonitor for simple one-shot usage in tests and StateManager.
/// </summary>
public static class WoWProcessDetector
{
    /// <summary>
    /// Waits for a WoW process to be ready for injection.
    /// </summary>
    /// <param name="process">The WoW process to monitor</param>
    /// <param name="timeout">Maximum time to wait for readiness</param>
    /// <param name="waitForLoginScreen">If true, waits until the login screen is detected. If false, just waits for client initialization.</param>
    /// <param name="logger">Optional logging callback</param>
    /// <returns>True if the process is ready, false if it timed out or exited</returns>
    public static async Task<bool> WaitForProcessReadyAsync(
        Process process,
        TimeSpan timeout,
        bool waitForLoginScreen = true,
        Action<string>? logger = null)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        logger?.Invoke($"Starting process readiness detection for PID {process.Id}");

        // First, wait a moment for the process to start up
        // This gives the EXE time to load its modules and begin initialization
        var initialWait = TimeSpan.FromSeconds(2);
        logger?.Invoke($"Waiting {initialWait.TotalSeconds}s for initial process startup...");
        await Task.Delay(initialWait);

        // Check if process exited during initial wait
        if (process.HasExited)
        {
            logger?.Invoke($"Process exited during initial wait with code {process.ExitCode}");
            return false;
        }

        // Now use WoWProcessMonitor to detect the login screen
        using var monitor = new WoWProcessMonitor(process);

        var sw = Stopwatch.StartNew();
        var remainingTimeout = timeout - initialWait;

        if (remainingTimeout <= TimeSpan.Zero)
        {
            logger?.Invoke("Timeout exceeded during initial wait");
            return false;
        }

        logger?.Invoke($"Monitoring for login screen (timeout: {remainingTimeout.TotalSeconds}s)...");

        var result = waitForLoginScreen
            ? await monitor.WaitForLoginScreenAsync(
                remainingTimeout,
                pollInterval: TimeSpan.FromMilliseconds(500),
                progress: p => logger?.Invoke($"[{p.Elapsed.TotalSeconds:F1}s] {p.Message}"))
            : await monitor.WaitForClientReadyAsync(
                remainingTimeout,
                pollInterval: TimeSpan.FromMilliseconds(500),
                progress: p => logger?.Invoke($"[{p.Elapsed.TotalSeconds:F1}s] {p.Message}"));

        logger?.Invoke($"Detection result: {result.Message} (Success: {result.Success})");

        return result.Success;
    }

    /// <summary>
    /// Checks if a WoW process is at the login screen.
    /// </summary>
    public static bool IsAtLoginScreen(Process process)
    {
        using var monitor = new WoWProcessMonitor(process);
        return monitor.IsAtLoginScreen();
    }

    /// <summary>
    /// Checks if a WoW process is at the character select screen.
    /// </summary>
    public static bool IsAtCharacterSelect(Process process)
    {
        using var monitor = new WoWProcessMonitor(process);
        return monitor.IsAtCharacterSelect();
    }

    /// <summary>
    /// Gets the current login state of a WoW process.
    /// </summary>
    public static string? GetLoginState(Process process)
    {
        using var monitor = new WoWProcessMonitor(process);
        return monitor.GetLoginState();
    }

    /// <summary>
    /// Checks if the WoW client is initialized (can read memory successfully).
    /// </summary>
    public static bool IsClientInitialized(Process process)
    {
        using var monitor = new WoWProcessMonitor(process);
        return monitor.IsClientInitialized();
    }
}
