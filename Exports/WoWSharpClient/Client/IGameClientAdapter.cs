using GameData.Core.Enums;

namespace WoWSharpClient.Client
{
    public interface IGameClientAdapter
    {
        void SendMovementOpcode(Opcode opcode, byte[] movementInfo);
        void SendMSGPacked(Opcode opcode, byte[] payload);
        void SendPing();
        void QueryTime();
        void SendSetActiveMover(ulong guid);
        void SendMoveWorldPortAcknowledge();
        void SendNameQuery(ulong guid);
    }

    public sealed class NetworkGameClientAdapter : IGameClientAdapter
    {
        private readonly WoWClient _client;
        public WoWClient Client => _client;
        public NetworkGameClientAdapter(WoWClient client) => _client = client;
        public void SendMovementOpcode(Opcode opcode, byte[] movementInfo) => _client.SendMovementOpcode(opcode, movementInfo);
        public void SendMSGPacked(Opcode opcode, byte[] payload) => _client.SendMSGPacked(opcode, payload);
        public void SendPing() => _client.SendPing();
        public void QueryTime() => _client.QueryTime();
        public void SendSetActiveMover(ulong guid) => _client.SendSetActiveMover(guid);
        public void SendMoveWorldPortAcknowledge() => _client.SendMoveWorldPortAcknowledge();
        public void SendNameQuery(ulong guid) => _client.SendNameQuery(guid);
        public void EnterWorld(ulong guid) => _client.EnterWorld(guid);
    }

    public sealed class InProcessGameClientAdapter : IGameClientAdapter
    {
        public void SendMovementOpcode(Opcode opcode, byte[] movementInfo) { }
        public void SendMSGPacked(Opcode opcode, byte[] payload) { }
        public void SendPing() { }
        public void QueryTime() { }
        public void SendSetActiveMover(ulong guid) { }
        public void SendMoveWorldPortAcknowledge() { }
        public void SendNameQuery(ulong guid) { }
    }
}
