using DecisionEngineService;
using ForegroundBotRunner;
using PromptHandlingService;
using System.Diagnostics;

namespace StateManager
{
    public class Program
    {
        private static IConfiguration _configuration;

        public static void Main(string[] args)
        {
            // Build configuration using the executable base directory so the appsettings.json
            // that is copied to the output folder is actually found.
            var baseDir = AppContext.BaseDirectory; // e.g. ...\Build\AnyCPU\Debug\net8.0\
            try
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(baseDir)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"FATAL: appsettings.json not found in '{baseDir}'. Ensure it is marked Copy to Output Directory = PreserveNewest. Exception: {ex.Message}");
                return; // Abort early – host builder would fail the same way
            }

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
                LaunchPathfindingServiceExecutable();

                // Wait for the service to become available
                Console.WriteLine("Waiting for PathfindingService to become available...");
                WaitForPathfindingServiceToStart();
            }
            else
            {
                Console.WriteLine("PathfindingService is already running.");
            }
        }

        private static void LaunchPathfindingServiceExecutable()
        {
            try
            {
                // Use unified build output structure
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var exePath = Path.Combine(baseDir, "PathfindingService.exe");
                var runtimeConfigPath = Path.Combine(baseDir, "PathfindingService.runtimeconfig.json");

                if (!File.Exists(exePath))
                {
                    Console.WriteLine($"PathfindingService executable not found at: {exePath}");
                    Console.WriteLine("Ensure the PathfindingService project has been built.");
                    return;
                }

                if (!File.Exists(runtimeConfigPath))
                {
                    Console.WriteLine($"PathfindingService runtime config not found at: {runtimeConfigPath}");
                    Console.WriteLine("Creating a default runtime config file...");
                    
                    // Create a minimal runtime config file
                    var runtimeConfig = @"{
  ""runtimeOptions"": {
    ""tfm"": ""net8.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""8.0.0""
    },
    ""configProperties"": {
      ""System.Reflection.Metadata.MetadataUpdater.IsSupported"": false
    }
  }
}";
                    File.WriteAllText(runtimeConfigPath, runtimeConfig);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"\"{exePath}\" & echo. & echo PathfindingService exited with code %ERRORLEVEL% & pause\"",
                    WorkingDirectory = baseDir,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process.Start(psi);
                Console.WriteLine("PathfindingService process started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch PathfindingService: {ex.Message}");
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
                var portValue = _configuration["PathfindingService:Port"];
                if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(portValue))
                {
                    return false;
                }
                var port = int.Parse(portValue);

                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect(ipAddress, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
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
                    // Ensure the base path is the executable directory for consistency
                    builder.SetBasePath(AppContext.BaseDirectory);
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
                    // services.AddTransient<ForegroundBotWorker>(); // temporarily disabled for isolation
                });
    }
}
