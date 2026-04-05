using BloogBot.AI.States;

namespace BloogBot.AI.Transitions;

/// <summary>
/// Thread-safe implementation of the forbidden transition registry.
/// Maintains a list of rules and provides efficient transition validation.
/// </summary>
public sealed class ForbiddenTransitionRegistry : IForbiddenTransitionRegistry
{
    private readonly List<ForbiddenTransitionRule> _rules = new();
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// Creates a new registry with default safety rules.
    /// </summary>
    public ForbiddenTransitionRegistry()
    {
        RegisterDefaultRules();
    }

    /// <summary>
    /// Creates a new registry without default rules.
    /// </summary>
    public static ForbiddenTransitionRegistry CreateEmpty() =>
        new(skipDefaultRules: true);

    private ForbiddenTransitionRegistry(bool skipDefaultRules)
    {
        if (!skipDefaultRules)
            RegisterDefaultRules();
    }

    private void RegisterDefaultRules()
    {
        // Combat cannot transition to social activities while aggressors present
        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "CombatToChattingBlocked",
            BotActivity.Combat,
            BotActivity.Chatting,
            ctx => ctx.ObjectManager?.Aggressors?.Any() == true,
            "Cannot chat while in active combat"));

        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "CombatToTradingBlocked",
            BotActivity.Combat,
            BotActivity.Trading,
            ctx => ctx.ObjectManager?.Aggressors?.Any() == true,
            "Cannot trade while in active combat"));

        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "CombatToMailingBlocked",
            BotActivity.Combat,
            BotActivity.Mailing,
            ctx => ctx.ObjectManager?.Aggressors?.Any() == true,
            "Cannot access mail while in active combat"));

        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "CombatToAuctionBlocked",
            BotActivity.Combat,
            BotActivity.Auction,
            ctx => ctx.ObjectManager?.Aggressors?.Any() == true,
            "Cannot use auction house while in active combat"));

        // Ghost form restrictions - only Resting is allowed
        RegisterRule(new ForbiddenTransitionRule(
            "GhostFormCombatBlocked",
            ForbiddenTransitionRule.Any,
            BotActivity.Combat,
            ctx => ctx.ObjectManager?.Player?.InGhostForm == true,
            "Cannot enter combat while in ghost form"));

        RegisterRule(new ForbiddenTransitionRule(
            "GhostFormGrindingBlocked",
            ForbiddenTransitionRule.Any,
            BotActivity.Grinding,
            ctx => ctx.ObjectManager?.Player?.InGhostForm == true,
            "Cannot grind while in ghost form - must recover first"));

        // Dungeon/Raid exit restrictions
        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "DungeonToGrindingBlocked",
            BotActivity.Dungeoning,
            BotActivity.Grinding,
            ctx => true, // Always blocked - must leave dungeon first
            "Cannot grind while in dungeon - must complete or leave instance"));

        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "RaidToGrindingBlocked",
            BotActivity.Raiding,
            BotActivity.Grinding,
            ctx => true,
            "Cannot grind while in raid - must complete or leave instance"));

        // Battleground restrictions
        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "BattlegroundToQuestingBlocked",
            BotActivity.Battlegrounding,
            BotActivity.Questing,
            ctx => ctx.ObjectManager?.Player?.InBattleground == true,
            "Cannot quest while in battleground"));

        RegisterRule(ForbiddenTransitionRule.BlockWhen(
            "BattlegroundToProfessionsBlocked",
            BotActivity.Battlegrounding,
            BotActivity.Professions,
            ctx => ctx.ObjectManager?.Player?.InBattleground == true,
            "Cannot use professions while in battleground"));
    }

    /// <inheritdoc />
    public TransitionCheckResult CheckTransition(BotActivity from, BotActivity to)
    {
        // Create a minimal context for non-predicate rules
        return CheckTransition(from, to, new TransitionContext(null!, null!, null));
    }

    /// <inheritdoc />
    public TransitionCheckResult CheckTransition(BotActivity from, BotActivity to, TransitionContext context)
    {
        _lock.EnterReadLock();
        try
        {
            foreach (var rule in _rules)
            {
                if (rule.Matches(from, to, context))
                {
                    return TransitionCheckResult.Forbidden(rule.HumanReadableReason, rule.RuleName);
                }
            }
            return TransitionCheckResult.Allowed();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void RegisterRule(ForbiddenTransitionRule rule)
    {
        _lock.EnterWriteLock();
        try
        {
            // Remove existing rule with same name
            _rules.RemoveAll(r => r.RuleName == rule.RuleName);
            _rules.Add(rule);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void RemoveRule(string ruleName)
    {
        _lock.EnterWriteLock();
        try
        {
            _rules.RemoveAll(r => r.RuleName == ruleName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void SetRuleEnabled(string ruleName, bool enabled)
    {
        _lock.EnterWriteLock();
        try
        {
            var rule = _rules.FirstOrDefault(r => r.RuleName == ruleName);
            if (rule != null)
            {
                rule.IsEnabled = enabled;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ForbiddenTransitionRule> GetAllRules()
    {
        _lock.EnterReadLock();
        try
        {
            return _rules.ToList().AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public ForbiddenTransitionRule? GetRule(string ruleName)
    {
        _lock.EnterReadLock();
        try
        {
            return _rules.FirstOrDefault(r => r.RuleName == ruleName);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
