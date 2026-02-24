namespace BloogBot.AI.Summary;

/// <summary>
/// Result of the multi-pass summary distillation pipeline.
/// Provides both compact and detailed summaries optimized for downstream use.
/// </summary>
public sealed record DistilledSummary(
    string CompactSummary,
    string DetailedSummary,
    IReadOnlyList<string> KeyInsights,
    DateTimeOffset GeneratedAt,
    int PassCount)
{
    /// <summary>
    /// Maximum length for compact summaries.
    /// </summary>
    public const int MaxCompactLength = 200;

    /// <summary>
    /// Maximum length for detailed summaries.
    /// </summary>
    public const int MaxDetailedLength = 2000;

    /// <summary>
    /// Creates an empty summary (used when no context is available).
    /// </summary>
    public static DistilledSummary Empty() =>
        new(
            "No context available.",
            "No context available for summarization.",
            Array.Empty<string>(),
            DateTimeOffset.UtcNow,
            0);

    /// <summary>
    /// Creates a simple summary without multi-pass distillation.
    /// </summary>
    public static DistilledSummary Simple(string compact, string detailed) =>
        new(
            Truncate(compact, MaxCompactLength),
            Truncate(detailed, MaxDetailedLength),
            Array.Empty<string>(),
            DateTimeOffset.UtcNow,
            1);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
}
