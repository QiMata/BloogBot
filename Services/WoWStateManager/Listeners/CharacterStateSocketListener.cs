using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WoWStateManager.Clients;
using WoWStateManager.Coordination;
using WoWStateManager.Progression;
using WoWStateManager.Settings;

namespace WoWStateManager.Listeners
{
    public class CharacterStateSocketListener : ProtobufPipelineSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        /// <summary>
        /// Thread-safe snapshot storage. Written by port 5002 (bot polls), read by port 8088 (test queries).
        /// Must be ConcurrentDictionary because these ports run on separate threads.
        /// </summary>
        public ConcurrentDictionary<string, WoWActivitySnapshot> CurrentActivityMemberList { get; } = new();

        /// <summary>
        /// Maximum number of pending actions per account. Prevents unbounded growth if a bot stops polling.
        /// </summary>
        private const int MaxPendingActionsPerAccount = 50;

        /// <summary>
        /// Maximum age for a pending action before it's considered stale and dropped.
        /// </summary>
        private static readonly TimeSpan PendingActionTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Pending actions queued by external callers (e.g. test fixtures via port 8088).
        /// Stored per-account and consumed in FIFO order on subsequent bot polls.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TimestampedAction>> _pendingActions = new();

        /// <summary>
        /// After delivering a forwarded action, suppress CombatCoordinator for that account
        /// until this time. Prevents the coordinator from overwriting multi-second actions
        /// (e.g. GatherNode mining channel) on the very next poll cycle.
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> _coordinatorSuppressedUntil = new();
        private readonly int _coordinatorSuppressionSeconds;

        private readonly List<CharacterSettings> _characterSettings;
        private readonly MangosSOAPClient? _soapClient;
        private readonly ProgressionPlanner _progressionPlanner;
        private CombatCoordinator? _combatCoordinator;
        private DungeoneeringCoordinator? _dungeoneeringCoordinator;

        /// <summary>
        /// Checked at use-time (not construction-time) so tests can toggle it
        /// between StateManager restarts via Environment.SetEnvironmentVariable.
        /// </summary>
        private static bool IsCoordinatorDisabled =>
            Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") == "1";

        public CharacterStateSocketListener(List<CharacterSettings> characterSettings, string ipAddress, int port, MangosSOAPClient? soapClient, ProgressionPlanner progressionPlanner, ILogger<CharacterStateSocketListener> logger) : base(ipAddress, port, logger)
        {
            _characterSettings = characterSettings;
            _soapClient = soapClient;
            _progressionPlanner = progressionPlanner;
            _coordinatorSuppressionSeconds = GetCoordinatorSuppressionSeconds();
            if (IsCoordinatorDisabled)
                logger.LogInformation("COMBAT_COORD: Coordinator DISABLED via WWOW_TEST_DISABLE_COORDINATOR=1 (at startup)");
            characterSettings.ForEach(settings => CurrentActivityMemberList.TryAdd(settings.AccountName, new()));
        }

        private static int GetCoordinatorSuppressionSeconds()
        {
            var raw = Environment.GetEnvironmentVariable("WWOW_TEST_COORD_SUPPRESS_SECONDS");
            if (!int.TryParse(raw, out var seconds))
                return 15;

            return Math.Clamp(seconds, 1, 3600);
        }

        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            string accountName = request.AccountName;
            string characterName = request.CharacterName;

            string screenState = request.ScreenState;

            _logger.LogDebug($"Incoming state update for account '{accountName}', ScreenState='{screenState}'");

            // Log snapshot with screen state info (used by integration tests and monitoring)
            // Reduced to Debug to prevent stdout pipe saturation with 10+ bots polling
            if (!string.IsNullOrEmpty(screenState))
            {
                var charInfo = !string.IsNullOrEmpty(characterName) ? $", Character='{characterName}'" : "";
                var errCount = request.RecentErrors?.Count ?? 0;
                var errSuffix = errCount > 0 ? $", RecentErrors={errCount}" : "";
                _logger.LogDebug($"SNAPSHOT_RECEIVED: Account='{accountName}', ScreenState='{screenState}'{charInfo}{errSuffix}");
            }

            // Handle "?" account name - assign to Foreground account (only FG bots send "?")
            if ("?" == accountName)
            {
                _logger.LogInformation($"Processing '?' assignment. Dictionary has {CurrentActivityMemberList.Count} entries:");
                foreach (var activityKeyValue in CurrentActivityMemberList)
                {
                    var slotAccountName = activityKeyValue.Value.AccountName;
                    var isEmpty = string.IsNullOrEmpty(slotAccountName);
                    _logger.LogInformation($"  Slot '{activityKeyValue.Key}': AccountName='{slotAccountName}', isEmpty={isEmpty}");
                }

                // Only FG bots send "?" - assign them to the Foreground account from settings.
                // This must work on EVERY poll, not just the first. The FG slot may already
                // have AccountName set from a prior poll — that's fine, re-assign it.
                var fgSettings = _characterSettings.Find(cs => cs.RunnerType == Settings.BotRunnerType.Foreground);
                if (fgSettings != null && CurrentActivityMemberList.ContainsKey(fgSettings.AccountName))
                {
                    accountName = fgSettings.AccountName;
                    request.AccountName = accountName;
                }
                else
                {
                    // No Foreground setting found — fall back to first empty slot
                    foreach (var activityKeyValue in CurrentActivityMemberList)
                    {
                        if (string.IsNullOrEmpty(activityKeyValue.Value.AccountName))
                        {
                            accountName = activityKeyValue.Key;
                            request.AccountName = accountName;
                            _logger.LogInformation($"Assigned account '{accountName}' to idle slot (no FG config)");
                            break;
                        }
                    }
                }
            }

            if (!CurrentActivityMemberList.ContainsKey(accountName))
            {
                _logger.LogWarning($"Requested account '{accountName}' not found in CurrentActivityMemberList");
                return new WoWActivitySnapshot();
            }

            // Store the incoming state update from the bot
            CurrentActivityMemberList[accountName] = request;
            _logger.LogDebug($"Updated state for account '{accountName}'");

            // Build the response — start with the stored snapshot
            var response = CurrentActivityMemberList[accountName];

            // Clear any stale action from the request so only freshly injected actions are returned
            response.CurrentAction = null;

            // Coordinate combat (group formation + combat support)
            InjectCoordinatedActions(accountName, response);

            // Inject pending test/external action (overrides coordinated action if present).
            // Use FIFO so rapid multi-step setup actions (target -> chat command, etc.) are not dropped.
            if (_pendingActions.TryGetValue(accountName, out var pendingQueue))
            {
                while (pendingQueue.TryDequeue(out var timestampedAction))
                {
                    // Drop stale actions that have exceeded TTL
                    if (DateTime.UtcNow - timestampedAction.EnqueuedAt > PendingActionTtl)
                    {
                        _logger.LogWarning($"DROPPING STALE ACTION for '{accountName}': {timestampedAction.Action.ActionType} (age={DateTime.UtcNow - timestampedAction.EnqueuedAt:mm\\:ss})");
                        continue;
                    }

                    var pendingAction = timestampedAction.Action;

                    // Drop stale chat actions if the sender is dead/ghost.
                    // Action forwarding can be delayed by coordinator suppression; by delivery time the bot may have died.
                    if (pendingAction.ActionType == ActionType.SendChat && IsDeadOrGhostState(response, out var deadReason))
                    {
                        _logger.LogInformation($"DROPPING PENDING ACTION for '{accountName}': SendChat blocked while dead/ghost ({deadReason})");
                        continue;
                    }

                    response.CurrentAction = pendingAction;
                    // Suppress CombatCoordinator so multi-second forwarded actions
                    // are not overwritten on the next poll cycle.
                    _coordinatorSuppressedUntil[accountName] = DateTime.UtcNow.AddSeconds(_coordinatorSuppressionSeconds);
                    Console.WriteLine($"[ACTION-DIAG] INJECTING PENDING ACTION to '{accountName}': {pendingAction.ActionType}");
                    _logger.LogInformation($"INJECTING PENDING ACTION to '{accountName}': {pendingAction.ActionType} (coordinator suppressed {_coordinatorSuppressionSeconds}s)");
                    break;
                }

                if (pendingQueue.IsEmpty)
                    _pendingActions.TryRemove(accountName, out _);
            }

            // Log when an action is being delivered to a bot
            if (response.CurrentAction != null && response.CurrentAction.ActionType != ActionType.Wait)
            {
                _logger.LogInformation($"DELIVERING ACTION to '{accountName}': {response.CurrentAction.ActionType}");
            }

            return response;
        }

        /// <summary>
        /// Queues an action to be delivered to the specified bot on its next poll.
        /// Used by the test fixture (via port 8088) to send commands to bots.
        /// </summary>
        /// <summary>
        /// Returns true if the action was successfully enqueued, false if it was dropped (e.g. dead/ghost state).
        /// </summary>
        public bool EnqueueAction(string accountName, ActionMessage action)
        {
            if (action.ActionType == ActionType.SendChat
                && CurrentActivityMemberList.TryGetValue(accountName, out var current)
                && IsDeadOrGhostState(current, out var deadReason))
            {
                _logger.LogInformation($"DROPPING QUEUED ACTION for '{accountName}': SendChat blocked while dead/ghost ({deadReason})");
                return false;
            }

            var queue = _pendingActions.GetOrAdd(accountName, _ => new ConcurrentQueue<TimestampedAction>());

            // Enforce depth cap — drop oldest if at capacity
            while (queue.Count >= MaxPendingActionsPerAccount && queue.TryDequeue(out var dropped))
            {
                _logger.LogWarning($"DROPPING OLDEST ACTION for '{accountName}': {dropped.Action.ActionType} (queue at capacity {MaxPendingActionsPerAccount})");
            }

            queue.Enqueue(new TimestampedAction(action));
            Console.WriteLine($"[ACTION-DIAG] QUEUED ACTION for '{accountName}': {action.ActionType} (pending={queue.Count})");
            _logger.LogInformation($"QUEUED ACTION for '{accountName}': {action.ActionType} (pending={queue.Count})");
            return true;
        }

        private static bool IsDeadOrGhostState(WoWActivitySnapshot? snap, out string reason)
        {
            reason = string.Empty;

            var player = snap?.Player;
            var unit = player?.Unit;
            if (player == null || unit == null)
                return false;

            const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
            const uint standStateMask = 0xFF;
            const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

            var reasons = new List<string>();
            if (unit.Health == 0)
                reasons.Add("health=0");

            if ((player.PlayerFlags & playerFlagGhost) != 0)
                reasons.Add("ghostFlag=1");

            var standState = unit.Bytes1 & standStateMask;
            if (standState == standStateDead)
                reasons.Add("standState=dead");

            // NOTE: The former "deadTextSeen" heuristic (any RecentChatMessages/RecentErrors containing
            // "dead") was removed. It caused false positives: a "[SYSTEM] You are dead." message from a
            // prior test stayed in the 50-message rolling window and permanently blocked all subsequent
            // chat actions for the rest of the session, even after the character was revived.
            // health=0, ghostFlag, and standState=dead are real-time game-state fields and sufficient.

            if (reasons.Count == 0)
                return false;

            reason = string.Join(", ", reasons);
            return true;
        }

        /// <summary>
        /// Wraps an ActionMessage with an enqueue timestamp for TTL expiry.
        /// </summary>
        private sealed record TimestampedAction(ActionMessage Action)
        {
            public DateTime EnqueuedAt { get; } = DateTime.UtcNow;
        }

        private void InjectCoordinatedActions(string accountName, WoWActivitySnapshot response)
        {
            // Checked at use-time so tests can toggle coordinator mid-session
            if (IsCoordinatorDisabled)
                return;

            // Skip if a forwarded action was recently delivered — let it complete without interference.
            // Exception: never suppress during dungeoneering dispatch/in-progress — the leader MUST
            // receive START_DUNGEONEERING even if a pending action (e.g. DisbandGroup from fixture
            // cleanup) was recently delivered. Without this, the FG bot misses the dispatch window
            // and all bots start as follower=False with no leader.
            if (_coordinatorSuppressedUntil.TryGetValue(accountName, out var until) && DateTime.UtcNow < until)
            {
                var isDungeonDispatch = _dungeoneeringCoordinator != null
                    && (_dungeoneeringCoordinator.State == DungeoneeringCoordinator.CoordState.DispatchDungeoneering
                     || _dungeoneeringCoordinator.State == DungeoneeringCoordinator.CoordState.DungeonInProgress
                     || _dungeoneeringCoordinator.State == DungeoneeringCoordinator.CoordState.WaitForRFCSettle);
                if (!isDungeonDispatch)
                    return;
            }

            if (CurrentActivityMemberList.Count < 2)
            {
                _logger.LogDebug($"COMBAT_COORD_DEBUG: Skipping — only {CurrentActivityMemberList.Count} members");
                return;
            }

            // Use DungeoneeringCoordinator for 3+ bots, CombatCoordinator for exactly 2
            if (_characterSettings.Count > 2)
            {
                InjectDungeoneeringActions(accountName, response);
            }
            else
            {
                InjectCombatActions(accountName, response);
            }

            // Progression planning — lowest priority, only if no combat/dungeon action was injected
            if (response.CurrentAction == null)
            {
                var charSettings = _characterSettings.Find(cs => cs.AccountName == accountName);
                var buildConfig = charSettings?.BuildConfig;
                var progressionAction = _progressionPlanner.GetNextAction(response, buildConfig);
                if (progressionAction != null)
                {
                    response.CurrentAction = progressionAction;
                    _logger.LogInformation("PROGRESSION: Injecting {ActionType} for '{Account}'",
                        progressionAction.ActionType, accountName);
                }
            }
        }

        private void InjectDungeoneeringActions(string accountName, WoWActivitySnapshot response)
        {
            if (_dungeoneeringCoordinator == null)
            {
                // Leader = first bot in settings (typically TESTBOT1 / FG Warrior).
                // FG crash during map transitions is resolved (PostMessage fix + teleport stagger + SEH wrappers).
                var leaderAccount = _characterSettings.First().AccountName;
                var allAccounts = _characterSettings.Select(cs => cs.AccountName);

                _dungeoneeringCoordinator = new DungeoneeringCoordinator(leaderAccount, allAccounts, _characterSettings, _soapClient, _logger);
            }

            var action = _dungeoneeringCoordinator.GetAction(accountName, CurrentActivityMemberList);
            if (action != null)
            {
                response.CurrentAction = action;
            }
        }

        private void InjectCombatActions(string accountName, WoWActivitySnapshot response)
        {
            // Lazy-init the coordinator once we can resolve roles
            if (_combatCoordinator == null)
            {
                string? fgAccount = null, bgAccount = null;
                foreach (var cs in _characterSettings)
                {
                    _logger.LogInformation($"COMBAT_COORD_DEBUG: CharSetting '{cs.AccountName}' RunnerType={cs.RunnerType}");
                    if (cs.RunnerType == Settings.BotRunnerType.Foreground)
                        fgAccount = cs.AccountName;
                    else if (cs.RunnerType == Settings.BotRunnerType.Background)
                        bgAccount = cs.AccountName;
                }

                _logger.LogInformation($"COMBAT_COORD_DEBUG: Resolved fg='{fgAccount}' bg='{bgAccount}'");

                if (fgAccount == null || bgAccount == null)
                    return;

                _combatCoordinator = new CombatCoordinator(fgAccount, bgAccount, _logger);
                _logger.LogInformation($"COMBAT_COORD: Initialized — Foreground='{fgAccount}', Background='{bgAccount}'");
            }

            var action = _combatCoordinator.GetAction(accountName, CurrentActivityMemberList);
            if (action != null)
            {
                response.CurrentAction = action;
            }
        }
    }
}
