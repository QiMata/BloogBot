using System;
using System.Collections.Concurrent;
using System.Net;
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

        /// <summary>
        /// Initializes a new instance of the NewWorldClient class.
        /// </summary>
        /// <param name="connection">The connection implementation.</param>
        /// <param name="framer">The message framer for world protocol.</param>
        /// <param name="encryptor">The encryptor (initially NoEncryption, then RC4 after auth).</param>
        /// <param name="codec">The packet codec for world protocol.</param>
        /// <param name="router">The message router for world protocol.</param>
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

        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        public bool IsConnected => _pipeline.IsConnected;

        /// <summary>
        /// Gets a value indicating whether the client is authenticated with the world server.
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// Connects to the world server and stores authentication details for later use.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="host">The hostname or IP address.</param>
        /// <param name="sessionKey">The session key from authentication server.</param>
        /// <param name="port">The port number (default 8085).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ConnectAsync(string username, string host, byte[] sessionKey, int port = 8085, CancellationToken cancellationToken = default)
        {
            // Store authentication details for responding to the server's auth challenge
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _sessionKey = sessionKey ?? throw new ArgumentNullException(nameof(sessionKey));
            _isAuthenticated = false;

            Console.WriteLine($"[NewWorldClient] Connecting to world server {host}:{port} for user '{username}'");
            
            await _pipeline.ConnectAsync(host, port, cancellationToken);
            
            Console.WriteLine("[NewWorldClient] Connected to world server. Waiting for SMSG_AUTH_CHALLENGE...");
        }

        /// <summary>
        /// Disconnects from the world server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            // Reset authentication state
            _isAuthenticated = false;
            _username = string.Empty;
            _sessionKey = [];
            
            Console.WriteLine("[NewWorldClient] Disconnecting from world server");
            return _pipeline.DisconnectAsync(cancellationToken);
        }

        /// <summary>
        /// Sends a character enumeration request.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendCharEnumAsync(CancellationToken cancellationToken = default)
        {
            if (!_isAuthenticated)
            {
                Console.WriteLine("[NewWorldClient] Cannot send character enum - not authenticated");
                return;
            }

            // Character enum has no payload
            await _pipeline.SendAsync(Opcode.CMSG_CHAR_ENUM, ReadOnlyMemory<byte>.Empty, cancellationToken);
        }

        /// <summary>
        /// Sends a ping packet to the server.
        /// </summary>
        /// <param name="sequence">The ping sequence number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendPingAsync(uint sequence, CancellationToken cancellationToken = default)
        {
            var payload = new byte[8];
            BitConverter.GetBytes(sequence).CopyTo(payload, 0);
            BitConverter.GetBytes(0u).CopyTo(payload, 4); // latency (0 for now)

            await _pipeline.SendAsync(Opcode.CMSG_PING, payload, cancellationToken);
        }

        /// <summary>
        /// Sends a time query request to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendQueryTimeAsync(CancellationToken cancellationToken = default)
        {
            await _pipeline.SendAsync(Opcode.CMSG_QUERY_TIME, ReadOnlyMemory<byte>.Empty, cancellationToken);
        }

        /// <summary>
        /// Sends a player login request for the specified character GUID.
        /// </summary>
        /// <param name="guid">The character GUID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
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

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        /// <param name="type">The chat message type.</param>
        /// <param name="language">The language of the message.</param>
        /// <param name="destinationName">The destination (for whispers/channels).</param>
        /// <param name="message">The message text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendChatMessageAsync(ChatMsg type, Language language, string destinationName, string message, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
            
            writer.Write((uint)type);
            writer.Write((uint)language);

            if (type == ChatMsg.CHAT_MSG_WHISPER || type == ChatMsg.CHAT_MSG_CHANNEL)
            {
                writer.Write(Encoding.UTF8.GetBytes(destinationName));
                writer.Write((byte)0); // null terminator
            }

            writer.Write(Encoding.UTF8.GetBytes(message));
            writer.Write((byte)0); // null terminator

            writer.Flush();
            await _pipeline.SendAsync(Opcode.CMSG_MESSAGECHAT, ms.ToArray(), cancellationToken);
        }

        /// <summary>
        /// Sends a name query for the specified GUID.
        /// </summary>
        /// <param name="guid">The GUID to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendNameQueryAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            var payload = new byte[8];
            BitConverter.GetBytes(guid).CopyTo(payload, 0);

            await _pipeline.SendAsync(Opcode.CMSG_NAME_QUERY, payload, cancellationToken);
        }

        /// <summary>
        /// Sends a move world port acknowledge.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendMoveWorldPortAckAsync(CancellationToken cancellationToken = default)
        {
            await _pipeline.SendAsync(Opcode.MSG_MOVE_WORLDPORT_ACK, ReadOnlyMemory<byte>.Empty, cancellationToken);
        }

        /// <summary>
        /// Sends a set active mover packet.
        /// </summary>
        /// <param name="guid">The mover GUID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendSetActiveMoverAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            var payload = new byte[8];
            BitConverter.GetBytes(guid).CopyTo(payload, 0);

            await _pipeline.SendAsync(Opcode.CMSG_SET_ACTIVE_MOVER, payload, cancellationToken);
        }

        /// <summary>
        /// Sends a movement packet with the specified opcode and movement data.
        /// </summary>
        /// <param name="opcode">The movement opcode.</param>
        /// <param name="movementInfo">The movement information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendMovementAsync(Opcode opcode, byte[] movementInfo, CancellationToken cancellationToken = default)
        {
            await _pipeline.SendAsync(opcode, movementInfo, cancellationToken);
        }

        /// <summary>
        /// Updates the encryptor (typically switching from NoEncryption to RC4 after authentication).
        /// </summary>
        /// <param name="newEncryptor">The new encryptor to use.</param>
        public void UpdateEncryptor(IEncryptor newEncryptor)
        {
            _encryptor = newEncryptor ?? throw new ArgumentNullException(nameof(newEncryptor));
            
            // Update the pipeline's encryptor using the property
            _pipeline.Encryptor = newEncryptor;
            
            Console.WriteLine($"[NewWorldClient] Updated encryptor to {newEncryptor.GetType().Name}");
        }

        /// <summary>
        /// Registers handlers for all expected world server opcodes.
        /// </summary>
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
            
            // Targeting and combat related handlers
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSTART, HandleAttackStart);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSTOP, HandleAttackStop);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_NOTINRANGE, HandleAttackSwingNotInRange);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_BADFACING, HandleAttackSwingBadFacing);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_NOTSTANDING, HandleAttackSwingNotStanding);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_DEADTARGET, HandleAttackSwingDeadTarget);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_CANT_ATTACK, HandleAttackSwingCantAttack);
            
            // Add more handlers as needed
        }

        private async Task HandleAuthChallenge(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_AUTH_CHALLENGE ({payload.Length} bytes)");
            
            if (payload.Length < 4)
            {
                Console.WriteLine($"[NewWorldClient] Incomplete SMSG_AUTH_CHALLENGE packet.");
                return;
            }

            // Extract server seed from payload (first 4 bytes)
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

        /// <summary>
        /// Sends CMSG_AUTH_SESSION packet to authenticate with the world server.
        /// </summary>
        /// <param name="serverSeed">The server seed from SMSG_AUTH_CHALLENGE.</param>
        private async Task SendAuthSessionAsync(byte[] serverSeed)
        {
            if (string.IsNullOrEmpty(_username) || _sessionKey.Length == 0)
            {
                Console.WriteLine("[NewWorldClient] Cannot send auth session - missing username or session key");
                return;
            }

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream, Encoding.UTF8, true);

            // World of Warcraft client build version (1.12.1)
            const uint build = 5875;
            const uint serverId = 1;
            
            // Generate random client seed
            var random = new Random();
            uint clientSeed = (uint)random.Next(int.MinValue, int.MaxValue);

            // Generate client proof using session key and seeds
            byte[] clientProof = PacketManager.GenerateClientProof(_username, clientSeed, serverSeed, _sessionKey);
            
            // Generate addon information
            byte[] decompressedAddonInfo = PacketManager.GenerateAddonInfo();
            byte[] compressedAddonInfo = PacketManager.Compress(decompressedAddonInfo);
            uint decompressedAddonInfoSize = (uint)decompressedAddonInfo.Length;

            // Write the packet data
            writer.Write(build);
            writer.Write(serverId);
            writer.Write(Encoding.UTF8.GetBytes(_username));
            writer.Write((byte)0); // null terminator
            writer.Write(clientSeed);
            writer.Write(clientProof);
            writer.Write(decompressedAddonInfoSize);
            writer.Write(compressedAddonInfo);

            writer.Flush();
            byte[] payload = memoryStream.ToArray();

            Console.WriteLine($"[NewWorldClient] -> CMSG_AUTH_SESSION [{payload.Length} bytes] for user '{_username}'");

            await _pipeline.SendAsync(Opcode.CMSG_AUTH_SESSION, payload);
        }

        private async Task HandleAuthResponse(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_AUTH_RESPONSE ({payload.Length} bytes)");
            
            if (payload.Length > 0)
            {
                var result = payload.Span[0];
                if (result == 0x0C) // AUTH_OK
                {
                    _isAuthenticated = true;
                    Console.WriteLine("[NewWorldClient] World authentication successful!");
                    
                    // Fire authentication success event
                    OnAuthenticationSuccessful?.Invoke();
                    
                    // Now you can switch to encrypted communication if needed
                    // and automatically send character enumeration
                    Console.WriteLine("[NewWorldClient] Requesting character list...");
                    await SendCharEnumAsync();
                }
                else
                {
                    _isAuthenticated = false;
                    Console.WriteLine($"[NewWorldClient] World authentication failed with code: {result:X2}");
                    
                    // Fire authentication failure event
                    OnAuthenticationFailed?.Invoke(result);
                }
            }
            else
            {
                _isAuthenticated = false;
                Console.WriteLine("[NewWorldClient] Received empty SMSG_AUTH_RESPONSE");
                OnAuthenticationFailed?.Invoke(0xFF); // Unknown error
            }

            await Task.CompletedTask;
        }

        private async Task HandleCharEnum(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_CHAR_ENUM ({payload.Length} bytes)");
            
            try
            {
                if (payload.Length >= 1)
                {
                    using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));
                    byte numChars = reader.ReadByte();
                    
                    Console.WriteLine($"[NewWorldClient] Found {numChars} characters on account");
                    
                    for (int i = 0; i < numChars && reader.BaseStream.Position < reader.BaseStream.Length; i++)
                    {
                        try
                        {
                            ulong guid = reader.ReadUInt64();
                            string name = PacketManager.ReadCString(reader);
                            byte race = reader.ReadByte();
                            byte characterClass = reader.ReadByte();
                            byte gender = reader.ReadByte();
                            
                            // Skip other character data for now
                            // This would include skin, face, hair style, hair color, facial hair, level, zone, map, position, etc.
                            
                            Console.WriteLine($"[NewWorldClient]   Character {i + 1}: {name} (GUID: {guid}, Race: {race}, Class: {characterClass}, Gender: {gender})");
                            
                            // TODO: Fire event with character data or store in a collection
                            OnCharacterFound?.Invoke(guid, name, race, characterClass, gender);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[NewWorldClient] Error parsing character {i + 1}: {ex}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewWorldClient] Error parsing character enumeration: {ex}");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandlePong(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length >= 4)
            {
                var sequence = BitConverter.ToUInt32(payload.Span[0..4]);
                Console.WriteLine($"[NewWorldClient] Received SMSG_PONG for sequence {sequence}");
            }

            await Task.CompletedTask;
        }

        private async Task HandleQueryTimeResponse(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length >= 4)
            {
                var serverTime = BitConverter.ToUInt32(payload.Span[0..4]);
                Console.WriteLine($"[NewWorldClient] Received SMSG_QUERY_TIME_RESPONSE: {serverTime}");
            }

            await Task.CompletedTask;
        }

        private async Task HandleLoginVerifyWorld(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_LOGIN_VERIFY_WORLD ({payload.Length} bytes)");
            
            if (payload.Length >= 20)
            {
                using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));
                uint mapId = reader.ReadUInt32();
                float positionX = reader.ReadSingle();
                float positionY = reader.ReadSingle();
                float positionZ = reader.ReadSingle();
                float facing = reader.ReadSingle();

                Console.WriteLine($"[NewWorldClient] Player location - Map: {mapId}, Position: ({positionX:F2}, {positionY:F2}, {positionZ:F2}), Facing: {facing:F2}");
            }

            await Task.CompletedTask;
        }

        private async Task HandleNewWorld(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_NEW_WORLD ({payload.Length} bytes)");
            await Task.CompletedTask;
        }

        private async Task HandleTransferPending(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_TRANSFER_PENDING ({payload.Length} bytes)");
            await Task.CompletedTask;
        }

        private async Task HandleCharacterLoginFailed(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_CHARACTER_LOGIN_FAILED ({payload.Length} bytes)");
            await Task.CompletedTask;
        }

        private async Task HandleMessageChat(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_MESSAGECHAT ({payload.Length} bytes)");
            // Parse chat message here
            await Task.CompletedTask;
        }

        private async Task HandleNameQueryResponse(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[NewWorldClient] Received SMSG_NAME_QUERY_RESPONSE ({payload.Length} bytes)");
            // Parse name query response here
            await Task.CompletedTask;
        }

        // Targeting and combat packet handlers
        private async Task HandleAttackStart(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSTART ({payload.Length} bytes)");
            
            if (payload.Length >= 16)
            {
                using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));
                ulong attackerGuid = reader.ReadUInt64();
                ulong victimGuid = reader.ReadUInt64();
                
                Console.WriteLine($"[WorldClient] Attack started - Attacker: {attackerGuid:X}, Victim: {victimGuid:X}");
                
                // Fire event for attack agents to track attack state
                OnAttackStateChanged?.Invoke(true, attackerGuid, victimGuid);
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleAttackStop(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSTOP ({payload.Length} bytes)");
            
            if (payload.Length >= 16)
            {
                using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));
                ulong attackerGuid = reader.ReadUInt64();
                ulong victimGuid = reader.ReadUInt64();
                
                Console.WriteLine($"[WorldClient] Attack stopped - Attacker: {attackerGuid:X}, Victim: {victimGuid:X}");
                
                // Fire event for attack agents to track attack state
                OnAttackStateChanged?.Invoke(false, attackerGuid, victimGuid);
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleAttackSwingNotInRange(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSWING_NOTINRANGE ({payload.Length} bytes) - Target not in range");
            OnAttackError?.Invoke("Target not in range");
            await Task.CompletedTask;
        }

        private async Task HandleAttackSwingBadFacing(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSWING_BADFACING ({payload.Length} bytes) - Bad facing for attack");
            OnAttackError?.Invoke("Bad facing for attack");
            await Task.CompletedTask;
        }

        private async Task HandleAttackSwingNotStanding(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSWING_NOTSTANDING ({payload.Length} bytes) - Must be standing to attack");
            OnAttackError?.Invoke("Must be standing to attack");
            await Task.CompletedTask;
        }

        private async Task HandleAttackSwingDeadTarget(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSWING_DEADTARGET ({payload.Length} bytes) - Target is dead");
            OnAttackError?.Invoke("Target is dead");
            await Task.CompletedTask;
        }

        private async Task HandleAttackSwingCantAttack(ReadOnlyMemory<byte> payload)
        {
            Console.WriteLine($"[WorldClient] Received SMSG_ATTACKSWING_CANT_ATTACK ({payload.Length} bytes) - Cannot attack target");
            OnAttackError?.Invoke("Cannot attack target");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Exposes connection events.
        /// </summary>
        public event Action? Connected
        {
            add => _pipeline.Connected += value;
            remove => _pipeline.Connected -= value;
        }

        /// <summary>
        /// Exposes disconnection events.
        /// </summary>
        public event Action<Exception?>? Disconnected
        {
            add => _pipeline.Disconnected += value;
            remove => _pipeline.Disconnected -= value;
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