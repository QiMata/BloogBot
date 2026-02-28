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
    private float[] _waypointAcceptanceRadii = [];
    private int _currentIndex;
    private Position? _destination;
    private long _lastCalculationTick;
    private bool _hasCalculatedPath;
    private Position? _lastWaypointSamplePosition;
    private float _lastWaypointSampleDistance = float.NaN;
    private int _stalledNearWaypointSamples;

    // LOS-based string-pulling and runtime lookahead skip
    private const int MAX_STRINGPULL_LOOKAHEAD = 8;
    private const int MAX_RUNTIME_LOS_LOOKAHEAD = 6;
    private const long LOS_SKIP_CACHE_TTL_MS = 500;
    private int _losSkipCacheIndex = -1;
    private int _losSkipCacheFarthest = -1;
    private long _losSkipCacheTick;

    // Adaptive acceptance radius: turn angle at each waypoint determines how tightly
    // the bot must follow it. Straight paths get MAX, sharp corners get MIN.
    private const float MIN_ACCEPTANCE_RADIUS = 2f;       // at 90°+ corners
    private const float MAX_ACCEPTANCE_RADIUS = 6f;       // on straight paths
    private const float SHARP_TURN_ANGLE_DEG = 90f;       // angle that maps to MIN
    private const float WAYPOINT_REACH_DISTANCE = 3f;     // default fallback (no radii computed)
    private const float CORNER_COMMIT_DISTANCE = 1.25f;   // default fallback
    private const float RECALCULATE_DISTANCE = 10f;

    // Cliff/edge detection constants
    private const float CLIFF_PROBE_DISTANCE = 3f;        // probe ground 3yd ahead
    private const float CLIFF_DROP_THRESHOLD = 8f;         // 8yd drop = cliff danger
    private const float CLIFF_LETHAL_DROP = 50f;           // guaranteed death fall

    // Jump physics constraints (derived from PhysicsConstants)
    private const float JUMP_VELOCITY = 7.95577f;
    private const float GRAVITY = 19.2911f;
    private const float MAX_JUMP_HEIGHT = 1.64f;           // JUMP_VELOCITY^2 / (2*GRAVITY)
    private const float MAX_JUMP_DISTANCE_2D = 8f;         // conservative horizontal max at run speed
    private const float GAP_DETECTION_DEPTH_MIN = 3f;      // minimum gap depth to consider
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

        // When adaptive radii are computed, they are the primary distance thresholds.
        // WAYPOINT_REACH_DISTANCE is only the fallback when no radii exist.
        // The caller's minWaypointDistance always acts as a floor.
        var waypointAdvanceDistance = MathF.Max(WAYPOINT_REACH_DISTANCE, minWaypointDistance);

        // Advance reached waypoints, but avoid skipping corner waypoints into blocked segments.
        AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);

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

        if (!TryResolveWaypoint(currentPosition, destination, mapId, minWaypointDistance, allowDirectFallback, out var waypoint))
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
                    AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
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
    private void AdvanceReachableWaypoints(Position currentPosition, uint mapId, float minWaypointDistance)
    {
        while (_currentIndex < _waypoints.Length)
        {
            // Adaptive radius is the primary threshold; WAYPOINT_REACH_DISTANCE is
            // only the fallback when no radii were computed. Caller's minWaypointDistance
            // is always a floor (e.g., corpse runs need a minimum approach distance).
            var effectiveRadius = _waypointAcceptanceRadii.Length > _currentIndex
                ? MathF.Max(_waypointAcceptanceRadii[_currentIndex], minWaypointDistance)
                : MathF.Max(WAYPOINT_REACH_DISTANCE, minWaypointDistance);

            var distanceToWaypoint = currentPosition.DistanceTo(_waypoints[_currentIndex]);
            if (distanceToWaypoint >= effectiveRadius)
                break;

            if (_enableDynamicProbeSkipping
                && ShouldSkipProbeWaypoint(currentPosition, mapId, effectiveRadius, distanceToWaypoint))
            {
                _currentIndex++;
                continue;
            }

            if (!CanAdvanceToNextWaypoint(currentPosition, mapId, distanceToWaypoint))
                break;

            _currentIndex++;
        }

        // After reaching the current waypoint, try to skip further ahead via LOS.
        if (_enableDynamicProbeSkipping && !_strictPathValidation && _currentIndex < _waypoints.Length)
            TryLosSkipAhead(currentPosition, mapId);
    }

    private bool CanAdvanceToNextWaypoint(Position currentPosition, uint mapId, float distanceToCurrentWaypoint)
    {
        if (_currentIndex + 1 >= _waypoints.Length)
            return true;

        // Use waypoint's acceptance radius as commit distance (already scaled for corners).
        var commitDistance = _waypointAcceptanceRadii.Length > _currentIndex
            ? _waypointAcceptanceRadii[_currentIndex]
            : CORNER_COMMIT_DISTANCE;

        if (distanceToCurrentWaypoint > commitDistance)
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
        float minWaypointDistance,
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
            AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
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

        // Non-strict: trust the navmesh waypoint even when LOS is blocked.
        if (!_strictPathValidation)
            return true;

        if (currentPosition.DistanceTo(waypoint) <= CORNER_COMMIT_DISTANCE
            && TryPromoteReachableWaypoint(currentPosition, mapId, out waypoint))
            return true;

        CalculatePath(currentPosition, destination, mapId, force: true);
        AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
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

        // Non-strict: trust the navmesh waypoint even when LOS is blocked.
        // Long paths (corpse runs) often have initial corners behind terrain.
        if (!_strictPathValidation)
            return true;

        return currentPosition.DistanceTo(waypoint) <= CORNER_COMMIT_DISTANCE
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

    private Position[] StringPullPath(uint mapId, Position start, Position[] path)
    {
        if (path.Length <= 2)
            return path;

        var pulled = new List<Position>(path.Length);
        var anchor = start;
        var anchorIndex = -1; // -1 = start position

        while (anchorIndex < path.Length - 1)
        {
            // Scan forward from anchor, find farthest waypoint with clear LOS.
            var farthestVisible = anchorIndex + 1;
            var scanLimit = Math.Min(path.Length - 1, anchorIndex + 1 + MAX_STRINGPULL_LOOKAHEAD);

            for (var candidate = anchorIndex + 2; candidate <= scanLimit; candidate++)
            {
                if (!TryGetLineOfSight(anchor, path[candidate], mapId, out var los) || !los)
                    break; // Geometry coherence: stop on first LOS failure.

                farthestVisible = candidate;
            }

            // Always preserve the waypoint we advance to (it's either the farthest
            // visible or the next one if nothing further was visible).
            pulled.Add(path[farthestVisible]);
            anchor = path[farthestVisible];
            anchorIndex = farthestVisible;
        }

        return pulled.ToArray();
    }

    private bool TryLosSkipAhead(Position currentPosition, uint mapId)
    {
        if (_currentIndex + 1 >= _waypoints.Length)
            return false;

        var nowTick = _tickProvider();

        // Use cached result if still valid and the index hasn't changed.
        if (_losSkipCacheIndex == _currentIndex
            && nowTick - _losSkipCacheTick < LOS_SKIP_CACHE_TTL_MS
            && _losSkipCacheFarthest > _currentIndex)
        {
            _currentIndex = _losSkipCacheFarthest;
            return true;
        }

        var farthestVisible = _currentIndex;
        var scanLimit = Math.Min(_waypoints.Length - 1, _currentIndex + MAX_RUNTIME_LOS_LOOKAHEAD);

        for (var candidate = _currentIndex + 1; candidate <= scanLimit; candidate++)
        {
            if (!TryGetLineOfSight(currentPosition, _waypoints[candidate], mapId, out var los) || !los)
                break;

            farthestVisible = candidate;
        }

        // Cache the result regardless.
        _losSkipCacheIndex = _currentIndex;
        _losSkipCacheFarthest = farthestVisible;
        _losSkipCacheTick = nowTick;

        if (farthestVisible <= _currentIndex)
            return false;

        _currentIndex = farthestVisible;
        return true;
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

        if (!HasSaneSegments(path) || !HasDestinationProgress(start, end, path))
            return false;

        // In non-strict mode, trust the navmesh path without collision-based LOS
        // validation between consecutive corners. Long outdoor paths (460y+ corpse
        // runs with 140+ corners) have many corner pairs where terrain/buildings
        // block LOS even though the navmesh route is valid and walkable.
        if (!_strictPathValidation)
            return true;

        return HasTraversableSegments(mapId, start, path);
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
        // LOS-based string-pulling: remove intermediate waypoints where a straight
        // line is unobstructed. Corners remain because LOS is blocked by walls.
        var pulledPath = _enableProbeHeuristics
            ? StringPullPath(mapId, start, prunedPath)
            : prunedPath;
        var usable = IsPathUsable(mapId, start, end, pulledPath);
        if (!usable && pulledPath.Length > 0)
        {
            Serilog.Log.Warning("[NavigationPath] Path rejected by IsPathUsable: raw={RawCount} sanitized={SanitizedCount} pruned={PrunedCount} pulled={PulledCount} smooth={Smooth} strict={Strict} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1})",
                rawPath.Length, sanitizedPath.Length, prunedPath.Length, pulledPath.Length, smoothPath, _strictPathValidation,
                start.X, start.Y, start.Z, end.X, end.Y, end.Z);
        }
        return usable ? pulledPath : [];
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
            _waypointAcceptanceRadii = [];
            _currentIndex = 0;
            return;
        }

        try
        {
            // When probe heuristics are enabled (normal navigation), prefer smooth
            // paths (Detour string-pulling) — fewer redundant waypoints.
            // When disabled (corpse runs), prefer non-smooth paths to avoid Detour
            // string-pulling routes through steep Z transitions that the headless
            // client can't safely descend.
            var preferSmooth = _enableProbeHeuristics;
            _waypoints = GetValidatedPath(mapId, start, end, smoothPath: preferSmooth);
            if (_waypoints.Length == 0)
                _waypoints = GetValidatedPath(mapId, start, end, smoothPath: !preferSmooth);

            // Always begin at index 0. GetNextWaypoint() will safely advance
            // near/duplicate start points with LOS guards instead of blindly
            // skipping a potential first corner waypoint.
            _currentIndex = 0;
            _waypointAcceptanceRadii = [];
            if (_enableProbeHeuristics)
                ComputeWaypointAcceptanceRadii(start);
        }
        catch
        {
            _waypoints = [];
            _waypointAcceptanceRadii = [];
            _currentIndex = 0;
        }
    }

    private void ComputeWaypointAcceptanceRadii(Position start)
    {
        _waypointAcceptanceRadii = new float[_waypoints.Length];
        for (var i = 0; i < _waypoints.Length; i++)
        {
            var prev = i == 0 ? start : _waypoints[i - 1];
            var curr = _waypoints[i];

            // Destination waypoint: always use tight radius so we stop precisely.
            if (i + 1 >= _waypoints.Length)
            {
                _waypointAcceptanceRadii[i] = MIN_ACCEPTANCE_RADIUS;
                continue;
            }

            var next = _waypoints[i + 1];
            var turnAngleDeg = ComputeTurnAngle2D(prev, curr, next);

            // Map: 0° (straight) → MAX_ACCEPTANCE, ≥90° → MIN_ACCEPTANCE
            var t = Math.Clamp(turnAngleDeg / SHARP_TURN_ANGLE_DEG, 0f, 1f);
            _waypointAcceptanceRadii[i] = MathF.Max(
                MIN_ACCEPTANCE_RADIUS,
                MAX_ACCEPTANCE_RADIUS - t * (MAX_ACCEPTANCE_RADIUS - MIN_ACCEPTANCE_RADIUS));
        }
    }

    internal static float ComputeTurnAngle2D(Position prev, Position curr, Position next)
    {
        var ax = curr.X - prev.X;
        var ay = curr.Y - prev.Y;
        var bx = next.X - curr.X;
        var by = next.Y - curr.Y;
        var lenA = MathF.Sqrt(ax * ax + ay * ay);
        var lenB = MathF.Sqrt(bx * bx + by * by);
        if (lenA < 0.01f || lenB < 0.01f) return 0f;
        var dot = (ax * bx + ay * by) / (lenA * lenB);
        return MathF.Acos(Math.Clamp(dot, -1f, 1f)) * (180f / MathF.PI);
    }

    /// <summary>
    /// Probes ground Z ahead of the current movement direction.
    /// Returns the drop distance if an edge/cliff is detected, or 0 if safe.
    /// Returns -1 if the probe is unavailable (no pathfinding client or probe failed).
    /// </summary>
    public float ProbeEdgeAhead(Position currentPos, Position targetWaypoint, uint mapId, float probeDistance = CLIFF_PROBE_DISTANCE)
    {
        if (_pathfinding == null) return -1f;

        var dx = targetWaypoint.X - currentPos.X;
        var dy = targetWaypoint.Y - currentPos.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) return 0f;

        var probeX = currentPos.X + dx / len * probeDistance;
        var probeY = currentPos.Y + dy / len * probeDistance;

        try
        {
            var (groundZ, found) = _pathfinding.GetGroundZ(mapId, new Position(probeX, probeY, currentPos.Z));
            if (!found) return float.MaxValue; // void/no ground = lethal
            var drop = currentPos.Z - groundZ;
            return drop > 0 ? drop : 0f;
        }
        catch
        {
            return -1f; // IPC failure
        }
    }

    /// <summary>
    /// Whether the next movement toward the target waypoint would go over a cliff edge.
    /// </summary>
    public bool IsCliffAhead(Position currentPos, Position targetWaypoint, uint mapId)
    {
        var drop = ProbeEdgeAhead(currentPos, targetWaypoint, mapId);
        return drop >= CLIFF_DROP_THRESHOLD;
    }

    /// <summary>
    /// Whether a cliff ahead is lethal (guaranteed death from fall damage).
    /// </summary>
    public bool IsLethalCliffAhead(Position currentPos, Position targetWaypoint, uint mapId)
    {
        var drop = ProbeEdgeAhead(currentPos, targetWaypoint, mapId);
        return drop >= CLIFF_LETHAL_DROP || drop == float.MaxValue;
    }

    /// <summary>
    /// Fall damage estimation using vanilla WoW 1.12.1 formula.
    /// No damage below ~14.57yd. Above that, scales with max health.
    /// </summary>
    public static float EstimateFallDamage(float fallDistance, float maxHealth, bool hasSafeFall = false)
    {
        if (hasSafeFall) fallDistance *= 0.5f;
        const float threshold = 14.57f;
        if (fallDistance <= threshold) return 0f;
        var damagePercent = (fallDistance - threshold) / 100f;
        return MathF.Min(maxHealth, maxHealth * damagePercent);
    }

    /// <summary>
    /// Assesses whether a jump/fall from current position to a target is survivable.
    /// Returns estimated fall damage, or -1 if assessment is unavailable.
    /// </summary>
    public float AssessJumpDamage(Position from, Position to, float maxHealth, bool hasSafeFall = false)
    {
        var fallDistance = from.Z - to.Z;
        if (fallDistance <= 0) return 0f;
        return EstimateFallDamage(fallDistance, maxHealth, hasSafeFall);
    }

    // ===================== Gap jump detection =====================

    /// <summary>
    /// Describes a detected gap between two consecutive waypoints.
    /// </summary>
    public readonly struct GapInfo(int waypointIndex, float gapWidth2D, float gapDepth, float landingZDelta, bool isJumpable)
    {
        public int WaypointIndex { get; } = waypointIndex;
        public float GapWidth2D { get; } = gapWidth2D;
        public float GapDepth { get; } = gapDepth;
        public float LandingZDelta { get; } = landingZDelta;
        public bool IsJumpable { get; } = isJumpable;
    }

    /// <summary>
    /// Detects gaps in the current path by probing ground Z at midpoints between
    /// consecutive waypoints. A gap is detected when the midpoint ground Z drops
    /// significantly below both endpoints.
    /// </summary>
    public GapInfo[] DetectGaps(uint mapId)
    {
        if (_pathfinding == null || _waypoints.Length < 2)
            return [];

        var gaps = new List<GapInfo>();

        for (int i = _currentIndex; i < _waypoints.Length - 1; i++)
        {
            var wp1 = _waypoints[i];
            var wp2 = _waypoints[i + 1];

            // Probe ground Z at midpoint
            var midX = (wp1.X + wp2.X) * 0.5f;
            var midY = (wp1.Y + wp2.Y) * 0.5f;
            var midZ = MathF.Max(wp1.Z, wp2.Z);

            try
            {
                var (midGroundZ, found) = _pathfinding.GetGroundZ(mapId, new Position(midX, midY, midZ));
                if (!found) continue;

                var depthFromWp1 = wp1.Z - midGroundZ;
                var depthFromWp2 = wp2.Z - midGroundZ;

                // Both endpoints must be significantly above the midpoint ground
                if (depthFromWp1 < GAP_DETECTION_DEPTH_MIN || depthFromWp2 < GAP_DETECTION_DEPTH_MIN)
                    continue;

                var gapDepth = MathF.Min(depthFromWp1, depthFromWp2);
                var dx = wp2.X - wp1.X;
                var dy = wp2.Y - wp1.Y;
                var gapWidth2D = MathF.Sqrt(dx * dx + dy * dy);
                var landingZDelta = wp2.Z - wp1.Z;

                var isJumpable = gapWidth2D <= MAX_JUMP_DISTANCE_2D
                    && landingZDelta <= MAX_JUMP_HEIGHT;

                gaps.Add(new GapInfo(i, gapWidth2D, gapDepth, landingZDelta, isJumpable));
            }
            catch
            {
                // IPC failure — skip this segment
            }
        }

        return gaps.ToArray();
    }

    /// <summary>
    /// Returns gap info if the current waypoint is a gap launch point, or null if not.
    /// </summary>
    public GapInfo? GetCurrentGapInfo(uint mapId)
    {
        if (_currentIndex >= _waypoints.Length - 1) return null;

        var gaps = DetectGaps(mapId);
        foreach (var gap in gaps)
        {
            if (gap.WaypointIndex == _currentIndex)
                return gap;
        }
        return null;
    }

    /// <summary>
    /// Force a path recalculation on the next GetNextWaypoint call.
    /// Call this when the target changes (e.g., mob died, new target acquired).
    /// </summary>
    public void Clear()
    {
        _waypoints = [];
        _waypointAcceptanceRadii = [];
        _currentIndex = 0;
        _destination = null;
        _lastCalculationTick = 0;
        _hasCalculatedPath = false;
        _lastWaypointSamplePosition = null;
        _lastWaypointSampleDistance = float.NaN;
        _stalledNearWaypointSamples = 0;
        _losSkipCacheIndex = -1;
        _losSkipCacheFarthest = -1;
        _losSkipCacheTick = 0;
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
