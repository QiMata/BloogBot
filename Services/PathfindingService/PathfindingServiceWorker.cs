namespace PathfindingService
{
    public class PathfindingServiceWorker : BackgroundService
    {
        private readonly ILogger<PathfindingServiceWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;

        private PathfindingSocketServer _pathfindingSocketServer;

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
            // Start the socket server first so StateManager can connect immediately
            var ipAddress = _configuration["PathfindingService:IpAddress"];
            var port = int.Parse(_configuration["PathfindingService:Port"]);

            _logger.LogInformation($"Starting PathfindingService socket server on {ipAddress}:{port}...");

            _pathfindingSocketServer = new PathfindingSocketServer(
                ipAddress,
                port,
                _loggerFactory.CreateLogger<PathfindingSocketServer>()
            );

            _logger.LogInformation($"PathfindingService socket server started. Now loading navigation and physics data...");

            // Load nav/physics in background - this is the slow part
            await Task.Run(() => _pathfindingSocketServer.InitializeNavigation(), stoppingToken);

            _logger.LogInformation("PathfindingService fully initialized and ready to handle requests.");

            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
