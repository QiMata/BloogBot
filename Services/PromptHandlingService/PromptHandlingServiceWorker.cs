using PromptHandlingService.Cache;

namespace PromptHandlingService
{
    public class PromptHandlingServiceWorker(ILogger<PromptHandlingServiceWorker> logger, PromptCache promptCache) : BackgroundService
    {
        private readonly ILogger<PromptHandlingServiceWorker> _logger = logger;
        private readonly PromptCache _promptCache = promptCache;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Prompt cache ready at {DatabasePath}", _promptCache.DatabasePath);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                //if (_logger.IsEnabled(LogLevel.Information))
                //{
                //    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                //}
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
