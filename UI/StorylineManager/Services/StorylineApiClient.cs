using System.Net.Http.Json;
using PromptHandlingService.Storylines;

namespace StorylineManager.Services;

public sealed class StorylineApiClient
{
    private readonly HttpClient _httpClient;

    public StorylineApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Uri? BaseAddress => _httpClient.BaseAddress;

    public async Task<StorylineHealthDto?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<StorylineHealthDto>("health", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PersonaProfileDto>> GetPersonasAsync(CancellationToken cancellationToken = default) =>
        await GetListAsync<PersonaProfileDto>("personas", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PersonaVersionDto>> GetPersonaVersionsAsync(
        string? personaId = null,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(personaId)
            ? "persona-versions"
            : $"persona-versions?personaId={Uri.EscapeDataString(personaId)}";
        return await GetListAsync<PersonaVersionDto>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NarrativeGraphDto>> GetGraphsAsync(CancellationToken cancellationToken = default) =>
        await GetListAsync<NarrativeGraphDto>("graphs", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<GameplayStoryArcDto>> GetGameplayArcsAsync(CancellationToken cancellationToken = default) =>
        await GetListAsync<GameplayStoryArcDto>("gameplay-arcs", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<CharacterStoryBindingDto>> GetCharactersAsync(CancellationToken cancellationToken = default) =>
        await GetListAsync<CharacterStoryBindingDto>("characters", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ActivityCatalogItemDto>> GetActivityCatalogAsync(CancellationToken cancellationToken = default) =>
        await GetListAsync<ActivityCatalogItemDto>("activity-catalog", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<StorylineDraftDto>> GetDraftsAsync(CancellationToken cancellationToken = default) =>
        await GetListAsync<StorylineDraftDto>("drafts", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MemoryCandidateDto>> GetMemoryCandidatesAsync(
        string characterId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var path = $"memory-candidates?characterId={Uri.EscapeDataString(characterId)}&status={Uri.EscapeDataString(status)}";
        return await GetListAsync<MemoryCandidateDto>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StorylineDraftDto?> SaveDraftAsync(StorylineDraftDto draft, CancellationToken cancellationToken = default)
    {
        var response = string.IsNullOrWhiteSpace(draft.DraftId)
            ? await _httpClient.PostAsJsonAsync("drafts", draft, cancellationToken).ConfigureAwait(false)
            : await _httpClient.PutAsJsonAsync($"drafts/{Uri.EscapeDataString(draft.DraftId)}", draft, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StorylineDraftDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PublishDraftResultDto?> PublishDraftAsync(
        string draftId,
        PublishDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"drafts/{Uri.EscapeDataString(draftId)}/publish",
            request,
            cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<PublishDraftResultDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FoundryDeploymentPreviewDto?> PreviewFoundryDeploymentAsync(
        FoundryDeploymentTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("foundry/deployments/preview", request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FoundryDeploymentPreviewDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FoundryDeploymentDto?> QueueFoundryDeploymentAsync(
        FoundryDeploymentTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("foundry/deployments", request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FoundryDeploymentDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FoundryDeploymentDto?> GetFoundryDeploymentAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
                $"foundry/deployments/{Uri.EscapeDataString(deploymentId)}",
                cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FoundryDeploymentDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FoundryDeploymentDto?> PromoteFoundryDeploymentAsync(
        string deploymentId,
        PromoteFoundryDeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"foundry/deployments/{Uri.EscapeDataString(deploymentId)}/promote",
            request,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FoundryDeploymentDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MemoryCandidateDto?> ReviewMemoryCandidateAsync(
        string candidateId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"memory-candidates/{Uri.EscapeDataString(candidateId)}/review",
            new MemoryCandidateReviewDto(candidateId, status, Environment.UserName, string.Empty),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemoryCandidateDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GraphLayoutDto?> GetGraphLayoutAsync(string graphId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"graph-layouts/{Uri.EscapeDataString(graphId)}", cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GraphLayoutDto>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GraphLayoutDto?> SaveGraphLayoutAsync(GraphLayoutDto layout, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"graph-layouts/{Uri.EscapeDataString(layout.GraphId)}",
            layout,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GraphLayoutDto>(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        var result = await _httpClient.GetFromJsonAsync<T[]>(path, cancellationToken).ConfigureAwait(false);
        return result ?? Array.Empty<T>();
    }
}
