using System;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
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
        /// Creates a new WoWClientOrchestrator with properly configured auth and world clients.
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
        public static ITargetingNetworkAgent CreateTargetingNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateTargetingNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a targeting network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending targeting packets.</param>
        /// <param name="logger">The logger for targeting operations.</param>
        /// <returns>A configured targeting network agent instance.</returns>
        public static ITargetingNetworkAgent CreateTargetingNetworkAgent(IWorldClient worldClient, ILogger<TargetingNetworkAgent> logger)
        {
            return AgentFactory.CreateTargetingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending attack packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging attack operations.</param>
        /// <returns>A configured attack network agent instance.</returns>
        public static IAttackNetworkAgent CreateAttackNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAttackNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an attack network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending attack packets.</param>
        /// <param name="logger">The logger for attack operations.</param>
        /// <returns>A configured attack network agent instance.</returns>
        public static IAttackNetworkAgent CreateAttackNetworkAgent(IWorldClient worldClient, ILogger<AttackNetworkAgent> logger)
        {
            return AgentFactory.CreateAttackNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a quest network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending quest packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging quest operations.</param>
        /// <returns>A configured quest network agent instance.</returns>
        public static IQuestNetworkAgent CreateQuestNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateQuestNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a quest network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending quest packets.</param>
        /// <param name="logger">The logger for quest operations.</param>
        /// <returns>A configured quest network agent instance.</returns>
        public static IQuestNetworkAgent CreateQuestNetworkAgent(IWorldClient worldClient, ILogger<QuestNetworkAgent> logger)
        {
            return AgentFactory.CreateQuestNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a looting network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending looting packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging looting operations.</param>
        /// <returns>A configured looting network agent instance.</returns>
        public static ILootingNetworkAgent CreateLootingNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateLootingNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a looting network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending looting packets.</param>
        /// <param name="logger">The logger for looting operations.</param>
        /// <returns>A configured looting network agent instance.</returns>
        public static ILootingNetworkAgent CreateLootingNetworkAgent(IWorldClient worldClient, ILogger<LootingNetworkAgent> logger)
        {
            return AgentFactory.CreateLootingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a game object network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending game object packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging game object operations.</param>
        /// <returns>A configured game object network agent instance.</returns>
        public static IGameObjectNetworkAgent CreateGameObjectNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateGameObjectNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a game object network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending game object packets.</param>
        /// <param name="logger">The logger for game object operations.</param>
        /// <returns>A configured game object network agent instance.</returns>
        public static IGameObjectNetworkAgent CreateGameObjectNetworkAgent(IWorldClient worldClient, ILogger<GameObjectNetworkAgent> logger)
        {
            return AgentFactory.CreateGameObjectNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a vendor network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending vendor packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging vendor operations.</param>
        /// <returns>A configured vendor network agent instance.</returns>
        public static IVendorNetworkAgent CreateVendorNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateVendorNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a vendor network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending vendor packets.</param>
        /// <param name="logger">The logger for vendor operations.</param>
        /// <returns>A configured vendor network agent instance.</returns>
        public static IVendorNetworkAgent CreateVendorNetworkAgent(IWorldClient worldClient, ILogger<VendorNetworkAgent> logger)
        {
            return AgentFactory.CreateVendorNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a flight master network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending flight master packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging flight master operations.</param>
        /// <returns>A configured flight master network agent instance.</returns>
        public static IFlightMasterNetworkAgent CreateFlightMasterNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateFlightMasterNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a flight master network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending flight master packets.</param>
        /// <param name="logger">The logger for flight master operations.</param>
        /// <returns>A configured flight master network agent instance.</returns>
        public static IFlightMasterNetworkAgent CreateFlightMasterNetworkAgent(IWorldClient worldClient, ILogger<FlightMasterNetworkAgent> logger)
        {
            return AgentFactory.CreateFlightMasterNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a dead actor agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending death/resurrection packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging death operations.</param>
        /// <returns>A configured dead actor agent instance.</returns>
        public static IDeadActorAgent CreateDeadActorAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateDeadActorAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a dead actor agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending death/resurrection packets.</param>
        /// <param name="logger">The logger for death operations.</param>
        /// <returns>A configured dead actor agent instance.</returns>
        public static IDeadActorAgent CreateDeadActorAgent(IWorldClient worldClient, ILogger<DeadActorAgent> logger)
        {
            return AgentFactory.CreateDeadActorAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an inventory network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending inventory packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging inventory operations.</param>
        /// <returns>A configured inventory network agent instance.</returns>
        public static IInventoryNetworkAgent CreateInventoryNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateInventoryNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an inventory network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending inventory packets.</param>
        /// <param name="logger">The logger for inventory operations.</param>
        /// <returns>A configured inventory network agent instance.</returns>
        public static IInventoryNetworkAgent CreateInventoryNetworkAgent(IWorldClient worldClient, ILogger<InventoryNetworkAgent> logger)
        {
            return AgentFactory.CreateInventoryNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an item use network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending item use packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging item use operations.</param>
        /// <returns>A configured item use network agent instance.</returns>
        public static IItemUseNetworkAgent CreateItemUseNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateItemUseNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an item use network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending item use packets.</param>
        /// <param name="logger">The logger for item use operations.</param>
        /// <returns>A configured item use network agent instance.</returns>
        public static IItemUseNetworkAgent CreateItemUseNetworkAgent(IWorldClient worldClient, ILogger<ItemUseNetworkAgent> logger)
        {
            return AgentFactory.CreateItemUseNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an equipment network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending equipment packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging equipment operations.</param>
        /// <returns>A configured equipment network agent instance.</returns>
        public static IEquipmentNetworkAgent CreateEquipmentNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateEquipmentNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an equipment network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending equipment packets.</param>
        /// <param name="logger">The logger for equipment operations.</param>
        /// <returns>A configured equipment network agent instance.</returns>
        public static IEquipmentNetworkAgent CreateEquipmentNetworkAgent(IWorldClient worldClient, ILogger<EquipmentNetworkAgent> logger)
        {
            return AgentFactory.CreateEquipmentNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a spell casting network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending spell casting packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging spell casting operations.</param>
        /// <returns>A configured spell casting network agent instance.</returns>
        public static ISpellCastingNetworkAgent CreateSpellCastingNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateSpellCastingNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a spell casting network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending spell casting packets.</param>
        /// <param name="logger">The logger for spell casting operations.</param>
        /// <returns>A configured spell casting network agent instance.</returns>
        public static ISpellCastingNetworkAgent CreateSpellCastingNetworkAgent(IWorldClient worldClient, ILogger<SpellCastingNetworkAgent> logger)
        {
            return AgentFactory.CreateSpellCastingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates all network agents as a coordinated set.
        /// This maintains backward compatibility while providing access to all available agents.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A tuple containing all network agents.</returns>
        public static (
            ITargetingNetworkAgent TargetingAgent,
            IAttackNetworkAgent AttackAgent,
            IQuestNetworkAgent QuestAgent,
            ILootingNetworkAgent LootingAgent,
            IGameObjectNetworkAgent GameObjectAgent,
            IVendorNetworkAgent VendorAgent,
            IFlightMasterNetworkAgent FlightMasterAgent,
            IDeadActorAgent DeadActorAgent,
            IInventoryNetworkAgent InventoryAgent,
            IItemUseNetworkAgent ItemUseAgent,
            IEquipmentNetworkAgent EquipmentAgent,
            ISpellCastingNetworkAgent SpellCastingAgent,
            IAuctionHouseNetworkAgent AuctionHouseAgent,
            IBankNetworkAgent BankAgent,
            IMailNetworkAgent MailAgent,
            IGuildNetworkAgent GuildAgent,
            IPartyNetworkAgent PartyAgent,
            ITrainerNetworkAgent TrainerAgent
        ) CreateAllNetworkAgents(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAllNetworkAgents(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates combat-focused network agents (targeting and attack) as a coordinated pair.
        /// This maintains backward compatibility with the previous combat agents pattern.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A tuple containing both the targeting agent and attack agent.</returns>
        public static (ITargetingNetworkAgent TargetingAgent, IAttackNetworkAgent AttackAgent) CreateCombatNetworkAgents(
            IWorldClient worldClient,
            ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateCombatNetworkAgents(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a Network Agent Factory that provides coordinated access to all network agents.
        /// </summary>
        /// <param name="worldClient">The world client for network communication.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A configured Network Agent Factory instance.</returns>
        public static IAgentFactory CreateNetworkAgentFactory(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateNetworkAgentFactory(worldClient, loggerFactory ?? new NullLoggerFactory());
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
        public static IAgentFactory CreateNetworkAgentFactory(
            ITargetingNetworkAgent targetingAgent,
            IAttackNetworkAgent attackAgent,
            IQuestNetworkAgent questAgent,
            ILootingNetworkAgent lootingAgent,
            IGameObjectNetworkAgent gameObjectAgent,
            ILogger<NetworkAgentFactory> logger)
        {
            return AgentFactory.CreateNetworkAgentFactory(targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent, logger);
        }

        /// <summary>
        /// Creates an auction house network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending auction house packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging auction house operations.</param>
        /// <returns>A configured auction house network agent instance.</returns>
        public static IAuctionHouseNetworkAgent CreateAuctionHouseNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAuctionHouseNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a bank network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A configured bank network agent.</returns>
        public static IBankNetworkAgent CreateBankNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateBankNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a mail network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending mail packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging mail operations.</param>
        /// <returns>A configured mail network agent instance.</returns>
        public static IMailNetworkAgent CreateMailNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateMailNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a mail network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending mail packets.</param>
        /// <param name="logger">The logger for mail operations.</param>
        /// <returns>A configured mail network agent instance.</returns>
        public static IMailNetworkAgent CreateMailNetworkAgent(IWorldClient worldClient, ILogger<MailNetworkAgent> logger)
        {
            return AgentFactory.CreateMailNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a guild network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending guild packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging guild operations.</param>
        /// <returns>A configured guild network agent instance.</returns>
        public static IGuildNetworkAgent CreateGuildNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateGuildNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a guild network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending guild packets.</param>
        /// <param name="logger">The logger for guild operations.</param>
        /// <returns>A configured guild network agent instance.</returns>
        public static IGuildNetworkAgent CreateGuildNetworkAgent(IWorldClient worldClient, ILogger<GuildNetworkAgent> logger)
        {
            return AgentFactory.CreateGuildNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a party network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending party packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging party operations.</param>
        /// <returns>A configured party network agent instance.</returns>
        public static IPartyNetworkAgent CreatePartyNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreatePartyNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a party network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending party packets.</param>
        /// <param name="logger">The logger for party operations.</param>
        /// <returns>A configured party network agent instance.</returns>
        public static IPartyNetworkAgent CreatePartyNetworkAgent(IWorldClient worldClient, ILogger<PartyNetworkAgent> logger)
        {
            return AgentFactory.CreatePartyNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a trainer network agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending trainer packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging trainer operations.</param>
        /// <returns>A configured trainer network agent instance.</returns>
        public static ITrainerNetworkAgent CreateTrainerNetworkAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateTrainerNetworkAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a trainer network agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending trainer packets.</param>
        /// <param name="logger">The logger for trainer operations.</param>
        /// <returns>A configured trainer network agent instance.</returns>
        public static ITrainerNetworkAgent CreateTrainerNetworkAgent(IWorldClient worldClient, ILogger<TrainerNetworkAgent> logger)
        {
            return AgentFactory.CreateTrainerNetworkAgent(worldClient, logger);
        }
    }
}