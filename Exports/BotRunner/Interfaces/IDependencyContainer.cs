namespace BotRunner.Interfaces;

/// <summary>
/// Container for dependencies injected into bot tasks.
/// </summary>
public interface IDependencyContainer
{
    /// <summary>
    /// Repository for quest-related database lookups.
    /// </summary>
    IQuestRepository? QuestRepository { get; }
}
