using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Handles chat channel join/leave/message operations over the Mangos protocol.
    /// Exposes opcode-backed reactive observables for channel notifications.
    /// </summary>
    /// <remarks>
    /// This component handles the low-level channel opcodes (CMSG_JOIN_CHANNEL, CMSG_LEAVE_CHANNEL,
    /// SMSG_CHANNEL_NOTIFY). For sending messages to a channel, use <see cref="ChatNetworkClientComponent.ChannelAsync"/>
    /// which builds the CMSG_MESSAGECHAT with CHAT_MSG_CHANNEL type.
    /// </remarks>
    public class ChannelNetworkClientComponent : NetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ChannelNetworkClientComponent> _logger;
        private bool _disposed;

        // Reactive streams
        private readonly IObservable<ChannelNotification> _channelNotified;

        // Self-subscription to keep .Do() side effects active
        private readonly IDisposable _channelNotifiedSub;

        // Track joined channels
        private readonly HashSet<string> _joinedChannels = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _channelLock = new();

        public ChannelNetworkClientComponent(IWorldClient worldClient, ILogger<ChannelNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _channelNotified = SafeOpcodeStream(Opcode.SMSG_CHANNEL_NOTIFY)
                .Select(ParseChannelNotify)
                .Do(notification =>
                {
                    switch (notification.Type)
                    {
                        case ChannelNotifyType.YouJoined:
                            lock (_channelLock) { _joinedChannels.Add(notification.ChannelName); }
                            _logger.LogInformation("Joined channel: {Channel}", notification.ChannelName);
                            break;
                        case ChannelNotifyType.YouLeft:
                            lock (_channelLock) { _joinedChannels.Remove(notification.ChannelName); }
                            _logger.LogInformation("Left channel: {Channel}", notification.ChannelName);
                            break;
                        default:
                            _logger.LogDebug("Channel notification: Type={Type} Channel={Channel} Player={Player:X}",
                                notification.Type, notification.ChannelName, notification.PlayerGuid);
                            break;
                    }
                })
                .Publish().RefCount();

            _channelNotifiedSub = _channelNotified.Subscribe(_ => { });
        }

        #region State

        /// <summary>Returns the set of channels this client has joined.</summary>
        public IReadOnlyCollection<string> JoinedChannels
        {
            get
            {
                lock (_channelLock) { return new List<string>(_joinedChannels).AsReadOnly(); }
            }
        }

        /// <summary>Returns true if the client has joined the specified channel.</summary>
        public bool IsInChannel(string channelName)
        {
            lock (_channelLock) { return _joinedChannels.Contains(channelName); }
        }

        #endregion

        #region Reactive (public)

        /// <summary>Emits whenever SMSG_CHANNEL_NOTIFY arrives (join/leave/muted/etc. notifications).</summary>
        public IObservable<ChannelNotification> ChannelNotified => _channelNotified;

        #endregion

        #region Operations (CMSG)

        /// <summary>
        /// Sends CMSG_JOIN_CHANNEL to join a chat channel.
        /// </summary>
        /// <param name="channelName">Name of the channel to join.</param>
        /// <param name="password">Optional channel password.</param>
        public async Task JoinChannelAsync(string channelName, string password = "", CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Joining channel: {Channel}", channelName);

                // CMSG_JOIN_CHANNEL (1.12.1): uint32 channelId=0 + uint8 hasVoice=0 + uint8 hasVoice2=0 +
                //                              CString channelName + CString password
                var nameBytes = Encoding.UTF8.GetBytes(channelName);
                var passBytes = Encoding.UTF8.GetBytes(password ?? "");
                var payload = new byte[4 + 1 + 1 + nameBytes.Length + 1 + passBytes.Length + 1];

                int offset = 0;
                // uint32 channelId = 0
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), 0);
                offset += 4;
                // uint8 hasVoice = 0
                payload[offset++] = 0;
                // uint8 hasVoice2 = 0
                payload[offset++] = 0;
                // CString channelName (null-terminated)
                nameBytes.CopyTo(payload, offset);
                offset += nameBytes.Length;
                payload[offset++] = 0;
                // CString password (null-terminated)
                passBytes.CopyTo(payload, offset);
                offset += passBytes.Length;
                payload[offset] = 0;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_JOIN_CHANNEL, payload, cancellationToken);

                _logger.LogInformation("Join channel request sent: {Channel}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join channel: {Channel}", channelName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends CMSG_LEAVE_CHANNEL to leave a chat channel.
        /// </summary>
        /// <param name="channelName">Name of the channel to leave.</param>
        public async Task LeaveChannelAsync(string channelName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Leaving channel: {Channel}", channelName);

                // CMSG_LEAVE_CHANNEL: uint32 unk=0 + CString channelName
                var nameBytes = Encoding.UTF8.GetBytes(channelName);
                var payload = new byte[4 + nameBytes.Length + 1];

                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0);
                nameBytes.CopyTo(payload, 4);
                payload[4 + nameBytes.Length] = 0;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LEAVE_CHANNEL, payload, cancellationToken);

                _logger.LogInformation("Leave channel request sent: {Channel}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave channel: {Channel}", channelName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Sends a message to a chat channel using CMSG_MESSAGECHAT with CHAT_MSG_CHANNEL type.
        /// This is a convenience method; equivalent to ChatNetworkClientComponent.ChannelAsync().
        /// </summary>
        /// <param name="channelName">Target channel name.</param>
        /// <param name="message">Message text.</param>
        public async Task SendChannelMessageAsync(string channelName, string message, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Sending channel message to {Channel}: {Message}", channelName, message);

                // CMSG_MESSAGECHAT: uint32 chatType + uint32 language + CString channelName + CString message
                var channelBytes = Encoding.UTF8.GetBytes(channelName);
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var payload = new byte[4 + 4 + channelBytes.Length + 1 + messageBytes.Length + 1];

                int offset = 0;
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), (uint)ChatMsg.CHAT_MSG_CHANNEL);
                offset += 4;
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), (uint)Language.Common);
                offset += 4;
                channelBytes.CopyTo(payload, offset);
                offset += channelBytes.Length;
                payload[offset++] = 0;
                messageBytes.CopyTo(payload, offset);
                offset += messageBytes.Length;
                payload[offset] = 0;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MESSAGECHAT, payload, cancellationToken);

                _logger.LogInformation("Channel message sent to {Channel}", channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send channel message to {Channel}", channelName);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Parsing Helpers

        /// <summary>
        /// Parses SMSG_CHANNEL_NOTIFY (0x099).
        /// Format: uint8 notifyType + CString channelName + (varies by type, may include player GUID)
        /// </summary>
        private ChannelNotification ParseChannelNotify(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 2)
                    return new ChannelNotification(ChannelNotifyType.Joined, string.Empty, 0);

                int offset = 0;
                byte notifyType = span[offset++];

                // CString channelName
                int nullIdx = span[offset..].IndexOf((byte)0);
                string channelName = nullIdx > 0
                    ? Encoding.UTF8.GetString(span.Slice(offset, nullIdx))
                    : string.Empty;
                offset += (nullIdx >= 0 ? nullIdx : 0) + 1;

                // Some notification types include a player GUID after the channel name
                ulong playerGuid = 0;
                if (offset + 8 <= span.Length)
                {
                    playerGuid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
                }

                return new ChannelNotification((ChannelNotifyType)notifyType, channelName, playerGuid);
            }
            catch
            {
                return new ChannelNotification(ChannelNotifyType.Joined, string.Empty, 0);
            }
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _channelNotifiedSub?.Dispose();

            _logger.LogDebug("Disposing ChannelNetworkClientComponent");
            base.Dispose();
        }

        #endregion
    }

    #region Data Models

    /// <summary>Parsed SMSG_CHANNEL_NOTIFY data.</summary>
    public record ChannelNotification(ChannelNotifyType Type, string ChannelName, ulong PlayerGuid);

    /// <summary>Channel notification types from SMSG_CHANNEL_NOTIFY.</summary>
    public enum ChannelNotifyType : byte
    {
        Joined = 0,
        Left = 1,
        YouJoined = 2,
        YouLeft = 3,
        WrongPassword = 4,
        NotMember = 5,
        NotModerator = 6,
        PasswordChanged = 7,
        OwnerChanged = 8,
        PlayerNotFound = 9,
        NotOwner = 10,
        ChannelOwner = 11,
        ModeChange = 12,
        Announcements = 13,
        NotInLfg = 14,
        Already = 15,
        Invited = 16,
        InviteWrongFaction = 17
    }

    #endregion
}
