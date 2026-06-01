using System.Security.Cryptography;
using System.Text;
using PromptHandlingService.Foundry;

namespace PromptHandlingService.Storylines;

public sealed class StorylineFoundryInstructionBuilder
{
    private static readonly string[] DisallowedIntents =
    [
        "world-action",
        "state-transition",
        "trade",
        "combat",
        "mail",
        "invite"
    ];

    public string Build(StorylineFoundryInstructionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.PersonaProfile);
        ArgumentNullException.ThrowIfNull(source.PersonaVersion);
        ArgumentNullException.ThrowIfNull(source.Graph);

        var nodes = source.Nodes
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var transitions = source.Transitions
            .OrderBy(transition => transition.SortOrder)
            .ThenBy(transition => transition.TransitionId, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        AppendLine(builder, "task: storyline-persona-dialogue-advisory");
        AppendLine(builder, "boundary: advisory dialogue only; do not mutate state, select transitions, execute world actions, or imply that an action was performed.");
        AppendLine(builder, "runtimeContext: active node, compact memory summary, mood, and player input are supplied per request.");
        AppendLine(builder, "outputContract:");
        AppendLine(builder, $"  {PersonaPromptAssembler.OutputContract}");
        AppendLine(builder, "disallowedIntents:");
        foreach (var intent in DisallowedIntents.Order(StringComparer.Ordinal))
        {
            AppendLine(builder, $"  - {intent}");
        }

        AppendLine(builder, "persona:");
        AppendLine(builder, $"  id: {Normalize(source.PersonaProfile.PersonaId)}");
        AppendLine(builder, $"  displayName: {Normalize(source.PersonaProfile.DisplayName)}");
        AppendLine(builder, $"  description: {Normalize(source.PersonaProfile.Description)}");
        AppendLine(builder, "personaVersion:");
        AppendLine(builder, $"  id: {Normalize(source.PersonaVersion.PersonaVersionId)}");
        AppendLine(builder, $"  version: {Normalize(source.PersonaVersion.Version)}");
        AppendLine(builder, $"  summary: {Normalize(source.PersonaVersion.PromptSummary)}");
        AppendLine(builder, "graph:");
        AppendLine(builder, $"  id: {Normalize(source.Graph.GraphId)}");
        AppendLine(builder, $"  name: {Normalize(source.Graph.Name)}");
        AppendLine(builder, $"  description: {Normalize(source.Graph.Description)}");
        AppendLine(builder, "nodes:");
        foreach (var node in nodes)
        {
            AppendLine(builder, $"  - id: {Normalize(node.NodeId)}");
            AppendLine(builder, $"    title: {Normalize(node.Title)}");
            AppendLine(builder, $"    summary: {Normalize(node.Summary)}");
            AppendLine(builder, $"    fallbackReply: {Normalize(node.FallbackReply)}");
        }

        AppendLine(builder, "transitions:");
        foreach (var transition in transitions)
        {
            AppendLine(builder, $"  - id: {Normalize(transition.TransitionId)}");
            AppendLine(builder, $"    from: {Normalize(transition.FromNodeId)}");
            AppendLine(builder, $"    to: {Normalize(transition.ToNodeId)}");
            AppendLine(builder, $"    triggerKind: {Normalize(transition.TriggerKind)}");
            AppendLine(builder, $"    guard: {Normalize(transition.GuardExpression)}");
        }

        return builder.ToString();
    }

    public string ComputeContentHash(string instructions)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            return string.Empty;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(instructions));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        builder.Append(line).Append('\n');
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(none)";
        }

        return value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}

public sealed record StorylineFoundryInstructionSource(
    PersonaProfile PersonaProfile,
    PersonaVersion PersonaVersion,
    NarrativeGraph Graph,
    IReadOnlyList<NarrativeNode> Nodes,
    IReadOnlyList<NarrativeTransition> Transitions);
