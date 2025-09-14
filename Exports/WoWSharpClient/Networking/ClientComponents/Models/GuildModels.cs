using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents guild information.
    /// </summary>
    public class GuildInfo
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public uint GuildId { get; set; }

        /// <summary>
        /// Gets or sets the guild name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the guild MOTD (Message of the Day).
        /// </summary>
        public string MOTD { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the guild info text.
        /// </summary>
        public string Info { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of guild members.
        /// </summary>
        public uint MemberCount { get; set; }

        /// <summary>
        /// Gets or sets the guild creation date.
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the guild leader name.
        /// </summary>
        public string LeaderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the guild ranks.
        /// </summary>
        public GuildRankInfo[] Ranks { get; set; } = Array.Empty<GuildRankInfo>();

        /// <summary>
        /// Gets or sets the guild emblem information.
        /// </summary>
        public GuildEmblem? Emblem { get; set; }
    }

    /// <summary>
    /// Represents a guild member.
    /// </summary>
    public class GuildMember
    {
        /// <summary>
        /// Gets or sets the member GUID.
        /// </summary>
        public ulong Guid { get; set; }

        /// <summary>
        /// Gets or sets the member name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the member's guild rank.
        /// </summary>
        public uint Rank { get; set; }

        /// <summary>
        /// Gets or sets the member's class.
        /// </summary>
        public uint Class { get; set; }

        /// <summary>
        /// Gets or sets the member's race.
        /// </summary>
        public uint Race { get; set; }

        /// <summary>
        /// Gets or sets the member's gender.
        /// </summary>
        public uint Gender { get; set; }

        /// <summary>
        /// Gets or sets the member's level.
        /// </summary>
        public uint Level { get; set; }

        /// <summary>
        /// Gets or sets the member's zone.
        /// </summary>
        public string Zone { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the member is online.
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// Gets or sets the member's status.
        /// </summary>
        public GuildMemberStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the member's public note.
        /// </summary>
        public string PublicNote { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the member's officer note.
        /// </summary>
        public string OfficerNote { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the member last logged on.
        /// </summary>
        public DateTime? LastLogon { get; set; }
    }

    /// <summary>
    /// Represents a guild rank with permissions.
    /// </summary>
    public class GuildRankInfo
    {
        /// <summary>
        /// Gets or sets the rank ID.
        /// </summary>
        public uint RankId { get; set; }

        /// <summary>
        /// Gets or sets the rank name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rank permissions.
        /// </summary>
        public GuildRankPermissions Permissions { get; set; }

        /// <summary>
        /// Gets or sets the number of members with this rank.
        /// </summary>
        public uint MemberCount { get; set; }
    }

    /// <summary>
    /// Represents guild rank permissions.
    /// </summary>
    [Flags]
    public enum GuildRankPermissions : uint
    {
        None = 0,
        GuildChat = 1 << 0,
        OfficerChat = 1 << 1,
        Promote = 1 << 2,
        Demote = 1 << 3,
        Invite = 1 << 4,
        Remove = 1 << 5,
        SetMOTD = 1 << 6,
        EditPublicNote = 1 << 7,
        ViewOfficerNote = 1 << 8,
        EditOfficerNote = 1 << 9
    }

    /// <summary>
    /// Represents guild member status.
    /// </summary>
    public enum GuildMemberStatus
    {
        Offline = 0,
        Online = 1,
        AFK = 2,
        DND = 3
    }

    /// <summary>
    /// Represents guild emblem information.
    /// </summary>
    public class GuildEmblem
    {
        /// <summary>
        /// Gets or sets the emblem style.
        /// </summary>
        public uint Style { get; set; }

        /// <summary>
        /// Gets or sets the emblem color.
        /// </summary>
        public uint Color { get; set; }

        /// <summary>
        /// Gets or sets the border style.
        /// </summary>
        public uint BorderStyle { get; set; }

        /// <summary>
        /// Gets or sets the border color.
        /// </summary>
        public uint BorderColor { get; set; }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public uint BackgroundColor { get; set; }
    }

    /// <summary>
    /// Represents guild event log entry.
    /// </summary>
    public class GuildEventLogEntry
    {
        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public GuildEventType EventType { get; set; }

        /// <summary>
        /// Gets or sets the primary player name involved.
        /// </summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secondary player name (for promotions, demotions, etc.).
        /// </summary>
        public string? SecondaryPlayerName { get; set; }

        /// <summary>
        /// Gets or sets the old rank (for rank changes).
        /// </summary>
        public uint? OldRank { get; set; }

        /// <summary>
        /// Gets or sets the new rank (for rank changes).
        /// </summary>
        public uint? NewRank { get; set; }

        /// <summary>
        /// Gets or sets when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents guild event types.
    /// </summary>
    public enum GuildEventType
    {
        Joined = 0,
        Left = 1,
        Promoted = 2,
        Demoted = 3,
        Invited = 4,
        Removed = 5,
        LeaderChanged = 6,
        MOTDChanged = 7,
        InfoChanged = 8,
        Created = 9,
        Disbanded = 10
    }

    /// <summary>
    /// Represents the result of a guild operation.
    /// </summary>
    public enum GuildResult
    {
        Success = 0,
        InternalError = 1,
        AlreadyInGuild = 2,
        AlreadyInGuildS = 3,
        InvitedToGuild = 4,
        AlreadyInvitedToGuildS = 5,
        GuildNameInvalid = 6,
        GuildNameExists = 7,
        GuildLeaderLeave = 8,
        GuildPermissions = 9,
        GuildPlayerNotInGuild = 10,
        GuildPlayerNotInGuildS = 11,
        GuildPlayerNotFound = 12,
        GuildNotAllied = 13,
        GuildRankTooHigh = 14,
        GuildRankTooLow = 15
    }

    /// <summary>
    /// Represents guild invitation data.
    /// </summary>
    public class GuildInviteData
    {
        /// <summary>
        /// Gets or sets the inviter name.
        /// </summary>
        public string InviterName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the guild name.
        /// </summary>
        public string GuildName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the invitation was received.
        /// </summary>
        public DateTime InviteTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether the invitation is still pending.
        /// </summary>
        public bool IsPending { get; set; } = true;
    }
}