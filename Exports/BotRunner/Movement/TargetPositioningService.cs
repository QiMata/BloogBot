using BotRunner.Clients;
using GameData.Core.Constants;
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
        private readonly PathfindingClient _pathfindingClient;
        private readonly float _engagementRange;
        private NavigationPath? _navPath;

        public TargetPositioningService(IObjectManager objectManager, PathfindingClient pathfindingClient, float engagementRange = 25f)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            _pathfindingClient = pathfindingClient ?? throw new ArgumentNullException(nameof(pathfindingClient));
            _engagementRange = engagementRange;
        }

        public bool EnsureInCombatRange(IWoWUnit target)
        {
            ArgumentNullException.ThrowIfNull(target);

            var player = _objectManager.Player;

            if (player == null)
            {
                return false;
            }

            if (_navPath == null)
            {
                var (radius, height) = RaceDimensions.GetCapsuleForRace(player.Race, player.Gender);
                _navPath = new NavigationPath(_pathfindingClient,
                    capsuleRadius: radius,
                    capsuleHeight: height,
                    nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(_objectManager, start, end));
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
                var waypoint = _navPath.GetNextWaypoint(
                    playerPosition,
                    targetPosition,
                    player.MapId,
                    allowDirectFallback: false);
                if (waypoint != null)
                {
                    _objectManager.MoveToward(waypoint);
                }
                else
                {
                    _objectManager.StopAllMovement();
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
