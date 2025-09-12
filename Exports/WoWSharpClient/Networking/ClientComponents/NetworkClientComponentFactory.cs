using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Network Agent Factory that provides coordinated access to all network agents.
    /// Uses a lazy builder pattern where agents are created only when first accessed.
    /// </summary>
    public class NetworkClientComponentFactory : IAgentFactory
    {
        private readonly IWorldClient _worldClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<NetworkClientComponentFactory> _logger;

        // Lazy-initialized agents
        private ITargetingNetworkClientComponent? _targetingAgent;
        private IAttackNetworkClientComponent? _attackAgent;
        private IChatNetworkClientComponent? _chatAgent;
        private IQuestNetworkClientComponent? _questAgent;
        private ILootingNetworkClientComponent? _lootingAgent;
        private IGameObjectNetworkClientComponent? _gameObjectAgent;
        private IVendorNetworkClientComponent? _vendorAgent;
        private IFlightMasterNetworkClientComponent? _flightMasterAgent;
        private IDeadActorClientComponent? _deadActorAgent;
        private IInventoryNetworkClientComponent? _inventoryAgent;
        private IItemUseNetworkClientComponent? _itemUseAgent;
        private IEquipmentNetworkClientComponent? _equipmentAgent;
        private ISpellCastingNetworkClientComponent? _spellCastingAgent;
        private IAuctionHouseNetworkClientComponent? _auctionHouseAgent;
        private IBankNetworkClientComponent? _bankAgent;
        private IMailNetworkClientComponent? _mailAgent;
        private IGuildNetworkClientComponent? _guildAgent;
        private IPartyNetworkClientComponent? _partyAgent;
        private ITrainerNetworkClientComponent? _trainerAgent;
        private ITalentNetworkClientComponent? _talentAgent;
        private IProfessionsNetworkClientComponent? _professionsAgent;
        private IEmoteNetworkClientComponent? _emoteAgent;
        private IGossipNetworkClientComponent? _gossipAgent;
        private IFriendNetworkClientComponent? _friendAgent;
        private IIgnoreNetworkClientComponent? _ignoreAgent;
        private ITradeNetworkClientComponent? _tradeAgent;

        // Thread safety locks
        private readonly object _targetingLock = new object();
        private readonly object _attackLock = new object();
        private readonly object _chatLock = new object();
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
        private readonly object _auctionHouseLock = new object();
        private readonly object _bankLock = new object();
        private readonly object _mailLock = new object();
        private readonly object _guildLock = new object();
        private readonly object _partyLock = new object();
        private readonly object _trainerLock = new object();
        private readonly object _talentLock = new object();
        private readonly object _professionsLock = new object();
        private readonly object _emoteLock = new object();
        private readonly object _gossipLock = new object();
        private readonly object _friendLock = new object();
        private readonly object _ignoreLock = new object();
        private readonly object _tradeLock = new object();

        // Event handler setup tracking
        private bool _eventsSetup = false;
        private readonly object _eventsLock = new object();

        #region Agent Access Properties

        /// <inheritdoc />
        public ITargetingNetworkClientComponent TargetingAgent
        {
            get
            {
                if (_targetingAgent == null)
                {
                    lock (_targetingLock)
                    {
                        if (_targetingAgent == null)
                        {
                            _logger.LogDebug("Creating TargetingNetworkClientComponent lazily");
                            _targetingAgent = AgentFactory.CreateTargetingNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("TargetingNetworkClientComponent created successfully");
                        }
                    }
                }
                return _targetingAgent;
            }
        }

        /// <inheritdoc />
        public IAttackNetworkClientComponent AttackAgent
        {
            get
            {
                if (_attackAgent == null)
                {
                    lock (_attackLock)
                    {
                        if (_attackAgent == null)
                        {
                            _logger.LogDebug("Creating AttackNetworkClientComponent lazily");
                            _attackAgent = AgentFactory.CreateAttackNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("AttackNetworkClientComponent created successfully");
                        }
                    }
                }
                return _attackAgent;
            }
        }

        /// <inheritdoc />
        public IChatNetworkClientComponent ChatAgent
        {
            get
            {
                if (_chatAgent == null)
                {
                    lock (_chatLock)
                    {
                        if (_chatAgent == null)
                        {
                            _logger.LogDebug("Creating ChatNetworkClientComponent lazily");
                            _chatAgent = AgentFactory.CreateChatNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("ChatNetworkClientComponent created successfully");
                        }
                    }
                }
                return _chatAgent;
            }
        }

        /// <inheritdoc />
        public IQuestNetworkClientComponent QuestAgent
        {
            get
            {
                if (_questAgent == null)
                {
                    lock (_questLock)
                    {
                        if (_questAgent == null)
                        {
                            _logger.LogDebug("Creating QuestNetworkClientComponent lazily");
                            _questAgent = AgentFactory.CreateQuestNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("QuestNetworkClientComponent created successfully");
                        }
                    }
                }
                return _questAgent;
            }
        }

        /// <inheritdoc />
        public ILootingNetworkClientComponent LootingAgent
        {
            get
            {
                if (_lootingAgent == null)
                {
                    lock (_lootingLock)
                    {
                        if (_lootingAgent == null)
                        {
                            _logger.LogDebug("Creating LootingNetworkClientComponent lazily");
                            _lootingAgent = AgentFactory.CreateLootingNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("LootingNetworkClientComponent created successfully");
                        }
                    }
                }
                return _lootingAgent;
            }
        }

        /// <inheritdoc />
        public IGameObjectNetworkClientComponent GameObjectAgent
        {
            get
            {
                if (_gameObjectAgent == null)
                {
                    lock (_gameObjectLock)
                    {
                        if (_gameObjectAgent == null)
                        {
                            _logger.LogDebug("Creating GameObjectNetworkClientComponent lazily");
                            _gameObjectAgent = AgentFactory.CreateGameObjectNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("GameObjectNetworkClientComponent created successfully");
                        }
                    }
                }
                return _gameObjectAgent;
            }
        }

        /// <inheritdoc />
        public IVendorNetworkClientComponent VendorAgent
        {
            get
            {
                if (_vendorAgent == null)
                {
                    lock (_vendorLock)
                    {
                        if (_vendorAgent == null)
                        {
                            _logger.LogDebug("Creating VendorNetworkClientComponent lazily");
                            _vendorAgent = AgentFactory.CreateVendorNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("VendorNetworkClientComponent created successfully");
                        }
                    }
                }
                return _vendorAgent;
            }
        }

        /// <inheritdoc />
        public IFlightMasterNetworkClientComponent FlightMasterAgent
        {
            get
            {
                if (_flightMasterAgent == null)
                {
                    lock (_flightMasterLock)
                    {
                        if (_flightMasterAgent == null)
                        {
                            _logger.LogDebug("Creating FlightMasterNetworkClientComponent lazily");
                            _flightMasterAgent = AgentFactory.CreateFlightMasterNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("FlightMasterNetworkClientComponent created successfully");
                        }
                    }
                }
                return _flightMasterAgent;
            }
        }

        /// <inheritdoc />
        public IDeadActorClientComponent DeadActorAgent
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
        public IInventoryNetworkClientComponent InventoryAgent
        {
            get
            {
                if (_inventoryAgent == null)
                {
                    lock (_inventoryLock)
                    {
                        if (_inventoryAgent == null)
                        {
                            _logger.LogDebug("Creating InventoryNetworkClientComponent lazily");
                            _inventoryAgent = AgentFactory.CreateInventoryNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("InventoryNetworkClientComponent created successfully");
                        }
                    }
                }
                return _inventoryAgent;
            }
        }

        /// <inheritdoc />
        public IItemUseNetworkClientComponent ItemUseAgent
        {
            get
            {
                if (_itemUseAgent == null)
                {
                    lock (_itemUseLock)
                    {
                        if (_itemUseAgent == null)
                        {
                            _logger.LogDebug("Creating ItemUseNetworkClientComponent lazily");
                            _itemUseAgent = AgentFactory.CreateItemUseNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("ItemUseNetworkClientComponent created successfully");
                        }
                    }
                }
                return _itemUseAgent;
            }
        }

        /// <inheritdoc />
        public IEquipmentNetworkClientComponent EquipmentAgent
        {
            get
            {
                if (_equipmentAgent == null)
                {
                    lock (_equipmentLock)
                    {
                        if (_equipmentAgent == null)
                        {
                            _logger.LogDebug("Creating EquipmentNetworkClientComponent lazily");
                            _equipmentAgent = AgentFactory.CreateEquipmentNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("EquipmentNetworkClientComponent created successfully");
                        }
                    }
                }
                return _equipmentAgent;
            }
        }

        /// <inheritdoc />
        public ISpellCastingNetworkClientComponent SpellCastingAgent
        {
            get
            {
                if (_spellCastingAgent == null)
                {
                    lock (_spellCastingLock)
                    {
                        if (_spellCastingAgent == null)
                        {
                            _logger.LogDebug("Creating SpellCastingNetworkClientComponent lazily");
                            _spellCastingAgent = AgentFactory.CreateSpellCastingNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("SpellCastingNetworkClientComponent created successfully");
                        }
                    }
                }
                return _spellCastingAgent;
            }
        }

        /// <inheritdoc />
        public IAuctionHouseNetworkClientComponent AuctionHouseAgent
        {
            get
            {
                if (_auctionHouseAgent == null)
                {
                    lock (_auctionHouseLock)
                    {
                        if (_auctionHouseAgent == null)
                        {
                            _logger.LogDebug("Creating AuctionHouseNetworkClientComponent lazily");
                            _auctionHouseAgent = AgentFactory.CreateAuctionHouseNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("AuctionHouseNetworkClientComponent created successfully");
                        }
                    }
                }
                return _auctionHouseAgent;
            }
        }

        /// <inheritdoc />
        public IBankNetworkClientComponent BankAgent
        {
            get
            {
                if (_bankAgent == null)
                {
                    lock (_bankLock)
                    {
                        if (_bankAgent == null)
                        {
                            _logger.LogDebug("Creating BankNetworkClientComponent lazily");
                            _bankAgent = AgentFactory.CreateBankNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("BankNetworkClientComponent created successfully");
                        }
                    }
                }
                return _bankAgent;
            }
        }

        /// <inheritdoc />
        public IMailNetworkClientComponent MailAgent
        {
            get
            {
                if (_mailAgent == null)
                {
                    lock (_mailLock)
                    {
                        if (_mailAgent == null)
                        {
                            _logger.LogDebug("Creating MailNetworkClientComponent lazily");
                            _mailAgent = AgentFactory.CreateMailNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("MailNetworkClientComponent created successfully");
                        }
                    }
                }
                return _mailAgent;
            }
        }

        /// <inheritdoc />
        public IGuildNetworkClientComponent GuildAgent
        {
            get
            {
                if (_guildAgent == null)
                {
                    lock (_guildLock)
                    {
                        if (_guildAgent == null)
                        {
                            _logger.LogDebug("Creating GuildNetworkClientComponent lazily");
                            _guildAgent = AgentFactory.CreateGuildNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("GuildNetworkClientComponent created successfully");
                        }
                    }
                }
                return _guildAgent;
            }
        }

        /// <inheritdoc />
        public IPartyNetworkClientComponent PartyAgent
        {
            get
            {
                if (_partyAgent == null)
                {
                    lock (_partyLock)
                    {
                        if (_partyAgent == null)
                        {
                            _logger.LogDebug("Creating PartyNetworkClientComponent lazily");
                            _partyAgent = AgentFactory.CreatePartyNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("PartyNetworkClientComponent created successfully");
                        }
                    }
                }
                return _partyAgent;
            }
        }

        /// <inheritdoc />
        public ITrainerNetworkClientComponent TrainerAgent
        {
            get
            {
                if (_trainerAgent == null)
                {
                    lock (_trainerLock)
                    {
                        if (_trainerAgent == null)
                        {
                            _logger.LogDebug("Creating TrainerNetworkClientComponent lazily");
                            _trainerAgent = AgentFactory.CreateTrainerNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("TrainerNetworkClientComponent created successfully");
                        }
                    }
                }
                return _trainerAgent;
            }
        }

        /// <inheritdoc />
        public ITalentNetworkClientComponent TalentAgent
        {
            get
            {
                if (_talentAgent == null)
                {
                    lock (_talentLock)
                    {
                        if (_talentAgent == null)
                        {
                            _logger.LogDebug("Creating TalentNetworkClientComponent lazily");
                            _talentAgent = AgentFactory.CreateTalentNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("TalentNetworkClientComponent created successfully");
                        }
                    }
                }
                return _talentAgent;
            }
        }

        /// <inheritdoc />
        public IProfessionsNetworkClientComponent ProfessionsAgent
        {
            get
            {
                if (_professionsAgent == null)
                {
                    lock (_professionsLock)
                    {
                        if (_professionsAgent == null)
                        {
                            _logger.LogDebug("Creating ProfessionsNetworkClientComponent lazily");
                            _professionsAgent = AgentFactory.CreateProfessionsNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("ProfessionsNetworkClientComponent created successfully");
                        }
                    }
                }
                return _professionsAgent;
            }
        }

        /// <inheritdoc />
        public IEmoteNetworkClientComponent EmoteAgent
        {
            get
            {
                if (_emoteAgent == null)
                {
                    lock (_emoteLock)
                    {
                        if (_emoteAgent == null)
                        {
                            _logger.LogDebug("Creating EmoteNetworkClientComponent lazily");
                            _emoteAgent = AgentFactory.CreateEmoteNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("EmoteNetworkClientComponent created successfully");
                        }
                    }
                }
                return _emoteAgent;
            }
        }

        /// <inheritdoc />
        public IGossipNetworkClientComponent GossipAgent
        {
            get
            {
                if (_gossipAgent == null)
                {
                    lock (_gossipLock)
                    {
                        if (_gossipAgent == null)
                        {
                            _logger.LogDebug("Creating GossipNetworkClientComponent lazily");
                            _gossipAgent = AgentFactory.CreateGossipNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("GossipNetworkClientComponent created successfully");
                        }
                    }
                }
                return _gossipAgent;
            }
        }

        /// <inheritdoc />
        public IFriendNetworkClientComponent FriendAgent
        {
            get
            {
                if (_friendAgent == null)
                {
                    lock (_friendLock)
                    {
                        if (_friendAgent == null)
                        {
                            _logger.LogDebug("Creating FriendNetworkClientComponent lazily");
                            _friendAgent = AgentFactory.CreateFriendNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("FriendNetworkClientComponent created successfully");
                        }
                    }
                }
                return _friendAgent;
            }
        }

        /// <inheritdoc />
        public IIgnoreNetworkClientComponent IgnoreAgent
        {
            get
            {
                if (_ignoreAgent == null)
                {
                    lock (_ignoreLock)
                    {
                        if (_ignoreAgent == null)
                        {
                            _logger.LogDebug("Creating IgnoreNetworkClientComponent lazily");
                            _ignoreAgent = AgentFactory.CreateIgnoreNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("IgnoreNetworkClientComponent created successfully");
                        }
                    }
                }
                return _ignoreAgent;
            }
        }

        /// <inheritdoc />
        public ITradeNetworkClientComponent TradeAgent
        {
            get
            {
                if (_tradeAgent == null)
                {
                    lock (_tradeLock)
                    {
                        if (_tradeAgent == null)
                        {
                            _logger.LogDebug("Creating TradeNetworkClientComponent lazily");
                            _tradeAgent = AgentFactory.CreateTradeNetworkClientComponent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("TradeNetworkClientComponent created successfully");
                        }
                    }
                }
                return _tradeAgent;
            }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkClientComponentFactory"/> class.
        /// </summary>
        /// <param name="worldClient">The world client.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public NetworkClientComponentFactory(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<NetworkClientComponentFactory>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkClientComponentFactory"/> class with pre-configured agents.
        /// This constructor is primarily used for testing scenarios.
        /// </summary>
        /// <param name="targetingAgent">The targeting network agent.</param>
        /// <param name="attackAgent">The attack network agent.</param>
        /// <param name="questAgent">The quest network agent.</param>
        /// <param name="lootingAgent">The looting network agent.</param>
        /// <param name="gameObjectAgent">The game object network agent.</param>
        /// <param name="logger">The logger for the factory.</param>
        public NetworkClientComponentFactory(
            ITargetingNetworkClientComponent targetingAgent,
            IAttackNetworkClientComponent attackAgent,
            IQuestNetworkClientComponent questAgent,
            ILootingNetworkClientComponent lootingAgent,
            IGameObjectNetworkClientComponent gameObjectAgent,
            ILogger<NetworkClientComponentFactory> logger)
        {
            _targetingAgent = targetingAgent ?? throw new ArgumentNullException(nameof(targetingAgent));
            _attackAgent = attackAgent ?? throw new ArgumentNullException(nameof(attackAgent));
            _questAgent = questAgent ?? throw new ArgumentNullException(nameof(questAgent));
            _lootingAgent = lootingAgent ?? throw new ArgumentNullException(nameof(lootingAgent));
            _gameObjectAgent = gameObjectAgent ?? throw new ArgumentNullException(nameof(gameObjectAgent));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // For this constructor, we don't have a world client or logger factory since we're using pre-configured agents
            _worldClient = null!;
            _loggerFactory = null!;

            // Set up event handlers immediately since agents are provided
            SetupEventHandlersIfNeeded();
        }

        /// <summary>
        /// Sets up event handlers for all agents if not already done.
        /// </summary>
        private void SetupEventHandlersIfNeeded()
        {
            if (_eventsSetup)
                return;

            lock (_eventsLock)
            {
                if (_eventsSetup)
                    return;

                _logger.LogDebug("Setting up event handlers for all agents");

                // Setup event handlers for each agent
                // Example: TargetingAgent.SomeEvent += OnSomeEventHandler;

                if (_emoteAgent != null)
                {
                    // Will be updated when IEmoteNetworkClientComponent event handling is implemented with reactive observables
                }

                if (_gossipAgent != null)
                {
                    // Setup gossip agent reactive observables subscriptions
                    _gossipAgent.GossipMenuOpened.Subscribe(menuData => OnGossipMenuOpened(menuData.NpcGuid));
                    _gossipAgent.GossipMenuClosed.Subscribe(menuData => OnGossipMenuClosed());
                    _gossipAgent.SelectedOptions.Subscribe(optionData => OnGossipOptionSelected(optionData.Index, optionData.Text));
                    _gossipAgent.ServiceDiscovered.Subscribe(serviceData => OnGossipServiceDiscovered(serviceData.ServiceType, serviceData.NpcGuid));
                    _gossipAgent.GossipErrors.Subscribe(errorData => OnGossipError(errorData.ErrorMessage));
                }

                _eventsSetup = true;
                _logger.LogDebug("Event handlers setup complete");
            }
        }

        private void OnBankOperationFailed(Models.BankOperationType operation, string error)
        {
            _logger.LogWarning("Bank operation {Operation} failed: {Error}", operation, error);
        }

        private void OnGossipMenuOpened(ulong npcGuid)
        {
            _logger.LogDebug("Gossip menu opened for NPC: {NpcGuid:X}", npcGuid);
        }

        private void OnGossipMenuClosed()
        {
            _logger.LogDebug("Gossip menu closed");
        }

        private void OnGossipOptionSelected(uint optionIndex, string optionText)
        {
            _logger.LogDebug("Gossip option selected: {OptionIndex} - {OptionText}", optionIndex, optionText);
        }

        private void OnGossipServiceDiscovered(GossipServiceType serviceType, ulong npcGuid)
        {
            _logger.LogDebug("Gossip service discovered: {ServiceType} from NPC {NpcGuid:X}", serviceType, npcGuid);
        }

        private void OnGossipError(string error)
        {
            _logger.LogWarning("Gossip error: {Error}", error);
        }
    }
}