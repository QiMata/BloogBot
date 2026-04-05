using BotRunner.Tasks.Social;

namespace BotRunner.Tests.Social;

public class WhisperTrackerTests
{
    [Fact]
    public void RecordIncoming_Stores()
    {
        var tracker = new WhisperTracker();
        tracker.RecordIncoming("PlayerOne", "hello");

        var history = tracker.GetHistory("PlayerOne");
        Assert.Single(history);
        Assert.True(history[0].IsIncoming);
        Assert.Equal("hello", history[0].Text);
    }

    [Fact]
    public void RecordOutgoing_Stores()
    {
        var tracker = new WhisperTracker();
        tracker.RecordOutgoing("PlayerOne", "hi back");

        var history = tracker.GetHistory("PlayerOne");
        Assert.Single(history);
        Assert.False(history[0].IsIncoming);
        Assert.Equal("hi back", history[0].Text);
    }

    [Fact]
    public void GetHistory_Ordered()
    {
        var tracker = new WhisperTracker();
        tracker.RecordIncoming("PlayerOne", "first");
        tracker.RecordOutgoing("PlayerOne", "second");
        tracker.RecordIncoming("PlayerOne", "third");

        var history = tracker.GetHistory("PlayerOne");
        Assert.Equal(3, history.Count);
        Assert.Equal("first", history[0].Text);
        Assert.Equal("second", history[1].Text);
        Assert.Equal("third", history[2].Text);
    }

    [Fact]
    public void HasUnreadWhispers_True_WhenIncoming()
    {
        var tracker = new WhisperTracker();
        tracker.RecordIncoming("PlayerOne", "hey");

        Assert.True(tracker.HasUnreadWhispers());
    }

    [Fact]
    public void HasUnreadWhispers_False_WhenLastIsOutgoing()
    {
        var tracker = new WhisperTracker();
        tracker.RecordIncoming("PlayerOne", "hey");
        tracker.RecordOutgoing("PlayerOne", "what's up");

        Assert.False(tracker.HasUnreadWhispers());
    }

    [Fact]
    public void MaxMessages_EvictsOldest()
    {
        var tracker = new WhisperTracker(maxMessagesPerPlayer: 3);
        tracker.RecordIncoming("PlayerOne", "msg1");
        tracker.RecordIncoming("PlayerOne", "msg2");
        tracker.RecordIncoming("PlayerOne", "msg3");
        tracker.RecordIncoming("PlayerOne", "msg4");

        var history = tracker.GetHistory("PlayerOne");
        Assert.Equal(3, history.Count);
        // Oldest ("msg1") should be evicted
        Assert.Equal("msg2", history[0].Text);
        Assert.Equal("msg4", history[2].Text);
    }

    [Fact]
    public void GetOldestUnrespondedWhisper()
    {
        var tracker = new WhisperTracker();
        tracker.RecordIncoming("PlayerOne", "need help");

        var whisper = tracker.GetOldestUnrespondedWhisper();
        Assert.NotNull(whisper);
        Assert.Equal("need help", whisper!.Text);
    }

    [Fact]
    public void GetOldestUnrespondedWhisper_Null_WhenAllResponded()
    {
        var tracker = new WhisperTracker();
        tracker.RecordIncoming("PlayerOne", "need help");
        tracker.RecordOutgoing("PlayerOne", "on my way");

        Assert.Null(tracker.GetOldestUnrespondedWhisper());
    }

    [Fact]
    public void GetHistory_ReturnsEmpty_ForUnknownPlayer()
    {
        var tracker = new WhisperTracker();

        var history = tracker.GetHistory("Nobody");
        Assert.Empty(history);
    }
}
