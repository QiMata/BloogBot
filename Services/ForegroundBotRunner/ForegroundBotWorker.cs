using System.Diagnostics;

#if NET8_0_OR_GREATER
using WoWSharpClient;
using BotRunner;
using BotRunner.Clients;
using Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameData.Core.Interfaces;
using WoWSharpClient.Client;

namespace ForegroundBotRunner
{
    public class ForegroundBotWorker : BackgroundService
    {
        private readonly PathfindingClient _pathfindingClient;
        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly ILogger<ForegroundBotWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;
        private readonly WoWClient _wowClient = new();

        private BotRunnerService? _botRunner;
        private CancellationToken _stoppingToken;

        public ForegroundBotWorker(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ForegroundBotWorker>();

            _wowClient.SetIpAddress(configuration["LoginServer:IpAddress"] ?? "127.0.0.1");

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

            if (Environment.GetEnvironmentVariable("BLOOGBOT_WAIT_DEBUG") == "1")
            {
                while (!Debugger.IsAttached)
                {
                    Console.WriteLine("[ForegroundBotWorker] Waiting for debugger attach...");
                    Thread.Sleep(1000);
                }
                Debugger.Break();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            try
            {
                _logger.LogInformation("ForegroundBotWorker starting in injected WoW process...");
                _logger.LogInformation($"Current Process: {Process.GetCurrentProcess().ProcessName} (ID: {Process.GetCurrentProcess().Id})");

                // Log to injection log file for diagnostics
                var logPath = Path.Combine(AppContext.BaseDirectory, "BloogBotLogs", "injection.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
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
                var logPath = Path.Combine(AppContext.BaseDirectory, "BloogBotLogs", "injection.log");
                File.AppendAllText(logPath, $"FATAL ERROR in ForegroundBotWorker: {ex}\n");
                throw;
            }
        }

        private async Task InitializeBotComponents()
        {
            _logger.LogInformation("Initializing bot components...");
            var activitySnapshot = new ActivitySnapshot();
            WoWSharpObjectManager.Instance.Initialize(_wowClient, _pathfindingClient, _loggerFactory.CreateLogger<WoWSharpObjectManager>());
            _botRunner = new BotRunnerService(WoWSharpObjectManager.Instance, _characterStateUpdateClient, _pathfindingClient);
            _logger.LogInformation("BotRunnerService initialized, starting bot...");
            _botRunner.Start();
            _logger.LogInformation("Bot started successfully. Bot is now running injected in WoW process...");
            await Task.Delay(1000);
        }

        private async Task MonitorBotHealth()
        {
            if (_botRunner == null)
            {
                _logger.LogWarning("BotRunner is null, attempting to reinitialize...");
                await InitializeBotComponents();
            }
        }

        private async Task PeriodicDiagnostics(CancellationToken cancellationToken)
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "BloogBotLogs", "injection.log");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, cancellationToken);
                    _logger.LogInformation("=== PERIODIC BOT STATUS CHECK ===");
                    _logger.LogInformation($"Bot Status: {(_botRunner != null ? "Running" : "Not Initialized")}");
                    _logger.LogInformation($"Process: {Process.GetCurrentProcess().ProcessName} (ID: {Process.GetCurrentProcess().Id})");
                    _logger.LogInformation("=====================================");
                    File.AppendAllText(logPath, $"Periodic check at {DateTime.Now:HH:mm:ss} - Bot: {(_botRunner != null ? "Running" : "Not Initialized")}\n");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in periodic diagnostics");
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ForegroundBotWorker stop requested...");
            _botRunner?.Stop();
            _pathfindingClient?.Close();
            _characterStateUpdateClient?.Close();
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("ForegroundBotWorker stopped.");
        }
    }
}
#endif
