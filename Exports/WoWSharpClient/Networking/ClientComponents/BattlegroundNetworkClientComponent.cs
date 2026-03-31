using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Handles battleground queue, invite, and scoreboard operations over the Mangos protocol.
    /// Exposes opcode-backed reactive observables and maintains lightweight local state.
    /// </summary>
    public class BattlegroundNetworkClientComponent : NetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<BattlegroundNetworkClientComponent> _logger;
        private bool _disposed;

        // Reactive streams
        private readonly IObservable<BattlegroundStatus> _statusChanged;
        private readonly IObservable<BattlegroundList> _listReceived;
        private readonly IObservable<PvPLogData> _scoreboardReceived;

        // Self-subscriptions to keep .Do() side effects active
        private readonly IDisposable _statusChangedSub;

        public BattlegroundNetworkClientComponent(IWorldClient worldClient, ILogger<BattlegroundNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _statusChanged = SafeOpcodeStream(Opcode.SMSG_BATTLEFIELD_STATUS)
                .Select(ParseBattlefieldStatus)
                .Do(status =>
                {
                    CurrentBgTypeId = status.BgTypeId > 0 ? status.BgTypeId : null;

                    CurrentState = status.Status switch
                    {
                        BattlegroundStatusId.None => BattlegroundState.None,
                        BattlegroundStatusId.Queued => BattlegroundState.Queued,
                        BattlegroundStatusId.WaitJoin => BattlegroundState.Invited,
                        BattlegroundStatusId.InProgress => BattlegroundState.InBattleground,
                        _ => BattlegroundState.None
                    };

                    _logger.LogInformation(
                        "Battlefield status: Slot={QueueSlot} BgType={BgType} Status={Status} State={State}",
                        status.QueueSlot, status.BgTypeId, status.Status, CurrentState);
                })
                .Publish().RefCount();

            _listReceived = SafeOpcodeStream(Opcode.SMSG_BATTLEFIELD_LIST)
                .Select(ParseBattlefieldList)
                .Do(list =>
                {
                    _logger.LogInformation(
                        "Battlefield list: BgType={BgType} Instances={Count} from BattleMaster={Guid:X}",
                        list.BgTypeId, list.InstanceIds.Count, list.BattleMasterGuid);
                })
                .Publish().RefCount();

            _scoreboardReceived = SafeOpcodeStream(Opcode.MSG_PVP_LOG_DATA)
                .Select(ParsePvPLogData)
                .Do(log =>
                {
                    _logger.LogInformation(
                        "PvP scoreboard: Finished={Finished} Players={Count}",
                        log.IsFinished, log.Scores.Count);
                })
                .Publish().RefCount();

            // Self-subscribe to keep state tracking active
            _statusChangedSub = _statusChanged.Subscribe(_ => { });
        }

        #region State

        /// <summary>Current battleground state derived from the latest SMSG_BATTLEFIELD_STATUS.</summary>
        public BattlegroundState CurrentState { get; private set; }

        /// <summary>Current battleground type ID, or null if not queued/active.</summary>
        public uint? CurrentBgTypeId { get; private set; }

        /// <summary>Queue time in milliseconds from the latest status update, if available.</summary>
        public uint? QueueTimeMs { get; private set; }

        #endregion

        #region Reactive (public)

        /// <summary>Emits whenever SMSG_BATTLEFIELD_STATUS arrives (queue/invite/active status updates).</summary>
        public IObservable<BattlegroundStatus> StatusChanged => _statusChanged;

        /// <summary>Emits whenever SMSG_BATTLEFIELD_LIST arrives (list of active BG instances).</summary>
        public IObservable<BattlegroundList> ListReceived => _listReceived;

        /// <summary>Emits whenever MSG_PVP_LOG_DATA arrives as SMSG (scoreboard data).</summary>
        public IObservable<PvPLogData> ScoreboardReceived => _scoreboardReceived;

        #endregion

        #region Operations (CMSG)

        /// <summary>
        /// Sends CMSG_BATTLEMASTER_JOIN to queue for a battleground.
        /// </summary>
        /// <param name="bgTypeId">Battleground type ID.</param>
        /// <param name="instanceId">Specific instance ID, or 0 for any.</param>
        /// <param name="asGroup">Whether to join as a group.</param>
        /// <param name="battleMasterGuid">GUID of the battlemaster NPC. Use 0 for queue-from-anywhere (may be rejected by anticheat).</param>
        public async Task JoinQueueAsync(uint bgTypeId, uint instanceId = 0, bool asGroup = false,
            CancellationToken cancellationToken = default, ulong battleMasterGuid = 0)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Joining BG queue: BgType={BgType} Instance={Instance} AsGroup={AsGroup} GUID={Guid:X}",
                    bgTypeId, instanceId, asGroup, battleMasterGuid);

                // CMSG_BATTLEMASTER_JOIN: uint64 battleMasterGuid + uint32 bgTypeId + uint32 instanceId + uint8 joinAsGroup
                var payload = new byte[8 + 4 + 4 + 1];
                BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(0, 8), battleMasterGuid);
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), bgTypeId);
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), instanceId);
                payload[16] = (byte)(asGroup ? 1 : 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEMASTER_JOIN, payload, cancellationToken);

                _logger.LogInformation("BG queue join sent: BgType={BgType}", bgTypeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join BG queue: BgType={BgType}", bgTypeId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends CMSG_BATTLEFIELD_PORT with action=1 to accept a battleground invite.
        /// </summary>
        public async Task AcceptInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Accepting BG invite");

                // CMSG_BATTLEFIELD_PORT: uint8 action (1=accept)
                var payload = new byte[1];
                payload[0] = 1;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_PORT, payload, cancellationToken);

                _logger.LogInformation("BG invite accepted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept BG invite");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends CMSG_BATTLEFIELD_PORT with action=0 to decline a battleground invite.
        /// </summary>
        public async Task DeclineInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Declining BG invite");

                // CMSG_BATTLEFIELD_PORT: uint8 action (0=decline)
                var payload = new byte[1];
                payload[0] = 0;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_PORT, payload, cancellationToken);

                _logger.LogInformation("BG invite declined");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decline BG invite");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends CMSG_LEAVE_BATTLEFIELD to leave the current battleground.
        /// </summary>
        public async Task LeaveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Leaving battlefield");

                // CMSG_LEAVE_BATTLEFIELD: uint8 unk=0, uint8 unk=0
                var payload = new byte[2];

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LEAVE_BATTLEFIELD, payload, cancellationToken);

                _logger.LogInformation("Leave battlefield sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave battlefield");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends MSG_PVP_LOG_DATA as CMSG (empty payload) to request the scoreboard.
        /// </summary>
        public async Task RequestScoreboardAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting PvP scoreboard");

                await _worldClient.SendOpcodeAsync(Opcode.MSG_PVP_LOG_DATA, [], cancellationToken);

                _logger.LogInformation("PvP scoreboard request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request PvP scoreboard");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Parsing Helpers

        /// <summary>
        /// Parses SMSG_BATTLEFIELD_STATUS (0x2D4).
        /// Format: uint32 queueSlot + uint32 bgTypeId + uint32 unkn1 + uint32 unkn2 +
        ///         uint32 clientInstanceId + uint8 isRatedBg + uint32 statusId
        ///         If statusId == 2 (WaitJoin): + uint32 mapId + uint32 timeToAccept
        /// </summary>
        private BattlegroundStatus ParseBattlefieldStatus(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // Minimum: 4+4+4+4+4+1+4 = 25 bytes
                if (span.Length < 25)
                    return new BattlegroundStatus(0, 0, BattlegroundStatusId.None, null, null);

                int offset = 0;
                uint queueSlot = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint bgTypeId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                offset += 4; // unkn1
                offset += 4; // unkn2
                offset += 4; // clientInstanceId
                offset += 1; // isRatedBg
                uint statusId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                uint? mapId = null;
                uint? timeToAcceptMs = null;

                if ((BattlegroundStatusId)statusId == BattlegroundStatusId.WaitJoin && offset + 8 <= span.Length)
                {
                    mapId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    timeToAcceptMs = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
                }

                return new BattlegroundStatus(queueSlot, bgTypeId, (BattlegroundStatusId)statusId, mapId, timeToAcceptMs);
            }
            catch
            {
                return new BattlegroundStatus(0, 0, BattlegroundStatusId.None, null, null);
            }
        }

        /// <summary>
        /// Parses SMSG_BATTLEFIELD_LIST (0x23D).
        /// Format: uint64 battleMasterGuid + uint32 bgTypeId + uint32 count + count * uint32 instanceIds
        /// </summary>
        private BattlegroundList ParseBattlefieldList(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // Minimum: 8+4+4 = 16 bytes
                if (span.Length < 16)
                    return new BattlegroundList(0, 0, new List<uint>());

                int offset = 0;
                ulong battleMasterGuid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
                uint bgTypeId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                var instanceIds = new List<uint>((int)Math.Min(count, 100));
                for (uint i = 0; i < count && offset + 4 <= span.Length; i++)
                {
                    instanceIds.Add(BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)));
                    offset += 4;
                }

                return new BattlegroundList(battleMasterGuid, bgTypeId, instanceIds);
            }
            catch
            {
                return new BattlegroundList(0, 0, new List<uint>());
            }
        }

        /// <summary>
        /// Parses MSG_PVP_LOG_DATA (0x2E0) as SMSG.
        /// Format: uint8 isArena + uint8 isFinished + per-player records.
        /// Per-player (non-arena): uint64 playerGuid + uint32 killingBlows + uint32 honorableKills +
        ///                          uint32 deaths + uint32 bonusHonor + (bg-specific fields skipped)
        /// </summary>
        private PvPLogData ParsePvPLogData(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 2)
                    return new PvPLogData(false, new List<PvPPlayerScore>());

                int offset = 0;
                byte isArena = span[offset++];
                byte isFinished = span[offset++];

                // If finished, there may be winner data: uint8 winner (0=Horde, 1=Alliance)
                if (isFinished != 0 && offset < span.Length)
                    offset++; // skip winner byte

                // Player count: uint32
                if (offset + 4 > span.Length)
                    return new PvPLogData(isFinished != 0, new List<PvPPlayerScore>());

                uint playerCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                var scores = new List<PvPPlayerScore>((int)Math.Min(playerCount, 80));
                for (uint i = 0; i < playerCount && offset + 24 <= span.Length; i++)
                {
                    ulong playerGuid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
                    uint killingBlows = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    uint honorableKills = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    uint deaths = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    uint bonusHonor = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                    scores.Add(new PvPPlayerScore(playerGuid, killingBlows, honorableKills, deaths, bonusHonor));

                    // Skip BG-specific bonus fields: uint32 bgSpecificCount + bgSpecificCount * uint32
                    if (offset + 4 <= span.Length)
                    {
                        uint bgSpecificCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                        offset += (int)Math.Min(bgSpecificCount, 10) * 4; // skip bg-specific uint32 values
                    }
                }

                return new PvPLogData(isFinished != 0, scores);
            }
            catch
            {
                return new PvPLogData(false, new List<PvPPlayerScore>());
            }
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _statusChangedSub?.Dispose();

            _logger.LogDebug("Disposing BattlegroundNetworkClientComponent");
            base.Dispose();
        }

        #endregion
    }

    #region Data Models

    /// <summary>Parsed SMSG_BATTLEFIELD_STATUS data.</summary>
    public record BattlegroundStatus(uint QueueSlot, uint BgTypeId, BattlegroundStatusId Status, uint? MapId, uint? TimeToAcceptMs);

    /// <summary>Battleground status values from SMSG_BATTLEFIELD_STATUS.</summary>
    public enum BattlegroundStatusId : uint
    {
        None = 0,
        Queued = 1,
        WaitJoin = 2,
        InProgress = 3
    }

    /// <summary>Parsed SMSG_BATTLEFIELD_LIST data.</summary>
    public record BattlegroundList(ulong BattleMasterGuid, uint BgTypeId, List<uint> InstanceIds);

    /// <summary>Parsed MSG_PVP_LOG_DATA data.</summary>
    public record PvPLogData(bool IsFinished, List<PvPPlayerScore> Scores);

    /// <summary>Individual player score from MSG_PVP_LOG_DATA.</summary>
    public record PvPPlayerScore(ulong PlayerGuid, uint KillingBlows, uint HonorableKills, uint Deaths, uint BonusHonor);

    /// <summary>High-level battleground state derived from status updates.</summary>
    public enum BattlegroundState
    {
        None,
        Queued,
        Invited,
        InBattleground
    }

    #endregion
}
