namespace PromptHandlingService.Foundry;

public interface IFoundryResponsesClient
{
    Task<FoundryResponseEnvelope> CreateResponseAsync(FoundryResponseRequest request, CancellationToken cancellationToken);
}

public sealed record FoundryResponseRequest
{
    public FoundryResponseRequest(string inputText, string model, int maxOutputTokens)
        : this(inputText, model, maxOutputTokens, string.Empty, null)
    {
    }

    public FoundryResponseRequest(
        string inputText,
        string model,
        int maxOutputTokens,
        string agentName,
        string? agentVersion)
    {
        InputText = inputText;
        Model = model;
        MaxOutputTokens = maxOutputTokens;
        AgentName = agentName;
        AgentVersion = agentVersion;
    }

    public string InputText { get; init; }
    public string Model { get; init; }
    public int MaxOutputTokens { get; init; }
    public string AgentName { get; init; }
    public string? AgentVersion { get; init; }
}

public sealed record FoundryResponseEnvelope(
    string OutputText,
    string Model,
    string? ResponseId);
