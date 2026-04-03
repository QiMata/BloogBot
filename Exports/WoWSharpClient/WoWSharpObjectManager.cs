using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
using WoWSharpClient.Frames;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using WoWSharpClient.Parsers;
using WoWSharpClient.Screens;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;
using Enum = System.Enum;
using Timer = System.Timers.Timer;


namespace WoWSharpClient
{
    public partial class WoWSharpObjectManager : IObjectManager
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


        // Wrapper client for both auth and world transactions
        private WoWClient _woWClient;

        private PathfindingClient _pathfindingClient;
        private IPhysicsClient? _physicsClient;
        private SceneDataClient? _sceneDataClient;
        private bool _useLocalPhysics;

        // Movement controller - handles all movement logic
        private MovementController _movementController;
        private int _gameLoopReentrancyGuard; // 0 = idle, 1 = running


        private LoginScreen _loginScreen;

        private RealmSelectScreen _realmScreen;

        private CharacterSelectScreen _characterSelectScreen;


        private Timer _gameLoopTimer;


        private long _lastPingMs = 0;

        private WorldTimeTracker _worldTimeTracker;


        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);

        private Task _backgroundUpdateTask;

        private CancellationTokenSource _updateCancellation;

        // Optional cooldown checker — set by BackgroundBotWorker after SpellCastingNetworkClientComponent is created


        private WoWSharpObjectManager() { }


        public void Initialize(
            WoWClient wowClient,
            PathfindingClient pathfindingClient,
            ILogger<WoWSharpObjectManager> logger,
            IPhysicsClient? physicsClient = null,
            SceneDataClient? sceneDataClient = null,
            bool useLocalPhysics = false
        )
        {
            WoWSharpEventEmitter.Instance.Reset();
            lock (_objectsLock) _objects.Clear();
            _activePet = null;
            _pendingUpdates.Clear();

            _logger = logger;
            _pathfindingClient = pathfindingClient;
            _useLocalPhysics = useLocalPhysics;
            _physicsClient = useLocalPhysics
                ? physicsClient
                : sceneDataClient != null
                    ? physicsClient
                    : physicsClient ?? pathfindingClient;
            _sceneDataClient = sceneDataClient;
            _woWClient = wowClient;
            _worldTimeTracker = new WorldTimeTracker();
            _lastPositionUpdate = _worldTimeTracker.NowMS;
            _physicsTimeAccumulator = 0f;

            WoWSharpEventEmitter.Instance.OnLoginFailure += EventEmitter_OnLoginFailure;
            WoWSharpEventEmitter.Instance.OnLoginVerifyWorld += EventEmitter_OnLoginVerifyWorld;
            WoWSharpEventEmitter.Instance.OnWorldSessionStart += EventEmitter_OnWorldSessionStart;
            WoWSharpEventEmitter.Instance.OnWorldSessionEnd += EventEmitter_OnWorldSessionEnd;
            WoWSharpEventEmitter.Instance.OnCharacterListLoaded +=
                EventEmitter_OnCharacterListLoaded;
            WoWSharpEventEmitter.Instance.OnCharacterCreateResponse +=
                EventEmitter_OnCharacterCreateResponse;
            WoWSharpEventEmitter.Instance.OnChatMessage += EventEmitter_OnChatMessage;
            WoWSharpEventEmitter.Instance.OnForceMoveRoot += EventEmitter_OnForceMoveRoot;
            WoWSharpEventEmitter.Instance.OnForceMoveUnroot += EventEmitter_OnForceMoveUnroot;
            WoWSharpEventEmitter.Instance.OnMoveWaterWalk += EventEmitter_OnMoveWaterWalk;
            WoWSharpEventEmitter.Instance.OnMoveLandWalk += EventEmitter_OnMoveLandWalk;
            WoWSharpEventEmitter.Instance.OnMoveSetHover += EventEmitter_OnMoveSetHover;
            WoWSharpEventEmitter.Instance.OnMoveUnsetHover += EventEmitter_OnMoveUnsetHover;
            WoWSharpEventEmitter.Instance.OnMoveFeatherFall += EventEmitter_OnMoveFeatherFall;
            WoWSharpEventEmitter.Instance.OnMoveNormalFall += EventEmitter_OnMoveNormalFall;
            WoWSharpEventEmitter.Instance.OnForceWalkSpeedChange +=
                EventEmitter_OnForceWalkSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceRunSpeedChange +=
                EventEmitter_OnForceRunSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceRunBackSpeedChange +=
                EventEmitter_OnForceRunBackSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceSwimSpeedChange +=
                EventEmitter_OnForceSwimSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceSwimBackSpeedChange +=
                EventEmitter_OnForceSwimBackSpeedChange;
            WoWSharpEventEmitter.Instance.OnForceTurnRateChange +=
                EventEmitter_OnForceTurnRateChange;
            WoWSharpEventEmitter.Instance.OnForceMoveKnockBack += EventEmitter_OnForceMoveKnockBack;
            WoWSharpEventEmitter.Instance.OnForceTimeSkipped += EventEmitter_OnForceTimeSkipped;
            WoWSharpEventEmitter.Instance.OnTeleport += EventEmitter_OnTeleport;
            WoWSharpEventEmitter.Instance.OnClientControlUpdate +=
                EventEmitter_OnClientControlUpdate;
            WoWSharpEventEmitter.Instance.OnSetTimeSpeed += EventEmitter_OnSetTimeSpeed;
            WoWSharpEventEmitter.Instance.OnSpellGo += EventEmitter_OnSpellGo;

            // Restore player control when server-driven spline completes
            Splines.Instance.OnSplineCompleted += OnSplineCompleted;

            _loginScreen = new(_woWClient);
            _realmScreen = new(_woWClient);
            _characterSelectScreen = new(_woWClient);
            LootFrame = new NetworkLootFrame(() => _agentFactoryAccessor?.Invoke()?.LootingAgent);
        }

        private void InitializeMovementController()
        {
            // Initialize movement controller when we have a player
            if (Player != null
                && _woWClient != null
                && (_useLocalPhysics || _physicsClient != null || _sceneDataClient != null))
            {
                _movementController = new MovementController(
                    _woWClient,
                    _physicsClient,
                    (WoWLocalPlayer)Player,
                    _sceneDataClient
                );
                _movementController.OnStuckRecoveryRequired += HandleMovementControllerStuckRecovery;
            }
        }

        private int _movementStuckRecoveryGeneration;

        public int MovementStuckRecoveryGeneration => Volatile.Read(ref _movementStuckRecoveryGeneration);

        private void HandleMovementControllerStuckRecovery(int level, Position position)
        {
            var generation = Interlocked.Increment(ref _movementStuckRecoveryGeneration);
            Log.Warning(
                "[NAV-DIAG] MovementController stuck recovery signaled level={Level} generation={Generation} pos=({X:F1},{Y:F1},{Z:F1})",
                level,
                generation,
                position.X,
                position.Y,
                position.Z);
        }


        /// <summary>
        /// Optional callback invoked each game loop tick after movement/physics.
        /// Use this to drive bot AI logic (pathfinding, combat rotation, etc.).
        /// The float parameter is delta time in seconds.
        /// </summary>
        public Action<float>? OnBotTick { get; set; }


        public bool IsGameLoopRunning { get; private set; }

        /// <summary>
        /// Starts the game loop (~20 Hz timer). Physics runs at a fixed 50ms sub-step
        /// regardless of timer jitter. Prevents rubber banding and terrain sinking from
        /// variable delta times when System.Timers.Timer fires late under thread pool pressure.
        /// Tick order: spline updates → ping heartbeat → physics sub-steps → bot AI.
        /// Object updates are processed on a separate background thread.
        /// </summary>
        public void StartGameLoop()
        {
            if (IsGameLoopRunning) return;

            // Seed time tracking so the first tick doesn't see a huge delta from TimeSpan.Zero.
            if (_worldTimeTracker != null && _lastPositionUpdate == TimeSpan.Zero)
            {
                _lastPositionUpdate = _worldTimeTracker.NowMS;
                _physicsTimeAccumulator = 0f;
            }

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


        // Fixed physics timestep: 50ms (matches timer interval). When the timer fires late,
        // physics runs multiple sub-steps at this fixed dt instead of one large step.
        // This prevents rubber banding (large single-frame jumps) and sinking (over-integrated gravity).
        // Binary parity: WoW.exe runs CMovement::Update at render framerate (~30-60 FPS).
        // With local P/Invoke physics (zero IPC latency), we can match this rate.
        // 16ms = 60 FPS, matching typical WoW.exe framerate for smooth movement.
        private const float PHYSICS_FIXED_DT = 0.016f;
        // Maximum wall-clock delta to process per tick. Prevents runaway catch-up after long stalls
        // (e.g. GC pause, thread pool starvation). Caps at 4 sub-steps (200ms).
        private const float PHYSICS_MAX_DT = 0.200f;
        // Accumulated fractional time from previous tick, carried forward for sub-stepping.
        private float _physicsTimeAccumulator = 0f;
        private int _substepZeroCount = 0;

        private void OnGameLoopTick(object? sender, ElapsedEventArgs e)
        {
            // Reentrancy guard: System.Timers.Timer fires on ThreadPool threads.
            // If the physics IPC takes >50ms, the next tick fires concurrently.
            // Without this guard, two ticks read the same starting position and the
            // second overwrites the first's result — causing position oscillation.
            if (Interlocked.CompareExchange(ref _gameLoopReentrancyGuard, 1, 0) != 0)
                return;
            try
            {
                var now = _worldTimeTracker.NowMS;
                var delta = now - _lastPositionUpdate;
                var deltaSec = (float)delta.TotalMilliseconds / 1000f;

                // 1. Advance every monster/NPC spline before physics
                Splines.Instance.Update((float)delta.TotalMilliseconds);

                // 1a. Keep passenger world positions in sync with moving transports.
                SyncTransportPassengerWorldPositions();

                // 2. Handle ping heartbeat
                HandlePingHeartbeat((long)now.TotalMilliseconds);

                // 3. Update player movement if we're in control.
                // Allow physics during teleport if ground snap is pending — the bot needs gravity
                // to fall from teleport Z to the actual ground (e.g. teleported above a rooftop).
                var allowPhysics = _isInControl && Player != null && _movementController != null
                    && (!_isBeingTeleported || _movementController.NeedsGroundSnap);
                if (allowPhysics)
                {
                    // Sub-step physics at a fixed timestep to prevent rubber banding from timer jitter.
                    // System.Timers.Timer is thread-pool based — actual intervals can be 50-500ms.
                    // Without sub-stepping, a 200ms delta causes a single large physics step that
                    // moves the character 4x further than expected, then the server rubber-bands.
                    var clampedDelta = MathF.Min(deltaSec, PHYSICS_MAX_DT);
                    _physicsTimeAccumulator += clampedDelta;

                    var gameTimeMs = (uint)now.TotalMilliseconds;
                    int subSteps = 0;
                    while (_physicsTimeAccumulator >= PHYSICS_FIXED_DT)
                    {
                        var preX = Player.Position.X;
                        var preY = Player.Position.Y;
                        _movementController.Update(PHYSICS_FIXED_DT, gameTimeMs);
                        var postX = Player.Position.X;
                        var postY = Player.Position.Y;
                        var moved = MathF.Abs(postX - preX) >= 0.001f || MathF.Abs(postY - preY) >= 0.001f;
                        if (!moved && (Player.MovementFlags & GameData.Core.Enums.MovementFlags.MOVEFLAG_FORWARD) != 0)
                        {
                            _substepZeroCount++;
                            if (_substepZeroCount <= 3 || _substepZeroCount % 200 == 0)
                                Log.Warning("[SubStep] Zero-delta sub-step {SubStep}/{Total}: pre=({PreX:F3},{PreY:F3}) post=({PostX:F3},{PostY:F3}) accum={Accum:F4}",
                                    subSteps, (int)(_physicsTimeAccumulator / PHYSICS_FIXED_DT) + subSteps + 1,
                                    preX, preY, postX, postY, _physicsTimeAccumulator + PHYSICS_FIXED_DT);
                        }
                        _physicsTimeAccumulator -= PHYSICS_FIXED_DT;
                        // NOTE: Do NOT advance gameTimeMs between substeps. The virtual time
                        // increment caused packet timestamps to exceed real clock time, and on
                        // the next timer tick (uint)now.TotalMilliseconds would be LESS than the
                        // inflated gameTimeMs — the server detected backward timestamps as time
                        // manipulation and rubber-banded the player, causing ~50% of frames to
                        // have zero displacement. All substeps share the same real timestamp.
                        subSteps++;
                    }
                }

                // 3b. Clear teleport flag once ground snap is complete.
                // Previously _isBeingTeleported stayed true until the 500ms fallback timer,
                // blocking MoveToward and physics for up to 500ms after landing. Now we clear
                // it as soon as ground snap finishes, so the bot can immediately start moving.
                // This matches FG behavior where WoW.exe resumes movement immediately after
                // the teleport ACK + gravity settle.
                if (_isBeingTeleported && _movementController != null && !_movementController.NeedsGroundSnap)
                {
                    _isBeingTeleported = false;
                }

                // 4. Bot AI callback
                OnBotTick?.Invoke(deltaSec);

                _lastPositionUpdate = now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GameLoop] Tick error");
            }
            finally
            {
                Interlocked.Exchange(ref _gameLoopReentrancyGuard, 0);
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


        public bool HasEnteredWorld { get; internal set; }
        internal static TimeSpan WorldEntryRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
        internal bool HasPendingWorldEntry => Interlocked.Read(ref _pendingWorldEntryGuid) != 0;
        internal ulong PendingWorldEntryGuid => unchecked((ulong)Interlocked.Read(ref _pendingWorldEntryGuid));
        private long _pendingWorldEntryGuid;
        private long _pendingWorldEntryAttemptId;

        internal readonly object SpellLock = new();

        public List<Spell> Spells { get; internal set; } = [];

        public List<Cooldown> Cooldowns { get; internal set; } = [];

        // Quest objective progress tracking (updated from SMSG_QUESTUPDATE_ADD_KILL / ADD_ITEM)
        private readonly ConcurrentDictionary<(uint QuestId, uint ObjectId), QuestObjectiveProgress> _questObjectives = new();

        /// <summary>
        /// Real-time quest objective progress, keyed by (QuestId, ObjectId).
        /// Updated from SMSG_QUESTUPDATE_ADD_KILL and SMSG_QUESTUPDATE_ADD_ITEM packets.
        /// </summary>
        public IReadOnlyDictionary<(uint QuestId, uint ObjectId), QuestObjectiveProgress> QuestObjectives => _questObjectives;

        /// <summary>
        /// Updates kill-type quest objective progress. Called from QuestHandler.
        /// </summary>
        public void UpdateQuestKillProgress(uint questId, uint creatureEntry, uint current, uint required)
        {
            var key = (questId, creatureEntry);
            _questObjectives.AddOrUpdate(key,
                new QuestObjectiveProgress(questId, creatureEntry, current, required, QuestObjectiveTypes.Kill),
                (_, existing) => existing with { CurrentCount = current, RequiredCount = required });

            WoWSharpEventEmitter.Instance.FireOnQuestProgress();

            if (current >= required)
                WoWSharpEventEmitter.Instance.FireOnQuestObjectiveComplete();
        }

        /// <summary>
        /// Updates item-collection quest objective progress. Called from QuestHandler.
        /// The ADD_ITEM packet does not include the required count or questId — only itemId and current count.
        /// </summary>
        public void UpdateQuestItemProgress(uint itemId, uint current)
        {
            // ADD_ITEM has no questId — use 0 as a sentinel; callers should match by itemId
            var key = (0u, itemId);
            _questObjectives.AddOrUpdate(key,
                new QuestObjectiveProgress(0, itemId, current, 0, QuestObjectiveTypes.Collect),
                (_, existing) => existing with { CurrentCount = current });

            WoWSharpEventEmitter.Instance.FireOnQuestProgress();
        }

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


        private WoWLocalPet _activePet;

        public IWoWLocalPet Pet => _activePet;

        // Pet action bar and spell IDs populated from SMSG_PET_SPELLS
        private readonly List<(uint SpellId, byte ActionType)> _petActionBar = [];
        private readonly List<uint> _petSpellIds = [];
        private readonly object _petSpellLock = new();

        /// <summary>
        /// All pet spell IDs (action bar + additional spells) from SMSG_PET_SPELLS.
        /// </summary>
        internal IReadOnlyList<uint> PetSpellIds
        {
            get { lock (_petSpellLock) return _petSpellIds.ToArray(); }
        }

        /// <summary>
        /// Called by PetHandler when SMSG_PET_SPELLS is received.
        /// </summary>
        internal void SetPetSpells(ulong petGuid, List<(uint SpellId, byte ActionType)> actionBar, List<uint> spells)
        {
            lock (_petSpellLock)
            {
                _petActionBar.Clear();
                _petActionBar.AddRange(actionBar);
                _petSpellIds.Clear();
                // Merge action bar spell IDs and additional spells
                foreach (var entry in actionBar)
                    _petSpellIds.Add(entry.SpellId);
                foreach (var id in spells)
                {
                    if (!_petSpellIds.Contains(id))
                        _petSpellIds.Add(id);
                }
            }
        }

        /// <summary>
        /// Called by PetHandler when pet is dismissed (petGuid=0 or empty packet).
        /// </summary>
        internal void ClearPetSpells()
        {
            lock (_petSpellLock)
            {
                _petActionBar.Clear();
                _petSpellIds.Clear();
            }
        }

        /// <summary>
        /// Sends a CMSG_PET_ACTION packet. Called by WoWLocalPet methods.
        /// </summary>
        internal void SendPetAction(byte[] payload)
        {
            if (_woWClient == null)
            {
                Log.Warning("[PET] Cannot send CMSG_PET_ACTION — no world client connected");
                return;
            }
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_PET_ACTION, payload)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[PET] Failed to send CMSG_PET_ACTION");
                    else
                        Log.Debug("[PET] CMSG_PET_ACTION sent ({Len} bytes)", payload.Length);
                });
        }

        public ILoginScreen LoginScreen => _loginScreen;

        public IRealmSelectScreen RealmSelectScreen => _realmScreen;

        public ICharacterSelectScreen CharacterSelectScreen => _characterSelectScreen;


        public void EnterWorld(ulong characterGuid)
        {
            // Use the property setter (not the backing field) so it also
            // recreates the Player object with the correct GUID.
            // HasEnteredWorld intentionally flips true here so the login
            // sequence does not spam repeated CMSG_PLAYER_LOGIN packets while
            // we wait for SMSG_LOGIN_VERIFY_WORLD / object hydration.
            PlayerGuid = new HighGuid(characterGuid);
            HasEnteredWorld = true;
            if (_woWClient.WorldClient != null)
            {
                Interlocked.Exchange(ref _pendingWorldEntryGuid, unchecked((long)characterGuid));
                _ = _woWClient.EnterWorldAsync(characterGuid);
                SchedulePendingWorldEntryRetry(characterGuid);
            }
            else
            {
                ClearPendingWorldEntry();
            }

            InitializeMovementController();
        }

        public void ResetWorldSessionState(string source, bool preservePlayerGuid = true)
        {
            StopGameLoop();
            HasEnteredWorld = false;
            ClearPendingWorldEntry();
            _isInControl = false;
            _isBeingTeleported = false;
            _movementController = null;

            _pendingUpdates.Clear();
            lock (_objectsLock)
            {
                _objects.Clear();
            }
            _activePet = null;

            if (!preservePlayerGuid)
            {
                _playerGuid = new HighGuid(new byte[4], new byte[4]);
            }

            Player = new WoWLocalPlayer(_playerGuid);

            Log.Information("[WorldSession] Reset state from {Source}; preservePlayerGuid={Preserve}; guid=0x{Guid:X}",
                source, preservePlayerGuid, _playerGuid.FullGuid);
        }

        private void SchedulePendingWorldEntryRetry(ulong characterGuid)
        {
            var attemptId = Interlocked.Increment(ref _pendingWorldEntryAttemptId);
            Task.Delay(WorldEntryRetryDelay).ContinueWith(_ =>
            {
                if (Interlocked.Read(ref _pendingWorldEntryAttemptId) != attemptId)
                    return;

                var pendingGuid = unchecked((ulong)Interlocked.Read(ref _pendingWorldEntryGuid));
                if (pendingGuid == 0 || pendingGuid != characterGuid)
                    return;

                if (_woWClient.WorldClient == null)
                {
                    ClearPendingWorldEntry();
                    return;
                }

                Log.Warning("[WorldSession] Enter world timed out for guid 0x{Guid:X}. Retrying CMSG_PLAYER_LOGIN.",
                    characterGuid);

                _ = _woWClient.EnterWorldAsync(characterGuid);
                SchedulePendingWorldEntryRetry(characterGuid);
            });
        }

        private void ClearPendingWorldEntry()
        {
            Interlocked.Exchange(ref _pendingWorldEntryGuid, 0);
            Interlocked.Increment(ref _pendingWorldEntryAttemptId);
        }


        /// <summary>
        /// Logout and re-enter world with the same character. Waits for
        /// SMSG_LOGOUT_COMPLETE then sends CMSG_PLAYER_LOGIN. Returns true
        /// if the full cycle completed within the timeout.
        /// </summary>
        public async Task<bool> LogoutAndReenterWorldAsync(TimeSpan timeout)
        {
            if (_woWClient == null) return false;

            var characterGuid = PlayerGuid.FullGuid;
            if (characterGuid == 0)
            {
                Serilog.Log.Warning("[OBJMGR] LogoutAndReenterWorld: no character GUID");
                return false;
            }

            var worldClient = _woWClient.WorldClient;
            if (worldClient == null)
            {
                Serilog.Log.Warning("[OBJMGR] LogoutAndReenterWorld: no world client");
                return false;
            }

            // Subscribe to SMSG_LOGOUT_COMPLETE before sending request.
            var logoutTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var logoutSub = worldClient.LogoutComplete.Subscribe(_ => logoutTcs.TrySetResult(true));

            // Send CMSG_LOGOUT_REQUEST.
            HasEnteredWorld = false;
            Logout();
            Serilog.Log.Information("[OBJMGR] LogoutAndReenterWorld: sent CMSG_LOGOUT_REQUEST, waiting for SMSG_LOGOUT_COMPLETE...");

            // Wait for server to confirm logout.
            var logoutTask = logoutTcs.Task;
            if (await Task.WhenAny(logoutTask, Task.Delay(timeout)) != logoutTask)
            {
                Serilog.Log.Warning("[OBJMGR] LogoutAndReenterWorld: timed out waiting for SMSG_LOGOUT_COMPLETE");
                HasEnteredWorld = true; // restore state
                return false;
            }

            Serilog.Log.Information("[OBJMGR] LogoutAndReenterWorld: logout confirmed, re-entering world with GUID 0x{Guid:X}", characterGuid);

            // Brief pause to let server finalize the logout.
            await Task.Delay(1000);

            // Re-enter world with the same character.
            EnterWorld(characterGuid);

            // Wait for SMSG_LOGIN_VERIFY_WORLD (HasEnteredWorld will be set by the handler).
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (HasEnteredWorld && Player?.Position != null)
                {
                    Serilog.Log.Information("[OBJMGR] LogoutAndReenterWorld: re-entered world at ({X:F1},{Y:F1},{Z:F1})",
                        Player.Position.X, Player.Position.Y, Player.Position.Z);
                    return true;
                }
                await Task.Delay(200);
            }

            Serilog.Log.Warning("[OBJMGR] LogoutAndReenterWorld: timed out waiting for world re-entry");
            return false;
        }


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


        public void Logout()
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_LOGOUT_REQUEST, []);
        }


        public void AcceptResurrect()
        {
            if (_woWClient == null) return;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(0UL); // resurrectorGuid — 0 = spirit healer
            w.Write((byte)1); // status = accept
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_RESURRECT_RESPONSE, ms.ToArray());
        }


        public IWoWPlayer PartyLeader
        {
            get
            {
                var leaderGuid = PartyLeaderGuid;
                if (leaderGuid == 0) return null;
                lock (_objectsLock)
                {
                    return _objects.OfType<IWoWPlayer>().FirstOrDefault(p => p.Guid == leaderGuid);
                }
            }
        }


        public ulong PartyLeaderGuid
        {
            get
            {
                var factory = _agentFactoryAccessor?.Invoke();
                return factory?.PartyAgent?.LeaderGuid ?? _partyLeaderGuidOverride;
            }
            set => _partyLeaderGuidOverride = value;
        }
        private ulong _partyLeaderGuidOverride;


        public ulong Party1Guid => GetPartyMemberGuid(0);


        public ulong Party2Guid => GetPartyMemberGuid(1);


        public ulong Party3Guid => GetPartyMemberGuid(2);


        public ulong Party4Guid => GetPartyMemberGuid(3);

        private ulong GetPartyMemberGuid(int index)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            var members = factory?.PartyAgent?.GetGroupMembers();
            if (members == null || index >= members.Count) return 0;
            return members[index].Guid;
        }


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


        // Cursor state for inventory item pickup (equipment slots)
        private (byte Bag, byte Slot)? _cursorInventoryItem;

        public void PickupInventoryItem(uint inventorySlot)
        {
            // inventorySlot is the absolute equipment slot index (0=Head, 17=Ranged, etc.)
            _cursorInventoryItem = (0xFF, (byte)inventorySlot);
        }


        public void DeleteCursorItem()
        {
            // Delete whatever is currently on the cursor
            if (_cursorItem != null)
            {
                var src = _cursorItem.Value;
                _cursorItem = null;
                var factory = _agentFactoryAccessor?.Invoke();
                _ = factory?.InventoryAgent?.DestroyItemAsync(src.Bag, src.Slot, (uint)src.Quantity);
            }
            else if (_cursorInventoryItem != null)
            {
                var src = _cursorInventoryItem.Value;
                _cursorInventoryItem = null;
                var factory = _agentFactoryAccessor?.Invoke();
                _ = factory?.InventoryAgent?.DestroyItemAsync(src.Bag, src.Slot);
            }
        }


        public void EquipCursorItem()
        {
            // Equip whatever is on the cursor from a bag slot
            if (_cursorItem != null)
            {
                var src = _cursorItem.Value;
                _cursorItem = null;
                if (_woWClient != null)
                    _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOEQUIP_ITEM, [src.Bag, src.Slot]);
            }
        }


        public void ConfirmItemEquip() { }

        /// <summary>
        /// Send MSG_MOVE_WORLDPORT_ACK to acknowledge a cross-map transfer.
        /// Called when SMSG_TRANSFER_PENDING is received.
        /// </summary>


        public void SetRaidTarget(IWoWUnit target, TargetMarker marker)
        {
            if (target == null || _woWClient?.WorldClient == null) return;

            // MSG_RAID_TARGET_UPDATE (0x321): uint8 mode (0=set) + uint64 targetGuid + uint8 iconId
            // iconId: 0=none, 1=star, 2=circle, 3=diamond, 4=triangle, 5=moon, 6=square, 7=cross, 8=skull
            var payload = new byte[1 + 8 + 1];
            payload[0] = 0; // mode = set
            BitConverter.GetBytes(target.Guid).CopyTo(payload, 1);
            payload[9] = (byte)marker;

            _ = _woWClient.WorldClient.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.MSG_RAID_TARGET_UPDATE, payload);
        }


        public void JoinBattleGroundQueue() { }

        public void AcceptBattlegroundInvite()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.BattlegroundAgent != null)
                _ = factory.BattlegroundAgent.AcceptInviteAsync();
        }

        public void LeaveBattleground()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.BattlegroundAgent != null)
                _ = factory.BattlegroundAgent.LeaveAsync();
        }


        public void ResetInstances() { }


        public void PickupMacro(uint v) { }


        public void PlaceAction(uint v) { }


        public void InviteToGroup(ulong guid)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            var member = factory?.PartyAgent?.GetGroupMember(guid);
            if (member != null)
                _ = factory!.PartyAgent.InvitePlayerAsync(member.Name);
        }


        public void InviteByName(string characterName)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.InvitePlayerAsync(characterName);
        }


        public void KickPlayer(ulong guid)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.KickPlayerAsync(guid);
        }


        public void AcceptGroupInvite()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.AcceptInviteAsync();
        }


        public void DeclineGroupInvite()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.DeclineInviteAsync();
        }


        public void LeaveGroup()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.LeaveGroupAsync();
        }


        public void DisbandGroup()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.DisbandGroupAsync();
        }


        public void ConvertToRaid()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.ConvertToRaidAsync();
        }

        public void ChangeRaidSubgroup(string playerName, byte subGroup)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.ChangeSubGroupAsync(playerName, subGroup);
        }

        public bool HasPendingGroupInvite()
        {
            var factory = _agentFactoryAccessor?.Invoke();
            return factory?.PartyAgent?.HasPendingInvite ?? false;
        }


        public bool HasLootRollWindow(int itemId)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.LootingAgent == null) return false;
            var availableLoot = factory.LootingAgent.GetAvailableLoot();
            return availableLoot.Any(s => s.ItemId == (uint)itemId && s.RequiresRoll);
        }


        public void LootPass(int itemId)
        {
            var (lootGuid, slot) = FindPendingRollSlot(itemId);
            if (lootGuid == 0) return;
            var factory = _agentFactoryAccessor?.Invoke();
            _ = factory?.LootingAgent?.RollForLootAsync(lootGuid, slot, LootRollType.Pass);
        }


        public void LootRollGreed(int itemId)
        {
            var (lootGuid, slot) = FindPendingRollSlot(itemId);
            if (lootGuid == 0) return;
            var factory = _agentFactoryAccessor?.Invoke();
            _ = factory?.LootingAgent?.RollForLootAsync(lootGuid, slot, LootRollType.Greed);
        }


        public void LootRollNeed(int itemId)
        {
            var (lootGuid, slot) = FindPendingRollSlot(itemId);
            if (lootGuid == 0) return;
            var factory = _agentFactoryAccessor?.Invoke();
            _ = factory?.LootingAgent?.RollForLootAsync(lootGuid, slot, LootRollType.Need);
        }


        public void AssignLoot(int itemId, ulong playerGuid)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.LootingAgent == null) return;
            var slot = factory.LootingAgent.GetAvailableLoot()
                .FirstOrDefault(s => s.ItemId == (uint)itemId);
            if (slot != null)
                _ = factory.LootingAgent.AssignMasterLootAsync(slot.SlotIndex, playerGuid);
        }


        private (ulong LootGuid, byte Slot) FindPendingRollSlot(int itemId)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.LootingAgent == null) return (0, 0);
            var slot = factory.LootingAgent.GetAvailableLoot()
                .FirstOrDefault(s => s.ItemId == (uint)itemId && s.RequiresRoll && s.RollGuid.HasValue);
            if (slot == null) return (0, 0);
            return (slot.RollGuid!.Value, slot.SlotIndex);
        }


        public void SetGroupLoot(GroupLootSetting setting)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
            {
                // GroupLootSetting maps to master loot quality thresholds
                var threshold = setting switch
                {
                    GroupLootSetting.MasterLooterCommon => ItemQuality.Common,
                    GroupLootSetting.MasterLooterRare => ItemQuality.Rare,
                    GroupLootSetting.MasterLooterEpic => ItemQuality.Epic,
                    GroupLootSetting.MasterLooterLegendary => ItemQuality.Legendary,
                    _ => ItemQuality.Uncommon
                };
                _ = factory.PartyAgent.SetLootMethodAsync(LootMethod.MasterLooter, lootThreshold: threshold);
            }
        }


        public void PromoteLootManager(ulong playerGuid)
        {
            // Loot manager is set via SetLootMethodAsync with MasterLoot + lootMasterGuid
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.SetLootMethodAsync(LootMethod.MasterLooter, playerGuid);
        }


        public void PromoteAssistant(ulong playerGuid)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            var member = factory?.PartyAgent?.GetGroupMember(playerGuid);
            if (member != null)
                _ = factory!.PartyAgent.PromoteToAssistantAsync(member.Name);
        }


        public void PromoteLeader(ulong playerGuid)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.PartyAgent != null)
                _ = factory.PartyAgent.PromoteToLeaderAsync(playerGuid);
        }


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


        public void RefreshSkills() { }


        public void RefreshSpells() { }


        public record ObjectStateUpdate(
            ulong Guid,
            ObjectUpdateOperation Operation,
            WoWObjectType ObjectType,
            MovementInfoUpdate? MovementData,
            Dictionary<uint, object?> UpdatedFields
        );
    }
}
