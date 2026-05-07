using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Tests.Infrastructure;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
[Trait(TestCategories.Feature, TestCategories.NativeDll)]
public sealed class DetourCompatibilityTests(PhysicsEngineFixture fixture)
{
    private const uint MmapMagic = 0x4D4D4150; // MMAP
    private const uint DetourNavMeshMagic = 0x444E4156; // DNAV
    private const uint MmapVersion = 6;
    private const uint DetourNavMeshVersion = 7;

    private readonly PhysicsEngineFixture _fixture = fixture;

    [Fact]
    public void CompiledDetourAbi_UsesVersion7TilesVersion6WrappersAndCurrent64BitPolyRefs()
    {
        Assert.True(NavigationInterop.GetDetourCompatibilityInfo(out var info));

        Assert.Equal((uint)System.Runtime.InteropServices.Marshal.SizeOf<NavigationInterop.DetourCompatibilityInfo>(), info.StructSize);
        Assert.Equal(MmapMagic, info.MmapMagic);
        Assert.Equal(MmapVersion, info.MmapVersion);
        Assert.Equal(DetourNavMeshMagic, info.DetourNavMeshMagic);
        Assert.Equal(DetourNavMeshVersion, info.DetourNavMeshVersion);
        Assert.Equal(1u, info.DetourNavMeshStateVersion);
        Assert.Equal(4u, info.DtStatusSize);
        Assert.Equal(8u, info.DtPolyRefSize);
        Assert.Equal(8u, info.DtTileRefSize);
        Assert.Equal(20u, info.MmapTileHeaderSize);
        Assert.Equal(8u, info.NativePointerSize);
        Assert.Equal(1u, info.PolyRef64Enabled);
        Assert.Equal(6u, info.MaxVertsPerPolygon);
        Assert.Equal(64u, info.MaxAreas);
        Assert.Equal(16u, info.DtSaltBits);
        Assert.Equal(28u, info.DtTileBits);
        Assert.Equal(20u, info.DtPolyBits);
        Assert.Equal(1u, info.SupportsSlicedPathfinding);
        Assert.Equal(1u, info.SupportsPathCorridor);
        Assert.Equal(1u, info.SupportsAnyAngle);
        Assert.Equal(1u, info.SupportsRaycastCosts);
        Assert.Equal(1u, info.SupportsDistanceToWall);
        Assert.Equal(1u, info.SupportsLocalNeighbourhood);
    }

    [Fact]
    public void NativeMMapTileProbe_LoadsCurrentTileHeaderWithVersion7AndCurrent64BitRefs()
    {
        Assert.True(_fixture.IsInitialized, "Navigation.dll must load before probing mmap tile compatibility.");
        Assert.True(NavigationInterop.GetDetourCompatibilityInfo(out var abi));

        var (mapId, tileX, tileY, tilePath) = FindProbeTile();

        Assert.True(
            NavigationInterop.ProbeMMapTileCompatibility(mapId, tileX, tileY, out var tile),
            $"Native mmap tile probe did not find or load {tilePath}.");

        Assert.Equal((uint)System.Runtime.InteropServices.Marshal.SizeOf<NavigationInterop.MMapTileCompatibilityInfo>(), tile.StructSize);
        Assert.Equal(mapId, tile.MapId);
        Assert.Equal(tileX, tile.TileX);
        Assert.Equal(tileY, tile.TileY);
        Assert.Equal(1u, tile.FileFound);
        Assert.Equal(MmapMagic, tile.FileMmapMagic);
        Assert.Equal(abi.MmapVersion, tile.FileMmapVersion);
        Assert.Equal(DetourNavMeshVersion, tile.FileDetourVersion);
        Assert.True(tile.FileTileDataSize > 0, $"Expected {tilePath} to contain Detour tile data.");
        Assert.True(tile.FileUsesLiquids == 0u || tile.FileUsesLiquids == 1u, $"Expected usesLiquids to be a uint boolean, got {tile.FileUsesLiquids}.");
        Assert.Equal(1u, tile.HeaderCompatible);

        Assert.Equal(1u, tile.LoadSucceeded);
        Assert.True(tile.LoadedTileCountAtGrid > 0, $"Expected native Detour to expose tile grid {tileX},{tileY}.");
        Assert.Equal(8u, abi.DtTileRefSize);
        Assert.Equal(8u, abi.DtPolyRefSize);
        Assert.Equal(1u, abi.PolyRef64Enabled);

        Assert.Equal((int)DetourNavMeshMagic, tile.DetourHeaderMagic);
        Assert.Equal((int)DetourNavMeshVersion, tile.DetourHeaderVersion);
        Assert.True(tile.DetourHeaderPolyCount > 0);
        Assert.True(tile.DetourHeaderVertCount > 0);
        Assert.True(tile.DetourHeaderWalkableRadius > 0.0f);
        Assert.True(tile.DetourHeaderWalkableHeight > 0.0f);
    }

    private static (uint MapId, int TileX, int TileY, string Path) FindProbeTile()
    {
        PhysicsEngineFixture.EnsureDataDir();

        var dataRoot = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        Assert.False(string.IsNullOrWhiteSpace(dataRoot), "WWOW_DATA_DIR must point at a nav data root with mmaps.");

        var mmapsDir = Path.Combine(dataRoot!, "mmaps");
        Assert.True(Directory.Exists(mmapsDir), $"Expected mmaps directory at {mmapsDir}.");

        var candidate = Directory.EnumerateFiles(mmapsDir, "*.mmtile")
            .Select(ParseProbeTileOrNull)
            .Where(parsed => parsed.HasValue)
            .OrderBy(parsed => parsed!.Value.MapId is 0 or 1 ? 0 : 1)
            .ThenBy(parsed => parsed!.Value.MapId)
            .ThenBy(parsed => parsed!.Value.TileX)
            .ThenBy(parsed => parsed!.Value.TileY)
            .FirstOrDefault();

        Assert.NotNull(candidate);
        return candidate!.Value;
    }

    private static (uint MapId, int TileX, int TileY, string Path)? ParseProbeTileOrNull(string path)
    {
        return TryParseMMapTile(path, out var parsed) ? parsed : null;
    }

    private static bool TryParseMMapTile(
        string path,
        out (uint MapId, int TileX, int TileY, string Path) parsed)
    {
        parsed = default;

        var stem = Path.GetFileNameWithoutExtension(path);
        if (stem.Length != 7)
            return false;

        if (!uint.TryParse(stem[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var mapId))
            return false;
        if (!int.TryParse(stem.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var tileX))
            return false;
        if (!int.TryParse(stem.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var tileY))
            return false;

        parsed = (mapId, tileX, tileY, path);
        return true;
    }
}
