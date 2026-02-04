using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Networking.I;

namespace WoWSharpClient.Client
{
    /// <summary>
    /// Factory for creating pre-configured WoW client instances.
    /// </summary>
    public static class WoWClientFactory
    {
        /// <summary>
        /// Creates a new AuthClient with the standard authentication server configuration.
        /// </summary>
        /// <returns>A configured AuthClient instance.</returns>
        public static AuthClient CreateAuthClient()
        {
            var connection = new TcpConnection();
            var encryptor = new NoEncryption(); // Auth server uses no encryption
            IMessageFramer framer = new LengthPrefixedFramer(4, false); // 4-byte little-endian length prefix for auth
            var codec = new WoWPacketCodec(); // Could create AuthPacketCodec if needed
            var router = new MessageRouter<Opcode>();

            return new AuthClient(connection, framer, encryptor, codec, router);
        }

        /// <summary>
        /// Creates a new WorldClient with the standard world server configuration.
        /// </summary>
        /// <returns>A configured NewWorldClient instance.</returns>
        public static WorldClient CreateWorldClient()
        {
            var connection = new TcpConnection();
            var encryptor = new NoEncryption(); // Start with no encryption, switch to RC4 after auth
            IMessageFramer framer = new WoWMessageFramer(); // WoW-specific message framing
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            return new WorldClient(connection, framer, encryptor, codec, router);
        }

        /// <summary>
        /// Creats a new WoWClientOrchestrator with properly configured auth and world clients.
        /// </summary>
        /// <returns>A configured WoWClientOrchestrator instance.</returns>
        public static WoWClientOrchestrator CreateOrchestrator()
        {
            return new WoWClientOrchestrator();
        }

        /// <summary>
        /// Creates a basic auth client for testing purposes.
        /// </summary>
        /// <returns>A test-configured AuthClient instance.</returns>
        public static AuthClient CreateTestAuthClient()
        {
            var connection = new TcpConnection();
            var encryptor = new NoEncryption();
            IMessageFramer framer = new LengthPrefixedFramer(4, false);
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            var client = new AuthClient(connection, framer, encryptor, codec, router);
            
            // Add any test-specific configurations here
            
            return client;
        }

        /// <summary>
        /// Creates a basic world client for testing purposes.
        /// </summary>
        /// <returns>A test-configured NewWorldClient instance.</returns>
        public static WorldClient CreateTestWorldClient()
        {
            var connection = new TcpConnection();
            var encryptor = new NoEncryption();
            IMessageFramer framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            var client = new WorldClient(connection, framer, encryptor, codec, router);
            
            // Add any test-specific configurations here
            
            return client;
        }

        /// <summary>
        /// Creates a world client with a specific encryptor (e.g., for encrypted sessions).
        /// </summary>
        /// <param name="encryptor">The encryptor to use.</param>
        /// <returns>A configured NewWorldClient instance with the specified encryptor.</returns>
        public static WorldClient CreateWorldClientWithEncryption(IEncryptor encryptor)
        {
            var connection = new TcpConnection();
            IMessageFramer framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            return new WorldClient(connection, framer, encryptor, codec, router);
        }

        /// <summary>
        /// Creates a world client with connection manager for automatic reconnection.
        /// Note: ConnectionManager wraps IConnection but doesn't implement it directly.
        /// </summary>
        /// <param name="host">The hostname to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>A configured NewWorldClient instance with connection management.</returns>
        public static WorldClient CreateWorldClientWithReconnection(string host, int port)
        {
            var baseConnection = new TcpConnection();
            
            // Create reconnection policy
            var reconnectPolicy = new ExponentialBackoffPolicy(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromSeconds(30));

            var connectionManager = new ConnectionManager(baseConnection, reconnectPolicy, host, port);
            
            var encryptor = new NoEncryption();
            IMessageFramer framer = new WoWMessageFramer();
            var codec = new WoWPacketCodec();
            var router = new MessageRouter<Opcode>();

            // Use the underlying connection from the manager
            return new WorldClient(baseConnection, framer, encryptor, codec, router);
        }

        /// <summary>
        /// Creates a modern WoWClient that uses the new networking architecture internally.
        /// This provides backward compatibility for existing code.
        /// </summary>
        /// <returns>A modern WoWClient instance.</returns>
        public static WoWClient CreateModernWoWClient()
        {
            return new WoWClient();
        }

        /// <summary>
        /// Creates a legacy WoWClient for backward compatibility.
        /// This is deprecated and will be removed in a future version.
        /// </summary>
        /// <returns>A legacy WoWClient instance.</returns>
        [Obsolete("Use CreateModernWoWClient() instead. Legacy client will be removed in a future version.")]
        public static WoWClient CreateLegacyWoWClient()
        {
            return new WoWClient();
        }

        /// <summary>
        /// Creates a targeting network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending targeting packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging targeting operations.</param>
        /// <returns>A configured targeting network agent instance.</returns>
        public static ITargetingNetworkClientComponent CreateTargetingNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateTargetingNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a targeting network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending targeting packets.</param>
        /// <param name="logger">The logger for targeting operations.</param>
        /// <returns>A configured targeting network agent instance.</returns>
        public static ITargetingNetworkClientComponent CreateTargetingNetworkClientComponent(IWorldClient worldClient, ILogger<TargetingNetworkClientComponent> logger)
        {
            return AgentFactory.CreateTargetingNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending attack packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging attack operations.</param>
        /// <returns>A configured attack network agent instance.</returns>
        public static IAttackNetworkClientComponent CreateAttackNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAttackNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an attack network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending attack packets.</param>
        /// <param name="logger">The logger for attack operations.</param>
        /// <returns>A configured attack network agent instance.</returns>
        public static IAttackNetworkClientComponent CreateAttackNetworkClientComponent(IWorldClient worldClient, ILogger<AttackNetworkClientComponent> logger)
        {
            return AgentFactory.CreateAttackNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a quest network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending quest packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging quest operations.</param>
        /// <returns>A configured quest network agent instance.</returns>
        public static IQuestNetworkClientComponent CreateQuestNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateQuestNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a quest network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending quest packets.</param>
        /// <param name="logger">The logger for quest operations.</param>
        /// <returns>A configured quest network agent instance.</returns>
        public static IQuestNetworkClientComponent CreateQuestNetworkClientComponent(IWorldClient worldClient, ILogger<QuestNetworkClientComponent> logger)
        {
            return AgentFactory.CreateQuestNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a looting network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending looting packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging looting operations.</param>
        /// <returns>A configured looting network agent instance.</returns>
        public static ILootingNetworkClientComponent CreateLootingNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateLootingNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a looting network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending looting packets.</param>
        /// <param name="logger">The logger for looting operations.</param>
        /// <returns>A configured looting network agent instance.</returns>
        public static ILootingNetworkClientComponent CreateLootingNetworkClientComponent(IWorldClient worldClient, ILogger<LootingNetworkClientComponent> logger)
        {
            return AgentFactory.CreateLootingNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a game object network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending game object packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging game object operations.</param>
        /// <returns>A configured game object network agent instance.</returns>
        public static IGameObjectNetworkClientComponent CreateGameObjectNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateGameObjectNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a game object network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending game object packets.</param>
        /// <param name="logger">The logger for game object operations.</param>
        /// <returns>A configured game object network agent instance.</returns>
        public static IGameObjectNetworkClientComponent CreateGameObjectNetworkClientComponent(IWorldClient worldClient, ILogger<GameObjectNetworkClientComponent> logger)
        {
            return AgentFactory.CreateGameObjectNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a vendor network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending vendor packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging vendor operations.</param>
        /// <returns>A configured vendor network agent instance.</returns>
        public static IVendorNetworkClientComponent CreateVendorNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateVendorNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a vendor network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending vendor packets.</param>
        /// <param name="logger">The logger for vendor operations.</param>
        /// <returns>A configured vendor network agent instance.</returns>
        public static IVendorNetworkClientComponent CreateVendorNetworkClientComponent(IWorldClient worldClient, ILogger<VendorNetworkClientComponent> logger)
        {
            return AgentFactory.CreateVendorNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a flight master network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending flight master packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging flight master operations.</param>
        /// <returns>A configured flight master network agent instance.</returns>
        public static IFlightMasterNetworkClientComponent CreateFlightMasterNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateFlightMasterNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a flight master network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending flight master packets.</param>
        /// <param name="logger">The logger for flight master operations.</param>
        /// <returns>A configured flight master network agent instance.</returns>
        public static IFlightMasterNetworkClientComponent CreateFlightMasterNetworkClientComponent(IWorldClient worldClient, ILogger<FlightMasterNetworkClientComponent> logger)
        {
            return AgentFactory.CreateFlightMasterNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a dead actor agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending death/resurrection packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging death operations.</param>
        /// <returns>A configured dead actor agent instance.</returns>
        public static IDeadActorNetworkClientComponent CreateDeadActorAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateDeadActorAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a dead actor agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending death/resurrection packets.</param>
        /// <param name="logger">The logger for death operations.</param>
        /// <returns>A configured dead actor agent instance.</returns>
        public static IDeadActorNetworkClientComponent CreateDeadActorAgent(IWorldClient worldClient, ILogger<DeadActorClientComponent> logger)
        {
            return AgentFactory.CreateDeadActorAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an inventory network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending inventory packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging inventory operations.</param>
        /// <returns>A configured inventory network agent instance.</returns>
        public static IInventoryNetworkClientComponent CreateInventoryNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateInventoryNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an inventory network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending inventory packets.</param>
        /// <param name="logger">The logger for inventory operations.</param>
        /// <returns>A configured inventory network agent instance.</returns>
        public static IInventoryNetworkClientComponent CreateInventoryNetworkClientComponent(IWorldClient worldClient, ILogger<InventoryNetworkClientComponent> logger)
        {
            return AgentFactory.CreateInventoryNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates an item use network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending item use packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging item use operations.</param>
        /// <returns>A configured item use network agent instance.</returns>
        public static IItemUseNetworkClientComponent CreateItemUseNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateItemUseNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an item use network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending item use packets.</param>
        /// <param name="logger">The logger for item use operations.</param>
        /// <returns>A configured item use network agent instance.</returns>
        public static IItemUseNetworkClientComponent CreateItemUseNetworkClientComponent(IWorldClient worldClient, ILogger<ItemUseNetworkClientComponent> logger)
        {
            return AgentFactory.CreateItemUseNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates an equipment network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending equipment packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging equipment operations.</param>
        /// <returns>A configured equipment network agent instance.</returns>
        public static IEquipmentNetworkClientComponent CreateEquipmentNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateEquipmentNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an equipment network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending equipment packets.</param>
        /// <param name="logger">The logger for equipment operations.</param>
        /// <returns>A configured equipment network agent instance.</returns>
        public static IEquipmentNetworkClientComponent CreateEquipmentNetworkClientComponent(IWorldClient worldClient, ILogger<EquipmentNetworkClientComponent> logger)
        {
            return AgentFactory.CreateEquipmentNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a spell casting network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending spell casting packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging spell casting operations.</param>
        /// <returns>A configured spell casting network agent instance.</returns>
        public static ISpellCastingNetworkClientComponent CreateSpellCastingNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateSpellCastingNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a spell casting network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending spell casting packets.</param>
        /// <param name="logger">The logger for spell casting operations.</param>
        /// <returns>A configured spell casting network agent instance.</returns>
        public static ISpellCastingNetworkClientComponent CreateSpellCastingNetworkClientComponent(IWorldClient worldClient, ILogger<SpellCastingNetworkClientComponent> logger)
        {
            return AgentFactory.CreateSpellCastingNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a professions network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending profession packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging profession operations.</param>
        /// <returns>A configured professions network agent instance.</returns>
        public static IProfessionsNetworkClientComponent CreateProfessionsNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateProfessionsNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a professions network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending profession packets.</param>
        /// <param name="logger">The logger for profession operations.</param>
        /// <returns>A configured professions network agent instance.</returns>
        public static IProfessionsNetworkClientComponent CreateProfessionsNetworkClientComponent(IWorldClient worldClient, ILogger<ProfessionsNetworkClientComponent> logger)
        {
            return AgentFactory.CreateProfessionsNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates all network agents as a coordinated set.
        /// This maintains backward compatibility while providing access to all available agents.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A tuple containing all network agents.</returns>
        public static (
            ITargetingNetworkClientComponent TargetingAgent,
            IAttackNetworkClientComponent AttackAgent,
            IChatNetworkClientComponent ChatAgent,
            IQuestNetworkClientComponent QuestAgent,
            ILootingNetworkClientComponent LootingAgent,
            IGameObjectNetworkClientComponent GameObjectAgent,
            IVendorNetworkClientComponent VendorAgent,
            IFlightMasterNetworkClientComponent FlightMasterAgent,
            IDeadActorNetworkClientComponent DeadActorAgent,
            IInventoryNetworkClientComponent InventoryAgent,
            IItemUseNetworkClientComponent ItemUseAgent,
            IEquipmentNetworkClientComponent EquipmentAgent,
            ISpellCastingNetworkClientComponent SpellCastingAgent,
            IAuctionHouseNetworkClientComponent AuctionHouseAgent,
            IBankNetworkClientComponent BankAgent,
            IMailNetworkClientComponent MailAgent,
            IGuildNetworkClientComponent GuildAgent,
            IPartyNetworkClientComponent PartyAgent,
            ITrainerNetworkClientComponent TrainerAgent,
            ITalentNetworkClientComponent TalentAgent,
            IProfessionsNetworkClientComponent ProfessionsAgent,
            IEmoteNetworkClientComponent EmoteAgent,
            IGossipNetworkClientComponent GossipAgent,
            IFriendNetworkClientComponent FriendAgent,
            IIgnoreNetworkClientComponent IgnoreAgent,
            ITradeNetworkClientComponent TradeAgent
        ) CreateAllNetworkClientComponents(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAllNetworkClientComponents(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates combat-focused network agents (targeting and attack) as a coordinated pair.
        /// This maintains backward compatibility with the previous combat agents pattern.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A tuple containing both the targeting agent and attack agent.</returns>
        public static (ITargetingNetworkClientComponent TargetingAgent, IAttackNetworkClientComponent AttackAgent) CreateCombatNetworkClientComponents(
            IWorldClient worldClient,
            ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateCombatNetworkClientComponents(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a Network Agent Factory that provides coordinated access to all network agents.
        /// </summary>
        /// <param name="worldClient">The world client for network communication.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A configured Network Agent Factory instance.</returns>
        public static IAgentFactory CreateNetworkClientComponentFactory(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory ?? new NullLoggerFactory());
        }

        /// <summary>
        /// Creates a Network Agent Factory with individual agents.
        /// This method allows for more granular control over agent creation.
        /// </summary>
        /// <param name="targetingAgent">The targeting network agent.</param>
        /// <param name="attackAgent">The attack network agent.</param>
        /// <param name="questAgent">The quest network agent.</param>
        /// <param name="lootingAgent">The looting network agent.</param>
        /// <param name="gameObjectAgent">The game object network agent.</param>
        /// <param name="logger">Logger instance.</param>
        /// <returns>A configured Network Agent Factory instance.</returns>
        public static IAgentFactory CreateNetworkClientComponentFactory(
            ITargetingNetworkClientComponent targetingAgent,
            IAttackNetworkClientComponent attackAgent,
            IQuestNetworkClientComponent questAgent,
            ILootingNetworkClientComponent lootingAgent,
            IGameObjectNetworkClientComponent gameObjectAgent,
            ILogger<NetworkClientComponentFactory> logger)
        {
            return AgentFactory.CreateNetworkClientComponentFactory(targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent, logger);
        }

        /// <summary>
        /// Creates an auction house network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending auction house packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging auction house operations.</param>
        /// <returns>A configured auction house network agent instance.</returns>
        public static IAuctionHouseNetworkClientComponent CreateAuctionHouseNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAuctionHouseNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a bank network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A configured bank network agent.</returns>
        public static IBankNetworkClientComponent CreateBankNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateBankNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a mail network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending mail packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging mail operations.</param>
        /// <returns>A configured mail network agent instance.</returns>
        public static IMailNetworkClientComponent CreateMailNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateMailNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a mail network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending mail packets.</param>
        /// <param name="logger">The logger for mail operations.</param>
        /// <returns>A configured mail network agent instance.</returns>
        public static IMailNetworkClientComponent CreateMailNetworkClientComponent(IWorldClient worldClient, ILogger<MailNetworkClientComponent> logger)
        {
            return AgentFactory.CreateMailNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a guild network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending guild packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging guild operations.</param>
        /// <returns>A configured guild network agent instance.</returns>
        public static IGuildNetworkClientComponent CreateGuildNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateGuildNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a guild network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending guild packets.</param>
        /// <param name="logger">The logger for guild operations.</param>
        /// <returns>A configured guild network agent instance.</returns>
        public static IGuildNetworkClientComponent CreateGuildNetworkClientComponent(IWorldClient worldClient, ILogger<GuildNetworkClientComponent> logger)
        {
            return AgentFactory.CreateGuildNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a party network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending party packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging party operations.</param>
        /// <returns>A configured party network agent instance.</returns>
        public static IPartyNetworkClientComponent CreatePartyNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreatePartyNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a party network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending party packets.</param>
        /// <param name="logger">The logger for party operations.</param>
        /// <returns>A configured party network agent instance.</returns>
        public static IPartyNetworkClientComponent CreatePartyNetworkClientComponent(IWorldClient worldClient, ILogger<PartyNetworkClientComponent> logger)
        {
            return AgentFactory.CreatePartyNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a trainer network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending trainer packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging trainer operations.</param>
        /// <returns>A configured trainer network agent instance.</returns>
        public static ITrainerNetworkClientComponent CreateTrainerNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateTrainerNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a trainer network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending trainer packets.</param>
        /// <param name="logger">The logger for trainer operations.</param>
        /// <returns>A configured trainer network agent instance.</returns>
        public static ITrainerNetworkClientComponent CreateTrainerNetworkClientComponent(IWorldClient worldClient, ILogger<TrainerNetworkClientComponent> logger)
        {
            return AgentFactory.CreateTrainerNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates a talent network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending talent packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging talent operations.</param>
        /// <returns>A configured talent network agent instance.</returns>
        public static ITalentNetworkClientComponent CreateTalentNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateTalentNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a talent network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending talent packets.</param>
        /// <param name="logger">The logger for talent operations.</param>
        /// <returns>A configured talent network agent instance.</returns>
        public static ITalentNetworkClientComponent CreateTalentNetworkClientComponent(IWorldClient worldClient, ILogger<TalentNetworkClientComponent> logger)
        {
            return AgentFactory.CreateTalentNetworkClientComponent(worldClient, logger);
        }

        /// <summary>
        /// Creates an emote network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending emote packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging emote operations.</param>
        /// <returns>A configured emote network agent instance.</returns>
        public static IEmoteNetworkClientComponent CreateEmoteNetworkClientComponent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateEmoteNetworkClientComponentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an emote network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending emote packets.</param>
        /// <param name="logger">The logger for emote operations.</param>
        /// <returns>A configured emote network agent instance.</returns>
        public static IEmoteNetworkClientComponent CreateEmoteNetworkClientComponent(IWorldClient worldClient, ILogger<EmoteNetworkClientComponent> logger)
        {
            return AgentFactory.CreateEmoteNetworkClientComponent(worldClient, logger);
        }
    }
}