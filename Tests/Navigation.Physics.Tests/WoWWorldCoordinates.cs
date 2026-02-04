// WoWWorldCoordinates.cs - Known WoW world coordinates for physics testing
// These are real locations in the game world with documented expected behaviors.

namespace Navigation.Physics.Tests;

/// <summary>
/// Known WoW world coordinates for reproducible physics tests.
/// All coordinates are from WoW 1.12.1 (Vanilla) / 2.4.3 (TBC) / 3.3.5a (WotLK).
/// 
/// Coordinate system:
/// - X: East/West (positive = East)
/// - Y: North/South (positive = North)  
/// - Z: Up/Down (positive = Up)
/// - Orientation: Radians, 0 = North, increases counter-clockwise
/// </summary>
public static class WoWWorldCoordinates
{
    // ==========================================================================
    // ELWYNN FOREST (Map ID: 0 - Eastern Kingdoms)
    // ==========================================================================

    public static class ElwynnForest
    {
        public const uint MapId = 0;

        /// <summary>
        /// Northshire Abbey - Human starting area
        /// Flat terrain with buildings, stairs, and paths
        /// </summary>
        public static class NorthshireAbbey
        {
            // Spawn point for new human characters
            public static readonly WorldPosition SpawnPoint = new(
                MapId, -8949.95f, -132.493f, 83.5312f);

            // In front of the abbey building (flat ground)
            public static readonly WorldPosition AbbeyEntrance = new(
                MapId, -8921.0f, -134.0f, 82.0f);

            // On the abbey steps (for step-up testing)
            public static readonly WorldPosition AbbeyStepsBottom = new(
                MapId, -8915.0f, -135.0f, 82.0f);
            
            public static readonly WorldPosition AbbeyStepsTop = new(
                MapId, -8910.0f, -135.0f, 84.0f);

            // Path leading out of Northshire
            public static readonly WorldPosition PathToGoldshire = new(
                MapId, -8900.0f, -180.0f, 81.0f);
        }

        /// <summary>
        /// Goldshire - Town with inn, buildings, paths
        /// </summary>
        public static class Goldshire
        {
            // Center of Goldshire near the inn
            public static readonly WorldPosition TownCenter = new(
                MapId, -9464.0f, 62.0f, 56.0f);

            // Inside the Lion's Pride Inn (ground floor)
            public static readonly WorldPosition InnGroundFloor = new(
                MapId, -9457.0f, 43.0f, 57.0f);

            // Inn stairs (for indoor step testing)
            public static readonly WorldPosition InnStairsBottom = new(
                MapId, -9460.0f, 40.0f, 57.0f);

            public static readonly WorldPosition InnStairsTop = new(
                MapId, -9462.0f, 38.0f, 62.0f);
        }
    }

    // ==========================================================================
    // DUROTAR (Map ID: 1 - Kalimdor)
    // ==========================================================================

    public static class Durotar
    {
        public const uint MapId = 1;

        /// <summary>
        /// Valley of Trials - Orc/Troll starting area
        /// Rocky terrain with caves and slopes
        /// </summary>
        public static class ValleyOfTrials
        {
            // Spawn point for new orc characters
            public static readonly WorldPosition SpawnPoint = new(
                MapId, -618.518f, -4251.67f, 38.718f);

            // Near the Den (cave entrance)
            public static readonly WorldPosition DenEntrance = new(
                MapId, -600.0f, -4230.0f, 39.0f);

            // Sloped terrain for walkability testing
            public static readonly WorldPosition SlopeBottom = new(
                MapId, -590.0f, -4280.0f, 35.0f);

            public static readonly WorldPosition SlopeTop = new(
                MapId, -570.0f, -4280.0f, 45.0f);
        }

        /// <summary>
        /// Orgrimmar - Orc capital city
        /// Complex terrain with ramps, bridges, elevators
        /// </summary>
        public static class Orgrimmar
        {
            // Main gate entrance
            public static readonly WorldPosition MainGate = new(
                MapId, 1503.0f, -4415.0f, 22.0f);

            // Valley of Strength (central area)
            public static readonly WorldPosition ValleyOfStrength = new(
                MapId, 1629.0f, -4373.0f, 31.0f);

            // Ramp to upper level (for slope testing)
            public static readonly WorldPosition RampBottom = new(
                MapId, 1580.0f, -4400.0f, 25.0f);

            public static readonly WorldPosition RampTop = new(
                MapId, 1600.0f, -4380.0f, 35.0f);
        }
    }

    // ==========================================================================
    // STORMWIND CITY (Map ID: 0 - Eastern Kingdoms)
    // ==========================================================================

    public static class StormwindCity
    {
        public const uint MapId = 0;

        // Main gate entrance
        public static readonly WorldPosition MainGate = new(
            MapId, -8913.0f, 554.0f, 94.0f);

        // Trade District (flat urban terrain)
        public static readonly WorldPosition TradeDistrict = new(
            MapId, -8811.0f, 667.0f, 97.0f);

        // Cathedral steps (prominent stairs)
        public static readonly WorldPosition CathedralStepsBottom = new(
            MapId, -8512.0f, 848.0f, 106.0f);

        public static readonly WorldPosition CathedralStepsTop = new(
            MapId, -8512.0f, 870.0f, 112.0f);

        // Stockade entrance (doorway testing)
        public static readonly WorldPosition StockadeEntrance = new(
            MapId, -8764.0f, 844.0f, 88.0f);

        // Harbor dock (near water for swimming transition)
        public static readonly WorldPosition HarborDock = new(
            MapId, -8627.0f, 978.0f, 0.0f);
    }

    // ==========================================================================
    // IRONFORGE (Map ID: 0 - Eastern Kingdoms)
    // ==========================================================================

    public static class Ironforge
    {
        public const uint MapId = 0;

        // Main entrance from Dun Morogh
        public static readonly WorldPosition MainEntrance = new(
            MapId, -5039.0f, -819.0f, 495.0f);

        // The Great Forge (central area with ramps)
        public static readonly WorldPosition GreatForge = new(
            MapId, -4811.0f, -1103.0f, 502.0f);

        // Commons area (indoor flat terrain)
        public static readonly WorldPosition Commons = new(
            MapId, -4918.0f, -881.0f, 502.0f);

        // Ramp from Commons to Military Ward
        public static readonly WorldPosition RampToMilitaryWard = new(
            MapId, -4850.0f, -910.0f, 502.0f);
    }

    // ==========================================================================
    // SPECIAL TEST LOCATIONS
    // ==========================================================================

    /// <summary>
    /// Locations specifically chosen for testing edge cases
    /// </summary>
    public static class TestLocations
    {
        // Flat open terrain (Barrens)
        public static readonly WorldPosition FlatTerrain = new(
            1, -442.0f, -2598.0f, 96.0f);  // Crossroads area

        // Steep cliff (Thousand Needles approach)
        public static readonly WorldPosition SteepCliff = new(
            1, -5170.0f, -2120.0f, 90.0f);

        // Water edge (Westfall coast)
        public static readonly WorldPosition WaterEdge = new(
            0, -10383.0f, 1088.0f, 1.0f);

        // Narrow bridge (Wetlands)
        public static readonly WorldPosition NarrowBridge = new(
            0, -3674.0f, -2524.0f, 7.0f);

        // Cave interior (Fargodeep Mine)
        public static readonly WorldPosition CaveInterior = new(
            0, -9631.0f, 21.0f, 57.0f);
    }
}

/// <summary>
/// Represents a position in the WoW world
/// </summary>
public readonly struct WorldPosition
{
    public readonly uint MapId;
    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly float Orientation;

    public WorldPosition(uint mapId, float x, float y, float z, float orientation = 0)
    {
        MapId = mapId;
        X = x;
        Y = y;
        Z = z;
        Orientation = orientation;
    }

    /// <summary>
    /// Calculate grid tile coordinates for this position
    /// </summary>
    public (uint TileX, uint TileY) GetTileCoordinates()
    {
        // WoW uses a 64x64 grid, with coordinates offset
        const float gridSize = 533.33333f;
        const int centerGrid = 32;

        uint tileX = (uint)(centerGrid - (X / gridSize));
        uint tileY = (uint)(centerGrid - (Y / gridSize));

        return (tileX, tileY);
    }

    public NavigationInterop.Vector3 ToVector3() => new(X, Y, Z);

    public override string ToString() => $"Map {MapId}: ({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>
/// Represents a movement path between two points with expected frame data
/// </summary>
public class MovementPath
{
    public WorldPosition Start { get; init; }
    public WorldPosition End { get; init; }
    public float MoveSpeed { get; init; } = 7.0f;  // Default run speed
    public float DeltaTime { get; init; } = 1.0f / 60.0f;  // 60 FPS
    public NavigationInterop.MoveFlags Flags { get; init; } = NavigationInterop.MoveFlags.Forward;

    /// <summary>
    /// Expected positions at each frame along the path.
    /// These are manually verified or captured from the real client.
    /// </summary>
    public List<ExpectedFrame> ExpectedFrames { get; init; } = [];

    public float ExpectedDuration => (End.ToVector3() - Start.ToVector3()).Length() / MoveSpeed;
    public int ExpectedFrameCount => (int)MathF.Ceiling(ExpectedDuration / DeltaTime);
}

/// <summary>
/// Expected state at a specific frame during movement
/// </summary>
public class ExpectedFrame
{
    public int FrameNumber { get; init; }
    public float Time { get; init; }
    public NavigationInterop.Vector3 Position { get; init; }
    public NavigationInterop.Vector3 Velocity { get; init; }
    public bool IsGrounded { get; init; }
    public bool IsSwimming { get; init; }
    public float Tolerance { get; init; } = 0.1f;  // Position tolerance in yards

    public bool PositionMatches(NavigationInterop.Vector3 actual)
    {
        var diff = actual - Position;
        return diff.Length() <= Tolerance;
    }
}
