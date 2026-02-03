using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace StateManager;

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

        _logger.LogInformation("Pathfinding service not detected at {Ip}:{Port}. Launching local instance.", _options.IpAddress, _options.Port);
        global::PathfindingService.Program.LaunchServiceFromCommandLine();

        await WaitForServiceAsync(cancellationToken).ConfigureAwait(false);
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
