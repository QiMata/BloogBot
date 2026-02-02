using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Factory that provides access to network client components (agents). Supports
    /// either injected agents (for tests) or lazy creation using IWorldClient + ILoggerFactory.
    /// </summary>
    public class NetworkClientComponentFactory : IAgentFactory
    {
        private readonly IWorldClient? _worldClient;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<NetworkClientComponentFactory>? _logger;

        // Injected agents (may be null when using lazy constructor)
        private ITargetingNetworkClientComponent? _targetingAgent;
        private IAttackNetworkClientComponent? _attackAgent;
        private IChatNetworkClientComponent? _chatAgent;
        private IQuestNetworkClientComponent? _questAgent;
        private ILootingNetworkClientComponent? _lootingAgent;
        private IGameObjectNetworkClientComponent? _gameObjectAgent;
        private IVendorNetworkClientComponent? _vendorAgent;
        private IFlightMasterNetworkClientComponent? _flightMasterAgent;
        private IDeadActorNetworkClientComponent? _deadActorAgent;
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

        // Injected agents constructor (used in unit tests)
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

            // No lazy creation available in this mode
            _worldClient = null;
            _loggerFactory = null;

            SetupEventHandlersIfNeeded();
        }

        // Lazy constructor (creates agents on first access)
        public NetworkClientComponentFactory(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<NetworkClientComponentFactory>();
        }

        private void EnsureLazyAvailable()
        {
            if (_worldClient == null || _loggerFactory == null)
            {
                throw new InvalidOperationException("Lazy agent creation is not available for this factory instance.");
            }
        }

        private void SetupEventHandlersIfNeeded()
        {
            // Intentionally left minimal. Other components may wire Rx subscriptions centrally.
            // AuctionHouse moved to Rx-only; no legacy event wiring needed here.
        }

        // IAgentFactory properties with lazy creation using AgentFactory helpers
        public ITargetingNetworkClientComponent TargetingAgent
        {
            get
            {
                if (_targetingAgent == null)
                {
                    EnsureLazyAvailable();
                    _targetingAgent = AgentFactory.CreateTargetingNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _targetingAgent;
            }
        }

        public IAttackNetworkClientComponent AttackAgent
        {
            get
            {
                if (_attackAgent == null)
                {
                    EnsureLazyAvailable();
                    _attackAgent = AgentFactory.CreateAttackNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _attackAgent;
            }
        }

        public IChatNetworkClientComponent ChatAgent
        {
            get
            {
                if (_chatAgent == null)
                {
                    EnsureLazyAvailable();
                    _chatAgent = AgentFactory.CreateChatNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _chatAgent;
            }
        }

        public IQuestNetworkClientComponent QuestAgent
        {
            get
            {
                if (_questAgent == null)
                {
                    EnsureLazyAvailable();
                    _questAgent = AgentFactory.CreateQuestNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _questAgent;
            }
        }

        public ILootingNetworkClientComponent LootingAgent
        {
            get
            {
                if (_lootingAgent == null)
                {
                    EnsureLazyAvailable();
                    _lootingAgent = AgentFactory.CreateLootingNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _lootingAgent;
            }
        }

        public IGameObjectNetworkClientComponent GameObjectAgent
        {
            get
            {
                if (_gameObjectAgent == null)
                {
                    EnsureLazyAvailable();
                    _gameObjectAgent = AgentFactory.CreateGameObjectNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _gameObjectAgent;
            }
        }

        public IVendorNetworkClientComponent VendorAgent
        {
            get
            {
                if (_vendorAgent == null)
                {
                    EnsureLazyAvailable();
                    _vendorAgent = AgentFactory.CreateVendorNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _vendorAgent;
            }
        }

        public IFlightMasterNetworkClientComponent FlightMasterAgent
        {
            get
            {
                if (_flightMasterAgent == null)
                {
                    EnsureLazyAvailable();
                    _flightMasterAgent = AgentFactory.CreateFlightMasterNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _flightMasterAgent;
            }
        }

        public IDeadActorNetworkClientComponent DeadActorAgent
        {
            get
            {
                if (_deadActorAgent == null)
                {
                    EnsureLazyAvailable();
                    _deadActorAgent = AgentFactory.CreateDeadActorAgent(_worldClient!, _loggerFactory!);
                }
                return _deadActorAgent;
            }
        }

        public IInventoryNetworkClientComponent InventoryAgent
        {
            get
            {
                if (_inventoryAgent == null)
                {
                    EnsureLazyAvailable();
                    _inventoryAgent = AgentFactory.CreateInventoryNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _inventoryAgent;
            }
        }

        public IItemUseNetworkClientComponent ItemUseAgent
        {
            get
            {
                if (_itemUseAgent == null)
                {
                    EnsureLazyAvailable();
                    _itemUseAgent = AgentFactory.CreateItemUseNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _itemUseAgent;
            }
        }

        public IEquipmentNetworkClientComponent EquipmentAgent
        {
            get
            {
                if (_equipmentAgent == null)
                {
                    EnsureLazyAvailable();
                    _equipmentAgent = AgentFactory.CreateEquipmentNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _equipmentAgent;
            }
        }

        public ISpellCastingNetworkClientComponent SpellCastingAgent
        {
            get
            {
                if (_spellCastingAgent == null)
                {
                    EnsureLazyAvailable();
                    _spellCastingAgent = AgentFactory.CreateSpellCastingNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _spellCastingAgent;
            }
        }

        public IAuctionHouseNetworkClientComponent AuctionHouseAgent
        {
            get
            {
                if (_auctionHouseAgent == null)
                {
                    EnsureLazyAvailable();
                    _auctionHouseAgent = AgentFactory.CreateAuctionHouseNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _auctionHouseAgent;
            }
        }

        public IBankNetworkClientComponent BankAgent
        {
            get
            {
                if (_bankAgent == null)
                {
                    EnsureLazyAvailable();
                    _bankAgent = AgentFactory.CreateBankNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _bankAgent;
            }
        }

        public IMailNetworkClientComponent MailAgent
        {
            get
            {
                if (_mailAgent == null)
                {
                    EnsureLazyAvailable();
                    _mailAgent = AgentFactory.CreateMailNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _mailAgent;
            }
        }

        public IGuildNetworkClientComponent GuildAgent
        {
            get
            {
                if (_guildAgent == null)
                {
                    EnsureLazyAvailable();
                    _guildAgent = AgentFactory.CreateGuildNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _guildAgent;
            }
        }

        public IPartyNetworkClientComponent PartyAgent
        {
            get
            {
                if (_partyAgent == null)
                {
                    EnsureLazyAvailable();
                    _partyAgent = AgentFactory.CreatePartyNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _partyAgent;
            }
        }

        public ITrainerNetworkClientComponent TrainerAgent
        {
            get
            {
                if (_trainerAgent == null)
                {
                    EnsureLazyAvailable();
                    _trainerAgent = AgentFactory.CreateTrainerNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _trainerAgent;
            }
        }

        public ITalentNetworkClientComponent TalentAgent
        {
            get
            {
                if (_talentAgent == null)
                {
                    EnsureLazyAvailable();
                    _talentAgent = AgentFactory.CreateTalentNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _talentAgent;
            }
        }

        public IProfessionsNetworkClientComponent ProfessionsAgent
        {
            get
            {
                if (_professionsAgent == null)
                {
                    EnsureLazyAvailable();
                    _professionsAgent = AgentFactory.CreateProfessionsNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _professionsAgent;
            }
        }

        public IEmoteNetworkClientComponent EmoteAgent
        {
            get
            {
                if (_emoteAgent == null)
                {
                    EnsureLazyAvailable();
                    _emoteAgent = AgentFactory.CreateEmoteNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _emoteAgent;
            }
        }

        public IGossipNetworkClientComponent GossipAgent
        {
            get
            {
                if (_gossipAgent == null)
                {
                    EnsureLazyAvailable();
                    _gossipAgent = AgentFactory.CreateGossipNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _gossipAgent;
            }
        }

        public IFriendNetworkClientComponent FriendAgent
        {
            get
            {
                if (_friendAgent == null)
                {
                    EnsureLazyAvailable();
                    _friendAgent = AgentFactory.CreateFriendNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _friendAgent;
            }
        }

        public IIgnoreNetworkClientComponent IgnoreAgent
        {
            get
            {
                if (_ignoreAgent == null)
                {
                    EnsureLazyAvailable();
                    _ignoreAgent = AgentFactory.CreateIgnoreNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _ignoreAgent;
            }
        }

        public ITradeNetworkClientComponent TradeAgent
        {
            get
            {
                if (_tradeAgent == null)
                {
                    EnsureLazyAvailable();
                    _tradeAgent = AgentFactory.CreateTradeNetworkClientComponent(_worldClient!, _loggerFactory!);
                }
                return _tradeAgent;
            }
        }
    }
}