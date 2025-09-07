using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
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
        private readonly Mock<IVendorNetworkAgent> _mockVendorAgent;
        private readonly Mock<IFlightMasterNetworkAgent> _mockFlightMasterAgent;
        private readonly Mock<IDeadActorAgent> _mockDeadActorAgent;
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
            _mockVendorAgent = new Mock<IVendorNetworkAgent>();
            _mockFlightMasterAgent = new Mock<IFlightMasterNetworkAgent>();
            _mockDeadActorAgent = new Mock<IDeadActorAgent>();

            // Setup logger factory for lazy initialization tests
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(NetworkAgentFactory).FullName!)).Returns(_mockLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(TargetingNetworkAgent).FullName!)).Returns(new Mock<ILogger<TargetingNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(AttackNetworkAgent).FullName!)).Returns(new Mock<ILogger<AttackNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(QuestNetworkAgent).FullName!)).Returns(new Mock<ILogger<QuestNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(LootingNetworkAgent).FullName!)).Returns(new Mock<ILogger<LootingNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(GameObjectNetworkAgent).FullName!)).Returns(new Mock<ILogger<GameObjectNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(VendorNetworkAgent).FullName!)).Returns(new Mock<ILogger<VendorNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(FlightMasterNetworkAgent).FullName!)).Returns(new Mock<ILogger<FlightMasterNetworkAgent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(DeadActorAgent).FullName!)).Returns(new Mock<ILogger<DeadActorAgent>>().Object);

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
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.AttackAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_QuestAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.QuestAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_LootingAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.LootingAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_GameObjectAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.GameObjectAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_VendorAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.VendorAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<VendorNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_FlightMasterAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.FlightMasterAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<FlightMasterNetworkAgent>(agent);
        }

        [Fact]
        public void LazyConstructor_DeadActorAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.DeadActorAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<DeadActorAgent>(agent);
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

        [Fact]
        public void LazyFactory_AllAgents_AreAccessible()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act & Assert
            Assert.NotNull(lazyFactory.TargetingAgent);
            Assert.NotNull(lazyFactory.AttackAgent);
            Assert.NotNull(lazyFactory.QuestAgent);
            Assert.NotNull(lazyFactory.LootingAgent);
            Assert.NotNull(lazyFactory.GameObjectAgent);
            Assert.NotNull(lazyFactory.VendorAgent);
            Assert.NotNull(lazyFactory.FlightMasterAgent);
            Assert.NotNull(lazyFactory.DeadActorAgent);
        }

        [Fact]
        public void LazyFactory_MultipleAccess_ReturnsSameInstance()
        {
            // Arrange
            var lazyFactory = new NetworkAgentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent1 = lazyFactory.TargetingAgent;
            var agent2 = lazyFactory.TargetingAgent;

            // Assert
            Assert.Same(agent1, agent2);
        }

        #endregion
    }
}