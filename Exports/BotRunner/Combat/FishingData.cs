using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System;

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

        // Fishing pole weapon proficiency — required to equip fishing poles.
        // Real trainers grant this alongside fishing spells; GM .learn does not.
        public const uint FishingPoleProficiency = 7738;

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

        private static readonly HashSet<uint> FishingPoolEntries =
        [
            180248, // School of Tastyfish
            180582, // Oily Blackmouth School
            180655, // Floating Debris (Barrens pool system)
            180683, // Firefin Snapper School
            180684, // Greater Sagefish School
            180685, // Deviate Fish Node
            180901, // Bloodsail Wreckage
            180902, // Floating Wreckage
            180503, // Stonescale Eel Swarm
            180751, // Floating Debris
        ];

        public static IReadOnlyCollection<uint> KnownFishingPoolEntries => FishingPoolEntries;

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
        /// Returns the highest fishing rank currently present in the known-spell list.
        /// Rank-up packets supersede lower ranks server-side, so the spell book may only
        /// contain the highest learned fishing rank.
        /// </summary>
        public static uint GetBestKnownFishingSpellId(IEnumerable<uint>? knownSpellIds)
        {
            if (knownSpellIds == null)
                return 0;

            var spellSet = knownSpellIds as ISet<uint> ?? knownSpellIds.ToHashSet();
            if (spellSet.Contains(FishingRank4)) return FishingRank4;
            if (spellSet.Contains(FishingRank3)) return FishingRank3;
            if (spellSet.Contains(FishingRank2)) return FishingRank2;
            if (spellSet.Contains(FishingRank1)) return FishingRank1;
            return 0;
        }

        /// <summary>
        /// Resolves the castable fishing spell, preferring the highest currently-known rank
        /// and falling back to the skill-derived rank when the known-spell list is unavailable.
        /// </summary>
        public static uint ResolveCastableFishingSpellId(IEnumerable<uint>? knownSpellIds, int fishingSkill)
        {
            var knownRank = GetBestKnownFishingSpellId(knownSpellIds);
            return knownRank != 0 ? knownRank : GetBestFishingSpellId(fishingSkill);
        }

        /// <summary>
        /// Finds the best fishing lure in the player's inventory, preferring higher bonus.
        /// Returns null if no lure is found.
        /// </summary>
        public static IWoWItem? FindUsableLure(IObjectManager objectManager)
        {
            var lureLocation = FindUsableLureInBags(objectManager);
            return lureLocation == null ? null : objectManager.GetItem(lureLocation.Value.bag, lureLocation.Value.slot);
        }

        /// <summary>
        /// Finds the best fishing lure in the player's inventory, preferring higher bonus.
        /// Returns the (bag, slot, itemId) tuple, or null if none found.
        /// </summary>
        public static (int bag, int slot, uint itemId)? FindUsableLureInBags(IObjectManager objectManager)
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
                            return (bag, slot, itemId);
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
        /// Checks if a fishing pole is already equipped in any weapon slot.
        /// </summary>
        public static bool HasFishingPoleEquipped(IObjectManager objectManager)
            => objectManager.GetEquippedItems()
                .Where(item => item != null)
                .Any(item => IsFishingPole(item.ItemId));

        /// <summary>
        /// Checks if the supplied game object is a fishable pool/school.
        /// </summary>
        public static bool IsFishingPool(IWoWGameObject? gameObject)
        {
            if (gameObject?.Position == null)
                return false;

            if (gameObject.TypeId == (uint)GameObjectType.FishingHole)
                return true;

            if (FishingPoolEntries.Contains(gameObject.Entry))
                return true;

            return !string.IsNullOrWhiteSpace(gameObject.Name)
                && (gameObject.Name.Contains("School", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Pool", StringComparison.OrdinalIgnoreCase)
                    || gameObject.Name.Contains("Wreckage", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds the nearest visible fishing pool from the supplied position.
        /// </summary>
        public static IWoWGameObject? FindNearestFishingPool(IObjectManager objectManager, Position playerPosition, float maxDistance)
            => objectManager.GameObjects
                .Where(IsFishingPool)
                .Where(gameObject => gameObject.Position != null && gameObject.Position.DistanceTo(playerPosition) <= maxDistance)
                .OrderBy(gameObject => gameObject.Position!.DistanceTo(playerPosition))
                .FirstOrDefault();

        /// <summary>
        /// Calculates a shoreline approach point that keeps the player a fixed distance from the pool.
        /// </summary>
        public static Position GetPoolApproachPosition(Position playerPosition, Position poolPosition, float desiredDistance)
        {
            var dx = playerPosition.X - poolPosition.X;
            var dy = playerPosition.Y - poolPosition.Y;
            var planarDistance = MathF.Sqrt((dx * dx) + (dy * dy));
            var verticalDelta = playerPosition.Z - poolPosition.Z;
            var planarTargetDistance = desiredDistance;
            if (MathF.Abs(verticalDelta) > 0.01f && desiredDistance > MathF.Abs(verticalDelta))
                planarTargetDistance = MathF.Sqrt((desiredDistance * desiredDistance) - (verticalDelta * verticalDelta));

            if (planarDistance < 0.01f)
                return new Position(poolPosition.X - planarTargetDistance, poolPosition.Y, playerPosition.Z);

            var scale = planarTargetDistance / planarDistance;
            return new Position(
                poolPosition.X + (dx * scale),
                poolPosition.Y + (dy * scale),
                playerPosition.Z);
        }

        /// <summary>
        /// Calculates a set of shoreline approach points around the pool at the desired distance.
        /// The first candidate stays on the direct player->pool line; later candidates rotate around
        /// the pool so tasks can recover when the direct cast line is blocked by a dock edge or pier.
        /// </summary>
        public static Position[] GetPoolApproachCandidates(Position playerPosition, Position poolPosition, float desiredDistance)
        {
            var dx = playerPosition.X - poolPosition.X;
            var dy = playerPosition.Y - poolPosition.Y;
            var baseAngle = MathF.Atan2(dy, dx);
            if (MathF.Abs(dx) < 0.01f && MathF.Abs(dy) < 0.01f)
                baseAngle = MathF.PI;

            // Sweep ±120° in 20° steps at multiple distance rings within casting range.
            // Wider sweep finds dock/shore positions even when the pool is off to the side.
            ReadOnlySpan<float> angleOffsetsDeg = [0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f, 100f, -100f, 120f, -120f];
            ReadOnlySpan<float> distanceFactors = [1.0f, 0.7f, 1.3f]; // desired, closer, farther

            var candidates = new List<Position>(angleOffsetsDeg.Length * distanceFactors.Length);
            foreach (var distFactor in distanceFactors)
            {
                var ringDistance = GetPlanarTargetDistance(playerPosition, poolPosition, desiredDistance * distFactor);
                foreach (var offsetDeg in angleOffsetsDeg)
                {
                    var angle = baseAngle + (offsetDeg * (MathF.PI / 180f));
                    candidates.Add(new Position(
                        poolPosition.X + (MathF.Cos(angle) * ringDistance),
                        poolPosition.Y + (MathF.Sin(angle) * ringDistance),
                        playerPosition.Z));
                }
            }

            return candidates.ToArray();
        }

        /// <summary>
        /// Pulls the cast target slightly back from the pool center toward the player. This keeps the
        /// bobber in open water while still landing close enough to the pool to fish it.
        /// </summary>
        public static Position GetPoolCastTarget(Position playerPosition, Position poolPosition, float insetFromPool)
        {
            var dx = playerPosition.X - poolPosition.X;
            var dy = playerPosition.Y - poolPosition.Y;
            var planarDistance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (planarDistance <= 0.01f || insetFromPool <= 0f)
                return new Position(poolPosition.X, poolPosition.Y, poolPosition.Z);

            var scale = MathF.Min(insetFromPool, planarDistance) / planarDistance;
            return new Position(
                poolPosition.X + (dx * scale),
                poolPosition.Y + (dy * scale),
                poolPosition.Z);
        }

        private static float GetPlanarTargetDistance(Position playerPosition, Position poolPosition, float desiredDistance)
        {
            var verticalDelta = playerPosition.Z - poolPosition.Z;
            if (MathF.Abs(verticalDelta) > 0.01f && desiredDistance > MathF.Abs(verticalDelta))
                return MathF.Sqrt((desiredDistance * desiredDistance) - (verticalDelta * verticalDelta));

            return desiredDistance;
        }

        /// <summary>
        /// Checks if any of the known fishing pole item IDs match the given item ID.
        /// </summary>
        public static bool IsFishingPole(uint itemId) =>
            itemId == FishingPole || itemId == StrongFishingPole ||
            itemId == BigIronFishingPole || itemId == DarkwoodFishingPole;
    }
}
