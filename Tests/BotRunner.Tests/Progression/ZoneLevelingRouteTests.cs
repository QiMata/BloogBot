using BotRunner.Tasks.Progression;

namespace BotRunner.Tests.Progression;

public class ZoneLevelingRouteTests
{
    [Fact]
    public void GetZoneForLevel_ReturnsCorrectZone_Horde()
    {
        var zone = ZoneLevelingRoute.GetZoneForLevel(5, isHorde: true);

        Assert.NotNull(zone);
        Assert.Equal("Durotar", zone!.Name);
    }

    [Fact]
    public void GetZoneForLevel_ReturnsCorrectZone_Alliance()
    {
        var zone = ZoneLevelingRoute.GetZoneForLevel(5, isHorde: false);

        Assert.NotNull(zone);
        Assert.Equal("Elwynn Forest", zone!.Name);
    }

    [Fact]
    public void GetNextZone_AdvancesInRoute()
    {
        var next = ZoneLevelingRoute.GetNextZone("Durotar", isHorde: true);

        Assert.NotNull(next);
        Assert.Equal("The Barrens", next!.Name);
    }

    [Fact]
    public void GetNextZone_ReturnsNull_AtEnd()
    {
        var last = ZoneLevelingRoute.HordeRoute[^1].Name;
        var next = ZoneLevelingRoute.GetNextZone(last, isHorde: true);

        Assert.Null(next);
    }

    [Fact]
    public void HordeAndAllianceRoutesAreDifferent()
    {
        Assert.NotEqual(
            ZoneLevelingRoute.HordeRoute[0].Name,
            ZoneLevelingRoute.AllianceRoute[0].Name);
    }

    [Fact]
    public void GetZoneForLevel_ReturnsNull_ForInvalidLevel()
    {
        // Level 0 should not match any zone
        var zone = ZoneLevelingRoute.GetZoneForLevel(0, isHorde: true);

        Assert.Null(zone);
    }

    [Fact]
    public void AllZonesHaveValidMapIds()
    {
        foreach (var zone in ZoneLevelingRoute.HordeRoute)
        {
            Assert.True(zone.MapId == 0 || zone.MapId == 1,
                $"Zone {zone.Name} has unexpected MapId {zone.MapId}");
        }
    }
}
