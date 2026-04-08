using System;
using System.Collections.Generic;

namespace BotRunner.Constants;

/// <summary>
/// Well-known vanilla WoW consumable item IDs for reliable identification
/// without fragile string matching.
/// </summary>
public static class ConsumableIds
{
    /// <summary>Vanilla WoW healing potions by item ID.</summary>
    public static readonly HashSet<uint> HealthPotionIds = new()
    {
        118,   // Minor Healing Potion
        858,   // Lesser Healing Potion
        929,   // Healing Potion
        1710,  // Greater Healing Potion
        3928,  // Superior Healing Potion
        13446, // Major Healing Potion
    };

    /// <summary>Vanilla WoW mana potions by item ID.</summary>
    public static readonly HashSet<uint> ManaPotionIds = new()
    {
        2455,  // Minor Mana Potion
        3385,  // Lesser Mana Potion
        3827,  // Mana Potion
        6149,  // Greater Mana Potion
        13443, // Superior Mana Potion
        13444, // Major Mana Potion
    };

    /// <summary>Check by item ID (preferred).</summary>
    public static bool IsHealthPotion(uint itemId) => HealthPotionIds.Contains(itemId);

    /// <summary>Check by item ID (preferred).</summary>
    public static bool IsManaPotion(uint itemId) => ManaPotionIds.Contains(itemId);

    /// <summary>Fallback for items without a known ID mapping.</summary>
    public static bool IsHealthPotionByName(string name)
    {
        return name.Contains("healing potion", StringComparison.OrdinalIgnoreCase)
            || name.Contains("health potion", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Fallback for items without a known ID mapping.</summary>
    public static bool IsManaPotionByName(string name)
    {
        return name.Contains("mana potion", StringComparison.OrdinalIgnoreCase);
    }
}
