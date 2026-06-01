using PromptHandlingService.Storylines;

namespace PromptHandlingService.Foundry.Deployment;

public interface IStorylineFoundryDeploymentProvisioner
{
    Task<StorylineFoundryDeploymentProvisionResult> DeployAsync(
        StorylineFoundryDeploymentProvisionRequest request,
        CancellationToken cancellationToken);
}

public sealed class StorylineFoundryDeploymentProvisioner : IStorylineFoundryDeploymentProvisioner
{
    private readonly FoundryPersonaRuntimeOptions _options;

    public StorylineFoundryDeploymentProvisioner(FoundryPersonaRuntimeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<StorylineFoundryDeploymentProvisionResult> DeployAsync(
        StorylineFoundryDeploymentProvisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _options.Validate();

        var agentProvisioner = new FoundryAgentProvisioner(_options);
        var version = await agentProvisioner.EnsurePromptAgentVersionAsync(request.Instructions, cancellationToken)
            .ConfigureAwait(false);

        var runtime = new FoundryPersonaRuntime(_options);
        var binding = new PersonaPromptRuntimeBinding(
            request.Model,
            version.AgentName,
            version.Version,
            request.MaxOutputTokens);
        var smokeRequest = new PersonaPromptRequest(
            PersonaId: request.PersonaProfile.PersonaId,
            PersonaVersion: request.PersonaVersion.Version,
            CharacterName: "Storyline Deployment Smoke",
            Realm: "Westworld-Test",
            ActiveNarrativeNode: $"{request.Graph.GraphId}: deployment smoke check",
            CompactMemorySummary: "(none)",
            CurrentMoodState: "steady",
            InputText: "Reply with a short greeting only.",
            PersonaDescription: request.PersonaProfile.Description,
            PersonaPromptSummary: request.PersonaVersion.PromptSummary);

        var smokeResult = await runtime.GenerateAsync(smokeRequest, binding, cancellationToken)
            .ConfigureAwait(false);

        return new StorylineFoundryDeploymentProvisionResult(
            version.AgentName,
            version.Version,
            version.Id,
            smokeResult.Model);
    }
}

public sealed record StorylineFoundryDeploymentProvisionRequest(
    string Instructions,
    string Model,
    int MaxOutputTokens,
    PersonaProfile PersonaProfile,
    PersonaVersion PersonaVersion,
    NarrativeGraph Graph);

public sealed record StorylineFoundryDeploymentProvisionResult(
    string AgentName,
    string AgentVersion,
    string AgentVersionId,
    string Model);
