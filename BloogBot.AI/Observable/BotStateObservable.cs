using System.Reactive.Linq;
using System.Reactive.Subjects;
using BloogBot.AI.States;

namespace BloogBot.AI.Observable;

/// <summary>
/// Implementation of the global state observable.
/// Thread-safe, provides replay of latest state to new subscribers via BehaviorSubject.
/// This is the single source of truth for all state changes in the system.
/// </summary>
public sealed class BotStateObservable : IBotStateObservable, IDisposable
{
    private readonly BehaviorSubject<StateChangeEvent> _stateSubject;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new BotStateObservable with the specified initial activity.
    /// </summary>
    public BotStateObservable(BotActivity initialActivity = BotActivity.Resting)
    {
        var initialState = StateChangeEvent.CreateInitial(initialActivity);
        _stateSubject = new BehaviorSubject<StateChangeEvent>(initialState);
    }

    /// <inheritdoc />
    public IObservable<StateChangeEvent> StateChanges =>
        _stateSubject.AsObservable();

    /// <inheritdoc />
    public StateChangeEvent CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _stateSubject.Value;
            }
        }
    }

    /// <inheritdoc />
    public IObservable<StateChangeEvent> ActivityChanged =>
        StateChanges
            .DistinctUntilChanged(e => e.Activity)
            .Skip(1); // Skip initial state to only get changes

    /// <inheritdoc />
    public IObservable<StateChangeEvent> MinorStateChanged =>
        StateChanges
            .DistinctUntilChanged(e => (e.Activity, e.MinorState.Name))
            .Skip(1); // Skip initial state

    /// <inheritdoc />
    public IObservable<StateChangeEvent> WhenActivity(BotActivity activity) =>
        StateChanges.Where(e => e.Activity == activity);

    /// <inheritdoc />
    public IObservable<StateChangeEvent> WhenState(BotActivity activity, MinorState minorState) =>
        StateChanges.Where(e => e.Activity == activity && e.MinorState.Equals(minorState));

    /// <summary>
    /// Publishes a new state change event to all subscribers.
    /// Thread-safe operation that links the new state to the previous state.
    /// </summary>
    /// <param name="activity">The new activity state.</param>
    /// <param name="minorState">The new minor state.</param>
    /// <param name="source">What triggered this change.</param>
    /// <param name="reason">Human-readable reason for the change.</param>
    /// <param name="ruleName">Optional rule name if applicable.</param>
    public void PublishStateChange(
        BotActivity activity,
        MinorState minorState,
        StateChangeSource source,
        string reason,
        string? ruleName = null)
    {
        lock (_lock)
        {
            if (_disposed) return;

            var previousState = _stateSubject.Value;
            var newState = new StateChangeEvent(
                activity,
                minorState,
                source,
                reason,
                DateTimeOffset.UtcNow,
                previousState,
                ruleName);

            _stateSubject.OnNext(newState);
        }
    }

    /// <summary>
    /// Publishes a minor state change within the current activity.
    /// </summary>
    public void PublishMinorStateChange(
        MinorState minorState,
        StateChangeSource source,
        string reason)
    {
        lock (_lock)
        {
            if (_disposed) return;

            var current = _stateSubject.Value;
            if (minorState.ParentActivity != current.Activity)
            {
                throw new InvalidOperationException(
                    $"Cannot set minor state '{minorState.Name}' (for {minorState.ParentActivity}) " +
                    $"while in activity {current.Activity}");
            }

            var newState = current.WithMinorState(minorState, source, reason);
            _stateSubject.OnNext(newState);
        }
    }

    /// <summary>
    /// Publishes an activity change, resetting minor state to None.
    /// </summary>
    public void PublishActivityChange(
        BotActivity activity,
        StateChangeSource source,
        string reason,
        string? ruleName = null)
    {
        lock (_lock)
        {
            if (_disposed) return;

            var current = _stateSubject.Value;
            var newState = current.WithActivity(activity, source, reason, ruleName);
            _stateSubject.OnNext(newState);
        }
    }

    /// <summary>
    /// Releases all resources and completes the observable stream.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _stateSubject.OnCompleted();
            _stateSubject.Dispose();
        }
    }
}
