using GameData.Core.Enums;
using WoWSharpClient.Client;
using Microsoft.Extensions.Logging;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Implementation of quest network agent that handles quest operations in World of Warcraft.
    /// Manages quest interaction, acceptance, completion, and reward selection using the Mangos protocol.
    /// </summary>
    public class QuestNetworkAgent : IQuestNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<QuestNetworkAgent> _logger;

        /// <summary>
        /// Initializes a new instance of the QuestNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public QuestNetworkAgent(IWorldClient worldClient, ILogger<QuestNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public event Action<uint>? QuestOffered;

        /// <inheritdoc />
        public event Action<uint>? QuestAccepted;

        /// <inheritdoc />
        public event Action<uint>? QuestCompleted;

        /// <inheritdoc />
        public event Action<uint, string>? QuestProgressUpdated;

        /// <inheritdoc />
        public event Action<string>? QuestError;

        /// <inheritdoc />
        public async Task QueryQuestGiverStatusAsync(ulong npcGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Querying quest giver status for NPC: {NpcGuid:X}", npcGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(npcGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_STATUS_QUERY, payload, cancellationToken);

                _logger.LogInformation("Quest giver status query sent for NPC: {NpcGuid:X}", npcGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query quest giver status for NPC: {NpcGuid:X}", npcGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task HelloQuestGiverAsync(ulong questGiverGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Initiating conversation with quest giver: {QuestGiverGuid:X}", questGiverGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_HELLO, payload, cancellationToken);

                _logger.LogInformation("Quest giver hello sent to: {QuestGiverGuid:X}", questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to greet quest giver: {QuestGiverGuid:X}", questGiverGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QueryQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Querying quest {QuestId} from quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_QUERY_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest query sent for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AcceptQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Accepting quest {QuestId} from quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_ACCEPT_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest accept sent for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CompleteQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Completing quest {QuestId} with quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_COMPLETE_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest complete sent for quest {QuestId} to: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete quest {QuestId} with: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RequestQuestRewardAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting rewards for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_REQUEST_REWARD, payload, cancellationToken);

                _logger.LogInformation("Quest reward request sent for quest {QuestId} to: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request quest rewards for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ChooseQuestRewardAsync(ulong questGiverGuid, uint questId, uint rewardIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Choosing reward {RewardIndex} for quest {QuestId} from: {QuestGiverGuid:X}", 
                    rewardIndex, questId, questGiverGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);
                BitConverter.GetBytes(rewardIndex).CopyTo(payload, 12);

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_CHOOSE_REWARD, payload, cancellationToken);

                _logger.LogInformation("Quest reward choice sent: reward {RewardIndex} for quest {QuestId} to: {QuestGiverGuid:X}", 
                    rewardIndex, questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to choose quest reward {RewardIndex} for quest {QuestId} from: {QuestGiverGuid:X}", 
                    rewardIndex, questId, questGiverGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CancelQuestInteractionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Canceling quest interaction");

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTGIVER_CANCEL, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Quest interaction canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel quest interaction");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RemoveQuestFromLogAsync(byte questLogSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Removing quest from log slot: {QuestLogSlot}", questLogSlot);

                var payload = new byte[1];
                payload[0] = questLogSlot;

                await _worldClient.SendMovementAsync(Opcode.CMSG_QUESTLOG_REMOVE_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest removed from log slot: {QuestLogSlot}", questLogSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove quest from log slot: {QuestLogSlot}", questLogSlot);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PushQuestToPartyAsync(uint questId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Pushing quest {QuestId} to party", questId);

                var payload = new byte[4];
                BitConverter.GetBytes(questId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_PUSHQUESTTOPARTY, payload, cancellationToken);

                _logger.LogInformation("Quest {QuestId} pushed to party", questId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push quest {QuestId} to party", questId);
                throw;
            }
        }

        /// <summary>
        /// Reports a quest event based on server response.
        /// This should be called when receiving quest-related packets.
        /// </summary>
        /// <param name="eventType">The type of quest event.</param>
        /// <param name="questId">The quest ID associated with the event.</param>
        /// <param name="message">Optional message for the event.</param>
        public void ReportQuestEvent(string eventType, uint questId, string? message = null)
        {
            _logger.LogInformation("Quest event: {EventType} for quest {QuestId}", eventType, questId);

            switch (eventType.ToLowerInvariant())
            {
                case "offered":
                    QuestOffered?.Invoke(questId);
                    break;
                case "accepted":
                    QuestAccepted?.Invoke(questId);
                    break;
                case "completed":
                    QuestCompleted?.Invoke(questId);
                    break;
                case "progress":
                    QuestProgressUpdated?.Invoke(questId, message ?? "Quest progress updated");
                    break;
                case "error":
                    QuestError?.Invoke(message ?? "Quest error occurred");
                    break;
                default:
                    _logger.LogWarning("Unknown quest event type: {EventType}", eventType);
                    break;
            }
        }
    }
}