using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tasks.Economy;

/// <summary>
/// Manages gold thresholds: deposits excess to bank alt via mail,
/// sells vendor trash when low. Maintains operating reserve for repairs + consumables.
/// </summary>
public static class GoldThresholdManager
{
    /// <summary>Gold management decision.</summary>
    public enum GoldAction { None, SellVendorTrash, DepositExcess }

    /// <summary>
    /// Evaluate what gold management action to take.
    /// </summary>
    /// <param name="currentCopper">Current gold in copper.</param>
    /// <param name="minReserveCopper">Minimum operating reserve (repairs + consumables).</param>
    /// <param name="depositThresholdCopper">Deposit to bank alt when above this.</param>
    public static GoldAction Evaluate(uint currentCopper, uint minReserveCopper, uint depositThresholdCopper)
    {
        // Low on gold — sell trash to vendor
        if (currentCopper < minReserveCopper)
            return GoldAction.SellVendorTrash;

        // Too much gold — deposit excess to bank alt
        if (currentCopper > depositThresholdCopper)
            return GoldAction.DepositExcess;

        return GoldAction.None;
    }

    /// <summary>
    /// Calculate how much gold to deposit (keep the reserve).
    /// </summary>
    public static uint CalculateDepositAmount(uint currentCopper, uint reserveCopper)
    {
        if (currentCopper <= reserveCopper) return 0;
        return currentCopper - reserveCopper;
    }

    /// <summary>
    /// Default reserve amounts by level range.
    /// </summary>
    public static uint GetDefaultReserve(int characterLevel) => characterLevel switch
    {
        < 10 => 5000,       // 50 silver
        < 20 => 50000,      // 5 gold
        < 30 => 200000,     // 20 gold
        < 40 => 1000000,    // 100 gold (mount money)
        < 50 => 500000,     // 50 gold
        < 60 => 1000000,    // 100 gold
        _ => 5000000,       // 500 gold (epic mount savings)
    };

    /// <summary>
    /// Default deposit threshold (2x reserve).
    /// </summary>
    public static uint GetDefaultDepositThreshold(int characterLevel)
        => GetDefaultReserve(characterLevel) * 2;
}
