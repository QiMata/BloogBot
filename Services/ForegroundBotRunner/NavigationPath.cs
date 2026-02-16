using BotRunner.Clients;
using GameData.Core.Models;
using Serilog;
using System;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Manages a path of waypoints from the pathfinding service.
    /// Tracks progress through the path and handles recalculation.
    /// </summary>
    public class NavigationPath(PathfindingClient? pathfinding)
    {
        private readonly PathfindingClient? _pathfinding = pathfinding;
        private Position[] _waypoints = [];
        private int _currentIndex;
        private Position _destination;
        private int _lastCalculation;

        private const float WAYPOINT_REACH_DISTANCE = 3f;
        private const float RECALCULATE_DISTANCE = 10f;
        private const int RECALCULATE_COOLDOWN_MS = 2000;

        /// <summary>
        /// Gets the next waypoint to move toward, or the direct destination if no path is available.
        /// </summary>
        public Position? GetNextWaypoint(Position currentPosition, Position destination, uint mapId)
        {
            // If no pathfinding client, fall back to direct movement
            if (_pathfinding == null)
                return destination;

            // Check if destination changed significantly
            if (_destination == null || _destination.DistanceTo(destination) > RECALCULATE_DISTANCE)
            {
                CalculatePath(currentPosition, destination, mapId);
            }

            // No waypoints available - fall back to direct movement
            if (_waypoints.Length == 0)
                return destination;

            // Advance past reached waypoints
            while (_currentIndex < _waypoints.Length &&
                   currentPosition.DistanceTo(_waypoints[_currentIndex]) < WAYPOINT_REACH_DISTANCE)
            {
                _currentIndex++;
            }

            // All waypoints reached
            if (_currentIndex >= _waypoints.Length)
                return destination;

            return _waypoints[_currentIndex];
        }

        /// <summary>
        /// Calculate a new path from start to end.
        /// </summary>
        public void CalculatePath(Position start, Position end, uint mapId)
        {
            // Throttle recalculations
            if (Environment.TickCount - _lastCalculation < RECALCULATE_COOLDOWN_MS)
                return;

            _lastCalculation = Environment.TickCount;
            _destination = end;

            if (_pathfinding == null)
            {
                _waypoints = [];
                _currentIndex = 0;
                return;
            }

            try
            {
                _waypoints = _pathfinding.GetPath(mapId, start, end, true);
                _currentIndex = 0;

                if (_waypoints.Length > 1)
                {
                    Log.Debug("[NavigationPath] Path calculated: {WaypointCount} waypoints", _waypoints.Length);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[NavigationPath] Path calculation failed, using direct movement");
                _waypoints = [];
                _currentIndex = 0;
            }
        }

        /// <summary>
        /// Clear the current path.
        /// </summary>
        public void Clear()
        {
            _waypoints = [];
            _currentIndex = 0;
            _destination = null!;
        }

        /// <summary>
        /// Whether the path has remaining waypoints.
        /// </summary>
        public bool HasWaypoints => _waypoints.Length > 0 && _currentIndex < _waypoints.Length;

        /// <summary>
        /// Number of remaining waypoints.
        /// </summary>
        public int RemainingWaypoints => Math.Max(0, _waypoints.Length - _currentIndex);
    }
}
