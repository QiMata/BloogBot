using BotRunner.Combat;
using BotRunner.Movement;
using GameData.Core.Models;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

namespace BotRunner.Tests.Movement;

/// <summary>
/// Tests CrossMapRouter.PlanRoute for various travel scenarios.
/// Validates leg types and ordering for cross-map routes.
/// </summary>
public class CrossMapRouterTravelTests
{
    private readonly CrossMapRouter _router = new();
    private readonly ITestOutputHelper _output;

    public CrossMapRouterTravelTests(ITestOutputHelper output) => _output = output;

    private void LogRoute(System.Collections.Generic.List<RouteLeg> legs, string description)
    {
        _output.WriteLine($"=== {description} === ({legs.Count} legs)");
        for (int i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            _output.WriteLine($"  Leg {i}: {leg.Type} map={leg.MapId} " +
                $"start=({leg.Start.X:F0},{leg.Start.Y:F0}) end=({leg.End.X:F0},{leg.End.Y:F0}) " +
                $"est={leg.EstimatedTimeSec:F0}s");
        }
    }

    [Fact]
    public void SameMap_ShortWalk_SingleWalkLeg()
    {
        var legs = _router.PlanRoute(
            1, new Position(-601, -4297, 38),
            1, new Position(-630, -4340, 40),
            FlightPathData.Faction.Horde);

        LogRoute(legs, "VoT short walk");
        Assert.NotEmpty(legs);
        Assert.Equal(TransitionType.Walk, legs[0].Type);
        Assert.Equal(1u, legs[0].MapId);
    }

    [Fact]
    public void CrossContinent_OrgToUndercity_HasZeppelinLeg()
    {
        var legs = _router.PlanRoute(
            1, new Position(1676, -4315, 61),   // Orgrimmar
            0, new Position(1586, 239, -52),     // Undercity
            FlightPathData.Faction.Horde);

        LogRoute(legs, "Orgrimmar → Undercity");
        Assert.True(legs.Count >= 2, $"Expected at least 2 legs but got {legs.Count}");

        // Should contain at least one Zeppelin or transport leg for cross-continent
        var hasTransport = legs.Any(l =>
            l.Type == TransitionType.Zeppelin ||
            l.Type == TransitionType.Boat);
        Assert.True(hasTransport, "Cross-continent route should include a transport leg");
    }

    [Fact]
    public void SameMap_OrgToRFCEntrance_WalkAndPortal()
    {
        var legs = _router.PlanRoute(
            1, new Position(1676, -4315, 61),   // Orgrimmar
            1, new Position(1811, -4410, -18),   // RFC entrance (same map)
            FlightPathData.Faction.Horde);

        LogRoute(legs, "Orgrimmar → RFC entrance");
        Assert.NotEmpty(legs);
        // Same map — should be walk (RFC is inside Org)
        Assert.Equal(TransitionType.Walk, legs[0].Type);
    }

    [Fact]
    public void CrossContinent_BootyBayToRatchet_HasBoatLeg()
    {
        var legs = _router.PlanRoute(
            0, new Position(-14408, 419, 23),   // Booty Bay
            1, new Position(-956, -3754, 6),     // Ratchet
            FlightPathData.Faction.Horde);

        LogRoute(legs, "Booty Bay → Ratchet");
        Assert.True(legs.Count >= 2, $"Expected at least 2 legs but got {legs.Count}");

        var hasBoat = legs.Any(l => l.Type == TransitionType.Boat);
        Assert.True(hasBoat, "BB→Ratchet should use the boat");
    }

    [Fact]
    public void DungeonEntry_OrgToRFC_ProducesRoute()
    {
        // Org overworld to RFC instance (map 389)
        var legs = _router.PlanRoute(
            1, new Position(1676, -4315, 61),
            389, new Position(3, -11, -18),
            FlightPathData.Faction.Horde);

        LogRoute(legs, "Orgrimmar → RFC instance");
        Assert.NotEmpty(legs);
        // Should end with a DungeonPortal leg
        var hasPortal = legs.Any(l => l.Type == TransitionType.DungeonPortal);
        Assert.True(hasPortal, "Route to dungeon instance should include a portal leg");
    }
}
