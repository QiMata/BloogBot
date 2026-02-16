using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling party/raid group management operations in World of Warcraft.
    /// Reactive variant: exposes opcode-backed observables instead of events.
    /// </summary>
    public interface IPartyNetworkClientComponent : INetworkClientComponent
    {
        // State
        bool IsInGroup { get; }
        bool IsInRaid { get; }
        bool IsGroupLeader { get; }
        uint GroupSize { get; }
        LootMethod CurrentLootMethod { get; }
        bool HasPendingInvite { get; }

        // Reactive streams (server -> client)
        /// <summary>Stream of incoming party invites (inviter name).</summary>
        IObservable<string> PartyInvites { get; }
        /// <summary>Stream of group summaries received from the server (IsRaid, MemberCount).</summary>
        IObservable<(bool IsRaid, uint MemberCount)> GroupUpdates { get; }
        /// <summary>Stream of reasons when the player leaves or the group is disbanded.</summary>
        IObservable<string> GroupLeaves { get; }
        /// <summary>Stream of leadership changes (new leader name, new leader guid).</summary>
        IObservable<(string NewLeaderName, ulong NewLeaderGuid)> LeadershipChanges { get; }
        /// <summary>Stream of party command results parsed from SMSG_PARTY_COMMAND_RESULT.</summary>
        IObservable<(string Operation, bool Success, uint ResultCode)> PartyCommandResults { get; }
        /// <summary>Stream of errors for party-related operations not covered by command results.</summary>
        IObservable<(string Operation, string Error)> PartyErrors { get; }
        /// <summary>Stream fired when a ready check is initiated.</summary>
        IObservable<Unit> ReadyCheckRequests { get; }
        /// <summary>Stream of ready check confirmations (opaque, emits a Unit per confirmation).</summary>
        IObservable<Unit> ReadyCheckResponses { get; }

        #region Party Invite Operations
        Task InvitePlayerAsync(string playerName, CancellationToken cancellationToken = default);
        Task AcceptInviteAsync(CancellationToken cancellationToken = default);
        Task DeclineInviteAsync(CancellationToken cancellationToken = default);
        Task CancelInviteAsync(CancellationToken cancellationToken = default);
        #endregion

        #region Member Management
        Task KickPlayerAsync(string playerName, CancellationToken cancellationToken = default);
        Task KickPlayerAsync(ulong playerGuid, CancellationToken cancellationToken = default);
        Task LeaveGroupAsync(CancellationToken cancellationToken = default);
        Task DisbandGroupAsync(CancellationToken cancellationToken = default);
        #endregion

        #region Leadership Operations
        Task PromoteToLeaderAsync(string playerName, CancellationToken cancellationToken = default);
        Task PromoteToLeaderAsync(ulong playerGuid, CancellationToken cancellationToken = default);
        Task PromoteToAssistantAsync(string playerName, CancellationToken cancellationToken = default);
        #endregion

        #region Loot Settings
        Task SetLootMethodAsync(LootMethod lootMethod, ulong? lootMasterGuid = null, ItemQuality lootThreshold = ItemQuality.Uncommon, CancellationToken cancellationToken = default);
        #endregion

        #region Raid Operations
        Task ConvertToRaidAsync(CancellationToken cancellationToken = default);
        Task ChangeSubGroupAsync(string playerName, byte subGroup, CancellationToken cancellationToken = default);
        Task SwapSubGroupsAsync(string playerName1, string playerName2, CancellationToken cancellationToken = default);
        #endregion

        #region Information Requests
        Task RequestPartyMemberStatsAsync(ulong memberGuid, CancellationToken cancellationToken = default);
        Task InitiateReadyCheckAsync(CancellationToken cancellationToken = default);
        Task RespondToReadyCheckAsync(bool isReady, CancellationToken cancellationToken = default);
        #endregion

        #region Utility Methods
        IReadOnlyList<GroupMember> GetGroupMembers();
        bool IsPlayerInGroup(string playerName);
        bool IsPlayerInGroup(ulong playerGuid);
        GroupMember? GetGroupMember(string playerName);
        GroupMember? GetGroupMember(ulong playerGuid);
        #endregion
    }

    /// <summary>
    /// Represents information about a group member.
    /// </summary>
    public class GroupMember
    {
        public ulong Guid { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte SubGroup { get; set; }
        public bool IsOnline { get; set; }
        public bool IsLeader { get; set; }
        public bool IsAssistant { get; set; }
        public float HealthPercent { get; set; }
        public float ManaPercent { get; set; }
        public uint Level { get; set; }
        public Class Class { get; set; }
        public string Zone { get; set; } = string.Empty;
    }
}