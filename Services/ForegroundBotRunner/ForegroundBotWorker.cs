using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BotRunner;
using BotRunner.Clients;
using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Movement;
using Communication;
using ForegroundBotRunner.Logging;
using ForegroundBotRunner.Mem.Hooks;
using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#if NET8_0_OR_GREATER
namespace ForegroundBotRunner
{
    /// <summary>
    /// Worker that runs the bot using direct memory access when injected into WoW process.
    /// Uses BotRunnerService for all login, snapshot, and action dispatch — identical to BackgroundBotRunner.
    /// ForegroundBotWorker is a thin shell: create ObjectManager → create BotRunnerService → run anti-AFK.
    /// </summary>
    public class ForegroundBotWorker : BackgroundService
    {
        private readonly ILogger<ForegroundBotWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;

        private ObjectManager? _objectManager;
        private CharacterStateUpdateClient? _stateUpdateClient;
        private MovementRecorder? _movementRecorder;
        private PathfindingClient? _pathfindingClient;
        private BotRunnerService? _botRunner;

        // Automated recording: runs controlled movement scenarios instead of idle
        private readonly bool _automatedRecording;
        private Task? _scenarioRunnerTask;

        // Login credentials received from StateManager via IPC
        private string _accountName = string.Empty;

        // Track whether SignalEventManager hooks have been initialized after entering world
        private bool _hooksInitialized = false;

        // Diagnostic logging to file (for debugging when running inside WoW.exe)
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        private static int _diagnosticLogCount = 0;

        // Named pipe logger provider — when connected, DiagLog becomes fallback-only
        private static NamedPipeLoggerProvider? _pipeLoggerProvider;

        static ForegroundBotWorker()
        {
            // Write diagnostic log to WoW client's WWoWLogs directory
            // Note: Process.GetCurrentProcess().MainModule can throw "Access denied" inside injected processes
            string wowDir;
            try
            {
                wowDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory;
            }
            catch
            {
                // Fall back to AppContext.BaseDirectory which works even without process access
                wowDir = AppContext.BaseDirectory;
            }

            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            DiagnosticLogPath = Path.Combine(logsDir, "foreground_bot_debug.log");

            // Clear previous log on startup
            try { File.WriteAllText(DiagnosticLogPath, $"=== ForegroundBotWorker Diagnostic Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\nLogPath: {DiagnosticLogPath}\n"); } catch { }
        }

        /// <summary>
        /// Writes a diagnostic message to the file log for debugging.
        /// Always writes to file — pipe logger may silently drop messages.
        /// </summary>
        private static void DiagLog(string message)
        {
            try
            {
                lock (DiagnosticLogLock)
                {
                    _diagnosticLogCount++;
                    var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{_diagnosticLogCount:D5}] {message}\n";
                    File.AppendAllText(DiagnosticLogPath, line);
                }
            }
            catch { /* Ignore logging errors */ }
        }

        public ForegroundBotWorker(IConfiguration configuration, ILoggerFactory loggerFactory, PathfindingClient? pathfindingClient = null)
        {
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _pathfindingClient = pathfindingClient;
            _logger = loggerFactory.CreateLogger<ForegroundBotWorker>();
            _automatedRecording = Environment.GetEnvironmentVariable("BLOOGBOT_AUTOMATED_RECORDING") == "1";

            // Try to register the named-pipe logger so ILogger output reaches StateManager
            var envAccount = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME");
            if (!string.IsNullOrEmpty(envAccount))
            {
                try
                {
                    _pipeLoggerProvider = new NamedPipeLoggerProvider(envAccount);
                    loggerFactory.AddProvider(_pipeLoggerProvider);
                    DiagLog($"NamedPipeLoggerProvider registered for account '{envAccount}', connected={_pipeLoggerProvider.IsConnected}");
                }
                catch (Exception ex)
                {
                    DiagLog($"NamedPipeLoggerProvider failed to register: {ex.Message}");
                }
            }

            DiagLog($"ForegroundBotWorker constructor called. DiagnosticLogPath={DiagnosticLogPath}, AutomatedRecording={_automatedRecording}");
            _logger.LogInformation("ForegroundBotWorker initialized (direct memory mode) - will request account from StateManager");
            if (_automatedRecording)
            {
                _logger.LogInformation("*** AUTOMATED RECORDING MODE - will run movement scenarios after entering world ***");
            }
        }

        /// <summary>
        /// Connects to StateManager and requests an account assignment.
        /// Sends AccountName="?" to get assigned to the first available character slot.
        /// </summary>
        private async Task<bool> RequestAccountAssignmentAsync(CancellationToken stoppingToken)
        {
            var stateManagerIp = _configuration["CharacterStateListener:IpAddress"] ?? "127.0.0.1";
            var stateManagerPort = int.Parse(_configuration["CharacterStateListener:Port"] ?? "5002");

            _logger.LogInformation($"Connecting to StateManager at {stateManagerIp}:{stateManagerPort} for account assignment...");

            try
            {
                _stateUpdateClient = new CharacterStateUpdateClient(
                    stateManagerIp,
                    stateManagerPort,
                    _loggerFactory.CreateLogger<CharacterStateUpdateClient>());

                // Request account assignment by sending "?" as account name
                // StateManager will assign us to the first unassigned character slot
                var request = new WoWActivitySnapshot
                {
                    AccountName = "?",
                    Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                _logger.LogInformation("Requesting account assignment from StateManager (AccountName='?')...");

                var response = _stateUpdateClient.SendMemberStateUpdate(request);

                if (response != null && !string.IsNullOrEmpty(response.AccountName) && response.AccountName != "?")
                {
                    _accountName = response.AccountName;
                    _logger.LogInformation($"Account assignment received: {_accountName}");
                    return true;
                }
                else
                {
                    _logger.LogWarning("StateManager did not assign an account - no available character slots?");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to StateManager");
                return false;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var processId = Process.GetCurrentProcess().Id;
                var processName = Process.GetCurrentProcess().ProcessName;

                DiagLog($"ExecuteAsync starting. Process={processName} PID={processId}");
                _logger.LogInformation("ForegroundBotWorker starting in injected process...");
                _logger.LogInformation($"Current Process: {processName} (ID: {processId})");

                // Connect to StateManager and request account assignment
                if (!await RequestAccountAssignmentAsync(stoppingToken))
                {
                    DiagLog("Account assignment FAILED - BotRunnerService will have no account name");
                    _logger.LogError("Failed to get account assignment from StateManager");
                }
                else
                {
                    DiagLog($"Account assignment SUCCESS: {_accountName}");
                }

                // Initialize the direct memory ObjectManager
                await InitializeObjectManager();
                DiagLog("ObjectManager initialized");

                // Initialize and start BotRunnerService (handles login, snapshots, and action dispatch)
                InitializeBotRunner();

                int loopCount = 0;
                // Main execution loop - anti-AFK, SignalEventManager hooks, and movement recording
                // All login, snapshot, and action dispatch is handled by BotRunnerService.
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        loopCount++;
                        // Log state every 10 iterations (every 5 seconds)
                        if (loopCount % 10 == 1)
                        {
                            var screenState = _objectManager?.GetCurrentScreenState() ?? WoWScreenState.Unknown;
                            var hasEnteredWorld = _objectManager?.HasEnteredWorld ?? false;
                            var playerName = _objectManager?.Player?.Name ?? "(null)";
                            DiagLog($"LOOP#{loopCount}: ScreenState={screenState}, HasEnteredWorld={hasEnteredWorld}, Player={playerName}");
                        }

                        // Anti-AFK ALWAYS - prevents disconnect during login/charselect too
                        _objectManager?.AntiAfk();

                        // Initialize SignalEventManager hooks once after entering world.
                        // These inject assembly into WoW's event system and must NOT run during
                        // the world server handshake (causes disconnect).
                        if (_objectManager?.HasEnteredWorld == true && !_hooksInitialized)
                        {
                            ObjectManager.PauseNativeCallsDuringWorldEntry = false;
                            DiagLog("RESUMED native calls - now InWorld");

                            // Suppress Lua error popups — they block subsequent Lua calls
                            try
                            {
                                Mem.Functions.LuaCall("seterrorhandler(function() end)");
                                DiagLog("Lua error handler suppressed");
                            }
                            catch (Exception ex)
                            {
                                DiagLog($"Failed to suppress Lua errors: {ex.Message}");
                            }

                            try
                            {
                                SignalEventManager.InitializeHooks();
                                DiagLog("SignalEventManager hooks initialized");
                            }
                            catch (Exception ex)
                            {
                                DiagLog($"SignalEventManager hook init error: {ex.Message}");
                            }
                            _hooksInitialized = true;
                        }

                        // Poll MovementRecorder and run automated scenarios when in world
                        if (_objectManager?.HasEnteredWorld == true
                            && _objectManager?.IsContinentTransition != true)
                        {
                            _movementRecorder?.Poll();

                            // Launch automated movement scenarios (once)
                            if (_automatedRecording && _scenarioRunnerTask == null && _objectManager != null && _movementRecorder != null)
                            {
                                DiagLog("Launching automated movement scenario runner...");
                                var runner = new MovementScenarioRunner(_objectManager, _movementRecorder, _loggerFactory);
                                _scenarioRunnerTask = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await runner.RunAllScenariosAsync(stoppingToken);
                                        DiagLog("SCENARIO_RUNNER_COMPLETE: All scenarios finished successfully");
                                    }
                                    catch (Exception ex)
                                    {
                                        DiagLog($"SCENARIO_RUNNER_ERROR: {ex.Message}\n{ex.StackTrace}");
                                    }
                                });
                            }
                        }

                        await Task.Delay(500, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DiagLog($"LOOP ERROR: {ex.Message}");
                        _logger.LogError(ex, "Error in main execution loop");
                        await Task.Delay(5000, stoppingToken);
                    }
                }

                DiagLog("ExecuteAsync shutting down");
                _logger.LogInformation("ForegroundBotWorker is shutting down...");
            }
            catch (Exception ex)
            {
                DiagLog($"FATAL ERROR: {ex.Message}");
                _logger.LogError(ex, "Fatal error in ForegroundBotWorker");
                throw;
            }
        }

        private Task InitializeObjectManager()
        {
            _logger.LogInformation("Initializing direct memory ObjectManager...");

            // Get the event handler singleton (hooks into WoW events via memory)
            var eventHandler = WoWEventHandler.Instance;

            // Subscribe to disconnect/logout for ObjectManager state resets
            SubscribeToStateResetEvents(eventHandler);

            // Create the direct memory ObjectManager
            _objectManager = new ObjectManager(eventHandler, new WoWActivitySnapshot());

            // Create MovementRecorder for physics engine testing
            // Use macro to toggle: /run REC=(REC or 0)+1
            _movementRecorder = new MovementRecorder(() => _objectManager, _loggerFactory);

            _logger.LogInformation("ObjectManager initialized - direct memory access enabled");
            _logger.LogInformation("MovementRecorder ready - say 'rec' in chat to toggle recording");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribe to WoWEventHandler events for state resets on disconnect/logout.
        /// BotRunnerService handles login flow via ILoginScreen/ICharacterSelectScreen.
        /// These handlers only reset ObjectManager-level state that FG needs.
        /// </summary>
        private void SubscribeToStateResetEvents(WoWEventHandler eventHandler)
        {
            eventHandler.OnDisconnect += (_, _) =>
            {
                DiagLog("EVENT: OnDisconnect - resetting state");
                _logger.LogWarning("Disconnected from server");
                ObjectManager.PauseNativeCallsDuringWorldEntry = false;
                _hooksInitialized = false;
                if (_objectManager != null)
                {
                    _objectManager.HasEnteredWorld = false;
                    _objectManager.Player = null;
                }
            };

            eventHandler.OnLogout += (_, _) =>
            {
                DiagLog("EVENT: OnLogout - resetting world state");
                _logger.LogInformation("Player logout detected");
                _hooksInitialized = false;
                if (_objectManager != null)
                {
                    _objectManager.HasEnteredWorld = false;
                    _objectManager.Player = null;
                }
            };

            // Subscribe to chat messages for diagnostic logging
            eventHandler.OnChatMessage += (_, args) =>
            {
                DiagLog($"CHAT [{args.MsgType}] {args.SenderName}: {args.Text}");
            };

            eventHandler.OnSystemMessage += (_, args) =>
            {
                DiagLog($"<<< SYSTEM_MSG: {args.Message}");
            };

            eventHandler.OnErrorMessage += (_, args) =>
            {
                DiagLog($"<<< UI_ERROR: {args.Message}");
            };

            eventHandler.OnUiMessage += (_, args) =>
            {
                DiagLog($"<<< UI_INFO: {args.Message}");
            };

            eventHandler.OnSkillMessage += (_, args) =>
            {
                DiagLog($"<<< SKILL_MSG: {args.Message}");
            };
        }

        /// <summary>
        /// Creates and starts BotRunnerService, which handles login, snapshots, and action dispatch.
        /// Must be called after ObjectManager and StateManager client are initialized.
        /// </summary>
        private void InitializeBotRunner()
        {
            if (_stateUpdateClient == null || _objectManager == null)
            {
                DiagLog("InitializeBotRunner: SKIPPED - stateUpdateClient or objectManager is null");
                _logger.LogWarning("Cannot initialize BotRunnerService - missing dependencies");
                return;
            }

            // Create PathfindingClient from configuration if not injected.
            // Never pass a null client into ClassContainer: corpse retrieval must remain pathfinding-driven.
            if (_pathfindingClient == null)
            {
                try
                {
                    var pfIp = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
                    var pfPort = int.Parse(_configuration["PathfindingService:Port"] ?? "5001");
                    _pathfindingClient = new PathfindingClient(pfIp, pfPort, _loggerFactory.CreateLogger<PathfindingClient>());
                    DiagLog($"Created PathfindingClient: {pfIp}:{pfPort}");
                }
                catch (Exception ex)
                {
                    DiagLog($"Failed to create PathfindingClient: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to create PathfindingClient from config; using fallback client instance.");
                    _pathfindingClient = new PathfindingClient();
                    DiagLog("Created fallback PathfindingClient() after config failure");
                }
            }

            var pathfindingClient = _pathfindingClient ?? new PathfindingClient();
            if (_pathfindingClient == null)
            {
                DiagLog("PathfindingClient was null at container creation; using fallback PathfindingClient()");
                _pathfindingClient = pathfindingClient;
            }

            var container = CreateClassContainer(_accountName, pathfindingClient);

            _botRunner = new BotRunnerService(
                _objectManager,
                _stateUpdateClient,
                container,
                agentFactoryAccessor: null, // FG has no network agents
                accountName: _accountName,
                behaviorConfig: LoadBehaviorConfig(_configuration));

            _botRunner.Start();
            DiagLog("BotRunnerService started");
            _logger.LogInformation("BotRunnerService started - handling login, snapshots, and actions");
        }

        private static BotBehaviorConfig LoadBehaviorConfig(IConfiguration configuration)
        {
            var config = new BotBehaviorConfig();
            var section = configuration.GetSection("BotBehavior");
            if (!section.Exists()) return config;

            if (float.TryParse(section["MaxPullRange"], out var v1)) config.MaxPullRange = v1;
            if (int.TryParse(section["TargetLevelRangeBelow"], out var v2)) config.TargetLevelRangeBelow = v2;
            if (int.TryParse(section["TargetLevelRangeAbove"], out var v3)) config.TargetLevelRangeAbove = v3;
            if (int.TryParse(section["RestHpThresholdPct"], out var v4)) config.RestHpThresholdPct = v4;
            if (int.TryParse(section["RestManaThresholdPct"], out var v5)) config.RestManaThresholdPct = v5;
            if (int.TryParse(section["BagFullThreshold"], out var v6)) config.BagFullThreshold = v6;
            if (float.TryParse(section["GatherDetectRange"], out var v7)) config.GatherDetectRange = v7;
            if (float.TryParse(section["FishingPoolDetectRange"], out var v8)) config.FishingPoolDetectRange = v8;
            if (int.TryParse(section["StatsLogIntervalMs"], out var v9)) config.StatsLogIntervalMs = v9;
            return config;
        }

        private static IDependencyContainer CreateClassContainer(string? accountName, PathfindingClient pathfindingClient)
        {
            BotProfiles.Common.BotBase botProfile;

            if (!string.IsNullOrEmpty(accountName) && accountName.Length >= 4)
            {
                var classCode = accountName.Substring(2, 2);
                var @class = WoWNameGenerator.ParseClassCode(classCode);

                botProfile = @class switch
                {
                    Class.Warrior => new WarriorArms.WarriorArms(),
                    Class.Paladin => new PaladinRetribution.PaladinRetribution(),
                    Class.Rogue => new RogueCombat.RogueCombat(),
                    Class.Hunter => new HunterBeastMastery.HunterBeastMastery(),
                    Class.Priest => new PriestDiscipline.PriestDiscipline(),
                    Class.Shaman => new ShamanEnhancement.ShamanEnhancement(),
                    Class.Mage => new MageArcane.MageArcane(),
                    Class.Warlock => new WarlockAffliction.WarlockAffliction(),
                    Class.Druid => new DruidRestoration.DruidRestoration(),
                    _ => new WarriorArms.WarriorArms()
                };
            }
            else
            {
                botProfile = new WarriorArms.WarriorArms();
            }

            return new ClassContainer(
                botProfile.Name,
                botProfile.CreateRestTask,
                botProfile.CreateBuffTask,
                botProfile.CreateMoveToTargetTask,
                botProfile.CreatePvERotationTask,
                botProfile.CreatePvPRotationTask,
                pathfindingClient);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ForegroundBotWorker stop requested...");

            _botRunner?.Stop();

            // Stop any ongoing movement recording
            if (_movementRecorder?.IsRecording == true)
            {
                _logger.LogInformation("Stopping active movement recording...");
                _movementRecorder.StopRecording();
            }
            _movementRecorder?.Dispose();

            // Dispose the named-pipe logger provider
            _pipeLoggerProvider?.Dispose();

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("ForegroundBotWorker stopped.");
        }
    }
}
#endif
