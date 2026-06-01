namespace PromptHandlingService.Foundry;

public sealed record PersonaPromptRequest(
    string PersonaId,
    string PersonaVersion,
    string CharacterName,
    string Realm,
    string ActiveNarrativeNode,
    string CompactMemorySummary,
    string CurrentMoodState,
    string InputText,
    string PersonaDescription = "",
    string PersonaPromptSummary = "");
