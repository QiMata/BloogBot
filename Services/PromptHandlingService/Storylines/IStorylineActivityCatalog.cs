namespace PromptHandlingService.Storylines;

public interface IStorylineActivityCatalog
{
    IReadOnlyList<ActivityCatalogItemDto> List();

    bool Contains(string activityId);
}
