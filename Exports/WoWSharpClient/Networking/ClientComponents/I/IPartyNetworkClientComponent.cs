using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling party/raid group management operations in World of Warcraft.
    /// Manages party invites, member management, loot settings, raid conversion, and leadership operations.
    /// </summary>
    public interface IPartyNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the player is currently in a party or raid.
        /// </summary>
        bool IsInGroup { get; }

        /// <summary>
        /// Gets a value indicating whether the current group is a raid.
        /// </summary>
        bool IsInRaid { get; }

        /// <summary>
        /// Gets a value indicating whether the player is the group leader.
        /// </summary>
        bool IsGroupLeader { get; }

        /// <summary>
        /// Gets the number of members currently in the group.
        /// </summary>
        uint GroupSize { get; }

        /// <summary>
        /// Gets the current loot method setting for the group.
        /// </summary>
        LootMethod CurrentLootMethod { get; }

        /// <summary>
        /// Gets a value indicating whether there is a pending group invite.
        /// </summary>
        bool HasPendingInvite { get; }

        /// <summary>
        /// Event fired when a party invite is received.
        /// </summary>
        /// <param name="inviterName">The name of the player who sent the invite.</param>
        event Action<string>? PartyInviteReceived;

        /// <summary>
        /// Event fired when successfully joining a group.
        /// </summary>
        /// <param name="isRaid">Whether the joined group is a raid.</param>
        /// <param name="memberCount">The number of members in the group.</param>
        event Action<bool, uint>? GroupJoined;

        /// <summary>
        /// Event fired when leaving or being removed from a group.
        /// </summary>
        /// <param name="reason">The reason for leaving (left, kicked, disbanded, etc.).</param>
        event Action<string>? GroupLeft;

        /// <summary>
        /// Event fired when a member joins the group.
        /// </summary>
        /// <param name="memberName">The name of the member who joined.</param>
        /// <param name="memberGuid">The GUID of the member who joined.</param>
        event Action<string, ulong>? MemberJoined;

        /// <summary>
        /// Event fired when a member leaves the group.
        /// </summary>
        /// <param name="memberName">The name of the member who left.</param>
        /// <param name="memberGuid">The GUID of the member who left.</param>
        event Action<string, ulong>? MemberLeft;

        /// <summary>
        /// Event fired when group leadership changes.
        /// </summary>
        /// <param name="newLeaderName">The name of the new leader.</param>
        /// <param name="newLeaderGuid">The GUID of the new leader.</param>
        event Action<string, ulong>? LeadershipChanged;

        /// <summary>
        /// Event fired when the loot method changes.
        /// </summary>
        /// <param name="lootMethod">The new loot method.</param>
        /// <param name="lootMasterGuid">The GUID of the loot master (if applicable).</param>
        event Action<LootMethod, ulong?>? LootMethodChanged;

        /// <summary>
        /// Event fired when the group is converted between party and raid.
        /// </summary>
        /// <param name="isNowRaid">Whether the group is now a raid.</param>
        event Action<bool>? GroupConverted;

        /// <summary>
        /// Event fired when a party operation fails.
        /// </summary>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="error">The error message.</param>
        event Action<string, string>? PartyOperationFailed;

        #region Party Invite Operations

        /// <summary>
        /// Invites a player to the group by name.
        /// </summary>
        /// <param name="playerName">The name of the player to invite.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InvitePlayerAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Accepts a pending group invite.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AcceptInviteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Declines a pending group invite.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeclineInviteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a group invite that was sent to another player.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CancelInviteAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Member Management

        /// <summary>
        /// Removes a player from the group by name.
        /// </summary>
        /// <param name="playerName">The name of the player to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task KickPlayerAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a player from the group by GUID.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task KickPlayerAsync(ulong playerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Leaves the current group.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LeaveGroupAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disbands the group (leader only).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DisbandGroupAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Leadership Operations

        /// <summary>
        /// Promotes a player to group leader by name.
        /// </summary>
        /// <param name="playerName">The name of the player to promote.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PromoteToLeaderAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Promotes a player to group leader by GUID.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PromoteToLeaderAsync(ulong playerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Promotes a player to assistant leader (raid only).
        /// </summary>
        /// <param name="playerName">The name of the player to promote.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PromoteToAssistantAsync(string playerName, CancellationToken cancellationToken = default);

        #endregion

        #region Loot Settings

        /// <summary>
        /// Sets the loot method for the group.
        /// </summary>
        /// <param name="lootMethod">The loot method to set.</param>
        /// <param name="lootMasterGuid">The GUID of the loot master (for master looter method).</param>
        /// <param name="lootThreshold">The loot quality threshold.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetLootMethodAsync(LootMethod lootMethod, ulong? lootMasterGuid = null, ItemQuality lootThreshold = ItemQuality.Uncommon, CancellationToken cancellationToken = default);

        #endregion

        #region Raid Operations

        /// <summary>
        /// Converts the current party to a raid.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ConvertToRaidAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes a member's subgroup in the raid.
        /// </summary>
        /// <param name="playerName">The name of the player to move.</param>
        /// <param name="subGroup">The target subgroup (0-7).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ChangeSubGroupAsync(string playerName, byte subGroup, CancellationToken cancellationToken = default);

        /// <summary>
        /// Swaps two players' subgroups in the raid.
        /// </summary>
        /// <param name="playerName1">The name of the first player.</param>
        /// <param name="playerName2">The name of the second player.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SwapSubGroupsAsync(string playerName1, string playerName2, CancellationToken cancellationToken = default);

        #endregion

        #region Information Requests

        /// <summary>
        /// Requests party member statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RequestPartyMemberStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Initiates a ready check for the group.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitiateReadyCheckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Responds to a ready check.
        /// </summary>
        /// <param name="isReady">Whether the player is ready.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RespondToReadyCheckAsync(bool isReady, CancellationToken cancellationToken = default);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the current list of group members.
        /// </summary>
        /// <returns>A readonly list of group member information.</returns>
        IReadOnlyList<GroupMember> GetGroupMembers();

        /// <summary>
        /// Checks if a specific player is in the group.
        /// </summary>
        /// <param name="playerName">The name of the player to check.</param>
        /// <returns>True if the player is in the group, false otherwise.</returns>
        bool IsPlayerInGroup(string playerName);

        /// <summary>
        /// Checks if a specific player is in the group by GUID.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to check.</param>
        /// <returns>True if the player is in the group, false otherwise.</returns>
        bool IsPlayerInGroup(ulong playerGuid);

        /// <summary>
        /// Gets information about a specific group member.
        /// </summary>
        /// <param name="playerName">The name of the player.</param>
        /// <returns>Group member information, or null if not found.</returns>
        GroupMember? GetGroupMember(string playerName);

        /// <summary>
        /// Gets information about a specific group member by GUID.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player.</param>
        /// <returns>Group member information, or null if not found.</returns>
        GroupMember? GetGroupMember(ulong playerGuid);

        #endregion
    }

    /// <summary>
    /// Represents information about a group member.
    /// </summary>
    public class GroupMember
    {
        /// <summary>
        /// Gets or sets the member's GUID.
        /// </summary>
        public ulong Guid { get; set; }

        /// <summary>
        /// Gets or sets the member's name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the member's subgroup (for raids).
        /// </summary>
        public byte SubGroup { get; set; }

        /// <summary>
        /// Gets or sets whether the member is online.
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// Gets or sets whether the member is the group leader.
        /// </summary>
        public bool IsLeader { get; set; }

        /// <summary>
        /// Gets or sets whether the member is an assistant leader.
        /// </summary>
        public bool IsAssistant { get; set; }

        /// <summary>
        /// Gets or sets the member's current health percentage.
        /// </summary>
        public float HealthPercent { get; set; }

        /// <summary>
        /// Gets or sets the member's current mana percentage.
        /// </summary>
        public float ManaPercent { get; set; }

        /// <summary>
        /// Gets or sets the member's level.
        /// </summary>
        public uint Level { get; set; }

        /// <summary>
        /// Gets or sets the member's class.
        /// </summary>
        public Class Class { get; set; }

        /// <summary>
        /// Gets or sets the member's zone name.
        /// </summary>
        public string Zone { get; set; } = string.Empty;
    }
}