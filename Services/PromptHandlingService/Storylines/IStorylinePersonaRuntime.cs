namespace PromptHandlingService.Storylines;

public interface IStorylinePersonaRuntime
{
    Task<StorylinePersonaDialogueResult> GenerateAsync(StorylinePromptInput input, CancellationToken cancellationToken);
}
