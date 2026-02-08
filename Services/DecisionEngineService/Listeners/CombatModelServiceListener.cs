using BotCommLayer;
using Communication;

namespace DecisionEngineService.Listeners
{
    public class CombatModelServiceListener(string ipAddress, int port, ILogger logger) : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>(ipAddress, port, logger)
    {
        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            return base.HandleRequest(request);
        }
    }
}
