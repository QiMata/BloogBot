using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Parsers;
using WoWSharpClient.Screens;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;
using Enum = System.Enum;
using Timer = System.Timers.Timer;

namespace WoWSharpClient
{
    public class WoWSharpObjectManager : IObjectManager
    {
        private static WoWSharpObjectManager _instance;

        public static WoWSharpObjectManager Instance
        {
            get
            {
                _instance ??= new WoWSharpObjectManager();

                return _instance;
            }
        }

        private ILogger<WoWSharpObjectManager> _logger;

        // Wrapper client for both auth and world transactions
        private WoWClient _woWClient;
        private PathfindingClient _pathfindingClient;

        // Movement controller - handles all movement logic
        private MovementController _movementController;

        private LoginScreen _loginScreen;
        private RealmSelectScreen _realmScreen;
        private CharacterSelectScreen _characterSelectScreen;

        private Timer _gameLoopTimer;

        private long _lastPingMs = 0;
        private ControlBits _controlBits = ControlBits.Nothing;
        public bool IsPlayerMoving => !Player.MovementFlags.Equals(MovementFlags.MOVEFLAG_NONE);

        private bool _isInControl = false;
        private bool _isBeingTeleported = true;
        private ulong _currentTargetGuid;

        // Temporary diagnostic: log all opcodes received after GAMEOBJ_USE
        internal volatile bool _sniffingGameObjUse = false;
        private DateTime _sniffStartTime;

        private TimeSpan _lastPositionUpdate = TimeSpan.Zero;
        private WorldTimeTracker _worldTimeTracker;

        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
        private Task _backgroundUpdateTask;
        private CancellationTokenSource _updateCancellation;

        // Optional cooldown checker — set by BackgroundBotWorker after SpellCastingNetworkClientComponent is created
        private Func<uint, bool> _spellCooldownChecker;

        /// <summary>
        /// Set a delegate that checks if a spell ID is off cooldown (returns true if ready).
        /// Wire this to SpellCastingNetworkClientComponent.CanCastSpell().
        /// </summary>
        public void SetSpellCooldownChecker(Func<uint, bool> checker) => _spellCooldownChecker = checker;

        // Optional agent factory accessor — set by BackgroundBotWorker for LootTargetAsync
        private Func<IAgentFactory> _agentFactoryAccessor;

        public void SetAgentFactoryAccessor(Func<IAgentFactory> accessor) => _agentFactoryAccessor = accessor;

        private WoWSharpObjectManager() { }

        public void Initialize(
            WoWClient wowClient,
            PathfindingClient pathfindingClient,
            ILogger<WoWSharpObjectManager> logger
        )
        {
            WoWSharpEventEmitter.Instance.Reset();
            lock (_objectsLock) _objects.Clear();
            _pendingUpdates.Clear();

            _logger = logger;
            _pathfindingClient = pathfindingClient;
            _woWClient = wowClient;

            WoWSharpEventEmitter.Instance.OnLoginFailure += EventEmitter_OnLoginFailure;
            WoWSharpEventEmitter.Instance.OnLoginVerifyWorld += EventEmitter_OnLoginVerifyWorld;
            WoWSharpEventEmitter.Instance.OnWorldSessionStart += EventEmitter_OnWorldSessionStart;
            WoWSharpEventEmitter.Instance.OnWorldSessionEnd += EventEmitter_OnWorldSessionEnd;
            WoWSharpEventEmitter.Instance.OnCharacterListLoaded +=
                EventEmitter_OnCharacterListLoaded;
            WoWSharpEventEmitter.Instance.OnChatMessage += EventEmitter_OnChatMessage;
            WoWSharpEventEmitter.Instance.OnForceMoveRoot += EventEmitter_OnForceMoveRoot;
            WoWSharpEventEmitter.Instance.OnForceMoveUnroot += EventEmitter_OnForceMoveUnroot;
            WoWSharpEventEmitter.Instance.OnForceRunSpeedChange +=
                EventEmitter_OnForceRunSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceRunBackSpeedChange +=
                EventEmitter_OnForceRunBackSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceSwimSpeedChange +=
                EventEmitter_OnForceSwimSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceMoveKnockBack += EventEmitter_OnForceMoveKnockBack;
            WoWSharpEventEmitter.Instance.OnForceTimeSkipped += EventEmitter_OnForceTimeSkipped;
            WoWSharpEventEmitter.Instance.OnTeleport += EventEmitter_OnTeleport;
            WoWSharpEventEmitter.Instance.OnClientControlUpdate +=
                EventEmitter_OnClientControlUpdate;
            WoWSharpEventEmitter.Instance.OnSetTimeSpeed += EventEmitter_OnSetTimeSpeed;
            WoWSharpEventEmitter.Instance.OnSpellGo += EventEmitter_OnSpellGo;

            _loginScreen = new(_woWClient);
            _realmScreen = new(_woWClient);
            _characterSelectScreen = new(_woWClient);
        }
        private void InitializeMovementController()
        {
            // Initialize movement controller when we have a player
            if (Player != null && _woWClient != null && _pathfindingClient != null)
            {
                _movementController = new MovementController(
                    _woWClient,
                    _pathfindingClient,
                    (WoWLocalPlayer)Player
                );
            }
        }
        private void EventEmitter_OnSpellGo(object? sender, EventArgs e) { }

        private void EventEmitter_OnClientControlUpdate(object? sender, EventArgs e)
        {
            _isInControl = true;
            _isBeingTeleported = false;
            ResetMovementStateForTeleport("client-control-update");

            Log.Information("[OnClientControlUpdate] pos=({X:F1},{Y:F1},{Z:F1}) — server confirmed teleport complete",
                Player.Position.X, Player.Position.Y, Player.Position.Z);
        }

        private void EventEmitter_OnSetTimeSpeed(object? sender, OnSetTimeSpeedArgs e)
        {
            _ = _woWClient.QueryTimeAsync();
        }

        /// <summary>
        /// Optional callback invoked each game loop tick after movement/physics.
        /// Use this to drive bot AI logic (pathfinding, combat rotation, etc.).
        /// The float parameter is delta time in seconds.
        /// </summary>
        public Action<float>? OnBotTick { get; set; }

        public bool IsGameLoopRunning { get; private set; }

        /// <summary>
        /// Starts the fixed-timestep game loop (50ms / ~20 Hz).
        /// Tick order: spline updates → ping heartbeat → physics/movement → bot AI.
        /// Object updates are processed on a separate background thread.
        /// </summary>
        public void StartGameLoop()
        {
            if (IsGameLoopRunning) return;

            _gameLoopTimer = new Timer(50);
            _gameLoopTimer.Elapsed += OnGameLoopTick;
            _gameLoopTimer.AutoReset = true;
            _gameLoopTimer.Start();

            _updateCancellation = new CancellationTokenSource();
            _backgroundUpdateTask = Task.Run(() => ProcessUpdatesAsync(_updateCancellation.Token));

            IsGameLoopRunning = true;
            Log.Information("[GameLoop] Started (50ms tick, ~20 Hz)");
        }

        /// <summary>
        /// Stops the game loop and background update processor.
        /// </summary>
        public void StopGameLoop()
        {
            if (!IsGameLoopRunning) return;

            _gameLoopTimer?.Stop();
            _gameLoopTimer?.Dispose();
            _gameLoopTimer = null;

            _updateCancellation?.Cancel();
            try { _backgroundUpdateTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _updateCancellation?.Dispose();
            _updateCancellation = null;

            IsGameLoopRunning = false;
            Log.Information("[GameLoop] Stopped");
        }

        private void OnGameLoopTick(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var now = _worldTimeTracker.NowMS;
                var delta = now - _lastPositionUpdate;
                var deltaSec = (float)delta.TotalMilliseconds / 1000f;

                // 1. Advance every monster/NPC spline before physics
                Splines.Instance.Update((float)delta.TotalMilliseconds);

                // 2. Handle ping heartbeat
                HandlePingHeartbeat((long)now.TotalMilliseconds);

                // 3. Update player movement if we're in control
                if (_isInControl && !_isBeingTeleported && Player != null && _movementController != null)
                {
                    _movementController.Update(deltaSec, (uint)now.TotalMilliseconds);
                }

                // 4. Bot AI callback
                OnBotTick?.Invoke(deltaSec);

                _lastPositionUpdate = now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GameLoop] Tick error");
            }
        }

        private void HandlePingHeartbeat(long now)
        {
            const int interval = 30000;

            if (now - _lastPingMs < interval)
                return;

            _lastPingMs = now;
            _ = _woWClient.SendPingAsync();
        }

        // ============= INPUT HANDLERS =============
        public void StartMovement(ControlBits bits)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // Convert control bits to movement flags and update player state
            MovementFlags flags = ConvertControlBitsToFlags(bits, player.MovementFlags, true);
            player.MovementFlags = flags;
        }

        public void StopMovement(ControlBits bits)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // Clear the corresponding movement flags.
            // MovementController (game loop, 50ms) detects the flag change
            // and sends MSG_MOVE_STOP automatically.
            MovementFlags flags = ConvertControlBitsToFlags(bits, player.MovementFlags, false);
            player.MovementFlags = flags;

            // Clear path when forward movement stops
            if (bits.HasFlag(ControlBits.Front))
                _movementController?.ClearPath();
        }

        /// <summary>
        /// Clears all movement flags AND immediately sends MSG_MOVE_STOP to the server.
        /// Use before interactions that require the player to be stationary (CMSG_GAMEOBJ_USE, etc.).
        /// </summary>
        void IObjectManager.ForceStopImmediate()
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _movementController?.ClearPath();
            _movementController?.SendStopPacket((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
            Log.Information("[ForceStopImmediate] Cleared all movement flags and sent MSG_MOVE_STOP");
        }

        private MovementFlags ConvertControlBitsToFlags(ControlBits bits, MovementFlags currentFlags, bool add)
        {
            MovementFlags flags = currentFlags;

            if (bits.HasFlag(ControlBits.Front))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_FORWARD;
                else flags &= ~MovementFlags.MOVEFLAG_FORWARD;
            }
            if (bits.HasFlag(ControlBits.Back))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_BACKWARD;
                else flags &= ~MovementFlags.MOVEFLAG_BACKWARD;
            }
            if (bits.HasFlag(ControlBits.StrafeLeft))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_STRAFE_LEFT;
                else flags &= ~MovementFlags.MOVEFLAG_STRAFE_LEFT;
            }
            if (bits.HasFlag(ControlBits.StrafeRight))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_STRAFE_RIGHT;
                else flags &= ~MovementFlags.MOVEFLAG_STRAFE_RIGHT;
            }
            if (bits.HasFlag(ControlBits.Left))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_TURN_LEFT;
                else flags &= ~MovementFlags.MOVEFLAG_TURN_LEFT;
            }
            if (bits.HasFlag(ControlBits.Right))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_TURN_RIGHT;
                else flags &= ~MovementFlags.MOVEFLAG_TURN_RIGHT;
            }
            if (bits.HasFlag(ControlBits.Jump))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_JUMPING;
                else flags &= ~MovementFlags.MOVEFLAG_JUMPING;
            }

            return flags;
        }
        public void SetFacing(float facing)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            player.Facing = facing;

            // Send facing update immediately via movement controller
            if (_movementController != null && _isInControl && !_isBeingTeleported)
            {
                _movementController.SendFacingUpdate((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
            }
        }

        private WoWObject CreateObjectFromFields(
            WoWObjectType objectType,
            ulong guid,
            Dictionary<uint, object?> fields
        )
        {
            WoWObject obj = objectType switch
            {
                WoWObjectType.Item => new WoWItem(new HighGuid(guid)),
                WoWObjectType.Container => new WoWContainer(new HighGuid(guid)),
                WoWObjectType.Unit => new WoWUnit(new HighGuid(guid)),
                WoWObjectType.Player => guid == PlayerGuid.FullGuid
                    ? (WoWLocalPlayer)Player
                    : new WoWPlayer(new HighGuid(guid)),
                WoWObjectType.GameObj => new WoWGameObject(new HighGuid(guid)),
                WoWObjectType.DynamicObj => new WoWDynamicObject(new HighGuid(guid)),
                WoWObjectType.Corpse => new WoWCorpse(new HighGuid(guid)),
                _ => new WoWObject(new HighGuid(guid)),
            };
            ApplyFieldDiffs(obj, fields);
            return obj;
        }

        private void EventEmitter_OnForceTimeSkipped(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        { }

        private void EventEmitter_OnForceMoveKnockBack(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_MOVE_KNOCK_BACK_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    (WoWLocalPlayer)Player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }

        private void EventEmitter_OnForceSwimSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    (WoWLocalPlayer)Player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        private void EventEmitter_OnForceRunBackSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    (WoWLocalPlayer)Player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        private void EventEmitter_OnForceRunSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    (WoWLocalPlayer)Player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        private void EventEmitter_OnForceMoveUnroot(object? sender, RequiresAcknowledgementArgs e)
        {
            // Clear MOVEFLAG_ROOT before ACK — MaNGOS validates the flag is absent
            var player = (WoWLocalPlayer)Player;
            player.MovementFlags &= ~MovementFlags.MOVEFLAG_ROOT;

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_MOVE_UNROOT_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }

        private void EventEmitter_OnForceMoveRoot(object? sender, RequiresAcknowledgementArgs e)
        {
            // Set MOVEFLAG_ROOT and clear movement flags incompatible with root
            var player = (WoWLocalPlayer)Player;
            player.MovementFlags |= MovementFlags.MOVEFLAG_ROOT;
            player.MovementFlags &= ~MovementFlags.MOVEFLAG_MASK_MOVING;

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_MOVE_ROOT_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }

        private void EventEmitter_OnChatMessage(object? sender, ChatMessageArgs e)
        {
            string prefix = e.MsgType switch
            {
                ChatMsg.CHAT_MSG_SAY or ChatMsg.CHAT_MSG_MONSTER_SAY => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_YELL or ChatMsg.CHAT_MSG_MONSTER_YELL => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_WHISPER or ChatMsg.CHAT_MSG_MONSTER_WHISPER =>
                    $"[{(Objects.FirstOrDefault(x => x.Guid == e.SenderGuid) as WoWUnit)?.Name ?? ""}]",
                ChatMsg.CHAT_MSG_WHISPER_INFORM => $"To[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_EMOTE or ChatMsg.CHAT_MSG_TEXT_EMOTE or
                ChatMsg.CHAT_MSG_MONSTER_EMOTE or ChatMsg.CHAT_MSG_RAID_BOSS_EMOTE => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_SYSTEM => "[System]",
                ChatMsg.CHAT_MSG_PARTY or ChatMsg.CHAT_MSG_RAID or
                ChatMsg.CHAT_MSG_GUILD or ChatMsg.CHAT_MSG_OFFICER => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_CHANNEL or ChatMsg.CHAT_MSG_CHANNEL_NOTICE => "[Channel]",
                ChatMsg.CHAT_MSG_RAID_WARNING => "[Raid Warning]",
                ChatMsg.CHAT_MSG_LOOT => "[Loot]",
                _ => $"[{e.SenderGuid}][{e.MsgType}]",
            };

            if (e.MsgType == ChatMsg.CHAT_MSG_SYSTEM)
            {
                _systemMessages.Enqueue(e.Text);

                if (e.Text.StartsWith("You are being teleported"))
                {
                    ResetMovementStateForTeleport("chat-teleport-message");
                    _isBeingTeleported = true;
                }
            }

            Log.Information("[Chat] {MsgType} {Prefix}{Text}", e.MsgType, prefix, e.Text);
        }

        /// <summary>
        /// Called by MovementHandler BEFORE queuing a teleport position update,
        /// so the position write guard in ProcessUpdatesAsync allows it through.
        /// </summary>
        public void NotifyTeleportIncoming()
        {
            _isBeingTeleported = true;
            ResetMovementStateForTeleport("notify-teleport-incoming");
        }

        private void EventEmitter_OnTeleport(object? sender, RequiresAcknowledgementArgs e)
        {
            // _isBeingTeleported is already set by NotifyTeleportIncoming() before the update was queued.
            _isBeingTeleported = true;
            ResetMovementStateForTeleport("teleport-opcode");

            var player = (WoWLocalPlayer)Player;
            var ackPayload = MovementPacketHandler.BuildMoveTeleportAckPayload(
                player,
                e.Counter,
                (uint)_worldTimeTracker.NowMS.TotalMilliseconds
            );

            Log.Information("[ACK] TELEPORT counter={Counter} guid=0x{Guid:X} pos=({X:F1},{Y:F1},{Z:F1}) payloadLen={Len}",
                e.Counter, player.Guid, player.Position.X, player.Position.Y, player.Position.Z, ackPayload.Length);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.MSG_MOVE_TELEPORT_ACK,
                ackPayload
            );

            // Clear after a delay so ProcessUpdatesAsync has time to apply the position update.
            // SMSG_CLIENT_CONTROL_UPDATE will also clear this if it arrives sooner.
            // Also send a stop packet so the server knows we're stationary after teleport
            // (prevents stale MOVEFLAG_FORWARD from persisting on the server side).
            Task.Delay(500).ContinueWith(_ =>
            {
                Log.Information("[ACK] TELEPORT 500ms fallback: clearing _isBeingTeleported");
                _isBeingTeleported = false;
                _movementController?.SendStopPacket((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
            });
        }

        private void ResetMovementStateForTeleport(string source)
        {
            if (Player is not WoWLocalPlayer player)
                return;

            _controlBits = ControlBits.Nothing;
            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _movementController?.Reset();

            Log.Information("[TeleportReset] source={Source} flags cleared; pos=({X:F1},{Y:F1},{Z:F1})",
                source, player.Position.X, player.Position.Y, player.Position.Z);
        }

        private void EventEmitter_OnLoginVerifyWorld(object? sender, WorldInfo e)
        {
            ((WoWLocalPlayer)Player).MapId = e.MapId;

            Player.Position.X = e.PositionX;
            Player.Position.Y = e.PositionY;
            Player.Position.Z = e.PositionZ;

            _worldTimeTracker = new WorldTimeTracker();
            _lastPositionUpdate = _worldTimeTracker.NowMS;
            StartGameLoop();

            _ = _woWClient.SendMoveWorldPortAcknowledgeAsync();
        }

        private void EventEmitter_OnCharacterListLoaded(object? sender, EventArgs e)
        {
            _characterSelectScreen.HasReceivedCharacterList = true;
        }

        private void EventEmitter_OnWorldSessionStart(object? sender, EventArgs e)
        {
            _characterSelectScreen.RefreshCharacterListFromServer();
        }

        private void EventEmitter_OnLoginFailure(object? sender, EventArgs e)
        {
            Log.Error("[Login] Login failed");
            _woWClient.Dispose();
        }

        private void EventEmitter_OnWorldSessionEnd(object? sender, EventArgs e)
        {
            StopGameLoop();
            HasEnteredWorld = false;
            _isInControl = false;
            _movementController = null;
            Log.Information("[WorldSession] Session ended, game loop stopped");
        }

        public bool HasEnteredWorld { get; internal set; }
        public List<Spell> Spells { get; internal set; } = [];
        public List<Cooldown> Cooldowns { get; internal set; } = [];

        private HighGuid _playerGuid = new(new byte[4], new byte[4]);

        public HighGuid PlayerGuid
        {
            get => _playerGuid;
            set
            {
                _playerGuid = value;
                Player = new WoWLocalPlayer(_playerGuid);
            }
        }

        public IWoWEventHandler EventHandler => WoWSharpEventEmitter.Instance;

        public IWoWLocalPlayer Player { get; set; } =
            new WoWLocalPlayer(new HighGuid(new byte[4], new byte[4]));

        public IWoWLocalPet Pet => null;

        private static readonly List<WoWObject> _objects = [];
        private static readonly object _objectsLock = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _systemMessages = new();

        /// <summary>
        /// Returns an object by its full GUID, or null if not found.
        /// Checks the local player first, then the objects list.
        /// </summary>
        public WoWObject GetObjectByGuid(ulong guid)
        {
            if (guid == PlayerGuid.FullGuid)
                return Player as WoWObject;
            lock (_objectsLock) return _objects.FirstOrDefault(o => o.Guid == guid);
        }

        /// <summary>
        /// Returns a snapshot of the objects list. Safe to enumerate from any thread
        /// while ProcessUpdatesAsync modifies the underlying list.
        /// </summary>
        public IEnumerable<IWoWObject> Objects
        {
            get { lock (_objectsLock) return _objects.ToArray(); }
        }

        /// <summary>
        /// Units that are alive and targeting the player or party members.
        /// In WoWSharpClient, UnitReaction is not reliably set from server packets,
        /// so we use target-based detection instead of faction-based.
        /// </summary>
        public IEnumerable<IWoWUnit> Hostiles
        {
            get
            {
                var playerGuid = PlayerGuid.FullGuid;
                if (playerGuid == 0) return [];
                return Objects.OfType<IWoWUnit>()
                    .Where(u => u.Health > 0 && u.Guid != playerGuid)
                    .Where(u => u.TargetGuid == playerGuid || u.IsInCombat);
            }
        }

        /// <summary>
        /// Units actively in combat that are targeting the player or party.
        /// </summary>
        public IEnumerable<IWoWUnit> Aggressors =>
            Hostiles.Where(u => u.IsInCombat || u.IsFleeing);

        /// <summary>Aggressors that have mana (likely casters).</summary>
        public IEnumerable<IWoWUnit> CasterAggressors =>
            Aggressors.Where(u => u.ManaPercent > 0);

        /// <summary>Aggressors that have no mana (melee).</summary>
        public IEnumerable<IWoWUnit> MeleeAggressors =>
            Aggressors.Where(u => u.ManaPercent <= 0);

        /// <summary>
        /// Drains all pending system messages (CHAT_MSG_SYSTEM) received since last call.
        /// </summary>
        public List<string> DrainSystemMessages()
        {
            var messages = new List<string>();
            while (_systemMessages.TryDequeue(out var msg))
                messages.Add(msg);
            return messages;
        }
        public ILoginScreen LoginScreen => _loginScreen;
        public IRealmSelectScreen RealmSelectScreen => _realmScreen;
        public ICharacterSelectScreen CharacterSelectScreen => _characterSelectScreen;

        public void EnterWorld(ulong characterGuid)
        {
            // Use the property setter (not the backing field) so it also
            // recreates the Player object with the correct GUID.
            // Set PlayerGuid BEFORE HasEnteredWorld so snapshots never see
            // HasEnteredWorld=true with a stale zero-GUID player.
            PlayerGuid = new HighGuid(characterGuid);
            HasEnteredWorld = true;

            _ = _woWClient.EnterWorldAsync(characterGuid);

            InitializeMovementController();
        }

        private readonly Queue<ObjectStateUpdate> _pendingUpdates = new();

        public void QueueUpdate(ObjectStateUpdate update)
        {
            _pendingUpdates.Enqueue(update);
        }

        public async Task ProcessUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _updateSemaphore.WaitAsync(token);
                try
                {
                    while (_pendingUpdates.Count > 0)
                    {
                        var update = _pendingUpdates.Dequeue();

                        Log.Verbose("[ProcessUpdates] Op={Op} Type={Type} Guid={Guid:X}",
                            update.Operation, update.ObjectType, update.Guid);

                        try
                        {
                            switch (update.Operation)
                            {
                                case ObjectUpdateOperation.Add:
                                {
                                    var newObject = CreateObjectFromFields(
                                        update.ObjectType,
                                        update.Guid,
                                        update.UpdatedFields
                                    );
                                    lock (_objectsLock) _objects.Add(newObject);

                                    if (newObject is WoWItem item)
                                        Log.Information("[ProcessUpdates] ITEM CREATED: Guid={Guid:X} ItemId={ItemId} Fields={FieldCount}",
                                            update.Guid, item.ItemId, update.UpdatedFields.Count);

                                    if (update.MovementData != null && newObject is WoWUnit)
                                    {
                                        ApplyMovementData((WoWUnit)newObject, update.MovementData);

                                        Log.Verbose("[Movement-Add] Guid={Guid:X} Pos=({X:F2},{Y:F2},{Z:F2}) Flags=0x{Flags:X8}",
                                            update.Guid, update.MovementData.X, update.MovementData.Y,
                                            update.MovementData.Z, (uint)update.MovementData.MovementFlags);
                                    }

                                    if (newObject is WoWPlayer)
                                    {
                                        _ = _woWClient.SendNameQueryAsync(update.Guid);

                                        if (newObject is WoWLocalPlayer)
                                        {
                                            Log.Information("[LocalPlayer-Add] Taking control");
                                            _ = _woWClient.SendSetActiveMoverAsync(PlayerGuid.FullGuid);
                                            _isInControl = true;
                                            _isBeingTeleported = false;
                                        }
                                    }

                                    break;
                                }

                                case ObjectUpdateOperation.Update:
                                {
                                    WoWObject obj;
                                    int index;
                                    lock (_objectsLock)
                                    {
                                        index = _objects.FindIndex(o => o.Guid == update.Guid);
                                        if (index == -1)
                                        {
                                            Log.Warning("[ProcessUpdates] Update for unknown object {Guid:X}", update.Guid);
                                            break;
                                        }
                                        obj = _objects[index];
                                    }

                                    ApplyFieldDiffs(obj, update.UpdatedFields);

                                    if (update.MovementData != null && obj is WoWUnit)
                                    {
                                        // Only guard position writes for the local player (client-side prediction handles it).
                                        // Other units should always accept server position updates.
                                        bool isLocalPlayer = obj.Guid == PlayerGuid.FullGuid;
                                        var movementData = update.MovementData;

                                        // During teleports, clear queued moving/turn flags for local player updates.
                                        // This prevents stale MOVEFLAG_FORWARD from pre-teleport packets from getting re-applied.
                                        if (isLocalPlayer && _isBeingTeleported && movementData != null)
                                        {
                                            movementData = movementData.Clone();
                                            movementData.MovementFlags &= ~MovementFlags.MOVEFLAG_MASK_MOVING_OR_TURN;
                                        }

                                        bool allowPositionWrite = !isLocalPlayer || !(_isInControl && !_isBeingTeleported);
                                        ApplyMovementData((WoWUnit)obj, movementData, allowPositionWrite);

                                        Log.Verbose("[Movement-Update] Guid={Guid:X} Pos=({X:F2},{Y:F2},{Z:F2}) Flags=0x{Flags:X8}{Local}",
                                            update.Guid, movementData.X, movementData.Y,
                                            movementData.Z, (uint)movementData.MovementFlags,
                                            obj is WoWLocalPlayer ? " [LOCAL]" : "");
                                    }

                                    lock (_objectsLock) _objects[index] = obj;
                                    break;
                                }

                                case ObjectUpdateOperation.Remove:
                                {
                                    int removed;
                                    lock (_objectsLock) removed = _objects.RemoveAll(x => x.Guid == update.Guid);
                                    Log.Verbose("[Remove] Guid={Guid:X} (removed {Count})", update.Guid, removed);
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "[ProcessUpdates] Error processing {Op} for {Guid:X}",
                                update.Operation, update.Guid);
                        }
                    }
                }
                finally
                {
                    _updateSemaphore.Release();
                }

                await Task.Delay(10, token);
            }
        }

        private static void ApplyMovementData(WoWUnit unit, MovementInfoUpdate data, bool allowPositionWrite)
        {
            unit.MovementFlags = data.MovementFlags;
            unit.LastUpdated = data.LastUpdated;

            if (allowPositionWrite)
            {
                unit.Position.X = data.X;
                unit.Position.Y = data.Y;
                unit.Position.Z = data.Z;
            }

            unit.Facing = data.Facing;
            unit.TransportGuid = data.TransportGuid ?? 0;
            unit.TransportOffset = data.TransportOffset ?? unit.TransportOffset;
            unit.TransportOrientation = data.TransportOrientation ?? 0f;
            unit.TransportLastUpdated = data.TransportLastUpdated ?? 0;
            unit.SwimPitch = data.SwimPitch ?? 0f;
            unit.FallTime = data.FallTime;
            unit.JumpVerticalSpeed = data.JumpVerticalSpeed ?? 0f;
            unit.JumpSinAngle = data.JumpSinAngle ?? 0f;
            unit.JumpCosAngle = data.JumpCosAngle ?? 0f;
            unit.JumpHorizontalSpeed = data.JumpHorizontalSpeed ?? 0f;
            unit.SplineElevation = data.SplineElevation ?? 0f;

            if (data.MovementBlockUpdate != null)
            {
                unit.WalkSpeed = data.MovementBlockUpdate.WalkSpeed;
                unit.RunSpeed = data.MovementBlockUpdate.RunSpeed;
                unit.RunBackSpeed = data.MovementBlockUpdate.RunBackSpeed;
                unit.SwimSpeed = data.MovementBlockUpdate.SwimSpeed;
                unit.SwimBackSpeed = data.MovementBlockUpdate.SwimBackSpeed;
                unit.TurnRate = data.MovementBlockUpdate.TurnRate;
                unit.SplineFlags = data.MovementBlockUpdate.SplineFlags ?? SplineFlags.None;
                unit.SplineFinalPoint = data.MovementBlockUpdate.SplineFinalPoint ?? unit.SplineFinalPoint;
                unit.SplineTargetGuid = data.MovementBlockUpdate.SplineTargetGuid ?? 0;
                unit.SplineFinalOrientation = data.MovementBlockUpdate.SplineFinalOrientation ?? 0f;
                unit.SplineTimePassed = data.MovementBlockUpdate.SplineTimePassed ?? 0;
                unit.SplineDuration = data.MovementBlockUpdate.SplineDuration ?? 0;
                unit.SplineId = data.MovementBlockUpdate.SplineId ?? 0;
                unit.SplineNodes = data.MovementBlockUpdate.SplineNodes ?? [];
                unit.SplineFinalDestination = data.MovementBlockUpdate.SplineFinalDestination ?? unit.SplineFinalDestination;
                unit.SplineType = data.MovementBlockUpdate.SplineType;
                unit.SplineTargetGuid = data.MovementBlockUpdate.FacingTargetGuid;
                unit.FacingAngle = data.MovementBlockUpdate.FacingAngle;
                unit.FacingSpot = data.MovementBlockUpdate.FacingSpot;
                unit.SplineTimestamp = data.MovementBlockUpdate.SplineTimestamp;
                unit.SplinePoints = data.MovementBlockUpdate.SplinePoints;
            }
        }

        private static void ApplyContainerFieldDiffs(
            WoWContainer container,
            uint key,
            object? value
        )
        {
            var field = (EContainerFields)key;
            switch (field)
            {
                case EContainerFields.CONTAINER_FIELD_NUM_SLOTS:
                    container.NumOfSlots = (int)value;
                    break;
                case EContainerFields.CONTAINER_ALIGN_PAD:
                    break;
                case >= EContainerFields.CONTAINER_FIELD_SLOT_1
                and <= EContainerFields.CONTAINER_FIELD_SLOT_LAST:
                    {
                        // Store both low and high GUID parts at their natural offsets
                        // Slots[slot*2] = low part, Slots[slot*2+1] = high part
                        var slotFieldOffset =
                            (uint)field - (uint)EContainerFields.CONTAINER_FIELD_SLOT_1;

                        if (slotFieldOffset < container.Slots.Length)
                        {
                            container.Slots[slotFieldOffset] = (uint)value;
                        }
                    }
                    break;
                case EContainerFields.CONTAINER_END:
                    break;
            }
        }

        private static void ApplyUnitFieldDiffs(WoWUnit unit, uint key, object? value)
        {
            var field = (EUnitFields)key;
            switch (field)
            {
                case EUnitFields.UNIT_FIELD_CHARM:
                    unit.Charm.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHARM + 1:
                    unit.Charm.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMON:
                    unit.Summon.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMON + 1:
                    unit.Summon.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHARMEDBY:
                    unit.CharmedBy.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHARMEDBY + 1:
                    unit.CharmedBy.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMONEDBY:
                    unit.SummonedBy.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMONEDBY + 1:
                    unit.SummonedBy.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CREATEDBY:
                    unit.CreatedBy.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CREATEDBY + 1:
                    unit.CreatedBy.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_TARGET:
                    unit.TargetHighGuid.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_TARGET + 1:
                    unit.TargetHighGuid.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_PERSUADED:
                    unit.Persuaded.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_PERSUADED + 1:
                    unit.Persuaded.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHANNEL_OBJECT:
                    unit.ChannelObject.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHANNEL_OBJECT + 1:
                    unit.ChannelObject.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_HEALTH:
                    unit.Health = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER1:
                    unit.Powers[Powers.MANA] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER2:
                    unit.Powers[Powers.RAGE] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER3:
                    unit.Powers[Powers.FOCUS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER4:
                    unit.Powers[Powers.ENERGY] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER5:
                    unit.Powers[Powers.HAPPINESS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXHEALTH:
                    unit.MaxHealth = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER1:
                    unit.MaxPowers[Powers.MANA] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER2:
                    unit.MaxPowers[Powers.RAGE] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER3:
                    unit.MaxPowers[Powers.FOCUS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER4:
                    unit.MaxPowers[Powers.ENERGY] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER5:
                    unit.MaxPowers[Powers.HAPPINESS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_LEVEL:
                    unit.Level = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_FACTIONTEMPLATE:
                    unit.FactionTemplate = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BYTES_0:
                    byte[] value1 = (byte[])value;

                    unit.Bytes0[0] = value1[0];
                    unit.Bytes0[1] = value1[1];
                    unit.Bytes0[2] = value1[2];
                    unit.Bytes0[3] = value1[3];

                    // Unpack Race/Class/Gender for player objects
                    if (unit is WoWPlayer player0)
                    {
                        player0.Race = (Race)value1[0];
                        player0.Class = (Class)value1[1];
                        player0.Gender = (Gender)value1[2];
                    }
                    break;
                case >= EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_DISPLAY
                and <= EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_DISPLAY_02:
                    unit.VirtualItemSlotDisplay[
                        field - EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_DISPLAY
                    ] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_VIRTUAL_ITEM_INFO
                and <= EUnitFields.UNIT_VIRTUAL_ITEM_INFO_05:
                    unit.VirtualItemInfo[field - EUnitFields.UNIT_VIRTUAL_ITEM_INFO] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_FLAGS:
                    unit.UnitFlags = (UnitFlags)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURA
                and <= EUnitFields.UNIT_FIELD_AURA_LAST:
                    unit.AuraFields[field - EUnitFields.UNIT_FIELD_AURA] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURAFLAGS
                and <= EUnitFields.UNIT_FIELD_AURAFLAGS_05:
                    unit.AuraFlags[field - EUnitFields.UNIT_FIELD_AURAFLAGS] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURALEVELS
                and <= EUnitFields.UNIT_FIELD_AURALEVELS_LAST:
                    unit.AuraLevels[field - EUnitFields.UNIT_FIELD_AURALEVELS] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURAAPPLICATIONS
                and <= EUnitFields.UNIT_FIELD_AURAAPPLICATIONS_LAST:
                    unit.AuraApplications[field - EUnitFields.UNIT_FIELD_AURAAPPLICATIONS] =
                        (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_AURASTATE:
                    unit.AuraState = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BASEATTACKTIME:
                    unit.BaseAttackTime = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_OFFHANDATTACKTIME:
                    unit.OffhandAttackTime = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGEDATTACKTIME:
                    unit.OffhandAttackTime1 = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_BOUNDINGRADIUS:
                    unit.BoundingRadius = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_COMBATREACH:
                    unit.CombatReach = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_DISPLAYID:
                    unit.DisplayId = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_NATIVEDISPLAYID:
                    unit.NativeDisplayId = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MOUNTDISPLAYID:
                    unit.MountDisplayId = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MINDAMAGE:
                    unit.MinDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXDAMAGE:
                    unit.MaxDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MINOFFHANDDAMAGE:
                    unit.MinOffhandDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXOFFHANDDAMAGE:
                    unit.MaxOffhandDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BYTES_1:
                    byte[] value2 = (byte[])value;

                    unit.Bytes1[0] = value2[0];
                    unit.Bytes1[1] = value2[1];
                    unit.Bytes1[2] = value2[2];
                    unit.Bytes1[3] = value2[3];
                    break;
                case EUnitFields.UNIT_FIELD_PETNUMBER:
                    unit.PetNumber = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_PET_NAME_TIMESTAMP:
                    unit.PetNameTimestamp = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_PETEXPERIENCE:
                    unit.PetExperience = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_PETNEXTLEVELEXP:
                    unit.PetNextLevelExperience = (uint)value;
                    break;
                case EUnitFields.UNIT_DYNAMIC_FLAGS:
                    unit.DynamicFlags = (DynamicFlags)value;
                    break;
                case EUnitFields.UNIT_CHANNEL_SPELL:
                    unit.ChannelingId = (uint)value;
                    break;
                case EUnitFields.UNIT_MOD_CAST_SPEED:
                    unit.ModCastSpeed = (float)value;
                    break;
                case EUnitFields.UNIT_CREATED_BY_SPELL:
                    unit.CreatedBySpell = (uint)value;
                    break;
                case EUnitFields.UNIT_NPC_FLAGS:
                    unit.NpcFlags = (NPCFlags)value;
                    break;
                case EUnitFields.UNIT_NPC_EMOTESTATE:
                    unit.NpcEmoteState = (uint)value;
                    break;
                case EUnitFields.UNIT_TRAINING_POINTS:
                    unit.TrainingPoints = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT0:
                    unit.Strength = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT1:
                    unit.Agility = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT2:
                    unit.Stamina = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT3:
                    unit.Intellect = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT4:
                    unit.Spirit = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_RESISTANCES
                and <= EUnitFields.UNIT_FIELD_RESISTANCES_06:
                    unit.Resistances[field - EUnitFields.UNIT_FIELD_RESISTANCES] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BASE_MANA:
                    unit.BaseMana = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BASE_HEALTH:
                    unit.BaseHealth = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BYTES_2:
                    byte[] value3 = (byte[])value;

                    unit.Bytes2[0] = value3[0];
                    unit.Bytes2[1] = value3[1];
                    unit.Bytes2[2] = value3[2];
                    unit.Bytes2[3] = value3[3];
                    break;
                case EUnitFields.UNIT_FIELD_ATTACK_POWER:
                    unit.AttackPower = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_ATTACK_POWER_MODS:
                    unit.AttackPowerMods = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_ATTACK_POWER_MULTIPLIER:
                    unit.AttackPowerMultipler = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGED_ATTACK_POWER:
                    unit.RangedAttackPower = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGED_ATTACK_POWER_MODS:
                    unit.RangedAttackPowerMods = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGED_ATTACK_POWER_MULTIPLIER:
                    unit.RangedAttackPowerMultipler = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MINRANGEDDAMAGE:
                    unit.MinRangedDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXRANGEDDAMAGE:
                    unit.MaxRangedDamage = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_POWER_COST_MODIFIER
                and <= EUnitFields.UNIT_FIELD_POWER_COST_MODIFIER_06:
                    unit.PowerCostModifers[field - EUnitFields.UNIT_FIELD_POWER_COST_MODIFIER] =
                        (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_POWER_COST_MULTIPLIER
                and <= EUnitFields.UNIT_FIELD_POWER_COST_MULTIPLIER_06:
                    unit.PowerCostMultipliers[
                        field - EUnitFields.UNIT_FIELD_POWER_COST_MULTIPLIER
                    ] = (uint)value;
                    break;
            }
        }

        private static void ApplyFieldDiffs(WoWObject obj, Dictionary<uint, object?> updatedFields)
        {
            foreach (var (key, value) in updatedFields)
            {
                if (value == null)
                    continue;

                bool fieldHandled = false;

                // Check object-specific fields first, in inheritance order (most specific to least specific)

                // WoWContainer (inherits from WoWItem)
                if (obj is WoWContainer container)
                {
                    if (Enum.IsDefined(typeof(EContainerFields), key))
                    {
                        ApplyContainerFieldDiffs(container, key, value);
                        fieldHandled = true;
                    }
                    else if (Enum.IsDefined(typeof(EItemFields), key))
                    {
                        ApplyItemFieldDiffs(container, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWItem (but not container since container was handled above)
                else if (obj is WoWItem item)
                {
                    if (Enum.IsDefined(typeof(EItemFields), key))
                    {
                        ApplyItemFieldDiffs(item, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWPlayer/WoWLocalPlayer (inherits from WoWUnit)
                else if (obj is WoWPlayer player)
                {
                    // Player fields use ranges (e.g., PACK_SLOT_1..PACK_SLOT_LAST) where only
                    // the first and last values are in the enum. Enum.IsDefined returns false for
                    // intermediate values, silently dropping inventory/bank slot fields.
                    // Use a range check instead.
                    if (key >= (uint)EPlayerFields.PLAYER_DUEL_ARBITER && key <= (uint)EPlayerFields.PLAYER_END)
                    {
                        ApplyPlayerFieldDiffs(player, key, value, _objects);
                        fieldHandled = true;
                    }
                    else if (key >= (uint)EUnitFields.UNIT_FIELD_CHARM && key < (uint)EUnitFields.UNIT_END)
                    {
                        ApplyUnitFieldDiffs(player, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWUnit (but not player since player was handled above)
                else if (obj is WoWUnit unit)
                {
                    // Same range-based check as player fields — unit fields have arrays
                    // (auras, aura flags, etc.) where intermediate values aren't in the enum.
                    if (key >= (uint)EUnitFields.UNIT_FIELD_CHARM && key < (uint)EUnitFields.UNIT_END)
                    {
                        ApplyUnitFieldDiffs(unit, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWGameObject
                else if (obj is WoWGameObject go)
                {
                    if (Enum.IsDefined(typeof(EGameObjectFields), key))
                    {
                        ApplyGameObjectFieldDiffs(go, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWDynamicObject
                else if (obj is WoWDynamicObject dyn)
                {
                    if (Enum.IsDefined(typeof(EDynamicObjectFields), key))
                    {
                        ApplyDynamicObjectFieldDiffs(dyn, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWCorpse
                else if (obj is WoWCorpse corpse)
                {
                    if (Enum.IsDefined(typeof(ECorpseFields), key))
                    {
                        ApplyCorpseFieldDiffs(corpse, key, value);
                        fieldHandled = true;
                    }
                }

                // Fall back to base object fields if no specific field type was handled
                if (!fieldHandled && Enum.IsDefined(typeof(EObjectFields), key))
                {
                    ApplyObjectFieldDiffs(obj, key, value);
                }
            }
        }

        private static void ApplyObjectFieldDiffs(WoWObject obj, uint key, object value)
        {
            var field = (EObjectFields)key;
            switch (field)
            {
                case EObjectFields.OBJECT_FIELD_GUID:
                    obj.HighGuid.LowGuidValue = (byte[])value; // COMMENTED OUT - should not modify object's own GUID
                    break;
                case EObjectFields.OBJECT_FIELD_GUID + 1:
                    obj.HighGuid.HighGuidValue = (byte[])value; // COMMENTED OUT - should not modify object's own GUID
                    break;
                case EObjectFields.OBJECT_FIELD_TYPE:
                    break;
                case EObjectFields.OBJECT_FIELD_ENTRY:
                    obj.Entry = (uint)value;
                    break;
                case EObjectFields.OBJECT_FIELD_SCALE_X:
                    obj.ScaleX = (float)value;
                    break;
            }
        }

        private static void ApplyItemFieldDiffs(WoWItem item, uint key, object value)
        {
            var field = (EItemFields)key;
            switch (field)
            {
                case EItemFields.ITEM_FIELD_OWNER:
                    item.Owner.LowGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_OWNER + 1:
                    item.Owner.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_CONTAINED:
                    {
                        var bytes = (byte[])value;
                        item.Contained.LowGuidValue = bytes;
                        break;
                    }
                case EItemFields.ITEM_FIELD_CONTAINED + 1:
                    item.Contained.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_CREATOR:
                    item.CreatedBy.LowGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_CREATOR + 1:
                    item.CreatedBy.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_GIFTCREATOR:
                    item.GiftCreator.LowGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_GIFTCREATOR + 1:
                    item.GiftCreator.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_STACK_COUNT:
                    item.StackCount = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_DURATION:
                    item.Duration = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES:
                    item.SpellCharges[0] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_01:
                    item.SpellCharges[1] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_02:
                    item.SpellCharges[2] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_03:
                    item.SpellCharges[3] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_04:
                    item.SpellCharges[4] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_FLAGS:
                    item.ItemDynamicFlags = (ItemDynFlags)value;
                    break;
                case EItemFields.ITEM_FIELD_ENCHANTMENT:
                    item.Enchantments[0] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_PROPERTY_SEED:
                    item.PropertySeed = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_RANDOM_PROPERTIES_ID:
                    item.PropertySeed = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_ITEM_TEXT_ID:
                    item.ItemTextId = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_DURABILITY:
                    item.Durability = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_MAXDURABILITY:
                    item.MaxDurability = (uint)value;
                    break;
                case EItemFields.ITEM_END:
                    break;
            }
        }

        private static void ApplyGameObjectFieldDiffs(WoWGameObject go, uint key, object value)
        {
            var field = (EGameObjectFields)key;
            switch (field)
            {
                case EGameObjectFields.OBJECT_FIELD_CREATED_BY:
                    go.CreatedBy.LowGuidValue = (byte[])value;
                    break;
                case EGameObjectFields.OBJECT_FIELD_CREATED_BY + 1:
                    go.CreatedBy.HighGuidValue = (byte[])value;
                    break;
                case EGameObjectFields.GAMEOBJECT_DISPLAYID:
                    go.DisplayId = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_FLAGS:
                    go.Flags = ToUInt32(value);
                    break;
                case >= EGameObjectFields.GAMEOBJECT_ROTATION
                and < EGameObjectFields.GAMEOBJECT_STATE:
                    go.Rotation[key - (uint)EGameObjectFields.GAMEOBJECT_ROTATION] = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_STATE:
                    go.GoState = (GOState)ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_POS_X:
                    go.Position.X = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_POS_Y:
                    go.Position.Y = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_POS_Z:
                    go.Position.Z = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_FACING:
                    go.Facing = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_DYN_FLAGS:
                    go.DynamicFlags = (DynamicFlags)ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_FACTION:
                    go.FactionTemplate = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_TYPE_ID:
                    go.TypeId = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_LEVEL:
                    go.Level = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_ARTKIT:
                    go.ArtKit = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_ANIMPROGRESS:
                    go.AnimProgress = ToUInt32(value);
                    break;
            }
        }

        private static uint ToUInt32(object value) => value switch
        {
            uint u => u,
            int i => unchecked((uint)i),
            ushort us => us,
            short s => unchecked((uint)s),
            byte b => b,
            sbyte sb => unchecked((uint)sb),
            ulong ul => unchecked((uint)ul),
            long l => unchecked((uint)l),
            float f => unchecked((uint)MathF.Max(0f, f)),
            double d => unchecked((uint)Math.Max(0d, d)),
            Enum e => Convert.ToUInt32(e, CultureInfo.InvariantCulture),
            _ => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
        };

        private static float ToSingle(object value) => value switch
        {
            float f => f,
            double d => (float)d,
            uint u => u,
            int i => i,
            long l => l,
            ulong ul => ul,
            Enum e => Convert.ToUInt32(e, CultureInfo.InvariantCulture),
            _ => Convert.ToSingle(value, CultureInfo.InvariantCulture),
        };

        private static void ApplyDynamicObjectFieldDiffs(
            WoWDynamicObject dyn,
            uint key,
            object value
        )
        {
            var field = (EDynamicObjectFields)key;
            switch (field)
            {
                case EDynamicObjectFields.DYNAMICOBJECT_CASTER:
                    dyn.Caster.LowGuidValue = (byte[])value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_CASTER + 1:
                    dyn.Caster.HighGuidValue = (byte[])value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_BYTES:
                    dyn.Bytes = (byte[])value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_SPELLID:
                    dyn.SpellId = (uint)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_RADIUS:
                    dyn.Radius = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_POS_X:
                    dyn.Position.X = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_POS_Y:
                    dyn.Position.Y = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_POS_Z:
                    dyn.Position.Z = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_FACING:
                    dyn.Facing = (float)value;
                    break;
            }
        }

        private static void ApplyCorpseFieldDiffs(WoWCorpse corpse, uint key, object value)
        {
            var field = (ECorpseFields)key;
            switch (field)
            {
                case ECorpseFields.CORPSE_FIELD_OWNER:
                    corpse.OwnerGuid.LowGuidValue = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_OWNER + 1:
                    corpse.OwnerGuid.HighGuidValue = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_FACING:
                    corpse.Facing = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_POS_X:
                    corpse.Position.X = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_POS_Y:
                    corpse.Position.Y = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_POS_Z:
                    corpse.Position.Z = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_DISPLAY_ID:
                    corpse.DisplayId = (uint)value;
                    break;
                case >= ECorpseFields.CORPSE_FIELD_ITEM
                and < ECorpseFields.CORPSE_FIELD_BYTES_1:
                    corpse.Items[key - (uint)ECorpseFields.CORPSE_FIELD_ITEM] = (uint)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_BYTES_1:
                    corpse.Bytes1 = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_BYTES_2:
                    corpse.Bytes2 = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_GUILD:
                    corpse.Guild = (uint)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_FLAGS:
                    corpse.CorpseFlags = (CorpseFlags)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_DYNAMIC_FLAGS:
                    corpse.DynamicFlags = (DynamicFlags)value;
                    break;
            }
        }

        private static void ApplyPlayerFieldDiffs(
            WoWPlayer player,
            uint key,
            object value,
            List<WoWObject> objects
        )
        {
            var field = (EPlayerFields)key;
            Log.Verbose("[ApplyPlayerFieldDiffs] Field={Field} (0x{Key:X})", field, (uint)field);
            switch (field)
            {
                case EPlayerFields.PLAYER_FIELD_THIS_WEEK_CONTRIBUTION:
                    player.ThisWeekContribution = (uint)value;
                    break;
                case EPlayerFields.PLAYER_DUEL_ARBITER:
                    player.DuelArbiter.LowGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_DUEL_ARBITER + 1:
                    player.DuelArbiter.HighGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_FLAGS:
                    player.PlayerFlags = (PlayerFlags)value;
                    break;
                case EPlayerFields.PLAYER_GUILDID:
                    player.GuildId = (uint)value;
                    break;
                case EPlayerFields.PLAYER_GUILDRANK:
                    player.GuildRank = (uint)value;
                    break;
                case EPlayerFields.PLAYER_BYTES:
                    player.PlayerBytes = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_BYTES_2:
                    player.PlayerBytes2 = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_BYTES_3:
                    player.PlayerBytes3 = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_DUEL_TEAM:
                    player.GuildTimestamp = (uint)value;
                    break;
                case EPlayerFields.PLAYER_GUILD_TIMESTAMP:
                    player.GuildTimestamp = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_QUEST_LOG_1_1
                and <= EPlayerFields.PLAYER_QUEST_LOG_LAST_3:
                    {
                        uint questField = (field - EPlayerFields.PLAYER_QUEST_LOG_1_1) % 3;
                        int questIndex = (int)((field - EPlayerFields.PLAYER_QUEST_LOG_1_1) / 3);

                        if (questIndex >= 0 && questIndex < player.QuestLog.Length)
                        {
                            switch (questField)
                            {
                                case 0:
                                    player.QuestLog[questIndex].QuestId = (uint)value;
                                    break;
                                case 1:
                                    player.QuestLog[questIndex].QuestCounters = (byte[])value;
                                    break;
                                case 2:
                                    player.QuestLog[questIndex].QuestState = (uint)value;
                                    break;
                            }
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] QuestLog index {Index} out of bounds (length {Length})",
                                questIndex, player.QuestLog.Length);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_VISIBLE_ITEM_1_CREATOR
                and <= EPlayerFields.PLAYER_VISIBLE_ITEM_19_PAD:
                    {
                        uint visibleItemField =
                            (field - EPlayerFields.PLAYER_VISIBLE_ITEM_1_CREATOR) % 12;
                        int itemIndex = (int)(
                            (field - EPlayerFields.PLAYER_VISIBLE_ITEM_1_CREATOR) / 12
                        );
                        var visibleItem = player.VisibleItems[itemIndex];
                        switch (visibleItemField)
                        {
                            case 0:
                                visibleItem.CreatedBy.LowGuidValue = (byte[])value;
                                break;
                            case 1:
                                visibleItem.CreatedBy.HighGuidValue = (byte[])value;
                                break;
                            case 2:
                                ((WoWItem)visibleItem).ItemId = (uint)value;
                                break;
                            case 3:
                                visibleItem.Owner.LowGuidValue = (byte[])value;
                                break;
                            case 4:
                                visibleItem.Owner.HighGuidValue = (byte[])value;
                                break;
                            case 5:
                                visibleItem.Contained.LowGuidValue = (byte[])value;
                                break;
                            case 6:
                                visibleItem.Contained.HighGuidValue = (byte[])value;
                                break;
                            case 7:
                                visibleItem.GiftCreator.LowGuidValue = (byte[])value;
                                break;
                            case 8:
                                visibleItem.GiftCreator.HighGuidValue = (byte[])value;
                                break;
                            case 9:
                                ((WoWItem)visibleItem).StackCount = (uint)value;
                                break;
                            case 10:
                                ((WoWItem)visibleItem).Durability = (uint)value;
                                break;
                            case 11:
                                ((WoWItem)visibleItem).PropertySeed = (uint)value;
                                break;
                        }
                    }
                    break;

                case >= EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD
                and < EPlayerFields.PLAYER_FIELD_PACK_SLOT_1:
                    {
                        var inventoryIndex = field - EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD;
                        if (inventoryIndex >= 0 && inventoryIndex < player.Inventory.Length)
                        {
                            player.Inventory[inventoryIndex] = (uint)value;

                            // If this is a 2-byte field pair representing a GUID, populate VisibleItems
                            var itemGuid = (ulong)(uint)value;
                            if (itemGuid != 0 && inventoryIndex < player.VisibleItems.Length)
                            {
                                WoWItem actualItem;
                                lock (_objectsLock) actualItem = objects.FirstOrDefault(o => o.Guid == itemGuid) as WoWItem;
                                if (actualItem != null)
                                {
                                    player.VisibleItems[inventoryIndex] = actualItem;
                                }
                                else
                                {
                                    Log.Verbose("[ApplyPlayerFieldDiffs] No item found for GUID {Guid:X} at inventory index {Index}",
                                        itemGuid, inventoryIndex);
                                }
                            }
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] inventoryIndex {Index} out of bounds (length {Length}), field {Field}",
                                inventoryIndex, player.Inventory.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_PACK_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_PACK_SLOT_LAST:
                    {
                        var packIndex = field - EPlayerFields.PLAYER_FIELD_PACK_SLOT_1;
                        if (packIndex >= 0 && packIndex < player.PackSlots.Length)
                        {
                            var oldVal = player.PackSlots[packIndex];
                            player.PackSlots[packIndex] = (uint)value;
                            if ((uint)value != 0 && oldVal == 0)
                                Log.Information("[PackSlots] index={Index} set to 0x{Value:X8} (slot {Slot})",
                                    packIndex, (uint)value, packIndex / 2);
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] packIndex {Index} out of bounds (length {Length}), field {Field}",
                                packIndex, player.PackSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BANK_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_BANK_SLOT_LAST:
                    {
                        var bankIndex = field - EPlayerFields.PLAYER_FIELD_BANK_SLOT_1;
                        if (bankIndex >= 0 && bankIndex < player.BankSlots.Length)
                        {
                            player.BankSlots[bankIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] bankIndex {Index} out of bounds (length {Length}), field {Field}",
                                bankIndex, player.BankSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BANKBAG_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_BANKBAG_SLOT_LAST:
                    {
                        var bankBagIndex = field - EPlayerFields.PLAYER_FIELD_BANKBAG_SLOT_1;
                        if (bankBagIndex >= 0 && bankBagIndex < player.BankBagSlots.Length)
                        {
                            player.BankBagSlots[bankBagIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] bankBagIndex {Index} out of bounds (length {Length}), field {Field}",
                                bankBagIndex, player.BankBagSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_VENDORBUYBACK_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_VENDORBUYBACK_SLOT_LAST:
                    {
                        var vendorIndex = field - EPlayerFields.PLAYER_FIELD_VENDORBUYBACK_SLOT_1;
                        if (vendorIndex >= 0 && vendorIndex < player.VendorBuybackSlots.Length)
                        {
                            player.VendorBuybackSlots[vendorIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] vendorIndex {Index} out of bounds (length {Length}), field {Field}",
                                vendorIndex, player.VendorBuybackSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_KEYRING_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_KEYRING_SLOT_LAST:
                    {
                        var keyringIndex = field - EPlayerFields.PLAYER_FIELD_KEYRING_SLOT_1;
                        if (keyringIndex >= 0 && keyringIndex < player.KeyringSlots.Length)
                        {
                            player.KeyringSlots[keyringIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] keyringIndex {Index} out of bounds (length {Length}), field {Field}",
                                keyringIndex, player.KeyringSlots.Length, field);
                        }
                    }
                    break;
                case EPlayerFields.PLAYER_FARSIGHT:
                    player.Farsight = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_COMBO_TARGET:
                    player.ComboTarget.LowGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_FIELD_COMBO_TARGET + 1:
                    player.ComboTarget.HighGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_XP:
                    player.XP = (uint)value;
                    break;
                case EPlayerFields.PLAYER_NEXT_LEVEL_XP:
                    player.NextLevelXP = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_SKILL_INFO_1_1
                and <= EPlayerFields.PLAYER_SKILL_INFO_1_1 + 383:
                    {
                        // WoW 1.12.1 PLAYER_SKILL_INFO layout: INTERLEAVED, 3 fields per skill.
                        // Each skill slot occupies 3 consecutive uint32 fields:
                        //   offset + 0: SkillLine (low16) | Step (high16)   → SkillInt1
                        //   offset + 1: Current (low16)   | Max (high16)    → SkillInt2
                        //   offset + 2: TempBonus (low16) | PermBonus (high16) → SkillInt3
                        // Total: 128 skills × 3 fields = 384 fields.
                        int offset = (int)(field - EPlayerFields.PLAYER_SKILL_INFO_1_1);
                        int skillIndex = offset / 3;
                        int fieldType = offset % 3;
                        if (skillIndex < 128)
                        {
                            switch (fieldType)
                            {
                                case 0:
                                    player.SkillInfo[skillIndex].SkillInt1 = (uint)value;
                                    break;
                                case 1:
                                    player.SkillInfo[skillIndex].SkillInt2 = (uint)value;
                                    break;
                                case 2:
                                    player.SkillInfo[skillIndex].SkillInt3 = (uint)value;
                                    break;
                            }
                        }
                    }
                    break;
                case EPlayerFields.PLAYER_CHARACTER_POINTS1:
                    player.CharacterPoints1 = (uint)value;
                    break;
                case EPlayerFields.PLAYER_CHARACTER_POINTS2:
                    player.CharacterPoints2 = (uint)value;
                    break;
                case EPlayerFields.PLAYER_TRACK_CREATURES:
                    player.TrackCreatures = (uint)value;
                    break;
                case EPlayerFields.PLAYER_TRACK_RESOURCES:
                    player.TrackResources = (uint)value;
                    break;
                case EPlayerFields.PLAYER_BLOCK_PERCENTAGE:
                    player.BlockPercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_DODGE_PERCENTAGE:
                    player.DodgePercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_PARRY_PERCENTAGE:
                    player.ParryPercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_CRIT_PERCENTAGE:
                    player.CritPercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_RANGED_CRIT_PERCENTAGE:
                    player.RangedCritPercentage = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_EXPLORED_ZONES_1
                and < EPlayerFields.PLAYER_REST_STATE_EXPERIENCE:
                    player.ExploredZones[field - EPlayerFields.PLAYER_EXPLORED_ZONES_1] =
                        (uint)value;
                    break;
                case EPlayerFields.PLAYER_REST_STATE_EXPERIENCE:
                    player.RestStateExperience = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_COINAGE:
                    player.Coinage = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_POSSTAT0
                and <= EPlayerFields.PLAYER_FIELD_POSSTAT4:
                    player.StatBonusesPos[field - EPlayerFields.PLAYER_FIELD_POSSTAT0] =
                        (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_NEGSTAT0
                and <= EPlayerFields.PLAYER_FIELD_NEGSTAT4:
                    player.StatBonusesNeg[field - EPlayerFields.PLAYER_FIELD_NEGSTAT0] =
                        (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSPOSITIVE
                and <= EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE + 6:
                    if (field <= EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE)
                        player.ResistBonusesPos[
                            field - EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSPOSITIVE
                        ] = (uint)value;
                    else
                        player.ResistBonusesNeg[
                            field - EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE
                        ] = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_POS
                and <= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_POS + 6:
                    player.ModDamageDonePos[
                        field - EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_POS
                    ] = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_NEG
                and <= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_NEG + 6:
                    player.ModDamageDoneNeg[
                        field - EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_NEG
                    ] = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_PCT
                and <= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_PCT + 6:
                    player.ModDamageDonePct[
                        field - EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_PCT
                    ] = (float)value;
                    break;
                case EPlayerFields.PLAYER_AMMO_ID:
                    player.AmmoId = (uint)value;
                    break;
                case EPlayerFields.PLAYER_SELF_RES_SPELL:
                    player.SelfResSpell = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_PVP_MEDALS:
                    player.PvpMedals = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BUYBACK_PRICE_1
                and <= EPlayerFields.PLAYER_FIELD_BUYBACK_PRICE_LAST:
                    player.BuybackPrices[field - EPlayerFields.PLAYER_FIELD_BUYBACK_PRICE_1] =
                        (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BUYBACK_TIMESTAMP_1
                and <= EPlayerFields.PLAYER_FIELD_BUYBACK_TIMESTAMP_LAST:
                    player.BuybackTimestamps[
                        field - EPlayerFields.PLAYER_FIELD_BUYBACK_TIMESTAMP_1
                    ] = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_KILLS:
                    player.SessionKills = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_YESTERDAY_KILLS:
                    player.YesterdayKills = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_LAST_WEEK_KILLS:
                    player.LastWeekKills = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_LAST_WEEK_CONTRIBUTION:
                    player.LastWeekContribution = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_LIFETIME_HONORABLE_KILLS:
                    player.LifetimeHonorableKills = (uint)value;
                    break;
                // Note: PLAYER_FIELD_LIFETIME_DISHONORABLE_KILLS (0x4E8) is a vanilla-computed
                // value that collides with the visible items range (0x4DC-0x998) in this TBC enum.
                // Dishonorable kills was removed in TBC; this field is handled by the visible items range.
                case EPlayerFields.PLAYER_FIELD_BYTES2:
                    player.FieldBytes2 = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_FIELD_WATCHED_FACTION_INDEX:
                    player.WatchedFactionIndex = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_COMBAT_RATING_1
                and <= EPlayerFields.PLAYER_FIELD_COMBAT_RATING_1 + 20:
                    player.CombatRating[field - EPlayerFields.PLAYER_FIELD_COMBAT_RATING_1] =
                        (uint)value;
                    break;
                case EPlayerFields.PLAYER_CHOSEN_TITLE:
                    // Note: ChosenTitle property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER__FIELD_KNOWN_TITLES:
                    // Note: KnownTitles property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER__FIELD_KNOWN_TITLES + 1:
                    // Note: KnownTitles property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_HEALING_DONE_POS:
                    // Note: ModHealingDonePos property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_TARGET_RESISTANCE:
                    // Note: ModTargetResistance property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_FIELD_BYTES:
                    // Note: FieldBytes property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_OFFHAND_CRIT_PERCENTAGE:
                    // Note: OffhandCritPercentage property not implemented in WoWPlayer yet
                    break;
                case >= EPlayerFields.PLAYER_SPELL_CRIT_PERCENTAGE1
                and <= EPlayerFields.PLAYER_SPELL_CRIT_PERCENTAGE1 + 6:
                    // Note: SpellCritPercentage array not implemented in WoWPlayer yet
                    break;
                case >= EPlayerFields.PLAYER_FIELD_ARENA_TEAM_INFO_1_1
                and <= EPlayerFields.PLAYER_FIELD_ARENA_TEAM_INFO_1_1 + 17:
                    // Note: TBC-only — ArenaTeamInfo, HonorCurrency, ArenaCurrency
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_MANA_REGEN:
                    // Note: ModManaRegen property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_MANA_REGEN_INTERRUPT:
                    // Note: ModManaRegenInterrupt property not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_FIELD_MAX_LEVEL:
                    // Note: MaxLevel property not implemented in WoWPlayer yet
                    break;
                case >= EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1
                and <= EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1 + 9:
                    // Note: DailyQuests array not implemented in WoWPlayer yet
                    break;
                case EPlayerFields.PLAYER_FIELD_PADDING:
                    // Padding field, usually ignored
                    break;
            }
        }

        private static void ApplyMovementData(WoWUnit unit, MovementInfoUpdate data)
        {
            unit.MovementFlags = data.MovementFlags;
            unit.LastUpdated = data.LastUpdated;
            unit.Position.X = data.X;
            unit.Position.Y = data.Y;
            unit.Position.Z = data.Z;
            unit.Facing = data.Facing;
            unit.TransportGuid = data.TransportGuid ?? 0;
            unit.TransportOffset = data.TransportOffset ?? unit.TransportOffset;
            unit.TransportOrientation = data.TransportOrientation ?? 0f;
            unit.TransportLastUpdated = data.TransportLastUpdated ?? 0;
            unit.SwimPitch = data.SwimPitch ?? 0f;
            unit.FallTime = data.FallTime;
            unit.JumpVerticalSpeed = data.JumpVerticalSpeed ?? 0f;
            unit.JumpSinAngle = data.JumpSinAngle ?? 0f;
            unit.JumpCosAngle = data.JumpCosAngle ?? 0f;
            unit.JumpHorizontalSpeed = data.JumpHorizontalSpeed ?? 0f;
            unit.SplineElevation = data.SplineElevation ?? 0f;

            if (data.MovementBlockUpdate != null)
            {
                unit.WalkSpeed = data.MovementBlockUpdate.WalkSpeed;
                unit.RunSpeed = data.MovementBlockUpdate.RunSpeed;
                unit.RunBackSpeed = data.MovementBlockUpdate.RunBackSpeed;
                unit.SwimSpeed = data.MovementBlockUpdate.SwimSpeed;
                unit.SwimBackSpeed = data.MovementBlockUpdate.SwimBackSpeed;
                unit.TurnRate = data.MovementBlockUpdate.TurnRate;
                unit.SplineFlags = data.MovementBlockUpdate.SplineFlags ?? SplineFlags.None;
                unit.SplineFinalPoint = data.MovementBlockUpdate.SplineFinalPoint ?? unit.SplineFinalPoint;
                unit.SplineTargetGuid = data.MovementBlockUpdate.SplineTargetGuid ?? 0;
                unit.SplineFinalOrientation = data.MovementBlockUpdate.SplineFinalOrientation ?? 0f;
                unit.SplineTimePassed = data.MovementBlockUpdate.SplineTimePassed ?? 0;
                unit.SplineDuration = data.MovementBlockUpdate.SplineDuration ?? 0;
                unit.SplineId = data.MovementBlockUpdate.SplineId ?? 0;
                unit.SplineNodes = data.MovementBlockUpdate.SplineNodes ?? [];
                unit.SplineFinalDestination = data.MovementBlockUpdate.SplineFinalDestination ?? unit.SplineFinalDestination;
                unit.SplineType = data.MovementBlockUpdate.SplineType;
                unit.SplineTargetGuid = data.MovementBlockUpdate.FacingTargetGuid;
                unit.FacingAngle = data.MovementBlockUpdate.FacingAngle;
                unit.FacingSpot = data.MovementBlockUpdate.FacingSpot;
                unit.SplineTimestamp = data.MovementBlockUpdate.SplineTimestamp;
                unit.SplinePoints = data.MovementBlockUpdate.SplinePoints;
            }
        }

        #region NotImplemented

        public string ZoneText { get; private set; }

        public string MinimapZoneText { get; private set; }

        public string ServerName { get; private set; }

        public IGossipFrame GossipFrame { get; private set; }

        public ILootFrame LootFrame { get; private set; }

        public IMerchantFrame MerchantFrame { get; private set; }

        public ICraftFrame CraftFrame { get; private set; }

        public IQuestFrame QuestFrame { get; private set; }

        public IQuestGreetingFrame QuestGreetingFrame { get; private set; }

        public ITaxiFrame TaxiFrame { get; private set; }

        public ITradeFrame TradeFrame { get; private set; }

        public ITrainerFrame TrainerFrame { get; private set; }

        public ITalentFrame TalentFrame { get; private set; }

        public bool IsSpellReady(string spellName)
        {
            // Resolve spell name to highest-rank ID the player knows
            var knownIds = Spells.Select(s => s.Id);
            var spellId = GameData.Core.Constants.SpellData.GetHighestKnownRank(spellName, knownIds);

            // Spell not known
            if (spellId == 0) return false;

            // Check cooldown via delegate if wired, otherwise assume ready (server validates)
            if (_spellCooldownChecker != null)
                return _spellCooldownChecker(spellId);

            return true;
        }

        public void StopCasting()
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CANCEL_CAST, []);
        }

        public void CastSpell(string spellName, int rank = -1, bool castOnSelf = false)
        {
            // Resolve spell name to highest-rank ID the player knows
            var knownIds = Spells.Select(s => s.Id);
            var spellId = GameData.Core.Constants.SpellData.GetHighestKnownRank(spellName, knownIds);

            if (spellId == 0)
            {
                Log.Warning("[CastSpell] Spell '{SpellName}' not found in known spells or SpellData lookup", spellName);
                return;
            }

            CastSpell((int)spellId, rank, castOnSelf);
        }

        public void CastSpell(int spellId, int rank = -1, bool castOnSelf = false)
        {
            if (_woWClient == null) return;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);

            if (castOnSelf || _currentTargetGuid == 0)
            {
                // TARGET_FLAG_SELF = 0x0000 - server uses caster as target
                w.Write((ushort)0x0000);
                Log.Information("[CastSpell] spell={SpellId} targetSelf (guid=0x{Guid:X})", spellId, _currentTargetGuid);
            }
            else
            {
                // TARGET_FLAG_UNIT = 0x0002 - target a specific unit
                w.Write((ushort)0x0002);
                ReaderUtils.WritePackedGuid(w, _currentTargetGuid);
                Log.Information("[CastSpell] spell={SpellId} targetUnit=0x{Guid:X} packetHex={Hex}",
                    spellId, _currentTargetGuid, BitConverter.ToString(ms.ToArray()));
            }

            var payload = ms.ToArray();
            Log.Information("[CastSpell] Sending CMSG_CAST_SPELL ({Len} bytes): {Hex}",
                payload.Length, BitConverter.ToString(payload));
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[CastSpell] SEND FAILED for spell {SpellId}", spellId);
                    else
                        Log.Information("[CastSpell] SEND OK for spell {SpellId}", spellId);
                });
        }

        public void CastSpellOnGameObject(int spellId, ulong gameObjectGuid)
        {
            if (_woWClient == null) return;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);
            // TARGET_FLAG_OBJECT = 0x0800 — MaNGOS reads packed GO GUID for this flag
            w.Write((ushort)0x0800);
            ReaderUtils.WritePackedGuid(w, gameObjectGuid);

            var payload = ms.ToArray();
            Log.Information("[CastSpellOnGameObject] spell={SpellId} target=0x{Guid:X} ({Len} bytes): {Hex}",
                spellId, gameObjectGuid, payload.Length, BitConverter.ToString(payload));
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload);
        }

        public bool CanCastSpell(int spellId, ulong targetGuid)
        {
            return Spells.Any(s => s.Id == (uint)spellId);
        }

        public IReadOnlyCollection<uint> KnownSpellIds => Spells.Select(s => s.Id).ToArray();

        public void UseItem(int bagId, int slotId, ulong targetGuid = 0)
        {
            if (_woWClient == null) return;
            // For backpack (0xFF): slot uses ABSOLUTE inventory index (23 = INVENTORY_SLOT_ITEM_START).
            byte srcBag = bagId == 0 ? (byte)0xFF : (byte)(18 + bagId);
            byte srcSlot = bagId == 0 ? (byte)(23 + slotId) : (byte)slotId;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(srcBag);
            w.Write(srcSlot);
            w.Write((byte)0); // spellSlot
            w.Write((ushort)0x0000); // TARGET_FLAG_SELF
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_USE_ITEM, ms.ToArray());
        }

        public ulong GetBackpackItemGuid(int parSlot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return 0;
            int index = parSlot * 2;
            if (index < 0 || index + 1 >= player.PackSlots.Length) return 0;
            return ((ulong)player.PackSlots[index + 1] << 32) | player.PackSlots[index];
        }

        public ulong GetEquippedItemGuid(EquipSlot slot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return 0;
            // EquipSlot enum is offset by 1 from WoW internal slot numbering
            // (EquipSlot.Head=1 → internal slot 0, etc.)
            int internalSlot = (int)slot - 1;
            if (internalSlot < 0) return 0; // Ammo=0 has no inventory slot
            int index = internalSlot * 2;
            if (index + 1 >= player.Inventory.Length) return 0;
            return ((ulong)player.Inventory[index + 1] << 32) | player.Inventory[index];
        }

        public IWoWItem GetEquippedItem(EquipSlot slot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return null;

            // Try VisibleItems first (already populated with full item data)
            int slotIndex = (int)slot - 1;
            if (slotIndex >= 0 && slotIndex < player.VisibleItems.Length)
            {
                var visible = player.VisibleItems[slotIndex];
                if (visible?.ItemId > 0) return visible;
            }

            // Fall back to GUID lookup in objects list
            var guid = GetEquippedItemGuid(slot);
            return FindItemByGuid(guid);
        }

        public IWoWItem GetContainedItem(int bagSlot, int slotId)
        {
            ulong itemGuid;
            if (bagSlot == 0)
            {
                // Backpack — look up from PackSlots
                itemGuid = GetBackpackItemGuid(slotId);
            }
            else
            {
                // Extra bag — find the bag container, then look up its slot
                var bagEquipSlot = (EquipSlot)(19 + bagSlot); // Bag0=20 for bagSlot 1
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) return null;

                WoWContainer container;
                lock (_objectsLock)
                    container = _objects.FirstOrDefault(o => o.Guid == bagGuid) as WoWContainer;
                if (container == null) return null;

                itemGuid = container.GetItemGuid(slotId);
            }

            return FindItemByGuid(itemGuid);
        }

        public IEnumerable<IWoWItem> GetEquippedItems()
        {
            var player = Player as WoWPlayer;
            if (player == null) return [];

            var items = new List<IWoWItem>();
            // Equipment slots: Head(1) through Ranged(18)
            for (var slot = EquipSlot.Head; slot <= EquipSlot.Ranged; slot++)
            {
                var item = GetEquippedItem(slot);
                if (item != null && item.ItemId > 0) items.Add(item);
            }
            return items;
        }

        public IEnumerable<IWoWItem> GetContainedItems()
        {
            var player = Player as WoWPlayer;
            if (player == null) return [];

            var items = new List<IWoWItem>();

            // Backpack (bag 0): 16 slots
            for (int slot = 0; slot < 16; slot++)
            {
                var item = GetContainedItem(0, slot);
                if (item != null && item.ItemId > 0) items.Add(item);
            }

            // Extra bags (bag 1-4)
            for (int bag = 1; bag <= 4; bag++)
            {
                var bagEquipSlot = (EquipSlot)(19 + bag);
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) continue;

                WoWContainer container;
                lock (_objectsLock)
                    container = _objects.FirstOrDefault(o => o.Guid == bagGuid) as WoWContainer;
                if (container == null) continue;

                for (int slot = 0; slot < container.NumOfSlots; slot++)
                {
                    var item = FindItemByGuid(container.GetItemGuid(slot));
                    if (item != null && item.ItemId > 0) items.Add(item);
                }
            }

            return items;
        }

        public int CountFreeSlots(bool countSpecialSlots = false)
        {
            int freeSlots = 0;
            // Backpack: 16 slots
            for (int i = 0; i < 16; i++)
                if (GetContainedItem(0, i) == null) freeSlots++;
            // Extra bags
            for (int bag = 1; bag <= 4; bag++)
            {
                var bagEquipSlot = (EquipSlot)(19 + bag);
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) continue;
                WoWContainer container;
                lock (_objectsLock)
                    container = _objects.FirstOrDefault(o => o.Guid == bagGuid) as WoWContainer;
                if (container == null) continue;
                for (int slot = 0; slot < container.NumOfSlots; slot++)
                    if (FindItemByGuid(container.GetItemGuid(slot)) == null) freeSlots++;
            }
            return freeSlots;
        }

        public uint GetItemCount(uint itemId)
        {
            uint count = 0;
            foreach (var item in GetContainedItems())
                if (item.ItemId == itemId) count += item.StackCount;
            return count;
        }

        public void StartWandAttack()
        {
            // BG bot: cast "Shoot" spell (wand auto-attack)
            CastSpell("Shoot");
        }

        public void StopWandAttack()
        {
            // BG bot: stop casting to cancel wand auto-attack
            StopCasting();
        }

        public uint GetBagGuid(EquipSlot equipSlot)
        {
            // Return low 32 bits of the bag GUID for compatibility
            var guid = GetEquippedItemGuid(equipSlot);
            return (uint)(guid & 0xFFFFFFFF);
        }

        private WoWItem FindItemByGuid(ulong guid)
        {
            if (guid == 0) return null;
            lock (_objectsLock)
                return _objects.FirstOrDefault(o => o.Guid == guid) as WoWItem;
        }

        public void PickupContainedItem(int bagSlot, int slotId, int quantity) { }

        public void PlaceItemInContainer(int bagSlot, int slotId) { }

        public void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1)
        {
            if (_woWClient == null) { Log.Warning("[DestroyItem] _woWClient is null, cannot send packet"); return; }
            // For backpack (0xFF): slot uses ABSOLUTE inventory index (23 = INVENTORY_SLOT_ITEM_START).
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            byte count = quantity < 0 ? (byte)0xFF : (byte)Math.Min(quantity, 255);
            Log.Information("[DestroyItem] Sending CMSG_DESTROYITEM: bag=0x{Bag:X2}, slot={Slot}, count={Count}",
                srcBag, srcSlot, count);
            // CMSG_DESTROYITEM: bag(1) + slot(1) + count(1) + reserved(3) = 6 bytes
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_DESTROYITEM, [srcBag, srcSlot, count, 0, 0, 0]);
        }

        public void Logout()
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_LOGOUT_REQUEST, []);
        }

        public void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot) { }

        public void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null)
        {
            if (_woWClient == null) return;
            // Map logical bag index (0=backpack, 1-4=extra bags) to WoW packet bag/slot values.
            // For backpack (0xFF): slot uses ABSOLUTE inventory index (23 = INVENTORY_SLOT_ITEM_START).
            // For extra bags (19-22): slot is relative within the bag container.
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            // CMSG_AUTOEQUIP_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOEQUIP_ITEM, [srcBag, srcSlot]);
        }

        public void UnequipItem(EquipSlot slot) { }

        public void AcceptResurrect()
        {
            if (_woWClient == null) return;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(0UL); // resurrectorGuid — 0 = spirit healer
            w.Write((byte)1); // status = accept
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_RESURRECT_RESPONSE, ms.ToArray());
        }

        public IWoWPlayer PartyLeader => null;

        public ulong PartyLeaderGuid { get; set; }

        public ulong Party1Guid => 0;

        public ulong Party2Guid => 0;

        public ulong Party3Guid => 0;

        public ulong Party4Guid => 0;

        public ulong StarTargetGuid => 0;

        public ulong CircleTargetGuid => 0;

        public ulong DiamondTargetGuid => 0;

        public ulong TriangleTargetGuid => 0;

        public ulong MoonTargetGuid => 0;

        public ulong SquareTargetGuid => 0;

        public ulong CrossTargetGuid => 0;

        public ulong SkullTargetGuid => 0;

        public string GlueDialogText => string.Empty;

        public LoginStates LoginState => LoginStates.login;

        public void AntiAfk() { }

        public IWoWUnit GetTarget(IWoWUnit woWUnit)
        {
            if (woWUnit == null) return null;
            var targetGuid = woWUnit.TargetGuid;
            if (targetGuid == 0) return null;

            // Check if targeting the player
            if (targetGuid == PlayerGuid.FullGuid)
                return Player as IWoWUnit;

            // Search objects list
            lock (_objectsLock)
            {
                return _objects.OfType<IWoWUnit>().FirstOrDefault(u => u.Guid == targetGuid);
            }
        }

        public sbyte GetTalentRank(uint tabIndex, uint talentIndex)
        {
            return 0;
        }

        public void PickupInventoryItem(uint inventorySlot) { }

        public void DeleteCursorItem() { }

        public void EquipCursorItem() { }

        public void ConfirmItemEquip() { }

        /// <summary>
        /// Send MSG_MOVE_WORLDPORT_ACK to acknowledge a cross-map transfer.
        /// Called when SMSG_TRANSFER_PENDING is received.
        /// </summary>
        public void SendWorldportAck()
        {
            if (_woWClient == null) return;
            Serilog.Log.Information("[WorldportAck] Sending MSG_MOVE_WORLDPORT_ACK");
            _ = _woWClient.SendMSGPackedAsync(Opcode.MSG_MOVE_WORLDPORT_ACK, []);
        }

        public void SendChatMessage(string chatMessage)
        {
            if (_woWClient == null) return;
            // SAY chat requires a faction language, not Universal (server rejects language 0 for chat type 0)
            var language = Player?.Race switch
            {
                Race.Orc or Race.Undead or Race.Tauren or Race.Troll => Language.Orcish,
                _ => Language.Common,
            };
            _ = _woWClient.SendChatMessageAsync(ChatMsg.CHAT_MSG_SAY, language, "", chatMessage);
        }

        /// <summary>
        /// Sends a GM command (e.g. ".go xyz ...") and waits for the server's system message response.
        /// Returns all system messages received within the timeout window.
        /// </summary>
        public async Task<List<string>> SendGmCommandAsync(string command, int timeoutMs = 2000)
        {
            // Drain any stale messages
            DrainSystemMessages();

            SendChatMessage(command);

            // Wait for server response
            await Task.Delay(timeoutMs);

            return DrainSystemMessages();
        }

        public void SetRaidTarget(IWoWUnit target, TargetMarker v) { }

        public void JoinBattleGroundQueue() { }

        public void ResetInstances() { }

        public void PickupMacro(uint v) { }

        public void PlaceAction(uint v) { }

        public void InviteToGroup(ulong guid) { }

        public void InviteByName(string characterName) { }

        public void KickPlayer(ulong guid) { }

        public void AcceptGroupInvite() { }

        public void DeclineGroupInvite() { }

        public void LeaveGroup() { }

        public void DisbandGroup() { }

        public void ConvertToRaid() { }

        public bool HasPendingGroupInvite()
        {
            return false;
        }

        public bool HasLootRollWindow(int itemId)
        {
            return false;
        }

        public void LootPass(int itemId) { }

        public void LootRollGreed(int itemId) { }

        public void LootRollNeed(int itemId) { }

        public void AssignLoot(int itemId, ulong playerGuid) { }

        public void SetGroupLoot(GroupLootSetting setting) { }

        public void PromoteLootManager(ulong playerGuid) { }

        public void PromoteAssistant(ulong playerGuid) { }

        public void PromoteLeader(ulong playerGuid) { }

        public void DoEmote(Emote emote)
        {
            if (_woWClient == null) return;
            var packet = BitConverter.GetBytes((uint)emote);
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_EMOTE, packet);
        }

        public void DoEmote(TextEmote emote)
        {
            if (_woWClient == null) return;
            // CMSG_TEXT_EMOTE: uint32 textEmoteId + uint32 emoteNum + uint64 targetGuid
            using var ms = new System.IO.MemoryStream();
            using var w = new System.IO.BinaryWriter(ms);
            w.Write((uint)emote);
            w.Write(0u); // emoteNum
            w.Write(0UL); // targetGuid (self)
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_TEXT_EMOTE, ms.ToArray());
        }

        public uint GetManaCost(string spellName)
        {
            // Spell cost data not available from server packets in vanilla 1.12.1
            // Return 0 to indicate "can always attempt" — the server will reject if insufficient mana
            return 0;
        }

        public void MoveToward(Position pos)
        {
            if (pos == null || Player == null) return;

            // Face the target
            if (!Player.IsFacing(pos))
                SetFacing(Player.GetFacingForPosition(pos));

            // Keep directional intent deterministic: clear lateral/back flags before driving forward.
            StopMovement(ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
            StartMovement(ControlBits.Front);

            // Refresh waypoint when target changes; otherwise corpse-run can keep driving a stale blocked point.
            if (_movementController != null)
            {
                var currentWaypoint = _movementController.CurrentWaypoint;
                if (currentWaypoint == null || currentWaypoint.DistanceTo(pos) > 1f)
                    _movementController.SetTargetWaypoint(pos);
            }
        }

        public void MoveToward(Position position, float facing)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // Set facing and movement flags.
            // The MovementController (running in the game loop every 50ms) handles:
            //   - Physics step (ground snapping, collision, gravity)
            //   - Position update
            //   - Network packet sending (MSG_MOVE_START_FORWARD, heartbeats, etc.)
            player.Facing = facing;
            player.MovementFlags &= ~(MovementFlags.MOVEFLAG_BACKWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT | MovementFlags.MOVEFLAG_STRAFE_RIGHT);
            player.MovementFlags |= MovementFlags.MOVEFLAG_FORWARD;

            // Refresh waypoint when target changes; otherwise corpse-run can keep driving a stale blocked point.
            if (_movementController != null)
            {
                var currentWaypoint = _movementController.CurrentWaypoint;
                if (currentWaypoint == null || currentWaypoint.DistanceTo(position) > 1f)
                    _movementController.SetTargetWaypoint(position);
            }
        }

        /// <summary>
        /// Sets a full navigation path on the movement controller for waypoint-based following.
        /// The controller will interpolate Z from path waypoints and auto-advance through them.
        /// </summary>
        public void SetNavigationPath(Position[] path)
        {
            _movementController?.SetPath(path);
        }

        public void RefreshSkills() { }

        public void RefreshSpells() { }

        public void ReleaseSpirit()
        {
            if (_woWClient == null) return;
            Log.Information("[OBJMGR] ReleaseSpirit: sending CMSG_REPOP_REQUEST");
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_REPOP_REQUEST, []);
        }

        public void RetrieveCorpse()
        {
            if (_woWClient == null) return;
            // CMSG_RECLAIM_CORPSE: 8-byte ObjectGuid (zero = server infers from session)
            // Matches DeadActorClientComponent.ResurrectAtCorpseAsync() pattern.
            Log.Information("[OBJMGR] RetrieveCorpse: sending CMSG_RECLAIM_CORPSE (Player.Guid=0x{Guid:X16})", Player?.Guid ?? 0);
            var payload = new byte[8];
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_RECLAIM_CORPSE, payload);
        }

        public void InteractWithGameObject(ulong guid)
        {
            if (_woWClient == null) return;

            // Log player position + node distance for diagnostics (server silently drops if out of range)
            var player = Player;
            if (player != null)
            {
                var node = Objects.FirstOrDefault(o => o.Guid == guid);
                var nodeDist = node != null ? player.Position.DistanceTo(node.Position) : -1f;
                Log.Information("[GAMEOBJ_USE] Player pos=({X:F1},{Y:F1},{Z:F1}) flags=0x{Flags:X} nodeDist={Dist:F1}",
                    player.Position.X, player.Position.Y, player.Position.Z,
                    (uint)((WoWLocalPlayer)player).MovementFlags, nodeDist);
            }

            Log.Information("[GAMEOBJ_USE] _isBeingTeleported={Tp} _isInControl={Ctrl}",
                _isBeingTeleported, _isInControl);

            var payload = BitConverter.GetBytes(guid);
            Log.Information("[GAMEOBJ_USE] Sending CMSG_GAMEOBJ_USE for GUID=0x{Guid:X} (8 bytes: {Hex})",
                guid, BitConverter.ToString(payload));
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_GAMEOBJ_USE, payload);

            // Temporary packet sniffer: log all opcodes received for 5 seconds after GAMEOBJ_USE
            _sniffingGameObjUse = true;
            _sniffStartTime = DateTime.UtcNow;
            Task.Delay(5000).ContinueWith(_ =>
            {
                _sniffingGameObjUse = false;
                Log.Information("[GAMEOBJ_USE] Packet sniffer ended — no more logging");
            });
        }

        public void AutoStoreLootItem(byte slot)
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, new byte[] { slot });
        }

        public void ReleaseLoot(ulong lootGuid)
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_LOOT_RELEASE, BitConverter.GetBytes(lootGuid));
        }

        public async Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory != null)
            {
                await factory.LootingAgent.QuickLootAsync(targetGuid, ct);
            }
            else if (_woWClient != null)
            {
                // Fallback: send raw CMSG_LOOT packet
                await _woWClient.SendMSGPackedAsync(Opcode.CMSG_LOOT, BitConverter.GetBytes(targetGuid));
            }
        }

        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory != null)
            {
                await factory.VendorAgent.QuickVendorVisitAsync(vendorGuid, itemsToBuy, cancellationToken: ct);
            }
        }

        public async Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory != null)
            {
                await factory.MailAgent.QuickCollectAllMailAsync(mailboxGuid, ct);
            }
        }

        public async Task<int> LearnAllAvailableSpellsAsync(ulong trainerGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return 0;

            var trainer = factory.TrainerAgent;
            await trainer.OpenTrainerAsync(trainerGuid, ct);
            await Task.Delay(500, ct);

            var available = trainer.GetAvailableServices();
            if (available == null || available.Length == 0)
            {
                await trainer.CloseTrainerAsync(ct);
                return 0;
            }

            // Sort by cost (cheapest first), filter by player coinage
            var coinage = Player?.Coinage ?? uint.MaxValue;
            var affordable = available
                .Where(s => s.Cost <= coinage)
                .OrderBy(s => s.Cost)
                .ToList();

            int learned = 0;
            foreach (var spell in affordable)
            {
                try
                {
                    await trainer.LearnSpellAsync(trainerGuid, spell.SpellId, ct);
                    coinage -= spell.Cost;
                    learned++;
                    await Task.Delay(200, ct);
                }
                catch { break; }
            }

            await trainer.CloseTrainerAsync(ct);
            return learned;
        }

        public async Task<IReadOnlyList<uint>> DiscoverTaxiNodesAsync(ulong flightMasterGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return Array.Empty<uint>();

            var fm = factory.FlightMasterAgent;
            await fm.HelloFlightMasterAsync(flightMasterGuid, ct);

            for (int i = 0; i < 20; i++)
            {
                if (fm.IsTaxiMapOpen) break;
                await Task.Delay(100, ct);
            }

            var nodes = fm.AvailableTaxiNodes;
            try { await fm.CloseTaxiMapAsync(ct); } catch { }
            return nodes;
        }

        public async Task<bool> ActivateFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return false;

            var fm = factory.FlightMasterAgent;

            if (!fm.IsTaxiMapOpen)
            {
                await fm.HelloFlightMasterAsync(flightMasterGuid, ct);
                for (int i = 0; i < 20; i++)
                {
                    if (fm.IsTaxiMapOpen) break;
                    await Task.Delay(100, ct);
                }
                if (!fm.IsTaxiMapOpen) return false;
            }

            var sourceNodeId = fm.CurrentNodeId;
            if (!sourceNodeId.HasValue || !fm.IsNodeAvailable(destinationNodeId))
            {
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return false;
            }

            try
            {
                await fm.ActivateFlightAsync(flightMasterGuid, sourceNodeId.Value, destinationNodeId, ct);
                return true;
            }
            catch
            {
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return false;
            }
        }

        public async Task DepositExcessItemsAsync(ulong bankerGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var bank = factory.BankAgent;
            await bank.OpenBankAsync(bankerGuid, ct);

            for (int i = 0; i < 20; i++)
            {
                if (bank.IsBankWindowOpen) break;
                await Task.Delay(100, ct);
            }

            if (!bank.IsBankWindowOpen) return;

            int deposited = 0;
            for (byte bag = 0; bag < 5 && deposited < 10; bag++)
            {
                byte maxSlots = bag == 0 ? (byte)16 : (byte)20;
                for (byte slot = 0; slot < maxSlots && deposited < 10; slot++)
                {
                    var item = GetContainedItem(bag, slot);
                    if (item == null) continue;

                    // Keep consumables, quest items, reagents, keys, ammo
                    var info = item.Info;
                    if (info != null && (info.ItemClass == GameData.Core.Enums.ItemClass.Consumable
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Quest
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Reagent
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Key
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Lockpick
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Arrow
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Bullet))
                        continue;

                    try
                    {
                        await bank.DepositItemAsync(bag, slot, 0, ct);
                        deposited++;
                        await Task.Delay(200, ct);
                    }
                    catch { }
                }
            }

            await bank.CloseBankAsync(ct);
        }

        public async Task PostAuctionItemsAsync(ulong auctioneerGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var ah = factory.AuctionHouseAgent;
            await ah.OpenAuctionHouseAsync(auctioneerGuid, ct);

            for (int i = 0; i < 20; i++)
            {
                if (ah.IsAuctionHouseOpen) break;
                await Task.Delay(100, ct);
            }

            if (!ah.IsAuctionHouseOpen) return;

            var items = GetContainedItems()
                .Where(item => item.Quality >= GameData.Core.Enums.ItemQuality.Uncommon)
                .Take(5)
                .ToList();

            foreach (var item in items)
            {
                uint basePrice = item.Quality switch
                {
                    GameData.Core.Enums.ItemQuality.Uncommon => 5000u,
                    GameData.Core.Enums.ItemQuality.Rare => 50000u,
                    GameData.Core.Enums.ItemQuality.Epic => 500000u,
                    _ => 5000u,
                };
                int reqLevel = item.Info?.RequiredLevel ?? 1;
                uint startBid = (uint)(basePrice * (1f + reqLevel / 10f));
                uint buyout = (uint)(startBid * 1.5f);

                try
                {
                    await ah.PostAuctionAsync(item.Guid, startBid, buyout,
                        AuctionDuration.TwentyFourHours, ct);
                    await Task.Delay(300, ct);
                }
                catch { }
            }

            try { await ah.CloseAuctionHouseAsync(ct); } catch { }
        }

        public void SetTarget(ulong guid)
        {
            if (_woWClient == null) return;
            _currentTargetGuid = guid;
            var payload = BitConverter.GetBytes(guid);
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_SET_SELECTION, payload);
        }

        public void StopAttack()
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_ATTACKSTOP, []);
        }

        public void StartMeleeAttack()
        {
            if (_woWClient == null) return;
            // CMSG_ATTACKSWING requires the target's full 8-byte GUID
            var payload = BitConverter.GetBytes(_currentTargetGuid);
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_ATTACKSWING, payload);
        }

        public void StartRangedAttack()
        {
            // Ranged attack uses the same CMSG_ATTACKSWING opcode as melee
            StartMeleeAttack();
        }

        #endregion

        public enum ObjectUpdateOperation
        {
            Add,
            Update,
            Remove,
        }

        public record ObjectStateUpdate(
            ulong Guid,
            ObjectUpdateOperation Operation,
            WoWObjectType ObjectType,
            MovementInfoUpdate? MovementData,
            Dictionary<uint, object?> UpdatedFields
        );
    }
}
