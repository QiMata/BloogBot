namespace BloogBot.AI.Summary;

/// <summary>
/// Contract for multi-step summarization pipeline.
/// Produces distilled summaries through multiple LLM passes.
/// </summary>
public interface ISummaryPipeline
{
    /// <summary>
    /// Produces a distilled summary through multiple LLM passes.
    /// The pipeline extracts key facts, compresses them, and generates final summaries.
    /// </summary>
    /// <param name="context">The context to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Distilled summary optimized for compactness and clarity.</returns>
    Task<DistilledSummary> DistillAsync(
        SummaryContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of LLM passes used by this pipeline.
    /// </summary>
    int PassCount { get; }
}
