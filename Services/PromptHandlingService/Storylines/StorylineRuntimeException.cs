namespace PromptHandlingService.Storylines;

public sealed class StorylineRuntimeException : InvalidOperationException
{
    public StorylineRuntimeException(string message)
        : base(message)
    {
    }
}
