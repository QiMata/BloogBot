using BotRunner.Tasks.Economy;

namespace BotRunner.Tests.Economy;

public class GoldThresholdManagerTests
{
    [Fact]
    public void Evaluate_SellTrash_WhenBelowMin()
    {
        // Current gold (1000c = 10s) < min reserve (5000c = 50s)
        var action = GoldThresholdManager.Evaluate(
            currentCopper: 1000,
            minReserveCopper: 5000,
            depositThresholdCopper: 100000);

        Assert.Equal(GoldThresholdManager.GoldAction.SellVendorTrash, action);
    }

    [Fact]
    public void Evaluate_DepositExcess_WhenAboveThreshold()
    {
        var action = GoldThresholdManager.Evaluate(
            currentCopper: 200000,
            minReserveCopper: 5000,
            depositThresholdCopper: 100000);

        Assert.Equal(GoldThresholdManager.GoldAction.DepositExcess, action);
    }

    [Fact]
    public void Evaluate_None_WhenInRange()
    {
        var action = GoldThresholdManager.Evaluate(
            currentCopper: 50000,
            minReserveCopper: 5000,
            depositThresholdCopper: 100000);

        Assert.Equal(GoldThresholdManager.GoldAction.None, action);
    }

    [Fact]
    public void CalculateDepositAmount_SubtractsReserve()
    {
        var deposit = GoldThresholdManager.CalculateDepositAmount(
            currentCopper: 150000,
            reserveCopper: 50000);

        Assert.Equal(100000u, deposit);
    }

    [Fact]
    public void CalculateDepositAmount_ReturnsZero_WhenBelowReserve()
    {
        var deposit = GoldThresholdManager.CalculateDepositAmount(
            currentCopper: 3000,
            reserveCopper: 50000);

        Assert.Equal(0u, deposit);
    }

    [Theory]
    [InlineData(5, 5000u)]       // < 10 => 50 silver
    [InlineData(15, 50000u)]     // < 20 => 5 gold
    [InlineData(25, 200000u)]    // < 30 => 20 gold
    [InlineData(35, 1000000u)]   // < 40 => 100 gold
    [InlineData(45, 500000u)]    // < 50 => 50 gold
    [InlineData(55, 1000000u)]   // < 60 => 100 gold
    [InlineData(60, 5000000u)]   // >= 60 => 500 gold
    public void GetDefaultReserve_ScalesByLevel(int level, uint expectedReserve)
    {
        Assert.Equal(expectedReserve, GoldThresholdManager.GetDefaultReserve(level));
    }

    [Fact]
    public void GetDefaultDepositThreshold_Is2xReserve()
    {
        var reserve = GoldThresholdManager.GetDefaultReserve(30);
        var threshold = GoldThresholdManager.GetDefaultDepositThreshold(30);

        Assert.Equal(reserve * 2, threshold);
    }
}
