using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat
{
    /// <summary>
    /// Vanilla 1.12.1 crafting recipes for First Aid and Cooking.
    /// These are secondary professions all characters can learn.
    /// </summary>
    public static class CraftingData
    {
        // First Aid skill ID
        public const uint FirstAidSkillId = 129;
        // Cooking skill ID
        public const uint CookingSkillId = 185;

        /// <summary>
        /// A craftable recipe: spell ID to cast, required materials, result item, and skill requirement.
        /// </summary>
        public class Recipe
        {
            public uint SpellId { get; init; }
            public string Name { get; init; } = "";
            public uint ResultItemId { get; init; }
            public uint ResultCount { get; init; } = 1;
            public int RequiredSkill { get; init; }
            public int YellowSkill { get; init; }
            public int GreenSkill { get; init; }
            public int GreySkill { get; init; }
            public (uint ItemId, int Count)[] Materials { get; init; } = [];
        }

        // ======== FIRST AID RECIPES ========

        public static readonly Recipe[] FirstAidRecipes =
        [
            new() { SpellId = 3275, Name = "Linen Bandage", ResultItemId = 1251,
                RequiredSkill = 1, YellowSkill = 1, GreenSkill = 30, GreySkill = 50,
                Materials = [(2589u, 1)] },
            new() { SpellId = 3276, Name = "Heavy Linen Bandage", ResultItemId = 2581,
                RequiredSkill = 40, YellowSkill = 40, GreenSkill = 50, GreySkill = 80,
                Materials = [(2589u, 2)] },
            new() { SpellId = 3277, Name = "Wool Bandage", ResultItemId = 3530,
                RequiredSkill = 80, YellowSkill = 80, GreenSkill = 115, GreySkill = 150,
                Materials = [(2592u, 1)] },
            new() { SpellId = 3278, Name = "Heavy Wool Bandage", ResultItemId = 3531,
                RequiredSkill = 115, YellowSkill = 115, GreenSkill = 150, GreySkill = 180,
                Materials = [(2592u, 2)] },
            new() { SpellId = 7928, Name = "Silk Bandage", ResultItemId = 6450,
                RequiredSkill = 150, YellowSkill = 150, GreenSkill = 180, GreySkill = 210,
                Materials = [(4306u, 1)] },
            new() { SpellId = 7929, Name = "Heavy Silk Bandage", ResultItemId = 6451,
                RequiredSkill = 180, YellowSkill = 180, GreenSkill = 210, GreySkill = 240,
                Materials = [(4306u, 2)] },
            new() { SpellId = 10840, Name = "Mageweave Bandage", ResultItemId = 8544,
                RequiredSkill = 210, YellowSkill = 210, GreenSkill = 240, GreySkill = 270,
                Materials = [(4338u, 1)] },
            new() { SpellId = 10841, Name = "Heavy Mageweave Bandage", ResultItemId = 8545,
                RequiredSkill = 240, YellowSkill = 240, GreenSkill = 270, GreySkill = 300,
                Materials = [(4338u, 2)] },
            new() { SpellId = 18629, Name = "Runecloth Bandage", ResultItemId = 14529,
                RequiredSkill = 260, YellowSkill = 260, GreenSkill = 290, GreySkill = 300,
                Materials = [(14047u, 1)] },
            new() { SpellId = 18630, Name = "Heavy Runecloth Bandage", ResultItemId = 14530,
                RequiredSkill = 290, YellowSkill = 290, GreenSkill = 300, GreySkill = 300,
                Materials = [(14047u, 2)] },
        ];

        // ======== COOKING RECIPES ========

        public static readonly Recipe[] CookingRecipes =
        [
            new() { SpellId = 2538, Name = "Charred Wolf Meat", ResultItemId = 2679,
                RequiredSkill = 1, YellowSkill = 1, GreenSkill = 35, GreySkill = 55,
                Materials = [(2672u, 1)] },
            new() { SpellId = 2540, Name = "Roasted Boar Meat", ResultItemId = 2681,
                RequiredSkill = 1, YellowSkill = 1, GreenSkill = 35, GreySkill = 55,
                Materials = [(2677u, 1)] },
            new() { SpellId = 2539, Name = "Spiced Wolf Meat", ResultItemId = 2680,
                RequiredSkill = 10, YellowSkill = 10, GreenSkill = 45, GreySkill = 65,
                Materials = [(2672u, 1), (2678u, 1)] },
            new() { SpellId = 3397, Name = "Smoked Bear Meat", ResultItemId = 3731,
                RequiredSkill = 40, YellowSkill = 40, GreenSkill = 80, GreySkill = 100,
                Materials = [(3173u, 1)] },
            new() { SpellId = 6499, Name = "Boiled Clams", ResultItemId = 5525,
                RequiredSkill = 50, YellowSkill = 50, GreenSkill = 90, GreySkill = 110,
                Materials = [(5503u, 1), (159u, 1)] },
            new() { SpellId = 3373, Name = "Crocolisk Steak", ResultItemId = 3662,
                RequiredSkill = 80, YellowSkill = 80, GreenSkill = 120, GreySkill = 140,
                Materials = [(3712u, 1), (2678u, 1)] },
            new() { SpellId = 2547, Name = "Redridge Goulash", ResultItemId = 1082,
                RequiredSkill = 100, YellowSkill = 100, GreenSkill = 135, GreySkill = 155,
                Materials = [(1015u, 1), (2678u, 1)] },
            new() { SpellId = 7753, Name = "Roast Raptor", ResultItemId = 12210,
                RequiredSkill = 175, YellowSkill = 175, GreenSkill = 215, GreySkill = 235,
                Materials = [(12184u, 1), (3713u, 1)] },
            new() { SpellId = 7754, Name = "Spotted Yellowtail", ResultItemId = 6887,
                RequiredSkill = 225, YellowSkill = 225, GreenSkill = 250, GreySkill = 275,
                Materials = [(6361u, 1)] },
        ];

        // Cloth item IDs (used as First Aid materials)
        public const uint LinenCloth = 2589;
        public const uint WoolCloth = 2592;
        public const uint SilkCloth = 4306;
        public const uint MageweaveCloth = 4338;
        public const uint RuneclothItem = 14047;

        /// <summary>
        /// Returns the best recipe the player can craft for skill-ups, given current skill level
        /// and available materials (inventory item IDs + counts).
        /// </summary>
        public static Recipe? FindBestRecipeForSkillUp(
            Recipe[] recipes,
            int currentSkill,
            Dictionary<uint, int> inventory)
        {
            return recipes
                .Where(r => currentSkill >= r.RequiredSkill && currentSkill < r.GreySkill)
                .Where(r => HasMaterials(r, inventory))
                .OrderByDescending(r => r.RequiredSkill)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns how many times a recipe can be crafted with available materials.
        /// </summary>
        public static int MaxCraftCount(Recipe recipe, Dictionary<uint, int> inventory)
        {
            int maxCount = int.MaxValue;
            foreach (var (itemId, count) in recipe.Materials)
            {
                int available = inventory.GetValueOrDefault(itemId);
                if (available <= 0) return 0;
                maxCount = System.Math.Min(maxCount, available / count);
            }
            return maxCount == int.MaxValue ? 0 : maxCount;
        }

        private static bool HasMaterials(Recipe recipe, Dictionary<uint, int> inventory)
        {
            foreach (var (itemId, count) in recipe.Materials)
            {
                if (inventory.GetValueOrDefault(itemId) < count) return false;
            }
            return true;
        }
    }
}
