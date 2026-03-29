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
    public partial class StateManagerWorker : BackgroundService
    {
        private readonly ILogger<StateManagerWorker> _logger;

        private readonly ILoggerFactory _loggerFactory;

        private readonly IConfiguration _configuration;

        private readonly IServiceProvider _serviceProvider;


        private readonly CharacterStateSocketListener _activityMemberSocketListener;

        private readonly StateManagerSocketListener _worldStateManagerSocketListener;


        private readonly MangosSOAPClient _mangosSOAPClient;

        private readonly Dictionary<string, (IHostedService? Service, CancellationTokenSource TokenSource, Task asyncTask, uint? ProcessId)> _managedServices = [];

        // Optional basic backoff tracking (account -> last launch time)
        private readonly Dictionary<string, DateTime> _lastLaunchTimes = new();


        // 63c: Named-pipe log servers � one per foreground bot account
        private readonly Dictionary<string, BotLogPipeServer> _botLogPipeServers = new();

        /// <summary>
        /// Shared mutable timestamp used to communicate the real injection time
        /// from the injection code path to the monitoring task closure.
        /// Uses Interlocked on ticks since DateTime is a struct and cannot be volatile.
        /// </summary>


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

            var progressionPlanner = new Progression.ProgressionPlanner(
                _loggerFactory.CreateLogger<Progression.ProgressionPlanner>());

            _activityMemberSocketListener = new CharacterStateSocketListener(
                StateManagerSettings.Instance.CharacterSettings,
                configuration["CharacterStateListener:IpAddress"],
                int.Parse(configuration["CharacterStateListener:Port"]),
                _mangosSOAPClient,
                progressionPlanner,
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

                    // Also check SceneDataService availability (non-blocking — it's optional)
                    var sceneDataIp = _configuration["SceneDataService:IpAddress"] ?? "127.0.0.1";
                    var sceneDataPortStr = _configuration["SceneDataService:Port"] ?? "5003";
                    if (int.TryParse(sceneDataPortStr, out var sceneDataPort))
                    {
                        var sceneReady = await IsServiceReadyAsync(sceneDataIp, sceneDataPort, TimeSpan.FromSeconds(2));
                        _logger.LogInformation($"SceneDataService at {sceneDataIp}:{sceneDataPort}: {(sceneReady ? "READY" : "not available (optional)")}");
                    }

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
                _logger.LogError("PathfindingService is not ready after {Timeout}s — aborting bot startup. Navigation requires PathfindingService on port 5001.", serviceTimeout.TotalSeconds);
                return false;
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
                }

                // Always ensure GM level 6 (console) via direct DB update (SOAP writes to account_access
                // but brotalnia's build reads from account.gmlevel at login)
                _logger.LogInformation($"Ensuring GM level 6 for account: {accountName}");
                var gmResult = ReamldRepository.SetGMLevel(accountName, 6);
                _logger.LogInformation($"SetGMLevel result for {accountName}: {gmResult}");

                // Start the appropriate bot worker based on RunnerType
                switch (characterSettings.RunnerType)
                {
                    case Settings.BotRunnerType.Foreground:
                        _logger.LogInformation($"Starting Foreground bot worker for {accountName} (DLL injection)");
                        StartForegroundBotWorker(accountName, characterSettings.TargetProcessId, characterSettings.CharacterClass, characterSettings.CharacterRace, characterSettings.CharacterGender, characterSettings.BuildConfig?.SpecName, characterSettings.BuildConfig?.TalentBuildName);
                        break;

                    case Settings.BotRunnerType.Background:
                        _logger.LogInformation($"Starting Background bot worker for {accountName} (headless)");
                        StartBackgroundBotWorker(accountName, characterSettings.CharacterClass, characterSettings.CharacterRace, characterSettings.CharacterGender, characterSettings.BuildConfig?.SpecName, characterSettings.BuildConfig?.TalentBuildName);
                        break;

                    default:
                        _logger.LogWarning($"Unknown RunnerType {characterSettings.RunnerType} for {accountName}, defaulting to Foreground");
                        StartForegroundBotWorker(accountName, characterSettings.TargetProcessId, characterSettings.CharacterClass, characterSettings.CharacterRace, characterSettings.CharacterGender, characterSettings.BuildConfig?.SpecName, characterSettings.BuildConfig?.TalentBuildName);
                        break;
                }

                // Small delay to prevent overwhelming the system
                await Task.Delay(100, stoppingToken);

                // Longer delay to allow the process to fully initialize
                await Task.Delay(500, stoppingToken);
            }

            return true;
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
                    try { await Task.Delay(1000, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }

            _logger.LogInformation($"StateManagerServiceWorker has stopped.");
        }

        /// <summary>
        /// Guaranteed cleanup on host shutdown — even if ExecuteAsync throws.
        /// The .NET host calls StopAsync on all IHostedService instances during shutdown.
        /// </summary>


        /// <summary>
        /// Guaranteed cleanup on host shutdown — even if ExecuteAsync throws.
        /// The .NET host calls StopAsync on all IHostedService instances during shutdown.
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StateManagerWorker.StopAsync — cleaning up managed processes...");
            await StopAllManagedServices();
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("StateManagerWorker.StopAsync — complete.");
        }
    }
}
