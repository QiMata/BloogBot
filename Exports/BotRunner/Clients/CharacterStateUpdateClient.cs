using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;

namespace BotRunner.Clients
{
    public class CharacterStateUpdateClient : ProtobufSocketClient<ActivitySnapshot, ActivitySnapshot>
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

        public virtual ActivitySnapshot SendMemberStateUpdate(ActivitySnapshot update) => SendMessage(update);
    }
}
