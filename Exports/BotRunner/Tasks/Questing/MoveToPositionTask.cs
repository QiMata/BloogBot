using BotRunner.Interfaces;
using GameData.Core.Models;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: move the player to a specific (x,y,z) position using pathfinding.
/// Pops itself when within tolerance distance or if combat interrupts.
/// Maps to ActionType.GOTO from StateManager.
/// </summary>
public class MoveToPositionTask(IBotContext botContext, Position targetPosition, float tolerance = 3.0f) : BotTask(botContext), IBotTask
{
    private readonly Position _targetPosition = targetPosition;
    private readonly float _tolerance = tolerance;

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            BotTasks.Pop();
            return;
        }

        // Check if we've arrived
        if (player.Position.DistanceTo(_targetPosition) <= _tolerance)
        {
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Combat interrupts movement — pop so combat tasks can run
        if (player.IsInCombat)
        {
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // NavigateToward uses NavigationPath caching (2s cooldown, 10-unit threshold)
        NavigateToward(_targetPosition);
    }
}
