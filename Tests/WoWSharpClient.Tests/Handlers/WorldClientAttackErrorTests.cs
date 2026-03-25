using GameData.Core.Enums;
using GameData.Core.Models;
using System;
using System.Threading.Tasks;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Networking.Implementation;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class WorldClientAttackErrorTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        [Theory]
        [InlineData(Opcode.SMSG_ATTACKSWING_NOTINRANGE)]
        [InlineData(Opcode.SMSG_ATTACKSWING_BADFACING)]
        [InlineData(Opcode.SMSG_ATTACKSWING_NOTSTANDING)]
        [InlineData(Opcode.SMSG_ATTACKSWING_DEADTARGET)]
        public async Task AttackSwingError_ClearsLocalAutoAttackingState(Opcode opcode)
        {
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var localPlayer = new WoWLocalPlayer(new HighGuid(0x10))
            {
                IsAutoAttacking = true,
                TargetGuid = 0x1234,
            };
            WoWSharpObjectManager.Instance.Player = localPlayer;
            WoWSharpObjectManager.Instance.ClearPendingMeleeAttackStart();
            WoWSharpObjectManager.Instance.NotePendingMeleeAttackStart(localPlayer.TargetGuid);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            connection.InjectIncomingData(CreateSmsgPacket(opcode, Array.Empty<byte>()));
            await Task.Delay(200);

            Assert.False(localPlayer.IsAutoAttacking);
            Assert.False(WoWSharpObjectManager.Instance.HasPendingMeleeAttackStart(localPlayer.TargetGuid));
        }

        private static byte[] CreateSmsgPacket(Opcode opcode, byte[] payload)
        {
            var size = (ushort)(2 + payload.Length);
            var packet = new byte[4 + payload.Length];
            packet[0] = (byte)((size >> 8) & 0xFF);
            packet[1] = (byte)(size & 0xFF);
            packet[2] = (byte)((ushort)opcode & 0xFF);
            packet[3] = (byte)(((ushort)opcode >> 8) & 0xFF);
            Array.Copy(payload, 0, packet, 4, payload.Length);
            return packet;
        }
    }
}
