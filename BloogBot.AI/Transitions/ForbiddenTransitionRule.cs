using BloogBot.AI.States;

namespace BloogBot.AI.Transitions;

/// <summary>
/// Defines a forbidden transition rule with optional conditional predicate.
/// Rules can be explicit (specific from/to), use wildcards (null = any),
/// or include predicates for context-aware validation.
/// </summary>
public sealed class ForbiddenTransitionRule
{
    /// <summary>
    /// Unique name for this rule (used for logging and management).
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// The source activity. Null means "from any activity" (wildcard).
    /// </summary>
    public BotActivity? FromActivity { get; }

    /// <summary>
    /// The destination activity. Null means "to any activity" (wildcard).
    /// </summary>
    public BotActivity? ToActivity { get; }

    /// <summary>
    /// Optional predicate for context-aware evaluation.
    /// If null, the rule applies unconditionally when from/to match.
    /// If provided, the rule only applies when the predicate returns true.
    /// </summary>
    public Func<TransitionContext, bool>? Predicate { get; }

    /// <summary>
    /// Human-readable explanation of why this transition is forbidden.
    /// Used for logging and auditing.
    /// </summary>
    public string HumanReadableReason { get; }

    /// <summary>
    /// Whether this rule is currently enabled.
    /// Disabled rules are not evaluated.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Wildcard value for matching any activity.
    /// </summary>
    public static BotActivity? Any => null;

    public ForbiddenTransitionRule(
        string ruleName,
        BotActivity? fromActivity,
        BotActivity? toActivity,
        Func<TransitionContext, bool>? predicate,
        string humanReadableReason,
        bool isEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
            throw new ArgumentException("Rule name cannot be empty", nameof(ruleName));

        RuleName = ruleName;
        FromActivity = fromActivity;
        ToActivity = toActivity;
        Predicate = predicate;
        HumanReadableReason = humanReadableReason ?? string.Empty;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Checks if this rule matches the given transition.
    /// </summary>
    public bool Matches(BotActivity from, BotActivity to, TransitionContext context)
    {
        if (!IsEnabled)
            return false;

        // Check from activity (null = wildcard matches any)
        if (FromActivity.HasValue && FromActivity.Value != from)
            return false;

        // Check to activity (null = wildcard matches any)
        if (ToActivity.HasValue && ToActivity.Value != to)
            return false;

        // Check predicate if present
        if (Predicate != null && !Predicate(context))
            return false;

        return true;
    }

    /// <summary>
    /// Creates a simple rule that always blocks a specific transition.
    /// </summary>
    public static ForbiddenTransitionRule Block(
        string ruleName,
        BotActivity from,
        BotActivity to,
        string reason) =>
        new(ruleName, from, to, null, reason);

    /// <summary>
    /// Creates a conditional rule that blocks a transition when the predicate is true.
    /// </summary>
    public static ForbiddenTransitionRule BlockWhen(
        string ruleName,
        BotActivity? from,
        BotActivity? to,
        Func<TransitionContext, bool> predicate,
        string reason) =>
        new(ruleName, from, to, predicate, reason);

    /// <summary>
    /// Creates a rule that blocks any transition TO a specific activity.
    /// </summary>
    public static ForbiddenTransitionRule BlockAllTo(
        string ruleName,
        BotActivity to,
        string reason) =>
        new(ruleName, Any, to, null, reason);

    /// <summary>
    /// Creates a rule that blocks any transition FROM a specific activity.
    /// </summary>
    public static ForbiddenTransitionRule BlockAllFrom(
        string ruleName,
        BotActivity from,
        string reason) =>
        new(ruleName, from, Any, null, reason);

    public override string ToString() =>
        $"{RuleName}: {FromActivity?.ToString() ?? "*"} -> {ToActivity?.ToString() ?? "*"} " +
        $"({(IsEnabled ? "enabled" : "disabled")})";
}
