using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.I;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// A composition hub that coordinates connection, encryption, framing, and packet routing.
    /// </summary>
    /// <typeparam name="TOpcode">The type of opcode to use for packets.</typeparam>
    public sealed class PacketPipeline<TOpcode> : IDisposable where TOpcode : Enum
    {
        private readonly IConnection _connection;
        private readonly IMessageFramer _framer;
        private readonly IPacketCodec<TOpcode> _codec;
        private readonly IMessageRouter<TOpcode> _router;
        private IDisposable? _rxSubscription;
        private IDisposable? _discSubscription;
        private IDisposable? _connSubscription;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the encryptor used by this pipeline.
        /// This is typically used to switch from NoEncryption to RC4 after authentication.
        /// </summary>
        public IEncryptor Encryptor { get; set; }

        public PacketPipeline(
            IConnection connection,
            IEncryptor encryptor,
            IMessageFramer framer,
            IPacketCodec<TOpcode> codec,
            IMessageRouter<TOpcode> router)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            _framer = framer ?? throw new ArgumentNullException(nameof(framer));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            // Subscribe to bytes stream
            _rxSubscription = _connection.ReceivedBytes.Subscribe(OnBytesReceived);

            // Forward lifecycle events
            _connSubscription = _connection.WhenConnected.Subscribe(_ => Connected?.Invoke());
            _discSubscription = _connection.WhenDisconnected.Subscribe(ex => Disconnected?.Invoke(ex));
        }

        /// <summary>
        /// Gets a value indicating whether the underlying connection is established.
        /// </summary>
        public bool IsConnected => _connection.IsConnected;

        /// <summary>
        /// Event fired when the underlying connection is established.
        /// </summary>
        public event Action? Connected;

        /// <summary>
        /// Event fired when the underlying connection is disconnected. Exception is null for graceful disconnects.
        /// </summary>
        public event Action<Exception?>? Disconnected;

        /// <summary>
        /// Connects to the specified host and port.
        /// </summary>
        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            return _connection.ConnectAsync(host, port, cancellationToken);
        }

        /// <summary>
        /// Disconnects from the remote host.
        /// </summary>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            return _connection.DisconnectAsync(cancellationToken);
        }

        /// <summary>
        /// Sends a packet with the specified opcode and payload.
        /// </summary>
        public async Task SendAsync(TOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PacketPipeline<TOpcode>));

            // Encode opcode and payload into packet
            var packetData = _codec.Encode(opcode, payload);

            // Frame the packet
            var framedData = _framer.Frame(packetData);

            // Encrypt the framed data
            var encryptedData = Encryptor.Encrypt(framedData);

            // Send through connection
            await _connection.SendAsync(encryptedData, cancellationToken);
        }

        /// <summary>
        /// Registers a handler for the specified opcode.
        /// </summary>
        /// <param name="opcode">The opcode to handle.</param>
        /// <param name="handler">The handler function.</param>
        public void RegisterHandler(TOpcode opcode, Func<ReadOnlyMemory<byte>, Task> handler)
        {
            _router.Register(opcode, handler);
        }

        /// <summary>
        /// Exposes connection observables.
        /// </summary>
        public IObservable<System.Reactive.Unit> WhenConnected => _connection.WhenConnected;
        public IObservable<Exception?> WhenDisconnected => _connection.WhenDisconnected;

        private async void OnBytesReceived(ReadOnlyMemory<byte> data)
        {
            try
            {
                // Decrypt the data
                var decryptedData = Encryptor.Decrypt(data);

                // Append to framer
                _framer.Append(decryptedData);

                // Process all complete messages
                while (_framer.TryPop(out var message))
                {
                    // Decode packet
                    if (_codec.TryDecode(message, out var opcode, out var payload))
                    {
                        // Route to handler
                        await _router.RouteAsync(opcode, payload);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to decode packet of {message.Length} bytes");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing received bytes: {ex}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _rxSubscription?.Dispose();
                _discSubscription?.Dispose();
                _connSubscription?.Dispose();
                _connection.Dispose();

                if (_framer is IDisposable disposableFramer)
                    disposableFramer.Dispose();

                _disposed = true;
            }
        }
    }
}