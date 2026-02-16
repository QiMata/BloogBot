using WoWSharpClient.Networking.Implementation;
using GameData.Core.Enums;
using System;
using System.Threading.Tasks;

namespace WowSharpClient.NetworkTests
{
    public class NetworkingAbstractionsTests
    {
        [Fact]
        public void LengthPrefixedFramer_CanFrameAndDeframeMessages()
        {
            // Arrange
            using var framer = new LengthPrefixedFramer(4, false);
            var originalPayload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

            // Act
            var framedMessage = framer.Frame(originalPayload);
            framer.Append(framedMessage);

            // Assert
            Assert.True(framer.TryPop(out var deframedMessage));
            Assert.Equal(originalPayload, deframedMessage.ToArray());
        }

        [Fact]
        public void WoWPacketCodec_Encode_ProducesCorrectCmsgFormat()
        {
            // Arrange — Encode produces Client→Server: size(2 BE) + opcode(4 LE) + payload
            var codec = new WoWPacketCodec();
            var opcode = Opcode.CMSG_PING; // 0x01DC
            var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var encoded = codec.Encode(opcode, payload).ToArray();

            // Assert — total = 2 (size) + 4 (opcode) + 4 (payload) = 10
            Assert.Equal(10, encoded.Length);
            // Size field (big-endian) = opcode(4) + payload(4) = 8
            Assert.Equal(0x00, encoded[0]);
            Assert.Equal(0x08, encoded[1]);
            // Opcode (little-endian 4 bytes) = 0x000001DC
            Assert.Equal(0xDC, encoded[2]);
            Assert.Equal(0x01, encoded[3]);
            Assert.Equal(0x00, encoded[4]);
            Assert.Equal(0x00, encoded[5]);
            // Payload
            Assert.Equal(payload, encoded[6..]);
        }

        [Fact]
        public void WoWPacketCodec_TryDecode_ParsesSmsgFormat()
        {
            // Arrange — TryDecode expects Server→Client: size(2 BE) + opcode(2 LE) + payload
            var codec = new WoWPacketCodec();
            var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var smsgPacket = CreateSmsgPacket(Opcode.SMSG_PONG, payload);

            // Act
            var success = codec.TryDecode(smsgPacket, out var decodedOpcode, out var decodedPayload);

            // Assert
            Assert.True(success);
            Assert.Equal(Opcode.SMSG_PONG, decodedOpcode);
            Assert.Equal(payload, decodedPayload.ToArray());
        }

        /// <summary>
        /// Creates an SMSG-format packet: size(2 BE) + opcode(2 LE) + payload.
        /// </summary>
        internal static byte[] CreateSmsgPacket(Opcode opcode, byte[] payload)
        {
            var size = (ushort)(2 + payload.Length); // opcode(2) + payload
            var packet = new byte[2 + 2 + payload.Length];
            packet[0] = (byte)((size >> 8) & 0xFF);
            packet[1] = (byte)(size & 0xFF);
            packet[2] = (byte)((ushort)opcode & 0xFF);
            packet[3] = (byte)(((ushort)opcode >> 8) & 0xFF);
            Array.Copy(payload, 0, packet, 4, payload.Length);
            return packet;
        }

        [Fact]
        public void WoWMessageFramer_HandlesWoWProtocolCorrectly()
        {
            // Arrange
            using var framer = new WoWMessageFramer();
            
            // Create a WoW packet: size (big-endian) + opcode (little-endian) + payload
            var payload = new byte[] { 0x01, 0x02, 0x03 };
            var packet = new byte[7]; // 2 bytes size + 2 bytes opcode + 3 bytes payload
            
            // Size = 5 (opcode + payload) in big-endian
            packet[0] = 0x00;
            packet[1] = 0x05;
            
            // Opcode = CMSG_PING (0x1DC) in little-endian
            packet[2] = 0xDC;
            packet[3] = 0x01;
            
            // Payload
            Array.Copy(payload, 0, packet, 4, payload.Length);

            // Act
            framer.Append(packet);

            // Assert
            Assert.True(framer.TryPop(out var message));
            Assert.Equal(packet, message.ToArray());
        }

        [Fact]
        public void MessageRouter_CanRegisterAndRouteMessages()
        {
            // Arrange
            var router = new MessageRouter<Opcode>();
            var handlerCalled = false;
            var receivedPayload = Array.Empty<byte>();

            router.Register(Opcode.CMSG_PING, async (payload) =>
            {
                handlerCalled = true;
                receivedPayload = payload.ToArray();
                await Task.CompletedTask;
            });

            var testPayload = new byte[] { 0x01, 0x02, 0x03 };

            // Act
            router.RouteAsync(Opcode.CMSG_PING, testPayload).GetAwaiter().GetResult();

            // Assert
            Assert.True(handlerCalled);
            Assert.Equal(testPayload, receivedPayload);
        }

        [Fact]
        public void NoEncryption_PassesThroughDataUnchanged()
        {
            // Arrange
            var encryptor = new NoEncryption();
            var originalData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

            // Act
            var encrypted = encryptor.Encrypt(originalData);
            var decrypted = encryptor.Decrypt(encrypted);

            // Assert
            Assert.Equal(originalData, encrypted.ToArray());
            Assert.Equal(originalData, decrypted.ToArray());
        }

        [Fact]
        public void ExponentialBackoffPolicy_CalculatesDelaysCorrectly()
        {
            // Arrange
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromSeconds(10),
                backoffMultiplier: 2.0);

            // Act & Assert
            var delay1 = policy.GetDelay(1, null);
            var delay2 = policy.GetDelay(2, null);
            var delay3 = policy.GetDelay(3, null);
            var delay4 = policy.GetDelay(4, null); // Should be null (exceeds max attempts)

            Assert.NotNull(delay1);
            Assert.NotNull(delay2);
            Assert.NotNull(delay3);
            Assert.Null(delay4);

            Assert.True(delay2 > delay1);
            Assert.True(delay3 > delay2);
        }

        [Fact]
        public void FixedDelayPolicy_ReturnsConstantDelay()
        {
            // Arrange
            var fixedDelay = TimeSpan.FromSeconds(5);
            var policy = new FixedDelayPolicy(fixedDelay, maxAttempts: 2);

            // Act & Assert
            var delay1 = policy.GetDelay(1, null);
            var delay2 = policy.GetDelay(2, null);
            var delay3 = policy.GetDelay(3, null); // Should be null

            Assert.Equal(fixedDelay, delay1);
            Assert.Equal(fixedDelay, delay2);
            Assert.Null(delay3);
        }
    }
}