using System.Text.Json;

namespace PromptHandlingService.Storylines;

public sealed class StorylineManagementService : IStorylineManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IStorylineRepository _repository;
    private readonly IStorylineActivityCatalog _activityCatalog;

    public StorylineManagementService(
        IStorylineRepository repository,
        IStorylineActivityCatalog activityCatalog)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activityCatalog = activityCatalog ?? throw new ArgumentNullException(nameof(activityCatalog));
    }

    public async Task<StorylineHealthDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        var personaCount = await _repository.CountPersonasAsync(cancellationToken).ConfigureAwait(false);
        var graphCount = await _repository.CountNarrativeGraphsAsync(cancellationToken).ConfigureAwait(false);
        return new StorylineHealthDto("ok", _repository.DatabasePath, personaCount, graphCount);
    }

    public async Task<IReadOnlyList<PersonaProfileDto>> ListPersonasAsync(CancellationToken cancellationToken)
    {
        var profiles = await _repository.ListPersonaProfilesAsync(cancellationToken).ConfigureAwait(false);
        return profiles.Select(ToDto).ToArray();
    }

    public async Task<PersonaProfileDto?> GetPersonaAsync(string personaId, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetPersonaProfileAsync(personaId, cancellationToken).ConfigureAwait(false);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<IReadOnlyList<NarrativeGraphDto>> ListNarrativeGraphsAsync(CancellationToken cancellationToken)
    {
        var graphs = await _repository.ListNarrativeGraphsAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<NarrativeGraphDto>(graphs.Count);
        foreach (var graph in graphs)
        {
            result.Add(await BuildGraphDtoAsync(graph, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<NarrativeGraphDto?> GetNarrativeGraphAsync(string graphId, CancellationToken cancellationToken)
    {
        var graph = await _repository.GetNarrativeGraphAsync(graphId, cancellationToken).ConfigureAwait(false);
        return graph is null ? null : await BuildGraphDtoAsync(graph, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameplayStoryArcDto>> ListGameplayArcsAsync(CancellationToken cancellationToken)
    {
        var arcs = await _repository.ListGameplayArcsAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<GameplayStoryArcDto>(arcs.Count);
        foreach (var arc in arcs)
        {
            var steps = await _repository.ListGameplayArcStepsAsync(arc.ArcId, arc.VersionId, cancellationToken)
                .ConfigureAwait(false);
            result.Add(ToDto(arc, steps));
        }

        return result;
    }

    public async Task<IReadOnlyList<CharacterStoryBindingDto>> ListCharacterBindingsAsync(CancellationToken cancellationToken)
    {
        var bindings = await _repository.ListCharacterStoryBindingsAsync(cancellationToken).ConfigureAwait(false);
        return bindings.Select(ToDto).ToArray();
    }

    public async Task<CharacterStoryBindingDto?> GetCharacterBindingAsync(string characterId, CancellationToken cancellationToken)
    {
        var binding = await _repository.GetCharacterStoryBindingAsync(characterId, cancellationToken).ConfigureAwait(false);
        return binding is null ? null : ToDto(binding);
    }

    public async Task<IReadOnlyList<StorylineDraftDto>> ListDraftsAsync(string? kind, string? status, CancellationToken cancellationToken)
    {
        var drafts = await _repository.ListStorylineDraftsAsync(kind, status, cancellationToken).ConfigureAwait(false);
        return drafts.Select(ToDto).ToArray();
    }

    public async Task<StorylineDraftDto?> GetDraftAsync(string draftId, CancellationToken cancellationToken)
    {
        var draft = await _repository.GetStorylineDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        return draft is null ? null : ToDto(draft);
    }

    public async Task<StorylineDraftDto> SaveDraftAsync(StorylineDraftDto draft, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var row = new StorylineDraft
        {
            DraftId = string.IsNullOrWhiteSpace(draft.DraftId) ? Guid.NewGuid().ToString("N") : draft.DraftId.Trim(),
            Kind = draft.Kind.Trim(),
            TargetId = draft.TargetId.Trim(),
            Status = string.IsNullOrWhiteSpace(draft.Status) ? StorylineDraftStatus.Draft : draft.Status.Trim(),
            PayloadJson = draft.PayloadJson,
            CreatedAtUtc = draft.CreatedAtUtc == default ? now : draft.CreatedAtUtc,
            UpdatedAtUtc = now,
            PublishedAtUtc = draft.PublishedAtUtc,
            PublishedBy = draft.PublishedBy,
            PublishMessage = draft.PublishMessage
        };

        await _repository.UpsertStorylineDraftAsync(row, cancellationToken).ConfigureAwait(false);
        return ToDto(row);
    }

    public async Task<PublishDraftResultDto> PublishDraftAsync(
        string draftId,
        PublishDraftRequest request,
        CancellationToken cancellationToken)
    {
        var draft = await _repository.GetStorylineDraftAsync(draftId, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            return Failed(draftId, string.Empty, string.Empty, Error("draftId", "not_found", $"Draft '{draftId}' was not found."));
        }

        var errors = new List<ValidationErrorDto>();
        switch (draft.Kind)
        {
            case StorylineDraftKind.PersonaProfile:
                await PublishPersonaProfileAsync(draft, errors, cancellationToken).ConfigureAwait(false);
                break;
            case StorylineDraftKind.PersonaVersion:
                await PublishPersonaVersionAsync(draft, errors, cancellationToken).ConfigureAwait(false);
                break;
            case StorylineDraftKind.NarrativeGraph:
                await PublishNarrativeGraphAsync(draft, errors, cancellationToken).ConfigureAwait(false);
                break;
            case StorylineDraftKind.GameplayArc:
                await PublishGameplayArcAsync(draft, errors, cancellationToken).ConfigureAwait(false);
                break;
            case StorylineDraftKind.CharacterBinding:
                await PublishCharacterBindingAsync(draft, errors, cancellationToken).ConfigureAwait(false);
                break;
            default:
                errors.Add(Error("kind", "unsupported", $"Draft kind '{draft.Kind}' is not supported."));
                break;
        }

        if (errors.Count > 0)
        {
            return new PublishDraftResultDto(false, draft.DraftId, draft.Kind, draft.TargetId, errors);
        }

        await _repository.MarkStorylineDraftPublishedAsync(
            draft.DraftId,
            DateTime.UtcNow,
            request.PublishedBy.Trim(),
            request.Message.Trim(),
            cancellationToken).ConfigureAwait(false);

        return new PublishDraftResultDto(true, draft.DraftId, draft.Kind, draft.TargetId, Array.Empty<ValidationErrorDto>());
    }

    public async Task<IReadOnlyList<MemoryCandidateDto>> ListMemoryCandidatesAsync(
        string characterId,
        string status,
        CancellationToken cancellationToken)
    {
        var candidates = await _repository.GetMemoryCandidatesAsync(characterId, status, cancellationToken).ConfigureAwait(false);
        return candidates.Select(ToDto).ToArray();
    }

    public async Task<MemoryCandidateDto?> ReviewMemoryCandidateAsync(
        string candidateId,
        MemoryCandidateReviewDto review,
        CancellationToken cancellationToken)
    {
        var status = review.Status.Trim();
        if (!string.Equals(status, StorylineMemoryStatus.Approved, StringComparison.Ordinal) &&
            !string.Equals(status, StorylineMemoryStatus.Rejected, StringComparison.Ordinal))
        {
            throw new ArgumentException("Memory candidate review status must be Approved or Rejected.", nameof(review));
        }

        var candidate = await _repository.GetMemoryCandidateAsync(candidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return null;
        }

        await _repository.UpdateMemoryCandidateStatusAsync(candidateId, status, cancellationToken).ConfigureAwait(false);
        if (string.Equals(status, StorylineMemoryStatus.Approved, StringComparison.Ordinal))
        {
            await _repository.UpsertMemoryFactAsync(new MemoryFact
            {
                FactId = $"candidate-{candidate.CandidateId}",
                CharacterId = candidate.CharacterId,
                PersonaId = candidate.PersonaId,
                Text = candidate.CandidateText,
                Importance = 5,
                Status = StorylineMemoryStatus.Approved,
                CreatedAtUtc = DateTime.UtcNow,
                ApprovedAtUtc = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        return ToDto(candidate with { Status = status });
    }

    public Task<IReadOnlyList<ActivityCatalogItemDto>> ListActivityCatalogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_activityCatalog.List());
    }

    public async Task<GraphLayoutDto?> GetGraphLayoutAsync(string graphId, CancellationToken cancellationToken)
    {
        var layout = await _repository.GetGraphLayoutAsync(graphId, cancellationToken).ConfigureAwait(false);
        return layout is null ? null : FromLayoutJson(layout);
    }

    public async Task<GraphLayoutDto> SaveGraphLayoutAsync(GraphLayoutDto layout, CancellationToken cancellationToken)
    {
        ValidateRequired(layout.GraphId, nameof(layout.GraphId));
        var now = DateTime.UtcNow;
        var dto = layout with { UpdatedAtUtc = now };
        await _repository.UpsertGraphLayoutAsync(new GraphLayout
        {
            GraphId = dto.GraphId,
            LayoutJson = JsonSerializer.Serialize(dto, JsonOptions),
            UpdatedAtUtc = now
        }, cancellationToken).ConfigureAwait(false);
        return dto;
    }

    private async Task PublishPersonaProfileAsync(
        StorylineDraft draft,
        List<ValidationErrorDto> errors,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<PersonaProfileDto>(draft, errors);
        if (dto is null)
        {
            return;
        }

        Require(dto.PersonaId, "personaId", errors);
        Require(dto.DisplayName, "displayName", errors);
        RequireTarget(draft, dto.PersonaId, errors);
        if (errors.Count > 0)
        {
            return;
        }

        await _repository.UpsertPersonaProfileAsync(new PersonaProfile
        {
            PersonaId = dto.PersonaId,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            CreatedAtUtc = dto.CreatedAtUtc == default ? DateTime.UtcNow : dto.CreatedAtUtc
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishPersonaVersionAsync(
        StorylineDraft draft,
        List<ValidationErrorDto> errors,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<PersonaVersionDto>(draft, errors);
        if (dto is null)
        {
            return;
        }

        Require(dto.PersonaVersionId, "personaVersionId", errors);
        Require(dto.PersonaId, "personaId", errors);
        Require(dto.Version, "version", errors);
        RequireTarget(draft, dto.PersonaVersionId, errors);
        if (!string.IsNullOrWhiteSpace(dto.PersonaId) &&
            await _repository.GetPersonaProfileAsync(dto.PersonaId, cancellationToken).ConfigureAwait(false) is null)
        {
            errors.Add(Error("personaId", "missing_persona", $"Persona '{dto.PersonaId}' does not exist."));
        }

        if (errors.Count > 0)
        {
            return;
        }

        await _repository.UpsertPersonaVersionAsync(new PersonaVersion
        {
            PersonaVersionId = dto.PersonaVersionId,
            PersonaId = dto.PersonaId,
            Version = dto.Version,
            PromptSummary = dto.PromptSummary,
            IsActive = dto.IsActive,
            CreatedAtUtc = dto.CreatedAtUtc == default ? DateTime.UtcNow : dto.CreatedAtUtc
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishNarrativeGraphAsync(
        StorylineDraft draft,
        List<ValidationErrorDto> errors,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<NarrativeGraphDto>(draft, errors);
        if (dto is null)
        {
            return;
        }

        Require(dto.GraphId, "graphId", errors);
        Require(dto.Name, "name", errors);
        RequireTarget(draft, dto.GraphId, errors);
        if (dto.Nodes.Count == 0)
        {
            errors.Add(Error("nodes", "required", "A narrative graph must contain at least one node."));
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < dto.Nodes.Count; i++)
        {
            var node = dto.Nodes[i];
            Require(node.NodeId, $"nodes[{i}].nodeId", errors);
            if (!string.IsNullOrWhiteSpace(node.NodeId) && !nodeIds.Add(node.NodeId))
            {
                errors.Add(Error("nodes", "duplicate_node_id", $"Node '{node.NodeId}' appears more than once."));
            }
        }

        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var transition in dto.Transitions)
        {
            Require(transition.TransitionId, "transitions.transitionId", errors);
            if (!string.IsNullOrWhiteSpace(transition.TransitionId) && !transitionIds.Add(transition.TransitionId))
            {
                errors.Add(Error("transitions", "duplicate_transition_id", $"Transition '{transition.TransitionId}' appears more than once."));
            }

            if (!nodeIds.Contains(transition.FromNodeId))
            {
                errors.Add(Error("transitions.fromNodeId", "missing_node", $"Transition '{transition.TransitionId}' references missing from-node '{transition.FromNodeId}'."));
            }

            if (!nodeIds.Contains(transition.ToNodeId))
            {
                errors.Add(Error("transitions.toNodeId", "missing_node", $"Transition '{transition.TransitionId}' references missing to-node '{transition.ToNodeId}'."));
            }
        }

        if (errors.Count > 0)
        {
            return;
        }

        await _repository.UpsertNarrativeGraphSnapshotAsync(
            new NarrativeGraph
            {
                GraphId = dto.GraphId,
                Name = dto.Name,
                Description = dto.Description,
                CreatedAtUtc = dto.CreatedAtUtc == default ? DateTime.UtcNow : dto.CreatedAtUtc
            },
            dto.Nodes.Select(node => ToDomain(node, dto.GraphId)).ToArray(),
            dto.Transitions.Select(transition => ToDomain(transition, dto.GraphId)).ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishGameplayArcAsync(
        StorylineDraft draft,
        List<ValidationErrorDto> errors,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<GameplayStoryArcDto>(draft, errors);
        if (dto is null)
        {
            return;
        }

        Require(dto.ArcId, "arcId", errors);
        Require(dto.VersionId, "versionId", errors);
        Require(dto.Name, "name", errors);
        RequireTarget(draft, $"{dto.ArcId}:{dto.VersionId}", errors);
        if (dto.Steps.Count == 0)
        {
            errors.Add(Error("steps", "required", "A gameplay arc must contain at least one activity step."));
        }

        var orders = new HashSet<int>();
        foreach (var step in dto.Steps)
        {
            if (!orders.Add(step.StepOrder))
            {
                errors.Add(Error("steps.stepOrder", "duplicate_step_order", $"Step order '{step.StepOrder}' appears more than once."));
            }

            if (!_activityCatalog.Contains(step.ActivityId))
            {
                errors.Add(Error("steps.activityId", "invalid_activity_id", $"Activity '{step.ActivityId}' is not in the ActivityCatalog."));
            }
        }

        if (errors.Count > 0)
        {
            return;
        }

        var publishedAt = DateTime.UtcNow;
        await _repository.UpsertGameplayArcSnapshotAsync(
            new GameplayStoryArc
            {
                ArcId = dto.ArcId,
                VersionId = dto.VersionId,
                Name = dto.Name,
                Description = dto.Description,
                IsPublished = true,
                CreatedAtUtc = dto.CreatedAtUtc == default ? publishedAt : dto.CreatedAtUtc,
                PublishedAtUtc = publishedAt
            },
            dto.Steps.Select(step => ToDomain(step, dto.ArcId, dto.VersionId)).ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishCharacterBindingAsync(
        StorylineDraft draft,
        List<ValidationErrorDto> errors,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<CharacterStoryBindingDto>(draft, errors);
        if (dto is null)
        {
            return;
        }

        Require(dto.CharacterId, "characterId", errors);
        Require(dto.CharacterName, "characterName", errors);
        Require(dto.Realm, "realm", errors);
        Require(dto.PersonaId, "personaId", errors);
        Require(dto.PersonaVersionId, "personaVersionId", errors);
        Require(dto.ActiveGraphId, "activeGraphId", errors);
        Require(dto.ActiveNodeId, "activeNodeId", errors);
        RequireTarget(draft, dto.CharacterId, errors);

        if (!string.IsNullOrWhiteSpace(dto.PersonaId) &&
            await _repository.GetPersonaProfileAsync(dto.PersonaId, cancellationToken).ConfigureAwait(false) is null)
        {
            errors.Add(Error("personaId", "missing_persona", $"Persona '{dto.PersonaId}' does not exist."));
        }

        if (!string.IsNullOrWhiteSpace(dto.PersonaId) &&
            !string.IsNullOrWhiteSpace(dto.PersonaVersionId) &&
            await _repository.GetPersonaVersionAsync(dto.PersonaId, dto.PersonaVersionId, cancellationToken).ConfigureAwait(false) is null)
        {
            errors.Add(Error("personaVersionId", "missing_persona_version", $"Persona version '{dto.PersonaVersionId}' does not exist."));
        }

        if (!string.IsNullOrWhiteSpace(dto.ActiveGraphId) &&
            await _repository.GetNarrativeGraphAsync(dto.ActiveGraphId, cancellationToken).ConfigureAwait(false) is null)
        {
            errors.Add(Error("activeGraphId", "missing_graph", $"Narrative graph '{dto.ActiveGraphId}' does not exist."));
        }

        if (!string.IsNullOrWhiteSpace(dto.ActiveGraphId) &&
            !string.IsNullOrWhiteSpace(dto.ActiveNodeId) &&
            await _repository.GetNarrativeNodeAsync(dto.ActiveGraphId, dto.ActiveNodeId, cancellationToken).ConfigureAwait(false) is null)
        {
            errors.Add(Error("activeNodeId", "missing_node", $"Narrative node '{dto.ActiveNodeId}' does not exist in graph '{dto.ActiveGraphId}'."));
        }

        if (!string.IsNullOrWhiteSpace(dto.GameplayArcId) ||
            !string.IsNullOrWhiteSpace(dto.GameplayArcVersionId))
        {
            if (string.IsNullOrWhiteSpace(dto.GameplayArcId) || string.IsNullOrWhiteSpace(dto.GameplayArcVersionId))
            {
                errors.Add(Error("gameplayArcVersionId", "incomplete_gameplay_arc", "Both gameplay arc id and version id are required when either is set."));
            }
            else if (await _repository.GetGameplayArcAsync(dto.GameplayArcId, dto.GameplayArcVersionId, cancellationToken).ConfigureAwait(false) is null)
            {
                errors.Add(Error("gameplayArcId", "missing_gameplay_arc", $"Gameplay arc '{dto.GameplayArcId}:{dto.GameplayArcVersionId}' does not exist."));
            }
        }

        if (errors.Count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var binding = new CharacterStoryBinding
        {
            CharacterId = dto.CharacterId,
            CharacterName = dto.CharacterName,
            Realm = dto.Realm,
            PersonaId = dto.PersonaId,
            PersonaVersionId = dto.PersonaVersionId,
            ActiveGraphId = dto.ActiveGraphId,
            ActiveNodeId = dto.ActiveNodeId,
            ConversationBindingId = dto.ConversationBindingId,
            GameplayArcId = dto.GameplayArcId,
            GameplayArcVersionId = dto.GameplayArcVersionId,
            MoodState = string.IsNullOrWhiteSpace(dto.MoodState) ? "steady" : dto.MoodState,
            UpdatedAtUtc = now
        };

        await _repository.UpsertCharacterStoryBindingAsync(binding, cancellationToken).ConfigureAwait(false);
        await _repository.UpsertCharacterStateAsync(new CharacterState
        {
            CharacterId = binding.CharacterId,
            CharacterName = binding.CharacterName,
            Realm = binding.Realm,
            PersonaId = binding.PersonaId,
            PersonaVersionId = binding.PersonaVersionId,
            ActiveGraphId = binding.ActiveGraphId,
            ActiveNodeId = binding.ActiveNodeId,
            MoodState = binding.MoodState,
            UpdatedAtUtc = now
        }, cancellationToken).ConfigureAwait(false);
        await _repository.UpsertConversationBindingAsync(new ConversationBinding
        {
            ConversationBindingId = binding.ConversationBindingId,
            CharacterId = binding.CharacterId,
            PersonaId = binding.PersonaId,
            PersonaVersionId = binding.PersonaVersionId,
            GraphId = binding.ActiveGraphId,
            ActiveNodeId = binding.ActiveNodeId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NarrativeGraphDto> BuildGraphDtoAsync(NarrativeGraph graph, CancellationToken cancellationToken)
    {
        var nodes = await _repository.ListNarrativeNodesAsync(graph.GraphId, cancellationToken).ConfigureAwait(false);
        var transitions = await _repository.ListNarrativeTransitionsAsync(graph.GraphId, cancellationToken).ConfigureAwait(false);
        return ToDto(graph, nodes, transitions);
    }

    private T? Deserialize<T>(StorylineDraft draft, List<ValidationErrorDto> errors)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(draft.PayloadJson, JsonOptions);
            if (value is null)
            {
                errors.Add(Error("payloadJson", "invalid_json", "Payload JSON was empty."));
            }

            return value;
        }
        catch (JsonException ex)
        {
            errors.Add(Error("payloadJson", "invalid_json", ex.Message));
            return default;
        }
    }

    private static GraphLayoutDto FromLayoutJson(GraphLayout layout)
    {
        var dto = JsonSerializer.Deserialize<GraphLayoutDto>(layout.LayoutJson, JsonOptions);
        return dto is null
            ? new GraphLayoutDto(layout.GraphId, Array.Empty<GraphLayoutNodeDto>(), 1, layout.UpdatedAtUtc)
            : dto with { UpdatedAtUtc = layout.UpdatedAtUtc };
    }

    private static void RequireTarget(StorylineDraft draft, string actualTargetId, List<ValidationErrorDto> errors)
    {
        if (!string.Equals(draft.TargetId, actualTargetId, StringComparison.Ordinal))
        {
            errors.Add(Error("targetId", "target_mismatch", $"Draft target '{draft.TargetId}' does not match payload target '{actualTargetId}'."));
        }
    }

    private static void Require(string value, string fieldPath, List<ValidationErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Error(fieldPath, "required", $"{fieldPath} is required."));
        }
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static PublishDraftResultDto Failed(
        string draftId,
        string kind,
        string targetId,
        ValidationErrorDto error) =>
        new(false, draftId, kind, targetId, new[] { error });

    private static ValidationErrorDto Error(string fieldPath, string code, string message) => new(fieldPath, code, message);

    private static PersonaProfileDto ToDto(PersonaProfile profile) => new(
        profile.PersonaId,
        profile.DisplayName,
        profile.Description,
        profile.CreatedAtUtc);

    private static StorylineDraftDto ToDto(StorylineDraft draft) => new(
        draft.DraftId,
        draft.Kind,
        draft.TargetId,
        draft.Status,
        draft.PayloadJson,
        draft.CreatedAtUtc,
        draft.UpdatedAtUtc,
        draft.PublishedAtUtc,
        draft.PublishedBy,
        draft.PublishMessage);

    private static NarrativeGraphDto ToDto(
        NarrativeGraph graph,
        IReadOnlyList<NarrativeNode> nodes,
        IReadOnlyList<NarrativeTransition> transitions) => new(
        graph.GraphId,
        graph.Name,
        graph.Description,
        graph.CreatedAtUtc,
        nodes.Select(ToDto).ToArray(),
        transitions.Select(ToDto).ToArray());

    private static NarrativeNodeDto ToDto(NarrativeNode node) => new(
        node.NodeId,
        node.GraphId,
        node.Title,
        node.Summary,
        node.FallbackReply,
        node.SortOrder);

    private static NarrativeTransitionDto ToDto(NarrativeTransition transition) => new(
        transition.TransitionId,
        transition.GraphId,
        transition.FromNodeId,
        transition.ToNodeId,
        transition.TriggerKind,
        transition.GuardExpression,
        transition.SortOrder);

    private static NarrativeNode ToDomain(NarrativeNodeDto dto, string graphId) => new()
    {
        NodeId = dto.NodeId,
        GraphId = string.IsNullOrWhiteSpace(dto.GraphId) ? graphId : dto.GraphId,
        Title = dto.Title,
        Summary = dto.Summary,
        FallbackReply = dto.FallbackReply,
        SortOrder = dto.SortOrder
    };

    private static NarrativeTransition ToDomain(NarrativeTransitionDto dto, string graphId) => new()
    {
        TransitionId = dto.TransitionId,
        GraphId = string.IsNullOrWhiteSpace(dto.GraphId) ? graphId : dto.GraphId,
        FromNodeId = dto.FromNodeId,
        ToNodeId = dto.ToNodeId,
        TriggerKind = dto.TriggerKind,
        GuardExpression = dto.GuardExpression,
        SortOrder = dto.SortOrder
    };

    private static GameplayStoryArcDto ToDto(GameplayStoryArc arc, IReadOnlyList<GameplayArcStep> steps) => new(
        arc.ArcId,
        arc.VersionId,
        arc.Name,
        arc.Description,
        arc.IsPublished,
        arc.CreatedAtUtc,
        arc.PublishedAtUtc,
        steps.Select(ToDto).ToArray());

    private static GameplayArcStepDto ToDto(GameplayArcStep step) => new(
        step.StepId,
        step.ArcId,
        step.VersionId,
        step.StepOrder,
        step.ActivityId,
        step.NarrativeHook);

    private static GameplayArcStep ToDomain(GameplayArcStepDto dto, string arcId, string versionId) => new()
    {
        StepId = string.IsNullOrWhiteSpace(dto.StepId) ? $"{arcId}:{versionId}:{dto.StepOrder}" : dto.StepId,
        ArcId = arcId,
        VersionId = versionId,
        StepOrder = dto.StepOrder,
        ActivityId = dto.ActivityId,
        NarrativeHook = dto.NarrativeHook
    };

    private static CharacterStoryBindingDto ToDto(CharacterStoryBinding binding) => new(
        binding.CharacterId,
        binding.CharacterName,
        binding.Realm,
        binding.PersonaId,
        binding.PersonaVersionId,
        binding.ActiveGraphId,
        binding.ActiveNodeId,
        binding.ConversationBindingId,
        binding.GameplayArcId,
        binding.GameplayArcVersionId,
        binding.MoodState,
        binding.UpdatedAtUtc);

    private static MemoryCandidateDto ToDto(MemoryCandidate candidate) => new(
        candidate.CandidateId,
        candidate.CharacterId,
        candidate.PersonaId,
        candidate.SourceInput,
        candidate.CandidateText,
        candidate.Status,
        candidate.FoundryIntent,
        candidate.ActiveNodeId,
        candidate.ProposedAtUtc);
}
