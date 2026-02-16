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

    public sealed class NetworkGameClientAdapter(WoWClient client) : IGameClientAdapter
    {
        private readonly WoWClient _client = client;
        public WoWClient Client => _client;

        public void SendMovementOpcode(Opcode opcode, byte[] movementInfo) => _ = _client.SendMovementOpcodeAsync(opcode, movementInfo);
        public void SendMSGPacked(Opcode opcode, byte[] payload) => _ = _client.SendMSGPackedAsync(opcode, payload);
        public void SendPing() => _ = _client.SendPingAsync();
        public void QueryTime() => _ = _client.QueryTimeAsync();
        public void SendSetActiveMover(ulong guid) => _ = _client.SendSetActiveMoverAsync(guid);
        public void SendMoveWorldPortAcknowledge() => _ = _client.SendMoveWorldPortAcknowledgeAsync();
        public void SendNameQuery(ulong guid) => _ = _client.SendNameQueryAsync(guid);
        public void EnterWorld(ulong guid) => _ = _client.EnterWorldAsync(guid);
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
