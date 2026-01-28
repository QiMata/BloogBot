using BloogBot.AI.States;

namespace BloogBot.AI.Advisory;

/// <summary>
/// Represents an LLM's advisory output before deterministic validation.
/// This is the raw recommendation from the LLM, which may be overridden.
/// </summary>
public sealed record LlmAdvisoryResult(
    BotActivity SuggestedActivity,
    MinorState? SuggestedMinorState,
    string Reasoning,
    double Confidence,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Creates an advisory result with default timestamp.
    /// </summary>
    public static LlmAdvisoryResult Create(
        BotActivity activity,
        MinorState? minorState,
        string reasoning,
        double confidence = 0.5) =>
        new(activity, minorState, reasoning, confidence, DateTimeOffset.UtcNow);
}
