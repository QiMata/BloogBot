using BloogBot.AI.Observable;
using BloogBot.AI.States;

namespace WWoWBot.AI.Tests.Observable;

public sealed class BotStateObservableTests
{
    [Fact]
    public void Constructor_DefaultsToResting()
    {
        using var observable = new BotStateObservable();
        Assert.Equal(BotActivity.Resting, observable.CurrentState.Activity);
    }

    [Fact]
    public void Constructor_WithInitialActivity()
    {
        using var observable = new BotStateObservable(BotActivity.Grinding);
        Assert.Equal(BotActivity.Grinding, observable.CurrentState.Activity);
    }

    [Fact]
    public void PublishStateChange_UpdatesCurrentState()
    {
        using var observable = new BotStateObservable();
        observable.PublishStateChange(
            BotActivity.Combat,
            MinorState.None(BotActivity.Combat),
            StateChangeSource.Trigger,
            "combat started");

        Assert.Equal(BotActivity.Combat, observable.CurrentState.Activity);
    }

    [Fact]
    public void PublishStateChange_LinksPreviousState()
    {
        using var observable = new BotStateObservable();
        var initialState = observable.CurrentState;

        observable.PublishStateChange(
            BotActivity.Combat,
            MinorState.None(BotActivity.Combat),
            StateChangeSource.Trigger,
            "combat started");

        Assert.Equal(initialState, observable.CurrentState.PreviousState);
    }

    [Fact]
    public void PublishActivityChange_ResetsMinorStateToNone()
    {
        using var observable = new BotStateObservable(BotActivity.Combat);
        observable.PublishMinorStateChange(
            new MinorState(BotActivity.Combat, "Engaging", "targeting"),
            StateChangeSource.Deterministic, "engaging");

        observable.PublishActivityChange(BotActivity.Resting, StateChangeSource.Deterministic, "health low");

        Assert.Equal("None", observable.CurrentState.MinorState.Name);
    }

    [Fact]
    public void PublishMinorStateChange_UpdatesMinorState()
    {
        using var observable = new BotStateObservable(BotActivity.Combat);
        var minor = new MinorState(BotActivity.Combat, "Casting", "casting spell");

        observable.PublishMinorStateChange(minor, StateChangeSource.Deterministic, "casting");

        Assert.Equal("Casting", observable.CurrentState.MinorState.Name);
    }

    [Fact]
    public void PublishMinorStateChange_WrongActivity_Throws()
    {
        using var observable = new BotStateObservable(BotActivity.Combat);
        var wrongMinor = new MinorState(BotActivity.Resting, "Eating", "eating");

        Assert.Throws<InvalidOperationException>(() =>
            observable.PublishMinorStateChange(wrongMinor, StateChangeSource.Deterministic, "wrong"));
    }

    [Fact]
    public void StateChanges_NotifiesSubscribers()
    {
        using var observable = new BotStateObservable();
        var received = new List<StateChangeEvent>();

        using var sub = observable.StateChanges.Subscribe(e => received.Add(e));

        // BehaviorSubject replays current value immediately
        Assert.Single(received);

        observable.PublishActivityChange(BotActivity.Combat, StateChangeSource.Trigger, "attacked");

        Assert.Equal(2, received.Count);
        Assert.Equal(BotActivity.Combat, received[1].Activity);
    }

    [Fact]
    public void Dispose_CompletesStream()
    {
        var observable = new BotStateObservable();
        bool completed = false;

        observable.StateChanges.Subscribe(
            _ => { },
            () => completed = true);

        observable.Dispose();

        Assert.True(completed);
    }

    [Fact]
    public void Dispose_PublishAfterDispose_Ignored()
    {
        var observable = new BotStateObservable();
        var lastState = observable.CurrentState;
        observable.Dispose();

        // Should not throw, just ignored
        observable.PublishActivityChange(BotActivity.Combat, StateChangeSource.Trigger, "test");
    }

    [Fact]
    public void PublishActivityChange_IncludesRuleName()
    {
        using var observable = new BotStateObservable();
        observable.PublishActivityChange(
            BotActivity.Combat,
            StateChangeSource.TransitionBlocked,
            "blocked by rule",
            "TestRule");

        Assert.Equal("TestRule", observable.CurrentState.RuleName);
    }
}
