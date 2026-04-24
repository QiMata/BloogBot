using BotRunner.Activities;
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
using System.Diagnostics;
using System.Linq;
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
        private readonly string _initialAccountName;
        private readonly bool _useGmCommands;
        private readonly string? _assignedActivity;

        private WoWActivitySnapshot _activitySnapshot;

        // Message buffers -- collected from IWoWEventHandler events, flushed to snapshot each tick
        private readonly Queue<string> _recentChatMessages = new();
        private readonly Queue<string> _recentErrors = new();
        private readonly Queue<CommandAckEvent> _recentCommandAckEvents = new();
        private const int MaxBufferedMessages = 50;
        private const int MaxRecentCommandAcks = 10;
        private bool _battlegroundMessageEventsSubscribed;

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
        private PendingCommandAckState? _activeBehaviorTreeAck;
        private PendingCommandAckState? _activeLoadoutAck;

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
        private int _lastLoggedContainedItems = -1;
        private int _lastLoggedItemObjects = -1;
        private int _consecutiveTransitionSkips;
        private int _consecutiveNullStateResponses;
        private int _consecutiveStateUpdateFailures;

        private SnapshotChangeSignature _lastSentSignature;
        private DateTime _lastFullSnapshotSentAt = DateTime.MinValue;
        private static readonly TimeSpan FullSnapshotHeartbeatInterval = TimeSpan.FromSeconds(2);

        // P3.3: most recent LoadoutTask progress. Cached here so the snapshot keeps
        // reporting LoadoutReady/LoadoutFailed after the task itself is popped off
        // the stack (e.g. when downstream tasks take over post-loadout).
        private Communication.LoadoutStatus _lastLoadoutStatus = Communication.LoadoutStatus.LoadoutNotStarted;
        private string _lastLoadoutFailureReason = string.Empty;

        // P4.4: include RecentCommandAckCount so coordinator-visible ACK arrivals
        // trigger an immediate full snapshot. This does not reintroduce the P4.2
        // chat/error churn because ACK entries change rarely per dispatched command.
        private readonly record struct SnapshotChangeSignature(
            string ScreenState,
            int ConnectionState,
            uint CurrentMapId,
            bool IsObjectManagerValid,
            bool IsMapTransition,
            bool IsIndoors,
            ulong PartyLeaderGuid,
            int PositionBucketX,
            int PositionBucketY,
            int HealthBucket,
            bool IsDead,
            int LoadoutStatus,
            int RecentCommandAckCount);

        private readonly record struct PendingCommandAckState(
            string CorrelationId,
            Communication.ActionType ActionType,
            uint RelatedId);

        // Extracted components
        private readonly DiagnosticsRecorder _diagnosticsRecorder;
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
                                 IDiagnosticPacketTraceRecorder? diagnosticPacketTraceRecorder = null,
                                 bool useGmCommands = false,
                                 string? assignedActivity = null)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            _initialAccountName = accountName ?? "?";
            _activitySnapshot = new() { AccountName = _initialAccountName };
            _agentFactoryAccessor = agentFactoryAccessor;

            _characterStateUpdateClient = characterStateUpdateClient ?? throw new ArgumentNullException(nameof(characterStateUpdateClient));
            _talentService = talentService;
            _equipmentService = equipmentService;
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _behaviorConfig = behaviorConfig ?? new Constants.BotBehaviorConfig();
            _diagnosticPacketTraceRecorder = diagnosticPacketTraceRecorder;
            _useGmCommands = useGmCommands;
            _assignedActivity = string.IsNullOrWhiteSpace(assignedActivity) ? null : assignedActivity.Trim();

            var logsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
            System.IO.Directory.CreateDirectory(logsDir);
            _diagLogPath = System.IO.Path.Combine(logsDir, $"botrunner_{accountName ?? "unknown"}.diag.log");

            _autoReleaseCorpseTaskEnabled = !GetEnvironmentFlag("WWOW_DISABLE_AUTORELEASE_CORPSE_TASK");
            _autoRetrieveCorpseTaskEnabled = !GetEnvironmentFlag("WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK");

            // Initialize extracted components
            _diagnosticsRecorder = new DiagnosticsRecorder(objectManager, diagnosticPacketTraceRecorder);
            _diagnosticsRecorder.AccountNameAccessor = () => _activitySnapshot?.AccountName ?? "unknown";

            _interactionSequences = new InteractionSequenceBuilder(objectManager, agentFactoryAccessor, EnqueueDiagnosticMessage);

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
                    EnsureActivitySnapshot();
                    var inMapTransition = _objectManager.HasEnteredWorld && _objectManager.IsInMapTransition;

                    PopulateSnapshotFromObjectManager();
                    _diagnosticsRecorder.CaptureTransformFrame(_botTasks, Interlocked.Read(ref _tickCount), _activitySnapshot);

                    // DIAG: log MapId in snapshot for BG transfer debugging
                    var snapMapId = _activitySnapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                    if (snapMapId == 489 && _tickCount % 10 == 0)
                        Log.Information("[BG-DIAG] Snapshot contains MapId=489 at tick {Tick}", _tickCount);

                    SyncLoadoutStatusIntoSnapshot();

                    var currentSignature = ComputeSnapshotSignature(_activitySnapshot);
                    var nowUtc = DateTime.UtcNow;
                    var shouldSendFull =
                        !currentSignature.Equals(_lastSentSignature)
                        || nowUtc - _lastFullSnapshotSentAt >= FullSnapshotHeartbeatInterval;

                    WoWActivitySnapshot payload;
                    if (shouldSendFull)
                    {
                        _activitySnapshot.IsHeartbeatOnly = false;
                        payload = _activitySnapshot;
                    }
                    else
                    {
                        payload = new WoWActivitySnapshot
                        {
                            AccountName = _activitySnapshot.AccountName ?? _initialAccountName,
                            CharacterName = _activitySnapshot.CharacterName ?? string.Empty,
                            IsHeartbeatOnly = true,
                            ScreenState = _activitySnapshot.ScreenState ?? string.Empty,
                            ConnectionState = _activitySnapshot.ConnectionState,
                            IsObjectManagerValid = _activitySnapshot.IsObjectManagerValid,
                            IsMapTransition = _activitySnapshot.IsMapTransition,
                        };
                    }

                    Communication.WoWActivitySnapshot? incomingActivityMemberState = null;
                    var stateSendStopwatch = Stopwatch.StartNew();
                    try
                    {
                        incomingActivityMemberState = await _characterStateUpdateClient
                            .SendMemberStateUpdateAsync(payload, cancellationToken);
                        stateSendStopwatch.Stop();

                        if (shouldSendFull)
                        {
                            _lastSentSignature = currentSignature;
                            _lastFullSnapshotSentAt = nowUtc;
                        }

                        if (_consecutiveStateUpdateFailures > 0)
                        {
                            DiagLog($"[STATE-SEND] recovered after {_consecutiveStateUpdateFailures} failure(s)");
                            _consecutiveStateUpdateFailures = 0;
                        }

                        if (stateSendStopwatch.ElapsedMilliseconds >= 1000)
                        {
                            DiagLog(
                                $"[STATE-SEND] slow response {stateSendStopwatch.ElapsedMilliseconds}ms " +
                                $"account={_activitySnapshot?.AccountName ?? _initialAccountName} " +
                                $"screen={_activitySnapshot?.ScreenState ?? "?"}");
                        }
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
                        stateSendStopwatch.Stop();
                        var failures = ++_consecutiveStateUpdateFailures;
                        DiagLog(
                            $"[STATE-SEND] failed #{failures} type={ex.GetType().Name} elapsed={stateSendStopwatch.ElapsedMilliseconds}ms " +
                            $"account={_activitySnapshot?.AccountName ?? _initialAccountName} message={ex.Message}");
                    }

                    if (incomingActivityMemberState == null)
                    {
                        var nullResponses = ++_consecutiveNullStateResponses;
                        if (nullResponses == 1 || nullResponses % 10 == 0)
                        {
                            DiagLog(
                                $"[STATE-SEND] null response #{nullResponses}; preserving local snapshot " +
                                $"account={_activitySnapshot?.AccountName ?? _initialAccountName}");
                        }
                    }
                    else if (_consecutiveNullStateResponses > 0)
                    {
                        DiagLog($"[STATE-SEND] non-null response restored after {_consecutiveNullStateResponses} null response(s)");
                        _consecutiveNullStateResponses = 0;
                    }

                    if (inMapTransition)
                    {
                        var skipped = ++_consecutiveTransitionSkips;
                        if (skipped == 1 || skipped % 10 == 0)
                        {
                            var player = _objectManager.Player;
                            var pos = player?.Position;
                            DiagLog(
                                $"[TRANSITION-SKIP] count={skipped} enteredWorld={_objectManager.HasEnteredWorld} " +
                                $"inTransition={_objectManager.IsInMapTransition} " +
                                $"player={player != null} pos=({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F1})");
                        }

                        if (incomingActivityMemberState != null)
                        {
                            if (shouldSendFull)
                                _activitySnapshot = incomingActivityMemberState;
                            else
                                ApplyServerResponseFields(_activitySnapshot, incomingActivityMemberState);
                        }

                        var jitter = 500 + Random.Shared.Next(0, 1000);
                        await Task.Delay(jitter, cancellationToken);
                        continue;
                    }

                    if (_consecutiveTransitionSkips > 0)
                    {
                        DiagLog($"[TRANSITION-SKIP] resumed after {_consecutiveTransitionSkips} skipped loops");
                        _consecutiveTransitionSkips = 0;
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
                        var position = _objectManager.Player?.Position;
                        DiagLog(
                            $"[TICK#{_tickCount}] ready={playerWorldReady} action={actionType} tree={_behaviorTreeStatus} " +
                            $"tasks={_botTasks.Count}({taskTop}) screen={screenState} map={mapId} char={_activitySnapshot?.CharacterName ?? "?"} " +
                            $"snapIndoors={_activitySnapshot?.IsIndoors} physIndoors={_objectManager.PhysicsIsIndoors} " +
                            $"env=0x{(uint)_objectManager.PhysicsEnvironmentFlags:X} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");
                    }

                    UpdateBehaviorTree(incomingActivityMemberState);

                    if (_behaviorTree != null)
                    {
                        var prevStatus = _behaviorTreeStatus;
                        var behaviorTickStopwatch = Stopwatch.StartNew();
                        _behaviorTreeStatus = _behaviorTree.Tick(new TimeData(0.1f));
                        behaviorTickStopwatch.Stop();

                        if (behaviorTickStopwatch.ElapsedMilliseconds >= 1000)
                        {
                            DiagLog(
                                $"[TREE-TICK] slow tick {behaviorTickStopwatch.ElapsedMilliseconds}ms " +
                                $"status={_behaviorTreeStatus} prevStatus={prevStatus} corr={_currentActionCorrelationId}");
                        }

                        if (prevStatus == BehaviourTreeStatus.Running && _behaviorTreeStatus != BehaviourTreeStatus.Running
                            && !string.IsNullOrEmpty(_currentActionCorrelationId))
                        {
                            Log.Information($"[BOT RUNNER] Behavior tree completed with {_behaviorTreeStatus} [{_currentActionCorrelationId}]");
                            CompleteTrackedCommandAck(
                                ref _activeBehaviorTreeAck,
                                _behaviorTreeStatus == BehaviourTreeStatus.Success
                                    ? CommandAckEvent.Types.AckStatus.Success
                                    : CommandAckEvent.Types.AckStatus.Failed,
                                _behaviorTreeStatus == BehaviourTreeStatus.Success
                                    ? string.Empty
                                    : "behavior_tree_failed");
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

                    if (incomingActivityMemberState != null)
                    {
                        if (shouldSendFull)
                            _activitySnapshot = incomingActivityMemberState;
                        else
                            ApplyServerResponseFields(_activitySnapshot, incomingActivityMemberState);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[BOT RUNNER] {ex}");
                    DiagLog($"[LOOP-ERROR] {ex.GetType().Name}: {ex.Message}");
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        private static SnapshotChangeSignature ComputeSnapshotSignature(WoWActivitySnapshot snapshot)
        {
            if (snapshot == null)
                return default;

            var unit = snapshot.Player?.Unit;
            var pos = unit?.GameObject?.Base?.Position;
            var positionBucketX = pos != null ? (int)MathF.Floor(pos.X / 5f) : 0;
            var positionBucketY = pos != null ? (int)MathF.Floor(pos.Y / 5f) : 0;

            var healthBucket = 0;
            var isDead = false;
            if (unit != null)
            {
                var max = unit.MaxHealth;
                var current = unit.Health;
                if (max > 0)
                    healthBucket = (int)((current * 10L) / max);
                isDead = current == 0 && max > 0;
            }

            return new SnapshotChangeSignature(
                snapshot.ScreenState ?? string.Empty,
                (int)snapshot.ConnectionState,
                snapshot.CurrentMapId,
                snapshot.IsObjectManagerValid,
                snapshot.IsMapTransition,
                snapshot.IsIndoors,
                snapshot.PartyLeaderGuid,
                positionBucketX,
                positionBucketY,
                healthBucket,
                isDead,
                (int)snapshot.LoadoutStatus,
                snapshot.RecentCommandAcks.Count);
        }

        private void EnqueueCommandAckEvent(
            string? correlationId,
            Communication.ActionType actionType,
            CommandAckEvent.Types.AckStatus status,
            string? failureReason = null,
            uint relatedId = 0)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
                return;

            lock (_recentCommandAckEvents)
            {
                _recentCommandAckEvents.Enqueue(new CommandAckEvent
                {
                    CorrelationId = correlationId,
                    ActionType = actionType,
                    Status = status,
                    FailureReason = failureReason ?? string.Empty,
                    RelatedId = relatedId,
                });
            }
        }

        private void EnqueueCommandAckEvent(CommandAckEvent ackEvent)
        {
            ArgumentNullException.ThrowIfNull(ackEvent);
            EnqueueCommandAckEvent(
                ackEvent.CorrelationId,
                ackEvent.ActionType,
                ackEvent.Status,
                ackEvent.FailureReason,
                ackEvent.RelatedId);
        }

        private void FlushCommandAckEvents()
        {
            lock (_recentCommandAckEvents)
            {
                while (_recentCommandAckEvents.Count > 0)
                    _activitySnapshot.RecentCommandAcks.Add(_recentCommandAckEvents.Dequeue());
            }

            if (_activitySnapshot.RecentCommandAcks.Count > MaxRecentCommandAcks)
            {
                var kept = _activitySnapshot.RecentCommandAcks
                    .Skip(_activitySnapshot.RecentCommandAcks.Count - MaxRecentCommandAcks)
                    .ToList();
                _activitySnapshot.RecentCommandAcks.Clear();
                _activitySnapshot.RecentCommandAcks.Add(kept);
            }
        }

        private PendingCommandAckState BeginTrackedCommandAck(ActionMessage action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var correlationId = string.IsNullOrWhiteSpace(action.CorrelationId)
                ? $"act-{Interlocked.Increment(ref _actionSequenceNumber)}"
                : action.CorrelationId;
            var relatedId = GetRelatedId(action);
            var tracked = new PendingCommandAckState(correlationId, action.ActionType, relatedId);
            var currentAction = action.Clone();
            currentAction.CorrelationId = correlationId;

            _currentActionCorrelationId = correlationId;
            _activitySnapshot.CurrentAction = currentAction;
            EnqueueCommandAckEvent(correlationId, action.ActionType, CommandAckEvent.Types.AckStatus.Pending, relatedId: relatedId);

            return tracked;
        }

        private void CompleteTrackedCommandAck(
            ref PendingCommandAckState? trackedAck,
            CommandAckEvent.Types.AckStatus status,
            string? failureReason = null)
        {
            if (trackedAck is not PendingCommandAckState ack)
                return;

            EnqueueCommandAckEvent(ack.CorrelationId, ack.ActionType, status, failureReason, ack.RelatedId);
            trackedAck = null;
        }

        private static uint GetRelatedId(ActionMessage action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (action.ActionType == Communication.ActionType.SendChat
                && action.Parameters.Count > 0
                && action.Parameters[0].ParameterCase == RequestParameter.ParameterOneofCase.StringParam)
            {
                return ParseChatRelatedId(action.Parameters[0].StringParam);
            }

            if (action.Parameters.Count == 0)
                return 0;

            var first = action.Parameters[0];
            return first.ParameterCase switch
            {
                RequestParameter.ParameterOneofCase.IntParam when first.IntParam > 0 => (uint)first.IntParam,
                RequestParameter.ParameterOneofCase.LongParam when first.LongParam > 0 && first.LongParam <= uint.MaxValue => (uint)first.LongParam,
                _ => 0,
            };
        }

        private static uint ParseChatRelatedId(string? command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return 0;

            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                return 0;

            return parts[0].ToLowerInvariant() switch
            {
                ".learn" when uint.TryParse(parts[1], out var spellId) => spellId,
                ".additem" when uint.TryParse(parts[1], out var itemId) => itemId,
                ".additemset" when uint.TryParse(parts[1], out var itemSetId) => itemSetId,
                ".setskill" when uint.TryParse(parts[1], out var skillId) => skillId,
                ".levelup" when uint.TryParse(parts[1], out var levelDelta) => levelDelta,
                _ => 0,
            };
        }

        private static void ApplyServerResponseFields(WoWActivitySnapshot target, WoWActivitySnapshot source)
        {
            target.CurrentAction = source.CurrentAction;
            target.DesiredPartyLeaderName = source.DesiredPartyLeaderName ?? string.Empty;
            target.DesiredPartyIsRaid = source.DesiredPartyIsRaid;
            target.DesiredPartyMembers.Clear();
            foreach (var member in source.DesiredPartyMembers)
                target.DesiredPartyMembers.Add(member);
        }

        /// <summary>
        /// P3.3: pull the top <see cref="Tasks.LoadoutTask"/>'s progress (if any)
        /// into <see cref="_lastLoadoutStatus"/>/<see cref="_lastLoadoutFailureReason"/>
        /// and apply those to the outbound snapshot. Runs before
        /// <see cref="ComputeSnapshotSignature"/> so a LoadoutStatus transition
        /// forces a full snapshot send (LoadoutStatus is part of the signature).
        /// Caching the last-seen status means the snapshot keeps reporting
        /// LoadoutReady/LoadoutFailed even after the task pops off the stack.
        /// </summary>
        internal void SyncLoadoutStatusIntoSnapshot()
        {
            if (_botTasks.Count > 0 && _botTasks.Peek() is Tasks.LoadoutTask loadoutTask)
            {
                _lastLoadoutStatus = loadoutTask.Status;
                _lastLoadoutFailureReason = loadoutTask.FailureReason ?? string.Empty;
            }

            if (_activitySnapshot != null)
            {
                _activitySnapshot.LoadoutStatus = _lastLoadoutStatus;
                _activitySnapshot.LoadoutFailureReason = _lastLoadoutFailureReason;
            }

            if (_lastLoadoutStatus == Communication.LoadoutStatus.LoadoutReady)
            {
                CompleteTrackedCommandAck(ref _activeLoadoutAck, CommandAckEvent.Types.AckStatus.Success);
            }
            else if (_lastLoadoutStatus == Communication.LoadoutStatus.LoadoutFailed)
            {
                CompleteTrackedCommandAck(
                    ref _activeLoadoutAck,
                    CommandAckEvent.Types.AckStatus.Failed,
                    _lastLoadoutFailureReason);
            }
        }

        /// <summary>
        /// P3.3: handle an incoming <see cref="Communication.ActionType.ApplyLoadout"/>
        /// action by pushing a single <see cref="Tasks.LoadoutTask"/> onto the
        /// bot-task stack. Action dispatch skips the normal behavior-tree path
        /// because ApplyLoadout has no CharacterAction mapping — the whole
        /// execution lives in <see cref="Tasks.LoadoutTask"/> so BotRunner owns
        /// pacing and idempotency.
        /// </summary>
        internal void HandleApplyLoadoutAction(Communication.ActionMessage action)
        {
            if (_botTasks.Count > 0 && _botTasks.Peek() is Tasks.LoadoutTask)
            {
                Log.Information(
                    "[BOT RUNNER] ApplyLoadout received but a LoadoutTask is already active; ignoring duplicate [{Corr}]",
                    _currentActionCorrelationId);
                _activitySnapshot.PreviousAction = action;
                return;
            }

            var spec = action.LoadoutSpec ?? new Communication.LoadoutSpec();
            var loadoutCorrelationId = string.IsNullOrWhiteSpace(action.CorrelationId)
                ? _currentActionCorrelationId
                : action.CorrelationId;
            var context = new BotRunnerContext(
                _objectManager,
                _botTasks,
                _container,
                _behaviorConfig,
                EnqueueDiagnosticMessage);

            _botTasks.Push(new Tasks.LoadoutTask(
                context,
                spec,
                EnqueueCommandAckEvent,
                stepIndex => $"{loadoutCorrelationId}/step-{stepIndex + 1}"));
            _activitySnapshot.PreviousAction = action;

            Log.Information(
                "[BOT RUNNER] ApplyLoadout pushed LoadoutTask (targetLevel={TargetLevel} spells={SpellCount} equip={EquipCount} supplemental={SupplementalCount}) [{Corr}]",
                spec.TargetLevel,
                spec.SpellIdsToLearn.Count,
                spec.EquipItems.Count,
                spec.SupplementalItemIds.Count,
                _currentActionCorrelationId);
        }

        private void EnsureActivitySnapshot()
        {
            if (_activitySnapshot != null)
            {
                if (string.IsNullOrWhiteSpace(_activitySnapshot.AccountName))
                    _activitySnapshot.AccountName = _initialAccountName;

                return;
            }

            _activitySnapshot = new WoWActivitySnapshot
            {
                AccountName = _initialAccountName
            };

            DiagLog($"[STATE-SEND] restored missing activity snapshot for {_initialAccountName}");
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

                _currentActionCorrelationId = string.IsNullOrWhiteSpace(action.CorrelationId)
                    ? $"act-{Interlocked.Increment(ref _actionSequenceNumber)}"
                    : action.CorrelationId;
                Log.Information($"[BOT RUNNER] Received action from StateManager: {action.ActionType} ({(int)action.ActionType}) [{_currentActionCorrelationId}]");

                if (_diagnosticsRecorder.HandleDiagnosticAction(action))
                {
                    _activeBehaviorTreeAck = BeginTrackedCommandAck(action);
                    CompleteTrackedCommandAck(ref _activeBehaviorTreeAck, CommandAckEvent.Types.AckStatus.Success);
                    return;
                }

                if (action.ActionType == Communication.ActionType.ApplyLoadout)
                {
                    if (_botTasks.Count > 0 && _botTasks.Peek() is Tasks.LoadoutTask)
                    {
                        PendingCommandAckState? duplicateLoadoutAck = BeginTrackedCommandAck(action);
                        _activitySnapshot.PreviousAction = action;
                        Log.Information(
                            "[BOT RUNNER] ApplyLoadout received but a LoadoutTask is already active; ignoring duplicate [{Corr}]",
                            _currentActionCorrelationId);
                        CompleteTrackedCommandAck(
                            ref duplicateLoadoutAck,
                            CommandAckEvent.Types.AckStatus.Failed,
                            "loadout_task_already_active");
                        return;
                    }

                    _activeLoadoutAck = BeginTrackedCommandAck(action);
                    HandleApplyLoadoutAction(action);
                    return;
                }

                var actionList = ConvertActionMessageToCharacterActions(action);
                if (actionList.Count > 0)
                {
                    _activeBehaviorTreeAck = BeginTrackedCommandAck(action);

                    if (action.ActionType == Communication.ActionType.CastSpell
                        || action.ActionType == Communication.ActionType.GatherNode)
                    {
                        _spellCastLockoutUntil = DateTime.UtcNow.AddSeconds(SpellCastLockoutSeconds);
                    }

                    Log.Information($"[BOT RUNNER] Building behavior tree for: {actionList[0].Item1} [{_currentActionCorrelationId}]");
                    _behaviorTree = BuildBehaviorTreeFromActions(actionList);
                    _behaviorTreeStatus = BehaviourTreeStatus.Running;
                    _activitySnapshot.PreviousAction = action;
                    return;
                }

                EnqueueCommandAckEvent(
                    _currentActionCorrelationId,
                    action.ActionType,
                    CommandAckEvent.Types.AckStatus.Failed,
                    "unsupported_action",
                    GetRelatedId(action));
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

                if (TryBuildDesiredPartyBehaviorTree(incomingActivityMemberState))
                {
                    return;
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

                var attemptOffset = ResolveCharacterNameAttemptOffset();

                _behaviorTree = _interactionSequences.BuildCreateCharacterSequence(
                    [
                        WoWNameGenerator.GenerateName(
                            race,
                            gender,
                            BuildCharacterUniquenessSeed(_activitySnapshot.AccountName, createAttempts, attemptOffset)),
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
            => BuildCharacterUniquenessSeed(accountName, createAttempts, attemptOffset: 0);

        internal static string? BuildCharacterUniquenessSeed(string? accountName, int createAttempts, int attemptOffset)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return accountName;

            var effectiveAttempts = Math.Max(0, createAttempts) + Math.Max(0, attemptOffset);
            return effectiveAttempts <= 0
                ? accountName
                : $"{accountName}:{effectiveAttempts}";
        }

        internal static int ResolveCharacterNameAttemptOffset()
        {
            var rawValue = Environment.GetEnvironmentVariable("WWOW_CHARACTER_NAME_ATTEMPT_OFFSET");
            return int.TryParse(rawValue, out var parsedOffset) && parsedOffset >= 0
                ? parsedOffset
                : 0;
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
            var canReleaseSpirit = CanReleaseSpirit(player);
            var isDeadOrGhost = isGhost || canReleaseSpirit;
            if (!isDeadOrGhost)
                return;

            if (_botTasks.Count > 0 &&
                (_botTasks.Peek() is Tasks.ReleaseCorpseTask || _botTasks.Peek() is Tasks.RetrieveCorpseTask))
                return;

            if ((DateTime.UtcNow - _lastDeathRecoveryPush).TotalSeconds < 5)
                return;

            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig, EnqueueDiagnosticMessage);

            if (canReleaseSpirit)
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
        private static bool CanReleaseSpirit(IWoWLocalPlayer player) => DeathStateDetection.CanReleaseSpirit(player);
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

            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig, EnqueueDiagnosticMessage);

            try
            {
                var @class = WoWNameGenerator.ResolveClass(accountName);

                _botTasks.Push(new Tasks.IdleTask(context));
                Log.Information("[BOT RUNNER] Initialized idle task sequence for {Account} using {Profile} ({Class})",
                    accountName, _container.ClassContainer.Name, @class);

                // Activity dispatch: if this character was assigned an activity by
                // StateManager (via WWOW_ASSIGNED_ACTIVITY), push the resolved
                // root task on top of the idle task so it runs first and drops
                // back to idle when it pops. The useGmCommands flag lets the
                // task short-circuit outfit/travel via GM chat commands.
                var activityTask = ActivityResolver.Resolve(context, _assignedActivity, _useGmCommands);
                if (activityTask != null)
                {
                    _botTasks.Push(activityTask);
                    EnqueueDiagnosticMessage(
                        $"[ACTIVITY] Assigned '{_assignedActivity}' useGm={_useGmCommands}");
                    Log.Information(
                        "[BOT RUNNER] Activity '{Descriptor}' assigned for {Account} (useGmCommands={UseGm})",
                        _assignedActivity, accountName, _useGmCommands);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BOT RUNNER] Failed to initialize task sequence: {ex.Message}");
            }
        }
    }
}
