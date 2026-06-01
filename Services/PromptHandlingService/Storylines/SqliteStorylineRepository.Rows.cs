using SQLite;

namespace PromptHandlingService.Storylines;

// Partial of SqliteStorylineRepository: nested SQLite-net table-row DTOs + the seed document.
// Split out of SqliteStorylineRepository.cs for readability (behavior-preserving; no logic here).
public sealed partial class SqliteStorylineRepository
{
    internal sealed class StorylineSeedDocument
    {
        public List<PersonaProfile> PersonaProfiles { get; set; } = new();
        public List<PersonaVersion> PersonaVersions { get; set; } = new();
        public List<CharacterState> CharacterStates { get; set; } = new();
        public List<MemoryFact> MemoryFacts { get; set; } = new();
        public List<MemoryEpisode> MemoryEpisodes { get; set; } = new();
        public List<NarrativeGraph> NarrativeGraphs { get; set; } = new();
        public List<NarrativeNode> NarrativeNodes { get; set; } = new();
        public List<NarrativeTransition> NarrativeTransitions { get; set; } = new();
        public List<AgentBinding> AgentBindings { get; set; } = new();
        public List<ConversationBinding> ConversationBindings { get; set; } = new();
    }

    [Table("PersonaProfile")]
    internal sealed class PersonaProfileRow
    {
        [PrimaryKey]
        public string PersonaId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("PersonaVersion")]
    internal sealed class PersonaVersionRow
    {
        [PrimaryKey]
        public string PersonaVersionId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PromptSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("CharacterState")]
    internal sealed class CharacterStateRow
    {
        [PrimaryKey]
        public string CharacterId { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        [Indexed]
        public string Realm { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaVersionId { get; set; } = string.Empty;
        public string ActiveGraphId { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public string MoodState { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    [Table("MemoryFact")]
    internal sealed class MemoryFactRow
    {
        [PrimaryKey]
        public string FactId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Importance { get; set; }
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
    }

    [Table("MemoryEpisode")]
    internal sealed class MemoryEpisodeRow
    {
        [PrimaryKey]
        public string EpisodeId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int Importance { get; set; }
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("MemoryCandidate")]
    internal sealed class MemoryCandidateRow
    {
        [PrimaryKey]
        public string CandidateId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string SourceInput { get; set; } = string.Empty;
        public string CandidateText { get; set; } = string.Empty;
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public string FoundryIntent { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public DateTime ProposedAtUtc { get; set; }
    }

    [Table("NarrativeGraph")]
    internal sealed class NarrativeGraphRow
    {
        [PrimaryKey]
        public string GraphId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("NarrativeNode")]
    internal sealed class NarrativeNodeRow
    {
        [PrimaryKey]
        public string NodeId { get; set; } = string.Empty;
        [Indexed]
        public string GraphId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string FallbackReply { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    [Table("NarrativeTransition")]
    internal sealed class NarrativeTransitionRow
    {
        [PrimaryKey]
        public string TransitionId { get; set; } = string.Empty;
        [Indexed]
        public string GraphId { get; set; } = string.Empty;
        [Indexed]
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string TriggerKind { get; set; } = string.Empty;
        public string GuardExpression { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    [Table("AgentBinding")]
    internal sealed class AgentBindingRow
    {
        [PrimaryKey]
        public string BindingId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaVersionId { get; set; } = string.Empty;
        [Indexed]
        public string GraphId { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public string? AgentVersion { get; set; }
        public int MaxOutputTokens { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("StorylineFoundryDeployment")]
    internal sealed class StorylineFoundryDeploymentRow
    {
        [PrimaryKey]
        public string DeploymentId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaVersionId { get; set; } = string.Empty;
        [Indexed]
        public string GraphId { get; set; } = string.Empty;
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public string? AgentVersion { get; set; }
        public string? AgentVersionId { get; set; }
        public int MaxOutputTokens { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string PromotedBy { get; set; } = string.Empty;
        public DateTime? PromotedAtUtc { get; set; }
        public string ErrorText { get; set; } = string.Empty;
    }

    internal sealed class TableInfoRow
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("ConversationBinding")]
    internal sealed class ConversationBindingRow
    {
        [PrimaryKey]
        public string ConversationBindingId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        public string? GuestId { get; set; }
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaVersionId { get; set; } = string.Empty;
        public string GraphId { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    [Table("StorylineDraft")]
    internal sealed class StorylineDraftRow
    {
        [PrimaryKey]
        public string DraftId { get; set; } = string.Empty;
        [Indexed]
        public string Kind { get; set; } = string.Empty;
        [Indexed]
        public string TargetId { get; set; } = string.Empty;
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
        public string PublishedBy { get; set; } = string.Empty;
        public string PublishMessage { get; set; } = string.Empty;
    }

    [Table("GameplayStoryArc")]
    internal sealed class GameplayStoryArcRow
    {
        [PrimaryKey]
        public string ArcVersionKey { get; set; } = string.Empty;
        [Indexed]
        public string ArcId { get; set; } = string.Empty;
        [Indexed]
        public string VersionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
    }

    [Table("GameplayArcStep")]
    internal sealed class GameplayArcStepRow
    {
        [PrimaryKey]
        public string StepId { get; set; } = string.Empty;
        [Indexed]
        public string ArcVersionKey { get; set; } = string.Empty;
        [Indexed]
        public string ArcId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public string ActivityId { get; set; } = string.Empty;
        public string NarrativeHook { get; set; } = string.Empty;
    }

    [Table("CharacterStoryBinding")]
    internal sealed class CharacterStoryBindingRow
    {
        [PrimaryKey]
        public string CharacterId { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        [Indexed]
        public string Realm { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaVersionId { get; set; } = string.Empty;
        public string ActiveGraphId { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public string ConversationBindingId { get; set; } = string.Empty;
        public string GameplayArcId { get; set; } = string.Empty;
        public string GameplayArcVersionId { get; set; } = string.Empty;
        public string MoodState { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    [Table("GraphLayout")]
    internal sealed class GraphLayoutRow
    {
        [PrimaryKey]
        public string GraphId { get; set; } = string.Empty;
        public string LayoutJson { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}
