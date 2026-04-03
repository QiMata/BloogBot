using Pathfinding;

namespace BotRunner.Clients
{
    /// <summary>
    /// Abstraction for movement physics. Most BG runners execute physics locally
    /// from scene-backed Navigation.dll data, while PathfindingService remains as
    /// the shared fallback when local scene data is unavailable.
    /// </summary>
    public interface IPhysicsClient
    {
        PhysicsOutput PhysicsStep(PhysicsInput input);
    }
}
