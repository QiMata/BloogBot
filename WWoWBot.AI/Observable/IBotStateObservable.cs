using BloogBot.AI.States;

namespace BloogBot.AI.Observable;

/// <summary>
/// Contract for the global state observable - single source of truth.
/// All consumers who need state updates should subscribe to this interface.
/// </summary>
public interface IBotStateObservable
{
    /// <summary>
    /// Observable stream of all state changes (both activity and minor state).
    /// This is the primary stream for consumers who need all updates.
    /// </summary>
    IObservable<StateChangeEvent> StateChanges { get; }

    /// <summary>
    /// Current state snapshot.
    /// Thread-safe access to the latest state without subscribing.
    /// </summary>
    StateChangeEvent CurrentState { get; }

    /// <summary>
    /// Observable filtered to emit only when the major activity changes.
    /// Does not emit for minor state changes within the same activity.
    /// </summary>
    IObservable<StateChangeEvent> ActivityChanged { get; }

    /// <summary>
    /// Observable that emits when the minor state changes.
    /// Includes both same-activity minor state changes and activity changes
    /// (since activity changes always involve a minor state change).
    /// </summary>
    IObservable<StateChangeEvent> MinorStateChanged { get; }

    /// <summary>
    /// Observable filtered to specific activity.
    /// Only emits when in the specified activity state.
    /// </summary>
    IObservable<StateChangeEvent> WhenActivity(BotActivity activity);

    /// <summary>
    /// Observable filtered to specific activity and minor state.
    /// Only emits when in the exact specified state combination.
    /// </summary>
    IObservable<StateChangeEvent> WhenState(BotActivity activity, MinorState minorState);
}
