using GameData.Core.Interfaces;

namespace BotRunner.Combat
{
    /// <summary>
    /// Fishing spell IDs and constants for vanilla 1.12.1.
    /// </summary>
    public static class FishingData
    {
        // Fishing spell ranks (vanilla 1.12.1)
        public const uint FishingRank1 = 7620;   // Apprentice (1-75)
        public const uint FishingRank2 = 7731;   // Journeyman (75-150)
        public const uint FishingRank3 = 7732;   // Expert (150-225)
        public const uint FishingRank4 = 18248;  // Artisan (225-300)

        // Fishing skill ID
        public const uint FishingSkillId = 356;

        // Fishing bobber display IDs (vanilla 1.12.1)
        public const uint BobberDisplayId = 668;

        // Lure item IDs
        public const uint ShinyBauble = 6529;      // +25 fishing for 10 min
        public const uint NightcrawlerBait = 6530;  // +50 fishing for 10 min
        public const uint AquadynamicFishAttractor = 6533; // +100 fishing for 10 min
        public const uint BrightBaubles = 6532;     // +75 fishing for 10 min

        // Fishing pole item IDs
        public const uint FishingPole = 6256;        // Basic fishing pole
        public const uint StrongFishingPole = 6365;  // +5 fishing
        public const uint BigIronFishingPole = 6367;  // +20 fishing
        public const uint DarkwoodFishingPole = 19022; // +15 fishing

        // Lure IDs ordered by bonus (highest first)
        private static readonly (uint itemId, int bonus)[] LureTiers =
        [
            (AquadynamicFishAttractor, 100),
            (BrightBaubles, 75),
            (NightcrawlerBait, 50),
            (ShinyBauble, 25),
        ];

        // Fishing pole IDs ordered by bonus (highest first)
        private static readonly uint[] PoleIds =
        [
            BigIronFishingPole,     // +20
            DarkwoodFishingPole,    // +15
            StrongFishingPole,      // +5
            FishingPole,            // +0
        ];

        /// <summary>
        /// Returns the highest fishing spell rank the character should have at a given skill level.
        /// </summary>
        public static uint GetBestFishingSpellId(int fishingSkill)
        {
            if (fishingSkill >= 225) return FishingRank4;
            if (fishingSkill >= 150) return FishingRank3;
            if (fishingSkill >= 75) return FishingRank2;
            return FishingRank1;
        }

        /// <summary>
        /// Finds the best fishing lure in the player's inventory, preferring higher bonus.
        /// Returns null if no lure is found.
        /// </summary>
        public static IWoWItem? FindUsableLure(IObjectManager objectManager)
        {
            foreach (var (itemId, _) in LureTiers)
            {
                for (int bag = 0; bag < 5; bag++)
                {
                    int maxSlots = bag == 0 ? 16 : 20;
                    for (int slot = 0; slot < maxSlots; slot++)
                    {
                        var item = objectManager.GetItem(bag, slot);
                        if (item != null && item.ItemId == itemId)
                            return item;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a fishing pole in the player's inventory (not equipped), preferring higher bonus.
        /// Returns the (bag, slot) tuple, or null if none found.
        /// </summary>
        public static (int bag, int slot)? FindFishingPoleInBags(IObjectManager objectManager)
        {
            foreach (uint poleId in PoleIds)
            {
                for (int bag = 0; bag < 5; bag++)
                {
                    int maxSlots = bag == 0 ? 16 : 20;
                    for (int slot = 0; slot < maxSlots; slot++)
                    {
                        var item = objectManager.GetItem(bag, slot);
                        if (item != null && item.ItemId == poleId)
                            return (bag, slot);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if any of the known fishing pole item IDs match the given item ID.
        /// </summary>
        public static bool IsFishingPole(uint itemId) =>
            itemId == FishingPole || itemId == StrongFishingPole ||
            itemId == BigIronFishingPole || itemId == DarkwoodFishingPole;
    }
}
