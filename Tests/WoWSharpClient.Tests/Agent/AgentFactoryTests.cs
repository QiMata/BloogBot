using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Agent;
using WoWSharpClient.Client;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class AgentFactoryTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<TargetingNetworkAgent>> _mockTargetingLogger;
        private readonly Mock<ILogger<AttackNetworkAgent>> _mockAttackLogger;
        private readonly Mock<ILogger<QuestNetworkAgent>> _mockQuestLogger;
        private readonly Mock<ILogger<LootingNetworkAgent>> _mockLootingLogger;
        private readonly Mock<ILogger<GameObjectNetworkAgent>> _mockGameObjectLogger;

        public AgentFactoryTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockTargetingLogger = new Mock<ILogger<TargetingNetworkAgent>>();
            _mockAttackLogger = new Mock<ILogger<AttackNetworkAgent>>();
            _mockQuestLogger = new Mock<ILogger<QuestNetworkAgent>>();
            _mockLootingLogger = new Mock<ILogger<LootingNetworkAgent>>();
            _mockGameObjectLogger = new Mock<ILogger<GameObjectNetworkAgent>>();

            // Setup the factory to return our specific loggers
            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(TargetingNetworkAgent).FullName!))
                .Returns(_mockTargetingLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(AttackNetworkAgent).FullName!))
                .Returns(_mockAttackLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(QuestNetworkAgent).FullName!))
                .Returns(_mockQuestLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(LootingNetworkAgent).FullName!))
                .Returns(_mockLootingLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(GameObjectNetworkAgent).FullName!))
                .Returns(_mockGameObjectLogger.Object);
        }

        #region Targeting Network Agent Tests

        [Fact]
        public void CreateTargetingNetworkAgent_WithLogger_ReturnsTargetingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkAgent(_mockWorldClient.Object, _mockTargetingLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkAgent>(agent);
        }

        [Fact]
        public void CreateTargetingNetworkAgent_WithLoggerFactory_ReturnsTargetingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkAgent>(agent);
        }

        [Fact]
        public void CreateTargetingNetworkAgentForClient_WithLoggerFactory_ReturnsTargetingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkAgent>(agent);
        }

        [Fact]
        public void CreateTargetingNetworkAgentForClient_WithoutLoggerFactory_ReturnsTargetingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkAgent>(agent);
        }

        #endregion

        #region Attack Network Agent Tests

        [Fact]
        public void CreateAttackNetworkAgent_WithLogger_ReturnsAttackNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkAgent(_mockWorldClient.Object, _mockAttackLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkAgent>(agent);
        }

        [Fact]
        public void CreateAttackNetworkAgent_WithLoggerFactory_ReturnsAttackNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkAgent>(agent);
        }

        [Fact]
        public void CreateAttackNetworkAgentForClient_WithLoggerFactory_ReturnsAttackNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkAgent>(agent);
        }

        [Fact]
        public void CreateAttackNetworkAgentForClient_WithoutLoggerFactory_ReturnsAttackNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkAgent>(agent);
        }

        #endregion

        #region Quest Network Agent Tests

        [Fact]
        public void CreateQuestNetworkAgent_WithLogger_ReturnsQuestNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkAgent(_mockWorldClient.Object, _mockQuestLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkAgent>(agent);
        }

        [Fact]
        public void CreateQuestNetworkAgent_WithLoggerFactory_ReturnsQuestNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkAgent>(agent);
        }

        [Fact]
        public void CreateQuestNetworkAgentForClient_WithLoggerFactory_ReturnsQuestNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkAgent>(agent);
        }

        [Fact]
        public void CreateQuestNetworkAgentForClient_WithoutLoggerFactory_ReturnsQuestNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkAgent>(agent);
        }

        #endregion

        #region Looting Network Agent Tests

        [Fact]
        public void CreateLootingNetworkAgent_WithLogger_ReturnsLootingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkAgent(_mockWorldClient.Object, _mockLootingLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkAgent>(agent);
        }

        [Fact]
        public void CreateLootingNetworkAgent_WithLoggerFactory_ReturnsLootingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkAgent>(agent);
        }

        [Fact]
        public void CreateLootingNetworkAgentForClient_WithLoggerFactory_ReturnsLootingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkAgent>(agent);
        }

        [Fact]
        public void CreateLootingNetworkAgentForClient_WithoutLoggerFactory_ReturnsLootingNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkAgent>(agent);
        }

        #endregion

        #region Game Object Network Agent Tests

        [Fact]
        public void CreateGameObjectNetworkAgent_WithLogger_ReturnsGameObjectNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkAgent(_mockWorldClient.Object, _mockGameObjectLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkAgent>(agent);
        }

        [Fact]
        public void CreateGameObjectNetworkAgent_WithLoggerFactory_ReturnsGameObjectNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkAgent>(agent);
        }

        [Fact]
        public void CreateGameObjectNetworkAgentForClient_WithLoggerFactory_ReturnsGameObjectNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkAgent>(agent);
        }

        [Fact]
        public void CreateGameObjectNetworkAgentForClient_WithoutLoggerFactory_ReturnsGameObjectNetworkAgent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkAgent>(agent);
        }

        #endregion

        #region Convenience Methods Tests

        [Fact]
        public void CreateAllNetworkAgents_WithLoggerFactory_ReturnsAllAgents()
        {
            // Act
            var (targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent) = 
                AgentFactory.CreateAllNetworkAgents(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.NotNull(questAgent);
            Assert.NotNull(lootingAgent);
            Assert.NotNull(gameObjectAgent);
            Assert.IsType<TargetingNetworkAgent>(targetingAgent);
            Assert.IsType<AttackNetworkAgent>(attackAgent);
            Assert.IsType<QuestNetworkAgent>(questAgent);
            Assert.IsType<LootingNetworkAgent>(lootingAgent);
            Assert.IsType<GameObjectNetworkAgent>(gameObjectAgent);
        }

        [Fact]
        public void CreateAllNetworkAgents_WithoutLoggerFactory_ReturnsAllAgents()
        {
            // Act
            var (targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent) = 
                AgentFactory.CreateAllNetworkAgents(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.NotNull(questAgent);
            Assert.NotNull(lootingAgent);
            Assert.NotNull(gameObjectAgent);
            Assert.IsType<TargetingNetworkAgent>(targetingAgent);
            Assert.IsType<AttackNetworkAgent>(attackAgent);
            Assert.IsType<QuestNetworkAgent>(questAgent);
            Assert.IsType<LootingNetworkAgent>(lootingAgent);
            Assert.IsType<GameObjectNetworkAgent>(gameObjectAgent);
        }

        [Fact]
        public void CreateCombatNetworkAgents_WithLoggerFactory_ReturnsBothAgents()
        {
            // Act
            var (targetingAgent, attackAgent) = AgentFactory.CreateCombatNetworkAgents(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.IsType<TargetingNetworkAgent>(targetingAgent);
            Assert.IsType<AttackNetworkAgent>(attackAgent);
        }

        [Fact]
        public void CreateCombatNetworkAgents_WithoutLoggerFactory_ReturnsBothAgents()
        {
            // Act
            var (targetingAgent, attackAgent) = AgentFactory.CreateCombatNetworkAgents(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.IsType<TargetingNetworkAgent>(targetingAgent);
            Assert.IsType<AttackNetworkAgent>(attackAgent);
        }

        #endregion

        #region Network Agent Factory Tests

        [Fact]
        public void CreateNetworkAgentFactory_WithValidParameters_ReturnsFactory()
        {
            // Act
            var factory = AgentFactory.CreateNetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(factory);
            Assert.IsAssignableFrom<IAgentFactory>(factory);
        }

        [Fact]
        public void CreateNetworkAgentFactory_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateNetworkAgentFactory(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateNetworkAgentFactory_WithNullLoggerFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateNetworkAgentFactory(_mockWorldClient.Object, null!));
        }

        [Fact]
        public void CreateNetworkAgentFactory_WithIndividualAgents_ReturnsFactory()
        {
            // Arrange
            var mockTargetingAgent = new Mock<ITargetingNetworkAgent>();
            var mockAttackAgent = new Mock<IAttackNetworkAgent>();
            var mockQuestAgent = new Mock<IQuestNetworkAgent>();
            var mockLootingAgent = new Mock<ILootingNetworkAgent>();
            var mockGameObjectAgent = new Mock<IGameObjectNetworkAgent>();
            var mockLogger = new Mock<ILogger<NetworkAgentFactory>>();

            // Act
            var factory = AgentFactory.CreateNetworkAgentFactory(
                mockTargetingAgent.Object,
                mockAttackAgent.Object,
                mockQuestAgent.Object,
                mockLootingAgent.Object,
                mockGameObjectAgent.Object,
                mockLogger.Object);

            // Assert
            Assert.NotNull(factory);
            Assert.IsAssignableFrom<IAgentFactory>(factory);
            Assert.Same(mockTargetingAgent.Object, factory.TargetingAgent);
            Assert.Same(mockAttackAgent.Object, factory.AttackAgent);
            Assert.Same(mockQuestAgent.Object, factory.QuestAgent);
            Assert.Same(mockLootingAgent.Object, factory.LootingAgent);
            Assert.Same(mockGameObjectAgent.Object, factory.GameObjectAgent);
        }

        #endregion

        #region Null Parameter Tests

        [Fact]
        public void CreateTargetingNetworkAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateTargetingNetworkAgent(null!, _mockTargetingLogger.Object));
        }

        [Fact]
        public void CreateTargetingNetworkAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateTargetingNetworkAgent(_mockWorldClient.Object, (ILogger<TargetingNetworkAgent>)null!));
        }

        [Fact]
        public void CreateAttackNetworkAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAttackNetworkAgent(null!, _mockAttackLogger.Object));
        }

        [Fact]
        public void CreateAttackNetworkAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAttackNetworkAgent(_mockWorldClient.Object, (ILogger<AttackNetworkAgent>)null!));
        }

        [Fact]
        public void CreateQuestNetworkAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateQuestNetworkAgent(null!, _mockQuestLogger.Object));
        }

        [Fact]
        public void CreateQuestNetworkAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateQuestNetworkAgent(_mockWorldClient.Object, (ILogger<QuestNetworkAgent>)null!));
        }

        [Fact]
        public void CreateLootingNetworkAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateLootingNetworkAgent(null!, _mockLootingLogger.Object));
        }

        [Fact]
        public void CreateLootingNetworkAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateLootingNetworkAgent(_mockWorldClient.Object, (ILogger<LootingNetworkAgent>)null!));
        }

        [Fact]
        public void CreateGameObjectNetworkAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateGameObjectNetworkAgent(null!, _mockGameObjectLogger.Object));
        }

        [Fact]
        public void CreateGameObjectNetworkAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateGameObjectNetworkAgent(_mockWorldClient.Object, (ILogger<GameObjectNetworkAgent>)null!));
        }

        [Fact]
        public void CreateTargetingNetworkAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateTargetingNetworkAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateAttackNetworkAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAttackNetworkAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateQuestNetworkAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateQuestNetworkAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateLootingNetworkAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateLootingNetworkAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateGameObjectNetworkAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateGameObjectNetworkAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateAllNetworkAgents_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAllNetworkAgents(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateCombatNetworkAgents_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateCombatNetworkAgents(null!, _mockLoggerFactory.Object));
        }

        #endregion
    }
}