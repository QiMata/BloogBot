using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for chat network agent that handles all chat operations in World of Warcraft.
    /// Provides specialized channels, commands, and reactive observables for chat events.
    /// Uses reactive observables instead of traditional events for better composability and filtering.
    /// </summary>
    public interface IChatNetworkAgent
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether a chat operation is currently in progress.
        /// </summary>
        bool IsChatOperationInProgress { get; }

        /// <summary>
        /// Gets the timestamp of the last chat operation.
        /// </summary>
        DateTime? LastChatOperationTime { get; }

        /// <summary>
        /// Gets a value indicating whether the player is currently AFK.
        /// </summary>
        bool IsAfk { get; }

        /// <summary>
        /// Gets a value indicating whether the player is currently DND (Do Not Disturb).
        /// </summary>
        bool IsDnd { get; }

        /// <summary>
        /// Gets the current AFK message, if any.
        /// </summary>
        string? AfkMessage { get; }

        /// <summary>
        /// Gets the current DND message, if any.
        /// </summary>
        string? DndMessage { get; }

        #endregion

        #region Reactive Observables

        /// <summary>
        /// Observable stream of incoming chat messages from all channels.
        /// </summary>
        IObservable<ChatMessageData> IncomingMessages { get; }

        /// <summary>
        /// Observable stream of outgoing chat messages sent by this client.
        /// </summary>
        IObservable<OutgoingChatMessageData> OutgoingMessages { get; }

        /// <summary>
        /// Observable stream of chat notifications (channel joins, AFK mode changes, etc.).
        /// </summary>
        IObservable<ChatNotificationData> ChatNotifications { get; }

        /// <summary>
        /// Observable stream of executed chat commands.
        /// </summary>
        IObservable<ChatCommandData> ExecutedCommands { get; }

        /// <summary>
        /// Observable stream of say messages only.
        /// </summary>
        IObservable<ChatMessageData> SayMessages { get; }

        /// <summary>
        /// Observable stream of whisper messages only (both incoming and outgoing notifications).
        /// </summary>
        IObservable<ChatMessageData> WhisperMessages { get; }

        /// <summary>
        /// Observable stream of party messages only.
        /// </summary>
        IObservable<ChatMessageData> PartyMessages { get; }

        /// <summary>
        /// Observable stream of guild messages only.
        /// </summary>
        IObservable<ChatMessageData> GuildMessages { get; }

        /// <summary>
        /// Observable stream of raid messages only.
        /// </summary>
        IObservable<ChatMessageData> RaidMessages { get; }

        /// <summary>
        /// Observable stream of channel messages only.
        /// </summary>
        IObservable<ChatMessageData> ChannelMessages { get; }

        /// <summary>
        /// Observable stream of system messages only.
        /// </summary>
        IObservable<ChatMessageData> SystemMessages { get; }

        /// <summary>
        /// Observable stream of emote messages only.
        /// </summary>
        IObservable<ChatMessageData> EmoteMessages { get; }

        #endregion

        #region Message Sending

        /// <summary>
        /// Sends a chat message to the specified channel or recipient.
        /// </summary>
        /// <param name="chatType">The type of chat message to send.</param>
        /// <param name="message">The message content.</param>
        /// <param name="destination">The destination (player name for whisper, channel name for channel, null for others).</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task SendMessageAsync(ChatMsg chatType, string message, string? destination = null, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the say channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task SayAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the yell channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task YellAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a whisper message to a specific player.
        /// </summary>
        /// <param name="playerName">The name of the player to whisper to.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task WhisperAsync(string playerName, string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the party channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task PartyAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the guild channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task GuildAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the officer channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task OfficerAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the raid channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task RaidAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a raid warning message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task RaidWarningAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to a specific channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to send to.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="language">The language to send the message in.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task ChannelAsync(string channelName, string message, Language language = Language.Common, CancellationToken cancellationToken = default);

        #endregion

        #region Channel Management

        /// <summary>
        /// Joins a chat channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to join.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous join operation.</returns>
        Task JoinChannelAsync(string channelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Leaves a chat channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to leave.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous leave operation.</returns>
        Task LeaveChannelAsync(string channelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists members of a chat channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to list.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous list operation.</returns>
        Task ListChannelAsync(string channelName, CancellationToken cancellationToken = default);

        #endregion

        #region Player State Management

        /// <summary>
        /// Sets the player's AFK status.
        /// </summary>
        /// <param name="afkMessage">The AFK message to display, or null to remove AFK status.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous AFK operation.</returns>
        Task SetAfkAsync(string? afkMessage = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the player's DND (Do Not Disturb) status.
        /// </summary>
        /// <param name="dndMessage">The DND message to display, or null to remove DND status.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous DND operation.</returns>
        Task SetDndAsync(string? dndMessage = null, CancellationToken cancellationToken = default);

        #endregion

        #region Command Execution

        /// <summary>
        /// Executes a chat command (e.g., /who, /guild, /afk).
        /// </summary>
        /// <param name="command">The command to execute (without the leading slash).</param>
        /// <param name="arguments">Optional arguments for the command.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous command execution.</returns>
        Task ExecuteCommandAsync(string command, string[]? arguments = null, CancellationToken cancellationToken = default);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if a message can be sent to the specified chat type based on current state and rate limiting.
        /// </summary>
        /// <param name="chatType">The type of chat to check.</param>
        /// <returns>True if the message can be sent, false otherwise.</returns>
        bool CanSendMessage(ChatMsg chatType);

        /// <summary>
        /// Validates a message before sending.
        /// </summary>
        /// <param name="chatType">The type of chat message.</param>
        /// <param name="message">The message content.</param>
        /// <param name="destination">The destination (if applicable).</param>
        /// <returns>A validation result indicating whether the message is valid.</returns>
        ValidationResult ValidateMessage(ChatMsg chatType, string message, string? destination = null);

        /// <summary>
        /// Gets a list of currently active chat channels.
        /// </summary>
        /// <returns>A read-only list of active channel names.</returns>
        IReadOnlyList<string> GetActiveChannels();

        #endregion

        #region Public Methods for Server Response Handling

        /// <summary>
        /// Handles an incoming chat message from the server.
        /// This method should be called by the packet handler when a chat message is received.
        /// </summary>
        /// <param name="chatType">The type of chat message.</param>
        /// <param name="language">The language of the message.</param>
        /// <param name="senderGuid">The sender's GUID.</param>
        /// <param name="targetGuid">The target's GUID.</param>
        /// <param name="senderName">The sender's name.</param>
        /// <param name="channelName">The channel name (if applicable).</param>
        /// <param name="playerRank">The player's rank.</param>
        /// <param name="text">The message text.</param>
        /// <param name="playerChatTag">The player's chat tag.</param>
        void HandleIncomingMessage(ChatMsg chatType, Language language, ulong senderGuid, ulong targetGuid, 
            string senderName, string channelName, byte playerRank, string text, PlayerChatTag playerChatTag);

        #endregion
    }
}