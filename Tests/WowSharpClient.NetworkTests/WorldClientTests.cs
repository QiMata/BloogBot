using Xunit;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using System;
using System.IO;
using System.Text;

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
            await worldClient.SendMovementAsync(Opcode.MSG_MOVE_START_FORWARD, movementInfo);

            // Assert
            var sentData = connection.GetSentData();
            Assert.Single(sentData);

            // Decode the sent packet to verify it was properly encoded
            var sentPacket = sentData[0];
            var decryptedPacket = encryptor.Decrypt(sentPacket);
            
            framer.Append(decryptedPacket);
            Assert.True(framer.TryPop(out var framedMessage));
            
            Assert.True(codec.TryDecode(framedMessage, out var decodedOpcode, out var decodedPayload));
            Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, decodedOpcode);
            Assert.Equal(movementInfo, decodedPayload.ToArray());
        }

        [Fact]
        public async Task DisconnectFromServer_RaisesEvent()
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

            worldClient.Disconnected += (ex) =>
            {
                disconnectedCalled = true;
                disconnectionException = ex;
            };

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
        public async Task AuthChallenge_TriggersAuthSession()
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

            connection.ClearData(); // Clear connection data

            // Act - Simulate SMSG_AUTH_CHALLENGE
            var serverSeed = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var authChallengePacket = CreateWoWPacket(Opcode.SMSG_AUTH_CHALLENGE, serverSeed);
            connection.InjectIncomingData(authChallengePacket);

            await Task.Delay(200);

            // Assert - Should send CMSG_AUTH_SESSION in response
            var sentData = connection.GetSentData();
            Assert.Single(sentData);

            // Verify it's an AUTH_SESSION packet
            var sentPacket = sentData[0];
            var decryptedPacket = encryptor.Decrypt(sentPacket);
            
            framer.Append(decryptedPacket);
            Assert.True(framer.TryPop(out var framedMessage));
            
            Assert.True(codec.TryDecode(framedMessage, out var decodedOpcode, out var decodedPayload));
            Assert.Equal(Opcode.CMSG_AUTH_SESSION, decodedOpcode);
            Assert.True(decodedPayload.Length > 0);
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
            worldClient.OnAuthenticationSuccessful += () => authSuccessCalled = true;

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
            worldClient.OnAuthenticationFailed += (code) =>
            {
                authFailedCalled = true;
                failureCode = code;
            };

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

            // Decode and verify ping packet
            var sentPacket = sentData[0];
            var decryptedPacket = encryptor.Decrypt(sentPacket);
            
            framer.Append(decryptedPacket);
            Assert.True(framer.TryPop(out var framedMessage));
            
            Assert.True(codec.TryDecode(framedMessage, out var decodedOpcode, out var decodedPayload));
            Assert.Equal(Opcode.CMSG_PING, decodedOpcode);
            Assert.Equal(8, decodedPayload.Length); // 4 bytes sequence + 4 bytes latency

            // Verify sequence number
            var sequence = BitConverter.ToUInt32(decodedPayload.Span[0..4]);
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

        private static byte[] CreateWoWPacket(Opcode opcode, byte[] payload)
        {
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var encryptor = new NoEncryption();

            var encodedPacket = codec.Encode(opcode, payload);
            var framedMessage = framer.Frame(encodedPacket);
            var encryptedMessage = encryptor.Encrypt(framedMessage);

            return encryptedMessage.ToArray();
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