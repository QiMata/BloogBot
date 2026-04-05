namespace BloogBot.AI.Advisory;

/// <summary>
/// Contract for logging LLM advisory overrides for auditing and analysis.
/// </summary>
public interface IAdvisoryOverrideLog
{
    /// <summary>
    /// Logs an override that occurred during advisory validation.
    /// </summary>
    void LogOverride(AdvisoryResolution resolution);

    /// <summary>
    /// Gets the most recent overrides.
    /// </summary>
    IReadOnlyList<AdvisoryResolution> GetRecentOverrides(int count = 100);

    /// <summary>
    /// Gets overrides that occurred due to a specific rule.
    /// </summary>
    IReadOnlyList<AdvisoryResolution> GetOverridesByRule(string ruleName);

    /// <summary>
    /// Gets the total count of overrides since tracking began.
    /// </summary>
    int TotalOverrideCount { get; }

    /// <summary>
    /// Gets the count of overrides grouped by rule name.
    /// </summary>
    IReadOnlyDictionary<string, int> GetOverrideCountsByRule();
}
