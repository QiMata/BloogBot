using PathfindingService.Repository;

namespace PathfindingService
{
    public class PathfindingServiceWorker : BackgroundService
    {
        private readonly ILogger<PathfindingServiceWorker> _logger;
        private readonly PathfindingSocketServer _pathfindingSocketServer;

        public PathfindingServiceWorker(
            ILogger<PathfindingServiceWorker> logger,
            PathfindingSocketServer pathfindingSocketServer)
        {
            _logger = logger;
            _pathfindingSocketServer = pathfindingSocketServer;

            _logger.LogInformation("PathfindingServiceWorker initialized with dependency injection");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PathfindingServiceWorker starting...");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                // The PathfindingSocketServer runs its own socket listener loop
                // This worker just keeps the service alive
                await Task.Delay(1000, stoppingToken);
            }
            
            _logger.LogInformation("PathfindingServiceWorker stopping...");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PathfindingServiceWorker stop requested...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("PathfindingServiceWorker stopped.");
        }
    }
}
