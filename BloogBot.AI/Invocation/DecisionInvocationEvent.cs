namespace BloogBot.AI.Invocation;

/// <summary>
/// Event raised when a decision invocation occurs.
/// </summary>
public sealed record DecisionInvocationEvent(
    DateTimeOffset Timestamp,
    DecisionInvocationType Type,
    TimeSpan? TimeSinceLastInvocation);

/// <summary>
/// Type of decision invocation.
/// </summary>
public enum DecisionInvocationType
{
    /// <summary>Invocation triggered by the automatic timer.</summary>
    Automatic,

    /// <summary>Invocation triggered by ad-hoc request (InvokeNowAsync).</summary>
    AdHoc,

    /// <summary>Invocation triggered by manual/external request.</summary>
    Manual
}
