namespace PromptHandlingService.Storylines;

public interface IStorylineContextResolver
{
    Task<StorylineResolvedContext> ResolveAsync(StorylinePromptInput input, CancellationToken cancellationToken);
}
