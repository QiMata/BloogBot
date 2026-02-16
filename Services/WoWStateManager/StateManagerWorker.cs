using Communication;
using WoWStateManager.Clients;
using WoWStateManager.Listeners;
using WoWStateManager.Logging;
using WoWStateManager.Repository;
using WoWStateManager.Settings;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using static WinProcessImports;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace WoWStateManager
{
    public class StateManagerWorker : BackgroundService
    {
        private readonly ILogger<StateManagerWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        private readonly CharacterStateSocketListener _activityMemberSocketListener;
        private readonly StateManagerSocketListener _worldStateManagerSocketListener;

        private readonly MangosSOAPClient _mangosSOAPClient;

        private readonly Dictionary<string, (IHostedService? Service, CancellationTokenSource TokenSource, Task asyncTask, uint? ProcessId)> _managedServices = [];
        private readonly object _managedServicesLock = new();

        // Prevent repeated launches each loop iteration
        private bool _initialLaunchCompleted = false;
        // Optional basic backoff tracking (account -> last launch time)
        private readonly Dictionary<string, DateTime> _lastLaunchTimes = new();
        private static readonly TimeSpan MinRelaunchInterval = TimeSpan.FromMinutes(1);

        // 63c: Named-pipe log servers � one per foreground bot account
        private readonly Dictionary<string, BotLogPipeServer> _botLogPipeServers = new();

        /// <summary>
        /// Shared mutable timestamp used to communicate the real injection time
        /// from the injection code path to the monitoring task closure.
        /// Uses Interlocked on ticks since DateTime is a struct and cannot be volatile.
        /// </summary>
        private sealed class InjectionTimestampHolder(DateTime initial)
        {
            private long _ticks = initial.Ticks;
            public DateTime Value
            {
                get => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);
                set => Interlocked.Exchange(ref _ticks, value.Ticks);
            }
        }

        public StateManagerWorker(
            ILogger<StateManagerWorker> logger,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
            _configuration = configuration;

            _mangosSOAPClient = new MangosSOAPClient(
                configuration["MangosSOAP:IpAddress"],
                _loggerFactory.CreateLogger<MangosSOAPClient>());

            _activityMemberSocketListener = new CharacterStateSocketListener(
                StateManagerSettings.Instance.CharacterSettings,
                configuration["CharacterStateListener:IpAddress"],
                int.Parse(configuration["CharacterStateListener:Port"]),
                _loggerFactory.CreateLogger<CharacterStateSocketListener>()
            );

            _logger.LogInformation($"Started ActivityMemberListener| {configuration["CharacterStateListener:IpAddress"]}:{configuration["CharacterStateListener:Port"]}");

            _worldStateManagerSocketListener = new StateManagerSocketListener(
                configuration["StateManagerListener:IpAddress"],
                int.Parse(configuration["StateManagerListener:Port"]),
                _loggerFactory.CreateLogger<StateManagerSocketListener>()
            );

            _logger.LogInformation($"Started StateManagerListener| {configuration["StateManagerListener:IpAddress"]}:{configuration["StateManagerListener:Port"]}");

            // Updated to new IObservable-based API
            _worldStateManagerSocketListener.DataMessageStream.Subscribe(OnWorldStateUpdate);
        }

        public void StartBackgroundBotWorker(string accountName)
        {
            var tokenSource = new CancellationTokenSource();

            // Launch BackgroundBotRunner as a separate process so each bot owns its own
            // WoWSharpObjectManager/EventEmitter singletons (no cross-contamination).
            var botExePath = Path.Combine(AppContext.BaseDirectory, "BackgroundBotRunner.dll");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = botExePath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.Environment["WWOW_ACCOUNT_NAME"] = accountName;
            psi.Environment["WWOW_ACCOUNT_PASSWORD"] = "PASSWORD";

            var process = Process.Start(psi);
            var pid = (uint?)process?.Id;

            // Forward stdout/stderr to our logger in background tasks
            if (process != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            var line = await process.StandardOutput.ReadLineAsync();
                            if (line != null) _logger.LogInformation($"[{accountName}] {line}");
                        }
                    }
                    catch { }
                });
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = await process.StandardError.ReadLineAsync();
                            if (line != null) _logger.LogWarning($"[{accountName}:ERR] {line}");
                        }
                    }
                    catch { }
                });
            }

            lock (_managedServicesLock)
            {
                _managedServices.Add(accountName, (null, tokenSource, Task.CompletedTask, pid));
            }
            _logger.LogInformation($"Started BackgroundBotRunner process for account {accountName} (PID: {pid})");
        }

        public void StartForegroundBotWorker(string accountName, int? targetProcessId = null)
        {
            // Backoff: prevent rapid re-launch loops if process dies immediately
            lock (_managedServicesLock)
            {
                if (_lastLaunchTimes.TryGetValue(accountName, out var last) && DateTime.UtcNow - last < MinRelaunchInterval)
                {
                    _logger.LogWarning($"Skipping launch for {accountName} - last attempt {DateTime.UtcNow - last:g} ago (< {MinRelaunchInterval}).");
                    return;
                }
                _lastLaunchTimes[accountName] = DateTime.UtcNow;
            }

            // Start WoW process and inject the bot worker service
            StartForegroundBotRunner(accountName, targetProcessId);
        }

        /// <summary>
        /// Gets the status of all managed bot processes
        /// </summary>
        public Dictionary<string, string> GetManagedBotStatus()
        {
            var status = new Dictionary<string, string>();

            List<KeyValuePair<string, (IHostedService? Service, CancellationTokenSource TokenSource, Task asyncTask, uint? ProcessId)>> snapshot;
            lock (_managedServicesLock)
            {
                snapshot = _managedServices.ToList();
            }

            foreach (var kvp in snapshot)
            {
                var accountName = kvp.Key;
                var (Service, TokenSource, Task, ProcessId) = kvp.Value;

                if (Service != null)
                {
                    // This is a hosted service (BackgroundBotWorker)
                    status[accountName] = Task.IsCompleted ? "Stopped" : "Running (Hosted Service)";
                }
                else
                {
                    // This is a WoW process with injected bot - check for state updates from CharacterStateSocketListener
                    var botStatus = GetForegroundBotStatus(accountName, ProcessId);
                    status[accountName] = botStatus;
                }
            }

            return status;
        }

        /// <summary>
        /// Gets detailed status for a foreground (injected) bot by checking both process state and communication state
        /// </summary>
        private string GetForegroundBotStatus(string accountName, uint? processId)
        {
            // Check if we're receiving state updates from the bot
            var hasStateUpdates = _activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snapshot);
            var hasRecentUpdate = hasStateUpdates && snapshot != null && !string.IsNullOrEmpty(snapshot.AccountName);

            // Check process state if we have a PID
            bool processRunning = false;
            if (processId.HasValue)
            {
                try
                {
                    var process = Process.GetProcessById((int)processId.Value);
                    processRunning = process != null && !process.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process no longer exists
                    processRunning = false;
                }
                catch (Exception)
                {
                    // Access denied or other error - assume still running if we can't check
                    processRunning = true;
                }
            }

            // Build status string based on what we know
            if (!processRunning && processId.HasValue)
            {
                return "Process Terminated";
            }

            if (hasRecentUpdate)
            {
                var playerInfo = snapshot?.Player?.Unit?.GameObject?.Base != null
                    ? $", GUID: {snapshot.Player.Unit.GameObject.Base.Guid}"
                    : "";
                return $"Running (PID: {processId}{playerInfo})";
            }

            if (processRunning)
            {
                return $"Running (PID: {processId}, No State Updates)";
            }

            return processId.HasValue ? $"Unknown (PID: {processId})" : "Not Started";
        }

        /// <summary>
        /// Gets detailed information about a specific managed bot
        /// </summary>
        public string GetBotDetails(string accountName)
        {
            (IHostedService? Service, CancellationTokenSource TokenSource, Task asyncTask, uint? ProcessId) serviceTuple;
            lock (_managedServicesLock)
            {
                if (!_managedServices.TryGetValue(accountName, out serviceTuple))
                {
                    return $"Account '{accountName}' not found in managed services.";
                }
            }

            var (Service, TokenSource, Task, ProcessId) = serviceTuple;

            if (Service != null)
            {
                return $"Account: {accountName}\nType: Hosted Background Service\nTask Status: {Task.Status}\nCancellation Requested: {TokenSource.Token.IsCancellationRequested}";
            }
            else
            {
                var pidInfo = ProcessId.HasValue ? $"PID: {ProcessId}" : "PID: Unknown";
                var stateInfo = "";
                if (_activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snapshot) && snapshot?.Player != null)
                {
                    var guid = snapshot.Player.Unit?.GameObject?.Base?.Guid ?? 0;
                    stateInfo = $"\nPlayer GUID: {guid}\nTimestamp: {snapshot.Timestamp}";
                }
                return $"Account: {accountName}\nType: Injected WoW Process\n{pidInfo}\nMonitoring Task Status: {Task.Status}\nCancellation Requested: {TokenSource.Token.IsCancellationRequested}{stateInfo}";
            }
        }

        private void OnWorldStateUpdate(AsyncRequest dataMessage)
        {
            _logger.LogInformation($"Received world state update message with ID {dataMessage.Id}.");

            StateChangeRequest stateChange = dataMessage.StateChange;

            if (stateChange != null)
            {
                string? parameterDetails = null;
                if (stateChange.RequestParameter != null)
                {
                    RequestParameter param = stateChange.RequestParameter;
                    parameterDetails = param.ParameterCase switch
                    {
                        RequestParameter.ParameterOneofCase.FloatParam => param.FloatParam.ToString(),
                        RequestParameter.ParameterOneofCase.IntParam => param.IntParam.ToString(),
                        RequestParameter.ParameterOneofCase.LongParam => param.LongParam.ToString(),
                        RequestParameter.ParameterOneofCase.StringParam => param.StringParam,
                        _ => null
                    };
                }

                _logger.LogInformation(
                    $"State change request received: ChangeType={stateChange.ChangeType}, Parameter={parameterDetails ?? "<none>"}");
            }

            StateChangeResponse stateChangeResponse = new();
            _worldStateManagerSocketListener.SendMessageToClient(dataMessage.Id, stateChangeResponse);
            _logger.LogInformation($"StateChangeResponse dispatched to {dataMessage.Id}.");
        }

        /// <summary>
        /// Checks if a service is listening on the specified IP and port.
        /// Returns true if the service is ready to accept connections.
        /// </summary>
        private async Task<bool> IsServiceReadyAsync(string ip, int port, TimeSpan timeout)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(timeout);

                // Attempt to connect with timeout
                var connectTask = client.ConnectAsync(ip, port);
                var delayTask = Task.Delay(Timeout.Infinite, cts.Token);

                var completedTask = await Task.WhenAny(connectTask, delayTask);

                if (completedTask == connectTask && connectTask.IsCompletedSuccessfully && client.Connected)
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Waits for required services (PathfindingService) to be ready before spawning WoW clients.
        /// </summary>
        private async Task<bool> WaitForRequiredServicesAsync(CancellationToken stoppingToken, TimeSpan timeout)
        {
            var pathfindingIp = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var pathfindingPortStr = _configuration["PathfindingService:Port"] ?? "5001";

            if (!int.TryParse(pathfindingPortStr, out var pathfindingPort))
            {
                _logger.LogError($"Invalid PathfindingService port configuration: {pathfindingPortStr}");
                return false;
            }

            _logger.LogInformation($"Checking PathfindingService readiness at {pathfindingIp}:{pathfindingPort}...");

            var sw = Stopwatch.StartNew();
            var checkInterval = TimeSpan.FromSeconds(2);

            while (sw.Elapsed < timeout && !stoppingToken.IsCancellationRequested)
            {
                var isReady = await IsServiceReadyAsync(pathfindingIp, pathfindingPort, TimeSpan.FromSeconds(5));

                if (isReady)
                {
                    _logger.LogInformation($"PathfindingService is READY at {pathfindingIp}:{pathfindingPort} (checked in {sw.Elapsed.TotalSeconds:F1}s)");
                    return true;
                }

                _logger.LogDebug($"PathfindingService not ready yet, waiting... ({sw.Elapsed.TotalSeconds:F0}s elapsed)");
                await Task.Delay(checkInterval, stoppingToken);
            }

            _logger.LogWarning($"PathfindingService at {pathfindingIp}:{pathfindingPort} is NOT ready after {timeout.TotalSeconds:F0}s");
            return false;
        }

        private async Task<bool> ApplyDesiredWorkerState(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"ApplyDesiredWorkerState called. CharacterSettings count: {StateManagerSettings.Instance.CharacterSettings.Count}");

            // Wait for PathfindingService to be ready before spawning any WoW clients
            // This prevents race conditions where WoW starts but services aren't available
            var serviceTimeout = TimeSpan.FromSeconds(30);
            var pathfindingReady = await WaitForRequiredServicesAsync(stoppingToken, serviceTimeout);

            if (!pathfindingReady)
            {
                _logger.LogWarning("PathfindingService is not ready - proceeding anyway but navigation may fail");
                // Note: We proceed anyway since the service might come online later, but we log the warning
            }

            // 62d: Deduplicate CharacterSettings by AccountName to prevent double-launch races
            var seenAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduplicatedSettings = new List<Settings.CharacterSettings>();
            foreach (var cs in StateManagerSettings.Instance.CharacterSettings)
            {
                if (!seenAccounts.Add(cs.AccountName))
                {
                    _logger.LogWarning($"Duplicate CharacterSettings entry for account '{cs.AccountName}' � skipping.");
                    continue;
                }
                deduplicatedSettings.Add(cs);
            }

            for (int i = 0; i < deduplicatedSettings.Count; i++)
            {
                var characterSettings = deduplicatedSettings[i];
                var accountName = characterSettings.AccountName;

                // Skip if not configured to run
                if (!characterSettings.ShouldRun)
                {
                    _logger.LogDebug($"Account {accountName} configured not to run, skipping");
                    continue;
                }

                // Already tracked? skip
                bool alreadyManaged;
                lock (_managedServicesLock)
                {
                    alreadyManaged = _managedServices.ContainsKey(accountName);
                }
                if (alreadyManaged)
                {
                    _logger.LogDebug($"Account {accountName} already managed, skipping");
                    continue;
                }

                _logger.LogInformation($"Setting up new bot for account: {accountName} (RunnerType: {characterSettings.RunnerType})");

                // Ensure the account exists in the database
                if (!ReamldRepository.CheckIfAccountExists(accountName))
                {
                    _logger.LogInformation($"Creating new account: {accountName}");
                    await _mangosSOAPClient.CreateAccountAsync(accountName);
                    await Task.Delay(100);
                    await _mangosSOAPClient.SetGMLevelAsync(accountName, 3);
                }

                // Start the appropriate bot worker based on RunnerType
                switch (characterSettings.RunnerType)
                {
                    case Settings.BotRunnerType.Foreground:
                        _logger.LogInformation($"Starting Foreground bot worker for {accountName} (DLL injection)");
                        StartForegroundBotWorker(accountName, characterSettings.TargetProcessId);
                        break;

                    case Settings.BotRunnerType.Background:
                        _logger.LogInformation($"Starting Background bot worker for {accountName} (headless)");
                        StartBackgroundBotWorker(accountName);
                        break;

                    default:
                        _logger.LogWarning($"Unknown RunnerType {characterSettings.RunnerType} for {accountName}, defaulting to Foreground");
                        StartForegroundBotWorker(accountName, characterSettings.TargetProcessId);
                        break;
                }

                // Small delay to prevent overwhelming the system
                await Task.Delay(100, stoppingToken);

                // Longer delay to allow the process to fully initialize
                await Task.Delay(500, stoppingToken);
            }

            return true;
        }

        private void StartForegroundBotRunner(string accountName, int? targetProcessId = null)
        {
            // Set the path to ForegroundBotRunner.dll in an environment variable
            var foregroundBotDllPath = Path.Combine(AppContext.BaseDirectory, "ForegroundBotRunner.dll");
            Environment.SetEnvironmentVariable("FOREGROUNDBOT_DLL_PATH", foregroundBotDllPath);
            // Environment.SetEnvironmentVariable("WWOW_WAIT_DEBUG", "1"); // Uncomment to wait for debugger attach
            // Environment.SetEnvironmentVariable("LOADER_PAUSE_ON_EXCEPTION", "1"); // Enable pause on exception for debugging

            // Pass account credentials to the injected ForegroundBotRunner via environment variables (backup)
            // The password is always "PASSWORD" as set by MangosSOAPClient.CreateAccountAsync
            Environment.SetEnvironmentVariable("WWOW_ACCOUNT_NAME", accountName);
            Environment.SetEnvironmentVariable("WWOW_ACCOUNT_PASSWORD", "PASSWORD");
            _logger.LogInformation($"Set credentials environment variables for ForegroundBotRunner: WWOW_ACCOUNT_NAME={accountName}");

            // Enable optional loader console + extra diagnostics (config flag or always on for now)
            // Add to appsettings.json if desired: "Injection:AllocateConsole": "true"
            var allocConsoleFlag = _configuration["Injection:AllocateConsole"];
            if (string.IsNullOrEmpty(allocConsoleFlag) || allocConsoleFlag.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("LOADER_ALLOC_CONSOLE", "1");
            }
            else
            {
                Environment.SetEnvironmentVariable("LOADER_ALLOC_CONSOLE", null); // ensure not set
            }

            // 63c: Start named-pipe log server BEFORE launching WoW so the bot can connect immediately
            if (!_botLogPipeServers.ContainsKey(accountName))
            {
                var pipeServer = new BotLogPipeServer(accountName, _loggerFactory);
                pipeServer.Start();
                _botLogPipeServers[accountName] = pipeServer;
            }

            var PATH_TO_GAME = _configuration["GameClient:ExecutablePath"];

            // Check if we're injecting into an existing process
            bool injectIntoExisting = targetProcessId.HasValue;
            if (injectIntoExisting)
            {
                _logger.LogInformation($"=== INJECTING INTO EXISTING PROCESS {targetProcessId.Value} ===");
            }

            var startupInfo = new STARTUPINFO();

            // Pre-injection diagnostics
            _logger.LogInformation("=== DLL INJECTION DIAGNOSTICS START ===");

            // Check if WoW.exe exists
            if (!File.Exists(PATH_TO_GAME))
            {
                _logger.LogError($"WoW.exe not found at path: {PATH_TO_GAME}");
                return;
            }
            _logger.LogInformation($"[OK] WoW.exe found at: {PATH_TO_GAME}");

            // Enhanced DLL Path Diagnostics - Check environment variable first, then fall back to config
            var loaderPath = Environment.GetEnvironmentVariable("WWOW_LOADER_DLL_PATH");
            if (string.IsNullOrWhiteSpace(loaderPath))
            {
                loaderPath = _configuration["LoaderDllPath"];
            }
            else
            {
                _logger.LogInformation($"Using WWOW_LOADER_DLL_PATH environment variable: {loaderPath}");
            }

            if (string.IsNullOrWhiteSpace(loaderPath))
            {
                _logger.LogError("Loader DLL path is not configured. Please set 'WWOW_LOADER_DLL_PATH' environment variable or 'LoaderDllPath' in appsettings.json.");
                return;
            }

            // Resolve relative paths relative to the application base directory
            if (!Path.IsPathRooted(loaderPath))
            {
                var appBaseDir = AppContext.BaseDirectory;

                // First check if the file exists relative to the app base directory
                var localPath = Path.Combine(appBaseDir, loaderPath);
                if (File.Exists(localPath))
                {
                    loaderPath = localPath;
                    _logger.LogInformation($"Resolved relative path to app directory: {loaderPath}");
                }
                else
                {
                    // Walk up from bin/Debug/net8.0 to find the repo root
                    var repoRoot = appBaseDir;
                    for (int i = 0; i < 5; i++)
                    {
                        if (File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
                        {
                            break;
                        }
                        var parent = Directory.GetParent(repoRoot);
                        if (parent == null) break;
                        repoRoot = parent.FullName;
                    }

                    loaderPath = Path.Combine(repoRoot, loaderPath);
                    _logger.LogInformation($"Resolved relative path to repo root: {loaderPath}");
                }
            }

            _logger.LogInformation("DLL FILE ANALYSIS");

            // Verify the DLL exists before attempting injection
            if (!File.Exists(loaderPath))
            {
                _logger.LogError($"[FAIL] Loader.dll not found at path: {loaderPath}");
                return;
            }
            _logger.LogInformation($"[OK] Loader.dll found at: {loaderPath}");

            // Check loader DLL architecture
            try
            {
                var loaderInfo = FileVersionInfo.GetVersionInfo(loaderPath);
                _logger.LogInformation($"[OK] Loader.dll version info: {loaderInfo.FileDescription ?? "N/A"}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[WARN] Could not read Loader.dll version info: {ex.Message}");
            }

            // Verify ForegroundBotRunner.dll exists
            var loaderDir = Path.GetDirectoryName(loaderPath);
            var foregroundBotPath = Path.Combine(loaderDir ?? "", "ForegroundBotRunner.dll");
            
            if (!File.Exists(foregroundBotPath))
            {
                _logger.LogError($"[FAIL] ForegroundBotRunner.dll not found at: {foregroundBotPath}");
                _logger.LogError("The C++ loader expects ForegroundBotRunner.dll to be in the same directory as Loader.dll");
                return;
            }
            _logger.LogInformation($"[OK] ForegroundBotRunner.dll found at: {foregroundBotPath}");

            // Check for runtime config
            var runtimeConfigPath = Path.Combine(loaderDir ?? "", "ForegroundBotRunner.runtimeconfig.json");
            if (!File.Exists(runtimeConfigPath))
            {
                _logger.LogError($"[FAIL] ForegroundBotRunner.runtimeconfig.json not found at: {runtimeConfigPath}");
                _logger.LogError("The .NET loader requires a runtime config file for proper .NET 8 hosting");
                return;
            }
            _logger.LogInformation($"[OK] ForegroundBotRunner.runtimeconfig.json found at: {runtimeConfigPath}");

            // Verify .NET 8 runtime availability
            try
            {
                var dotnetInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--info",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var dotnetProcess = Process.Start(dotnetInfo);
                if (dotnetProcess != null)
                {
                    var output = dotnetProcess.StandardOutput.ReadToEnd();
                    dotnetProcess.WaitForExit();
                    
                    if (output.Contains(".NET 8.0"))
                    {
                        _logger.LogInformation("[OK] .NET 8.0 runtime detected on system");
                    }
                    else
                    {
                        _logger.LogWarning("[WARN] .NET 8.0 runtime not clearly detected in dotnet --info output");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[WARN] Could not verify .NET runtime: {ex.Message}");
            }

            uint processId;
            IntPtr processHandle;

            if (injectIntoExisting)
            {
                // Inject into an existing WoW process
                try
                {
                    var process = Process.GetProcessById(targetProcessId.Value);
                    processId = (uint)process.Id;
                    processHandle = process.Handle;
                    _logger.LogInformation($"Found existing WoW process: {process.ProcessName} (PID: {processId})");
                }
                catch (ArgumentException)
                {
                    _logger.LogError($"[FAIL] Target process ID {targetProcessId.Value} not found");
                    return;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _logger.LogError($"[FAIL] Cannot access target process {targetProcessId.Value}: {ex.Message}");
                    _logger.LogError("Try running StateManager as Administrator");
                    return;
                }
            }
            else
            {
                // run WoW.exe in a new process
                var createResult = CreateProcess(
                    PATH_TO_GAME,
                    null,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ProcessCreationFlag.CREATE_DEFAULT_ERROR_MODE,
                    IntPtr.Zero,
                    null,
                    ref startupInfo,
                    out PROCESS_INFORMATION processInfo);

                if (!createResult || processInfo.hProcess == IntPtr.Zero)
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogError($"[FAIL] CreateProcess failed. Error Code: {lastError} (0x{lastError:X})");
                    _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                    return;
                }

                processId = processInfo.dwProcessId;
                processHandle = processInfo.hProcess;  // Use the handle directly from CreateProcess

                // Close the primary thread handle immediately � we only need the process handle
                if (processInfo.hThread != IntPtr.Zero)
                    CloseHandle(processInfo.hThread);

                _logger.LogInformation($"WoW.exe started for account {accountName} (Process ID: {processId}, Handle: 0x{processHandle:X})");
            }

            // IMPORTANT: Add to managed services IMMEDIATELY to prevent duplicate launches
            var tokenSource = new CancellationTokenSource();
            var capturedProcessId = processId;
            // Use a holder object so the monitoring closure always reads the latest timestamp
            // (the injection code path updates this after actual injection completes).
            var injectionTimestampHolder = new InjectionTimestampHolder(DateTime.UtcNow);
            var monitoringTask = Task.Run(async () =>
            {
                bool processExited = false;
                try
                {
                    // Wait a bit before starting to monitor to avoid interfering with injection
                    await Task.Delay(10000, tokenSource.Token);

                    // 62c: Orphan reaper � track whether the bot has phoned home
                    bool snapshotReceived = false;

                    // Monitor the WoW process
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        // Check if process is still running
                        try
                        {
                            var proc = Process.GetProcessById((int)capturedProcessId);
                            if (proc == null || proc.HasExited)
                            {
                                processExited = true;
                                break;
                            }
                        }
                        catch (ArgumentException)
                        {
                            processExited = true;
                            break;
                        }
                        catch
                        {
                            // Ignore other errors (access denied etc), just keep monitoring
                        }

                        // 62c: Check for snapshot phone-home; kill orphans after 60s from actual injection
                        if (!snapshotReceived)
                        {
                            if (_activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snap)
                                && snap != null
                                && !string.IsNullOrEmpty(snap.AccountName)
                                && snap.AccountName != "?")
                            {
                                snapshotReceived = true;
                                _logger.LogInformation($"Orphan reaper: snapshot received for {accountName} (PID {capturedProcessId})");
                            }
                            else if (DateTime.UtcNow > injectionTimestampHolder.Value.AddSeconds(60))
                            {
                                _logger.LogWarning($"Orphan reaper: no snapshot from {accountName} (PID {capturedProcessId}) within 60s of injection � killing process.");
                                try
                                {
                                    var orphan = Process.GetProcessById((int)capturedProcessId);
                                    orphan.Kill();
                                }
                                catch (Exception killEx)
                                {
                                    _logger.LogWarning(killEx, $"Orphan reaper: failed to kill PID {capturedProcessId}");
                                }
                                processExited = true;
                                break;
                            }
                        }

                        await Task.Delay(5000, tokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation - don't remove from tracking
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error monitoring foreground bot process for account {accountName} (will continue tracking)");
                }
                finally
                {
                    // Only remove from managed services if process actually exited
                    if (processExited)
                    {
                        lock (_managedServicesLock)
                        {
                            _managedServices.Remove(accountName);
                        }
                        // Reset the activity member slot so '?' assignment works on relaunch
                        if (_activityMemberSocketListener.CurrentActivityMemberList.ContainsKey(accountName))
                        {
                            _activityMemberSocketListener.CurrentActivityMemberList[accountName] = new();
                            _logger.LogInformation($"Reset activity member slot for account {accountName}");
                        }
                        _logger.LogInformation($"Foreground bot process for account {accountName} has exited and been removed from tracking");
                    }
                }
            });

            // Add to managed services IMMEDIATELY to prevent race condition with ApplyDesiredWorkerState
            lock (_managedServicesLock)
            {
                _managedServices.Add(accountName, (null, tokenSource, monitoringTask, processId));
            }
            _logger.LogInformation($"Added {accountName} to managed services with PID {processId} - preventing duplicate launches");

            // 62a: Poll for WoW window before injection (up to 15s)
            // Uses WaitForSingleObject + EnumWindows instead of Process.GetProcessById
            // to avoid "Access denied" when the test host isn't running elevated.
            {
                var windowPollTimeout = TimeSpan.FromSeconds(15);
                var windowPollSw = Stopwatch.StartNew();
                bool windowReady = false;

                while (windowPollSw.Elapsed < windowPollTimeout)
                {
                    // Check if process is still alive using the CreateProcess handle
                    var processWait = WaitForSingleObject(processHandle, 0);
                    if (processWait == WAIT_OBJECT_0)
                    {
                        _logger.LogError($"WoW process (PID {processId}) exited before window appeared. Aborting injection for {accountName}.");
                        RemoveManagedService(accountName, tokenSource);
                        CloseHandleSafe(processHandle);
                        return;
                    }

                    // Check for a visible window belonging to this process ID
                    bool foundWindow = false;
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint windowPid);
                        if (windowPid == processId && IsWindowVisible(hWnd))
                        {
                            foundWindow = true;
                            return false; // stop enumerating
                        }
                        return true; // continue
                    }, IntPtr.Zero);

                    if (foundWindow)
                    {
                        windowReady = true;
                        _logger.LogInformation($"WoW window detected for PID {processId} after {windowPollSw.Elapsed.TotalSeconds:F1}s");
                        break;
                    }

                    Thread.Sleep(250);
                }

                if (!windowReady)
                {
                    _logger.LogError($"WoW window did not appear within {windowPollTimeout.TotalSeconds}s for PID {processId}. Aborting injection for {accountName}.");
                    RemoveManagedService(accountName, tokenSource);
                    CloseHandleSafe(processHandle);
                    return;
                }
            }

            _logger.LogInformation($"ATTEMPTING DLL INJECTION: {loaderPath}");

            // allocate enough memory to hold the full file path to Loader.dll within the WoW process
            var loaderPathPtr = VirtualAllocEx(
                processHandle,
                (IntPtr)0,
                loaderPath.Length * 2, // Unicode characters are 2 bytes each
                MemoryAllocationType.MEM_COMMIT,
                MemoryProtectionType.PAGE_EXECUTE_READWRITE);

            if (loaderPathPtr == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to allocate memory in target process. Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                
                // Check if target process is still alive using the CreateProcess handle
                if (WaitForSingleObject(processHandle, 0) == WAIT_OBJECT_0)
                {
                    _logger.LogError($"[FAIL] Target WoW process (PID {processId}) has already exited");
                }
                
                return;
            }
            _logger.LogInformation($"[OK] Memory allocated at address: 0x{loaderPathPtr:X}");

            // Give some time for memory allocation to settle
            Thread.Sleep(500);

            // write the file path to Loader.dll to the WoW process's memory
            var bytes = Encoding.Unicode.GetBytes(loaderPath);
            var bytesWritten = 0;
            var writeResult = WriteProcessMemory(processHandle, loaderPathPtr, bytes, bytes.Length, ref bytesWritten);

            if (!writeResult || bytesWritten != bytes.Length)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to write DLL path to target process");
                _logger.LogError($"Bytes written: {bytesWritten}/{bytes.Length}");
                _logger.LogError($"Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);
                return;
            }
            _logger.LogInformation($"[OK] DLL path written to target process: {bytesWritten} bytes");

            // search current process for the memory address of the LoadLibraryW function within the kernel32.dll module
            var moduleHandle = GetModuleHandle("kernel32.dll");
            var loaderDllPointer = GetProcAddress(moduleHandle, "LoadLibraryW");

            if (loaderDllPointer == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to get LoadLibraryW function address");
                _logger.LogError($"Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);
                return;
            }
            _logger.LogInformation($"[OK] LoadLibraryW address: 0x{loaderDllPointer:X}");

            // Give some time before remote thread creation
            Thread.Sleep(500);

            _logger.LogInformation("Creating remote thread for DLL injection...");

            // create a new thread with the execution starting at the LoadLibraryW function, 
            // with the path to our Loader.dll passed as a parameter
            var threadHandle = CreateRemoteThread(processHandle, (IntPtr)null, (IntPtr)0, loaderDllPointer, loaderPathPtr, 0, (IntPtr)null);

            if (threadHandle == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to create remote thread for DLL injection");
                _logger.LogError($"Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");

                // Common reasons for remote thread creation failure
                if (lastError == 5) // ACCESS_DENIED
                {
                    _logger.LogError("ACCESS_DENIED - Target process may have higher privileges or be protected");
                    _logger.LogError("Try running StateManager as Administrator");
                }
                else if (lastError == 8) // NOT_ENOUGH_MEMORY
                {
                    _logger.LogError("NOT_ENOUGH_MEMORY - Insufficient memory in target process");
                }

                VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);
                return;
            }

            _logger.LogInformation($"[OK] Remote thread created successfully (Handle: 0x{threadHandle:X})");
            _logger.LogInformation("Waiting for injection to complete...");

            // Wait for the injection thread to complete (with timeout)
            var waitResult = WaitForSingleObject(threadHandle, 30000); // 30 second timeout for injection

            if (waitResult == 0) // WAIT_OBJECT_0
            {
                // Get the thread exit code (LoadLibrary return value)
                if (GetExitCodeThread(threadHandle, out var exitCode))
                {
                    if (exitCode != 0)
                    {
                        _logger.LogInformation($"SUCCESS: DLL injection completed successfully!");
                        _logger.LogInformation($"[OK] LoadLibrary returned: 0x{exitCode:X} (Module handle)");
                        
                        // Give the injected DLL time to initialize
                        Thread.Sleep(2000);
                        
                        // Check for loader breadcrumb files to verify execution
                        var baseDir = loaderDir;
                        var stdcallBreadcrumb = Path.Combine(baseDir ?? "", "testentry_stdcall.txt");
                        var cdeclBreadcrumb = Path.Combine(baseDir ?? "", "testentry_cdecl.txt");
                        
                        if (File.Exists(stdcallBreadcrumb))
                        {
                            _logger.LogInformation($"[OK] Managed code execution confirmed (stdcall breadcrumb found)");
                            try
                            {
                                var content = File.ReadAllText(stdcallBreadcrumb);
                                _logger.LogInformation($"[OK] Breadcrumb content: {content.Trim()}");
                            }
                            catch { }
                        }
                        else if (File.Exists(cdeclBreadcrumb))
                        {
                            _logger.LogInformation($"[OK] Managed code execution confirmed (cdecl breadcrumb found)");
                        }
                        else
                        {
                            _logger.LogWarning($"[WARN] No execution breadcrumbs found. Managed code may not have executed properly.");
                            _logger.LogWarning($"Expected files: {stdcallBreadcrumb} or {cdeclBreadcrumb}");
                        }
                    }
                    else
                    {
                        _logger.LogError($"[FAIL] DLL injection failed. LoadLibrary returned 0 (failed to load)");

                        // Enhanced error analysis for LoadLibrary failure
                        _logger.LogError("POSSIBLE CAUSES FOR LOADLIBRARY FAILURE:");
                        _logger.LogError("   - DLL architecture mismatch (32-bit vs 64-bit)");
                        _logger.LogError("   - Missing dependencies (.NET runtime, Visual C++ redistributables)");
                        _logger.LogError("   - DLL file is corrupted or invalid");
                        _logger.LogError("   - Insufficient permissions");
                        _logger.LogError("   - DLL path contains invalid characters");
                        _logger.LogError("   - Target process doesn't support .NET CLR hosting");
                        _logger.LogError("   - WoW.exe may have anti-debugging/injection protection");
                    }
                }
                else
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogWarning($"WARNING: Could not retrieve thread exit code");
                    _logger.LogWarning($"Error Code: {lastError} (0x{lastError:X})");
                    _logger.LogWarning($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                }
            }
            else
            {
                _logger.LogWarning($"WARNING: Thread wait timed out or failed. Wait result: {waitResult}");
                if (waitResult == 258) // WAIT_TIMEOUT
                {
                    _logger.LogWarning("Thread execution timed out after 30 seconds");
                    _logger.LogWarning("This may indicate the DLL is loading but taking a long time to initialize");
                }
                else if (waitResult == 0xFFFFFFFF) // WAIT_FAILED
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogWarning($"Wait failed with error: {lastError} (0x{lastError:X})");
                }
            }

            CloseHandle(threadHandle);

            // Give some time before cleanup
            Thread.Sleep(500);

            // free the memory that was allocated by VirtualAllocEx
            VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);

            // Update the injection timestamp so the orphan reaper uses the real time
            injectionTimestampHolder.Value = DateTime.UtcNow;

            // 62b: Wait for bot phone-home after injection (poll CurrentActivityMemberList, 30s timeout)
            {
                var phoneHomeTimeout = TimeSpan.FromSeconds(30);
                var phoneHomeSw = Stopwatch.StartNew();
                bool phoneHomeReceived = false;

                while (phoneHomeSw.Elapsed < phoneHomeTimeout)
                {
                    if (_activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snapshot)
                        && snapshot != null
                        && !string.IsNullOrEmpty(snapshot.AccountName)
                        && snapshot.AccountName != "?")
                    {
                        phoneHomeReceived = true;
                        _logger.LogInformation($"Bot phone-home received for {accountName} after {phoneHomeSw.Elapsed.TotalSeconds:F1}s");
                        break;
                    }

                    Thread.Sleep(500);
                }

                if (!phoneHomeReceived)
                {
                    _logger.LogWarning($"No phone-home snapshot from {accountName} within {phoneHomeTimeout.TotalSeconds}s of injection. Bot may be unresponsive (PID {processId}).");
                }
            }

            // Close the process handle � we're done with low-level manipulation.
            // The monitoring task uses Process.GetProcessById which opens its own handle.
            CloseHandleSafe(processHandle);

            _logger.LogInformation($"Foreground Bot Runner setup completed for account {accountName} (Process ID: {processId})");
            _logger.LogInformation("=== DLL INJECTION DIAGNOSTICS END ===");
        }

        /// <summary>
        /// Removes an account from managed services and cancels its monitoring task.
        /// Used when injection aborts after the entry was already added.
        /// </summary>
        private void RemoveManagedService(string accountName, CancellationTokenSource tokenSource)
        {
            tokenSource.Cancel();
            lock (_managedServicesLock)
            {
                _managedServices.Remove(accountName);
            }
            // Reset the activity member slot so '?' assignment works on relaunch
            if (_activityMemberSocketListener.CurrentActivityMemberList.ContainsKey(accountName))
            {
                _activityMemberSocketListener.CurrentActivityMemberList[accountName] = new();
            }
            _logger.LogInformation($"Removed {accountName} from managed services after injection abort");
        }

        /// <summary>
        /// Safely closes a Win32 handle, ignoring errors on IntPtr.Zero.
        /// </summary>
        private static void CloseHandleSafe(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                try { CloseHandle(handle); } catch { }
            }
        }

        public void StopManagedService(string accountName)
        {
            (IHostedService? Service, CancellationTokenSource TokenSource, Task asyncTask, uint? ProcessId) serviceTuple;
            bool found;
            lock (_managedServicesLock)
            {
                found = _managedServices.TryGetValue(accountName, out serviceTuple);
                if (found) _managedServices.Remove(accountName);
            }

            if (found)
            {
                var (Service, TokenSource, Task, ProcessId) = serviceTuple;

                _logger.LogInformation($"Stopping managed service for account {accountName}");

                // Cancel the token to signal shutdown
                TokenSource.Cancel();

                // If it's a background service, stop it properly
                if (Service != null)
                {
                    _ = System.Threading.Tasks.Task.Run(async () => await Service.StopAsync(CancellationToken.None));
                }

                _logger.LogInformation($"Stopped managed service for account {accountName}");
            }
            else
            {
                _logger.LogWarning($"Attempted to stop non-existent managed service for account {accountName}");
            }
        }

        /// <summary>
        /// Stops all managed services gracefully
        /// </summary>
        public async Task StopAllManagedServices()
        {
            _logger.LogInformation("Stopping all managed services...");

            List<KeyValuePair<string, (IHostedService? Service, CancellationTokenSource TokenSource, Task asyncTask, uint? ProcessId)>> servicesToStop;
            lock (_managedServicesLock)
            {
                servicesToStop = _managedServices.ToList();
            }

            foreach (var kvp in servicesToStop)
            {
                var accountName = kvp.Key;
                var (Service, TokenSource, Task, ProcessId) = kvp.Value;

                try
                {
                    _logger.LogInformation($"Stopping service for account {accountName}");

                    // Cancel the token
                    TokenSource.Cancel();

                    // Stop hosted services properly
                    if (Service != null)
                    {
                        await Service.StopAsync(CancellationToken.None);
                    }

                    // Wait for monitoring task to complete (with timeout)
                    try
                    {
                        await Task.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning($"Timeout waiting for monitoring task to complete for account {accountName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error stopping service for account {accountName}");
                }
            }

            // Clear all services
            lock (_managedServicesLock)
            {
                _managedServices.Clear();
            }

            // 63c: Dispose all named-pipe log servers
            foreach (var kvp in _botLogPipeServers)
            {
                try { kvp.Value.Dispose(); } catch { }
            }
            _botLogPipeServers.Clear();

            _logger.LogInformation("All managed services stopped");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"StateManagerServiceWorker is running.");
            stoppingToken.Register(() => _logger.LogInformation($"StateManagerServiceWorker is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Only launch bots once at startup (prevents repeated WoW.exe spawning)
                    if (!_initialLaunchCompleted)
                    {
                        // Set flag FIRST to prevent re-launch loops even if errors occur
                        _initialLaunchCompleted = true;
                        _logger.LogInformation("Beginning initial bot launch (will not retry on failure)");
                        await ApplyDesiredWorkerState(stoppingToken);
                        _logger.LogInformation("Initial bot launch completed.");
                    }

                    // Detect terminated processes and allow re-launch via ApplyDesiredWorkerState.
                    // The monitoring task removes dead entries from _managedServices, so if a
                    // configured account is no longer tracked it will be picked up again.
                    bool hasTerminated = false;
                    lock (_managedServicesLock)
                    {
                        foreach (var cs in StateManagerSettings.Instance.CharacterSettings)
                        {
                            if (cs.ShouldRun && !_managedServices.ContainsKey(cs.AccountName))
                            {
                                hasTerminated = true;
                                break;
                            }
                        }
                    }
                    if (hasTerminated)
                    {
                        _logger.LogInformation("Detected terminated bot process(es) � attempting re-launch");
                        await ApplyDesiredWorkerState(stoppingToken);
                    }

                    // Log status of managed services periodically
                    int serviceCount;
                    lock (_managedServicesLock)
                    {
                        serviceCount = _managedServices.Count;
                    }

                    if (serviceCount > 0)
                    {
                        var statusReport = GetManagedBotStatus();
                        _logger.LogInformation($"Managed Services Status: {string.Join(", ", statusReport.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
                    }
                    else
                    {
                        _logger.LogDebug("No managed services currently running");
                    }

                    await Task.Delay(5000, stoppingToken); // Check every 5 seconds
                }
                catch (OperationCanceledException)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in StateManagerWorker main loop");
                    await Task.Delay(1000, stoppingToken); // Brief delay before retrying
                }
            }

            await StopAllManagedServices();
            _logger.LogInformation($"StateManagerServiceWorker has stopped.");
        }
    }

}
