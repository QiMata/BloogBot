using BotRunner.Travel;
using GameData.Core.Models;

namespace BotRunner.Tests.Travel;

public class InnkeeperDataTests
{
    [Fact]
    public void FindNearest_ReturnsClosest()
    {
        // Position near Orgrimmar inn
        var pos = new Position(1640f, -4440f, 16f);
        var inn = InnkeeperData.FindNearest(1, pos, InnkeeperData.InnFaction.Horde);

        Assert.NotNull(inn);
        Assert.Equal("Orgrimmar", inn!.LocationName);
    }

    [Fact]
    public void GetByFaction_Filters()
    {
        var hordeInns = InnkeeperData.GetByFaction(InnkeeperData.InnFaction.Horde);
        var allianceInns = InnkeeperData.GetByFaction(InnkeeperData.InnFaction.Alliance);

        // Horde inns include Horde + Neutral
        Assert.All(hordeInns, inn =>
            Assert.True(inn.Faction == InnkeeperData.InnFaction.Horde ||
                        inn.Faction == InnkeeperData.InnFaction.Neutral));

        // Alliance inns include Alliance + Neutral
        Assert.All(allianceInns, inn =>
            Assert.True(inn.Faction == InnkeeperData.InnFaction.Alliance ||
                        inn.Faction == InnkeeperData.InnFaction.Neutral));
    }

    [Fact]
    public void AllInnkeepers_ValidPositions()
    {
        foreach (var inn in InnkeeperData.AllInnkeepers)
        {
            // Position should not be origin (0,0,0)
            var isOrigin = inn.Position.X == 0 && inn.Position.Y == 0 && inn.Position.Z == 0;
            Assert.False(isOrigin, $"Innkeeper {inn.Name} at {inn.LocationName} has origin position");

            // Name should not be empty
            Assert.False(string.IsNullOrWhiteSpace(inn.Name),
                $"Innkeeper at {inn.LocationName} has empty name");
            Assert.False(string.IsNullOrWhiteSpace(inn.LocationName),
                $"Innkeeper {inn.Name} has empty location name");
        }
    }

    [Fact]
    public void FindNearest_IncludesNeutral()
    {
        // Position near Ratchet (neutral inn)
        var pos = new Position(-976f, -3789f, 5f);
        var inn = InnkeeperData.FindNearest(1, pos, InnkeeperData.InnFaction.Horde);

        Assert.NotNull(inn);
        Assert.Equal("Ratchet", inn!.LocationName);
    }

    [Fact]
    public void FindNearest_ReturnsNull_WhenNoMatch()
    {
        // Map 999 has no innkeepers
        var inn = InnkeeperData.FindNearest(999, new Position(0, 0, 0), InnkeeperData.InnFaction.Horde);

        Assert.Null(inn);
    }
}
