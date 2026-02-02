namespace BloogBot.AI.Transitions;

/// <summary>
/// Result of checking whether a transition is allowed.
/// </summary>
public sealed record TransitionCheckResult(
    bool IsAllowed,
    string? Reason,
    string? RuleName)
{
    /// <summary>
    /// Creates a result indicating the transition is allowed.
    /// </summary>
    public static TransitionCheckResult Allowed() =>
        new(true, null, null);

    /// <summary>
    /// Creates a result indicating the transition is forbidden.
    /// </summary>
    public static TransitionCheckResult Forbidden(string reason, string ruleName) =>
        new(false, reason, ruleName);
}
