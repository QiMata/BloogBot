using DecisionEngineService;
using ForegroundBotRunner;
using PromptHandlingService;
using System.Diagnostics;

namespace StateManager
{
    /// <summary>
    /// Tracks the state of the PathfindingService connection and process lifecycle.
    /// </summary>
    public enum PathfindingServiceState
    {
        /// <summary>Service was already running when StateManager started.</summary>
        AlreadyRunning,
        /// <summary>StateManager launched the service process and is waiting for first connection.</summary>
        Launched,
        /// <summary>Successfully connected to the service for the first time.</summary>
        Connected,
        /// <summary>Service process exited unexpectedly.</summary>
        ProcessExited,
        /// <summary>Connection failed after being previously connected.</summary>
        ConnectionLost,
        /// <summary>Timed out waiting for service to become available.</summary>
        TimedOut
    }

    public class Program
    {
        private static IConfiguration _configuration;
        private static Process _pathfindingProcess;
        private static PathfindingServiceState _serviceState = PathfindingServiceState.AlreadyRunning;

        private const int MaxRetries = 120; // 2 minutes max wait for nav/physics to load
        private const int RetryDelayMs = 1000;
        private const int QuickCheckDelayMs = 100; // Faster polling when we launched the process

        public static PathfindingServiceState ServiceState => _serviceState;

        public static void Main(string[] args)
        {
            // Build configuration using the executable base directory so the appsettings.json
            // that is copied to the output folder is actually found.
            var baseDir = AppContext.BaseDirectory;
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
                return;
            }

            // Check if PathfindingService is already running
            if (IsPathfindingServiceRunning())
            {
                _serviceState = PathfindingServiceState.AlreadyRunning;
                Console.WriteLine("PathfindingService is already running.");
            }
            else
            {
                // Launch PathfindingService and track the process
                _pathfindingProcess = LaunchPathfindingService();
                if (_pathfindingProcess != null)
                {
                    _serviceState = PathfindingServiceState.Launched;
                    _pathfindingProcess.EnableRaisingEvents = true;
                    _pathfindingProcess.Exited += OnPathfindingProcessExited;
                }
            }

            // Wait for PathfindingService to become available before starting bot profiles
            WaitForPathfindingService();

            CreateHostBuilder(args)
                .Build()
                .RunAsync();
        }

        private static Process LaunchPathfindingService()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "PathfindingService.dll",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                var process = Process.Start(processInfo);
                Console.WriteLine($"PathfindingService launched (PID: {process?.Id}).");
                return process;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch PathfindingService: {ex.Message}");
                return null;
            }
        }

        private static void OnPathfindingProcessExited(object sender, EventArgs e)
        {
            if (_serviceState == PathfindingServiceState.Launched)
            {
                // Process exited before we ever connected
                _serviceState = PathfindingServiceState.ProcessExited;
                Console.WriteLine($"PathfindingService process exited unexpectedly with code {_pathfindingProcess?.ExitCode}.");
            }
            else if (_serviceState == PathfindingServiceState.Connected)
            {
                // We had a connection but the process died
                _serviceState = PathfindingServiceState.ConnectionLost;
                Console.WriteLine($"PathfindingService process exited (code {_pathfindingProcess?.ExitCode}). Connection lost.");
            }
        }

        private static void WaitForPathfindingService()
        {
            var ipAddress = _configuration["PathfindingService:IpAddress"];
            var port = int.Parse(_configuration["PathfindingService:Port"]);

            // Only show waiting message if we launched the process (silent polling)
            bool weStartedProcess = _serviceState == PathfindingServiceState.Launched;
            if (weStartedProcess)
            {
                Console.WriteLine($"Waiting for PathfindingService at {ipAddress}:{port} (nav/physics loading may take time)...");
            }

            int delayMs = weStartedProcess ? QuickCheckDelayMs : RetryDelayMs;
            int maxAttempts = weStartedProcess ? MaxRetries * (RetryDelayMs / QuickCheckDelayMs) : MaxRetries;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Check if process died while we're waiting
                if (weStartedProcess && _serviceState == PathfindingServiceState.ProcessExited)
                {
                    throw new InvalidOperationException(
                        $"PathfindingService process exited with code {_pathfindingProcess?.ExitCode} before becoming available. " +
                        "Check the PathfindingService logs for errors.");
                }

                if (IsPathfindingServiceRunning())
                {
                    _serviceState = PathfindingServiceState.Connected;
                    if (weStartedProcess)
                    {
                        Console.WriteLine($"PathfindingService connected after {attempt * delayMs / 1000.0:F1}s.");
                    }
                    return;
                }

                // Silent polling - only log every 10 seconds if we launched the process
                if (weStartedProcess && attempt % (10000 / delayMs) == 0)
                {
                    Console.WriteLine($"Still waiting for PathfindingService... ({attempt * delayMs / 1000}s elapsed)");
                }

                Thread.Sleep(delayMs);
            }

            _serviceState = PathfindingServiceState.TimedOut;
            throw new TimeoutException(
                $"PathfindingService did not become available at {ipAddress}:{port} after {maxAttempts * delayMs / 1000} seconds. " +
                "The navigation mesh and physics data may still be loading, or there may be an error in the service.");
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

        /// <summary>
        /// Check if the PathfindingService is currently available.
        /// Can be called at runtime to verify service health.
        /// </summary>
        public static bool CheckPathfindingServiceHealth()
        {
            bool isRunning = IsPathfindingServiceRunning();
            
            if (!isRunning && _serviceState == PathfindingServiceState.Connected)
            {
                _serviceState = PathfindingServiceState.ConnectionLost;
            }
            else if (isRunning && _serviceState == PathfindingServiceState.ConnectionLost)
            {
                _serviceState = PathfindingServiceState.Connected;
            }

            return isRunning;
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
                    services.Configure<PathfindingServiceOptions>(hostContext.Configuration.GetSection("PathfindingService"));
                    services.AddHostedService<PathfindingServiceBootstrapper>();
                    services.AddHostedService<StateManagerWorker>();
                    services.AddHostedService<DecisionEngineWorker>();
                    services.AddHostedService<PromptHandlingServiceWorker>();
                    services.AddTransient<ForegroundBotWorker>(); // temporarily disabled for isolation
                });
    }
}
