using Microsoft.Extensions.Configuration;

namespace PromptHandlingService.Foundry;

public sealed class FoundryPersonaRuntimeOptions
{
    public const string SectionName = "Foundry:PersonaRuntime";

    public string ProjectEndpoint { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-5-mini";
    public string AgentName { get; init; } = "wwow-persona-runtime-dev";
    public string? AgentVersion { get; init; }
    public int TimeoutMs { get; init; } = 10_000;
    public int MaxOutputTokens { get; init; } = 512;

    public static FoundryPersonaRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var legacySection = configuration.GetSection("FoundryPersonaRuntime");

        string? ReadString(string key) =>
            section[key] ??
            legacySection[key] ??
            configuration[key];

        int ReadInt(string key, int defaultValue) =>
            int.TryParse(ReadString(key), out var value) ? value : defaultValue;

        return new FoundryPersonaRuntimeOptions
        {
            ProjectEndpoint = ReadString("projectEndpoint") ?? ReadString("ProjectEndpoint") ?? string.Empty,
            Model = ReadString("model") ?? ReadString("Model") ?? "gpt-5-mini",
            AgentName = ReadString("agentName") ?? ReadString("AgentName") ?? "wwow-persona-runtime-dev",
            AgentVersion = ReadString("agentVersion") ?? ReadString("AgentVersion"),
            TimeoutMs = ReadInt("timeoutMs", 10_000),
            MaxOutputTokens = ReadInt("maxOutputTokens", 512)
        };
    }

    public void Validate()
    {
        if (!Uri.TryCreate(ProjectEndpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("Foundry projectEndpoint must be an absolute URI.");
        }

        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Foundry projectEndpoint must use HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("Foundry model is required.");
        }

        if (string.IsNullOrWhiteSpace(AgentName))
        {
            throw new InvalidOperationException("Foundry agentName is required.");
        }

        if (TimeoutMs <= 0)
        {
            throw new InvalidOperationException("Foundry timeoutMs must be greater than zero.");
        }

        if (MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException("Foundry maxOutputTokens must be greater than zero.");
        }
    }
}
