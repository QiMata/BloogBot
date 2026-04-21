using BotRunner.Tasks.Dungeoneering;
using BotRunner.Travel;

namespace BotRunner.Tests.Combat;

public class DungeonWaypointsTests
{
    [Fact]
    public void RFC_HasWaypoints()
    {
        var waypoints = DungeonWaypoints.GetWaypointsForMap(389);
        Assert.NotNull(waypoints);
        Assert.True(waypoints.Count >= 10, $"RFC should have 10+ waypoints, got {waypoints.Count}");
    }

    [Fact]
    public void WailingCaverns_HasWaypoints()
    {
        var waypoints = DungeonWaypoints.GetWaypointsForMap(43);
        Assert.NotNull(waypoints);
        Assert.True(waypoints.Count >= 10, $"WC should have 10+ waypoints, got {waypoints.Count}");
    }

    [Fact]
    public void ShadowfangKeep_HasWaypoints()
    {
        var waypoints = DungeonWaypoints.GetWaypointsForMap(33);
        Assert.NotNull(waypoints);
        Assert.True(waypoints.Count >= 5, $"SFK should have 5+ waypoints, got {waypoints.Count}");
    }

    [Fact]
    public void UnknownMapId_ReturnsNull()
    {
        var waypoints = DungeonWaypoints.GetWaypointsForMap(9999);
        Assert.Null(waypoints);
    }

    [Fact]
    public void KalimdorMapId_ReturnsNull()
    {
        var waypoints = DungeonWaypoints.GetWaypointsForMap(1);
        Assert.Null(waypoints);
    }

    [Fact]
    public void DungeonEntryData_AllDungeons_HaveEntryPositions()
    {
        foreach (var dungeon in DungeonEntryData.AllDungeons)
        {
            Assert.NotNull(dungeon.EntrancePosition);
            Assert.NotNull(dungeon.InstanceEntryPosition);
            Assert.NotEqual(0f, dungeon.EntrancePosition.X);
            Assert.NotEqual(0f, dungeon.InstanceEntryPosition.X);
            Assert.True(dungeon.InstanceMapId > 1, $"{dungeon.Name}: instance map should be >1");
            Assert.False(string.IsNullOrEmpty(dungeon.Abbreviation), $"{dungeon.Name}: missing abbreviation");
        }
    }

    [Fact]
    public void DungeonEntryData_HordeDungeons_ExcludesAllianceOnly()
    {
        foreach (var d in DungeonEntryData.HordeDungeons)
            Assert.NotEqual(DungeonEntryData.DungeonFaction.Alliance, d.Faction);
    }

    [Fact]
    public void RaidEntryData_AllRaids_HaveEntryPositions()
    {
        foreach (var raid in RaidEntryData.AllRaids)
        {
            Assert.NotNull(raid.EntrancePosition);
            Assert.NotNull(raid.InstanceEntryPosition);
            Assert.NotEqual(0f, raid.InstanceEntryPosition.X);
            Assert.True(raid.MaxPlayers >= 20, $"{raid.Name}: max players should be >=20");
        }
    }

    [Fact]
    public void RaidEntryData_FortyManRaids_AllHaveCorrectSize()
    {
        foreach (var raid in RaidEntryData.FortyManRaids)
            Assert.Equal(40, raid.MaxPlayers);
    }

    [Fact]
    public void BattlemasterData_HasAllBgTypes()
    {
        Assert.NotNull(BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.WarsongGulch, DungeonEntryData.DungeonFaction.Horde));
        Assert.NotNull(BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.ArathiBasin, DungeonEntryData.DungeonFaction.Horde));
        Assert.NotNull(BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.AlteracValley, DungeonEntryData.DungeonFaction.Horde));
        Assert.NotNull(BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.WarsongGulch, DungeonEntryData.DungeonFaction.Alliance));
    }
}
