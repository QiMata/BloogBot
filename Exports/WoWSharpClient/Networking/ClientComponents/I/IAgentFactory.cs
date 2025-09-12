namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for the Agent Factory that provides access to all network agents.
    /// </summary>
    public interface IAgentFactory
    {
        #region Agent Access Properties

        /// <summary>
        /// Gets the targeting network agent for target selection operations.
        /// </summary>
        ITargetingNetworkClientComponent TargetingAgent { get; }

        /// <summary>
        /// Gets the attack network agent for combat operations.
        /// </summary>
        IAttackNetworkClientComponent AttackAgent { get; }

        /// <summary>
        /// Gets the chat network agent for sending and receiving chat messages.
        /// </summary>
        IChatNetworkClientComponent ChatAgent { get; }

        /// <summary>
        /// Gets the quest network agent for quest management operations.
        /// </summary>
        IQuestNetworkClientComponent QuestAgent { get; }

        /// <summary>
        /// Gets the looting network agent for loot operations.
        /// </summary>
        ILootingNetworkClientComponent LootingAgent { get; }

        /// <summary>
        /// Gets the game object network agent for game object interactions.
        /// </summary>
        IGameObjectNetworkClientComponent GameObjectAgent { get; }

        /// <summary>
        /// Gets the vendor network agent for buying, selling, and repairing operations.
        /// </summary>
        IVendorNetworkClientComponent VendorAgent { get; }

        /// <summary>
        /// Gets the flight master network agent for taxi and flight operations.
        /// </summary>
        IFlightMasterNetworkClientComponent FlightMasterAgent { get; }

        /// <summary>
        /// Gets the dead actor agent for death and resurrection operations.
        /// </summary>
        IDeadActorClientComponent DeadActorAgent { get; }

        /// <summary>
        /// Gets the inventory network agent for inventory management operations.
        /// </summary>
        IInventoryNetworkClientComponent InventoryAgent { get; }

        /// <summary>
        /// Gets the item use network agent for item usage operations.
        /// </summary>
        IItemUseNetworkClientComponent ItemUseAgent { get; }

        /// <summary>
        /// Gets the equipment network agent for equipment management operations.
        /// </summary>
        IEquipmentNetworkClientComponent EquipmentAgent { get; }

        /// <summary>
        /// Gets the spell casting network agent for spell casting operations.
        /// </summary>
        ISpellCastingNetworkClientComponent SpellCastingAgent { get; }

        /// <summary>
        /// Gets the auction house network agent for auction house operations.
        /// </summary>
        IAuctionHouseNetworkClientComponent AuctionHouseAgent { get; }

        /// <summary>
        /// Gets the bank network agent for personal bank operations.
        /// </summary>
        IBankNetworkClientComponent BankAgent { get; }

        /// <summary>
        /// Gets the mail network agent for mail system interactions.
        /// </summary>
        IMailNetworkClientComponent MailAgent { get; }

        /// <summary>
        /// Gets the guild network agent for guild management operations.
        /// </summary>
        IGuildNetworkClientComponent GuildAgent { get; }

        /// <summary>
        /// Gets the party network agent for party/raid group management operations.
        /// </summary>
        IPartyNetworkClientComponent PartyAgent { get; }

        /// <summary>
        /// Gets the trainer network agent for learning spells and abilities from class trainers.
        /// </summary>
        ITrainerNetworkClientComponent TrainerAgent { get; }

        /// <summary>
        /// Gets the talent network agent for allocating talent points and respecing.
        /// </summary>
        ITalentNetworkClientComponent TalentAgent { get; }

        /// <summary>
        /// Gets the professions network agent for profession skill training, crafting, and gathering operations.
        /// </summary>
        IProfessionsNetworkClientComponent ProfessionsAgent { get; }

        /// <summary>
        /// Gets the emote network agent for performing emotes and animations.
        /// </summary>
        IEmoteNetworkClientComponent EmoteAgent { get; }

        /// <summary>
        /// Gets the gossip network agent for NPC dialogue and multi-step conversations.
        /// </summary>
        IGossipNetworkClientComponent GossipAgent { get; }

        /// <summary>
        /// Gets the friend network agent for friends list operations.
        /// </summary>
        IFriendNetworkClientComponent FriendAgent { get; }

        /// <summary>
        /// Gets the ignore network agent for ignore list operations.
        /// </summary>
        IIgnoreNetworkClientComponent IgnoreAgent { get; }

        /// <summary>
        /// Gets the trade network agent for player-to-player trading.
        /// </summary>
        ITradeNetworkClientComponent TradeAgent { get; }

        #endregion
    }
}