using PromptHandlingService.Foundry;
using PromptHandlingService.Foundry.Deployment;

namespace PromptHandlingService.Storylines;

public sealed class StorylineFoundryDeploymentService : IStorylineFoundryDeploymentService
{
    private readonly IStorylineRepository _repository;
    private readonly StorylineFoundryInstructionBuilder _instructionBuilder;
    private readonly IStorylineFoundryDeploymentProvisioner _provisioner;
    private readonly IStorylineFoundryDeploymentQueue _queue;
    private readonly FoundryPersonaRuntimeOptions _options;

    public StorylineFoundryDeploymentService(
        IStorylineRepository repository,
        StorylineFoundryInstructionBuilder instructionBuilder,
        IStorylineFoundryDeploymentProvisioner provisioner,
        IStorylineFoundryDeploymentQueue queue,
        FoundryPersonaRuntimeOptions options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _instructionBuilder = instructionBuilder ?? throw new ArgumentNullException(nameof(instructionBuilder));
        _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<FoundryDeploymentPreviewDto> PreviewDeploymentAsync(
        FoundryDeploymentTargetRequest request,
        CancellationToken cancellationToken)
    {
        var target = Normalize(request);
        var errors = new List<ValidationErrorDto>();
        Require(target.PersonaId, nameof(target.PersonaId), errors);
        Require(target.PersonaVersionId, nameof(target.PersonaVersionId), errors);
        Require(target.GraphId, nameof(target.GraphId), errors);

        PersonaProfile? profile = null;
        PersonaVersion? version = null;
        NarrativeGraph? graph = null;
        IReadOnlyList<NarrativeNode> nodes = Array.Empty<NarrativeNode>();
        IReadOnlyList<NarrativeTransition> transitions = Array.Empty<NarrativeTransition>();

        if (errors.Count == 0)
        {
            profile = await _repository.GetPersonaProfileAsync(target.PersonaId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                errors.Add(Error(nameof(target.PersonaId), "missing_persona", $"Persona '{target.PersonaId}' does not exist."));
            }

            version = await _repository.GetPersonaVersionAsync(target.PersonaId, target.PersonaVersionId, cancellationToken)
                .ConfigureAwait(false);
            if (version is null)
            {
                errors.Add(Error(nameof(target.PersonaVersionId), "missing_persona_version", $"Persona version '{target.PersonaVersionId}' does not exist for persona '{target.PersonaId}'."));
            }

            graph = await _repository.GetNarrativeGraphAsync(target.GraphId, cancellationToken).ConfigureAwait(false);
            if (graph is null)
            {
                errors.Add(Error(nameof(target.GraphId), "missing_graph", $"Narrative graph '{target.GraphId}' does not exist."));
            }
        }

        if (errors.Count == 0 && graph is not null)
        {
            nodes = await _repository.ListNarrativeNodesAsync(target.GraphId, cancellationToken).ConfigureAwait(false);
            transitions = await _repository.ListNarrativeTransitionsAsync(target.GraphId, cancellationToken).ConfigureAwait(false);
            if (nodes.Count == 0)
            {
                errors.Add(Error("nodes", "missing_nodes", $"Narrative graph '{target.GraphId}' has no nodes."));
            }
        }

        var instructions = string.Empty;
        var contentHash = string.Empty;
        if (errors.Count == 0 && profile is not null && version is not null && graph is not null)
        {
            instructions = _instructionBuilder.Build(new StorylineFoundryInstructionSource(
                profile,
                version,
                graph,
                nodes,
                transitions));
            contentHash = _instructionBuilder.ComputeContentHash(instructions);
        }

        var latest = await _repository.GetLatestFoundryDeploymentAsync(
                target.PersonaId,
                target.PersonaVersionId,
                target.GraphId,
                cancellationToken).ConfigureAwait(false);

        return new FoundryDeploymentPreviewDto(
            errors.Count == 0,
            target.PersonaId,
            target.PersonaVersionId,
            target.GraphId,
            errors,
            contentHash,
            instructions,
            _options.Model,
            _options.AgentName,
            _options.MaxOutputTokens,
            latest is null ? null : ToDto(latest));
    }

    public async Task<FoundryDeploymentDto> QueueDeploymentAsync(
        FoundryDeploymentTargetRequest request,
        CancellationToken cancellationToken)
    {
        var preview = await PreviewDeploymentAsync(request, cancellationToken).ConfigureAwait(false);
        if (!preview.IsValid)
        {
            throw new StorylineFoundryDeploymentValidationException(preview);
        }

        var now = DateTime.UtcNow;
        var deployment = new StorylineFoundryDeployment
        {
            DeploymentId = Guid.NewGuid().ToString("N"),
            PersonaId = preview.PersonaId,
            PersonaVersionId = preview.PersonaVersionId,
            GraphId = preview.GraphId,
            Status = StorylineFoundryDeploymentStatus.Queued,
            ContentHash = preview.ContentHash,
            Instructions = preview.Instructions,
            Model = preview.Model,
            AgentName = preview.AgentName,
            MaxOutputTokens = preview.MaxOutputTokens,
            RequestedBy = NormalizeOptional(request.RequestedBy),
            RequestedAtUtc = now
        };

        await _repository.UpsertFoundryDeploymentAsync(deployment, cancellationToken).ConfigureAwait(false);
        await _queue.QueueAsync(deployment.DeploymentId, cancellationToken).ConfigureAwait(false);
        return ToDto(deployment);
    }

    public async Task<FoundryDeploymentDto?> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
    {
        var deployment = await _repository.GetFoundryDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        return deployment is null ? null : ToDto(deployment);
    }

    public async Task<FoundryDeploymentDto?> RunDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
    {
        var deployment = await _repository.GetFoundryDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        if (deployment is null)
        {
            return null;
        }

        if (!string.Equals(deployment.Status, StorylineFoundryDeploymentStatus.Queued, StringComparison.Ordinal))
        {
            return ToDto(deployment);
        }

        var running = deployment with
        {
            Status = StorylineFoundryDeploymentStatus.Running,
            StartedAtUtc = DateTime.UtcNow,
            ErrorText = string.Empty
        };
        await _repository.UpsertFoundryDeploymentAsync(running, cancellationToken).ConfigureAwait(false);

        try
        {
            var profile = await _repository.GetPersonaProfileAsync(running.PersonaId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Persona '{running.PersonaId}' does not exist.");
            var version = await _repository.GetPersonaVersionAsync(running.PersonaId, running.PersonaVersionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Persona version '{running.PersonaVersionId}' does not exist.");
            var graph = await _repository.GetNarrativeGraphAsync(running.GraphId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Narrative graph '{running.GraphId}' does not exist.");

            var result = await _provisioner.DeployAsync(
                new StorylineFoundryDeploymentProvisionRequest(
                    running.Instructions,
                    running.Model,
                    running.MaxOutputTokens,
                    profile,
                    version,
                    graph),
                cancellationToken).ConfigureAwait(false);

            var succeeded = running with
            {
                Status = StorylineFoundryDeploymentStatus.Succeeded,
                AgentName = result.AgentName,
                AgentVersion = result.AgentVersion,
                AgentVersionId = result.AgentVersionId,
                Model = result.Model,
                CompletedAtUtc = DateTime.UtcNow,
                ErrorText = string.Empty
            };
            await _repository.UpsertFoundryDeploymentAsync(succeeded, cancellationToken).ConfigureAwait(false);
            return ToDto(succeeded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failed = running with
            {
                Status = StorylineFoundryDeploymentStatus.Failed,
                CompletedAtUtc = DateTime.UtcNow,
                ErrorText = ex.Message
            };
            await _repository.UpsertFoundryDeploymentAsync(failed, CancellationToken.None).ConfigureAwait(false);
            return ToDto(failed);
        }
    }

    public async Task<FoundryDeploymentDto?> PromoteDeploymentAsync(
        string deploymentId,
        PromoteFoundryDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var deployment = await _repository.GetFoundryDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        if (deployment is null)
        {
            return null;
        }

        if (!string.Equals(deployment.Status, StorylineFoundryDeploymentStatus.Succeeded, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Foundry deployment '{deploymentId}' must be Succeeded before it can be promoted.");
        }

        if (string.IsNullOrWhiteSpace(deployment.AgentVersion))
        {
            throw new InvalidOperationException($"Foundry deployment '{deploymentId}' does not have a generated agent version.");
        }

        var now = DateTime.UtcNow;
        await _repository.PromoteAgentBindingAsync(new AgentBinding
        {
            BindingId = $"foundry-{deployment.DeploymentId}",
            PersonaId = deployment.PersonaId,
            PersonaVersionId = deployment.PersonaVersionId,
            GraphId = deployment.GraphId,
            Model = deployment.Model,
            AgentName = deployment.AgentName,
            AgentVersion = deployment.AgentVersion,
            MaxOutputTokens = deployment.MaxOutputTokens,
            IsDefault = true,
            CreatedAtUtc = now
        }, cancellationToken).ConfigureAwait(false);

        var promoted = deployment with
        {
            Status = StorylineFoundryDeploymentStatus.Promoted,
            PromotedBy = NormalizeOptional(request.PromotedBy),
            PromotedAtUtc = now
        };
        await _repository.UpsertFoundryDeploymentAsync(promoted, cancellationToken).ConfigureAwait(false);
        return ToDto(promoted);
    }

    private static FoundryDeploymentTargetRequest Normalize(FoundryDeploymentTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request with
        {
            PersonaId = NormalizeOptional(request.PersonaId),
            PersonaVersionId = NormalizeOptional(request.PersonaVersionId),
            GraphId = NormalizeOptional(request.GraphId),
            RequestedBy = NormalizeOptional(request.RequestedBy)
        };
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static void Require(string value, string fieldPath, List<ValidationErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Error(fieldPath, "required", $"{fieldPath} is required."));
        }
    }

    private static ValidationErrorDto Error(string fieldPath, string code, string message) => new(fieldPath, code, message);

    private static FoundryDeploymentDto ToDto(StorylineFoundryDeployment deployment) => new(
        deployment.DeploymentId,
        deployment.PersonaId,
        deployment.PersonaVersionId,
        deployment.GraphId,
        deployment.Status,
        deployment.ContentHash,
        deployment.Model,
        deployment.AgentName,
        deployment.AgentVersion,
        deployment.AgentVersionId,
        deployment.MaxOutputTokens,
        deployment.RequestedBy,
        deployment.RequestedAtUtc,
        deployment.StartedAtUtc,
        deployment.CompletedAtUtc,
        deployment.PromotedBy,
        deployment.PromotedAtUtc,
        deployment.ErrorText);
}

public sealed class StorylineFoundryDeploymentValidationException : InvalidOperationException
{
    public StorylineFoundryDeploymentValidationException(FoundryDeploymentPreviewDto preview)
        : base("Foundry deployment target is invalid.")
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
    }

    public FoundryDeploymentPreviewDto Preview { get; }
}
