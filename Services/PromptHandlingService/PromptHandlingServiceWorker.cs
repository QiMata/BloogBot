using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PromptHandlingService.Cache;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHandlingService
{
    public class PromptHandlingServiceWorker(ILogger<PromptHandlingServiceWorker> logger, PromptCache promptCache) : BackgroundService
    {
        private readonly ILogger<PromptHandlingServiceWorker> _logger = logger;
        private readonly PromptCache _promptCache = promptCache;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[PromptHandling] Service started — prompt cache ready at {DatabasePath}", _promptCache.DatabasePath);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
