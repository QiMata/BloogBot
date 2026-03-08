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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
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

        // Movement controller - handles all movement logic


        // Movement controller - handles all movement logic
        private MovementController _movementController;


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


        public bool HasEnteredWorld { get; internal set; }

        internal readonly object SpellLock = new();

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


        public void SetRaidTarget(IWoWUnit target, TargetMarker v) { }


        public void JoinBattleGroundQueue() { }


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
