using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;

namespace WoWStateManager.Listeners
{
    public class CharacterStateSocketListener : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        public Dictionary<string, WoWActivitySnapshot> CurrentActivityMemberList { get; } = [];

        private readonly List<CharacterSettings> _characterSettings;
        private CombatCoordinator? _combatCoordinator;

        public CharacterStateSocketListener(List<CharacterSettings> characterSettings, string ipAddress, int port, ILogger<CharacterStateSocketListener> logger) : base(ipAddress, port, logger)
        {
            _characterSettings = characterSettings;
            characterSettings.ForEach(settings => CurrentActivityMemberList.Add(settings.AccountName, new()));
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
                _logger.LogInformation($"SNAPSHOT_RECEIVED: Account='{accountName}', ScreenState='{screenState}'{charInfo}");
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

            // Log when an action is being delivered to a bot
            if (response.CurrentAction != null && response.CurrentAction.ActionType != ActionType.Wait)
            {
                _logger.LogInformation($"DELIVERING ACTION to '{accountName}': {response.CurrentAction.ActionType}");
            }

            return response;
        }

        private void InjectCoordinatedActions(string accountName, WoWActivitySnapshot response)
        {
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
