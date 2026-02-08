using BotCommLayer;
using Communication;

namespace WoWStateManager.Clients
{
    public class ActivityMemberUpdateClient(string ipAddress, int port, ILogger logger) : ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(ipAddress, port, logger)
    {
        public WoWActivitySnapshot SendMemberStateUpdate(WoWActivitySnapshot update) => SendMessage(update);
    }
}
