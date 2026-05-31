namespace PromptHandlingService.Storylines;

public interface IStorylineManagementService
{
    Task<StorylineHealthDto> GetHealthAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonaProfileDto>> ListPersonasAsync(CancellationToken cancellationToken);
    Task<PersonaProfileDto?> GetPersonaAsync(string personaId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NarrativeGraphDto>> ListNarrativeGraphsAsync(CancellationToken cancellationToken);
    Task<NarrativeGraphDto?> GetNarrativeGraphAsync(string graphId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameplayStoryArcDto>> ListGameplayArcsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CharacterStoryBindingDto>> ListCharacterBindingsAsync(CancellationToken cancellationToken);
    Task<CharacterStoryBindingDto?> GetCharacterBindingAsync(string characterId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StorylineDraftDto>> ListDraftsAsync(string? kind, string? status, CancellationToken cancellationToken);
    Task<StorylineDraftDto?> GetDraftAsync(string draftId, CancellationToken cancellationToken);
    Task<StorylineDraftDto> SaveDraftAsync(StorylineDraftDto draft, CancellationToken cancellationToken);
    Task<PublishDraftResultDto> PublishDraftAsync(string draftId, PublishDraftRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoryCandidateDto>> ListMemoryCandidatesAsync(string characterId, string status, CancellationToken cancellationToken);
    Task<MemoryCandidateDto?> ReviewMemoryCandidateAsync(string candidateId, MemoryCandidateReviewDto review, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActivityCatalogItemDto>> ListActivityCatalogAsync(CancellationToken cancellationToken);
    Task<GraphLayoutDto?> GetGraphLayoutAsync(string graphId, CancellationToken cancellationToken);
    Task<GraphLayoutDto> SaveGraphLayoutAsync(GraphLayoutDto layout, CancellationToken cancellationToken);
}
