namespace PromptHandlingService.Storylines;

public sealed record ValidationErrorDto(
    string FieldPath,
    string Code,
    string Message);

public sealed record PublishDraftRequest(
    string PublishedBy = "",
    string Message = "");

public sealed record PublishDraftResultDto(
    bool Published,
    string DraftId,
    string Kind,
    string TargetId,
    IReadOnlyList<ValidationErrorDto> Errors);

public sealed record FoundryDeploymentTargetRequest(
    string PersonaId,
    string PersonaVersionId,
    string GraphId,
    string RequestedBy = "");

public sealed record PromoteFoundryDeploymentRequest(
    string PromotedBy = "");

public sealed record FoundryDeploymentPreviewDto(
    bool IsValid,
    string PersonaId,
    string PersonaVersionId,
    string GraphId,
    IReadOnlyList<ValidationErrorDto> Errors,
    string ContentHash,
    string Instructions,
    string Model,
    string AgentName,
    int MaxOutputTokens,
    FoundryDeploymentDto? LastDeployment);

public sealed record FoundryDeploymentDto(
    string DeploymentId,
    string PersonaId,
    string PersonaVersionId,
    string GraphId,
    string Status,
    string ContentHash,
    string Model,
    string AgentName,
    string? AgentVersion,
    string? AgentVersionId,
    int MaxOutputTokens,
    string RequestedBy,
    DateTime RequestedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string PromotedBy,
    DateTime? PromotedAtUtc,
    string ErrorText);

public sealed record StorylineDraftDto(
    string DraftId,
    string Kind,
    string TargetId,
    string Status,
    string PayloadJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PublishedAtUtc,
    string PublishedBy,
    string PublishMessage);

public sealed record PersonaProfileDto(
    string PersonaId,
    string DisplayName,
    string Description,
    DateTime CreatedAtUtc);

public sealed record PersonaVersionDto(
    string PersonaVersionId,
    string PersonaId,
    string Version,
    string PromptSummary,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record NarrativeGraphDto(
    string GraphId,
    string Name,
    string Description,
    DateTime CreatedAtUtc,
    IReadOnlyList<NarrativeNodeDto> Nodes,
    IReadOnlyList<NarrativeTransitionDto> Transitions);

public sealed record NarrativeNodeDto(
    string NodeId,
    string GraphId,
    string Title,
    string Summary,
    string FallbackReply,
    int SortOrder);

public sealed record NarrativeTransitionDto(
    string TransitionId,
    string GraphId,
    string FromNodeId,
    string ToNodeId,
    string TriggerKind,
    string GuardExpression,
    int SortOrder);

public sealed record GameplayStoryArcDto(
    string ArcId,
    string VersionId,
    string Name,
    string Description,
    bool IsPublished,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    IReadOnlyList<GameplayArcStepDto> Steps);

public sealed record GameplayArcStepDto(
    string StepId,
    string ArcId,
    string VersionId,
    int StepOrder,
    string ActivityId,
    string NarrativeHook);

public sealed record CharacterStoryBindingDto(
    string CharacterId,
    string CharacterName,
    string Realm,
    string PersonaId,
    string PersonaVersionId,
    string ActiveGraphId,
    string ActiveNodeId,
    string ConversationBindingId,
    string GameplayArcId,
    string GameplayArcVersionId,
    string MoodState,
    DateTime UpdatedAtUtc);

public sealed record MemoryCandidateReviewDto(
    string CandidateId,
    string Status,
    string Reviewer,
    string ReviewNote);

public sealed record MemoryCandidateDto(
    string CandidateId,
    string CharacterId,
    string PersonaId,
    string SourceInput,
    string CandidateText,
    string Status,
    string FoundryIntent,
    string ActiveNodeId,
    DateTime ProposedAtUtc);

public sealed record GraphLayoutDto(
    string GraphId,
    IReadOnlyList<GraphLayoutNodeDto> Nodes,
    double CanvasScale,
    DateTime UpdatedAtUtc);

public sealed record GraphLayoutNodeDto(
    string NodeId,
    double X,
    double Y);

public sealed record ActivityCatalogItemDto(
    string Id,
    string Family,
    string Location,
    int MinLevel,
    int MaxLevel,
    string FactionPolicy,
    string DisplayLabel);

public sealed record StorylineHealthDto(
    string Status,
    string DatabasePath,
    int PersonaCount,
    int NarrativeGraphCount);
