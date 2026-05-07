using GameData.Core.Models;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using static BotRunner.Movement.TransportData;

namespace BotRunner.Movement;

/// <summary>
/// Current phase of a transport interaction.
/// </summary>
public enum TransportPhase
{
    /// <summary>Walking to the boarding stop's waiting position.</summary>
    Approaching,
    /// <summary>At the boarding stop, waiting for the transport to arrive.</summary>
    WaitingForArrival,
    /// <summary>Transport is at the stop — stepping onto it.</summary>
    Boarding,
    /// <summary>Riding the transport to the destination.</summary>
    Riding,
    /// <summary>Transport is at the destination — stepping off.</summary>
    Disembarking,
    /// <summary>Transport ride is complete.</summary>
    Complete,
}

/// <summary>
/// State machine for interacting with transports (elevators, boats, zeppelins).
/// Call <see cref="Update"/> each tick with the current state. Returns a position
/// to move toward, or null when the ride is complete.
/// </summary>
public class TransportWaitingLogic
{
    private const float ELEVATOR_MAX_WAIT_TIME_SEC = 120f; // 2 minutes max wait
    private const float SCHEDULED_TRANSPORT_MAX_WAIT_TIME_SEC = 420f; // 7 minutes for boats/zeppelins
    private const float BOARDING_TIMEOUT_SEC = 10f; // 10s to board once transport arrives
    private const float SCHEDULED_TRANSPORT_BOARDING_TIMEOUT_SEC = 60f;
    private const float DISEMBARK_TIMEOUT_SEC = 10f; // 10s to step off
    private const float SCHEDULED_TRANSPORT_EXIT_Z_TOLERANCE = 25f;
    private const float SCHEDULED_TRANSPORT_BOARDING_APPROACH_DISTANCE = 8f;
    private const float SCHEDULED_TRANSPORT_BOARDING_LOST_Z_DROP = 12f;
    private const float SCHEDULED_TRANSPORT_BOARDING_LOST_DISTANCE = 60f;
    private const float SCHEDULED_TRANSPORT_DOCK_STABLE_TIME_SEC = 5f;
    private const float SCHEDULED_TRANSPORT_DOCK_STABLE_EPSILON = 0.05f;
    private const float SCHEDULED_TRANSPORT_LOCAL_BOARDING_OFFSET_XY_TOLERANCE = 1.5f;
    private const float SCHEDULED_TRANSPORT_LOCAL_BOARDING_OFFSET_Z_TOLERANCE = 2.0f;
    private const float ELEVATOR_AT_STOP_Z_TOLERANCE = 3.0f;
    private const float ELEVATOR_STOP_MARKER_DISTANCE = 12.0f;
    private const float BOAT_AT_STOP_DISTANCE = 20f;
    private const float ZEPPELIN_AT_STOP_DISTANCE = 45f;
    private const uint ELEVATOR_STOP_MARKER_DISPLAY_ID = 462;

    private const string NativeOffMeshBoardingEnvVar = "WWOW_OFFMESH_NATIVE_BOARDING";

    // PFS-OVERHAUL-005 Phase 5: when set, suppress the hand-tuned BoardingPosition
    // nudges so navigation flows through the baked Detour off-mesh edges instead
    // of short-circuiting straight to the gangplank deck.
    internal static bool IsNativeOffMeshBoardingEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable(NativeOffMeshBoardingEnvVar),
            "1",
            StringComparison.Ordinal);

    private readonly TransportDefinition _transport;
    private readonly TransportStop _boardingStop;
    private readonly TransportStop _destinationStop;

    private float _phaseElapsed;
    private Position? _boardingWaypoint;
    private Position? _disembarkWaypoint;
    private ulong _trackedDockTransportGuid;
    private uint _trackedDockTransportEntry;
    private uint _trackedDockTransportDisplayId;
    private Position? _lastDockTransportPosition;
    private float _dockStableElapsed;

    public TransportPhase CurrentPhase { get; private set; } = TransportPhase.Approaching;
    public bool MissedBoardingAttempt { get; private set; }

    public TransportWaitingLogic(
        TransportDefinition transport,
        TransportStop boardingStop,
        TransportStop destinationStop)
    {
        _transport = transport;
        _boardingStop = boardingStop;
        _destinationStop = destinationStop;
    }

    /// <summary>
    /// Tick the state machine. Returns a position to move toward,
    /// or null when the ride is complete.
    /// </summary>
    /// <param name="currentPosition">Player's current world position.</param>
    /// <param name="currentTransportGuid">
    /// Non-zero when the player is on a transport (transportGuid from movement info).
    /// </param>
    /// <param name="nearbyObjects">
    /// Nearby dynamic objects for elevator-at-stop detection.
    /// </param>
    /// <param name="elapsedSec">Time since last tick in seconds.</param>
    /// <returns>Position to move toward, or null if ride is complete.</returns>
    public Position? Update(
        Position currentPosition,
        ulong currentTransportGuid,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects,
        float elapsedSec,
        uint? currentMapId = null,
        bool? currentIsOnTransport = null)
    {
        _phaseElapsed += elapsedSec;
        MissedBoardingAttempt = false;
        var isOnTransport = currentIsOnTransport ?? currentTransportGuid != 0;

        return CurrentPhase switch
        {
            TransportPhase.Approaching => HandleApproaching(currentPosition),
            TransportPhase.WaitingForArrival => HandleWaitingForArrival(currentPosition, isOnTransport, nearbyObjects, currentMapId, elapsedSec),
            TransportPhase.Boarding => HandleBoarding(currentPosition, isOnTransport, nearbyObjects),
            TransportPhase.Riding => HandleRiding(currentPosition, isOnTransport, nearbyObjects, currentMapId),
            TransportPhase.Disembarking => HandleDisembarking(currentPosition, isOnTransport),
            TransportPhase.Complete => null,
            _ => null,
        };
    }

    /// <summary>
    /// Check if a transport (elevator) is at a specific stop.
    /// For elevators: compare nearby GO Z to stop Z within tolerance.
    /// For boats/zeppelins: check if any nearby GO with matching display ID is within range.
    /// </summary>
    public bool IsTransportAtStop(
        IReadOnlyList<DynamicObjectProto>? nearbyObjects,
        TransportStop stop)
        => TryFindTransportAtStop(nearbyObjects, stop, out _);

    private bool TryFindTransportAtStop(
        IReadOnlyList<DynamicObjectProto>? nearbyObjects,
        TransportStop stop,
        out DynamicObjectProto transportObject)
    {
        transportObject = new DynamicObjectProto();

        if (nearbyObjects == null || nearbyObjects.Count == 0)
            return false;

        foreach (var obj in nearbyObjects)
        {
            if (_transport.Type == TransportType.Elevator)
            {
                var matchesElevatorCar = IsExpectedTransportObject(obj);
                var matchesStopMarker = obj.DisplayId == ELEVATOR_STOP_MARKER_DISPLAY_ID;
                if (!matchesElevatorCar && !matchesStopMarker)
                    continue;

                // Elevator: check if the car or stop marker is near the stop's Z.
                float zDiff = MathF.Abs(obj.Z - stop.WaitPosition.Z);
                if (zDiff > ELEVATOR_AT_STOP_Z_TOLERANCE)
                    continue;

                if (matchesElevatorCar)
                {
                    transportObject = obj;
                    return true;
                }

                float dx = obj.X - stop.WaitPosition.X;
                float dy = obj.Y - stop.WaitPosition.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist <= ELEVATOR_STOP_MARKER_DISTANCE)
                {
                    transportObject = obj;
                    return true;
                }
            }
            else
            {
                if (!IsExpectedTransportObject(obj))
                    continue;

                // Boat/zeppelin: check horizontal distance to the stop
                float dx = obj.X - stop.WaitPosition.X;
                float dy = obj.Y - stop.WaitPosition.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                var atStopDistance = _transport.Type == TransportType.Zeppelin
                    ? ZEPPELIN_AT_STOP_DISTANCE
                    : BOAT_AT_STOP_DISTANCE;
                if (dist <= atStopDistance)
                {
                    transportObject = obj;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsExpectedTransportObject(DynamicObjectProto obj)
        => TransportObjectIdentity.MatchesTransport(obj, _transport);

    private float GetMaxWaitTimeSec()
        => _transport.Type == TransportType.Elevator
            ? ELEVATOR_MAX_WAIT_TIME_SEC
            : SCHEDULED_TRANSPORT_MAX_WAIT_TIME_SEC;

    // =====================================================================
    // PHASE HANDLERS
    // =====================================================================

    private Position? HandleApproaching(Position currentPosition)
    {
        var navigationPosition = _boardingStop.NavigationPosition;
        float dist = DistanceXY(currentPosition, navigationPosition);
        if (dist <= _boardingStop.BoardingRadius)
        {
            TransitionTo(TransportPhase.WaitingForArrival);
            return navigationPosition;
        }

        if (IsAtConfiguredBoardingPosition(currentPosition))
        {
            TransitionTo(TransportPhase.WaitingForArrival);
            return _boardingStop.BoardingPosition;
        }

        return navigationPosition;
    }

    private Position? HandleWaitingForArrival(
        Position currentPosition,
        bool isOnTransport,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects,
        uint? currentMapId,
        float elapsedSec)
    {
        // Timeout — transport never came
        if (isOnTransport)
        {
            ResetDockStability();
            if (TryGetScheduledTransportBoardingOffset(out var boardingOffset)
                && !IsAtTransportLocalOffset(currentPosition, boardingOffset))
            {
                _boardingWaypoint = boardingOffset;
                TransitionTo(TransportPhase.Boarding);
                return boardingOffset;
            }

            TransitionTo(TransportPhase.Riding);
            return null;
        }

        if (IsCrossMapScheduledTransport() && IsAtDestinationStop(currentPosition, currentMapId))
        {
            ResetDockStability();
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        if (_phaseElapsed > GetMaxWaitTimeSec())
        {
            ResetDockStability();
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        if (TryFindTransportAtStop(nearbyObjects, _boardingStop, out var transportObject))
        {
            if (!IsTransportReadyToBoard(transportObject, elapsedSec))
            {
                return IsNativeOffMeshBoardingEnabled()
                    ? _boardingStop.NavigationPosition
                    : _boardingStop.BoardingPosition ?? _boardingStop.NavigationPosition;
            }

            _boardingWaypoint = CreateBoardingWaypoint(transportObject, _boardingStop);
            ResetDockStability();
            TransitionTo(TransportPhase.Boarding);
            return _boardingWaypoint;
        }

        ResetDockStability();

        if (ShouldUseConfiguredBoardingWaypoint())
            return _boardingStop.BoardingPosition;

        // Stay at the waiting position
        return _boardingStop.NavigationPosition;
    }

    private Position? HandleBoarding(
        Position currentPosition,
        bool isOnTransport,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects)
    {
        // Timeout — couldn't board
        if (!isOnTransport && IsScheduledTransportBoardingLost(currentPosition))
        {
            _boardingWaypoint = null;
            MissedBoardingAttempt = true;
            TransitionTo(TransportPhase.WaitingForArrival);
            return _boardingStop.NavigationPosition;
        }

        if (!isOnTransport && ShouldAbortBoarding(TryFindTransportAtStop(nearbyObjects, _boardingStop, out _)))
        {
            // Transport left without us — go back to waiting
            _boardingWaypoint = null;
            MissedBoardingAttempt = true;
            TransitionTo(TransportPhase.WaitingForArrival);
            return _boardingStop.NavigationPosition;
        }

        // We're on the transport — start riding
        if (isOnTransport)
        {
            if (TryGetScheduledTransportBoardingOffset(out var boardingOffset)
                && !IsAtTransportLocalOffset(currentPosition, boardingOffset))
            {
                _boardingWaypoint = boardingOffset;
                return boardingOffset;
            }

            _boardingWaypoint = null;
            _disembarkWaypoint = null;
            TransitionTo(TransportPhase.Riding);
            return null; // Don't move while riding
        }

        if (TryFindTransportAtStop(nearbyObjects, _boardingStop, out var transportObject))
            _boardingWaypoint = CreateBoardingWaypoint(transportObject, _boardingStop);

        // Move toward the transport object while preserving the known stop height.
        return _boardingWaypoint ?? _boardingStop.NavigationPosition;
    }

    private Position? HandleRiding(
        Position currentPosition,
        bool isOnTransport,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects,
        uint? currentMapId)
    {
        // During cross-continent scheduled transports, the client can briefly
        // report no transport GUID while the world transfer settles. Keep the
        // ride alive unless we are actually at the destination stop.
        if (!isOnTransport)
        {
            if (IsCrossMapScheduledTransport() && !IsAtDestinationStop(currentPosition, currentMapId))
                return null;

            TransitionTo(TransportPhase.Complete);
            return null;
        }

        // Check if we've arrived at the destination
        if (IsTransportAtStop(nearbyObjects, _destinationStop))
        {
            if (TryFindTransportAtStop(nearbyObjects, _destinationStop, out var transportObject))
                _disembarkWaypoint = CreateDisembarkWaypoint(transportObject, _destinationStop);

            TransitionTo(TransportPhase.Disembarking);
            return _disembarkWaypoint ?? _destinationStop.WaitPosition;
        }

        // Stay still while riding
        return null;
    }

    private Position? HandleDisembarking(Position currentPosition, bool isOnTransport)
    {
        // Timeout — force complete
        if (_phaseElapsed > DISEMBARK_TIMEOUT_SEC)
        {
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        // We're off the transport
        if (!isOnTransport)
        {
            _disembarkWaypoint = null;
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        // Move toward the destination stop's waiting position to step off
        return _disembarkWaypoint ?? _destinationStop.WaitPosition;
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private void TransitionTo(TransportPhase newPhase)
    {
        CurrentPhase = newPhase;
        _phaseElapsed = 0f;
    }

    private bool IsCrossMapScheduledTransport()
        => _transport.Type != TransportType.Elevator
            && _boardingStop.MapId != _destinationStop.MapId;

    private bool ShouldUseConfiguredBoardingWaypoint()
        => _transport.Type != TransportType.Elevator
            && _boardingStop.BoardingPosition != null
            && !IsNativeOffMeshBoardingEnabled();

    private bool IsTransportReadyToBoard(DynamicObjectProto transportObject, float elapsedSec)
    {
        if (_transport.Type == TransportType.Elevator)
            return true;

        var current = new Position(transportObject.X, transportObject.Y, transportObject.Z);
        if (!IsTrackedDockTransport(transportObject))
        {
            TrackDockTransport(transportObject, current);
            return false;
        }

        if (_lastDockTransportPosition is { } previous)
        {
            var movement = DistanceXY(previous, current);
            if (movement <= SCHEDULED_TRANSPORT_DOCK_STABLE_EPSILON)
            {
                _dockStableElapsed += MathF.Max(0f, elapsedSec);
            }
            else
            {
                _dockStableElapsed = 0f;
            }
        }

        _lastDockTransportPosition = current;
        return _dockStableElapsed >= SCHEDULED_TRANSPORT_DOCK_STABLE_TIME_SEC;
    }

    private bool IsTrackedDockTransport(DynamicObjectProto transportObject)
    {
        if (_trackedDockTransportGuid != 0 || transportObject.Guid != 0)
            return transportObject.Guid != 0 && transportObject.Guid == _trackedDockTransportGuid;

        return TransportObjectIdentity.ResolveEntry(transportObject) == _trackedDockTransportEntry
            && transportObject.DisplayId == _trackedDockTransportDisplayId;
    }

    private void TrackDockTransport(DynamicObjectProto transportObject, Position current)
    {
        _trackedDockTransportGuid = transportObject.Guid;
        _trackedDockTransportEntry = TransportObjectIdentity.ResolveEntry(transportObject);
        _trackedDockTransportDisplayId = transportObject.DisplayId;
        _lastDockTransportPosition = current;
        _dockStableElapsed = 0f;
    }

    private void ResetDockStability()
    {
        _trackedDockTransportGuid = 0;
        _trackedDockTransportEntry = 0;
        _trackedDockTransportDisplayId = 0;
        _lastDockTransportPosition = null;
        _dockStableElapsed = 0f;
    }

    private bool IsAtConfiguredBoardingPosition(Position currentPosition)
        => _transport.Type != TransportType.Elevator
            && _boardingStop.BoardingPosition != null
            && !IsNativeOffMeshBoardingEnabled()
            && DistanceXY(currentPosition, _boardingStop.BoardingPosition) <= _boardingStop.BoardingRadius;

    private bool IsScheduledTransportBoardingLost(Position currentPosition)
        => _transport.Type != TransportType.Elevator
            && (currentPosition.Z < _boardingStop.NavigationPosition.Z - SCHEDULED_TRANSPORT_BOARDING_LOST_Z_DROP
                || DistanceXY(currentPosition, _boardingStop.NavigationPosition) > SCHEDULED_TRANSPORT_BOARDING_LOST_DISTANCE);

    private bool TryGetScheduledTransportBoardingOffset(out Position boardingOffset)
    {
        if (_transport.Type != TransportType.Elevator
            && _boardingStop.TransportBoardingOffset is { } transportBoardingOffset)
        {
            boardingOffset = transportBoardingOffset;
            return true;
        }

        boardingOffset = default;
        return false;
    }

    private static bool IsAtTransportLocalOffset(Position currentPosition, Position localOffset)
        => DistanceXY(currentPosition, localOffset) <= SCHEDULED_TRANSPORT_LOCAL_BOARDING_OFFSET_XY_TOLERANCE
            && MathF.Abs(currentPosition.Z - localOffset.Z) <= SCHEDULED_TRANSPORT_LOCAL_BOARDING_OFFSET_Z_TOLERANCE;

    private bool ShouldAbortBoarding(bool transportAtStop)
    {
        if (_transport.Type == TransportType.Elevator)
            return _phaseElapsed > BOARDING_TIMEOUT_SEC;

        if (!transportAtStop && _phaseElapsed > BOARDING_TIMEOUT_SEC)
            return true;

        return _phaseElapsed > SCHEDULED_TRANSPORT_BOARDING_TIMEOUT_SEC;
    }

    private bool IsAtDestinationStop(Position currentPosition, uint? currentMapId)
    {
        if (currentMapId.HasValue && currentMapId.Value != _destinationStop.MapId)
            return false;

        var verticalDelta = MathF.Abs(currentPosition.Z - _destinationStop.WaitPosition.Z);
        return DistanceXY(currentPosition, _destinationStop.WaitPosition) <= _destinationStop.BoardingRadius
            && verticalDelta <= SCHEDULED_TRANSPORT_EXIT_Z_TOLERANCE;
    }

    private static float DistanceXY(Position a, Position b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private Position CreateBoardingWaypoint(DynamicObjectProto transportObject, TransportStop stop)
    {
        if (_transport.Type == TransportType.Elevator)
            return stop.WaitPosition;

        var anchor = stop.BoardingPosition ?? stop.WaitPosition;
        if (stop.TransportBoardingOffset != null)
        {
            // The local deck offset only becomes meaningful after the client has
            // a transport GUID. Before attachment, hold the fixed gangplank point
            // so the foreground client can acquire transport state naturally.
            return anchor;
        }

        var dx = transportObject.X - anchor.X;
        var dy = transportObject.Y - anchor.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0.01f)
            return anchor;

        var step = MathF.Min(distance, SCHEDULED_TRANSPORT_BOARDING_APPROACH_DISTANCE);
        return new Position(
            anchor.X + (dx / distance * step),
            anchor.Y + (dy / distance * step),
            anchor.Z);
    }

    private Position CreateDisembarkWaypoint(DynamicObjectProto transportObject, TransportStop stop)
    {
        if (_transport.Type == TransportType.Elevator)
            return stop.WaitPosition;

        return WorldToTransportLocal(stop.WaitPosition, transportObject);
    }

    private static Position WorldToTransportLocal(Position worldPosition, DynamicObjectProto transportObject)
    {
        var orientation = transportObject.Orientation;
        var cos = MathF.Cos(orientation);
        var sin = MathF.Sin(orientation);
        var dx = worldPosition.X - transportObject.X;
        var dy = worldPosition.Y - transportObject.Y;

        return new Position(
            dx * cos + dy * sin,
            dy * cos - dx * sin,
            worldPosition.Z - transportObject.Z);
    }

    private static Position TransportLocalToWorld(Position localPosition, DynamicObjectProto transportObject)
    {
        var orientation = transportObject.Orientation;
        var cos = MathF.Cos(orientation);
        var sin = MathF.Sin(orientation);

        return new Position(
            transportObject.X + (localPosition.X * cos) - (localPosition.Y * sin),
            transportObject.Y + (localPosition.X * sin) + (localPosition.Y * cos),
            transportObject.Z + localPosition.Z);
    }
}
