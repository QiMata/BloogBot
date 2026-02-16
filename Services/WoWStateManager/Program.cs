using DecisionEngineService;
using ForegroundBotRunner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        /// </summary>
        public static string GetStatusFilePath()
        {
            var x64Dir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "x64"));
            return Path.Combine(x64Dir, "pathfinding_status.json");
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
                .Run();
        }

        private static Process LaunchPathfindingService()
        {
            try
            {
                // PathfindingService is x64 — lives in Bot/{Config}/x64/, not Bot/{Config}/net8.0/
                var x64Dir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "x64"));
                var dllPath = Path.Combine(x64Dir, "PathfindingService.dll");

                if (!File.Exists(dllPath))
                {
                    Console.WriteLine($"PathfindingService.dll not found at: {dllPath}");
                    Console.WriteLine("Build the solution first: dotnet build WestworldOfWarcraft.sln");
                    return null;
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{dllPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = x64Dir
                };

                var process = Process.Start(processInfo);
                Console.WriteLine($"PathfindingService launched from {x64Dir} (PID: {process?.Id}).");
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
            else if (_serviceState == PathfindingServiceState.Loading)
            {
                // Process exited while loading navigation data
                _serviceState = PathfindingServiceState.ProcessExited;
                Console.WriteLine($"PathfindingService process exited during initialization (code {_pathfindingProcess?.ExitCode}).");
            }
            else if (_serviceState == PathfindingServiceState.Connected || _serviceState == PathfindingServiceState.Ready)
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
                Console.WriteLine($"Waiting for PathfindingService at {ipAddress}:{port}...");
            }

            int delayMs = weStartedProcess ? QuickCheckDelayMs : RetryDelayMs;
            int maxAttempts = weStartedProcess ? MaxRetries * (RetryDelayMs / QuickCheckDelayMs) : MaxRetries;
            var startTime = DateTime.Now;
            bool tcpConnected = false;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Check if process died while we're waiting
                if (weStartedProcess && _serviceState == PathfindingServiceState.ProcessExited)
                {
                    Console.WriteLine(
                        $"WARNING: PathfindingService process exited with code {_pathfindingProcess?.ExitCode} before becoming available. " +
                        "Proceeding without pathfinding. Navigation will fall back to direct movement.");
                    return;
                }

                // Phase 1: Wait for TCP connectivity
                if (!tcpConnected && IsPathfindingServiceRunning())
                {
                    tcpConnected = true;
                    _serviceState = PathfindingServiceState.Loading;
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"PathfindingService socket connected after {elapsed:F1}s. Waiting for navigation data to load...");
                }

                // Phase 2: Wait for ready status (navigation data loaded)
                if (tcpConnected)
                {
                    var status = PathfindingServiceStatus.ReadFromFile();
                    if (status?.IsReady == true)
                    {
                        _serviceState = PathfindingServiceState.Ready;
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        var mapsStr = status.LoadedMaps.Count > 0
                            ? $"Maps loaded: {string.Join(", ", status.LoadedMaps)}"
                            : "";
                        Console.WriteLine($"PathfindingService READY after {elapsed:F1}s. {mapsStr}");
                        return;
                    }
                    else if (status != null)
                    {
                        // Check if status file is stale (from a different process)
                        bool isStaleStatus = false;
                        try
                        {
                            var runningProcess = Process.GetProcessById(status.ProcessId);
                            // Process exists but check if it's actually PathfindingService
                            isStaleStatus = runningProcess.HasExited;
                        }
                        catch
                        {
                            // Process not found - status file is stale
                            isStaleStatus = true;
                        }

                        if (isStaleStatus)
                        {
                            // Status file is from a dead process but TCP is connected -
                            // the running service is ready but didn't update the file
                            _serviceState = PathfindingServiceState.Ready;
                            var elapsed = (DateTime.Now - startTime).TotalSeconds;
                            Console.WriteLine($"PathfindingService READY after {elapsed:F1}s (status file stale from PID {status.ProcessId}, TCP connected).");
                            return;
                        }

                        // Status file exists but not ready yet - show current status periodically
                        if (attempt % (5000 / delayMs) == 0)
                        {
                            Console.WriteLine($"  PathfindingService status: {status.StatusMessage}");
                        }
                    }
                    else
                    {
                        // No status file but TCP connected - service is ready
                        _serviceState = PathfindingServiceState.Ready;
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        Console.WriteLine($"PathfindingService READY after {elapsed:F1}s (TCP connected, no status file).");
                        return;
                    }
                }

                // Silent polling - only log every 10 seconds if we launched the process
                if (weStartedProcess && !tcpConnected && attempt % (10000 / delayMs) == 0)
                {
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"Still waiting for PathfindingService socket... ({elapsed:F0}s elapsed)");
                }

                Thread.Sleep(delayMs);
            }

            _serviceState = PathfindingServiceState.TimedOut;
            Console.WriteLine(
                $"WARNING: PathfindingService did not become available at {ipAddress}:{port} after {maxAttempts * delayMs / 1000} seconds. " +
                "Proceeding without pathfinding. Navigation will fall back to direct movement.");
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
                // PathfindingService is x64 — lives in Bot/{Config}/x64/
                var x64Dir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "x64"));
                var exePath = Path.Combine(x64Dir, "PathfindingService.exe");
                var dllPath = Path.Combine(x64Dir, "PathfindingService.dll");

                ProcessStartInfo psi;
                if (File.Exists(exePath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = x64Dir,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else if (File.Exists(dllPath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"\"{dllPath}\"",
                        WorkingDirectory = x64Dir,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else
                {
                    Console.WriteLine($"PathfindingService not found at: {exePath} or {dllPath}");
                    Console.WriteLine("Build the solution first: dotnet build WestworldOfWarcraft.sln");
                    return;
                }

                Process.Start(psi);
                Console.WriteLine($"PathfindingService process started from {x64Dir}.");
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
    }
}
