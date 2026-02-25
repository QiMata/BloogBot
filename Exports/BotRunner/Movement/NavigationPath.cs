using BotRunner.Clients;
using GameData.Core.Models;
using System.Collections.Generic;
using System;

namespace BotRunner.Movement;

/// <summary>
/// Manages a path of waypoints from the pathfinding service.
/// Tracks progress through the path and handles recalculation.
/// Caches the path so we don't re-query pathfinding every update tick.
/// </summary>
public class NavigationPath(
    PathfindingClient? pathfinding,
    Func<long>? tickProvider = null,
    bool enableProbeHeuristics = true,
    bool enableDynamicProbeSkipping = true,
    bool strictPathValidation = false)
{
    private readonly PathfindingClient? _pathfinding = pathfinding;
    private readonly Func<long> _tickProvider = tickProvider ?? (() => Environment.TickCount64);
    private readonly bool _enableProbeHeuristics = enableProbeHeuristics;
    private readonly bool _enableDynamicProbeSkipping = enableProbeHeuristics && enableDynamicProbeSkipping;
    private readonly bool _strictPathValidation = strictPathValidation;
    private Position[] _waypoints = [];
    private int _currentIndex;
    private Position? _destination;
    private long _lastCalculationTick;
    private bool _hasCalculatedPath;
    private Position? _lastWaypointSamplePosition;
    private float _lastWaypointSampleDistance = float.NaN;
    private int _stalledNearWaypointSamples;

    private const float WAYPOINT_REACH_DISTANCE = 3f;
    private const float CORNER_COMMIT_DISTANCE = 1.25f;
    private const float RECALCULATE_DISTANCE = 10f;
    private const int RECALCULATE_COOLDOWN_MS = 2000;
    private const float STALLED_NEAR_WAYPOINT_DISTANCE = 8f;
    private const float STALLED_SAMPLE_POSITION_EPSILON = 0.15f;
    private const float STALLED_SAMPLE_DISTANCE_EPSILON = 0.1f;
    private const int STALLED_SAMPLE_THRESHOLD = 24;
    private const int WAYPOINT_REACHABILITY_SCAN_LIMIT = 12;
    private const float PATH_POINT_DEDUP_EPSILON = 0.05f;
    private const float MAX_FIRST_WAYPOINT_DISTANCE = 120f;
    private const float MIN_DESTINATION_PROGRESS = 1f;
    private const float MAX_SEGMENT_DISTANCE = 1200f;
    private const float PATH_TRAVERSABILITY_SEGMENT_EPSILON = 0.05f;
    private const float STRICT_DESTINATION_ENDPOINT_DISTANCE = 8f;
    private const float MAX_PROBE_SEGMENT_DISTANCE = 2f;
    private const float PROBE_COLLINEARITY_DOT_MIN = 0.985f;

    /// <summary>
    /// Gets the next waypoint to move toward, or the direct destination if no path is available.
    /// Automatically calculates/recalculates the path as needed.
    /// </summary>
    public Position? GetNextWaypoint(Position currentPosition, Position destination, uint mapId, bool allowDirectFallback = true, float minWaypointDistance = 0f)
    {
        if (_pathfinding == null)
            return ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);

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
                return ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
            }
        }

        var waypointAdvanceDistance = MathF.Max(WAYPOINT_REACH_DISTANCE, minWaypointDistance);

        // Advance reached waypoints, but avoid skipping corner waypoints into blocked segments.
        AdvanceReachableWaypoints(currentPosition, mapId, waypointAdvanceDistance);

        if (_currentIndex >= _waypoints.Length)
        {
            // If we're still not near the destination, recalculate path periodically.
            if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
            {
                CalculatePath(currentPosition, destination, mapId);
            }

            if (_currentIndex >= _waypoints.Length)
                return ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
        }

        if (!TryResolveWaypoint(currentPosition, destination, mapId, waypointAdvanceDistance, allowDirectFallback, out var waypoint))
            return null;

        if (waypoint == null)
            return ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);

        var waypointDistance = currentPosition.DistanceTo(waypoint);
        if (_currentIndex >= _waypoints.Length)
        {
            if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
                CalculatePath(currentPosition, destination, mapId);
            if (_currentIndex >= _waypoints.Length)
                return ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
        }

        // If the next waypoint remains near while the bot itself does not move,
        // skip it so callers don't repeatedly drive a blocked micro-corner.
        if (_lastWaypointSamplePosition != null
            && waypointDistance <= STALLED_NEAR_WAYPOINT_DISTANCE
            && currentPosition.DistanceTo(_lastWaypointSamplePosition) <= STALLED_SAMPLE_POSITION_EPSILON
            && !float.IsNaN(_lastWaypointSampleDistance)
            && MathF.Abs(waypointDistance - _lastWaypointSampleDistance) <= STALLED_SAMPLE_DISTANCE_EPSILON)
        {
            _stalledNearWaypointSamples++;
            if (_stalledNearWaypointSamples >= STALLED_SAMPLE_THRESHOLD)
            {
                // In strict mode, never advance a stalled corner by index only.
                // Recalculate so we keep following service-validated turns.
                if (_strictPathValidation || !CanAdvanceToNextWaypoint(currentPosition, mapId, waypointDistance))
                {
                    CalculatePath(currentPosition, destination, mapId, force: true);
                    AdvanceReachableWaypoints(currentPosition, mapId, waypointAdvanceDistance);
                }
                else
                {
                    _currentIndex++;
                }
                _stalledNearWaypointSamples = 0;
                _lastWaypointSampleDistance = float.NaN;
                _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);

                if (_currentIndex >= _waypoints.Length)
                {
                    if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
                        CalculatePath(currentPosition, destination, mapId);
                    if (_currentIndex >= _waypoints.Length)
                        return ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
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
    private void AdvanceReachableWaypoints(Position currentPosition, uint mapId, float waypointAdvanceDistance)
    {
        while (_currentIndex < _waypoints.Length)
        {
            var distanceToWaypoint = currentPosition.DistanceTo(_waypoints[_currentIndex]);
            if (distanceToWaypoint >= waypointAdvanceDistance)
                break;

            if (_enableDynamicProbeSkipping
                && ShouldSkipProbeWaypoint(currentPosition, mapId, waypointAdvanceDistance, distanceToWaypoint))
            {
                _currentIndex++;
                continue;
            }

            if (!CanAdvanceToNextWaypoint(currentPosition, mapId, distanceToWaypoint))
                break;

            _currentIndex++;
        }
    }

    private bool CanAdvanceToNextWaypoint(Position currentPosition, uint mapId, float distanceToCurrentWaypoint)
    {
        if (_currentIndex + 1 >= _waypoints.Length)
            return true;

        // Only commit to next waypoint when we've actually reached this corner.
        // LOS-only skips can cut through geometry on tight turns.
        if (distanceToCurrentWaypoint > CORNER_COMMIT_DISTANCE)
            return false;

        if (!_strictPathValidation)
            return true;

        // Strict mode keeps corner adherence unless the immediate next waypoint is visible.
        return TryGetLineOfSight(currentPosition, _waypoints[_currentIndex + 1], mapId, out var nextWaypointVisible)
            && nextWaypointVisible;
    }

    private bool ShouldSkipProbeWaypoint(
        Position currentPosition,
        uint mapId,
        float waypointAdvanceDistance,
        float distanceToWaypoint)
    {
        if (_currentIndex + 1 >= _waypoints.Length)
            return false;

        // Keep normal corner commitment for very-close waypoints and only probe-skip
        // short, nearly-collinear lead-in points while still honoring LOS.
        if (distanceToWaypoint <= CORNER_COMMIT_DISTANCE || distanceToWaypoint > waypointAdvanceDistance)
            return false;

        var currentWaypoint = _waypoints[_currentIndex];
        var nextWaypoint = _waypoints[_currentIndex + 1];
        var nextSegmentDistance2D = currentWaypoint.DistanceTo2D(nextWaypoint);
        if (nextSegmentDistance2D > MAX_PROBE_SEGMENT_DISTANCE)
            return false;

        var leadDistance2D = currentPosition.DistanceTo2D(currentWaypoint);
        if (leadDistance2D <= PATH_POINT_DEDUP_EPSILON || nextSegmentDistance2D <= PATH_POINT_DEDUP_EPSILON)
            return true;

        var ax = currentWaypoint.X - currentPosition.X;
        var ay = currentWaypoint.Y - currentPosition.Y;
        var bx = nextWaypoint.X - currentWaypoint.X;
        var by = nextWaypoint.Y - currentWaypoint.Y;
        var collinearityDot = (ax * bx + ay * by) / (leadDistance2D * nextSegmentDistance2D);
        if (collinearityDot < PROBE_COLLINEARITY_DOT_MIN)
            return false;

        return TryGetLineOfSight(currentPosition, nextWaypoint, mapId, out var nextWaypointVisible)
            && nextWaypointVisible;
    }

    private bool TryResolveWaypoint(
        Position currentPosition,
        Position destination,
        uint mapId,
        float waypointAdvanceDistance,
        bool allowDirectFallback,
        out Position? waypoint)
    {
        waypoint = null;
        if (_currentIndex >= _waypoints.Length)
            return true;

        waypoint = _waypoints[_currentIndex];
        if (!TryGetLineOfSight(currentPosition, waypoint, mapId, out var isInLineOfSight))
        {
            if (!_strictPathValidation)
                return true;

            CalculatePath(currentPosition, destination, mapId, force: true);
            AdvanceReachableWaypoints(currentPosition, mapId, waypointAdvanceDistance);
            if (_currentIndex >= _waypoints.Length)
            {
                waypoint = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
                return waypoint != null;
            }

            waypoint = _waypoints[_currentIndex];
            if (!TryGetLineOfSight(currentPosition, waypoint, mapId, out isInLineOfSight))
                return false;
        }

        if (isInLineOfSight)
            return true;

        if (!_strictPathValidation
            && currentPosition.DistanceTo(waypoint) <= CORNER_COMMIT_DISTANCE
            && TryPromoteReachableWaypoint(currentPosition, mapId, out waypoint))
            return true;

        CalculatePath(currentPosition, destination, mapId, force: true);
        AdvanceReachableWaypoints(currentPosition, mapId, waypointAdvanceDistance);
        if (_currentIndex >= _waypoints.Length)
        {
            waypoint = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
            return waypoint != null;
        }

        waypoint = _waypoints[_currentIndex];
        if (!TryGetLineOfSight(currentPosition, waypoint, mapId, out isInLineOfSight))
            return !_strictPathValidation;

        if (isInLineOfSight)
            return true;

        return !_strictPathValidation
            && currentPosition.DistanceTo(waypoint) <= CORNER_COMMIT_DISTANCE
            && TryPromoteReachableWaypoint(currentPosition, mapId, out waypoint);
    }

    private bool TryPromoteReachableWaypoint(Position currentPosition, uint mapId, out Position? waypoint)
    {
        waypoint = null;
        if (_currentIndex >= _waypoints.Length)
            return false;

        var maxIndex = Math.Min(_waypoints.Length - 1, _currentIndex + WAYPOINT_REACHABILITY_SCAN_LIMIT);
        for (var index = _currentIndex + 1; index <= maxIndex; index++)
        {
            if (!TryGetLineOfSight(currentPosition, _waypoints[index], mapId, out var isInLineOfSight))
                return false;

            if (!isInLineOfSight)
                continue;

            _currentIndex = index;
            waypoint = _waypoints[_currentIndex];
            return true;
        }

        return false;
    }

    private Position? ResolveDirectFallback(Position currentPosition, Position destination, uint mapId, bool allowDirectFallback)
    {
        if (!allowDirectFallback)
            return null;

        if (_pathfinding == null)
            return destination;

        return HasLineOfSight(currentPosition, destination, mapId) ? destination : null;
    }

    private bool HasLineOfSight(Position from, Position to, uint mapId)
    {
        return TryGetLineOfSight(from, to, mapId, out var isInLineOfSight) && isInLineOfSight;
    }

    private bool TryGetLineOfSight(Position from, Position to, uint mapId, out bool isInLineOfSight)
    {
        if (_pathfinding == null)
        {
            isInLineOfSight = true;
            return true;
        }

        try
        {
            isInLineOfSight = _pathfinding.IsInLineOfSight(mapId, from, to);
            return true;
        }
        catch
        {
            isInLineOfSight = false;
            return false;
        }
    }

    private static bool IsFinitePosition(Position position)
        => float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);

    private static Position[] SanitizePath(Position[]? path)
    {
        if (path == null || path.Length == 0)
            return [];

        var sanitized = new List<Position>(path.Length);
        foreach (var point in path)
        {
            if (!IsFinitePosition(point))
                return [];

            if (sanitized.Count > 0 && sanitized[^1].DistanceTo(point) <= PATH_POINT_DEDUP_EPSILON)
                continue;

            sanitized.Add(new Position(point.X, point.Y, point.Z));
        }

        return sanitized.ToArray();
    }

    private static Position[] PruneProbeWaypoints(Position start, IReadOnlyList<Position> path)
    {
        if (path.Count <= 1)
            return path.Count == 0 ? [] : [path[0]];

        var pruned = new List<Position>(path.Count);
        var previousAnchor = start;

        for (var index = 0; index < path.Count; index++)
        {
            var current = path[index];
            if (index + 1 >= path.Count)
            {
                pruned.Add(current);
                break;
            }

            var next = path[index + 1];
            if (ShouldPruneProbeWaypoint(previousAnchor, current, next))
                continue;

            pruned.Add(current);
            previousAnchor = current;
        }

        return pruned.ToArray();
    }

    private static bool ShouldPruneProbeWaypoint(Position previous, Position current, Position next)
    {
        var nextSegmentDistance2D = current.DistanceTo2D(next);
        if (nextSegmentDistance2D > MAX_PROBE_SEGMENT_DISTANCE)
            return false;

        var leadDistance2D = previous.DistanceTo2D(current);
        if (leadDistance2D <= PATH_POINT_DEDUP_EPSILON || nextSegmentDistance2D <= PATH_POINT_DEDUP_EPSILON)
            return true;

        var ax = current.X - previous.X;
        var ay = current.Y - previous.Y;
        var bx = next.X - current.X;
        var by = next.Y - current.Y;
        var collinearityDot = (ax * bx + ay * by) / (leadDistance2D * nextSegmentDistance2D);
        return collinearityDot >= PROBE_COLLINEARITY_DOT_MIN;
    }

    private static bool HasDestinationProgress(Position start, Position end, IReadOnlyList<Position> path)
    {
        var startToEndDistance = start.DistanceTo(end);
        if (startToEndDistance <= WAYPOINT_REACH_DISTANCE)
            return true;

        var bestDistanceToEnd = startToEndDistance;
        foreach (var point in path)
            bestDistanceToEnd = MathF.Min(bestDistanceToEnd, point.DistanceTo(end));

        return bestDistanceToEnd <= startToEndDistance - MIN_DESTINATION_PROGRESS;
    }

    private static bool HasDestinationClosure(Position end, IReadOnlyList<Position> path)
    {
        if (path.Count == 0)
            return false;

        var finalWaypoint = path[^1];
        return finalWaypoint.DistanceTo(end) <= STRICT_DESTINATION_ENDPOINT_DISTANCE;
    }

    private static bool HasSaneSegments(IReadOnlyList<Position> path)
    {
        if (path.Count <= 1)
            return true;

        for (var i = 0; i < path.Count - 1; i++)
        {
            if (path[i].DistanceTo(path[i + 1]) > MAX_SEGMENT_DISTANCE)
                return false;
        }

        return true;
    }

    private bool HasTraversableSegments(uint mapId, Position start, IReadOnlyList<Position> path)
    {
        if (_pathfinding == null || path.Count == 0)
            return true;

        var from = start;
        for (var i = 0; i < path.Count; i++)
        {
            var to = path[i];
            if (from.DistanceTo(to) <= PATH_TRAVERSABILITY_SEGMENT_EPSILON)
            {
                from = to;
                continue;
            }

            // In strict mode LOS probe failures invalidate the path.
            // In non-strict mode, tolerate transient LOS probe failures.
            if (!TryGetLineOfSight(from, to, mapId, out var hasLineOfSight))
            {
                if (_strictPathValidation)
                    return false;

                from = to;
                continue;
            }

            if (!hasLineOfSight)
                return false;

            from = to;
        }

        return true;
    }

    private bool IsPathUsable(uint mapId, Position start, Position end, Position[] path)
    {
        if (path.Length == 0)
            return false;

        if (start.DistanceTo(path[0]) > MAX_FIRST_WAYPOINT_DISTANCE)
            return false;

        if (_strictPathValidation && !HasDestinationClosure(end, path))
            return false;

        return HasSaneSegments(path)
            && HasDestinationProgress(start, end, path)
            && HasTraversableSegments(mapId, start, path);
    }

    private Position[] GetValidatedPath(uint mapId, Position start, Position end, bool smoothPath)
    {
        if (_pathfinding == null)
            return [];

        var rawPath = _pathfinding.GetPath(mapId, start, end, smoothPath);
        var sanitizedPath = SanitizePath(rawPath);
        var prunedPath = _enableProbeHeuristics
            ? PruneProbeWaypoints(start, sanitizedPath)
            : sanitizedPath;
        return IsPathUsable(mapId, start, end, prunedPath) ? prunedPath : [];
    }

    public void CalculatePath(Position start, Position end, uint mapId, bool force = false)
    {
        var nowTick = _tickProvider();
        if (!force && _hasCalculatedPath && nowTick - _lastCalculationTick < RECALCULATE_COOLDOWN_MS)
            return;

        _lastCalculationTick = nowTick;
        _hasCalculatedPath = true;
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
            _waypoints = GetValidatedPath(mapId, start, end, smoothPath: false);
            if (_waypoints.Length == 0)
                _waypoints = GetValidatedPath(mapId, start, end, smoothPath: true);

            // Always begin at index 0. GetNextWaypoint() will safely advance
            // near/duplicate start points with LOS guards instead of blindly
            // skipping a potential first corner waypoint.
            _currentIndex = 0;
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
        _lastCalculationTick = 0;
        _hasCalculatedPath = false;
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
