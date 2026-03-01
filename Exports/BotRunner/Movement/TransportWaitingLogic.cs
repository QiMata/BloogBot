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
    private const float MAX_WAIT_TIME_SEC = 120f; // 2 minutes max wait
    private const float BOARDING_TIMEOUT_SEC = 10f; // 10s to board once transport arrives
    private const float DISEMBARK_TIMEOUT_SEC = 10f; // 10s to step off
    private const float ELEVATOR_AT_STOP_Z_TOLERANCE = 3.0f;
    private const float BOAT_AT_STOP_DISTANCE = 20f;

    private readonly TransportDefinition _transport;
    private readonly TransportStop _boardingStop;
    private readonly TransportStop _destinationStop;

    private float _phaseElapsed;

    public TransportPhase CurrentPhase { get; private set; } = TransportPhase.Approaching;

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
        float elapsedSec)
    {
        _phaseElapsed += elapsedSec;

        return CurrentPhase switch
        {
            TransportPhase.Approaching => HandleApproaching(currentPosition),
            TransportPhase.WaitingForArrival => HandleWaitingForArrival(currentPosition, nearbyObjects),
            TransportPhase.Boarding => HandleBoarding(currentPosition, currentTransportGuid),
            TransportPhase.Riding => HandleRiding(currentPosition, currentTransportGuid, nearbyObjects),
            TransportPhase.Disembarking => HandleDisembarking(currentPosition, currentTransportGuid),
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
    {
        if (nearbyObjects == null || nearbyObjects.Count == 0)
            return false;

        foreach (var obj in nearbyObjects)
        {
            if (obj.DisplayId != _transport.DisplayId) continue;

            if (_transport.Type == TransportType.Elevator)
            {
                // Elevator: check if the car's Z is near the stop's Z
                float zDiff = MathF.Abs(obj.Z - stop.WaitPosition.Z);
                if (zDiff <= ELEVATOR_AT_STOP_Z_TOLERANCE)
                    return true;
            }
            else
            {
                // Boat/zeppelin: check horizontal distance to the stop
                float dx = obj.X - stop.WaitPosition.X;
                float dy = obj.Y - stop.WaitPosition.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist <= BOAT_AT_STOP_DISTANCE)
                    return true;
            }
        }

        return false;
    }

    // =====================================================================
    // PHASE HANDLERS
    // =====================================================================

    private Position? HandleApproaching(Position currentPosition)
    {
        float dist = DistanceXY(currentPosition, _boardingStop.WaitPosition);
        if (dist <= _boardingStop.BoardingRadius)
        {
            TransitionTo(TransportPhase.WaitingForArrival);
            return _boardingStop.WaitPosition;
        }

        return _boardingStop.WaitPosition;
    }

    private Position? HandleWaitingForArrival(
        Position currentPosition,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects)
    {
        // Timeout — transport never came
        if (_phaseElapsed > MAX_WAIT_TIME_SEC)
        {
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        if (IsTransportAtStop(nearbyObjects, _boardingStop))
        {
            TransitionTo(TransportPhase.Boarding);
            return _boardingStop.WaitPosition;
        }

        // Stay at the waiting position
        return _boardingStop.WaitPosition;
    }

    private Position? HandleBoarding(Position currentPosition, ulong currentTransportGuid)
    {
        // Timeout — couldn't board
        if (_phaseElapsed > BOARDING_TIMEOUT_SEC)
        {
            // Transport left without us — go back to waiting
            TransitionTo(TransportPhase.WaitingForArrival);
            return _boardingStop.WaitPosition;
        }

        // We're on the transport — start riding
        if (currentTransportGuid != 0)
        {
            TransitionTo(TransportPhase.Riding);
            return null; // Don't move while riding
        }

        // Move toward the center of the boarding stop
        return _boardingStop.WaitPosition;
    }

    private Position? HandleRiding(
        Position currentPosition,
        ulong currentTransportGuid,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects)
    {
        // Fell off the transport somehow
        if (currentTransportGuid == 0)
        {
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        // Check if we've arrived at the destination
        if (IsTransportAtStop(nearbyObjects, _destinationStop))
        {
            TransitionTo(TransportPhase.Disembarking);
            return _destinationStop.WaitPosition;
        }

        // Stay still while riding
        return null;
    }

    private Position? HandleDisembarking(Position currentPosition, ulong currentTransportGuid)
    {
        // Timeout — force complete
        if (_phaseElapsed > DISEMBARK_TIMEOUT_SEC)
        {
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        // We're off the transport
        if (currentTransportGuid == 0)
        {
            TransitionTo(TransportPhase.Complete);
            return null;
        }

        // Move toward the destination stop's waiting position to step off
        return _destinationStop.WaitPosition;
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private void TransitionTo(TransportPhase newPhase)
    {
        CurrentPhase = newPhase;
        _phaseElapsed = 0f;
    }

    private static float DistanceXY(Position a, Position b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
