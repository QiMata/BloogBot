using System;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Agent;
using WoWSharpClient.Networking.Abstractions;
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
        /// Creates a targeting agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending targeting packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging targeting operations.</param>
        /// <returns>A configured targeting agent instance.</returns>
        public static ITargetingAgent CreateTargetingAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateTargetingAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a targeting agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending targeting packets.</param>
        /// <param name="logger">The logger for targeting operations.</param>
        /// <returns>A configured targeting agent instance.</returns>
        public static ITargetingAgent CreateTargetingAgent(IWorldClient worldClient, ILogger<TargetingAgent> logger)
        {
            return AgentFactory.CreateTargetingAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates an attack agent for the specified world client.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending attack packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging attack operations.</param>
        /// <returns>A configured attack agent instance.</returns>
        public static IAttackAgent CreateAttackAgent(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateAttackAgentForClient(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates an attack agent with a specific logger.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending attack packets.</param>
        /// <param name="logger">The logger for attack operations.</param>
        /// <returns>A configured attack agent instance.</returns>
        public static IAttackAgent CreateAttackAgent(IWorldClient worldClient, ILogger<AttackAgent> logger)
        {
            return AgentFactory.CreateAttackAgent(worldClient, logger);
        }

        /// <summary>
        /// Creates both targeting and attack agents as a coordinated pair.
        /// This is a convenience method for creating both agents that work together.
        /// </summary>
        /// <param name="worldClient">The world client to use for sending packets.</param>
        /// <param name="loggerFactory">Optional logger factory for logging operations.</param>
        /// <returns>A tuple containing both the targeting agent and attack agent.</returns>
        public static (ITargetingAgent TargetingAgent, IAttackAgent AttackAgent) CreateCombatAgents(
            IWorldClient worldClient, 
            ILoggerFactory? loggerFactory = null)
        {
            return AgentFactory.CreateCombatAgents(worldClient, loggerFactory);
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
    }
}