using BotRunner.Movement;
using GameData.Core.Models;
using Pathfinding;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using static BotRunner.Movement.TransportData;

namespace Navigation.Physics.Tests;

/// <summary>
/// Elevator scenario tests validating:
/// - TransportData correctly identifies Undercity elevators from position data
/// - TransportWaitingLogic handles full elevator ride cycles
/// - NavigationPath transport integration detects elevator crossings
/// - DetectElevatorCrossing identifies large Z-delta paths near elevator shafts
/// </summary>
public class ElevatorScenarioTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // Undercity elevator coordinates from recording data
    private static readonly Position UCUpperWest = new(1544.24f, 240.77f, 55.40f);
    private static readonly Position UCLowerWest = new(1544.24f, 240.77f, -43.0f);
    private static readonly Position UCUpperNorth = new(1596.15f, 291.80f, 55.40f);
    private static readonly Position UCLowerNorth = new(1596.15f, 291.80f, -43.0f);

    // =====================================================================
    // TRANSPORT DATA — ELEVATOR DETECTION
    // =====================================================================

    [Fact]
    public void TransportData_FindsUndercityElevator_NearUpperWestShaft()
    {
        var result = TransportData.FindNearestTransport(0, UCUpperWest, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
        Assert.Equal(20655u, result.GameObjectEntry);
        _output.WriteLine($"Found: {result.Name} (entry {result.GameObjectEntry})");
    }

    [Fact]
    public void TransportData_FindsUndercityElevator_NearLowerWestShaft()
    {
        var result = TransportData.FindNearestTransport(0, UCLowerWest, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
        _output.WriteLine($"Found: {result.Name} at lower level");
    }

    [Fact]
    public void TransportData_FindsNorthShaft_Separately()
    {
        var result = TransportData.FindNearestTransport(0, UCUpperNorth, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Contains("North", result.Name);
        _output.WriteLine($"Found: {result.Name}");
    }

    [Fact]
    public void TransportData_DoesNotFindElevator_InOrgrimmar()
    {
        var orgPos = new Position(1630f, -4373f, 31f);
        var result = TransportData.FindNearestTransport(1, orgPos, maxDistance: 20f);

        Assert.Null(result);
    }

    [Fact]
    public void TransportData_GetDestinationStop_FromUpper_ReturnsLower()
    {
        var dest = TransportData.GetDestinationStop(UndercityElevatorWest, UCUpperWest);

        Assert.NotNull(dest);
        Assert.Contains("Lower", dest.Name);
        Assert.True(dest.WaitPosition.Z < 0, "Lower stop should have negative Z");
        _output.WriteLine($"Destination: {dest.Name} at Z={dest.WaitPosition.Z:F1}");
    }

    [Fact]
    public void TransportData_GetDestinationStop_FromLower_ReturnsUpper()
    {
        var dest = TransportData.GetDestinationStop(UndercityElevatorWest, UCLowerWest);

        Assert.NotNull(dest);
        Assert.Contains("Upper", dest.Name);
        Assert.True(dest.WaitPosition.Z > 0, "Upper stop should have positive Z");
    }

    // =====================================================================
    // ELEVATOR CROSSING DETECTION
    // =====================================================================

    [Fact]
    public void DetectElevatorCrossing_UCUpperToLower_FindsElevator()
    {
        var result = TransportData.DetectElevatorCrossing(
            mapId: 0, from: UCUpperWest, to: UCLowerWest, minZDelta: 30f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
        _output.WriteLine($"Detected: {result.Name}, Z range={result.VerticalRange:F0}y");
    }

    [Fact]
    public void DetectElevatorCrossing_FlatPath_ReturnsNull()
    {
        var from = new Position(1544f, 241f, 55f);
        var to = new Position(1600f, 241f, 52f); // Only 3y drop

        var result = TransportData.DetectElevatorCrossing(0, from, to, minZDelta: 30f);
        Assert.Null(result);
    }

    [Fact]
    public void DetectElevatorCrossing_LargeZDeltaFarFromElevator_ReturnsNull()
    {
        // Large Z delta but nowhere near an elevator
        var from = new Position(0f, 0f, 100f);
        var to = new Position(0f, 0f, -50f);

        var result = TransportData.DetectElevatorCrossing(0, from, to, minZDelta: 30f);
        Assert.Null(result);
    }

    // =====================================================================
    // WAITING LOGIC — FULL ELEVATOR CYCLE
    // =====================================================================

    [Fact]
    public void WaitingLogic_FullElevatorRideDown_AllPhases()
    {
        var boardStop = UndercityElevatorWest.Stops[0]; // Upper
        var exitStop = UndercityElevatorWest.Stops[1];  // Lower
        var logic = new TransportWaitingLogic(UndercityElevatorWest, boardStop, exitStop);

        // Phase 1: Approaching (far from stop)
        var farPos = new Position(1560f, 260f, 55f);
        var target = logic.Update(farPos, 0, null, 0.5f);
        Assert.Equal(TransportPhase.Approaching, logic.CurrentPhase);
        Assert.NotNull(target);
        _output.WriteLine($"Phase 1 (Approaching): target=({target.X:F1}, {target.Y:F1})");

        // Phase 2: Arrive at stop → WaitingForArrival
        var atStop = new Position(1544f, 241f, 55f);
        target = logic.Update(atStop, 0, null, 0.5f);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        _output.WriteLine($"Phase 2 (WaitingForArrival)");

        // Phase 3: Elevator arrives at upper stop → Boarding
        var elevatorAtUpper = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = 55f }
        };
        target = logic.Update(atStop, 0, elevatorAtUpper, 0.5f);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        _output.WriteLine($"Phase 3 (Boarding): elevator at Z=55");

        // Phase 4: Step on → Riding
        target = logic.Update(atStop, currentTransportGuid: 42, null, 0.5f);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        _output.WriteLine($"Phase 4 (Riding)");

        // Phase 5: Riding — midway
        var elevatorMidway = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = 10f }
        };
        target = logic.Update(new Position(1544f, 241f, 10f), 42, elevatorMidway, 5f);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        Assert.Null(target); // Don't move while riding
        _output.WriteLine($"Phase 5 (Riding midway): Z≈10");

        // Phase 6: Arrive at lower stop → Disembarking
        var elevatorAtLower = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = -43f }
        };
        target = logic.Update(new Position(1544f, 241f, -43f), 42, elevatorAtLower, 5f);
        Assert.Equal(TransportPhase.Disembarking, logic.CurrentPhase);
        _output.WriteLine($"Phase 6 (Disembarking): at lower stop");

        // Phase 7: Step off → Complete
        target = logic.Update(new Position(1544f, 241f, -43f), 0, null, 0.5f);
        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
        _output.WriteLine($"Phase 7 (Complete): ride finished");
    }

    [Fact]
    public void WaitingLogic_ElevatorLeavesBeforeBoarding_ReturnsToWaiting()
    {
        var boardStop = UndercityElevatorWest.Stops[0];
        var exitStop = UndercityElevatorWest.Stops[1];
        var logic = new TransportWaitingLogic(UndercityElevatorWest, boardStop, exitStop);

        // Get to WaitingForArrival
        logic.Update(new Position(1544f, 241f, 55f), 0, null, 0.1f);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);

        // Elevator arrives → Boarding
        var elevatorAtUpper = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = 55f }
        };
        logic.Update(new Position(1544f, 241f, 55f), 0, elevatorAtUpper, 0.1f);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);

        // Boarding timeout (couldn't get on in time) → back to WaitingForArrival
        logic.Update(new Position(1544f, 241f, 55f), 0, null, 11f);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        _output.WriteLine("Elevator left, returned to WaitingForArrival");
    }

    // =====================================================================
    // NAVIGATION PATH — TRANSPORT INTEGRATION
    // =====================================================================

    [Fact]
    public void NavigationPath_CheckTransportNeeded_UCUpperToLower_ReturnsTrue()
    {
        var path = new NavigationPath(null);

        bool needed = path.CheckTransportNeeded(UCUpperWest, UCLowerWest, mapId: 0);

        Assert.True(needed);
        Assert.True(path.IsRidingTransport);
        Assert.NotNull(path.ActiveTransportRide);
        Assert.Equal(TransportPhase.Approaching, path.ActiveTransportRide.CurrentPhase);
        _output.WriteLine("NavigationPath detected elevator crossing");
    }

    [Fact]
    public void NavigationPath_CheckTransportNeeded_FlatPath_ReturnsFalse()
    {
        var path = new NavigationPath(null);
        var flat1 = new Position(1630f, -4373f, 31f);
        var flat2 = new Position(1650f, -4360f, 31f);

        bool needed = path.CheckTransportNeeded(flat1, flat2, mapId: 1);

        Assert.False(needed);
        Assert.False(path.IsRidingTransport);
    }

    [Fact]
    public void NavigationPath_CancelTransportRide_ClearsState()
    {
        var path = new NavigationPath(null);
        path.CheckTransportNeeded(UCUpperWest, UCLowerWest, mapId: 0);
        Assert.True(path.IsRidingTransport);

        path.CancelTransportRide();

        Assert.False(path.IsRidingTransport);
        Assert.Null(path.ActiveTransportRide);
    }

    [Fact]
    public void NavigationPath_GetTransportTarget_DelegatesCorrectly()
    {
        var path = new NavigationPath(null);
        path.CheckTransportNeeded(UCUpperWest, UCLowerWest, mapId: 0);

        // Should return a position to move toward (the boarding stop)
        var target = path.GetTransportTarget(
            new Position(1560f, 260f, 55f), 0, null, 1f / 60f);

        Assert.NotNull(target);
        _output.WriteLine($"Transport target: ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
    }
}
