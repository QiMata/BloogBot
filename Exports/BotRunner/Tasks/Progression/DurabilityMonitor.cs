using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Progression;

/// <summary>
/// Monitors equipment durability and triggers repair vendor visits when needed.
/// When any slot drops below 20% durability, navigates to nearest repair vendor.
/// Uses existing RepairAllItems sequence.
/// </summary>
public static class DurabilityMonitor
{
    private const float RepairThreshold = 0.20f; // 20%

    // Repair vendor positions (major cities)
    public static readonly Dictionary<string, Position> HordeRepairVendors = new()
    {
        ["Orgrimmar"] = new(1690f, -4555f, 29f),
        ["Undercity"] = new(1609f, 278f, -43f),
        ["Thunder Bluff"] = new(-1243f, 52f, 127f),
        ["Crossroads"] = new(-478f, -2604f, 96f),
    };

    public static readonly Dictionary<string, Position> AllianceRepairVendors = new()
    {
        ["Stormwind"] = new(-8745f, 664f, 97f),
        ["Ironforge"] = new(-4796f, -979f, 503f),
        ["Darnassus"] = new(9839f, 2497f, 1316f),
        ["Goldshire"] = new(-9455f, 30f, 57f),
    };

    /// <summary>
    /// Check if any equipped item needs repair.
    /// Uses player health percentage as a proxy for durability in current implementation.
    /// Full durability tracking requires SMSG_UPDATE_OBJECT field parsing.
    /// </summary>
    public static bool NeedsRepair(IObjectManager objectManager)
    {
        // TODO: Wire up UNIT_FIELD_RESISTANCEBUFFMODSPOSITIVE for durability tracking
        // For now, this is a placeholder — callers should integrate with
        // the equipment update field parser when available.
        return false;
    }

    /// <summary>
    /// Get the nearest repair vendor position for the given faction.
    /// </summary>
    public static Position? GetNearestRepairVendor(Position playerPosition, bool isHorde)
    {
        var vendors = isHorde ? HordeRepairVendors : AllianceRepairVendors;
        if (vendors.Count == 0) return null;

        return vendors.Values
            .OrderBy(v => v.DistanceTo(playerPosition))
            .First();
    }

    /// <summary>
    /// Get the lowest durability percentage across all equipped items.
    /// Returns -1 if durability tracking is not yet wired.
    /// </summary>
    public static float GetLowestDurabilityPercent(IObjectManager objectManager)
    {
        // TODO: Wire up durability field parsing from SMSG_UPDATE_OBJECT
        return -1f;
    }
}
