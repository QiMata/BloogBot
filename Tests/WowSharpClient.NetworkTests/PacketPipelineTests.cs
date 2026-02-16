using WoWSharpClient.Networking.Implementation;
using GameData.Core.Enums;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace WowSharpClient.NetworkTests
{
    public class PacketPipelineTests
    {
        [Fact]
        public async Task IncomingBytes_FramerDecryptDecode_RoutedToHandler()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();
            
            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var handlerCalled = false;
            var receivedOpcode = Opcode.CMSG_PING;
            var receivedPayload = Array.Empty<byte>();

            // Register handler
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                handlerCalled = true;
                receivedOpcode = Opcode.SMSG_PONG;
                receivedPayload = payload.ToArray();
                await Task.CompletedTask;
            });

            // Connect the pipeline
            await pipeline.ConnectAsync("test", 1234);

            // Act - Inject a server-format (SMSG) packet: size(2 BE) + opcode(2 LE) + payload
            var testPayload = new byte[] { 0x01, 0x02, 0x03 };
            var smsgPacket = NetworkingAbstractionsTests.CreateSmsgPacket(Opcode.SMSG_PONG, testPayload);
            connection.InjectIncomingData(smsgPacket);

            // Wait a moment for async processing
            await Task.Delay(100);

            // Assert
            Assert.True(handlerCalled);
            Assert.Equal(Opcode.SMSG_PONG, receivedOpcode);
            Assert.Equal(testPayload, receivedPayload);
        }

        [Fact]
        public async Task SendAsync_CodecFrameEncrypt_WritesToConnection()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();
            
            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            await pipeline.ConnectAsync("test", 1234);

            var testPayload = new byte[] { 0x04, 0x05, 0x06 };

            // Act
            await pipeline.SendAsync(Opcode.CMSG_PING, testPayload);

            // Assert
            var sentData = connection.GetSentData();
            Assert.Single(sentData);

            // Verify the sent data follows the expected pipeline: Codec -> Framer -> Encryptor
            var expectedEncoded = codec.Encode(Opcode.CMSG_PING, testPayload);
            var expectedFramed = framer.Frame(expectedEncoded);
            var expectedEncrypted = encryptor.Encrypt(expectedFramed);

            Assert.Equal(expectedEncrypted.ToArray(), sentData[0]);
        }

        [Fact]
        public async Task PartialFrames_CombineBeforeRouting()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();
            
            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var handlerCallCount = 0;
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                handlerCallCount++;
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            // Act - Create a complete SMSG packet and split it into partial frames
            var testPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var fullMessage = NetworkingAbstractionsTests.CreateSmsgPacket(Opcode.SMSG_PONG, testPayload);
            
            // Split into multiple partial frames
            var part1 = fullMessage.Take(3).ToArray();
            var part2 = fullMessage.Skip(3).ToArray();

            // Inject partial frames
            connection.InjectIncomingData(part1);
            await Task.Delay(50); // Wait briefly
            
            // Should not have called handler yet (incomplete frame)
            Assert.Equal(0, handlerCallCount);

            connection.InjectIncomingData(part2);
            await Task.Delay(100); // Wait for processing

            // Assert - Handler should be called once after complete frame is assembled
            Assert.Equal(1, handlerCallCount);
        }

        [Fact]
        public async Task HandlerThrows_PipelineContinuesOrPolicyDecides()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();
            
            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var firstHandlerCalled = false;
            var secondHandlerCalled = false;

            // Register a handler that throws
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                firstHandlerCalled = true;
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            });

            // Register another handler for a different opcode
            pipeline.RegisterHandler(Opcode.SMSG_AUTH_CHALLENGE, async (payload) =>
            {
                secondHandlerCalled = true;
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            // Act - Send a server-format message to the throwing handler
            var testPayload1 = new byte[] { 0x01, 0x02 };
            connection.InjectIncomingData(NetworkingAbstractionsTests.CreateSmsgPacket(Opcode.SMSG_PONG, testPayload1));

            await Task.Delay(100);

            // Send a server-format message to the non-throwing handler
            var testPayload2 = new byte[] { 0x03, 0x04 };
            connection.InjectIncomingData(NetworkingAbstractionsTests.CreateSmsgPacket(Opcode.SMSG_AUTH_CHALLENGE, testPayload2));

            await Task.Delay(100);

            // Assert - Pipeline should continue operating despite handler exception
            Assert.True(firstHandlerCalled);
            Assert.True(secondHandlerCalled);
            Assert.True(pipeline.IsConnected); // Pipeline should still be connected
        }

        [Fact]
        public async Task MultipleMessages_ProcessedInOrder()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();
            
            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var receivedPayloads = new List<byte[]>();
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                receivedPayloads.Add(payload.ToArray());
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            // Act - Send multiple messages
            var payloads = new[]
            {
                new byte[] { 0x01 },
                new byte[] { 0x02, 0x03 },
                new byte[] { 0x04, 0x05, 0x06 }
            };

            foreach (var payload in payloads)
            {
                connection.InjectIncomingData(NetworkingAbstractionsTests.CreateSmsgPacket(Opcode.SMSG_PONG, payload));
            }

            await Task.Delay(200);

            // Assert
            Assert.Equal(3, receivedPayloads.Count);
            Assert.Equal(new byte[] { 0x01 }, receivedPayloads[0]);
            Assert.Equal(new byte[] { 0x02, 0x03 }, receivedPayloads[1]);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x06 }, receivedPayloads[2]);
        }

        [Fact]
        public async Task ConnectionLost_FiresDisconnectedEvent()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();
            
            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var disconnectedCalled = false;
            Exception? disconnectionException = null;

            pipeline.Disconnected += (ex) =>
            {
                disconnectedCalled = true;
                disconnectionException = ex;
            };

            await pipeline.ConnectAsync("test", 1234);

            // Act
            var testException = new InvalidOperationException("Connection lost");
            connection.SimulateConnectionError(testException);

            await Task.Delay(100);

            // Assert
            Assert.True(disconnectedCalled);
            Assert.Equal(testException, disconnectionException);
            Assert.False(pipeline.IsConnected);
        }
    }
}