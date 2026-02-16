using Microsoft.Extensions.Logging;
using System;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Factory for creating network agent instances for World of Warcraft client operations.
    /// Provides a centralized way to create and configure all network agent components.
    /// </summary>
    public static class AgentFactory
    {
        #region Core Agent Creation Methods

        // Targeting Agent
        public static ITargetingNetworkClientComponent CreateTargetingNetworkClientComponent(IWorldClient worldClient, ILogger<TargetingNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new TargetingNetworkClientComponent(worldClient, logger);
        }

        public static ITargetingNetworkClientComponent CreateTargetingNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<TargetingNetworkClientComponent>();
            return new TargetingNetworkClientComponent(worldClient, logger);
        }

        public static ITargetingNetworkClientComponent CreateTargetingNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateTargetingNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<TargetingNetworkClientComponent>();
            return new TargetingNetworkClientComponent(worldClient, logger);
        }

        // Attack Agent
        public static IAttackNetworkClientComponent CreateAttackNetworkClientComponent(IWorldClient worldClient, ILogger<AttackNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new AttackNetworkClientComponent(worldClient, logger);
        }

        public static IAttackNetworkClientComponent CreateAttackNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<AttackNetworkClientComponent>();
            return new AttackNetworkClientComponent(worldClient, logger);
        }

        public static IAttackNetworkClientComponent CreateAttackNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateAttackNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<AttackNetworkClientComponent>();
            return new AttackNetworkClientComponent(worldClient, logger);
        }

        // Chat Agent
        public static IChatNetworkClientComponent CreateChatNetworkClientComponent(IWorldClient worldClient, ILogger<ChatNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new ChatNetworkClientComponent(worldClient, logger);
        }

        public static IChatNetworkClientComponent CreateChatNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<ChatNetworkClientComponent>();
            return new ChatNetworkClientComponent(worldClient, logger);
        }

        public static IChatNetworkClientComponent CreateChatNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateChatNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<ChatNetworkClientComponent>();
            return new ChatNetworkClientComponent(worldClient, logger);
        }

        // Quest Agent
        public static IQuestNetworkClientComponent CreateQuestNetworkClientComponent(IWorldClient worldClient, ILogger<QuestNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new QuestNetworkClientComponent(worldClient, logger);
        }

        public static IQuestNetworkClientComponent CreateQuestNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<QuestNetworkClientComponent>();
            return new QuestNetworkClientComponent(worldClient, logger);
        }

        public static IQuestNetworkClientComponent CreateQuestNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateQuestNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<QuestNetworkClientComponent>();
            return new QuestNetworkClientComponent(worldClient, logger);
        }

        // Looting Agent
        public static ILootingNetworkClientComponent CreateLootingNetworkClientComponent(IWorldClient worldClient, ILogger<LootingNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new LootingNetworkClientComponent(worldClient, logger);
        }

        public static ILootingNetworkClientComponent CreateLootingNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<LootingNetworkClientComponent>();
            return new LootingNetworkClientComponent(worldClient, logger);
        }

        public static ILootingNetworkClientComponent CreateLootingNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateLootingNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<LootingNetworkClientComponent>();
            return new LootingNetworkClientComponent(worldClient, logger);
        }

        // Game Object Agent
        public static IGameObjectNetworkClientComponent CreateGameObjectNetworkClientComponent(IWorldClient worldClient, ILogger<GameObjectNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new GameObjectNetworkClientComponent(worldClient, logger);
        }

        public static IGameObjectNetworkClientComponent CreateGameObjectNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<GameObjectNetworkClientComponent>();
            return new GameObjectNetworkClientComponent(worldClient, logger);
        }

        public static IGameObjectNetworkClientComponent CreateGameObjectNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateGameObjectNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<GameObjectNetworkClientComponent>();
            return new GameObjectNetworkClientComponent(worldClient, logger);
        }

        // Gossip Agent
        public static IGossipNetworkClientComponent CreateGossipNetworkClientComponent(IWorldClient worldClient, ILogger<GossipNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new GossipNetworkClientComponent(worldClient, logger);
        }

        public static IGossipNetworkClientComponent CreateGossipNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<GossipNetworkClientComponent>();
            return new GossipNetworkClientComponent(worldClient, logger);
        }

        public static IGossipNetworkClientComponent CreateGossipNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
            {
                var logger = loggerFactory.CreateLogger<GossipNetworkClientComponent>();
                return new GossipNetworkClientComponent(worldClient, logger);
            }
            var consoleLogger = new ConsoleLogger<GossipNetworkClientComponent>();
            return new GossipNetworkClientComponent(worldClient, consoleLogger);
        }

        #endregion

        #region Implemented Agent Factory Methods

        // Vendor Agent - These methods exist but classes don't
        public static IVendorNetworkClientComponent CreateVendorNetworkClientComponent(IWorldClient worldClient, ILogger<VendorNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new VendorNetworkClientComponent(worldClient, logger);
        }

        public static IVendorNetworkClientComponent CreateVendorNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<VendorNetworkClientComponent>();
            return new VendorNetworkClientComponent(worldClient, logger);
        }

        public static IVendorNetworkClientComponent CreateVendorNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateVendorNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<VendorNetworkClientComponent>();
            return new VendorNetworkClientComponent(worldClient, logger);
        }

        // Flight Master Agent
        public static IFlightMasterNetworkClientComponent CreateFlightMasterNetworkClientComponent(IWorldClient worldClient, ILogger<FlightMasterNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new FlightMasterNetworkClientComponent(worldClient, logger);
        }

        public static IFlightMasterNetworkClientComponent CreateFlightMasterNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<FlightMasterNetworkClientComponent>();
            return new FlightMasterNetworkClientComponent(worldClient, logger);
        }

        public static IFlightMasterNetworkClientComponent CreateFlightMasterNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateFlightMasterNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<FlightMasterNetworkClientComponent>();
            return new FlightMasterNetworkClientComponent(worldClient, logger);
        }

        // Dead Actor Agent
        public static IDeadActorNetworkClientComponent CreateDeadActorAgent(IWorldClient worldClient, ILogger<DeadActorClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new DeadActorClientComponent(worldClient, logger);
        }

        public static IDeadActorNetworkClientComponent CreateDeadActorAgent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<DeadActorClientComponent>();
            return new DeadActorClientComponent(worldClient, logger);
        }

        public static IDeadActorNetworkClientComponent CreateDeadActorAgentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateDeadActorAgent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<DeadActorClientComponent>();
            return new DeadActorClientComponent(worldClient, logger);
        }

        // Inventory Agent
        public static IInventoryNetworkClientComponent CreateInventoryNetworkClientComponent(IWorldClient worldClient, ILogger<InventoryNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new InventoryNetworkClientComponent(worldClient, logger);
        }

        public static IInventoryNetworkClientComponent CreateInventoryNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<InventoryNetworkClientComponent>();
            return new InventoryNetworkClientComponent(worldClient, logger);
        }

        public static IInventoryNetworkClientComponent CreateInventoryNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateInventoryNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<InventoryNetworkClientComponent>();
            return new InventoryNetworkClientComponent(worldClient, logger);
        }

        // Item Use Agent
        public static IItemUseNetworkClientComponent CreateItemUseNetworkClientComponent(IWorldClient worldClient, ILogger<ItemUseNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new ItemUseNetworkClientComponent(worldClient, logger);
        }

        public static IItemUseNetworkClientComponent CreateItemUseNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<ItemUseNetworkClientComponent>();
            return new ItemUseNetworkClientComponent(worldClient, logger);
        }

        public static IItemUseNetworkClientComponent CreateItemUseNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateItemUseNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<ItemUseNetworkClientComponent>();
            return new ItemUseNetworkClientComponent(worldClient, logger);
        }

        // Equipment Agent
        public static IEquipmentNetworkClientComponent CreateEquipmentNetworkClientComponent(IWorldClient worldClient, ILogger<EquipmentNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new EquipmentNetworkClientComponent(worldClient, logger);
        }

        public static IEquipmentNetworkClientComponent CreateEquipmentNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<EquipmentNetworkClientComponent>();
            return new EquipmentNetworkClientComponent(worldClient, logger);
        }

        public static IEquipmentNetworkClientComponent CreateEquipmentNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateEquipmentNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<EquipmentNetworkClientComponent>();
            return new EquipmentNetworkClientComponent(worldClient, logger);
        }

        // Spell Casting Agent
        public static ISpellCastingNetworkClientComponent CreateSpellCastingNetworkClientComponent(IWorldClient worldClient, ILogger<SpellCastingNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new SpellCastingNetworkClientComponent(worldClient, logger);
        }

        public static ISpellCastingNetworkClientComponent CreateSpellCastingNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<SpellCastingNetworkClientComponent>();
            return new SpellCastingNetworkClientComponent(worldClient, logger);
        }

        public static ISpellCastingNetworkClientComponent CreateSpellCastingNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateSpellCastingNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<SpellCastingNetworkClientComponent>();
            return new SpellCastingNetworkClientComponent(worldClient, logger);
        }

        // Friend Agent
        public static IFriendNetworkClientComponent CreateFriendNetworkClientComponent(IWorldClient worldClient, ILogger<FriendNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new FriendNetworkClientComponent(worldClient, logger);
        }

        public static IFriendNetworkClientComponent CreateFriendNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<FriendNetworkClientComponent>();
            return new FriendNetworkClientComponent(worldClient, logger);
        }

        public static IFriendNetworkClientComponent CreateFriendNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateFriendNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<FriendNetworkClientComponent>();
            return new FriendNetworkClientComponent(worldClient, logger);
        }

        // Ignore Agent
        public static IIgnoreNetworkClientComponent CreateIgnoreNetworkClientComponent(IWorldClient worldClient, ILogger<IgnoreNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new IgnoreNetworkClientComponent(worldClient, logger);
        }

        public static IIgnoreNetworkClientComponent CreateIgnoreNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<IgnoreNetworkClientComponent>();
            return new IgnoreNetworkClientComponent(worldClient, logger);
        }

        public static IIgnoreNetworkClientComponent CreateIgnoreNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateIgnoreNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<IgnoreNetworkClientComponent>();
            return new IgnoreNetworkClientComponent(worldClient, logger);
        }

        // Trade Agent
        public static ITradeNetworkClientComponent CreateTradeNetworkClientComponent(IWorldClient worldClient, ILogger<TradeNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new TradeNetworkClientComponent(worldClient, logger);
        }

        public static ITradeNetworkClientComponent CreateTradeNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<TradeNetworkClientComponent>();
            return new TradeNetworkClientComponent(worldClient, logger);
        }

        public static ITradeNetworkClientComponent CreateTradeNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateTradeNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<TradeNetworkClientComponent>();
            return new TradeNetworkClientComponent(worldClient, logger);
        }

        // Auction House Agent
        public static IAuctionHouseNetworkClientComponent CreateAuctionHouseNetworkClientComponent(IWorldClient worldClient, ILogger<AuctionHouseNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new AuctionHouseNetworkClientComponent(worldClient, logger);
        }

        public static IAuctionHouseNetworkClientComponent CreateAuctionHouseNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<AuctionHouseNetworkClientComponent>();
            return new AuctionHouseNetworkClientComponent(worldClient, logger);
        }

        public static IAuctionHouseNetworkClientComponent CreateAuctionHouseNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateAuctionHouseNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<AuctionHouseNetworkClientComponent>();
            return new AuctionHouseNetworkClientComponent(worldClient, logger);
        }

        // Bank Agent
        public static IBankNetworkClientComponent CreateBankNetworkClientComponent(IWorldClient worldClient, ILogger<BankNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new BankNetworkClientComponent(worldClient, logger);
        }

        public static IBankNetworkClientComponent CreateBankNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<BankNetworkClientComponent>();
            return new BankNetworkClientComponent(worldClient, logger);
        }

        public static IBankNetworkClientComponent CreateBankNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateBankNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<BankNetworkClientComponent>();
            return new BankNetworkClientComponent(worldClient, logger);
        }

        // Mail Agent
        public static IMailNetworkClientComponent CreateMailNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<MailNetworkClientComponent>();
            return new MailNetworkClientComponent(worldClient, logger);
        }

        public static IMailNetworkClientComponent CreateMailNetworkClientComponent(IWorldClient worldClient, ILogger<MailNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new MailNetworkClientComponent(worldClient, logger);
        }

        public static IMailNetworkClientComponent CreateMailNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateMailNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<MailNetworkClientComponent>();
            return new MailNetworkClientComponent(worldClient, logger);
        }

        // Guild Agent
        public static IGuildNetworkClientComponent CreateGuildNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<GuildNetworkClientComponent>();
            return new GuildNetworkClientComponent(worldClient, logger);
        }

        public static IGuildNetworkClientComponent CreateGuildNetworkClientComponent(IWorldClient worldClient, ILogger<GuildNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new GuildNetworkClientComponent(worldClient, logger);
        }

        public static IGuildNetworkClientComponent CreateGuildNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateGuildNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<GuildNetworkClientComponent>();
            return new GuildNetworkClientComponent(worldClient, logger);
        }

        // Party Agent
        public static IPartyNetworkClientComponent CreatePartyNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<PartyNetworkClientComponent>();
            return new PartyNetworkClientComponent(worldClient, logger);
        }

        public static IPartyNetworkClientComponent CreatePartyNetworkClientComponent(IWorldClient worldClient, ILogger<PartyNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new PartyNetworkClientComponent(worldClient, logger);
        }

        public static IPartyNetworkClientComponent CreatePartyNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreatePartyNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<PartyNetworkClientComponent>();
            return new PartyNetworkClientComponent(worldClient, logger);
        }

        // Trainer Agent
        public static ITrainerNetworkClientComponent CreateTrainerNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<TrainerNetworkClientComponent>();
            return new TrainerNetworkClientComponent(worldClient, logger);
        }

        public static ITrainerNetworkClientComponent CreateTrainerNetworkClientComponent(IWorldClient worldClient, ILogger<TrainerNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new TrainerNetworkClientComponent(worldClient, logger);
        }

        public static ITrainerNetworkClientComponent CreateTrainerNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateTrainerNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<TrainerNetworkClientComponent>();
            return new TrainerNetworkClientComponent(worldClient, logger);
        }

        // Talent Agent
        public static ITalentNetworkClientComponent CreateTalentNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<TalentNetworkClientComponent>();
            return new TalentNetworkClientComponent(worldClient, logger);
        }

        public static ITalentNetworkClientComponent CreateTalentNetworkClientComponent(IWorldClient worldClient, ILogger<TalentNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new TalentNetworkClientComponent(worldClient, logger);
        }

        public static ITalentNetworkClientComponent CreateTalentNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateTalentNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<TalentNetworkClientComponent>();
            return new TalentNetworkClientComponent(worldClient, logger);
        }

        // Professions Agent
        public static IProfessionsNetworkClientComponent CreateProfessionsNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<ProfessionsNetworkClientComponent>();
            return new ProfessionsNetworkClientComponent(worldClient, logger);
        }

        public static IProfessionsNetworkClientComponent CreateProfessionsNetworkClientComponent(IWorldClient worldClient, ILogger<ProfessionsNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new ProfessionsNetworkClientComponent(worldClient, logger);
        }

        public static IProfessionsNetworkClientComponent CreateProfessionsNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateProfessionsNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<ProfessionsNetworkClientComponent>();
            return new ProfessionsNetworkClientComponent(worldClient, logger);
        }

        // Character Init Agent
        public static ICharacterInitNetworkClientComponent CreateCharacterInitNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<CharacterInitNetworkClientComponent>();
            return new CharacterInitNetworkClientComponent(worldClient, logger);
        }

        public static ICharacterInitNetworkClientComponent CreateCharacterInitNetworkClientComponent(IWorldClient worldClient, ILogger<CharacterInitNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new CharacterInitNetworkClientComponent(worldClient, logger);
        }

        public static ICharacterInitNetworkClientComponent CreateCharacterInitNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateCharacterInitNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<CharacterInitNetworkClientComponent>();
            return new CharacterInitNetworkClientComponent(worldClient, logger);
        }

        // Emote Agent
        public static IEmoteNetworkClientComponent CreateEmoteNetworkClientComponent(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            var logger = loggerFactory.CreateLogger<EmoteNetworkClientComponent>();
            return new EmoteNetworkClientComponent(worldClient, logger);
        }

        public static IEmoteNetworkClientComponent CreateEmoteNetworkClientComponent(IWorldClient worldClient, ILogger<EmoteNetworkClientComponent> logger)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(logger);
            return new EmoteNetworkClientComponent(worldClient, logger);
        }

        public static IEmoteNetworkClientComponent CreateEmoteNetworkClientComponentForClient(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            if (loggerFactory != null)
                return CreateEmoteNetworkClientComponent(worldClient, loggerFactory);
            var logger = new ConsoleLogger<EmoteNetworkClientComponent>();
            return new EmoteNetworkClientComponent(worldClient, logger);
        }

        #endregion

        #region Convenience Methods

        /// <summary>
        /// Creates all network agents as a coordinated set.
        /// </summary>
        public static (
            ITargetingNetworkClientComponent TargetingAgent,
            IAttackNetworkClientComponent AttackAgent,
            IChatNetworkClientComponent ChatAgent,
            IQuestNetworkClientComponent QuestAgent,
            ILootingNetworkClientComponent LootingAgent,
            IGameObjectNetworkClientComponent GameObjectAgent,
            IVendorNetworkClientComponent VendorAgent,
            IFlightMasterNetworkClientComponent FlightMasterAgent,
            IDeadActorNetworkClientComponent DeadActorAgent,
            IInventoryNetworkClientComponent InventoryAgent,
            IItemUseNetworkClientComponent ItemUseAgent,
            IEquipmentNetworkClientComponent EquipmentAgent,
            ISpellCastingNetworkClientComponent SpellCastingAgent,
            IAuctionHouseNetworkClientComponent AuctionHouseAgent,
            IBankNetworkClientComponent BankAgent,
            IMailNetworkClientComponent MailAgent,
            IGuildNetworkClientComponent GuildAgent,
            IPartyNetworkClientComponent PartyAgent,
            ITrainerNetworkClientComponent TrainerAgent,
            ITalentNetworkClientComponent TalentAgent,
            IProfessionsNetworkClientComponent ProfessionsAgent,
            IEmoteNetworkClientComponent EmoteAgent,
            IGossipNetworkClientComponent GossipAgent,
            IFriendNetworkClientComponent FriendAgent,
            IIgnoreNetworkClientComponent IgnoreAgent,
            ITradeNetworkClientComponent TradeAgent
        ) CreateAllNetworkClientComponents(IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return (
                CreateTargetingNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateAttackNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateChatNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateQuestNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateLootingNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateGameObjectNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateVendorNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateFlightMasterNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateDeadActorAgentForClient(worldClient, loggerFactory),
                CreateInventoryNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateItemUseNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateEquipmentNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateSpellCastingNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateAuctionHouseNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateBankNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateMailNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateGuildNetworkClientComponentForClient(worldClient, loggerFactory),
                CreatePartyNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateTrainerNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateTalentNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateProfessionsNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateEmoteNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateGossipNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateFriendNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateIgnoreNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateTradeNetworkClientComponentForClient(worldClient, loggerFactory)
            );
        }

        /// <summary>
        /// Creates combat-focused network agents.
        /// </summary>
        public static (ITargetingNetworkClientComponent TargetingAgent, IAttackNetworkClientComponent AttackAgent) CreateCombatNetworkClientComponents(
            IWorldClient worldClient, ILoggerFactory? loggerFactory = null)
        {
            return (
                CreateTargetingNetworkClientComponentForClient(worldClient, loggerFactory),
                CreateAttackNetworkClientComponentForClient(worldClient, loggerFactory)
            );
        }

        /// <summary>
        /// Creates a Network Agent Factory.
        /// </summary>
        public static IAgentFactory CreateNetworkClientComponentFactory(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            return new NetworkClientComponentFactory(worldClient, loggerFactory);
        }

        /// <summary>
        /// Creates a network agent factory using a subset of agents.
        /// </summary>
        public static IAgentFactory CreateNetworkClientComponentFactory(
            ITargetingNetworkClientComponent targetingAgent,
            IAttackNetworkClientComponent attackAgent,
            IQuestNetworkClientComponent questAgent,
            ILootingNetworkClientComponent lootingAgent,
            IGameObjectNetworkClientComponent gameObjectAgent,
            ILogger<NetworkClientComponentFactory> logger)
        {
            ArgumentNullException.ThrowIfNull(targetingAgent);
            ArgumentNullException.ThrowIfNull(attackAgent);
            ArgumentNullException.ThrowIfNull(questAgent);
            ArgumentNullException.ThrowIfNull(lootingAgent);
            ArgumentNullException.ThrowIfNull(gameObjectAgent);
            ArgumentNullException.ThrowIfNull(logger);
            
            return new NetworkClientComponentFactory(targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent, logger);
        }

        #endregion
    }

    /// <summary>
    /// Simple console logger implementation for development and testing.
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