using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Handles battleground queue, invite, and scoreboard operations over the Mangos protocol.
    /// Exposes opcode-backed reactive observables and maintains lightweight local state.
    /// </summary>
    public class BattlegroundNetworkClientComponent : NetworkClientComponent, IDisposable
    {
        private static readonly uint[] KnownBattlegroundMapIds = [30u, 489u, 529u];

        private readonly IWorldClient _worldClient;
        private readonly ILogger<BattlegroundNetworkClientComponent> _logger;
        private bool _disposed;

        // Reactive streams
        private readonly IObservable<BattlegroundStatus> _statusChanged;
        private readonly IObservable<uint?> _groupJoined;
        private readonly IObservable<ulong> _playerJoined;
        private readonly IObservable<ulong> _playerLeft;
        private readonly IObservable<ReadOnlyMemory<byte>> _instanceOwnershipUpdated;
        private readonly IObservable<BattlegroundList> _listReceived;
        private readonly IObservable<PvPLogData> _scoreboardReceived;

        // Self-subscriptions to keep .Do() side effects active
        private readonly IDisposable _statusChangedSub;
        private readonly IDisposable _groupJoinedSub;
        private readonly IDisposable _playerJoinedSub;
        private readonly IDisposable _playerLeftSub;
        private readonly IDisposable _instanceOwnershipUpdatedSub;
        private uint? _lastRequestedBgMapId;

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

            _groupJoined = SafeOpcodeStream(Opcode.SMSG_GROUP_JOINED_BATTLEGROUND)
                .Select(ParseGroupJoinedBattleground)
                .Do(groupJoinedMapId =>
                {
                    var mapId = groupJoinedMapId ?? _lastRequestedBgMapId;
                    if (mapId.HasValue)
                        CurrentBgTypeId = mapId.Value;

                    if (CurrentState == BattlegroundState.None)
                        CurrentState = BattlegroundState.Queued;

                    _logger.LogInformation(
                        "Group battleground queue acknowledged: MapId={MapId} State={State}",
                        mapId,
                        CurrentState);
                })
                .Publish().RefCount();

            _playerJoined = SafeOpcodeStream(Opcode.SMSG_BATTLEGROUND_PLAYER_JOINED)
                .Select(ParseBattlegroundPlayerGuid)
                .Where(playerGuid => playerGuid != 0)
                .Do(playerGuid =>
                {
                    _logger.LogDebug("Battleground player joined: PlayerGuid=0x{PlayerGuid:X}", playerGuid);
                })
                .Publish().RefCount();

            _playerLeft = SafeOpcodeStream(Opcode.SMSG_BATTLEGROUND_PLAYER_LEFT)
                .Select(ParseBattlegroundPlayerGuid)
                .Where(playerGuid => playerGuid != 0)
                .Do(playerGuid =>
                {
                    _logger.LogDebug("Battleground player left: PlayerGuid=0x{PlayerGuid:X}", playerGuid);
                })
                .Publish().RefCount();

            _instanceOwnershipUpdated = SafeOpcodeStream(Opcode.SMSG_UPDATE_INSTANCE_OWNERSHIP)
                .Do(_ =>
                {
                    _logger.LogDebug("Instance ownership updated");
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
            _groupJoinedSub = _groupJoined.Subscribe(_ => { });
            _playerJoinedSub = _playerJoined.Subscribe(_ => { });
            _playerLeftSub = _playerLeft.Subscribe(_ => { });
            _instanceOwnershipUpdatedSub = _instanceOwnershipUpdated.Subscribe(_ => { });
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

        /// <summary>Emits the GUID from SMSG_BATTLEGROUND_PLAYER_JOINED.</summary>
        public IObservable<ulong> BattlegroundPlayerJoined => _playerJoined;

        /// <summary>Emits the GUID from SMSG_BATTLEGROUND_PLAYER_LEFT.</summary>
        public IObservable<ulong> BattlegroundPlayerLeft => _playerLeft;

        /// <summary>Emits whenever MSG_PVP_LOG_DATA arrives as SMSG (scoreboard data).</summary>
        public IObservable<PvPLogData> ScoreboardReceived => _scoreboardReceived;

        #endregion

        #region Operations (CMSG)

        /// <summary>
        /// Sends CMSG_BATTLEMASTER_JOIN to queue for a battleground.
        /// </summary>
        /// <param name="bgMapId">Battleground type ID.</param>
        /// <param name="instanceId">Specific instance ID, or 0 for any.</param>
        /// <param name="asGroup">Whether to join as a group.</param>
        /// <param name="bgMapId">The BG's MAP ID (489=WSG, 529=AB, 30=AV) — NOT the BG type ID.
        /// VMaNGOS reads this field as mapId and converts to BG type internally via GetBattleGroundTypeIdByMapId.</param>
        /// <param name="battleMasterGuid">GUID of the battlemaster NPC. Required by VMaNGOS anticheat.</param>
        public async Task JoinQueueAsync(uint bgMapId, uint instanceId = 0, bool asGroup = false,
            CancellationToken cancellationToken = default, ulong battleMasterGuid = 0)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Joining BG queue: MapId={MapId} Instance={Instance} AsGroup={AsGroup} GUID={Guid:X}",
                    bgMapId, instanceId, asGroup, battleMasterGuid);
                _lastRequestedBgMapId = bgMapId;

                // CMSG_BATTLEMASTER_JOIN: uint64 guid + uint32 mapId + uint32 instanceId + uint8 joinAsGroup
                // IMPORTANT: The second field is the BG MAP ID (489, 529, 30), NOT the BG type enum (2, 3, 1).
                // VMaNGOS BattleGroundHandler.cpp:106 reads this as mapId and converts via GetBattleGroundTypeIdByMapId.
                var payload = new byte[8 + 4 + 4 + 1];
                BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(0, 8), battleMasterGuid);
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), bgMapId);
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), instanceId);
                payload[16] = (byte)(asGroup ? 1 : 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEMASTER_JOIN, payload, cancellationToken);

                _logger.LogInformation("BG queue join sent: BgType={BgType}", bgMapId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join BG queue: BgType={BgType}", bgMapId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends CMSG_BATTLEFIELD_PORT with action=1 to accept a battleground invite.
        /// VMaNGOS 1.12.1 format (BattleGroundHandler.cpp:383):
        ///   uint32 mapId + uint8 action (1=accept, 0=leave)
        /// </summary>
        public async Task AcceptInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);

                // Use the mapId from the last SMSG_BATTLEFIELD_STATUS
                var mapId = CurrentBgTypeId ?? 489u; // fallback to WSG
                _logger.LogDebug("Accepting BG invite: mapId={MapId}", mapId);

                // CMSG_BATTLEFIELD_PORT: uint32 mapId + uint8 action (1=accept, 0=leave)
                var payload = BuildBattlefieldPortPayload(mapId, action: 1);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_PORT, payload, cancellationToken);

                _logger.LogInformation("BG invite accepted (mapId={MapId})", mapId);
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
                var mapId = CurrentBgTypeId ?? 489u; // fallback to WSG
                _logger.LogDebug("Declining BG invite: mapId={MapId}", mapId);

                // CMSG_BATTLEFIELD_PORT: uint32 mapId + uint8 action (1=accept, 0=leave)
                var payload = BuildBattlefieldPortPayload(mapId, action: 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_PORT, payload, cancellationToken);

                _logger.LogInformation("BG invite declined (mapId={MapId})", mapId);
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
                if (CurrentState is BattlegroundState.Queued or BattlegroundState.Invited)
                {
                    await ClearQueueAsync(CurrentBgTypeId ?? 489u, CurrentState, cancellationToken);
                    return;
                }

                if (CurrentState == BattlegroundState.InBattleground)
                {
                    await SendLeaveBattlefieldAsync(cancellationToken);
                    return;
                }

                // After relog / port-out, stale battleground queues can exist before we have received
                // an SMSG_BATTLEFIELD_STATUS update. Ask the server first, then do a conservative
                // queue-clear sweep across the known vanilla battleground maps as a fallback.
                await RequestStatusAsync(cancellationToken);
                await Task.Delay(150, cancellationToken);

                if (CurrentState is BattlegroundState.Queued or BattlegroundState.Invited)
                {
                    await ClearQueueAsync(CurrentBgTypeId ?? 489u, CurrentState, cancellationToken);
                    return;
                }

                if (CurrentState == BattlegroundState.InBattleground)
                {
                    await SendLeaveBattlefieldAsync(cancellationToken);
                    return;
                }

                _logger.LogDebug("Battleground state unknown; sending queue-clear sweep before leave fallback");
                foreach (var mapId in KnownBattlegroundMapIds)
                {
                    var queuePayload = BuildBattlefieldPortPayload(mapId, action: 0);
                    await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_PORT, queuePayload, cancellationToken);
                }

                _logger.LogInformation(
                    "Battleground queue clear sweep sent for maps: {MapIds}",
                    string.Join(", ", KnownBattlegroundMapIds));

                await SendLeaveBattlefieldAsync(cancellationToken);
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

        private static byte[] BuildBattlefieldPortPayload(uint mapId, byte action)
        {
            var payload = new byte[5];
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), mapId);
            payload[4] = action;
            return payload;
        }

        private async Task RequestStatusAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Requesting battleground status refresh");
            await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_STATUS, [], cancellationToken);
        }

        private async Task ClearQueueAsync(uint mapId, BattlegroundState state, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Clearing battleground queue: mapId={MapId} state={State}", mapId, state);

            var queuePayload = BuildBattlefieldPortPayload(mapId, action: 0);
            await _worldClient.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_PORT, queuePayload, cancellationToken);

            _logger.LogInformation("Battleground queue cleared (mapId={MapId}, state={State})", mapId, state);
        }

        private async Task SendLeaveBattlefieldAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Leaving battlefield");

            // CMSG_LEAVE_BATTLEFIELD: uint8 unk=0, uint8 unk=0
            var payload = new byte[2];

            await _worldClient.SendOpcodeAsync(Opcode.CMSG_LEAVE_BATTLEFIELD, payload, cancellationToken);

            _logger.LogInformation("Leave battlefield sent");
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
        /// VMaNGOS 1.12.1 format (BattleGroundMgr.cpp:BuildBattleGroundStatusPacket):
        ///
        /// statusId == 0 (None):
        ///   uint32 queueSlot + uint32 0
        ///
        /// statusId != 0:
        ///   uint32 queueSlot + uint32 bgMapId + uint8 bracketId + uint32 clientInstanceId + uint32 statusId
        ///   + variable fields depending on statusId:
        ///     STATUS_WAIT_QUEUE (1): uint32 avgWaitTime + uint32 timeInQueue
        ///     STATUS_WAIT_JOIN  (2): uint32 timeToRemove
        ///     STATUS_IN_PROGRESS(3): uint32 timeToAutoLeave + uint32 elapsedTime
        /// </summary>
        private BattlegroundStatus ParseBattlefieldStatus(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;

                // statusId == 0 (None) packet is only 8 bytes: uint32 queueSlot + uint32 0
                if (span.Length < 8)
                    return new BattlegroundStatus(0, 0, BattlegroundStatusId.None, null, null);

                int offset = 0;
                uint queueSlot = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                // Check if this is a "None" status (short packet)
                if (span.Length <= 8)
                {
                    // uint32 queueSlot + uint32 0 — no BG info
                    return new BattlegroundStatus(queueSlot, 0, BattlegroundStatusId.None, null, null);
                }

                // Full packet: queueSlot(4) + mapId(4) + bracketId(1) + instanceId(4) + statusId(4) = 17 bytes min
                if (span.Length < 17)
                    return new BattlegroundStatus(queueSlot, 0, BattlegroundStatusId.None, null, null);

                uint bgMapId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                byte bracketId = span[offset]; offset += 1;  // uint8, NOT uint32
                uint clientInstanceId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint statusId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                uint? mapId = null;
                uint? timeToAcceptMs = null;

                switch ((BattlegroundStatusId)statusId)
                {
                    case BattlegroundStatusId.Queued: // STATUS_WAIT_QUEUE
                        // uint32 avgWaitTime + uint32 timeInQueue
                        break;
                    case BattlegroundStatusId.WaitJoin: // STATUS_WAIT_JOIN
                        // uint32 timeToRemove (time to accept invite)
                        if (offset + 4 <= span.Length)
                        {
                            timeToAcceptMs = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
                            mapId = bgMapId; // BG map is the bgMapId field
                        }
                        break;
                    case BattlegroundStatusId.InProgress: // STATUS_IN_PROGRESS
                        // uint32 timeToAutoLeave + uint32 elapsedTime
                        break;
                }

                return new BattlegroundStatus(queueSlot, bgMapId, (BattlegroundStatusId)statusId, mapId, timeToAcceptMs);
            }
            catch (Exception ex)
            {
                return new BattlegroundStatus(0, 0, BattlegroundStatusId.None, null, null);
            }
        }

        /// <summary>
        /// Parses SMSG_BATTLEFIELD_LIST (0x23D).
        /// Format: uint64 battleMasterGuid + uint32 bgMapId + uint32 count + count * uint32 instanceIds
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
                uint bgMapId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                var instanceIds = new List<uint>((int)Math.Min(count, 100));
                for (uint i = 0; i < count && offset + 4 <= span.Length; i++)
                {
                    instanceIds.Add(BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)));
                    offset += 4;
                }

                return new BattlegroundList(battleMasterGuid, bgMapId, instanceIds);
            }
            catch
            {
                return new BattlegroundList(0, 0, new List<uint>());
            }
        }

        /// <summary>
        /// Parses SMSG_GROUP_JOINED_BATTLEGROUND (0x2E8).
        /// The packet is used as an acknowledgment that a grouped battleground join succeeded.
        /// Current VMaNGOS runs may omit map data here, so we fall back to the last join request.
        /// </summary>
        private static uint? ParseGroupJoinedBattleground(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 4)
                return null;

            var candidate = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
            return IsKnownBattlegroundMapId(candidate) ? candidate : null;
        }

        /// <summary>
        /// Parses SMSG_BATTLEGROUND_PLAYER_JOINED / SMSG_BATTLEGROUND_PLAYER_LEFT.
        /// Vanilla uses a packed GUID payload for the player whose roster state changed.
        /// </summary>
        private static ulong ParseBattlegroundPlayerGuid(ReadOnlyMemory<byte> payload)
        {
            if (payload.IsEmpty)
                return 0;

            try
            {
                using var stream = new MemoryStream(payload.ToArray(), writable: false);
                using var reader = new BinaryReader(stream);
                return ReaderUtils.ReadPackedGuid(reader);
            }
            catch
            {
                return 0;
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

        private static bool IsKnownBattlegroundMapId(uint mapId)
        {
            foreach (var knownMapId in KnownBattlegroundMapIds)
            {
                if (knownMapId == mapId)
                    return true;
            }

            return false;
        }

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _statusChangedSub?.Dispose();
            _groupJoinedSub?.Dispose();
            _playerJoinedSub?.Dispose();
            _playerLeftSub?.Dispose();
            _instanceOwnershipUpdatedSub?.Dispose();

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
