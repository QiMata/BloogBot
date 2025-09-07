using System;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.Implementation;

namespace WoWSharpClient.Networking.Examples
{
    /// <summary>
    /// Example showing how to use the networking abstractions for a WoW client connection.
    /// </summary>
    public sealed class WoWNetworkingExample : IDisposable
    {
        private readonly PacketPipeline<Opcode> _pipeline;
        private readonly ConnectionManager _connectionManager;

        public WoWNetworkingExample(string host, int port)
        {
            // Create the networking components
            var connection = new TcpConnection();
            var encryptor = new NoEncryption(); // Start with no encryption, switch to WoW encryption after auth
            var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            // Create the pipeline that coordinates everything
            _pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

            // Create connection manager with reconnection policy
            var reconnectPolicy = new ExponentialBackoffPolicy(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromSeconds(30));

            _connectionManager = new ConnectionManager(connection, reconnectPolicy, host, port);

            // Set up event handlers
            _connectionManager.Connected += OnConnected;
            _connectionManager.Disconnected += OnDisconnected;

            // Register packet handlers
            RegisterPacketHandlers();
        }

        private void RegisterPacketHandlers()
        {
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_CHALLENGE, HandleAuthChallenge);
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_RESPONSE, HandleAuthResponse);
            _pipeline.RegisterHandler(Opcode.SMSG_CHAR_ENUM, HandleCharEnum);
            _pipeline.RegisterHandler(Opcode.SMSG_PONG, HandlePong);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _connectionManager.ConnectAsync(cancellationToken);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            await _connectionManager.DisconnectAsync(cancellationToken);
        }

        public async Task SendPingAsync(uint sequence, CancellationToken cancellationToken = default)
        {
            // Create ping payload
            var payload = new byte[8];
            BitConverter.GetBytes(sequence).CopyTo(payload, 0);
            BitConverter.GetBytes(0u).CopyTo(payload, 4); // latency (0 for now)

            await _pipeline.SendAsync(Opcode.CMSG_PING, payload, cancellationToken);
        }

        public async Task SendCharEnumAsync(CancellationToken cancellationToken = default)
        {
            // Character enum has no payload
            await _pipeline.SendAsync(Opcode.CMSG_CHAR_ENUM, ReadOnlyMemory<byte>.Empty, cancellationToken);
        }

        private void OnConnected()
        {
            Console.WriteLine("[WoWNetworking] Connected to server");
        }

        private void OnDisconnected(Exception? exception)
        {
            if (exception != null)
                Console.WriteLine($"[WoWNetworking] Disconnected due to error: {exception.Message}");
            else
                Console.WriteLine("[WoWNetworking] Disconnected gracefully");
        }

        private async Task HandleAuthChallenge(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WoWNetworking] Received SMSG_AUTH_CHALLENGE ({payload.Length} bytes)");
            
            // Here you would typically:
            // 1. Extract server seed from payload
            // 2. Generate client proof
            // 3. Send CMSG_AUTH_SESSION
            
            await Task.CompletedTask;
        }

        private async Task HandleAuthResponse(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WoWNetworking] Received SMSG_AUTH_RESPONSE ({payload.Length} bytes)");
            
            if (payload.Length > 0)
            {
                var result = payload.Span[0];
                if (result == 0x0C) // AUTH_OK
                {
                    Console.WriteLine("[WoWNetworking] Authentication successful!");
                    // Now you can switch to encrypted communication
                    // and send character enumeration
                    await SendCharEnumAsync();
                }
                else
                {
                    Console.WriteLine($"[WoWNetworking] Authentication failed with code: {result:X2}");
                }
            }

            await Task.CompletedTask;
        }

        private async Task HandleCharEnum(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WoWNetworking] Received SMSG_CHAR_ENUM ({payload.Length} bytes)");
            
            // Here you would parse the character list
            // For now, just acknowledge receipt
            
            await Task.CompletedTask;
        }

        private async Task HandlePong(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length >= 4)
            {
                var sequence = BitConverter.ToUInt32(payload.Span[0..4]);
                Console.WriteLine($"[WoWNetworking] Received SMSG_PONG for sequence {sequence}");
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _connectionManager?.Dispose();
            _pipeline?.Dispose();
        }
    }

    /// <summary>
    /// Factory class for creating pre-configured networking setups.
    /// </summary>
    public static class WoWNetworkingFactory
    {
        /// <summary>
        /// Creates a basic WoW client networking setup with no encryption.
        /// </summary>
        public static PacketPipeline<Opcode> CreateBasicPipeline()
        {
            var connection = new TcpConnection();
            var encryptor = new NoEncryption();
            var framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            return new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
        }

        /// <summary>
        /// Creates a WoW client networking setup with length-prefixed framing (for testing).
        /// </summary>
        public static PacketPipeline<Opcode> CreateLengthPrefixedPipeline()
        {
            var connection = new TcpConnection();
            var encryptor = new NoEncryption();
            var framer = new LengthPrefixedFramer(4, false); // 4-byte little-endian length prefix
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            return new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
        }

        /// <summary>
        /// Creates a connection manager with reasonable defaults for WoW.
        /// </summary>
        public static ConnectionManager CreateConnectionManager(IConnection connection, string host, int port)
        {
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromSeconds(2),
                maxDelay: TimeSpan.FromSeconds(30));

            return new ConnectionManager(connection, policy, host, port);
        }
    }
}