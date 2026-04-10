using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

/// <summary>
/// Splits full .scene files into tile-based .scenetile files.
/// Each tile is 533.33y × 533.33y, matching WoW's ADT grid (64×64).
///
/// Tile coordinate system (matches MaNGOS):
///   worldX = (32 - tileX) * TILE_SIZE
///   worldY = (32 - tileY) * TILE_SIZE
///   tileX = 32 - (int)floor(worldX / TILE_SIZE)
///   tileY = 32 - (int)floor(worldY / TILE_SIZE)
///
/// Output: scenes/tiles/{mapId}_{tileX:00}_{tileY:00}.scenetile
///
/// dotnet test --filter "FullyQualifiedName~SceneTileSplitter" --configuration Release -v n
/// </summary>
[Collection("PhysicsEngine")]
public class SceneTileSplitterTests
{
    private readonly ITestOutputHelper _output;
    public const float TILE_SIZE = 533.33333f;

    public SceneTileSplitterTests(ITestOutputHelper output) => _output = output;

    public static int WorldToTileX(float worldX) => 32 - (int)MathF.Floor(worldX / TILE_SIZE);
    public static int WorldToTileY(float worldY) => 32 - (int)MathF.Floor(worldY / TILE_SIZE);
    public static float TileMinX(int tileX) => (32 - tileX) * TILE_SIZE;
    public static float TileMinY(int tileY) => (32 - tileY) * TILE_SIZE;
    public static float TileMaxX(int tileX) => TileMinX(tileX) + TILE_SIZE;
    public static float TileMaxY(int tileY) => TileMinY(tileY) + TILE_SIZE;

    // All maps with .scene files need tile splitting for SceneDataService.
    // Open world (0, 1), battlegrounds (30, 489, 529), and all dungeons/raids.
    private static readonly uint[] MapsToSplit = [
        0, 1,                                          // Continents
        30, 489, 529,                                  // Battlegrounds (AV, WSG, AB)
        13, 33, 34, 36, 43, 47, 48, 70, 90,          // Dungeons (low)
        109, 129, 169, 189, 209, 229, 230, 289, 329, 349, // Dungeons (mid-high) + Emerald Dream
        369, 389, 429,                                 // Dungeons (misc)
        249, 309, 409, 449, 469, 509, 531, 533,       // Raids + PvP halls
    ];

    [Fact]
    [Trait("Category", "SceneExtraction")]
    public void SplitSceneFilesIntoTiles()
    {
        var dataDir = ResolveDataDir();
        Skip.If(string.IsNullOrEmpty(dataDir), "No data directory found");

        var scenesDir = Path.Combine(dataDir!, "scenes");
        var tilesDir = Path.Combine(scenesDir, "tiles");
        Directory.CreateDirectory(tilesDir);

        int totalTiles = 0, totalTriangles = 0;
        int totalNewTiles = 0;

        foreach (var mapId in ResolveMapsToSplit())
        {
            var scenePath = Path.Combine(scenesDir, $"{mapId}.scene");
            if (!File.Exists(scenePath))
            {
                _output.WriteLine($"  [Map {mapId}] SKIP — no .scene file");
                continue;
            }

            _output.WriteLine($"  [Map {mapId}] Loading .scene ({new FileInfo(scenePath).Length / 1024 / 1024}MB)...");

            // Load full scene into Navigation.dll
            bool loaded = LoadSceneCache(mapId, scenePath);
            if (!loaded)
            {
                _output.WriteLine($"  [Map {mapId}] FAILED to load .scene file");
                continue;
            }

            // Use brute-force bounds for ALL maps. The 5-point GroundZ sampling
            // misses tiles with only indoor/underground geometry (e.g. Stormwind
            // Champion's Hall), causing the physics engine to have no collision data
            // and bots to float after teleport.
            var populatedTiles = DiscoverTilesForMap(mapId, dataDir!, scenePath);
            _output.WriteLine($"  [Map {mapId}] {populatedTiles.Count} candidate tiles");

            int mapTiles = 0;
            int mapNewTiles = 0;
            foreach (var (tx, ty) in populatedTiles.OrderBy(t => t.tx).ThenBy(t => t.ty))
            {
                var tilePath = Path.Combine(tilesDir, $"{mapId}_{tx:D2}_{ty:D2}.scenetile");
                if (File.Exists(tilePath))
                {
                    mapTiles++;
                    continue; // Already split
                }

                float minX = TileMinX(tx);
                float minY = TileMinY(ty);
                float maxX = TileMaxX(tx);
                float maxY = TileMaxY(ty);

                // Extract this tile's triangles into a bounded .scene file
                bool ok = ExtractSceneCache(mapId, tilePath, minX, minY, maxX, maxY);
                if (ok && File.Exists(tilePath))
                {
                    var tileSize = new FileInfo(tilePath).Length;
                    mapTiles++;
                    mapNewTiles++;
                    totalTriangles += (int)(tileSize / 40); // rough estimate
                }
            }

            totalTiles += mapTiles;
            totalNewTiles += mapNewTiles;
            _output.WriteLine($"  [Map {mapId}] {mapTiles} tiles available ({mapNewTiles} new) in {tilesDir}");

            // Unload to free memory
            ClearSceneCache(mapId);
        }

        _output.WriteLine($"\nDone: {totalTiles} tiles ({totalNewTiles} new), ~{totalTriangles} triangles");
    }

    [Fact]
    [Trait("Category", "SceneExtraction")]
    public void ExtractExplicitSceneTiles()
    {
        var requestedTiles = ParseExplicitTileKeys();
        Skip.If(requestedTiles.Count == 0, "No explicit tile keys set (WWOW_SCENE_TILE_KEYS).");

        var dataDir = ResolveDataDir();
        Skip.If(string.IsNullOrEmpty(dataDir), "No data directory found");

        var scenesDir = Path.Combine(dataDir!, "scenes");
        var tilesDir = Path.Combine(scenesDir, "tiles");
        Directory.CreateDirectory(tilesDir);

        var loadedMaps = new HashSet<uint>();
        try
        {
            foreach (var (mapId, tileX, tileY) in requestedTiles)
            {
                var scenePath = Path.Combine(scenesDir, $"{mapId}.scene");
                if (File.Exists(scenePath) && !loadedMaps.Contains(mapId))
                {
                    if (!LoadSceneCache(mapId, scenePath))
                    {
                        throw new InvalidOperationException($"Failed to load source .scene for map {mapId}: {scenePath}");
                    }

                    loadedMaps.Add(mapId);
                }

                var tilePath = Path.Combine(tilesDir, $"{mapId}_{tileX:D2}_{tileY:D2}.scenetile");
                if (File.Exists(tilePath))
                {
                    _output.WriteLine($"  [Tile {mapId}_{tileX:D2}_{tileY:D2}] already exists");
                    continue;
                }

                float minX = TileMinX(tileX);
                float minY = TileMinY(tileY);
                float maxX = TileMaxX(tileX);
                float maxY = TileMaxY(tileY);

                bool ok = ExtractSceneCache(mapId, tilePath, minX, minY, maxX, maxY);
                Assert.True(ok, $"ExtractSceneCache failed for {mapId}_{tileX:D2}_{tileY:D2}");
                Assert.True(File.Exists(tilePath), $"Output tile not created: {tilePath}");

                _output.WriteLine($"  [Tile {mapId}_{tileX:D2}_{tileY:D2}] extracted ({new FileInfo(tilePath).Length} bytes)");
            }
        }
        finally
        {
            foreach (var mapId in loadedMaps)
            {
                ClearSceneCache(mapId);
            }
        }
    }

    [Theory]
    [InlineData(1629f, -4373f, 29, 41)]  // Orgrimmar
    [InlineData(-259f, -4350f, 33, 41)]  // Valley of Trials
    [InlineData(686f, -294f, 31, 33)]    // AV starting area
    [InlineData(0f, 0f, 32, 32)]         // Origin
    public void WorldToTile_CorrectMapping(float worldX, float worldY, int expectedTileX, int expectedTileY)
    {
        Assert.Equal(expectedTileX, WorldToTileX(worldX));
        Assert.Equal(expectedTileY, WorldToTileY(worldY));
    }

    [Fact]
    public void TileBounds_RoundTrip()
    {
        // Tile (29, 41) should cover Orgrimmar area
        int tx = WorldToTileX(1629f);
        int ty = WorldToTileY(-4373f);
        float minX = TileMinX(tx);
        float maxX = TileMaxX(tx);
        float minY = TileMinY(ty);
        float maxY = TileMaxY(ty);

        _output.WriteLine($"Tile ({tx},{ty}): X=[{minX:F1}, {maxX:F1}], Y=[{minY:F1}, {maxY:F1}]");

        // Orgrimmar at (1629, -4373) should be inside
        Assert.True(1629 >= minX && 1629 <= maxX, $"Org X={1629} not in [{minX:F1},{maxX:F1}]");
        Assert.True(-4373 >= minY && -4373 <= maxY, $"Org Y={-4373} not in [{minY:F1},{maxY:F1}]");
    }

    /// <summary>
    /// Discover populated tiles by scanning scene bounds from the loaded SceneCache.
    /// Uses GetGroundZ at tile centers to filter out empty tiles.
    /// </summary>
    private HashSet<(int tx, int ty)> DiscoverPopulatedTiles(uint mapId, string scenePath)
    {
        var tiles = new HashSet<(int, int)>();

        // Read scene bounds from file header (first 64 bytes)
        using var fs = File.OpenRead(scenePath);
        using var br = new BinaryReader(fs);
        br.ReadUInt32(); // magic
        br.ReadUInt32(); // version
        br.ReadUInt32(); // mapId
        br.ReadUInt32(); // triCount
        br.ReadSingle(); // cellSize
        br.ReadUInt32(); // cellsX
        br.ReadUInt32(); // cellsY
        br.ReadUInt32(); // triIdxCount
        br.ReadSingle(); // liquidCellSize
        br.ReadUInt32(); // liquidCellsX
        br.ReadUInt32(); // liquidCellsY
        float minX = br.ReadSingle();
        float minY = br.ReadSingle();
        float maxX = br.ReadSingle();
        float maxY = br.ReadSingle();

        _output.WriteLine($"    Scene bounds: X=[{minX:F0},{maxX:F0}] Y=[{minY:F0},{maxY:F0}]");

        int minTileX = WorldToTileX(maxX); // Note: inverted — larger world X = smaller tileX
        int maxTileX = WorldToTileX(minX);
        int minTileY = WorldToTileY(maxY);
        int maxTileY = WorldToTileY(minY);

        _output.WriteLine($"    Tile range: X=[{minTileX},{maxTileX}] Y=[{minTileY},{maxTileY}]");

        for (int tx = minTileX; tx <= maxTileX; tx++)
        {
            for (int ty = minTileY; ty <= maxTileY; ty++)
            {
                // Check center + 4 quadrant samples. Small dungeons may not cover the center.
                float x0 = TileMinX(tx), y0 = TileMinY(ty);
                float cx = x0 + TILE_SIZE / 2, cy = y0 + TILE_SIZE / 2;
                float q = TILE_SIZE / 4;
                bool found = false;
                foreach (var (sx, sy) in new[] { (cx, cy), (x0 + q, y0 + q), (x0 + 3 * q, y0 + q), (x0 + q, y0 + 3 * q), (x0 + 3 * q, y0 + 3 * q) })
                {
                    float gz = GetGroundZ(mapId, sx, sy, 500f, 1000f);
                    if (gz > -199000f) { found = true; break; }
                }
                if (found)
                    tiles.Add((tx, ty));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Fallback: return ALL tiles in the scene bounds without GetGroundZ validation.
    /// Used for small dungeons where the physics engine can't find ground at sample points.
    /// </summary>
    private HashSet<(int tx, int ty)> DiscoverAllTilesInBounds(string scenePath)
    {
        var tiles = new HashSet<(int, int)>();
        using var fs = File.OpenRead(scenePath);
        using var br = new BinaryReader(fs);
        // Skip: magic(4) + version(4) + mapId(4) + triCount(4) + cellSize(4) + cellsX(4) + cellsY(4) + triIdxCount(4) + liquidCellSize(4) + liquidCellsX(4) + liquidCellsY(4) = 44 bytes
        br.ReadBytes(44); // skip to bounds
        float minX = br.ReadSingle(), minY = br.ReadSingle();
        float maxX = br.ReadSingle(), maxY = br.ReadSingle();

        int minTileX = WorldToTileX(maxX);
        int maxTileX = WorldToTileX(minX);
        int minTileY = WorldToTileY(maxY);
        int maxTileY = WorldToTileY(minY);

        for (int tx = minTileX; tx <= maxTileX; tx++)
            for (int ty = minTileY; ty <= maxTileY; ty++)
                tiles.Add((tx, ty));

        return tiles;
    }

    private HashSet<(int tx, int ty)> DiscoverTilesForMap(uint mapId, string dataDir, string scenePath)
    {
        // Prefer ADT map-file discovery for full continent coverage. Some .scene files are
        // intentionally bounded and do not include all populated map tiles.
        var tilesFromMapFiles = DiscoverTilesFromMapFiles(dataDir, mapId);
        if (tilesFromMapFiles.Count > 0)
        {
            _output.WriteLine($"    Tile source: maps/ ({tilesFromMapFiles.Count} tiles)");
            return tilesFromMapFiles;
        }

        var tilesFromBounds = DiscoverAllTilesInBounds(scenePath);
        _output.WriteLine($"    Tile source: .scene bounds ({tilesFromBounds.Count} tiles)");
        return tilesFromBounds;
    }

    private static HashSet<(int tx, int ty)> DiscoverTilesFromMapFiles(string dataDir, uint mapId)
    {
        var mapsDir = Path.Combine(dataDir, "maps");
        if (!Directory.Exists(mapsDir))
            return [];

        var tiles = new HashSet<(int, int)>();
        var prefix = mapId.ToString("D3", CultureInfo.InvariantCulture);

        foreach (var path in Directory.EnumerateFiles(mapsDir, $"{prefix}????.map"))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (stem.Length < 7)
                continue;

            if (!int.TryParse(stem.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var tileX))
                continue;

            if (!int.TryParse(stem.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var tileY))
                continue;

            if (tileX is < 0 or > 63 || tileY is < 0 or > 63)
                continue;

            tiles.Add((tileX, tileY));
        }

        return tiles;
    }

    private List<(uint mapId, int tileX, int tileY)> ParseExplicitTileKeys()
    {
        var explicitTiles = Environment.GetEnvironmentVariable("WWOW_SCENE_TILE_KEYS");
        if (string.IsNullOrWhiteSpace(explicitTiles))
            return [];

        var parsed = new List<(uint mapId, int tileX, int tileY)>();
        foreach (var token in explicitTiles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                _output.WriteLine($"  [ExplicitTiles] SKIP invalid token: {token}");
                continue;
            }

            if (!uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var mapId) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tileX) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var tileY))
            {
                _output.WriteLine($"  [ExplicitTiles] SKIP unparsable token: {token}");
                continue;
            }

            if (tileX is < 0 or > 63 || tileY is < 0 or > 63)
            {
                _output.WriteLine($"  [ExplicitTiles] SKIP out-of-range tile token: {token}");
                continue;
            }

            parsed.Add((mapId, tileX, tileY));
        }

        return parsed.Distinct().ToList();
    }

    private IEnumerable<uint> ResolveMapsToSplit()
    {
        var overrideMaps = Environment.GetEnvironmentVariable("WWOW_SCENE_TILE_MAP_IDS");
        if (string.IsNullOrWhiteSpace(overrideMaps))
            return MapsToSplit;

        var parsed = overrideMaps
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? (uint?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        return parsed.Length > 0 ? parsed : MapsToSplit;
    }

    private static string? ResolveDataDir()
    {
        var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (!string.IsNullOrEmpty(dataDir) && Directory.Exists(dataDir)) return dataDir;

        var candidates = new[]
        {
            @"E:\repos\Westworld of Warcraft\Data",
            @"D:\MaNGOS\data",
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private const string NavDll = "Navigation.dll";

    [DllImport(NavDll, EntryPoint = "ClearSceneCache", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ClearSceneCache(uint mapId);
}
