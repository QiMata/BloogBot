namespace WoWSharpClient.Agent
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

        #endregion
    }
}