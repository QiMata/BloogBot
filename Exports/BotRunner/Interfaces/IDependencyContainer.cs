using BotRunner.Clients;

namespace BotRunner.Interfaces;

/// <summary>
/// Container for dependencies injected into bot tasks.
/// </summary>
public interface IDependencyContainer
{
    /// <summary>
    /// Client for pathfinding service.
    /// </summary>
    PathfindingClient PathfindingClient { get; }
}
