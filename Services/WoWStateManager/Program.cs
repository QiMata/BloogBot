using DecisionEngineService;
using ForegroundBotRunner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PromptHandlingService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace WoWStateManager
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
        TimedOut,
        /// <summary>Service is connected but not yet ready (loading navigation data).</summary>
        Loading,
        /// <summary>Service is fully ready to handle requests.</summary>
        Ready
    }

    /// <summary>
    /// Status information read from PathfindingService's status file.
    /// </summary>
    public class PathfindingServiceStatus
    {
        public bool IsReady { get; set; }
        public string StatusMessage { get; set; } = "";
        public List<uint> LoadedMaps { get; set; } = [];
        public DateTime Timestamp { get; set; }
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the path to the status file (in the PathfindingService directory).
        /// Checks base directory first (unified net8.0 output), then ../x64/ fallback.
        /// </summary>
        public static string GetStatusFilePath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var basePath = Path.Combine(baseDir, "pathfinding_status.json");
            if (File.Exists(basePath)) return basePath;

            var x64Dir = Path.GetFullPath(Path.Combine(baseDir, "..", "x64"));
            var x64Path = Path.Combine(x64Dir, "pathfinding_status.json");
            if (File.Exists(x64Path)) return x64Path;

            // Default to base dir (where PathfindingService will write it)
            return basePath;
        }

        /// <summary>
        /// Reads status from the status file. Returns null if file doesn't exist or is invalid.
        /// </summary>
        public static PathfindingServiceStatus? ReadFromFile()
        {
            var path = GetStatusFilePath();
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PathfindingServiceStatus>(json);
            }
            catch
            {
                return null;
            }
        }
    }

    public class Program
    {
        private static IConfiguration _configuration;
        private static PathfindingServiceState _serviceState = PathfindingServiceState.AlreadyRunning;

        private const int ExternalServiceMaxRetries = 15; // Keep external dependency waits short when StateManager does not own process launch.
        private const int RetryDelayMs = 1000;
        private const int SceneDataMaxRetries = 5; // Best-effort only; BG workers can fall back to local preloaded physics.
        private const int SceneDataRetryDelayMs = 500;

        public static PathfindingServiceState ServiceState => _serviceState;

        public static void Main(string[] args)
        {
            // Build configuration using the executable base directory so the appsettings.json
            // that is copied to the output folder is actually found.
            var baseDir = AppContext.BaseDirectory;
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Production;
            try
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(baseDir)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"FATAL: appsettings.json not found in '{baseDir}'. Ensure it is marked Copy to Output Directory = PreserveNewest. Exception: {ex.Message}");
                return;
            }

            TrySetEnvironmentVariableFromConfig("WWOW_REALMD_CONNECTION_STRING", _configuration.GetConnectionString("Realmd"));
            TrySetEnvironmentVariableFromConfig("WWOW_MANGOS_WORLD_CONNECTION_STRING", _configuration.GetConnectionString("MangosWorld"));

            if (IsPathfindingServiceRunning())
            {
                _serviceState = PathfindingServiceState.AlreadyRunning;
                Console.WriteLine("PathfindingService is already running.");
            }
            else
            {
                _serviceState = PathfindingServiceState.ConnectionLost;
                Console.WriteLine("PathfindingService is not running. StateManager expects it to be managed externally.");
            }

            WaitForPathfindingService();

            if (IsSceneDataServiceRunning())
            {
                Console.WriteLine("SceneDataService is already running.");
            }
            else
            {
                Console.WriteLine("SceneDataService is not running. StateManager expects it to be managed externally.");
            }

            WaitForSceneDataService();

            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        private static void WaitForPathfindingService()
        {
            var ipAddress = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var portValue = _configuration["PathfindingService:Port"];
            var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 5001;

            Console.WriteLine($"Waiting for PathfindingService at {ipAddress}:{port} (external dependency)...");

            const int delayMs = RetryDelayMs;
            int maxAttempts = ExternalServiceMaxRetries;
            var startTime = DateTime.Now;
            bool tcpConnected = false;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Phase 1: Wait for TCP connectivity
                if (!tcpConnected && IsPathfindingServiceRunning())
                {
                    tcpConnected = true;
                    _serviceState = PathfindingServiceState.Loading;
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"PathfindingService socket connected after {elapsed:F1}s. Waiting for navigation data to load...");
                }

                // Phase 2: Wait for ready status (navigation data loaded)
                // For Docker deployments, the status file may have a mismatched PID
                // (container PID != host PID). If TCP is connected and status says ready,
                // trust it regardless of PID.
                if (tcpConnected)
                {
                    var status = PathfindingServiceStatus.ReadFromFile();
                    if (status != null && status.IsReady)
                    {
                        _serviceState = PathfindingServiceState.Ready;
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        var mapsStr = status.LoadedMaps.Count > 0
                            ? $"Maps loaded: {string.Join(", ", status.LoadedMaps)}"
                            : "";
                        Console.WriteLine($"PathfindingService READY after {elapsed:F1}s. {mapsStr}");
                        return;
                    }

                    // No status file (Docker container writes to its own filesystem).
                    // If TCP connected, treat as ready after a brief grace period.
                    if (status == null && attempt >= 5)
                    {
                        _serviceState = PathfindingServiceState.Ready;
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        Console.WriteLine($"PathfindingService READY (TCP connected, no local status file — likely Docker). {elapsed:F1}s");
                        return;
                    }

                    if (attempt % 5 == 0)
                    {
                        Console.WriteLine(status != null
                            ? $"  PathfindingService status: {status.StatusMessage}"
                            : "  PathfindingService status file not found yet (Docker container uses internal filesystem).");
                    }
                }

                Thread.Sleep(delayMs);
            }

            _serviceState = PathfindingServiceState.TimedOut;
            Console.WriteLine(
                $"WARNING: PathfindingService did not become available at {ipAddress}:{port} after {maxAttempts * delayMs / 1000} seconds. " +
                "Proceeding without pathfinding. Navigation will fall back to direct movement.");
        }

        private static void WaitForSceneDataService()
        {
            var ipAddress = GetSceneDataServiceIpAddress();
            var port = GetSceneDataServicePort();
            Console.WriteLine($"Waiting for SceneDataService at {ipAddress}:{port} (external dependency)...");

            var startTime = DateTime.Now;
            for (int attempt = 1; attempt <= SceneDataMaxRetries; attempt++)
            {
                if (IsSceneDataServiceRunning())
                {
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"SceneDataService READY after {elapsed:F1}s.");
                    return;
                }

                if (attempt % 5 == 0)
                {
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"Still waiting for SceneDataService socket... ({elapsed:F0}s elapsed)");
                }

                Thread.Sleep(SceneDataRetryDelayMs);
            }

            Console.WriteLine(
                $"WARNING: SceneDataService did not become available at {ipAddress}:{port} after {(SceneDataMaxRetries * SceneDataRetryDelayMs) / 1000.0:F1} seconds. " +
                "Background bots will still launch and retry scene-slice acquisition on demand once the service becomes available.");
        }

        private static bool IsStatusFromLivePathfindingProcess(PathfindingServiceStatus status)
        {
            try
            {
                var process = Process.GetProcessById(status.ProcessId);
                if (process.HasExited)
                    return false;

                var processName = process.ProcessName;
                return processName.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
                    || processName.Contains("PathfindingService", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
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

        private static bool IsSceneDataServiceRunning()
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect(GetSceneDataServiceIpAddress(), GetSceneDataServicePort(), null, null);
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
                    builder.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    builder.AddEnvironmentVariables();

                    if (args != null)
                        builder.AddCommandLine(args);
                })
                .ConfigureLogging(logging =>
                {
                    // Avoid default Windows EventLog provider dependency in test/runtime environments.
                    logging.ClearProviders();
                    logging.AddConsole();
                    // Ensure all categories (including CharacterStateSocketListener / DungeoneeringCoordinator)
                    // log at Information level. Without this, ClearProviders + AddConsole defaults to Warning
                    // for some provider-category combos, hiding coordinator state transitions from test output.
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // MaNGOS server auto-launch (MySQL, realmd, mangosd)
                    services.Configure<MangosServerOptions>(hostContext.Configuration.GetSection("MangosServer"));
                    services.AddHostedService<MangosServerBootstrapper>();

                    services.AddHostedService<StateManagerWorker>();
                    services.AddHostedService<DecisionEngineWorker>();

                    // Register PromptCache for PromptHandlingServiceWorker
                    var promptCachePath = Path.Combine(AppContext.BaseDirectory, "prompt_cache.db");
                    services.AddSingleton(new PromptHandlingService.Cache.PromptCache(promptCachePath));
                    services.AddHostedService<PromptHandlingServiceWorker>();

                    services.AddTransient<ForegroundBotWorker>(); // temporarily disabled for isolation
                });

        private static void TrySetEnvironmentVariableFromConfig(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                return;

            Environment.SetEnvironmentVariable(name, value);
        }

        private static string GetSceneDataServiceIpAddress()
            => _configuration["SceneDataService:IpAddress"]
               ?? Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_IP")
               ?? "127.0.0.1";

        private static int GetSceneDataServicePort()
        {
            var portValue =
                _configuration["SceneDataService:Port"]
                ?? Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_PORT")
                ?? "5003";

            return int.TryParse(portValue, out var port) ? port : 5003;
        }
    }
}
