namespace WoWSharpClient.Networking.Agent.I
{
    /// <summary>
    /// Interface for handling quest operations in World of Warcraft.
    /// Manages quest interaction, acceptance, completion, and reward selection.
    /// </summary>
    public interface IQuestNetworkAgent
    {
        /// <summary>
        /// Event fired when a quest is offered by an NPC.
        /// </summary>
        event Action<uint> QuestOffered;

        /// <summary>
        /// Event fired when a quest is successfully accepted.
        /// </summary>
        event Action<uint> QuestAccepted;

        /// <summary>
        /// Event fired when a quest is completed.
        /// </summary>
        event Action<uint> QuestCompleted;

        /// <summary>
        /// Event fired when quest progress is updated.
        /// </summary>
        event Action<uint, string> QuestProgressUpdated;

        /// <summary>
        /// Event fired when a quest error occurs.
        /// </summary>
        event Action<string> QuestError;

        /// <summary>
        /// Queries the quest giver status of an NPC.
        /// Sends CMSG_QUESTGIVER_STATUS_QUERY to the server.
        /// </summary>
        /// <param name="npcGuid">The GUID of the NPC to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryQuestGiverStatusAsync(ulong npcGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Initiates conversation with a quest giver.
        /// Sends CMSG_QUESTGIVER_HELLO to the server.
        /// </summary>
        /// <param name="questGiverGuid">The GUID of the quest giver.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HelloQuestGiverAsync(ulong questGiverGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries details for a specific quest.
        /// Sends CMSG_QUESTGIVER_QUERY_QUEST to the server.
        /// </summary>
        /// <param name="questGiverGuid">The GUID of the quest giver.</param>
        /// <param name="questId">The ID of the quest to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Accepts a quest from a quest giver.
        /// Sends CMSG_QUESTGIVER_ACCEPT_QUEST to the server.
        /// </summary>
        /// <param name="questGiverGuid">The GUID of the quest giver.</param>
        /// <param name="questId">The ID of the quest to accept.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AcceptQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes a quest with a quest giver.
        /// Sends CMSG_QUESTGIVER_COMPLETE_QUEST to the server.
        /// </summary>
        /// <param name="questGiverGuid">The GUID of the quest giver.</param>
        /// <param name="questId">The ID of the quest to complete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CompleteQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests rewards for a completed quest.
        /// Sends CMSG_QUESTGIVER_REQUEST_REWARD to the server.
        /// </summary>
        /// <param name="questGiverGuid">The GUID of the quest giver.</param>
        /// <param name="questId">The ID of the quest to request rewards for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestQuestRewardAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Chooses a reward for a completed quest.
        /// Sends CMSG_QUESTGIVER_CHOOSE_REWARD to the server.
        /// </summary>
        /// <param name="questGiverGuid">The GUID of the quest giver.</param>
        /// <param name="questId">The ID of the quest.</param>
        /// <param name="rewardIndex">The index of the reward to choose.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ChooseQuestRewardAsync(ulong questGiverGuid, uint questId, uint rewardIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the current quest interaction.
        /// Sends CMSG_QUESTGIVER_CANCEL to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelQuestInteractionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a quest from the quest log.
        /// Sends CMSG_QUESTLOG_REMOVE_QUEST to the server.
        /// </summary>
        /// <param name="questLogSlot">The quest log slot to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveQuestFromLogAsync(byte questLogSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes a quest to party members.
        /// Sends CMSG_PUSHQUESTTOPARTY to the server.
        /// </summary>
        /// <param name="questId">The ID of the quest to push.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PushQuestToPartyAsync(uint questId, CancellationToken cancellationToken = default);
    }
}