namespace StorylineManager;

public sealed class StorylineManagerOptions
{
    public const string SectionName = "StorylineManager";

    public string ApiBaseUrl { get; init; } = "http://127.0.0.1:5147/api/storylines/v1/";
}
