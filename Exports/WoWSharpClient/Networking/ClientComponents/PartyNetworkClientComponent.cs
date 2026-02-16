using System.Buffers.Binary;
using System.Text;
using System.Reactive;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

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

        // Self-subscriptions that keep state-tracking .Do() side effects active
        // even when no external code subscribes to the public observables.
        private readonly IDisposable _partyInviteSub;
        private readonly IDisposable _groupUpdateSub;
        private readonly IDisposable _groupLeaveSub;
        private readonly IDisposable _leaderChangeSub;
        private readonly IDisposable _commandResultSub;

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

            // Self-subscribe so that the .Do() side effects (HasPendingInvite, IsInGroup, etc.)
            // always fire when packets arrive, even if no external code subscribes.
            _partyInviteSub = _partyInvites.Subscribe(_ => { });
            _groupUpdateSub = _groupUpdates.Subscribe(_ => { });
            _groupLeaveSub = _groupLeaves.Subscribe(_ => { });
            _leaderChangeSub = _leadershipChanges.Subscribe(_ => { });
            _commandResultSub = _partyCommandResults.Subscribe(_ => { });
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

                // 1.12.1 CMSG_GROUP_SET_LEADER uses ObjectGuid(8), not name
                var member = GetGroupMember(playerName);
                if (member == null)
                {
                    throw new ArgumentException($"Player '{playerName}' is not in the group");
                }

                await PromoteToLeaderAsync(member.Guid, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to promote player to leader: {PlayerName}", playerName);
                throw;
            }
        }

        public async Task PromoteToLeaderAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsGroupLeader)
                {
                    throw new InvalidOperationException("Only the group leader can promote other players");
                }

                _logger.LogDebug("Promoting player to leader by GUID: {PlayerGuid:X}", playerGuid);

                // 1.12.1 CMSG_GROUP_SET_LEADER: ObjectGuid(8)
                var payload = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(payload, playerGuid);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GROUP_SET_LEADER, payload, cancellationToken);

                _logger.LogInformation("Leadership promotion sent for player GUID: {PlayerGuid:X}", playerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to promote player to leader by GUID: {PlayerGuid:X}", playerGuid);
                throw;
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

                // 1.12.1 CMSG_GROUP_ASSISTANT_LEADER: ObjectGuid(8) + flag(1)
                var member = GetGroupMember(playerName);
                if (member == null)
                {
                    throw new ArgumentException($"Player '{playerName}' is not in the group");
                }

                _logger.LogDebug("Promoting player to assistant: {PlayerName}", playerName);

                var payload = new byte[9]; // guid(8) + flag(1)
                BinaryPrimitives.WriteUInt64LittleEndian(payload, member.Guid);
                payload[8] = 1; // 1 = promote to assistant

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

                // 1.12.1 CMSG_LOOT_METHOD: uint32(4) + ObjectGuid(8) + uint32(4) = 16 bytes
                var payload = new byte[16];
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), (uint)lootMethod);
                BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(4, 8), lootMasterGuid ?? 0UL);
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), (uint)lootThreshold);

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
        public async Task RequestPartyMemberStatsAsync(ulong memberGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting party member stats for {MemberGuid:X}", memberGuid);

                // 1.12.1 CMSG_REQUEST_PARTY_MEMBER_STATS: ObjectGuid(8)
                var payload = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(payload, memberGuid);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REQUEST_PARTY_MEMBER_STATS, payload, cancellationToken);

                _logger.LogInformation("Party member stats request sent for {MemberGuid:X}", memberGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request party member stats for {MemberGuid:X}", memberGuid);
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

        /// <summary>
        /// SMSG_GROUP_INVITE: CString inviterName
        /// </summary>
        private string ParseGroupInvite(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length == 0) return "Unknown";
            int nullIdx = span.IndexOf((byte)0);
            return nullIdx > 0
                ? Encoding.UTF8.GetString(span[..nullIdx])
                : Encoding.UTF8.GetString(span);
        }

        /// <summary>
        /// SMSG_GROUP_LIST (1.12.1 cmangos-classic format):
        /// groupType(1) + ownSubGroup(1) + ownFlags(1) + memberCount(4)
        /// + [CString name + guid(8) + online(1) + flags(1)] * memberCount
        /// + leaderGuid(8)
        /// + [if memberCount > 0: lootMethod(1) + looterGuid(8) + lootThreshold(1)]
        /// </summary>
        private (bool IsRaid, uint MemberCount) ParseGroupList(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // Header: groupType(1) + subGroup(1) + flags(1) + count(4) = 7 bytes
                if (span.Length < 7) return (IsInRaid, GroupSize);

                byte groupType = span[0];
                // span[1] = ownSubGroup, span[2] = ownFlags (unused here)
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(3, 4));

                var members = new List<GroupMember>((int)count);
                int offset = 7;

                for (uint i = 0; i < count && offset < span.Length; i++)
                {
                    // CString name (null-terminated)
                    int nameEnd = span[offset..].IndexOf((byte)0);
                    if (nameEnd < 0) break;
                    string name = Encoding.UTF8.GetString(span.Slice(offset, nameEnd));
                    offset += nameEnd + 1;

                    // guid(8) + online(1) + memberFlags(1) = 10 bytes
                    if (offset + 10 > span.Length) break;
                    ulong guid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
                    offset += 8;
                    byte online = span[offset++];
                    byte memberFlags = span[offset++];

                    members.Add(new GroupMember
                    {
                        Guid = guid,
                        Name = name,
                        IsOnline = online != 0,
                        SubGroup = memberFlags,
                    });
                }

                // leaderGuid(8)
                ulong leaderGuid = 0;
                if (offset + 8 <= span.Length)
                {
                    leaderGuid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
                    offset += 8;
                }

                // Mark leader; if no member matches leaderGuid, self is the leader
                bool selfIsLeader = true;
                foreach (var m in members)
                {
                    m.IsLeader = m.Guid == leaderGuid;
                    if (m.IsLeader) selfIsLeader = false;
                }

                // Loot settings (only present when count > 0)
                LootMethod parsedLootMethod = LootMethod.FreeForAll;
                if (count > 0 && offset + 10 <= span.Length)
                {
                    parsedLootMethod = (LootMethod)span[offset++];
                    offset += 8; // looterGuid
                    offset += 1; // lootThreshold
                }

                lock (_groupLock)
                {
                    _groupMembers.Clear();
                    _groupMembers.AddRange(members);
                }

                IsGroupLeader = selfIsLeader && count > 0;
                CurrentLootMethod = parsedLootMethod;

                return (groupType == 1, count);
            }
            catch
            {
                return (IsInRaid, GroupSize);
            }
        }

        /// <summary>
        /// SMSG_GROUP_SET_LEADER: CString newLeaderName (no GUID in packet).
        /// GUID is looked up from local group member cache.
        /// </summary>
        private (string NewLeaderName, ulong NewLeaderGuid) ParseGroupSetLeader(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length == 0) return ("Unknown", 0UL);

            int nullIdx = span.IndexOf((byte)0);
            string name = nullIdx > 0
                ? Encoding.UTF8.GetString(span[..nullIdx])
                : Encoding.UTF8.GetString(span);

            // Look up GUID from group members
            ulong guid = 0;
            lock (_groupLock)
            {
                var member = _groupMembers.FirstOrDefault(
                    m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (member != null) guid = member.Guid;
            }
            return (name, guid);
        }

        /// <summary>
        /// SMSG_PARTY_COMMAND_RESULT: operation(uint32) + memberName(CString) + result(uint32).
        /// The CString sits between the two uint32 fields.
        /// </summary>
        private (string Operation, bool Success, uint ResultCode) ParsePartyCommandResult(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // Minimum: uint32(4) + null(1) + uint32(4) = 9 bytes
                if (span.Length < 9) return ("Unknown", false, 0xFFFFFFFF);

                uint operation = BinaryPrimitives.ReadUInt32LittleEndian(span[..4]);

                // Skip CString member name after offset 4
                int nullIdx = span[4..].IndexOf((byte)0);
                if (nullIdx < 0) return ("Unknown", false, 0xFFFFFFFF);
                int resultOffset = 4 + nullIdx + 1;

                if (resultOffset + 4 > span.Length) return ("Unknown", false, 0xFFFFFFFF);

                uint result = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(resultOffset, 4));
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

            _partyInviteSub?.Dispose();
            _groupUpdateSub?.Dispose();
            _groupLeaveSub?.Dispose();
            _leaderChangeSub?.Dispose();
            _commandResultSub?.Dispose();

            _logger.LogDebug("Disposing PartyNetworkClientComponent");
        }
        #endregion
    }
}