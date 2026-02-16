using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BotRunner;
using BotRunner.Clients;
using GameData.Core.Interfaces;

namespace ForegroundBotRunner
{
    public class MinimalLoader
    {
        private static volatile bool _botInitialized = false;
        private static volatile bool _shouldExit = false;
        private static Thread? _botThread;
        private static BotRunnerService? _botRunnerService;
        private static IServiceProvider? _serviceProvider;

        // Explicitly specify stdcall so it matches native declaration on x86.
        [UnmanagedCallersOnly(EntryPoint = "TestEntry", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int TestEntry(IntPtr argsPtr, int size)
        {
            try
            {
                // Write a tiny breadcrumb file so native side can see we executed
                TryWrite("testentry_stdcall.txt", "Entered stdcall TestEntry\n");
                
                // Start the actual bot functionality using existing BotRunnerService
                StartBotLogic();
                
                // Sleep a little to keep thread alive momentarily
                Thread.Sleep(50);
                return 42; // distinct code
            }
            catch (Exception ex)
            {
                TryWrite("testentry_stdcall_error.txt", $"Exception: {ex}\n");
                return -1;
            }
        }

        // cdecl variant for diagnostic comparison
        [UnmanagedCallersOnly(EntryPoint = "TestEntryCdecl", CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int TestEntryCdecl(IntPtr argsPtr, int size)
        {
            try
            {
                TryWrite("testentry_cdecl.txt", "Entered cdecl TestEntryCdecl\n");
                
                // Start the actual bot functionality using existing BotRunnerService
                StartBotLogic();
                
                Thread.Sleep(50);
                return 43; // distinct code
            }
            catch (Exception ex)
            {
                TryWrite("testentry_cdecl_error.txt", $"Exception: {ex}\n");
                return -1;
            }
        }

        private static void StartBotLogic()
        {
            if (_botInitialized)
                return;

            _botInitialized = true;
            TryWrite("bot_startup.txt", "Starting real BotRunnerService with UI automation...\n");

            // Start bot logic in a separate thread to avoid blocking the injection
            _botThread = new Thread(async () =>
            {
                try
                {
                    await RunRealBotServiceAsync();
                }
                catch (Exception ex)
                {
                    TryWrite("bot_logic_error.txt", $"BotRunnerService exception: {ex}\n");
                }
            })
            {
                IsBackground = false, // Keep process alive
                Name = "BotRunnerServiceThread"
            };

            _botThread.Start();
            TryWrite("bot_startup.txt", "BotRunnerService thread started with UI automation support.\n");
        }

        private static async Task RunRealBotServiceAsync()
        {
            TryWrite("bot_service_init.txt", "Initializing real BotRunnerService with DI container and UI automation...\n");

            try
            {
                // Set up dependency injection container with real services
                var services = new ServiceCollection();
                
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add real WoW ObjectManager
                services.AddSingleton<IObjectManager>(provider => 
                {
                    TryWrite("objectmanager_init.txt", "Initializing WoWSharpObjectManager...\n");
                    return WoWSharpClient.WoWSharpObjectManager.Instance;
                });

                // Add real communication clients
                services.AddSingleton<CharacterStateUpdateClient>(provider =>
                {
                    TryWrite("character_client_init.txt", "Initializing CharacterStateUpdateClient...\n");
                    // Configure with test account settings
                    return new CharacterStateUpdateClient("127.0.0.1", 8081, provider.GetRequiredService<ILogger<CharacterStateUpdateClient>>());
                });

                services.AddSingleton<PathfindingClient>(provider =>
                {
                    TryWrite("pathfinding_client_init.txt", "Initializing PathfindingClient...\n");
                    return new PathfindingClient("127.0.0.1", 5000, provider.GetRequiredService<ILogger<PathfindingClient>>());
                });

                // Add the main BotRunnerService
                services.AddSingleton<BotRunnerService>();

                _serviceProvider = services.BuildServiceProvider();
                TryWrite("bot_service_init.txt", "DI container configured successfully\n");

                // Wait a moment for WoW to fully load
                await Task.Delay(5000);

                // IMPLEMENTATION FROM MEMORY 88b52498-c116-4608-bb87-72628725859b:
                // Use UI automation to interact with WoW process like a real user
                TryWrite("ui_automation.txt", "Starting UI automation for character creation...\n");
                
                // Focus the WoW window first
                if (WoWUIAutomation.FocusWoWWindow())
                {
                    TryWrite("ui_automation.txt", "WoW window focused successfully\n");
                    
                    // Give WoW time to be ready for input
                    await Task.Delay(2000);
                    
                    // Try to handle character creation screen if we're there
                    TryWrite("ui_automation.txt", "Attempting character creation via UI automation...\n");
                    WoWUIAutomation.BotUIActions.ClickCharacterCreation();
                    await Task.Delay(1000);
                    
                    WoWUIAutomation.BotUIActions.CreateRandomCharacter();
                    await Task.Delay(3000);
                    
                    WoWUIAutomation.BotUIActions.EnterWorld();
                    await Task.Delay(5000);
                    
                    TryWrite("ui_automation.txt", "Character creation sequence completed\n");
                }
                else
                {
                    TryWrite("ui_automation.txt", "Could not focus WoW window, proceeding with ObjectManager only\n");
                }

                // Initialize the real BotRunnerService
                _botRunnerService = _serviceProvider.GetRequiredService<BotRunnerService>();
                TryWrite("bot_service_init.txt", "BotRunnerService instance created\n");

                // Wait for ObjectManager to initialize
                var objectManager = _serviceProvider.GetRequiredService<IObjectManager>();
                TryWrite("wow_init.txt", "Waiting for WoW ObjectManager to initialize...\n");
                
                // Give WoW time to fully load before starting bot logic
                await Task.Delay(10000);
                
                TryWrite("wow_init.txt", "Starting BotRunnerService...\n");
                
                // Start the real bot service
                _botRunnerService.Start();
                TryWrite("bot_service_running.txt", "BotRunnerService started successfully!\n");
                
                // Keep the service running and implement basic bot behavior
                while (!_shouldExit)
                {
                    await Task.Delay(1000);
                    
                    // Log status periodically and implement simple movement
                    if (objectManager.HasEnteredWorld && objectManager.Player != null)
                    {
                        TryWrite("bot_status.txt", $"STATUS: IN_WORLD\n" +
                                               $"Player: {objectManager.Player.Name ?? "Unknown"}\n" +
                                               $"Position: {objectManager.Player.Position.X:F2}, {objectManager.Player.Position.Y:F2}, {objectManager.Player.Position.Z:F2}\n" +
                                               $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                        
                        // Implement simple movement for integration test
                        await PerformTestMovement(objectManager);
                    }
                    else if (objectManager.LoginScreen.IsLoggedIn)
                    {
                        TryWrite("bot_status.txt", $"STATUS: LOGGED_IN\n" +
                                               $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                    }
                    else
                    {
                        TryWrite("bot_status.txt", $"STATUS: CONNECTING\n" +
                                               $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                TryWrite("bot_service_error.txt", $"Fatal error in BotRunnerService: {ex}\n");
                throw;
            }
            finally
            {
                // Clean up
                _botRunnerService?.Stop();
                (_serviceProvider as IDisposable)?.Dispose();
                TryWrite("bot_service_shutdown.txt", "BotRunnerService shut down\n");
            }
        }

        private static async Task PerformTestMovement(IObjectManager objectManager)
        {
            try
            {
                // Simple test movement - move forward for integration test validation
                var currentPos = objectManager.Player.Position;
                var targetPos = new GameData.Core.Models.Position(
                    currentPos.X + 10.0f,
                    currentPos.Y,
                    currentPos.Z
                );

                TryWrite("movement_test.txt", $"Moving from {currentPos} to {targetPos}\n");
                
                // Use both UI automation and ObjectManager for reliable movement
                WoWUIAutomation.BotUIActions.MoveTowardsDirection(2.0f, forward: true);
                
                // Also use ObjectManager for precise control
                objectManager.MoveToward(targetPos, 0.0f);
                
                await Task.Delay(3000); // Allow movement to complete
                
                var newPos = objectManager.Player.Position;
                var distanceMoved = currentPos.DistanceTo(newPos);
                
                TryWrite("movement_test.txt", $"Movement completed. New position: {newPos}, Distance moved: {distanceMoved:F2}\n");
            }
            catch (Exception ex)
            {
                TryWrite("movement_error.txt", $"Error during test movement: {ex}\n");
            }
        }

        private static void TryWrite(string fileName, string msg)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                var path = System.IO.Path.Combine(baseDir, fileName);
                var fullMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
                System.IO.File.AppendAllText(path, fullMsg);
            }
            catch { }
        }

        // Cleanup method for graceful shutdown
        public static void Shutdown()
        {
            _shouldExit = true;
            TryWrite("bot_shutdown.txt", "Bot shutdown requested\n");
            _botRunnerService?.Stop();
        }
    }
}