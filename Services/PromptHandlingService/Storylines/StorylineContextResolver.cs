using System.Text;
using PromptHandlingService.Foundry;

namespace PromptHandlingService.Storylines;

public sealed class StorylineContextResolver : IStorylineContextResolver
{
    private readonly IStorylineRepository _repository;
    private readonly StorylineRuntimeOptions _options;

    public StorylineContextResolver(IStorylineRepository repository, StorylineRuntimeOptions options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<StorylineResolvedContext> ResolveAsync(StorylinePromptInput input, CancellationToken cancellationToken)
    {
        ValidateInput(input);

        var state = await _repository.GetCharacterStateAsync(input.CharacterId, cancellationToken).ConfigureAwait(false)
            ?? throw new StorylineRuntimeException($"Missing character state for '{input.CharacterId}'.");

        if (!string.Equals(state.Realm, input.Realm, StringComparison.OrdinalIgnoreCase))
        {
            throw new StorylineRuntimeException($"Character state realm '{state.Realm}' does not match input realm '{input.Realm}'.");
        }

        var profile = await _repository.GetPersonaProfileAsync(state.PersonaId, cancellationToken).ConfigureAwait(false)
            ?? throw new StorylineRuntimeException($"Missing persona profile '{state.PersonaId}'.");

        var version = await _repository.GetPersonaVersionAsync(state.PersonaId, state.PersonaVersionId, cancellationToken).ConfigureAwait(false)
            ?? throw new StorylineRuntimeException($"Missing persona version '{state.PersonaVersionId}' for persona '{state.PersonaId}'.");

        var node = await _repository.GetNarrativeNodeAsync(state.ActiveGraphId, state.ActiveNodeId, cancellationToken).ConfigureAwait(false)
            ?? throw new StorylineRuntimeException($"Missing narrative node '{state.ActiveNodeId}' in graph '{state.ActiveGraphId}'.");

        var transitions = await _repository.GetNarrativeTransitionsAsync(state.ActiveGraphId, state.ActiveNodeId, cancellationToken)
            .ConfigureAwait(false);

        var binding = await _repository.GetAgentBindingAsync(state.PersonaId, state.PersonaVersionId, cancellationToken).ConfigureAwait(false)
            ?? throw new StorylineRuntimeException($"Missing agent binding for persona '{state.PersonaId}' version '{state.PersonaVersionId}'.");

        var memorySummary = await BuildMemorySummaryAsync(state.CharacterId, state.PersonaId, cancellationToken).ConfigureAwait(false);
        var characterName = string.IsNullOrWhiteSpace(input.CharacterName) ? state.CharacterName : input.CharacterName.Trim();

        var promptRequest = new PersonaPromptRequest(
            PersonaId: profile.PersonaId,
            PersonaVersion: version.Version,
            CharacterName: characterName,
            Realm: state.Realm,
            ActiveNarrativeNode: $"{node.NodeId}: {node.Title}. {node.Summary}",
            CompactMemorySummary: memorySummary,
            CurrentMoodState: state.MoodState,
            InputText: input.InputText.Trim(),
            PersonaDescription: profile.Description,
            PersonaPromptSummary: version.PromptSummary);

        return new StorylineResolvedContext(
            input,
            state,
            profile,
            version,
            node,
            transitions,
            binding,
            promptRequest);
    }

    private async Task<string> BuildMemorySummaryAsync(string characterId, string personaId, CancellationToken cancellationToken)
    {
        var facts = await _repository.GetApprovedMemoryFactsAsync(characterId, personaId, cancellationToken).ConfigureAwait(false);
        var episodes = await _repository.GetApprovedMemoryEpisodesAsync(characterId, personaId, cancellationToken).ConfigureAwait(false);

        var items = facts
            .Select(fact => new MemorySummaryItem(
                $"fact: {fact.Text}",
                fact.Importance,
                fact.ApprovedAtUtc ?? fact.CreatedAtUtc,
                fact.FactId))
            .Concat(episodes.Select(episode => new MemorySummaryItem(
                $"episode: {episode.Summary}",
                episode.Importance,
                episode.OccurredAtUtc,
                episode.EpisodeId)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .OrderByDescending(item => item.Importance)
            .ThenByDescending(item => item.Timestamp)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

        if (items.Length == 0)
        {
            return "(none)";
        }

        var builder = new StringBuilder();
        foreach (var item in items)
        {
            AppendWithinLimit(builder, item.Text.Trim(), _options.MaxMemorySummaryCharacters);
            if (builder.Length >= _options.MaxMemorySummaryCharacters)
            {
                break;
            }
        }

        return builder.Length == 0 ? "(none)" : builder.ToString();
    }

    private static void AppendWithinLimit(StringBuilder builder, string line, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(line) || builder.Length >= maxCharacters)
        {
            return;
        }

        var prefixLength = builder.Length == 0 ? 0 : Environment.NewLine.Length;
        var available = maxCharacters - builder.Length - prefixLength;
        if (available <= 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line.Length <= available ? line : line[..available]);
    }

    private static void ValidateInput(StorylinePromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateRequired(input.CharacterId, nameof(input.CharacterId));
        ValidateRequired(input.Realm, nameof(input.Realm));
        ValidateRequired(input.InputText, nameof(input.InputText));
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private sealed record MemorySummaryItem(string Text, int Importance, DateTime Timestamp, string Id);
}
