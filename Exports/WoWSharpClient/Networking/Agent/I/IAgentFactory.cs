namespace WoWSharpClient.Networking.Agent.I
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
        ITargetingNetworkAgent TargetingAgent { get; }

        /// <summary>
        /// Gets the attack network agent for combat operations.
        /// </summary>
        IAttackNetworkAgent AttackAgent { get; }

        /// <summary>
        /// Gets the quest network agent for quest management operations.
        /// </summary>
        IQuestNetworkAgent QuestAgent { get; }

        /// <summary>
        /// Gets the looting network agent for loot operations.
        /// </summary>
        ILootingNetworkAgent LootingAgent { get; }

        /// <summary>
        /// Gets the game object network agent for game object interactions.
        /// </summary>
        IGameObjectNetworkAgent GameObjectAgent { get; }

        /// <summary>
        /// Gets the vendor network agent for buying, selling, and repairing operations.
        /// </summary>
        IVendorNetworkAgent VendorAgent { get; }

        /// <summary>
        /// Gets the flight master network agent for taxi and flight operations.
        /// </summary>
        IFlightMasterNetworkAgent FlightMasterAgent { get; }

        /// <summary>
        /// Gets the dead actor agent for death and resurrection operations.
        /// </summary>
        IDeadActorAgent DeadActorAgent { get; }

        /// <summary>
        /// Gets the inventory network agent for inventory management operations.
        /// </summary>
        IInventoryNetworkAgent InventoryAgent { get; }

        /// <summary>
        /// Gets the item use network agent for item usage operations.
        /// </summary>
        IItemUseNetworkAgent ItemUseAgent { get; }

        /// <summary>
        /// Gets the equipment network agent for equipment management operations.
        /// </summary>
        IEquipmentNetworkAgent EquipmentAgent { get; }

        /// <summary>
        /// Gets the spell casting network agent for spell casting operations.
        /// </summary>
        ISpellCastingNetworkAgent SpellCastingAgent { get; }

        /// <summary>
        /// Gets the auction house network agent for auction house operations.
        /// </summary>
        IAuctionHouseNetworkAgent AuctionHouseAgent { get; }

        /// <summary>
        /// Gets the bank network agent for personal bank operations.
        /// </summary>
        IBankNetworkAgent BankAgent { get; }

        /// <summary>
        /// Gets the mail network agent for mail system interactions.
        /// </summary>
        IMailNetworkAgent MailAgent { get; }

        /// <summary>
        /// Gets the guild network agent for guild management operations.
        /// </summary>
        IGuildNetworkAgent GuildAgent { get; }

        /// <summary>
        /// Gets the party network agent for party/raid group management operations.
        /// </summary>
        IPartyNetworkAgent PartyAgent { get; }

        /// <summary>
        /// Gets the trainer network agent for learning spells and abilities from class trainers.
        /// </summary>
        ITrainerNetworkAgent TrainerAgent { get; }

        #endregion
    }
}