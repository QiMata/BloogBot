using GameData.Core.Models;

namespace WWoW.RecordedTests.PathingTests.Models;

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
        )
    };

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
