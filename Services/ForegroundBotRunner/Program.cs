using BotRunner;
using BotRunner.Clients;
using Communication;
using ForegroundBotRunner.Statics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ForegroundBotRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting ForegroundBotRunner...");

                // Create a log file for injection diagnostics
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"\n=== ForegroundBotRunner Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");

                // Display process information immediately for injection confirmation
                DisplayProcessInfo();

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

                // Create logger factory for injection context
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                var logger = loggerFactory.CreateLogger<Program>();

                logger.LogInformation("ForegroundBotRunner starting in injected context...");
                logger.LogInformation($"Current Process: {Process.GetCurrentProcess().ProcessName} (ID: {Process.GetCurrentProcess().Id})");

                File.AppendAllText(logPath, $"Logger created successfully. Process: {Process.GetCurrentProcess().ProcessName}\n");

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
                File.AppendAllText(logPath, "BotRunnerService created successfully\n");

                // Start the bot
                botRunner.Start();

                logger.LogInformation("Bot started successfully. Bot is now running injected in WoW process...");
                File.AppendAllText(logPath, "Bot started successfully!\n");

                // Start a background task to periodically display process info
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(30000); // Every 30 seconds
                        Console.WriteLine("\n=== PERIODIC INJECTION STATUS CHECK ===");
                        DisplayProcessInfo();
                        Console.WriteLine("========================================\n");
                        
                        File.AppendAllText(logPath, $"Periodic check at {DateTime.Now:HH:mm:ss} - Still running\n");
                    }
                });

                // Keep the application running (since we're injected, we run indefinitely)
                var cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cancellationTokenSource.Cancel();
                    logger.LogInformation("Shutdown requested...");
                    File.AppendAllText(logPath, "Shutdown requested\n");
                };

                // Wait for cancellation or WoW process termination
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }

                logger.LogInformation("Shutting down...");
                File.AppendAllText(logPath, "Shutting down normally\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in ForegroundBotRunner: {ex}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"FATAL ERROR: {ex}\n");
                
                Environment.Exit(1);
            }
        }

        private static void DisplayProcessInfo()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                
                Console.WriteLine("=== INJECTION CONFIRMATION ===");
                Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Process Name: {currentProcess.ProcessName}");
                Console.WriteLine($"Process ID: {currentProcess.Id}");
                Console.WriteLine($"Main Module: {currentProcess.MainModule?.FileName ?? "N/A"}");
                Console.WriteLine($"Main Window Title: {currentProcess.MainWindowTitle}");
                Console.WriteLine($"Working Set (MB): {currentProcess.WorkingSet64 / 1024 / 1024:N2}");
                Console.WriteLine($"Thread Count: {currentProcess.Threads.Count}");
                Console.WriteLine($"Base Address: 0x{currentProcess.MainModule?.BaseAddress.ToString("X")}");
                
                // Check if we're running in WoW.exe
                var isWoWProcess = currentProcess.ProcessName.ToLower().Contains("wow") || 
                                 currentProcess.MainModule?.FileName?.ToLower().Contains("wow") == true;
                
                Console.WriteLine($"*** INJECTION STATUS: {(isWoWProcess ? "SUCCESS - Running in WoW Process!" : "WARNING - Not in WoW Process")} ***");
                
                // Show some loaded modules
                try
                {
                    var relevantModules = new List<string>();
                    foreach (ProcessModule module in currentProcess.Modules)
                    {
                        var moduleName = module.ModuleName?.ToLower();
                        if (moduleName != null && (moduleName.Contains("loader") || 
                                                 moduleName.Contains("fastcall") || 
                                                 moduleName.Contains("navigation") ||
                                                 moduleName.Contains("wow")))
                        {
                            relevantModules.Add($"  - {module.ModuleName} (0x{module.BaseAddress:X})");
                        }
                    }
                    
                    if (relevantModules.Any())
                    {
                        Console.WriteLine("Relevant Loaded Modules:");
                        foreach (var module in relevantModules.Take(5)) // Limit for readability
                        {
                            Console.WriteLine(module);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading modules: {ex.Message}");
                }
                
                Console.WriteLine("==============================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying process info: {ex.Message}");
            }
        }
    }
}
