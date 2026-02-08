using System.Diagnostics;
using System.Net.Sockets;
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// Detects and auto-launches the MaNGOS server stack (MySQL, realmd, mangosd)
/// for integration tests. Thread-safe — multiple test classes can call
/// EnsureRunningAsync concurrently without double-launching.
/// </summary>
public static class MangosServerLauncher
{
    private static readonly object _lock = new();
    private static bool _initialized;
    private static Process? _mysqlProcess;
    private static Process? _realmdProcess;
    private static Process? _mangosdProcess;

    /// <summary>
    /// Ensures MySQL, realmd, and mangosd are all running.
    /// Launches any that are missing. Waits for TCP readiness.
    /// </summary>
    public static async Task EnsureRunningAsync(
        IntegrationTestConfig config,
        ITestOutputHelper? output = null)
    {
        lock (_lock)
        {
            if (_initialized) return;
            _initialized = true;
        }

        if (!Directory.Exists(config.MangosDirectory))
        {
            output?.WriteLine($"MaNGOS directory not found: {config.MangosDirectory}");
            return;
        }

        await EnsureServiceAsync(
            "MySQL",
            "mysqld",
            Path.Combine(config.MangosDirectory, @"mysql5\bin\mysqld.exe"),
            "--console --max_allowed_packet=128M",
            Path.Combine(config.MangosDirectory, @"mysql5\bin"),
            "127.0.0.1",
            config.MySqlPort,
            timeoutSeconds: 30,
            output);

        await EnsureServiceAsync(
            "realmd",
            "realmd",
            Path.Combine(config.MangosDirectory, "realmd.exe"),
            "",
            config.MangosDirectory,
            config.AuthServerIp,
            config.AuthServerPort,
            timeoutSeconds: 15,
            output);

        await EnsureServiceAsync(
            "mangosd",
            "mangosd",
            Path.Combine(config.MangosDirectory, "mangosd.exe"),
            "",
            config.MangosDirectory,
            "127.0.0.1",
            config.WorldServerPort,
            timeoutSeconds: 60,
            output);
    }

    private static async Task EnsureServiceAsync(
        string displayName,
        string processName,
        string exePath,
        string arguments,
        string workingDirectory,
        string ip,
        int port,
        int timeoutSeconds,
        ITestOutputHelper? output)
    {
        // Check TCP first
        if (await IsPortOpenAsync(ip, port))
        {
            output?.WriteLine($"{displayName} already running on {ip}:{port}");
            return;
        }

        // Check if process exists but isn't listening yet
        if (IsProcessRunning(processName))
        {
            output?.WriteLine($"{displayName} process found, waiting for port {port}...");
        }
        else
        {
            if (!File.Exists(exePath))
            {
                output?.WriteLine($"{displayName} executable not found: {exePath}");
                return;
            }

            output?.WriteLine($"Launching {displayName} from {exePath}...");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                output?.WriteLine($"Failed to start {displayName}");
                return;
            }

            output?.WriteLine($"{displayName} launched (PID: {process.Id})");

            // Track launched process
            lock (_lock)
            {
                switch (processName)
                {
                    case "mysqld": _mysqlProcess = process; break;
                    case "realmd": _realmdProcess = process; break;
                    case "mangosd": _mangosdProcess = process; break;
                }
            }
        }

        // Wait for TCP readiness
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            if (await IsPortOpenAsync(ip, port))
            {
                output?.WriteLine($"{displayName} available on port {port} after {sw.Elapsed.TotalSeconds:F1}s");
                return;
            }
            await Task.Delay(1000);
        }

        output?.WriteLine($"{displayName} did not become available on port {port} within {timeoutSeconds}s");
    }

    private static async Task<bool> IsPortOpenAsync(string ip, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(500));
            return ReferenceEquals(completed, connectTask) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
