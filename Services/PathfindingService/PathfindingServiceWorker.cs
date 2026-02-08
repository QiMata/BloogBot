using PathfindingService.Repository;

namespace PathfindingService
{
    public class PathfindingServiceWorker : BackgroundService
    {
        private readonly ILogger<PathfindingServiceWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private PathfindingSocketServer? _pathfindingSocketServer;

        public PathfindingServiceWorker(
            ILogger<PathfindingServiceWorker> logger,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Clear any stale status file from previous run
            PathfindingServiceStatus.DeleteStatusFile();

            // Start the socket server first so StateManager can connect immediately
            var ipAddress = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var port = int.Parse(_configuration["PathfindingService:Port"] ?? "5001");

            _logger.LogInformation($"Starting PathfindingService socket server on {ipAddress}:{port}...");

            _pathfindingSocketServer = new PathfindingSocketServer(
                ipAddress,
                port,
                _loggerFactory.CreateLogger<PathfindingSocketServer>()
            );

            _logger.LogInformation($"PathfindingService socket server started. Now loading navigation and physics data...");

            // Load nav/physics in background - this is the slow part
            // Status file is written inside InitializeNavigation() when complete
            await Task.Run(() => _pathfindingSocketServer.InitializeNavigation(), stoppingToken);

            _logger.LogInformation("PathfindingService fully initialized and ready to handle requests.");

            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("PathfindingServiceWorker stopping...");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PathfindingServiceWorker stop requested...");

            // Clean up status file so StateManager knows we're not running
            PathfindingServiceStatus.DeleteStatusFile();

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("PathfindingServiceWorker stopped.");
        }
    }
}
