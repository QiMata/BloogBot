using Pathfinding;

namespace BotRunner.Clients
{
    /// <summary>
    /// Abstraction for physics step computation. Allows swapping between:
    /// - LocalPhysicsClient: direct P/Invoke to Navigation.dll (zero latency, binary parity)
    /// - PathfindingClient: TCP to PathfindingService (existing, adds 5-20ms latency)
    /// </summary>
    public interface IPhysicsClient
    {
        PhysicsOutput PhysicsStep(PhysicsInput input);
    }
}
