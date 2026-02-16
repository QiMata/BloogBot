using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling quest operations in World of Warcraft.
    /// Manages quest interaction, acceptance, completion, and reward selection.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public interface IQuestNetworkClientComponent : INetworkClientComponent
    {
        #region Properties

        /// <summary>
        /// Gets the currently active quest giver GUID if in conversation.
        /// </summary>
        ulong? CurrentQuestGiver { get; }

        #endregion

        #region Reactive Observables

        /// <summary>
        /// Observable stream of quest operations (offered, accepted, completed, etc.).
        /// </summary>
        IObservable<QuestData> QuestOperations { get; }

        /// <summary>
        /// Observable stream of quest progress updates.
        /// </summary>
        IObservable<QuestProgressData> QuestProgress { get; }

        /// <summary>
        /// Observable stream of quest reward selections.
        /// </summary>
        IObservable<QuestRewardData> QuestRewards { get; }

        /// <summary>
        /// Observable stream of quest errors.
        /// </summary>
        IObservable<QuestErrorData> QuestErrors { get; }

        /// <summary>
        /// Observable stream of quest operations filtered to only offered quests.
        /// </summary>
        IObservable<QuestData> QuestOffered { get; }

        /// <summary>
        /// Observable stream of quest operations filtered to only accepted quests.
        /// </summary>
        IObservable<QuestData> QuestAccepted { get; }

        /// <summary>
        /// Observable stream of quest operations filtered to only completed quests.
        /// </summary>
        IObservable<QuestData> QuestCompleted { get; }

        /// <summary>
        /// Observable stream of quest operations filtered to only abandoned quests.
        /// </summary>
        IObservable<QuestData> QuestAbandoned { get; }

        #endregion

        #region Operations

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

        #endregion
    }
}