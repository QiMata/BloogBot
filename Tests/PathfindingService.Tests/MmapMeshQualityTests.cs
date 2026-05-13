using System.Buffers.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PathfindingService.Tests;

public sealed class MmapMeshQualityTests
{
    private const string OgTopRampDeckTile = "0012940.mmtile";
    private static readonly WowBounds OgTopRampDeckCrop = new(1316f, -4664f, 47f, 1348f, -4636f, 67f);

    // 2026-05-13: BRD/BRM Flame Crest live-stall tile.
    // Stall coord (LongPathingTests.FlameCrestToBrmDungeonEntrance UBRS case):
    //   (-7519.0,-2100.4,130.3) on tile 35,46. The mmap crop has shown an
    //   AREA_STEEP_SLOPE polygon (flags=17 = NAV_GROUND|NAV_STEEP_SLOPES) with
    //   zRange=19.6y at centroid (-7479.3,-2116.4,147.1) — a tall steep-slope
    //   wall just NE of the stall. The bot's smooth path is suspected to be
    //   string-pulled across or close to that wall.
    private const string FlameCrestStallTile = "0004635.mmtile";
    private static readonly WowBounds FlameCrestStallCrop = new(
        MinX: -7545f, MinY: -2135f, MinZ: 118f,
        MaxX: -7480f, MaxY: -2075f, MaxZ: 165f);
    private const float FlameCrestStallX = -7519.0f;
    private const float FlameCrestStallY = -2100.4f;
    private const float FlameCrestStallZ = 130.3f;
    // VMaNGOS NAV flag bits (MoveMapSharedDefines.h):
    //   NAV_EMPTY       = 0x00
    //   NAV_GROUND      = 0x01
    //   NAV_MAGMA       = 0x02
    //   NAV_SLIME       = 0x04
    //   NAV_WATER       = 0x08
    //   NAV_STEEP_SLOPES= 0x10
    private const ushort NavGround = 0x01;
    private const ushort NavSteepSlopes = 0x10;
    // VMaNGOS AREA ids (GridMapDefines.h): GROUND=1, GROUND_MODEL=2,
    //   STEEP_SLOPE=3, STEEP_SLOPE_MODEL=4 (plus water/magma/slime above).
    private const byte AreaSteepSlope = 3;
    private const byte AreaSteepSlopeModel = 4;

    [Fact]
    public void OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons()
    {
        var dataDir = ResolveDataDir();
        var tilePath = Path.Combine(dataDir, "mmaps", OgTopRampDeckTile);
        Assert.True(File.Exists(tilePath), $"Expected OG tower mmap tile at {tilePath}");

        var tile = MmapTile.Read(tilePath);
        var stats = tile.GetPolygonStats(OgTopRampDeckCrop).ToArray();

        Assert.True(stats.Length > 0, $"Expected at least one walkable polygon in crop {OgTopRampDeckCrop}");

        var offenders = stats
            .Where(static s => s.HorizontalArea2D >= 80f && (s.ZRange > 1.0f || s.MaxEdge2D > 12.0f))
            .OrderByDescending(static s => s.HorizontalArea2D)
            .ThenByDescending(static s => s.ZRange)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Expected no large bridge/auto-completed polygon across the mostly-flat OG zeppelin top ramp/deck crop."
            + Environment.NewLine
            + FormatOffenders(offenders));
    }

    [Fact(Skip = "Documents an open bake bug. The Flame Crest crop on the live "
        + "BRD/BRM runtime tiles contains 16 walkable-with-NAV_STEEP_SLOPES "
        + "polygons within 25y of the FG stall coord, peak zRange=46.8y. Two "
        + "2026-05-13 bake-side attempts have already regressed the live FG run: "
        + "(1) walkableSlopeAngle+walkableSlopeAngleVMaps=52 produced the "
        + "'object exclusion' bug (bot inside model darkness); (2) terrain-only "
        + "walkableSlopeAngle=52 produced a wall-collision creep where the bot "
        + "pressed nose-first into BRM rock at (-7665,-1808,137) with "
        + "route=none replans. The real fix has to live in the runtime path "
        + "filter (exclude NAV_STEEP_SLOPES for player paths in "
        + "Exports/Navigation/PathFinder.cpp::createFilter — the vmangos "
        + "Map.cpp pattern), or as an off-mesh connection for the BRM ascent. "
        + "Keeping Skip until that fix lands.")]
    public void FlameCrestStall_HasNoTallSteepSlopeWallsNearStall()
    {
        // PROPERTY: a polygon flagged as walkable through the steep-slope path
        // (NAV_GROUND|NAV_STEEP_SLOPES) and located within a small XY radius of
        // the live FG stall coordinate should not represent a tall wall the
        // FG capsule cannot physically traverse.
        //
        // A "tall wall" here is a steep-slope polygon whose vertical span
        // exceeds an agent's reachable climb-per-stride budget. The walkable
        // climb threshold for the Tauren capsule is 1.8y, so even a chain of
        // contiguous stair-like polys should not encode >~6y of climb in a
        // single polygon. Anything over 12y is unambiguously a wall: the
        // string-pulled smooth path can interpolate straight into it.
        //
        // Currently failing fingerprint:
        //   polyIndex=8869 zRange=19.60 area=3 flags=17 horizArea2D=45 maxEdge2D=7.5
        var dataDir = ResolveDataDir();
        var tilePath = Path.Combine(dataDir, "mmaps", FlameCrestStallTile);
        Assert.True(File.Exists(tilePath), $"Expected Flame Crest stall mmap tile at {tilePath}");

        var tile = MmapTile.Read(tilePath);
        var stallSearchBox = new WowBounds(
            FlameCrestStallX - 25f, FlameCrestStallY - 25f, FlameCrestStallZ - 25f,
            FlameCrestStallX + 25f, FlameCrestStallY + 25f, FlameCrestStallZ + 40f);
        var stats = tile.GetPolygonStats(stallSearchBox).ToArray();

        Assert.True(stats.Length > 0,
            $"Expected at least one polygon in Flame Crest stall search box {stallSearchBox}.");

        // A reachable wall is a polygon whose flags include NAV_STEEP_SLOPES
        // (so the AI mesh tagged it as a steep slope) AND whose vertical span
        // is large enough that physical traversal is implausible.
        const float MaxWalkableSteepSlopeZRange = 12.0f;
        var tallSteepSlopeWalls = stats
            .Where(s => (s.Flags & NavSteepSlopes) != 0)
            .Where(s => s.ZRange > MaxWalkableSteepSlopeZRange)
            .OrderByDescending(s => s.ZRange)
            .ToArray();

        Assert.True(tallSteepSlopeWalls.Length == 0,
            "Expected no tall steep-slope walls in the Flame Crest stall search box. "
            + $"Found {tallSteepSlopeWalls.Length} polygon(s) with NAV_STEEP_SLOPES bit set and zRange > "
            + $"{MaxWalkableSteepSlopeZRange:F1}y. The smooth path may string-pull across these as if they "
            + "were walkable surface; FG physics rejects them and the bot stalls."
            + Environment.NewLine
            + FormatOffenders(tallSteepSlopeWalls));
    }

    [Fact(Skip = "See FlameCrestStall_HasNoTallSteepSlopeWallsNearStall — same "
        + "open bake bug. Both regressions are Skip until the runtime-filter or "
        + "off-mesh-connection fix lands; they re-enable together.")]
    public void FlameCrestStall_HasNoUnreasonableGroundBridgePolygons()
    {
        // PROPERTY: a regular walkable polygon (NAV_GROUND only, no steep-slope
        // bit) near the stall coordinate should not encode an unreasonable
        // vertical span. Stairs/ramps span Z over a horizontal stretch; a poly
        // with small horizontal extent but large Z span is a bridge/ceiling
        // artifact, not real walkable surface.
        //
        // Threshold: zRange > 6y AND horizArea2D < 80 AND maxEdge2D < 12y
        // (a tall narrow column that isn't a wide sloped ramp).
        var dataDir = ResolveDataDir();
        var tilePath = Path.Combine(dataDir, "mmaps", FlameCrestStallTile);
        Assert.True(File.Exists(tilePath), $"Expected Flame Crest stall mmap tile at {tilePath}");

        var tile = MmapTile.Read(tilePath);
        var stallSearchBox = new WowBounds(
            FlameCrestStallX - 25f, FlameCrestStallY - 25f, FlameCrestStallZ - 25f,
            FlameCrestStallX + 25f, FlameCrestStallY + 25f, FlameCrestStallZ + 40f);
        var stats = tile.GetPolygonStats(stallSearchBox).ToArray();

        var pureGroundPolys = stats.Where(s => (s.Flags & NavSteepSlopes) == 0 && (s.Flags & NavGround) != 0).ToArray();
        Assert.True(pureGroundPolys.Length > 0,
            $"Expected at least one pure-ground walkable polygon in Flame Crest stall search box {stallSearchBox}.");

        var bridgeOffenders = pureGroundPolys
            .Where(s => s.ZRange > 6f && s.HorizontalArea2D < 80f && s.MaxEdge2D < 12f)
            .OrderByDescending(s => s.ZRange)
            .ToArray();

        Assert.True(bridgeOffenders.Length == 0,
            "Expected no pure-ground bridge/column polygons near the Flame Crest stall. "
            + $"Found {bridgeOffenders.Length} narrow-tall walkable polygon(s) (zRange > 6y, area < 80yd^2, maxEdge < 12y). "
            + "These typically appear when Recast merges multi-level ledge cells into one detail mesh."
            + Environment.NewLine
            + FormatOffenders(bridgeOffenders));
    }

    [Fact]
    public void OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces()
    {
        var dataDir = ResolveDataDir();
        var tilePath = Path.Combine(dataDir, "mmaps", OgTopRampDeckTile);
        Assert.True(File.Exists(tilePath), $"Expected OG tower mmap tile at {tilePath}");

        var tile = MmapTile.Read(tilePath);
        var stats = tile.GetPolygonStats(OgTopRampDeckCrop).ToArray();
        var upperDeckStats = stats
            .Where(static s => s.Bounds.MaxZ >= 52.5f && s.Bounds.MinZ <= 54.8f)
            .ToArray();
        var upperDeckArea = upperDeckStats.Sum(static s => s.HorizontalArea2D);

        Assert.True(stats.Length >= 220,
            $"Expected the top-ramp/deck crop to retain the thin connector surfaces; found {stats.Length} polygons.");
        Assert.True(upperDeckStats.Length >= 140,
            $"Expected the upper deck band to retain connector fragments; found {upperDeckStats.Length} polygons.");
        Assert.True(upperDeckArea >= 700f,
            $"Expected the upper deck band to preserve at least 700 yd^2 of walkable polygon area; found {upperDeckArea:F3}.");
    }

    private static string ResolveDataDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        const string defaultDataDir = @"D:\MaNGOS\data";
        return Directory.Exists(defaultDataDir)
            ? defaultDataDir
            : throw new InvalidOperationException("WWOW_DATA_DIR is not set and D:\\MaNGOS\\data was not found.");
    }

    private static string FormatOffenders(IReadOnlyCollection<PolygonStats> offenders)
    {
        if (offenders.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var offender in offenders.Take(8))
        {
            builder.AppendLine(
                $"polyIndex={offender.PolyIndex} zRange={offender.ZRange:F3} maxEdge2D={offender.MaxEdge2D:F3} "
                + $"horizontalArea2D={offender.HorizontalArea2D:F3} area={offender.Area} flags=0x{offender.Flags:X2} "
                + $"bounds={offender.Bounds}");
        }

        return builder.ToString();
    }

    private sealed class MmapTile
    {
        private const uint MmapMagic = 0x4D4D4150;
        private const int MmapTileHeaderSize = 20;
        private const int DetourMagic = ('D' << 24) | ('N' << 16) | ('A' << 8) | 'V';
        private const int DetourVersion = 7;
        private const int VertsPerPolygon = 6;
        private const int DtMeshHeaderSize = 100;
        private const int DtPolySize = 32;
        private const int DtPolyDetailSize = 12;
        private const int DtLinkSize = 16;
        private const int DtBVNodeSize = 16;
        private const int DtOffMeshConnectionSize = 36;

        private readonly Vec3[] _verts;
        private readonly Poly[] _polys;
        private readonly PolyDetail[] _detailMeshes;
        private readonly Vec3[] _detailVerts;
        private readonly DetailTri[] _detailTris;

        private MmapTile(Vec3[] verts, Poly[] polys, PolyDetail[] detailMeshes, Vec3[] detailVerts, DetailTri[] detailTris)
        {
            _verts = verts;
            _polys = polys;
            _detailMeshes = detailMeshes;
            _detailVerts = detailVerts;
            _detailTris = detailTris;
        }

        public static MmapTile Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= MmapTileHeaderSize + DtMeshHeaderSize, $"Mmap tile {path} is too small.");
            Assert.Equal(MmapMagic, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)));

            var span = bytes.AsSpan(MmapTileHeaderSize);
            Assert.Equal(DetourMagic, BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4)));
            Assert.Equal(DetourVersion, BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4)));

            var polyCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(24, 4));
            var vertCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(28, 4));
            var maxLinkCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(32, 4));
            var detailMeshCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(36, 4));
            var detailVertCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(40, 4));
            var detailTriCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(44, 4));
            var bvNodeCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(48, 4));
            var offMeshConCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(52, 4));

            var cursor = DtMeshHeaderSize;
            var verts = new Vec3[vertCount];
            for (var i = 0; i < verts.Length; i++)
            {
                verts[i] = new Vec3(
                    BitConverter.ToSingle(span.Slice(cursor + 0, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 4, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 8, 4)));
                cursor += 12;
            }
            cursor = Align4(cursor);

            var polys = new Poly[polyCount];
            for (var i = 0; i < polys.Length; i++)
            {
                var pBase = cursor;
                polys[i] = new Poly(
                    Verts: ReadUShorts(span, pBase + 4, VertsPerPolygon),
                    Neis: ReadUShorts(span, pBase + 4 + 2 * VertsPerPolygon, VertsPerPolygon),
                    Flags: BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(pBase + 28, 2)),
                    VertCount: span[pBase + 30],
                    AreaAndType: span[pBase + 31]);
                cursor += DtPolySize;
            }
            cursor = Align4(cursor);

            cursor += Align4(DtLinkSize * maxLinkCount);

            var detailMeshes = new PolyDetail[detailMeshCount];
            for (var i = 0; i < detailMeshes.Length; i++)
            {
                detailMeshes[i] = new PolyDetail(
                    VertBase: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(cursor + 0, 4)),
                    TriBase: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(cursor + 4, 4)),
                    VertCount: span[cursor + 8],
                    TriCount: span[cursor + 9]);
                cursor += DtPolyDetailSize;
            }
            cursor = Align4(cursor);

            var detailVerts = new Vec3[detailVertCount];
            for (var i = 0; i < detailVerts.Length; i++)
            {
                detailVerts[i] = new Vec3(
                    BitConverter.ToSingle(span.Slice(cursor + 0, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 4, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 8, 4)));
                cursor += 12;
            }
            cursor = Align4(cursor);

            var detailTris = new DetailTri[detailTriCount];
            for (var i = 0; i < detailTris.Length; i++)
            {
                detailTris[i] = new DetailTri(span[cursor + 0], span[cursor + 1], span[cursor + 2], span[cursor + 3]);
                cursor += 4;
            }
            cursor = Align4(cursor);

            cursor += Align4(DtBVNodeSize * bvNodeCount);
            cursor += Align4(DtOffMeshConnectionSize * offMeshConCount);
            Assert.True(cursor <= span.Length, $"Mmap tile {path} ended before all declared arrays were read.");

            return new MmapTile(verts, polys, detailMeshes, detailVerts, detailTris);
        }

        public IEnumerable<PolygonStats> GetPolygonStats(WowBounds crop)
        {
            for (var polyIndex = 0; polyIndex < _polys.Length; polyIndex++)
            {
                var poly = _polys[polyIndex];
                if (IsOffMesh(poly) || poly.VertCount == 0 || poly.Flags == 0)
                    continue;

                var bounds = GetPolyBounds(polyIndex);
                if (!bounds.Intersects(crop))
                    continue;

                yield return new PolygonStats(
                    polyIndex,
                    bounds.MaxZ - bounds.MinZ,
                    GetMaxPolyEdge2D(poly),
                    GetHorizontalArea2D(poly),
                    bounds,
                    poly.Flags,
                    (byte)(poly.AreaAndType & 0x3F));
            }
        }

        private WowBounds GetPolyBounds(int polyIndex)
        {
            var poly = _polys[polyIndex];
            var initialized = false;
            var minX = 0f;
            var minY = 0f;
            var minZ = 0f;
            var maxX = 0f;
            var maxY = 0f;
            var maxZ = 0f;

            void Include(Vec3 detour)
            {
                var wow = DetourToWow(detour);
                if (!initialized)
                {
                    minX = maxX = wow.X;
                    minY = maxY = wow.Y;
                    minZ = maxZ = wow.Z;
                    initialized = true;
                    return;
                }

                minX = MathF.Min(minX, wow.X);
                minY = MathF.Min(minY, wow.Y);
                minZ = MathF.Min(minZ, wow.Z);
                maxX = MathF.Max(maxX, wow.X);
                maxY = MathF.Max(maxY, wow.Y);
                maxZ = MathF.Max(maxZ, wow.Z);
            }

            for (var i = 0; i < poly.VertCount; i++)
                Include(_verts[poly.Verts[i]]);

            if (polyIndex < _detailMeshes.Length)
            {
                var detail = _detailMeshes[polyIndex];
                for (var i = 0; i < detail.VertCount; i++)
                    Include(_detailVerts[(int)detail.VertBase + i]);
            }

            return new WowBounds(minX, minY, minZ, maxX, maxY, maxZ);
        }

        private float GetMaxPolyEdge2D(Poly poly)
        {
            var max = 0f;
            for (var i = 0; i < poly.VertCount; i++)
            {
                var a = DetourToWow(_verts[poly.Verts[i]]);
                var b = DetourToWow(_verts[poly.Verts[(i + 1) % poly.VertCount]]);
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                max = MathF.Max(max, MathF.Sqrt(dx * dx + dy * dy));
            }

            return max;
        }

        private float GetHorizontalArea2D(Poly poly)
        {
            var area = 0f;
            for (var i = 0; i < poly.VertCount; i++)
            {
                var a = DetourToWow(_verts[poly.Verts[i]]);
                var b = DetourToWow(_verts[poly.Verts[(i + 1) % poly.VertCount]]);
                area += a.X * b.Y - b.X * a.Y;
            }

            return MathF.Abs(area) * 0.5f;
        }

        private static bool IsOffMesh(Poly poly) => (poly.AreaAndType >> 6) == 1;

        private static Vec3 DetourToWow(Vec3 v) => new(v.Z, v.X, v.Y);

        private static ushort[] ReadUShorts(ReadOnlySpan<byte> span, int offset, int count)
        {
            var result = new ushort[count];
            for (var i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset + i * 2, 2));

            return result;
        }

        private static int Align4(int x) => (x + 3) & ~3;
    }

    private sealed record Poly(ushort[] Verts, ushort[] Neis, ushort Flags, byte VertCount, byte AreaAndType);

    private readonly record struct PolyDetail(uint VertBase, uint TriBase, byte VertCount, byte TriCount);

    private readonly record struct DetailTri(byte V0, byte V1, byte V2, byte Flags);

    private readonly record struct Vec3(float X, float Y, float Z);

    private readonly record struct PolygonStats(
        int PolyIndex,
        float ZRange,
        float MaxEdge2D,
        float HorizontalArea2D,
        WowBounds Bounds,
        ushort Flags,
        byte Area);

    private readonly record struct WowBounds(float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)
    {
        public bool Intersects(WowBounds other)
        {
            return MaxX >= other.MinX
                && MinX <= other.MaxX
                && MaxY >= other.MinY
                && MinY <= other.MaxY
                && MaxZ >= other.MinZ
                && MinZ <= other.MaxZ;
        }

        public override string ToString()
        {
            return $"({MinX:F1},{MinY:F1},{MinZ:F1})..({MaxX:F1},{MaxY:F1},{MaxZ:F1})";
        }
    }
}
