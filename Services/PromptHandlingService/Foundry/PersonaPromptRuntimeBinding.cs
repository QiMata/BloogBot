namespace PromptHandlingService.Foundry;

public sealed record PersonaPromptRuntimeBinding(
    string Model,
    string AgentName,
    string? AgentVersion,
    int MaxOutputTokens)
{
    public static PersonaPromptRuntimeBinding FromOptions(FoundryPersonaRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new PersonaPromptRuntimeBinding(
            options.Model,
            options.AgentName,
            options.AgentVersion,
            options.MaxOutputTokens);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("Foundry runtime binding model is required.");
        }

        if (string.IsNullOrWhiteSpace(AgentName))
        {
            throw new InvalidOperationException("Foundry runtime binding agentName is required.");
        }

        if (MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException("Foundry runtime binding maxOutputTokens must be greater than zero.");
        }
    }
}
