using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace BotRunner.Tasks.Progression;

/// <summary>
/// Hunter ammo management. Tracks ammo count from ranged slot/ammo pouch.
/// When below threshold (200), navigates to ammo vendor.
/// Uses existing BuyItem sequence with vendor GUID.
/// </summary>
public static class AmmoManager
{
    private const int LowAmmoThreshold = 200;
    private const int BuyQuantity = 200; // One stack

    // Common ammo item IDs by type and level
    public static readonly Dictionary<string, uint[]> ArrowIds = new()
    {
        ["Rough"] = [2512],           // Level 1-10
        ["Sharp"] = [2515],           // Level 10-25
        ["Razor"] = [3030],           // Level 25-40
        ["Jagged"] = [11285],         // Level 40-55
        ["Thorium Headed"] = [18042], // Level 55-60
    };

    public static readonly Dictionary<string, uint[]> BulletIds = new()
    {
        ["Light Shot"] = [2516],      // Level 1-10
        ["Heavy Shot"] = [2519],      // Level 10-25
        ["Solid Shot"] = [3033],      // Level 25-40
        ["Hi-Impact Mithril Slugs"] = [11284], // Level 40-55
        ["Thorium Shells"] = [15997], // Level 55-60
    };

    // Ammo vendor positions
    public static readonly Dictionary<string, Position> HordeAmmoVendors = new()
    {
        ["Orgrimmar"] = new(2109f, -4636f, 48f), // Near hunter trainers
        ["Crossroads"] = new(-472f, -2608f, 96f),
    };

    public static readonly Dictionary<string, Position> AllianceAmmoVendors = new()
    {
        ["Stormwind"] = new(-8413f, 541f, 91f), // Near hunter trainers
        ["Ironforge"] = new(-4867f, -968f, 502f),
    };

    /// <summary>
    /// Check if the hunter needs to restock ammo by checking known ammo item IDs.
    /// </summary>
    public static bool NeedsAmmo(IObjectManager objectManager, uint ammoItemId)
    {
        var count = objectManager.GetItemCount(ammoItemId);
        return count < (uint)LowAmmoThreshold;
    }

    /// <summary>
    /// Get the best ammo item ID for the character's level and weapon type.
    /// </summary>
    public static uint GetBestAmmoForLevel(int characterLevel, bool useBullets)
    {
        var ammoTable = useBullets ? BulletIds : ArrowIds;

        if (characterLevel >= 55)
            return useBullets ? 15997u : 18042u;
        if (characterLevel >= 40)
            return useBullets ? 11284u : 11285u;
        if (characterLevel >= 25)
            return useBullets ? 3033u : 3030u;
        if (characterLevel >= 10)
            return useBullets ? 2519u : 2515u;
        return useBullets ? 2516u : 2512u;
    }

    /// <summary>
    /// Get nearest ammo vendor position.
    /// </summary>
    public static Position? GetNearestAmmoVendor(Position playerPosition, bool isHorde)
    {
        var vendors = isHorde ? HordeAmmoVendors : AllianceAmmoVendors;
        Position? nearest = null;
        float bestDist = float.MaxValue;

        foreach (var pos in vendors.Values)
        {
            var dist = pos.DistanceTo(playerPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = pos;
            }
        }

        return nearest;
    }
}
