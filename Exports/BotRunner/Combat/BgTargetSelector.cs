using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Battleground-aware target prioritization.
/// Priority: flag carrier > healer > cloth DPS > melee DPS.
/// Uses ObjectManager's hostile unit list + class/role detection.
/// </summary>
public static class BgTargetSelector
{
    public enum BgTargetPriority
    {
        FlagCarrier = 1,
        Healer = 2,
        ClothDps = 3,
        MeleeDps = 4,
        Other = 5
    }

    /// <summary>
    /// Rank hostile targets by BG priority within the given range.
    /// </summary>
    public static IReadOnlyList<(IWoWUnit Unit, BgTargetPriority Priority)> RankTargets(
        IEnumerable<IWoWUnit> hostiles,
        IWoWLocalPlayer player,
        float maxRange = 40f)
    {
        return hostiles
            .Where(u => u.Health > 0 && u.Position.DistanceTo(player.Position) <= maxRange)
            .Select(u => (Unit: u, Priority: ClassifyTarget(u)))
            .OrderBy(t => (int)t.Priority)
            .ThenBy(t => t.Unit.Position.DistanceTo(player.Position))
            .ToList();
    }

    /// <summary>
    /// Get the highest-priority hostile target in range.
    /// </summary>
    public static IWoWUnit? GetBestTarget(
        IEnumerable<IWoWUnit> hostiles,
        IWoWLocalPlayer player,
        float maxRange = 40f)
    {
        return RankTargets(hostiles, player, maxRange).FirstOrDefault().Unit;
    }

    private static BgTargetPriority ClassifyTarget(IWoWUnit unit)
    {
        // Healers: detect by mana pool + casting behavior
        // Units with high mana and currently casting are likely healers
        if (unit.IsCasting && unit.MaxMana > 0 && unit.ManaPercent > 20)
            return BgTargetPriority.Healer;

        // Cloth wearers: low health pool relative to level suggests cloth
        // Rough heuristic: cloth has ~3000-4000 HP at 60, plate has ~5000-7000
        if (unit.MaxHealth < 4000 && unit.MaxMana > 3000)
            return BgTargetPriority.ClothDps;

        // High mana + not casting = potential healer idle
        if (unit.MaxMana > 4000 && unit.ManaPercent > 50)
            return BgTargetPriority.Healer;

        // Low/no mana = melee DPS (Warrior, Rogue)
        if (unit.MaxMana == 0 || unit.MaxMana < 1000)
            return BgTargetPriority.MeleeDps;

        return BgTargetPriority.Other;
    }
}
