namespace PromptHandlingService
{
    public class PromptHandlingServiceWorker(ILogger<PromptHandlingServiceWorker> logger) : BackgroundService
    {
        private readonly ILogger<PromptHandlingServiceWorker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PromptHandlingServiceWorker is running.");

            stoppingToken.Register(() =>
                _logger.LogInformation("PromptHandlingServiceWorker is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && stoppingToken.IsCancellationRequested))
                {
                    _logger.LogError(ex, "Error occurred in PromptHandlingServiceWorker loop.");
                }
            }

            _logger.LogInformation("PromptHandlingServiceWorker has stopped.");
        }
    }
}
