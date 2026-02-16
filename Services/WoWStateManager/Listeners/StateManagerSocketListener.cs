using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using System;

namespace WoWStateManager.Listeners
{
    public class StateManagerSocketListener(string ipAddress, int port, ILogger<StateManagerSocketListener> logger) : ProtobufAsyncSocketServer<StateChangeResponse>(ipAddress, port, logger)
    {
        // Expose as IObservable rather than Subject to avoid direct Subject usage
        public IObservable<AsyncRequest> DataMessageStream => _instanceObservable;
    }
}
