using System.Text;
using Microsoft.Extensions.Logging;

namespace BloogBot.AI.Summary;

/// <summary>
/// Multi-pass summarization pipeline that distills context through LLM passes.
/// Pass 1: Extract key facts from raw context
/// Pass 2: Compress and prioritize facts
/// Pass 3: Generate final compact and detailed summaries
/// </summary>
public sealed class SummaryPipeline : ISummaryPipeline
{
    private readonly Func<string, string, CancellationToken, Task<string>> _llmInvoke;
    private readonly ILogger<SummaryPipeline>? _logger;

    /// <inheritdoc />
    public int PassCount => 3;

    /// <summary>
    /// Creates a new SummaryPipeline with the specified LLM invocation function.
    /// </summary>
    /// <param name="llmInvoke">Function that takes (system prompt, user prompt) and returns LLM response.</param>
    /// <param name="logger">Optional logger.</param>
    public SummaryPipeline(
        Func<string, string, CancellationToken, Task<string>> llmInvoke,
        ILogger<SummaryPipeline>? logger = null)
    {
        _llmInvoke = llmInvoke ?? throw new ArgumentNullException(nameof(llmInvoke));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DistilledSummary> DistillAsync(
        SummaryContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.RecentStates.Count == 0 && context.RecentChatMessages.Count == 0)
        {
            return DistilledSummary.Empty();
        }

        try
        {
            // Pass 1: Extract key facts
            _logger?.LogDebug("Summary pipeline: Starting Pass 1 (extraction)");
            var rawFacts = await ExtractKeyFactsAsync(context, cancellationToken);

            // Pass 2: Compress and prioritize
            _logger?.LogDebug("Summary pipeline: Starting Pass 2 (compression)");
            var compressedFacts = await CompressAndPrioritizeAsync(rawFacts, cancellationToken);

            // Pass 3: Generate final summaries
            _logger?.LogDebug("Summary pipeline: Starting Pass 3 (synthesis)");
            var (compact, detailed, insights) = await GenerateFinalSummariesAsync(
                compressedFacts, context, cancellationToken);

            _logger?.LogInformation("Summary pipeline completed: {CompactLength} chars compact, {DetailedLength} chars detailed",
                compact.Length, detailed.Length);

            return new DistilledSummary(
                compact,
                detailed,
                insights,
                DateTimeOffset.UtcNow,
                PassCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Summary pipeline failed, returning fallback summary");
            return CreateFallbackSummary(context);
        }
    }

    private async Task<string> ExtractKeyFactsAsync(SummaryContext context, CancellationToken ct)
    {
        var systemPrompt = @"You are extracting key facts from game state data.
Extract only the most important facts that would be relevant for decision-making.
Output each fact on its own line, prefixed with a category tag.
Categories: [STATE], [COMBAT], [QUEST], [SOCIAL], [LOCATION], [RESOURCE]
Be concise - one sentence per fact maximum.";

        var userPrompt = BuildExtractionPrompt(context);

        return await _llmInvoke(systemPrompt, userPrompt, ct);
    }

    private async Task<string> CompressAndPrioritizeAsync(string rawFacts, CancellationToken ct)
    {
        var systemPrompt = @"You are compressing and prioritizing extracted facts.
Your task:
1. Remove any redundant or duplicate information
2. Combine related facts where possible
3. Order facts by importance (most important first)
4. Remove any facts that are trivial or not actionable
Output the compressed facts, one per line.
Aim for no more than 10 key facts.";

        return await _llmInvoke(systemPrompt, rawFacts, ct);
    }

    private async Task<(string compact, string detailed, IReadOnlyList<string> insights)> GenerateFinalSummariesAsync(
        string compressedFacts,
        SummaryContext context,
        CancellationToken ct)
    {
        var systemPrompt = @"You are generating summaries for a game AI decision system.
Based on the provided facts, generate:
1. COMPACT: A single-line summary (max 200 chars) capturing the current situation
2. DETAILED: A structured summary (max 2000 chars) with sections for state, goals, and context
3. INSIGHTS: 3-5 key insights that would help with decision-making

Format your response as:
COMPACT: [your compact summary]
DETAILED:
[your detailed summary]
INSIGHTS:
- [insight 1]
- [insight 2]
- [insight 3]";

        var userPrompt = $"Character: {context.Character.Name} (Level {context.Character.Level} {context.Character.Class})\n" +
                         $"Zone: {context.Character.Zone}\n\n" +
                         $"Compressed Facts:\n{compressedFacts}";

        var response = await _llmInvoke(systemPrompt, userPrompt, ct);

        return ParseFinalResponse(response);
    }

    private string BuildExtractionPrompt(SummaryContext context)
    {
        var sb = new StringBuilder();

        // Character info
        sb.AppendLine($"Character: {context.Character.Name}");
        sb.AppendLine($"Class: {context.Character.Class}, Level: {context.Character.Level}");
        sb.AppendLine($"Health: {context.Character.HealthPercent}%, Mana: {context.Character.ManaPercent}%");
        sb.AppendLine($"Zone: {context.Character.Zone} / {context.Character.SubZone}");
        sb.AppendLine($"In Combat: {context.Character.InCombat}, In Party: {context.Character.InParty}");
        sb.AppendLine();

        // Recent states
        if (context.RecentStates.Count > 0)
        {
            sb.AppendLine("Recent State Changes:");
            foreach (var state in context.RecentStates.TakeLast(10))
            {
                sb.AppendLine($"  [{state.Timestamp:HH:mm:ss}] {state.Activity}.{state.MinorState.Name} - {state.Reason}");
            }
            sb.AppendLine();
        }

        // Quests
        if (context.ActiveQuests.Count > 0)
        {
            sb.AppendLine("Active Quests:");
            foreach (var quest in context.ActiveQuests)
            {
                sb.AppendLine($"  {quest.QuestName}: {quest.CurrentObjective} ({quest.ObjectivesCompleted}/{quest.ObjectivesTotal})");
            }
            sb.AppendLine();
        }

        // Environment
        sb.AppendLine($"Nearby Hostiles: {context.Environment.NearbyHostileCount}");
        sb.AppendLine($"Nearby Friendlies: {context.Environment.NearbyFriendlyCount}");
        if (context.Environment.CurrentTarget != null)
            sb.AppendLine($"Current Target: {context.Environment.CurrentTarget}");
        sb.AppendLine();

        // Chat
        if (context.RecentChatMessages.Count > 0)
        {
            sb.AppendLine("Recent Chat:");
            foreach (var msg in context.RecentChatMessages.TakeLast(5))
            {
                sb.AppendLine($"  {msg}");
            }
        }

        return sb.ToString();
    }

    private (string compact, string detailed, IReadOnlyList<string> insights) ParseFinalResponse(string response)
    {
        var compact = "Current state summary unavailable.";
        var detailed = response;
        var insights = new List<string>();

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Parse COMPACT
        var compactLine = lines.FirstOrDefault(l => l.StartsWith("COMPACT:", StringComparison.OrdinalIgnoreCase));
        if (compactLine != null)
        {
            compact = compactLine.Substring("COMPACT:".Length).Trim();
            if (compact.Length > DistilledSummary.MaxCompactLength)
                compact = compact.Substring(0, DistilledSummary.MaxCompactLength - 3) + "...";
        }

        // Parse DETAILED
        var detailedStart = Array.FindIndex(lines, l => l.StartsWith("DETAILED:", StringComparison.OrdinalIgnoreCase));
        var insightsStart = Array.FindIndex(lines, l => l.StartsWith("INSIGHTS:", StringComparison.OrdinalIgnoreCase));

        if (detailedStart >= 0 && insightsStart > detailedStart)
        {
            detailed = string.Join("\n", lines.Skip(detailedStart + 1).Take(insightsStart - detailedStart - 1));
            if (detailed.Length > DistilledSummary.MaxDetailedLength)
                detailed = detailed.Substring(0, DistilledSummary.MaxDetailedLength - 3) + "...";
        }

        // Parse INSIGHTS
        if (insightsStart >= 0)
        {
            insights = lines
                .Skip(insightsStart + 1)
                .Where(l => l.TrimStart().StartsWith("-"))
                .Select(l => l.TrimStart().TrimStart('-').Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }

        return (compact, detailed, insights);
    }

    private DistilledSummary CreateFallbackSummary(SummaryContext context)
    {
        var compact = $"{context.Character.Name} ({context.Character.Level} {context.Character.Class}) in {context.Character.Zone}";
        var detailed = $"Character: {context.Character.Name}\n" +
                       $"Level {context.Character.Level} {context.Character.Class}\n" +
                       $"Location: {context.Character.Zone} / {context.Character.SubZone}\n" +
                       $"Health: {context.Character.HealthPercent}%\n" +
                       $"Active quests: {context.ActiveQuests.Count}\n" +
                       $"Nearby hostiles: {context.Environment.NearbyHostileCount}";

        return new DistilledSummary(
            compact,
            detailed,
            Array.Empty<string>(),
            DateTimeOffset.UtcNow,
            0);
    }
}
