using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Network Agent Factory that provides coordinated access to all network agents.
    /// Uses a lazy builder pattern where agents are created only when first accessed.
    /// </summary>
    public class NetworkAgentFactory : IAgentFactory
    {
        private readonly IWorldClient _worldClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<NetworkAgentFactory> _logger;

        // Lazy-initialized agents
        private ITargetingNetworkAgent? _targetingAgent;
        private IAttackNetworkAgent? _attackAgent;
        private IQuestNetworkAgent? _questAgent;
        private ILootingNetworkAgent? _lootingAgent;
        private IGameObjectNetworkAgent? _gameObjectAgent;
        private IVendorNetworkAgent? _vendorAgent;
        private IFlightMasterNetworkAgent? _flightMasterAgent;
        private IDeadActorAgent? _deadActorAgent;
        private IInventoryNetworkAgent? _inventoryAgent;
        private IItemUseNetworkAgent? _itemUseAgent;
        private IEquipmentNetworkAgent? _equipmentAgent;
        private ISpellCastingNetworkAgent? _spellCastingAgent;

        // Thread safety locks
        private readonly object _targetingLock = new object();
        private readonly object _attackLock = new object();
        private readonly object _questLock = new object();
        private readonly object _lootingLock = new object();
        private readonly object _gameObjectLock = new object();
        private readonly object _vendorLock = new object();
        private readonly object _flightMasterLock = new object();
        private readonly object _deadActorLock = new object();
        private readonly object _inventoryLock = new object();
        private readonly object _itemUseLock = new object();
        private readonly object _equipmentLock = new object();
        private readonly object _spellCastingLock = new object();

        // Event handler setup tracking
        private bool _eventsSetup = false;
        private readonly object _eventsLock = new object();

        #region Agent Access Properties

        /// <inheritdoc />
        public ITargetingNetworkAgent TargetingAgent
        {
            get
            {
                if (_targetingAgent == null)
                {
                    lock (_targetingLock)
                    {
                        if (_targetingAgent == null)
                        {
                            _logger.LogDebug("Creating TargetingNetworkAgent lazily");
                            _targetingAgent = AgentFactory.CreateTargetingNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("TargetingNetworkAgent created successfully");
                        }
                    }
                }
                return _targetingAgent;
            }
        }

        /// <inheritdoc />
        public IAttackNetworkAgent AttackAgent
        {
            get
            {
                if (_attackAgent == null)
                {
                    lock (_attackLock)
                    {
                        if (_attackAgent == null)
                        {
                            _logger.LogDebug("Creating AttackNetworkAgent lazily");
                            _attackAgent = AgentFactory.CreateAttackNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("AttackNetworkAgent created successfully");
                        }
                    }
                }
                return _attackAgent;
            }
        }

        /// <inheritdoc />
        public IQuestNetworkAgent QuestAgent
        {
            get
            {
                if (_questAgent == null)
                {
                    lock (_questLock)
                    {
                        if (_questAgent == null)
                        {
                            _logger.LogDebug("Creating QuestNetworkAgent lazily");
                            _questAgent = AgentFactory.CreateQuestNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("QuestNetworkAgent created successfully");
                        }
                    }
                }
                return _questAgent;
            }
        }

        /// <inheritdoc />
        public ILootingNetworkAgent LootingAgent
        {
            get
            {
                if (_lootingAgent == null)
                {
                    lock (_lootingLock)
                    {
                        if (_lootingAgent == null)
                        {
                            _logger.LogDebug("Creating LootingNetworkAgent lazily");
                            _lootingAgent = AgentFactory.CreateLootingNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("LootingNetworkAgent created successfully");
                        }
                    }
                }
                return _lootingAgent;
            }
        }

        /// <inheritdoc />
        public IGameObjectNetworkAgent GameObjectAgent
        {
            get
            {
                if (_gameObjectAgent == null)
                {
                    lock (_gameObjectLock)
                    {
                        if (_gameObjectAgent == null)
                        {
                            _logger.LogDebug("Creating GameObjectNetworkAgent lazily");
                            _gameObjectAgent = AgentFactory.CreateGameObjectNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("GameObjectNetworkAgent created successfully");
                        }
                    }
                }
                return _gameObjectAgent;
            }
        }

        /// <inheritdoc />
        public IVendorNetworkAgent VendorAgent
        {
            get
            {
                if (_vendorAgent == null)
                {
                    lock (_vendorLock)
                    {
                        if (_vendorAgent == null)
                        {
                            _logger.LogDebug("Creating VendorNetworkAgent lazily");
                            _vendorAgent = AgentFactory.CreateVendorNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("VendorNetworkAgent created successfully");
                        }
                    }
                }
                return _vendorAgent;
            }
        }

        /// <inheritdoc />
        public IFlightMasterNetworkAgent FlightMasterAgent
        {
            get
            {
                if (_flightMasterAgent == null)
                {
                    lock (_flightMasterLock)
                    {
                        if (_flightMasterAgent == null)
                        {
                            _logger.LogDebug("Creating FlightMasterNetworkAgent lazily");
                            _flightMasterAgent = AgentFactory.CreateFlightMasterNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("FlightMasterNetworkAgent created successfully");
                        }
                    }
                }
                return _flightMasterAgent;
            }
        }

        /// <inheritdoc />
        public IDeadActorAgent DeadActorAgent
        {
            get
            {
                if (_deadActorAgent == null)
                {
                    lock (_deadActorLock)
                    {
                        if (_deadActorAgent == null)
                        {
                            _logger.LogDebug("Creating DeadActorAgent lazily");
                            _deadActorAgent = AgentFactory.CreateDeadActorAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("DeadActorAgent created successfully");
                        }
                    }
                }
                return _deadActorAgent;
            }
        }

        /// <inheritdoc />
        public IInventoryNetworkAgent InventoryAgent
        {
            get
            {
                if (_inventoryAgent == null)
                {
                    lock (_inventoryLock)
                    {
                        if (_inventoryAgent == null)
                        {
                            _logger.LogDebug("Creating InventoryNetworkAgent lazily");
                            _inventoryAgent = AgentFactory.CreateInventoryNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("InventoryNetworkAgent created successfully");
                        }
                    }
                }
                return _inventoryAgent;
            }
        }

        /// <inheritdoc />
        public IItemUseNetworkAgent ItemUseAgent
        {
            get
            {
                if (_itemUseAgent == null)
                {
                    lock (_itemUseLock)
                    {
                        if (_itemUseAgent == null)
                        {
                            _logger.LogDebug("Creating ItemUseNetworkAgent lazily");
                            _itemUseAgent = AgentFactory.CreateItemUseNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("ItemUseNetworkAgent created successfully");
                        }
                    }
                }
                return _itemUseAgent;
            }
        }

        /// <inheritdoc />
        public IEquipmentNetworkAgent EquipmentAgent
        {
            get
            {
                if (_equipmentAgent == null)
                {
                    lock (_equipmentLock)
                    {
                        if (_equipmentAgent == null)
                        {
                            _logger.LogDebug("Creating EquipmentNetworkAgent lazily");
                            _equipmentAgent = AgentFactory.CreateEquipmentNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("EquipmentNetworkAgent created successfully");
                        }
                    }
                }
                return _equipmentAgent;
            }
        }

        /// <inheritdoc />
        public ISpellCastingNetworkAgent SpellCastingAgent
        {
            get
            {
                if (_spellCastingAgent == null)
                {
                    lock (_spellCastingLock)
                    {
                        if (_spellCastingAgent == null)
                        {
                            _logger.LogDebug("Creating SpellCastingNetworkAgent lazily");
                            _spellCastingAgent = AgentFactory.CreateSpellCastingNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("SpellCastingNetworkAgent created successfully");
                        }
                    }
                }
                return _spellCastingAgent;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the NetworkAgentFactory class.
        /// Agents are created lazily when their properties are first accessed.
        /// </summary>
        /// <param name="worldClient">The world client for network communication.</param>
        /// <param name="loggerFactory">Logger factory for creating loggers.</param>
        public NetworkAgentFactory(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            _worldClient = worldClient;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<NetworkAgentFactory>();

            _logger.LogInformation("Network Agent Factory initialized with lazy builder pattern");
        }

        /// <summary>
        /// Initializes a new instance of the NetworkAgentFactory class with individual agents.
        /// This constructor is maintained for compatibility but doesn't use the lazy pattern.
        /// </summary>
        /// <param name="targetingAgent">The targeting network agent.</param>
        /// <param name="attackAgent">The attack network agent.</param>
        /// <param name="questAgent">The quest network agent.</param>
        /// <param name="lootingAgent">The looting network agent.</param>
        /// <param name="gameObjectAgent">The game object network agent.</param>
        /// <param name="logger">Logger instance.</param>
        public NetworkAgentFactory(
            ITargetingNetworkAgent targetingAgent,
            IAttackNetworkAgent attackAgent,
            IQuestNetworkAgent questAgent,
            ILootingNetworkAgent lootingAgent,
            IGameObjectNetworkAgent gameObjectAgent,
            ILogger<NetworkAgentFactory> logger)
        {
            _targetingAgent = targetingAgent ?? throw new ArgumentNullException(nameof(targetingAgent));
            _attackAgent = attackAgent ?? throw new ArgumentNullException(nameof(attackAgent));
            _questAgent = questAgent ?? throw new ArgumentNullException(nameof(questAgent));
            _lootingAgent = lootingAgent ?? throw new ArgumentNullException(nameof(lootingAgent));
            _gameObjectAgent = gameObjectAgent ?? throw new ArgumentNullException(nameof(gameObjectAgent));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // These fields won't be used in this constructor path, but we need to initialize them
            _worldClient = null!;
            _loggerFactory = null!;

            SetupEventHandlers();
            _logger.LogInformation("Network Agent Factory initialized with provided agents");
        }

        #endregion

        #region Event Setup

        /// <summary>
        /// Sets up event handlers for coordinating between agents.
        /// Thread-safe and only sets up events once.
        /// </summary>
        private void SetupEventHandlersIfNeeded()
        {
            if (!_eventsSetup)
            {
                lock (_eventsLock)
                {
                    if (!_eventsSetup)
                    {
                        SetupEventHandlers();
                        _eventsSetup = true;
                    }
                }
            }
        }

        /// <summary>
        /// Sets up event handlers for coordinating between agents.
        /// </summary>
        private void SetupEventHandlers()
        {
            // Only setup events for agents that are already created
            if (_targetingAgent != null)
            {
                _targetingAgent.TargetChanged += OnTargetChanged;
            }

            if (_attackAgent != null)
            {
                _attackAgent.AttackStarted += OnAttackStarted;
                _attackAgent.AttackStopped += OnAttackStopped;
                _attackAgent.AttackError += OnAttackError;
            }

            if (_questAgent != null)
            {
                _questAgent.QuestOffered += OnQuestOffered;
                _questAgent.QuestAccepted += OnQuestAccepted;
                _questAgent.QuestCompleted += OnQuestCompleted;
                _questAgent.QuestError += OnQuestError;
            }

            if (_lootingAgent != null)
            {
                _lootingAgent.LootWindowOpened += OnLootWindowOpened;
                _lootingAgent.LootWindowClosed += OnLootWindowClosed;
                _lootingAgent.ItemLooted += OnItemLooted;
                _lootingAgent.MoneyLooted += OnMoneyLooted;
                _lootingAgent.LootError += OnLootError;
            }

            if (_gameObjectAgent != null)
            {
                _gameObjectAgent.GameObjectInteracted += OnGameObjectInteracted;
                _gameObjectAgent.ChestOpened += OnChestOpened;
                _gameObjectAgent.NodeHarvested += OnNodeHarvested;
                _gameObjectAgent.GatheringFailed += OnGatheringFailed;
            }

            if (_vendorAgent != null)
            {
                _vendorAgent.VendorWindowOpened += OnVendorWindowOpened;
                _vendorAgent.VendorWindowClosed += OnVendorWindowClosed;
                _vendorAgent.ItemPurchased += OnItemPurchased;
                _vendorAgent.ItemSold += OnItemSold;
                _vendorAgent.ItemsRepaired += OnItemsRepaired;
                _vendorAgent.VendorError += OnVendorError;
            }

            if (_flightMasterAgent != null)
            {
                _flightMasterAgent.TaxiMapOpened += OnTaxiMapOpened;
                _flightMasterAgent.TaxiMapClosed += OnTaxiMapClosed;
                _flightMasterAgent.FlightActivated += OnFlightActivated;
                _flightMasterAgent.TaxiNodeStatusReceived += OnTaxiNodeStatusReceived;
                _flightMasterAgent.FlightMasterError += OnFlightMasterError;
            }

            if (_deadActorAgent != null)
            {
                _deadActorAgent.OnDeath += OnCharacterDeath;
                _deadActorAgent.OnSpiritReleased += OnSpiritReleased;
                _deadActorAgent.OnResurrected += OnCharacterResurrected;
                _deadActorAgent.OnResurrectionRequest += OnResurrectionRequest;
                _deadActorAgent.OnCorpseLocationUpdated += OnCorpseLocationUpdated;
                _deadActorAgent.OnDeathError += OnDeathError;
            }

            if (_inventoryAgent != null)
            {
                _inventoryAgent.ItemMoved += OnItemMoved;
                _inventoryAgent.ItemSplit += OnItemSplit;
                _inventoryAgent.ItemsSwapped += OnItemsSwapped;
                _inventoryAgent.ItemDestroyed += OnItemDestroyed;
                _inventoryAgent.InventoryError += OnInventoryError;
            }

            if (_itemUseAgent != null)
            {
                _itemUseAgent.ItemUsed += OnItemUsed;
                _itemUseAgent.ItemUseStarted += OnItemUseStarted;
                _itemUseAgent.ItemUseCompleted += OnItemUseCompleted;
                _itemUseAgent.ItemUseFailed += OnItemUseFailed;
                _itemUseAgent.ConsumableEffectApplied += OnConsumableEffectApplied;
            }

            if (_equipmentAgent != null)
            {
                _equipmentAgent.ItemEquipped += OnItemEquipped;
                _equipmentAgent.ItemUnequipped += OnItemUnequipped;
                _equipmentAgent.EquipmentSwapped += OnEquipmentSwapped;
                _equipmentAgent.EquipmentError += OnEquipmentError;
                _equipmentAgent.DurabilityChanged += OnDurabilityChanged;
            }

            if (_spellCastingAgent != null)
            {
                _spellCastingAgent.SpellCastStarted += OnSpellCastStarted;
                _spellCastingAgent.SpellCastCompleted += OnSpellCastCompleted;
                _spellCastingAgent.SpellCastFailed += OnSpellCastFailed;
                _spellCastingAgent.ChannelingStarted += OnChannelingStarted;
                _spellCastingAgent.ChannelingEnded += OnChannelingEnded;
                _spellCastingAgent.SpellCooldownStarted += OnSpellCooldownStarted;
                _spellCastingAgent.SpellHit += OnSpellHit;
            }
        }

        #endregion

        #region Event Handlers

        private void OnTargetChanged(ulong? newTarget)
        {
            if (newTarget.HasValue)
            {
                _logger.LogDebug("Target changed to: {NewTarget:X}", newTarget.Value);
            }
            else
            {
                _logger.LogDebug("Target cleared");
            }
        }

        private void OnAttackStarted(ulong victimGuid)
        {
            _logger.LogDebug("Attack started on: {VictimGuid:X}", victimGuid);
        }

        private void OnAttackStopped()
        {
            _logger.LogDebug("Attack stopped");
        }

        private void OnAttackError(string error)
        {
            _logger.LogWarning("Attack error: {Error}", error);
        }

        private void OnQuestOffered(uint questId)
        {
            _logger.LogDebug("Quest offered: {QuestId}", questId);
        }

        private void OnQuestAccepted(uint questId)
        {
            _logger.LogDebug("Quest accepted: {QuestId}", questId);
        }

        private void OnQuestCompleted(uint questId)
        {
            _logger.LogDebug("Quest completed: {QuestId}", questId);
        }

        private void OnQuestError(string error)
        {
            _logger.LogWarning("Quest error: {Error}", error);
        }

        private void OnLootWindowOpened(ulong lootTargetGuid)
        {
            _logger.LogDebug("Loot window opened for: {LootTargetGuid:X}", lootTargetGuid);
        }

        private void OnLootWindowClosed()
        {
            _logger.LogDebug("Loot window closed");
        }

        private void OnItemLooted(uint itemId, uint quantity)
        {
            _logger.LogDebug("Item looted: {Quantity}x {ItemId}", quantity, itemId);
        }

        private void OnMoneyLooted(uint amount)
        {
            _logger.LogDebug("Money looted: {Amount} copper", amount);
        }

        private void OnLootError(string error)
        {
            _logger.LogWarning("Loot error: {Error}", error);
        }

        private void OnGameObjectInteracted(ulong gameObjectGuid)
        {
            _logger.LogDebug("Game object interacted: {GameObjectGuid:X}", gameObjectGuid);
        }

        private void OnChestOpened(ulong chestGuid)
        {
            _logger.LogDebug("Chest opened: {ChestGuid:X}", chestGuid);
        }

        private void OnNodeHarvested(ulong nodeGuid, uint itemId)
        {
            _logger.LogDebug("Node harvested: {NodeGuid:X}, Item: {ItemId}", nodeGuid, itemId);
        }

        private void OnGatheringFailed(ulong nodeGuid, string error)
        {
            _logger.LogWarning("Gathering failed for node {NodeGuid:X}: {Error}", nodeGuid, error);
        }

        private void OnVendorWindowOpened(ulong vendorGuid)
        {
            _logger.LogDebug("Vendor window opened for: {VendorGuid:X}", vendorGuid);
        }

        private void OnVendorWindowClosed()
        {
            _logger.LogDebug("Vendor window closed");
        }

        private void OnItemPurchased(uint itemId, uint quantity, uint cost)
        {
            _logger.LogDebug("Item purchased: {Quantity}x {ItemId} for {Cost} copper", quantity, itemId, cost);
        }

        private void OnItemSold(uint itemId, uint quantity, uint value)
        {
            _logger.LogDebug("Item sold: {Quantity}x {ItemId} for {Value} copper", quantity, itemId, value);
        }

        private void OnItemsRepaired(uint cost)
        {
            _logger.LogDebug("Items repaired for {Cost} copper", cost);
        }

        private void OnVendorError(string error)
        {
            _logger.LogWarning("Vendor error: {Error}", error);
        }

        private void OnTaxiMapOpened(ulong flightMasterGuid, IReadOnlyList<uint> availableNodes)
        {
            _logger.LogDebug("Taxi map opened for flight master: {FlightMasterGuid:X} with {NodeCount} nodes", flightMasterGuid, availableNodes.Count);
        }

        private void OnTaxiMapClosed()
        {
            _logger.LogDebug("Taxi map closed");
        }

        private void OnFlightActivated(uint sourceNodeId, uint destinationNodeId, uint cost)
        {
            _logger.LogDebug("Flight activated from node {SourceNode} to {DestinationNode} for {Cost} copper", sourceNodeId, destinationNodeId, cost);
        }

        private void OnTaxiNodeStatusReceived(uint nodeId, byte status)
        {
            _logger.LogDebug("Taxi node status received for node {NodeId}: {Status}", nodeId, status);
        }

        private void OnFlightMasterError(string error)
        {
            _logger.LogWarning("Flight master error: {Error}", error);
        }

        private void OnCharacterDeath()
        {
            _logger.LogInformation("Character died");
        }

        private void OnSpiritReleased()
        {
            _logger.LogInformation("Spirit released");
        }

        private void OnCharacterResurrected()
        {
            _logger.LogInformation("Character resurrected");
        }

        private void OnResurrectionRequest(ulong resurrectorGuid, string resurrectorName)
        {
            _logger.LogDebug("Resurrection request from {ResurrectorName} ({ResurrectorGuid:X})", resurrectorName, resurrectorGuid);
        }

        private void OnCorpseLocationUpdated(float x, float y, float z)
        {
            _logger.LogDebug("Corpse location updated: ({X:F2}, {Y:F2}, {Z:F2})", x, y, z);
        }

        private void OnDeathError(string error)
        {
            _logger.LogWarning("Death operation error: {Error}", error);
        }

        // New agent event handlers
        private void OnItemMoved(ulong itemGuid, byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot)
        {
            _logger.LogDebug("Item {ItemGuid:X} moved from {SourceBag}:{SourceSlot} to {DestBag}:{DestSlot}",
                itemGuid, sourceBag, sourceSlot, destinationBag, destinationSlot);
        }

        private void OnItemSplit(ulong itemGuid, uint splitQuantity)
        {
            _logger.LogDebug("Item {ItemGuid:X} split with quantity {Quantity}", itemGuid, splitQuantity);
        }

        private void OnItemsSwapped(ulong firstItemGuid, ulong secondItemGuid)
        {
            _logger.LogDebug("Items swapped: {FirstItem:X} <-> {SecondItem:X}", firstItemGuid, secondItemGuid);
        }

        private void OnItemDestroyed(ulong itemGuid, uint quantity)
        {
            _logger.LogDebug("Item {ItemGuid:X} destroyed with quantity {Quantity}", itemGuid, quantity);
        }

        private void OnInventoryError(string error)
        {
            _logger.LogWarning("Inventory error: {Error}", error);
        }

        private void OnItemUsed(ulong itemGuid, uint itemId, ulong? targetGuid)
        {
            _logger.LogDebug("Item {ItemGuid:X} (ID: {ItemId}) used on target {Target:X}", itemGuid, itemId, targetGuid ?? 0);
        }

        private void OnItemUseStarted(ulong itemGuid, uint castTime)
        {
            _logger.LogDebug("Item {ItemGuid:X} use started with cast time {CastTime}ms", itemGuid, castTime);
        }

        private void OnItemUseCompleted(ulong itemGuid)
        {
            _logger.LogDebug("Item {ItemGuid:X} use completed", itemGuid);
        }

        private void OnItemUseFailed(ulong itemGuid, string error)
        {
            _logger.LogWarning("Item {ItemGuid:X} use failed: {Error}", itemGuid, error);
        }

        private void OnConsumableEffectApplied(uint itemId, uint spellId)
        {
            _logger.LogDebug("Consumable item {ItemId} applied effect spell {SpellId}", itemId, spellId);
        }

        private void OnItemEquipped(ulong itemGuid, EquipmentSlot slot)
        {
            _logger.LogDebug("Item {ItemGuid:X} equipped to slot {Slot}", itemGuid, slot);
        }

        private void OnItemUnequipped(ulong itemGuid, EquipmentSlot slot)
        {
            _logger.LogDebug("Item {ItemGuid:X} unequipped from slot {Slot}", itemGuid, slot);
        }

        private void OnEquipmentSwapped(ulong firstItemGuid, EquipmentSlot firstSlot, ulong secondItemGuid, EquipmentSlot secondSlot)
        {
            _logger.LogDebug("Equipment swapped: {FirstItem:X} ({FirstSlot}) <-> {SecondItem:X} ({SecondSlot})",
                firstItemGuid, firstSlot, secondItemGuid, secondSlot);
        }

        private void OnEquipmentError(string error)
        {
            _logger.LogWarning("Equipment error: {Error}", error);
        }

        private void OnDurabilityChanged(EquipmentSlot slot, uint currentDurability, uint maxDurability)
        {
            _logger.LogDebug("Durability changed for slot {Slot}: {Current}/{Max}", slot, currentDurability, maxDurability);
        }

        private void OnSpellCastStarted(uint spellId, uint castTime, ulong? targetGuid)
        {
            _logger.LogDebug("Spell {SpellId} cast started with cast time {CastTime}ms on target {Target:X}", 
                spellId, castTime, targetGuid ?? 0);
        }

        private void OnSpellCastCompleted(uint spellId, ulong? targetGuid)
        {
            _logger.LogDebug("Spell {SpellId} cast completed on target {Target:X}", spellId, targetGuid ?? 0);
        }

        private void OnSpellCastFailed(uint spellId, string reason)
        {
            _logger.LogWarning("Spell {SpellId} cast failed: {Reason}", spellId, reason);
        }

        private void OnChannelingStarted(uint spellId, uint duration)
        {
            _logger.LogDebug("Channeling started for spell {SpellId} with duration {Duration}ms", spellId, duration);
        }

        private void OnChannelingEnded(uint spellId, bool completed)
        {
            _logger.LogDebug("Channeling ended for spell {SpellId}, completed: {Completed}", spellId, completed);
        }

        private void OnSpellCooldownStarted(uint spellId, uint cooldownTime)
        {
            _logger.LogDebug("Spell {SpellId} cooldown started: {CooldownTime}ms", spellId, cooldownTime);
        }

        private void OnSpellHit(uint spellId, ulong targetGuid, uint? damage, uint? healed)
        {
            _logger.LogDebug("Spell {SpellId} hit target {Target:X} - Damage: {Damage}, Healed: {Healed}",
                spellId, targetGuid, damage, healed);
        }

        #endregion
    }
}