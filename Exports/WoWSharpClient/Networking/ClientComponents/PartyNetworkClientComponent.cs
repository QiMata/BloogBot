using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of party network agent that handles party/raid group management operations in World of Warcraft.
    /// Manages party invites, member management, loot settings, raid conversion, and leadership operations using the Mangos protocol.
    /// </summary>
    public class PartyNetworkClientComponent : IPartyNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<PartyNetworkClientComponent> _logger;

        private readonly List<GroupMember> _groupMembers = [];
        private readonly object _groupLock = new object();

        /// <summary>
        /// Initializes a new instance of the PartyNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public PartyNetworkClientComponent(IWorldClient worldClient, ILogger<PartyNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsInGroup { get; private set; }

        /// <inheritdoc />
        public bool IsInRaid { get; private set; }

        /// <inheritdoc />
        public bool IsGroupLeader { get; private set; }

        /// <inheritdoc />
        public uint GroupSize { get; private set; }

        /// <inheritdoc />
        public LootMethod CurrentLootMethod { get; private set; }

        /// <inheritdoc />
        public bool HasPendingInvite { get; private set; }

        /// <inheritdoc />
        public event Action<string>? PartyInviteReceived;

        /// <inheritdoc />
        public event Action<bool, uint>? GroupJoined;

        /// <inheritdoc />
        public event Action<string>? GroupLeft;

        /// <inheritdoc />
        public event Action<string, ulong>? MemberJoined;

        /// <inheritdoc />
        public event Action<string, ulong>? MemberLeft;

        /// <inheritdoc />
        public event Action<string, ulong>? LeadershipChanged;

        /// <inheritdoc />
        public event Action<LootMethod, ulong?>? LootMethodChanged;

        /// <inheritdoc />
        public event Action<bool>? GroupConverted;

        /// <inheritdoc />
        public event Action<string, string>? PartyOperationFailed;

        #region Party Invite Operations

        /// <inheritdoc />
        public async Task InvitePlayerAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
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
                PartyOperationFailed?.Invoke("InvitePlayer", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("AcceptInvite", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("DeclineInvite", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("CancelInvite", ex.Message);
                throw;
            }
        }

        #endregion

        #region Member Management

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("KickPlayer", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("KickPlayerByGuid", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("LeaveGroup", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("DisbandGroup", ex.Message);
                throw;
            }
        }

        #endregion

        #region Leadership Operations

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("PromoteToLeader", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("PromoteToAssistant", ex.Message);
                throw;
            }
        }

        #endregion

        #region Loot Settings

        /// <inheritdoc />
        public async Task SetLootMethodAsync(LootMethod lootMethod, ulong? lootMasterGuid = null, LootQuality lootThreshold = LootQuality.Uncommon, CancellationToken cancellationToken = default)
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
                PartyOperationFailed?.Invoke("SetLootMethod", ex.Message);
                throw;
            }
        }

        #endregion

        #region Raid Operations

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("ConvertToRaid", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("ChangeSubGroup", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("SwapSubGroups", ex.Message);
                throw;
            }
        }

        #endregion

        #region Information Requests

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("RequestPartyMemberStats", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("InitiateReadyCheck", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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
                PartyOperationFailed?.Invoke("RespondToReadyCheck", ex.Message);
                throw;
            }
        }

        #endregion

        #region Utility Methods

        /// <inheritdoc />
        public IReadOnlyList<GroupMember> GetGroupMembers()
        {
            lock (_groupLock)
            {
                return _groupMembers.ToList().AsReadOnly();
            }
        }

        /// <inheritdoc />
        public bool IsPlayerInGroup(string playerName)
        {
            lock (_groupLock)
            {
                return _groupMembers.Any(m => m.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <inheritdoc />
        public bool IsPlayerInGroup(ulong playerGuid)
        {
            lock (_groupLock)
            {
                return _groupMembers.Any(m => m.Guid == playerGuid);
            }
        }

        /// <inheritdoc />
        public GroupMember? GetGroupMember(string playerName)
        {
            lock (_groupLock)
            {
                return _groupMembers.FirstOrDefault(m => m.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <inheritdoc />
        public GroupMember? GetGroupMember(ulong playerGuid)
        {
            lock (_groupLock)
            {
                return _groupMembers.FirstOrDefault(m => m.Guid == playerGuid);
            }
        }

        #endregion

        #region Server Response Handling

        /// <summary>
        /// Handles server responses for party-related packets.
        /// </summary>
        /// <param name="opcode">The opcode of the received packet.</param>
        /// <param name="data">The packet data.</param>
        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                switch (opcode)
                {
                    case Opcode.SMSG_GROUP_INVITE:
                        HandleGroupInvite(data);
                        break;
                    case Opcode.SMSG_GROUP_LIST:
                        HandleGroupList(data);
                        break;
                    case Opcode.SMSG_GROUP_DESTROYED:
                        HandleGroupDestroyed(data);
                        break;
                    case Opcode.SMSG_GROUP_SET_LEADER:
                        HandleGroupSetLeader(data);
                        break;
                    case Opcode.SMSG_PARTY_MEMBER_STATS:
                    case Opcode.SMSG_PARTY_MEMBER_STATS_FULL:
                        HandlePartyMemberStats(data);
                        break;
                    case Opcode.SMSG_PARTY_COMMAND_RESULT:
                        HandlePartyCommandResult(data);
                        break;
                    case Opcode.MSG_RAID_READY_CHECK:
                        HandleReadyCheck(data);
                        break;
                    case Opcode.MSG_RAID_READY_CHECK_CONFIRM:
                        HandleReadyCheckResponse(data);
                        break;
                    case Opcode.SMSG_RAID_GROUP_ONLY:
                        HandleRaidGroupOnly(data);
                        break;
                    default:
                        _logger.LogDebug("Unhandled party-related opcode: {Opcode}", opcode);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling party server response for opcode: {Opcode}", opcode);
            }
        }

        private void HandleGroupInvite(byte[] data)
        {
            try
            {
                if (data.Length < 1)
                {
                    _logger.LogWarning("Invalid group invite data length: {Length}", data.Length);
                    return;
                }

                // Parse group invite data (simplified)
                var inviterName = "Unknown"; // Would parse from data in a real implementation

                _logger.LogInformation("Group invite received from {Inviter}", inviterName);
                HasPendingInvite = true;
                PartyInviteReceived?.Invoke(inviterName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling group invite");
            }
        }

        private void HandleGroupList(byte[] data)
        {
            try
            {
                if (data.Length < 5)
                {
                    _logger.LogWarning("Invalid group list data length: {Length}", data.Length);
                    return;
                }

                lock (_groupLock)
                {
                    _groupMembers.Clear();

                    // Parse group list data (simplified - real implementation would parse member data)
                    var groupType = data[0]; // 0 = party, 1 = raid
                    var memberCount = BitConverter.ToUInt32(data, 1);

                    IsInGroup = memberCount > 0;
                    IsInRaid = groupType == 1;
                    GroupSize = memberCount;

                    _logger.LogInformation("Group list updated - Type: {Type}, Members: {Count}", 
                        IsInRaid ? "Raid" : "Party", memberCount);

                    GroupJoined?.Invoke(IsInRaid, memberCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling group list");
            }
        }

        private void HandleGroupDestroyed(byte[] data)
        {
            try
            {
                lock (_groupLock)
                {
                    _groupMembers.Clear();
                    IsInGroup = false;
                    IsInRaid = false;
                    IsGroupLeader = false;
                    GroupSize = 0;
                }

                _logger.LogInformation("Group disbanded");
                GroupLeft?.Invoke("Group disbanded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling group destroyed");
            }
        }

        private void HandleGroupSetLeader(byte[] data)
        {
            try
            {
                if (data.Length < 8)
                {
                    _logger.LogWarning("Invalid group set leader data length: {Length}", data.Length);
                    return;
                }

                var newLeaderGuid = BitConverter.ToUInt64(data, 0);
                var newLeaderName = "Unknown"; // Would parse from data

                _logger.LogInformation("Group leadership changed to: {Leader} ({LeaderGuid:X})", newLeaderName, newLeaderGuid);
                LeadershipChanged?.Invoke(newLeaderName, newLeaderGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling group set leader");
            }
        }

        private void HandlePartyMemberStats(byte[] data)
        {
            try
            {
                // Parse party member stats (simplified)
                _logger.LogDebug("Party member stats received");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling party member stats");
            }
        }

        private void HandlePartyCommandResult(byte[] data)
        {
            try
            {
                if (data.Length < 8)
                {
                    _logger.LogWarning("Invalid party command result data length: {Length}", data.Length);
                    return;
                }

                var operation = BitConverter.ToUInt32(data, 0);
                var result = BitConverter.ToUInt32(data, 4);

                var operationName = operation switch
                {
                    0x00 => "Invite",
                    0x01 => "Leave",
                    0x02 => "SetLeader",
                    0x03 => "LootMethod",
                    0x04 => "ChangeSubGroup",
                    _ => $"Unknown({operation:X})"
                };

                if (result == 0)
                {
                    _logger.LogInformation("Party operation {Operation} succeeded", operationName);
                }
                else
                {
                    _logger.LogWarning("Party operation {Operation} failed with result: {Result}", operationName, result);
                    PartyOperationFailed?.Invoke(operationName, $"Operation failed with result: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling party command result");
            }
        }

        private void HandleReadyCheck(byte[] data)
        {
            try
            {
                _logger.LogInformation("Ready check initiated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ready check");
            }
        }

        private void HandleReadyCheckResponse(byte[] data)
        {
            try
            {
                _logger.LogDebug("Ready check response received");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ready check response");
            }
        }

        private void HandleRaidGroupOnly(byte[] data)
        {
            try
            {
                _logger.LogInformation("Instance requires raid group");
                PartyOperationFailed?.Invoke("InstanceEntry", "This instance requires a raid group");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling raid group only");
            }
        }

        #endregion
    }
}