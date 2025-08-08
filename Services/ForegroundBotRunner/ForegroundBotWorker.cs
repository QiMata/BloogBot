using BotRunner;
using BotRunner.Clients;
using Communication;
using ForegroundBotRunner.Statics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ForegroundBotRunner
{
    public class ForegroundBotWorker : BackgroundService
    {
        private readonly PathfindingClient _pathfindingClient;
        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly ILogger<ForegroundBotWorker> _logger;
        private readonly IConfiguration _configuration;

        private BotRunnerService _botRunner;
        private CancellationToken _stoppingToken;

        public ForegroundBotWorker(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<ForegroundBotWorker>();

            // Initialize clients with retry logic built-in
            _pathfindingClient = new PathfindingClient(
                configuration["PathfindingService:IpAddress"]!, 
                int.Parse(configuration["PathfindingService:Port"]!), 
                loggerFactory.CreateLogger<PathfindingClient>()
            );
            
            _characterStateUpdateClient = new CharacterStateUpdateClient(
                configuration["CharacterStateListener:IpAddress"]!, 
                int.Parse(configuration["CharacterStateListener:Port"]!), 
                loggerFactory.CreateLogger<CharacterStateUpdateClient>()
            );

            _logger.LogInformation("ForegroundBotWorker initialized");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            try
            {
                _logger.LogInformation("ForegroundBotWorker starting in injected WoW process...");
                _logger.LogInformation($"Current Process: {Process.GetCurrentProcess().ProcessName} (ID: {Process.GetCurrentProcess().Id})");

                // Log to injection log file for diagnostics
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"ForegroundBotWorker started at {DateTime.Now:HH:mm:ss}\n");

                // Initialize WoW integration components
                await InitializeBotComponents();

                // Start periodic diagnostics
                _ = Task.Run(async () => await PeriodicDiagnostics(stoppingToken), stoppingToken);

                // Main execution loop - keep the service running
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Monitor bot health and restart if needed
                        await MonitorBotHealth();
                        
                        // Brief delay to prevent excessive CPU usage
                        await Task.Delay(1000, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in main execution loop");
                        await Task.Delay(5000, stoppingToken); // Wait before retrying
                    }
                }

                _logger.LogInformation("ForegroundBotWorker is shutting down...");
                File.AppendAllText(logPath, $"ForegroundBotWorker shutting down at {DateTime.Now:HH:mm:ss}\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in ForegroundBotWorker");
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"FATAL ERROR in ForegroundBotWorker: {ex}\n");
                throw;
            }
        }

        private async Task InitializeBotComponents()
        {
            _logger.LogInformation("Initializing bot components...");

            // Create ActivitySnapshot for tracking character state
            var activitySnapshot = new ActivitySnapshot();

            // Create core components directly (since we're in the injected WoW process)
            var eventHandler = WoWEventHandler.Instance;
            var objectManager = new ObjectManager(eventHandler, activitySnapshot);

            // Create BotRunnerService with the proper dependencies
            _botRunner = new BotRunnerService(objectManager, _characterStateUpdateClient, _pathfindingClient);

            _logger.LogInformation("BotRunnerService initialized, starting bot...");

            // Start the bot
            _botRunner.Start();

            _logger.LogInformation("Bot started successfully. Bot is now running injected in WoW process...");

            // Give the bot a moment to fully initialize
            await Task.Delay(1000);
        }

        private async Task MonitorBotHealth()
        {
            // Add bot health monitoring logic here
            // For example:
            // - Check if bot is responsive
            // - Monitor for crashes or exceptions
            // - Restart bot if needed
            // - Report health status back to StateManager

            if (_botRunner == null)
            {
                _logger.LogWarning("BotRunner is null, attempting to reinitialize...");
                await InitializeBotComponents();
            }
        }

        private async Task PeriodicDiagnostics(CancellationToken cancellationToken)
        {
            var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, cancellationToken); // Every 30 seconds

                    _logger.LogInformation("=== PERIODIC BOT STATUS CHECK ===");
                    _logger.LogInformation($"Bot Status: {(_botRunner != null ? "Running" : "Not Initialized")}");
                    _logger.LogInformation($"Process: {Process.GetCurrentProcess().ProcessName} (ID: {Process.GetCurrentProcess().Id})");
                    _logger.LogInformation("=====================================");

                    File.AppendAllText(logPath, $"Periodic check at {DateTime.Now:HH:mm:ss} - Bot: {(_botRunner != null ? "Running" : "Not Initialized")}\n");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in periodic diagnostics");
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ForegroundBotWorker stop requested...");

            // Stop the bot runner if it's running
            _botRunner.Stop();

            // Clean up clients
            _pathfindingClient?.Close();
            _characterStateUpdateClient?.Close();

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("ForegroundBotWorker stopped.");
        }
    }
}
