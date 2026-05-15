using System.Collections.Generic;
using System.Linq;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// Activity catalog the Config editor pulls from. The first pass covers
    /// the entire vanilla WWoW group-content surface: 21 dungeons, 7 raids,
    /// and battleground brackets (WSG 10-19/20-29/30-39/40-49/50-59/60,
    /// AB 20-29/30-39/40-49/50-59/60, AV 51-60). Level ranges, faction
    /// policies, and attunement requirements are taken from
    /// <c>docs/Plan/Activities/_catalog_rows/03_dungeons.md</c> and
    /// <c>04_raids_bg_attune.md</c>.
    ///
    /// Each Activity carries a <c>StateChangeGoal</c> describing the
    /// character-state change it produces (leveling, honor, faction rep,
    /// tier-set loot, attunement progression, etc.) so the editor user can
    /// see at-a-glance why a config slot exists.
    ///
    /// PHASE 2: swap this for live binding to the StateManager
    /// <c>WoWStateManager.Activities.ActivityCatalog</c> (the canonical
    /// 86-row source). Today the rows are hand-mirrored to keep the UI
    /// independent of the StateManager assembly.
    /// </summary>
    public sealed class ActivityCatalogService
    {
        public IReadOnlyList<ActivityTemplate> Templates { get; }

        public ActivityCatalogService()
        {
            var list = new List<ActivityTemplate>();
            SeedAcquisitions(list);
            SeedBattlegrounds(list);
            SeedDungeons(list);
            SeedRaids(list);
            Templates = list;
        }

        public ActivityModel Instantiate(ActivityTemplate template)
        {
            // Activity slots are run by a single faction; templates that say
            // Either default to Alliance on instantiate. User flips via dropdown.
            var slotFaction = template.FactionRestriction == Faction.Either
                ? Faction.Alliance
                : template.FactionRestriction;

            var activity = new ActivityModel
            {
                ActivityId = template.Id,
                DisplayName = template.DisplayName,
                Family = template.Family,
                Location = template.Location,
                MinPlayers = template.MinPlayers,
                MaxPlayers = template.MaxPlayers,
                LevelMin = template.LevelMin,
                LevelMax = template.LevelMax,
                FactionRestriction = slotFaction,
                StateChangeGoal = template.StateChangeGoal,
            };

            foreach (var att in template.AttunementRequirements)
                activity.AttunementRequirements.Add(att);

            foreach (var p in template.DefaultParameters)
            {
                var copy = new ActivityParameter
                {
                    Key = p.Key,
                    Value = p.Value,
                    Description = p.Description,
                    IsRequired = p.IsRequired,
                    SearchKind = p.SearchKind,
                };
                if (p.Choices.Count > 0)
                    copy.SetChoices(p.Choices);
                activity.Parameters.Add(copy);
            }

            return activity;
        }

        // -------- Choice value sets (single source of truth) ---------

        private static readonly string[] FactionChoices = ["Alliance", "Horde"];
        private static readonly string[] StandingChoices = ["Neutral", "Friendly", "Honored", "Revered", "Exalted"];

        /// <summary>WoW 1.12.1-accurate reputation methods. Display-spaced names so
        /// dropdown rows read as English; runtime can match by exact string.</summary>
        private static readonly string[] RepMethodChoices =
            ["Item Turn-In",         // Scourgestones, Encrypted Twilight Texts, Lava Belt, Qiraji Insignia
             "Raid Mob Kills",       // MC trash for Hydraxian, AQ40 trash for Brood of Nozdormu
             "Dungeon Mob Kills",    // Strat/Scholo for Argent Dawn, BRD for Thorium Brotherhood
             "Quest Chain",          // Tribute Run, Wintersaber dailies, Cenarion Silithus chain
             "World Mob Kills",      // Timbermaw Furbolgs, Hydraxian elementals in Silithus
             "BG Victories"];        // Stormpike/Frostwolf, Silverwing/Warsong, League/Defilers

        /// <summary>Major reputation factions reachable for grind in WoW 1.12.1.</summary>
        private static readonly string[] ReputationFactionChoices =
            ["Argent Dawn",
             "Cenarion Circle",
             "Brood of Nozdormu",
             "Hydraxian Waterlords",
             "Thorium Brotherhood",
             "Timbermaw Hold",
             "Wintersaber Trainers",
             "Shen'dralar",
             "Stormpike Guard",
             "Frostwolf Clan",
             "Silverwing Sentinels",
             "Warsong Outriders",
             "League of Arathor",
             "The Defilers",
             "Gelkis Clan Centaur",
             "Magram Clan Centaur",
             "Bloodsail Buccaneers",
             "Steamwheedle Cartel",
             "Zandalar Tribe"];

        /// <summary>1.12.1 trainable skills. Riding is excluded — it's purchased (75g/1000g),
        /// not skill-leveled. Weapon and defense skills are passively trained by use.</summary>
        private static readonly string[] SkillChoices =
            ["Fishing", "Cooking", "FirstAid",
             "Mining", "Herbalism", "Skinning",
             "Engineering", "Enchanting", "Alchemy",
             "Blacksmithing", "Leatherworking", "Tailoring",
             "Lockpicking"];

        private static readonly string[] SkillMethodChoices =
            ["GatherNodes",          // Mining/Herbalism via node-routing
             "VendorRecipeGrind",    // Buy reagents → craft skill-ups
             "TrainerCheckpoint",    // Train at 75/150/225, then practice up
             "MaterialFarm",         // Skinning mob-skin grinding
             "FishingRoute"];        // Fishing pools or coast

        // Leveling Method values are now Sub-Type templates; the array is kept
        // for legacy callers but Battlegrounding is removed (PvP doesn't reward
        // XP in 1.12 — BG XP wasn't introduced until later expansions).
        // ElitePartyGrind/Mixed dropped — ElitePartyGrind is a MobGrind variant.
        private static readonly string[] LevelingMethodChoices =
            ["MobGrind", "Questing", "Dungeon"];

        // Per-bracket dungeon choices for the Dungeon sub-type of Leveling.
        // No "Auto" entry — the dungeon list IS the picker; the coordinator
        // picks by level if the user leaves the param blank.
        private static readonly string[] DungeonChoicesBracket10_20 =
            ["Ragefire Chasm (13-18)", "The Deadmines (17-26)",
             "Wailing Caverns (17-24)", "Shadowfang Keep (18-25)"];

        private static readonly string[] DungeonChoicesBracket20_30 =
            ["The Stockade (22-30)", "Blackfathom Deeps (24-32)",
             "Scarlet Monastery — Graveyard (26-36)"];

        private static readonly string[] DungeonChoicesBracket30_40 =
            ["Gnomeregan (29-38)", "Razorfen Kraul (29-38)",
             "Scarlet Monastery — Library (29-39)",
             "Scarlet Monastery — Armory (32-42)",
             "Scarlet Monastery — Cathedral (34-45)",
             "Razorfen Downs (37-46)", "Uldaman (35-45)"];

        private static readonly string[] DungeonChoicesBracket40_50 =
            ["Zul'Farrak (44-54)", "Maraudon (45-55)",
             "Sunken Temple (50-60)", "Dire Maul — East (54-60)"];

        private static readonly string[] DungeonChoicesBracket50_60 =
            ["Blackrock Depths (52-60)",
             "Lower Blackrock Spire (55-60)",
             "Upper Blackrock Spire (58-60)",
             "Dire Maul — West (56-60)", "Dire Maul — North (58-60)",
             "Scholomance (58-60)",
             "Stratholme — Live (58-60)", "Stratholme — Undead (58-60)"];

        /// <summary>Vanilla WoW 1.12.1 zones partitioned by 10-level brackets. Each
        /// bracket's array drives a separate Zone-(min-max) parameter dropdown on the
        /// Leveling activity, so the user picks a different zone per bracket their
        /// level range passes through. Per-zone mob lookup is a future iteration
        /// (planned: query mangos.creature_template by map+area filtered for non-elite).</summary>
        private static readonly string[] ZoneChoicesBracket01_10 =
            ["Auto", "Elwynn Forest", "Dun Morogh", "Teldrassil",
             "Durotar", "Mulgore", "Tirisfal Glades"];

        private static readonly string[] ZoneChoicesBracket10_20 =
            ["Auto", "Westfall", "Loch Modan", "Darkshore",
             "The Barrens", "Silverpine Forest",
             "Redridge Mountains", "Duskwood",
             "Stonetalon Mountains", "Ashenvale"];

        private static readonly string[] ZoneChoicesBracket20_30 =
            ["Auto", "Wetlands", "Hillsbrad Foothills",
             "Thousand Needles", "Arathi Highlands",
             "Stranglethorn Vale (Grom'gol/Booty Bay south)",
             "Ashenvale (upper)", "Duskwood (upper)"];

        private static readonly string[] ZoneChoicesBracket30_40 =
            ["Auto", "Desolace",
             "Stranglethorn Vale (Nesingwary, Zuuldaia)",
             "Dustwallow Marsh", "Swamp of Sorrows",
             "Arathi Highlands (upper)", "Badlands"];

        private static readonly string[] ZoneChoicesBracket40_50 =
            ["Auto", "Searing Gorge", "Tanaris",
             "Feralas", "The Hinterlands", "Azshara",
             "Un'Goro Crater (lower)", "Blasted Lands (lower)"];

        private static readonly string[] ZoneChoicesBracket50_60 =
            ["Auto", "Burning Steppes", "Felwood",
             "Western Plaguelands", "Eastern Plaguelands",
             "Winterspring", "Silithus", "Blasted Lands",
             "Un'Goro Crater (upper)"];

        private static readonly string[] EarnGoldMethodChoices =
            ["UseProfessionSkills",  // Gathering professions → AH
             "TrashGrind",           // Vendor trash from mob kills
             "FishingGrind",         // Fishing + vendor / AH
             "GatherMaterials",      // Pure herb/ore routes for AH
             "CraftItems",           // Make sellables (potions, bandages, bags)
             "AuctionFlip",          // Buy underpriced → resell
             "VendorItems",          // Greys/whites/greens to vendor
             "RepeatableQuests",     // Repeatable gold-payout quests
             "DungeonFarm",          // Dungeon green/blue runs for vendor
             "RaidLootFarm",         // Raid green/recipe runs
             "EliteMobFarm",         // Named elite circuits
             "EventFarm"];           // Holiday / world-event payouts

        /// <summary>How an item is obtained — drives item-source-aware UI.</summary>
        private static readonly string[] ItemSourceChoices =
            ["QuestReward",
             "RaidBossDrop",
             "DungeonBossDrop",
             "EliteMobDrop",
             "WorldDrop",
             "VendorPurchase",
             "Crafted",
             "ReputationReward",
             "PvPHonorReward",
             "ProfessionDiscovery"];

        /// <summary>How a skill or spell is obtained.</summary>
        private static readonly string[] SkillSpellSourceChoices =
            ["ClassTrainer",         // Bought from class trainer at the right level
             "ProfessionTrainer",    // Bought from a profession trainer (Cooking, Fishing, etc.)
             "TalentPoint",          // Earned via level-up talent points
             "QuestReward",          // Awarded by quest (e.g. Druid swim form quest)
             "SpellbookDrop",        // Dungeon/raid drop spellbook (rare in 1.12.1)
             "RacialPassive"];       // Innate racial (Forsaken Cannibalize, etc.)

        private static readonly string[] WsgBracketChoices =
            ["10-19", "20-29", "30-39", "40-49", "50-59", "60-60"];
        private static readonly string[] AbBracketChoices =
            ["20-29", "30-39", "40-49", "50-59", "60-60"];
        private static readonly string[] AvBracketChoices = ["51-60"];
        // BG strategies — drawn from community / Defias guides for each BG.
        // Names are display-formatted with spaces so the dropdown reads as English.
        private static readonly string[] WsgStrategyChoices =
            ["Flag Rush",            // straight to enemy flag room, grab & run
             "Mid-Field Control",    // contest midfield, deny enemy carriers
             "Stealth Cap",          // rogue/druid sneak pulls
             "Defense Heavy",        // hold friendly flag room
             "Extend Friendly EFC"]; // pocket-heal carrier outside FR

        // AB strategies. Stables is the Alliance home node; Farm is the Horde
        // home node. Choices are labeled with the home-side so it's obvious
        // which to pick for each Faction; the activity's Faction also makes the
        // wrong-side option a no-op at runtime.
        private static readonly string[] AbStrategyChoices =
            ["Stables / LM / BS (Alliance 3-Cap)",
             "Farm / LM / BS (Horde 3-Cap)",
             "4-Cap Push",
             "5-Cap Sweep",
             "Counter-Cap Defense"];

        private static readonly string[] AvStrategyChoices =
            ["Zerg General",         // straight to enemy faction boss
             "Full Clear",           // capture all towers/graveyards first
             "Defensive Turtle"];    // defend in own base + bunkers

        // Auction House manipulation tactics. Common across vanilla goldmaker
        // guides (Crouching Tiger, Hidden Goblin / Tradeskillmaster classic).
        // The runtime drives buy/sell thresholds from rolling market history;
        // the strategy picks the BEHAVIOR pattern the coordinator applies.
        private static readonly string[] AhFlipStrategyChoices =
            ["Undercut Snipe",            // list at -1c below current lowest; fast turnover, low margin
             "Buyout & Relist",           // sweep listings priced below market, relist at market mean
             "Reset (Walling)",           // buy every listing of an item, relist much higher to set a new floor
             "Restock & Cycle",           // keep N units always-listed on high-volume items; replenish as they sell
             "Material Arbitrage",        // buy raw mats, list crafted output (potions, bandages, bags)
             "Cross-Faction (Neutral AH)",// Booty Bay / Gadgetzan / Everlook price diff between factions
             "Cooldown Resale",           // Transmute / shard cooldown mats; daily / weekly cycle
             "Bulk Repackaging",          // buy large stacks, split into smaller (small stacks command per-unit premium)
             "Peak-Time Pricing",         // list during raid prep (Fri/Sat); de-list off-peak
             "Recipe Sniping",            // buy underpriced recipes (high % markup on rare drops)
             "Twink Gear Resale"];        // BoE blues at popular bracket slots (19/29/39/49/59)

        private static ActivityParameter Param(string key, string value, bool required, string? description,
            string[]? choices = null, SearchKind searchKind = SearchKind.None)
        {
            var p = new ActivityParameter
            {
                Key = key,
                Value = value,
                IsRequired = required,
                Description = description,
                SearchKind = searchKind,
            };
            if (choices != null)
                p.SetChoices(choices);
            return p;
        }

        public ActivityTemplate? FindById(string activityId)
        {
            foreach (var t in Templates)
                if (t.Id == activityId)
                    return t;
            return null;
        }

        /// <summary>
        /// Group templates by <see cref="ActivityTemplate.Family"/> for the
        /// two-stage Family → Instance pickers in the Activity Detail header.
        /// Ordering: Battleground / Dungeon/Raid first (largest instance
        /// counts), then the acquisition families.
        /// </summary>
        public IReadOnlyList<ActivityFamilyGroup> GetFamilies()
        {
            var order = new[] { "Battleground", "Dungeon/Raid", "Leveling", "Reputation",
                "Skill", "Earn Gold", "Acquisition", "Questing" };

            return Templates
                .GroupBy(t => t.Family)
                .OrderBy(g => System.Array.IndexOf(order, g.Key) is int i && i >= 0 ? i : int.MaxValue)
                .ThenBy(g => g.Key)
                .Select(g => new ActivityFamilyGroup(g.Key, g.OrderBy(t => t.DisplayName).ToList()))
                .ToList();
        }

        // -------- Acquisitions (character-state-change goals) ---------
        //
        // These templates aren't tied to a specific in-game encounter. Each one
        // names a class of state change (level gain, rep climb, item drop,
        // skill increase, gold accumulation, mount acquisition, quest
        // completion) and exposes the parameters that scope the work. The
        // coordinator running the activity reads the parameters and picks the
        // actual zone / method / target.
        //
        // All default to broad level 1-60 range and "Either" faction; the
        // user narrows via parameters (Zone / Faction-specific reps, etc.).

        private static void SeedAcquisitions(List<ActivityTemplate> list)
        {
            // Leveling: Method IS the Sub-Type. Each sub-type has its own param set.
            SeedLevelingSubTypes(list);

            // Reputation: each grindable faction becomes a Sub-Type with the
            // methods that actually work for that faction in 1.12.1.
            SeedReputationSubTypes(list);

            // Skill: one Sub-Type per trainable skill. Methods are filtered to
            // what actually levels THAT skill in 1.12.1.
            SeedSkillSubTypes(list);

            // Earn Gold: the Method IS the Sub-Type. Each sub-type carries the
            // parameters that matter for its loop (AH flipping vs. mob grinding
            // are very different surfaces). All share the "TargetGold" stop
            // condition and the "Earn Gold" family.
            SeedEarnGoldSubTypes(list);

            list.Add(new(
                Id: "acquire.item",
                DisplayName: "Acquire Item",
                Family: "Acquisition",
                Location: "(varies by source)",
                MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                LevelMin: 1, LevelMax: 60,
                FactionRestriction: Faction.Either,
                StateChangeGoal: "Obtain a specific item for one or more characters (named drop / quest reward / vendor / crafted)",
                AttunementRequirements: [],
                DefaultParameters:
                [
                    // The item's source is data-driven from item_template / loot tables.
                    // No user-pickable Source param — if the item drops in a dungeon
                    // the runtime farms the dungeon; if it's crafted, the runtime
                    // crafts it (or sources via AH / character-request if implemented).
                    Param("TargetItem", "Carrot on a Stick", required: true,
                          "Type to search item_template by name (runtime resolves source)",
                          searchKind: SearchKind.Item)
                ],
                Objectives: AcquireObjectives("named item", "drop / vendor / quest / craft")));

            // Acquire Skill/Spell — separate from Item since the runtime path is different
            // (talent points, trainers, quest chains, racial passives).
            list.Add(new(
                Id: "acquire.skill-spell",
                DisplayName: "Acquire Skill / Spell",
                Family: "Acquisition",
                Location: "(varies by source)",
                MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                LevelMin: 1, LevelMax: 60,
                FactionRestriction: Faction.Either,
                StateChangeGoal: "Add a specific spell, ability, or skill rank to one or more characters (level-up trainer / talent / quest reward / racial)",
                AttunementRequirements: [],
                DefaultParameters:
                [
                    // Source is data-driven: most spells are bought from a class
                    // trainer (runtime picks "Closest" by default and falls back to
                    // a quest-reward path when the spell is gated by a quest). For
                    // class-locked spells the runtime marks the activity COMPLETE
                    // once eligible characters acquire it — non-eligible characters
                    // are tagged as "completed as much as possible".
                    Param("TargetSpell", "Frostbolt (Rank 11)", required: true,
                          "Type to search by spell name OR by class name (e.g. 'Mage' returns all Mage spells). Runtime resolves trainer or quest.",
                          searchKind: SearchKind.Spell)
                ],
                Objectives: AcquireObjectives("skill/spell", "trainer / talent / quest")));

            list.Add(new(
                Id: "complete.quest",
                DisplayName: "Complete Quest",
                Family: "Questing",
                Location: "(varies by quest)",
                MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                LevelMin: 1, LevelMax: 60,
                FactionRestriction: Faction.Either,
                StateChangeGoal: "Complete a specific quest (chain head or one-off) and turn in for XP / reputation / item / reward",
                AttunementRequirements: [],
                DefaultParameters:
                [
                    Param("TargetQuestId", "0", required: true,
                          "Quest ID (click 🔍 to search quest_template by title or entry)",
                          searchKind: SearchKind.Quest),
                    Param("TargetQuestName", "(name)", required: false,
                          "Display name (optional reference label)"),
                    Param("ChainName", "", required: false,
                          "Optional quest-chain identifier (e.g. 'Onyxia Attunement', 'Seal of Ascension')"),
                    Param("RewardChoice", "", required: false,
                          "Reward index to select on turn-in (empty = pick best by class)")
                ],
                Objectives: AcquireObjectives("quest completion", "accept → objectives → turn in")));

            // Mount is a skill (apprentice/journeyman/expert riding is a skill;
            // the mount-summoning ability is a Spell). The catalog covers it via
            // acquire.skill-spell for the riding/mount-summon ability, plus
            // acquire.item for the mount-trinket itemId (e.g. Deathcharger's Reins).
        }

        /// <summary>
        /// One Skill template per skill, with skill-appropriate Method choices.
        /// Cooking can't be levelled by gathering nodes; Mining/Herbalism can't
        /// be levelled by crafting recipes; Weapon skills are leveled passively
        /// by attacking mobs at <c>mob_level ≈ weapon_skill / 5 ± 2</c>.
        /// </summary>
        private static void SeedSkillSubTypes(List<ActivityTemplate> list)
        {
            ActivityTemplate Skl(string idSuffix, string display, Faction faction,
                int minLevel, int cap, string goal, IReadOnlyList<ActivityParameter> extraParams) =>
                new(
                    Id: $"acquire.skill.{idSuffix}",
                    DisplayName: display,
                    Family: "Skill",
                    Location: display,
                    MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                    LevelMin: minLevel, LevelMax: 60,
                    FactionRestriction: faction,
                    StateChangeGoal: goal,
                    AttunementRequirements: [],
                    DefaultParameters: [
                        Param("Target Level", cap.ToString(), true,
                              $"Stop once skill ≥ this value (cap {cap} in 1.12.1)"),
                        .. extraParams
                    ],
                    Objectives: AcquireObjectives("skill level", display.ToLowerInvariant() + " loop"));

            // Method param is implicit in the skill — Mining means mine nodes,
            // Cooking means cook recipes, Weapon Skill means attack mobs at
            // appropriate level. Trainer visits happen automatically at
            // 75/150/225/300 caps when Target Level > current.

            // ── Gathering professions (node-based) ──
            list.Add(Skl("mining", "Mining", Faction.Either, 5, 300,
                "Raise Mining by mining ore veins. Trainer visits auto at 75/150/225/300.",
                []));
            list.Add(Skl("herbalism", "Herbalism", Faction.Either, 5, 300,
                "Raise Herbalism by gathering herb nodes",
                []));
            list.Add(Skl("skinning", "Skinning", Faction.Either, 5, 300,
                "Raise Skinning by skinning beast corpses (level-appropriate zones)",
                []));
            list.Add(Skl("fishing", "Fishing", Faction.Either, 5, 300,
                "Raise Fishing by casting at pools (best skill-up) and open water",
                []));

            // ── Crafting professions (recipe-based) ──
            list.Add(Skl("cooking", "Cooking", Faction.Either, 5, 300,
                "Raise Cooking by cooking recipes (purchased / quested / fished). Cannot be raised by gathering.",
                []));
            list.Add(Skl("first-aid", "First Aid", Faction.Either, 5, 300,
                "Raise First Aid by making bandages from looted cloth. Quest 'Triage' unlocks rank past 225.",
                []));
            list.Add(Skl("engineering", "Engineering", Faction.Either, 5, 300,
                "Raise Engineering by crafting recipes. Pick Gnomish or Goblin spec at 200.",
                []));
            list.Add(Skl("enchanting", "Enchanting", Faction.Either, 5, 300,
                "Raise Enchanting by disenchanting greens and applying enchants",
                []));
            list.Add(Skl("alchemy", "Alchemy", Faction.Either, 5, 300,
                "Raise Alchemy by brewing potions, elixirs and flasks",
                []));
            list.Add(Skl("blacksmithing", "Blacksmithing", Faction.Either, 5, 300,
                "Raise Blacksmithing by smithing weapons/armor. Armorsmith/Weaponsmith spec at 200.",
                []));
            list.Add(Skl("leatherworking", "Leatherworking", Faction.Either, 5, 300,
                "Raise Leatherworking. Tribal/Elemental/Dragonscale spec at 225.",
                []));
            list.Add(Skl("tailoring", "Tailoring", Faction.Either, 5, 300,
                "Raise Tailoring by crafting cloth recipes (bags, robes)",
                []));
            list.Add(Skl("lockpicking", "Lockpicking (Rogue only)", Faction.Either, 16, 300,
                "Rogue-only: raise Lockpicking by picking locked boxes and doors at level-appropriate locations",
                []));

            // ── Weapon skills (passive-by-use, formula-driven) ──
            list.Add(Skl("weapon", "Weapon Skill", Faction.Either, 1, 300,
                "Raise a weapon skill by attacking mobs whose level matches mob_level ≈ weapon_skill / 5 ± 2. Runtime picks targets accordingly.",
                [ Param("WeaponType", "One-Handed Sword", true,
                        "Weapon to level — runtime checks the character is wielding it",
                        ["One-Handed Sword", "Two-Handed Sword",
                         "One-Handed Axe", "Two-Handed Axe",
                         "One-Handed Mace", "Two-Handed Mace",
                         "Polearm", "Dagger", "Fist Weapon", "Staff",
                         "Bow", "Crossbow", "Gun", "Thrown",
                         "Wand", "Defense", "Unarmed"]) ]));
        }

        /// <summary>
        /// One Reputation template per grindable faction. Each has its own
        /// Method choices because factions don't share grind paths in 1.12.1:
        /// Argent Dawn → Scourgestones + Strat/Scholo + Naxx;
        /// Cenarion Circle → Twilight Texts + AQ20 + Silithus quests;
        /// Hydraxian → MC trash + Aqual Quintessence;
        /// BG reps → BG victories only; etc.
        /// </summary>
        private static void SeedReputationSubTypes(List<ActivityTemplate> list)
        {
            ActivityTemplate Rep(string id, string display, Faction faction,
                int minLevel, string goal, string[] validMethods, string defaultMethod) =>
                new(
                    Id: $"rep.{id}",
                    DisplayName: display,
                    Family: "Reputation",
                    Location: display,
                    MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                    LevelMin: minLevel, LevelMax: 60,
                    FactionRestriction: faction,
                    StateChangeGoal: goal,
                    AttunementRequirements: [],
                    DefaultParameters: [
                        Param("TargetStanding", "Exalted", true,
                              "Friendly / Honored / Revered / Exalted", StandingChoices),
                        Param("Method", defaultMethod, true,
                              "Grind method available for this faction in 1.12.1",
                              validMethods)
                    ],
                    Objectives: AcquireObjectives("faction standing", display.ToLowerInvariant() + " grind"));

            // PvE end-game reps
            list.Add(Rep("argent-dawn", "Argent Dawn", Faction.Either, 55,
                "Scourgestones + Strat/Scholo runs unlock the Naxx attunement and T0.5 quests",
                ["Item Turn-In", "Dungeon Mob Kills", "Raid Mob Kills"], "Item Turn-In"));

            list.Add(Rep("cenarion-circle", "Cenarion Circle", Faction.Either, 55,
                "Encrypted Twilight Texts + AQ20 + Silithus questline (Cenarion War Hippogryph at Exalted)",
                ["Item Turn-In", "Raid Mob Kills", "Quest Chain"], "Item Turn-In"));

            list.Add(Rep("brood-of-nozdormu", "Brood of Nozdormu", Faction.Either, 60,
                "Qiraji Insignia + AQ40 trash/bosses (Reins of the Black Qiraji Resonating Crystal at Exalted)",
                ["Item Turn-In", "Raid Mob Kills"], "Raid Mob Kills"));

            list.Add(Rep("hydraxian-waterlords", "Hydraxian Waterlords", Faction.Either, 55,
                "MC trash kills + Aqual Quintessence unlock to douse runes",
                ["Raid Mob Kills", "Quest Chain", "World Mob Kills"], "Raid Mob Kills"));

            list.Add(Rep("thorium-brotherhood", "Thorium Brotherhood", Faction.Either, 55,
                "Lava Belt + Volatile Rum recipe turn-ins + BRD Thaurissan boss",
                ["Item Turn-In", "Dungeon Mob Kills"], "Item Turn-In"));

            list.Add(Rep("timbermaw-hold", "Timbermaw Hold", Faction.Either, 48,
                "Deadwood Furbolg (Felwood) + Winterfall Furbolg (Winterspring) kills + necklace turn-ins",
                ["World Mob Kills", "Item Turn-In"], "World Mob Kills"));

            list.Add(Rep("wintersaber-trainers", "Wintersaber Trainers", Faction.Alliance, 50,
                "Daily Wintersaber quests in Winterspring (Reins of the Winterspring Frostsaber at Exalted)",
                ["Quest Chain"], "Quest Chain"));

            list.Add(Rep("shen-dralar", "Shen'dralar", Faction.Either, 56,
                "DM West book turn-ins (Librams of Focus/Protection/Rapidity/Resilience/Voracity) + Pristine Hide",
                ["Item Turn-In", "Dungeon Mob Kills"], "Item Turn-In"));

            list.Add(Rep("zandalar-tribe", "Zandalar Tribe", Faction.Either, 60,
                "ZG runs + Bijou/Coin/Voodoo Doll turn-ins at Yojamba Isle",
                ["Raid Mob Kills", "Item Turn-In"], "Item Turn-In"));

            list.Add(Rep("gelkis-clan-centaur", "Gelkis Clan Centaur", Faction.Either, 30,
                "Kill Magram Centaur in Desolace + Gelkis quest chain",
                ["World Mob Kills", "Quest Chain"], "World Mob Kills"));

            list.Add(Rep("magram-clan-centaur", "Magram Clan Centaur", Faction.Either, 30,
                "Kill Gelkis Centaur in Desolace + Magram quest chain",
                ["World Mob Kills", "Quest Chain"], "World Mob Kills"));

            list.Add(Rep("bloodsail-buccaneers", "Bloodsail Buccaneers", Faction.Either, 40,
                "Kill Booty Bay / Ratchet / Everlook / Gadgetzan goblins (no Steamwheedle access at Hated)",
                ["World Mob Kills"], "World Mob Kills"));

            list.Add(Rep("steamwheedle-cartel", "Steamwheedle Cartel", Faction.Either, 40,
                "Kill Bloodsail Buccaneers (STV coast, Northern Stranglethorn pirates)",
                ["World Mob Kills"], "World Mob Kills"));

            // Battleground reps — only via BG victories
            list.Add(Rep("stormpike-guard", "Stormpike Guard", Faction.Alliance, 51,
                "Win Alterac Valley as Alliance (Stormpike Battlecharger mount at Exalted)",
                ["BG Victories"], "BG Victories"));
            list.Add(Rep("frostwolf-clan", "Frostwolf Clan", Faction.Horde, 51,
                "Win Alterac Valley as Horde (Frostwolf Howler mount at Exalted)",
                ["BG Victories"], "BG Victories"));
            list.Add(Rep("silverwing-sentinels", "Silverwing Sentinels", Faction.Alliance, 10,
                "Win Warsong Gulch as Alliance",
                ["BG Victories"], "BG Victories"));
            list.Add(Rep("warsong-outriders", "Warsong Outriders", Faction.Horde, 10,
                "Win Warsong Gulch as Horde",
                ["BG Victories"], "BG Victories"));
            list.Add(Rep("league-of-arathor", "League of Arathor", Faction.Alliance, 20,
                "Win Arathi Basin as Alliance",
                ["BG Victories"], "BG Victories"));
            list.Add(Rep("the-defilers", "The Defilers", Faction.Horde, 20,
                "Win Arathi Basin as Horde",
                ["BG Victories"], "BG Victories"));
        }

        /// <summary>
        /// Leveling sub-types. Each method becomes its own template under
        /// Family="Leveling" with method-specific parameters.
        /// Battlegrounding is intentionally absent — pre-2.0 BGs don't reward XP.
        /// </summary>
        private static void SeedLevelingSubTypes(List<ActivityTemplate> list)
        {
            ActivityTemplate Lvl(string idSuffix, string display, string goal,
                IReadOnlyList<ActivityParameter> extraParams) =>
                new(
                    Id: $"acquire.levels.{idSuffix}",
                    DisplayName: display,
                    Family: "Leveling",
                    Location: "(varies by zone/dungeon)",
                    MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                    LevelMin: 1, LevelMax: 60,
                    FactionRestriction: Faction.Either,
                    StateChangeGoal: goal,
                    AttunementRequirements: [],
                    DefaultParameters: [
                        Param("LevelStart", "1", required: true, "Current level to begin from"),
                        Param("LevelEnd", "60", required: true, "Stop once characters reach this level"),
                        .. extraParams
                    ],
                    Objectives: AcquireObjectives("character level", display.ToLowerInvariant()));

            list.Add(Lvl("mob-grind", "Mob Grind",
                "Increment level by killing mobs in a chosen zone per bracket",
                [
                    Param("Zone (1-10)", "Auto", false, "Starter zone for 1-10", ZoneChoicesBracket01_10),
                    Param("Zone (10-20)", "Auto", false, "Zone for 10-20", ZoneChoicesBracket10_20),
                    Param("Zone (20-30)", "Auto", false, "Zone for 20-30", ZoneChoicesBracket20_30),
                    Param("Zone (30-40)", "Auto", false, "Zone for 30-40", ZoneChoicesBracket30_40),
                    Param("Zone (40-50)", "Auto", false, "Zone for 40-50", ZoneChoicesBracket40_50),
                    Param("Zone (50-60)", "Auto", false, "Zone for 50-60", ZoneChoicesBracket50_60)
                ]));

            list.Add(Lvl("questing", "Questing",
                "Increment level by running quests in a chosen zone per bracket",
                [
                    Param("Zone (1-10)", "Auto", false, "Starter zone for 1-10", ZoneChoicesBracket01_10),
                    Param("Zone (10-20)", "Auto", false, "Zone for 10-20", ZoneChoicesBracket10_20),
                    Param("Zone (20-30)", "Auto", false, "Zone for 20-30", ZoneChoicesBracket20_30),
                    Param("Zone (30-40)", "Auto", false, "Zone for 30-40", ZoneChoicesBracket30_40),
                    Param("Zone (40-50)", "Auto", false, "Zone for 40-50", ZoneChoicesBracket40_50),
                    Param("Zone (50-60)", "Auto", false, "Zone for 50-60", ZoneChoicesBracket50_60)
                ]));

            list.Add(Lvl("dungeon", "Dungeon",
                "Level by running dungeons. Coordinator auto-selects the right dungeon by current character bracket (RFC/Deadmines/WC/SFK at 10-20, BFD/Stockades/SM-Graveyard at 20-30, ...). No per-bracket overrides — the bracket-to-dungeon mapping is hard game data.",
                []));

            // ElitePartyGrind dropped — same as MobGrind in practice.
            // Mixed dropped — pick a single sub-type per Activity slot; if you
            // want multiple strategies, create multiple Activities.
        }

        /// <summary>
        /// One Earn Gold template per method. Picking a sub-type (Auction Flip,
        /// Mob Grind, Fishing, etc.) reshapes the parameter set to match the
        /// loop the coordinator will run.
        /// </summary>
        private static void SeedEarnGoldSubTypes(List<ActivityTemplate> list)
        {
            ActivityTemplate Earn(string idSuffix, string display, string goal,
                IReadOnlyList<ActivityParameter> extraParams) =>
                new(
                    Id: $"earn.gold.{idSuffix}",
                    DisplayName: display,
                    Family: "Earn Gold",
                    Location: "(varies)",
                    MinPlayers: 1, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                    LevelMin: 1, LevelMax: 60,
                    FactionRestriction: Faction.Either,
                    StateChangeGoal: goal,
                    AttunementRequirements: [],
                    DefaultParameters: [
                        Param("TargetGold", "1000", required: true, "Target gold balance (in gold, not copper)"),
                        .. extraParams
                    ],
                    Objectives: AcquireObjectives("gold balance", display.ToLowerInvariant() + " loop"));

            list.Add(Earn("profession-skills", "Use Profession Skills",
                "Earn gold by selling profession output (potions, bags, enchants, glyphs)",
                [
                    Param("Skill", "Alchemy", required: true,
                          "Profession to drive the loop", SkillChoices),
                    Param("OutputItem", "Major Mana Potion", required: false,
                          "Concrete item to craft + sell (free-form; auto-resolve later from item_template)")
                ]));

            list.Add(Earn("trash-grind", "Trash Grind",
                "Kill packs of trash mobs and vendor the greys/whites/greens that drop",
                [
                    Param("FarmZone", "Eastern Plaguelands", required: true,
                          "Zone with profitable trash-density (Felwood / Winterspring / EPL / Burning Steppes / Silithus)",
                          ZoneChoicesBracket50_60)
                ]));

            list.Add(Earn("fishing-grind", "Fishing Grind",
                "Fish raw food and oils; vendor or AH the stacks",
                [
                    Param("FishingSpot", "Stranglethorn Vale (Booty Bay)", required: true,
                          "Named fishing spot (coastal nodes + Stonescale Eel / Oily Blackmouth schools)"),
                    Param("TargetCatch", "Stonescale Eel", required: false,
                          "Preferred catch (Stonescale Eel / Nightfin Snapper / Oily Blackmouth / vendor-trash)")
                ]));

            list.Add(Earn("gather-materials", "Gather Materials",
                "Run gathering routes (mining / herbalism / skinning) and AH the materials",
                [
                    Param("GatherSkill", "Mining", required: true,
                          "Mining / Herbalism / Skinning",
                          ["Mining", "Herbalism", "Skinning"]),
                    Param("RouteZone", "Un'Goro Crater", required: true,
                          "Zone for the gather route", ZoneChoicesBracket40_50),
                    Param("TargetNodeType", "Rich Thorium Vein", required: false,
                          "Preferred node tier (free-form; resolved against gameobject_template later)")
                ]));

            list.Add(Earn("craft-items", "Craft Items",
                "Buy raw mats → craft → list crafted item",
                [
                    Param("Skill", "Alchemy", required: true,
                          "Crafting profession", SkillChoices),
                    Param("RecipeName", "Major Mana Potion", required: true,
                          "Recipe display name (search the item_template for the crafted output)",
                          searchKind: SearchKind.Item),
                    Param("MaxMaterialCost", "5g", required: false,
                          "Don't buy mats above this per-stack price")
                ]));

            list.Add(Earn("auction-flip", "Auction Flip",
                "Manipulate AH prices using a chosen tactic. Exact buy/sell thresholds are coordinator state (market-history-driven), not user-tuned — too fragile to hardcode.",
                [
                    Param("Strategy", "Undercut Snipe", required: true,
                          "AH-manipulation tactic — runtime tracks market history and applies the chosen behavior",
                          AhFlipStrategyChoices)
                ]));

            // Vendor Items merged into Trash Grind — same loop ("kill stuff,
            // vendor what drops"), just a duplicate naming. Trash Grind covers it.

            list.Add(Earn("repeatable-quests", "Repeatable Quests",
                "Run repeatable quests that pay gold + items (Wintersaber dailies, Argent Dawn turn-ins, etc.)",
                [
                    Param("QuestId", "0", required: true,
                          "Repeatable quest ID (click 🔍 to search quest_template)",
                          searchKind: SearchKind.Quest),
                    Param("QuestGiverNpc", "", required: false,
                          "NPC who hands out the quest (free-form for now)")
                ]));

            list.Add(Earn("dungeon-farm", "Dungeon Farm (Green/Blue Vendor)",
                "Spam a dungeon for green/blue drops and vendor or AH them",
                [
                    Param("DungeonName", "Stratholme — Live", required: true,
                          "Dungeon to farm (use the Dungeon/Raid Type for a structured run)")
                ]));

            list.Add(Earn("raid-loot-farm", "Raid Loot Farm",
                "Re-clear raid trash on lockout reset for guaranteed greens/recipes/Bloodvine etc.",
                [
                    Param("RaidName", "Zul'Gurub", required: true,
                          "ZG (Bloodvine) / AQ20 / MC (trash recipes) / BWL (recipes)"),
                    Param("TargetItem", "Bloodvine", required: false,
                          "Specific target loot (free-form for now)")
                ]));

            list.Add(Earn("elite-mob-farm", "Elite Mob Farm",
                "Solo or duo named-elite circuits (named-mob drops + cloth + sometimes recipes)",
                [
                    Param("EliteName", "Doomguards (Burning Steppes patrol)", required: true,
                          "Named elite or patrol description"),
                    Param("FarmZone", "Burning Steppes", required: true,
                          "Zone of the elite circuit", ZoneChoicesBracket50_60)
                ]));

            list.Add(Earn("event-farm", "Event Farm",
                "Holiday / world-event payouts (Stranglethorn Fishing Extravaganza, Lunar Festival coins, etc.)",
                [
                    Param("EventName", "Stranglethorn Fishing Extravaganza", required: true,
                          "Named in-game event (Fishing Extravaganza / Lunar Festival / Winterveil / etc.)")
                ]));
        }

        private static IReadOnlyList<ObjectiveDefinition> AcquireObjectives(string targetLabel, string methodLabel) =>
        [
            new("read-goal", $"Read goal parameters",
                $"Parse the activity parameters into the target ({targetLabel}) and method ({methodLabel}).",
                Tasks:
                [
                    new("ReadParametersTask", "Coordinator",
                        "Coordinator pulls parameters off the Activity model.",
                        Actions: [ new("ReadParameter", "No wire message — local read") ])
                ]),
            new("travel-to-working-area", "Travel to working location",
                "Get every assigned character to the staging area for the chosen method.",
                Tasks:
                [
                    new("TravelToTask", "Travel",
                        "Pathfind to the working zone / NPC / node cluster.",
                        Actions: [ new("TravelTo", "Navigate to the staging coords") ])
                ]),
            new("execute-method", $"Execute method: {methodLabel}",
                "Run the loop appropriate for the chosen method until the target is met.",
                Tasks:
                [
                    new("AcquisitionLoopTask", "Coordinator",
                        "Outer loop: pick the next unit of work, run the matching sub-task, check the goal.",
                        Actions:
                        [
                            new("StartMeleeAttack", "Mob-farming method"),
                            new("CastSpell", "Caster rotation when farming"),
                            new("Interact", "Gather node / pick up loot / talk to NPC"),
                            new("AcceptQuest", "Quest-based methods"),
                            new("CompleteQuest", "Turn in on completion"),
                            new("LootCorpse", "Drop methods")
                        ])
                ]),
            new("verify-state-change", $"Verify {targetLabel} against goal",
                "Read character state (level / standing / inventory / gold / skill) and decide whether to stop or loop.",
                Tasks:
                [
                    new("StateCheckTask", "Coordinator",
                        "Snapshot read of the relevant character-state field; activity completes when target is reached.",
                        Actions: [ new("ReadCharacterState", "No wire message — snapshot read") ])
                ])
        ];

        // -------- Battlegrounds (one row per BG × bracket) ---------

        private static void SeedBattlegrounds(List<ActivityTemplate> list)
        {
            foreach (var b in WsgBracketChoices)
            {
                var (min, max) = ParseBracket(b);
                list.Add(new(
                    Id: $"bg.wsg.{b}",
                    DisplayName: $"Warsong Gulch ({b})",
                    Family: "Battleground",
                    Location: "Warsong Gulch",
                    MinPlayers: 10, MaxPlayers: 10,
                    LevelMin: min, LevelMax: max,
                    FactionRestriction: Faction.Either,
                    StateChangeGoal: "Honor + Silverwing Sentinels / Warsong Outriders rep; Mark of Honor reward turn-ins",
                    AttunementRequirements: [],
                    DefaultParameters:
                    [
                        Param("Strategy", "Flag Rush", required: true,
                              "Tactical approach for the WSG match", WsgStrategyChoices)
                    ],
                    Objectives: WsgObjectives()));
            }

            foreach (var b in AbBracketChoices)
            {
                var (min, max) = ParseBracket(b);
                list.Add(new(
                    Id: $"bg.ab.{b}",
                    DisplayName: $"Arathi Basin ({b})",
                    Family: "Battleground",
                    Location: "Arathi Basin",
                    MinPlayers: 15, MaxPlayers: 15,
                    LevelMin: min, LevelMax: max,
                    FactionRestriction: Faction.Either,
                    StateChangeGoal: "Honor + League of Arathor / Defilers rep; resource-cap victories",
                    AttunementRequirements: [],
                    DefaultParameters:
                    [
                        Param("Strategy", "Stables / LM / BS (Alliance 3-Cap)", required: true,
                              "Resource-node strategy (pick the home-side variant matching the activity's Faction)",
                              AbStrategyChoices)
                    ],
                    Objectives: AbObjectives()));
            }

            list.Add(new(
                Id: "bg.av.51-60",
                DisplayName: "Alterac Valley (51-60)",
                Family: "Battleground",
                Location: "Alterac Mountains",
                MinPlayers: 40, MaxPlayers: ActivityModel.MaxCharactersPerActivity,
                LevelMin: 51, LevelMax: 60,
                FactionRestriction: Faction.Either,
                StateChangeGoal: "Honor + Stormpike Guard / Frostwolf Clan rep (Exalted → mount + Korrak's Revenge tier-0.5 quest unlocks)",
                AttunementRequirements: [],
                DefaultParameters:
                [
                    Param("Strategy", "Zerg General", required: true,
                          "Tactical approach for the AV match", AvStrategyChoices)
                ],
                Objectives: AvObjectives()));
        }

        private static (int min, int max) ParseBracket(string bracket)
        {
            var parts = bracket.Split('-');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        // -------- Dungeons ---------

        private static void SeedDungeons(List<ActivityTemplate> list)
        {
            void Add(string id, string name, string location, int min, int max,
                int minPlayers, int maxPlayers, string stateGoal,
                IReadOnlyList<string>? attunements = null,
                Faction faction = Faction.Either)
            {
                // Level requirements are hardcoded by the game engine + db tables;
                // not user-tunable in the UI. The catalog row still carries LevelMin/Max
                // for character-eligibility checks but no MinLevel parameter is exposed.
                list.Add(new(
                    Id: id, DisplayName: name, Family: "Dungeon/Raid", Location: location,
                    MinPlayers: minPlayers, MaxPlayers: maxPlayers,
                    LevelMin: min, LevelMax: max,
                    FactionRestriction: faction,
                    StateChangeGoal: stateGoal,
                    AttunementRequirements: attunements ?? [],
                    DefaultParameters: [],
                    Objectives: DungeonObjectives(name)));
            }

            Add("dungeon.ragefire-chasm", "Ragefire Chasm", "Orgrimmar — Cleft of Shadow", 13, 18, 1, 5,
                "Leveling 13-18; Bloodspire / Singing Blade greens", faction: Faction.Horde);

            Add("dungeon.deadmines", "The Deadmines", "Westfall — Moonbrook", 17, 26, 1, 5,
                "Leveling 17-26; Cookie's Stirring Rod, Smite's Mighty Hammer, Cruel Barb", faction: Faction.Alliance);

            Add("dungeon.wailing-caverns", "Wailing Caverns", "The Barrens", 17, 24, 1, 5,
                "Leveling 17-24; Druid \"Glowing Lizardscale Cloak\" quest; serpent-skin BoEs");

            Add("dungeon.shadowfang-keep", "Shadowfang Keep", "Silverpine Forest", 18, 25, 1, 5,
                "Leveling 18-25; Assassin's Blade, Black Wolf Bracers, Meteor Shard");

            Add("dungeon.stockade", "The Stockade", "Stormwind City", 22, 30, 1, 5,
                "Leveling 22-30; one-room dungeon, fast level-pad", faction: Faction.Alliance);

            Add("dungeon.blackfathom-deeps", "Blackfathom Deeps", "Ashenvale", 24, 32, 1, 5,
                "Leveling 24-32; Aquamarine Signet, Naga-skin gloves, Twilight's Hammer rep prep");

            Add("dungeon.gnomeregan", "Gnomeregan", "Dun Morogh", 29, 38, 1, 5,
                "Leveling 29-38; Workshirt set, Electrocutioner Lever; Gnomeregan Exiles rep", faction: Faction.Alliance);

            Add("dungeon.razorfen-kraul", "Razorfen Kraul", "Southern Barrens", 29, 38, 1, 5,
                "Leveling 29-38; Tortoise / Quillpike weapons; \"The Crone of the Kraul\" turn-in");

            Add("dungeon.scarlet-monastery-graveyard", "Scarlet Monastery — Graveyard", "Tirisfal Glades", 26, 36, 1, 5,
                "Leveling 26-36; Interrogator Vishas finale; Scarlet Crusade tabard farm starts here");

            Add("dungeon.scarlet-monastery-library", "Scarlet Monastery — Library", "Tirisfal Glades", 29, 39, 1, 5,
                "Leveling 29-39; Houndmaster Loksey, Arcanist Doan; magic-school upgrade greens");

            Add("dungeon.scarlet-monastery-armory", "Scarlet Monastery — Armory", "Tirisfal Glades", 32, 42, 1, 5,
                "Leveling 32-42; Herod kill (Whitemane key in Library), Scarlet Leggings, Ravager");

            Add("dungeon.scarlet-monastery-cathedral", "Scarlet Monastery — Cathedral", "Tirisfal Glades", 34, 45, 1, 5,
                "Leveling 34-45; Mograine + Whitemane; Verigan's Fist (Paladin), Whitemane's Chapeau");

            Add("dungeon.razorfen-downs", "Razorfen Downs", "Southern Barrens", 37, 46, 1, 5,
                "Leveling 37-46; Glutton, Mordresh Fire Eye, Tuten'kash; quest-cluster turn-ins");

            Add("dungeon.uldaman", "Uldaman", "Badlands", 35, 45, 1, 5,
                "Leveling 35-45; Reclaimed Sword of the Sky, Stoneslayer; Dwarven attunement bridge");

            Add("dungeon.zul-farrak", "Zul'Farrak", "Tanaris", 44, 54, 1, 5,
                "Leveling 44-54; Sang'thraze the Deflector, Skull-Slugger Pendant; Mallet of Zul'Farrak prereq");

            Add("dungeon.maraudon", "Maraudon", "Desolace", 45, 55, 1, 5,
                "Leveling 45-55; Hatebringer, Cloudstrider Eagle; Earthwarder's Mantle (Druid)");

            Add("dungeon.sunken-temple", "Sunken Temple", "Swamp of Sorrows", 50, 60, 1, 5,
                "Leveling 50-60; Jammal'an + Hakkari priests; Druid Swim-form quest, Jewel of Kajaro");

            Add("dungeon.blackrock-depths", "Blackrock Depths", "Blackrock Mountain", 52, 60, 1, 5,
                "Leveling 52-60; Emperor Dagran, Princess Theradras; Onyxia / MC attunement prereqs; Thorium Brotherhood rep gate");

            Add("dungeon.lower-blackrock-spire", "Lower Blackrock Spire", "Blackrock Mountain", 55, 60, 1, 5,
                "Pre-raid BiS gathering (Helm of Narv etc.); UBRS attunement quest chain start");

            Add("dungeon.upper-blackrock-spire", "Upper Blackrock Spire", "Blackrock Mountain", 58, 60, 1, 10,
                "Pre-raid BiS (Plans: Lionheart Helm, Drakefire Amulet); BWL attunement (Blackhand's Command quest)",
                attunements: ["Seal of Ascension"]);

            Add("dungeon.dire-maul-east", "Dire Maul — East", "Feralas — Eldreth Row", 54, 60, 1, 5,
                "Leveling 54-60; Tribute quest chain; Pusillin book run; Foror's Compendium half (Druid)");

            Add("dungeon.dire-maul-west", "Dire Maul — West", "Feralas", 56, 60, 1, 5,
                "Shen'dralar rep grind start; libram drops; Treant book; Highborne summoning ritual");

            Add("dungeon.dire-maul-north", "Dire Maul — North", "Feralas", 58, 60, 1, 5,
                "Tribute Run (King of the Gordok buff), Mol Dar's Moon Cleaver, Hide of the Wild leatherworking patterns");

            Add("dungeon.scholomance", "Scholomance", "Western Plaguelands", 58, 60, 1, 5,
                "Pre-raid BiS (Robe of the Archmage, Mar'li's Brain Pan); AD rep; Necromantic Focus turn-ins",
                attunements: ["Skeleton Key"]);

            Add("dungeon.stratholme-undead", "Stratholme — Undead (Baron)", "Eastern Plaguelands", 58, 60, 1, 5,
                "Argent Dawn rep, T0.5 quest chain bosses, 45-min Baron run (Deathcharger's Reins)",
                attunements: ["Premium Key"]);

            Add("dungeon.stratholme-live", "Stratholme — Live", "Eastern Plaguelands", 58, 60, 1, 5,
                "Argent Dawn rep, T0.5 quest chain bosses, Scarlet Crusade reputation flips here");
        }

        // -------- Raids ---------

        private static void SeedRaids(List<ActivityTemplate> list)
        {
            void Add(string id, string name, string location, int min, int max,
                int minPlayers, int maxPlayers, string stateGoal,
                IReadOnlyList<string>? attunements = null)
            {
                // Level requirements are game-engine-enforced; not user-tunable.
                list.Add(new(
                    Id: id, DisplayName: name, Family: "Dungeon/Raid", Location: location,
                    MinPlayers: minPlayers, MaxPlayers: maxPlayers,
                    LevelMin: min, LevelMax: max,
                    FactionRestriction: Faction.Either,
                    StateChangeGoal: stateGoal,
                    AttunementRequirements: attunements ?? [],
                    DefaultParameters: [],
                    Objectives: RaidObjectives(name)));
            }

            Add("raid.zg", "Zul'Gurub", "Stranglethorn Vale", 60, 60, 10, 20,
                "Zandalar Tribe rep (→ Exalted enchants); High Priest tier 0.5 set pieces; Bloodvine plot");

            Add("raid.aq20", "Ruins of Ahn'Qiraj", "Silithus", 60, 60, 10, 20,
                "Cenarion Circle rep; AQ20 short raid set; Mantis Hilt → Hand of Ragnaros prereqs");

            Add("raid.mc", "Molten Core", "Blackrock Mountain", 60, 60, 20, 40,
                "Tier 1 gear (8/8), Hydraxian Waterlords rep, BindOnAccount bind-rolls (Eye of Sulfuras, Sulfuras Hand)",
                attunements: ["Attunement to the Core"]);

            Add("raid.onyxia", "Onyxia's Lair", "Dustwallow Marsh", 60, 60, 40, 40,
                "Onyxia Tooth Pendant world-buff; tier 2 helm token; head turn-in 3hr server buff",
                attunements: ["Drakefire Amulet"]);

            Add("raid.bwl", "Blackwing Lair", "Blackrock Mountain", 60, 60, 40, 40,
                "Tier 2 chest/legs; Elementium Reinforced Bulwark; Vaelastrasz / Nefarian world-firsts; Drakefire (UBRS) attunement prereq",
                attunements: ["Blackhand's Command"]);

            Add("raid.aq40", "Temple of Ahn'Qiraj", "Silithus", 60, 60, 40, 40,
                "Tier 2.5 set, Scepter of the Shifting Sands questline (Bug trio, C'Thun), Brood of Nozdormu Exalted mount",
                attunements: ["Scepter of the Shifting Sands"]);

            Add("raid.naxx", "Naxxramas", "Eastern Plaguelands", 60, 60, 40, 40,
                "Tier 3 set, Atiesh staff pieces, Argent Dawn Exalted; Kel'Thuzad world-first",
                attunements: ["The Dread Citadel — Naxxramas (Argent Dawn Honored)"]);
        }

        // -------- Hierarchy seeds (Phase 1) ---------

        private static IReadOnlyList<ObjectiveDefinition> DungeonObjectives(string dungeonName) =>
        [
            new("reach-entrance", $"Travel to {dungeonName} entrance",
                "All party members converge on the instance portal.",
                Tasks:
                [
                    new("TravelToTask", "Travel",
                        "Pathfind to the entrance coordinates.",
                        Actions: [ new("TravelTo", "Navigate to the portal") ]),
                ]),
            new("form-party", "Form party and enter",
                "Leader forms group, members accept, all step through the portal.",
                Tasks:
                [
                    new("FormPartyTask", "Group",
                        "Send invites, wait for accepts, promote leader if needed.",
                        Actions:
                        [
                            new("SendGroupInvite", "Invite each party slot by name"),
                            new("AcceptGroupInvite", "Members accept the invite")
                        ]),
                    new("EnterInstanceTask", "Travel",
                        "Click through the instance portal.",
                        Actions: [ new("Interact", "Step through the portal") ])
                ]),
            new("clear-trash", "Clear trash to first boss",
                "Marked-target pulls; tank holds threat; healer keeps party up.",
                Tasks:
                [
                    new("PullStrategyTask", "Combat",
                        "Leader marks skull on the next pull target.",
                        Actions: [ new("SetSelection", "Lock the leader's selection") ]),
                    new("PvERotationTask", "Combat",
                        "Each class executes its PvE rotation against the marked target.",
                        Actions:
                        [
                            new("StartMeleeAttack", "Melee classes engage"),
                            new("CastSpell", "Casters fire their rotation")
                        ])
                ]),
            new("kill-bosses", "Defeat bosses + loot",
                "Per-encounter mechanics; loot priority per BotSelectionPolicy.",
                Tasks:
                [
                    new("BossEncounterTask", "Combat",
                        "Per-encounter mechanics overlay on top of PvE rotation.",
                        Actions:
                        [
                            new("StartMeleeAttack", "Tank engages and holds threat"),
                            new("CastSpell", "DPS / healer rotation")
                        ]),
                    new("LootCorpseTask", "Loot",
                        "Loot the boss; roll need-before-greed by default.",
                        Actions:
                        [
                            new("LootCorpse", "Open the boss loot"),
                            new("LootRollNeed", "Roll need on viable upgrades")
                        ])
                ]),
            new("exit-instance", "Exit the instance",
                "All party members leave through the entrance.",
                Tasks:
                [
                    new("TravelToTask", "Travel",
                        "Walk back to the exit portal.",
                        Actions: [ new("TravelTo", "Navigate to the exit") ]),
                    new("InteractTask", "Travel",
                        "Click the portal to exit.",
                        Actions: [ new("Interact", "Step through the exit portal") ])
                ])
        ];

        private static IReadOnlyList<ObjectiveDefinition> RaidObjectives(string raidName) =>
        [
            new("attune-and-travel", $"Attune + travel to {raidName}",
                "Verify attunement key, then travel to raid entrance.",
                Tasks:
                [
                    new("AttunementCheckTask", "Raid",
                        "Verify the required quest / item is on each character.",
                        Actions: [ new("UseItem", "Equip / activate the attunement item if needed") ]),
                    new("TravelToTask", "Travel",
                        "Travel to the raid portal.",
                        Actions: [ new("TravelTo", "Navigate to the portal") ])
                ]),
            new("form-raid", "Form raid + enter",
                "Raid leader assigns roles, converts party → raid, all enter.",
                Tasks:
                [
                    new("FormRaidTask", "Group",
                        "Invite, convert to raid, set roles.",
                        Actions:
                        [
                            new("SendGroupInvite", "Mass invite"),
                            new("AcceptGroupInvite", "All members accept")
                        ])
                ]),
            new("clear-progression", "Clear progression bosses",
                "Boss-by-boss kill sequence per raid order.",
                Tasks:
                [
                    new("BossEncounterTask", "Raid",
                        "Per-boss mechanics + PvE rotation.",
                        Actions:
                        [
                            new("StartMeleeAttack", "Tank engages"),
                            new("CastSpell", "DPS/heal rotation")
                        ]),
                    new("LootCorpseTask", "Loot",
                        "Loot distribution per group policy.",
                        Actions: [ new("LootCorpse", "Open loot"), new("LootRollNeed", "Roll need on upgrades") ])
                ]),
        ];

        private static IReadOnlyList<ObjectiveDefinition> WsgObjectives() =>
        [
            new("queue-for-wsg", "Queue for WSG",
                "Queue and accept the pop.",
                Tasks:
                [
                    new("BattlegroundQueueTask", "Bg",
                        "Add to WSG queue and accept.",
                        Actions:
                        [
                            new("JoinBattleground", "Join WSG queue"),
                            new("AcceptBattleground", "Accept pop")
                        ])
                ]),
            new("flag-offense", "Capture enemy flag",
                "Travel to enemy base, pick up the flag, return home.",
                Tasks:
                [
                    new("GoToTask", "Travel",
                        "Move to the enemy flag room.",
                        Actions: [ new("TravelTo", "Path into the flag room") ]),
                    new("InteractTask", "Bg",
                        "Pick up + return the flag.",
                        Actions: [ new("Interact", "Click flag to pick up / cap") ])
                ]),
            new("flag-defense", "Defend friendly flag",
                "Opportunistic PvP rotation when enemies push.",
                Tasks:
                [
                    new("PvPRotationTask", "Combat",
                        "Class-spec PvP rotation against enemy carriers.",
                        Actions:
                        [
                            new("SetSelection", "Target enemy flag carrier"),
                            new("CastSpell", "Apply CC / damage rotation"),
                            new("StartMeleeAttack", "Engage if in melee")
                        ])
                ]),
            new("exit-bg", "Leave when match ends",
                "Auto-leave on victory or defeat.",
                Tasks:
                [
                    new("LeaveBattlegroundTask", "Bg",
                        "Drop out of WSG.",
                        Actions: [ new("LeaveBattleground", "Leave the BG") ])
                ])
        ];

        private static IReadOnlyList<ObjectiveDefinition> AbObjectives() =>
        [
            new("queue-for-ab", "Queue for AB",
                "Queue and accept the pop.",
                Tasks:
                [
                    new("BattlegroundQueueTask", "Bg",
                        "Add to AB queue and accept.",
                        Actions:
                        [
                            new("JoinBattleground", "Join AB queue"),
                            new("AcceptBattleground", "Accept pop")
                        ])
                ]),
            new("capture-resource-nodes", "Capture and hold resource nodes",
                "Spread to 3-5 nodes (Farm, Lumber Mill, Mine, Stables, Blacksmith); hold to 2000.",
                Tasks:
                [
                    new("CaptureObjectiveTask", "Bg",
                        "Travel to a node and click-capture.",
                        Actions:
                        [
                            new("TravelTo", "Move to the node flag"),
                            new("Interact", "Click to capture")
                        ]),
                    new("DefendNodeTask", "Bg",
                        "Hold a captured node against pushes.",
                        Actions:
                        [
                            new("PvPRotationTask", "Engage attackers"),
                            new("CastSpell", "Apply control / damage rotation")
                        ])
                ]),
            new("exit-bg", "Leave when match ends",
                "Auto-leave on victory or defeat.",
                Tasks:
                [
                    new("LeaveBattlegroundTask", "Bg",
                        "Drop out of AB.",
                        Actions: [ new("LeaveBattleground", "Leave the BG") ])
                ])
        ];

        private static IReadOnlyList<ObjectiveDefinition> AvObjectives() =>
        [
            new("queue-for-av", "Queue for AV",
                "All 40+ bots queue and accept when popped.",
                Tasks:
                [
                    new("BattlegroundQueueTask", "Bg",
                        "Join AV queue.",
                        Actions:
                        [
                            new("JoinBattleground", "Add to AV queue"),
                            new("AcceptBattleground", "Accept the BG pop")
                        ])
                ]),
            new("capture-graveyards-and-towers", "Capture graveyards and towers",
                "Push to enemy graveyards/towers and capture them.",
                Tasks:
                [
                    new("CaptureObjectiveTask", "Bg",
                        "Travel to the flag/tower and click-capture.",
                        Actions:
                        [
                            new("TravelTo", "Move to the objective"),
                            new("Interact", "Click the flag to capture")
                        ])
                ]),
            new("kill-faction-boss", "Defeat the enemy faction boss",
                "End-game push: down Vanndar / Drek'Thar.",
                Tasks:
                [
                    new("BossEncounterTask", "Bg",
                        "Coordinated assault on the faction general.",
                        Actions: [ new("CastSpell", "Rotation"), new("StartMeleeAttack", "Melee engage") ])
                ]),
            new("exit-bg", "Leave the battleground",
                "Match end → leave for the home zone.",
                Tasks:
                [
                    new("LeaveBattlegroundTask", "Bg",
                        "Leave the BG when the match ends.",
                        Actions: [ new("LeaveBattleground", "Leave the BG") ])
                ])
        ];
    }

    /// <summary>Static template definition used to instantiate an <see cref="ActivityModel"/>.</summary>
    public sealed record ActivityTemplate(
        string Id,
        string DisplayName,
        string Family,
        string Location,
        int MinPlayers,
        int MaxPlayers,
        int LevelMin,
        int LevelMax,
        Faction FactionRestriction,
        string StateChangeGoal,
        IReadOnlyList<string> AttunementRequirements,
        IReadOnlyList<ActivityParameter> DefaultParameters,
        IReadOnlyList<ObjectiveDefinition> Objectives);

    /// <summary>One row in the family dropdown; carries the instance list bound to the secondary picker.</summary>
    public sealed record ActivityFamilyGroup(string FamilyName, IReadOnlyList<ActivityTemplate> Instances);
}
