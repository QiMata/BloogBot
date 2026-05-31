using PromptHandlingService.Storylines;
using WoWStateManager.Activities;

namespace PromptHandlingService.Api;

public sealed class ActivityCatalogStorylineAdapter : IStorylineActivityCatalog
{
    private readonly IActivityCatalog _catalog;

    public ActivityCatalogStorylineAdapter(IActivityCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<ActivityCatalogItemDto> List()
    {
        return _catalog.All
            .Select(activity => new ActivityCatalogItemDto(
                activity.Id,
                activity.Family.ToString(),
                activity.Location,
                activity.LevelRange.Min,
                activity.LevelRange.Max,
                $"{activity.FactionPolicy.Requirement}; crossFaction={activity.FactionPolicy.AllowCrossFaction}",
                $"{activity.Activity} - {activity.Location}"))
            .OrderBy(activity => activity.Family, StringComparer.Ordinal)
            .ThenBy(activity => activity.DisplayLabel, StringComparer.Ordinal)
            .ToArray();
    }

    public bool Contains(string activityId)
    {
        return !string.IsNullOrWhiteSpace(activityId) &&
            _catalog.TryGetById(activityId, out _);
    }
}
