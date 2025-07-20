namespace PathfindingService
{
    public class PathfindingServiceWorker : BackgroundService
    {
        private readonly ILogger<PathfindingServiceWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;

        private readonly PathfindingSocketServer _pathfindingSocketServer;
        public PathfindingServiceWorker(
            ILogger<PathfindingServiceWorker> logger,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _configuration = configuration;

            _pathfindingSocketServer = new PathfindingSocketServer(
                configuration["PathfindingService:IpAddress"],
                int.Parse(configuration["PathfindingService:Port"]),
                _loggerFactory.CreateLogger<PathfindingSocketServer>()
            );

            _logger.LogInformation($"Started PathfindingService| {_configuration["PathfindingService:IpAddress"]}:{_configuration["PathfindingService:Port"]}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PathfindingServiceWorker is running.");

            stoppingToken.Register(() =>
                _logger.LogInformation("PathfindingServiceWorker is stopping."));

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
                    _logger.LogError(ex, "Error occurred in PathfindingServiceWorker loop.");
                }
            }

            _logger.LogInformation("PathfindingServiceWorker has stopped.");
        }
    }
}
