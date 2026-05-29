using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RecordedTests.PathingTests.Models;

/// <summary>
/// Factory for all pathing test definitions. Provides access to all 20 predefined pathing tests.
/// </summary>
public static class PathingTestDefinitions
{
    /// <summary>
    /// Gets all 20 pathing test definitions.
    /// </summary>
    public static IReadOnlyList<PathingTestDefinition> All { get; } = new[]
    {
        // ============================================================================
        // BASIC POINT-TO-POINT TESTS (3 tests)
        // ============================================================================

        new PathingTestDefinition(
            Name: "Northshire_ElwynnForest_ShortDistance",
            Category: "Basic",
            Description: "Navigate from Northshire Abbey to Goldshire (short distance, simple terrain)",
            MapId: 0,
            StartPosition: new Position(-8914f, -133f, 81f),
            EndPosition: new Position(-9465f, 64f, 56f),
            SetupCommands: new[]
            {
                ".character level 1",
                ".tele Northshire Valley",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(5)
        ),

        new PathingTestDefinition(
            Name: "Goldshire_Stormwind_MediumDistance",
            Category: "Basic",
            Description: "Navigate from Goldshire to Stormwind City (medium distance, road following)",
            MapId: 0,
            StartPosition: new Position(-9465f, 64f, 56f),
            EndPosition: new Position(-8833f, 622f, 94f),
            SetupCommands: new[]
            {
                ".character level 10",
                ".tele Goldshire",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8)
        ),

        new PathingTestDefinition(
            Name: "CrossContinents_Wetlands_To_IronForge",
            Category: "Basic",
            Description: "Navigate from Menethil Harbor to Ironforge (cross-zone, elevation changes)",
            MapId: 0,
            StartPosition: new Position(-3792f, -832f, 10f),
            EndPosition: new Position(-4918f, -941f, 501f),
            SetupCommands: new[]
            {
                ".character level 20",
                ".tele Menethil Harbor",
                ".modify money 1000000",
                ".modify speed all 1.5"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(20)
        ),

        // ============================================================================
        // TRANSPORT TESTS (4 tests)
        // ============================================================================

        new PathingTestDefinition(
            Name: "BoatTravel_Menethil_To_Auberdine",
            Category: "Transport",
            Description: "Use boat transport from Menethil Harbor to Auberdine (Darkshore)",
            MapId: 0,
            StartPosition: new Position(-3792f, -832f, 10f),
            EndPosition: new Position(6719f, 227f, 24f), // Auberdine dock
            SetupCommands: new[]
            {
                ".character level 15",
                ".tele Menethil Harbor",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(12),
            Transport: TransportMode.Boat
        ),

        new PathingTestDefinition(
            Name: "BoatTravel_Ratchet_To_BootyBay",
            Category: "Transport",
            Description: "Use boat from Ratchet to Booty Bay (cross-continent transport)",
            MapId: 1,
            StartPosition: new Position(-996f, -3826f, 6f),
            EndPosition: new Position(-14281f, 552f, 7f), // Booty Bay dock
            SetupCommands: new[]
            {
                ".character level 20",
                ".tele Ratchet",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15),
            Transport: TransportMode.Boat
        ),

        new PathingTestDefinition(
            Name: "ZeppelinTravel_Orgrimmar_To_Undercity",
            Category: "Transport",
            Description: "Use zeppelin from Orgrimmar to Undercity",
            MapId: 1,
            StartPosition: new Position(1503f, -4415f, 22f),
            EndPosition: new Position(2059f, 274f, 97f), // Undercity zeppelin tower
            SetupCommands: new[]
            {
                ".character level 10",
                ".tele Orgrimmar",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(12),
            Transport: TransportMode.Zeppelin
        ),

        new PathingTestDefinition(
            Name: "ZeppelinTravel_Undercity_To_GromGol",
            Category: "Transport",
            Description: "Use zeppelin from Undercity to Grom'gol Base Camp (STV)",
            MapId: 0,
            StartPosition: new Position(2059f, 274f, 97f),
            EndPosition: new Position(-12420f, 129f, 4f), // Grom'gol platform
            SetupCommands: new[]
            {
                ".character level 15",
                ".tele Undercity",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15),
            Transport: TransportMode.Zeppelin
        ),

        // ============================================================================
        // CAVE AND COMPLEX TERRAIN TESTS (3 tests)
        // ============================================================================

        new PathingTestDefinition(
            Name: "CaveNavigation_Fargodeep_Mine",
            Category: "Cave",
            Description: "Navigate through Fargodeep Mine (simple cave with single entrance/exit)",
            MapId: 0,
            StartPosition: new Position(-9832f, -1365f, 41f), // Outside entrance
            EndPosition: new Position(-9796f, -1318f, 39f), // Deepest point
            SetupCommands: new[]
            {
                ".character level 5",
                ".go xyz -9832 -1365 41 0",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8)
        ),

        new PathingTestDefinition(
            Name: "CaveNavigation_Deadmines_Entrance_To_VanCleef",
            Category: "Cave",
            Description: "Navigate through Deadmines instance (complex multi-level dungeon)",
            MapId: 36, // Deadmines instance
            StartPosition: new Position(-16.4f, -383.07f, 61.78f), // Instance entrance
            EndPosition: new Position(-64.43f, -819.39f, 41.25f), // VanCleef's ship
            SetupCommands: new[]
            {
                ".character level 20",
                ".go xyz -16.4 -383.07 61.78 36",
                ".modify hp 50000",
                ".modify mana 50000",
                ".gm visible off"
            },
            TeardownCommands: new[]
            {
                ".gm visible on",
                ".character delete"
            },
            ExpectedDuration: TimeSpan.FromMinutes(20)
        ),

        new PathingTestDefinition(
            Name: "CaveNavigation_WailingCaverns_Spiral",
            Category: "Cave",
            Description: "Navigate Wailing Caverns spiral cave system",
            MapId: 43, // Wailing Caverns instance
            StartPosition: new Position(-163.49f, 132.9f, -73.66f), // Entrance
            EndPosition: new Position(-75.59f, 240.78f, -95.4f), // Bottom of spiral
            SetupCommands: new[]
            {
                ".character level 18",
                ".go xyz -163.49 132.9 -73.66 43",
                ".modify hp 50000",
                ".modify speed all 1.5"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(18)
        ),

        // ============================================================================
        // OBSTACLE AND TERRAIN CHALLENGE TESTS (3 tests)
        // ============================================================================

        new PathingTestDefinition(
            Name: "MountainClimbing_ThousandNeedles_To_Feralas",
            Category: "Terrain",
            Description: "Navigate mountain pass from Thousand Needles to Feralas",
            MapId: 1,
            StartPosition: new Position(-5375f, -2509f, -58f), // Thousand Needles high plateau
            EndPosition: new Position(-4467f, 303f, 41f), // Feralas
            SetupCommands: new[]
            {
                ".character level 40",
                ".go xyz -5375 -2509 -58 1",
                ".modify money 1000000",
                ".modify speed all 1.2"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15)
        ),

        new PathingTestDefinition(
            Name: "WaterNavigation_STV_Coast_Swim",
            Category: "Terrain",
            Description: "Navigate along Stranglethorn Vale coastline (swimming test)",
            MapId: 0,
            StartPosition: new Position(-14281f, 552f, 7f), // Booty Bay docks
            EndPosition: new Position(-13234f, 339f, 29f), // Northern coastline
            SetupCommands: new[]
            {
                ".character level 30",
                ".tele Stranglethorn Vale",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15)
        ),

        new PathingTestDefinition(
            Name: "BridgeCrossing_Redridge_LakeEverstill",
            Category: "Terrain",
            Description: "Cross Lake Everstill via bridge (structure navigation)",
            MapId: 0,
            StartPosition: new Position(-9431f, -2237f, 64f), // Lakeshire
            EndPosition: new Position(-9352f, -2052f, 64f), // Opposite shore
            SetupCommands: new[]
            {
                ".character level 15",
                ".tele Redridge Mountains",
                ".modify money 1000000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8)
        ),

        // ============================================================================
        // ADVANCED MULTI-SEGMENT TESTS (3 tests)
        // ============================================================================

        new PathingTestDefinition(
            Name: "GrandTour_Alliance_Capitals",
            Category: "Advanced",
            Description: "Visit all Alliance capital cities in sequence",
            MapId: 0,
            StartPosition: new Position(-8833f, 622f, 94f), // Stormwind
            EndPosition: new Position(9961f, 2280f, 1331f), // Exodar
            SetupCommands: new[]
            {
                ".character level 40",
                ".tele Stormwind City",
                ".modify money 5000000",
                ".modify speed all 2.0"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(40),
            Transport: TransportMode.Boat,
            IntermediateWaypoint: "Stormwind → Ironforge → Darnassus (via boat) → Exodar (via boat)"
        ),

        new PathingTestDefinition(
            Name: "GrandTour_Horde_Capitals",
            Category: "Advanced",
            Description: "Visit all Horde capital cities in sequence",
            MapId: 1,
            StartPosition: new Position(1503f, -4415f, 22f), // Orgrimmar
            EndPosition: new Position(9738f, -7454f, 13f), // Silvermoon
            SetupCommands: new[]
            {
                ".character level 40",
                ".tele Orgrimmar",
                ".modify money 5000000",
                ".modify speed all 2.0"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(40),
            Transport: TransportMode.Zeppelin,
            IntermediateWaypoint: "Orgrimmar → Thunder Bluff → Undercity (via zeppelin) → Silvermoon (via teleport orb)"
        ),

        new PathingTestDefinition(
            Name: "CrossContinent_Kalimdor_To_EasternKingdoms",
            Category: "Advanced",
            Description: "Travel from Teldrassil to Eastern Plaguelands",
            MapId: 1,
            StartPosition: new Position(10311f, 832f, 1326f), // Teldrassil
            EndPosition: new Position(2280f, -5275f, 82f), // Eastern Plaguelands
            SetupCommands: new[]
            {
                ".character level 50",
                ".tele Teldrassil",
                ".modify money 5000000",
                ".modify speed all 1.5"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(50),
            Transport: TransportMode.Boat,
            IntermediateWaypoint: "Teldrassil → Darnassus → Boat to Auberdine → Boat to Menethil → Wetlands → Eastern Plaguelands"
        ),

        // ============================================================================
        // EDGE CASE AND STRESS TESTS (4 tests)
        // ============================================================================

        new PathingTestDefinition(
            Name: "StuckRecovery_WesternPlaguelands_River",
            Category: "EdgeCase",
            Description: "Test recovery from getting stuck in river terrain",
            MapId: 0,
            StartPosition: new Position(1744f, -1723f, 60f), // Middle of river
            EndPosition: new Position(1783f, -1675f, 63f), // Nearest road
            SetupCommands: new[]
            {
                ".character level 45",
                ".go xyz 1744 -1723 60 0",
                ".modify speed all 0.5"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(10)
        ),

        new PathingTestDefinition(
            Name: "AggroAvoidance_STV_HighLevel_Mobs",
            Category: "EdgeCase",
            Description: "Navigate through high-level mob area while avoiding aggro",
            MapId: 0,
            StartPosition: new Position(-12420f, 129f, 4f), // Grom'gol Base Camp
            EndPosition: new Position(-14281f, 552f, 7f), // Booty Bay
            SetupCommands: new[]
            {
                ".character level 25",
                ".tele Stranglethorn Vale",
                ".modify hp 10000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15)
        ),

        new PathingTestDefinition(
            Name: "NightNavigation_Duskwood_NoLight",
            Category: "EdgeCase",
            Description: "Navigate Duskwood at night (low visibility test)",
            MapId: 0,
            StartPosition: new Position(-10531f, -1281f, 41f), // Darkshire
            EndPosition: new Position(-10899f, -394f, 40f), // Raven Hill Cemetery
            SetupCommands: new[]
            {
                ".character level 20",
                ".tele Duskwood",
                ".wchange 0 night"
            },
            TeardownCommands: new[]
            {
                ".wchange 0 day",
                ".character delete"
            },
            ExpectedDuration: TimeSpan.FromMinutes(12)
        ),

        new PathingTestDefinition(
            Name: "RapidPathRecalculation_Barrens_Oasis_Loop",
            Category: "EdgeCase",
            Description: "Navigate circular path with frequent recalculation triggers",
            MapId: 1,
            StartPosition: new Position(-450f, -2645f, 96f), // Crossroads
            EndPosition: new Position(-450f, -2645f, 96f), // Back to Crossroads
            SetupCommands: new[]
            {
                ".character level 15",
                ".tele The Barrens",
                ".modify speed all 1.8"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(20),
            IntermediateWaypoint: "Crossroads → Ratchet → Camp Taurajo → Crossroads (circular)"
        ),

        // ============================================================================
        // ============================================================================
        // PHASE 1 SCAFFOLD ROWS — seeded by COMPREHENSIVE_TEST_PLAN, gated Experimental.
        //
        // Every row below is Status = TestStatus.Experimental and is excluded from the
        // default sweep until Phase 2 promotes it to Stable. To run them:
        //     dotnet run -- --include-experimental
        // or  dotnet run -- --status Experimental
        //
        // For dungeon scaffolds: start/end coords come from Bot/named-locations.json
        // (where present) or docs/Plan/Activities/_catalog_rows/03_dungeons.md. Phase 2
        // expands each row to a full boss-to-boss Waypoints chain using /go probing per
        // BAKE_RECIPE.md and COMPREHENSIVE_TEST_PLAN.md.
        // ============================================================================
        // ============================================================================

        // --------------------------------------------------------------------------
        // DUNGEON SCAFFOLDS (21 vanilla 5-mans)
        //
        // Each row deposits the test character INSIDE the instance via `.go xyz X Y Z mapId`
        // and walks to a placeholder end coord (the same entrance coord, offset by a
        // few yards in-bounds). Phase 2 replaces the end coord with the real final-boss
        // location and adds Waypoints[] for intermediate bosses.
        //
        // Bake status: all 21 instance maps are confirmed baked under prod-data per
        // BAKE_RECIPE.md (2026-05-29). No BakeBlocked rows in this batch.
        // --------------------------------------------------------------------------

        ScaffoldDungeon(
            name: "Dungeon_RagefireChasm_Scaffold",
            description: "Inside Ragefire Chasm, walk from instance entry toward first chamber. Phase 2: boss chain via Oggleflint → Taragaman → Jergosh → Bazzalan.",
            mapId: 389, entrance: new Position(2.0f, -19.0f, -16.0f), level: 13),

        ScaffoldDungeon(
            name: "Dungeon_WailingCaverns_Scaffold",
            description: "Inside Wailing Caverns, walk from entry crossroads toward spiral descent. Phase 2: boss chain via Kresh → Lord Cobrahn → Lady Anacondra → Lord Pythas → Skum → Lord Serpentis → Verdan → Mutanus.",
            mapId: 43, entrance: new Position(-163.49f, 132.9f, -73.66f), level: 18),

        ScaffoldDungeon(
            name: "Dungeon_Deadmines_Scaffold",
            description: "Inside Deadmines, walk from interior portal toward goblin foundry. Phase 2: boss chain via Rhahk'Zor → Sneed's Shredder → Sneed → Gilnid → Mr. Smite → Cookie → VanCleef.",
            mapId: 36, entrance: new Position(-16.4f, -383.07f, 61.78f), level: 20),

        ScaffoldDungeon(
            name: "Dungeon_ShadowfangKeep_Scaffold",
            description: "Inside Shadowfang Keep courtyard, walk toward upper crypt. Phase 2: boss chain via Rethilgore → Razorclaw → Baron Silverlaine → Commander Springvale → Wolf Master Nandos → Odo the Blindwatcher → Fenrus → Archmage Arugal.",
            mapId: 33, entrance: new Position(-234.0f, 1561.0f, 76.0f), level: 25),

        ScaffoldDungeon(
            name: "Dungeon_BlackfathomDeeps_Scaffold",
            description: "Inside Blackfathom Deeps, walk from inner ramp toward fire pools. Phase 2: boss chain via Ghamoo-Ra → Lady Sarevess → Gelihast → Lord Kelris → Aku'mai.",
            mapId: 48, entrance: new Position(-152.0f, 106.0f, -39.0f), level: 24),

        ScaffoldDungeon(
            name: "Dungeon_Stockades_Scaffold",
            description: "Inside The Stockades from Stormwind entry hall. Phase 2: boss chain via Targorr → Kam Deepfury → Hamhock → Bazil Thredd → Dextren Ward.",
            mapId: 34, entrance: new Position(50.0f, 0.0f, -22.0f), level: 24),

        ScaffoldDungeon(
            name: "Dungeon_Gnomeregan_Scaffold",
            description: "Inside Gnomeregan launch pad, walk toward Workshop. Phase 2: boss chain via Grubbis → Viscous Fallout → Electrocutioner 6000 → Crowd Pummeler 9-60 → Dark Iron Ambassador → Mekgineer Thermaplugg.",
            mapId: 90, entrance: new Position(-332.0f, -2.0f, -152.0f), level: 29),

        ScaffoldDungeon(
            name: "Dungeon_RazorfenKraul_Scaffold",
            description: "Inside Razorfen Kraul, walk from outer cliff path toward inner pen. Phase 2: boss chain via Roogug → Aggem Thorncurse → Death Speaker Jargba → Overlord Ramtusk → Agathelos → Charlga Razorflank.",
            mapId: 47, entrance: new Position(1942.0f, 1542.0f, 82.0f), level: 30),

        ScaffoldDungeon(
            name: "Dungeon_RazorfenDowns_Scaffold",
            description: "Inside Razorfen Downs, walk from boneyard toward inner sanctum. Phase 2: boss chain via Tuten'kash → Henry Stern → Mordresh Fire Eye → Glutton → Ragglesnout → Plaguemaw → Amnennar.",
            mapId: 129, entrance: new Position(2592.0f, 1108.0f, 50.0f), level: 39),

        ScaffoldDungeon(
            name: "Dungeon_Uldaman_Scaffold",
            description: "Inside Uldaman, walk from Dig Two entry toward Map chamber. Phase 2: boss chain via Revelosh → Iron Bound Trogg → Annora → Ancient Stone Keeper → Galgann → Grimlok → Archaedas.",
            mapId: 70, entrance: new Position(-228.0f, 49.0f, -46.0f), level: 41),

        ScaffoldDungeon(
            name: "Dungeon_ZulFarrak_Scaffold",
            description: "Inside Zul'Farrak, walk from Mortar lane toward Pyramid. Phase 2: boss chain via Antu'sul → Theka the Martyr → Witch Doctor Zum'rah → Nekrum Gutchewer → Shadowpriest Sezz'ziz → Sergeant Bly + Adventurers → Hydromancer Velratha → Chief Ukorz Sandscalp.",
            mapId: 209, entrance: new Position(1213.0f, 841.0f, 9.0f), level: 46),

        ScaffoldDungeon(
            name: "Dungeon_Maraudon_Princess_Scaffold",
            description: "Maraudon Princess wing (orange portal). Phase 2: boss chain via Tinkerer Gizlock → Lord Vyletongue → Celebras → Princess Theradras.",
            mapId: 349, entrance: new Position(1075.0f, -480.0f, -36.0f), level: 49),

        ScaffoldDungeon(
            name: "Dungeon_Maraudon_Wicked_Scaffold",
            description: "Maraudon Wicked Grotto (purple portal). Phase 2: boss chain via Noxxion → Razorlash.",
            mapId: 349, entrance: new Position(836.0f, -343.0f, -47.0f), level: 46),

        ScaffoldDungeon(
            name: "Dungeon_SunkenTemple_Scaffold",
            description: "Inside Sunken Temple, walk from outer ring toward inner sanctum. Phase 2: boss chain via Atal'alarion → 6 Prophets → Dreamscythe + Weaver → Avatar of Hakkar → Jammal'an → Shade of Eranikus.",
            mapId: 109, entrance: new Position(-320.0f, 90.0f, -150.0f), level: 50),

        ScaffoldDungeon(
            name: "Dungeon_BlackrockDepths_Scaffold",
            description: "Inside BRD, walk from Detention Block toward Shadowforge City. Phase 2: long boss chain (Lord Roccor → Ring of Law → Anub'shiah → Eviscerator → Pyromancer Loregrain → Houndmaster Grebmar → Bael'Gar → Lord Incendius → Warder Stilgiss → Fineous Darkvire → Bael'Gar's brood → Magmus → Emperor Dagran Thaurissan).",
            mapId: 230, entrance: new Position(472.0f, 24.0f, -70.0f), level: 55),

        ScaffoldDungeon(
            name: "Dungeon_LowerBlackrockSpire_Scaffold",
            description: "Inside LBRS, walk from entry hall toward Beast wing. Phase 2: boss chain via Highlord Omokk → Shadow Hunter Vosh'gajin → War Master Voone → Mother Smolderweb → Urok Doomhowl → Quartermaster Zigris → Halycon → Gizrul → Overlord Wyrmthalak.",
            mapId: 229, entrance: new Position(75.0f, -228.0f, 110.0f), level: 55),

        ScaffoldDungeon(
            name: "Dungeon_UpperBlackrockSpire_Scaffold",
            description: "Inside UBRS, walk from Father Flame chamber toward Pyre. Phase 2: boss chain via Pyroguard Emberseer → Solakar Flamewreath → Father Flame → Jed Runewatcher → Goraluk Anvilcrack → Warchief Rend Blackhand → Lord Valthalak → The Beast → General Drakkisath.",
            mapId: 229, entrance: new Position(75.0f, -228.0f, 110.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_Scholomance_Scaffold",
            description: "Inside Scholomance, walk from entrance hall toward Headmaster's study. Phase 2: boss chain via Kirtonos → Jandice Barov → Rattlegore → Marduk Blackpool → Vectus → Ras Frostwhisper → Instructor Malicia → Doctor Theolen Krastinov → Lorekeeper Polkelt → The Ravenian → Lord Alexei Barov → Lady Illucia Barov → Darkmaster Gandling.",
            mapId: 289, entrance: new Position(180.0f, 90.0f, 100.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_Stratholme_Live_Scaffold",
            description: "Stratholme Live side. Phase 2: boss chain via Skul → Fras Siabi → Hearthsinger Forresten → Postmaster Malown → Timmy the Cruel → Crimson Hammersmith → Cannon Master Willey → Archivist Galford → Balnazzar.",
            mapId: 329, entrance: new Position(3700.0f, -3500.0f, 132.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_Stratholme_Undead_Scaffold",
            description: "Stratholme Undead side. Phase 2: boss chain via Magistrate Barthilas → Stonespine → Black Guard Swordsmith → Cannon Master Willey → Postmaster Malown → Ramstein the Gorger → Skul → Baron Rivendare.",
            mapId: 329, entrance: new Position(3187.0f, -4063.0f, 107.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_DireMaul_East_Scaffold",
            description: "Dire Maul East wing. Phase 2: boss chain via Pusillin → Lethtendris → Hydrospawn → Zevrim Thornhoof → Alzzin the Wildshaper.",
            mapId: 429, entrance: new Position(-3978.0f, 1130.0f, 161.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_DireMaul_West_Scaffold",
            description: "Dire Maul West wing. Phase 2: boss chain via Tendris Warpwood → Magister Kalendris → Tsu'zee → Illyanna Ravenoak → Immol'thar → Prince Tortheldrin.",
            mapId: 429, entrance: new Position(-3980.0f, 1131.0f, 161.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_DireMaul_North_Scaffold",
            description: "Dire Maul North wing (Tribute run or full clear). Phase 2: boss chain via Guard Mol'dar → Stomper Kreeg → Guard Slip'kik → Captain Kromcrush → Cho'Rush the Observer → King Gordok.",
            mapId: 429, entrance: new Position(-3979.0f, 1130.0f, 161.0f), level: 58),

        ScaffoldDungeon(
            name: "Dungeon_ScarletMonastery_Graveyard_Scaffold",
            description: "Scarlet Monastery Graveyard wing. Phase 2: boss chain via Interrogator Vishas → Bloodmage Thalnos → Ironspine → Azshir the Sleepless → Fallen Champion.",
            mapId: 189, entrance: new Position(853.0f, 1321.0f, 19.0f), level: 30),

        ScaffoldDungeon(
            name: "Dungeon_ScarletMonastery_Library_Scaffold",
            description: "Scarlet Monastery Library wing. Phase 2: boss chain via Houndmaster Loksey → Arcanist Doan.",
            mapId: 189, entrance: new Position(1183.0f, 1670.0f, 30.0f), level: 33),

        ScaffoldDungeon(
            name: "Dungeon_ScarletMonastery_Armory_Scaffold",
            description: "Scarlet Monastery Armory wing. Phase 2: boss chain via Herod.",
            mapId: 189, entrance: new Position(1605.0f, 1228.0f, 19.0f), level: 36),

        ScaffoldDungeon(
            name: "Dungeon_ScarletMonastery_Cathedral_Scaffold",
            description: "Scarlet Monastery Cathedral wing. Phase 2: boss chain via Scarlet Commander Mograine → High Inquisitor Whitemane.",
            mapId: 189, entrance: new Position(855.0f, 1321.0f, 19.0f), level: 38),

        // --------------------------------------------------------------------------
        // LONG-TRAVEL — EASTERN KINGDOMS (continent map 0)
        // Real coords sourced from Bot/named-locations.json hub markers.
        // --------------------------------------------------------------------------

        new PathingTestDefinition(
            Name: "LongTravel_EK_Goldshire_To_Lakeshire",
            Category: "LongTravel.EK",
            Description: "Walk from Goldshire (Elwynn) east through Redridge to Lakeshire town center. Tests Elwynn→Redridge zone seam + bridge crossing.",
            MapId: 0,
            StartPosition: new Position(-9465f, 64f, 56f),
            EndPosition: new Position(-9248f, -2244f, 67f),
            SetupCommands: new[] { ".character level 20", ".tele Goldshire", ".modify speed all 1.5" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: validate cross-zone road network + Stonewatch bridge."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_Stormwind_To_IronforgeViaDeeprunTram",
            Category: "LongTravel.EK",
            Description: "Stormwind Dwarven District tram → Ironforge Mystic Ward. Tests interior city → tram off-mesh → continent transition.",
            MapId: 0,
            StartPosition: new Position(-8364f, 542f, 96f),
            EndPosition: new Position(-4918f, -941f, 501f),
            SetupCommands: new[] { ".character level 10", ".tele Stormwind", ".modify money 1000000" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(10),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: tram is map 369; needs off-mesh connection in offmesh.txt for entry/exit portals."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_Menethil_To_LightHopeChapel",
            Category: "LongTravel.EK",
            Description: "Menethil Harbor → Wetlands → Hillsbrad → Eastern Plaguelands → Light's Hope. End-to-end EK north traverse.",
            MapId: 0,
            StartPosition: new Position(-3792f, -832f, 10f),
            EndPosition: new Position(2275f, -5346f, 88f),
            SetupCommands: new[] { ".character level 60", ".tele Menethil Harbor", ".modify speed all 2.0" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(45),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: long-distance multi-zone, passes through PvP zones and crosses Thoradin's Wall."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_Undercity_To_Tarren_Mill",
            Category: "LongTravel.EK",
            Description: "Tirisfal Glades Undercity → Silverpine → Hillsbrad Tarren Mill. Horde south traverse.",
            MapId: 0,
            StartPosition: new Position(1676f, 1678f, 121f),
            EndPosition: new Position(-852f, -592f, 22f),
            SetupCommands: new[] { ".character level 25", ".tele Undercity", ".modify speed all 1.5" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(25),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: cross-zone via Sepulcher waypoint."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_BootyBay_To_Stormwind_NorthRoad",
            Category: "LongTravel.EK",
            Description: "Booty Bay → STV north road → Duskwood → Elwynn → Stormwind. Long road follow with multiple zone seams.",
            MapId: 0,
            StartPosition: new Position(-14441f, 553f, 22f),
            EndPosition: new Position(-8833f, 622f, 94f),
            SetupCommands: new[] { ".character level 30", ".tele Booty Bay", ".modify speed all 1.8" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(35),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: STV→Duskwood gorge bridge + Duskwood→Elwynn night-day fade."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_FlameCrest_To_Ironforge",
            Category: "LongTravel.EK",
            Description: "Burning Steppes Flame Crest → Searing Gorge → Loch Modan → Dun Morogh → Ironforge. Tests the BRM exterior route + tunnel transitions.",
            MapId: 0,
            StartPosition: new Position(-7997f, -1462f, 137f),
            EndPosition: new Position(-4918f, -941f, 501f),
            SetupCommands: new[] { ".character level 55", ".tele Burning Steppes" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(30),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: exercises Loop 25 long-pathing fixes (decklip/wall-support)."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_LightHope_To_Stratholme_Gate",
            Category: "LongTravel.EK",
            Description: "Light's Hope Chapel → EPL north road → Stratholme main gate.",
            MapId: 0,
            StartPosition: new Position(2275f, -5346f, 88f),
            EndPosition: new Position(3359f, -3380f, 144f),
            SetupCommands: new[] { ".character level 58", ".tele Light's Hope" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: EPL road has multiple Scourge encampments; aggro avoidance optional."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_EK_Darnassus_BoatStub_To_Stormwind_HarborStub",
            Category: "LongTravel.EK",
            Description: "Bridge test: Auberdine→Menethil boat lane + interior city ramp. NOTE: starting in EK because the boat ride completes there.",
            MapId: 0,
            StartPosition: new Position(-3792f, -832f, 10f), // Menethil dock
            EndPosition: new Position(-8833f, 622f, 94f), // SW Trade District
            SetupCommands: new[] { ".character level 25", ".tele Menethil Harbor" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(20),
            Transport: TransportMode.None,
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: pure-walk fallback for the Auberdine→SW route when boat is unavailable."
        ),

        // --------------------------------------------------------------------------
        // LONG-TRAVEL — KALIMDOR (continent map 1)
        // --------------------------------------------------------------------------

        new PathingTestDefinition(
            Name: "LongTravel_Kalimdor_Orgrimmar_To_ThunderBluff",
            Category: "LongTravel.Kalimdor",
            Description: "Orgrimmar → Durotar → Barrens → Mulgore → Thunder Bluff elevator.",
            MapId: 1,
            StartPosition: new Position(1633f, -4439f, 38f),
            EndPosition: new Position(-1290f, 75f, 127f),
            SetupCommands: new[] { ".character level 15", ".tele Orgrimmar", ".modify speed all 1.5" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(25),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: TB elevator off-mesh link required (not yet in offmesh.txt)."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_Kalimdor_Crossroads_To_Ratchet",
            Category: "LongTravel.Kalimdor",
            Description: "Barrens Crossroads → Ratchet (east road).",
            MapId: 1,
            StartPosition: new Position(-450f, -2645f, 96f),
            EndPosition: new Position(-996f, -3826f, 6f),
            SetupCommands: new[] { ".character level 15", ".tele The Barrens" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: short Barrens road, regression target."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_Kalimdor_Astranaar_To_Auberdine",
            Category: "LongTravel.Kalimdor",
            Description: "Ashenvale Astranaar → Darkshore Auberdine docks.",
            MapId: 1,
            StartPosition: new Position(2728f, -377f, 107f),
            EndPosition: new Position(6303f, 491f, 14f),
            SetupCommands: new[] { ".character level 25", ".tele Ashenvale", ".modify speed all 1.5" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(18),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: Ashenvale→Darkshore zone seam + Mystral Lake bypass."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_Kalimdor_Gadgetzan_To_CenarionHold",
            Category: "LongTravel.Kalimdor",
            Description: "Tanaris Gadgetzan → Un'Goro → Silithus Cenarion Hold. South Kalimdor end-game traverse.",
            MapId: 1,
            StartPosition: new Position(-7177f, -3779f, 9f),
            EndPosition: new Position(-6817f, 824f, 51f),
            SetupCommands: new[] { ".character level 55", ".tele Tanaris", ".modify speed all 2.0" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(35),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: Un'Goro crater rim descent + Silithus zone seam."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_Kalimdor_Everlook_To_Felwood_Timbermaw",
            Category: "LongTravel.Kalimdor",
            Description: "Winterspring Everlook → Timbermaw Hold tunnel → Felwood Whisperwind Grove.",
            MapId: 1,
            StartPosition: new Position(6717f, -4655f, 722f),
            EndPosition: new Position(5408f, -749f, 339f),
            SetupCommands: new[] { ".character level 55", ".tele Everlook" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(25),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: Timbermaw tunnel is a 3-stage off-mesh interior; faction-gated."
        ),

        new PathingTestDefinition(
            Name: "LongTravel_Kalimdor_Camp_Mojache_To_Maraudon",
            Category: "LongTravel.Kalimdor",
            Description: "Feralas Camp Mojache → Desolace → Maraudon entrance.",
            MapId: 1,
            StartPosition: new Position(-4400f, 252f, 36f),
            EndPosition: new Position(-1428f, 2607f, 76f),
            SetupCommands: new[] { ".character level 45", ".tele Feralas", ".modify speed all 1.5" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(20),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: cross-zone via Mok'thardin bridge."
        ),

        // --------------------------------------------------------------------------
        // CROSS-CONTINENT — boat / zeppelin chains
        // These extend the existing 4 Transport rows with end-to-end multi-leg trips.
        // --------------------------------------------------------------------------

        new PathingTestDefinition(
            Name: "CrossContinent_Stormwind_To_Auberdine_BoatChain",
            Category: "LongTravel.CrossContinent",
            Description: "Stormwind harbor → boat → Menethil → boat → Auberdine. Two consecutive boat handoffs.",
            MapId: 0,
            StartPosition: new Position(-8833f, 622f, 94f),
            EndPosition: new Position(6303f, 491f, 14f),
            EndMapId: 1,
            SetupCommands: new[] { ".character level 20", ".tele Stormwind", ".modify money 1000000" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(30),
            Transport: TransportMode.Boat,
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: multi-boat chain — needs two transport handoffs in sequence."
        ),

        new PathingTestDefinition(
            Name: "CrossContinent_Orgrimmar_To_Undercity_To_GromGol",
            Category: "LongTravel.CrossContinent",
            Description: "OG → zeppelin → UC → zeppelin → Grom'gol. Horde double-zeppelin.",
            MapId: 1,
            StartPosition: new Position(1633f, -4439f, 38f),
            EndPosition: new Position(-12420f, 129f, 4f),
            EndMapId: 0,
            SetupCommands: new[] { ".character level 25", ".tele Orgrimmar", ".modify money 1000000" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(25),
            Transport: TransportMode.Zeppelin,
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: relies on loop-24 OG zeppelin tile (40,29) off-mesh."
        ),

        new PathingTestDefinition(
            Name: "CrossContinent_Ironforge_To_DwarvenDistrict_To_AH",
            Category: "LongTravel.CrossContinent",
            Description: "Ironforge Mystic Ward → tram → Stormwind Dwarven District → Trade District AH.",
            MapId: 0,
            StartPosition: new Position(-4918f, -941f, 501f),
            EndPosition: new Position(-8833f, 622f, 94f),
            SetupCommands: new[] { ".character level 10", ".tele Ironforge", ".modify money 1000000" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(15),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: requires tram map 369 off-mesh links."
        ),

        new PathingTestDefinition(
            Name: "CrossContinent_TBoardElevator_To_Mulgore",
            Category: "LongTravel.CrossContinent",
            Description: "Thunder Bluff inner ring → elevator → Mulgore outdoor.",
            MapId: 1,
            StartPosition: new Position(-1290f, 75f, 127f),
            EndPosition: new Position(-2917f, -257f, 53f),
            SetupCommands: new[] { ".character level 5", ".tele Thunder Bluff" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: TB elevator off-mesh required (currently a placeholder in offmesh.txt:48-50)."
        ),

        // --------------------------------------------------------------------------
        // GRAND TOUR — multi-segment Waypoints chains (exercise the new chain runner)
        // --------------------------------------------------------------------------

        new PathingTestDefinition(
            Name: "GrandTour_Alliance_CapitalsAndHubs",
            Category: "GrandTour",
            Description: "Stormwind → Goldshire → Westfall (Sentinel Hill) → Duskwood (Darkshire) → Redridge (Lakeshire) → Loch Modan → Ironforge. 6-segment Alliance hub tour.",
            MapId: 0,
            StartPosition: new Position(-8833f, 622f, 94f),
            EndPosition: null,
            SetupCommands: new[] { ".character level 60", ".tele Stormwind", ".modify speed all 2.0" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(60),
            Waypoints: new[]
            {
                new NamedWaypoint("Stormwind_TradeDistrict", new Position(-8833f, 622f, 94f)),
                new NamedWaypoint("Goldshire", new Position(-9465f, 64f, 56f)),
                new NamedWaypoint("Sentinel_Hill_Westfall", new Position(-10663f, 1037f, 32f)),
                new NamedWaypoint("Darkshire_Duskwood", new Position(-10473f, -1156f, 36f)),
                new NamedWaypoint("Lakeshire_Redridge", new Position(-9248f, -2244f, 67f)),
                new NamedWaypoint("Thelsamar_LochModan", new Position(-4843f, -3475f, 305f)),
                new NamedWaypoint("Ironforge_MysticWard", new Position(-4918f, -941f, 501f)),
            },
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: exercises 6 zone seams + tram off-mesh. Largest single-continent chain."
        ),

        new PathingTestDefinition(
            Name: "GrandTour_Horde_CapitalsAndHubs",
            Category: "GrandTour",
            Description: "Orgrimmar → Crossroads → Ratchet → Camp Taurajo → Thunder Bluff → Mulgore → Stonetalon Sun Rock. 6-segment Horde hub tour.",
            MapId: 1,
            StartPosition: new Position(1633f, -4439f, 38f),
            EndPosition: null,
            SetupCommands: new[] { ".character level 60", ".tele Orgrimmar", ".modify speed all 2.0" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(55),
            Waypoints: new[]
            {
                new NamedWaypoint("Orgrimmar_ValleyOfStrength", new Position(1633f, -4439f, 38f)),
                new NamedWaypoint("Crossroads_Barrens", new Position(-450f, -2645f, 96f)),
                new NamedWaypoint("Ratchet_Barrens", new Position(-996f, -3826f, 6f)),
                new NamedWaypoint("Camp_Taurajo", new Position(-2381f, -1972f, 96f)),
                new NamedWaypoint("ThunderBluff_HighRise", new Position(-1290f, 75f, 127f)),
                new NamedWaypoint("BloodhoofVillage_Mulgore", new Position(-2382f, -348f, -9f)),
                new NamedWaypoint("SunRock_Stonetalon", new Position(919f, 940f, 105f)),
            },
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: exercises TB elevator off-mesh + 4 zone seams."
        ),

        // --------------------------------------------------------------------------
        // CAPITAL INTERIOR LOOPS — short multi-segment chains inside major cities.
        // Validate close-range pathfinding + interior off-mesh (banks, AHs, portals).
        // --------------------------------------------------------------------------

        new PathingTestDefinition(
            Name: "CapitalLoop_Stormwind_TradeAH_Bank_MageQuarter_Cathedral",
            Category: "CapitalLoop",
            Description: "Stormwind Trade District → AH → Bank → Mage Quarter → Cathedral Square loop.",
            MapId: 0,
            StartPosition: new Position(-8833f, 622f, 94f),
            EndPosition: null,
            SetupCommands: new[] { ".character level 10", ".tele Stormwind" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8),
            Waypoints: new[]
            {
                new NamedWaypoint("TradeDistrict_Fountain", new Position(-8833f, 622f, 94f)),
                new NamedWaypoint("Auction_House", new Position(-8740f, 661f, 97f)),
                new NamedWaypoint("Bank_StormwindCity", new Position(-8400f, 642f, 96f)),
                new NamedWaypoint("MageQuarter_PortalRoom", new Position(-9009f, 875f, 29f)),
                new NamedWaypoint("Cathedral_Square", new Position(-8519f, 833f, 105f)),
            },
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: short-range interior path; tests close-quarters obstacle dodge."
        ),

        new PathingTestDefinition(
            Name: "CapitalLoop_Orgrimmar_Valley_AH_Cleft_Drag",
            Category: "CapitalLoop",
            Description: "Orgrimmar Valley of Strength → AH → Cleft of Shadow → Drag → back.",
            MapId: 1,
            StartPosition: new Position(1633f, -4439f, 38f),
            EndPosition: null,
            SetupCommands: new[] { ".character level 10", ".tele Orgrimmar" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8),
            Waypoints: new[]
            {
                new NamedWaypoint("ValleyOfStrength_Fountain", new Position(1633f, -4439f, 38f)),
                new NamedWaypoint("Auction_House", new Position(1671f, -4346f, 60f)),
                new NamedWaypoint("CleftOfShadow_Entrance", new Position(1812f, -4418f, -18f)),
                new NamedWaypoint("Drag_Bank", new Position(1631f, -4376f, 31f)),
                new NamedWaypoint("ValleyOfWisdom_Thrall", new Position(1907f, -4254f, 88f)),
            },
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: interior path through Cleft of Shadow vertical transition (RFC portal nearby)."
        ),

        new PathingTestDefinition(
            Name: "CapitalLoop_Ironforge_Bank_AH_MysticWard_Forge",
            Category: "CapitalLoop",
            Description: "Ironforge tram-arrival → Commons Bank → AH → Mystic Ward → Great Forge.",
            MapId: 0,
            StartPosition: new Position(-4918f, -941f, 501f),
            EndPosition: null,
            SetupCommands: new[] { ".character level 10", ".tele Ironforge" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(7),
            Waypoints: new[]
            {
                new NamedWaypoint("Tram_Arrival", new Position(-4918f, -941f, 501f)),
                new NamedWaypoint("Commons_Bank", new Position(-4843f, -1106f, 501f)),
                new NamedWaypoint("Auction_House", new Position(-4710f, -1167f, 502f)),
                new NamedWaypoint("MysticWard_Inn", new Position(-4632f, -913f, 501f)),
                new NamedWaypoint("GreatForge_Throne", new Position(-4838f, -1170f, 502f)),
            },
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: interior path tests close-range obstacle avoidance + ring-shaped city geometry."
        ),

        new PathingTestDefinition(
            Name: "CapitalLoop_Undercity_Bank_AH_Apothecarium",
            Category: "CapitalLoop",
            Description: "Undercity zep-arrival → Bank → AH → Apothecarium → back to throne.",
            MapId: 0,
            StartPosition: new Position(2059f, 274f, 97f),
            EndPosition: null,
            SetupCommands: new[] { ".character level 10", ".tele Undercity" },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(8),
            Waypoints: new[]
            {
                new NamedWaypoint("Zep_Arrival_Tower", new Position(2059f, 274f, 97f)),
                new NamedWaypoint("Bank_Trade_Quarter", new Position(1602f, 240f, -52f)),
                new NamedWaypoint("Auction_House", new Position(1689f, 144f, -47f)),
                new NamedWaypoint("Apothecarium", new Position(1781f, 39f, -46f)),
                new NamedWaypoint("Royal_Quarter_Throne", new Position(1633f, 240f, -85f)),
            },
            Status: TestStatus.Experimental,
            StatusReason: "Phase 3: UC ring + elevator descent — exercises vertical interior nav."
        ),
    };

    /// <summary>
    /// Helper: produces a 1-segment Experimental dungeon scaffold row with a small in-bounds offset.
    /// Phase 2 replaces these with full boss-to-boss <c>Waypoints</c> chains via /go probing
    /// (see <c>docs/Plan/Pathfinding/COMPREHENSIVE_TEST_PLAN.md</c>).
    /// </summary>
    private static PathingTestDefinition ScaffoldDungeon(string name, string description, uint mapId, Position entrance, int level)
    {
        // 8-yard offset in +Y to give the runner a non-trivial path inside the instance.
        var nearby = new Position(entrance.X, entrance.Y + 8f, entrance.Z);
        return new PathingTestDefinition(
            Name: name,
            Category: "Dungeon",
            Description: description,
            MapId: mapId,
            StartPosition: entrance,
            EndPosition: nearby,
            SetupCommands: new[]
            {
                $".character level {level}",
                $".go xyz {entrance.X} {entrance.Y} {entrance.Z} {mapId}",
                ".modify hp 50000",
                ".modify mana 50000"
            },
            TeardownCommands: new[] { ".character delete" },
            ExpectedDuration: TimeSpan.FromMinutes(5),
            Status: TestStatus.Experimental,
            StatusReason: "Phase 2 scaffold: replace EndPosition with full Waypoints[] boss chain via /go probing."
        );
    }

    /// <summary>
    /// Gets a test definition by name.
    /// </summary>
    /// <param name="name">The test name</param>
    /// <returns>The test definition</returns>
    /// <exception cref="ArgumentException">Thrown when test name is not found</exception>
    public static PathingTestDefinition GetByName(string name)
        => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"Test not found: {name}", nameof(name));

    /// <summary>
    /// Gets all tests in a specific category.
    /// </summary>
    /// <param name="category">The category name</param>
    /// <returns>All tests in the specified category</returns>
    public static IEnumerable<PathingTestDefinition> GetByCategory(string category)
        => All.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets all test categories.
    /// </summary>
    public static IEnumerable<string> GetCategories()
        => All.Select(t => t.Category).Distinct();
}
