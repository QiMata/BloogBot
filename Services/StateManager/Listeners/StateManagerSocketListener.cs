using BotCommLayer;
using Communication;
using System;

namespace StateManager.Listeners
{
    public class StateManagerSocketListener(string ipAddress, int port, ILogger<StateManagerSocketListener> logger) : ProtobufAsyncSocketServer<StateChangeResponse>(ipAddress, port, logger)
    {
        // Expose as IObservable rather than Subject to avoid direct Subject usage
        public IObservable<AsyncRequest> DataMessageStream => _instanceObservable;
    }
}
