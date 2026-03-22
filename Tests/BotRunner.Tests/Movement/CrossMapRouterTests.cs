using BotRunner.Combat;
using BotRunner.Movement;
using GameData.Core.Models;
using System.Linq;

namespace BotRunner.Tests.Movement;

public class CrossMapRouterTests
{
    private readonly CrossMapRouter _router = new();

    // =====================================================================
    // SAME MAP — SIMPLE WALK
    // =====================================================================

    [Fact]
    public void SameMap_SameArea_SingleWalkLeg()
    {
        var start = new Position(1630, -4373, 31);  // Orgrimmar
        var end = new Position(1650, -4360, 31);    // Nearby

        var legs = _router.PlanRoute(1, start, 1, end,
            FlightPathData.Faction.Horde);

        Assert.Single(legs);
        Assert.Equal(TransitionType.Walk, legs[0].Type);
        Assert.Equal(1u, legs[0].MapId);
    }

    // =====================================================================
    // SAME MAP — ELEVATOR CROSSING
    // =====================================================================

    [Fact]
    public void SameMap_UndercityElevator_InsertsElevatorLeg()
    {
        // From above the elevator shaft (upper level) to below (lower level)
        var start = new Position(1544, 241, 55);   // Near UC upper
        var end = new Position(1544, 241, -43);    // Near UC lower

        var legs = _router.PlanRoute(0, start, 0, end,
            FlightPathData.Faction.Horde);

        // Should have: Walk(optional) + Elevator + Walk(optional)
        Assert.True(legs.Count >= 1);
        Assert.Contains(legs, l => l.Type == TransitionType.Elevator);

        var elevatorLeg = legs.First(l => l.Type == TransitionType.Elevator);
        Assert.NotNull(elevatorLeg.Transport);
        Assert.NotNull(elevatorLeg.BoardStop);
        Assert.NotNull(elevatorLeg.ExitStop);
    }

    [Fact]
    public void SameMap_SmallZDelta_NoElevator()
    {
        // Same map, small Z change — should not insert elevator
        var start = new Position(1000, 1000, 50);
        var end = new Position(1100, 1000, 45);

        var legs = _router.PlanRoute(0, start, 0, end,
            FlightPathData.Faction.Horde);

        Assert.Single(legs);
        Assert.Equal(TransitionType.Walk, legs[0].Type);
    }

    // =====================================================================
    // CROSS-MAP — ZEPPELIN (HORDE)
    // =====================================================================

    [Fact]
    public void Map0ToMap1_Horde_ZeppelinRoute()
    {
        // Undercity (EK) → Orgrimmar (Kalimdor)
        var start = new Position(2066, 288, 97);      // Near UC zeppelin tower
        var end = new Position(1630, -4373, 31);      // Orgrimmar

        var legs = _router.PlanRoute(0, start, 1, end,
            FlightPathData.Faction.Horde);

        Assert.NotEmpty(legs);
        Assert.Contains(legs, l => l.Type == TransitionType.Zeppelin);
    }

    [Fact]
    public void Map1ToMap0_Horde_ZeppelinRoute()
    {
        // Orgrimmar (Kalimdor) → Undercity (EK)
        var start = new Position(1320, -4649, 53);    // Near Org zeppelin tower
        var end = new Position(1700, 200, 50);         // Somewhere in UC area

        var legs = _router.PlanRoute(1, start, 0, end,
            FlightPathData.Faction.Horde);

        Assert.NotEmpty(legs);
        Assert.Contains(legs, l => l.Type == TransitionType.Zeppelin);
    }

    // =====================================================================
    // CROSS-MAP — BOAT (NEUTRAL)
    // =====================================================================

    [Fact]
    public void Map0ToMap1_Neutral_BoatRoute()
    {
        // Booty Bay (EK) → Ratchet (Kalimdor) via boat
        var start = new Position(-14280, 553, 9);     // Near Booty Bay dock
        var end = new Position(-996, -3827, 6);       // Near Ratchet dock

        var legs = _router.PlanRoute(0, start, 1, end,
            FlightPathData.Faction.Horde);

        Assert.NotEmpty(legs);
        Assert.Contains(legs, l => l.Type == TransitionType.Boat);
    }

    // =====================================================================
    // CROSS-MAP — DUNGEON PORTAL
    // =====================================================================

    [Fact]
    public void Map1ToMapRFC_DungeonPortal()
    {
        // Orgrimmar → Ragefire Chasm entrance
        var start = new Position(1811, -4410, -18);
        var end = new Position(3, -11, -18);

        var legs = _router.PlanRoute(1, start, 389, end,
            FlightPathData.Faction.Horde);

        Assert.NotEmpty(legs);
        Assert.Contains(legs, l => l.Type == TransitionType.DungeonPortal);
    }

    [Fact]
    public void MapRFCToMap1_DungeonPortalExit()
    {
        // Ragefire Chasm → Orgrimmar (exit portal)
        var start = new Position(3, -11, -18);
        var end = new Position(1630, -4373, 31);

        var legs = _router.PlanRoute(389, start, 1, end,
            FlightPathData.Faction.Horde);

        Assert.NotEmpty(legs);
        Assert.Contains(legs, l => l.Type == TransitionType.DungeonPortal);
    }

    // =====================================================================
    // ROUTE STRUCTURE
    // =====================================================================

    [Fact]
    public void Route_HasReasonableTimeEstimates()
    {
        var start = new Position(2066, 288, 97);
        var end = new Position(1630, -4373, 31);

        var legs = _router.PlanRoute(0, start, 1, end,
            FlightPathData.Faction.Horde);

        float totalTime = legs.Sum(l => l.EstimatedTimeSec);
        Assert.True(totalTime > 0, "Route should have positive time estimate");
        Assert.True(totalTime < 3600, "Route should not exceed 1 hour estimate");
    }

    [Fact]
    public void Route_WalkLegs_HaveCorrectMapId()
    {
        var start = new Position(2066, 288, 97);
        var end = new Position(1630, -4373, 31);

        var legs = _router.PlanRoute(0, start, 1, end,
            FlightPathData.Faction.Horde);

        foreach (var leg in legs.Where(l => l.Type == TransitionType.Walk))
        {
            Assert.True(leg.MapId == 0 || leg.MapId == 1,
                $"Walk leg should be on map 0 or 1, got {leg.MapId}");
        }
    }

    // =====================================================================
    // EDGE CASES
    // =====================================================================

    [Fact]
    public void UnknownDestination_EmptyResult()
    {
        // Map 999 doesn't exist in the graph
        var start = new Position(0, 0, 0);
        var end = new Position(0, 0, 0);

        var legs = _router.PlanRoute(0, start, 999, end,
            FlightPathData.Faction.Horde);

        Assert.Empty(legs);
    }

    [Fact]
    public void SamePosition_SingleWalkLeg()
    {
        var pos = new Position(100, 100, 100);

        var legs = _router.PlanRoute(0, pos, 0, pos,
            FlightPathData.Faction.Horde);

        Assert.Single(legs);
        Assert.Equal(TransitionType.Walk, legs[0].Type);
    }

    [Fact]
    public void FactionFilter_HordeCannotUseAllianceBoat()
    {
        // Auberdine ↔ Teldrassil is Alliance-only
        var start = new Position(6587, 797, 5);     // Auberdine
        var end = new Position(8642, 837, 23);       // Rut'theran Village

        var legs = _router.PlanRoute(1, start, 1, end,
            FlightPathData.Faction.Horde);

        // Should still produce a route (walk), but no boat leg
        // because the Alliance-only boat won't be used
        foreach (var leg in legs)
        {
            if (leg.Type == TransitionType.Boat && leg.Transport != null)
            {
                // If a boat is included, it shouldn't be the Alliance-only one
                Assert.NotEqual("Boat: Auberdine ↔ Teldrassil (Rut'theran Village)",
                    leg.Transport.Name);
            }
        }
    }

    // =====================================================================
    // MAP TRANSITION GRAPH QUERIES
    // =====================================================================

    [Fact]
    public void GetTransitionsFrom_EK_IncludesZeppelinsAndBoats()
    {
        var transitions = MapTransitionGraph.GetTransitionsFrom(0, FlightPathData.Faction.Horde).ToList();

        Assert.NotEmpty(transitions);
        Assert.Contains(transitions, t => t.Type == TransitionType.Zeppelin);
    }

    [Fact]
    public void HasDirectTransition_EKToKalimdor_True()
    {
        Assert.True(MapTransitionGraph.HasDirectTransition(0, 1, FlightPathData.Faction.Horde));
    }

    [Fact]
    public void HasDirectTransition_EKToUnknownMap_False()
    {
        Assert.False(MapTransitionGraph.HasDirectTransition(0, 999));
    }

    [Fact]
    public void FindNearestTransition_NearZeppelinTower_FindsIt()
    {
        var pos = new Position(2066, 288, 97); // Near UC zeppelin tower
        var t = MapTransitionGraph.FindNearestTransition(0, pos, targetMapId: 1,
            faction: FlightPathData.Faction.Horde);

        Assert.NotNull(t);
        Assert.Equal(TransitionType.Zeppelin, t.Type);
    }
}
