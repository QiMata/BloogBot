using BotRunner.Clients;
using GameData.Core.Models;
using System;

namespace BotRunner.Movement;

/// <summary>
/// Manages a path of waypoints from the pathfinding service.
/// Tracks progress through the path and handles recalculation.
/// Caches the path so we don't re-query pathfinding every update tick.
/// </summary>
public class NavigationPath(PathfindingClient? pathfinding, Func<long>? tickProvider = null)
{
    private readonly PathfindingClient? _pathfinding = pathfinding;
    private readonly Func<long> _tickProvider = tickProvider ?? (() => Environment.TickCount64);
    private Position[] _waypoints = [];
    private int _currentIndex;
    private Position? _destination;
    private long _lastCalculationTick = -RECALCULATE_COOLDOWN_MS;
    private Position? _lastWaypointSamplePosition;
    private float _lastWaypointSampleDistance = float.NaN;
    private int _stalledNearWaypointSamples;

    private const float WAYPOINT_REACH_DISTANCE = 3f;
    private const float RECALCULATE_DISTANCE = 10f;
    private const int RECALCULATE_COOLDOWN_MS = 2000;
    private const float STALLED_NEAR_WAYPOINT_DISTANCE = 8f;
    private const float STALLED_SAMPLE_POSITION_EPSILON = 0.15f;
    private const float STALLED_SAMPLE_DISTANCE_EPSILON = 0.1f;
    private const int STALLED_SAMPLE_THRESHOLD = 24;

    /// <summary>
    /// Gets the next waypoint to move toward, or the direct destination if no path is available.
    /// Automatically calculates/recalculates the path as needed.
    /// </summary>
    public Position? GetNextWaypoint(Position currentPosition, Position destination, uint mapId, bool allowDirectFallback = true, float minWaypointDistance = 0f)
    {
        if (_pathfinding == null)
            return allowDirectFallback ? destination : null;

        // Recalculate if destination changed significantly
        if (_destination == null || _destination.DistanceTo(destination) > RECALCULATE_DISTANCE)
        {
            CalculatePath(currentPosition, destination, mapId);
        }

        if (_waypoints.Length == 0)
        {
            if (!allowDirectFallback)
            {
                CalculatePath(currentPosition, destination, mapId);
                if (_waypoints.Length == 0)
                    return null;
            }
            else
            {
                return destination;
            }
        }

        var waypointAdvanceDistance = MathF.Max(WAYPOINT_REACH_DISTANCE, minWaypointDistance);

        // Advance past reached (or intentionally skipped-near) waypoints
        while (_currentIndex < _waypoints.Length &&
               currentPosition.DistanceTo(_waypoints[_currentIndex]) < waypointAdvanceDistance)
        {
            _currentIndex++;
        }

        if (_currentIndex >= _waypoints.Length)
        {
            // If we're still not near the destination, recalculate path periodically.
            if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
            {
                CalculatePath(currentPosition, destination, mapId);
            }

            if (_currentIndex >= _waypoints.Length)
                return allowDirectFallback ? destination : null;
        }

        // If the next waypoint remains near while the bot itself does not move,
        // skip it so callers don't repeatedly drive a blocked micro-corner.
        var waypoint = _waypoints[_currentIndex];
        var waypointDistance = currentPosition.DistanceTo(waypoint);
        if (_lastWaypointSamplePosition != null
            && waypointDistance <= STALLED_NEAR_WAYPOINT_DISTANCE
            && currentPosition.DistanceTo(_lastWaypointSamplePosition) <= STALLED_SAMPLE_POSITION_EPSILON
            && !float.IsNaN(_lastWaypointSampleDistance)
            && MathF.Abs(waypointDistance - _lastWaypointSampleDistance) <= STALLED_SAMPLE_DISTANCE_EPSILON)
        {
            _stalledNearWaypointSamples++;
            if (_stalledNearWaypointSamples >= STALLED_SAMPLE_THRESHOLD)
            {
                _currentIndex++;
                _stalledNearWaypointSamples = 0;
                _lastWaypointSampleDistance = float.NaN;
                _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);

                if (_currentIndex >= _waypoints.Length)
                {
                    if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
                        CalculatePath(currentPosition, destination, mapId);
                    if (_currentIndex >= _waypoints.Length)
                        return allowDirectFallback ? destination : null;
                }

                waypoint = _waypoints[_currentIndex];
                waypointDistance = currentPosition.DistanceTo(waypoint);
            }
        }
        else
        {
            _stalledNearWaypointSamples = 0;
        }

        _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);
        _lastWaypointSampleDistance = waypointDistance;
        return waypoint;
    }

    /// <summary>
    /// Calculate a new path from start to end.
    /// </summary>
    public void CalculatePath(Position start, Position end, uint mapId)
    {
        var nowTick = _tickProvider();
        if (nowTick - _lastCalculationTick < RECALCULATE_COOLDOWN_MS)
            return;

        _lastCalculationTick = nowTick;
        _destination = end;
        _lastWaypointSamplePosition = null;
        _lastWaypointSampleDistance = float.NaN;
        _stalledNearWaypointSamples = 0;

        if (_pathfinding == null)
        {
            _waypoints = [];
            _currentIndex = 0;
            return;
        }

        try
        {
            _waypoints = _pathfinding.GetPath(mapId, start, end, true);
            if (_waypoints.Length == 0)
                _waypoints = _pathfinding.GetPath(mapId, start, end, false);
            // Skip waypoint[0] since it's usually the current position
            _currentIndex = _waypoints.Length > 1 ? 1 : 0;
        }
        catch
        {
            _waypoints = [];
            _currentIndex = 0;
        }
    }

    /// <summary>
    /// Force a path recalculation on the next GetNextWaypoint call.
    /// Call this when the target changes (e.g., mob died, new target acquired).
    /// </summary>
    public void Clear()
    {
        _waypoints = [];
        _currentIndex = 0;
        _destination = null;
        _lastCalculationTick = -RECALCULATE_COOLDOWN_MS;
        _lastWaypointSamplePosition = null;
        _lastWaypointSampleDistance = float.NaN;
        _stalledNearWaypointSamples = 0;
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
