using BotRunner.Tasks;

namespace BotRunner.Tests.Combat;

public class WaitTrackerTests
{
    [Fact]
    public void For_FirstCall_ReturnsTrue()
    {
        var tracker = new WaitTracker();
        Assert.True(tracker.For("test", 1000));
    }

    [Fact]
    public void For_SecondCallImmediately_ReturnsFalse()
    {
        var tracker = new WaitTracker();
        tracker.For("test", 5000);
        Assert.False(tracker.For("test", 5000));
    }

    [Fact]
    public void For_DifferentKeys_IndependentTimers()
    {
        var tracker = new WaitTracker();
        tracker.For("a", 5000);
        // Different key should return true (first call)
        Assert.True(tracker.For("b", 5000));
    }

    [Fact]
    public void For_ZeroMs_AlwaysReturnsTrue()
    {
        var tracker = new WaitTracker();
        Assert.True(tracker.For("test", 0));
        Assert.True(tracker.For("test", 0));
    }

    [Fact]
    public void Remove_RemovesKey_NextCallReturnsTrue()
    {
        var tracker = new WaitTracker();
        tracker.For("test", 5000);
        tracker.Remove("test");
        Assert.True(tracker.For("test", 5000));
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow()
    {
        var tracker = new WaitTracker();
        tracker.Remove("nonexistent");
    }

    [Fact]
    public void RemoveAll_ClearsAllKeys()
    {
        var tracker = new WaitTracker();
        tracker.For("a", 5000);
        tracker.For("b", 5000);
        tracker.For("c", 5000);

        tracker.RemoveAll();

        Assert.True(tracker.For("a", 5000));
        Assert.True(tracker.For("b", 5000));
        Assert.True(tracker.For("c", 5000));
    }

    [Fact]
    public void For_ResetOnSuccess_ResetsTimer()
    {
        var tracker = new WaitTracker();
        // First call sets timer
        tracker.For("test", 0, resetOnSuccess: true);
        // Immediately returns true (0ms wait), and resets
        Assert.True(tracker.For("test", 0, resetOnSuccess: true));
    }

    [Fact]
    public void For_ResetOnSuccessFalse_DoesNotResetTimer()
    {
        var tracker = new WaitTracker();
        // First call
        tracker.For("test", 0, resetOnSuccess: false);
        // Should still return true for 0ms wait
        Assert.True(tracker.For("test", 0, resetOnSuccess: false));
    }

    [Fact]
    public void For_LargeMilliseconds_ReturnsFalseOnSecondCall()
    {
        var tracker = new WaitTracker();
        tracker.For("test", int.MaxValue);
        Assert.False(tracker.For("test", int.MaxValue));
    }
}
