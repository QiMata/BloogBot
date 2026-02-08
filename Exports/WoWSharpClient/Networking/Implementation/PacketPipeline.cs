using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.I;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// A composition hub that coordinates connection, encryption, framing, and packet routing.
    ///
    /// Receive path handles per-packet header decryption to support WoW's stateful stream
    /// cipher where only the 4-byte S→C header is encrypted (not the payload). Multiple
    /// packets can arrive in a single TCP segment, requiring header-by-header decryption.
    /// </summary>
    /// <typeparam name="TOpcode">The type of opcode to use for packets.</typeparam>
    public sealed class PacketPipeline<TOpcode> : IDisposable where TOpcode : Enum
    {
        private const int ServerHeaderSize = 4; // S→C: 2 size (BE) + 2 opcode (LE)

        private readonly IConnection _connection;
        private readonly IMessageFramer _framer;
        private readonly IPacketCodec<TOpcode> _codec;
        private readonly IMessageRouter<TOpcode> _router;
        private IDisposable? _rxSubscription;
        private IDisposable? _discSubscription;
        private IDisposable? _connSubscription;
        private bool _disposed;

        // Raw receive buffer for per-packet header decryption
        private readonly MemoryStream _rawReceiveBuffer = new();
        private byte[]? _pendingDecryptedHeader;

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

        /// <summary>
        /// Processes received TCP data. Uses per-packet header decryption when a header-only
        /// encryptor (e.g. VanillaHeaderEncryptor) is active, otherwise uses the legacy
        /// full-chunk decrypt + framer path (for unencrypted or non-WoW protocols).
        /// </summary>
        private async void OnBytesReceived(ReadOnlyMemory<byte> data)
        {
            try
            {
                if (Encryptor is NoEncryption)
                {
                    // Unencrypted path: use framer directly (auth client, pre-encryption world client)
                    _framer.Append(data);
                    while (_framer.TryPop(out var message))
                    {
                        if (_codec.TryDecode(message, out var opcode, out var payload))
                            await _router.RouteAsync(opcode, payload);
                        else
                            Console.WriteLine($"Failed to decode packet of {message.Length} bytes");
                    }
                    return;
                }

                // Encrypted path: per-packet header decryption for WoW's stream cipher.
                // Only the 4-byte S→C header is encrypted per packet, not the payload.
                // Multiple packets in one TCP segment require individual header decryption.
                _rawReceiveBuffer.Write(data.Span);

                while (true)
                {
                    var bufferArray = _rawReceiveBuffer.ToArray();
                    long bufferLen = _rawReceiveBuffer.Length;

                    // Step 1: Decrypt header if we haven't yet
                    if (_pendingDecryptedHeader == null)
                    {
                        if (bufferLen < ServerHeaderSize)
                            break;

                        // Extract and decrypt only the 4-byte header
                        var rawHeader = new byte[ServerHeaderSize];
                        Array.Copy(bufferArray, 0, rawHeader, 0, ServerHeaderSize);
                        var decryptedHeaderMem = Encryptor.Decrypt(new ReadOnlyMemory<byte>(rawHeader));
                        _pendingDecryptedHeader = decryptedHeaderMem.ToArray();
                    }

                    // Step 2: Read size from decrypted header and check if full message available
                    ushort size = (ushort)((_pendingDecryptedHeader[0] << 8) | _pendingDecryptedHeader[1]);
                    int totalMessageSize = size + 2; // size field doesn't include itself

                    if (bufferLen < totalMessageSize)
                        break; // Wait for more data (header already decrypted, cached)

                    // Step 3: Extract complete message with decrypted header + cleartext payload
                    var message = new byte[totalMessageSize];
                    _pendingDecryptedHeader.CopyTo(message, 0);
                    if (totalMessageSize > ServerHeaderSize)
                        Array.Copy(bufferArray, ServerHeaderSize, message, ServerHeaderSize, totalMessageSize - ServerHeaderSize);

                    // Step 4: Decode and route
                    if (_codec.TryDecode(new ReadOnlyMemory<byte>(message), out var opcode, out var payload))
                    {
                        await _router.RouteAsync(opcode, payload);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to decode packet of {message.Length} bytes (size={size})");
                    }

                    // Step 5: Remove processed bytes from raw buffer
                    int remaining = (int)(bufferLen - totalMessageSize);
                    _rawReceiveBuffer.SetLength(0);
                    if (remaining > 0)
                        _rawReceiveBuffer.Write(bufferArray, totalMessageSize, remaining);

                    _pendingDecryptedHeader = null;
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
                _rawReceiveBuffer.Dispose();

                if (_framer is IDisposable disposableFramer)
                    disposableFramer.Dispose();

                _disposed = true;
            }
        }
    }
}