using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace WoWStateManager;

internal sealed class PathfindingServiceBootstrapper(
    IOptions<PathfindingServiceOptions> options,
    ILogger<PathfindingServiceBootstrapper> logger
) : IHostedService
{
    private readonly PathfindingServiceOptions _options = options.Value;
    private readonly ILogger<PathfindingServiceBootstrapper> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await IsServiceRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Pathfinding service already reachable at {Ip}:{Port}.", _options.IpAddress, _options.Port);
            return;
        }

        _logger.LogInformation("Pathfinding service not detected at {Ip}:{Port}. Attempting to launch.", _options.IpAddress, _options.Port);

        try
        {
            LaunchPathfindingServiceInProcess();
        }
        catch (Exception ex) when (ex is System.IO.FileNotFoundException or System.IO.FileLoadException or TypeLoadException)
        {
            _logger.LogWarning("PathfindingService assembly not available ({Message}). Proceeding without in-process pathfinding.", ex.Message);
            return;
        }

        await WaitForServiceAsync(cancellationToken).ConfigureAwait(false);
    }

    // Isolated into a separate non-inlined method so the JIT only loads
    // the PathfindingService assembly when this method is actually called,
    // not when StartAsync's async state machine is compiled.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LaunchPathfindingServiceInProcess()
    {
        global::PathfindingService.Program.LaunchServiceFromCommandLine();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> IsServiceRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_options.IpAddress, _options.Port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken)).ConfigureAwait(false);
            return ReferenceEquals(completed, connectTask) && client.Connected;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to connect to pathfinding service at {Ip}:{Port}.", _options.IpAddress, _options.Port);
            return false;
        }
    }

    private async Task WaitForServiceAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (await IsServiceRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Pathfinding service became available.");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Pathfinding service did not start within the expected timeframe.");
    }
}

internal sealed class PathfindingServiceOptions
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; }
}
