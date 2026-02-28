using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly bool _autoReleaseCorpseTaskEnabled;
        private readonly bool _autoRetrieveCorpseTaskEnabled;

        private WoWActivitySnapshot _activitySnapshot;
        private int _lastLoggedContainedItems = -1;
        private int _lastLoggedItemObjects = -1;

        // Message buffers â€” collected from IWoWEventHandler events, flushed to snapshot each tick
        private readonly Queue<string> _recentChatMessages = new();
        private readonly Queue<string> _recentErrors = new();
        private const int MaxBufferedMessages = 50;

        // File-based diagnostic logger (works in injected FG process where Serilog isn't configured)
        // Uses PID in filename so BG/FG processes don't clobber each other's logs
        private static readonly string _diagPath;
        private static readonly object _diagLock = new();
        static BotRunnerService()
        {
            try
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var procName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot");
                Directory.CreateDirectory(dir);
                _diagPath = Path.Combine(dir, $"botrunner_diag_{procName}_{pid}.log");
                File.WriteAllText(_diagPath, $"=== BotRunnerService diag started {DateTime.Now:yyyy-MM-dd HH:mm:ss} proc={procName} pid={pid} ===\n");
            }
            catch { _diagPath = ""; }
        }
        private static void DiagLog(string msg)
        {
            if (string.IsNullOrEmpty(_diagPath)) return;
            try { lock (_diagLock) { File.AppendAllText(_diagPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } } catch { }
        }

        private Task? _asyncBotTaskRunnerTask;
        private CancellationTokenSource? _cts;

        private IBehaviourTreeNode? _behaviorTree;
        private BehaviourTreeStatus _behaviorTreeStatus = BehaviourTreeStatus.Success;

        // Spell-cast lockout: prevents movement actions from interrupting active spell casts.
        // Channeled spells like fishing need time to complete without being overridden by GoTo.
        private DateTime _spellCastLockoutUntil = DateTime.MinValue;
        private const double SpellCastLockoutSeconds = 20.0;

        // Action dispatch correlation: stable token linking receive → dispatch → completion logs.
        private string _currentActionCorrelationId = "";
        private long _actionSequenceNumber;

        private readonly Stack<IBotTask> _botTasks = new();
        private bool _tasksInitialized;
        private DateTime _lastDeathRecoveryPush = DateTime.MinValue;
        private DateTime _lastReleaseSpiritCommandUtc = DateTime.MinValue;
        private static readonly TimeSpan ReleaseSpiritCommandCooldown = TimeSpan.FromSeconds(2);
        private Position? _lastKnownAlivePosition;
        private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
        private const uint StandStateMask = 0xFF;
        private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

        public BotRunnerService(IObjectManager objectManager,
                                 CharacterStateUpdateClient characterStateUpdateClient,
                                 IDependencyContainer container,
                                 Func<IAgentFactory?>? agentFactoryAccessor = null,
                                 string? accountName = null,
                                 ITalentService? talentService = null,
                                 IEquipmentService? equipmentService = null,
                                 Constants.BotBehaviorConfig? behaviorConfig = null)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            _activitySnapshot = new() { AccountName = accountName ?? "?" };
            _agentFactoryAccessor = agentFactoryAccessor;

            _characterStateUpdateClient = characterStateUpdateClient ?? throw new ArgumentNullException(nameof(characterStateUpdateClient));
            _talentService = talentService;
            _equipmentService = equipmentService;
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _behaviorConfig = behaviorConfig ?? new Constants.BotBehaviorConfig();
            _autoReleaseCorpseTaskEnabled = !GetEnvironmentFlag("WWOW_DISABLE_AUTORELEASE_CORPSE_TASK");
            _autoRetrieveCorpseTaskEnabled = !GetEnvironmentFlag("WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK");

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
                    PopulateSnapshotFromObjectManager();

                    var incomingActivityMemberState = _characterStateUpdateClient.SendMemberStateUpdate(_activitySnapshot);

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

                    // Death recovery must continue even if a behavior tree is currently running.
                    // Some chat/action trees can stay Running while dead, which otherwise starves
                    // ReleaseCorpse/RetrieveCorpse and leaves the character ghost-stalled.
                    if (_objectManager.HasEnteredWorld)
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

        private void UpdateBehaviorTree(WoWActivitySnapshot incomingActivityMemberState)
        {
            // Check for new incoming actions FIRST â€” they can interrupt a running tree
            if (_objectManager.HasEnteredWorld
                && incomingActivityMemberState.CurrentAction != null
                && incomingActivityMemberState.CurrentAction.ActionType != Communication.ActionType.Wait)
            {
                var action = incomingActivityMemberState.CurrentAction;

                // Spell-cast lockout: don't let movement actions interrupt active spell casts.
                // Channeled spells (fishing, etc.) need time to complete.
                if (action.ActionType == Communication.ActionType.Goto
                    && DateTime.UtcNow < _spellCastLockoutUntil)
                {
                    return;
                }

                _currentActionCorrelationId = $"act-{Interlocked.Increment(ref _actionSequenceNumber)}";
                Log.Information($"[BOT RUNNER] Received action from StateManager: {action.ActionType} ({(int)action.ActionType}) [{_currentActionCorrelationId}]");
                var actionList = ConvertActionMessageToCharacterActions(action);
                if (actionList.Count > 0)
                {
                    // Set lockout when casting a spell
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
            }

            if (_behaviorTree != null && _behaviorTreeStatus == BehaviourTreeStatus.Running)
            {
                return;
            }

            // Already in world â€” skip all login/charselect checks and go straight to InWorld handling.
            // Without this guard, ICharacterSelectScreen implementations that return HasReceivedCharacterList=false
            // when InWorld (e.g., FG's FgCharacterSelectScreen) cause an early return before InitializeTaskSequence.
            if (_objectManager.HasEnteredWorld)
            {
                if (!_tasksInitialized)
                {
                    _tasksInitialized = true;
                    InitializeTaskSequence();
                }

                _behaviorTree = null;
                return;
            }

            if (_objectManager.LoginScreen?.IsLoggedIn != true)
            {
                _behaviorTree = BuildLoginSequence(incomingActivityMemberState.AccountName, "PASSWORD");
                return;
            }

            if (_objectManager.RealmSelectScreen?.CurrentRealm == null)
            {
                _behaviorTree = BuildRealmSelectionSequence();
                return;
            }

            if (_objectManager.CharacterSelectScreen?.HasReceivedCharacterList != true)
            {
                if (_objectManager.CharacterSelectScreen?.HasRequestedCharacterList != true)
                {
                    _behaviorTree = BuildRequestCharacterSequence();
                }

                return;
            }

            if (_objectManager.CharacterSelectScreen?.CharacterSelects.Count == 0)
            {
                Class @class = WoWNameGenerator.ParseClassCode(_activitySnapshot.AccountName.Substring(2, 2));
                Race race = WoWNameGenerator.ParseRaceCode(_activitySnapshot.AccountName[..2]);
                Gender gender = WoWNameGenerator.DetermineGender(@class);

                _behaviorTree = BuildCreateCharacterSequence(
                    [
                        WoWNameGenerator.GenerateName(race, gender),
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

            // Not yet in world â€” build EnterWorld sequence
            _behaviorTree = BuildEnterWorldSequence(_objectManager.CharacterSelectScreen?.CharacterSelects[0].Guid ?? 0);
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

            // Already have a death recovery task on the stack
            if (_botTasks.Count > 0 &&
                (_botTasks.Peek() is Tasks.ReleaseCorpseTask || _botTasks.Peek() is Tasks.RetrieveCorpseTask))
                return;

            // Cooldown: wait 5s between pushes to avoid spamming while server processes the request
            if ((DateTime.UtcNow - _lastDeathRecoveryPush).TotalSeconds < 5)
                return;

            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig);

            // Dead but not ghost - release spirit first
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

            // Ghost form â€” navigate to corpse and resurrect
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

        private static bool IsZeroPosition(Position? pos)
        {
            if (pos == null)
                return true;

            return Math.Abs(pos.X) < 0.001f
                && Math.Abs(pos.Y) < 0.001f
                && Math.Abs(pos.Z) < 0.001f;
        }

        private static bool HasGhostFlag(IWoWLocalPlayer player)
        {
            try { return (((uint)player.PlayerFlags) & PlayerFlagGhost) != 0; }
            catch { return false; }
        }

        private static bool IsStandStateDead(IWoWLocalPlayer player)
        {
            try
            {
                var bytes1 = player.Bytes1;
                return bytes1 != null
                    && bytes1.Length > 0
                    && (bytes1[0] & StandStateMask) == StandStateDead;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsGhostState(IWoWLocalPlayer player)
        {
            if (HasGhostFlag(player))
                return true;

            try { return player.InGhostForm; }
            catch { return false; }
        }

        private static bool IsCorpseState(IWoWLocalPlayer player)
            => !IsGhostState(player) && (player.Health == 0 || IsStandStateDead(player));

        private static bool IsDeadOrGhostState(IWoWLocalPlayer player)
            => IsGhostState(player) || IsCorpseState(player);

        private void UpdateLastKnownAlivePosition(IWoWLocalPlayer player)
        {
            if (IsDeadOrGhostState(player))
                return;

            var pos = player.Position;
            if (IsZeroPosition(pos))
                return;

            _lastKnownAlivePosition = new Position(pos!.X, pos.Y, pos.Z);
            DiagLog($"DeathRecovery: update last alive pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");
        }

        private void InitializeTaskSequence()
        {
            var accountName = _activitySnapshot.AccountName;
            if (string.IsNullOrEmpty(accountName) || accountName == "?" || accountName.Length < 4)
            {
                Log.Information("[BOT RUNNER] No valid account name for task initialization, using wait.");
                return;
            }

            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig);

            try
            {
                var classCode = accountName.Substring(2, 2);
                var @class = WoWNameGenerator.ParseClassCode(classCode);

                // IdleTask sits at the bottom of the stack â€” does nothing.
                // StateManager sends actions via IPC that build behavior trees.
                // Push tasks in reverse order (stack is LIFO)
                _botTasks.Push(new Tasks.IdleTask(context));
                _botTasks.Push(new Tasks.WaitTask(context, 3000));
                _botTasks.Push(new Tasks.TeleportTask(context, "valleyoftrials"));
                Log.Information("[BOT RUNNER] Initialized {Class} task sequence for {Account} using {Profile}",
                    @class, accountName, _container.ClassContainer.Name);
            }
            catch (Exception ex)
            {
                Log.Error($"[BOT RUNNER] Failed to initialize task sequence: {ex.Message}");
            }
        }
    }
}

