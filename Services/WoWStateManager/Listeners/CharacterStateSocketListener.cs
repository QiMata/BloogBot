using BotCommLayer;
using Communication;
using WoWStateManager.Settings;

namespace WoWStateManager.Listeners
{
    public class CharacterStateSocketListener : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        public Dictionary<string, WoWActivitySnapshot> CurrentActivityMemberList { get; } = [];

        public CharacterStateSocketListener(List<CharacterSettings> characterSettings, string ipAddress, int port, ILogger<CharacterStateSocketListener> logger) : base(ipAddress, port, logger)
        {
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
                foreach (var activityKeyValue in CurrentActivityMemberList)
                {
                    if (string.IsNullOrEmpty(activityKeyValue.Value.AccountName))
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

            // Return the stored snapshot (which now includes the update)
            return CurrentActivityMemberList[accountName];
        }
    }
}
