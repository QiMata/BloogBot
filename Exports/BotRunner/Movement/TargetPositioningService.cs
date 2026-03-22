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
<<<<<<< HEAD
        private readonly float _engagementRange;
        private readonly NavigationPath _navPath;
=======
        private readonly PathfindingClient _pathfindingClient;
        private readonly float _engagementRange;
        private NavigationPath? _navPath;
>>>>>>> cpp_physics_system

        public TargetPositioningService(IObjectManager objectManager, PathfindingClient pathfindingClient, float engagementRange = 25f)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
<<<<<<< HEAD
            ArgumentNullException.ThrowIfNull(pathfindingClient);
            _engagementRange = engagementRange;
            _navPath = new NavigationPath(pathfindingClient);
=======
            _pathfindingClient = pathfindingClient ?? throw new ArgumentNullException(nameof(pathfindingClient));
            _engagementRange = engagementRange;
>>>>>>> cpp_physics_system
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
                    nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(_objectManager, start, end),
                    race: player.Race,
                    gender: player.Gender);
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
<<<<<<< HEAD
                var waypoint = _navPath.GetNextWaypoint(playerPosition, targetPosition, player.MapId);
                if (waypoint != null)
                {
                    _objectManager.MoveToward(waypoint);
=======
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
>>>>>>> cpp_physics_system
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
