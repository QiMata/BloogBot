using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Networking.Abstractions;
using GameData.Core.Enums;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace WowSharpClient.NetworkTests
{
    /// <summary>
    /// A deterministic test encryptor that XOR-transforms only the 4-byte S->C header
    /// (for Decrypt) and the 6-byte C->S header (for Encrypt). This is NOT NoEncryption,
    /// so it triggers the encrypted receive path in PacketPipeline.OnBytesReceived.
    /// The XOR key is fixed and known, making tests fully deterministic.
    /// </summary>
    internal sealed class XorHeaderEncryptor : IEncryptor
    {
        private const byte XorKey = 0xAA;
        private const int ServerHeaderSize = 4;
        private const int ClientHeaderSize = 6;

        public ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> data)
        {
            if (data.Length < ClientHeaderSize)
                return data;

            var result = data.ToArray();
            for (int i = 0; i < ClientHeaderSize; i++)
                result[i] ^= XorKey;
            return result;
        }

        public ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> data)
        {
            if (data.Length < ServerHeaderSize)
                return data;

            var result = data.ToArray();
            for (int i = 0; i < ServerHeaderSize; i++)
                result[i] ^= XorKey;
            return result;
        }

        /// <summary>
        /// Encrypts the 4-byte S->C header with the known XOR key so the pipeline
        /// can decrypt it back to the original cleartext header.
        /// </summary>
        public static byte[] EncryptSmsgHeader(byte[] clearHeader)
        {
            if (clearHeader.Length < ServerHeaderSize)
                throw new ArgumentException("Header must be at least 4 bytes");
            var encrypted = (byte[])clearHeader.Clone();
            for (int i = 0; i < ServerHeaderSize; i++)
                encrypted[i] ^= XorKey;
            return encrypted;
        }

        /// <summary>
        /// Creates an SMSG packet with an XOR-encrypted header and cleartext payload.
        /// This mimics what a real WoW server sends: encrypted 4-byte header + cleartext body.
        /// </summary>
        public static byte[] CreateEncryptedSmsgPacket(Opcode opcode, byte[] payload)
        {
            // Build cleartext header: size(2 BE) + opcode(2 LE)
            var size = (ushort)(2 + payload.Length); // opcode(2) + payload
            var header = new byte[4];
            header[0] = (byte)((size >> 8) & 0xFF);
            header[1] = (byte)(size & 0xFF);
            header[2] = (byte)((ushort)opcode & 0xFF);
            header[3] = (byte)(((ushort)opcode >> 8) & 0xFF);

            // XOR-encrypt the header
            var encryptedHeader = EncryptSmsgHeader(header);

            // Combine encrypted header + cleartext payload
            var packet = new byte[4 + payload.Length];
            Array.Copy(encryptedHeader, 0, packet, 0, 4);
            Array.Copy(payload, 0, packet, 4, payload.Length);
            return packet;
        }
    }

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

        // --- WSCN-TST-001: Encrypted receive-state tests ---

        [Fact]
        public async Task Encrypted_SingleCompletePacket_RoutedToHandler()
        {
            // Arrange - Use XorHeaderEncryptor (non-NoEncryption) to trigger encrypted path
            using var connection = new InMemoryConnection();
            var encryptor = new XorHeaderEncryptor();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var receivedPayload = Array.Empty<byte>();
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                receivedPayload = payload.ToArray();
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            // Act - Inject a packet with XOR-encrypted header + cleartext payload
            var testPayload = new byte[] { 0x01, 0x02, 0x03 };
            var encryptedPacket = XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, testPayload);
            connection.InjectIncomingData(encryptedPacket);

            await Task.Delay(100);

            // Assert
            Assert.Equal(testPayload, receivedPayload);
        }

        [Fact]
        public async Task Encrypted_FragmentedHeader_WaitsForFullHeaderBeforeDecrypt()
        {
            // Arrange - Send only 2 bytes of the 4-byte encrypted header first
            using var connection = new InMemoryConnection();
            var encryptor = new XorHeaderEncryptor();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var handlerCallCount = 0;
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            // Build the full encrypted packet
            var testPayload = new byte[] { 0xAA, 0xBB };
            var fullPacket = XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, testPayload);

            // Act - Send only first 2 bytes (partial header)
            connection.InjectIncomingData(fullPacket.Take(2).ToArray());
            await Task.Delay(50);
            Assert.Equal(0, handlerCallCount); // Cannot decrypt partial header

            // Send remaining 2 bytes of header + full payload
            connection.InjectIncomingData(fullPacket.Skip(2).ToArray());
            await Task.Delay(100);

            // Assert - Now complete packet should be processed
            Assert.Equal(1, handlerCallCount);
        }

        [Fact]
        public async Task Encrypted_FragmentedBody_HeaderDecryptedOnce_ThenWaitsForPayload()
        {
            // Arrange - Send the full 4-byte encrypted header but only partial payload
            using var connection = new InMemoryConnection();
            var encryptor = new XorHeaderEncryptor();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var receivedPayload = Array.Empty<byte>();
            var handlerCallCount = 0;
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                receivedPayload = payload.ToArray();
                Interlocked.Increment(ref handlerCallCount);
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            // Build encrypted packet with a 5-byte payload
            var testPayload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var fullPacket = XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, testPayload);

            // Act - Send header + 2 bytes of payload (need 5 more)
            connection.InjectIncomingData(fullPacket.Take(6).ToArray()); // 4 header + 2 payload
            await Task.Delay(50);
            Assert.Equal(0, handlerCallCount); // Header decrypted, but body incomplete

            // Send remaining payload bytes
            connection.InjectIncomingData(fullPacket.Skip(6).ToArray());
            await Task.Delay(100);

            // Assert - Full packet now processed, header was decrypted exactly once
            Assert.Equal(1, handlerCallCount);
            Assert.Equal(testPayload, receivedPayload);
        }

        [Fact]
        public async Task Encrypted_RemainderCarry_TwoPacketsInOneSegment()
        {
            // Arrange - Send two complete encrypted packets in a single TCP segment
            using var connection = new InMemoryConnection();
            var encryptor = new XorHeaderEncryptor();
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

            // Build two encrypted packets
            var payload1 = new byte[] { 0x11, 0x22 };
            var payload2 = new byte[] { 0x33, 0x44, 0x55 };
            var packet1 = XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, payload1);
            var packet2 = XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, payload2);

            // Concatenate into one segment
            var combined = new byte[packet1.Length + packet2.Length];
            Array.Copy(packet1, 0, combined, 0, packet1.Length);
            Array.Copy(packet2, 0, combined, packet1.Length, packet2.Length);

            // Act - Inject as a single chunk
            connection.InjectIncomingData(combined);
            await Task.Delay(150);

            // Assert - Both packets should be decoded
            Assert.Equal(2, receivedPayloads.Count);
            Assert.Equal(payload1, receivedPayloads[0]);
            Assert.Equal(payload2, receivedPayloads[1]);
        }

        [Fact]
        public async Task Encrypted_HeaderResetAfterPacket_NextPacketDecryptsCorrectly()
        {
            // Arrange - Verify _pendingDecryptedHeader is reset between packets
            using var connection = new InMemoryConnection();
            var encryptor = new XorHeaderEncryptor();
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

            // Act - Send first packet complete, then second packet complete (separate injects)
            var payload1 = new byte[] { 0xAA };
            var payload2 = new byte[] { 0xBB, 0xCC };
            connection.InjectIncomingData(XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, payload1));
            await Task.Delay(100);

            Assert.Single(receivedPayloads);
            Assert.Equal(payload1, receivedPayloads[0]);

            // Second packet - header must be decrypted fresh (not using stale state)
            connection.InjectIncomingData(XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, payload2));
            await Task.Delay(100);

            Assert.Equal(2, receivedPayloads.Count);
            Assert.Equal(payload2, receivedPayloads[1]);
        }

        [Fact]
        public async Task Encrypted_SplitAcrossThreeChunks_ReassemblesCorrectly()
        {
            // Arrange - Fragment a packet into 3 pieces: partial header, rest of header, payload
            using var connection = new InMemoryConnection();
            var encryptor = new XorHeaderEncryptor();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            var receivedPayload = Array.Empty<byte>();
            pipeline.RegisterHandler(Opcode.SMSG_PONG, async (payload) =>
            {
                receivedPayload = payload.ToArray();
                await Task.CompletedTask;
            });

            await pipeline.ConnectAsync("test", 1234);

            var testPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var fullPacket = XorHeaderEncryptor.CreateEncryptedSmsgPacket(Opcode.SMSG_PONG, testPayload);

            // Act - Chunk 1: first byte of header
            connection.InjectIncomingData(fullPacket.Take(1).ToArray());
            await Task.Delay(30);

            // Chunk 2: rest of header
            connection.InjectIncomingData(fullPacket.Skip(1).Take(3).ToArray());
            await Task.Delay(30);

            // Chunk 3: full payload
            connection.InjectIncomingData(fullPacket.Skip(4).ToArray());
            await Task.Delay(100);

            // Assert
            Assert.Equal(testPayload, receivedPayload);
        }

        // --- WSCN-TST-002: Concurrent send serialization tests ---

        [Fact]
        public async Task ConcurrentSends_Serialized_NoPayloadInterleaving()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
            await pipeline.ConnectAsync("test", 1234);

            // Act - Fire 10 concurrent sends
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var payload = new byte[] { (byte)i, (byte)(i + 100) };
                tasks.Add(pipeline.SendAsync(Opcode.CMSG_PING, payload));
            }

            await Task.WhenAll(tasks);

            // Assert - All 10 sends should complete and produce 10 distinct packets
            var sentData = connection.GetSentData();
            Assert.Equal(10, sentData.Length);

            // Each sent packet must be well-formed: size(2 BE) + opcode(4 LE) + payload(2)
            foreach (var packet in sentData)
            {
                Assert.Equal(8, packet.Length); // 2 + 4 + 2
                // Verify size field (big-endian): opcode(4) + payload(2) = 6
                Assert.Equal(0x00, packet[0]);
                Assert.Equal(0x06, packet[1]);
            }
        }

        [Fact]
        public async Task ConcurrentSends_AllPayloadsPreserved()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            using var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
            await pipeline.ConnectAsync("test", 1234);

            // Act - Send 20 concurrent packets with unique marker bytes
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                var payload = new byte[] { (byte)i };
                tasks.Add(pipeline.SendAsync(Opcode.CMSG_PING, payload));
            }

            await Task.WhenAll(tasks);

            // Assert - All 20 unique payloads should be present (order may vary)
            var sentData = connection.GetSentData();
            Assert.Equal(20, sentData.Length);

            // Extract the marker byte from each sent packet (last byte of each CMSG)
            var markers = sentData.Select(p => p[p.Length - 1]).OrderBy(b => b).ToArray();
            var expected = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
            Assert.Equal(expected, markers);
        }

        [Fact]
        public async Task SendAsync_AfterDispose_ThrowsObjectDisposed()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var encryptor = new NoEncryption();
            using var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
            await pipeline.ConnectAsync("test", 1234);
            pipeline.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => pipeline.SendAsync(Opcode.CMSG_PING, new byte[] { 0x01 }));
        }
    }
}