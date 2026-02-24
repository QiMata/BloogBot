using BloogBot.AI.Observable;
using GameData.Core.Interfaces;

namespace BloogBot.AI.Advisory;

/// <summary>
/// Contract for validating and potentially overriding LLM advisory outputs.
/// Deterministic logic always has final authority over LLM suggestions.
/// </summary>
public interface IAdvisoryValidator
{
    /// <summary>
    /// Validates an LLM advisory result against deterministic rules.
    /// Returns a resolution indicating whether the advisory was accepted or overridden.
    /// </summary>
    /// <param name="advisory">The LLM's recommendation.</param>
    /// <param name="currentState">Current state of the bot.</param>
    /// <param name="objectManager">Game object manager for context.</param>
    /// <returns>Resolution with final decision and override details if applicable.</returns>
    AdvisoryResolution Validate(
        LlmAdvisoryResult advisory,
        StateChangeEvent currentState,
        IObjectManager objectManager);
}
