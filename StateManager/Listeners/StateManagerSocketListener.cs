using System.Reactive.Subjects;
using BotCommLayer;
using Communication;

namespace StateManager.Listeners
{
    public class StateManagerSocketListener(
        string ipAddress,
        int port,
        ILogger<StateManagerSocketListener> logger
    ) : ProtobufAsyncSocketServer<StateChangeResponse>(ipAddress, port, logger)
    {
        public Subject<AsyncRequest> DataMessageSubject => _instanceObservable;
    }
}
