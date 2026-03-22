using BotRunner.Clients;
<<<<<<< HEAD
using GameData.Core.Models;
=======
using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using System.Collections.Generic;
using System.Diagnostics;
>>>>>>> cpp_physics_system
using System;

namespace BotRunner.Movement;

/// <summary>
<<<<<<< HEAD
=======
/// Lightweight counters for debugging and monitoring pathfinding behaviour.
/// Single-threaded per bot — no locks needed.
/// </summary>
public sealed class NavigationMetrics
{
    /// <summary>Total paths computed via GetValidatedPath.</summary>
    public int PathsCalculated { get; private set; }

    /// <summary>Paths that returned empty (sanitization, validation, or pathfinding failure).</summary>
    public int PathsFailed { get; private set; }

    /// <summary>Individual waypoints reached and advanced past.</summary>
    public int WaypointsReached { get; private set; }

    /// <summary>Stall/recalculation events (forced path recalculations).</summary>
    public int RecalculationsTriggered { get; private set; }

    /// <summary>Waypoints whose Z was corrected by Phase 3a collision ground queries.</summary>
    public int ZCorrections { get; private set; }

    /// <summary>String-pull segments rejected by Phase 5a width check.</summary>
    public int WidthChecksFailed { get; private set; }

    /// <summary>Multi-directional cliff probe activations.</summary>
    public int CliffProbesTriggered { get; private set; }

    /// <summary>Waypoints rerouted around detected cliff edges.</summary>
    public int CliffReroutes { get; private set; }

    /// <summary>
    /// Waypoint index advances driven by physics-confirmed position via UpdateCorridorPosition.
    /// Distinct from WaypointsReached which counts all advances including those from GetNextWaypoint.
    /// </summary>
    public int CorridorAdvances { get; private set; }

    /// <summary>Running average of path waypoint count across all calculated paths.</summary>
    public float AveragePathLength { get; private set; }

    /// <summary>Wall-clock milliseconds spent on the most recent path calculation.</summary>
    public long LastPathDurationMs { get; private set; }

    /// <summary>Maximum 2D perpendicular drift from planned path observed during execution.</summary>
    public float MaxObservedDrift { get; private set; }

    /// <summary>Number of execution samples where drift exceeded the warning threshold (8y).</summary>
    public int DriftWarnings { get; private set; }

    internal void RecordDrift(float drift)
    {
        if (drift > MaxObservedDrift) MaxObservedDrift = drift;
        if (drift > 8f) DriftWarnings++;
    }

    // Running totals for computing the average incrementally.
    private long _totalWaypointCount;

    internal void IncrementPathsCalculated() => PathsCalculated++;
    internal void IncrementPathsFailed() => PathsFailed++;
    internal void IncrementWaypointsReached() => WaypointsReached++;
    internal void IncrementRecalculationsTriggered() => RecalculationsTriggered++;
    internal void AddZCorrections(int count) { ZCorrections += count; }
    internal void IncrementWidthChecksFailed() => WidthChecksFailed++;
    internal void IncrementCliffProbesTriggered() => CliffProbesTriggered++;
    internal void IncrementCliffReroutes() => CliffReroutes++;
    internal void IncrementCorridorAdvances() => CorridorAdvances++;

    /// <summary>
    /// Number of path segments rejected because a registered dynamic object
    /// (closed door, trophy pillar, etc.) intersected the segment at generation time.
    /// </summary>
    public int DynamicObstacleDeflections { get; private set; }
    internal void IncrementDynamicObstacleDeflections() => DynamicObstacleDeflections++;

    internal void RecordPathLength(int waypointCount)
    {
        _totalWaypointCount += waypointCount;
        AveragePathLength = PathsCalculated > 0
            ? (float)_totalWaypointCount / PathsCalculated
            : 0f;
    }

    internal void RecordPathDuration(long elapsedMs) => LastPathDurationMs = elapsedMs;

    /// <summary>Reset all counters to zero.</summary>
    public void Reset()
    {
        PathsCalculated = 0;
        PathsFailed = 0;
        WaypointsReached = 0;
        RecalculationsTriggered = 0;
        ZCorrections = 0;
        WidthChecksFailed = 0;
        CliffProbesTriggered = 0;
        CliffReroutes = 0;
        CorridorAdvances = 0;
        DynamicObstacleDeflections = 0;
        AveragePathLength = 0f;
        LastPathDurationMs = 0;
        MaxObservedDrift = 0f;
        DriftWarnings = 0;
        _totalWaypointCount = 0;
    }
}

public static class NavigationTraceReason
{
    public const string InitialPath = "initial_path";
    public const string DestinationChanged = "destination_changed";
    public const string PathUnavailable = "path_unavailable";
    public const string PathExhaustedStillFar = "path_exhausted_still_far";
    public const string StalledNearWaypoint = "stalled_near_waypoint";
    public const string StrictWaypointRecalc = "strict_waypoint_recalc";
    public const string WallStuck = "wall_stuck";
    public const string Manual = "manual";
}

public readonly record struct NavigationExecutionSample(
    Position CurrentPosition,
    Position Destination,
    Position? ReturnedWaypoint,
    int WaypointIndex,
    int PlanVersion,
    float DistanceToWaypoint,
    bool UsedDirectFallback,
    long Tick,
    string Resolution);

public sealed record NavigationTraceSnapshot(
    uint MapId,
    Position? RequestedStart,
    Position? RequestedDestination,
    Position[] ServiceWaypoints,
    Position[] PlannedWaypoints,
    PathAffordanceInfo Affordances,
    Position? ActiveWaypoint,
    int CurrentWaypointIndex,
    int PlanVersion,
    string? LastReplanReason,
    string? LastResolution,
    bool UsedDirectFallback,
    bool UsedNearbyObjectOverlay,
    int NearbyObjectCount,
    bool SmoothPath,
    bool IsShortRoute,
    long LastPlanTick,
    NavigationExecutionSample[] ExecutionSamples)
{
    /// <summary>
    /// Compute planned-vs-executed drift metrics from the execution samples
    /// and planned waypoints. Returns (maxDrift, avgDrift, wallDeflectCount,
    /// directFallbackCount, distinctPlanVersions).
    /// </summary>
    public (float MaxDrift, float AvgDrift, int WallDeflectCount, int DirectFallbackCount, int ReplanCount) ComputeDriftMetrics()
    {
        if (ExecutionSamples.Length == 0 || PlannedWaypoints.Length < 2)
            return (0f, 0f, 0, 0, 0);

        float maxDrift = 0f;
        float totalDrift = 0f;
        int driftSamples = 0;
        int wallDeflectCount = 0;
        int directFallbackCount = 0;
        var planVersions = new HashSet<int>();

        foreach (var sample in ExecutionSamples)
        {
            planVersions.Add(sample.PlanVersion);

            if (sample.Resolution == "wall_deflect") wallDeflectCount++;
            if (sample.UsedDirectFallback) directFallbackCount++;

            // Compute perpendicular distance from actual position to nearest planned path segment
            var drift = MinDistanceToPath(sample.CurrentPosition, PlannedWaypoints);
            if (drift > maxDrift) maxDrift = drift;
            totalDrift += drift;
            driftSamples++;
        }

        var avgDrift = driftSamples > 0 ? totalDrift / driftSamples : 0f;
        return (maxDrift, avgDrift, wallDeflectCount, directFallbackCount, planVersions.Count);
    }

    private static float MinDistanceToPath(Position point, Position[] path)
    {
        var minDist = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var dist = PerpendicularDistance2D(point, path[i], path[i + 1]);
            if (dist < minDist) minDist = dist;
        }
        // Also check distance to last waypoint
        if (path.Length > 0)
        {
            var endDist = Distance2D(point, path[^1]);
            if (endDist < minDist) minDist = endDist;
        }
        return minDist == float.MaxValue ? 0f : minDist;
    }

    private static float PerpendicularDistance2D(Position point, Position segA, Position segB)
    {
        var dx = segB.X - segA.X;
        var dy = segB.Y - segA.Y;
        var lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-6f)
            return Distance2D(point, segA);

        // Project point onto segment, clamped to [0,1]
        var t = ((point.X - segA.X) * dx + (point.Y - segA.Y) * dy) / lenSq;
        t = Math.Clamp(t, 0f, 1f);

        var projX = segA.X + t * dx;
        var projY = segA.Y + t * dy;
        var ex = point.X - projX;
        var ey = point.Y - projY;
        return MathF.Sqrt(ex * ex + ey * ey);
    }

    private static float Distance2D(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

internal readonly record struct ValidatedPathResult(
    Position[] RawPath,
    Position[] PlannedPath,
    bool UsedNearbyObjectOverlay,
    int NearbyObjectCount,
    bool SmoothPath);

/// <summary>
/// Classifies the traversal type of a path segment between two consecutive waypoints.
/// Used for decision-making (e.g., "can the bot safely walk this path?") and diagnostics.
/// </summary>
public enum SegmentAffordance : byte
{
    /// <summary>Roughly flat ground (slope &lt; 15°, |Z delta| &lt; 1y).</summary>
    Walk = 0,
    /// <summary>Upward slope or step (Z gain 1-3y, slope 15-45°).</summary>
    StepUp = 1,
    /// <summary>Steep upward climb (Z gain or slope > 45°).</summary>
    SteepClimb = 2,
    /// <summary>Moderate drop (Z loss 2-6y).</summary>
    Drop = 3,
    /// <summary>Large drop/cliff (Z loss > 6y).</summary>
    Cliff = 4,
    /// <summary>Near-vertical transition (2D distance &lt; 0.5y, Z delta > 2y) — likely elevator/portal.</summary>
    Vertical = 5,
}

/// <summary>
/// Affordance metadata for each segment in a planned path.
/// Segment i describes the transition from waypoint[i] to waypoint[i+1].
/// </summary>
public readonly record struct PathAffordanceInfo(
    SegmentAffordance[] Segments,
    int StepUpCount,
    int DropCount,
    int CliffCount,
    int VerticalCount,
    float TotalZGain,
    float TotalZLoss,
    float MaxSlopeAngleDeg)
{
    public static PathAffordanceInfo Empty => new([], 0, 0, 0, 0, 0f, 0f, 0f);

    public static PathAffordanceInfo Classify(Position[] waypoints)
    {
        if (waypoints.Length < 2)
            return Empty;

        var segments = new SegmentAffordance[waypoints.Length - 1];
        int stepUpCount = 0, dropCount = 0, cliffCount = 0, verticalCount = 0;
        float totalZGain = 0f, totalZLoss = 0f, maxSlopeDeg = 0f;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dz = b.Z - a.Z;
            var dist2D = MathF.Sqrt(dx * dx + dy * dy);
            var absDz = MathF.Abs(dz);

            // Track total elevation change
            if (dz > 0) totalZGain += dz;
            else totalZLoss += -dz;

            // Slope angle in degrees
            float slopeDeg = dist2D > 0.01f
                ? MathF.Atan2(absDz, dist2D) * (180f / MathF.PI)
                : (absDz > 0.5f ? 90f : 0f);
            if (slopeDeg > maxSlopeDeg) maxSlopeDeg = slopeDeg;

            // Classify
            SegmentAffordance affordance;
            if (dist2D < 0.5f && absDz > 2f)
            {
                affordance = SegmentAffordance.Vertical;
                verticalCount++;
            }
            else if (dz < -6f)
            {
                affordance = SegmentAffordance.Cliff;
                cliffCount++;
            }
            else if (dz < -2f)
            {
                affordance = SegmentAffordance.Drop;
                dropCount++;
            }
            else if (slopeDeg > 45f && dz > 0)
            {
                affordance = SegmentAffordance.SteepClimb;
                stepUpCount++;
            }
            else if (dz > 1f || (slopeDeg > 15f && dz > 0))
            {
                affordance = SegmentAffordance.StepUp;
                stepUpCount++;
            }
            else
            {
                affordance = SegmentAffordance.Walk;
            }

            segments[i] = affordance;
        }

        return new(segments, stepUpCount, dropCount, cliffCount, verticalCount,
            totalZGain, totalZLoss, maxSlopeDeg);
    }
}

/// <summary>
>>>>>>> cpp_physics_system
/// Manages a path of waypoints from the pathfinding service.
/// Tracks progress through the path and handles recalculation.
/// Caches the path so we don't re-query pathfinding every update tick.
/// </summary>
<<<<<<< HEAD
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
=======
public class NavigationPath(
    PathfindingClient? pathfinding,
    Func<long>? tickProvider = null,
    bool enableProbeHeuristics = true,
    bool enableDynamicProbeSkipping = true,
    bool strictPathValidation = false,
    float capsuleRadius = 0.6f,
    float capsuleHeight = 2.5f,
    Func<Position, Position, IReadOnlyList<DynamicObjectProto>>? nearbyObjectProvider = null,
    Race race = 0,
    Gender gender = 0)
{
    private readonly PathfindingClient? _pathfinding = pathfinding;
    private readonly Func<long> _tickProvider = tickProvider ?? (() => Environment.TickCount64);
    private readonly bool _enableProbeHeuristics = enableProbeHeuristics;
    private readonly bool _enableDynamicProbeSkipping = enableProbeHeuristics && enableDynamicProbeSkipping;
    private readonly bool _strictPathValidation = strictPathValidation;
    private readonly float _capsuleRadius = capsuleRadius;
    private readonly float _capsuleHeight = capsuleHeight;
    private readonly Func<Position, Position, IReadOnlyList<DynamicObjectProto>>? _nearbyObjectProvider = nearbyObjectProvider;
    private readonly Race _race = race;
    private readonly Gender _gender = gender;
    private Position[] _waypoints = [];
    private float[] _waypointAcceptanceRadii = [];

    /// <summary>
    /// Exposes the current waypoint array for passing to MovementController's dead-reckoning.
    /// Returns empty array if no path has been calculated.
    /// </summary>
    public Position[] CurrentWaypoints => _waypoints;
    private int _currentIndex;
    private Position? _destination;
    private long _lastCalculationTick;
    private bool _hasCalculatedPath;
    private Position? _lastWaypointSamplePosition;
    private float _lastWaypointSampleDistance = float.NaN;
    private int _stalledNearWaypointSamples;
    private int _consecutiveWallHitSamples;

    // Layer 2: Wall-normal deflection avoidance
    private Position? _avoidanceWaypoint;
    private int _avoidanceFramesRemaining;
    private int _consecutiveAvoidanceFailures;
    private const int AVOIDANCE_LIFETIME_FRAMES = 10;       // 500ms at 50ms ticks
    private const int MAX_AVOIDANCE_FAILURES = 3;
    private const float DEFLECTION_MULTIPLIER = 3f;          // * capsuleRadius
    private const float BLOCKED_FRACTION_THRESHOLD = 0.5f;

    // Layer 1: Proactive LOS lookahead
    private bool _nextSegmentBlocked;
    private int _lastProbeWaypointIndex = -1;

    private float _characterSpeed = 7.0f;         // actual run speed; updated via UpdateCharacterSpeed()

    /// <summary>
    /// Pathfinding metrics for debugging and monitoring.
    /// </summary>
    public NavigationMetrics Metrics { get; } = new();

    // LOS-based string-pulling and runtime lookahead skip
    private const int MAX_STRINGPULL_LOOKAHEAD = 8;
    private const int MAX_RUNTIME_LOS_LOOKAHEAD = 6;
    private const long LOS_SKIP_CACHE_TTL_MS = 500;
    private int _losSkipCacheIndex = -1;
    private int _losSkipCacheFarthest = -1;
    private long _losSkipCacheTick;

    // Adaptive acceptance radius: turn angle at each waypoint determines how tightly
    // the bot must follow it. Straight paths get MAX, sharp corners get MIN.
    private const float MIN_ACCEPTANCE_RADIUS = 3.5f;     // at 90°+ corners — padded for execution tolerance
    private const float MAX_ACCEPTANCE_RADIUS = 7f;       // on straight paths — wider corridor for smoother flow
    private const float SHARP_TURN_ANGLE_DEG = 90f;       // angle that maps to MIN
    private const float WAYPOINT_REACH_DISTANCE = 3.5f;   // default fallback (no radii computed)
    private const float CORNER_COMMIT_DISTANCE = 1.25f;   // default fallback
    private const float RECALCULATE_DISTANCE = 10f;

    // Cliff/edge detection constants
    private const float CLIFF_PROBE_DISTANCE = 3f;        // probe ground 3yd ahead
    private const float CLIFF_DROP_THRESHOLD = 8f;         // 8yd drop = cliff danger
    private const float CLIFF_LETHAL_DROP = 50f;           // guaranteed death fall
    private const float CLIFF_LATERAL_PROBE_DISTANCE = 1.5f; // probe ground 1.5yd to each side
    private const float CLIFF_NEARBY_DROP_THRESHOLD = 3f;    // 3yd drop = nearby cliff danger

    // Jump physics constraints (derived from PhysicsConstants)
    private const float JUMP_VELOCITY = 7.95577f;
    private const float GRAVITY = 19.2911f;
    private const float MAX_JUMP_HEIGHT = 1.64f;           // JUMP_VELOCITY^2 / (2*GRAVITY)
    private const float MAX_JUMP_DISTANCE_2D = 8f;         // conservative horizontal max at run speed
    private const float GAP_DETECTION_DEPTH_MIN = 3f;      // minimum gap depth to consider
>>>>>>> cpp_physics_system
    private const int RECALCULATE_COOLDOWN_MS = 2000;
    private const float STALLED_NEAR_WAYPOINT_DISTANCE = 8f;
    private const float STALLED_SAMPLE_POSITION_EPSILON = 0.15f;
    private const float STALLED_SAMPLE_DISTANCE_EPSILON = 0.1f;
<<<<<<< HEAD
    private const int STALLED_SAMPLE_THRESHOLD = 24;
=======
    private const int STALLED_SAMPLE_THRESHOLD = 6;      // detect stuck faster (was 10)
    private const int WAYPOINT_REACHABILITY_SCAN_LIMIT = 12;
    private const float PATH_POINT_DEDUP_EPSILON = 0.05f;
    private const float MAX_FIRST_WAYPOINT_DISTANCE = 120f;
    private const float MIN_DESTINATION_PROGRESS = 1f;
    private const float MAX_SEGMENT_DISTANCE = 1200f;
    private const float PATH_TRAVERSABILITY_SEGMENT_EPSILON = 0.05f;
    private const float STRICT_DESTINATION_ENDPOINT_DISTANCE = 8f;
    private const float MAX_PROBE_SEGMENT_DISTANCE = 2f;
    private const float PROBE_COLLINEARITY_DOT_MIN = 0.985f;
    private const int MAX_TRACE_SAMPLES = 64;
    private const float SHORT_ROUTE_TRACE_DISTANCE = 40f;
    private const string TRACE_RESOLUTION_WAYPOINT = "waypoint";
    private const string TRACE_RESOLUTION_WALL_DEFLECT = "wall_deflect";
    private const string TRACE_RESOLUTION_DIRECT_FALLBACK = "direct_fallback";
    private const string TRACE_RESOLUTION_NO_ROUTE = "no_route";

    private readonly List<NavigationExecutionSample> _executionSamples = [];
    private Position[] _traceServiceWaypoints = [];
    private Position[] _tracePlannedWaypoints = [];
    private PathAffordanceInfo _traceAffordances = PathAffordanceInfo.Empty;
    private Position? _traceRequestedStart;
    private Position? _traceRequestedDestination;
    private uint _traceMapId;
    private int _tracePlanVersion;
    private string? _traceLastReplanReason;
    private string? _traceLastResolution;
    private bool _traceUsedDirectFallback;
    private bool _traceUsedNearbyObjectOverlay;
    private int _traceNearbyObjectCount;
    private bool _traceSmoothPath;
    private bool _traceIsShortRoute;
    private long _traceLastPlanTick;

    /// <summary>
    /// Latest route plan and execution samples so live failures can be attributed
    /// to service output versus runtime drift without relying on logs alone.
    /// </summary>
    public NavigationTraceSnapshot TraceSnapshot => new(
        _traceMapId,
        ClonePosition(_traceRequestedStart),
        ClonePosition(_traceRequestedDestination),
        ClonePositions(_traceServiceWaypoints),
        ClonePositions(_tracePlannedWaypoints),
        _traceAffordances,
        _currentIndex < _waypoints.Length ? ClonePosition(_waypoints[_currentIndex]) : null,
        _currentIndex,
        _tracePlanVersion,
        _traceLastReplanReason,
        _traceLastResolution,
        _traceUsedDirectFallback,
        _traceUsedNearbyObjectOverlay,
        _traceNearbyObjectCount,
        _traceSmoothPath,
        _traceIsShortRoute,
        _traceLastPlanTick,
        CloneExecutionSamples());
>>>>>>> cpp_physics_system

    /// <summary>
    /// Gets the next waypoint to move toward, or the direct destination if no path is available.
    /// Automatically calculates/recalculates the path as needed.
    /// </summary>
<<<<<<< HEAD
    public Position? GetNextWaypoint(Position currentPosition, Position destination, uint mapId, bool allowDirectFallback = true, float minWaypointDistance = 0f)
    {
        if (_pathfinding == null)
            return allowDirectFallback ? destination : null;
=======
    public Position? GetNextWaypoint(Position currentPosition, Position destination, uint mapId, bool allowDirectFallback = true, float minWaypointDistance = 0f, bool physicsHitWall = false, float wallNormalX = 0f, float wallNormalY = 0f, float blockedFraction = 1f)
    {
        if (_pathfinding == null)
        {
            var fallback = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
            return RecordWaypointResult(
                currentPosition,
                destination,
                fallback,
                usedDirectFallback: fallback != null,
                fallback != null ? TRACE_RESOLUTION_DIRECT_FALLBACK : TRACE_RESOLUTION_NO_ROUTE);
        }
>>>>>>> cpp_physics_system

        // Recalculate if destination changed significantly
        if (_destination == null || _destination.DistanceTo(destination) > RECALCULATE_DISTANCE)
        {
<<<<<<< HEAD
            CalculatePath(currentPosition, destination, mapId);
=======
            var reason = _destination == null
                ? NavigationTraceReason.InitialPath
                : NavigationTraceReason.DestinationChanged;
            CalculatePath(currentPosition, destination, mapId, reason: reason);
>>>>>>> cpp_physics_system
        }

        if (_waypoints.Length == 0)
        {
            if (!allowDirectFallback)
            {
<<<<<<< HEAD
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
=======
                CalculatePath(currentPosition, destination, mapId, reason: NavigationTraceReason.PathUnavailable);
                if (_waypoints.Length == 0)
                    return RecordWaypointResult(currentPosition, destination, null, usedDirectFallback: false, TRACE_RESOLUTION_NO_ROUTE);
            }
            else
            {
                var fallback = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
                return RecordWaypointResult(
                    currentPosition,
                    destination,
                    fallback,
                    usedDirectFallback: fallback != null,
                    fallback != null ? TRACE_RESOLUTION_DIRECT_FALLBACK : TRACE_RESOLUTION_NO_ROUTE);
            }
        }

        // When adaptive radii are computed, they are the primary distance thresholds.
        // WAYPOINT_REACH_DISTANCE is only the fallback when no radii exist.
        // The caller's minWaypointDistance always acts as a floor.
        var waypointAdvanceDistance = MathF.Max(WAYPOINT_REACH_DISTANCE, minWaypointDistance);

        // Advance reached waypoints, but avoid skipping corner waypoints into blocked segments.
        AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
>>>>>>> cpp_physics_system

        if (_currentIndex >= _waypoints.Length)
        {
            // If we're still not near the destination, recalculate path periodically.
            if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
            {
<<<<<<< HEAD
                CalculatePath(currentPosition, destination, mapId);
            }

            if (_currentIndex >= _waypoints.Length)
                return allowDirectFallback ? destination : null;
=======
                CalculatePath(currentPosition, destination, mapId, reason: NavigationTraceReason.PathExhaustedStillFar);
            }

            if (_currentIndex >= _waypoints.Length)
            {
                var fallback = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
                return RecordWaypointResult(
                    currentPosition,
                    destination,
                    fallback,
                    usedDirectFallback: fallback != null,
                    fallback != null ? TRACE_RESOLUTION_DIRECT_FALLBACK : TRACE_RESOLUTION_NO_ROUTE);
            }
        }

        if (!TryResolveWaypoint(currentPosition, destination, mapId, minWaypointDistance, allowDirectFallback, out var waypoint))
            return RecordWaypointResult(currentPosition, destination, null, usedDirectFallback: false, TRACE_RESOLUTION_NO_ROUTE);

        if (waypoint == null)
        {
            var fallback = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
            return RecordWaypointResult(
                currentPosition,
                destination,
                fallback,
                usedDirectFallback: fallback != null,
                fallback != null ? TRACE_RESOLUTION_DIRECT_FALLBACK : TRACE_RESOLUTION_NO_ROUTE);
        }

        var waypointDistance = currentPosition.DistanceTo2D(waypoint);
        if (_currentIndex >= _waypoints.Length)
        {
            if (currentPosition.DistanceTo2D(destination) > WAYPOINT_REACH_DISTANCE)
                CalculatePath(currentPosition, destination, mapId, reason: NavigationTraceReason.PathExhaustedStillFar);
            if (_currentIndex >= _waypoints.Length)
            {
                var fallback = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
                return RecordWaypointResult(
                    currentPosition,
                    destination,
                    fallback,
                    usedDirectFallback: fallback != null,
                    fallback != null ? TRACE_RESOLUTION_DIRECT_FALLBACK : TRACE_RESOLUTION_NO_ROUTE);
            }
        }

        // Phase 2: Wall-normal deflection + fallback repath.
        // Layer 2 (reactive): When significantly blocked, compute an avoidance waypoint using
        // the wall normal — the bot deflects away from the wall geometrically instead of
        // walking into it and counting ticks.
        // Layer 3 (fallback): If deflection fails 3x consecutively, force a full repath.
        const int WALL_STUCK_THRESHOLD = 15;
        if (physicsHitWall)
        {
            _consecutiveWallHitSamples++;

            // Layer 2: Geometric deflection using wall normal
            if (blockedFraction < BLOCKED_FRACTION_THRESHOLD && _avoidanceWaypoint == null)
            {
                float normalLen2D = MathF.Sqrt(wallNormalX * wallNormalX + wallNormalY * wallNormalY);
                if (normalLen2D > 0.01f)
                {
                    float deflectDist = _capsuleRadius * DEFLECTION_MULTIPLIER;
                    float deflectX = (wallNormalX / normalLen2D) * deflectDist;
                    float deflectY = (wallNormalY / normalLen2D) * deflectDist;
                    var candidate = new Position(
                        currentPosition.X + deflectX,
                        currentPosition.Y + deflectY,
                        currentPosition.Z);

                    // Validate the deflection point is reachable (LOS check via pathfinding service)
                    bool reachable = false;
                    try { reachable = _pathfinding.IsInLineOfSight(mapId, currentPosition, candidate); }
                    catch { /* IPC failure — skip deflection this frame */ }

                    if (reachable)
                    {
                        _avoidanceWaypoint = candidate;
                        _avoidanceFramesRemaining = AVOIDANCE_LIFETIME_FRAMES;
                        _consecutiveAvoidanceFailures = 0;
                        _consecutiveWallHitSamples = 0;
                    }
                    else
                    {
                        _consecutiveAvoidanceFailures++;
                    }
                }
            }

            // Layer 3: If deflection keeps failing, force a full repath
            if (_consecutiveAvoidanceFailures >= MAX_AVOIDANCE_FAILURES
                || _consecutiveWallHitSamples >= WALL_STUCK_THRESHOLD)
            {
                CalculatePath(currentPosition, destination, mapId, force: true, reason: NavigationTraceReason.WallStuck);
                AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
                _consecutiveWallHitSamples = 0;
                _consecutiveAvoidanceFailures = 0;
                _avoidanceWaypoint = null;
                _avoidanceFramesRemaining = 0;
                _stalledNearWaypointSamples = 0;
                _lastWaypointSampleDistance = float.NaN;
                _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);

                if (_currentIndex < _waypoints.Length)
                {
                    waypoint = _waypoints[_currentIndex];
                    waypointDistance = currentPosition.DistanceTo2D(waypoint);
                }
            }
            else if (_stalledNearWaypointSamples > 0)
            {
                // Wall contact but not significantly blocked — reset stall counter (bot is sliding)
                _stalledNearWaypointSamples = 0;
                _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);
                _lastWaypointSampleDistance = float.NaN;
                Metrics.IncrementCorridorAdvances();
            }
        }
        else
        {
            _consecutiveWallHitSamples = 0;
            _consecutiveAvoidanceFailures = 0;
>>>>>>> cpp_physics_system
        }

        // If the next waypoint remains near while the bot itself does not move,
        // skip it so callers don't repeatedly drive a blocked micro-corner.
<<<<<<< HEAD
        var waypoint = _waypoints[_currentIndex];
        var waypointDistance = currentPosition.DistanceTo(waypoint);
=======
>>>>>>> cpp_physics_system
        if (_lastWaypointSamplePosition != null
            && waypointDistance <= STALLED_NEAR_WAYPOINT_DISTANCE
            && currentPosition.DistanceTo(_lastWaypointSamplePosition) <= STALLED_SAMPLE_POSITION_EPSILON
            && !float.IsNaN(_lastWaypointSampleDistance)
            && MathF.Abs(waypointDistance - _lastWaypointSampleDistance) <= STALLED_SAMPLE_DISTANCE_EPSILON)
        {
            _stalledNearWaypointSamples++;
            if (_stalledNearWaypointSamples >= STALLED_SAMPLE_THRESHOLD)
            {
<<<<<<< HEAD
                _currentIndex++;
=======
                // In strict mode, never advance a stalled corner by index only.
                // Recalculate so we keep following service-validated turns.
                // Always recalculate when stalled — never advance by index alone.
                // Index-only advance bypasses path validation and can push the bot
                // into invalid geometry. If recalculation fails, mark path as exhausted.
                CalculatePath(currentPosition, destination, mapId, force: true, reason: NavigationTraceReason.StalledNearWaypoint);
                AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
>>>>>>> cpp_physics_system
                _stalledNearWaypointSamples = 0;
                _lastWaypointSampleDistance = float.NaN;
                _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);

                if (_currentIndex >= _waypoints.Length)
                {
<<<<<<< HEAD
                    if (currentPosition.DistanceTo(destination) > WAYPOINT_REACH_DISTANCE)
                        CalculatePath(currentPosition, destination, mapId);
                    if (_currentIndex >= _waypoints.Length)
                        return allowDirectFallback ? destination : null;
                }

                waypoint = _waypoints[_currentIndex];
                waypointDistance = currentPosition.DistanceTo(waypoint);
=======
                    if (currentPosition.DistanceTo2D(destination) > WAYPOINT_REACH_DISTANCE)
                        CalculatePath(currentPosition, destination, mapId, reason: NavigationTraceReason.PathExhaustedStillFar);
                    if (_currentIndex >= _waypoints.Length)
                    {
                        var fallback = ResolveDirectFallback(currentPosition, destination, mapId, allowDirectFallback);
                        return RecordWaypointResult(
                            currentPosition,
                            destination,
                            fallback,
                            usedDirectFallback: fallback != null,
                            fallback != null ? TRACE_RESOLUTION_DIRECT_FALLBACK : TRACE_RESOLUTION_NO_ROUTE);
                    }
                }

                waypoint = _waypoints[_currentIndex];
                waypointDistance = currentPosition.DistanceTo2D(waypoint);
>>>>>>> cpp_physics_system
            }
        }
        else
        {
            _stalledNearWaypointSamples = 0;
        }

        _lastWaypointSamplePosition = new Position(currentPosition.X, currentPosition.Y, currentPosition.Z);
        _lastWaypointSampleDistance = waypointDistance;
<<<<<<< HEAD
        return waypoint;
=======

        // Layer 2: If an avoidance waypoint is active, steer toward it instead of the path waypoint.
        // The bot deflects away from the wall and resumes the normal path once the avoidance expires.
        if (_avoidanceWaypoint != null && _avoidanceFramesRemaining > 0)
        {
            _avoidanceFramesRemaining--;
            var avoidResult = _avoidanceWaypoint;
            if (_avoidanceFramesRemaining <= 0)
                _avoidanceWaypoint = null;
            return RecordWaypointResult(currentPosition, destination, avoidResult, usedDirectFallback: false, TRACE_RESOLUTION_WALL_DEFLECT);
        }

        // Clear avoidance if bot has moved past the wall (no longer hitting)
        if (!physicsHitWall && _avoidanceWaypoint != null)
        {
            _avoidanceWaypoint = null;
            _avoidanceFramesRemaining = 0;
        }

        return RecordWaypointResult(currentPosition, destination, waypoint, usedDirectFallback: false, TRACE_RESOLUTION_WAYPOINT);
>>>>>>> cpp_physics_system
    }

    /// <summary>
    /// Calculate a new path from start to end.
    /// </summary>
<<<<<<< HEAD
    public void CalculatePath(Position start, Position end, uint mapId)
    {
        var nowTick = _tickProvider();
        if (nowTick - _lastCalculationTick < RECALCULATE_COOLDOWN_MS)
            return;

        _lastCalculationTick = nowTick;
=======
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

            // Use 2D distance for waypoint advancement. Navmesh Z values are approximate
            // and can differ from actual terrain by several yards. Using 3D distance causes
            // the bot to circle around waypoints it's already standing on top of.
            var distanceToWaypoint = currentPosition.DistanceTo2D(_waypoints[_currentIndex]);
            if (distanceToWaypoint >= effectiveRadius)
                break;

            if (_enableDynamicProbeSkipping
                && ShouldSkipProbeWaypoint(currentPosition, mapId, effectiveRadius, distanceToWaypoint))
            {
                _currentIndex++;
                Metrics.IncrementWaypointsReached();
                continue;
            }

            if (!CanAdvanceToNextWaypoint(currentPosition, mapId, distanceToWaypoint))
                break;

            _currentIndex++;
            Metrics.IncrementWaypointsReached();
        }

        // Look-ahead skip: if we overshot the current waypoint and are closer (2D) to
        // a later waypoint, jump ahead. This prevents the bot from doubling back to a
        // waypoint it already passed. Common when navmesh Z is inaccurate and the bot
        // walks over the waypoint without triggering the radius check.
        // Limit scan to a small window (3 waypoints) to prevent distant waypoints from
        // pulling the bot off course and causing oscillation/jitter.
        if (_currentIndex < _waypoints.Length - 1)
        {
            var distToCurrent = currentPosition.DistanceTo2D(_waypoints[_currentIndex]);
            var bestIndex = _currentIndex;
            var bestDist = distToCurrent;

            var scanEnd = Math.Min(_currentIndex + 4, _waypoints.Length);
            for (int i = _currentIndex + 1; i < scanEnd; i++)
            {
                var d = currentPosition.DistanceTo2D(_waypoints[i]);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }

            if (bestIndex > _currentIndex)
            {
                var skipped = bestIndex - _currentIndex;
                _currentIndex = bestIndex;
                for (int s = 0; s < skipped; s++)
                    Metrics.IncrementWaypointsReached();
            }
        }

        // After reaching the current waypoint, try to skip further ahead via LOS.
        if (_enableDynamicProbeSkipping && !_strictPathValidation && _currentIndex < _waypoints.Length)
            TryLosSkipAhead(currentPosition, mapId);
    }

    private bool CanAdvanceToNextWaypoint(Position currentPosition, uint mapId, float distanceToCurrentWaypoint2D)
    {
        if (_currentIndex + 1 >= _waypoints.Length)
            return true;

        // In non-strict mode, always advance once within the effective radius (caller already
        // checked effectiveRadius). The old commitDistance check was stricter than effectiveRadius,
        // causing the bot to get stuck at waypoints it had clearly reached (especially when
        // navmesh Z differs from terrain Z).
        if (!_strictPathValidation)
            return true;

        // Strict mode: use the tighter acceptance radius as commit distance and require LOS.
        var commitDistance = _waypointAcceptanceRadii.Length > _currentIndex
            ? _waypointAcceptanceRadii[_currentIndex]
            : CORNER_COMMIT_DISTANCE;

        if (distanceToCurrentWaypoint2D > commitDistance)
            return false;

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

            CalculatePath(currentPosition, destination, mapId, force: true, reason: NavigationTraceReason.StrictWaypointRecalc);
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

        CalculatePath(currentPosition, destination, mapId, force: true, reason: NavigationTraceReason.StrictWaypointRecalc);
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

    private Position? RecordWaypointResult(
        Position currentPosition,
        Position destination,
        Position? waypoint,
        bool usedDirectFallback,
        string resolution)
    {
        _traceUsedDirectFallback = usedDirectFallback;
        _traceLastResolution = resolution;

        var distanceToWaypoint = waypoint == null
            ? float.NaN
            : currentPosition.DistanceTo(waypoint);

        _executionSamples.Add(new NavigationExecutionSample(
            ClonePosition(currentPosition)!,
            ClonePosition(destination)!,
            ClonePosition(waypoint),
            _currentIndex,
            _tracePlanVersion,
            distanceToWaypoint,
            usedDirectFallback,
            _tickProvider(),
            resolution));

        if (_executionSamples.Count > MAX_TRACE_SAMPLES)
            _executionSamples.RemoveAt(0);

        // Drift detection: compute perpendicular distance from actual position to planned path
        if (_waypoints.Length >= 2)
        {
            var drift = ComputeDriftFromPath(currentPosition, _waypoints);
            Metrics.RecordDrift(drift);

            if (drift > 12f)
            {
                Serilog.Log.Warning(
                    "[NavigationPath] Drift {Drift:F1}y from planned path at ({X:F1},{Y:F1},{Z:F1}) res={Resolution} idx={Idx}/{Total}",
                    drift, currentPosition.X, currentPosition.Y, currentPosition.Z,
                    resolution, _currentIndex, _waypoints.Length);
            }
        }

        return waypoint;
    }

    private void RecordCalculatedTrace(
        uint mapId,
        Position start,
        Position end,
        in ValidatedPathResult path,
        string reason)
    {
        _traceMapId = mapId;
        _traceRequestedStart = ClonePosition(start);
        _traceRequestedDestination = ClonePosition(end);
        _traceServiceWaypoints = ClonePositions(path.RawPath);
        _tracePlannedWaypoints = ClonePositions(_waypoints);
        _traceAffordances = PathAffordanceInfo.Classify(_waypoints);
        _tracePlanVersion++;
        _traceLastReplanReason = reason;
        _traceLastResolution = null;
        _traceUsedDirectFallback = false;
        _traceUsedNearbyObjectOverlay = path.UsedNearbyObjectOverlay;
        _traceNearbyObjectCount = path.NearbyObjectCount;
        _traceSmoothPath = path.SmoothPath;
        _traceIsShortRoute = start.DistanceTo(end) <= SHORT_ROUTE_TRACE_DISTANCE;
        _traceLastPlanTick = _lastCalculationTick;
    }

    private NavigationExecutionSample[] CloneExecutionSamples()
    {
        if (_executionSamples.Count == 0)
            return [];

        var snapshot = new NavigationExecutionSample[_executionSamples.Count];
        for (var i = 0; i < _executionSamples.Count; i++)
        {
            var sample = _executionSamples[i];
            snapshot[i] = new NavigationExecutionSample(
                ClonePosition(sample.CurrentPosition)!,
                ClonePosition(sample.Destination)!,
                ClonePosition(sample.ReturnedWaypoint),
                sample.WaypointIndex,
                sample.PlanVersion,
                sample.DistanceToWaypoint,
                sample.UsedDirectFallback,
                sample.Tick,
                sample.Resolution);
        }

        return snapshot;
    }

    private static Position[] ClonePositions(IReadOnlyList<Position> positions)
    {
        if (positions.Count == 0)
            return [];

        var clone = new Position[positions.Count];
        for (var i = 0; i < positions.Count; i++)
            clone[i] = ClonePosition(positions[i])!;

        return clone;
    }

    private static Position? ClonePosition(Position? position)
        => position == null ? null : new Position(position.X, position.Y, position.Z);

    /// <summary>
    /// Minimum 2D perpendicular distance from a point to the nearest segment of the path.
    /// </summary>
    private static float ComputeDriftFromPath(Position point, Position[] path)
    {
        var minDist = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var dist = PerpendicularDistance2D(point, path[i], path[i + 1]);
            if (dist < minDist) minDist = dist;
        }
        return minDist == float.MaxValue ? 0f : minDist;
    }

    private static float PerpendicularDistance2D(Position point, Position segA, Position segB)
    {
        var dx = segB.X - segA.X;
        var dy = segB.Y - segA.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-6f)
        {
            var ex = point.X - segA.X;
            var ey = point.Y - segA.Y;
            return MathF.Sqrt(ex * ex + ey * ey);
        }
        var t = Math.Clamp(((point.X - segA.X) * dx + (point.Y - segA.Y) * dy) / lenSq, 0f, 1f);
        var px = point.X - (segA.X + t * dx);
        var py = point.Y - (segA.Y + t * dy);
        return MathF.Sqrt(px * px + py * py);
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

            // Phase 5a: When string-pulling would skip corners (farthestVisible > next),
            // verify the direct shortcut segment has sufficient lateral clearance for the
            // character's capsule. If the shortcut is too narrow (e.g., cutting across a
            // narrow bridge or ledge), fall back to the immediate next waypoint so the
            // original corners — which route around the narrow passage — are preserved.
            if (farthestVisible > anchorIndex + 1
                && !IsSegmentWideEnoughForCharacter(anchor, path[farthestVisible], mapId))
            {
                Metrics.IncrementWidthChecksFailed();
                farthestVisible = anchorIndex + 1;
            }

            // Always preserve the waypoint we advance to (it's either the farthest
            // visible or the next one if nothing further was visible).
            pulled.Add(path[farthestVisible]);
            anchor = path[farthestVisible];
            anchorIndex = farthestVisible;
        }

        return pulled.ToArray();
    }

    /// <summary>
    /// Phase 3: Check each consecutive waypoint pair for dynamic object triangle intersection.
    /// String-pulling ensures waypoints are LOS-clear at generation time, but the immediate
    /// next navmesh waypoint (anchor+1) is always included without an explicit LOS check.
    /// This step detects when a registered dynamic object (closed door, etc.) sits between
    /// two consecutive waypoints.
    /// Returns the original path when clear, or an empty array when any segment is blocked
    /// (causing GetValidatedPath to reject the path and trigger recalculation on next tick).
    /// </summary>
    private Position[] ValidateSegmentsAgainstDynamicObjects(uint mapId, Position[] path)
    {
        if (_pathfinding == null || path.Length < 2)
            return path;

        for (int i = 0; i < path.Length - 1; i++)
        {
            bool intersects;
            try { intersects = _pathfinding.SegmentIntersectsDynamicObjects(mapId, path[i], path[i + 1]); }
            catch { continue; } // Service unavailable — assume clear

            if (intersects)
            {
                Metrics.IncrementDynamicObstacleDeflections();
                Serilog.Log.Warning(
                    "[NavigationPath] Dynamic obstacle blocks segment {A} → {B} (deflections={Count}); forcing path recalc.",
                    i, i + 1, Metrics.DynamicObstacleDeflections);
                return [];
            }
        }

        return path;
    }

    private bool TryLosSkipAhead(Position currentPosition, uint mapId)
    {
        if (_currentIndex + 1 >= _waypoints.Length)
            return false;

        // Layer 1: Proactive LOS lookahead — when waypoint index changes, probe the
        // segment from the next waypoint to the one after it. If blocked, the next
        // waypoint is a real corner — cap skip-ahead so we don't jump past it.
        if (_currentIndex != _lastProbeWaypointIndex && _currentIndex + 2 < _waypoints.Length)
        {
            _lastProbeWaypointIndex = _currentIndex;
            try
            {
                // Check if the segment AFTER the next waypoint has LOS. If not, the
                // next waypoint is likely a corner that must be honored.
                _nextSegmentBlocked = !_pathfinding!.IsInLineOfSight(
                    mapId, _waypoints[_currentIndex + 1], _waypoints[_currentIndex + 2]);
            }
            catch { _nextSegmentBlocked = false; }
        }

        var nowTick = _tickProvider();

        // Use cached result if still valid and the index hasn't changed.
        if (_losSkipCacheIndex == _currentIndex
            && nowTick - _losSkipCacheTick < LOS_SKIP_CACHE_TTL_MS
            && _losSkipCacheFarthest > _currentIndex)
        {
            // Respect Layer 1 corner cap even for cached results
            var cappedFarthest = _nextSegmentBlocked
                ? Math.Min(_losSkipCacheFarthest, _currentIndex + 1)
                : _losSkipCacheFarthest;
            if (cappedFarthest > _currentIndex)
            {
                _currentIndex = cappedFarthest;
                return true;
            }
        }

        var farthestVisible = _currentIndex;
        // When the segment after the next waypoint is blocked, the next waypoint is a
        // corner — cap skip-ahead to at most that corner (don't jump past it).
        var maxLookahead = _nextSegmentBlocked ? 1 : MAX_RUNTIME_LOS_LOOKAHEAD;
        var scanLimit = Math.Min(_waypoints.Length - 1, _currentIndex + maxLookahead);

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

            // Phase 5b: Check overhead clearance — only in strict mode to avoid
            // constant probes on long outdoor paths where ceilings don't exist.
            if (_strictPathValidation && !HasSufficientHeadroom(from, to, mapId))
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

    private ValidatedPathResult GetValidatedPath(uint mapId, Position start, Position end, bool smoothPath)
    {
        if (_pathfinding == null)
            return new([], [], false, 0, smoothPath);

        Metrics.IncrementPathsCalculated();
        var sw = Stopwatch.StartNew();

        IReadOnlyList<DynamicObjectProto>? nearbyObjects = null;
        try
        {
            nearbyObjects = _nearbyObjectProvider?.Invoke(start, end);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[NavigationPath] Nearby-object overlay generation failed; falling back to legacy path request.");
        }

        var usedNearbyObjectOverlay = nearbyObjects is { Count: > 0 };
        var nearbyObjectCount = nearbyObjects?.Count ?? 0;

        var rawPath = usedNearbyObjectOverlay
            ? _pathfinding.GetPath(mapId, start, end, nearbyObjects, smoothPath, _race, _gender)
            : _pathfinding.GetPath(mapId, start, end, nearbyObjects: null, smoothPath, _race, _gender);
        var sanitizedPath = SanitizePath(rawPath);
        var prunedPath = _enableProbeHeuristics
            ? PruneProbeWaypoints(start, sanitizedPath)
            : sanitizedPath;
        // LOS-based string-pulling: remove intermediate waypoints where a straight
        // line is unobstructed. Corners remain because LOS is blocked by walls.
        var pulledPath = _enableProbeHeuristics
            ? StringPullPath(mapId, start, prunedPath)
            : prunedPath;

        // Phase 3: Validate path segments against dynamic objects (closed doors, etc.).
        // String-pulling already checks LOS, but consecutive navmesh waypoints (4y apart)
        // are always included without an explicit LOS check between them. This step
        // catches segments through registered dynamic objects using triangle intersection.
        // Returns empty if any segment is blocked, causing IsPathUsable to reject and retry.
        var dynValidatedPath = _enableProbeHeuristics
            ? ValidateSegmentsAgainstDynamicObjects(mapId, pulledPath)
            : pulledPath;

        // Phase 3a: Post-path Z correction — replace navmesh Z with collision ground Z
        // where they differ. Fixes Orgrimmar WMO areas where navmesh Z diverges from
        // the actual walkable surface by a few yards.
        var zCorrectedPath = CorrectPathZFromCollision(mapId, dynValidatedPath);

        // Phase 4b: Cliff rerouting — probe each segment for cliff edges and insert
        // offset waypoints to steer the bot away from dangerous drops.
        var cliffReroutedPath = _enableProbeHeuristics
            ? ReroutePathAroundCliffs(mapId, start, zCorrectedPath)
            : zCorrectedPath;

        var usable = IsPathUsable(mapId, start, end, cliffReroutedPath);

        sw.Stop();
        Metrics.RecordPathDuration(sw.ElapsedMilliseconds);

        if (!usable && cliffReroutedPath.Length > 0)
        {
            Serilog.Log.Warning("[NavigationPath] Path rejected by IsPathUsable: raw={RawCount} sanitized={SanitizedCount} pruned={PrunedCount} pulled={PulledCount} zCorrected={ZCorrectedCount} cliffRerouted={CliffReroutedCount} smooth={Smooth} strict={Strict} start=({SX:F1},{SY:F1},{SZ:F1}) end=({EX:F1},{EY:F1},{EZ:F1})",
                rawPath.Length, sanitizedPath.Length, prunedPath.Length, pulledPath.Length, zCorrectedPath.Length, cliffReroutedPath.Length, smoothPath, _strictPathValidation,
                start.X, start.Y, start.Z, end.X, end.Y, end.Z);
        }

        var result = usable ? cliffReroutedPath : [];
        Metrics.RecordPathLength(result.Length);
        if (result.Length == 0)
            Metrics.IncrementPathsFailed();
        return new(rawPath, result, usedNearbyObjectOverlay, nearbyObjectCount, smoothPath);
    }

    /// <summary>
    /// Phase 3a: Correct waypoint Z values using collision ground queries.
    /// The navmesh may produce Z values that differ from the actual walkable surface
    /// (e.g., Orgrimmar WMO floors where navmesh is 3-4y below the collision surface).
    /// For each waypoint, query GetGroundZ and use the collision Z if it's within 5y
    /// of the navmesh Z. If the collision query fails or returns a value too far from
    /// the navmesh Z, keep the original navmesh Z.
    /// </summary>
    private Position[] CorrectPathZFromCollision(uint mapId, Position[] path)
    {
        if (_pathfinding == null || path.Length == 0)
            return path;

        const float MAX_Z_CORRECTION = 5.0f; // Max navmesh-collision Z delta to accept

        var corrected = new Position[path.Length];
        int corrections = 0;

        for (int i = 0; i < path.Length; i++)
        {
            var wp = path[i];
            var (groundZ, found) = _pathfinding.GetGroundZ(mapId, wp);
            if (found && MathF.Abs(groundZ - wp.Z) <= MAX_Z_CORRECTION)
            {
                corrected[i] = new Position(wp.X, wp.Y, groundZ);
                if (MathF.Abs(groundZ - wp.Z) > 0.1f)
                    corrections++;
            }
            else
            {
                corrected[i] = wp;
            }
        }

        if (corrections > 0)
        {
            Metrics.AddZCorrections(corrections);
            Serilog.Log.Debug("[NavigationPath] Z-corrected {Count}/{Total} waypoints from collision ground (mapId={MapId})",
                corrections, path.Length, mapId);
        }

        return corrected;
    }

    public void CalculatePath(Position start, Position end, uint mapId, bool force = false, string reason = NavigationTraceReason.Manual)
    {
        var nowTick = _tickProvider();
        if (!force && _hasCalculatedPath && nowTick - _lastCalculationTick < RECALCULATE_COOLDOWN_MS)
            return;

        if (force)
            Metrics.IncrementRecalculationsTriggered();

        _lastCalculationTick = nowTick;
        _hasCalculatedPath = true;
>>>>>>> cpp_physics_system
        _destination = end;
        _lastWaypointSamplePosition = null;
        _lastWaypointSampleDistance = float.NaN;
        _stalledNearWaypointSamples = 0;
<<<<<<< HEAD
=======
        _consecutiveWallHitSamples = 0;
        _avoidanceWaypoint = null;
        _avoidanceFramesRemaining = 0;
        _consecutiveAvoidanceFailures = 0;
        _nextSegmentBlocked = false;
        _lastProbeWaypointIndex = -1;
>>>>>>> cpp_physics_system

        if (_pathfinding == null)
        {
            _waypoints = [];
<<<<<<< HEAD
            _currentIndex = 0;
=======
            _waypointAcceptanceRadii = [];
            _currentIndex = 0;
            RecordCalculatedTrace(mapId, start, end, new([], [], false, 0, _enableProbeHeuristics), reason);
>>>>>>> cpp_physics_system
            return;
        }

        try
        {
<<<<<<< HEAD
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
=======
            // When probe heuristics are enabled (normal navigation), prefer smooth
            // paths (Detour string-pulling) — fewer redundant waypoints.
            // When disabled (corpse runs), prefer non-smooth paths to avoid Detour
            // string-pulling routes through steep Z transitions that the headless
            // client can't safely descend.
            var preferSmooth = _enableProbeHeuristics;
            var selectedPath = GetValidatedPath(mapId, start, end, smoothPath: preferSmooth);
            _waypoints = selectedPath.PlannedPath;
            if (_waypoints.Length == 0)
            {
                selectedPath = GetValidatedPath(mapId, start, end, smoothPath: !preferSmooth);
                _waypoints = selectedPath.PlannedPath;
            }

            // Always begin at index 0. GetNextWaypoint() will safely advance
            // near/duplicate start points with LOS guards instead of blindly
            // skipping a potential first corner waypoint.
            _currentIndex = 0;
            _waypointAcceptanceRadii = [];
            if (_enableProbeHeuristics)
            {
                OffsetCornerWaypoints(start);
                ComputeWaypointAcceptanceRadii(start);
            }

            RecordCalculatedTrace(mapId, start, end, selectedPath, reason);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("[NAV-DIAG] CalculatePath FAILED: map={MapId}, start=({SX:F1},{SY:F1},{SZ:F1}), end=({EX:F1},{EY:F1},{EZ:F1}): {Error}",
                mapId, start.X, start.Y, start.Z, end.X, end.Y, end.Z, ex.Message);
            _waypoints = [];
            _waypointAcceptanceRadii = [];
            _currentIndex = 0;
            RecordCalculatedTrace(mapId, start, end, new([], [], false, 0, _enableProbeHeuristics), reason);
        }
    }

    private void ComputeWaypointAcceptanceRadii(Position start)
    {
        // Speed-based floor: at full speed the bot covers _characterSpeed yards per second.
        // Half a second at 20% margin prevents overshoot at full run speed.
        float speedBasedFloor = _characterSpeed * 0.5f * 1.2f;

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
            float angleBasedRadius = MAX_ACCEPTANCE_RADIUS - t * (MAX_ACCEPTANCE_RADIUS - MIN_ACCEPTANCE_RADIUS);
            _waypointAcceptanceRadii[i] = MathF.Max(speedBasedFloor, angleBasedRadius);
        }
    }

    /// <summary>
    /// Offset sharp-corner waypoints AWAY from the inner wall of the turn.
    ///
    /// The bisector of (inDir + outDir) points toward the INNER (concave) side of the turn —
    /// that is, toward the wall the bot would collide with at a tight corner. Moving the waypoint
    /// in that direction pushes it INTO the wall, making corners worse.
    ///
    /// We negate the bisector so the waypoint shifts toward the outer gap, giving the capsule
    /// clearance to arc smoothly through the corner without pressing into the inner wall.
    /// </summary>
    private void OffsetCornerWaypoints(Position start)
    {
        const float cornerAngleThreshold = 60f;
        float offsetDistance = _capsuleRadius * 3.0f; // 3× radius gives safe wall clearance at full speed
        const float minSegmentLength = 4f; // only offset when segments are long enough

        for (var i = 0; i < _waypoints.Length; i++)
        {
            if (i + 1 >= _waypoints.Length) continue; // skip destination

            var prev = i == 0 ? start : _waypoints[i - 1];
            var curr = _waypoints[i];
            var next = _waypoints[i + 1];

            var turnAngle = ComputeTurnAngle2D(prev, curr, next);
            if (turnAngle < cornerAngleThreshold) continue;

            // Compute bisector of incoming and outgoing unit direction vectors.
            // This bisector points toward the INNER corner (concave wall).
            // Negate it to get the direction AWAY from the inner wall (toward the outer gap).
            var inDx = curr.X - prev.X;
            var inDy = curr.Y - prev.Y;
            var inLen = MathF.Sqrt(inDx * inDx + inDy * inDy);
            var outDx = next.X - curr.X;
            var outDy = next.Y - curr.Y;
            var outLen = MathF.Sqrt(outDx * outDx + outDy * outDy);
            if (inLen < minSegmentLength || outLen < minSegmentLength) continue;

            // bisDir: toward inner corner (concave side).
            // -bisDir: away from inner wall = toward outer gap = correct offset direction.
            var bisX = inDx / inLen + outDx / outLen;
            var bisY = inDy / inLen + outDy / outLen;
            var bisLen = MathF.Sqrt(bisX * bisX + bisY * bisY);
            if (bisLen < 0.01f) continue;

            _waypoints[i] = new Position(
                curr.X - (bisX / bisLen) * offsetDistance,  // negated: push away from inner wall
                curr.Y - (bisY / bisLen) * offsetDistance,
                curr.Z);
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
>>>>>>> cpp_physics_system
        }
    }

    /// <summary>
<<<<<<< HEAD
=======
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
    /// Probes ground Z at an angular offset from the movement direction.
    /// Returns the drop distance if an edge/cliff is detected, or 0 if safe.
    /// Returns -1 if the probe is unavailable (no pathfinding client or probe failed).
    /// </summary>
    /// <param name="currentPos">Current character position.</param>
    /// <param name="headingRadians">Movement heading in radians (atan2(dy, dx) toward target).</param>
    /// <param name="angleOffsetRadians">Angle offset from heading (positive = left/CCW, negative = right/CW).</param>
    /// <param name="mapId">Map ID for ground Z query.</param>
    /// <param name="probeDistance">How far to probe from current position.</param>
    private float ProbeEdgeAtAngle(Position currentPos, float headingRadians, float angleOffsetRadians, uint mapId, float probeDistance)
    {
        if (_pathfinding == null) return -1f;

        var probeAngle = headingRadians + angleOffsetRadians;
        var probeX = currentPos.X + MathF.Cos(probeAngle) * probeDistance;
        var probeY = currentPos.Y + MathF.Sin(probeAngle) * probeDistance;

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
    /// Multi-directional cliff probe. Checks for cliffs in 5 directions relative
    /// to the movement heading: forward (0deg), forward-left (+45deg), forward-right (-45deg),
    /// left (+90deg), and right (-90deg).
    /// <para>
    /// Forward probe uses <see cref="CLIFF_PROBE_DISTANCE"/> (3yd).
    /// Diagonal probes (+/-45deg) use the average of forward and lateral distances.
    /// Lateral probes (+/-90deg) use <see cref="CLIFF_LATERAL_PROBE_DISTANCE"/> (1.5yd).
    /// </para>
    /// Returns the result of each probe as a <see cref="CliffProbeResult"/>.
    /// </summary>
    public CliffProbeResult ProbeEdgesMultiDirectional(Position currentPos, Position targetWaypoint, uint mapId)
    {
        Metrics.IncrementCliffProbesTriggered();

        var dx = targetWaypoint.X - currentPos.X;
        var dy = targetWaypoint.Y - currentPos.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f)
            return new CliffProbeResult(0f, 0f, 0f, 0f, 0f);

        var heading = MathF.Atan2(dy, dx);

        const float deg45 = MathF.PI / 4f;
        const float deg90 = MathF.PI / 2f;
        var diagonalDistance = (CLIFF_PROBE_DISTANCE + CLIFF_LATERAL_PROBE_DISTANCE) / 2f; // 2.25yd

        var forward      = ProbeEdgeAtAngle(currentPos, heading, 0f,     mapId, CLIFF_PROBE_DISTANCE);
        var forwardLeft  = ProbeEdgeAtAngle(currentPos, heading, deg45,  mapId, diagonalDistance);
        var forwardRight = ProbeEdgeAtAngle(currentPos, heading, -deg45, mapId, diagonalDistance);
        var left         = ProbeEdgeAtAngle(currentPos, heading, deg90,  mapId, CLIFF_LATERAL_PROBE_DISTANCE);
        var right        = ProbeEdgeAtAngle(currentPos, heading, -deg90, mapId, CLIFF_LATERAL_PROBE_DISTANCE);

        return new CliffProbeResult(forward, forwardLeft, forwardRight, left, right);
    }

    /// <summary>
    /// Whether a cliff is detected in ANY direction around the movement heading.
    /// Uses <see cref="CLIFF_NEARBY_DROP_THRESHOLD"/> (3yd) for lateral/diagonal probes
    /// and <see cref="CLIFF_DROP_THRESHOLD"/> (8yd) for the forward probe.
    /// Returns true if any probe detects a significant drop.
    /// </summary>
    public bool IsCliffNearby(Position currentPos, Position targetWaypoint, uint mapId)
    {
        var result = ProbeEdgesMultiDirectional(currentPos, targetWaypoint, mapId);
        return result.IsCliffDetected(CLIFF_DROP_THRESHOLD, CLIFF_NEARBY_DROP_THRESHOLD);
    }

    /// <summary>
    /// Phase 4b: Attempt to reroute around a detected cliff edge by offsetting the path
    /// perpendicular to the movement direction, away from the cliff side.
    /// <para>
    /// When cliff is on one side only: offsets away from that side.
    /// When cliff is ahead: tries both left and right offsets, picks the one with valid ground.
    /// </para>
    /// </summary>
    /// <param name="mapId">Map ID for ground Z queries.</param>
    /// <param name="from">Current waypoint position (or bot position).</param>
    /// <param name="to">Next waypoint position (movement target).</param>
    /// <param name="probeResult">Multi-directional cliff probe result for this segment.</param>
    /// <returns>An offset waypoint that avoids the cliff, or null if no safe reroute is found.</returns>
    public Position? RerouteAroundCliff(uint mapId, Position from, Position to, CliffProbeResult probeResult)
    {
        if (_pathfinding == null)
            return null;

        // Compute movement heading and perpendicular (left = +90deg, right = -90deg)
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f)
            return null;

        var heading = MathF.Atan2(dy, dx);
        var offsetDistance = _capsuleRadius * 4.0f;

        // Perpendicular directions: left is heading + 90deg, right is heading - 90deg
        var leftAngle = heading + MathF.PI / 2f;
        var rightAngle = heading - MathF.PI / 2f;

        // Determine which sides have cliff danger using the thresholds
        bool cliffForward = DropExceedsThresholdStatic(probeResult.Forward, CLIFF_DROP_THRESHOLD);
        bool cliffLeft = DropExceedsThresholdStatic(probeResult.Left, CLIFF_NEARBY_DROP_THRESHOLD)
                      || DropExceedsThresholdStatic(probeResult.ForwardLeft, CLIFF_NEARBY_DROP_THRESHOLD);
        bool cliffRight = DropExceedsThresholdStatic(probeResult.Right, CLIFF_NEARBY_DROP_THRESHOLD)
                       || DropExceedsThresholdStatic(probeResult.ForwardRight, CLIFF_NEARBY_DROP_THRESHOLD);

        if (!cliffForward && !cliffLeft && !cliffRight)
            return null; // No cliff detected — no reroute needed

        // Midpoint of the segment — the rerouted waypoint will be offset from here
        var midX = (from.X + to.X) / 2f;
        var midY = (from.Y + to.Y) / 2f;
        var midZ = (from.Z + to.Z) / 2f;

        if (cliffLeft && !cliffRight)
        {
            // Cliff on left — offset right (away from cliff)
            return TryOffsetWaypoint(mapId, midX, midY, midZ, rightAngle, offsetDistance);
        }

        if (cliffRight && !cliffLeft)
        {
            // Cliff on right — offset left (away from cliff)
            return TryOffsetWaypoint(mapId, midX, midY, midZ, leftAngle, offsetDistance);
        }

        // Cliff ahead (or on both sides) — try both directions, pick whichever has ground
        var leftCandidate = TryOffsetWaypoint(mapId, midX, midY, midZ, leftAngle, offsetDistance);
        var rightCandidate = TryOffsetWaypoint(mapId, midX, midY, midZ, rightAngle, offsetDistance);

        if (leftCandidate != null && rightCandidate != null)
        {
            // Both valid — prefer the one with ground closer to our current Z (less vertical change)
            var leftDeltaZ = MathF.Abs(leftCandidate.Z - midZ);
            var rightDeltaZ = MathF.Abs(rightCandidate.Z - midZ);
            return leftDeltaZ <= rightDeltaZ ? leftCandidate : rightCandidate;
        }

        return leftCandidate ?? rightCandidate;
    }

    /// <summary>
    /// Attempts to create an offset waypoint at the given angle from a midpoint.
    /// Queries GetGroundZ to verify the offset position is walkable.
    /// Returns the offset position with valid ground Z, or null if ground is not found
    /// or the ground Z differs too much from the reference Z (potential cliff at the offset).
    /// </summary>
    private Position? TryOffsetWaypoint(uint mapId, float midX, float midY, float midZ, float angle, float distance)
    {
        var offsetX = midX + MathF.Cos(angle) * distance;
        var offsetY = midY + MathF.Sin(angle) * distance;

        try
        {
            var (groundZ, found) = _pathfinding.GetGroundZ(mapId, new Position(offsetX, offsetY, midZ));
            if (!found)
                return null;

            // Reject if the offset point itself is a cliff (ground is much lower than reference)
            if (midZ - groundZ > CLIFF_DROP_THRESHOLD)
                return null;

            return new Position(offsetX, offsetY, groundZ);
        }
        catch
        {
            return null; // IPC failure
        }
    }

    /// <summary>
    /// Phase 4b path pipeline: scans each segment of the path for cliff edges and inserts
    /// rerouted waypoints where needed. Processes segments from start toward end so that
    /// inserted waypoints don't shift indices of segments not yet processed.
    /// </summary>
    private Position[] ReroutePathAroundCliffs(uint mapId, Position start, Position[] path)
    {
        if (_pathfinding == null || path.Length == 0)
            return path;

        var result = new List<Position>(path.Length + 4); // slight over-alloc for inserts
        int reroutes = 0;

        // Check segment from start to first waypoint
        var prevPos = start;
        for (int i = 0; i < path.Length; i++)
        {
            var wp = path[i];
            var probeResult = ProbeEdgesMultiDirectional(prevPos, wp, mapId);

            if (probeResult.IsCliffDetected(CLIFF_DROP_THRESHOLD, CLIFF_NEARBY_DROP_THRESHOLD))
            {
                var reroutedWp = RerouteAroundCliff(mapId, prevPos, wp, probeResult);
                if (reroutedWp != null)
                {
                    result.Add(reroutedWp);
                    reroutes++;
                    Serilog.Log.Debug(
                        "[NavigationPath] Cliff reroute at segment {Idx}: ({FX:F1},{FY:F1},{FZ:F1})->({TX:F1},{TY:F1},{TZ:F1}) via ({RX:F1},{RY:F1},{RZ:F1}) drop={MaxDrop:F1}",
                        i, prevPos.X, prevPos.Y, prevPos.Z, wp.X, wp.Y, wp.Z,
                        reroutedWp.X, reroutedWp.Y, reroutedWp.Z, probeResult.MaxDrop);
                }
            }

            result.Add(wp);
            prevPos = wp;
        }

        if (reroutes > 0)
        {
            for (int r = 0; r < reroutes; r++)
                Metrics.IncrementCliffReroutes();

            Serilog.Log.Information(
                "[NavigationPath] Phase 4b: inserted {Count} cliff-reroute waypoint(s) (mapId={MapId}, path {Before}->{After})",
                reroutes, mapId, path.Length, result.Count);
        }

        return result.Count == path.Length ? path : result.ToArray();
    }

    /// <summary>
    /// Static helper matching CliffProbeResult.DropExceedsThreshold logic for use outside the record struct.
    /// </summary>
    private static bool DropExceedsThresholdStatic(float drop, float threshold) =>
        drop >= threshold || drop == float.MaxValue;

    /// <summary>
    /// Result of a multi-directional cliff probe. Each field contains the drop distance
    /// in that direction: 0 = safe/level, positive = drop detected, -1 = unavailable,
    /// float.MaxValue = void/no ground.
    /// </summary>
    public readonly record struct CliffProbeResult(
        float Forward,
        float ForwardLeft,
        float ForwardRight,
        float Left,
        float Right)
    {
        /// <summary>
        /// Whether any probe detected a cliff. The forward probe uses a separate
        /// (typically higher) threshold since forward drops are expected when
        /// descending terrain intentionally.
        /// </summary>
        public bool IsCliffDetected(float forwardThreshold, float lateralThreshold)
        {
            return DropExceedsThreshold(Forward, forwardThreshold)
                || DropExceedsThreshold(ForwardLeft, lateralThreshold)
                || DropExceedsThreshold(ForwardRight, lateralThreshold)
                || DropExceedsThreshold(Left, lateralThreshold)
                || DropExceedsThreshold(Right, lateralThreshold);
        }

        /// <summary>
        /// The maximum drop distance across all probes, ignoring unavailable probes (-1).
        /// </summary>
        public float MaxDrop => MathF.Max(0f, MathF.Max(
            MathF.Max(EffectiveDrop(Forward), EffectiveDrop(ForwardLeft)),
            MathF.Max(MathF.Max(EffectiveDrop(ForwardRight), EffectiveDrop(Left)), EffectiveDrop(Right))));

        /// <summary>
        /// Whether any probe returned void/no-ground (float.MaxValue).
        /// </summary>
        public bool HasVoid =>
            Forward == float.MaxValue || ForwardLeft == float.MaxValue ||
            ForwardRight == float.MaxValue || Left == float.MaxValue || Right == float.MaxValue;

        private static bool DropExceedsThreshold(float drop, float threshold) =>
            drop >= threshold || drop == float.MaxValue;

        private static float EffectiveDrop(float drop) =>
            drop < 0 ? 0f : drop; // treat unavailable (-1) as 0
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
>>>>>>> cpp_physics_system
    /// Force a path recalculation on the next GetNextWaypoint call.
    /// Call this when the target changes (e.g., mob died, new target acquired).
    /// </summary>
    public void Clear()
    {
        _waypoints = [];
<<<<<<< HEAD
        _currentIndex = 0;
        _destination = null;
        _lastCalculationTick = -RECALCULATE_COOLDOWN_MS;
        _lastWaypointSamplePosition = null;
        _lastWaypointSampleDistance = float.NaN;
        _stalledNearWaypointSamples = 0;
=======
        _waypointAcceptanceRadii = [];
        _currentIndex = 0;
        _destination = null;
        _lastCalculationTick = 0;
        _hasCalculatedPath = false;
        _lastWaypointSamplePosition = null;
        _lastWaypointSampleDistance = float.NaN;
        _stalledNearWaypointSamples = 0;
        _consecutiveWallHitSamples = 0;
        _avoidanceWaypoint = null;
        _avoidanceFramesRemaining = 0;
        _consecutiveAvoidanceFailures = 0;
        _nextSegmentBlocked = false;
        _lastProbeWaypointIndex = -1;
        _losSkipCacheIndex = -1;
        _losSkipCacheFarthest = -1;
        _losSkipCacheTick = 0;
        _traceServiceWaypoints = [];
        _tracePlannedWaypoints = [];
        _traceAffordances = PathAffordanceInfo.Empty;
        _traceRequestedStart = null;
        _traceRequestedDestination = null;
        _traceMapId = 0;
        _tracePlanVersion = 0;
        _traceLastReplanReason = null;
        _traceLastResolution = null;
        _traceUsedDirectFallback = false;
        _traceUsedNearbyObjectOverlay = false;
        _traceNearbyObjectCount = 0;
        _traceSmoothPath = false;
        _traceIsShortRoute = false;
        _traceLastPlanTick = 0;
        _executionSamples.Clear();
    }

    /// <summary>
    /// Update the character's run speed so acceptance radii scale correctly.
    /// Recomputes radii only when the speed changes by more than 0.5 y/s to avoid
    /// churning on minor floating-point jitter each tick.
    /// </summary>
    public void UpdateCharacterSpeed(float speed)
    {
        if (MathF.Abs(speed - _characterSpeed) < 0.5f)
            return;

        _characterSpeed = speed;

        // Recompute radii if we already have a path with a start reference.
        if (_waypoints.Length > 0 && _enableProbeHeuristics)
        {
            var start = _currentIndex > 0 ? _waypoints[_currentIndex - 1] : _waypoints[0];
            ComputeWaypointAcceptanceRadii(start);
        }
>>>>>>> cpp_physics_system
    }

    /// <summary>
    /// Whether the path has remaining waypoints.
    /// </summary>
    public bool HasWaypoints => _waypoints.Length > 0 && _currentIndex < _waypoints.Length;

    /// <summary>
    /// Number of remaining waypoints.
    /// </summary>
    public int RemainingWaypoints => Math.Max(0, _waypoints.Length - _currentIndex);
<<<<<<< HEAD
=======

    // =========================================================================
    // TRANSPORT AWARENESS — elevator/boat/zeppelin integration
    // =========================================================================

    private TransportWaitingLogic? _activeTransportRide;
    private TransportData.TransportDefinition? _pendingTransport;

    /// <summary>
    /// The currently active transport ride state machine, or null if not riding.
    /// </summary>
    public TransportWaitingLogic? ActiveTransportRide => _activeTransportRide;

    /// <summary>
    /// Whether a transport ride is currently active.
    /// </summary>
    public bool IsRidingTransport => _activeTransportRide != null
        && _activeTransportRide.CurrentPhase != TransportPhase.Complete;

    /// <summary>
    /// Check if navigating from current to destination requires a transport (elevator).
    /// If so, activates the transport state machine and returns true.
    /// Call <see cref="GetTransportTarget"/> each tick while <see cref="IsRidingTransport"/> is true.
    /// </summary>
    public bool CheckTransportNeeded(Position current, Position destination, uint mapId)
    {
        // Don't re-detect if already riding
        if (IsRidingTransport) return true;

        var elevator = TransportData.DetectElevatorCrossing(mapId, current, destination);
        if (elevator == null) return false;

        var boardStop = TransportData.FindNearestStop(elevator, current);
        var exitStop = TransportData.GetDestinationStop(elevator, current);
        if (boardStop == null || exitStop == null) return false;

        _pendingTransport = elevator;
        _activeTransportRide = new TransportWaitingLogic(elevator, boardStop, exitStop);
        return true;
    }

    /// <summary>
    /// Get the position to move toward during a transport ride.
    /// Returns null when the ride is complete — caller should resume normal pathfinding.
    /// </summary>
    public Position? GetTransportTarget(
        Position currentPosition,
        ulong currentTransportGuid,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects,
        float deltaTimeSec)
    {
        if (_activeTransportRide == null) return null;

        var target = _activeTransportRide.Update(
            currentPosition, currentTransportGuid, nearbyObjects, deltaTimeSec);

        // Ride complete — clear transport state and force path recalculation
        if (_activeTransportRide.CurrentPhase == TransportPhase.Complete)
        {
            _pendingTransport = null;
            _activeTransportRide = null;
            Clear();
        }

        return target;
    }

    /// <summary>
    /// Cancel any active transport ride. Use when the bot is interrupted
    /// (death, combat, teleport).
    /// </summary>
    public void CancelTransportRide()
    {
        _pendingTransport = null;
        _activeTransportRide = null;
    }

    // ── Phase 5a: Runtime path width validation ──────────────────────────

    /// <summary>
    /// Phase 5a: Check whether a path segment has sufficient lateral clearance
    /// for the character's capsule radius.
    ///
    /// Probes the ground at two points perpendicular to the segment direction
    /// at ±capsuleRadius from the segment midpoint. If either lateral probe
    /// has no ground or has a Z difference exceeding the threshold, the
    /// segment is considered too narrow (e.g., a ledge, bridge edge, or
    /// narrow corridor wall).
    /// </summary>
    /// <param name="from">Start of the segment.</param>
    /// <param name="to">End of the segment.</param>
    /// <param name="mapId">Map ID for ground queries.</param>
    /// <returns>True if the segment is wide enough; false if too narrow.</returns>
    private bool IsSegmentWideEnoughForCharacter(Position from, Position to, uint mapId)
    {
        // If pathfinding is unavailable, skip the check (conservative: assume OK).
        if (_pathfinding == null)
            return true;

        const float LATERAL_Z_THRESHOLD = 2.0f;

        // Compute the midpoint of the segment.
        float midX = (from.X + to.X) * 0.5f;
        float midY = (from.Y + to.Y) * 0.5f;
        float midZ = (from.Z + to.Z) * 0.5f;

        // Compute the segment direction in 2D (XY plane).
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float len2D = MathF.Sqrt(dx * dx + dy * dy);

        // Degenerate segment (zero-length in 2D): can't determine perpendicular.
        if (len2D < 0.001f)
            return true;

        // Perpendicular direction (rotated 90 degrees in XY plane).
        float perpX = -dy / len2D;
        float perpY = dx / len2D;

        // Probe at +capsuleRadius and -capsuleRadius from the midpoint.
        var probeLeft = new Position(
            midX + perpX * _capsuleRadius,
            midY + perpY * _capsuleRadius,
            midZ);

        var probeRight = new Position(
            midX - perpX * _capsuleRadius,
            midY - perpY * _capsuleRadius,
            midZ);

        try
        {
            var (leftZ, leftFound) = _pathfinding.GetGroundZ(mapId, probeLeft);
            if (!leftFound || MathF.Abs(leftZ - midZ) > LATERAL_Z_THRESHOLD)
                return false;

            var (rightZ, rightFound) = _pathfinding.GetGroundZ(mapId, probeRight);
            if (!rightFound || MathF.Abs(rightZ - midZ) > LATERAL_Z_THRESHOLD)
                return false;
        }
        catch
        {
            // If the ground query throws, be conservative and assume passable.
            return true;
        }

        return true;
    }

    // ── Phase 5b: Runtime path headroom validation ───────────────────────

    /// <summary>
    /// Phase 5b: Check whether a path segment has sufficient overhead clearance
    /// for the character's capsule height.
    ///
    /// Samples the segment midpoint and checks LOS from the top of the capsule
    /// (groundZ + _capsuleHeight) upward by a 0.4y margin. If that short vertical
    /// segment is blocked by geometry (WMO ceiling, cave roof, bridge underside),
    /// the character would clip through the ceiling — return false.
    ///
    /// Uses the existing IsInLineOfSight infrastructure (no new C++ export needed).
    /// A failed LOS upward probe = ceiling at or below capsule height.
    /// </summary>
    /// <param name="from">Start of the segment.</param>
    /// <param name="to">End of the segment.</param>
    /// <param name="mapId">Map ID for collision queries.</param>
    /// <returns>True if headroom is sufficient; false if ceiling is too low.</returns>
    private bool HasSufficientHeadroom(Position from, Position to, uint mapId)
    {
        if (_pathfinding == null)
            return true; // Can't check — assume clear

        // Sample the midpoint of the segment.
        var midX = (from.X + to.X) * 0.5f;
        var midY = (from.Y + to.Y) * 0.5f;
        var midZ = (from.Z + to.Z) * 0.5f;

        // Cast a 0.4y ray upward from the top of the capsule.
        // If geometry blocks this short segment, the ceiling is within capsule height.
        const float HeadroomMargin = 0.4f;
        var headTop = new Position(midX, midY, midZ + _capsuleHeight);
        var headProbe = new Position(midX, midY, midZ + _capsuleHeight + HeadroomMargin);

        try
        {
            return _pathfinding.IsInLineOfSight(mapId, headTop, headProbe);
        }
        catch
        {
            return true; // Service unavailable — assume clear
        }
    }
>>>>>>> cpp_physics_system
}
