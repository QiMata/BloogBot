using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Agent;
using WoWSharpClient.Client;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class NetworkAgentFactoryTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<NetworkAgentFactory>> _mockLogger;
        private readonly Mock<ITargetingNetworkAgent> _mockTargetingAgent;
        private readonly Mock<IAttackNetworkAgent> _mockAttackAgent;
        private readonly Mock<IQuestNetworkAgent> _mockQuestAgent;
        private readonly Mock<ILootingNetworkAgent> _mockLootingAgent;
        private readonly Mock<IGameObjectNetworkAgent> _mockGameObjectAgent;
        private readonly NetworkAgentFactory _factory;

        public NetworkAgentFactoryTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockLogger = new Mock<ILogger<NetworkAgentFactory>>();
            _mockTargetingAgent = new Mock<ITargetingNetworkAgent>();
            _mockAttackAgent = new Mock<IAttackNetworkAgent>();
            _mockQuestAgent = new Mock<IQuestNetworkAgent>();
            _mockLootingAgent = new Mock<ILootingNetworkAgent>();
            _mockGameObjectAgent = new Mock<IGameObjectNetworkAgent>();

            _factory = new NetworkAgentFactory(
                _mockTargetingAgent.Object,
                _mockAttackAgent.Object,
                _mockQuestAgent.Object,
                _mockLootingAgent.Object,
                _mockGameObjectAgent.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidAgents_InitializesCorrectly()
        {
            // Assert
            Assert.NotNull(_factory.TargetingAgent);
            Assert.NotNull(_factory.AttackAgent);
            Assert.NotNull(_factory.QuestAgent);
            Assert.NotNull(_factory.LootingAgent);
            Assert.NotNull(_factory.GameObjectAgent);
        }

        [Fact]
        public void Constructor_WithNullTargetingAgent_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new NetworkAgentFactory(
                null!,
                _mockAttackAgent.Object,
                _mockQuestAgent.Object,
                _mockLootingAgent.Object,
                _mockGameObjectAgent.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new NetworkAgentFactory(
                _mockTargetingAgent.Object,
                _mockAttackAgent.Object,
                _mockQuestAgent.Object,
                _mockLootingAgent.Object,
                _mockGameObjectAgent.Object,
                null!));
        }

        [Fact]
        public void Constructor_WithWorldClientAndLoggerFactory_InitializesCorrectly()
        {
            // Act
            var factory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(factory);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new NetworkAgentFactory(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new NetworkAgentFactory(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Property Tests

        [Fact]
        public void TargetingAgent_ReturnsCorrectAgent()
        {
            // Act
            var agent = _factory.TargetingAgent;

            // Assert
            Assert.Same(_mockTargetingAgent.Object, agent);
        }

        [Fact]
        public void AttackAgent_ReturnsCorrectAgent()
        {
            // Act
            var agent = _factory.AttackAgent;

            // Assert
            Assert.Same(_mockAttackAgent.Object, agent);
        }

        [Fact]
        public void QuestAgent_ReturnsCorrectAgent()
        {
            // Act
            var agent = _factory.QuestAgent;

            // Assert
            Assert.Same(_mockQuestAgent.Object, agent);
        }

        [Fact]
        public void LootingAgent_ReturnsCorrectAgent()
        {
            // Act
            var agent = _factory.LootingAgent;

            // Assert
            Assert.Same(_mockLootingAgent.Object, agent);
        }

        [Fact]
        public void GameObjectAgent_ReturnsCorrectAgent()
        {
            // Act
            var agent = _factory.GameObjectAgent;

            // Assert
            Assert.Same(_mockGameObjectAgent.Object, agent);
        }

        #endregion

        #region Lazy Initialization Tests

        [Fact]
        public void LazyConstructor_TargetingAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var mockLoggerForTargeting = new Mock<ILogger<TargetingNetworkAgent>>();
            _mockLoggerFactory.Setup(x => x.CreateLogger<NetworkAgentFactory>())
                .Returns(_mockLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger<TargetingNetworkAgent>())
                .Returns(mockLoggerForTargeting.Object);

            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.TargetingAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_AttackAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var mockLoggerForAttack = new Mock<ILogger<AttackNetworkAgent>>();
            _mockLoggerFactory.Setup(x => x.CreateLogger<NetworkAgentFactory>())
                .Returns(_mockLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger<AttackNetworkAgent>())
                .Returns(mockLoggerForAttack.Object);

            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.AttackAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkAgent>(agent);
        }

        #endregion

        #region Interface Compliance Tests

        [Fact]
        public void NetworkAgentFactory_ImplementsIAgentFactory()
        {
            // Assert
            Assert.IsAssignableFrom<IAgentFactory>(_factory);
        }

        [Fact]
        public void IAgentFactory_PropertiesMatchImplementation()
        {
            // Arrange
            IAgentFactory interfaceFactory = _factory;

            // Assert
            Assert.Same(_factory.TargetingAgent, interfaceFactory.TargetingAgent);
            Assert.Same(_factory.AttackAgent, interfaceFactory.AttackAgent);
            Assert.Same(_factory.QuestAgent, interfaceFactory.QuestAgent);
            Assert.Same(_factory.LootingAgent, interfaceFactory.LootingAgent);
            Assert.Same(_factory.GameObjectAgent, interfaceFactory.GameObjectAgent);
        }

        #endregion
    }
}