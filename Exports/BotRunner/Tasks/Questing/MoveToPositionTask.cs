using BotRunner.Interfaces;
using GameData.Core.Models;

namespace BotRunner.Tasks.Questing;

/// <summary>
/// Task that moves the player to a specific position using pathfinding.
/// </summary>
public class MoveToPositionTask : BotTask, IBotTask
{
    private readonly Position _targetPosition;
    private readonly float _tolerance;
    private List<Position>? _path;
    private int _currentPathIndex;
    private DateTime _lastPathRequest;
    private const int PATH_REQUEST_COOLDOWN_MS = 1000;

    public MoveToPositionTask(IBotContext botContext, Position targetPosition, float tolerance = 3.0f)
        : base(botContext)
    {
        _targetPosition = targetPosition;
        _tolerance = tolerance;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            BotTasks.Pop();
            return;
        }

        // Check if we've arrived
        var distanceToTarget = player.Position.DistanceTo(_targetPosition);
        if (distanceToTarget <= _tolerance)
        {
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Check if we're in combat - interrupt movement
        if (player.IsInCombat)
        {
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Request path if needed
        if (_path == null || _path.Count == 0)
        {
            if ((DateTime.Now - _lastPathRequest).TotalMilliseconds < PATH_REQUEST_COOLDOWN_MS)
                return;

            _lastPathRequest = DateTime.Now;
            // For now, use direct movement - pathfinding service integration would go here
            _path = new List<Position> { _targetPosition };
            _currentPathIndex = 0;
            return;
        }

        // Get current waypoint
        var currentWaypoint = _path[_currentPathIndex];
        var distanceToWaypoint = player.Position.DistanceTo(currentWaypoint);

        // Check if we've reached current waypoint
        if (distanceToWaypoint <= _tolerance)
        {
            _currentPathIndex++;
            if (_currentPathIndex >= _path.Count)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }
            currentWaypoint = _path[_currentPathIndex];
        }

        // Move toward waypoint
        ObjectManager.MoveToward(currentWaypoint);
    }
}
