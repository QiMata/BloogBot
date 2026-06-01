using PromptHandlingService.Foundry;

namespace PromptHandlingService.Storylines;

public sealed class StorylinePersonaRuntime : IStorylinePersonaRuntime
{
    private static readonly HashSet<string> DisallowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "world-action",
        "state-transition",
        "trade",
        "combat",
        "mail",
        "invite"
    };

    private readonly IStorylineContextResolver _contextResolver;
    private readonly IStorylineRepository _repository;
    private readonly IFoundryPersonaRuntime _foundryRuntime;

    public StorylinePersonaRuntime(
        IStorylineContextResolver contextResolver,
        IStorylineRepository repository,
        IFoundryPersonaRuntime foundryRuntime)
    {
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _foundryRuntime = foundryRuntime ?? throw new ArgumentNullException(nameof(foundryRuntime));
    }

    public async Task<StorylinePersonaDialogueResult> GenerateAsync(StorylinePromptInput input, CancellationToken cancellationToken)
    {
        var context = await _contextResolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
        var binding = context.AgentBinding.ToRuntimeBinding();
        var foundryResult = await _foundryRuntime.GenerateAsync(context.PromptRequest, binding, cancellationToken)
            .ConfigureAwait(false);

        if (IsDisallowedIntent(foundryResult.Intent))
        {
            return BuildFallbackResult(
                context,
                foundryResult,
                $"disallowed-intent:{foundryResult.Intent}");
        }

        var pendingIds = await StoreMemoryCandidatesAsync(context, foundryResult, cancellationToken).ConfigureAwait(false);

        return new StorylinePersonaDialogueResult(
            context.CharacterState.CharacterId,
            context.PromptRequest.CharacterName,
            context.CharacterState.Realm,
            foundryResult.ReplyText,
            foundryResult.Intent,
            UsedDeterministicFallback: false,
            FallbackReason: string.Empty,
            context.PersonaProfile.PersonaId,
            context.PersonaVersion.Version,
            context.CharacterState.ActiveNodeId,
            foundryResult.FoundryAgentName,
            foundryResult.FoundryAgentVersion,
            foundryResult.Model,
            pendingIds);
    }

    private async Task<IReadOnlyList<string>> StoreMemoryCandidatesAsync(
        StorylineResolvedContext context,
        PersonaPromptResult foundryResult,
        CancellationToken cancellationToken)
    {
        var pendingIds = new List<string>();
        foreach (var candidateText in foundryResult.MemoryCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidateText))
            {
                continue;
            }

            var candidate = new MemoryCandidate
            {
                CandidateId = Guid.NewGuid().ToString("N"),
                CharacterId = context.CharacterState.CharacterId,
                PersonaId = context.PersonaProfile.PersonaId,
                SourceInput = context.Input.InputText.Trim(),
                CandidateText = candidateText.Trim(),
                Status = StorylineMemoryStatus.Pending,
                FoundryIntent = foundryResult.Intent,
                ActiveNodeId = context.CharacterState.ActiveNodeId,
                ProposedAtUtc = DateTime.UtcNow
            };

            await _repository.AddMemoryCandidateAsync(candidate, cancellationToken).ConfigureAwait(false);
            pendingIds.Add(candidate.CandidateId);
        }

        return pendingIds;
    }

    private static StorylinePersonaDialogueResult BuildFallbackResult(
        StorylineResolvedContext context,
        PersonaPromptResult foundryResult,
        string reason)
    {
        var reply = string.IsNullOrWhiteSpace(context.ActiveNode.FallbackReply)
            ? "I can only help with conversation right now."
            : context.ActiveNode.FallbackReply;

        return new StorylinePersonaDialogueResult(
            context.CharacterState.CharacterId,
            context.PromptRequest.CharacterName,
            context.CharacterState.Realm,
            reply,
            "fallback",
            UsedDeterministicFallback: true,
            FallbackReason: reason,
            context.PersonaProfile.PersonaId,
            context.PersonaVersion.Version,
            context.CharacterState.ActiveNodeId,
            foundryResult.FoundryAgentName,
            foundryResult.FoundryAgentVersion,
            foundryResult.Model,
            Array.Empty<string>());
    }

    private static bool IsDisallowedIntent(string intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return false;
        }

        return DisallowedIntents.Contains(intent.Trim());
    }
}
