using BotRunner.Combat;
using Xunit;

namespace BotRunner.Tests.Combat;

public class LootCouncilSimulatorTests
{
    [Fact]
    public void RecordRoll_AddsEntry()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "Player1", LootCouncilSimulator.RollType.MainSpec);
        var rolls = lc.GetRolls(1000);
        Assert.Single(rolls);
        Assert.Equal("Player1", rolls[0].PlayerName);
    }

    [Fact]
    public void GetWinner_MainSpecBeatsOffSpec()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "OffSpec", LootCouncilSimulator.RollType.OffSpec);
        lc.RecordRoll(1000, 2, "MainSpec", LootCouncilSimulator.RollType.MainSpec);
        var winner = lc.GetWinner(1000);
        Assert.NotNull(winner);
        Assert.Equal("MainSpec", winner!.PlayerName);
    }

    [Fact]
    public void GetWinner_PassesExcluded()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "Passer", LootCouncilSimulator.RollType.Pass);
        lc.RecordRoll(1000, 2, "Roller", LootCouncilSimulator.RollType.Greed);
        var winner = lc.GetWinner(1000);
        Assert.NotNull(winner);
        Assert.Equal("Roller", winner!.PlayerName);
    }

    [Fact]
    public void GetWinner_NullWhenAllPass()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "P1", LootCouncilSimulator.RollType.Pass);
        lc.RecordRoll(1000, 2, "P2", LootCouncilSimulator.RollType.Pass);
        Assert.Null(lc.GetWinner(1000));
    }

    [Fact]
    public void AllRollsIn_TrueWhenExpectedCount()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "P1", LootCouncilSimulator.RollType.MainSpec);
        lc.RecordRoll(1000, 2, "P2", LootCouncilSimulator.RollType.OffSpec);
        Assert.True(lc.AllRollsIn(1000, 2));
        Assert.False(lc.AllRollsIn(1000, 3));
    }

    [Fact]
    public void ClearItem_RemovesRolls()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "P1", LootCouncilSimulator.RollType.MainSpec);
        lc.ClearItem(1000);
        Assert.Empty(lc.GetRolls(1000));
    }

    [Fact]
    public void Reset_ClearsAll()
    {
        var lc = new LootCouncilSimulator();
        lc.RecordRoll(1000, 1, "P1", LootCouncilSimulator.RollType.MainSpec);
        lc.RecordRoll(2000, 2, "P2", LootCouncilSimulator.RollType.Greed);
        lc.Reset();
        Assert.Empty(lc.GetRolls(1000));
        Assert.Empty(lc.GetRolls(2000));
    }

    [Fact]
    public void RecordRoll_RollValueBetween1And100()
    {
        var lc = new LootCouncilSimulator();
        for (int i = 0; i < 50; i++)
        {
            lc.RecordRoll((uint)i, (ulong)i, $"P{i}", LootCouncilSimulator.RollType.MainSpec);
        }
        // Check all rolls are in valid range
        for (int i = 0; i < 50; i++)
        {
            var rolls = lc.GetRolls((uint)i);
            Assert.Single(rolls);
            Assert.InRange(rolls[0].RollValue, 1, 100);
        }
    }
}
