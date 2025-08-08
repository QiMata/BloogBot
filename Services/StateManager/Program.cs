using BackgroundBotRunner;
using DecisionEngineService;
using ForegroundBotRunner;
using PromptHandlingService;

namespace StateManager
{
    public class Program
    {
        private static IConfiguration _configuration;

        public static void Main(string[] args)
        {
            // Build configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Launch PathfindingService if not already running and wait for it to be ready
            EnsurePathfindingServiceIsAvailable();

            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        private static void EnsurePathfindingServiceIsAvailable()
        {
            if (!IsPathfindingServiceRunning())
            {
                Console.WriteLine("PathfindingService is not running. Launching...");
                PathfindingService.Program.LaunchServiceFromCommandLine();
                
                // Wait for the service to become available
                Console.WriteLine("Waiting for PathfindingService to become available...");
                WaitForPathfindingServiceToStart();
            }
            else
            {
                Console.WriteLine("PathfindingService is already running.");
            }
        }

        private static void WaitForPathfindingServiceToStart()
        {
            const int maxWaitTimeMs = 30000; // 30 seconds
            const int checkIntervalMs = 1000; // 1 second
            int elapsedMs = 0;

            while (elapsedMs < maxWaitTimeMs)
            {
                if (IsPathfindingServiceRunning())
                {
                    Console.WriteLine($"PathfindingService is now available after {elapsedMs / 1000} seconds.");
                    return;
                }

                Console.WriteLine($"Waiting for PathfindingService... ({elapsedMs / 1000}s/{maxWaitTimeMs / 1000}s)");
                Thread.Sleep(checkIntervalMs);
                elapsedMs += checkIntervalMs;
            }

            Console.WriteLine($"Warning: PathfindingService did not become available within {maxWaitTimeMs / 1000} seconds.");
            Console.WriteLine("Continuing anyway - clients will use retry logic to connect.");
        }

        private static bool IsPathfindingServiceRunning()
        {
            try
            {
                // Read PathfindingService connection info from appsettings.json
                var ipAddress = _configuration["PathfindingService:IpAddress"];
                var port = int.Parse(_configuration["PathfindingService:Port"]);

                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect(ipAddress, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500)); // Increased timeout slightly
                if (success)
                {
                    client.EndConnect(result);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    builder.AddEnvironmentVariables();

                    if (args != null)
                        builder.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<StateManagerWorker>();
                    services.AddHostedService<DecisionEngineWorker>();
                    services.AddHostedService<PromptHandlingServiceWorker>();
                    services.AddTransient<ForegroundBotWorker>();
                });
    }
}
