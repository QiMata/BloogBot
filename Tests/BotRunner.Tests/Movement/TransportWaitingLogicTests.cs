using BotRunner.Movement;
using GameData.Core.Models;
using Pathfinding;
using System;
using System.Collections.Generic;
using static BotRunner.Movement.TransportData;

namespace BotRunner.Tests.Movement;

public class TransportWaitingLogicTests
{
    private const float DT = 1f / 60f;

    // Test elevator: simple 2-stop definition
    private static readonly TransportDefinition TestElevator = new(
        GameObjectEntry: 99999,
        DisplayId: 455,
        Name: "Test Elevator",
        Type: TransportType.Elevator,
        Stops:
        [
            new("Upper", 0, new Position(100, 100, 50), BoardingRadius: 6f),
            new("Lower", 0, new Position(100, 100, -50), BoardingRadius: 6f),
        ],
        VerticalRange: 100f);

    private static readonly TransportStop UpperStop = TestElevator.Stops[0];
    private static readonly TransportStop LowerStop = TestElevator.Stops[1];

    // =====================================================================
    // PHASE TRANSITIONS
    // =====================================================================

    [Fact]
    public void InitialPhase_IsApproaching()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        Assert.Equal(TransportPhase.Approaching, logic.CurrentPhase);
    }

    [Fact]
    public void Approaching_FarFromStop_ReturnsWaitPosition()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        var farPosition = new Position(200, 200, 50);

        var target = logic.Update(farPosition, 0, null, DT);

        Assert.Equal(TransportPhase.Approaching, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.Equal(UpperStop.WaitPosition.X, target.X);
        Assert.Equal(UpperStop.WaitPosition.Y, target.Y);
    }

    [Fact]
    public void Approaching_NearStop_TransitionsToWaitingForArrival()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        var nearPosition = new Position(101, 101, 50); // Within 6y boarding radius

        logic.Update(nearPosition, 0, null, DT);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
    }

    [Fact]
    public void WaitingForArrival_TransportNotPresent_StaysWaiting()
    {
        var logic = MakeWaitingLogic();
        var noObjects = new List<DynamicObjectProto>();

        var target = logic.Update(UpperStop.WaitPosition, 0, noObjects, DT);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(target);
    }

    [Fact]
    public void WaitingForArrival_ElevatorArrives_TransitionsToBoarding()
    {
        var logic = MakeWaitingLogic();

        // Elevator car arrives at the upper stop (Z near 50)
        var objects = MakeElevatorAtZ(51f);

        logic.Update(UpperStop.WaitPosition, 0, objects, DT);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
    }

    [Fact]
    public void WaitingForArrival_ElevatorTimeout_TransitionsToComplete()
    {
        var logic = MakeWaitingLogic();
        var noObjects = new List<DynamicObjectProto>();

        // Simulate 121 seconds of waiting
        logic.Update(UpperStop.WaitPosition, 0, noObjects, 121f);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinWithoutOverlay_PrepositionsAtBoardingWaypointAfterDwell()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = logic.Update(boardingStop.WaitPosition, 0, null, 121f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 1);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 1);
        Assert.Equal(boardingStop.WaitPosition.Z, target.Z, 1);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinWithoutOverlay_PrepositionsAtBoardingWaypointBeforeArrival()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = logic.Update(boardingStop.WaitPosition, 0, null, 5f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 1);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 1);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 1);
    }

    [Fact]
    public void Approaching_ZeppelinAtConfiguredBoardingPosition_TransitionsToWaitingOnTheDeck()
    {
        // Phase 5.3.5: with OG.ApproachPosition anchored to Zeppelin Master Frezza
        // (z=53.6, same deck tier as BoardingPosition z=53.89), the bot teleported
        // to BoardingPosition is within BoardingRadius=12f of NavigationPosition
        // (~11.6y XY apart, identical Z tier). The early `dist <= BoardingRadius`
        // branch fires and returns NavigationPosition. Pre-Phase-5.3.5 (when
        // ApproachPosition was at the wrong-tier z=51.6 city ground), the bot at
        // BoardingPosition was OUTSIDE BoardingRadius of NavigationPosition and the
        // `IsAtConfiguredBoardingPosition` fallback returned BoardingPosition.
        // Both branches transition to WaitingForArrival; only the returned waypoint
        // differs. The new geometry consolidates these cases since both points are
        // on the same deck within attachment distance of the docked zeppelin.
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var boardingPosition = boardingStop.BoardingPosition!;
        var navigationPosition = boardingStop.NavigationPosition;
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        var firstTarget = logic.Update(boardingPosition, 0, null, DT);
        var secondTarget = logic.Update(boardingPosition, 0, null, 5f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(firstTarget);
        Assert.NotNull(secondTarget);
        // Returned waypoint must be on the deck — either the NPC-anchored
        // NavigationPosition (Frezza) or BoardingPosition (gangplank attach point).
        // Both are valid wait points on the same elevation.
        Assert.True(
            (Math.Abs(firstTarget!.X - navigationPosition.X) < 0.5f && Math.Abs(firstTarget.Y - navigationPosition.Y) < 0.5f)
            || (Math.Abs(firstTarget.X - boardingPosition.X) < 0.5f && Math.Abs(firstTarget.Y - boardingPosition.Y) < 0.5f),
            $"Expected firstTarget on deck near Frezza({navigationPosition.X:F2},{navigationPosition.Y:F2}) "
            + $"or BoardingPosition({boardingPosition.X:F2},{boardingPosition.Y:F2}); "
            + $"got ({firstTarget.X:F2},{firstTarget.Y:F2},{firstTarget.Z:F2}).");
        Assert.Equal(navigationPosition.Z, firstTarget.Z, 1);
        Assert.True(
            (Math.Abs(secondTarget!.X - navigationPosition.X) < 0.5f && Math.Abs(secondTarget.Y - navigationPosition.Y) < 0.5f)
            || (Math.Abs(secondTarget.X - boardingPosition.X) < 0.5f && Math.Abs(secondTarget.Y - boardingPosition.Y) < 0.5f),
            $"Expected secondTarget on deck.");
    }

    [Fact]
    public void WaitingForArrival_OnTransport_TransitionsToRiding()
    {
        var logic = MakeWaitingLogic();

        var target = logic.Update(UpperStop.WaitPosition, currentTransportGuid: 0xABCUL, nearbyObjects: null, elapsedSec: DT);

        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        Assert.Null(target);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinOnTransportBeforeDeckOffset_ReturnsLocalBoardingOffset()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);
        var sideOffset = new Position(6.7f, 0.1f, -18.6f);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = logic.Update(
            sideOffset,
            currentTransportGuid: 0xABCUL,
            nearbyObjects: null,
            elapsedSec: DT,
            currentMapId: boardingStop.MapId,
            currentIsOnTransport: true);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.TransportBoardingOffset);
        Assert.Equal(boardingStop.TransportBoardingOffset!.X, target!.X, 3);
        Assert.Equal(boardingStop.TransportBoardingOffset.Y, target.Y, 3);
        Assert.Equal(boardingStop.TransportBoardingOffset.Z, target.Z, 3);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinAtStopButMoving_StaysWaitingAtBoardingPosition()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        logic.Update(
            boardingStop.WaitPosition,
            0,
            MakeZeppelinAtDock(0, ZeppelinUndercityOrgrimmar.GameObjectEntry, 1318.1f, -4658.0f, 71.9f),
            DT);
        var target = logic.Update(
            boardingStop.WaitPosition,
            0,
            MakeZeppelinAtDock(0, ZeppelinUndercityOrgrimmar.GameObjectEntry, 1338.0f, -4639.0f, 71.9f),
            1f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 1);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 1);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 1);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinAtStopStillCreeping_StaysWaitingAtBoardingPosition()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        logic.Update(
            boardingStop.WaitPosition,
            0,
            MakeZeppelinAtDock(0, ZeppelinUndercityOrgrimmar.GameObjectEntry, 1318.1f, -4658.0f, 71.9f),
            DT);
        var target = logic.Update(
            boardingStop.WaitPosition,
            0,
            MakeZeppelinAtDock(0, ZeppelinUndercityOrgrimmar.GameObjectEntry, 1318.2f, -4658.0f, 71.9f),
            5f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 1);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 1);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 1);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinAtStopStable_TransitionsToBoarding()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 3);
    }

    [Fact]
    public void WaitingForArrival_ZeppelinAtStopStable_HoldsConfiguredBoardingPositionBeforeTransportAttachment()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);
        const float orientation = -MathF.PI / 2f;

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = AdvanceStableZeppelinAtDock(
            logic,
            boardingStop.WaitPosition,
            MakeZeppelinAtDock(
                0,
                ZeppelinUndercityOrgrimmar.GameObjectEntry,
                1318.1f,
                -4658.0f,
                71.9f,
                orientation));

        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 3);
    }

    [Fact]
    public void WaitingForArrival_CrossMapDestinationReached_TransitionsToComplete()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = logic.Update(destinationStop.WaitPosition, 0, nearbyObjects: null, elapsedSec: DT, currentMapId: destinationStop.MapId);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
        Assert.Null(target);
    }

    [Fact]
    public void Boarding_OnTransport_TransitionsToRiding()
    {
        var logic = MakeBoardingLogic();

        // Player is now on the transport (transportGuid != 0)
        logic.Update(UpperStop.WaitPosition, currentTransportGuid: 12345, null, DT);

        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
    }

    [Fact]
    public void Boarding_Timeout_TransitionsBackToWaiting()
    {
        var logic = MakeBoardingLogic();

        // 11 seconds of failing to board
        logic.Update(UpperStop.WaitPosition, 0, null, 11f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
    }

    [Fact]
    public void Boarding_OnTransportAfterDefaultTimeout_TransitionsToRiding()
    {
        var logic = MakeBoardingLogic();

        logic.Update(UpperStop.WaitPosition, currentTransportGuid: 12345, null, 11f);

        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
    }

    [Fact]
    public void Boarding_ZeppelinOnTransportBeforeDeckOffset_StaysBoarding()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);
        var sideOffset = new Position(6.7f, 0.1f, -18.6f);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        var target = logic.Update(
            sideOffset,
            currentTransportGuid: 0xABCUL,
            nearbyObjects: MakeOrgrimmarUndercityZeppelinAtDock(),
            elapsedSec: DT,
            currentMapId: boardingStop.MapId,
            currentIsOnTransport: true);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.TransportBoardingOffset);
        Assert.Equal(boardingStop.TransportBoardingOffset!.X, target!.X, 3);
        Assert.Equal(boardingStop.TransportBoardingOffset.Y, target.Y, 3);
        Assert.Equal(boardingStop.TransportBoardingOffset.Z, target.Z, 3);
    }

    [Fact]
    public void Boarding_ZeppelinOnTransportAtDeckOffset_TransitionsToRiding()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        var target = logic.Update(
            boardingStop.TransportBoardingOffset!,
            currentTransportGuid: 0xABCUL,
            nearbyObjects: MakeOrgrimmarUndercityZeppelinAtDock(),
            elapsedSec: DT,
            currentMapId: boardingStop.MapId,
            currentIsOnTransport: true);

        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        Assert.Null(target);
    }

    [Fact]
    public void Boarding_ZeppelinAtStop_WaitsPastElevatorBoardingTimeout()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        var target = logic.Update(boardingStop.WaitPosition, 0, MakeOrgrimmarUndercityZeppelinAtDock(), 11f);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 3);
    }

    [Fact]
    public void Boarding_ZeppelinGoneAfterDefaultTimeout_TransitionsBackToWaiting()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        var target = logic.Update(boardingStop.WaitPosition, 0, nearbyObjects: null, elapsedSec: 11f);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.True(logic.MissedBoardingAttempt);
        Assert.NotNull(target);
        Assert.Equal(boardingStop.NavigationPosition.X, target!.X, 1);
        Assert.Equal(boardingStop.NavigationPosition.Y, target.Y, 1);
    }

    [Fact]
    public void Boarding_ZeppelinObjectMovesWithinStop_HoldsConfiguredBoardingPositionBeforeTransportAttachment()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);
        var updatedTransportPosition = new Position(1338f, -4639f, 71.9f);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var initialTarget = AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        var movingObjectTarget = logic.Update(
            boardingStop.WaitPosition,
            0,
            MakeZeppelinAtDock(
                0,
                ZeppelinUndercityOrgrimmar.GameObjectEntry,
                updatedTransportPosition.X,
                updatedTransportPosition.Y,
                updatedTransportPosition.Z),
            DT);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        Assert.NotNull(initialTarget);
        Assert.NotNull(movingObjectTarget);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, initialTarget!.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, initialTarget.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, initialTarget.Z, 3);
        Assert.Equal(boardingStop.BoardingPosition.X, movingObjectTarget!.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, movingObjectTarget.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, movingObjectTarget.Z, 3);
    }

    [Fact]
    public void Boarding_ZeppelinFallsBelowDeck_FlagsMissedBoarding()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        var fallenPosition = new Position(
            boardingStop.WaitPosition.X,
            boardingStop.WaitPosition.Y,
            boardingStop.WaitPosition.Z - 20f);
        var target = logic.Update(fallenPosition, 0, MakeOrgrimmarUndercityZeppelinAtDock(), DT);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.True(logic.MissedBoardingAttempt);
        Assert.NotNull(target);
        Assert.Equal(boardingStop.NavigationPosition.X, target!.X, 1);
        Assert.Equal(boardingStop.NavigationPosition.Y, target.Y, 1);
    }

    [Fact]
    public void Riding_DestinationReached_TransitionsToDisembarking()
    {
        var logic = MakeRidingLogic();

        // Elevator at the lower stop (destination)
        var objects = MakeElevatorAtZ(-49f);

        logic.Update(LowerStop.WaitPosition, currentTransportGuid: 12345, objects, DT);

        Assert.Equal(TransportPhase.Disembarking, logic.CurrentPhase);
    }

    [Fact]
    public void Riding_StillInTransit_StaysRiding()
    {
        var logic = MakeRidingLogic();

        // Elevator midway (Z=0, neither at upper 50 nor lower -50)
        var objects = MakeElevatorAtZ(0f);

        var target = logic.Update(new Position(100, 100, 0), currentTransportGuid: 12345, objects, DT);

        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        Assert.Null(target); // Stay still while riding
    }

    [Fact]
    public void Riding_FellOff_TransitionsToComplete()
    {
        var logic = MakeRidingLogic();

        // Transport guid suddenly 0 — fell off
        logic.Update(new Position(100, 100, 10), currentTransportGuid: 0, null, DT);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    [Fact]
    public void Riding_CrossMapZeppelinGuidDropAwayFromDestination_StaysRiding()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = MakeZeppelinRidingLogic(boardingStop, destinationStop);

        var target = logic.Update(
            new Position(2995.2f, 1739.2f, -2.1f),
            currentTransportGuid: 0,
            nearbyObjects: null,
            elapsedSec: DT,
            currentMapId: destinationStop.MapId,
            currentIsOnTransport: false);

        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        Assert.Null(target);
    }

    [Fact]
    public void Riding_CrossMapZeppelinGuidDropAtDestination_Completes()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = MakeZeppelinRidingLogic(boardingStop, destinationStop);

        logic.Update(
            destinationStop.WaitPosition,
            currentTransportGuid: 0,
            nearbyObjects: null,
            elapsedSec: DT,
            currentMapId: destinationStop.MapId,
            currentIsOnTransport: false);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    [Fact]
    public void Riding_ZeppelinAtDestination_ReturnsTransportLocalDisembarkWaypoint()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = MakeZeppelinRidingLogic(boardingStop, destinationStop);
        var transportAtDestination = MakeZeppelinAtDock(
            0,
            ZeppelinUndercityOrgrimmar.GameObjectEntry,
            destinationStop.WaitPosition.X + 10f,
            destinationStop.WaitPosition.Y - 5f,
            destinationStop.WaitPosition.Z + 30f,
            orientation: 0f);

        var target = logic.Update(
            boardingStop.TransportBoardingOffset!,
            currentTransportGuid: 0xABCUL,
            nearbyObjects: transportAtDestination,
            elapsedSec: DT,
            currentMapId: destinationStop.MapId,
            currentIsOnTransport: true);

        Assert.Equal(TransportPhase.Disembarking, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.Equal(-10f, target!.X, 3);
        Assert.Equal(5f, target.Y, 3);
        Assert.Equal(-30f, target.Z, 3);
    }

    [Fact]
    public void Disembarking_OffTransport_TransitionsToComplete()
    {
        var logic = MakeDisembarkingLogic();

        // Player stepped off (transportGuid = 0)
        logic.Update(LowerStop.WaitPosition, 0, null, DT);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    [Fact]
    public void Disembarking_Timeout_ForcesComplete()
    {
        var logic = MakeDisembarkingLogic();

        // Still on transport after 11 seconds
        logic.Update(LowerStop.WaitPosition, currentTransportGuid: 12345, null, 11f);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    [Fact]
    public void Complete_ReturnsNull()
    {
        var logic = MakeCompletedLogic();

        var target = logic.Update(LowerStop.WaitPosition, 0, null, DT);

        Assert.Null(target);
        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    // =====================================================================
    // FULL CYCLE TEST
    // =====================================================================

    [Fact]
    public void FullCycle_Approaching_To_Complete()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);

        // 1. Approach
        Assert.Equal(TransportPhase.Approaching, logic.CurrentPhase);
        logic.Update(new Position(200, 200, 50), 0, null, DT);
        Assert.Equal(TransportPhase.Approaching, logic.CurrentPhase);

        // 2. Arrive at stop → WaitingForArrival
        logic.Update(new Position(101, 101, 50), 0, null, DT);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);

        // 3. Elevator arrives → Boarding
        logic.Update(UpperStop.WaitPosition, 0, MakeElevatorAtZ(50f), DT);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);

        // 4. Step on → Riding
        logic.Update(UpperStop.WaitPosition, 12345, null, DT);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);

        // 5. Arrive at destination → Disembarking
        logic.Update(LowerStop.WaitPosition, 12345, MakeElevatorAtZ(-50f), DT);
        Assert.Equal(TransportPhase.Disembarking, logic.CurrentPhase);

        // 6. Step off → Complete
        logic.Update(LowerStop.WaitPosition, 0, null, DT);
        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
    }

    // =====================================================================
    // IsTransportAtStop TESTS
    // =====================================================================

    [Fact]
    public void IsTransportAtStop_ElevatorNearStopZ_ReturnsTrue()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        var objects = MakeElevatorAtZ(51f); // Within 3y tolerance of 50

        Assert.True(logic.IsTransportAtStop(objects, UpperStop));
    }

    [Fact]
    public void IsTransportAtStop_ElevatorFarFromStopZ_ReturnsFalse()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        var objects = MakeElevatorAtZ(0f); // Midway, 50y from upper stop

        Assert.False(logic.IsTransportAtStop(objects, UpperStop));
    }

    [Fact]
    public void IsTransportAtStop_NullOrEmpty_ReturnsFalse()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);

        Assert.False(logic.IsTransportAtStop(null, UpperStop));
        Assert.False(logic.IsTransportAtStop(new List<DynamicObjectProto>(), UpperStop));
    }

    [Fact]
    public void IsTransportAtStop_WrongDisplayId_ReturnsFalse()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        var objects = new List<DynamicObjectProto>
        {
            new() { DisplayId = 9999, X = 100, Y = 100, Z = 50 }
        };

        Assert.False(logic.IsTransportAtStop(objects, UpperStop));
    }

    [Fact]
    public void IsTransportAtStop_ElevatorDoorMarkerNearStop_ReturnsTrue()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        var objects = new List<DynamicObjectProto>
        {
            new() { DisplayId = 462, X = 109f, Y = 100f, Z = 50f }
        };

        Assert.True(logic.IsTransportAtStop(objects, UpperStop));
    }

    [Fact]
    public void IsTransportAtStop_ZeppelinObjectOriginOffset_ReturnsTrue()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        Assert.True(logic.IsTransportAtStop(MakeOrgrimmarUndercityZeppelinAtDock(), boardingStop));
    }

    [Fact]
    public void IsTransportAtStop_ZeppelinGuidDerivedEntry_ReturnsTrue()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        var objects = MakeZeppelinAtDock(
            MovingTransportGuid(ZeppelinUndercityOrgrimmar.GameObjectEntry),
            reportedEntry: 0);

        Assert.True(logic.IsTransportAtStop(objects, boardingStop));
    }

    [Fact]
    public void IsTransportAtStop_ZeppelinWrongReportedEntryButCorrectGuid_ReturnsTrue()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        var objects = MakeZeppelinAtDock(
            MovingTransportGuid(ZeppelinUndercityOrgrimmar.GameObjectEntry),
            reportedEntry: ZeppelinOrgrimmarGromgol.GameObjectEntry);

        Assert.True(logic.IsTransportAtStop(objects, boardingStop));
    }

    [Fact]
    public void IsTransportAtStop_ZeppelinSameDisplayWrongEntry_ReturnsFalse()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        Assert.False(logic.IsTransportAtStop(MakeOrgrimmarGromgolZeppelinAtDock(), boardingStop));
    }

    [Fact]
    public void IsTransportAtStop_ZeppelinSameDisplayWrongGuidEntry_ReturnsFalse()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        var objects = MakeZeppelinAtDock(
            MovingTransportGuid(ZeppelinOrgrimmarGromgol.GameObjectEntry),
            reportedEntry: 0);

        Assert.False(logic.IsTransportAtStop(objects, boardingStop));
    }

    [Fact]
    public void WaitingForArrival_ZeppelinObjectOriginOffset_ReturnsConfiguredBoardingPositionBeforeAttachment()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);

        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.NotNull(boardingStop.BoardingPosition);
        Assert.Equal(boardingStop.BoardingPosition!.X, target!.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, target.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, target.Z, 3);
        Assert.True(
            boardingStop.TransportBoardingOffset!.Y <= -3.0f,
            "The zeppelin boarding offset should remain a post-attachment lower deck settle point.");
        Assert.True(
            DistanceXY(target, boardingStop.BoardingPosition!) <= 0.1f,
            "Active pre-attachment boarding should hold the configured gangplank point instead of steering into deck-local space.");
    }

    [Fact]
    public void WaitingForArrival_ZeppelinSameDisplayWrongEntry_StaysAtWaitPosition()
    {
        var boardingStop = ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = ZeppelinUndercityOrgrimmar.Stops[1];
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);

        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        var target = logic.Update(boardingStop.WaitPosition, 0, MakeOrgrimmarGromgolZeppelinAtDock(), DT);

        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(target);
        Assert.Equal(boardingStop.WaitPosition.X, target!.X, 1);
        Assert.Equal(boardingStop.WaitPosition.Y, target.Y, 1);
        Assert.Equal(boardingStop.WaitPosition.Z, target.Z, 1);
    }

    // =====================================================================
    // TransportData LOOKUP TESTS
    // =====================================================================

    [Fact]
    public void FindNearestTransport_NearUndercityElevator_FindsIt()
    {
        // Position near the west Undercity elevator upper stop
        var pos = new Position(1545, 241, 55);
        var result = TransportData.FindNearestTransport(0, pos, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Equal("Undercity Elevator (West)", result.Name);
    }

    [Fact]
    public void FindNearestTransport_FarFromAny_ReturnsNull()
    {
        var pos = new Position(0, 0, 0);
        var result = TransportData.FindNearestTransport(0, pos, maxDistance: 20f);

        Assert.Null(result);
    }

    [Fact]
    public void FindByEntry_KnownEntry_ReturnsTransport()
    {
        var result = TransportData.FindByEntry(20655);
        Assert.NotNull(result);
        Assert.Equal("Undercity Elevator (West)", result.Name);
    }

    [Fact]
    public void FindByEntry_OrgrimmarUndercityZeppelin_UsesLiveRouteEntry()
    {
        var result = TransportData.FindByEntry(164871);

        Assert.NotNull(result);
        Assert.Same(ZeppelinUndercityOrgrimmar, result);
        Assert.Equal("Zeppelin: Orgrimmar <-> Undercity", result.Name);
        AssertHasStopNear(result, 1, new Position(1320.142944f, -4653.158691f, 53.891945f), 2f);
        AssertHasStopNear(result, 0, new Position(2066.911377f, 290.113708f, 97.031593f), 2f);
        Assert.DoesNotContain(result.Stops, stop => IsStopNear(stop, 0, new Position(-12407.0f, 214.0f, 32.0f), 50f));
    }

    [Fact]
    public void FindByEntry_GromgolZeppelins_AreNotOrgrimmarUndercityRoute()
    {
        var gromgolUndercity = TransportData.FindByEntry(176495);
        var orgrimmarGromgol = TransportData.FindByEntry(175080);

        Assert.NotNull(gromgolUndercity);
        Assert.NotNull(orgrimmarGromgol);

        Assert.Same(ZeppelinUndercityGromgol, gromgolUndercity);
        Assert.Same(ZeppelinOrgrimmarGromgol, orgrimmarGromgol);
        Assert.DoesNotContain(gromgolUndercity.Stops, stop => IsStopNear(stop, 1, new Position(1320.0f, -4649.0f, 53.0f), 20f));
        AssertHasStopNear(orgrimmarGromgol, 1, new Position(1317.0f, -4652.0f, 53.0f), 2f);
        AssertHasStopNear(orgrimmarGromgol, 0, new Position(-12407.0f, 214.0f, 32.0f), 10f);
    }

    [Fact]
    public void FindByGuid_MovingTransportGuid_ReturnsOrgrimmarUndercityZeppelin()
    {
        var result = TransportData.FindByGuid(MovingTransportGuid(ZeppelinUndercityOrgrimmar.GameObjectEntry));

        Assert.Same(ZeppelinUndercityOrgrimmar, result);
    }

    [Fact]
    public void FindByGuid_StaticTransportGuid_ReturnsElevator()
    {
        var result = TransportData.FindByGuid(StaticTransportGuid(UndercityElevatorWest.GameObjectEntry));

        Assert.Same(UndercityElevatorWest, result);
    }

    [Fact]
    public void GetDestinationStop_ReturnsOtherStop()
    {
        var pos = new Position(1544, 241, 55); // Near upper stop
        var dest = TransportData.GetDestinationStop(UndercityElevatorWest, pos);

        Assert.NotNull(dest);
        Assert.Contains("Lower", dest.Name);
    }

    [Fact]
    public void DetectElevatorCrossing_LargeZDelta_NearElevator_DetectsIt()
    {
        var from = new Position(1544, 241, 55);   // Upper
        var to = new Position(1544, 241, -43);     // Lower
        var result = TransportData.DetectElevatorCrossing(0, from, to, minZDelta: 30f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
    }

    [Fact]
    public void DetectElevatorCrossing_SmallZDelta_ReturnsNull()
    {
        var from = new Position(1544, 241, 55);
        var to = new Position(1544, 241, 45);   // Only 10y drop
        var result = TransportData.DetectElevatorCrossing(0, from, to, minZDelta: 30f);

        Assert.Null(result);
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private static TransportWaitingLogic MakeWaitingLogic()
    {
        var logic = new TransportWaitingLogic(TestElevator, UpperStop, LowerStop);
        // Advance to WaitingForArrival
        logic.Update(new Position(101, 101, 50), 0, null, DT);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        return logic;
    }

    private static TransportWaitingLogic MakeBoardingLogic()
    {
        var logic = MakeWaitingLogic();
        logic.Update(UpperStop.WaitPosition, 0, MakeElevatorAtZ(50f), DT);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        return logic;
    }

    private static TransportWaitingLogic MakeRidingLogic()
    {
        var logic = MakeBoardingLogic();
        logic.Update(UpperStop.WaitPosition, 12345, null, DT);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        return logic;
    }

    private static TransportWaitingLogic MakeDisembarkingLogic()
    {
        var logic = MakeRidingLogic();
        logic.Update(LowerStop.WaitPosition, 12345, MakeElevatorAtZ(-50f), DT);
        Assert.Equal(TransportPhase.Disembarking, logic.CurrentPhase);
        return logic;
    }

    private static TransportWaitingLogic MakeCompletedLogic()
    {
        var logic = MakeDisembarkingLogic();
        logic.Update(LowerStop.WaitPosition, 0, null, DT);
        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
        return logic;
    }

    private static TransportWaitingLogic MakeZeppelinRidingLogic(
        TransportStop boardingStop,
        TransportStop destinationStop)
    {
        var logic = new TransportWaitingLogic(ZeppelinUndercityOrgrimmar, boardingStop, destinationStop);
        logic.Update(boardingStop.WaitPosition, 0, null, DT);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        AdvanceStableZeppelinAtDock(logic, boardingStop.WaitPosition);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        logic.Update(
            boardingStop.TransportBoardingOffset!,
            0xABCUL,
            MakeOrgrimmarUndercityZeppelinAtDock(),
            DT,
            boardingStop.MapId,
            currentIsOnTransport: true);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        return logic;
    }

    private static Position? AdvanceStableZeppelinAtDock(
        TransportWaitingLogic logic,
        Position currentPosition,
        IReadOnlyList<DynamicObjectProto>? nearbyObjects = null)
    {
        var objects = nearbyObjects ?? MakeOrgrimmarUndercityZeppelinAtDock();
        var waitingTarget = logic.Update(currentPosition, 0, objects, DT);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        Assert.NotNull(waitingTarget);

        var boardingTarget = logic.Update(currentPosition, 0, objects, 5f);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        return boardingTarget;
    }

    private static void AssertHasStopNear(TransportDefinition transport, uint mapId, Position expected, float maxDistance)
    {
        Assert.Contains(transport.Stops, stop => IsStopNear(stop, mapId, expected, maxDistance));
    }

    private static bool IsStopNear(TransportStop stop, uint mapId, Position expected, float maxDistance)
    {
        if (stop.MapId != mapId)
            return false;

        float dx = stop.WaitPosition.X - expected.X;
        float dy = stop.WaitPosition.Y - expected.Y;
        float dz = stop.WaitPosition.Z - expected.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) <= maxDistance;
    }

    private static float DistanceXY(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static List<DynamicObjectProto> MakeElevatorAtZ(float z)
    {
        return
        [
            new DynamicObjectProto
            {
                DisplayId = 455,
                X = 100,
                Y = 100,
                Z = z,
            }
        ];
    }

    private static List<DynamicObjectProto> MakeOrgrimmarUndercityZeppelinAtDock()
        => MakeZeppelinAtDock(
            guid: 0,
            reportedEntry: ZeppelinUndercityOrgrimmar.GameObjectEntry);

    private static List<DynamicObjectProto> MakeOrgrimmarGromgolZeppelinAtDock()
        => MakeZeppelinAtDock(
            guid: 0,
            reportedEntry: ZeppelinOrgrimmarGromgol.GameObjectEntry);

    private static List<DynamicObjectProto> MakeZeppelinAtDock(
        ulong guid,
        uint reportedEntry,
        float x = 1318.1f,
        float y = -4658.0f,
        float z = 71.9f,
        float orientation = 0f)
    {
        return
        [
            new DynamicObjectProto
            {
                Guid = guid,
                Entry = reportedEntry,
                DisplayId = ZeppelinUndercityOrgrimmar.DisplayId,
                X = x,
                Y = y,
                Z = z,
                Orientation = orientation,
                Scale = 1f
            }
        ];
    }

    private static ulong MovingTransportGuid(uint entry)
        => 0x1FC0000000000000UL | entry;

    private static ulong StaticTransportGuid(uint entry, ulong low = 1)
        => 0xF120000000000000UL | ((ulong)entry << 24) | (low & 0x00FFFFFFUL);
}
