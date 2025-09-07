using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace WoWSharpClient.Agent
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
            IGameObjectNetworkAgent GameObjectAgent
        ) CreateAllNetworkAgents(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            var targetingAgent = CreateTargetingNetworkAgentForClient(worldClient, loggerFactory);
            var attackAgent = CreateAttackNetworkAgentForClient(worldClient, loggerFactory);
            var questAgent = CreateQuestNetworkAgentForClient(worldClient, loggerFactory);
            var lootingAgent = CreateLootingNetworkAgentForClient(worldClient, loggerFactory);
            var gameObjectAgent = CreateGameObjectNetworkAgentForClient(worldClient, loggerFactory);

            return (targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent);
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