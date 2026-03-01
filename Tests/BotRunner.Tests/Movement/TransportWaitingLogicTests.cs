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
    public void WaitingForArrival_Timeout_TransitionsToComplete()
    {
        var logic = MakeWaitingLogic();
        var noObjects = new List<DynamicObjectProto>();

        // Simulate 121 seconds of waiting
        logic.Update(UpperStop.WaitPosition, 0, noObjects, 121f);

        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
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
}
