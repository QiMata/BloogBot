namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Result of a guild command reported by the server (parsed from SMSG_GUILD_COMMAND_RESULT).
    /// </summary>
    public readonly record struct GuildCommandResult(string Operation, bool Success, uint ResultCode);

    /// <summary>
    /// Member online/offline status change notification (derived from guild event opcodes).
    /// </summary>
    public readonly record struct GuildMemberStatusChange(string MemberName, bool IsOnline);
}
