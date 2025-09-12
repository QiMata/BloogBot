using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Networking.I;
using System.IO;
using System.Text;

namespace WoWSharpClient.Client
{
    /// <summary>
    /// High-level world server client that composes the networking stack.
    /// Handles the world server protocol without direct socket management.
    /// </summary>
    public sealed class WorldClient : IWorldClient
    {
        private readonly PacketPipeline<Opcode> _pipeline;
        private readonly IConnection _connection;
        private IEncryptor _encryptor;
        private bool _disposed;
        private uint _lastPingTime;
        
        // Authentication state
        private string _username = string.Empty;
        private byte[] _sessionKey = [];
        private bool _isAuthenticated = false;

        public WorldClient(
            IConnection connection,
            IMessageFramer framer,
            IEncryptor encryptor,
            IPacketCodec<Opcode> codec,
            IMessageRouter<Opcode> router)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            
            _pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);
            
            RegisterWorldHandlers();
        }

        public bool IsConnected => _pipeline.IsConnected;
        public bool IsAuthenticated => _isAuthenticated;

        public void RegisterOpcodeHandler(Opcode opcode, Func<byte[], Task> handler)
            => _pipeline.RegisterHandler(opcode, payload => handler(payload.ToArray()));

        public async Task ConnectAsync(string username, string host, byte[] sessionKey, int port = 8085, CancellationToken cancellationToken = default)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _sessionKey = sessionKey ?? throw new ArgumentNullException(nameof(sessionKey));
            _isAuthenticated = false;

            Console.WriteLine($"[NewWorldClient] Connecting to world server {host}:{port} for user '{username}'");
            
            await _pipeline.ConnectAsync(host, port, cancellationToken);
            
            Console.WriteLine("[NewWorldClient] Connected to world server. Waiting for SMSG_AUTH_CHALLENGE...");
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _isAuthenticated = false;
            _username = string.Empty;
            _sessionKey = [];
            
            Console.WriteLine("[NewWorldClient] Disconnecting from world server");
            return _pipeline.DisconnectAsync(cancellationToken);
        }

        public async Task SendCharEnumAsync(CancellationToken cancellationToken = default)
        {
            if (!_isAuthenticated)
            {
                Console.WriteLine("[NewWorldClient] Cannot send character enum - not authenticated");
                return;
            }

            await _pipeline.SendAsync(Opcode.CMSG_CHAR_ENUM, ReadOnlyMemory<byte>.Empty, cancellationToken);
        }

        public async Task SendPingAsync(uint sequence, CancellationToken cancellationToken = default)
        {
            var payload = new byte[8];
            BitConverter.GetBytes(sequence).CopyTo(payload, 0);
            BitConverter.GetBytes(0u).CopyTo(payload, 4);
            await _pipeline.SendAsync(Opcode.CMSG_PING, payload, cancellationToken);
        }

        public async Task SendQueryTimeAsync(CancellationToken cancellationToken = default)
            => await _pipeline.SendAsync(Opcode.CMSG_QUERY_TIME, ReadOnlyMemory<byte>.Empty, cancellationToken);

        public async Task SendPlayerLoginAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            if (!_isAuthenticated)
            {
                Console.WriteLine("[NewWorldClient] Cannot send player login - not authenticated");
                return;
            }

            var payload = new byte[8];
            BitConverter.GetBytes(guid).CopyTo(payload, 0);
            await _pipeline.SendAsync(Opcode.CMSG_PLAYER_LOGIN, payload, cancellationToken);
        }

        public async Task SendChatMessageAsync(ChatMsg type, Language language, string destinationName, string message, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
            
            writer.Write((uint)type);
            writer.Write((uint)language);

            if (type == ChatMsg.CHAT_MSG_WHISPER || type == ChatMsg.CHAT_MSG_CHANNEL)
            {
                writer.Write(Encoding.UTF8.GetBytes(destinationName));
                writer.Write((byte)0);
            }

            writer.Write(Encoding.UTF8.GetBytes(message));
            writer.Write((byte)0);

            writer.Flush();
            await _pipeline.SendAsync(Opcode.CMSG_MESSAGECHAT, ms.ToArray(), cancellationToken);
        }

        public async Task SendNameQueryAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            var payload = new byte[8];
            BitConverter.GetBytes(guid).CopyTo(payload, 0);
            await _pipeline.SendAsync(Opcode.CMSG_NAME_QUERY, payload, cancellationToken);
        }

        public async Task SendMoveWorldPortAckAsync(CancellationToken cancellationToken = default)
            => await _pipeline.SendAsync(Opcode.MSG_MOVE_WORLDPORT_ACK, ReadOnlyMemory<byte>.Empty, cancellationToken);

        public async Task SendSetActiveMoverAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            var payload = new byte[8];
            BitConverter.GetBytes(guid).CopyTo(payload, 0);
            await _pipeline.SendAsync(Opcode.CMSG_SET_ACTIVE_MOVER, payload, cancellationToken);
        }

        public async Task SendOpcodeAsync(Opcode opcode, byte[] payload, CancellationToken cancellationToken = default)
            => await _pipeline.SendAsync(opcode, payload, cancellationToken);

        public void UpdateEncryptor(IEncryptor newEncryptor)
        {
            _encryptor = newEncryptor ?? throw new ArgumentNullException(nameof(newEncryptor));
            _pipeline.Encryptor = newEncryptor;
            Console.WriteLine($"[NewWorldClient] Updated encryptor to {newEncryptor.GetType().Name}");
        }

        private void RegisterWorldHandlers()
        {
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_CHALLENGE, HandleAuthChallenge);
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_RESPONSE, HandleAuthResponse);
            _pipeline.RegisterHandler(Opcode.SMSG_CHAR_ENUM, HandleCharEnum);
            _pipeline.RegisterHandler(Opcode.SMSG_PONG, HandlePong);
            _pipeline.RegisterHandler(Opcode.SMSG_QUERY_TIME_RESPONSE, HandleQueryTimeResponse);
            _pipeline.RegisterHandler(Opcode.SMSG_LOGIN_VERIFY_WORLD, HandleLoginVerifyWorld);
            _pipeline.RegisterHandler(Opcode.SMSG_NEW_WORLD, HandleNewWorld);
            _pipeline.RegisterHandler(Opcode.SMSG_TRANSFER_PENDING, HandleTransferPending);
            _pipeline.RegisterHandler(Opcode.SMSG_CHARACTER_LOGIN_FAILED, HandleCharacterLoginFailed);
            _pipeline.RegisterHandler(Opcode.SMSG_MESSAGECHAT, HandleMessageChat);
            _pipeline.RegisterHandler(Opcode.SMSG_NAME_QUERY_RESPONSE, HandleNameQueryResponse);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSTART, HandleAttackStart);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSTOP, HandleAttackStop);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_NOTINRANGE, HandleAttackSwingNotInRange);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_BADFACING, HandleAttackSwingBadFacing);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_NOTSTANDING, HandleAttackSwingNotStanding);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_DEADTARGET, HandleAttackSwingDeadTarget);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_CANT_ATTACK, HandleAttackSwingCantAttack);
        }

        public event Action? Connected
        {
            add { _pipeline.WhenConnected.Subscribe(_ => value?.Invoke()); }
            remove { }
        }

        public event Action<Exception?>? Disconnected
        {
            add { _pipeline.WhenDisconnected.Subscribe(ex => value?.Invoke(ex)); }
            remove { }
        }

        /// <summary>
        /// Fired when world server authentication succeeds.
        /// </summary>
        public event Action? OnAuthenticationSuccessful;

        /// <summary>
        /// Fired when world server authentication fails.
        /// </summary>
        /// <param name="errorCode">The authentication error code.</param>
        public event Action<byte>? OnAuthenticationFailed;

        /// <summary>
        /// Fired when a character is found during character enumeration.
        /// </summary>
        /// <param name="guid">Character GUID.</param>
        /// <param name="name">Character name.</param>
        /// <param name="race">Character race.</param>
        /// <param name="characterClass">Character class.</param>
        /// <param name="gender">Character gender.</param>
        public event Action<ulong, string, byte, byte, byte>? OnCharacterFound;

        /// <summary>
        /// Fired when attack state changes (start/stop).
        /// </summary>
        /// <param name="isAttacking">Whether attacking started or stopped.</param>
        /// <param name="attackerGuid">The attacker's GUID.</param>
        /// <param name="victimGuid">The victim's GUID.</param>
        public event Action<bool, ulong, ulong>? OnAttackStateChanged;

        /// <summary>
        /// Fired when an attack error occurs (not in range, bad facing, etc.).
        /// </summary>
        /// <param name="errorMessage">The error message describing why the attack failed.</param>
        public event Action<string>? OnAttackError;

        // --- Handlers ---
        private async Task HandleAuthChallenge(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_AUTH_CHALLENGE ({payload.Length} bytes)");
            if (payload.Length < 4)
            {
                Console.WriteLine("[NewWorldClient] Incomplete SMSG_AUTH_CHALLENGE packet.");
                return;
            }

            var serverSeed = payload.Slice(0, 4).ToArray();
            try
            {
                await SendAuthSessionAsync(serverSeed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewWorldClient] Error sending CMSG_AUTH_SESSION: {ex}");
            }
        }

        private async Task SendAuthSessionAsync(byte[] serverSeed)
        {
            if (string.IsNullOrEmpty(_username) || _sessionKey.Length == 0)
            {
                Console.WriteLine("[NewWorldClient] Cannot send auth session - missing username or session key");
                return;
            }

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream, Encoding.UTF8, true);

            const uint build = 5875; // 1.12.1
            const uint serverId = 1;

            var random = new Random();
            uint clientSeed = (uint)random.Next(int.MinValue, int.MaxValue);

            byte[] clientProof = PacketManager.GenerateClientProof(_username, clientSeed, serverSeed, _sessionKey);
            byte[] decompressedAddonInfo = PacketManager.GenerateAddonInfo();
            byte[] compressedAddonInfo = PacketManager.Compress(decompressedAddonInfo);
            uint decompressedAddonInfoSize = (uint)decompressedAddonInfo.Length;

            writer.Write(build);
            writer.Write(serverId);
            writer.Write(Encoding.UTF8.GetBytes(_username));
            writer.Write((byte)0);
            writer.Write(clientSeed);
            writer.Write(clientProof);
            writer.Write(decompressedAddonInfoSize);
            writer.Write(compressedAddonInfo);

            writer.Flush();
            byte[] payload = memoryStream.ToArray();

            Console.WriteLine($"[NewWorldClient] -> CMSG_AUTH_SESSION [{payload.Length} bytes] for user '{_username}'");

            await _pipeline.SendAsync(Opcode.CMSG_AUTH_SESSION, payload);
        }

        private Task HandleAuthResponse(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_AUTH_RESPONSE ({payload.Length} bytes)");

            if (payload.Length > 0)
            {
                var result = payload.Span[0];
                if (result == 0x0C) // AUTH_OK
                {
                    _isAuthenticated = true;
                    Console.WriteLine("[NewWorldClient] World authentication successful!");
                    OnAuthenticationSuccessful?.Invoke();
                }
                else
                {
                    _isAuthenticated = false;
                    Console.WriteLine($"[NewWorldClient] World authentication failed with code: {result:X2}");
                    OnAuthenticationFailed?.Invoke(result);
                }
            }
            else
            {
                _isAuthenticated = false;
                Console.WriteLine("[NewWorldClient] Received empty SMSG_AUTH_RESPONSE");
                OnAuthenticationFailed?.Invoke(0xFF);
            }

            return Task.CompletedTask;
        }

        // --- Minimal handler stubs for the rest ---
        private Task HandleCharEnum(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandlePong(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleQueryTimeResponse(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleLoginVerifyWorld(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleNewWorld(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleTransferPending(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleCharacterLoginFailed(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleMessageChat(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleNameQueryResponse(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackStart(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackStop(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackSwingNotInRange(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackSwingBadFacing(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackSwingNotStanding(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackSwingDeadTarget(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleAttackSwingCantAttack(ReadOnlyMemory<byte> payload) => Task.CompletedTask;

        public void Dispose()
        {
            if (!_disposed)
            {
                _pipeline?.Dispose();
                _disposed = true;
            }
        }
    }
}