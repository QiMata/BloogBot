using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ForegroundBotRunner;

internal sealed class ForegroundBotHostedService(ILogger<ForegroundBotHostedService> logger) : IHostedService
{
    private readonly ILogger<ForegroundBotHostedService> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Foreground bot runner host initialized. Attach to the game process via Loader for execution.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Foreground bot runner host stopping.");
        return Task.CompletedTask;
    }
}
