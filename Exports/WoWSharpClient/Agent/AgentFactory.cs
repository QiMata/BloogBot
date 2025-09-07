using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Factory for creating agent instances for World of Warcraft client operations.
    /// Provides a centralized way to create and configure agent components.
    /// </summary>
    public static class AgentFactory
    {
        /// <summary>
        /// Creates a new targeting agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the targeting agent.</param>
        /// <returns>A new targeting agent instance.</returns>
        public static ITargetingAgent CreateTargetingAgent(IWorldClient worldClient, ILogger<TargetingAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new TargetingAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a new targeting agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the targeting agent logger.</param>
        /// <returns>A new targeting agent instance.</returns>
        public static ITargetingAgent CreateTargetingAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<TargetingAgent>();
            return new TargetingAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack agent instance.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the attack agent.</param>
        /// <returns>A new attack agent instance.</returns>
        public static IAttackAgent CreateAttackAgent(IWorldClient worldClient, ILogger<AttackAgent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);

            return new AttackAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack agent instance with a logger factory.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="loggerFactory">Logger factory for creating the attack agent logger.</param>
        /// <returns>A new attack agent instance.</returns>
        public static IAttackAgent CreateAttackAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var logger = loggerFactory.CreateLogger<AttackAgent>();
            return new AttackAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates a targeting agent using the WoWClientFactory pattern.
        /// This method integrates with the existing WoWSharpClient architecture.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured targeting agent.</returns>
        public static ITargetingAgent CreateTargetingAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            // If logger factory is provided, use it
            if (loggerFactory != null)
            {
                return CreateTargetingAgent(worldClient, loggerFactory);
            }

            // Create a simple console logger for development/testing
            var logger = new ConsoleLogger<TargetingAgent>();
            return new TargetingAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack agent using the WoWClientFactory pattern.
        /// This method integrates with the existing WoWSharpClient architecture.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, a simple console logger will be created.</param>
        /// <returns>A configured attack agent.</returns>
        public static IAttackAgent CreateAttackAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);

            // If logger factory is provided, use it
            if (loggerFactory != null)
            {
                return CreateAttackAgent(worldClient, loggerFactory);
            }

            // Create a simple console logger for development/testing
            var logger = new ConsoleLogger<AttackAgent>();
            return new AttackAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates both targeting and attack agents as a coordinated pair.
        /// </summary>
        /// <param name="worldClient">The world client instance.</param>
        /// <param name="loggerFactory">Optional logger factory. If null, simple console loggers will be created.</param>
        /// <returns>A tuple containing the targeting agent and attack agent.</returns>
        public static (ITargetingAgent TargetingAgent, IAttackAgent AttackAgent) CreateCombatAgents(
            IWorldClient worldClient, 
            ILoggerFactory? loggerFactory = null)
        {
            var targetingAgent = CreateTargetingAgentForClient(worldClient, loggerFactory);
            var attackAgent = CreateAttackAgentForClient(worldClient, loggerFactory);
            
            return (targetingAgent, attackAgent);
        }
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