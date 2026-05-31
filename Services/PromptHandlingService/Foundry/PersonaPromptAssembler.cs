using System.Text;

namespace PromptHandlingService.Foundry;

public sealed class PersonaPromptAssembler
{
    public const string OutputContract =
        "Return only minified JSON with keys replyText, intent, memoryCandidates, rationale.";

    public string Assemble(PersonaPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequired(request.PersonaId, nameof(request.PersonaId));
        ValidateRequired(request.PersonaVersion, nameof(request.PersonaVersion));
        ValidateRequired(request.ActiveNarrativeNode, nameof(request.ActiveNarrativeNode));
        ValidateRequired(request.InputText, nameof(request.InputText));

        var builder = new StringBuilder();
        builder.AppendLine("task: persona-dialogue-advisory");
        builder.AppendLine("boundary: advisory text only; do not choose game-state transitions or world actions.");
        builder.AppendLine("persona:");
        builder.AppendLine($"  id: {Normalize(request.PersonaId)}");
        builder.AppendLine($"  version: {Normalize(request.PersonaVersion)}");
        builder.AppendLine($"  description: {Normalize(request.PersonaDescription)}");
        builder.AppendLine($"  promptSummary: {Normalize(request.PersonaPromptSummary)}");
        builder.AppendLine("character:");
        builder.AppendLine($"  name: {Normalize(request.CharacterName)}");
        builder.AppendLine($"  realm: {Normalize(request.Realm)}");
        builder.AppendLine("narrative:");
        builder.AppendLine($"  activeNode: {Normalize(request.ActiveNarrativeNode)}");
        builder.AppendLine("memory:");
        builder.AppendLine($"  compactSummary: {Normalize(request.CompactMemorySummary)}");
        builder.AppendLine("state:");
        builder.AppendLine($"  mood: {Normalize(request.CurrentMoodState)}");
        builder.AppendLine("input:");
        builder.AppendLine($"  text: {Normalize(request.InputText)}");
        builder.AppendLine("output:");
        builder.Append(OutputContract);
        return builder.ToString();
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(none)";
        }

        return value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
    }
}
