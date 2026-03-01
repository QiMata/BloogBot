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

            try
            {
                CreateHostBuilder(args)
                    .Build()
                    .Run();
            }
            finally
            {
                // Kill PathfindingService if WE launched it (don't kill pre-existing instances)
                if (_pathfindingProcess != null && _serviceState != PathfindingServiceState.AlreadyRunning)
                {
                    try
                    {
                        if (!_pathfindingProcess.HasExited)
                        {
                            Console.WriteLine($"Stopping PathfindingService (PID: {_pathfindingProcess.Id})...");
                            _pathfindingProcess.Kill();
                            _pathfindingProcess.WaitForExit(5000);
                            Console.WriteLine("PathfindingService stopped.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not stop PathfindingService: {ex.Message}");
                    }
                    finally
                    {
                        _pathfindingProcess.Dispose();
                        _pathfindingProcess = null;
                    }
                }
            }
        }

        private static Process LaunchPathfindingService()
        {
            try
            {
                // Try same directory first (unified net8.0 output), fall back to ../x64/
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var x64Dir = Path.GetFullPath(Path.Combine(baseDir, "..", "x64"));
                var dllPath = Path.Combine(baseDir, "PathfindingService.dll");
                var serviceDir = baseDir;

                if (!File.Exists(dllPath))
                {
                    dllPath = Path.Combine(x64Dir, "PathfindingService.dll");
                    serviceDir = x64Dir;
                }

                if (!File.Exists(dllPath))
                {
                    Console.WriteLine($"PathfindingService.dll not found at: {Path.Combine(baseDir, "PathfindingService.dll")} or {Path.Combine(x64Dir, "PathfindingService.dll")}");
                    Console.WriteLine("Build the solution first: dotnet build WestworldOfWarcraft.sln");
                    return null;
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{dllPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = Environment.GetEnvironmentVariable("WWOW_SHOW_WINDOWS") != "1",
                    WorkingDirectory = serviceDir
                };

                var process = Process.Start(processInfo);
                Console.WriteLine($"PathfindingService launched from {serviceDir} (PID: {process?.Id}).");
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
                    if (status != null)
                    {
                        bool statusMatchesLiveService = IsStatusFromLivePathfindingProcess(status, weStartedProcess);
                        if (status.IsReady && statusMatchesLiveService)
                        {
                            _serviceState = PathfindingServiceState.Ready;
                            var elapsed = (DateTime.Now - startTime).TotalSeconds;
                            var mapsStr = status.LoadedMaps.Count > 0
                                ? $"Maps loaded: {string.Join(", ", status.LoadedMaps)}"
                                : "";
                            Console.WriteLine($"PathfindingService READY after {elapsed:F1}s. {mapsStr}");
                            return;
                        }

                        // Status exists but not usable yet (still loading or stale/mismatched PID)
                        if (attempt % (5000 / delayMs) == 0)
                        {
                            if (!statusMatchesLiveService)
                            {
                                Console.WriteLine(
                                    $"  PathfindingService status ignored (stale/mismatched PID {status.ProcessId}). Waiting for live ready status...");
                            }
                            else
                            {
                                Console.WriteLine($"  PathfindingService status: {status.StatusMessage}");
                            }
                        }
                    }
                    else if (attempt % (5000 / delayMs) == 0)
                    {
                        Console.WriteLine("  PathfindingService status file not found yet. Waiting for ready status...");
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

        private static bool IsStatusFromLivePathfindingProcess(PathfindingServiceStatus status, bool weStartedProcess)
        {
            try
            {
                var process = Process.GetProcessById(status.ProcessId);
                if (process.HasExited)
                    return false;

                // If we launched the service, require exact PID match to avoid stale-status false positives.
                if (weStartedProcess && _pathfindingProcess != null)
                    return process.Id == _pathfindingProcess.Id;

                // For pre-existing service, we can't know PID upfront; require plausible process identity.
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
                .ConfigureLogging(logging =>
                {
                    // Avoid default Windows EventLog provider dependency in test/runtime environments.
                    logging.ClearProviders();
                    logging.AddConsole();
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
