using BotRunner.Clients;

namespace BotRunner.Interfaces;

/// <summary>
/// Container for dependencies injected into bot tasks.
/// Provides pathfinding, class-specific task factories, and optional repositories.
/// </summary>
public interface IDependencyContainer
{
    /// <summary>
    /// Repository for quest-related database lookups.
    /// </summary>
    IQuestRepository? QuestRepository { get; }

    /// <summary>
    /// Client for pathfinding and physics services.
    /// </summary>
    PathfindingClient PathfindingClient { get; }

    /// <summary>
    /// Class/spec-specific task factories (rest, pull, combat, buff).
    /// </summary>
    IClassContainer ClassContainer { get; }
}
