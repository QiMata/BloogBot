namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling guild-related operations in World of Warcraft.
    /// Manages guild invites, guild bank interactions, member management, and guild settings.
    /// </summary>
    public interface IGuildNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the player is currently in a guild.
        /// </summary>
        bool IsInGuild { get; }

        /// <summary>
        /// Gets the current guild ID if the player is in a guild.
        /// </summary>
        uint? CurrentGuildId { get; }

        /// <summary>
        /// Gets a value indicating whether a guild window is currently open.
        /// </summary>
        bool IsGuildWindowOpen { get; }

        /// <summary>
        /// Gets a value indicating whether a guild bank window is currently open.
        /// </summary>
        bool IsGuildBankWindowOpen { get; }

        /// <summary>
        /// Event fired when a guild invite is received.
        /// </summary>
        /// <param name="inviterName">The name of the player who sent the invite.</param>
        /// <param name="guildName">The name of the guild.</param>
        event Action<string, string>? GuildInviteReceived;

        /// <summary>
        /// Event fired when the player successfully joins a guild.
        /// </summary>
        /// <param name="guildId">The guild ID.</param>
        /// <param name="guildName">The guild name.</param>
        event Action<uint, string>? GuildJoined;

        /// <summary>
        /// Event fired when the player leaves or is removed from a guild.
        /// </summary>
        /// <param name="guildId">The guild ID.</param>
        /// <param name="reason">The reason for leaving (kicked, left, disbanded, etc.).</param>
        event Action<uint, string>? GuildLeft;

        /// <summary>
        /// Event fired when a guild member comes online.
        /// </summary>
        /// <param name="memberName">The member's name.</param>
        event Action<string>? GuildMemberOnline;

        /// <summary>
        /// Event fired when a guild member goes offline.
        /// </summary>
        /// <param name="memberName">The member's name.</param>
        event Action<string>? GuildMemberOffline;

        /// <summary>
        /// Event fired when the guild roster is received from the server.
        /// </summary>
        /// <param name="memberCount">The number of guild members.</param>
        event Action<uint>? GuildRosterReceived;

        /// <summary>
        /// Event fired when guild information is received.
        /// </summary>
        /// <param name="guildId">The guild ID.</param>
        /// <param name="guildName">The guild name.</param>
        /// <param name="guildInfo">Guild information string.</param>
        event Action<uint, string, string>? GuildInfoReceived;

        /// <summary>
        /// Event fired when a guild message of the day (MOTD) is received.
        /// </summary>
        /// <param name="motd">The message of the day.</param>
        event Action<string>? GuildMOTDReceived;

        /// <summary>
        /// Event fired when the guild bank window is opened.
        /// </summary>
        /// <param name="bankGuid">The GUID of the guild bank.</param>
        event Action<ulong>? GuildBankWindowOpened;

        /// <summary>
        /// Event fired when the guild bank window is closed.
        /// </summary>
        event Action? GuildBankWindowClosed;

        /// <summary>
        /// Event fired when an item is deposited to the guild bank.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="quantity">The quantity deposited.</param>
        /// <param name="tabIndex">The guild bank tab index.</param>
        /// <param name="slotIndex">The slot index in the tab.</param>
        event Action<uint, uint, byte, byte>? ItemDepositedToGuildBank;

        /// <summary>
        /// Event fired when an item is withdrawn from the guild bank.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="quantity">The quantity withdrawn.</param>
        /// <param name="tabIndex">The guild bank tab index.</param>
        /// <param name="slotIndex">The slot index in the tab.</param>
        event Action<uint, uint, byte, byte>? ItemWithdrawnFromGuildBank;

        /// <summary>
        /// Event fired when money is deposited to the guild bank.
        /// </summary>
        /// <param name="amount">The amount of money deposited in copper.</param>
        event Action<uint>? MoneyDepositedToGuildBank;

        /// <summary>
        /// Event fired when money is withdrawn from the guild bank.
        /// </summary>
        /// <param name="amount">The amount of money withdrawn in copper.</param>
        event Action<uint>? MoneyWithdrawnFromGuildBank;

        /// <summary>
        /// Event fired when a guild operation fails.
        /// </summary>
        /// <param name="operation">The failed operation.</param>
        /// <param name="error">The error message.</param>
        event Action<string, string>? GuildOperationFailed;

        /// <summary>
        /// Accepts a pending guild invitation.
        /// Sends CMSG_GUILD_ACCEPT to accept the guild invite.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AcceptGuildInviteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Declines a pending guild invitation.
        /// Sends CMSG_GUILD_DECLINE to decline the guild invite.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeclineGuildInviteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Invites a player to the guild.
        /// Sends CMSG_GUILD_INVITE to invite the specified player.
        /// </summary>
        /// <param name="playerName">The name of the player to invite.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InvitePlayerToGuildAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a player from the guild.
        /// Sends CMSG_GUILD_REMOVE to remove the specified player from the guild.
        /// </summary>
        /// <param name="playerName">The name of the player to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemovePlayerFromGuildAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Promotes a guild member to a higher rank.
        /// Sends CMSG_GUILD_PROMOTE to promote the specified player.
        /// </summary>
        /// <param name="playerName">The name of the player to promote.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PromoteGuildMemberAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Demotes a guild member to a lower rank.
        /// Sends CMSG_GUILD_DEMOTE to demote the specified player.
        /// </summary>
        /// <param name="playerName">The name of the player to demote.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DemoteGuildMemberAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Leaves the current guild.
        /// Sends CMSG_GUILD_LEAVE to leave the guild.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LeaveGuildAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disbands the guild (guild leader only).
        /// Sends CMSG_GUILD_DISBAND to disband the guild.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DisbandGuildAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the guild message of the day.
        /// Sends CMSG_GUILD_MOTD to set the message of the day.
        /// </summary>
        /// <param name="motd">The message of the day to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetGuildMOTDAsync(string motd, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the guild information text.
        /// Sends CMSG_GUILD_INFO_TEXT to set the guild information.
        /// </summary>
        /// <param name="guildInfo">The guild information text to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetGuildInfoAsync(string guildInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the guild roster from the server.
        /// Sends CMSG_GUILD_ROSTER to request the guild member list.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestGuildRosterAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests guild information from the server.
        /// Sends MSG_GUILD_INFO to request guild details.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestGuildInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the guild bank by interacting with a guild bank NPC or object.
        /// Sends CMSG_GOSSIP_HELLO to initiate guild bank interaction.
        /// </summary>
        /// <param name="guildBankGuid">The GUID of the guild bank NPC or object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenGuildBankAsync(ulong guildBankGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the guild bank window.
        /// This typically happens automatically when moving away from the guild bank.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseGuildBankAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deposits an item from the player's inventory to the guild bank.
        /// Sends CMSG_GUILD_BANK_DEPOSIT_ITEM to deposit the item.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="tabIndex">The guild bank tab index to deposit to.</param>
        /// <param name="targetSlot">The target slot in the guild bank tab.</param>
        /// <param name="quantity">The quantity to deposit (for stackable items).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DepositItemToGuildBankAsync(byte bagId, byte slotId, byte tabIndex, byte targetSlot, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Withdraws an item from the guild bank to the player's inventory.
        /// Sends CMSG_GUILD_BANK_WITHDRAW_ITEM to withdraw the item.
        /// </summary>
        /// <param name="tabIndex">The guild bank tab index to withdraw from.</param>
        /// <param name="slotIndex">The slot index in the guild bank tab.</param>
        /// <param name="quantity">The quantity to withdraw (for stackable items).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WithdrawItemFromGuildBankAsync(byte tabIndex, byte slotIndex, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deposits money to the guild bank.
        /// Sends CMSG_GUILD_BANK_DEPOSIT_MONEY to deposit the specified amount.
        /// </summary>
        /// <param name="amount">The amount of money to deposit in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DepositMoneyToGuildBankAsync(uint amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Withdraws money from the guild bank.
        /// Sends CMSG_GUILD_BANK_WITHDRAW_MONEY to withdraw the specified amount.
        /// </summary>
        /// <param name="amount">The amount of money to withdraw in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WithdrawMoneyFromGuildBankAsync(uint amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the guild bank contents for a specific tab.
        /// Sends CMSG_GUILD_BANK_QUERY_TAB to request tab contents.
        /// </summary>
        /// <param name="tabIndex">The guild bank tab index to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryGuildBankTabAsync(byte tabIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a note for a guild member (officers only).
        /// Sends CMSG_GUILD_SET_PUBLIC_NOTE or CMSG_GUILD_SET_OFFICER_NOTE.
        /// </summary>
        /// <param name="playerName">The name of the player to set the note for.</param>
        /// <param name="note">The note text.</param>
        /// <param name="isOfficerNote">True for officer note, false for public note.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetGuildMemberNoteAsync(string playerName, string note, bool isOfficerNote = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new guild with the specified name.
        /// Sends CMSG_GUILD_CREATE to create a guild.
        /// </summary>
        /// <param name="guildName">The name of the guild to create.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CreateGuildAsync(string guildName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete guild bank interaction: open, deposit/withdraw, close.
        /// This is a convenience method for quick guild bank operations.
        /// </summary>
        /// <param name="guildBankGuid">The GUID of the guild bank.</param>
        /// <param name="operation">The operation to perform (deposit/withdraw).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickGuildBankOperationAsync(ulong guildBankGuid, Func<Task> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the specified guild bank GUID has an open guild bank window.
        /// </summary>
        /// <param name="guildBankGuid">The GUID to check.</param>
        /// <returns>True if the guild bank window is open for the specified GUID, false otherwise.</returns>
        bool IsGuildBankOpen(ulong guildBankGuid);

        /// <summary>
        /// Gets the current guild rank of the player.
        /// </summary>
        /// <returns>The guild rank ID, or null if not in a guild.</returns>
        uint? GetCurrentGuildRank();

        /// <summary>
        /// Checks if the player has the specified guild permission.
        /// </summary>
        /// <param name="permission">The permission to check.</param>
        /// <returns>True if the player has the permission, false otherwise.</returns>
        bool HasGuildPermission(uint permission);
    }
}