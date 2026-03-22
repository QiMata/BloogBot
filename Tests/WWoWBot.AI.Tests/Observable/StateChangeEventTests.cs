using BloogBot.AI.Observable;
using BloogBot.AI.States;

namespace WWoWBot.AI.Tests.Observable;

public sealed class StateChangeEventTests
{
    [Fact]
    public void Constructor_MismatchedMinorState_ThrowsArgumentException()
    {
        var minorState = MinorState.None(BotActivity.Combat);
        Assert.Throws<ArgumentException>(() =>
            new StateChangeEvent(BotActivity.Resting, minorState, StateChangeSource.Deterministic, "test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_PreservesAllProperties()
    {
        var minorState = MinorState.None(BotActivity.Grinding);
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new StateChangeEvent(BotActivity.Grinding, minorState, StateChangeSource.Trigger, "combat started", timestamp, null, "TestRule");

        Assert.Equal(BotActivity.Grinding, evt.Activity);
        Assert.Equal(minorState, evt.MinorState);
        Assert.Equal(StateChangeSource.Trigger, evt.Source);
        Assert.Equal("combat started", evt.Reason);
        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Null(evt.PreviousState);
        Assert.Equal("TestRule", evt.RuleName);
    }

    [Fact]
    public void CreateInitial_DefaultsToResting()
    {
        var evt = StateChangeEvent.CreateInitial();

        Assert.Equal(BotActivity.Resting, evt.Activity);
        Assert.Equal("None", evt.MinorState.Name);
        Assert.Equal(StateChangeSource.Initialization, evt.Source);
        Assert.Null(evt.PreviousState);
    }

    [Fact]
    public void CreateInitial_WithSpecificActivity()
    {
        var evt = StateChangeEvent.CreateInitial(BotActivity.Grinding);
        Assert.Equal(BotActivity.Grinding, evt.Activity);
    }

    [Fact]
    public void DurationInPreviousState_NoPreviousState_ReturnsNull()
    {
        var evt = StateChangeEvent.CreateInitial();
        Assert.Null(evt.DurationInPreviousState);
    }

    [Fact]
    public void DurationInPreviousState_WithPreviousState_ReturnsTimeSpan()
    {
        var prev = new StateChangeEvent(BotActivity.Resting, MinorState.None(BotActivity.Resting),
            StateChangeSource.Initialization, "init", DateTimeOffset.UtcNow.AddSeconds(-10));
        var current = new StateChangeEvent(BotActivity.Grinding, MinorState.None(BotActivity.Grinding),
            StateChangeSource.Deterministic, "grinding", DateTimeOffset.UtcNow, prev);

        var duration = current.DurationInPreviousState;
        Assert.NotNull(duration);
        Assert.True(duration!.Value.TotalSeconds >= 9); // approximate
    }

    [Fact]
    public void IsActivityChange_DifferentActivity_ReturnsTrue()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Resting);
        var current = prev.WithActivity(BotActivity.Grinding, StateChangeSource.Deterministic, "grinding");

        Assert.True(current.IsActivityChange);
    }

    [Fact]
    public void IsActivityChange_SameActivity_ReturnsFalse()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Resting);
        var current = prev.WithMinorState(
            new MinorState(BotActivity.Resting, "Eating", "Eating food"),
            StateChangeSource.Deterministic, "eating");

        Assert.False(current.IsActivityChange);
    }

    [Fact]
    public void IsActivityChange_NoPreviousState_ReturnsFalse()
    {
        var evt = StateChangeEvent.CreateInitial();
        Assert.False(evt.IsActivityChange);
    }

    [Fact]
    public void IsMinorStateChange_SameActivityDifferentMinor_ReturnsTrue()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Combat);
        var current = prev.WithMinorState(
            new MinorState(BotActivity.Combat, "Engaging", "Engaging target"),
            StateChangeSource.Deterministic, "engaging");

        Assert.True(current.IsMinorStateChange);
    }

    [Fact]
    public void IsMinorStateChange_DifferentActivity_ReturnsFalse()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Resting);
        var current = prev.WithActivity(BotActivity.Combat, StateChangeSource.Deterministic, "combat");

        Assert.False(current.IsMinorStateChange);
    }

    [Fact]
    public void IsLlmSourced_LlmAdvisory_ReturnsTrue()
    {
        var evt = new StateChangeEvent(BotActivity.Grinding, MinorState.None(BotActivity.Grinding),
            StateChangeSource.LlmAdvisory, "llm suggested", DateTimeOffset.UtcNow);

        Assert.True(evt.IsLlmSourced);
    }

    [Fact]
    public void IsLlmSourced_LlmOverridden_ReturnsTrue()
    {
        var evt = new StateChangeEvent(BotActivity.Grinding, MinorState.None(BotActivity.Grinding),
            StateChangeSource.LlmOverridden, "llm overridden", DateTimeOffset.UtcNow);

        Assert.True(evt.IsLlmSourced);
    }

    [Fact]
    public void IsLlmSourced_Deterministic_ReturnsFalse()
    {
        var evt = new StateChangeEvent(BotActivity.Grinding, MinorState.None(BotActivity.Grinding),
            StateChangeSource.Deterministic, "deterministic", DateTimeOffset.UtcNow);

        Assert.False(evt.IsLlmSourced);
    }

    [Fact]
    public void WithMinorState_PreservesActivity_SetsPreviousState()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Combat);
        var minor = new MinorState(BotActivity.Combat, "Casting", "Casting spell");
        var current = prev.WithMinorState(minor, StateChangeSource.Deterministic, "casting");

        Assert.Equal(BotActivity.Combat, current.Activity);
        Assert.Equal("Casting", current.MinorState.Name);
        Assert.Same(prev, current.PreviousState);
    }

    [Fact]
    public void WithActivity_ChangesActivity_ResetMinorToNone()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Resting);
        var current = prev.WithActivity(BotActivity.Combat, StateChangeSource.Trigger, "attacked");

        Assert.Equal(BotActivity.Combat, current.Activity);
        Assert.Equal("None", current.MinorState.Name);
        Assert.Same(prev, current.PreviousState);
    }

    [Fact]
    public void WithActivity_IncludesRuleName()
    {
        var prev = StateChangeEvent.CreateInitial(BotActivity.Resting);
        var current = prev.WithActivity(BotActivity.Combat, StateChangeSource.TransitionBlocked, "blocked", "TestRule");

        Assert.Equal("TestRule", current.RuleName);
    }

    [Fact]
    public void ToString_IncludesActivityAndSource()
    {
        var evt = StateChangeEvent.CreateInitial(BotActivity.Grinding);
        var str = evt.ToString();

        Assert.Contains("Grinding", str);
        Assert.Contains("Initialization", str);
    }
}
