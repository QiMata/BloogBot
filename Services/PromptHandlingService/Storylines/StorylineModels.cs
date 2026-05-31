using PromptHandlingService.Foundry;

namespace PromptHandlingService.Storylines;

public static class StorylineMemoryStatus
{
    public const string Approved = "Approved";
    public const string Pending = "Pending";
    public const string Rejected = "Rejected";
}

public sealed record PersonaProfile
{
    public string PersonaId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record PersonaVersion
{
    public string PersonaVersionId { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string PromptSummary { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record CharacterState
{
    public string CharacterId { get; init; } = string.Empty;
    public string CharacterName { get; init; } = string.Empty;
    public string Realm { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string PersonaVersionId { get; init; } = string.Empty;
    public string ActiveGraphId { get; init; } = string.Empty;
    public string ActiveNodeId { get; init; } = string.Empty;
    public string MoodState { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

public static class StorylineDraftStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
}

public static class StorylineDraftKind
{
    public const string PersonaProfile = "personaProfile";
    public const string PersonaVersion = "personaVersion";
    public const string NarrativeGraph = "narrativeGraph";
    public const string GameplayArc = "gameplayArc";
    public const string CharacterBinding = "characterBinding";
}

public sealed record StorylineDraft
{
    public string DraftId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string Status { get; init; } = StorylineDraftStatus.Draft;
    public string PayloadJson { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; init; }
    public string PublishedBy { get; init; } = string.Empty;
    public string PublishMessage { get; init; } = string.Empty;
}

public sealed record MemoryFact
{
    public string FactId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public int Importance { get; init; }
    public string Status { get; init; } = StorylineMemoryStatus.Approved;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; init; }
}

public sealed record MemoryEpisode
{
    public string EpisodeId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public int Importance { get; init; }
    public string Status { get; init; } = StorylineMemoryStatus.Approved;
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record MemoryCandidate
{
    public string CandidateId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string SourceInput { get; init; } = string.Empty;
    public string CandidateText { get; init; } = string.Empty;
    public string Status { get; init; } = StorylineMemoryStatus.Pending;
    public string FoundryIntent { get; init; } = string.Empty;
    public string ActiveNodeId { get; init; } = string.Empty;
    public DateTime ProposedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record NarrativeGraph
{
    public string GraphId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record NarrativeNode
{
    public string NodeId { get; init; } = string.Empty;
    public string GraphId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string FallbackReply { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public sealed record NarrativeTransition
{
    public string TransitionId { get; init; } = string.Empty;
    public string GraphId { get; init; } = string.Empty;
    public string FromNodeId { get; init; } = string.Empty;
    public string ToNodeId { get; init; } = string.Empty;
    public string TriggerKind { get; init; } = string.Empty;
    public string GuardExpression { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public sealed record AgentBinding
{
    public string BindingId { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string PersonaVersionId { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string? AgentVersion { get; init; }
    public int MaxOutputTokens { get; init; } = 512;
    public bool IsDefault { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public PersonaPromptRuntimeBinding ToRuntimeBinding() =>
        new(Model, AgentName, AgentVersion, MaxOutputTokens);
}

public sealed record ConversationBinding
{
    public string ConversationBindingId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string? GuestId { get; init; }
    public string PersonaId { get; init; } = string.Empty;
    public string PersonaVersionId { get; init; } = string.Empty;
    public string GraphId { get; init; } = string.Empty;
    public string ActiveNodeId { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record GameplayStoryArc
{
    public string ArcId { get; init; } = string.Empty;
    public string VersionId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsPublished { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; init; }
}

public sealed record GameplayArcStep
{
    public string StepId { get; init; } = string.Empty;
    public string ArcId { get; init; } = string.Empty;
    public string VersionId { get; init; } = string.Empty;
    public int StepOrder { get; init; }
    public string ActivityId { get; init; } = string.Empty;
    public string NarrativeHook { get; init; } = string.Empty;
}

public sealed record CharacterStoryBinding
{
    public string CharacterId { get; init; } = string.Empty;
    public string CharacterName { get; init; } = string.Empty;
    public string Realm { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string PersonaVersionId { get; init; } = string.Empty;
    public string ActiveGraphId { get; init; } = string.Empty;
    public string ActiveNodeId { get; init; } = string.Empty;
    public string ConversationBindingId { get; init; } = string.Empty;
    public string GameplayArcId { get; init; } = string.Empty;
    public string GameplayArcVersionId { get; init; } = string.Empty;
    public string MoodState { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record GraphLayout
{
    public string GraphId { get; init; } = string.Empty;
    public string LayoutJson { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record StorylinePromptInput(
    string CharacterId,
    string CharacterName,
    string Realm,
    string? GuestId,
    string InputText,
    string? TriggerKind = null);

public sealed record StorylinePersonaDialogueResult(
    string CharacterId,
    string CharacterName,
    string Realm,
    string ReplyText,
    string Intent,
    bool UsedDeterministicFallback,
    string FallbackReason,
    string PersonaId,
    string PersonaVersion,
    string ActiveNodeId,
    string FoundryAgentName,
    string FoundryAgentVersion,
    string Model,
    IReadOnlyList<string> PendingMemoryCandidateIds);

public sealed record StorylineResolvedContext(
    StorylinePromptInput Input,
    CharacterState CharacterState,
    PersonaProfile PersonaProfile,
    PersonaVersion PersonaVersion,
    NarrativeNode ActiveNode,
    IReadOnlyList<NarrativeTransition> OutboundTransitions,
    AgentBinding AgentBinding,
    PersonaPromptRequest PromptRequest);
