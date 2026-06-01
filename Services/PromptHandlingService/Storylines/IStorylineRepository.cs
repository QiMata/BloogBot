namespace PromptHandlingService.Storylines;

public interface IStorylineRepository
{
    string DatabasePath { get; }

    Task<int> CountPersonasAsync(CancellationToken cancellationToken);
    Task<int> CountNarrativeGraphsAsync(CancellationToken cancellationToken);

    Task UpsertPersonaProfileAsync(PersonaProfile profile, CancellationToken cancellationToken);
    Task<PersonaProfile?> GetPersonaProfileAsync(string personaId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonaProfile>> ListPersonaProfilesAsync(CancellationToken cancellationToken);

    Task UpsertPersonaVersionAsync(PersonaVersion version, CancellationToken cancellationToken);
    Task<PersonaVersion?> GetPersonaVersionAsync(string personaId, string personaVersionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonaVersion>> ListPersonaVersionsAsync(string? personaId, CancellationToken cancellationToken);

    Task UpsertCharacterStateAsync(CharacterState state, CancellationToken cancellationToken);
    Task<CharacterState?> GetCharacterStateAsync(string characterId, CancellationToken cancellationToken);

    Task UpsertMemoryFactAsync(MemoryFact fact, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoryFact>> GetApprovedMemoryFactsAsync(string characterId, string personaId, CancellationToken cancellationToken);

    Task UpsertMemoryEpisodeAsync(MemoryEpisode episode, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoryEpisode>> GetApprovedMemoryEpisodesAsync(string characterId, string personaId, CancellationToken cancellationToken);

    Task AddMemoryCandidateAsync(MemoryCandidate candidate, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoryCandidate>> GetMemoryCandidatesAsync(string characterId, string status, CancellationToken cancellationToken);
    Task<MemoryCandidate?> GetMemoryCandidateAsync(string candidateId, CancellationToken cancellationToken);
    Task UpdateMemoryCandidateStatusAsync(string candidateId, string status, CancellationToken cancellationToken);

    Task UpsertNarrativeGraphAsync(NarrativeGraph graph, CancellationToken cancellationToken);
    Task<NarrativeGraph?> GetNarrativeGraphAsync(string graphId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NarrativeGraph>> ListNarrativeGraphsAsync(CancellationToken cancellationToken);

    Task UpsertNarrativeNodeAsync(NarrativeNode node, CancellationToken cancellationToken);
    Task<NarrativeNode?> GetNarrativeNodeAsync(string graphId, string nodeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NarrativeNode>> ListNarrativeNodesAsync(string graphId, CancellationToken cancellationToken);

    Task UpsertNarrativeTransitionAsync(NarrativeTransition transition, CancellationToken cancellationToken);
    Task<IReadOnlyList<NarrativeTransition>> GetNarrativeTransitionsAsync(string graphId, string fromNodeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NarrativeTransition>> ListNarrativeTransitionsAsync(string graphId, CancellationToken cancellationToken);
    Task UpsertNarrativeGraphSnapshotAsync(
        NarrativeGraph graph,
        IReadOnlyList<NarrativeNode> nodes,
        IReadOnlyList<NarrativeTransition> transitions,
        CancellationToken cancellationToken);

    Task UpsertAgentBindingAsync(AgentBinding binding, CancellationToken cancellationToken);
    Task<AgentBinding?> GetAgentBindingAsync(string personaId, string personaVersionId, CancellationToken cancellationToken);
    Task<AgentBinding?> GetAgentBindingAsync(string personaId, string personaVersionId, string graphId, CancellationToken cancellationToken);
    Task PromoteAgentBindingAsync(AgentBinding binding, CancellationToken cancellationToken);

    Task UpsertFoundryDeploymentAsync(StorylineFoundryDeployment deployment, CancellationToken cancellationToken);
    Task<StorylineFoundryDeployment?> GetFoundryDeploymentAsync(string deploymentId, CancellationToken cancellationToken);
    Task<StorylineFoundryDeployment?> GetLatestFoundryDeploymentAsync(
        string personaId,
        string personaVersionId,
        string graphId,
        CancellationToken cancellationToken);

    Task UpsertConversationBindingAsync(ConversationBinding binding, CancellationToken cancellationToken);
    Task<ConversationBinding?> GetConversationBindingAsync(string characterId, string? guestId, CancellationToken cancellationToken);

    Task UpsertStorylineDraftAsync(StorylineDraft draft, CancellationToken cancellationToken);
    Task<StorylineDraft?> GetStorylineDraftAsync(string draftId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StorylineDraft>> ListStorylineDraftsAsync(string? kind, string? status, CancellationToken cancellationToken);
    Task MarkStorylineDraftPublishedAsync(
        string draftId,
        DateTime publishedAtUtc,
        string publishedBy,
        string publishMessage,
        CancellationToken cancellationToken);

    Task UpsertGameplayArcSnapshotAsync(
        GameplayStoryArc arc,
        IReadOnlyList<GameplayArcStep> steps,
        CancellationToken cancellationToken);
    Task<GameplayStoryArc?> GetGameplayArcAsync(string arcId, string versionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameplayStoryArc>> ListGameplayArcsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GameplayArcStep>> ListGameplayArcStepsAsync(string arcId, string versionId, CancellationToken cancellationToken);

    Task UpsertCharacterStoryBindingAsync(CharacterStoryBinding binding, CancellationToken cancellationToken);
    Task<CharacterStoryBinding?> GetCharacterStoryBindingAsync(string characterId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CharacterStoryBinding>> ListCharacterStoryBindingsAsync(CancellationToken cancellationToken);

    Task UpsertGraphLayoutAsync(GraphLayout layout, CancellationToken cancellationToken);
    Task<GraphLayout?> GetGraphLayoutAsync(string graphId, CancellationToken cancellationToken);
}
