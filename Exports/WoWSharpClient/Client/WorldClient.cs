using System.Collections.Concurrent;
using GameData.Core.Enums;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Networking.I;
using System.Text;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.IO;

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

        // Reactive subjects
        private readonly Subject<Unit> _authenticationSucceeded = new();
        private readonly Subject<byte> _authenticationFailed = new();
        private readonly Subject<(ulong Guid, string Name, byte Race, byte Class, byte Gender)> _characterFound = new();
        private readonly Subject<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)> _attackStateChanged = new();
        private readonly Subject<string> _attackErrors = new();

        // Dynamic opcode subjects registry
        private readonly ConcurrentDictionary<Opcode, ISubject<ReadOnlyMemory<byte>>> _opcodeStreams = new();

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

        // Reactive opcode registration: returns an observable stream per opcode
        public IObservable<ReadOnlyMemory<byte>> RegisterOpcodeHandler(Opcode opcode)
        {
            var subject = (ISubject<ReadOnlyMemory<byte>>)_opcodeStreams.GetOrAdd(opcode, _ => new Subject<ReadOnlyMemory<byte>>());
            _pipeline.RegisterHandler(opcode, payload =>
            {
                subject.OnNext(payload);
                return Task.CompletedTask;
            });
            return subject.AsObservable();
        }

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
            // WorldClient-specific handlers (custom logic, not bridgeable)
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_CHALLENGE, HandleAuthChallenge);
            _pipeline.RegisterHandler(Opcode.SMSG_AUTH_RESPONSE, HandleAuthResponse);
            _pipeline.RegisterHandler(Opcode.SMSG_PONG, HandlePong);
            _pipeline.RegisterHandler(Opcode.SMSG_CHARACTER_LOGIN_FAILED, HandleCharacterLoginFailed);

            // Attack handlers (emit to reactive subjects)
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSTART, HandleAttackStart);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSTOP, HandleAttackStop);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_NOTINRANGE, HandleAttackSwingNotInRange);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_BADFACING, HandleAttackSwingBadFacing);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_NOTSTANDING, HandleAttackSwingNotStanding);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_DEADTARGET, HandleAttackSwingDeadTarget);
            _pipeline.RegisterHandler(Opcode.SMSG_ATTACKSWING_CANT_ATTACK, HandleAttackSwingCantAttack);

            // Inventory diagnostics
            _pipeline.RegisterHandler(Opcode.SMSG_INVENTORY_CHANGE_FAILURE, payload =>
            {
                if (payload.Length >= 1)
                {
                    byte errorCode = payload.Span[0];
                    Console.WriteLine($"[INVENTORY_CHANGE_FAILURE] errorCode=0x{errorCode:X2} ({GetInventoryErrorName(errorCode)}) len={payload.Length} raw={BitConverter.ToString(payload.ToArray())}");
                    Serilog.Log.Warning("[WorldClient] INVENTORY_CHANGE_FAILURE: errorCode={Error} (0x{ErrorHex:X2}) len={Len}",
                        errorCode, errorCode, payload.Length);
                }
                return Task.CompletedTask;
            });

            // Bridge all legacy OpCodeDispatcher handlers so WoWSharpObjectManager
            // receives events for object updates, movement, position, spells, etc.
            BridgeToLegacy(Opcode.SMSG_CHAR_ENUM, Handlers.CharacterSelectHandler.HandleCharEnum);
            BridgeToLegacy(Opcode.SMSG_ADDON_INFO, Handlers.CharacterSelectHandler.HandleAddonInfo);
            BridgeToLegacy(Opcode.SMSG_CHAR_CREATE, Handlers.CharacterSelectHandler.HandleCharCreate);
            BridgeToLegacy(Opcode.SMSG_NAME_QUERY_RESPONSE, Handlers.CharacterSelectHandler.HandleNameQueryResponse);
            BridgeToLegacy(Opcode.SMSG_SET_REST_START, Handlers.CharacterSelectHandler.HandleSetRestStart);

            // Login/World
            BridgeToLegacy(Opcode.SMSG_LOGIN_VERIFY_WORLD, Handlers.LoginHandler.HandleLoginVerifyWorld);
            BridgeToLegacy(Opcode.SMSG_TRANSFER_PENDING, Handlers.LoginHandler.HandleTransferPending);
            BridgeToLegacy(Opcode.SMSG_NEW_WORLD, Handlers.LoginHandler.HandleNewWorld);
            BridgeToLegacy(Opcode.SMSG_LOGIN_SETTIMESPEED, Handlers.LoginHandler.HandleSetTimeSpeed);
            BridgeToLegacy(Opcode.SMSG_QUERY_TIME_RESPONSE, Handlers.LoginHandler.HandleTimeQueryResponse);

            // Object updates (critical for position/health/player creation)
            BridgeToLegacy(Opcode.SMSG_UPDATE_OBJECT, Handlers.ObjectUpdateHandler.HandleUpdateObject);
            BridgeToLegacy(Opcode.SMSG_COMPRESSED_UPDATE_OBJECT, Handlers.ObjectUpdateHandler.HandleUpdateObject);

            // Client control
            BridgeToLegacy(Opcode.SMSG_CLIENT_CONTROL_UPDATE, OpCodeDispatcher.HandleSMSGClientControlUpdate);

            // Chat
            BridgeToLegacy(Opcode.SMSG_MESSAGECHAT, Handlers.ChatHandler.HandleServerChatMessage);

            // Account data
            BridgeToLegacy(Opcode.SMSG_ACCOUNT_DATA_TIMES, Handlers.AccountDataHandler.HandleAccountData);
            BridgeToLegacy(Opcode.SMSG_UPDATE_ACCOUNT_DATA, Handlers.AccountDataHandler.HandleAccountData);

            // Spells
            BridgeToLegacy(Opcode.SMSG_INITIAL_SPELLS, Handlers.SpellHandler.HandleInitialSpells);
            BridgeToLegacy(Opcode.SMSG_LEARNED_SPELL, Handlers.SpellHandler.HandleLearnedSpell);
            BridgeToLegacy(Opcode.SMSG_SPELLLOGMISS, Handlers.SpellHandler.HandleSpellLogMiss);
            BridgeToLegacy(Opcode.SMSG_SPELL_GO, Handlers.SpellHandler.HandleSpellGo);
            BridgeToLegacy(Opcode.SMSG_SPELL_START, Handlers.SpellHandler.HandleSpellStart);
            BridgeToLegacy(Opcode.SMSG_ATTACKERSTATEUPDATE, Handlers.SpellHandler.HandleAttackerStateUpdate);
            BridgeToLegacy(Opcode.SMSG_DESTROY_OBJECT, Handlers.SpellHandler.HandleDestroyObject);
            BridgeToLegacy(Opcode.SMSG_CAST_FAILED, Handlers.SpellHandler.HandleCastFailed);
            BridgeToLegacy(Opcode.SMSG_SPELL_FAILURE, Handlers.SpellHandler.HandleSpellFailure);
            BridgeToLegacy(Opcode.SMSG_SPELLHEALLOG, Handlers.SpellHandler.HandleSpellHealLog);
            BridgeToLegacy(Opcode.SMSG_LOG_XPGAIN, Handlers.SpellHandler.HandleLogXpGain);
            BridgeToLegacy(Opcode.SMSG_LEVELUP_INFO, Handlers.SpellHandler.HandleLevelUpInfo);
            BridgeToLegacy(Opcode.SMSG_ATTACKSTART, Handlers.SpellHandler.HandleAttackStart);
            BridgeToLegacy(Opcode.SMSG_ATTACKSTOP, Handlers.SpellHandler.HandleAttackStop);
            BridgeToLegacy(Opcode.SMSG_GAMEOBJECT_CUSTOM_ANIM, Handlers.SpellHandler.HandleGameObjectCustomAnim);

            // Death / corpse
            BridgeToLegacy(Opcode.SMSG_CORPSE_RECLAIM_DELAY, Handlers.DeathHandler.HandleCorpseReclaimDelay);

            // Stand state / world state
            BridgeToLegacy(Opcode.SMSG_STANDSTATE_UPDATE, Handlers.StandStateHandler.HandleStandStateUpdate);
            BridgeToLegacy(Opcode.SMSG_INIT_WORLD_STATES, Handlers.WorldStateHandler.HandleInitWorldStates);

            // Movement packets
            BridgeToLegacy(Opcode.SMSG_MONSTER_MOVE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_COMPRESSED_MOVES, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_FEATHER_FALL, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_KNOCK_BACK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_LAND_WALK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_NORMAL_FALL, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_SET_FLIGHT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_SET_HOVER, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_UNSET_FLIGHT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_UNSET_HOVER, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_MOVE_WATER_WALK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_FORCE_MOVE_ROOT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_FORCE_MOVE_UNROOT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_FORCE_RUN_SPEED_CHANGE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_FEATHER_FALL, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_LAND_WALK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_NORMAL_FALL, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_ROOT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_SET_HOVER, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_SET_RUN_MODE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_SET_WALK_MODE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_START_SWIM, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_STOP_SWIM, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_UNROOT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_UNSET_HOVER, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.SMSG_SPLINE_MOVE_WATER_WALK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_TELEPORT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_TELEPORT_ACK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_TIME_SKIPPED, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_JUMP, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_FALL_LAND, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_START_FORWARD, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_START_BACKWARD, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_STOP, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_START_STRAFE_LEFT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_START_STRAFE_RIGHT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_STOP_STRAFE, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_START_TURN_LEFT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_START_TURN_RIGHT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_STOP_TURN, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_SET_FACING, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_ROOT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_UNROOT, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_SET_RUN_SPEED, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_SET_SWIM_SPEED, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_WATER_WALK, Handlers.MovementHandler.HandleUpdateMovement);
            BridgeToLegacy(Opcode.MSG_MOVE_HEARTBEAT, Handlers.MovementHandler.HandleUpdateMovement);
        }

        /// <summary>
        /// Bridges an opcode to a legacy handler (Action&lt;Opcode, byte[]&gt;) from OpCodeDispatcher.
        /// </summary>
        private void BridgeToLegacy(Opcode opcode, Action<Opcode, byte[]> legacyHandler)
        {
            _pipeline.RegisterHandler(opcode, payload =>
            {
                try
                {
                    legacyHandler(opcode, payload.ToArray());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Bridge] ERROR handling {opcode}: {ex.Message}");
                }
                return Task.CompletedTask;
            });
        }

        // Reactive IWorldClient streams (forward or expose)
        public IObservable<Unit> WhenConnected => _pipeline.WhenConnected;
        public IObservable<Exception?> WhenDisconnected => _pipeline.WhenDisconnected;
        public IObservable<Unit> AuthenticationSucceeded => _authenticationSucceeded;
        public IObservable<byte> AuthenticationFailed => _authenticationFailed;
        public IObservable<(ulong Guid, string Name, byte Race, byte Class, byte Gender)> CharacterFound => _characterFound;
        public IObservable<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)> AttackStateChanged => _attackStateChanged;
        public IObservable<string> AttackErrors => _attackErrors;

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

                // Enable encryption immediately after sending CMSG_AUTH_SESSION.
                // The server enables encryption right after receiving our auth session,
                // so SMSG_AUTH_RESPONSE (the next packet) will already be encrypted.
                if (_sessionKey.Length == 40)
                {
                    UpdateEncryptor(new VanillaHeaderEncryptor(_sessionKey));
                }
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
                    // Encryption was already enabled in HandleAuthChallenge right after
                    // sending CMSG_AUTH_SESSION (before this response was received).
                    Console.WriteLine("[NewWorldClient] World authentication successful!");
                    _authenticationSucceeded.OnNext(Unit.Default);

                    // Automatically request character list after successful auth
                    _ = SendCharEnumAsync();
                }
                else
                {
                    _isAuthenticated = false;
                    Console.WriteLine($"[NewWorldClient] World authentication failed with code: {result:X2}");
                    _authenticationFailed.OnNext(result);
                }
            }
            else
            {
                _isAuthenticated = false;
                Console.WriteLine("[NewWorldClient] Received empty SMSG_AUTH_RESPONSE");
                _authenticationFailed.OnNext(0xFF);
            }

            return Task.CompletedTask;
        }

        private Task HandlePong(ReadOnlyMemory<byte> payload) => Task.CompletedTask;
        private Task HandleCharacterLoginFailed(ReadOnlyMemory<byte> payload) => Task.CompletedTask;

        private Task HandleAttackStart(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length >= 16)
                {
                    var attacker = BitConverter.ToUInt64(payload.Span[0..8]);
                    var victim = BitConverter.ToUInt64(payload.Span[8..16]);
                    _attackStateChanged.OnNext((true, attacker, victim));
                }
                else
                {
                    _attackStateChanged.OnNext((true, 0, 0));
                }
            }
            catch (Exception ex)
            {
                _attackErrors.OnNext($"Error parsing ATTACKSTART: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private Task HandleAttackStop(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length >= 16)
                {
                    var attacker = BitConverter.ToUInt64(payload.Span[0..8]);
                    var victim = BitConverter.ToUInt64(payload.Span[8..16]);
                    _attackStateChanged.OnNext((false, attacker, victim));
                }
                else
                {
                    _attackStateChanged.OnNext((false, 0, 0));
                }
            }
            catch (Exception ex)
            {
                _attackErrors.OnNext($"Error parsing ATTACKSTOP: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private Task HandleAttackSwingNotInRange(ReadOnlyMemory<byte> payload)
        {
            _attackErrors.OnNext("Attack failed: Not in range.");
            return Task.CompletedTask;
        }
        private Task HandleAttackSwingBadFacing(ReadOnlyMemory<byte> payload)
        {
            _attackErrors.OnNext("Attack failed: Bad facing.");
            return Task.CompletedTask;
        }
        private Task HandleAttackSwingNotStanding(ReadOnlyMemory<byte> payload)
        {
            _attackErrors.OnNext("Attack failed: Not standing.");
            return Task.CompletedTask;
        }
        private Task HandleAttackSwingDeadTarget(ReadOnlyMemory<byte> payload)
        {
            _attackErrors.OnNext("Attack failed: Target is dead.");
            return Task.CompletedTask;
        }
        private Task HandleAttackSwingCantAttack(ReadOnlyMemory<byte> payload)
        {
            _attackErrors.OnNext("Attack failed: Can't attack.");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _pipeline?.Dispose();
                _authenticationSucceeded.OnCompleted();
                _authenticationFailed.OnCompleted();
                _characterFound.OnCompleted();
                _attackStateChanged.OnCompleted();
                _attackErrors.OnCompleted();
                foreach (var kv in _opcodeStreams)
                {
                    kv.Value.OnCompleted();
                }
                _authenticationSucceeded.Dispose();
                _authenticationFailed.Dispose();
                _characterFound.Dispose();
                _attackStateChanged.Dispose();
                _attackErrors.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Returns a human-readable name for SMSG_INVENTORY_CHANGE_FAILURE error codes (vanilla 1.12.1).
        /// </summary>
        private static string GetInventoryErrorName(byte code) => code switch
        {
            0x00 => "OK",
            0x01 => "CANT_EQUIP_LEVEL_I",
            0x02 => "CANT_EQUIP_SKILL",
            0x03 => "ITEM_DOESNT_GO_TO_SLOT",
            0x04 => "BAG_FULL",
            0x05 => "NONEMPTY_BAG_OVER_OTHER_BAG",
            0x06 => "CANT_TRADE_EQUIP_BAGS",
            0x07 => "ONLY_AMMO_CAN_GO_HERE",
            0x08 => "NO_REQUIRED_PROFICIENCY",
            0x09 => "NO_EQUIPMENT_SLOT_AVAILABLE",
            0x0A => "YOU_CAN_NEVER_USE_THAT_ITEM",
            0x0B => "YOU_CAN_NEVER_USE_THAT_ITEM2",
            0x0C => "NO_EQUIPMENT_SLOT_AVAILABLE2",
            0x0D => "CANT_EQUIP_WITH_TWOHANDED",
            0x0E => "CANT_DUAL_WIELD",
            0x10 => "ITEM_DOESNT_GO_INTO_BAG",
            0x11 => "ITEM_DOESNT_GO_INTO_BAG2",
            0x12 => "CANT_CARRY_MORE_OF_THIS",
            0x13 => "NO_EQUIPMENT_SLOT_AVAILABLE3",
            0x14 => "ITEM_CANT_STACK",
            0x15 => "ITEM_CANT_BE_EQUIPPED",
            0x16 => "ITEMS_CANT_BE_SWAPPED",
            0x17 => "SLOT_IS_EMPTY",
            0x18 => "ITEM_NOT_FOUND",
            0x19 => "CANT_DROP_SOULBOUND",
            0x1A => "OUT_OF_RANGE",
            0x1B => "TRIED_TO_SPLIT_MORE_THAN_COUNT",
            0x1C => "COULDNT_SPLIT_ITEMS",
            0x1D => "MISSING_REAGENT",
            0x31 => "CANT_EQUIP_RATING",
            _ => $"UNKNOWN_0x{code:X2}"
        };
    }
}
