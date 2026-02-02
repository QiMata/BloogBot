namespace BloogBot.AI.Observable;

/// <summary>
/// Identifies the source that triggered a state change.
/// Used for auditing and understanding why state transitions occurred.
/// </summary>
public enum StateChangeSource
{
    /// <summary>
    /// State change was determined by game state evaluation logic.
    /// This is the most common source for reactive state changes.
    /// </summary>
    Deterministic,

    /// <summary>
    /// State change was triggered by an explicit trigger event.
    /// Examples: CombatStarted, QuestComplete, LowHealth.
    /// </summary>
    Trigger,

    /// <summary>
    /// State change was suggested by LLM advisory and accepted.
    /// The LLM recommendation was followed without override.
    /// </summary>
    LlmAdvisory,

    /// <summary>
    /// LLM suggested a state change but deterministic logic overrode it.
    /// The final state differs from what the LLM recommended.
    /// </summary>
    LlmOverridden,

    /// <summary>
    /// State change was requested manually by external system or user.
    /// </summary>
    Manual,

    /// <summary>
    /// State change occurred due to interval-based re-evaluation timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// State change occurred during system initialization.
    /// </summary>
    Initialization,

    /// <summary>
    /// State change was triggered by forbidden transition validation.
    /// The requested transition was blocked and an alternative was selected.
    /// </summary>
    TransitionBlocked
}
