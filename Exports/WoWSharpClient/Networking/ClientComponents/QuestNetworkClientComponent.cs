using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of quest network agent that handles quest operations in World of Warcraft.
    /// Manages quest interaction, acceptance, completion, and reward selection using the Mangos protocol.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public class QuestNetworkClientComponent : IQuestNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<QuestNetworkClientComponent> _logger;
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private ulong? _currentQuestGiver;

        // Reactive observables
        private readonly Subject<QuestData> _questOperations = new();
        private readonly Subject<QuestProgressData> _questProgress = new();
        private readonly Subject<QuestRewardData> _questRewards = new();
        private readonly Subject<QuestErrorData> _questErrors = new();

        // Filtered observables (lazy-initialized)
        private IObservable<QuestData>? _questOffered;
        private IObservable<QuestData>? _questAccepted;
        private IObservable<QuestData>? _questCompleted;
        private IObservable<QuestData>? _questAbandoned;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the QuestNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public QuestNetworkClientComponent(IWorldClient worldClient, ILogger<QuestNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Properties

        /// <inheritdoc />
        public bool IsOperationInProgress => _isOperationInProgress;

        /// <inheritdoc />
        public DateTime? LastOperationTime => _lastOperationTime;

        /// <inheritdoc />
        public ulong? CurrentQuestGiver => _currentQuestGiver;

        #endregion

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<QuestData> QuestOperations => _questOperations;

        /// <inheritdoc />
        public IObservable<QuestProgressData> QuestProgress => _questProgress;

        /// <inheritdoc />
        public IObservable<QuestRewardData> QuestRewards => _questRewards;

        /// <inheritdoc />
        public IObservable<QuestErrorData> QuestErrors => _questErrors;

        /// <inheritdoc />
        public IObservable<QuestData> QuestOffered =>
            _questOffered ??= _questOperations.Where(q => q.OperationType == QuestOperationType.Offered);

        /// <inheritdoc />
        public IObservable<QuestData> QuestAccepted =>
            _questAccepted ??= _questOperations.Where(q => q.OperationType == QuestOperationType.Accepted);

        /// <inheritdoc />
        public IObservable<QuestData> QuestCompleted =>
            _questCompleted ??= _questOperations.Where(q => q.OperationType == QuestOperationType.Completed);

        /// <inheritdoc />
        public IObservable<QuestData> QuestAbandoned =>
            _questAbandoned ??= _questOperations.Where(q => q.OperationType == QuestOperationType.Abandoned);

        #endregion

        #region Operations

        /// <inheritdoc />
        public async Task QueryQuestGiverStatusAsync(ulong npcGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Querying quest giver status for NPC: {NpcGuid:X}", npcGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(npcGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_STATUS_QUERY, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest giver status query sent for NPC: {NpcGuid:X}", npcGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query quest giver status for NPC: {NpcGuid:X}", npcGuid);

                var errorData = new QuestErrorData(ex.Message, null, npcGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task HelloQuestGiverAsync(ulong questGiverGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Initiating conversation with quest giver: {QuestGiverGuid:X}", questGiverGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_HELLO, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest giver hello sent to NPC: {QuestGiverGuid:X}", questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate conversation with quest giver: {QuestGiverGuid:X}", questGiverGuid);

                var errorData = new QuestErrorData(ex.Message, null, questGiverGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task QueryQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Querying quest {QuestId} from quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_QUERY_QUEST, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest query sent for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);

                var errorData = new QuestErrorData(ex.Message, questId, questGiverGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task AcceptQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Accepting quest {QuestId} from quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_ACCEPT_QUEST, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest accept sent for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);

                var errorData = new QuestErrorData(ex.Message, questId, questGiverGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task CompleteQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Completing quest {QuestId} with quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_COMPLETE_QUEST, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest complete sent for quest {QuestId} to: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete quest {QuestId} with: {QuestGiverGuid:X}", questId, questGiverGuid);

                var errorData = new QuestErrorData(ex.Message, questId, questGiverGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task RequestQuestRewardAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Requesting rewards for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_REQUEST_REWARD, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest reward request sent for quest {QuestId} to: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request quest rewards for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);

                var errorData = new QuestErrorData(ex.Message, questId, questGiverGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task ChooseQuestRewardAsync(ulong questGiverGuid, uint questId, uint rewardIndex, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Choosing reward {RewardIndex} for quest {QuestId} from: {QuestGiverGuid:X}", 
                    rewardIndex, questId, questGiverGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);
                BitConverter.GetBytes(rewardIndex).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_CHOOSE_REWARD, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest reward choice sent: reward {RewardIndex} for quest {QuestId} to: {QuestGiverGuid:X}", 
                    rewardIndex, questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to choose quest reward {RewardIndex} for quest {QuestId} from: {QuestGiverGuid:X}", 
                    rewardIndex, questId, questGiverGuid);

                var errorData = new QuestErrorData(ex.Message, questId, questGiverGuid, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task CancelQuestInteractionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Canceling quest interaction");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_CANCEL, [], cancellationToken);

                _currentQuestGiver = null;
                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest interaction canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel quest interaction");

                var errorData = new QuestErrorData(ex.Message, null, _currentQuestGiver, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task RemoveQuestFromLogAsync(byte questLogSlot, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Removing quest from log slot: {QuestLogSlot}", questLogSlot);

                var payload = new byte[1];
                payload[0] = questLogSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTLOG_REMOVE_QUEST, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest removed from log slot: {QuestLogSlot}", questLogSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove quest from log slot: {QuestLogSlot}", questLogSlot);

                var errorData = new QuestErrorData(ex.Message, null, null, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task PushQuestToPartyAsync(uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Pushing quest {QuestId} to party", questId);

                var payload = new byte[4];
                BitConverter.GetBytes(questId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_PUSHQUESTTOPARTY, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Quest {QuestId} pushed to party", questId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push quest {QuestId} to party", questId);

                var errorData = new QuestErrorData(ex.Message, questId, null, DateTime.UtcNow);
                _questErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        #endregion

        #region Server Response Handling

        /// <inheritdoc />
        public void HandleQuestOperation(uint questId, string questTitle, ulong questGiverGuid, QuestOperationType operationType)
        {
            if (_disposed) return;

            _logger.LogInformation("Quest operation: {OperationType} for quest {QuestId} ({QuestTitle})", 
                operationType, questId, questTitle);

            var questData = new QuestData(questId, questTitle, questGiverGuid, operationType, DateTime.UtcNow);
            _questOperations.OnNext(questData);
        }

        /// <inheritdoc />
        public void HandleQuestProgress(uint questId, string questTitle, string progressText, uint completedObjectives, uint totalObjectives)
        {
            if (_disposed) return;

            _logger.LogInformation("Quest progress: {QuestId} ({QuestTitle}) - {CompletedObjectives}/{TotalObjectives}", 
                questId, questTitle, completedObjectives, totalObjectives);

            var progressData = new QuestProgressData(questId, questTitle, progressText, completedObjectives, totalObjectives, DateTime.UtcNow);
            _questProgress.OnNext(progressData);
        }

        /// <inheritdoc />
        public void HandleQuestReward(uint questId, uint rewardIndex, uint itemId, string itemName, uint quantity)
        {
            if (_disposed) return;

            _logger.LogInformation("Quest reward: {QuestId} - reward {RewardIndex}: {ItemName} x{Quantity}", 
                questId, rewardIndex, itemName, quantity);

            var rewardData = new QuestRewardData(questId, rewardIndex, itemId, itemName, quantity, DateTime.UtcNow);
            _questRewards.OnNext(rewardData);
        }

        /// <inheritdoc />
        public void HandleQuestError(string errorMessage, uint? questId = null, ulong? questGiverGuid = null)
        {
            if (_disposed) return;

            _logger.LogError("Quest error: {ErrorMessage} (Quest: {QuestId}, QuestGiver: {QuestGiverGuid:X})", 
                errorMessage, questId ?? 0, questGiverGuid ?? 0);

            var errorData = new QuestErrorData(errorMessage, questId, questGiverGuid, DateTime.UtcNow);
            _questErrors.OnNext(errorData);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _questOperations.OnCompleted();
            _questProgress.OnCompleted();
            _questRewards.OnCompleted();
            _questErrors.OnCompleted();

            _questOperations.Dispose();
            _questProgress.Dispose();
            _questRewards.Dispose();
            _questErrors.Dispose();
        }

        #endregion

        #region Legacy Methods (for backwards compatibility)

        /// <summary>
        /// Reports a quest event based on server response.
        /// This should be called when receiving quest-related packets.
        /// Use HandleQuestOperation, HandleQuestProgress, etc. instead.
        /// </summary>
        /// <param name="eventType">The type of quest event.</param>
        /// <param name="questId">The quest ID associated with the event.</param>
        /// <param name="message">Optional message for the event.</param>
        [Obsolete("Use specific Handle methods instead")]
        public void ReportQuestEvent(string eventType, uint questId, string? message = null)
        {
            var operationType = eventType.ToLowerInvariant() switch
            {
                "offered" => QuestOperationType.Offered,
                "accepted" => QuestOperationType.Accepted,
                "completed" => QuestOperationType.Completed,
                "progress" => QuestOperationType.ProgressUpdated,
                _ => QuestOperationType.ProgressUpdated
            };

            if (eventType.ToLowerInvariant() == "error")
            {
                HandleQuestError(message ?? "Quest error occurred", questId);
            }
            else if (eventType.ToLowerInvariant() == "progress")
            {
                HandleQuestProgress(questId, "Unknown Quest", message ?? "Quest progress updated", 0, 0);
            }
            else
            {
                HandleQuestOperation(questId, "Unknown Quest", _currentQuestGiver ?? 0, operationType);
            }
        }

        #endregion
    }
}