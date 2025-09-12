using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of chat network agent that handles all chat operations in World of Warcraft.
    /// Provides specialized channels, commands, and reactive observables for chat events.
    /// Uses reactive observables instead of traditional events for better composability and filtering.
    /// </summary>
    public class ChatNetworkClientComponent : IChatNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ChatNetworkClientComponent> _logger;
        
        // Operation state tracking
        private bool _isChatOperationInProgress;
        private DateTime? _lastChatOperationTime;
        
        // Player state
        private bool _isAfk;
        private bool _isDnd;
        private string? _afkMessage;
        private string? _dndMessage;
        
        // Active channels tracking
        private readonly ConcurrentBag<string> _activeChannels = [];
        
        // Reactive observables for chat events
        private readonly Subject<ChatMessageData> _incomingMessages = new();
        private readonly Subject<OutgoingChatMessageData> _outgoingMessages = new();
        private readonly Subject<ChatNotificationData> _chatNotifications = new();
        private readonly Subject<ChatCommandData> _executedCommands = new();
        
        // Filtered observables for specific chat types
        private readonly Lazy<IObservable<ChatMessageData>> _sayMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _whisperMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _partyMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _guildMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _raidMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _channelMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _systemMessages;
        private readonly Lazy<IObservable<ChatMessageData>> _emoteMessages;
        
        // Chat rate limiting
        private readonly Dictionary<ChatMsg, DateTime> _lastMessageTimes = new();
        private readonly Dictionary<ChatMsg, TimeSpan> _chatCooldowns = new()
        {
            { ChatMsg.CHAT_MSG_SAY, TimeSpan.FromSeconds(1) },
            { ChatMsg.CHAT_MSG_YELL, TimeSpan.FromSeconds(2) },
            { ChatMsg.CHAT_MSG_WHISPER, TimeSpan.FromMilliseconds(500) },
            { ChatMsg.CHAT_MSG_PARTY, TimeSpan.FromMilliseconds(500) },
            { ChatMsg.CHAT_MSG_GUILD, TimeSpan.FromMilliseconds(500) },
            { ChatMsg.CHAT_MSG_RAID, TimeSpan.FromMilliseconds(500) },
            { ChatMsg.CHAT_MSG_CHANNEL, TimeSpan.FromMilliseconds(500) }
        };
        
        // Thread safety
        private readonly object _stateLock = new();
        private bool _disposed = false;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ChatNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public ChatNetworkClientComponent(IWorldClient worldClient, ILogger<ChatNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize filtered observables lazily for better performance
            _sayMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_SAY));
            
            _whisperMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_WHISPER || msg.ChatType == ChatMsg.CHAT_MSG_WHISPER_INFORM));
            
            _partyMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_PARTY));
            
            _guildMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_GUILD));
            
            _raidMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_RAID || msg.ChatType == ChatMsg.CHAT_MSG_RAID_WARNING));
            
            _channelMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_CHANNEL));
            
            _systemMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_SYSTEM));
            
            _emoteMessages = new Lazy<IObservable<ChatMessageData>>(() => 
                _incomingMessages.Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_EMOTE || msg.ChatType == ChatMsg.CHAT_MSG_TEXT_EMOTE));

            // Subscribe to global chat messages from the event emitter
            WoWSharpEventEmitter.Instance.OnChatMessage += (sender, args) =>
            {
                HandleIncomingMessage(args.MsgType, args.Language, args.SenderGuid, args.TargetGuid,
                    args.SenderName, args.ChannelName, args.PlayerRank, args.Text, args.PlayerChatTag);
            };

            _logger.LogDebug("ChatNetworkClientComponent initialized with reactive observables and global event integration");
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public bool IsChatOperationInProgress => _isChatOperationInProgress;

        /// <inheritdoc />
        public DateTime? LastChatOperationTime => _lastChatOperationTime;

        /// <inheritdoc />
        public bool IsAfk => _isAfk;

        /// <inheritdoc />
        public bool IsDnd => _isDnd;

        /// <inheritdoc />
        public string? AfkMessage => _afkMessage;

        /// <inheritdoc />
        public string? DndMessage => _dndMessage;

        #endregion

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<ChatMessageData> IncomingMessages => _incomingMessages;

        /// <inheritdoc />
        public IObservable<OutgoingChatMessageData> OutgoingMessages => _outgoingMessages;

        /// <inheritdoc />
        public IObservable<ChatNotificationData> ChatNotifications => _chatNotifications;

        /// <inheritdoc />
        public IObservable<ChatCommandData> ExecutedCommands => _executedCommands;

        /// <inheritdoc />
        public IObservable<ChatMessageData> SayMessages => _sayMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> WhisperMessages => _whisperMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> PartyMessages => _partyMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> GuildMessages => _guildMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> RaidMessages => _raidMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> ChannelMessages => _channelMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> SystemMessages => _systemMessages.Value;

        /// <inheritdoc />
        public IObservable<ChatMessageData> EmoteMessages => _emoteMessages.Value;

        #endregion

        #region Message Sending

        /// <inheritdoc />
        public async Task SendMessageAsync(ChatMsg chatType, string message, string? destination = null, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            var validation = ValidateMessage(chatType, message, destination);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            if (!CanSendMessage(chatType))
            {
                throw new InvalidOperationException($"Cannot send {chatType} message at this time");
            }

            try
            {
                SetOperationInProgress(true);
                
                _logger.LogDebug("Sending {ChatType} message: '{Message}' to '{Destination}'", chatType, message, destination ?? "default");

                // Apply rate limiting
                await ApplyRateLimitingAsync(chatType, cancellationToken);

                // Send the message via world client
                await _worldClient.SendChatMessageAsync(chatType, language, destination ?? string.Empty, message, cancellationToken);

                // Record the outgoing message
                var outgoingMessage = new OutgoingChatMessageData(chatType, language, destination ?? string.Empty, message, DateTime.UtcNow);
                _outgoingMessages.OnNext(outgoingMessage);

                // Update last message time for rate limiting
                lock (_stateLock)
                {
                    _lastMessageTimes[chatType] = DateTime.UtcNow;
                }

                _logger.LogInformation("Successfully sent {ChatType} message", chatType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {ChatType} message: '{Message}'", chatType, message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SayAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_SAY, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task YellAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_YELL, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task WhisperAsync(string playerName, string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_WHISPER, message, playerName, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task PartyAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_PARTY, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task GuildAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_GUILD, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task OfficerAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_OFFICER, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task RaidAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_RAID, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task RaidWarningAsync(string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_RAID_WARNING, message, null, language, cancellationToken);
        }

        /// <inheritdoc />
        public async Task ChannelAsync(string channelName, string message, Language language = Language.Common, CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(ChatMsg.CHAT_MSG_CHANNEL, message, channelName, language, cancellationToken);
        }

        #endregion

        #region Channel Management

        /// <inheritdoc />
        public async Task JoinChannelAsync(string channelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(channelName));

            try
            {
                SetOperationInProgress(true);
                
                _logger.LogDebug("Joining channel: {ChannelName}", channelName);
                
                await ExecuteCommandAsync("join", [channelName], cancellationToken);
                
                // Add to active channels (will be confirmed by server response)
                _activeChannels.Add(channelName);
                
                _logger.LogInformation("Joined channel: {ChannelName}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join channel: {ChannelName}", channelName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task LeaveChannelAsync(string channelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(channelName));

            try
            {
                SetOperationInProgress(true);
                
                _logger.LogDebug("Leaving channel: {ChannelName}", channelName);
                
                await ExecuteCommandAsync("leave", [channelName], cancellationToken);
                
                _logger.LogInformation("Left channel: {ChannelName}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave channel: {ChannelName}", channelName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ListChannelAsync(string channelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(channelName));

            try
            {
                SetOperationInProgress(true);
                
                _logger.LogDebug("Listing channel: {ChannelName}", channelName);
                
                await ExecuteCommandAsync("chatlist", [channelName], cancellationToken);
                
                _logger.LogInformation("Listed channel: {ChannelName}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list channel: {ChannelName}", channelName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Player State Management

        /// <inheritdoc />
        public async Task SetAfkAsync(string? afkMessage = null, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                
                if (string.IsNullOrWhiteSpace(afkMessage))
                {
                    _logger.LogDebug("Removing AFK status");
                    await ExecuteCommandAsync("afk", null, cancellationToken);
                    
                    lock (_stateLock)
                    {
                        _isAfk = false;
                        _afkMessage = null;
                    }
                }
                else
                {
                    _logger.LogDebug("Setting AFK status with message: {AfkMessage}", afkMessage);
                    await ExecuteCommandAsync("afk", [afkMessage], cancellationToken);
                    
                    lock (_stateLock)
                    {
                        _isAfk = true;
                        _afkMessage = afkMessage;
                    }
                }

                var notification = new ChatNotificationData(
                    _isAfk ? ChatNotificationType.AfkMode : ChatNotificationType.AfkMode, 
                    _isAfk ? $"AFK: {afkMessage}" : "No longer AFK", 
                    null, 
                    null, 
                    DateTime.UtcNow);
                _chatNotifications.OnNext(notification);
                
                _logger.LogInformation("AFK status updated: {IsAfk}", _isAfk);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set AFK status");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SetDndAsync(string? dndMessage = null, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                
                if (string.IsNullOrWhiteSpace(dndMessage))
                {
                    _logger.LogDebug("Removing DND status");
                    await ExecuteCommandAsync("dnd", null, cancellationToken);
                    
                    lock (_stateLock)
                    {
                        _isDnd = false;
                        _dndMessage = null;
                    }
                }
                else
                {
                    _logger.LogDebug("Setting DND status with message: {DndMessage}", dndMessage);
                    await ExecuteCommandAsync("dnd", [dndMessage], cancellationToken);
                    
                    lock (_stateLock)
                    {
                        _isDnd = true;
                        _dndMessage = dndMessage;
                    }
                }

                var notification = new ChatNotificationData(
                    _isDnd ? ChatNotificationType.DndMode : ChatNotificationType.DndMode, 
                    _isDnd ? $"DND: {dndMessage}" : "No longer DND", 
                    null, 
                    null, 
                    DateTime.UtcNow);
                _chatNotifications.OnNext(notification);
                
                _logger.LogInformation("DND status updated: {IsDnd}", _isDnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set DND status");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Command Execution

        /// <inheritdoc />
        public async Task ExecuteCommandAsync(string command, string[]? arguments = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be null or empty", nameof(command));

            try
            {
                SetOperationInProgress(true);
                
                // Build command string
                var commandBuilder = new StringBuilder();
                commandBuilder.Append('/').Append(command);
                
                if (arguments != null && arguments.Length > 0)
                {
                    foreach (var arg in arguments)
                    {
                        if (!string.IsNullOrWhiteSpace(arg))
                        {
                            commandBuilder.Append(' ').Append(arg);
                        }
                    }
                }

                var fullCommand = commandBuilder.ToString();
                _logger.LogDebug("Executing command: {Command}", fullCommand);

                // Send as a say message (commands are typically sent this way in WoW)
                await _worldClient.SendChatMessageAsync(ChatMsg.CHAT_MSG_SAY, Language.Common, string.Empty, fullCommand, cancellationToken);

                // Record the executed command
                var commandData = new ChatCommandData(command, arguments ?? [], DateTime.UtcNow);
                _executedCommands.OnNext(commandData);

                _logger.LogInformation("Successfully executed command: {Command}", command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command: {Command}", command);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Utility Methods

        /// <inheritdoc />
        public bool CanSendMessage(ChatMsg chatType)
        {
            // Check rate limiting
            if (_chatCooldowns.ContainsKey(chatType) && _lastMessageTimes.ContainsKey(chatType))
            {
                var timeSinceLastMessage = DateTime.UtcNow - _lastMessageTimes[chatType];
                if (timeSinceLastMessage < _chatCooldowns[chatType])
                {
                    return false;
                }
            }

            // Check specific conditions based on chat type
            return chatType switch
            {
                ChatMsg.CHAT_MSG_PARTY => true, // Would need party state check in real implementation
                ChatMsg.CHAT_MSG_GUILD => true, // Would need guild membership check in real implementation
                ChatMsg.CHAT_MSG_RAID => true,  // Would need raid membership check in real implementation
                ChatMsg.CHAT_MSG_OFFICER => true, // Would need officer privileges check in real implementation
                ChatMsg.CHAT_MSG_RAID_WARNING => true, // Would need raid leader check in real implementation
                _ => true
            };
        }

        /// <inheritdoc />
        public ValidationResult ValidateMessage(ChatMsg chatType, string message, string? destination = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return new ValidationResult(false, "Message cannot be null or empty");
            }

            if (message.Length > 255)
            {
                return new ValidationResult(false, "Message too long (maximum 255 characters)");
            }

            switch (chatType)
            {
                case ChatMsg.CHAT_MSG_WHISPER:
                    if (string.IsNullOrWhiteSpace(destination))
                    {
                        return new ValidationResult(false, "Whisper requires a destination player name");
                    }
                    break;
                case ChatMsg.CHAT_MSG_CHANNEL:
                    if (string.IsNullOrWhiteSpace(destination))
                    {
                        return new ValidationResult(false, "Channel message requires a channel name");
                    }
                    break;
            }

            return new ValidationResult(true);
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetActiveChannels()
        {
            return _activeChannels.ToList().AsReadOnly();
        }

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
        public void HandleIncomingMessage(ChatMsg chatType, Language language, ulong senderGuid, ulong targetGuid, 
            string senderName, string channelName, byte playerRank, string text, PlayerChatTag playerChatTag)
        {
            try
            {
                var messageData = new ChatMessageData(
                    chatType, language, senderGuid, targetGuid, senderName, channelName, 
                    playerRank, text, playerChatTag, DateTime.UtcNow);

                _incomingMessages.OnNext(messageData);

                _logger.LogDebug("Received {ChatType} message from {SenderName}: '{Text}'", chatType, senderName, text);

                // Handle special notifications
                HandleChatNotifications(chatType, text, senderName, channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling incoming chat message");
                _incomingMessages.OnError(ex);
            }
        }

        /// <summary>
        /// Handles chat notifications like channel joins, AFK mode changes, etc.
        /// </summary>
        private void HandleChatNotifications(ChatMsg chatType, string text, string senderName, string channelName)
        {
            ChatNotificationData? notification = null;

            switch (chatType)
            {
                case ChatMsg.CHAT_MSG_CHANNEL_JOIN:
                    notification = new ChatNotificationData(ChatNotificationType.ChannelJoined, text, senderName, channelName, DateTime.UtcNow);
                    break;

                case ChatMsg.CHAT_MSG_CHANNEL_LEAVE:
                    notification = new ChatNotificationData(ChatNotificationType.ChannelLeft, text, senderName, channelName, DateTime.UtcNow);
                    break;

                case ChatMsg.CHAT_MSG_AFK:
                    notification = new ChatNotificationData(ChatNotificationType.AfkMode, text, senderName, null, DateTime.UtcNow);
                    break;

                case ChatMsg.CHAT_MSG_DND:
                    notification = new ChatNotificationData(ChatNotificationType.DndMode, text, senderName, null, DateTime.UtcNow);
                    break;
            }

            if (notification != null)
            {
                _chatNotifications.OnNext(notification);
            }
        }

        #endregion

        #region Private Helper Methods

        private void SetOperationInProgress(bool inProgress)
        {
            lock (_stateLock)
            {
                _isChatOperationInProgress = inProgress;
                if (inProgress)
                {
                    _lastChatOperationTime = DateTime.UtcNow;
                }
            }
        }

        private async Task ApplyRateLimitingAsync(ChatMsg chatType, CancellationToken cancellationToken)
        {
            if (!_chatCooldowns.ContainsKey(chatType) || !_lastMessageTimes.ContainsKey(chatType))
                return;

            var timeSinceLastMessage = DateTime.UtcNow - _lastMessageTimes[chatType];
            var requiredCooldown = _chatCooldowns[chatType];

            if (timeSinceLastMessage < requiredCooldown)
            {
                var remainingCooldown = requiredCooldown - timeSinceLastMessage;
                _logger.LogDebug("Applying rate limiting for {ChatType}, waiting {RemainingMs}ms", chatType, remainingCooldown.TotalMilliseconds);
                
                await Task.Delay(remainingCooldown, cancellationToken);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes of the chat network agent and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing ChatNetworkClientComponent");

            // Complete all observables
            _incomingMessages.Dispose();
            _outgoingMessages.Dispose();
            _chatNotifications.Dispose();
            _executedCommands.Dispose();

            _disposed = true;
            _logger.LogDebug("ChatNetworkClientComponent disposed");
        }

        #endregion
    }
}