using BloogBot.AI.States;

namespace BloogBot.AI.Transitions;

/// <summary>
/// Registry for forbidden transition rules.
/// Provides validation of state transitions and runtime rule management.
/// </summary>
public interface IForbiddenTransitionRegistry
{
    /// <summary>
    /// Checks if a transition from one activity to another is allowed.
    /// Uses default/empty context for predicate evaluation.
    /// </summary>
    TransitionCheckResult CheckTransition(BotActivity from, BotActivity to);

    /// <summary>
    /// Checks if a transition is allowed with full context for predicate evaluation.
    /// </summary>
    TransitionCheckResult CheckTransition(BotActivity from, BotActivity to, TransitionContext context);

    /// <summary>
    /// Registers a new rule. If a rule with the same name exists, it is replaced.
    /// </summary>
    void RegisterRule(ForbiddenTransitionRule rule);

    /// <summary>
    /// Removes a rule by name.
    /// </summary>
    void RemoveRule(string ruleName);

    /// <summary>
    /// Enables or disables a rule by name.
    /// </summary>
    void SetRuleEnabled(string ruleName, bool enabled);

    /// <summary>
    /// Gets all registered rules.
    /// </summary>
    IReadOnlyList<ForbiddenTransitionRule> GetAllRules();

    /// <summary>
    /// Gets a specific rule by name.
    /// </summary>
    ForbiddenTransitionRule? GetRule(string ruleName);
}
