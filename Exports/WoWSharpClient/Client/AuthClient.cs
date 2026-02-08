using System.Net;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Networking.I;
using System.Text;
using System.Security.Cryptography;
using WowSrp.Client;
using Org.BouncyCastle.Utilities;

namespace WoWSharpClient.Client
{
    /// <summary>
    /// High-level authentication client that composes the networking stack.
    /// Handles the auth server protocol without direct socket management.
    /// </summary>
    public sealed class AuthClient : IDisposable
    {
        private readonly PacketPipeline<Opcode> _pipeline;
        private readonly IConnection _connection;
        private readonly IEncryptor _encryptor;
        
        private string _username = string.Empty;
        private string _password = string.Empty;
        private SrpClient? _srpClient;
        private SrpClientChallenge? _srpClientChallenge;
        private byte[] _serverProof = [];
        private bool _disposed;

        // For handling async realm list requests
        private TaskCompletionSource<List<Realm>>? _realmListCompletionSource;

        // For waiting on the full SRP6 handshake to complete
        private TaskCompletionSource<bool>? _loginCompletionSource;

        // TCP receive buffer for auth protocol (handles fragmentation)
        private readonly List<byte> _authBuffer = new();
        private readonly object _bufferLock = new();

        /// <summary>
        /// Initializes a new instance of the AuthClient class.
        /// </summary>
        /// <param name="connection">The connection implementation.</param>
        /// <param name="framer">The message framer for auth protocol.</param>
        /// <param name="encryptor">The encryptor (typically NoEncryption for auth).</param>
        /// <param name="codec">The packet codec for auth protocol.</param>
        /// <param name="router">The message router for auth protocol.</param>
        public AuthClient(
            IConnection connection,
            IMessageFramer framer,
            IEncryptor encryptor,
            IPacketCodec<Opcode> codec,
            IMessageRouter<Opcode> router)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            
            _pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
            
            RegisterAuthHandlers();
        }

        /// <summary>
        /// Gets the current username.
        /// </summary>
        public string Username => _username;

        /// <summary>
        /// Gets the server IP address.
        /// </summary>
        public IPAddress? IPAddress { get; private set; }

        /// <summary>
        /// Gets the server proof bytes.
        /// </summary>
        public byte[] ServerProof => _serverProof;

        /// <summary>
        /// Gets the session key for world server authentication.
        /// </summary>
        public byte[] SessionKey => _srpClient?.SessionKey ?? [];

        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        public bool IsConnected => _pipeline.IsConnected;

        /// <summary>
        /// Reactive observable for connection established.
        /// </summary>
        public IObservable<System.Reactive.Unit> WhenConnected => _pipeline.WhenConnected;

        /// <summary>
        /// Reactive observable for disconnection events. Exception is null for graceful disconnects.
        /// </summary>
        public IObservable<Exception?> WhenDisconnected => _pipeline.WhenDisconnected;

        /// <summary>
        /// Connects to the authentication server.
        /// </summary>
        /// <param name="host">The hostname or IP address.</param>
        /// <param name="port">The port number (default 3724).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ConnectAsync(string host, int port = 3724, CancellationToken cancellationToken = default)
        {
            if (IPAddress.TryParse(host, out var ipAddress))
            {
                IPAddress = ipAddress;
            }
            else
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                IPAddress = addresses.Length > 0 ? addresses[0] : null;
            }

            await _pipeline.ConnectAsync(host, port, cancellationToken);
            WoWSharpEventEmitter.Instance.FireOnLoginConnect();
        }

        /// <summary>
        /// Disconnects from the authentication server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            return _pipeline.DisconnectAsync(cancellationToken);
        }

        /// <summary>
        /// Performs the login sequence with the provided credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            _username = username;
            _password = password;

            // Set up a TCS to wait for the full SRP6 handshake (challenge + proof)
            var loginTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _loginCompletionSource = loginTcs;

            // Create CMD_AUTH_LOGON_CHALLENGE packet
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream, Encoding.UTF8, true);

            int packetSize = 30 + _username.Length;

            writer.Write((byte)0x00); // Opcode: CMD_AUTH_LOGON_CHALLENGE
            writer.Write((byte)0x03); // Protocol Version: 3
            writer.Write((ushort)packetSize); // Packet Size (little-endian)

            writer.Write(Encoding.UTF8.GetBytes("WoW\0")); // Game Name: "WoW\0"
            writer.Write((byte)0x01); // Major Version: 1
            writer.Write((byte)0x0C); // Minor Version: 12
            writer.Write((byte)0x01); // Patch Version: 1
            writer.Write((ushort)0x16F3); // Build: 5875 (little-endian)

            writer.Write(Encoding.UTF8.GetBytes("68x\0")); // Platform: "68x\0"
            writer.Write(Encoding.UTF8.GetBytes("niW\0")); // OS: "niW\0"
            writer.Write(Encoding.UTF8.GetBytes("BGne")); // Locale: "enGB"

            writer.Write((uint)60); // Timezone Bias
            writer.Write((uint)0x0100007F); // Client IP

            writer.Write((byte)_username.Length); // Username length
            writer.Write(Encoding.UTF8.GetBytes(_username.ToUpper()));

            writer.Flush();
            byte[] packetData = memoryStream.ToArray();

            Console.WriteLine($"[AuthClient] -> CMD_AUTH_LOGON_CHALLENGE [{packetData.Length}] hex={BitConverter.ToString(packetData)}");
            WoWSharpEventEmitter.Instance.FireOnHandshakeBegin();

            // For auth server, we send raw data directly since it doesn't use standard opcode format
            await _connection.SendAsync(packetData, cancellationToken);

            // Wait for the full SRP6 handshake to complete (proof verified)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                var proofResult = await loginTcs.Task.WaitAsync(timeoutCts.Token);
                if (!proofResult)
                {
                    throw new InvalidOperationException("[AuthClient] SRP6 proof failed - server rejected credentials");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AuthClient] Login handshake timed out");
                throw;
            }
            finally
            {
                _loginCompletionSource = null;
            }
        }

        /// <summary>
        /// Requests the realm list from the authentication server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available realms.</returns>
        public async Task<List<Realm>> GetRealmListAsync(CancellationToken cancellationToken = default)
        {
            // Use TaskCompletionSource to wait for the realm list response
            var realmListTcs = new TaskCompletionSource<List<Realm>>();
            
            // Store the TCS for the response handler to complete
            _realmListCompletionSource = realmListTcs;

            // Create CMD_REALM_LIST packet
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream, Encoding.UTF8, true);
            
            writer.Write((byte)0x10);   // CMD_REALM_LIST opcode
            writer.Write((uint)0x00);   // Padding (unused, always 0)

            writer.Flush();
            byte[] packetData = memoryStream.ToArray();

            Console.WriteLine($"[AuthClient] -> CMD_REALM_LIST ({packetData.Length} bytes) hex={BitConverter.ToString(packetData)}");

            // For auth server, we send raw data directly
            await _connection.SendAsync(packetData, cancellationToken);

            // Wait for the response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout

            try
            {
                return await realmListTcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AuthClient] Realm list request timed out");
                return [];
            }
            finally
            {
                _realmListCompletionSource = null;
            }
        }

        /// <summary>
        /// Registers handlers for all expected authentication opcodes.
        /// </summary>
        private void RegisterAuthHandlers()
        {
            // Note: Auth server uses different packet format, so these handlers 
            // might need special handling or custom framing
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_CHALLENGE, HandleAuthChallenge);
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_RESPONSE, HandleAuthResponse);

            // Use ReceivedBytes observable instead of BytesReceived event
            _connection.ReceivedBytes.Subscribe(HandleRawAuthPackets);
        }

        private async void HandleRawAuthPackets(ReadOnlyMemory<byte> data)
        {
            try
            {
                // Auth protocol doesn't use standard WoW framing.
                // TCP doesn't guarantee packet boundaries, so we buffer data
                // and extract complete packets by opcode + known/declared size.
                var bytes = data.ToArray();
                Console.WriteLine($"[AuthClient] RAW_RECV [{bytes.Length}] first8={BitConverter.ToString(bytes.Take(Math.Min(8, bytes.Length)).ToArray())}");
                lock (_bufferLock)
                {
                    _authBuffer.AddRange(bytes);
                }

                await DrainAuthBuffer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthClient] Error handling raw auth packet: {ex}");
            }
        }

        private async Task DrainAuthBuffer()
        {
            while (true)
            {
                byte[] snapshot;
                lock (_bufferLock)
                {
                    if (_authBuffer.Count == 0) return;
                    snapshot = _authBuffer.ToArray();
                }

                byte opcode = snapshot[0];
                int consumed = 0;

                switch (opcode)
                {
                    case 0x00: // CMD_AUTH_LOGON_CHALLENGE response
                    {
                        // opcode(1) + unk(1) + result(1) = 3 bytes minimum
                        if (snapshot.Length < 3) return;
                        byte result = snapshot[2];
                        if (result != 0x00) // Not SUCCESS — short error packet (3 bytes)
                        {
                            consumed = 3;
                            await HandleAuthLogonChallengeResponse(snapshot.AsMemory(0, consumed));
                        }
                        else
                        {
                            // Success: fixed 119 bytes
                            if (snapshot.Length < 119) return; // Wait for more data
                            consumed = 119;
                            await HandleAuthLogonChallengeResponse(snapshot.AsMemory(0, consumed));
                        }
                        break;
                    }
                    case 0x01: // CMD_AUTH_LOGON_PROOF response
                    {
                        if (snapshot.Length < 2) return;
                        byte result = snapshot[1];
                        if (result != 0x00) // Error — opcode(1) + error(1) + unk(2) = 4 bytes
                        {
                            int errorLen = Math.Min(4, snapshot.Length);
                            consumed = errorLen;
                            await HandleAuthLogonProofResponse(snapshot.AsMemory(0, consumed));
                        }
                        else
                        {
                            // Success: opcode(1) + error(1) + M2(20) + accountFlags(4) = 26 bytes
                            if (snapshot.Length < 26) return;
                            consumed = 26;
                            await HandleAuthLogonProofResponse(snapshot.AsMemory(0, consumed));
                        }
                        break;
                    }
                    case 0x10: // CMD_REALM_LIST response
                    {
                        // opcode(1) + size(2 LE) then size bytes of body
                        if (snapshot.Length < 3) return;
                        ushort bodySize = BitConverter.ToUInt16(snapshot, 1);
                        int totalSize = 3 + bodySize;
                        if (snapshot.Length < totalSize) return; // Wait for more data
                        consumed = totalSize;
                        await HandleRealmListResponse(snapshot.AsMemory(0, consumed));
                        break;
                    }
                    default:
                        Console.WriteLine($"[AuthClient] Unknown auth opcode: 0x{opcode:X2}, buffer size: {snapshot.Length}");
                        // Discard one byte and retry
                        consumed = 1;
                        break;
                }

                if (consumed > 0)
                {
                    lock (_bufferLock)
                    {
                        _authBuffer.RemoveRange(0, consumed);
                    }
                }
                else
                {
                    return; // Need more data
                }
            }
        }

        private async Task HandleAuthLogonChallengeResponse(ReadOnlyMemory<byte> data)
        {
            var packet = data.ToArray();
            Console.WriteLine($"[AuthClient] <- CMD_AUTH_LOGON_CHALLENGE [{packet.Length}] first16={BitConverter.ToString(packet.Take(Math.Min(16, packet.Length)).ToArray())}");

            byte opcode = packet[0];
            ResponseCode result = (ResponseCode)packet[2];

            if (opcode == 0x00 && result == ResponseCode.RESPONSE_SUCCESS)
            {
                byte[] serverPublicKey = packet.Skip(3).Take(32).ToArray();
                byte generator = packet[36];
                byte largeSafePrimeLength = packet[37];
                byte[] largeSafePrime = packet.Skip(38).Take(largeSafePrimeLength).ToArray();
                byte[] salt = packet.Skip(70).Take(32).ToArray();
                byte[] crcSalt = packet.Skip(102).Take(16).ToArray();

                await SendLogonProof(serverPublicKey, generator, largeSafePrime, salt, crcSalt);
            }
            else
            {
                Console.WriteLine($"[AuthClient] AUTH_CHALLENGE FAILED: opcode {opcode:X2}, result {result} (0x{(byte)result:X2})");
                WoWSharpEventEmitter.Instance.FireOnLoginFailure();
                _loginCompletionSource?.TrySetResult(false);
            }
        }

        private async Task SendLogonProof(byte[] serverPublicKey, byte generator, byte[] largeSafePrime, byte[] salt, byte[] crcSalt)
        {
            try
            {
                var challenge = new SrpClientChallenge(_username, _password, generator, largeSafePrime, serverPublicKey, salt);
                _srpClientChallenge = challenge;

                byte[] clientPublicKey = challenge.ClientPublicKey;
                byte[] clientProof = challenge.ClientProof;
                
                byte[] crcHash = SHA1.HashData(Arrays.Concatenate(crcSalt, clientProof));

                using var memoryStream = new MemoryStream();
                using var writer = new BinaryWriter(memoryStream, Encoding.UTF8, true);
                
                writer.Write((byte)0x01); // Opcode: CMD_AUTH_LOGON_PROOF
                writer.Write(clientPublicKey);
                writer.Write(clientProof);
                writer.Write(crcHash);
                writer.Write((byte)0x00); // Num keys
                writer.Write((byte)0x00); // 2FA disabled

                writer.Flush();
                byte[] packetData = memoryStream.ToArray();

                Console.WriteLine($"[AuthClient] -> CMD_AUTH_LOGON_PROOF [{packetData.Length}] hex={BitConverter.ToString(packetData)}");

                await _connection.SendAsync(packetData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthClient] Error in SendLogonProof: {ex}");
            }
        }

        private async Task HandleAuthLogonProofResponse(ReadOnlyMemory<byte> data)
        {
            var packet = data.ToArray();
            Console.WriteLine($"[AuthClient] <- CMD_AUTH_LOGON_PROOF [{packet.Length}] hex={BitConverter.ToString(packet)}");

            if (packet.Length < 2)
                return;

            byte opcode = packet[0];
            ResponseCode result = (ResponseCode)packet[1];

            if (opcode == 0x01 && result == ResponseCode.RESPONSE_SUCCESS)
            {
                if (packet.Length >= 26)
                {
                    byte[] serverProof = packet.Skip(2).Take(20).ToArray();
                    _serverProof = serverProof;

                    var verificationResult = _srpClientChallenge?.VerifyServerProof(serverProof);
                    if (!verificationResult.HasValue)
                    {
                        WoWSharpEventEmitter.Instance.FireOnLoginFailure();
                        _loginCompletionSource?.TrySetResult(false);
                    }
                    else
                    {
                        _srpClient = verificationResult.Value;
                        WoWSharpEventEmitter.Instance.FireOnLoginSuccess();
                        _loginCompletionSource?.TrySetResult(true);
                    }
                }
            }
            else
            {
                Console.WriteLine($"[AuthClient] Failed AUTH_PROOF response: opcode {opcode:X2}, result {result}");
                WoWSharpEventEmitter.Instance.FireOnLoginFailure();
                _loginCompletionSource?.TrySetResult(false);
            }

            await Task.CompletedTask;
        }

        private async Task HandleAuthChallenge(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[AuthClient] Received SMSG_AUTH_CHALLENGE ({payload.Length} bytes)");
            // This would be for world server auth challenge, not auth server
            await Task.CompletedTask;
        }

        private async Task HandleAuthResponse(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[AuthClient] Received SMSG_AUTH_RESPONSE ({payload.Length} bytes)");
            // This would be for world server auth response, not auth server
            await Task.CompletedTask;
        }

        private async Task HandleRealmListResponse(ReadOnlyMemory<byte> data)
        {
            try
            {
                var packet = data.ToArray();
                Console.WriteLine($"[AuthClient] <- CMD_REALM_LIST [{packet.Length}]");

                var realms = new List<Realm>();
                
                if (packet.Length >= 8)
                {
                    byte opcode = packet[0];
                    uint size = BitConverter.ToUInt16(packet, 1);
                    // Vanilla 1.12.1: cmd(1) + size(2) + padding(4) + numRealms(1) = 8 bytes header
                    uint numOfRealms = packet[7]; // uint8 at offset 7

                    if (opcode == 0x10 && packet.Length >= 8) // REALM_LIST
                    {
                        using var bodyStream = new MemoryStream(packet, 8, packet.Length - 8);
                        using var bodyReader = new BinaryReader(bodyStream, Encoding.UTF8, true);
                        
                        for (int i = 0; i < numOfRealms && bodyStream.Position < bodyStream.Length; i++)
                        {
                            try
                            {
                                var realm = new Realm()
                                {
                                    RealmType = bodyReader.ReadUInt32(),
                                    Flags = bodyReader.ReadByte(),
                                    RealmName = PacketManager.ReadCString(bodyReader),
                                };
                                
                                var addressPort = PacketManager.ReadCString(bodyReader);
                                if (addressPort.Contains(':'))
                                {
                                    realm.AddressPort = int.Parse(addressPort.Split(':')[1]);
                                }

                                realm.Population = bodyReader.ReadSingle();
                                realm.NumChars = bodyReader.ReadByte();
                                realm.RealmCategory = bodyReader.ReadByte();
                                realm.RealmId = bodyReader.ReadByte();

                                realms.Add(realm);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[AuthClient] Error parsing realm {i}: {ex}");
                                break;
                            }
                        }
                    }
                }

                // Complete the TaskCompletionSource if waiting
                _realmListCompletionSource?.TrySetResult(realms);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthClient] Error handling realm list response: {ex}");
                _realmListCompletionSource?.TrySetResult([]);
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // No event to detach; pipeline and connection will be disposed by owners as needed
                _pipeline?.Dispose();
                _disposed = true;
            }
        }
    }
}