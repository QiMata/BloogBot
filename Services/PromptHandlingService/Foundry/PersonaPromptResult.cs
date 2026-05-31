namespace PromptHandlingService.Foundry;

public sealed record PersonaPromptResult(
    string ReplyText,
    string Intent,
    IReadOnlyList<string> MemoryCandidates,
    string Rationale,
    string FoundryAgentName,
    string FoundryAgentVersion,
    string Model);
