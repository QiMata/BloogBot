using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Clients;
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
    /// Uses the direct memory ObjectManager implementation (not WoWSharpClient network mode).
    /// </summary>
    public class ForegroundBotWorker : BackgroundService
    {
        private readonly ILogger<ForegroundBotWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;

        private ObjectManager? _objectManager;
        private readonly IWoWActivitySnapshot _activitySnapshot;
        private CharacterStateUpdateClient? _stateUpdateClient;
        private MovementRecorder? _movementRecorder;
        private GrindBot? _grindBot;
        private PathfindingClient? _pathfindingClient;

        // Automated recording: runs controlled movement scenarios instead of GrindBot
        private readonly bool _automatedRecording;
        private Task? _scenarioRunnerTask;

        // Login credentials received from StateManager via IPC
        private string _accountName = string.Empty;
        private readonly string _accountPassword = "PASSWORD"; // Always "PASSWORD" per MangosSOAPClient

        // Login state tracking
        private bool _loginAttempted = false;
        private bool _enterWorldAttempted = false;
        private bool _isLoadingWorld = false;  // Set when EnterWorld clicked, cleared when in world or explicit event
        private bool _characterListReady = false;  // Set when UPDATE_SELECTED_CHARACTER fires, cleared on logout
        private DateTime _lastLoginAttemptTime = DateTime.MinValue;
        private DateTime _lastEnterWorldAttemptTime = DateTime.MinValue;
        private DateTime _loadingStartedTime = DateTime.MinValue;  // Track when we started loading to prevent premature timeout
        private DateTime _loginScreenFirstSeenTime = DateTime.MinValue; // When LoginScreen state was first detected (Lua init grace period)
        private DateTime _characterSelectFirstSeenTime = DateTime.MinValue; // When CharacterSelect state was first detected
        private static readonly TimeSpan LoginRetryInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan WorldLoadingTimeout = TimeSpan.FromSeconds(60);  // Max time to wait for world to load
        private static readonly TimeSpan LuaInitGracePeriod = TimeSpan.FromSeconds(3);   // Wait for WoW Lua engine to initialize after login screen appears
        private static readonly TimeSpan CharSelectGracePeriod = TimeSpan.FromSeconds(3); // Wait for character list to populate in memory

        // Diagnostic logging to file (for debugging when running inside WoW.exe)
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        private static int _diagnosticLogCount = 0;

        // 63d: Named pipe logger provider — when connected, DiagLog becomes fallback-only
        private static NamedPipeLoggerProvider? _pipeLoggerProvider;

        // Pending action from StateManager response (set by background send thread, read by main loop)
        private Communication.ActionMessage? _pendingAction;

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
            _activitySnapshot = new WoWActivitySnapshot();
            _automatedRecording = Environment.GetEnvironmentVariable("BLOOGBOT_AUTOMATED_RECORDING") == "1";

            // 63d: Try to register the named-pipe logger so ILogger output reaches StateManager
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
                    DiagLog("Account assignment FAILED - login automation disabled");
                    _logger.LogError("Failed to get account assignment from StateManager - login automation disabled");
                }
                else
                {
                    DiagLog($"Account assignment SUCCESS: {_accountName}");
                }

                // Initialize the direct memory ObjectManager
                await InitializeObjectManager();
                DiagLog("ObjectManager initialized");

                int loopCount = 0;
                // Main execution loop - handles login automation and bot logic
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
                            var continentId = _objectManager?.ContinentId ?? 0;
                            DiagLog($"LOOP#{loopCount}: ScreenState={screenState}, ContinentId=0x{continentId:X}, HasEnteredWorld={hasEnteredWorld}, Player={playerName}, _isLoadingWorld={_isLoadingWorld}");
                        }

                        // Anti-AFK ALWAYS - prevents disconnect during login/charselect too
                        // Must be called every loop iteration, not just when in world
                        _objectManager?.AntiAfk();

                        // Handle login state machine
                        await ProcessLoginStateMachineAsync();

                        // Get current screen state
                        var currentScreenState = _objectManager?.GetCurrentScreenState() ?? WoWScreenState.Unknown;

                        // Send activity snapshot to StateManager in ALL states
                        // This gives StateManager visibility into login, charselect, loading, etc.
                        SendActivitySnapshot(currentScreenState);

                        // Process any pending actions from StateManager response (e.g., group invite)
                        if (currentScreenState == WoWScreenState.InWorld
                            && _objectManager?.HasEnteredWorld == true)
                        {
                            ProcessPendingAction();
                        }

                        // If in world, run bot logic and poll recorder.
                        // Skip during continent transitions — object pointers are invalid.
                        if (currentScreenState == WoWScreenState.InWorld
                            && _objectManager?.HasEnteredWorld == true
                            && _objectManager?.IsContinentTransition != true)
                        {
                            // Poll MovementRecorder - checks REC Lua variable to toggle recording
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

                            if (!_automatedRecording && _movementRecorder?.IsRecording != true)
                            {
                                // Initialize GrindBot on first world entry (skip entirely when recorder is present)
                                if (_grindBot == null && _objectManager != null)
                                {
                                    var questRepo = Repository.SqliteQuestRepository.TryCreate();
                                    var groupMgr = new Grouping.GroupManager(_objectManager);
                                    _grindBot = new GrindBot(_objectManager, _pathfindingClient, questRepo, groupMgr, DiagLog);
                                    DiagLog($"GrindBot initialized (pathfinding={_pathfindingClient != null})");
                                    _logger.LogInformation("GrindBot initialized (pathfinding={HasPathfinding})", _pathfindingClient != null);
                                }

                                // Run bot state machine
                                _grindBot?.Update();

                                // Diagnostic: log GrindBot phase periodically
                                if (loopCount % 10 == 0 && _grindBot != null)
                                {
                                    var p = _objectManager?.Player;
                                    DiagLog($"GRINDBOT: phase={_grindBot.CurrentPhase}, pos=({p?.Position?.X:F0},{p?.Position?.Y:F0},{p?.Position?.Z:F0}), hp={p?.HealthPercent ?? 0}%");
                                }
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

            // Subscribe to login-related events
            SubscribeToLoginEvents(eventHandler);

            // Create the direct memory ObjectManager
            _objectManager = new ObjectManager(eventHandler, _activitySnapshot);

            // Subscribe to OnEnteredWorld event to send snapshot immediately when entering world
            // This avoids race conditions between the enumeration thread and main loop
            _objectManager.OnEnteredWorld += OnPlayerEnteredWorld;

            // Create MovementRecorder for physics engine testing
            // Use macro to toggle: /run REC=(REC or 0)+1
            _movementRecorder = new MovementRecorder(() => _objectManager, _loggerFactory);

            _logger.LogInformation("ObjectManager initialized - direct memory access enabled");
            _logger.LogInformation("MovementRecorder ready - say 'rec' in chat to toggle recording");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the player first enters the world. Sends the snapshot immediately
        /// to avoid race conditions where the player disconnects before the main loop runs.
        /// </summary>
        private void OnPlayerEnteredWorld(object? sender, EventArgs e)
        {
            var playerName = _objectManager?.Player?.Name ?? "(null)";
            DiagLog($"OnPlayerEnteredWorld EVENT FIRED! Player={playerName}");
            _logger.LogInformation($"Player entered world: {playerName} - sending snapshot immediately");
            // Send snapshot immediately when entering world
            SendActivitySnapshot(WoWScreenState.InWorld);
        }

        /// <summary>
        /// Subscribe to WoWEventHandler events for login flow monitoring.
        /// </summary>
        private void SubscribeToLoginEvents(WoWEventHandler eventHandler)
        {
            eventHandler.OnHandshakeBegin += (_, _) =>
            {
                DiagLog("EVENT: OnHandshakeBegin");
                _logger.LogInformation("Login handshake started...");
            };

            eventHandler.OnWrongLogin += (_, _) =>
            {
                DiagLog($"EVENT: OnWrongLogin for account={_accountName}");
                _logger.LogError($"Login failed - invalid credentials for account: {_accountName}");
                _loginAttempted = false; // Allow retry with potentially different credentials
            };

            eventHandler.OnCharacterListLoaded += (_, _) =>
            {
                DiagLog("EVENT: OnCharacterListLoaded - setting _characterListReady=true, resetting loading state");
                _logger.LogInformation("Character list loaded - ready to select character");
                _enterWorldAttempted = false; // Reset to allow enter world attempt
                _isLoadingWorld = false;  // Reset loading state - we're back at character select
                _loadingStartedTime = DateTime.MinValue;  // Reset loading timer
                _characterListReady = true;  // Character list is now ready
            };

            eventHandler.OnChooseRealm += (_, _) =>
            {
                DiagLog("EVENT: OnChooseRealm - resetting loading state");
                _logger.LogInformation("Realm selection screen detected");
                _isLoadingWorld = false;  // Reset loading state
                _loadingStartedTime = DateTime.MinValue;  // Reset loading timer
            };

            eventHandler.OnDisconnect += (_, _) =>
            {
                DiagLog("EVENT: OnDisconnect - resetting state (keeping login cooldown)");
                _logger.LogWarning("Disconnected from server - will attempt to reconnect after cooldown");
                // NOTE: Do NOT reset _loginAttempted here - the _lastLoginAttemptTime cooldown
                // prevents rapid reconnection that triggers CMD_REALM_LIST rate limiting
                _enterWorldAttempted = false;
                _isLoadingWorld = false;  // Reset loading state on disconnect
                _loadingStartedTime = DateTime.MinValue;  // Reset loading timer on disconnect
                _characterListReady = false;  // Reset character list ready on disconnect
                _loginScreenFirstSeenTime = DateTime.MinValue;  // Reset Lua init grace period on disconnect
                _characterSelectFirstSeenTime = DateTime.MinValue;  // Reset char select grace period on disconnect

                ObjectManager.PauseNativeCallsDuringWorldEntry = false;  // Re-enable native calls on disconnect
                if (_objectManager != null)
                {
                    _objectManager.HasEnteredWorld = false;  // Reset world state on disconnect
                    _objectManager.Player = null;  // Clear player reference on disconnect
                }
            };

            eventHandler.OnLogout += (_, _) =>
            {
                DiagLog("EVENT: OnLogout - resetting world state");
                _logger.LogInformation("Player logout detected - resetting world state");
                _enterWorldAttempted = false;
                _isLoadingWorld = false;
                _loadingStartedTime = DateTime.MinValue;  // Reset loading timer on logout
                _characterListReady = false;  // Reset character list ready on logout
                _loginScreenFirstSeenTime = DateTime.MinValue;  // Reset Lua init grace period on logout
                _characterSelectFirstSeenTime = DateTime.MinValue;  // Reset char select grace period on logout

                if (_objectManager != null)
                {
                    _objectManager.HasEnteredWorld = false;
                    _objectManager.Player = null;
                }
            };

            eventHandler.InServerQueue += (_, _) =>
            {
                DiagLog("EVENT: InServerQueue");
                _logger.LogDebug("Waiting in server queue...");
            };

            // Subscribe to ALL chat messages - log them for debugging
            eventHandler.OnChatMessage += (_, args) =>
            {
                DiagLog($"CHAT [{args.MsgType}] {args.SenderName}: {args.Text}");
                _logger.LogDebug($"Chat message [{args.MsgType}] from {args.SenderName}: {args.Text}");
            };

            // Subscribe to OnSystemMessage for server welcome/system messages (CHAT_MSG_SYSTEM fires this, NOT OnChatMessage)
            eventHandler.OnSystemMessage += (_, args) =>
            {
                DiagLog($"<<< SYSTEM_MSG: {args.Message}");
                _logger.LogInformation($"System message: {args.Message}");
            };

            // Subscribe to raw events for additional visibility
            eventHandler.OnEvent += (_, args) =>
            {
                // Log system-related events that might indicate server messages
                if (args.EventName.StartsWith("CHAT_MSG_") ||
                    args.EventName.Contains("SYSTEM") ||
                    args.EventName.Contains("PLAYER_ENTERING"))
                {
                    var argsStr = args.Parameters != null && args.Parameters.Length > 0
                        ? string.Join(", ", args.Parameters.Select(a => a?.ToString() ?? "null"))
                        : "(no args)";
                    DiagLog($"RAW_EVENT: {args.EventName} args=[{argsStr}]");
                }
            };
        }

        /// <summary>
        /// Handles the login state machine using ContinentId-based screen detection.
        /// State transitions: Login Screen → Character Select → Loading World → In World
        ///
        /// Detection Logic (ContinentId is the PRIMARY discriminator):
        /// - LoginState == "login" → LoginScreen
        /// - LoginState == "connecting" → Connecting
        /// - LoginState == "charselect" + ContinentId == 0xFFFFFFFF → CharacterSelect
        /// - LoginState == "charselect" + ContinentId == 0xFF → LoadingWorld
        /// - LoginState == "charselect" + ContinentId < 0xFF → InWorld
        /// </summary>
        private Task ProcessLoginStateMachineAsync()
        {
            if (_objectManager == null || string.IsNullOrEmpty(_accountName))
            {
                return Task.CompletedTask;
            }

            // Use the new ContinentId-based screen state detection
            var screenState = _objectManager.GetCurrentScreenState();

            // Skip login state machine if we're in world
            if (screenState == WoWScreenState.InWorld)
            {
                _isLoadingWorld = false;
                _loadingStartedTime = DateTime.MinValue;
                // Re-enable native calls now that we're safely in world
                if (ObjectManager.PauseNativeCallsDuringWorldEntry)
                {
                    ObjectManager.PauseNativeCallsDuringWorldEntry = false;
                    DiagLog("ProcessLoginStateMachine: RESUMED native calls - now InWorld");

                    // Now that we're safely in world, initialize SignalEventManager hooks.
                    // These inject assembly into WoW's event system and must NOT run during
                    // the world server handshake (causes disconnect).
                    try
                    {
                        SignalEventManager.InitializeHooks();
                        DiagLog("ProcessLoginStateMachine: SignalEventManager hooks initialized");
                    }
                    catch (Exception ex)
                    {
                        DiagLog($"ProcessLoginStateMachine: SignalEventManager hook init error: {ex.Message}");
                    }
                }
                return Task.CompletedTask;
            }

            // If loading, check for timeout before allowing retry
            if (screenState == WoWScreenState.LoadingWorld || _isLoadingWorld)
            {
                // Track when loading started
                if (_loadingStartedTime == DateTime.MinValue)
                {
                    _loadingStartedTime = DateTime.UtcNow;
                    _isLoadingWorld = true;
                    DiagLog("ProcessLoginStateMachine: LoadingWorld state detected, starting timeout timer");
                }

                // Check if we've timed out waiting for world to load
                var loadingDuration = DateTime.UtcNow - _loadingStartedTime;
                if (loadingDuration < WorldLoadingTimeout)
                {
                    // Still within loading timeout window - wait
                    return Task.CompletedTask;
                }
                else
                {
                    // Loading timed out - allow retry
                    DiagLog($"ProcessLoginStateMachine: World loading TIMEOUT after {loadingDuration.TotalSeconds:F1}s - allowing retry");
                    _isLoadingWorld = false;
                    _loadingStartedTime = DateTime.MinValue;
                    ObjectManager.PauseNativeCallsDuringWorldEntry = false;  // Re-enable native calls on timeout
                }
            }

            try
            {
                switch (screenState)
                {
                    case WoWScreenState.LoginScreen:
                        // At login screen - attempt to log in
                        _isLoadingWorld = false;
                        _loadingStartedTime = DateTime.MinValue;
                        HandleLoginScreenState();
                        break;

                    case WoWScreenState.Connecting:
                        // Connecting to server - wait for completion
                        DiagLog("ProcessLoginStateMachine: Connecting to server...");
                        break;

                    case WoWScreenState.CharacterSelect:
                        // At character select - attempt to enter world
                        HandleCharacterSelectState();
                        break;

                    case WoWScreenState.CharacterCreate:
                        // At character create screen - unexpected, log it
                        DiagLog("ProcessLoginStateMachine: At CharacterCreate screen - unexpected state");
                        break;

                    case WoWScreenState.Disconnected:
                        // Disconnected - reset state but keep login cooldown to prevent realm list spam
                        DiagLog("ProcessLoginStateMachine: Disconnected - resetting state (keeping login cooldown)");
                        // NOTE: Do NOT reset _loginAttempted - _lastLoginAttemptTime cooldown prevents spam
                        _enterWorldAttempted = false;
                        _isLoadingWorld = false;
                        _loadingStartedTime = DateTime.MinValue;
                        _characterListReady = false;

                        ObjectManager.PauseNativeCallsDuringWorldEntry = false;  // Re-enable native calls
                        break;

                    default:
                        DiagLog($"ProcessLoginStateMachine: Unhandled screenState={screenState}");
                        break;
                }
            }
            catch (Exception ex)
            {
                DiagLog($"ProcessLoginStateMachine ERROR: {ex.Message}");
                _logger.LogDebug($"Error in login state machine: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the login screen state - attempt to log in with credentials.
        /// </summary>
        private void HandleLoginScreenState()
        {
            // Verify we're actually at login screen using screen state
            var screenState = _objectManager?.GetCurrentScreenState() ?? WoWScreenState.Unknown;
            if (screenState != WoWScreenState.LoginScreen)
            {
                DiagLog($"HandleLoginScreenState: Not at login screen (screenState={screenState})");
                _loginScreenFirstSeenTime = DateTime.MinValue; // Reset grace period when leaving login screen
                return;
            }

            // Grace period: WoW's Lua engine needs time to initialize after the login screen appears.
            // Calling LuaCall too early causes the delegate to crash on the main thread, and
            // ThreadSynchronizer returns null which cascades into NullReferenceException.
            if (_loginScreenFirstSeenTime == DateTime.MinValue)
            {
                _loginScreenFirstSeenTime = DateTime.UtcNow;
                DiagLog($"HandleLoginScreenState: Login screen first detected, waiting {LuaInitGracePeriod.TotalSeconds}s for Lua engine init");
                return;
            }
            if (DateTime.UtcNow - _loginScreenFirstSeenTime < LuaInitGracePeriod)
            {
                return; // Still within Lua init grace period
            }

            // Throttle login attempts - always respect cooldown regardless of disconnect resets
            if (_lastLoginAttemptTime != DateTime.MinValue && DateTime.UtcNow - _lastLoginAttemptTime < LoginRetryInterval)
            {
                return; // Wait for retry interval
            }

            DiagLog($"HandleLoginScreenState: Attempting login for account={_accountName}");
            _logger.LogInformation($"At login screen - attempting login for account: {_accountName}");

            // Capture state before login attempt
            LoginStateMonitor.CaptureSnapshot("Login_PRE_SUBMIT");

            try
            {
                _objectManager?.DefaultServerLogin(_accountName, _accountPassword);
                _loginAttempted = true;
                _lastLoginAttemptTime = DateTime.UtcNow;
                DiagLog("HandleLoginScreenState: Login credentials submitted");
                _logger.LogInformation("Login credentials submitted, waiting for authentication...");

                // Capture state after login attempt
                LoginStateMonitor.CaptureSnapshot("Login_POST_SUBMIT");
            }
            catch (Exception ex)
            {
                DiagLog($"HandleLoginScreenState ERROR: {ex.Message}");
                LoginStateMonitor.CaptureSnapshot($"Login_ERROR_{ex.GetType().Name}");
                _logger.LogError(ex, "Failed to submit login credentials");
            }
        }

        /// <summary>
        /// Handle the character select state - select a character and enter world.
        /// </summary>
        private void HandleCharacterSelectState()
        {
            // Verify we're actually at character select using screen state (ContinentId == 0xFFFFFFFF)
            var screenState = _objectManager?.GetCurrentScreenState() ?? WoWScreenState.Unknown;
            if (screenState != WoWScreenState.CharacterSelect)
            {
                // If we're in world now, reset the loading flags
                if (screenState == WoWScreenState.InWorld)
                {
                    DiagLog($"HandleCharacterSelectState: Now InWorld (screenState={screenState})");
                    _isLoadingWorld = false;
                    _loadingStartedTime = DateTime.MinValue;
                }
                else
                {
                    DiagLog($"HandleCharacterSelectState: Not at charselect (screenState={screenState})");
                }
                return;
            }

            // If HasEnteredWorld is true but we're back at CharacterSelect, we logged out or disconnected
            // Reset state to allow re-entry
            if (_objectManager?.HasEnteredWorld == true)
            {
                DiagLog("HandleCharacterSelectState: RESETTING STATE - HasEnteredWorld=true but back at CharacterSelect!");
                _logger.LogInformation("Back at character select after being in world - resetting state");
                _objectManager.HasEnteredWorld = false;
                _objectManager.Player = null;
                ObjectManager.ClearCachedGuid(); // Clear cached GUID on logout
                _enterWorldAttempted = false;
                _characterListReady = false;
                _characterSelectFirstSeenTime = DateTime.MinValue; // Reset grace period
                return; // Don't try to enter world this tick, let the state settle
            }

            // Wait for character list to be loaded before attempting to enter world.
            // The UPDATE_SELECTED_CHARACTER event sets _characterListReady = true, but
            // SignalEventManager hooks are deferred until after world entry (to avoid
            // disconnect during handshake). Fall back to memory polling when hooks aren't active.
            if (!_characterListReady)
            {
                // Grace period: let the character list data settle in memory
                if (_characterSelectFirstSeenTime == DateTime.MinValue)
                {
                    _characterSelectFirstSeenTime = DateTime.UtcNow;
                    DiagLog($"HandleCharacterSelectState: Character select first detected, waiting {CharSelectGracePeriod.TotalSeconds}s for character list");
                    return;
                }
                if (DateTime.UtcNow - _characterSelectFirstSeenTime < CharSelectGracePeriod)
                {
                    return; // Still within grace period
                }

                // After grace period, check character count from memory directly
                int memCharCount = ObjectManager.MaxCharacterCount;
                if (memCharCount > 0)
                {
                    DiagLog($"HandleCharacterSelectState: Hooks not active, but MaxCharacterCount={memCharCount} from memory — proceeding");
                    _characterListReady = true;
                }
                else
                {
                    return; // Character list not yet loaded
                }
            }

            // Check if there are characters on the account
            int charCount = ObjectManager.MaxCharacterCount;
            if (charCount <= 0)
            {
                DiagLog($"HandleCharacterSelectState: No characters (count={charCount})");
                _logger.LogWarning($"No characters on account (MaxCharacterCount={charCount}) - cannot enter world");
                return;
            }

            // Minimum delay between Enter World attempts to prevent spam clicking
            var minDelay = TimeSpan.FromSeconds(3);
            if (_enterWorldAttempted && DateTime.UtcNow - _lastEnterWorldAttemptTime < minDelay)
            {
                return; // Wait for minimum delay between attempts
            }

            DiagLog($"HandleCharacterSelectState: Attempting EnterWorld with {charCount} character(s)");
            _logger.LogInformation($"At character select screen with {charCount} character(s) - attempting to enter world...");

            try
            {
                // CRITICAL: Pause native function calls during world entry to avoid ThreadSynchronizer interference
                // The WM_USER messages from ThreadSynchronizer can disrupt the world server handshake
                ObjectManager.PauseNativeCallsDuringWorldEntry = true;
                DiagLog("HandleCharacterSelectState: PAUSED native calls during world entry");

                // For now, just click the Enter World button (selects first/default character)
                // TODO: In future, could parse character list and select specific character by name/GUID
                _objectManager?.EnterWorld(0);
                _enterWorldAttempted = true;
                _isLoadingWorld = true;
                _loadingStartedTime = DateTime.UtcNow;  // Track when we started loading
                _lastEnterWorldAttemptTime = DateTime.UtcNow;
                DiagLog($"HandleCharacterSelectState: EnterWorld command sent, _isLoadingWorld=true, timeout={WorldLoadingTimeout.TotalSeconds}s");
                _logger.LogInformation("Enter world command sent, waiting for world load...");
            }
            catch (Exception ex)
            {
                DiagLog($"HandleCharacterSelectState ERROR: {ex.Message}");
                _logger.LogError(ex, "Failed to enter world");
                _isLoadingWorld = false;
                _loadingStartedTime = DateTime.MinValue;
                ObjectManager.PauseNativeCallsDuringWorldEntry = false;  // Re-enable on error
            }
        }

        /// <summary>
        /// Sends the current activity snapshot to StateManager.
        /// The snapshot is populated by ObjectManager.UpdateProbe() with character data.
        /// Fire-and-forget: sends asynchronously to avoid blocking the main thread on socket I/O.
        /// </summary>
        private volatile bool _snapshotSendInProgress = false;
        private int _snapshotSentCount = 0;
        private WoWScreenState _lastReportedScreenState = WoWScreenState.Unknown;
        private void SendActivitySnapshot(WoWScreenState screenState)
        {
            if (_stateUpdateClient == null || string.IsNullOrEmpty(_accountName))
            {
                return;
            }

            // Skip if a send is already in progress (prevents queue buildup)
            if (_snapshotSendInProgress)
            {
                return;
            }

            // Cast to concrete type for protobuf serialization
            if (_activitySnapshot is not WoWActivitySnapshot snapshot)
            {
                return;
            }

            // Ensure account name is set
            if (string.IsNullOrEmpty(snapshot.AccountName) || snapshot.AccountName == "?")
            {
                snapshot.AccountName = _accountName;
            }

            // Set screen state on the snapshot
            snapshot.ScreenState = screenState.ToString();
            snapshot.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Log state transitions
            if (screenState != _lastReportedScreenState)
            {
                DiagLog($"SCREEN_STATE_CHANGED: {_lastReportedScreenState} -> {screenState}");
                _logger.LogInformation($"Screen state changed: {_lastReportedScreenState} -> {screenState}");
                _lastReportedScreenState = screenState;
            }

            // Populate snapshot fields when player is in world.
            // Skip during continent transitions — object pointers are invalid and would crash.
            if (screenState == WoWScreenState.InWorld
                && _objectManager?.IsContinentTransition != true
                && _objectManager?.Player is ForegroundBotRunner.Objects.WoWUnit unit)
            {
                try
                {
                    var moveFlags = (uint)unit.MovementFlags;
                    snapshot.MovementData = new Game.MovementData
                    {
                        MovementFlags = moveFlags,
                        FallTime = unit.FallTime,
                        JumpVerticalSpeed = unit.JumpVerticalSpeed,
                        JumpSinAngle = unit.JumpSinAngle,
                        JumpCosAngle = unit.JumpCosAngle,
                        JumpHorizontalSpeed = unit.JumpHorizontalSpeed,
                        SwimPitch = unit.SwimPitch,
                        WalkSpeed = unit.WalkSpeed,
                        RunSpeed = unit.RunSpeed,
                        RunBackSpeed = unit.RunBackSpeed,
                        SwimSpeed = unit.SwimSpeed,
                        SwimBackSpeed = unit.SwimBackSpeed,
                        TurnRate = unit.TurnRate,
                        Position = new Game.Position
                        {
                            X = unit.Position.X,
                            Y = unit.Position.Y,
                            Z = unit.Position.Z
                        },
                        Facing = unit.Facing,
                        FallStartHeight = unit.FallStartHeight,
                    };

                    // Transport data - only valid when MOVEFLAG_ONTRANSPORT is set
                    const uint MOVEFLAG_ONTRANSPORT = 0x200;
                    if ((moveFlags & MOVEFLAG_ONTRANSPORT) != 0)
                    {
                        snapshot.MovementData.TransportGuid = unit.TransportGuid;
                        var tOffset = unit.TransportOffset;
                        snapshot.MovementData.TransportOffsetX = tOffset.X;
                        snapshot.MovementData.TransportOffsetY = tOffset.Y;
                        snapshot.MovementData.TransportOffsetZ = tOffset.Z;
                        snapshot.MovementData.TransportOrientation = unit.TransportOrientation;
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot: Error populating movement data: {ex.Message}");
                }

                // Party leader GUID (0 if not in a group)
                try
                {
                    snapshot.PartyLeaderGuid = _objectManager.PartyLeaderGuid;
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot: Error populating party leader GUID: {ex.Message}");
                }

                // Populate Player protobuf
                try
                {
                    snapshot.Player = BuildPlayerProtobuf(unit);
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot: Error populating player: {ex.Message}");
                }

                // Populate NearbyUnits (hostile/neutral units within 40y)
                try
                {
                    snapshot.NearbyUnits.Clear();
                    var playerPos = unit.Position;
                    foreach (var nearbyUnit in _objectManager.Units
                        .OfType<ForegroundBotRunner.Objects.WoWUnit>()
                        .Where(u => u.Guid != unit.Guid && u.Position.DistanceTo(playerPos) < 40f))
                    {
                        snapshot.NearbyUnits.Add(BuildUnitProtobuf(nearbyUnit));
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot: Error populating nearby units: {ex.Message}");
                }

                // Populate NearbyObjects (game objects within 40y)
                try
                {
                    snapshot.NearbyObjects.Clear();
                    var playerPos = unit.Position;
                    foreach (var go in _objectManager.GameObjects
                        .OfType<ForegroundBotRunner.Objects.WoWGameObject>()
                        .Where(g => g.Position.DistanceTo(playerPos) < 40f))
                    {
                        snapshot.NearbyObjects.Add(BuildGameObjectProtobuf(go));
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot: Error populating nearby objects: {ex.Message}");
                }

                // Populate CurrentAction from GrindBot phase
                try
                {
                    if (_grindBot != null)
                    {
                        snapshot.CurrentAction = new ActionMessage
                        {
                            ActionType = GrindPhaseToActionType(_grindBot.CurrentPhase),
                            ActionResult = ResponseResult.InProgress
                        };
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot: Error populating action: {ex.Message}");
                }
            }

            // Log first snapshot send
            _snapshotSentCount++;
            if (_snapshotSentCount == 1)
            {
                DiagLog($"SendActivitySnapshot: FIRST snapshot - Account={snapshot.AccountName}, ScreenState={snapshot.ScreenState}, Character={snapshot.CharacterName}");
            }

            // Send on thread pool to avoid blocking main thread; store response for action processing
            _snapshotSendInProgress = true;
            Task.Run(() =>
            {
                try
                {
                    var response = _stateUpdateClient.SendMemberStateUpdate(snapshot);
                    if (response?.CurrentAction != null
                        && response.CurrentAction.ActionType != Communication.ActionType.Wait)
                    {
                        _pendingAction = response.CurrentAction;
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"SendActivitySnapshot ERROR: {ex.Message}");
                    _logger.LogWarning($"Failed to send snapshot: {ex.Message}");
                }
                finally
                {
                    _snapshotSendInProgress = false;
                }
            });
        }

        /// <summary>
        /// Processes pending actions received from StateManager (e.g., group invite/accept/leave).
        /// Called from the main loop thread so ObjectManager Lua calls use ThreadSynchronizer safely.
        /// </summary>
        private void ProcessPendingAction()
        {
            var action = System.Threading.Interlocked.Exchange(ref _pendingAction, null);
            if (action == null) return;

            try
            {
                switch (action.ActionType)
                {
                    case Communication.ActionType.SendGroupInvite:
                        var playerName = action.Parameters.FirstOrDefault()?.StringParam;
                        if (!string.IsNullOrEmpty(playerName))
                        {
                            DiagLog($"ACTION: Inviting '{playerName}' to group");
                            _logger.LogInformation($"Processing StateManager action: invite '{playerName}' to group");
                            _objectManager?.InviteByName(playerName);
                        }
                        break;

                    case Communication.ActionType.AcceptGroupInvite:
                        DiagLog("ACTION: Accepting group invite");
                        _logger.LogInformation("Processing StateManager action: accept group invite");
                        _objectManager?.AcceptGroupInvite();
                        break;

                    case Communication.ActionType.LeaveGroup:
                        DiagLog("ACTION: Leaving group");
                        _logger.LogInformation("Processing StateManager action: leave group");
                        _objectManager?.LeaveGroup();
                        break;

                    default:
                        DiagLog($"ACTION: Unhandled action type {action.ActionType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                DiagLog($"ProcessPendingAction ERROR: {ex.Message}");
                _logger.LogWarning(ex, "Error processing pending action from StateManager");
            }
        }

        private Game.WoWPlayer BuildPlayerProtobuf(ForegroundBotRunner.Objects.WoWUnit unit)
        {
            var pos = unit.Position;
            var player = new Game.WoWPlayer
            {
                Unit = BuildUnitProtobuf(unit),
            };

            // Populate player-specific fields from LocalPlayer
            if (unit is ForegroundBotRunner.Objects.LocalPlayer lp)
            {
                try { player.PlayerXP = 0; } catch { } // Not implemented yet
                try { player.Coinage = 0; } catch { }   // Not implemented yet
            }

            return player;
        }

        private Game.WoWUnit BuildUnitProtobuf(ForegroundBotRunner.Objects.WoWUnit unit)
        {
            var pos = unit.Position;
            var protoUnit = new Game.WoWUnit
            {
                GameObject = new Game.WoWGameObject
                {
                    Base = new Game.WoWObject
                    {
                        Guid = unit.Guid,
                        ObjectType = (uint)unit.ObjectType,
                        Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z },
                        Facing = unit.Facing,
                        ScaleX = unit.ScaleX,
                    },
                    FactionTemplate = (uint)unit.FactionId,
                    Level = (uint)unit.Level,
                },
                Health = (uint)unit.Health,
                MaxHealth = (uint)unit.MaxHealth,
                TargetGuid = unit.TargetGuid,
                UnitFlags = (uint)unit.UnitFlags,
                DynamicFlags = (uint)unit.DynamicFlags,
                MovementFlags = (uint)unit.MovementFlags,
                MountDisplayId = unit.MountDisplayId,
                ChannelSpellId = (uint)unit.ChannelingId,
                SummonedBy = unit.SummonedByGuid,
                NpcFlags = (uint)unit.NPCFlags,
            };

            // Power map: 0=Mana, 1=Rage, 3=Energy
            try { if (unit.MaxMana > 0) { protoUnit.Power[0] = (uint)unit.Mana; protoUnit.MaxPower[0] = (uint)unit.MaxMana; } } catch { }
            try { if (unit.Rage > 0) { protoUnit.Power[1] = (uint)unit.Rage; protoUnit.MaxPower[1] = 100; } } catch { }
            try { protoUnit.Power[3] = (uint)unit.Energy; protoUnit.MaxPower[3] = 100; } catch { }

            // Auras (buff spell IDs)
            try
            {
                foreach (var buff in unit.Buffs)
                    protoUnit.Auras.Add((uint)buff.Id);
            }
            catch { }

            return protoUnit;
        }

        private Game.WoWGameObject BuildGameObjectProtobuf(ForegroundBotRunner.Objects.WoWGameObject go)
        {
            var pos = go.Position;
            return new Game.WoWGameObject
            {
                Base = new Game.WoWObject
                {
                    Guid = go.Guid,
                    ObjectType = (uint)go.ObjectType,
                    Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z },
                    Facing = go.Facing,
                },
                GoState = (uint)go.GoState,
                Level = go.Level,
                FactionTemplate = go.FactionTemplate,
                DisplayId = go.DisplayId,
                Flags = go.Flags,
                DynamicFlags = (uint)go.DynamicFlags,
                Name = go.Name,
                Entry = go.Entry,
                GameObjectType = go.TypeId,
                ArtKit = go.ArtKit,
                AnimProgress = go.AnimProgress,
            };
        }

        private static ActionType GrindPhaseToActionType(GrindBotPhase phase) => phase switch
        {
            GrindBotPhase.Idle => ActionType.Wait,
            GrindBotPhase.FindTarget => ActionType.Wait,
            GrindBotPhase.MoveToTarget => ActionType.Goto,
            GrindBotPhase.Combat => ActionType.InteractWith,
            GrindBotPhase.Loot => ActionType.InteractWith,
            GrindBotPhase.Rest => ActionType.Wait,
            GrindBotPhase.Dead => ActionType.Wait,
            _ => ActionType.Wait,
        };

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ForegroundBotWorker stop requested...");

            // Stop any ongoing movement recording
            if (_movementRecorder?.IsRecording == true)
            {
                _logger.LogInformation("Stopping active movement recording...");
                _movementRecorder.StopRecording();
            }
            _movementRecorder?.Dispose();

            // 63d: Dispose the named-pipe logger provider
            _pipeLoggerProvider?.Dispose();

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("ForegroundBotWorker stopped.");
        }
    }
}
#endif
