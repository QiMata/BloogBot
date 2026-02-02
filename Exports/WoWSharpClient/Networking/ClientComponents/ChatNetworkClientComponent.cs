using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of chat network agent that handles all chat operations in World of Warcraft.
    /// Provides specialized channels, commands, and reactive observables for chat events.
    /// Uses reactive observables from world client opcode streams (no Subjects or events).
    /// </summary>
    public class ChatNetworkClientComponent : NetworkClientComponent, IChatNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ChatNetworkClientComponent> _logger;
        private readonly object _stateLock = new();
        
        // Operation state tracking (chat-specific extras)
        private bool _isChatOperationInProgress;
        private DateTime? _lastChatOperationTime;
        
        // Player state
        private bool _isAfk;
        private bool _isDnd;
        private string? _afkMessage;
        private string? _dndMessage;
        
        // Active channels tracking (use dictionary as a set)
        private readonly ConcurrentDictionary<string, byte> _activeChannels = new(StringComparer.OrdinalIgnoreCase);
        
        // Reactive observables for chat events (built from world client opcode streams)
        private readonly IObservable<ChatMessageData> _incomingMessages;
        private readonly IObservable<OutgoingChatMessageData> _outgoingMessages;
        private readonly IObservable<ChatNotificationData> _chatNotifications;
        private readonly IObservable<ChatCommandData> _executedCommands;
        
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

            // Build incoming message observable from world client opcode stream
            var stream = _worldClient.RegisterOpcodeHandler(Opcode.SMSG_MESSAGECHAT);
            if (stream is not null)
            {
                _incomingMessages = stream
                    .Select(payload => ParseChatMessage(payload))
                    .Do(msg =>
                    {
                        _logger.LogDebug("Received {ChatType} message from {SenderName}: '{Text}'", msg.ChatType, msg.SenderName, msg.Text);
                        UpdateActiveChannels(msg.ChatType, msg.ChannelName);
                    })
                    .Publish()
                    .RefCount();
            }
            else
            {
                _incomingMessages = Observable.Empty<ChatMessageData>();
            }

            // Notifications derived from incoming messages
            _chatNotifications = _incomingMessages
                .Select(TryMapToNotification)
                .Where(n => n is not null)
                .Select(n => n!);

            // Outgoing messages: no Subjects/events; expose a never-ending stream (no pushes)
            _outgoingMessages = Observable.Never<OutgoingChatMessageData>();

            // Executed commands: no Subjects/events; expose a never-ending stream (no pushes)
            _executedCommands = Observable.Never<ChatCommandData>();

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

            _logger.LogDebug("ChatNetworkClientComponent initialized with opcode-backed observables");
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
                SetChatOperationInProgress(true);
                
                _logger.LogDebug("Sending {ChatType} message: '{Message}' to '{Destination}'", chatType, message, destination ?? "default");

                // Apply rate limiting
                await ApplyRateLimitingAsync(chatType, cancellationToken);

                // Send the message via world client
                await _worldClient.SendChatMessageAsync(chatType, language, destination ?? string.Empty, message, cancellationToken);

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
                SetChatOperationInProgress(false);
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
                SetChatOperationInProgress(true);
                
                _logger.LogDebug("Joining channel: {ChannelName}", channelName);
                
                await ExecuteCommandAsync("join", [channelName], cancellationToken);
                
                // Optimistically add to active channels (server response will confirm)
                _activeChannels[channelName] = 0;
                
                _logger.LogInformation("Joined channel: {ChannelName}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join channel: {ChannelName}", channelName);
                throw;
            }
            finally
            {
                SetChatOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task LeaveChannelAsync(string channelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(channelName));

            try
            {
                SetChatOperationInProgress(true);
                
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
                SetChatOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ListChannelAsync(string channelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(channelName));

            try
            {
                SetChatOperationInProgress(true);
                
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
                SetChatOperationInProgress(false);
            }
        }

        #endregion

        #region Player State Management

        /// <inheritdoc />
        public async Task SetAfkAsync(string? afkMessage = null, CancellationToken cancellationToken = default)
        {
            try
            {
                SetChatOperationInProgress(true);
                
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

                _logger.LogInformation("AFK status updated: {IsAfk}", _isAfk);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set AFK status");
                throw;
            }
            finally
            {
                SetChatOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SetDndAsync(string? dndMessage = null, CancellationToken cancellationToken = default)
        {
            try
            {
                SetChatOperationInProgress(true);
                
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

                _logger.LogInformation("DND status updated: {IsDnd}", _isDnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set DND status");
                throw;
            }
            finally
            {
                SetChatOperationInProgress(false);
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
                SetChatOperationInProgress(true);
                
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

                _logger.LogInformation("Successfully executed command: {Command}", command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command: {Command}", command);
                throw;
            }
            finally
            {
                SetChatOperationInProgress(false);
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
            return _activeChannels.Keys.ToList().AsReadOnly();
        }

        #endregion

        #region Public Methods for Server Response Handling

        /// <summary>
        /// Handles an incoming chat message from the server.
        /// This method remains for compatibility; incoming pipeline is opcode-backed.
        /// </summary>
        public void HandleIncomingMessage(ChatMsg chatType, Language language, ulong senderGuid, ulong targetGuid, 
            string senderName, string channelName, byte playerRank, string text, PlayerChatTag playerChatTag)
        {
            try
            {
                _logger.LogDebug("Compat incoming {ChatType} from {SenderName}: '{Text}'", chatType, senderName, text);
                UpdateActiveChannels(chatType, channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleIncomingMessage");
            }
        }

        /// <summary>
        /// Map chat types to high-level notifications.
        /// </summary>
        private static ChatNotificationData? TryMapToNotification(ChatMessageData msg)
        {
            return msg.ChatType switch
            {
                ChatMsg.CHAT_MSG_CHANNEL_JOIN => new ChatNotificationData(ChatNotificationType.ChannelJoined, msg.Text, msg.SenderName, msg.ChannelName, msg.Timestamp),
                ChatMsg.CHAT_MSG_CHANNEL_LEAVE => new ChatNotificationData(ChatNotificationType.ChannelLeft, msg.Text, msg.SenderName, msg.ChannelName, msg.Timestamp),
                ChatMsg.CHAT_MSG_AFK => new ChatNotificationData(ChatNotificationType.AfkMode, msg.Text, msg.SenderName, null, msg.Timestamp),
                ChatMsg.CHAT_MSG_DND => new ChatNotificationData(ChatNotificationType.DndMode, msg.Text, msg.SenderName, null, msg.Timestamp),
                _ => null
            };
        }

        private void UpdateActiveChannels(ChatMsg chatType, string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName)) return;

            switch (chatType)
            {
                case ChatMsg.CHAT_MSG_CHANNEL_JOIN:
                    _activeChannels[channelName] = 0;
                    break;
                case ChatMsg.CHAT_MSG_CHANNEL_LEAVE:
                    _activeChannels.TryRemove(channelName, out _);
                    break;
            }
        }

        #endregion

        #region Private Helper Methods

        private void SetChatOperationInProgress(bool inProgress)
        {
            // Update base operation state
            SetOperationInProgress(inProgress);

            // Update chat-specific state
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

        private static ChatMessageData ParseChatMessage(ReadOnlyMemory<byte> payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));

            ChatMsg chatType = (ChatMsg)reader.ReadByte();
            Language language = (Language)reader.ReadInt32();

            string senderName = string.Empty;
            string channelName = string.Empty;
            ulong senderGuid = 0;
            ulong targetGuid = 0;
            byte playerRank = 0;

            switch (chatType)
            {
                case ChatMsg.CHAT_MSG_MONSTER_WHISPER:
                case ChatMsg.CHAT_MSG_RAID_BOSS_WHISPER:
                case ChatMsg.CHAT_MSG_RAID_BOSS_EMOTE:
                case ChatMsg.CHAT_MSG_MONSTER_EMOTE:
                    reader.ReadUInt32(); // senderNameLength, discard
                    senderName = ReaderUtils.ReadCString(reader);
                    targetGuid = ReaderUtils.ReadPackedGuid(reader);
                    break;

                case ChatMsg.CHAT_MSG_SAY:
                case ChatMsg.CHAT_MSG_PARTY:
                case ChatMsg.CHAT_MSG_YELL:
                    senderGuid = reader.ReadUInt64();
                    reader.ReadUInt64(); // duplicate sender GUID, discard
                    break;

                case ChatMsg.CHAT_MSG_MONSTER_SAY:
                case ChatMsg.CHAT_MSG_MONSTER_YELL:
                    senderGuid = reader.ReadUInt64();
                    reader.ReadUInt32(); // senderNameLength, discard
                    senderName = ReaderUtils.ReadCString(reader);
                    targetGuid = reader.ReadUInt64();
                    break;

                case ChatMsg.CHAT_MSG_CHANNEL:
                    channelName = ReaderUtils.ReadCString(reader);
                    playerRank = (byte)reader.ReadUInt32(); // raw uint to byte
                    senderGuid = reader.ReadUInt64();
                    break;

                default:
                    senderGuid = reader.ReadUInt64();
                    break;
            }

            // Special case handling (e.g., swap sender/receiver for battleground messages)
            if (chatType == ChatMsg.CHAT_MSG_BG_SYSTEM_ALLIANCE || chatType == ChatMsg.CHAT_MSG_BG_SYSTEM_HORDE)
            {
                (senderGuid, targetGuid) = (targetGuid, senderGuid);
            }

            uint textLength = reader.ReadUInt32();
            string text = ReaderUtils.ReadString(reader, textLength);
            PlayerChatTag playerChatTag = (PlayerChatTag)reader.ReadByte();

            return new ChatMessageData(
                chatType,
                language,
                senderGuid,
                targetGuid,
                senderName,
                channelName,
                playerRank,
                text,
                playerChatTag,
                DateTime.UtcNow
            );
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

            _disposed = true;
            _logger.LogDebug("ChatNetworkClientComponent disposed");
        }

        #endregion
    }
}