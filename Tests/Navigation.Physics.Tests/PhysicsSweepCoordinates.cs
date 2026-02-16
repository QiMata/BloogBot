// PhysicsSweepCoordinates.cs - Comprehensive coordinate list for physics calibration sweeps
// These coordinates are organized by terrain type and expected physics behavior.
// Use these with GeometryExtractionTests to gather realistic values for PhysicsEngine calibration.

namespace Navigation.Physics.Tests;

/// <summary>
/// Master list of world coordinates for systematic geometry extraction sweeps.
/// Organized by terrain characteristics to calibrate different physics behaviors:
/// - Slope angles and walkability thresholds
/// - Step heights and stair climbing
/// - Ground snapping distances
/// - Surface types (terrain, WMO floors, bridges)
/// </summary>
public static class PhysicsSweepCoordinates
{
    // ==========================================================================
    // FLAT TERRAIN - Ground Detection Calibration
    // ==========================================================================
    // Use these locations to establish baseline ground detection behavior.
    // Expected: Normal.Z ? 1.0, all surfaces walkable.

    public static class FlatTerrain
    {
        /// <summary>
        /// The Barrens - Crossroads area (open savanna, very flat)
        /// Excellent for baseline ground normal calibration
        /// </summary>
        public static readonly SweepLocation CrossroadsPlains = new(
            MapId: 1,
            CenterX: -442.0f,
            CenterY: -2598.0f,
            CenterZ: 96.0f,
            SweepRadius: 50.0f,
            Description: "Crossroads area - flat open terrain",
            ExpectedSlopeRange: (0, 10));

        /// <summary>
        /// Elwynn Forest - East of Goldshire (rolling hills, mostly flat)
        /// </summary>
        public static readonly SweepLocation ElwynnFields = new(
            MapId: 0,
            CenterX: -9200.0f,
            CenterY: 200.0f,
            CenterZ: 70.0f,
            SweepRadius: 50.0f,
            Description: "Elwynn Forest fields - gentle terrain",
            ExpectedSlopeRange: (0, 20));

        /// <summary>
        /// Mulgore - Open plains near Bloodhoof Village
        /// </summary>
        public static readonly SweepLocation MulgorePlains = new(
            MapId: 1,
            CenterX: -2338.0f,
            CenterY: -346.0f,
            CenterZ: -9.0f,
            SweepRadius: 50.0f,
            Description: "Mulgore plains - flat grassland",
            ExpectedSlopeRange: (0, 15));

        /// <summary>
        /// Tirisfal Glades - Open area near Brill
        /// </summary>
        public static readonly SweepLocation TirisfalFields = new(
            MapId: 0,
            CenterX: 2271.0f,
            CenterY: 323.0f,
            CenterZ: 35.0f,
            SweepRadius: 50.0f,
            Description: "Tirisfal Glades fields - flat terrain",
            ExpectedSlopeRange: (0, 15));

        /// <summary>
        /// Westfall - Farmland area (flat agricultural terrain)
        /// </summary>
        public static readonly SweepLocation WestfallFarms = new(
            MapId: 0,
            CenterX: -10647.0f,
            CenterY: 1024.0f,
            CenterZ: 34.0f,
            SweepRadius: 50.0f,
            Description: "Westfall farmland - flat agricultural terrain",
            ExpectedSlopeRange: (0, 15));
    }

    // ==========================================================================
    // GENTLE SLOPES (10-30°) - Normal Movement Calibration
    // ==========================================================================
    // Common terrain slopes that should be fully walkable with normal movement.

    public static class GentleSlopes
    {
        /// <summary>
        /// Dun Morogh - Coldridge Valley hillsides
        /// </summary>
        public static readonly SweepLocation ColdridgeHills = new(
            MapId: 0,
            CenterX: -6096.0f,
            CenterY: 377.0f,
            CenterZ: 395.0f,
            SweepRadius: 40.0f,
            Description: "Coldridge Valley hillsides",
            ExpectedSlopeRange: (10, 30));

        /// <summary>
        /// Teldrassil - Paths leading up from Dolanaar
        /// </summary>
        public static readonly SweepLocation DolanaarPaths = new(
            MapId: 1,
            CenterX: 9663.0f,
            CenterY: 855.0f,
            CenterZ: 1294.0f,
            SweepRadius: 30.0f,
            Description: "Teldrassil paths near Dolanaar",
            ExpectedSlopeRange: (10, 35));

        /// <summary>
        /// Durotar - Hills near Sen'jin Village
        /// </summary>
        public static readonly SweepLocation SenjinHills = new(
            MapId: 1,
            CenterX: -851.0f,
            CenterY: -4920.0f,
            CenterZ: 20.0f,
            SweepRadius: 40.0f,
            Description: "Durotar coastal hills",
            ExpectedSlopeRange: (15, 35));

        /// <summary>
        /// Redridge Mountains - Lakeshire area hillsides
        /// </summary>
        public static readonly SweepLocation RedridgeHills = new(
            MapId: 0,
            CenterX: -9300.0f,
            CenterY: -2200.0f,
            CenterZ: 70.0f,
            SweepRadius: 40.0f,
            Description: "Redridge lakeside hills",
            ExpectedSlopeRange: (15, 40));
    }

    // ==========================================================================
    // MODERATE SLOPES (30-50°) - Walkability Testing
    // ==========================================================================
    // Slopes that are walkable but getting close to the limit.

    public static class ModerateSlopes
    {
        /// <summary>
        /// Stranglethorn Vale - Hillsides near Booty Bay
        /// </summary>
        public static readonly SweepLocation StranglethornHills = new(
            MapId: 0,
            CenterX: -14354.0f,
            CenterY: 510.0f,
            CenterZ: 22.0f,
            SweepRadius: 35.0f,
            Description: "Stranglethorn hillsides",
            ExpectedSlopeRange: (30, 50));

        /// <summary>
        /// Ashenvale - Slopes near Astranaar
        /// </summary>
        public static readonly SweepLocation AshenvaleSlopes = new(
            MapId: 1,
            CenterX: 2682.0f,
            CenterY: -371.0f,
            CenterZ: 108.0f,
            SweepRadius: 35.0f,
            Description: "Ashenvale forest slopes",
            ExpectedSlopeRange: (25, 45));

        /// <summary>
        /// Hillsbrad Foothills - Mountain approaches
        /// </summary>
        public static readonly SweepLocation HillsbradSlopes = new(
            MapId: 0,
            CenterX: -699.0f,
            CenterY: -994.0f,
            CenterZ: 155.0f,
            SweepRadius: 40.0f,
            Description: "Hillsbrad mountain approaches",
            ExpectedSlopeRange: (30, 50));

        /// <summary>
        /// Stonetalon Mountains - Lower mountain paths
        /// </summary>
        public static readonly SweepLocation StonetalonLower = new(
            MapId: 1,
            CenterX: 930.0f,
            CenterY: 920.0f,
            CenterZ: 104.0f,
            SweepRadius: 35.0f,
            Description: "Stonetalon lower paths",
            ExpectedSlopeRange: (30, 50));
    }

    // ==========================================================================
    // STEEP SLOPES (50-60°) - Walkability Boundary Testing
    // ==========================================================================
    // Critical for calibrating the walkability threshold (60° = cos?¹(0.5)).

    public static class SteepSlopes
    {
        /// <summary>
        /// Thousand Needles - Mesa approaches (near the walkability limit)
        /// </summary>
        public static readonly SweepLocation ThousandNeedlesMesas = new(
            MapId: 1,
            CenterX: -5170.0f,
            CenterY: -2120.0f,
            CenterZ: -50.0f,
            SweepRadius: 30.0f,
            Description: "Thousand Needles mesa slopes - near 60° limit",
            ExpectedSlopeRange: (50, 65));

        /// <summary>
        /// Wetlands - Cliffs along the coast
        /// </summary>
        public static readonly SweepLocation WetlandsCliffs = new(
            MapId: 0,
            CenterX: -3674.0f,
            CenterY: -2524.0f,
            CenterZ: 20.0f,
            SweepRadius: 30.0f,
            Description: "Wetlands coastal cliffs",
            ExpectedSlopeRange: (45, 70));

        /// <summary>
        /// Feralas - Steep hillsides
        /// </summary>
        public static readonly SweepLocation FeralasSlopes = new(
            MapId: 1,
            CenterX: -4395.0f,
            CenterY: 162.0f,
            CenterZ: 25.0f,
            SweepRadius: 35.0f,
            Description: "Feralas steep hillsides",
            ExpectedSlopeRange: (50, 65));

        /// <summary>
        /// Searing Gorge - Rocky terrain with steep faces
        /// </summary>
        public static readonly SweepLocation SearingGorgeRocks = new(
            MapId: 0,
            CenterX: -6552.0f,
            CenterY: -1168.0f,
            CenterZ: 310.0f,
            SweepRadius: 30.0f,
            Description: "Searing Gorge rocky slopes",
            ExpectedSlopeRange: (45, 70));
    }

    // ==========================================================================
    // VERTICAL/NON-WALKABLE (>60°) - Wall Detection Calibration
    // ==========================================================================
    // Surfaces that should NOT be walkable and require slide behavior.

    public static class VerticalSurfaces
    {
        /// <summary>
        /// Un'Goro Crater - Crater walls
        /// </summary>
        public static readonly SweepLocation UngoroCraterWalls = new(
            MapId: 1,
            CenterX: -6200.0f,
            CenterY: -1090.0f,
            CenterZ: -270.0f,
            SweepRadius: 25.0f,
            Description: "Un'Goro crater walls - vertical faces",
            ExpectedSlopeRange: (70, 90));

        /// <summary>
        /// Tanaris - Cliff faces near Gadgetzan
        /// </summary>
        public static readonly SweepLocation TanarisCliffs = new(
            MapId: 1,
            CenterX: -7180.0f,
            CenterY: -3785.0f,
            CenterZ: 8.0f,
            SweepRadius: 25.0f,
            Description: "Tanaris cliff faces",
            ExpectedSlopeRange: (65, 90));

        /// <summary>
        /// Desolace - Rocky outcrops and cliff faces
        /// </summary>
        public static readonly SweepLocation DesolaceCliffs = new(
            MapId: 1,
            CenterX: -1863.0f,
            CenterY: 1568.0f,
            CenterZ: 59.0f,
            SweepRadius: 30.0f,
            Description: "Desolace cliff faces",
            ExpectedSlopeRange: (60, 85));

        /// <summary>
        /// Blasted Lands - Rocky formations
        /// </summary>
        public static readonly SweepLocation BlastedLandsRocks = new(
            MapId: 0,
            CenterX: -11184.0f,
            CenterY: -3259.0f,
            CenterZ: 7.0f,
            SweepRadius: 25.0f,
            Description: "Blasted Lands rock formations",
            ExpectedSlopeRange: (65, 90));
    }

    // ==========================================================================
    // STAIRS AND STEPS - Step Height Calibration
    // ==========================================================================
    // Locations with stairs/steps to calibrate StepHeight constant (2.125 yards).

    public static class StairsAndSteps
    {
        /// <summary>
        /// Stormwind Cathedral - Wide stone stairs
        /// </summary>
        public static readonly SweepLocation StormwindCathedralStairs = new(
            MapId: 0,
            CenterX: -8512.0f,
            CenterY: 860.0f,
            CenterZ: 109.0f,
            SweepRadius: 15.0f,
            Description: "Stormwind Cathedral stairs",
            ExpectedSlopeRange: (0, 90),  // Mix of flat and vertical
            HasStepGeometry: true);

        /// <summary>
        /// Orgrimmar - Ramps and steps in Valley of Strength
        /// </summary>
        public static readonly SweepLocation OrgrimmarRamps = new(
            MapId: 1,
            CenterX: 1629.0f,
            CenterY: -4373.0f,
            CenterZ: 31.0f,
            SweepRadius: 20.0f,
            Description: "Orgrimmar Valley of Strength ramps",
            ExpectedSlopeRange: (0, 60),
            HasStepGeometry: true);

        /// <summary>
        /// Ironforge - Great Forge ramps
        /// </summary>
        public static readonly SweepLocation IronforgeRamps = new(
            MapId: 0,
            CenterX: -4811.0f,
            CenterY: -1103.0f,
            CenterZ: 502.0f,
            SweepRadius: 20.0f,
            Description: "Ironforge Great Forge ramps",
            ExpectedSlopeRange: (0, 45),
            HasStepGeometry: true);

        /// <summary>
        /// Undercity - Spiral ramp system
        /// </summary>
        public static readonly SweepLocation UndercityRamps = new(
            MapId: 0,
            CenterX: 1586.0f,
            CenterY: 239.0f,
            CenterZ: -52.0f,
            SweepRadius: 25.0f,
            Description: "Undercity spiral ramps",
            ExpectedSlopeRange: (0, 40),
            HasStepGeometry: true);

        /// <summary>
        /// Thunder Bluff - Elevator platforms and rises
        /// </summary>
        public static readonly SweepLocation ThunderBluffRises = new(
            MapId: 1,
            CenterX: -1196.0f,
            CenterY: 29.0f,
            CenterZ: 177.0f,
            SweepRadius: 20.0f,
            Description: "Thunder Bluff rises and platforms",
            ExpectedSlopeRange: (0, 30),
            HasStepGeometry: true);

        /// <summary>
        /// Goldshire Inn - Indoor stairs
        /// </summary>
        public static readonly SweepLocation GoldshireInnStairs = new(
            MapId: 0,
            CenterX: -9460.0f,
            CenterY: 40.0f,
            CenterZ: 60.0f,
            SweepRadius: 10.0f,
            Description: "Goldshire Inn indoor stairs",
            ExpectedSlopeRange: (0, 90),
            HasStepGeometry: true);
    }

    // ==========================================================================
    // INDOOR/WMO FLOORS - WMO Collision Calibration
    // ==========================================================================
    // Interior spaces for testing WMO-specific geometry and step handling.

    public static class IndoorAreas
    {
        /// <summary>
        /// Stormwind Keep - Throne room (flat WMO floor)
        /// </summary>
        public static readonly SweepLocation StormwindKeep = new(
            MapId: 0,
            CenterX: -8443.0f,
            CenterY: 332.0f,
            CenterZ: 121.0f,
            SweepRadius: 15.0f,
            Description: "Stormwind Keep throne room",
            ExpectedSlopeRange: (0, 10),
            IsWmoInterior: true);

        /// <summary>
        /// Ironforge - Commons area (carved stone interior)
        /// </summary>
        public static readonly SweepLocation IronforgeCommons = new(
            MapId: 0,
            CenterX: -4918.0f,
            CenterY: -881.0f,
            CenterZ: 502.0f,
            SweepRadius: 20.0f,
            Description: "Ironforge Commons",
            ExpectedSlopeRange: (0, 15),
            IsWmoInterior: true);

        /// <summary>
        /// Undercity - Main ring (underground interior)
        /// </summary>
        public static readonly SweepLocation UndercityRing = new(
            MapId: 0,
            CenterX: 1586.0f,
            CenterY: 239.0f,
            CenterZ: -52.0f,
            SweepRadius: 25.0f,
            Description: "Undercity main ring",
            ExpectedSlopeRange: (0, 20),
            IsWmoInterior: true);

        /// <summary>
        /// Wailing Caverns - Cave interior
        /// </summary>
        public static readonly SweepLocation WailingCaverns = new(
            MapId: 1,
            CenterX: -740.0f,
            CenterY: -2214.0f,
            CenterZ: 16.0f,
            SweepRadius: 20.0f,
            Description: "Wailing Caverns entrance",
            ExpectedSlopeRange: (0, 45),
            IsWmoInterior: true);

        /// <summary>
        /// Fargodeep Mine - Mine interior with varied slopes
        /// </summary>
        public static readonly SweepLocation FargodeepMine = new(
            MapId: 0,
            CenterX: -9631.0f,
            CenterY: 21.0f,
            CenterZ: 57.0f,
            SweepRadius: 15.0f,
            Description: "Fargodeep Mine interior",
            ExpectedSlopeRange: (0, 50),
            IsWmoInterior: true);
    }

    // ==========================================================================
    // BRIDGES AND ELEVATED STRUCTURES - Gap Detection
    // ==========================================================================
    // For testing StepDownHeight (4.0 yards) and falling behavior.

    public static class BridgesAndElevated
    {
        /// <summary>
        /// Wetlands - Thandol Span bridge
        /// </summary>
        public static readonly SweepLocation ThandolSpan = new(
            MapId: 0,
            CenterX: -2534.0f,
            CenterY: -2420.0f,
            CenterZ: 82.0f,
            SweepRadius: 20.0f,
            Description: "Thandol Span bridge",
            ExpectedSlopeRange: (0, 20),
            HasElevatedGeometry: true);

        /// <summary>
        /// Stranglethorn - Gurubashi Arena bridges
        /// </summary>
        public static readonly SweepLocation GurubashiArena = new(
            MapId: 0,
            CenterX: -13234.0f,
            CenterY: 227.0f,
            CenterZ: 33.0f,
            SweepRadius: 15.0f,
            Description: "Gurubashi Arena walkways",
            ExpectedSlopeRange: (0, 30),
            HasElevatedGeometry: true);

        /// <summary>
        /// Darnassus - Tree platforms and bridges
        /// </summary>
        public static readonly SweepLocation DarnassusPlatforms = new(
            MapId: 1,
            CenterX: 9869.0f,
            CenterY: 2493.0f,
            CenterZ: 1316.0f,
            SweepRadius: 20.0f,
            Description: "Darnassus tree platforms",
            ExpectedSlopeRange: (0, 25),
            HasElevatedGeometry: true);

        /// <summary>
        /// Booty Bay - Dock walkways
        /// </summary>
        public static readonly SweepLocation BootyBayDocks = new(
            MapId: 0,
            CenterX: -14438.0f,
            CenterY: 471.0f,
            CenterZ: 15.0f,
            SweepRadius: 15.0f,
            Description: "Booty Bay dock walkways",
            ExpectedSlopeRange: (0, 20),
            HasElevatedGeometry: true);

        /// <summary>
        /// Ratchet - Pier and dock structures
        /// </summary>
        public static readonly SweepLocation RatchetDocks = new(
            MapId: 1,
            CenterX: -956.0f,
            CenterY: -3754.0f,
            CenterZ: 5.0f,
            SweepRadius: 15.0f,
            Description: "Ratchet dock structures",
            ExpectedSlopeRange: (0, 15),
            HasElevatedGeometry: true);
    }

    // ==========================================================================
    // WATER EDGES - Water Level Transition Calibration
    // ==========================================================================
    // For calibrating WaterLevelDelta (2.0 yards) and swimming transitions.

    public static class WaterEdges
    {
        /// <summary>
        /// Westfall - Coastal beach transition
        /// </summary>
        public static readonly SweepLocation WestfallCoast = new(
            MapId: 0,
            CenterX: -10383.0f,
            CenterY: 1088.0f,
            CenterZ: 1.0f,
            SweepRadius: 20.0f,
            Description: "Westfall coastal beach",
            ExpectedSlopeRange: (0, 30),
            HasWaterTransition: true);

        /// <summary>
        /// Loch Modan - Lake shore
        /// </summary>
        public static readonly SweepLocation LochModanShore = new(
            MapId: 0,
            CenterX: -5405.0f,
            CenterY: -2894.0f,
            CenterZ: 340.0f,
            SweepRadius: 25.0f,
            Description: "Loch Modan lake shore",
            ExpectedSlopeRange: (0, 35),
            HasWaterTransition: true);

        /// <summary>
        /// Ashenvale - River bank
        /// </summary>
        public static readonly SweepLocation AshenvaleRiver = new(
            MapId: 1,
            CenterX: 2300.0f,
            CenterY: -2100.0f,
            CenterZ: 90.0f,
            SweepRadius: 20.0f,
            Description: "Ashenvale river bank",
            ExpectedSlopeRange: (0, 40),
            HasWaterTransition: true);

        /// <summary>
        /// Stranglethorn - Beach/jungle transition
        /// </summary>
        public static readonly SweepLocation StranglethornBeach = new(
            MapId: 0,
            CenterX: -14300.0f,
            CenterY: -200.0f,
            CenterZ: 0.5f,
            SweepRadius: 25.0f,
            Description: "Stranglethorn beach",
            ExpectedSlopeRange: (0, 25),
            HasWaterTransition: true);

        /// <summary>
        /// Dustwallow Marsh - Swamp water edges
        /// </summary>
        public static readonly SweepLocation DustwallowSwamp = new(
            MapId: 1,
            CenterX: -3476.0f,
            CenterY: -4115.0f,
            CenterZ: 17.0f,
            SweepRadius: 30.0f,
            Description: "Dustwallow swamp edges",
            ExpectedSlopeRange: (0, 20),
            HasWaterTransition: true);
    }

    // ==========================================================================
    // MIXED TERRAIN - Complex Geometry Testing
    // ==========================================================================
    // Locations with varied geometry for comprehensive testing.

    public static class MixedTerrain
    {
        /// <summary>
        /// Stranglethorn - Zul'Gurub entrance area (jungle with ruins)
        /// </summary>
        public static readonly SweepLocation ZulGurubEntrance = new(
            MapId: 0,
            CenterX: -11916.0f,
            CenterY: -1215.0f,
            CenterZ: 92.0f,
            SweepRadius: 30.0f,
            Description: "Zul'Gurub entrance - ruins and jungle",
            ExpectedSlopeRange: (0, 70));

        /// <summary>
        /// Burning Steppes - Rocky terrain with lava edges
        /// </summary>
        public static readonly SweepLocation BurningSteppes = new(
            MapId: 0,
            CenterX: -8179.0f,
            CenterY: -2174.0f,
            CenterZ: 135.0f,
            SweepRadius: 35.0f,
            Description: "Burning Steppes rocky terrain",
            ExpectedSlopeRange: (0, 75));

        /// <summary>
        /// Eastern Plaguelands - Varied terrain with ruins
        /// </summary>
        public static readonly SweepLocation EasternPlaguelands = new(
            MapId: 0,
            CenterX: 2280.0f,
            CenterY: -5312.0f,
            CenterZ: 82.0f,
            SweepRadius: 40.0f,
            Description: "Eastern Plaguelands mixed terrain",
            ExpectedSlopeRange: (0, 60));

        /// <summary>
        /// Winterspring - Snow-covered mountains and valleys
        /// </summary>
        public static readonly SweepLocation Winterspring = new(
            MapId: 1,
            CenterX: 6724.0f,
            CenterY: -4620.0f,
            CenterZ: 721.0f,
            SweepRadius: 40.0f,
            Description: "Winterspring mountain terrain",
            ExpectedSlopeRange: (0, 65));

        /// <summary>
        /// Silithus - Desert with hive structures
        /// </summary>
        public static readonly SweepLocation Silithus = new(
            MapId: 1,
            CenterX: -6815.0f,
            CenterY: 836.0f,
            CenterZ: 50.0f,
            SweepRadius: 35.0f,
            Description: "Silithus desert terrain",
            ExpectedSlopeRange: (0, 50));
    }

    // ==========================================================================
    // DUNGEON ENTRANCES - Transition Zone Testing
    // ==========================================================================
    // Dungeon entrances where outdoor terrain meets WMO geometry.

    public static class DungeonEntrances
    {
        /// <summary>
        /// Deadmines - Moonbrook mine entrance
        /// </summary>
        public static readonly SweepLocation DeadminesEntrance = new(
            MapId: 0,
            CenterX: -11208.0f,
            CenterY: 1673.0f,
            CenterZ: 24.0f,
            SweepRadius: 15.0f,
            Description: "Deadmines entrance",
            ExpectedSlopeRange: (0, 60));

        /// <summary>
        /// Scarlet Monastery - Courtyard
        /// </summary>
        public static readonly SweepLocation ScarletMonastery = new(
            MapId: 0,
            CenterX: 2873.0f,
            CenterY: -764.0f,
            CenterZ: 160.0f,
            SweepRadius: 20.0f,
            Description: "Scarlet Monastery courtyard",
            ExpectedSlopeRange: (0, 45));

        /// <summary>
        /// Blackrock Mountain - Main entrance area
        /// </summary>
        public static readonly SweepLocation BlackrockEntrance = new(
            MapId: 0,
            CenterX: -7522.0f,
            CenterY: -1233.0f,
            CenterZ: 285.0f,
            SweepRadius: 25.0f,
            Description: "Blackrock Mountain entrance",
            ExpectedSlopeRange: (0, 70));

        /// <summary>
        /// Razorfen Kraul - Exterior approach
        /// </summary>
        public static readonly SweepLocation RazorfenKraul = new(
            MapId: 1,
            CenterX: -4470.0f,
            CenterY: -1677.0f,
            CenterZ: 86.0f,
            SweepRadius: 20.0f,
            Description: "Razorfen Kraul entrance",
            ExpectedSlopeRange: (0, 55));
    }

    // ==========================================================================
    // HELPER METHODS - Aggregate Collections
    // ==========================================================================

    /// <summary>
    /// Returns all sweep locations organized by category.
    /// Use for systematic full-world calibration runs.
    /// </summary>
    public static IEnumerable<SweepLocation> GetAllSweepLocations()
    {
        // Flat terrain
        yield return FlatTerrain.CrossroadsPlains;
        yield return FlatTerrain.ElwynnFields;
        yield return FlatTerrain.MulgorePlains;
        yield return FlatTerrain.TirisfalFields;
        yield return FlatTerrain.WestfallFarms;

        // Gentle slopes
        yield return GentleSlopes.ColdridgeHills;
        yield return GentleSlopes.DolanaarPaths;
        yield return GentleSlopes.SenjinHills;
        yield return GentleSlopes.RedridgeHills;

        // Moderate slopes
        yield return ModerateSlopes.StranglethornHills;
        yield return ModerateSlopes.AshenvaleSlopes;
        yield return ModerateSlopes.HillsbradSlopes;
        yield return ModerateSlopes.StonetalonLower;

        // Steep slopes
        yield return SteepSlopes.ThousandNeedlesMesas;
        yield return SteepSlopes.WetlandsCliffs;
        yield return SteepSlopes.FeralasSlopes;
        yield return SteepSlopes.SearingGorgeRocks;

        // Vertical surfaces
        yield return VerticalSurfaces.UngoroCraterWalls;
        yield return VerticalSurfaces.TanarisCliffs;
        yield return VerticalSurfaces.DesolaceCliffs;
        yield return VerticalSurfaces.BlastedLandsRocks;

        // Stairs and steps
        yield return StairsAndSteps.StormwindCathedralStairs;
        yield return StairsAndSteps.OrgrimmarRamps;
        yield return StairsAndSteps.IronforgeRamps;
        yield return StairsAndSteps.UndercityRamps;
        yield return StairsAndSteps.ThunderBluffRises;
        yield return StairsAndSteps.GoldshireInnStairs;

        // Indoor areas
        yield return IndoorAreas.StormwindKeep;
        yield return IndoorAreas.IronforgeCommons;
        yield return IndoorAreas.UndercityRing;
        yield return IndoorAreas.WailingCaverns;
        yield return IndoorAreas.FargodeepMine;

        // Bridges and elevated
        yield return BridgesAndElevated.ThandolSpan;
        yield return BridgesAndElevated.GurubashiArena;
        yield return BridgesAndElevated.DarnassusPlatforms;
        yield return BridgesAndElevated.BootyBayDocks;
        yield return BridgesAndElevated.RatchetDocks;

        // Water edges
        yield return WaterEdges.WestfallCoast;
        yield return WaterEdges.LochModanShore;
        yield return WaterEdges.AshenvaleRiver;
        yield return WaterEdges.StranglethornBeach;
        yield return WaterEdges.DustwallowSwamp;

        // Mixed terrain
        yield return MixedTerrain.ZulGurubEntrance;
        yield return MixedTerrain.BurningSteppes;
        yield return MixedTerrain.EasternPlaguelands;
        yield return MixedTerrain.Winterspring;
        yield return MixedTerrain.Silithus;

        // Dungeon entrances
        yield return DungeonEntrances.DeadminesEntrance;
        yield return DungeonEntrances.ScarletMonastery;
        yield return DungeonEntrances.BlackrockEntrance;
        yield return DungeonEntrances.RazorfenKraul;
    }

    /// <summary>
    /// Returns locations specifically useful for walkability threshold calibration.
    /// Focus on slopes near the 60° boundary.
    /// </summary>
    public static IEnumerable<SweepLocation> GetWalkabilityCalibrationLocations()
    {
        yield return ModerateSlopes.StranglethornHills;
        yield return ModerateSlopes.HillsbradSlopes;
        yield return SteepSlopes.ThousandNeedlesMesas;
        yield return SteepSlopes.WetlandsCliffs;
        yield return SteepSlopes.FeralasSlopes;
        yield return SteepSlopes.SearingGorgeRocks;
    }

    /// <summary>
    /// Returns locations for step height calibration.
    /// </summary>
    public static IEnumerable<SweepLocation> GetStepHeightCalibrationLocations()
    {
        yield return StairsAndSteps.StormwindCathedralStairs;
        yield return StairsAndSteps.OrgrimmarRamps;
        yield return StairsAndSteps.IronforgeRamps;
        yield return StairsAndSteps.UndercityRamps;
        yield return StairsAndSteps.ThunderBluffRises;
        yield return StairsAndSteps.GoldshireInnStairs;
    }

    /// <summary>
    /// Returns locations for water transition calibration.
    /// </summary>
    public static IEnumerable<SweepLocation> GetWaterTransitionLocations()
    {
        yield return WaterEdges.WestfallCoast;
        yield return WaterEdges.LochModanShore;
        yield return WaterEdges.AshenvaleRiver;
        yield return WaterEdges.StranglethornBeach;
        yield return WaterEdges.DustwallowSwamp;
    }
}

/// <summary>
/// Represents a location for geometry extraction sweep testing.
/// </summary>
public readonly record struct SweepLocation(
    uint MapId,
    float CenterX,
    float CenterY,
    float CenterZ,
    float SweepRadius,
    string Description,
    (int Min, int Max) ExpectedSlopeRange,
    bool HasStepGeometry = false,
    bool IsWmoInterior = false,
    bool HasElevatedGeometry = false,
    bool HasWaterTransition = false)
{
    /// <summary>
    /// Calculate tile coordinates for this location.
    /// </summary>
    public (uint TileX, uint TileY) GetTileCoordinates()
    {
        const float gridSize = 533.33333f;
        const int centerGrid = 32;

        uint tileX = (uint)(centerGrid - (CenterX / gridSize));
        uint tileY = (uint)(centerGrid - (CenterY / gridSize));

        return (tileX, tileY);
    }

    /// <summary>
    /// Get the bounding box for geometry queries.
    /// </summary>
    public (float MinX, float MinY, float MaxX, float MaxY) GetBoundingBox()
    {
        return (
            CenterX - SweepRadius,
            CenterY - SweepRadius,
            CenterX + SweepRadius,
            CenterY + SweepRadius);
    }

    /// <summary>
    /// Convert to WorldPosition for use with existing test infrastructure.
    /// </summary>
    public WorldPosition ToWorldPosition() => new(MapId, CenterX, CenterY, CenterZ);
}
