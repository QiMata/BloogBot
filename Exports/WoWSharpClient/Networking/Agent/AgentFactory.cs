using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Factory for creating network agent instances for World of Warcraft client operations.
    /// Provides a centralized way to create and configure all network agent components.
    /// </summary>
    public static class AgentFactory
    {
        #region Targeting Network Agent

        /// <summary>
        /// Creates a new targeting network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the targeting agent.</param>
        /// <returns>A new targeting network agent instance.</returns>
        public static ITargetingNetworkAgent CreateTargetingNetworkAgent(IWorldClient worldClient, ILogger<TargetingNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new TargetingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a new targeting network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the targeting agent logger.</param>
        /// <returns>A new targeting network agent instance.</returns>
        public static ITargetingNetworkAgent CreateTargetingNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<TargetingNetworkAgent>();
            return new TargetingNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Attack Network Agent

        /// <summary>
        /// Creates an attack network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the attack agent.</param>
        /// <returns>A new attack network agent instance.</returns>
        public static IAttackNetworkAgent CreateAttackNetworkAgent(IWorldClient worldClient, ILogger<AttackNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new AttackNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the attack agent logger.</param>
        /// <returns>A new attack network agent instance.</returns>
        public static IAttackNetworkAgent CreateAttackNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<AttackNetworkAgent>();
            return new AttackNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Quest Network Agent

        /// <summary>
        /// Creates a quest network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the quest agent.</param>
        /// <returns>A new quest network agent instance.</returns>
        public static IQuestNetworkAgent CreateQuestNetworkAgent(IWorldClient worldClient, ILogger<QuestNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new QuestNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a quest network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the quest agent logger.</param>
        /// <returns>A new quest network agent instance.</returns>
        public static IQuestNetworkAgent CreateQuestNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<QuestNetworkAgent>();
            return new QuestNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Looting Network Agent

        /// <summary>
        /// Creates a looting network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the looting agent.</param>
        /// <returns>A new looting network agent instance.</returns>
        public static ILootingNetworkAgent CreateLootingNetworkAgent(IWorldClient worldClient, ILogger<LootingNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new LootingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a looting network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the looting agent logger.</param>
        /// <returns>A new looting network agent instance.</returns>
        public static ILootingNetworkAgent CreateLootingNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<LootingNetworkAgent>();
            return new LootingNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Game Object Network Agent

        /// <summary>
        /// Creates a game object network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the game object agent.</param>
        /// <returns>A new game object network agent instance.</returns>
        public static IGameObjectNetworkAgent CreateGameObjectNetworkAgent(IWorldClient worldClient, ILogger<GameObjectNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new GameObjectNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a game object network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the game object agent logger.</param>
        /// <returns>A new game object network agent instance.</returns>
        public static IGameObjectNetworkAgent CreateGameObjectNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<GameObjectNetworkAgent>();
            return new GameObjectNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Vendor Network Agent

        /// <summary>
        /// Creates a vendor network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the vendor agent.</param>
        /// <returns>A new vendor network agent instance.</returns>
        public static IVendorNetworkAgent CreateVendorNetworkAgent(IWorldClient worldClient, ILogger<VendorNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new VendorNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a vendor network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the vendor agent logger.</param>
        /// <returns>A new vendor network agent instance.</returns>
        public static IVendorNetworkAgent CreateVendorNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<VendorNetworkAgent>();
            return new VendorNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Flight Master Network Agent

        /// <summary>
        /// Creates a flight master network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the flight master agent.</param>
        /// <returns>A new flight master network agent instance.</returns>
        public static IFlightMasterNetworkAgent CreateFlightMasterNetworkAgent(IWorldClient worldClient, ILogger<FlightMasterNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new FlightMasterNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a flight master network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the flight master agent logger.</param>
        /// <returns>A new flight master network agent instance.</returns>
        public static IFlightMasterNetworkAgent CreateFlightMasterNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<FlightMasterNetworkAgent>();
            return new FlightMasterNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Dead Actor Agent

        /// <summary>
        /// Creates a dead actor agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the dead actor agent.</param>
        /// <returns>A new dead actor agent instance.</returns>
        public static IDeadActorAgent CreateDeadActorAgent(IWorldClient worldClient, ILogger<DeadActorAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new DeadActorAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a dead actor agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the dead actor agent logger.</param>
        /// <returns>A new dead actor agent instance.</returns>
        public static IDeadActorAgent CreateDeadActorAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<DeadActorAgent>();
            return new DeadActorAgent(worldClient, logger);
        }

        #endregion

        #region Inventory Network Agent

        /// <summary>
        /// Creates a new inventory network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the inventory agent.</param>
        /// <returns>A new inventory network agent instance.</returns>
        public static IInventoryNetworkAgent CreateInventoryNetworkAgent(IWorldClient worldClient, ILogger<InventoryNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new InventoryNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a new inventory network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the inventory agent logger.</param>
        /// <returns>A new inventory network agent instance.</returns>
        public static IInventoryNetworkAgent CreateInventoryNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<InventoryNetworkAgent>();
            return new InventoryNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Item Use Network Agent

        /// <summary>
        /// Creates a new item use network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the item use agent.</param>
        /// <returns>A new item use network agent instance.</returns>
        public static IItemUseNetworkAgent CreateItemUseNetworkAgent(IWorldClient worldClient, ILogger<ItemUseNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new ItemUseNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a new item use network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the item use agent logger.</param>
        /// <returns>A new item use network agent instance.</returns>
        public static IItemUseNetworkAgent CreateItemUseNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<ItemUseNetworkAgent>();
            return new ItemUseNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Equipment Network Agent

        /// <summary>
        /// Creates a new equipment network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the equipment agent.</param>
        /// <returns>A new equipment network agent instance.</returns>
        public static IEquipmentNetworkAgent CreateEquipmentNetworkAgent(IWorldClient worldClient, ILogger<EquipmentNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new EquipmentNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a new equipment network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the equipment agent logger.</param>
        /// <returns>A new equipment network agent instance.</returns>
        public static IEquipmentNetworkAgent CreateEquipmentNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<EquipmentNetworkAgent>();
            return new EquipmentNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Spell Casting Network Agent

        /// <summary>
        /// Creates a new spell casting network agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the spell casting agent.</param>
        /// <returns>A new spell casting network agent instance.</returns>
        public static ISpellCastingNetworkAgent CreateSpellCastingNetworkAgent(IWorldClient worldClient, ILogger<SpellCastingNetworkAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new SpellCastingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a new spell casting network agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the spell casting agent logger.</param>
        /// <returns>A new spell casting network agent instance.</returns>
        public static ISpellCastingNetworkAgent CreateSpellCastingNetworkAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<SpellCastingNetworkAgent>();
            return new SpellCastingNetworkAgent(worldClient, logger);
        }

        #endregion

        #region Convenience Methods

        /// <summary>
        /// Creates a targeting network agent using the WoWClientFactory pattern.
        /// This method integrates with the existing WoWSharpClient architecture.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured targeting network agent.</returns>
        public static ITargetingNetworkAgent CreateTargetingNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateTargetingNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<TargetingNetworkAgent>();
            return new TargetingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured attack network agent.</returns>
        public static IAttackNetworkAgent CreateAttackNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateAttackNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<AttackNetworkAgent>();
            return new AttackNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a quest network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured quest network agent.</returns>
        public static IQuestNetworkAgent CreateQuestNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateQuestNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<QuestNetworkAgent>();
            return new QuestNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a looting network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured looting network agent.</returns>
        public static ILootingNetworkAgent CreateLootingNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateLootingNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<LootingNetworkAgent>();
            return new LootingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a game object network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured game object network agent.</returns>
        public static IGameObjectNetworkAgent CreateGameObjectNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateGameObjectNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<GameObjectNetworkAgent>();
            return new GameObjectNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a vendor network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured vendor network agent.</returns>
        public static IVendorNetworkAgent CreateVendorNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateVendorNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<VendorNetworkAgent>();
            return new VendorNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a flight master network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured flight master network agent.</returns>
        public static IFlightMasterNetworkAgent CreateFlightMasterNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateFlightMasterNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<FlightMasterNetworkAgent>();
            return new FlightMasterNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a dead actor agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured dead actor agent.</returns>
        public static IDeadActorAgent CreateDeadActorAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateDeadActorAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<DeadActorAgent>();
            return new DeadActorAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an inventory network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured inventory network agent.</returns>
        public static IInventoryNetworkAgent CreateInventoryNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateInventoryNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<InventoryNetworkAgent>();
            return new InventoryNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an item use network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured item use network agent.</returns>
        public static IItemUseNetworkAgent CreateItemUseNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateItemUseNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<ItemUseNetworkAgent>();
            return new ItemUseNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an equipment network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured equipment network agent.</returns>
        public static IEquipmentNetworkAgent CreateEquipmentNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateEquipmentNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<EquipmentNetworkAgent>();
            return new EquipmentNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a spell casting network agent using the WoWClientFactory pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured spell casting network agent.</returns>
        public static ISpellCastingNetworkAgent CreateSpellCastingNetworkAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            if (loggerFactory != null)
            {
                return CreateSpellCastingNetworkAgent(worldClient, loggerFactory);
            }

            var logger = new ConsoleLogger<SpellCastingNetworkAgent>();
            return new SpellCastingNetworkAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates all network agents as a coordinated set.
        /// This is a convenience method for creating all agents that work together.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, simple console loggers will be created.</param>
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
            ISpellCastingNetworkAgent SpellCastingAgent
        ) CreateAllNetworkAgents(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            var targetingAgent = CreateTargetingNetworkAgentForClient(worldClient, loggerFactory);
            var attackAgent = CreateAttackNetworkAgentForClient(worldClient, loggerFactory);
            var questAgent = CreateQuestNetworkAgentForClient(worldClient, loggerFactory);
            var lootingAgent = CreateLootingNetworkAgentForClient(worldClient, loggerFactory);
            var gameObjectAgent = CreateGameObjectNetworkAgentForClient(worldClient, loggerFactory);
            var vendorAgent = CreateVendorNetworkAgentForClient(worldClient, loggerFactory);
            var flightMasterAgent = CreateFlightMasterNetworkAgentForClient(worldClient, loggerFactory);
            var deadActorAgent = CreateDeadActorAgentForClient(worldClient, loggerFactory);
            var inventoryAgent = CreateInventoryNetworkAgentForClient(worldClient, loggerFactory);
            var itemUseAgent = CreateItemUseNetworkAgentForClient(worldClient, loggerFactory);
            var equipmentAgent = CreateEquipmentNetworkAgentForClient(worldClient, loggerFactory);
            var spellCastingAgent = CreateSpellCastingNetworkAgentForClient(worldClient, loggerFactory);

            return (targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent, vendorAgent, flightMasterAgent, deadActorAgent, inventoryAgent, itemUseAgent, equipmentAgent, spellCastingAgent);
        }

        /// <summary>
        /// Creates combat-focused agents (targeting and attack) as a coordinated pair.
        /// This maintains backward compatibility with the previous combat agents pattern.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, simple console loggers will be created.</param>
        /// <returns>A tuple containing the targeting agent and attack agent.</returns>
        public static (ITargetingNetworkAgent TargetingAgent, IAttackNetworkAgent AttackAgent) CreateCombatNetworkAgents(
            IWorldClient worldClient,
            ILoggerFactory? loggerFactory = null)
        {
            var targetingAgent = CreateTargetingNetworkAgentForClient(worldClient, loggerFactory);
            var attackAgent = CreateAttackNetworkAgentForClient(worldClient, loggerFactory);

            return (targetingAgent, attackAgent);
        }

        #endregion

        #region Network Agent Factory

        /// <summary>
        /// Creates a Network Agent Factory that provides coordinated access to all network agents.
        /// </summary>
        /// <param name="worldClient">The world client for network communication.</param>
        /// <param name="loggerFactory">Logger factory for creating loggers.</param>
        /// <returns>A configured Network Agent Factory instance.</returns>
        public static IAgentFactory CreateNetworkAgentFactory(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            return new NetworkAgentFactory(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a Network Agent Factory with individual agents.
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
            return new NetworkAgentFactory(targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent, logger);
        }

        #endregion
    }

    /// <summary>
    /// Simple console logger implementation for development and testing.
    /// This is used when no logger factory is provided.
    /// </summary>
    internal class ConsoleLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var levelName = logLevel.ToString().ToUpper();

            Console.WriteLine($"[{timestamp}] [{levelName}] {typeof(T).Name}: {message}");

            if (exception != null)
            {
                Console.WriteLine($"[{timestamp}] [{levelName}] Exception: {exception}");
            }
        }
    }
}