using BloogBot.AI.States;

namespace BloogBot.AI.Observable;

/// <summary>
/// Immutable record representing a state change event.
/// This is the single source of truth for all state information,
/// providing a consistent snapshot for consumers.
/// </summary>
public sealed record StateChangeEvent
{
    /// <summary>
    /// The current major activity state (e.g., Combat, Questing, Resting).
    /// </summary>
    public BotActivity Activity { get; }

    /// <summary>
    /// The current minor state within the activity (e.g., Combat.Engaging).
    /// </summary>
    public MinorState MinorState { get; }

    /// <summary>
    /// What triggered this state change.
    /// </summary>
    public StateChangeSource Source { get; }

    /// <summary>
    /// Human-readable explanation of why this state change occurred.
    /// Useful for logging, debugging, and auditing.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// When this state change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The previous state, if available.
    /// Null for the initial state or when history is not tracked.
    /// </summary>
    public StateChangeEvent? PreviousState { get; }

    /// <summary>
    /// Optional name of the rule that caused this state (e.g., for forbidden transitions).
    /// </summary>
    public string? RuleName { get; }

    public StateChangeEvent(
        BotActivity activity,
        MinorState minorState,
        StateChangeSource source,
        string reason,
        DateTimeOffset timestamp,
        StateChangeEvent? previousState = null,
        string? ruleName = null)
    {
        if (minorState.ParentActivity != activity)
            throw new ArgumentException(
                $"Minor state '{minorState.Name}' belongs to {minorState.ParentActivity}, not {activity}",
                nameof(minorState));

        Activity = activity;
        MinorState = minorState;
        Source = source;
        Reason = reason ?? string.Empty;
        Timestamp = timestamp;
        PreviousState = previousState;
        RuleName = ruleName;
    }

    /// <summary>
    /// Calculates how long the system was in the previous state.
    /// Returns null if there is no previous state.
    /// </summary>
    public TimeSpan? DurationInPreviousState =>
        PreviousState != null ? Timestamp - PreviousState.Timestamp : null;

    /// <summary>
    /// Returns true if the major activity changed from the previous state.
    /// </summary>
    public bool IsActivityChange =>
        PreviousState != null && PreviousState.Activity != Activity;

    /// <summary>
    /// Returns true if only the minor state changed (same activity).
    /// </summary>
    public bool IsMinorStateChange =>
        PreviousState != null &&
        PreviousState.Activity == Activity &&
        !PreviousState.MinorState.Equals(MinorState);

    /// <summary>
    /// Returns true if this state change was caused by an LLM advisory.
    /// </summary>
    public bool IsLlmSourced =>
        Source == StateChangeSource.LlmAdvisory || Source == StateChangeSource.LlmOverridden;

    /// <summary>
    /// Creates a new state change with an updated minor state.
    /// Preserves the same activity but changes the minor state.
    /// </summary>
    public StateChangeEvent WithMinorState(
        MinorState newMinorState,
        StateChangeSource source,
        string reason) =>
        new(Activity, newMinorState, source, reason, DateTimeOffset.UtcNow, this);

    /// <summary>
    /// Creates a new state change with a new activity.
    /// Minor state defaults to None for the new activity.
    /// </summary>
    public StateChangeEvent WithActivity(
        BotActivity newActivity,
        StateChangeSource source,
        string reason,
        string? ruleName = null) =>
        new(newActivity, MinorState.None(newActivity), source, reason, DateTimeOffset.UtcNow, this, ruleName);

    /// <summary>
    /// Creates an initial state for system startup.
    /// </summary>
    public static StateChangeEvent CreateInitial(BotActivity activity = BotActivity.Resting) =>
        new(
            activity,
            MinorState.None(activity),
            StateChangeSource.Initialization,
            "System initialization",
            DateTimeOffset.UtcNow);

    /// <summary>
    /// Returns a string representation for logging.
    /// </summary>
    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}] {Activity}.{MinorState.Name} ({Source}: {Reason})";
}
