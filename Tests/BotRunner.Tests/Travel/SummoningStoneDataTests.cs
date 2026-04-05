using BotRunner.Travel;
using GameData.Core.Models;

namespace BotRunner.Tests.Travel;

public class SummoningStoneDataTests
{
    [Fact]
    public void GetByInstanceMapId_Finds()
    {
        // Wailing Caverns instance map = 43
        var stone = SummoningStoneData.GetByInstanceMapId(43);

        Assert.NotNull(stone);
        Assert.Equal("Wailing Caverns", stone!.DungeonName);
        Assert.Equal("WC", stone.Abbreviation);
    }

    [Fact]
    public void GetByInstanceMapId_ReturnsNull_ForCityDungeon()
    {
        // RFC (map 389) has no meeting stone
        var stone = SummoningStoneData.GetByInstanceMapId(389);

        Assert.Null(stone);
    }

    [Fact]
    public void GetNearby_ReturnsInRange()
    {
        // Position near Wailing Caverns meeting stone
        var pos = new Position(-720f, -2220f, 17f);
        var nearby = SummoningStoneData.GetNearby(1, pos, searchRadius: 50f);

        Assert.True(nearby.Count >= 1, "Should find WC meeting stone nearby");
        Assert.Contains(nearby, s => s.DungeonName == "Wailing Caverns");
    }

    [Fact]
    public void GetNearby_ReturnsEmpty_WhenFarAway()
    {
        var pos = new Position(0f, 0f, 0f);
        var nearby = SummoningStoneData.GetNearby(1, pos, searchRadius: 10f);

        Assert.Empty(nearby);
    }

    [Fact]
    public void AllStones_Count()
    {
        // All dungeons with meeting stones (excludes RFC, Stockade)
        var dungeonCount = DungeonEntryData.DungeonsWithMeetingStones.Count;

        Assert.Equal(dungeonCount, SummoningStoneData.AllStones.Count);
        Assert.True(SummoningStoneData.AllStones.Count > 15,
            $"Expected more than 15 summoning stones, got {SummoningStoneData.AllStones.Count}");
    }

    [Fact]
    public void AllStones_HaveValidPositions()
    {
        foreach (var stone in SummoningStoneData.AllStones)
        {
            var isOrigin = stone.StonePosition.X == 0 && stone.StonePosition.Y == 0 && stone.StonePosition.Z == 0;
            Assert.False(isOrigin, $"Stone for {stone.DungeonName} has origin position");
        }
    }
}
