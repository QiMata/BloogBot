using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace WoWStateManager;

internal sealed class MangosServerBootstrapper(
    IOptions<MangosServerOptions> options,
    ILogger<MangosServerBootstrapper> logger
) : IHostedService
{
    private readonly MangosServerOptions _options = options.Value;
    private readonly ILogger<MangosServerBootstrapper> _logger = logger;

    private Process? _mysqlProcess;
    private Process? _realmdProcess;
    private Process? _mangosdProcess;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoLaunch)
        {
            _logger.LogInformation("MaNGOS auto-launch disabled.");
            return;
        }

        if (!Directory.Exists(_options.MangosDirectory))
        {
            _logger.LogWarning("MaNGOS directory not found at {Dir}. Skipping auto-launch.", _options.MangosDirectory);
            return;
        }

        await EnsureMySqlAsync(cancellationToken);
        await EnsureRealmdAsync(cancellationToken);
        await EnsureMangosdAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureMySqlAsync(CancellationToken ct)
    {
        if (await IsPortOpenAsync("127.0.0.1", _options.MySqlPort, ct))
        {
            _logger.LogInformation("MySQL already running on port {Port}.", _options.MySqlPort);
            return;
        }

        if (IsProcessRunning("mysqld"))
        {
            _logger.LogInformation("mysqld process found but not yet accepting connections. Waiting...");
        }
        else
        {
            var exePath = Path.Combine(_options.MangosDirectory, _options.MySqlRelativePath);
            if (!File.Exists(exePath))
            {
                _logger.LogWarning("MySQL executable not found at {Path}. Skipping.", exePath);
                return;
            }

            _logger.LogInformation("Launching MySQL from {Path}...", exePath);
            _mysqlProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = _options.MySqlArgs,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            });

            if (_mysqlProcess == null)
            {
                _logger.LogError("Failed to start MySQL process.");
                return;
            }

            _logger.LogInformation("MySQL launched (PID: {Pid}).", _mysqlProcess.Id);
        }

        await WaitForPortAsync("MySQL", "127.0.0.1", _options.MySqlPort, _options.MySqlTimeoutSeconds, ct);
    }

    private async Task EnsureRealmdAsync(CancellationToken ct)
    {
        if (await IsPortOpenAsync("127.0.0.1", _options.RealmdPort, ct))
        {
            _logger.LogInformation("realmd already running on port {Port}.", _options.RealmdPort);
            return;
        }

        if (IsProcessRunning("realmd"))
        {
            _logger.LogInformation("realmd process found but not yet accepting connections. Waiting...");
        }
        else
        {
            var exePath = Path.Combine(_options.MangosDirectory, _options.RealmdExe);
            if (!File.Exists(exePath))
            {
                _logger.LogWarning("realmd executable not found at {Path}. Skipping.", exePath);
                return;
            }

            _logger.LogInformation("Launching realmd from {Path}...", exePath);
            _realmdProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = _options.MangosDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            });

            if (_realmdProcess == null)
            {
                _logger.LogError("Failed to start realmd process.");
                return;
            }

            _logger.LogInformation("realmd launched (PID: {Pid}).", _realmdProcess.Id);
        }

        await WaitForPortAsync("realmd", "127.0.0.1", _options.RealmdPort, _options.RealmdTimeoutSeconds, ct);
    }

    private async Task EnsureMangosdAsync(CancellationToken ct)
    {
        if (await IsPortOpenAsync("127.0.0.1", _options.MangosdPort, ct))
        {
            _logger.LogInformation("mangosd already running on port {Port}.", _options.MangosdPort);
            return;
        }

        if (IsProcessRunning("mangosd"))
        {
            _logger.LogInformation("mangosd process found but not yet accepting connections. Waiting...");
        }
        else
        {
            var exePath = Path.Combine(_options.MangosDirectory, _options.MangosdExe);
            if (!File.Exists(exePath))
            {
                _logger.LogWarning("mangosd executable not found at {Path}. Skipping.", exePath);
                return;
            }

            _logger.LogInformation("Launching mangosd from {Path}...", exePath);
            _mangosdProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = _options.MangosDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            });

            if (_mangosdProcess == null)
            {
                _logger.LogError("Failed to start mangosd process.");
                return;
            }

            _logger.LogInformation("mangosd launched (PID: {Pid}).", _mangosdProcess.Id);
        }

        await WaitForPortAsync("mangosd", "127.0.0.1", _options.MangosdPort, _options.MangosdTimeoutSeconds, ct);
    }

    private async Task WaitForPortAsync(string serviceName, string ip, int port, int timeoutSeconds, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();

            if (await IsPortOpenAsync(ip, port, ct))
            {
                _logger.LogInformation("{Service} available on port {Port} after {Elapsed:F1}s.",
                    serviceName, port, sw.Elapsed.TotalSeconds);
                return;
            }

            if (sw.Elapsed.TotalSeconds % 10 < 1.1)
            {
                _logger.LogDebug("Waiting for {Service} on port {Port}... ({Elapsed:F0}s)",
                    serviceName, port, sw.Elapsed.TotalSeconds);
            }

            await Task.Delay(1000, ct);
        }

        _logger.LogWarning("{Service} did not become available on port {Port} within {Timeout}s.",
            serviceName, port, timeoutSeconds);
    }

    private static async Task<bool> IsPortOpenAsync(string ip, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(500, ct));
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
