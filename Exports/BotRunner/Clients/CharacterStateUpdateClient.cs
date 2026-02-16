using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;

namespace BotRunner.Clients
{
    public class CharacterStateUpdateClient : ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        private readonly ILogger _logger;

        // Production constructor: connects to remote listener
        public CharacterStateUpdateClient(string ipAddress, int port, ILogger logger)
            : base(ipAddress, port, logger)
        {
            _logger = logger;
        }

        // Test constructor: no socket connection, allows overriding SendMemberStateUpdate
        public CharacterStateUpdateClient(ILogger logger)
            : base()
        {
            _logger = logger;
        }

        public virtual WoWActivitySnapshot SendMemberStateUpdate(WoWActivitySnapshot update) => SendMessage(update);
    }
}
