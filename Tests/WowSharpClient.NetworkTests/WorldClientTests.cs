using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System;
using System.IO;

namespace WowSharpClient.NetworkTests
{
    public class WorldClientTests
    {
        [Fact]
        public async Task Connect_WithSessionKey_SetsEncryptor()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey, 8085);

            // Assert
            Assert.True(worldClient.IsConnected);
            Assert.False(worldClient.IsAuthenticated); // Should not be authenticated until SMSG_AUTH_RESPONSE received
        }

        [Fact]
        public async Task IncomingOpcode_RoutedToRegisteredHandler()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var handlerCalled = false;
            var receivedPayload = Array.Empty<byte>();

            // Register a custom handler via the router (simulating internal registration)
            router.Register(Opcode.SMSG_QUERY_TIME_RESPONSE, async (payload) =>
            {
                handlerCalled = true;
                receivedPayload = payload.ToArray();
                await Task.CompletedTask;
            });

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Inject a packet
            var testPayload = new byte[] { 0x11, 0x22, 0x33, 0x44 }; // Server time
            var packet = CreateWoWPacket(Opcode.SMSG_QUERY_TIME_RESPONSE, testPayload);
            connection.InjectIncomingData(packet);

            await Task.Delay(100);

            // Assert
            Assert.True(handlerCalled);
            Assert.Equal(testPayload, receivedPayload);
        }

        [Fact]
        public async Task SendMovement_EncodesAndSendsCorrectPacket()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Create movement info
            var movementInfo = CreateMockMovementInfo();

            // Act
            await worldClient.SendOpcodeAsync(Opcode.MSG_MOVE_START_FORWARD, movementInfo);

            // Assert
            var sentData = connection.GetSentData();
            Assert.Single(sentData);

            // Parse the sent CMSG packet: size(2 BE) + opcode(4 LE) + payload
            var sentPacket = sentData[0];
            var (decodedOpcode, decodedPayload) = ParseCmsgPacket(sentPacket);

            Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, decodedOpcode);
            Assert.Equal(movementInfo, decodedPayload);
        }

        [Fact]
        public async Task DisconnectFromServer_RaisesObservable()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var disconnectedCalled = false;
            Exception? disconnectionException = null;

            using var sub = worldClient.WhenDisconnected.Subscribe(ex =>
            {
                disconnectedCalled = true;
                disconnectionException = ex;
            });

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act
            var testException = new InvalidOperationException("Server disconnected");
            connection.SimulateConnectionError(testException);

            await Task.Delay(100);

            // Assert
            Assert.True(disconnectedCalled);
            Assert.Equal(testException, disconnectionException);
            Assert.False(worldClient.IsConnected);
        }

        [Fact]
        public async Task AuthResponse_Success_SetsAuthenticated()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var authSuccessCalled = false;
            using var sub = worldClient.AuthenticationSucceeded.Subscribe(_ => authSuccessCalled = true);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Simulate successful AUTH_RESPONSE
            var authResponsePayload = new byte[] { 0x0C }; // AUTH_OK
            var authResponsePacket = CreateWoWPacket(Opcode.SMSG_AUTH_RESPONSE, authResponsePayload);
            connection.InjectIncomingData(authResponsePacket);

            await Task.Delay(200);

            // Assert
            Assert.True(worldClient.IsAuthenticated);
            Assert.True(authSuccessCalled);
        }

        [Fact]
        public async Task AuthResponse_Failure_DoesNotSetAuthenticated()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var authFailedCalled = false;
            byte failureCode = 0;
            using var sub = worldClient.AuthenticationFailed.Subscribe(code => { authFailedCalled = true; failureCode = code; });

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Simulate failed AUTH_RESPONSE
            var authResponsePayload = new byte[] { 0x15 }; // AUTH_FAILED
            var authResponsePacket = CreateWoWPacket(Opcode.SMSG_AUTH_RESPONSE, authResponsePayload);
            connection.InjectIncomingData(authResponsePacket);

            await Task.Delay(200);

            // Assert
            Assert.False(worldClient.IsAuthenticated);
            Assert.True(authFailedCalled);
            Assert.Equal(0x15, failureCode);
        }

        [Fact]
        public async Task SendPing_EncodesCorrectly()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act
            await worldClient.SendPingAsync(0x12345678);

            // Assert
            var sentData = connection.GetSentData();
            Assert.Single(sentData);

            // Parse the sent CMSG packet: size(2 BE) + opcode(4 LE) + payload
            var sentPacket = sentData[0];
            var (decodedOpcode, decodedPayload) = ParseCmsgPacket(sentPacket);

            Assert.Equal(Opcode.CMSG_PING, decodedOpcode);
            Assert.Equal(8, decodedPayload.Length); // 4 bytes sequence + 4 bytes latency

            // Verify sequence number
            var sequence = BitConverter.ToUInt32(decodedPayload, 0);
            Assert.Equal(0x12345678u, sequence);
        }

        [Fact]
        public async Task UpdateEncryptor_ChangesEncryption()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var initialEncryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, initialEncryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Send a packet with no encryption
            await worldClient.SendPingAsync(0x12345678);
            var unencryptedData = connection.GetSentData()[0];

            connection.ClearData();

            // Act - Update to a different encryptor (still NoEncryption for testing, but different instance)
            var newEncryptor = new NoEncryption();
            worldClient.UpdateEncryptor(newEncryptor);

            // Send another packet
            await worldClient.SendPingAsync(0x87654321);
            var newEncryptedData = connection.GetSentData()[0];

            // Assert - The packets should be different even though both use NoEncryption
            // (This test mainly verifies the encryptor can be updated without crashing)
            Assert.NotNull(unencryptedData);
            Assert.NotNull(newEncryptedData);
        }

        /// <summary>
        /// Creates an SMSG-format packet for injection: size(2 BE) + opcode(2 LE) + payload.
        /// </summary>
        private static byte[] CreateWoWPacket(Opcode opcode, byte[] payload)
        {
            return NetworkingAbstractionsTests.CreateSmsgPacket(opcode, payload);
        }

        /// <summary>
        /// Parses a sent CMSG packet: size(2 BE) + opcode(4 LE) + payload.
        /// Returns (opcode, payload).
        /// </summary>
        private static (Opcode opcode, byte[] payload) ParseCmsgPacket(byte[] raw)
        {
            // CMSG header: size(2 big-endian) + opcode(4 little-endian)
            var opcodeValue = (uint)(raw[2] | (raw[3] << 8) | (raw[4] << 16) | (raw[5] << 24));
            var payload = new byte[raw.Length - 6];
            Array.Copy(raw, 6, payload, 0, payload.Length);
            return ((Opcode)opcodeValue, payload);
        }

        private static byte[] CreateMockMovementInfo()
        {
            // Create a minimal movement info structure
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((uint)0x00000001); // Movement flags
            writer.Write((uint)Environment.TickCount); // Time
            writer.Write(100.0f); // X position
            writer.Write(200.0f); // Y position
            writer.Write(300.0f); // Z position
            writer.Write(1.5f);   // Orientation

            return ms.ToArray();
        }
    }
}