using Microsoft.Extensions.Logging;
using Moq;
using System;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class NetworkClientComponentFactoryTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<NetworkClientComponentFactory>> _mockLogger;
        private readonly Mock<ITargetingNetworkClientComponent> _mockTargetingAgent;
        private readonly Mock<IAttackNetworkClientComponent> _mockAttackAgent;
        private readonly Mock<IQuestNetworkClientComponent> _mockQuestAgent;
        private readonly Mock<ILootingNetworkClientComponent> _mockLootingAgent;
        private readonly Mock<IGameObjectNetworkClientComponent> _mockGameObjectAgent;
        private readonly Mock<IVendorNetworkClientComponent> _mockVendorAgent;
        private readonly Mock<IFlightMasterNetworkClientComponent> _mockFlightMasterAgent;
        private readonly NetworkClientComponentFactory _factory;

        public NetworkClientComponentFactoryTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockLogger = new Mock<ILogger<NetworkClientComponentFactory>>();
            _mockTargetingAgent = new Mock<ITargetingNetworkClientComponent>();
            _mockAttackAgent = new Mock<IAttackNetworkClientComponent>();
            _mockQuestAgent = new Mock<IQuestNetworkClientComponent>();
            _mockLootingAgent = new Mock<ILootingNetworkClientComponent>();
            _mockGameObjectAgent = new Mock<IGameObjectNetworkClientComponent>();
            _mockVendorAgent = new Mock<IVendorNetworkClientComponent>();
            _mockFlightMasterAgent = new Mock<IFlightMasterNetworkClientComponent>();

            // Setup logger factory for lazy initialization tests
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(NetworkClientComponentFactory).FullName!)).Returns(_mockLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(TargetingNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<TargetingNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(AttackNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<AttackNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(QuestNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<QuestNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(LootingNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<LootingNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(GameObjectNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<GameObjectNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(VendorNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<VendorNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(FlightMasterNetworkClientComponent).FullName!)).Returns(new Mock<ILogger<FlightMasterNetworkClientComponent>>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(DeadActorClientComponent).FullName!)).Returns(new Mock<ILogger<DeadActorClientComponent>>().Object);

            // Setup mock observables to prevent null reference exceptions during SetupEventHandlers
            SetupMockObservables();

            _factory = new NetworkClientComponentFactory(
                _mockTargetingAgent.Object,
                _mockAttackAgent.Object,
                _mockQuestAgent.Object,
                _mockLootingAgent.Object,
                _mockGameObjectAgent.Object,
                _mockLogger.Object);
        }

        private void SetupMockObservables()
        {
            // Create mock observables
            var mockTargetingObservable = new Mock<IObservable<TargetingData>>();
            var mockAttackStateObservable = new Mock<IObservable<AttackStateData>>();
            var mockAttackErrorObservable = new Mock<IObservable<AttackErrorData>>();
            var mockQuestOfferedObservable = new Mock<IObservable<QuestData>>();
            var mockQuestAcceptedObservable = new Mock<IObservable<QuestData>>();
            var mockQuestCompletedObservable = new Mock<IObservable<QuestData>>();
            var mockQuestErrorObservable = new Mock<IObservable<QuestErrorData>>();
            var mockLootWindowOpenedObservable = new Mock<IObservable<LootWindowData>>();
            var mockLootWindowClosedObservable = new Mock<IObservable<LootWindowData>>();
            var mockItemLootObservable = new Mock<IObservable<LootData>>();
            var mockMoneyLootObservable = new Mock<IObservable<MoneyLootData>>();
            var mockLootErrorObservable = new Mock<IObservable<LootErrorData>>();

            // Setup observable properties for targeting agent
            _mockTargetingAgent.Setup(x => x.TargetChanges).Returns(mockTargetingObservable.Object);

            // Setup observable properties for attack agent
            _mockAttackAgent.Setup(x => x.AttackStateChanges).Returns(mockAttackStateObservable.Object);
            _mockAttackAgent.Setup(x => x.AttackErrors).Returns(mockAttackErrorObservable.Object);

            // Setup observable properties for quest agent
            _mockQuestAgent.Setup(x => x.QuestOffered).Returns(mockQuestOfferedObservable.Object);
            _mockQuestAgent.Setup(x => x.QuestAccepted).Returns(mockQuestAcceptedObservable.Object);
            _mockQuestAgent.Setup(x => x.QuestCompleted).Returns(mockQuestCompletedObservable.Object);
            _mockQuestAgent.Setup(x => x.QuestErrors).Returns(mockQuestErrorObservable.Object);

            // Setup observable properties for looting agent
            _mockLootingAgent.Setup(x => x.LootWindowOpened).Returns(mockLootWindowOpenedObservable.Object);
            _mockLootingAgent.Setup(x => x.LootWindowClosed).Returns(mockLootWindowClosedObservable.Object);
            _mockLootingAgent.Setup(x => x.ItemLoot).Returns(mockItemLootObservable.Object);
            _mockLootingAgent.Setup(x => x.MoneyLoot).Returns(mockMoneyLootObservable.Object);
            _mockLootingAgent.Setup(x => x.LootErrors).Returns(mockLootErrorObservable.Object);

            // Setup mock Subscribe methods to return disposables
            var mockDisposable = new Mock<IDisposable>();
            mockTargetingObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<TargetingData>>())).Returns(mockDisposable.Object);
            mockAttackStateObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<AttackStateData>>())).Returns(mockDisposable.Object);
            mockAttackErrorObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<AttackErrorData>>())).Returns(mockDisposable.Object);
            mockQuestOfferedObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<QuestData>>())).Returns(mockDisposable.Object);
            mockQuestAcceptedObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<QuestData>>())).Returns(mockDisposable.Object);
            mockQuestCompletedObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<QuestData>>())).Returns(mockDisposable.Object);
            mockQuestErrorObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<QuestErrorData>>())).Returns(mockDisposable.Object);
            mockLootWindowOpenedObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<LootWindowData>>())).Returns(mockDisposable.Object);
            mockLootWindowClosedObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<LootWindowData>>())).Returns(mockDisposable.Object);
            mockItemLootObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<LootData>>())).Returns(mockDisposable.Object);
            mockMoneyLootObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<MoneyLootData>>())).Returns(mockDisposable.Object);
            mockLootErrorObservable.Setup(x => x.Subscribe(It.IsAny<IObserver<LootErrorData>>())).Returns(mockDisposable.Object);
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
            Assert.Throws<ArgumentNullException>(() => new NetworkClientComponentFactory(
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
            Assert.Throws<ArgumentNullException>(() => new NetworkClientComponentFactory(
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
            var factory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(factory);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new NetworkClientComponentFactory(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new NetworkClientComponentFactory(_mockWorldClient.Object, null!));
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
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.TargetingAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_AttackAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.AttackAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_QuestAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.QuestAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_LootingAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.LootingAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_GameObjectAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.GameObjectAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_VendorAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.VendorAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<VendorNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_FlightMasterAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.FlightMasterAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<FlightMasterNetworkClientComponent>(agent);
        }

        [Fact]
        public void LazyConstructor_DeadActorAgent_CreatesAgentOnFirstAccess()
        {
            // Arrange
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent = lazyFactory.DeadActorAgent;

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<DeadActorClientComponent>(agent);
        }

        #endregion

        #region Interface Compliance Tests

        [Fact]
        public void NetworkClientComponentFactory_ImplementsIAgentFactory()
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
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

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
            var lazyFactory = new NetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Act
            var agent1 = lazyFactory.TargetingAgent;
            var agent2 = lazyFactory.TargetingAgent;

            // Assert
            Assert.Same(agent1, agent2);
        }

        #endregion
    }
}