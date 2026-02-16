using BloogBot.AI.Observable;
using BloogBot.AI.States;
using BloogBot.AI.Transitions;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BloogBot.AI.Advisory;

/// <summary>
/// Validates LLM advisory outputs against deterministic rules.
/// Deterministic logic always has final authority over LLM suggestions.
/// </summary>
public sealed class AdvisoryValidator : IAdvisoryValidator
{
    private readonly IForbiddenTransitionRegistry _forbiddenTransitions;
    private readonly IAdvisoryOverrideLog? _overrideLog;
    private readonly ILogger<AdvisoryValidator>? _logger;

    public AdvisoryValidator(
        IForbiddenTransitionRegistry forbiddenTransitions,
        IAdvisoryOverrideLog? overrideLog = null,
        ILogger<AdvisoryValidator>? logger = null)
    {
        _forbiddenTransitions = forbiddenTransitions ?? throw new ArgumentNullException(nameof(forbiddenTransitions));
        _overrideLog = overrideLog;
        _logger = logger;
    }

    /// <inheritdoc />
    public AdvisoryResolution Validate(
        LlmAdvisoryResult advisory,
        StateChangeEvent currentState,
        IObjectManager objectManager)
    {
        // Check 1: Forbidden transitions
        var transitionContext = new TransitionContext(currentState, objectManager, null);
        var forbiddenCheck = _forbiddenTransitions.CheckTransition(
            currentState.Activity,
            advisory.SuggestedActivity,
            transitionContext);

        if (!forbiddenCheck.IsAllowed)
        {
            return LogAndReturn(CreateOverride(
                advisory,
                currentState.Activity,
                currentState.MinorState,
                forbiddenCheck.Reason!,
                forbiddenCheck.RuleName!));
        }

        // Check 2: Combat safety - must enter combat if being attacked
        if (RequiresCombatOverride(advisory, objectManager))
        {
            return LogAndReturn(CreateOverride(
                advisory,
                BotActivity.Combat,
                MinorState.None(BotActivity.Combat),
                "Hostile units detected - combat takes priority",
                "CombatSafetyRule"));
        }

        // Check 3: Health safety - must rest if low health
        if (RequiresHealthOverride(advisory, objectManager))
        {
            return LogAndReturn(CreateOverride(
                advisory,
                BotActivity.Resting,
                MinorState.None(BotActivity.Resting),
                "Low health requires recovery",
                "HealthSafetyRule"));
        }

        // Check 4: Ghost form - must rest if dead
        if (RequiresGhostOverride(advisory, objectManager))
        {
            return LogAndReturn(CreateOverride(
                advisory,
                BotActivity.Resting,
                MinorState.None(BotActivity.Resting),
                "Ghost form requires resting to recover",
                "GhostSafetyRule"));
        }

        // Check 5: UI frame priority - active UI takes precedence
        var uiOverride = CheckUiFrameOverride(advisory, objectManager);
        if (uiOverride != null)
        {
            return LogAndReturn(uiOverride);
        }

        // Advisory accepted
        _logger?.LogDebug(
            "LLM advisory accepted: {Activity} (confidence: {Confidence:P0})",
            advisory.SuggestedActivity,
            advisory.Confidence);

        return AdvisoryResolution.Accepted(advisory);
    }

    private bool RequiresCombatOverride(LlmAdvisoryResult advisory, IObjectManager om)
    {
        if (advisory.SuggestedActivity == BotActivity.Combat)
            return false;

        var player = om.Player;
        if (player == null) return false;

        return om.Aggressors?.Any(a => a.TargetGuid == player.Guid) == true;
    }

    private bool RequiresHealthOverride(LlmAdvisoryResult advisory, IObjectManager om)
    {
        if (advisory.SuggestedActivity == BotActivity.Resting)
            return false;

        var player = om.Player;
        return player?.HealthPercent < 40;
    }

    private bool RequiresGhostOverride(LlmAdvisoryResult advisory, IObjectManager om)
    {
        if (advisory.SuggestedActivity == BotActivity.Resting)
            return false;

        return om.Player?.InGhostForm == true;
    }

    private AdvisoryResolution? CheckUiFrameOverride(LlmAdvisoryResult advisory, IObjectManager om)
    {
        // Trade frame takes priority
        if (om.TradeFrame?.IsOpen == true && advisory.SuggestedActivity != BotActivity.Trading)
        {
            return CreateOverride(
                advisory,
                BotActivity.Trading,
                MinorState.None(BotActivity.Trading),
                "Trade frame is open - must complete or cancel trade",
                "TradeFramePriorityRule");
        }

        // Quest frame takes priority
        if (om.QuestFrame?.IsOpen == true && advisory.SuggestedActivity != BotActivity.Questing)
        {
            return CreateOverride(
                advisory,
                BotActivity.Questing,
                MinorState.None(BotActivity.Questing),
                "Quest frame is open - must handle quest interaction",
                "QuestFramePriorityRule");
        }

        // Loot frame takes priority (complete combat cleanup)
        var lootFrame = om.LootFrame;
        if (lootFrame != null && (lootFrame.IsOpen || lootFrame.LootCount > 0) &&
            advisory.SuggestedActivity != BotActivity.Combat)
        {
            return CreateOverride(
                advisory,
                BotActivity.Combat,
                MinorState.None(BotActivity.Combat),
                "Loot available - must complete combat cleanup",
                "LootFramePriorityRule");
        }

        return null;
    }

    private AdvisoryResolution CreateOverride(
        LlmAdvisoryResult advisory,
        BotActivity overrideActivity,
        MinorState? overrideMinorState,
        string reason,
        string rule)
    {
        _logger?.LogInformation(
            "LLM advisory overridden: {Suggested} -> {Override}. Rule: {Rule}. Reason: {Reason}",
            advisory.SuggestedActivity,
            overrideActivity,
            rule,
            reason);

        return AdvisoryResolution.Overridden(
            advisory,
            overrideActivity,
            overrideMinorState,
            reason,
            rule);
    }

    private AdvisoryResolution LogAndReturn(AdvisoryResolution resolution)
    {
        if (resolution.WasOverridden)
        {
            _overrideLog?.LogOverride(resolution);
        }
        return resolution;
    }
}
