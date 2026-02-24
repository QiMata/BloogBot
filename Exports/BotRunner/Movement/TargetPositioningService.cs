using BotRunner.Clients;
using GameData.Core.Interfaces;
using System;

namespace BotRunner.Movement
{
    public interface ITargetPositioningService
    {
        bool EnsureInCombatRange(IWoWUnit target);
    }

    public class TargetPositioningService : ITargetPositioningService
    {
        private readonly IObjectManager _objectManager;
        private readonly float _engagementRange;
        private readonly NavigationPath _navPath;

        public TargetPositioningService(IObjectManager objectManager, PathfindingClient pathfindingClient, float engagementRange = 25f)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            ArgumentNullException.ThrowIfNull(pathfindingClient);
            _engagementRange = engagementRange;
            _navPath = new NavigationPath(pathfindingClient);
        }

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

            float directDistance = playerPosition.DistanceTo(targetPosition);

            if (directDistance > _engagementRange)
            {
                var waypoint = _navPath.GetNextWaypoint(playerPosition, targetPosition, player.MapId);
                if (waypoint != null)
                {
                    _objectManager.MoveToward(waypoint);
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
