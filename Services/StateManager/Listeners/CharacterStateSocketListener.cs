using BotCommLayer;
using Communication;

namespace StateManager.Listeners
{
    public class CharacterStateSocketListener : ProtobufSocketServer<ActivitySnapshot, ActivitySnapshot>
    {
        public Dictionary<string, ActivitySnapshot> CurrentActivityMemberList { get; } = [];

        public CharacterStateSocketListener(List<CharacterDefinition> characterDefinitions, string ipAddress, int port, ILogger<CharacterStateSocketListener> logger) : base(ipAddress, port, logger)
        {
            characterDefinitions.ForEach(characterDefinition => CurrentActivityMemberList.Add(characterDefinition.AccountName, new()));
        }

        protected override ActivitySnapshot HandleRequest(ActivitySnapshot request)
        {
            string accountName = request.AccountName;

            _logger.LogInformation($"Incoming state request for account '{accountName}'");

            if ("?" == accountName)
            {
                foreach (var activityKeyValue in CurrentActivityMemberList)
                {
                    if (string.IsNullOrEmpty(activityKeyValue.Value.AccountName))
                    {
                        CurrentActivityMemberList[activityKeyValue.Key].AccountName = activityKeyValue.Key;
                        accountName = activityKeyValue.Value.AccountName;
                        _logger.LogInformation($"Assigned account '{accountName}' to idle slot");
                        break;
                    }
                }
            }

            if (!CurrentActivityMemberList.TryGetValue(accountName, out var snapshot))
            {
                _logger.LogWarning($"Requested account '{accountName}' not found in CurrentActivityMemberList");
                return new ActivitySnapshot();
            }

            return snapshot;
        }
    }
}
