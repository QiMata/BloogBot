using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Social;

/// <summary>
/// Tracks whisper conversation history per player.
/// Stores last N messages per sender for AI response generation.
/// Filters incoming SMSG_MESSAGECHAT by CHAT_MSG_WHISPER type.
/// </summary>
public class WhisperTracker
{
    public record WhisperMessage(string SenderName, string Text, DateTime Timestamp, bool IsIncoming);

    private readonly ConcurrentDictionary<string, List<WhisperMessage>> _history = new();
    private readonly int _maxMessagesPerPlayer;

    public WhisperTracker(int maxMessagesPerPlayer = 10)
    {
        _maxMessagesPerPlayer = maxMessagesPerPlayer;
    }

    /// <summary>Record an incoming whisper.</summary>
    public void RecordIncoming(string senderName, string text)
    {
        Record(senderName, text, isIncoming: true);
    }

    /// <summary>Record an outgoing whisper.</summary>
    public void RecordOutgoing(string recipientName, string text)
    {
        Record(recipientName, text, isIncoming: false);
    }

    private void Record(string playerName, string text, bool isIncoming)
    {
        var message = new WhisperMessage(playerName, text, DateTime.UtcNow, isIncoming);
        _history.AddOrUpdate(playerName,
            [message],
            (_, list) =>
            {
                list.Add(message);
                if (list.Count > _maxMessagesPerPlayer)
                    list.RemoveAt(0);
                return list;
            });
    }

    /// <summary>Get conversation history with a specific player.</summary>
    public IReadOnlyList<WhisperMessage> GetHistory(string playerName)
    {
        return _history.TryGetValue(playerName, out var list) ? list : [];
    }

    /// <summary>Get all players who have sent whispers.</summary>
    public IReadOnlyList<string> GetActiveConversations()
    {
        return _history.Keys.ToList();
    }

    /// <summary>Get the most recent unresponded whisper (for AI queue).</summary>
    public WhisperMessage? GetOldestUnrespondedWhisper()
    {
        foreach (var (_, messages) in _history)
        {
            var last = messages.LastOrDefault();
            if (last is { IsIncoming: true })
                return last;
        }
        return null;
    }

    /// <summary>Check if there are unread incoming whispers.</summary>
    public bool HasUnreadWhispers()
    {
        return _history.Values.Any(list => list.LastOrDefault()?.IsIncoming == true);
    }
}
