namespace BloogBot.AI.Invocation;

/// <summary>
/// Contract for controlling decision invocation timing.
/// Supports automatic interval-based invocation and ad-hoc requests.
/// </summary>
public interface IDecisionInvoker : IDisposable
{
    /// <summary>
    /// Gets the currently configured interval between automatic invocations.
    /// </summary>
    TimeSpan CurrentInterval { get; }

    /// <summary>
    /// Gets the time remaining until the next automatic invocation.
    /// Returns TimeSpan.Zero if automatic invocation is disabled or paused.
    /// </summary>
    TimeSpan TimeUntilNextInvocation { get; }

    /// <summary>
    /// Gets whether automatic invocation is currently enabled.
    /// </summary>
    bool IsAutoInvocationEnabled { get; }

    /// <summary>
    /// Gets whether the invoker is currently paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Invokes decision logic immediately (ad-hoc invocation).
    /// If configured, this resets the interval timer.
    /// </summary>
    Task InvokeNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the interval between automatic invocations.
    /// The new interval is clamped to valid range.
    /// </summary>
    void SetInterval(TimeSpan interval);

    /// <summary>
    /// Pauses automatic invocations.
    /// Ad-hoc invocations are still allowed.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes automatic invocations.
    /// </summary>
    void Resume();

    /// <summary>
    /// Observable stream of decision invocation events.
    /// </summary>
    IObservable<DecisionInvocationEvent> Invocations { get; }
}
