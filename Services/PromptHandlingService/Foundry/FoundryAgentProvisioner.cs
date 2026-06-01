using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;

namespace PromptHandlingService.Foundry;

public sealed class FoundryAgentProvisioner
{
    private readonly FoundryPersonaRuntimeOptions _options;
    private readonly AIProjectClient _projectClient;

    public FoundryAgentProvisioner(FoundryPersonaRuntimeOptions options)
        : this(options, new DefaultAzureCredential())
    {
    }

    public FoundryAgentProvisioner(FoundryPersonaRuntimeOptions options, Azure.Core.TokenCredential credential)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(credential);
        _options.Validate();
        _projectClient = new AIProjectClient(new Uri(_options.ProjectEndpoint), credential);
    }

    public async Task<FoundryAgentVersionInfo> EnsurePromptAgentVersionAsync(
        string instructions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new ArgumentException("Prompt-agent instructions are required.", nameof(instructions));
        }

        var definition = new DeclarativeAgentDefinition(_options.Model)
        {
            Instructions = instructions
        };

        var creationOptions = new ProjectsAgentVersionCreationOptions(definition)
        {
            Description = "WWoW persona dialogue advisory prompt agent."
        };
        creationOptions.Metadata["wwow-purpose"] = "persona-dialogue-advisory";

        var result = await _projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            _options.AgentName,
            creationOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new FoundryAgentVersionInfo(result.Value.Name, result.Value.Version, result.Value.Id);
    }
}

public sealed record FoundryAgentVersionInfo(string AgentName, string Version, string Id);
