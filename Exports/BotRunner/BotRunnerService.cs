using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Helpers;
using BotRunner.Interfaces;
using BotRunner.SequenceBuilders;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.I;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        private readonly IObjectManager _objectManager;

        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly ITalentService? _talentService;
        private readonly IEquipmentService? _equipmentService;
        private readonly IDependencyContainer _container;
        private readonly Func<IAgentFactory?>? _agentFactoryAccessor;
        private readonly Constants.BotBehaviorConfig _behaviorConfig;
        private readonly IDiagnosticPacketTraceRecorder? _diagnosticPacketTraceRecorder;
        private readonly bool _autoReleaseCorpseTaskEnabled;
        private readonly bool _autoRetrieveCorpseTaskEnabled;

        private WoWActivitySnapshot _activitySnapshot;

        // Message buffers -- collected from IWoWEventHandler events, flushed to snapshot each tick
        private readonly Queue<string> _recentChatMessages = new();
        private readonly Queue<string> _recentErrors = new();
        private const int MaxBufferedMessages = 50;

        // DiagLog writes to file and Serilog. FG context lacks Serilog global config.
        // Instance path is scoped per bot account; static overload logs to Serilog only
        // for callers (tasks) that don't have an instance reference.
        private readonly string _diagLogPath;
        internal void DiagLog(string msg)
        {
            Log.Information("[DIAG] {Msg}", msg);
            try { System.IO.File.AppendAllText(_diagLogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {msg}\n"); } catch { }
        }
        internal static void DiagLog(string msg, string? logPath = null)
        {
            Log.Information("[DIAG] {Msg}", msg);
            if (logPath != null)
                try { System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {msg}\n"); } catch { }
        }

        private Task? _asyncBotTaskRunnerTask;
        private CancellationTokenSource? _cts;
        private long _tickCount;

        private IBehaviourTreeNode? _behaviorTree;
        private BehaviourTreeStatus _behaviorTreeStatus = BehaviourTreeStatus.Success;

        // Spell-cast lockout: prevents movement actions from interrupting active spell casts.
        // Channeled spells like fishing need time to complete without being overridden by GoTo.
        private DateTime _spellCastLockoutUntil = DateTime.MinValue;
        private const double SpellCastLockoutSeconds = 20.0;

        // Action dispatch correlation: stable token linking receive -> dispatch -> completion logs.
        private string _currentActionCorrelationId = "";
        private long _actionSequenceNumber;

        private readonly Stack<IBotTask> _botTasks = new();
        private bool _tasksInitialized;
        // Sticky flag: once we've entered the world, never fall back to login/charselect
        // sequences until an explicit logout is detected. Prevents CreateCharacter spam
        // during transient state drops (teleports, zone transitions).
        private bool _everEnteredWorld;
        // Set when we initiate a character delete (race/gender mismatch).
        // Cleared once the char list is empty so we don't spam delete every tick.
        private bool _pendingCharacterDeletion;
        private DateTime _lastDeathRecoveryPush = DateTime.MinValue;
        private DateTime _lastReleaseSpiritCommandUtc = DateTime.MinValue;
        private static readonly TimeSpan ReleaseSpiritCommandCooldown = TimeSpan.FromSeconds(2);
        private Position? _lastKnownAlivePosition;

        // Extracted components
        private readonly SnapshotBuilder _snapshotBuilder;
        private readonly DiagnosticsRecorder _diagnosticsRecorder;
        private readonly ActionDispatcher _actionDispatcher;
        private readonly CombatSequenceBuilder _combatSequences;
        private readonly MovementSequenceBuilder _movementSequences;
        private readonly InteractionSequenceBuilder _interactionSequences;

        // Binary compatibility shim for already-built service binaries that still bind to the
        // pre-packet-recorder constructor signature. Keep parameter names distinct so current
        // source callers continue to bind to the newer overload when using named arguments.
        public BotRunnerService(IObjectManager objectManager,
                                 CharacterStateUpdateClient characterStateUpdateClient,
                                 IDependencyContainer container,
                                 Func<IAgentFactory?>? legacyAgentFactoryAccessor,
                                 string? legacyAccountName,
                                 ITalentService? legacyTalentService,
                                 IEquipmentService? legacyEquipmentService,
                                 Constants.BotBehaviorConfig? legacyBehaviorConfig)
            : this(
                objectManager,
                characterStateUpdateClient,
                container,
                legacyAgentFactoryAccessor,
                legacyAccountName,
                legacyTalentService,
                legacyEquipmentService,
                legacyBehaviorConfig,
                diagnosticPacketTraceRecorder: null)
        {
        }

        public BotRunnerService(IObjectManager objectManager,
                                 CharacterStateUpdateClient characterStateUpdateClient,
                                 IDependencyContainer container,
                                 Func<IAgentFactory?>? agentFactoryAccessor = null,
                                 string? accountName = null,
                                 ITalentService? talentService = null,
                                 IEquipmentService? equipmentService = null,
                                 Constants.BotBehaviorConfig? behaviorConfig = null,
                                 IDiagnosticPacketTraceRecorder? diagnosticPacketTraceRecorder = null)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            _activitySnapshot = new() { AccountName = accountName ?? "?" };
            _agentFactoryAccessor = agentFactoryAccessor;

            _characterStateUpdateClient = characterStateUpdateClient ?? throw new ArgumentNullException(nameof(characterStateUpdateClient));
            _talentService = talentService;
            _equipmentService = equipmentService;
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _behaviorConfig = behaviorConfig ?? new Constants.BotBehaviorConfig();
            _diagnosticPacketTraceRecorder = diagnosticPacketTraceRecorder;

            var logsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
            System.IO.Directory.CreateDirectory(logsDir);
            _diagLogPath = System.IO.Path.Combine(logsDir, $"botrunner_{accountName ?? "unknown"}.diag.log");

            _autoReleaseCorpseTaskEnabled = !GetEnvironmentFlag("WWOW_DISABLE_AUTORELEASE_CORPSE_TASK");
            _autoRetrieveCorpseTaskEnabled = !GetEnvironmentFlag("WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK");

            // Initialize extracted components
            _snapshotBuilder = new SnapshotBuilder(objectManager, agentFactoryAccessor);
            _diagnosticsRecorder = new DiagnosticsRecorder(objectManager, diagnosticPacketTraceRecorder);
            _diagnosticsRecorder.AccountNameAccessor = () => _activitySnapshot?.AccountName ?? "unknown";

            _combatSequences = new CombatSequenceBuilder(objectManager, container);
            _movementSequences = new MovementSequenceBuilder(objectManager, container);
            _interactionSequences = new InteractionSequenceBuilder(objectManager, agentFactoryAccessor);

            _actionDispatcher = new ActionDispatcher(
                objectManager,
                container,
                agentFactoryAccessor,
                _behaviorConfig,
                _combatSequences,
                _movementSequences,
                _interactionSequences,
                () => _botTasks,
                EnqueueDiagnosticMessage,
                msg => DiagLog(msg),
                player => IsDeadOrGhostState(player),
                player => IsGhostState(player),
                player => IsCorpseState(player),
                () => _lastReleaseSpiritCommandUtc,
                value => _lastReleaseSpiritCommandUtc = value,
                () => _lastKnownAlivePosition);

            // Subscribe to chat/error events for test observability
            SubscribeToMessageEvents();
        }

        private static bool GetEnvironmentFlag(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        public void Start()
        {
            if (_asyncBotTaskRunnerTask == null || _asyncBotTaskRunnerTask.IsCompleted)
            {
                _cts = new CancellationTokenSource();
                _asyncBotTaskRunnerTask = StartBotTaskRunnerAsync(_cts.Token);
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _asyncBotTaskRunnerTask?.Wait(1000);
            }
            catch { }
            finally
            {
                _asyncBotTaskRunnerTask?.Dispose();
                _asyncBotTaskRunnerTask = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task StartBotTaskRunnerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_objectManager.HasEnteredWorld && _objectManager.IsInMapTransition)
                    {
                        var jitter = 500 + Random.Shared.Next(0, 1000);
                        await Task.Delay(jitter, cancellationToken);
                        continue;
                    }

                    _snapshotBuilder.PopulateSnapshotFromObjectManager(
                        _activitySnapshot,
                        Interlocked.Read(ref _tickCount),
                        FlushMessageBuffers,
                        UpdateLastKnownAlivePosition);
                    _diagnosticsRecorder.CaptureTransformFrame(_botTasks, Interlocked.Read(ref _tickCount), _activitySnapshot);

                    // DIAG: log MapId in snapshot for BG transfer debugging
                    var snapMapId = _activitySnapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                    if (snapMapId == 489 && _tickCount % 10 == 0)
                        Log.Information("[BG-DIAG] Snapshot contains MapId=489 at tick {Tick}", _tickCount);

                    Communication.WoWActivitySnapshot? incomingActivityMemberState = null;
                    try
                    {
                        incomingActivityMemberState = await _characterStateUpdateClient
                            .SendMemberStateUpdateAsync(_activitySnapshot, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex) when (
                        ex is ObjectDisposedException
                        || ex is System.IO.IOException
                        || ex is System.Net.Sockets.SocketException
                        || ex is InvalidOperationException
                        || ex is TimeoutException)
                    {
                    }

                    var playerWorldReady = _objectManager.HasEnteredWorld
                        && WorldEntryHydration.IsReadyForWorldInteraction(_objectManager.Player);

                    Interlocked.Increment(ref _tickCount);
                    if (_tickCount % 100 == 1)
                    {
                        var action = incomingActivityMemberState?.CurrentAction;
                        var actionType = action?.ActionType.ToString() ?? "null";
                        var taskTop = _botTasks.Count > 0 ? _botTasks.Peek().GetType().Name : "empty";
                        var screenState = _activitySnapshot?.ScreenState ?? "?";
                        var mapId = (_objectManager.Player as GameData.Core.Interfaces.IWoWPlayer)?.MapId ?? 0;
                        DiagLog($"[TICK#{_tickCount}] ready={playerWorldReady} action={actionType} tree={_behaviorTreeStatus} tasks={_botTasks.Count}({taskTop}) screen={screenState} map={mapId} char={_activitySnapshot?.CharacterName ?? "?"}");
                    }

                    UpdateBehaviorTree(incomingActivityMemberState);

                    if (_behaviorTree != null)
                    {
                        var prevStatus = _behaviorTreeStatus;
                        _behaviorTreeStatus = _behaviorTree.Tick(new TimeData(0.1f));

                        if (prevStatus == BehaviourTreeStatus.Running && _behaviorTreeStatus != BehaviourTreeStatus.Running
                            && !string.IsNullOrEmpty(_currentActionCorrelationId))
                        {
                            Log.Information($"[BOT RUNNER] Behavior tree completed with {_behaviorTreeStatus} [{_currentActionCorrelationId}]");
                        }
                    }

                    if (playerWorldReady)
                    {
                        PushDeathRecoveryIfNeeded();

                        var hasDeathRecoveryTask =
                            _botTasks.Count > 0 &&
                            (_botTasks.Peek() is Tasks.ReleaseCorpseTask || _botTasks.Peek() is Tasks.RetrieveCorpseTask);
                        var canRunGeneralTasks = _behaviorTree == null || _behaviorTreeStatus != BehaviourTreeStatus.Running;

                        if (_botTasks.Count > 0 && (hasDeathRecoveryTask || canRunGeneralTasks))
                            _botTasks.Peek().Update();
                    }

                    _activitySnapshot = incomingActivityMemberState;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BOT RUNNER] {ex}");
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        private void UpdateBehaviorTree(WoWActivitySnapshot? incomingActivityMemberState)
        {
            var playerWorldReady = _objectManager.HasEnteredWorld
                && WorldEntryHydration.IsReadyForWorldInteraction(_objectManager.Player);

            if (incomingActivityMemberState?.CurrentAction != null
                && incomingActivityMemberState.CurrentAction.ActionType != Communication.ActionType.Wait
                && !playerWorldReady)
            {
                var p = _objectManager.Player;
                Log.Warning($"[BOT RUNNER] ACTION DROPPED: playerWorldReady=false " +
                    $"(HasEnteredWorld={_objectManager.HasEnteredWorld}, " +
                    $"Player={p != null}, Guid={p?.Guid ?? 0}, Pos={p?.Position != null}, MaxHP={p?.MaxHealth ?? 0}) " +
                    $"action={incomingActivityMemberState.CurrentAction.ActionType}");
            }

            if (incomingActivityMemberState?.CurrentAction != null
                && incomingActivityMemberState.CurrentAction.ActionType == Communication.ActionType.RetrieveCorpse)
            {
                DiagLog($"[TICK-DIAG] RetrieveCorpse action received. playerWorldReady={playerWorldReady} taskCount={_botTasks.Count} treeStatus={_behaviorTreeStatus}");
            }

            if (playerWorldReady
                && incomingActivityMemberState?.CurrentAction != null
                && incomingActivityMemberState.CurrentAction.ActionType != Communication.ActionType.Wait)
            {
                var action = incomingActivityMemberState.CurrentAction;
                DiagLog($"[ACTION-RECV] type={action.ActionType} params={action.Parameters.Count} ready={playerWorldReady}");

                if (action.ActionType == Communication.ActionType.Goto
                    && DateTime.UtcNow < _spellCastLockoutUntil)
                {
                    return;
                }

                _currentActionCorrelationId = $"act-{Interlocked.Increment(ref _actionSequenceNumber)}";
                Log.Information($"[BOT RUNNER] Received action from StateManager: {action.ActionType} ({(int)action.ActionType}) [{_currentActionCorrelationId}]");

                if (_diagnosticsRecorder.HandleDiagnosticAction(action))
                    return;

                var actionList = ActionDispatcher.ConvertActionMessageToCharacterActions(action);
                if (actionList.Count > 0)
                {
                    if (action.ActionType == Communication.ActionType.CastSpell
                        || action.ActionType == Communication.ActionType.GatherNode)
                    {
                        _spellCastLockoutUntil = DateTime.UtcNow.AddSeconds(SpellCastLockoutSeconds);
                    }

                    Log.Information($"[BOT RUNNER] Building behavior tree for: {actionList[0].Item1} [{_currentActionCorrelationId}]");
                    _behaviorTree = _actionDispatcher.BuildBehaviorTreeFromActions(actionList);
                    _behaviorTreeStatus = BehaviourTreeStatus.Running;
                    _activitySnapshot.PreviousAction = action;
                    return;
                }
            }

            if (_behaviorTree != null && _behaviorTreeStatus == BehaviourTreeStatus.Running)
            {
                return;
            }

            if (_objectManager.HasEnteredWorld)
            {
                _everEnteredWorld = true;
                if (!WorldEntryHydration.IsReadyForWorldInteraction(_objectManager.Player))
                {
                    _behaviorTree = null;
                    return;
                }

                if (!_tasksInitialized)
                {
                    _tasksInitialized = true;
                    InitializeTaskSequence();
                }

                _behaviorTree = null;
                return;
            }

            if (_everEnteredWorld)
            {
                var loginScreen = _objectManager.LoginScreen;
                if (loginScreen != null && loginScreen.IsOpen)
                {
                    Log.Information("[BOT RUNNER] Explicit logout detected, resetting world entry state");
                    _everEnteredWorld = false;
                    _tasksInitialized = false;
                }
                else
                {
                    return;
                }
            }

            if (_objectManager.LoginScreen?.IsLoggedIn != true)
            {
                _behaviorTree = _interactionSequences.BuildLoginSequence(incomingActivityMemberState?.AccountName ?? _activitySnapshot.AccountName, "PASSWORD");
                return;
            }

            if (_objectManager.RealmSelectScreen?.CurrentRealm == null)
            {
                _behaviorTree = _interactionSequences.BuildRealmSelectionSequence();
                return;
            }

            if (_objectManager.CharacterSelectScreen?.HasReceivedCharacterList != true)
            {
                if (_objectManager.CharacterSelectScreen?.HasRequestedCharacterList != true)
                {
                    _behaviorTree = _interactionSequences.BuildRequestCharacterSequence();
                }

                return;
            }

            Class @class = WoWNameGenerator.ResolveClass(_activitySnapshot.AccountName);
            Race race = WoWNameGenerator.ResolveRace(_activitySnapshot.AccountName);
            Gender gender = WoWNameGenerator.ResolveGender(@class);

            var charSelects = _objectManager.CharacterSelectScreen?.CharacterSelects;

            if (charSelects?.Count > 0)
            {
                var first = charSelects[0];
                if (first.Gender != gender || first.Race != race)
                {
                    if (!_pendingCharacterDeletion)
                    {
                        Log.Warning("[BOT RUNNER] Character mismatch: existing={Race}/{Gender}, configured={CfgRace}/{CfgGender}. Deleting to recreate.",
                            first.Race, first.Gender, race, gender);
                        _behaviorTree = _interactionSequences.BuildDeleteCharacterSequence(first.Guid);
                        _pendingCharacterDeletion = true;
                    }
                    return;
                }
            }

            if (charSelects?.Count == 0)
            {
                if (_objectManager.CharacterSelectScreen?.IsCharacterCreationPending == true)
                {
                    return;
                }

                _pendingCharacterDeletion = false;
                var createAttempts = _objectManager.CharacterSelectScreen?.CharacterCreateAttempts ?? 0;
                if (createAttempts > 0)
                {
                    Log.Warning("[BOT RUNNER] Retrying character creation for {Account} (attempt {Attempt})",
                        _activitySnapshot.AccountName,
                        createAttempts + 1);
                }

                _behaviorTree = _interactionSequences.BuildCreateCharacterSequence(
                    [
                        WoWNameGenerator.GenerateName(
                            race,
                            gender,
                            BuildCharacterUniquenessSeed(_activitySnapshot.AccountName, createAttempts)),
                        race,
                        gender,
                        @class,
                        0,
                        0,
                        0,
                        0,
                        0
                    ]
                );

                return;
            }

            _pendingCharacterDeletion = false;
            _behaviorTree = _interactionSequences.BuildEnterWorldSequence(_objectManager.CharacterSelectScreen?.CharacterSelects[0].Guid ?? 0);
        }

        internal static string? BuildCharacterUniquenessSeed(string? accountName, int createAttempts)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return accountName;

            return createAttempts <= 0
                ? accountName
                : $"{accountName}:{createAttempts}";
        }

        internal static Position? ResolveNextWaypoint(Position[]? positions, Action<string>? logAction = null)
        {
            if (positions == null || positions.Length == 0)
            {
                logAction?.Invoke("Path contained no waypoints. Skipping movement.");
                return null;
            }

            if (positions.Length == 1)
            {
                logAction?.Invoke("Path contained a single waypoint. Using waypoint[0].");
                return positions[0];
            }

            return positions[1];
        }

        private void PushDeathRecoveryIfNeeded()
        {
            var player = _objectManager.Player;
            if (player == null) return;

            var isGhost = IsGhostState(player);
            var isCorpse = IsCorpseState(player);
            var isDeadOrGhost = isGhost || isCorpse;
            if (!isDeadOrGhost)
                return;

            if (_botTasks.Count > 0 &&
                (_botTasks.Peek() is Tasks.ReleaseCorpseTask || _botTasks.Peek() is Tasks.RetrieveCorpseTask))
                return;

            if ((DateTime.UtcNow - _lastDeathRecoveryPush).TotalSeconds < 5)
                return;

            var context = new ActionDispatcher.BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig, EnqueueDiagnosticMessage);

            if (isCorpse)
            {
                if (!_autoReleaseCorpseTaskEnabled)
                {
                    DiagLog("DeathRecovery: corpse detected but auto-release is disabled; waiting for explicit ReleaseCorpse action");
                    return;
                }

                Log.Information("[TASK-PUSH] task=ReleaseCorpseTask reason=DeathRecoveryCorpse hp={Health} ghostFlag={GhostFlag} standDead={StandDead}",
                    player.Health, HasGhostFlag(player), IsStandStateDead(player));
                DiagLog("DeathRecovery: corpse -> push ReleaseCorpseTask");
                _lastDeathRecoveryPush = DateTime.UtcNow;
                _botTasks.Push(new Tasks.ReleaseCorpseTask(context));
                return;
            }

            if (isGhost)
            {
                if (!_autoRetrieveCorpseTaskEnabled)
                {
                    DiagLog("DeathRecovery: ghost detected but auto-retrieve is disabled; waiting for explicit RetrieveCorpse action");
                    return;
                }

                var corpsePos = player.CorpsePosition;
                if (IsZeroPosition(corpsePos) && _lastKnownAlivePosition != null)
                {
                    corpsePos = new Position(_lastKnownAlivePosition.X, _lastKnownAlivePosition.Y, _lastKnownAlivePosition.Z);
                    Log.Information("[BOT RUNNER] Corpse position unavailable; falling back to last alive position ({X:F0}, {Y:F0}, {Z:F0})",
                        corpsePos.X, corpsePos.Y, corpsePos.Z);
                    DiagLog($"DeathRecovery: ghost fallback corpse pos=({corpsePos.X:F1},{corpsePos.Y:F1},{corpsePos.Z:F1})");
                }

                if (IsZeroPosition(corpsePos))
                {
                    DiagLog("DeathRecovery: ghost but corpse position unavailable (no fallback)");
                    return;
                }

                Log.Information("[TASK-PUSH] task=RetrieveCorpseTask reason=DeathRecoveryGhost pos=({X:F0},{Y:F0},{Z:F0}) hp={Health} reclaimDelay={ReclaimDelay}s ghostFlag={GhostFlag} standDead={StandDead}",
                    corpsePos.X, corpsePos.Y, corpsePos.Z,
                    player.Health, player.CorpseRecoveryDelaySeconds, HasGhostFlag(player), IsStandStateDead(player));
                DiagLog($"DeathRecovery: ghost -> push RetrieveCorpseTask pos=({corpsePos.X:F1},{corpsePos.Y:F1},{corpsePos.Z:F1})");
                _lastDeathRecoveryPush = DateTime.UtcNow;
                _botTasks.Push(new Tasks.RetrieveCorpseTask(context, corpsePos));
            }
        }

        internal static bool IsZeroPosition(Position? pos)
        {
            if (pos == null)
                return true;

            return Math.Abs(pos.X) < 0.001f
                && Math.Abs(pos.Y) < 0.001f
                && Math.Abs(pos.Z) < 0.001f;
        }

        private static bool HasGhostFlag(IWoWLocalPlayer player) => DeathStateDetection.HasGhostFlag(player);
        private static bool IsStandStateDead(IWoWLocalPlayer player) => DeathStateDetection.IsStandStateDead(player);
        private static bool IsGhostState(IWoWLocalPlayer player) => DeathStateDetection.IsGhost(player);
        private static bool IsCorpseState(IWoWLocalPlayer player) => DeathStateDetection.IsCorpse(player);
        private static bool IsDeadOrGhostState(IWoWLocalPlayer player) => DeathStateDetection.IsDeadOrGhost(player);

        private void UpdateLastKnownAlivePosition(IWoWLocalPlayer player)
        {
            if (IsDeadOrGhostState(player))
                return;

            var pos = player.Position;
            if (IsZeroPosition(pos))
                return;

            _lastKnownAlivePosition = new Position(pos!.X, pos.Y, pos.Z);
        }

        private void InitializeTaskSequence()
        {
            var accountName = _activitySnapshot.AccountName;
            if (string.IsNullOrEmpty(accountName) || accountName == "?" || accountName.Length < 4)
            {
                Log.Information("[BOT RUNNER] No valid account name for task initialization, using wait.");
                return;
            }

            var context = new ActionDispatcher.BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig, EnqueueDiagnosticMessage);

            try
            {
                var @class = WoWNameGenerator.ResolveClass(accountName);

                _botTasks.Push(new Tasks.IdleTask(context));
                Log.Information("[BOT RUNNER] Initialized idle task sequence for {Account} using {Profile} ({Class})",
                    accountName, _container.ClassContainer.Name, @class);
            }
            catch (Exception ex)
            {
                Log.Error($"[BOT RUNNER] Failed to initialize task sequence: {ex.Message}");
            }
        }
    }
}
