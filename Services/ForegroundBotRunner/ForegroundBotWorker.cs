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
using ForegroundBotRunner.Diagnostics;
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
        private ForegroundPacketTraceRecorder? _packetTraceRecorder;
        private ForegroundAckCorpusRecorder? _ackCorpusRecorder;
        private PathfindingClient? _pathfindingClient;
        private BotRunnerService? _botRunner;

        // Automated recording: runs controlled movement scenarios instead of idle
        private readonly bool _automatedRecording;
        private readonly bool _recordingArtifactsEnabled;
        private Task? _scenarioRunnerTask;

        // Login credentials received from StateManager via IPC
        private string _accountName = string.Empty;

        // Track whether SignalEventManager hooks have been initialized after entering world
        private bool _hooksInitialized = false;

        // Timestamp of world entry — polls are deferred for 2s to let WoW's UI frame system stabilize
        private DateTime _worldEntryTime = DateTime.MinValue;

        // Packet-driven connection state machine
        private readonly ConnectionStateMachine _connectionState = new();
        private uint _lastObservedContinentId = 0xFFFFFFFF;
        private static readonly ushort FishingCustomAnimOpcode = (ushort)Opcode.SMSG_GAMEOBJECT_CUSTOM_ANIM;
        private static readonly TimeSpan WorldEntryCinematicGrace = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan WorldEntryCinematicRetry = TimeSpan.FromSeconds(2);
        internal static bool IsWorkerInTransition(bool isInMapTransition, uint continentId) =>
            isInMapTransition || continentId == 0xFFFFFFFF || continentId == 0xFF;
        internal static bool ShouldPollWorkerLuaDiagnostics(bool isInMapTransition, WoWScreenState screenState) =>
            !isInMapTransition && screenState != WoWScreenState.LoadingWorld;
        private DateTime _loadingWorldSinceUtc = DateTime.MinValue;
        private DateTime _lastWorldEntryCinematicDismissAttemptUtc = DateTime.MinValue;
        private bool _worldEntryHydrated;
        private const string WorldEntryCinematicDismissLua =
            "if StopCinematic then pcall(StopCinematic) end " +
            "if CinematicFrame_CancelCinematic then pcall(CinematicFrame_CancelCinematic) end " +
            "if StopMovie then pcall(StopMovie) end " +
            "if MovieFrame and MovieFrame.CloseDialog and MovieFrame.CloseDialog:IsVisible() then MovieFrame.CloseDialog:Click() end " +
            "if MovieFrameCloseDialog and MovieFrameCloseDialog:IsVisible() then MovieFrameCloseDialog:Click() end";

        // Diagnostic logging to file (for debugging when running inside WoW.exe)
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        private static int _diagnosticLogCount = 0;

        // Named pipe logger provider — when connected, DiagLog becomes fallback-only
        private static NamedPipeLoggerProvider? _pipeLoggerProvider;

        static ForegroundBotWorker()
        {
            DiagnosticLogPath = RecordingFileArtifactGate.ResolveWoWLogsPath("foreground_bot_debug.log");
            if (!string.IsNullOrWhiteSpace(DiagnosticLogPath))
            {
                try { File.WriteAllText(DiagnosticLogPath, $"=== ForegroundBotWorker Diagnostic Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\nLogPath: {DiagnosticLogPath}\n"); } catch { }
            }
        }

        /// <summary>
        /// Writes a diagnostic message to the file log for debugging.
        /// Always writes to file — pipe logger may silently drop messages.
        /// </summary>
        private static void DiagLog(string message)
        {
            if (string.IsNullOrWhiteSpace(DiagnosticLogPath))
            {
                return;
            }

            try
            {
                lock (DiagnosticLogLock)
                {
                    RotateLogIfNeeded();
                    _diagnosticLogCount++;
                    var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{_diagnosticLogCount:D5}] {message}\n";
                    File.AppendAllText(DiagnosticLogPath, line);
                }
            }
            catch { /* Ignore logging errors */ }
        }

        /// <summary>
        /// Rotates the diagnostic log file if it exceeds 10 MB. The previous log is
        /// kept as a single ".old" file; older history is discarded.
        /// </summary>
        private static void RotateLogIfNeeded()
        {
            try
            {
                if (File.Exists(DiagnosticLogPath) && new FileInfo(DiagnosticLogPath).Length > 10 * 1024 * 1024)
                {
                    var oldPath = DiagnosticLogPath + ".old";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(DiagnosticLogPath, oldPath);
                }
            }
            catch { /* ignore rotation errors */ }
        }

        public ForegroundBotWorker(IConfiguration configuration, ILoggerFactory loggerFactory, PathfindingClient? pathfindingClient = null)
        {
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _pathfindingClient = pathfindingClient;
            _logger = loggerFactory.CreateLogger<ForegroundBotWorker>();
            _automatedRecording = Environment.GetEnvironmentVariable("BLOOGBOT_AUTOMATED_RECORDING") == "1";
            _recordingArtifactsEnabled = RecordingArtifactsFeature.IsEnabled();

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

            if (_automatedRecording && !_recordingArtifactsEnabled)
            {
                _logger.LogWarning("Automated recording requested but recording artifacts are disabled. Set {EnvVar}=1 to enable scenario captures.",
                    RecordingArtifactsFeature.EnvironmentVariableName);
            }
        }

        /// <summary>
        /// Connects to StateManager and requests an account assignment.
        /// When the injector already knows the target account, register with that explicit
        /// account name so multiple foreground bots do not collide on the same slot.
        /// Falls back to AccountName="?" only for legacy launches that did not pass one.
        /// </summary>
        internal static string ResolveStateManagerRegistrationAccount(string? configuredAccountName)
            => string.IsNullOrWhiteSpace(configuredAccountName) ? "?" : configuredAccountName.Trim();

        private async Task<bool> RequestAccountAssignmentAsync(CancellationToken stoppingToken)
        {
            var stateManagerIp = _configuration["CharacterStateListener:IpAddress"] ?? "127.0.0.1";
            var stateManagerPort = int.Parse(_configuration["CharacterStateListener:Port"] ?? "5002");
            var requestedAccountName = ResolveStateManagerRegistrationAccount(
                Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME"));

            _logger.LogInformation($"Connecting to StateManager at {stateManagerIp}:{stateManagerPort} for account assignment...");

            try
            {
                _stateUpdateClient = new CharacterStateUpdateClient(
                    stateManagerIp,
                    stateManagerPort,
                    _loggerFactory.CreateLogger<CharacterStateUpdateClient>());

                // Prefer the explicit configured account when available so multiple foreground
                // runners can register independently. Fall back to "?" for legacy launches.
                var request = new WoWActivitySnapshot
                {
                    AccountName = requestedAccountName,
                    Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                _logger.LogInformation("Requesting account assignment from StateManager (AccountName='{AccountName}')...",
                    requestedAccountName);

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
                        var workerContId = _objectManager?.ContinentId ?? 0xFFFFFFFF;
                        bool isInMapTransition = _objectManager?.IsInMapTransition == true;
                        bool workerInTransition = IsWorkerInTransition(isInMapTransition, workerContId);
                        var screenState = _objectManager?.GetCurrentScreenState() ?? WoWScreenState.Unknown;
                        var hasEnteredWorld = _objectManager?.HasEnteredWorld ?? false;

                        // Log state every 10 iterations (every 5 seconds)
                        if (loopCount % 10 == 1)
                        {
                            var playerName = ObjectManager.GetDiagnosticPlayerLabel(
                                () => _objectManager?.Player?.Name,
                                workerInTransition);
                            var connState = _connectionState.CurrentState;
                            var pktSend = PacketLogger.SendCount;
                            var pktRecv = PacketLogger.RecvCount;
                            var rawLoginState = ForegroundBotRunner.Mem.MemoryManager.ReadString(ForegroundBotRunner.Mem.Offsets.CharacterScreen.LoginState) ?? "(null)";
                            var maxChars = ObjectManager.MaxCharacterCount;
                            DiagLog($"LOOP#{loopCount}: ScreenState={screenState}, LoginState='{rawLoginState}', HasEnteredWorld={hasEnteredWorld}, Player={playerName}, ConnState={connState}, TX={pktSend}, RX={pktRecv}, MaxChars={maxChars}");
                            _logger.LogInformation(
                                "[FG-STATE] account={Account} screen={ScreenState} loginState={LoginState} enteredWorld={HasEnteredWorld} maxChars={MaxChars} connState={ConnState} player={Player}",
                                string.IsNullOrWhiteSpace(_accountName) ? "?" : _accountName,
                                screenState,
                                rawLoginState,
                                hasEnteredWorld,
                                maxChars,
                                connState,
                                playerName);
                        }

                        // Anti-AFK ALWAYS - prevents disconnect during login/charselect too
                        _objectManager?.AntiAfk();

                        // Ensure Lua error capture is active before world entry so realm/charselect
                        // failures are visible in logs without full 80-bot bring-up.
                        if (ShouldPollWorkerLuaDiagnostics(workerInTransition, screenState))
                        {
                            ObjectManager.EnsureLuaErrorCaptureInstalled("worker.loop");
                            if (loopCount % 6 == 0)
                            {
                                ObjectManager.CaptureLuaErrors("worker.loop");
                            }
                        }

                        if (hasEnteredWorld && screenState == WoWScreenState.InWorld && _objectManager?.Player != null)
                        {
                            _worldEntryHydrated = true;
                        }

                        if (hasEnteredWorld && screenState == WoWScreenState.LoadingWorld)
                        {
                            if (_loadingWorldSinceUtc == DateTime.MinValue)
                                _loadingWorldSinceUtc = DateTime.UtcNow;

                            var loadingDuration = DateTime.UtcNow - _loadingWorldSinceUtc;
                            var sinceLastAttempt = _lastWorldEntryCinematicDismissAttemptUtc == DateTime.MinValue
                                ? TimeSpan.MaxValue
                                : DateTime.UtcNow - _lastWorldEntryCinematicDismissAttemptUtc;

                            if (ShouldAttemptWorldEntryCinematicDismiss(
                                    screenState,
                                    hasEnteredWorld,
                                    _worldEntryHydrated,
                                    loadingDuration,
                                    sinceLastAttempt))
                            {
                                _lastWorldEntryCinematicDismissAttemptUtc = DateTime.UtcNow;
                                TryDismissWorldEntryCinematic(loadingDuration);
                            }
                        }
                        else
                        {
                            _loadingWorldSinceUtc = DateTime.MinValue;
                        }

                        // At character select with characters present + not already entering world:
                        // click "Enter World" button directly via Lua.
                        // Do not skip the intro cinematic for freshly created characters.
                        if (!ObjectManager.PauseNativeCallsDuringWorldEntry
                            && !hasEnteredWorld
                            && screenState == WoWScreenState.CharacterSelect
                            && ObjectManager.MaxCharacterCount > 0
                            && loopCount % 6 == 3) // Every 3s, rate-limited
                        {
                            try
                            {
                                DiagLog($"[FG-ENTER] CharSelect with {ObjectManager.MaxCharacterCount} char(s). Clicking Enter World button...");
                                ObjectManager.MainThreadLuaCall(
                                    "if CharSelectEnterWorldButton and CharSelectEnterWorldButton:IsVisible() then " +
                                    "CharSelectEnterWorldButton:Click() end");
                            }
                            catch (Exception ex) { DiagLog($"[FG-ENTER] Error: {ex.Message}"); }
                        }

                        // Initialize SignalEventManager hooks once after entering world.
                        // These inject assembly into WoW's event system and must NOT run during
                        // the world server handshake (causes disconnect).
                        if (_objectManager?.HasEnteredWorld == true && !_hooksInitialized)
                        {
                            ObjectManager.PauseNativeCallsDuringWorldEntry = false;
                            DiagLog("RESUMED native calls - now InWorld");

                            // NOTE: WardenDisabler.Initialize() is NOT called — Warden is disabled
                            // server-side (Warden.WinEnabled=0 in mangosd.conf). The hook at
                            // 0x006CA22E causes ILLEGAL_INSTRUCTION crashes when enabled.
                            // Re-enable only when connecting to a Warden-enabled server.

                            // Ensure Lua error capture remains installed after world entry.
                            try
                            {
                                ObjectManager.EnsureLuaErrorCaptureInstalled("world-entry");
                                ObjectManager.CaptureLuaErrors("world-entry");
                                DiagLog("Lua error capture ensured at world entry");
                            }
                            catch (Exception ex)
                            {
                                DiagLog($"Failed to ensure Lua error capture at world entry: {ex.Message}");
                            }

                            // Set WWOW_DISABLE_PACKET_HOOKS=1 to skip ALL hook installation (crash diagnostics)
                            var disableHooks = Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS");

                            try
                            {
                                if (disableHooks == "1")
                                {
                                    DiagLog("SignalEventManager hooks SKIPPED (WWOW_DISABLE_PACKET_HOOKS=1)");
                                }
                                else
                                {
                                    SignalEventManager.InitializeHooks();
                                    DiagLog("SignalEventManager hooks initialized");
                                }
                            }
                            catch (Exception ex)
                            {
                                DiagLog($"SignalEventManager hook init error: {ex.Message}");
                            }

                            // Initialize packet capture hooks
                            try
                            {
                                PacketLogger.OnPacketCaptured += _connectionState.ProcessPacket;
                                PacketLogger.OnPacketCaptured += HandleCapturedPacket;
                                if (disableHooks == "1")
                                {
                                    DiagLog("PacketLogger hooks SKIPPED (WWOW_DISABLE_PACKET_HOOKS=1)");
                                }
                                else
                                {
                                    PacketLogger.InitializeHooks();
                                }
                                _connectionState.ForceState(
                                    ConnectionStateMachine.State.InWorld,
                                    "initial world entry detected via HasEnteredWorld");
                                Mem.ThreadSynchronizer.SetConnectionStateMachine(_connectionState);
                                DiagLog($"PacketLogger hooks initialized (send={PacketLogger.IsActive}, recv={PacketLogger.IsRecvActive})");
                            }
                            catch (Exception ex)
                            {
                                DiagLog($"PacketLogger hook init error: {ex.Message}");
                            }

                            _hooksInitialized = true;
                            _worldEntryTime = DateTime.UtcNow;
                        }

                        // Poll MovementRecorder and run automated scenarios when in world.
                        // Check the broader map-transition guard plus raw ContinentId so FG stays
                        // inert during transfer/teleport windows before every downstream poll notices.
                        // Infer inbound packets from ContinentId changes for the state machine.
                        // This is a safety net when the recv hook misses opcodes (e.g., SMSG_LOGIN_VERIFY_WORLD
                        // is not captured by the FG recv hook during instance transitions).
                        if (_hooksInitialized && workerContId != _lastObservedContinentId)
                        {
                            bool wasInTransition = _lastObservedContinentId == 0xFF || _lastObservedContinentId == 0xFFFFFFFF;
                            bool nowValid = workerContId != 0xFF && workerContId != 0xFFFFFFFF;
                            bool nowInTransition = workerContId == 0xFF || workerContId == 0xFFFFFFFF;

                            if (nowInTransition && !wasInTransition)
                            {
                                // Entering loading screen = SMSG_TRANSFER_PENDING received
                                PacketLogger.RecordInboundPacket(0x003F); // SMSG_TRANSFER_PENDING
                                DiagLog($"ContinentId→0x{workerContId:X}: inferred SMSG_TRANSFER_PENDING");
                            }
                            else if (wasInTransition && nowValid)
                            {
                                // Loading complete, new map = SMSG_LOGIN_VERIFY_WORLD
                                // Cross-map: 0xFFFFFFFF → valid. Same-continent: 0xFF → valid.
                                PacketLogger.RecordInboundPacket(0x0236); // SMSG_LOGIN_VERIFY_WORLD
                                DiagLog($"ContinentId→0x{workerContId:X}: inferred SMSG_LOGIN_VERIFY_WORLD (map transition complete)");
                            }
                            else if (workerContId == 0xFFFFFFFF && _lastObservedContinentId < 0xFF)
                            {
                                // Was in world, now at charselect = logout/disconnect
                                PacketLogger.RecordInboundPacket(0x004D); // SMSG_LOGOUT_COMPLETE
                                DiagLog($"ContinentId→0xFFFFFFFF: inferred SMSG_LOGOUT_COMPLETE");
                            }
                            _lastObservedContinentId = workerContId;
                        }
                        if (_objectManager?.HasEnteredWorld == true && !workerInTransition)
                        {
                            // Defer polls for 2s after world entry — WoW's UI frame system
                            // (CreateFrame, FrameXML) needs time to stabilize after the loading screen.
                            // Without this, the first CreateFrame call in EnsureChatHook can native-crash.
                            if ((DateTime.UtcNow - _worldEntryTime).TotalSeconds < 2.0)
                            {
                                await Task.Delay(500, stoppingToken);
                                continue;
                            }

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
            if (_recordingArtifactsEnabled)
            {
                _movementRecorder = new MovementRecorder(() => _objectManager, _loggerFactory);
                _packetTraceRecorder = new ForegroundPacketTraceRecorder(_loggerFactory);
                _ackCorpusRecorder = new ForegroundAckCorpusRecorder(_loggerFactory);
                _ackCorpusRecorder.Start();
                _logger.LogInformation("MovementRecorder ready - say 'rec' in chat to toggle recording");
            }
            else
            {
                _movementRecorder = null;
                _packetTraceRecorder = null;
                _ackCorpusRecorder = null;
                _logger.LogInformation("Recording artifacts disabled; FG movement and packet recorders are inactive. Set {EnvVar}=1 to enable.",
                    RecordingArtifactsFeature.EnvironmentVariableName);
            }

            _logger.LogInformation("ObjectManager initialized - direct memory access enabled");
            return Task.CompletedTask;
        }

        internal static bool ShouldAttemptWorldEntryCinematicDismiss(
            WoWScreenState screenState,
            bool hasEnteredWorld,
            bool worldEntryHydrated,
            TimeSpan loadingWorldDuration,
            TimeSpan sinceLastAttempt)
        {
            return hasEnteredWorld
                && !worldEntryHydrated
                && screenState == WoWScreenState.LoadingWorld
                && loadingWorldDuration >= WorldEntryCinematicGrace
                && sinceLastAttempt >= WorldEntryCinematicRetry;
        }

        private void TryDismissWorldEntryCinematic(TimeSpan loadingDuration)
        {
            try
            {
                DiagLog($"[FG-CINEMATIC] Attempting world-entry cinematic dismiss (loading={loadingDuration.TotalSeconds:F1}s)");
                ObjectManager.MainThreadLuaCall(WorldEntryCinematicDismissLua);
                ObjectManager.CaptureLuaErrors("worker.world-entry-cinematic-dismiss");
            }
            catch (Exception ex)
            {
                DiagLog($"[FG-CINEMATIC] Dismiss attempt failed: {ex.Message}");
                _logger.LogDebug(ex, "World-entry cinematic dismiss attempt failed");
            }
        }

        private void HandleCapturedPacket(PacketDirection direction, ushort opcode, int size)
        {
            if (direction != PacketDirection.Recv || opcode != FishingCustomAnimOpcode)
                return;

            var objectManager = _objectManager;
            if (objectManager?.HasEnteredWorld != true)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    for (var attempt = 0; attempt < 6; attempt++)
                    {
                        await Task.Delay(75 + (attempt * 50)).ConfigureAwait(false);
                        if (objectManager.TryAutoInteractFishingBobberFromPacket())
                            return;
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"HandleCapturedPacket fishing custom anim failed: {ex.Message}");
                }
            });
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
                Mem.ThreadSynchronizer.ResetObjMgrValidState();
                _hooksInitialized = false;
                _worldEntryHydrated = false;
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
                Mem.ThreadSynchronizer.ResetObjMgrValidState();
                _hooksInitialized = false;
                _worldEntryHydrated = false;
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
                behaviorConfig: LoadBehaviorConfig(_configuration),
                diagnosticPacketTraceRecorder: _packetTraceRecorder);

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
            var @class = WoWNameGenerator.ResolveClass(accountName);
            var specOverride = Environment.GetEnvironmentVariable("WWOW_CHARACTER_SPEC");

            var botProfile = BotProfiles.Common.BotProfileResolver.Resolve(specOverride, @class);

            return new ClassContainer(
                botProfile.Name,
                botProfile.CreateRestTask,
                botProfile.CreateBuffTask,
                botProfile.CreateMoveToTargetTask,
                botProfile.CreatePvERotationTask,
                botProfile.CreatePvPRotationTask,
                pathfindingClient,
                createPullTargetTask: botProfile.CreatePullTargetTask);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ForegroundBotWorker stop requested...");

            _botRunner?.Stop();
            PacketLogger.OnPacketCaptured -= _connectionState.ProcessPacket;
            PacketLogger.OnPacketCaptured -= HandleCapturedPacket;

            // Stop any ongoing movement recording
            if (_movementRecorder?.IsRecording == true)
            {
                _logger.LogInformation("Stopping active movement recording...");
                _movementRecorder.StopRecording();
            }
            _movementRecorder?.Dispose();
            _packetTraceRecorder?.Dispose();
            _ackCorpusRecorder?.Dispose();

            // Dispose the named-pipe logger provider
            _pipeLoggerProvider?.Dispose();

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("ForegroundBotWorker stopped.");
        }
    }
}
#endif
