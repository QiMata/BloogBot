using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class AgentFactoryTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<TargetingNetworkClientComponent>> _mockTargetingLogger;
        private readonly Mock<ILogger<AttackNetworkClientComponent>> _mockAttackLogger;
        private readonly Mock<ILogger<QuestNetworkClientComponent>> _mockQuestLogger;
        private readonly Mock<ILogger<LootingNetworkClientComponent>> _mockLootingLogger;
        private readonly Mock<ILogger<GameObjectNetworkClientComponent>> _mockGameObjectLogger;
        private readonly Mock<ILogger<VendorNetworkClientComponent>> _mockVendorLogger;
        private readonly Mock<ILogger<FlightMasterNetworkClientComponent>> _mockFlightMasterLogger;
        private readonly Mock<ILogger<DeadActorClientComponent>> _mockDeadActorLogger;
        private readonly Mock<ILogger<InventoryNetworkClientComponent>> _mockInventoryLogger;
        private readonly Mock<ILogger<ItemUseNetworkClientComponent>> _mockItemUseLogger;
        private readonly Mock<ILogger<EquipmentNetworkClientComponent>> _mockEquipmentLogger;
        private readonly Mock<ILogger<SpellCastingNetworkClientComponent>> _mockSpellCastingLogger;
        private readonly Mock<ILogger<AuctionHouseNetworkClientComponent>> _mockAuctionHouseLogger;
        private readonly Mock<ILogger<BankNetworkClientComponent>> _mockBankLogger;
        private readonly Mock<ILogger<MailNetworkClientComponent>> _mockMailLogger;
        private readonly Mock<ILogger<GuildNetworkClientComponent>> _mockGuildLogger;
        private readonly Mock<ILogger<PartyNetworkClientComponent>> _mockPartyLogger;
        private readonly Mock<ILogger<TrainerNetworkClientComponent>> _mockTrainerLogger;
        private readonly Mock<ILogger<TalentNetworkClientComponent>> _mockTalentLogger;
        private readonly Mock<ILogger<ProfessionsNetworkClientComponent>> _mockProfessionsLogger;
        private readonly Mock<ILogger<EmoteNetworkClientComponent>> _mockEmoteLogger;
        private readonly Mock<ILogger<NetworkClientComponentFactory>> _mockNetworkClientComponentFactoryLogger;

        public AgentFactoryTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockTargetingLogger = new Mock<ILogger<TargetingNetworkClientComponent>>();
            _mockAttackLogger = new Mock<ILogger<AttackNetworkClientComponent>>();
            _mockQuestLogger = new Mock<ILogger<QuestNetworkClientComponent>>();
            _mockLootingLogger = new Mock<ILogger<LootingNetworkClientComponent>>();
            _mockGameObjectLogger = new Mock<ILogger<GameObjectNetworkClientComponent>>();
            _mockVendorLogger = new Mock<ILogger<VendorNetworkClientComponent>>();
            _mockFlightMasterLogger = new Mock<ILogger<FlightMasterNetworkClientComponent>>();
            _mockDeadActorLogger = new Mock<ILogger<DeadActorClientComponent>>();
            _mockInventoryLogger = new Mock<ILogger<InventoryNetworkClientComponent>>();
            _mockItemUseLogger = new Mock<ILogger<ItemUseNetworkClientComponent>>();
            _mockEquipmentLogger = new Mock<ILogger<EquipmentNetworkClientComponent>>();
            _mockSpellCastingLogger = new Mock<ILogger<SpellCastingNetworkClientComponent>>();
            _mockAuctionHouseLogger = new Mock<ILogger<AuctionHouseNetworkClientComponent>>();
            _mockBankLogger = new Mock<ILogger<BankNetworkClientComponent>>();
            _mockMailLogger = new Mock<ILogger<MailNetworkClientComponent>>();
            _mockGuildLogger = new Mock<ILogger<GuildNetworkClientComponent>>();
            _mockPartyLogger = new Mock<ILogger<PartyNetworkClientComponent>>();
            _mockTrainerLogger = new Mock<ILogger<TrainerNetworkClientComponent>>();
            _mockTalentLogger = new Mock<ILogger<TalentNetworkClientComponent>>();
            _mockProfessionsLogger = new Mock<ILogger<ProfessionsNetworkClientComponent>>();
            _mockEmoteLogger = new Mock<ILogger<EmoteNetworkClientComponent>>();
            _mockNetworkClientComponentFactoryLogger = new Mock<ILogger<NetworkClientComponentFactory>>();

            // Setup the factory to return our specific loggers
            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(TargetingNetworkClientComponent).FullName!))
                .Returns(_mockTargetingLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(AttackNetworkClientComponent).FullName!))
                .Returns(_mockAttackLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(QuestNetworkClientComponent).FullName!))
                .Returns(_mockQuestLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(LootingNetworkClientComponent).FullName!))
                .Returns(_mockLootingLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(GameObjectNetworkClientComponent).FullName!))
                .Returns(_mockGameObjectLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(VendorNetworkClientComponent).FullName!))
                .Returns(_mockVendorLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(FlightMasterNetworkClientComponent).FullName!))
                .Returns(_mockFlightMasterLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(DeadActorClientComponent).FullName!))
                .Returns(_mockDeadActorLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(InventoryNetworkClientComponent).FullName!))
                .Returns(_mockInventoryLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(ItemUseNetworkClientComponent).FullName!))
                .Returns(_mockItemUseLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(EquipmentNetworkClientComponent).FullName!))
                .Returns(_mockEquipmentLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(SpellCastingNetworkClientComponent).FullName!))
                .Returns(_mockSpellCastingLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(AuctionHouseNetworkClientComponent).FullName!))
                .Returns(_mockAuctionHouseLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(BankNetworkClientComponent).FullName!))
                .Returns(_mockBankLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(MailNetworkClientComponent).FullName!))
                .Returns(_mockMailLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(GuildNetworkClientComponent).FullName!))
                .Returns(_mockGuildLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(PartyNetworkClientComponent).FullName!))
                .Returns(_mockPartyLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(TrainerNetworkClientComponent).FullName!))
                .Returns(_mockTrainerLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(TalentNetworkClientComponent).FullName!))
                .Returns(_mockTalentLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(ProfessionsNetworkClientComponent).FullName!))
                .Returns(_mockProfessionsLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(EmoteNetworkClientComponent).FullName!))
                .Returns(_mockEmoteLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(NetworkClientComponentFactory).FullName!))
                .Returns(_mockNetworkClientComponentFactoryLogger.Object);
        }

        #region Targeting Network Agent Tests

        [Fact]
        public void CreateTargetingNetworkClientComponent_WithLogger_ReturnsTargetingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkClientComponent(_mockWorldClient.Object, _mockTargetingLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateTargetingNetworkClientComponent_WithLoggerFactory_ReturnsTargetingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateTargetingNetworkClientComponentForClient_WithLoggerFactory_ReturnsTargetingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateTargetingNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsTargetingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingNetworkClientComponent>(agent);
        }

        #endregion

        #region Attack Network Agent Tests

        [Fact]
        public void CreateAttackNetworkClientComponent_WithLogger_ReturnsAttackNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkClientComponent(_mockWorldClient.Object, _mockAttackLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateAttackNetworkClientComponent_WithLoggerFactory_ReturnsAttackNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateAttackNetworkClientComponentForClient_WithLoggerFactory_ReturnsAttackNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateAttackNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsAttackNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateAttackNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackNetworkClientComponent>(agent);
        }

        #endregion

        #region Quest Network Agent Tests

        [Fact]
        public void CreateQuestNetworkClientComponent_WithLogger_ReturnsQuestNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkClientComponent(_mockWorldClient.Object, _mockQuestLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateQuestNetworkClientComponent_WithLoggerFactory_ReturnsQuestNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateQuestNetworkClientComponentForClient_WithLoggerFactory_ReturnsQuestNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateQuestNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsQuestNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateQuestNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<QuestNetworkClientComponent>(agent);
        }

        #endregion

        #region Looting Network Agent Tests

        [Fact]
        public void CreateLootingNetworkClientComponent_WithLogger_ReturnsLootingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkClientComponent(_mockWorldClient.Object, _mockLootingLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateLootingNetworkClientComponent_WithLoggerFactory_ReturnsLootingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateLootingNetworkClientComponentForClient_WithLoggerFactory_ReturnsLootingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateLootingNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsLootingNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateLootingNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<LootingNetworkClientComponent>(agent);
        }

        #endregion

        #region Game Object Network Agent Tests

        [Fact]
        public void CreateGameObjectNetworkClientComponent_WithLogger_ReturnsGameObjectNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkClientComponent(_mockWorldClient.Object, _mockGameObjectLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateGameObjectNetworkClientComponent_WithLoggerFactory_ReturnsGameObjectNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateGameObjectNetworkClientComponentForClient_WithLoggerFactory_ReturnsGameObjectNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateGameObjectNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsGameObjectNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateGameObjectNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<GameObjectNetworkClientComponent>(agent);
        }

        #endregion

        #region Vendor Network Agent Tests

        [Fact]
        public void CreateVendorNetworkClientComponent_WithLogger_ReturnsVendorNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateVendorNetworkClientComponent(_mockWorldClient.Object, _mockVendorLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<VendorNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateVendorNetworkClientComponent_WithLoggerFactory_ReturnsVendorNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateVendorNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<VendorNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateVendorNetworkClientComponentForClient_WithLoggerFactory_ReturnsVendorNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateVendorNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<VendorNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateVendorNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsVendorNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateVendorNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<VendorNetworkClientComponent>(agent);
        }

        #endregion

        #region Flight Master Network Agent Tests

        [Fact]
        public void CreateFlightMasterNetworkClientComponent_WithLogger_ReturnsFlightMasterNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateFlightMasterNetworkClientComponent(_mockWorldClient.Object, _mockFlightMasterLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<FlightMasterNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateFlightMasterNetworkClientComponent_WithLoggerFactory_ReturnsFlightMasterNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateFlightMasterNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<FlightMasterNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateFlightMasterNetworkClientComponentForClient_WithLoggerFactory_ReturnsFlightMasterNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateFlightMasterNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<FlightMasterNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateFlightMasterNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsFlightMasterNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateFlightMasterNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<FlightMasterNetworkClientComponent>(agent);
        }

        #endregion

        #region Dead Actor Agent Tests

        [Fact]
        public void CreateDeadActorAgent_WithLogger_ReturnsDeadActorAgent()
        {
            // Act
            var agent = AgentFactory.CreateDeadActorAgent(_mockWorldClient.Object, _mockDeadActorLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<DeadActorClientComponent>(agent);
        }

        [Fact]
        public void CreateDeadActorAgent_WithLoggerFactory_ReturnsDeadActorAgent()
        {
            // Act
            var agent = AgentFactory.CreateDeadActorAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<DeadActorClientComponent>(agent);
        }

        [Fact]
        public void CreateDeadActorAgentForClient_WithLoggerFactory_ReturnsDeadActorAgent()
        {
            // Act
            var agent = AgentFactory.CreateDeadActorAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<DeadActorClientComponent>(agent);
        }

        [Fact]
        public void CreateDeadActorAgentForClient_WithoutLoggerFactory_ReturnsDeadActorAgent()
        {
            // Act
            var agent = AgentFactory.CreateDeadActorAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<DeadActorClientComponent>(agent);
        }

        #endregion

        #region Convenience Methods Tests

        [Fact]
        public void CreateAllNetworkClientComponents_WithLoggerFactory_ReturnsAllAgents()
        {
            // Act
            var (targetingAgent, attackAgent, chatAgent, questAgent, lootingAgent, gameObjectAgent, vendorAgent, flightMasterAgent, deadActorAgent, inventoryAgent, itemUseAgent, equipmentAgent, spellCastingAgent, auctionHouseAgent, bankAgent, mailAgent, guildAgent, partyAgent, trainerAgent, talentAgent, professionsAgent, emoteAgent, gossipAgent, friendAgent, ignoreAgent, tradeAgent) = AgentFactory.CreateAllNetworkClientComponents(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.NotNull(chatAgent);
            Assert.NotNull(questAgent);
            Assert.NotNull(lootingAgent);
            Assert.NotNull(gameObjectAgent);
            Assert.NotNull(vendorAgent);
            Assert.NotNull(flightMasterAgent);
            Assert.NotNull(deadActorAgent);
            Assert.NotNull(inventoryAgent);
            Assert.NotNull(itemUseAgent);
            Assert.NotNull(equipmentAgent);
            Assert.NotNull(spellCastingAgent);
            Assert.NotNull(auctionHouseAgent);
            Assert.NotNull(bankAgent);
            Assert.NotNull(mailAgent);
            Assert.NotNull(guildAgent);
            Assert.NotNull(partyAgent);
            Assert.NotNull(trainerAgent);
            Assert.NotNull(talentAgent);
            Assert.NotNull(professionsAgent);
            Assert.NotNull(emoteAgent);
            Assert.NotNull(gossipAgent);
            Assert.NotNull(friendAgent);
            Assert.NotNull(ignoreAgent);
            Assert.NotNull(tradeAgent);
            Assert.IsType<TargetingNetworkClientComponent>(targetingAgent);
            Assert.IsType<AttackNetworkClientComponent>(attackAgent);
            Assert.IsType<ChatNetworkClientComponent>(chatAgent);
            Assert.IsType<QuestNetworkClientComponent>(questAgent);
            Assert.IsType<LootingNetworkClientComponent>(lootingAgent);
            Assert.IsType<GameObjectNetworkClientComponent>(gameObjectAgent);
            Assert.IsType<VendorNetworkClientComponent>(vendorAgent);
            Assert.IsType<FlightMasterNetworkClientComponent>(flightMasterAgent);
            Assert.IsType<DeadActorClientComponent>(deadActorAgent);
            Assert.IsType<InventoryNetworkClientComponent>(inventoryAgent);
            Assert.IsType<ItemUseNetworkClientComponent>(itemUseAgent);
            Assert.IsType<EquipmentNetworkClientComponent>(equipmentAgent);
            Assert.IsType<SpellCastingNetworkClientComponent>(spellCastingAgent);
            Assert.IsType<AuctionHouseNetworkClientComponent>(auctionHouseAgent);
            Assert.IsType<BankNetworkClientComponent>(bankAgent);
            Assert.IsType<MailNetworkClientComponent>(mailAgent);
            Assert.IsType<GuildNetworkClientComponent>(guildAgent);
            Assert.IsType<PartyNetworkClientComponent>(partyAgent);
            Assert.IsType<TrainerNetworkClientComponent>(trainerAgent);
            Assert.IsType<TalentNetworkClientComponent>(talentAgent);
            Assert.IsType<ProfessionsNetworkClientComponent>(professionsAgent);
            Assert.IsType<EmoteNetworkClientComponent>(emoteAgent);
            Assert.IsType<GossipNetworkClientComponent>(gossipAgent);
            Assert.IsType<FriendNetworkClientComponent>(friendAgent);
            Assert.IsType<IgnoreNetworkClientComponent>(ignoreAgent);
            Assert.IsType<TradeNetworkClientComponent>(tradeAgent);
        }

        [Fact]
        public void CreateAllNetworkClientComponents_WithoutLoggerFactory_ReturnsAllAgents()
        {
            // Act
            var (targetingAgent, attackAgent, chatAgent, questAgent, lootingAgent, gameObjectAgent, vendorAgent, flightMasterAgent, deadActorAgent, inventoryAgent, itemUseAgent, equipmentAgent, spellCastingAgent, auctionHouseAgent, bankAgent, mailAgent, guildAgent, partyAgent, trainerAgent, talentAgent, professionsAgent, emoteAgent, gossipAgent, friendAgent, ignoreAgent, tradeAgent) = AgentFactory.CreateAllNetworkClientComponents(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.NotNull(chatAgent);
            Assert.NotNull(questAgent);
            Assert.NotNull(lootingAgent);
            Assert.NotNull(gameObjectAgent);
            Assert.NotNull(vendorAgent);
            Assert.NotNull(flightMasterAgent);
            Assert.NotNull(deadActorAgent);
            Assert.NotNull(inventoryAgent);
            Assert.NotNull(itemUseAgent);
            Assert.NotNull(equipmentAgent);
            Assert.NotNull(spellCastingAgent);
            Assert.NotNull(auctionHouseAgent);
            Assert.NotNull(bankAgent);
            Assert.NotNull(mailAgent);
            Assert.NotNull(guildAgent);
            Assert.NotNull(partyAgent);
            Assert.NotNull(trainerAgent);
            Assert.NotNull(talentAgent);
            Assert.NotNull(professionsAgent);
            Assert.NotNull(emoteAgent);
            Assert.NotNull(gossipAgent);
            Assert.NotNull(friendAgent);
            Assert.NotNull(ignoreAgent);
            Assert.NotNull(tradeAgent);
            Assert.IsType<TargetingNetworkClientComponent>(targetingAgent);
            Assert.IsType<AttackNetworkClientComponent>(attackAgent);
            Assert.IsType<ChatNetworkClientComponent>(chatAgent);
            Assert.IsType<QuestNetworkClientComponent>(questAgent);
            Assert.IsType<LootingNetworkClientComponent>(lootingAgent);
            Assert.IsType<GameObjectNetworkClientComponent>(gameObjectAgent);
            Assert.IsType<VendorNetworkClientComponent>(vendorAgent);
            Assert.IsType<FlightMasterNetworkClientComponent>(flightMasterAgent);
            Assert.IsType<DeadActorClientComponent>(deadActorAgent);
            Assert.IsType<InventoryNetworkClientComponent>(inventoryAgent);
            Assert.IsType<ItemUseNetworkClientComponent>(itemUseAgent);
            Assert.IsType<EquipmentNetworkClientComponent>(equipmentAgent);
            Assert.IsType<SpellCastingNetworkClientComponent>(spellCastingAgent);
            Assert.IsType<AuctionHouseNetworkClientComponent>(auctionHouseAgent);
            Assert.IsType<BankNetworkClientComponent>(bankAgent);
            Assert.IsType<MailNetworkClientComponent>(mailAgent);
            Assert.IsType<GuildNetworkClientComponent>(guildAgent);
            Assert.IsType<PartyNetworkClientComponent>(partyAgent);
            Assert.IsType<TrainerNetworkClientComponent>(trainerAgent);
            Assert.IsType<TalentNetworkClientComponent>(talentAgent);
            Assert.IsType<ProfessionsNetworkClientComponent>(professionsAgent);
            Assert.IsType<EmoteNetworkClientComponent>(emoteAgent);
            Assert.IsType<GossipNetworkClientComponent>(gossipAgent);
            Assert.IsType<FriendNetworkClientComponent>(friendAgent);
            Assert.IsType<IgnoreNetworkClientComponent>(ignoreAgent);
            Assert.IsType<TradeNetworkClientComponent>(tradeAgent);
        }

        [Fact]
        public void CreateCombatNetworkClientComponents_WithLoggerFactory_ReturnsBothAgents()
        {
            // Act
            var (targetingAgent, attackAgent) = AgentFactory.CreateCombatNetworkClientComponents(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.IsType<TargetingNetworkClientComponent>(targetingAgent);
            Assert.IsType<AttackNetworkClientComponent>(attackAgent);
        }

        [Fact]
        public void CreateCombatNetworkClientComponents_WithoutLoggerFactory_ReturnsBothAgents()
        {
            // Act
            var (targetingAgent, attackAgent) = AgentFactory.CreateCombatNetworkClientComponents(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.IsType<TargetingNetworkClientComponent>(targetingAgent);
            Assert.IsType<AttackNetworkClientComponent>(attackAgent);
        }

        #endregion

        #region Network Agent Factory Tests

        [Fact]
        public void CreateNetworkClientComponentFactory_WithValidParameters_ReturnsFactory()
        {
            // Act
            var factory = AgentFactory.CreateNetworkClientComponentFactory(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(factory);
            Assert.IsAssignableFrom<IAgentFactory>(factory);
        }

        [Fact]
        public void CreateNetworkClientComponentFactory_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateNetworkClientComponentFactory(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateNetworkClientComponentFactory_WithNullLoggerFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateNetworkClientComponentFactory(_mockWorldClient.Object, null!));
        }

        [Fact]
        public void CreateNetworkClientComponentFactory_WithIndividualAgents_ReturnsFactory()
        {
            // Arrange
            var mockTargetingAgent = new Mock<ITargetingNetworkClientComponent>();
            var mockAttackAgent = new Mock<IAttackNetworkClientComponent>();
            var mockQuestAgent = new Mock<IQuestNetworkClientComponent>();
            var mockLootingAgent = new Mock<ILootingNetworkClientComponent>();
            var mockGameObjectAgent = new Mock<IGameObjectNetworkClientComponent>();
            var mockLogger = new Mock<ILogger<NetworkClientComponentFactory>>();

            // Setup mock observables to prevent null reference exceptions
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
            mockTargetingAgent.Setup(x => x.TargetChanges).Returns(mockTargetingObservable.Object);

            // Setup observable properties for attack agent
            mockAttackAgent.Setup(x => x.AttackStateChanges).Returns(mockAttackStateObservable.Object);
            mockAttackAgent.Setup(x => x.AttackErrors).Returns(mockAttackErrorObservable.Object);

            // Setup observable properties for quest agent
            mockQuestAgent.Setup(x => x.QuestOffered).Returns(mockQuestOfferedObservable.Object);
            mockQuestAgent.Setup(x => x.QuestAccepted).Returns(mockQuestAcceptedObservable.Object);
            mockQuestAgent.Setup(x => x.QuestCompleted).Returns(mockQuestCompletedObservable.Object);
            mockQuestAgent.Setup(x => x.QuestErrors).Returns(mockQuestErrorObservable.Object);

            // Setup observable properties for looting agent
            mockLootingAgent.Setup(x => x.LootWindowOpened).Returns(mockLootWindowOpenedObservable.Object);
            mockLootingAgent.Setup(x => x.LootWindowClosed).Returns(mockLootWindowClosedObservable.Object);
            mockLootingAgent.Setup(x => x.ItemLoot).Returns(mockItemLootObservable.Object);
            mockLootingAgent.Setup(x => x.MoneyLoot). Returns(mockMoneyLootObservable.Object);
            mockLootingAgent.Setup(x => x.LootErrors).Returns(mockLootErrorObservable.Object);

            // Setup mock Subscribe methods to return disposables (to prevent null reference)
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

            // Also set up the extension method Subscribe(Action<T>) calls by setting up the overload that extension methods use
            // The extension method Subscribe(Action<T>) internally calls Subscribe(IObserver<T>) so our setup above should cover it

            // Act
            var factory = AgentFactory.CreateNetworkClientComponentFactory(
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
        public void CreateTargetingNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateTargetingNetworkClientComponent(null!, _mockTargetingLogger.Object));
        }

        [Fact]
        public void CreateTargetingNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateTargetingNetworkClientComponent(_mockWorldClient.Object, (ILogger<TargetingNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateAttackNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateAttackNetworkClientComponent(null!, _mockAttackLogger.Object));
        }

        [Fact]
        public void CreateAttackNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateAttackNetworkClientComponent(_mockWorldClient.Object, (ILogger<AttackNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateQuestNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateQuestNetworkClientComponent(null!, _mockQuestLogger.Object));
        }

        [Fact]
        public void CreateQuestNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateQuestNetworkClientComponent(_mockWorldClient.Object, (ILogger<QuestNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateLootingNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateLootingNetworkClientComponent(null!, _mockLootingLogger.Object));
        }

        [Fact]
        public void CreateLootingNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateLootingNetworkClientComponent(_mockWorldClient.Object, (ILogger<LootingNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateGameObjectNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateGameObjectNetworkClientComponent(null!, _mockGameObjectLogger.Object));
        }

        [Fact]
        public void CreateGameObjectNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateGameObjectNetworkClientComponent(_mockWorldClient.Object, (ILogger<GameObjectNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateVendorNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateVendorNetworkClientComponent(null!, _mockVendorLogger.Object));
        }

        [Fact]
        public void CreateVendorNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateVendorNetworkClientComponent(_mockWorldClient.Object, (ILogger<VendorNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateFlightMasterNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateFlightMasterNetworkClientComponent(null!, _mockFlightMasterLogger.Object));
        }

        [Fact]
        public void CreateFlightMasterNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateFlightMasterNetworkClientComponent(_mockWorldClient.Object, (ILogger<FlightMasterNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateDeadActorAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateDeadActorAgent(null!, _mockDeadActorLogger.Object));
        }

        [Fact]
        public void CreateDeadActorAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateDeadActorAgent(_mockWorldClient.Object, (ILogger<DeadActorClientComponent>)null!));
        }

        [Fact]
        public void CreateTargetingNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateTargetingNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateAttackNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateAttackNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateQuestNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateQuestNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateLootingNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateLootingNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateGameObjectNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateGameObjectNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateVendorNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateVendorNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateFlightMasterNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateFlightMasterNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateDeadActorAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateDeadActorAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateAllNetworkClientComponents_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateAllNetworkClientComponents(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateCombatNetworkClientComponents_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateCombatNetworkClientComponents(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateMailNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateMailNetworkClientComponent(null!, _mockMailLogger.Object));
        }

        [Fact]
        public void CreateMailNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateMailNetworkClientComponent(_mockWorldClient.Object, (ILogger<MailNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateMailNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateMailNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateGuildNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateGuildNetworkClientComponent(null!, _mockGuildLogger.Object));
        }

        [Fact]
        public void CreateGuildNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateGuildNetworkClientComponent(_mockWorldClient.Object, (ILogger<GuildNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateGuildNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateGuildNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        #endregion

        #region Party Network Agent Tests

        [Fact]
        public void CreatePartyNetworkClientComponent_WithLogger_ReturnsPartyNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreatePartyNetworkClientComponent(_mockWorldClient.Object, _mockPartyLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<PartyNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreatePartyNetworkClientComponent_WithLoggerFactory_ReturnsPartyNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreatePartyNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<PartyNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreatePartyNetworkClientComponentForClient_WithLoggerFactory_ReturnsPartyNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreatePartyNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<PartyNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreatePartyNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsPartyNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreatePartyNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<PartyNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreatePartyNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreatePartyNetworkClientComponent(null!, _mockPartyLogger.Object));
        }

        [Fact]
        public void CreatePartyNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreatePartyNetworkClientComponent(_mockWorldClient.Object, (ILogger<PartyNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreatePartyNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreatePartyNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        #endregion

        #region Trainer Network Agent Tests

        [Fact]
        public void CreateTrainerNetworkClientComponent_WithLoggerFactory_ReturnsAgent()
        {
            // Act
            var agent = AgentFactory.CreateTrainerNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TrainerNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateTrainerNetworkClientComponent_WithLogger_ReturnsAgent()
        {
            // Act
            var agent = AgentFactory.CreateTrainerNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object.CreateLogger<TrainerNetworkClientComponent>());

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TrainerNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateTrainerNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateTrainerNetworkClientComponent(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateTrainerNetworkClientComponent_WithNullLoggerFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateTrainerNetworkClientComponent(_mockWorldClient.Object, (ILoggerFactory)null!));
        }

        [Fact]
        public void CreateTrainerNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateTrainerNetworkClientComponent(_mockWorldClient.Object, (ILogger<TrainerNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateTrainerNetworkClientComponentForClient_WithLoggerFactory_ReturnsAgent()
        {
            // Act
            var agent = AgentFactory.CreateTrainerNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TrainerNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateTrainerNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsAgent()
        {
            // Act
            var agent = AgentFactory.CreateTrainerNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TrainerNetworkClientComponent>(agent);
        }

        #endregion

        #region Professions Network Agent Tests

        [Fact]
        public void CreateProfessionsNetworkClientComponent_WithLogger_ReturnsProfessionsNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateProfessionsNetworkClientComponent(_mockWorldClient.Object, _mockProfessionsLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<ProfessionsNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateProfessionsNetworkClientComponent_WithLoggerFactory_ReturnsProfessionsNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateProfessionsNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<ProfessionsNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateProfessionsNetworkClientComponentForClient_WithLoggerFactory_ReturnsProfessionsNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateProfessionsNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<ProfessionsNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateProfessionsNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsProfessionsNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateProfessionsNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<ProfessionsNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateProfessionsNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateProfessionsNetworkClientComponent(null!, _mockProfessionsLogger.Object));
        }

        [Fact]
        public void CreateProfessionsNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateProfessionsNetworkClientComponent(_mockWorldClient.Object, (ILogger<ProfessionsNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateProfessionsNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateProfessionsNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        #endregion

        #region Emote Network Agent Tests

        [Fact]
        public void CreateEmoteNetworkClientComponent_WithLogger_ReturnsEmoteNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateEmoteNetworkClientComponent(_mockWorldClient.Object, _mockEmoteLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<EmoteNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateEmoteNetworkClientComponent_WithLoggerFactory_ReturnsEmoteNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateEmoteNetworkClientComponent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<EmoteNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateEmoteNetworkClientComponentForClient_WithLoggerFactory_ReturnsEmoteNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateEmoteNetworkClientComponentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<EmoteNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateEmoteNetworkClientComponentForClient_WithoutLoggerFactory_ReturnsEmoteNetworkClientComponent()
        {
            // Act
            var agent = AgentFactory.CreateEmoteNetworkClientComponentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<EmoteNetworkClientComponent>(agent);
        }

        [Fact]
        public void CreateEmoteNetworkClientComponent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateEmoteNetworkClientComponent(null!, _mockEmoteLogger.Object));
        }

        [Fact]
        public void CreateEmoteNetworkClientComponent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateEmoteNetworkClientComponent(_mockWorldClient.Object, (ILogger<EmoteNetworkClientComponent>)null!));
        }

        [Fact]
        public void CreateEmoteNetworkClientComponentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                AgentFactory.CreateEmoteNetworkClientComponentForClient(null!, _mockLoggerFactory.Object));
        }

        #endregion
    }
}