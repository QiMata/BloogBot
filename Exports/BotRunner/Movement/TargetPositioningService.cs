using BotRunner.Clients;
using GameData.Core.Interfaces;
using System;

namespace BotRunner.Movement
{
    public interface ITargetPositioningService
    {
        bool EnsureInCombatRange(IWoWUnit target);
    }

    public class TargetPositioningService(IObjectManager objectManager, PathfindingClient pathfindingClient, float engagementRange = 25f) : ITargetPositioningService
    {
        private readonly IObjectManager _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
        private readonly PathfindingClient _pathfindingClient = pathfindingClient ?? throw new ArgumentNullException(nameof(pathfindingClient));
        private readonly float _engagementRange = engagementRange;

        public bool EnsureInCombatRange(IWoWUnit target)
        {
            ArgumentNullException.ThrowIfNull(target);

            var player = _objectManager.Player;

            if (player == null)
            {
                return false;
            }

            var playerPosition = player.Position;
            var targetPosition = target.Position;

            if (playerPosition == null || targetPosition == null)
            {
                return false;
            }

            var pathDistance = _pathfindingClient.GetPathingDistance(player.MapId, playerPosition, targetPosition);

            if (pathDistance > _engagementRange)
            {
                var positions = _pathfindingClient.GetPath(player.MapId, playerPosition, targetPosition, true);
                var nextWaypoint = BotRunnerService.ResolveNextWaypoint(positions);

                if (nextWaypoint != null)
                {
                    _objectManager.MoveToward(nextWaypoint);
                }

                return false;
            }

            if (!player.IsFacing(target))
            {
                _objectManager.Face(targetPosition);
                return false;
            }

            _objectManager.StopAllMovement();
            return true;
        }
    }
}
