using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Static database of vendor food/water item IDs by level tier, plus class-specific reagents.
/// Used by VendorVisitTask to buy consumables and by RestTasks to find usable items.
/// All items listed are commonly sold by general goods vendors and innkeepers in vanilla 1.12.1.
/// </summary>
public static class ConsumableData
{
    /// <summary>Target number of food items to maintain in inventory.</summary>
    public const int TARGET_FOOD_COUNT = 20;

    /// <summary>Target number of drink items to maintain in inventory.</summary>
    public const int TARGET_DRINK_COUNT = 20;

    /// <summary>Minimum food count before triggering a vendor visit.</summary>
    public const int LOW_FOOD_THRESHOLD = 4;

    /// <summary>Minimum drink count before triggering a vendor visit.</summary>
    public const int LOW_DRINK_THRESHOLD = 4;

    /// <summary>Hearthstone item ID (every character has one).</summary>
    public const uint HEARTHSTONE_ITEM_ID = 6948;

    /// <summary>
    /// Finds the Hearthstone in inventory. Returns null if not found.
    /// </summary>
    public static IWoWItem? FindHearthstone(IObjectManager objectManager)
    {
        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item != null && item.ItemId == HEARTHSTONE_ITEM_ID)
                    return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Vendor food items by level tier (requiredLevel, itemId, name).
    /// Higher tiers restore more health. Sorted by level descending for lookup.
    /// </summary>
    private static readonly (int Level, uint ItemId, string Name)[] FoodTiers =
    [
        (45, 8950, "Homemade Cherry Pie"),   // Restores 2148 health over 30s
        (35, 8952, "Roasted Quail"),         // Restores 1392 health over 30s
        (25, 3771, "Wild Hog Shank"),        // Restores 874 health over 27s
        (15, 3770, "Mutton Chop"),           // Restores 552 health over 24s
        (5,  2287, "Haunch of Meat"),        // Restores 243 health over 21s
        (1,  117,  "Tough Jerky"),           // Restores 61 health over 18s
    ];

    /// <summary>
    /// Vendor drink items by level tier (requiredLevel, itemId, name).
    /// Higher tiers restore more mana. Sorted by level descending for lookup.
    /// </summary>
    private static readonly (int Level, uint ItemId, string Name)[] DrinkTiers =
    [
        (45, 8766, "Morning Glory Dew"),     // Restores 2934 mana over 30s
        (35, 1645, "Moonberry Juice"),       // Restores 1992 mana over 30s
        (25, 1708, "Sweet Nectar"),          // Restores 1344 mana over 27s
        (15, 1205, "Melon Juice"),           // Restores 835 mana over 24s
        (5,  1179, "Ice Cold Milk"),         // Restores 436 mana over 21s
        (1,  159,  "Refreshing Spring Water"), // Restores 151 mana over 18s
    ];

    /// <summary>
    /// Bandage items by level tier (requiredLevel, itemId, name).
    /// Created via First Aid profession. Not vendor-purchasable but can be in inventory.
    /// </summary>
    private static readonly (int Level, uint ItemId, string Name)[] BandageTiers =
    [
        (40, 14530, "Heavy Runecloth Bandage"), // Heals 2000 over 8s
        (35, 14529, "Runecloth Bandage"),       // Heals 1360 over 8s
        (30, 8545,  "Heavy Mageweave Bandage"), // Heals 1104 over 8s
        (25, 8544,  "Mageweave Bandage"),       // Heals 800 over 8s
        (20, 6451,  "Heavy Silk Bandage"),       // Heals 640 over 8s
        (15, 6450,  "Silk Bandage"),             // Heals 400 over 8s
        (10, 3531,  "Heavy Wool Bandage"),       // Heals 301 over 7s
        (5,  3530,  "Wool Bandage"),             // Heals 161 over 7s
        (1,  2581,  "Heavy Linen Bandage"),      // Heals 114 over 6s
        (1,  1251,  "Linen Bandage"),            // Heals 66 over 6s
    ];

    /// <summary>All known food item IDs (for inventory scanning).</summary>
    public static readonly HashSet<uint> AllFoodItemIds = new(FoodTiers.Select(t => t.ItemId));

    /// <summary>All known drink item IDs (for inventory scanning).</summary>
    public static readonly HashSet<uint> AllDrinkItemIds = new(DrinkTiers.Select(t => t.ItemId));

    /// <summary>All known bandage item IDs (for inventory scanning).</summary>
    public static readonly HashSet<uint> AllBandageItemIds = new(BandageTiers.Select(t => t.ItemId));

    /// <summary>
    /// Gets the best food item ID for the given player level.
    /// Returns the highest-tier food the player can use.
    /// </summary>
    public static uint GetFoodItemId(uint playerLevel)
    {
        foreach (var tier in FoodTiers)
        {
            if (playerLevel >= tier.Level)
                return tier.ItemId;
        }
        return FoodTiers[^1].ItemId; // fallback to lowest tier
    }

    /// <summary>
    /// Gets the best drink item ID for the given player level.
    /// Returns the highest-tier drink the player can use.
    /// </summary>
    public static uint GetDrinkItemId(uint playerLevel)
    {
        foreach (var tier in DrinkTiers)
        {
            if (playerLevel >= tier.Level)
                return tier.ItemId;
        }
        return DrinkTiers[^1].ItemId; // fallback to lowest tier
    }

    /// <summary>
    /// Counts total food items across all inventory bags.
    /// Matches any known food item ID and sums stack counts.
    /// </summary>
    public static int CountFood(IObjectManager objectManager)
    {
        int count = 0;
        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item != null && AllFoodItemIds.Contains(item.ItemId))
                    count += (int)item.StackCount;
            }
        }
        return count;
    }

    /// <summary>
    /// Counts total drink items across all inventory bags.
    /// Matches any known drink item ID and sums stack counts.
    /// </summary>
    public static int CountDrink(IObjectManager objectManager)
    {
        int count = 0;
        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item != null && AllDrinkItemIds.Contains(item.ItemId))
                    count += (int)item.StackCount;
            }
        }
        return count;
    }

    /// <summary>
    /// Finds the best food item in inventory (highest tier the player can use).
    /// Returns null if no food found.
    /// </summary>
    public static IWoWItem? FindBestFood(IObjectManager objectManager)
    {
        IWoWItem? best = null;
        int bestTier = -1;

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item == null || !AllFoodItemIds.Contains(item.ItemId)) continue;

                int tier = GetFoodTierIndex(item.ItemId);
                if (tier > bestTier)
                {
                    bestTier = tier;
                    best = item;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Finds the best drink item in inventory (highest tier the player can use).
    /// Returns null if no drink found.
    /// </summary>
    public static IWoWItem? FindBestDrink(IObjectManager objectManager)
    {
        IWoWItem? best = null;
        int bestTier = -1;

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item == null || !AllDrinkItemIds.Contains(item.ItemId)) continue;

                int tier = GetDrinkTierIndex(item.ItemId);
                if (tier > bestTier)
                {
                    bestTier = tier;
                    best = item;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Finds the best bandage item in inventory (highest tier the player can use).
    /// Returns null if no bandage found.
    /// </summary>
    public static IWoWItem? FindBestBandage(IObjectManager objectManager)
    {
        IWoWItem? best = null;
        int bestTier = -1;

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item == null || !AllBandageItemIds.Contains(item.ItemId)) continue;

                int tier = GetBandageTierIndex(item.ItemId);
                if (tier > bestTier)
                {
                    bestTier = tier;
                    best = item;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Class-specific reagents needed for important spells.
    /// (Class, ItemId, Name, TargetCount, MinLevel)
    /// </summary>
    private static readonly (Class Class, uint ItemId, string Name, int TargetCount, int MinLevel)[] ReagentData =
    [
        // Shaman: Ankh for Reincarnation (self-res, avoids corpse run)
        (Class.Shaman, 17030, "Ankh", 5, 30),
        // Paladin: Symbol of Kings for Greater Blessing of Kings (group buff)
        (Class.Paladin, 21177, "Symbol of Kings", 10, 52),
        // Priest: Sacred Candle for Prayer of Fortitude (group buff)
        (Class.Priest, 17029, "Sacred Candle", 10, 48),
        // Mage: Arcane Powder for group portals/intellect (less critical for solo)
        (Class.Mage, 17020, "Arcane Powder", 5, 46),
    ];

    /// <summary>All known reagent item IDs (for inventory scanning).</summary>
    public static readonly HashSet<uint> AllReagentItemIds = new(ReagentData.Select(r => r.ItemId));

    /// <summary>
    /// Counts total reagent items of a specific type across all inventory bags.
    /// </summary>
    public static int CountReagent(IObjectManager objectManager, uint itemId)
    {
        int count = 0;
        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item != null && item.ItemId == itemId)
                    count += (int)item.StackCount;
            }
        }
        return count;
    }

    /// <summary>
    /// Gets reagents to buy for a specific class and level.
    /// Returns a dictionary of itemId → quantity.
    /// </summary>
    public static Dictionary<uint, uint> GetReagentsToBuy(IObjectManager objectManager, Class playerClass, uint playerLevel)
    {
        var result = new Dictionary<uint, uint>();
        foreach (var (cls, itemId, _, targetCount, minLevel) in ReagentData)
        {
            if (cls != playerClass) continue;
            if (playerLevel < minLevel) continue;

            int current = CountReagent(objectManager, itemId);
            if (current < targetCount)
            {
                result[itemId] = (uint)(targetCount - current);
            }
        }
        return result;
    }

    /// <summary>
    /// Determines what consumables to buy based on player level and current inventory.
    /// Returns a dictionary of itemId → quantity suitable for vendor purchase.
    /// </summary>
    public static Dictionary<uint, uint> GetConsumablesToBuy(IObjectManager objectManager, uint playerLevel, bool usesMana, Class playerClass = Class.Warrior)
    {
        var itemsToBuy = new Dictionary<uint, uint>();

        // Food — always needed
        int currentFood = CountFood(objectManager);
        if (currentFood < TARGET_FOOD_COUNT)
        {
            uint foodId = GetFoodItemId(playerLevel);
            uint foodToBuy = (uint)(TARGET_FOOD_COUNT - currentFood);
            if (foodToBuy > 0)
                itemsToBuy[foodId] = foodToBuy;
        }

        // Drink — only for mana classes
        if (usesMana)
        {
            int currentDrink = CountDrink(objectManager);
            if (currentDrink < TARGET_DRINK_COUNT)
            {
                uint drinkId = GetDrinkItemId(playerLevel);
                uint drinkToBuy = (uint)(TARGET_DRINK_COUNT - currentDrink);
                if (drinkToBuy > 0)
                    itemsToBuy[drinkId] = drinkToBuy;
            }
        }

        // Class-specific reagents
        var reagents = GetReagentsToBuy(objectManager, playerClass, playerLevel);
        foreach (var (itemId, qty) in reagents)
            itemsToBuy[itemId] = qty;

        return itemsToBuy;
    }

    /// <summary>
    /// Returns true if the player is low on consumables and should visit a vendor.
    /// </summary>
    public static bool IsLowOnConsumables(IObjectManager objectManager, bool usesMana)
    {
        if (CountFood(objectManager) <= LOW_FOOD_THRESHOLD)
            return true;

        if (usesMana && CountDrink(objectManager) <= LOW_DRINK_THRESHOLD)
            return true;

        return false;
    }

    /// <summary>
    /// Maps scroll name prefixes to the buff name they apply.
    /// In vanilla 1.12.1, scrolls grant a buff matching the stat name.
    /// </summary>
    private static readonly (string ScrollPrefix, string BuffName)[] ScrollBuffMap =
    [
        ("Scroll of Agility", "Agility"),
        ("Scroll of Intellect", "Intellect"),
        ("Scroll of Protection", "Armor"),
        ("Scroll of Spirit", "Spirit"),
        ("Scroll of Stamina", "Stamina"),
        ("Scroll of Strength", "Strength"),
    ];

    /// <summary>
    /// Finds scrolls in inventory that the player doesn't already have the buff for.
    /// Returns the best (highest tier) scroll per buff type.
    /// </summary>
    public static List<IWoWItem> FindUsableScrolls(IObjectManager objectManager)
    {
        var player = objectManager.Player;
        if (player == null) return [];

        // Collect all scrolls from inventory, grouped by buff type
        var scrollsByBuff = new Dictionary<string, (IWoWItem item, string name)>();

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item == null) continue;

                string itemName = item.Info?.Name ?? item.Name;
                if (string.IsNullOrEmpty(itemName)) continue;

                foreach (var (scrollPrefix, buffName) in ScrollBuffMap)
                {
                    if (!itemName.StartsWith(scrollPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip if player already has this buff
                    if (player.HasBuff(buffName))
                        break;

                    // Keep the scroll with the highest rank numeral (later in name = higher)
                    if (!scrollsByBuff.TryGetValue(buffName, out var existing) ||
                        string.Compare(itemName, existing.name, StringComparison.Ordinal) > 0)
                    {
                        scrollsByBuff[buffName] = (item, itemName);
                    }
                    break;
                }
            }
        }

        return scrollsByBuff.Values.Select(v => v.item).ToList();
    }

    /// <summary>
    /// Elixir name→buff mappings for auto-use from inventory.
    /// Battle elixirs and guardian elixirs are separate categories (one of each active at a time).
    /// In vanilla, elixirs overwrite same-category buffs, so we only use one per type.
    /// Format: (ItemNamePrefix, BuffName, IsBattle)
    /// </summary>
    private static readonly (string NamePrefix, string BuffName, bool IsBattle)[] ElixirBuffMap =
    [
        // Battle Elixirs (offensive stats)
        ("Elixir of the Mongoose", "Elixir of the Mongoose", true),      // +25 Agi, +2% Crit
        ("Elixir of the Giants", "Elixir of the Giants", true),          // +25 Str
        ("Elixir of Greater Agility", "Elixir of Greater Agility", true),// +25 Agi
        ("Elixir of Agility", "Agility", true),                          // +15 Agi
        ("Elixir of Greater Intellect", "Elixir of Greater Intellect", true), // +25 Int
        ("Elixir of Lion's Strength", "Elixir of Lion's Strength", true),// +4 Str
        ("Elixir of Minor Agility", "Elixir of Minor Agility", true),   // +4 Agi

        // Guardian Elixirs (defensive stats)
        ("Elixir of Superior Defense", "Elixir of Superior Defense", false), // +450 Armor
        ("Elixir of Greater Defense", "Elixir of Greater Defense", false),   // +250 Armor
        ("Elixir of Fortitude", "Elixir of Fortitude", false),             // +120 HP
        ("Elixir of Defense", "Elixir of Defense", false),                 // +150 Armor
        ("Elixir of Minor Defense", "Elixir of Minor Defense", false),     // +50 Armor
        ("Elixir of Minor Fortitude", "Elixir of Minor Fortitude", false), // +27 HP
    ];

    /// <summary>
    /// Buff food name→buff mappings. These are cooked foods that give "Well Fed" or specific stat buffs.
    /// Matched by item name prefix. Only usable when not already "Well Fed".
    /// </summary>
    private static readonly string[] WellFedFoodNames =
    [
        // High-level buff foods
        "Dirge's Kickin' Chimaerok Chops",
        "Grilled Squid",
        "Nightfin Soup",
        "Runn Tum Tuber Surprise",
        "Monster Omelet",
        "Tender Wolf Steak",
        "Sagefish Delight",
        "Smoked Sagefish",
        "Hot Lion Chops",
        "Jungle Stew",
        "Carrion Surprise",
        "Seasoned Wolf Kabob",
        "Redridge Goulash",
        "Crunchy Spider Surprise",
        "Roasted Boar Meat",
        "Smoked Bear Meat",
        "Herb Baked Egg",
        "Spiced Wolf Meat",
    ];

    /// <summary>
    /// Finds elixirs in inventory that the player doesn't already have the buff for.
    /// Returns one battle elixir and one guardian elixir (if available and unbuffed).
    /// </summary>
    public static List<IWoWItem> FindUsableElixirs(IObjectManager objectManager)
    {
        var player = objectManager.Player;
        if (player == null) return [];

        IWoWItem? bestBattle = null;
        IWoWItem? bestGuardian = null;
        int bestBattleIdx = int.MaxValue;
        int bestGuardianIdx = int.MaxValue;

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item == null) continue;

                string itemName = item.Info?.Name ?? item.Name;
                if (string.IsNullOrEmpty(itemName)) continue;

                for (int i = 0; i < ElixirBuffMap.Length; i++)
                {
                    var (namePrefix, buffName, isBattle) = ElixirBuffMap[i];
                    if (!itemName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip if player already has this buff
                    if (player.HasBuff(buffName))
                        break;

                    // Keep the best (first in list = highest priority)
                    if (isBattle && i < bestBattleIdx)
                    {
                        bestBattle = item;
                        bestBattleIdx = i;
                    }
                    else if (!isBattle && i < bestGuardianIdx)
                    {
                        bestGuardian = item;
                        bestGuardianIdx = i;
                    }
                    break;
                }
            }
        }

        var result = new List<IWoWItem>();
        if (bestBattle != null) result.Add(bestBattle);
        if (bestGuardian != null) result.Add(bestGuardian);
        return result;
    }

    /// <summary>
    /// Finds buff food in inventory that provides "Well Fed" stat bonus.
    /// Returns the first matching item if the player is not already Well Fed.
    /// </summary>
    public static IWoWItem? FindUsableBuffFood(IObjectManager objectManager)
    {
        var player = objectManager.Player;
        if (player == null) return null;

        // Already have Well Fed buff — no need
        if (player.HasBuff("Well Fed")) return null;

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item == null) continue;

                string itemName = item.Info?.Name ?? item.Name;
                if (string.IsNullOrEmpty(itemName)) continue;

                foreach (var foodName in WellFedFoodNames)
                {
                    if (itemName.Equals(foodName, StringComparison.OrdinalIgnoreCase))
                        return item;
                }
            }
        }

        return null;
    }

    // Higher index = higher tier (FoodTiers is sorted descending, so index 0 is best)
    private static int GetFoodTierIndex(uint itemId)
    {
        for (int i = 0; i < FoodTiers.Length; i++)
        {
            if (FoodTiers[i].ItemId == itemId)
                return FoodTiers.Length - i; // invert so higher tier = higher index
        }
        return 0;
    }

    private static int GetDrinkTierIndex(uint itemId)
    {
        for (int i = 0; i < DrinkTiers.Length; i++)
        {
            if (DrinkTiers[i].ItemId == itemId)
                return DrinkTiers.Length - i;
        }
        return 0;
    }

    private static int GetBandageTierIndex(uint itemId)
    {
        for (int i = 0; i < BandageTiers.Length; i++)
        {
            if (BandageTiers[i].ItemId == itemId)
                return BandageTiers.Length - i;
        }
        return 0;
    }
}
