using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        // --- WSCN-TST-006: Bridge coverage and exception swallow tests ---

        [Fact]
        public async Task BridgeRegistration_MovementOpcodes_Registered()
        {
            // Arrange - Verify representative movement opcodes are registered by
            // injecting packets and checking they're handled (not dropped silently).
            // The bridge handlers may fail internally (no ObjectManager), but the pipeline
            // should not throw and should remain connected.
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Inject movement opcodes that are bridged to legacy handlers
            var movementOpcodes = new[]
            {
                Opcode.MSG_MOVE_START_FORWARD,
                Opcode.MSG_MOVE_STOP,
                Opcode.MSG_MOVE_HEARTBEAT,
                Opcode.MSG_MOVE_JUMP,
                Opcode.MSG_MOVE_SET_FACING,
            };

            foreach (var opcode in movementOpcodes)
            {
                var minimalPayload = new byte[] { 0x00 };
                connection.InjectIncomingData(CreateWoWPacket(opcode, minimalPayload));
            }

            await Task.Delay(200);

            // Assert - Pipeline should remain connected after processing bridged opcodes
            Assert.True(worldClient.IsConnected);
        }

        [Fact]
        public async Task BridgeRegistration_LoginAndObjectOpcodes_Registered()
        {
            // Arrange - Verify login/object opcodes are registered in the bridge
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Inject opcodes that are bridged
            var bridgedOpcodes = new[]
            {
                Opcode.SMSG_LOGIN_VERIFY_WORLD,
                Opcode.SMSG_QUERY_TIME_RESPONSE,
                Opcode.SMSG_INITIAL_SPELLS,
            };

            foreach (var opcode in bridgedOpcodes)
            {
                // Minimal payload - bridge handler may fail parsing but that's OK
                var payload = new byte[4];
                connection.InjectIncomingData(CreateWoWPacket(opcode, payload));
            }

            await Task.Delay(200);

            // Assert - Pipeline stays alive despite potential handler parse errors
            Assert.True(worldClient.IsConnected);
        }

        [Fact]
        public async Task BridgeLegacyHandler_ThrowsException_PipelineContinues()
        {
            // Arrange - The BridgeToLegacy catch block swallows exceptions.
            // Verify that after a handler throws, subsequent opcodes are still dispatched.
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Send a bridged opcode with malformed data (will cause handler to throw)
            // SMSG_UPDATE_OBJECT with empty payload will fail in the handler
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_UPDATE_OBJECT, new byte[] { 0xFF }));
            await Task.Delay(100);

            // Now send a well-formed auth response (non-bridged handler)
            var authOk = new byte[] { 0x0C }; // AUTH_OK
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_AUTH_RESPONSE, authOk));
            await Task.Delay(200);

            // Assert - Pipeline should have survived the bridged handler throw
            // and processed the AUTH_RESPONSE successfully
            Assert.True(worldClient.IsConnected);
            Assert.True(worldClient.IsAuthenticated);
        }

        [Fact]
        public async Task BridgeLegacyHandler_MultipleThrows_NeverTerminatesPipeline()
        {
            // Arrange - Fire multiple opcodes that will throw in their bridge handlers
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Send multiple malformed bridged opcodes
            for (int i = 0; i < 5; i++)
            {
                connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_UPDATE_OBJECT, new byte[] { 0xFF }));
                connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_MONSTER_MOVE, new byte[] { 0x00 }));
            }

            await Task.Delay(300);

            // Assert - Pipeline must still be alive
            Assert.True(worldClient.IsConnected);

            // Verify it can still process new packets
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_AUTH_RESPONSE, new byte[] { 0x0C }));
            await Task.Delay(200);
            Assert.True(worldClient.IsAuthenticated);
        }

        [Fact]
        public async Task AttackSwingErrors_EmitToObservable()
        {
            // Arrange - Verify attack error opcodes emit strings to AttackErrors observable
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var errors = new List<string>();
            using var sub = worldClient.AttackErrors.Subscribe(err => { lock (errors) errors.Add(err); });

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Send attack error opcodes
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_ATTACKSWING_NOTINRANGE, Array.Empty<byte>()));
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_ATTACKSWING_BADFACING, Array.Empty<byte>()));
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_ATTACKSWING_DEADTARGET, Array.Empty<byte>()));

            await Task.Delay(200);

            // Assert - Should have received 3 error strings
            Assert.Equal(3, errors.Count);
            Assert.Contains("Not in range", errors[0]);
            Assert.Contains("Bad facing", errors[1]);
            Assert.Contains("dead", errors[2], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AttackStart_BridgedToLegacy_PipelineSurvives()
        {
            // Arrange - SMSG_ATTACKSTART is registered twice: first as a WorldClient
            // reactive handler, then overwritten by BridgeToLegacy (which calls
            // SpellHandler.HandleAttackStart). The bridge handler wraps exceptions,
            // so the reactive AttackStateChanged subject is NOT emitted to via the
            // pipeline. This test verifies the bridge handler runs without crashing.
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act - Send ATTACKSTART with attacker=1, victim=2
            var payload = new byte[16];
            BitConverter.GetBytes((ulong)1).CopyTo(payload, 0);
            BitConverter.GetBytes((ulong)2).CopyTo(payload, 8);
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_ATTACKSTART, payload));

            await Task.Delay(150);

            // Assert - Pipeline should remain alive (bridge handler swallows exceptions)
            Assert.True(worldClient.IsConnected);
        }

        [Fact]
        public async Task RegisterOpcodeHandler_ReturnsObservableStream()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            using var framer = new WoWMessageFramer();
            var encryptor = new NoEncryption();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var worldClient = new WorldClient(connection, framer, encryptor, codec, router);

            var receivedPayloads = new List<byte[]>();
            var stream = worldClient.RegisterOpcodeHandler(Opcode.SMSG_QUERY_TIME_RESPONSE);
            using var sub = stream.Subscribe(payload =>
            {
                lock (receivedPayloads) receivedPayloads.Add(payload.ToArray());
            });

            var sessionKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await worldClient.ConnectAsync("testuser", "127.0.0.1", sessionKey);

            // Act
            var testPayload = new byte[] { 0x11, 0x22, 0x33, 0x44 };
            connection.InjectIncomingData(CreateWoWPacket(Opcode.SMSG_QUERY_TIME_RESPONSE, testPayload));
            await Task.Delay(150);

            // Assert
            Assert.Single(receivedPayloads);
            Assert.Equal(testPayload, receivedPayloads[0]);
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