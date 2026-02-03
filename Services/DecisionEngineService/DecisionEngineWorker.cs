namespace DecisionEngineService
{
    public class DecisionEngineWorker(ILogger<DecisionEngineWorker> logger) : BackgroundService
    {
        private readonly ILogger<DecisionEngineWorker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DecisionEngineWorker is running.");

            stoppingToken.Register(() =>
                _logger.LogInformation("DecisionEngineWorker is stopping."));

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
                    _logger.LogError(ex, "Error occurred in DecisionEngineWorker loop.");
                }
            }

            _logger.LogInformation("DecisionEngineWorker has stopped.");
        }
    }
}
