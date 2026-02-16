using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;

namespace DecisionEngineService.Clients
{
    public class CombatModelClient(string ipAddress, ILogger logger) : ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(ipAddress, 8080, logger)
    {

    }
}
