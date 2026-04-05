using BloogBot.AI.States;

namespace BloogBot.AI.Advisory;

/// <summary>
/// Result after deterministic validation of an LLM advisory.
/// Contains both the original recommendation and the final decision.
/// </summary>
public sealed record AdvisoryResolution(
    LlmAdvisoryResult Original,
    bool WasOverridden,
    BotActivity FinalActivity,
    MinorState? FinalMinorState,
    string? OverrideReason,
    string? OverrideRule)
{
    /// <summary>
    /// Creates a resolution where the advisory was accepted without override.
    /// </summary>
    public static AdvisoryResolution Accepted(LlmAdvisoryResult advisory) =>
        new(
            advisory,
            WasOverridden: false,
            advisory.SuggestedActivity,
            advisory.SuggestedMinorState,
            null,
            null);

    /// <summary>
    /// Creates a resolution where the advisory was overridden.
    /// </summary>
    public static AdvisoryResolution Overridden(
        LlmAdvisoryResult advisory,
        BotActivity finalActivity,
        MinorState? finalMinorState,
        string reason,
        string ruleName) =>
        new(
            advisory,
            WasOverridden: true,
            finalActivity,
            finalMinorState,
            reason,
            ruleName);
}
