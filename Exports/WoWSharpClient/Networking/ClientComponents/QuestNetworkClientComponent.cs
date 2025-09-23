using System.Reactive;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of quest network agent that handles quest operations in World of Warcraft.
    /// Manages quest interaction, acceptance, completion, sharing and reward selection using the Mangos protocol.
    /// Purely observable (no Subjects/events). Streams are derived from opcode handlers or local state changes.
    /// </summary>
    public class QuestNetworkClientComponent : NetworkClientComponent, IQuestNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<QuestNetworkClientComponent> _logger;
        private bool _disposed;

        private ulong? _currentQuestGiver;

        // Reactive Streams (opcode-backed)
        public IObservable<QuestData> QuestOperations { get; }
        public IObservable<QuestProgressData> QuestProgress { get; }
        public IObservable<QuestRewardData> QuestRewards { get; }
        public IObservable<QuestErrorData> QuestErrors { get; }

        public IObservable<QuestData> QuestOffered { get; }
        public IObservable<QuestData> QuestAccepted { get; }
        public IObservable<QuestData> QuestCompleted { get; }
        public IObservable<QuestData> QuestAbandoned { get; }

        /// <summary>
        /// Initializes a new instance of the QuestNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public QuestNetworkClientComponent(IWorldClient worldClient, ILogger<QuestNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Build opcode-backed streams. We use minimal parsers to keep it robust when payload layout differs by core.
            // Offered quests often arrive via details/list packets.
            var offeredFromDetails = SafeOpcodeStream(Opcode.SMSG_QUESTGIVER_QUEST_DETAILS)
                .Select(ParseQuestDetailsToQuestData)
                .Where(q => q is not null)
                .Select(q => q!)
                .Publish()
                .RefCount();

            var offeredFromList = SafeOpcodeStream(Opcode.SMSG_QUESTGIVER_QUEST_LIST)
                .SelectMany(ParseQuestListToQuestData)
                .Publish()
                .RefCount();

            // Accepted can be confirmed by server; map that as Accepted when possible
            var accepted = SafeOpcodeStream(Opcode.SMSG_QUEST_CONFIRM_ACCEPT)
                .Select(ParseQuestConfirmAcceptToQuestData)
                .Where(q => q is not null)
                .Select(q => q!)
                .Publish()
                .RefCount();

            // Completed/Reward available
            var completed = SafeOpcodeStream(Opcode.SMSG_QUESTGIVER_QUEST_COMPLETE)
                .Select(ParseQuestCompleteToQuestData)
                .Where(q => q is not null)
                .Select(q => q!)
                .Publish()
                .RefCount();

            // Errors
            var failed = SafeOpcodeStream(Opcode.SMSG_QUESTGIVER_QUEST_FAILED)
                .Select(ParseQuestFailedToError)
                .Publish()
                .RefCount();

            var invalid = SafeOpcodeStream(Opcode.SMSG_QUESTGIVER_QUEST_INVALID)
                .Select(ParseQuestInvalidToError)
                .Publish()
                .RefCount();

            // Rewards chosen (some cores emit a message; if not, keep empty)
            var rewards = Observable.Empty<QuestRewardData>();

            // Progress updates (no reliable opcode across cores here -> keep empty for now)
            var progress = Observable.Empty<QuestProgressData>();

            QuestErrors = failed.Merge(invalid).Publish().RefCount();
            QuestRewards = rewards.Publish().RefCount();
            QuestProgress = progress.Publish().RefCount();

            QuestOperations = offeredFromDetails
                .Merge(offeredFromList)
                .Merge(accepted)
                .Merge(completed)
                .Publish()
                .RefCount();

            // Filtered streams
            QuestOffered = QuestOperations.Where(q => q.OperationType == QuestOperationType.Offered).Publish().RefCount();
            QuestAccepted = QuestOperations.Where(q => q.OperationType == QuestOperationType.Accepted).Publish().RefCount();
            QuestCompleted = QuestOperations.Where(q => q.OperationType == QuestOperationType.Completed).Publish().RefCount();
            QuestAbandoned = QuestOperations.Where(q => q.OperationType == QuestOperationType.Abandoned).Publish().RefCount();
        }

        public ulong? CurrentQuestGiver => _currentQuestGiver;

        #region Operations

        /// <inheritdoc />
        public async Task QueryQuestGiverStatusAsync(ulong npcGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Querying quest giver status for NPC: {NpcGuid:X}", npcGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(npcGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_STATUS_QUERY, payload, cancellationToken);

                _logger.LogInformation("Quest giver status query sent for NPC: {NpcGuid:X}", npcGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query quest giver status for NPC: {NpcGuid:X}", npcGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task HelloQuestGiverAsync(ulong questGiverGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Initiating conversation with quest giver: {QuestGiverGuid:X}", questGiverGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_HELLO, payload, cancellationToken);

                _currentQuestGiver = questGiverGuid;
                _logger.LogInformation("Quest giver hello sent to NPC: {QuestGiverGuid:X}", questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate conversation with quest giver: {QuestGiverGuid:X}", questGiverGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QueryQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Querying quest {QuestId} from quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_QUERY_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest query sent for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task AcceptQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Accepting quest {QuestId} from quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_ACCEPT_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest accept sent for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CompleteQuestAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Completing quest {QuestId} with quest giver: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_COMPLETE_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest complete sent for quest {QuestId} to: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete quest {QuestId} with: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task RequestQuestRewardAsync(ulong questGiverGuid, uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Requesting rewards for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_REQUEST_REWARD, payload, cancellationToken);

                _logger.LogInformation("Quest reward request sent for quest {QuestId} to: {QuestGiverGuid:X}", questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request quest rewards for quest {QuestId} from: {QuestGiverGuid:X}", questId, questGiverGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ChooseQuestRewardAsync(ulong questGiverGuid, uint questId, uint rewardIndex, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Choosing reward {RewardIndex} for quest {QuestId} from: {QuestGiverGuid:X}",
                    rewardIndex, questId, questGiverGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(questGiverGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);
                BitConverter.GetBytes(rewardIndex).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_CHOOSE_REWARD, payload, cancellationToken);

                _logger.LogInformation("Quest reward choice sent: reward {RewardIndex} for quest {QuestId} to: {QuestGiverGuid:X}",
                    rewardIndex, questId, questGiverGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to choose quest reward {RewardIndex} for quest {QuestId} from: {QuestGiverGuid:X}",
                    rewardIndex, questId, questGiverGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CancelQuestInteractionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Canceling quest interaction");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTGIVER_CANCEL, [], cancellationToken);

                _currentQuestGiver = null;
                _logger.LogInformation("Quest interaction canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel quest interaction");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task RemoveQuestFromLogAsync(byte questLogSlot, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Removing quest from log slot: {QuestLogSlot}", questLogSlot);

                var payload = new byte[1];
                payload[0] = questLogSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_QUESTLOG_REMOVE_QUEST, payload, cancellationToken);

                _logger.LogInformation("Quest removed from log slot: {QuestLogSlot}", questLogSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove quest from log slot: {QuestLogSlot}", questLogSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task PushQuestToPartyAsync(uint questId, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(QuestNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Pushing quest {QuestId} to party", questId);

                var payload = new byte[4];
                BitConverter.GetBytes(questId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_PUSHQUESTTOPARTY, payload, cancellationToken);

                _logger.LogInformation("Quest {QuestId} pushed to party", questId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push quest {QuestId} to party", questId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Helpers

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private static QuestData? ParseQuestDetailsToQuestData(ReadOnlyMemory<byte> payload)
        {
            // Minimal parser: attempt to read questId from first 4 bytes, otherwise 0
            var span = payload.Span;
            uint questId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0U;
            return new QuestData(questId, "Quest Details", 0UL, QuestOperationType.Offered, DateTime.UtcNow);
        }

        private static IEnumerable<QuestData> ParseQuestListToQuestData(ReadOnlyMemory<byte> payload)
        {
            // Without spec, emit a single placeholder offered entry if payload present
            yield return new QuestData(0U, "Quest List", 0UL, QuestOperationType.Offered, DateTime.UtcNow);
        }

        private static QuestData? ParseQuestConfirmAcceptToQuestData(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint questId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0U;
            return new QuestData(questId, "Quest Accepted", 0UL, QuestOperationType.Accepted, DateTime.UtcNow);
        }

        private static QuestData? ParseQuestCompleteToQuestData(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint questId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0U;
            return new QuestData(questId, "Quest Completed", 0UL, QuestOperationType.Completed, DateTime.UtcNow);
        }

        private static QuestErrorData ParseQuestFailedToError(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint questId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0U;
            return new QuestErrorData("Quest failed", questId, null, DateTime.UtcNow);
        }

        private static QuestErrorData ParseQuestInvalidToError(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint questId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0U;
            return new QuestErrorData("Quest invalid", questId, null, DateTime.UtcNow);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("QuestNetworkClientComponent disposed");
            base.Dispose();
        }

        #endregion

        #region Legacy Methods (for backwards compatibility)
        [Obsolete("Use opcode-backed observables")] 
        public void ReportQuestEvent(string eventType, uint questId, string? message = null) { }
        #endregion
    }
}