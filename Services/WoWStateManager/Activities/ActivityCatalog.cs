using System;
using System.Collections.Generic;
using GameData.Core.Models.Activities;

namespace WoWStateManager.Activities
{
    /// <summary>
    /// Compiled <see cref="ActivityDefinition"/> catalog. Rows are pasted
    /// from <c>docs/Plan/Activities/_catalog_rows/*.md</c> in
    /// <c>00_INDEX.md</c> order (S0.3). 86 rows total.
    /// </summary>
    public sealed class ActivityCatalog : IActivityCatalog
    {
        /// <summary>
        /// Bumped whenever the compiled catalog shape changes. Phase 0
        /// initial value is 1.
        /// </summary>
        public const int CatalogVersionConst = 1;

        private static readonly IReadOnlyList<ActivityDefinition> _all = BuildAll();

        private static readonly IReadOnlyDictionary<string, ActivityDefinition> _byId = BuildIndex(_all);

        public int CatalogVersion => CatalogVersionConst;

        public IReadOnlyList<ActivityDefinition> All => _all;

        public bool TryGetById(string id, out ActivityDefinition? def)
        {
            if (id == null)
            {
                def = null;
                return false;
            }

            if (_byId.TryGetValue(id, out var hit))
            {
                def = hit;
                return true;
            }

            def = null;
            return false;
        }

        private static IReadOnlyDictionary<string, ActivityDefinition> BuildIndex(
            IReadOnlyList<ActivityDefinition> rows)
        {
            var dict = new Dictionary<string, ActivityDefinition>(rows.Count, StringComparer.Ordinal);
            foreach (var row in rows)
            {
                // Catalog test (Spec/04 invariant 1) asserts unique Ids; this
                // throw is a defensive safety net for the singleton boot.
                dict.Add(row.Id, row);
            }
            return dict;
        }

        private static IReadOnlyList<ActivityDefinition> BuildAll()
        {
            return new ActivityDefinition[]
            {
                // ---- Shard 1: _catalog_rows/01_questing_part1.md (16 rows) ----
                // -- Starter questing (6) --
                ActivityCatalogRows.QuestStarterElwynnForest,
                ActivityCatalogRows.QuestStarterDunMorogh,
                ActivityCatalogRows.QuestStarterTeldrassil,
                ActivityCatalogRows.QuestStarterDurotar,
                ActivityCatalogRows.QuestStarterTirisfalGlades,
                ActivityCatalogRows.QuestStarterMulgore,
                // -- Zone questing part 1 (10) --
                ActivityCatalogRows.QuestZoneWestfall,
                ActivityCatalogRows.QuestZoneLochModan,
                ActivityCatalogRows.QuestZoneDarkshore,
                ActivityCatalogRows.QuestZoneSilverpineForest,
                ActivityCatalogRows.QuestZoneTheBarrens,
                ActivityCatalogRows.QuestZoneRedridgeMountains,
                ActivityCatalogRows.QuestZoneAshenvale,
                ActivityCatalogRows.QuestZoneDuskwood,
                ActivityCatalogRows.QuestZoneWetlands,
                ActivityCatalogRows.QuestZoneHillsbradFoothills,

                // ---- Shard 2: _catalog_rows/02_questing_part2.md (19 rows) ----
                ActivityCatalogRows.QuestZoneStonetalonMountains,
                ActivityCatalogRows.QuestZoneThousandNeedles,
                ActivityCatalogRows.QuestZoneDesolace,
                ActivityCatalogRows.QuestZoneArathiHighlands,
                ActivityCatalogRows.QuestZoneStranglethornVale,
                ActivityCatalogRows.QuestZoneDustwallowMarsh,
                ActivityCatalogRows.QuestZoneBadlands,
                ActivityCatalogRows.QuestZoneTanaris,
                ActivityCatalogRows.QuestZoneFeralas,
                ActivityCatalogRows.QuestZoneSearingGorge,
                ActivityCatalogRows.QuestZoneAzshara,
                ActivityCatalogRows.QuestZoneTheHinterlands,
                ActivityCatalogRows.QuestZoneFelwood,
                ActivityCatalogRows.QuestZoneUngoroCrater,
                ActivityCatalogRows.QuestZoneWesternPlaguelands,
                ActivityCatalogRows.QuestZoneEasternPlaguelands,
                ActivityCatalogRows.QuestZoneBurningSteppes,
                ActivityCatalogRows.QuestZoneWinterspring,
                ActivityCatalogRows.QuestZoneSilithus,

                // ---- Shard 3: _catalog_rows/03_dungeons.md (21 rows) ----
                ActivityCatalogRows.DungeonRagefireChasm,
                ActivityCatalogRows.DungeonWailingCaverns,
                ActivityCatalogRows.DungeonDeadmines,
                ActivityCatalogRows.DungeonShadowfangKeep,
                ActivityCatalogRows.DungeonBlackfathomDeeps,
                ActivityCatalogRows.DungeonRazorfenKraul,
                ActivityCatalogRows.DungeonGnomeregan,
                ActivityCatalogRows.DungeonRazorfenDowns,
                ActivityCatalogRows.DungeonUldaman,
                ActivityCatalogRows.DungeonZulFarrak,
                ActivityCatalogRows.DungeonMaraudon,
                ActivityCatalogRows.DungeonSunkenTemple,
                ActivityCatalogRows.DungeonBlackrockDepths,
                ActivityCatalogRows.DungeonLowerBlackrockSpire,
                ActivityCatalogRows.DungeonUpperBlackrockSpire,
                ActivityCatalogRows.DungeonDireMaulEast,
                ActivityCatalogRows.DungeonDireMaulWest,
                ActivityCatalogRows.DungeonDireMaulNorth,
                ActivityCatalogRows.DungeonScholomance,
                ActivityCatalogRows.DungeonStratholmeUndead,
                ActivityCatalogRows.DungeonStratholmeLive,

                // ---- Shard 4: _catalog_rows/04_raids_bg_attune.md (15 rows) ----
                ActivityCatalogRows.RaidZg,
                ActivityCatalogRows.RaidAq20,
                ActivityCatalogRows.RaidMc,
                ActivityCatalogRows.RaidOnyxia,
                ActivityCatalogRows.RaidBwl,
                ActivityCatalogRows.RaidAq40,
                ActivityCatalogRows.RaidNaxx,
                ActivityCatalogRows.BgWsg,
                ActivityCatalogRows.BgAb,
                ActivityCatalogRows.BgAv,
                ActivityCatalogRows.AttuneMc,
                ActivityCatalogRows.AttuneOnyHorde,
                ActivityCatalogRows.AttuneOnyAlliance,
                ActivityCatalogRows.AttuneBwl,
                ActivityCatalogRows.AttuneNaxx,

                // ---- Shard 5: _catalog_rows/05_misc.md (15 rows) ----
                ActivityCatalogRows.ProfMiningRoute,
                ActivityCatalogRows.ProfHerbalismRoute,
                ActivityCatalogRows.ProfSkinningRoute,
                ActivityCatalogRows.ProfCityTrainerLoop,
                ActivityCatalogRows.EconAhRestock,
                ActivityCatalogRows.EconVendorLoop,
                ActivityCatalogRows.RepTimbermawHold,
                ActivityCatalogRows.RepArgentDawn,
                ActivityCatalogRows.RepCenarionCircle,
                ActivityCatalogRows.RepThoriumBrotherhood,
                ActivityCatalogRows.RepZandalarTribe,
                ActivityCatalogRows.EventStvFishingExtravaganza,
                ActivityCatalogRows.BossAzuregos,
                ActivityCatalogRows.BossKazzak,
                ActivityCatalogRows.BossEmeraldDragons,
            };
        }
    }
}
