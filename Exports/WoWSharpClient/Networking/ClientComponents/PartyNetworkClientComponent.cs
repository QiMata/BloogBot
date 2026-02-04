using System.Text;
using System.Reactive;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Reactive party/raid management over the Mangos protocol.
    /// Exposes opcode-backed observables and maintains lightweight local state.
    /// </summary>
    public class PartyNetworkClientComponent : NetworkClientComponent, IPartyNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<PartyNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private readonly List<GroupMember> _groupMembers = [];
        private readonly object _groupLock = new object();
        private bool _disposed;

        // Reactive streams
        private readonly IObservable<string> _partyInvites;
        private readonly IObservable<(bool IsRaid, uint MemberCount)> _groupUpdates;
        private readonly IObservable<string> _groupLeaves;
        private readonly IObservable<(string NewLeaderName, ulong NewLeaderGuid)> _leadershipChanges;
        private readonly IObservable<(string Operation, bool Success, uint ResultCode)> _partyCommandResults;
        private readonly IObservable<(string Operation, string Error)> _partyErrors;
        private readonly IObservable<Unit> _readyCheckRequests;
        private readonly IObservable<Unit> _readyCheckResponses;

        public PartyNetworkClientComponent(IWorldClient worldClient, ILogger<PartyNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _partyInvites = SafeOpcodeStream(Opcode.SMSG_GROUP_INVITE)
                .Select(ParseGroupInvite)
                .Do(inviter =>
                {
                    _logger.LogInformation("Group invite received from {Inviter}", inviter);
                    HasPendingInvite = true;
                })
                .Publish().RefCount();

            _groupUpdates = SafeOpcodeStream(Opcode.SMSG_GROUP_LIST)
                .Select(ParseGroupList)
                .Do(info =>
                {
                    lock (_groupLock)
                    {
                        IsInRaid = info.IsRaid;
                        GroupSize = info.MemberCount;
                        IsInGroup = info.MemberCount > 0;
                    }
                    _logger.LogInformation("Group list updated - Type: {Type}, Members: {Count}",
                        info.IsRaid ? "Raid" : "Party", info.MemberCount);
                })
                .Publish().RefCount();

            _groupLeaves = SafeOpcodeStream(Opcode.SMSG_GROUP_DESTROYED)
                .Select(_ => "Group disbanded")
                .Do(reason =>
                {
                    lock (_groupLock)
                    {
                        _groupMembers.Clear();
                    }
                    lock (_stateLock)
                    {
                        IsInGroup = false;
                        IsInRaid = false;
                        IsGroupLeader = false;
                        GroupSize = 0;
                    }
                    _logger.LogInformation("{Reason}", reason);
                })
                .Publish().RefCount();

            _leadershipChanges = SafeOpcodeStream(Opcode.SMSG_GROUP_SET_LEADER)
                .Select(ParseGroupSetLeader)
                .Do(change => _logger.LogInformation("Group leadership changed to: {Leader} ({LeaderGuid:X})", change.NewLeaderName, change.NewLeaderGuid))
                .Publish().RefCount();

            _partyCommandResults = SafeOpcodeStream(Opcode.SMSG_PARTY_COMMAND_RESULT)
                .Select(ParsePartyCommandResult)
                .Do(r =>
                {
                    if (r.Success) _logger.LogInformation("Party operation {Operation} succeeded", r.Operation);
                    else _logger.LogWarning("Party operation {Operation} failed with result: {Code}", r.Operation, r.ResultCode);
                })
                .Publish().RefCount();

            var raidGroupOnlyErrors = SafeOpcodeStream(Opcode.SMSG_RAID_GROUP_ONLY)
                .Select(_ => (Operation: "InstanceEntry", Error: "This instance requires a raid group"))
                .Do(_ => _logger.LogInformation("Instance requires raid group"));

            _partyErrors = raidGroupOnlyErrors.Publish().RefCount();

            _readyCheckRequests = SafeOpcodeStream(Opcode.MSG_RAID_READY_CHECK)
                .Select(_ => Unit.Default)
                .Do(_ => _logger.LogInformation("Ready check initiated"))
                .Publish().RefCount();

            _readyCheckResponses = SafeOpcodeStream(Opcode.MSG_RAID_READY_CHECK_CONFIRM)
                .Select(_ => Unit.Default)
                .Do(_ => _logger.LogDebug("Ready check response received"))
                .Publish().RefCount();
        }

        #region INetworkClientComponent Implementation
        // IsOperationInProgress and LastOperationTime provided by base class
        #endregion

        #region State
        public bool IsInGroup { get; private set; }
        public bool IsInRaid { get; private set; }
        public bool IsGroupLeader { get; private set; }
        public uint GroupSize { get; private set; }
        public LootMethod CurrentLootMethod { get; private set; }
        public bool HasPendingInvite { get; private set; }
        #endregion

        #region Reactive (public)
        public IObservable<string> PartyInvites => _partyInvites;
        public IObservable<(bool IsRaid, uint MemberCount)> GroupUpdates => _groupUpdates;
        public IObservable<string> GroupLeaves => _groupLeaves;
        public IObservable<(string NewLeaderName, ulong NewLeaderGuid)> LeadershipChanges => _leadershipChanges;
        public IObservable<(string Operation, bool Success, uint ResultCode)> PartyCommandResults => _partyCommandResults;
        public IObservable<(string Operation, string Error)> PartyErrors => _partyErrors;
        public IObservable<Unit> ReadyCheckRequests => _readyCheckRequests;
        public IObservable<Unit> ReadyCheckResponses => _readyCheckResponses;
        #endregion

        #region Party Invite Operations
        public async Task InvitePlayerAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                _logger.LogDebug("Inviting player to group: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_INVITE, payload, cancellationToken);

                _logger.LogInformation("Group invite sent to player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invite player to group: {PlayerName}", playerName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task AcceptInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Accepting group invite");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_ACCEPT, [], cancellationToken);

                _logger.LogInformation("Group invite accepted");
                HasPendingInvite = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept group invite");
                throw;
            }
        }

        public async Task DeclineInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Declining group invite");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_DECLINE, [], cancellationToken);

                _logger.LogInformation("Group invite declined");
                HasPendingInvite = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decline group invite");
                throw;
            }
        }

        public async Task CancelInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Canceling group invite");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_CANCEL, [], cancellationToken);

                _logger.LogInformation("Group invite canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel group invite");
                throw;
            }
        }
        #endregion

        #region Member Management
        public async Task KickPlayerAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                _logger.LogDebug("Kicking player from group: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_UNINVITE, payload, cancellationToken);

                _logger.LogInformation("Kick request sent for player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kick player from group: {PlayerName}", playerName);
                throw;
            }
        }

        public async Task KickPlayerAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Kicking player from group by GUID: {PlayerGuid:X}", playerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(playerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_UNINVITE_GUID, payload, cancellationToken);

                _logger.LogInformation("Kick request sent for player GUID: {PlayerGuid:X}", playerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kick player from group by GUID: {PlayerGuid:X}", playerGuid);
                throw;
            }
        }

        public async Task LeaveGroupAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Leaving group");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_DISBAND, [], cancellationToken);

                _logger.LogInformation("Group leave request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave group");
                throw;
            }
        }

        public async Task DisbandGroupAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can disband the group");
                }

                _logger.LogDebug("Disbanding group");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_DISBAND, [], cancellationToken);

                _logger.LogInformation("Group disband request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disband group");
                throw;
            }
        }
        #endregion

        #region Leadership Operations
        public async Task PromoteToLeaderAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can promote other players");
                }

                _logger.LogDebug("Promoting player to leader: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_SET_LEADER, payload, cancellationToken);

                _logger.LogInformation("Leadership promotion sent for player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to promote player to leader: {PlayerName}", playerName);
                throw;
            }
        }

        public async Task PromoteToLeaderAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            var member = GetGroupMember(playerGuid);
            if (member != null)
            {
                await PromoteToLeaderAsync(member.Name, cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Player with GUID {playerGuid:X} is not in the group");
            }
        }

        public async Task PromoteToAssistantAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                if (!IsInRaid)
                {
                    throw new InvalidOperationException("Assistant leaders are only available in raids");
                }

                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can promote assistants");
                }

                _logger.LogDebug("Promoting player to assistant: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_ASSISTANT_LEADER, payload, cancellationToken);

                _logger.LogInformation("Assistant promotion sent for player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to promote player to assistant: {PlayerName}", playerName);
                throw;
            }
        }
        #endregion

        #region Loot Settings
        public async Task SetLootMethodAsync(LootMethod lootMethod, ulong? lootMasterGuid = null, ItemQuality lootThreshold = ItemQuality.Uncommon, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can change loot settings");
                }

                _logger.LogDebug("Setting loot method: {LootMethod}, Threshold: {LootThreshold}", lootMethod, lootThreshold);

                var payload = new byte[17]; // 1 + 8 + 8 bytes
                payload[0] = (byte)lootMethod;

                if (lootMasterGuid.HasValue)
                {
                    BitConverter.GetBytes(lootMasterGuid.Value).CopyTo(payload, 1);
                }

                BitConverter.GetBytes((ulong)lootThreshold).CopyTo(payload, 9);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_METHOD, payload, cancellationToken);

                _logger.LogInformation("Loot method changed to: {LootMethod}", lootMethod);
                CurrentLootMethod = lootMethod;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set loot method: {LootMethod}", lootMethod);
                throw;
            }
        }
        #endregion

        #region Raid Operations
        public async Task ConvertToRaidAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can convert to raid");
                }

                if (IsInRaid)
                {
                    throw new InvalidOperationException("Group is already a raid");
                }

                _logger.LogDebug("Converting party to raid");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_RAID_CONVERT, [], cancellationToken);

                _logger.LogInformation("Raid conversion request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert to raid");
                throw;
            }
        }

        public async Task ChangeSubGroupAsync(string playerName, byte subGroup, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                if (!IsInRaid)
                {
                    throw new InvalidOperationException("Subgroups are only available in raids");
                }

                if (subGroup > 7)
                {
                    throw new ArgumentOutOfRangeException(nameof(subGroup), "Subgroup must be between 0 and 7");
                }

                _logger.LogDebug("Changing player subgroup: {PlayerName} to subgroup {SubGroup}", playerName, subGroup);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 2]; // name + null terminator + subgroup
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator
                payload[nameBytes.Length + 1] = subGroup;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_CHANGE_SUB_GROUP, payload, cancellationToken);

                _logger.LogInformation("Subgroup change request sent for player: {PlayerName} to subgroup {SubGroup}", playerName, subGroup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change player subgroup: {PlayerName} to {SubGroup}", playerName, subGroup);
                throw;
            }
        }

        public async Task SwapSubGroupsAsync(string playerName1, string playerName2, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName1);
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName2);

                if (!IsInRaid)
                {
                    throw new InvalidOperationException("Subgroups are only available in raids");
                }

                _logger.LogDebug("Swapping subgroups: {Player1} <-> {Player2}", playerName1, playerName2);

                var name1Bytes = Encoding.UTF8.GetBytes(playerName1);
                var name2Bytes = Encoding.UTF8.GetBytes(playerName2);
                var payload = new byte[name1Bytes.Length + 1 + name2Bytes.Length + 1];

                var offset = 0;
                Array.Copy(name1Bytes, 0, payload, offset, name1Bytes.Length);
                offset += name1Bytes.Length;
                payload[offset++] = 0; // Null terminator

                Array.Copy(name2Bytes, 0, payload, offset, name2Bytes.Length);
                offset += name2Bytes.Length;
                payload[offset] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_SWAP_SUB_GROUP, payload, cancellationToken);

                _logger.LogInformation("Subgroup swap request sent: {Player1} <-> {Player2}", playerName1, playerName2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap subgroups: {Player1} <-> {Player2}", playerName1, playerName2);
                throw;
            }
        }
        #endregion

        #region Information Requests
        public async Task RequestPartyMemberStatsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting party member stats");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REQUEST_PARTY_MEMBER_STATS, [], cancellationToken);

                _logger.LogInformation("Party member stats request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request party member stats");
                throw;
            }
        }

        public async Task InitiateReadyCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can initiate ready checks");
                }

                _logger.LogDebug("Initiating ready check");

                await _worldClient.SendOpcodeAsync(Opcode.MSG_RAID_READY_CHECK, [], cancellationToken);

                _logger.LogInformation("Ready check initiated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate ready check");
                throw;
            }
        }

        public async Task RespondToReadyCheckAsync(bool isReady, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Responding to ready check: {IsReady}", isReady);

                var payload = new byte[1];
                payload[0] = (byte)(isReady ? 1 : 0);

                await _worldClient.SendOpcodeAsync(Opcode.MSG_RAID_READY_CHECK_CONFIRM, payload, cancellationToken);

                _logger.LogInformation("Ready check response sent: {IsReady}", isReady);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to respond to ready check: {IsReady}", isReady);
                throw;
            }
        }
        #endregion

        #region Utility Methods
        public IReadOnlyList<GroupMember> GetGroupMembers()
        {
            lock (_groupLock)
            {
                return _groupMembers.ToList().AsReadOnly();
            }
        }

        public bool IsPlayerInGroup(string playerName)
        {
            lock (_groupLock)
            {
                return _groupMembers.Any(m => m.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool IsPlayerInGroup(ulong playerGuid)
        {
            lock (_groupLock)
            {
                return _groupMembers.Any(m => m.Guid == playerGuid);
            }
        }

        public GroupMember? GetGroupMember(string playerName)
        {
            lock (_groupLock)
            {
                return _groupMembers.FirstOrDefault(m => m.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public GroupMember? GetGroupMember(ulong playerGuid)
        {
            lock (_groupLock)
            {
                return _groupMembers.FirstOrDefault(m => m.Guid == playerGuid);
            }
        }
        #endregion

        #region Parsing Helpers
        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private string ParseGroupInvite(ReadOnlyMemory<byte> payload)
        {
            // Placeholder: actual parsing TBD
            return "Unknown";
        }

        private (bool IsRaid, uint MemberCount) ParseGroupList(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 5) return (IsInRaid, GroupSize);
                var groupType = span[0];
                var count = BitConverter.ToUInt32(span.Slice(1, 4));
                return (groupType == 1, count);
            }
            catch
            {
                return (IsInRaid, GroupSize);
            }
        }

        private (string NewLeaderName, ulong NewLeaderGuid) ParseGroupSetLeader(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            ulong guid = span.Length >= 8 ? BitConverter.ToUInt64(span[..8]) : 0UL;
            var name = "Unknown";
            return (name, guid);
        }

        private (string Operation, bool Success, uint ResultCode) ParsePartyCommandResult(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 8) return ("Unknown", false, 0xFFFFFFFF);
                var operation = BitConverter.ToUInt32(span[..4]);
                var result = BitConverter.ToUInt32(span.Slice(4, 4));
                var operationName = operation switch
                {
                    0x00 => "Invite",
                    0x01 => "Leave",
                    0x02 => "SetLeader",
                    0x03 => "LootMethod",
                    0x04 => "ChangeSubGroup",
                    _ => $"Unknown({operation:X})"
                };
                return (operationName, result == 0, result);
            }
            catch
            {
                return ("Unknown", false, 0xFFFFFFFF);
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("Disposing PartyNetworkClientComponent");
        }
        #endregion
    }
}