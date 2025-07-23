using BotRunner;
using BotRunner.Clients;
using Communication;
using ForegroundBotRunner.Statics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForegroundBotRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting ForegroundBotRunner...");

                // Simple configuration using hard-coded values for injection context
                var configDict = new Dictionary<string, string?>
                {
                    ["PathfindingService:IpAddress"] = "127.0.0.1",
                    ["PathfindingService:Port"] = "5000",
                    ["CharacterStateListener:IpAddress"] = "127.0.0.1",
                    ["CharacterStateListener:Port"] = "5002"
                };

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configDict)
                    .Build();

                // With the following to provide the required 'configure' argument:
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                var logger = loggerFactory.CreateLogger<Program>();

                logger.LogInformation("ForegroundBotRunner starting in injected context...");

                // Create ActivitySnapshot for tracking character state
                var activitySnapshot = new ActivitySnapshot();

                // Create core components directly (since we're in the injected WoW process)
                var eventHandler = WoWEventHandler.Instance;
                var objectManager = new ObjectManager(eventHandler, activitySnapshot);

                // Create client connections to external services
                var pathfindingClient = new PathfindingClient(
                    configuration["PathfindingService:IpAddress"]!,
                    int.Parse(configuration["PathfindingService:Port"]!),
                    loggerFactory.CreateLogger<PathfindingClient>()
                );

                var characterStateUpdateClient = new CharacterStateUpdateClient(
                    configuration["CharacterStateListener:IpAddress"]!,
                    int.Parse(configuration["CharacterStateListener:Port"]!),
                    loggerFactory.CreateLogger<CharacterStateUpdateClient>()
                );

                // Create BotRunnerService with the proper dependencies
                var botRunner = new BotRunnerService(objectManager, characterStateUpdateClient, pathfindingClient);

                logger.LogInformation("BotRunnerService initialized, starting bot...");

                // Start the bot
                botRunner.Start();

                logger.LogInformation("Bot started successfully. Bot is now running injected in WoW process...");

                // Keep the application running (since we're injected, we run indefinitely)
                var cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cancellationTokenSource.Cancel();
                    logger.LogInformation("Shutdown requested...");
                };

                // Wait for cancellation or WoW process termination
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }

                logger.LogInformation("Shutting down...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in ForegroundBotRunner: {ex}");
                Environment.Exit(1);
            }
        }
    }
}
