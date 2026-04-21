using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Text;
using System.Threading.Tasks;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Implementation;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class WorldClientNotificationTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        [Fact]
        public async Task Notification_FiresSystemMessage()
        {
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            string? systemMessage = null;
            EventHandler<OnUiMessageArgs> handler = (_, args) => systemMessage = args.Message;
            WoWSharpEventEmitter.Instance.OnSystemMessage += handler;

            try
            {
                var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                worldClient.SetHandlerContext(WoWSharpObjectManager.Instance, WoWSharpEventEmitter.Instance);
                await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

                connection.InjectIncomingData(CreateSmsgPacket(
                    Opcode.SMSG_NOTIFICATION,
                    Encoding.UTF8.GetBytes("Taxi path learned.\0")));
                await Task.Delay(200);

                Assert.Equal("Taxi path learned.", systemMessage);
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnSystemMessage -= handler;
            }
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
