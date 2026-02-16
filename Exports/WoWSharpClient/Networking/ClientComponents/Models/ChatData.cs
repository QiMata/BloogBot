using GameData.Core.Enums;
using System;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents data for an incoming chat message.
    /// </summary>
    public record ChatMessageData(
        ChatMsg ChatType,
        Language Language,
        ulong SenderGuid,
        ulong TargetGuid,
        string SenderName,
        string ChannelName,
        byte PlayerRank,
        string Text,
        PlayerChatTag PlayerChatTag,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for an outgoing chat message.
    /// </summary>
    public record OutgoingChatMessageData(
        ChatMsg ChatType,
        Language Language,
        string Destination,
        string Text,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for a chat notification.
    /// </summary>
    public record ChatNotificationData(
        ChatNotificationType NotificationType,
        string Message,
        string? PlayerName,
        string? ChannelName,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for an executed chat command.
    /// </summary>
    public record ChatCommandData(
        string Command,
        string[] Arguments,
        DateTime Timestamp);

    /// <summary>
    /// Types of chat notifications.
    /// </summary>
    public enum ChatNotificationType
    {
        ChannelJoined,
        ChannelLeft,
        ChannelModerated,
        ChannelUnmoderated,
        AfkMode,
        DndMode,
        PlayerJoined,
        PlayerLeft,
        PlayerBanned,
        PlayerUnbanned,
        MessageThrottled,
        CommandFailed,
        CommandSuccess
    }
}