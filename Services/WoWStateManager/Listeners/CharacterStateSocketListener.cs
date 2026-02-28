using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;

namespace WoWStateManager.Listeners
{
    public class CharacterStateSocketListener : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
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
        private CombatCoordinator? _combatCoordinator;

        public CharacterStateSocketListener(List<CharacterSettings> characterSettings, string ipAddress, int port, ILogger<CharacterStateSocketListener> logger) : base(ipAddress, port, logger)
        {
            _characterSettings = characterSettings;
            _coordinatorSuppressionSeconds = GetCoordinatorSuppressionSeconds();
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
            if (!string.IsNullOrEmpty(screenState))
            {
                var charInfo = !string.IsNullOrEmpty(characterName) ? $", Character='{characterName}'" : "";
                var errCount = request.RecentErrors?.Count ?? 0;
                var errSuffix = errCount > 0 ? $", RecentErrors={errCount}" : "";
                _logger.LogInformation($"SNAPSHOT_RECEIVED: Account='{accountName}', ScreenState='{screenState}'{charInfo}{errSuffix}");
            }

            // Handle "?" account name - assign to first available idle slot
            if ("?" == accountName)
            {
                _logger.LogInformation($"Processing '?' assignment. Dictionary has {CurrentActivityMemberList.Count} entries:");
                foreach (var activityKeyValue in CurrentActivityMemberList)
                {
                    var slotAccountName = activityKeyValue.Value.AccountName;
                    var isEmpty = string.IsNullOrEmpty(slotAccountName);
                    _logger.LogInformation($"  Slot '{activityKeyValue.Key}': AccountName='{slotAccountName}', isEmpty={isEmpty}");

                    if (isEmpty)
                    {
                        accountName = activityKeyValue.Key;
                        request.AccountName = accountName;
                        _logger.LogInformation($"Assigned account '{accountName}' to idle slot");
                        break;
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
        public void EnqueueAction(string accountName, ActionMessage action)
        {
            if (action.ActionType == ActionType.SendChat
                && CurrentActivityMemberList.TryGetValue(accountName, out var current)
                && IsDeadOrGhostState(current, out var deadReason))
            {
                _logger.LogInformation($"DROPPING QUEUED ACTION for '{accountName}': SendChat blocked while dead/ghost ({deadReason})");
                return;
            }

            var queue = _pendingActions.GetOrAdd(accountName, _ => new ConcurrentQueue<TimestampedAction>());

            // Enforce depth cap — drop oldest if at capacity
            while (queue.Count >= MaxPendingActionsPerAccount && queue.TryDequeue(out var dropped))
            {
                _logger.LogWarning($"DROPPING OLDEST ACTION for '{accountName}': {dropped.Action.ActionType} (queue at capacity {MaxPendingActionsPerAccount})");
            }

            queue.Enqueue(new TimestampedAction(action));
            _logger.LogInformation($"QUEUED ACTION for '{accountName}': {action.ActionType} (pending={queue.Count})");
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

            var deadTextSeen =
                (snap?.RecentErrors?.Any(e =>
                    e.Contains("dead", StringComparison.OrdinalIgnoreCase) ||
                    e.Contains("can't chat", StringComparison.OrdinalIgnoreCase) ||
                    e.Contains("cannot chat", StringComparison.OrdinalIgnoreCase)) ?? false)
                || (snap?.RecentChatMessages?.Any(m =>
                    m.Contains("dead", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("can't chat", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("cannot chat", StringComparison.OrdinalIgnoreCase)) ?? false);

            if (deadTextSeen)
                reasons.Add("deadTextSeen=1");

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
            // Skip if a forwarded action was recently delivered — let it complete without interference
            if (_coordinatorSuppressedUntil.TryGetValue(accountName, out var until) && DateTime.UtcNow < until)
                return;

            if (CurrentActivityMemberList.Count < 2)
            {
                _logger.LogDebug($"COMBAT_COORD_DEBUG: Skipping — only {CurrentActivityMemberList.Count} members");
                return;
            }

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
