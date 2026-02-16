using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace PathfindingService
{
    public class PathfindingServiceWorker(
        ILogger<PathfindingServiceWorker> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration) : BackgroundService
    {
        private readonly ILogger<PathfindingServiceWorker> _logger = logger;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly IConfiguration _configuration = configuration;
        private PathfindingSocketServer? _pathfindingSocketServer;

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
