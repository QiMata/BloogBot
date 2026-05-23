// MmapVisualize — parse a .mmtile file and emit a Wavefront OBJ of all walkable
// detail-mesh triangles, grouped by Z layer. Optional --mark adds octahedron
// markers at specific coords. Optional --include-vmaps grafts in the matching
// VMap (.vmtile) collision geometry as a separate OBJ object so you can see
// when the navmesh's polygons overlap unbaked wall geometry.
//
// Output coords follow WoW convention (X/Y horizontal, Z up). Standard OBJ
// viewers interpret Z-up well enough; pass --y-up to swap (Y becomes vertical).
//
// Format references:
//   - docs/physics/MMAP_FORMAT.md (WWoW header layout)
//   - Exports/Navigation/Detour/Include/DetourNavMesh.h (dtMeshHeader, dtPoly,
//     dtPolyDetail, dtBVNode, dtOffMeshConnection)
//   - Exports/Navigation/Detour/Source/DetourNavMeshBuilder.cpp (array order)
//
// Usage:
//   MmapVisualize.exe --mmtile <path> --out <obj-path>
//                     [--mark X,Y,Z label]*
//                     [--z-band <yards>]   (default 2)
//                     [--reachable-from X,Y,Z] [--reachable-mode all|only|unreachable]
//                     [--crop minX,minY,minZ,maxX,maxY,maxZ]
//                     [--polys i,j,k]
//                     [--poly-report <csv-path>]
//                     [--y-up]             (swap to Y-up world for OBJ viewer)
//                     [--quiet]
//
// Exit codes: 0 success, 1 bad args, 2 parse failure.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MmapVisualize;

internal static class Program
{
    private const uint MmapMagic = 0x4D4D4150;            // 'MMAP'
    private const int MmapTileHeaderSize = 20;
    private const int DetourMagic = ('D' << 24) | ('N' << 16) | ('A' << 8) | 'V';
    private const int DetourVersion = 7;
    private const int VertsPerPolygon = 6;
    private const int DtMeshHeaderSize = 100;             // dtAlign4(sizeof(dtMeshHeader)) per DetourNavMeshBuilder.cpp:401
    private const int DtPolySize = 32;
    private const int DtPolyDetailSize = 12;
    private const int DtLinkSize = 16;                    // dtPolyRef64=8 + uint+4 + 4*byte = 16
    private const int DtBVNodeSize = 16;                  // 6 ushorts + 1 int = 16
    private const int DtOffMeshConnectionSize = 36;       // 6 floats + 1 float + ushort + byte + byte + uint = 36
    private const float SteepSurfaceEpsilonDegrees = 1.0f;
    private const float SteepSurfaceMinTriangleNormal = 1e-5f;
    private const float MixedWallPolyMinSteepAreaRatio = 0.08f;
    private const float MixedWallPolyMinZRange = 1.0f;
    private const float MixedWallPolyMinEdgeLength2D = 6.0f;
    private const float MixedWallPolyMinArea2D = 8.0f;
    private const float MixedWallPolyClimbOvershoot = 0.15f;
    private const ushort NavGround = 0x01;
    private const float ShadowedLedgeMaxArea2D = 20.0f;
    private const float ShadowedLedgeMaxEdge2D = 7.0f;
    private const float ShadowedLedgeMaxZRange = 1.5f;
    private const float ShadowedLedgeMinVerticalGap = 1.0f;
    private const float ShadowedLedgeMaxVerticalGap = 4.0f;
    private const float ShadowedLedgeMinOverlapArea2D = 0.5f;
    private const float ShadowedLedgeMinOverlapRatio = 0.35f;

    private static int Main(string[] args)
    {
        try
        {
            var opts = Args.Parse(args);
            var bytes = File.ReadAllBytes(opts.MmtilePath);
            var tile = ParseTile(bytes);
            if (tile == null)
            {
                Console.Error.WriteLine($"error: {opts.MmtilePath} is not a valid mmtile (header check failed)");
                return 2;
            }

            if (!opts.Quiet)
            {
                Console.Error.WriteLine($"# tile=({tile.Header.X},{tile.Header.Y},{tile.Header.Layer}) " +
                    $"polys={tile.Header.PolyCount} verts={tile.Header.VertCount} " +
                    $"detailMeshes={tile.Header.DetailMeshCount} detailTris={tile.Header.DetailTriCount} " +
                    $"offMesh={tile.Header.OffMeshConCount}");
                Console.Error.WriteLine($"# bmin=({tile.Header.BMin.X:F2},{tile.Header.BMin.Y:F2},{tile.Header.BMin.Z:F2}) " +
                    $"bmax=({tile.Header.BMax.X:F2},{tile.Header.BMax.Y:F2},{tile.Header.BMax.Z:F2})");
            }

            using var writer = new StreamWriter(opts.OutPath, append: false, Encoding.UTF8);
            writer.NewLine = "\n";
            WriteObj(writer, tile, opts);
            if (!opts.Quiet)
                Console.Error.WriteLine($"# wrote {opts.OutPath}");
            return 0;
        }
        catch (ArgException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine(Args.UsageText);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"fatal: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
    }

    private static Tile? ParseTile(byte[] bytes)
    {
        if (bytes.Length < MmapTileHeaderSize + DtMeshHeaderSize)
            return null;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)) != MmapMagic)
            return null;

        var detourBase = MmapTileHeaderSize;
        var span = bytes.AsSpan(detourBase);

        if (BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4)) != DetourMagic)
            return null;
        if (BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4)) != DetourVersion)
            return null;

        var header = new MeshHeader(
            X: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4)),
            Y: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12, 4)),
            Layer: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(16, 4)),
            UserId: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(20, 4)),
            PolyCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(24, 4)),
            VertCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(28, 4)),
            MaxLinkCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(32, 4)),
            DetailMeshCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(36, 4)),
            DetailVertCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(40, 4)),
            DetailTriCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(44, 4)),
            BvNodeCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(48, 4)),
            OffMeshConCount: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(52, 4)),
            OffMeshBase: BinaryPrimitives.ReadInt32LittleEndian(span.Slice(56, 4)),
            WalkableHeight: BitConverter.ToSingle(span.Slice(60, 4)),
            WalkableRadius: BitConverter.ToSingle(span.Slice(64, 4)),
            WalkableClimb: BitConverter.ToSingle(span.Slice(68, 4)),
            BMin: new Vec3(
                BitConverter.ToSingle(span.Slice(72, 4)),
                BitConverter.ToSingle(span.Slice(76, 4)),
                BitConverter.ToSingle(span.Slice(80, 4))),
            BMax: new Vec3(
                BitConverter.ToSingle(span.Slice(84, 4)),
                BitConverter.ToSingle(span.Slice(88, 4)),
                BitConverter.ToSingle(span.Slice(92, 4))),
            BvQuantFactor: BitConverter.ToSingle(span.Slice(96, 4)));

        var cursor = DtMeshHeaderSize;
        var verts = new Vec3[header.VertCount];
        for (var i = 0; i < header.VertCount; i++)
        {
            verts[i] = new Vec3(
                BitConverter.ToSingle(span.Slice(cursor + 0, 4)),
                BitConverter.ToSingle(span.Slice(cursor + 4, 4)),
                BitConverter.ToSingle(span.Slice(cursor + 8, 4)));
            cursor += 12;
        }
        cursor = Align4(cursor);

        var polys = new Poly[header.PolyCount];
        for (var i = 0; i < header.PolyCount; i++)
        {
            var pBase = cursor;
            var p = new Poly(
                FirstLink: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(pBase + 0, 4)),
                Verts: ReadUShorts(span, pBase + 4, VertsPerPolygon),
                Neis: ReadUShorts(span, pBase + 4 + 2 * VertsPerPolygon, VertsPerPolygon),
                Flags: BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(pBase + 28, 2)),
                VertCount: span[pBase + 30],
                AreaAndType: span[pBase + 31]);
            polys[i] = p;
            cursor += DtPolySize;
        }
        cursor = Align4(cursor);

        // Skip the dtLink array (allocated but empty in serialized tile data)
        cursor += Align4(DtLinkSize * header.MaxLinkCount);

        var detailMeshes = new PolyDetail[header.DetailMeshCount];
        for (var i = 0; i < header.DetailMeshCount; i++)
        {
            detailMeshes[i] = new PolyDetail(
                VertBase: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(cursor + 0, 4)),
                TriBase: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(cursor + 4, 4)),
                VertCount: span[cursor + 8],
                TriCount: span[cursor + 9]);
            cursor += DtPolyDetailSize;
        }
        cursor = Align4(cursor);

        var detailVerts = new Vec3[header.DetailVertCount];
        for (var i = 0; i < header.DetailVertCount; i++)
        {
            detailVerts[i] = new Vec3(
                BitConverter.ToSingle(span.Slice(cursor + 0, 4)),
                BitConverter.ToSingle(span.Slice(cursor + 4, 4)),
                BitConverter.ToSingle(span.Slice(cursor + 8, 4)));
            cursor += 12;
        }
        cursor = Align4(cursor);

        var detailTris = new (byte v0, byte v1, byte v2, byte flags)[header.DetailTriCount];
        for (var i = 0; i < header.DetailTriCount; i++)
        {
            detailTris[i] = (
                span[cursor + 0],
                span[cursor + 1],
                span[cursor + 2],
                span[cursor + 3]);
            cursor += 4;
        }
        cursor = Align4(cursor);

        // Skip BV tree (we don't render it)
        cursor += Align4(DtBVNodeSize * header.BvNodeCount);

        var offMesh = new OffMeshConnection[header.OffMeshConCount];
        for (var i = 0; i < header.OffMeshConCount; i++)
        {
            offMesh[i] = new OffMeshConnection(
                Start: new Vec3(
                    BitConverter.ToSingle(span.Slice(cursor + 0, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 4, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 8, 4))),
                End: new Vec3(
                    BitConverter.ToSingle(span.Slice(cursor + 12, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 16, 4)),
                    BitConverter.ToSingle(span.Slice(cursor + 20, 4))),
                Radius: BitConverter.ToSingle(span.Slice(cursor + 24, 4)),
                Poly: BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(cursor + 28, 2)),
                Flags: span[cursor + 30],
                Side: span[cursor + 31],
                UserId: BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(cursor + 32, 4)));
            cursor += DtOffMeshConnectionSize;
        }

        return new Tile(header, verts, polys, detailMeshes, detailVerts, detailTris, offMesh);
    }

    private static ushort[] ReadUShorts(ReadOnlySpan<byte> span, int offset, int count)
    {
        var result = new ushort[count];
        for (var i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset + i * 2, 2));
        return result;
    }

    private static int Align4(int x) => (x + 3) & ~3;

    private static bool IsRenderableNavPoly(Poly poly) => !IsOffMesh(poly) && poly.VertCount > 0 && poly.Flags != 0;

    private static void WriteObj(StreamWriter w, Tile tile, Args opts)
    {
        // vmangos stores MaNGOS/Recast verts as (WoW_Y, WoW_Z_up, WoW_X).
        // This looks backwards if you start from normal WoW XYZ, but it matches
        // the extractor and the in-game debug command:
        //   tileX = 32 - worldY / GRID_SIZE
        //   tileY = 32 - worldX / GRID_SIZE
        // So Detour vx is WoW Y, Detour vy is vertical WoW Z, and Detour vz is
        // WoW X.
        //
        // Default OBJ output: Y-up right-handed (OBJ X = WoW X, OBJ Y = WoW Z,
        // OBJ Z = -WoW Y).
        // Most 3D viewers (Windows 3D Viewer, Blender default, glTF) interpret
        // OBJ as Y-up — without this remap the ground appears edge-on and you
        // have to rotate the model 90° in the viewer. We negate WoW Y so the
        // result preserves right-handedness:
        //   (Xo, Yo, Zo) = (vz, vy, -vx) = (WoW_X, WoW_Z, -WoW_Y)
        // Camera looking down -Z sees WoW X on the horizontal screen axis and
        // vertical height on screen Y.
        //
        // --z-up emits WoW-native Z-up: (WoW_X, WoW_Y, WoW_Z) = (vz, vx, vy).
        // Use this only with viewers that respect the OBJ file's coord system
        // (Blender with Z-up import, MeshLab with manual axis settings).
        Vec3 ToWorld(Vec3 v) => opts.ZUp
            ? DetourToWow(v)                                // Z-up WoW: X/Y horizontal, Z vertical
            : new Vec3(v.Z, v.Y, -v.X);                      // Y-up OBJ: X=WoW X, Y=WoW Z, Z=-WoW Y

        w.WriteLine("# MmapVisualize OBJ — generated from mmtile");
        w.WriteLine($"# tile=({tile.Header.X},{tile.Header.Y},{tile.Header.Layer}) " +
                    $"polys={tile.Header.PolyCount} detailTris={tile.Header.DetailTriCount} " +
                    $"offMesh={tile.Header.OffMeshConCount}");
        w.WriteLine($"# bmin=({tile.Header.BMin.X:F2},{tile.Header.BMin.Y:F2},{tile.Header.BMin.Z:F2}) " +
                    $"bmax=({tile.Header.BMax.X:F2},{tile.Header.BMax.Y:F2},{tile.Header.BMax.Z:F2})");
        w.WriteLine($"# coord-system: {(opts.ZUp ? "Z-up WoW (X/Y horizontal, Z vertical) — pass to Blender as Z-up" : "Y-up OBJ (X=WoW X, Y=WoW Z, Z=-WoW Y) — default OBJ viewer convention")}");
        w.WriteLine();

        if (opts.CropWow is { } crop)
        {
            w.WriteLine($"# crop-wow=({crop.MinX:F2},{crop.MinY:F2},{crop.MinZ:F2})..({crop.MaxX:F2},{crop.MaxY:F2},{crop.MaxZ:F2})");
            w.WriteLine();
        }

        var reachabilityFilter = BuildReachabilityFilter(tile, opts);

        // Group polygons by Z band for selective layer hide/show in the viewer.
        // The WoW vertical axis corresponds to Detour vy, NOT vz — Detour stores
        // (WoW_Y, WoW_Z, WoW_X). Polygon Z banding therefore uses .Y of the raw
        // Detour vert.
        var bandPolys = new Dictionary<int, List<int>>();
        for (var pIdx = 0; pIdx < tile.Polys.Length; pIdx++)
        {
            var poly = tile.Polys[pIdx];
            // Skip off-mesh-connection polygons (rendered separately).
            if (!IsRenderableNavPoly(poly))
                continue;
            if (reachabilityFilter != null && !reachabilityFilter.Contains(pIdx))
                continue;
            if (opts.PolyFilter.Count > 0 && !opts.PolyFilter.Contains(pIdx))
                continue;
            if (opts.CropWow is { } cropFilter && !PolyIntersectsCrop(tile, pIdx, cropFilter))
                continue;
            // Use the polygon's first vertex's WoW-vertical for band assignment
            // (close enough — we don't care about sub-yard precision in banding).
            var firstVert = tile.Verts[poly.Verts[0]];
            var bandKey = (int)Math.Floor(firstVert.Y / opts.ZBand);
            if (!bandPolys.TryGetValue(bandKey, out var list))
            {
                list = new List<int>();
                bandPolys[bandKey] = list;
            }
            list.Add(pIdx);
        }

        var globalVertOffset = 1; // OBJ indices are 1-based

        // ---- Object: navmesh ----
        w.WriteLine("o navmesh");

        foreach (var bandKey in bandPolys.Keys.OrderBy(k => k))
        {
            var bandLow = bandKey * opts.ZBand;
            var bandHigh = bandLow + opts.ZBand;
            w.WriteLine($"g navmesh_z_{bandLow:F0}_to_{bandHigh:F0}");

            // Collect all detail triangles for polygons in this band, with their
            // resolved world-space vertices. Uses the detail mesh (most accurate
            // ground triangulation) rather than the simplified polygon outline.
            var localFaces = new List<(int a, int b, int c)>();
            var localVerts = new List<Vec3>();

            foreach (var pIdx in bandPolys[bandKey])
            {
                var poly = tile.Polys[pIdx];
                var dmesh = tile.DetailMeshes[pIdx];

                // Build the resolved-vertex array for this polygon: first the
                // polygon's main vertices, then its appended detail vertices.
                // dtPolyDetail.vertCount is the count of APPENDED detail verts;
                // the base polygon verts come from poly.verts[].
                var polyVerts = new Vec3[poly.VertCount + dmesh.VertCount];
                for (var i = 0; i < poly.VertCount; i++)
                    polyVerts[i] = tile.Verts[poly.Verts[i]];
                for (var i = 0; i < dmesh.VertCount; i++)
                    polyVerts[poly.VertCount + i] = tile.DetailVerts[(int)dmesh.VertBase + i];

                for (var t = 0; t < dmesh.TriCount; t++)
                {
                    var tri = tile.DetailTris[(int)dmesh.TriBase + t];
                    if (tri.v0 >= polyVerts.Length || tri.v1 >= polyVerts.Length || tri.v2 >= polyVerts.Length)
                        continue;
                    var aWorld = ToWorld(polyVerts[tri.v0]);
                    var bWorld = ToWorld(polyVerts[tri.v1]);
                    var cWorld = ToWorld(polyVerts[tri.v2]);
                    var aIdx = AddVert(localVerts, aWorld);
                    var bIdx = AddVert(localVerts, bWorld);
                    var cIdx = AddVert(localVerts, cWorld);
                    localFaces.Add((aIdx, bIdx, cIdx));
                }
            }

            foreach (var v in localVerts)
                w.WriteLine($"v {v.X.ToString("F4", CultureInfo.InvariantCulture)} " +
                            $"{v.Y.ToString("F4", CultureInfo.InvariantCulture)} " +
                            $"{v.Z.ToString("F4", CultureInfo.InvariantCulture)}");
            foreach (var f in localFaces)
                w.WriteLine($"f {globalVertOffset + f.a} {globalVertOffset + f.b} {globalVertOffset + f.c}");
            globalVertOffset += localVerts.Count;
            w.WriteLine();
        }

        // ---- Object: offmesh connections (rendered as line segments) ----
        if (tile.OffMesh.Length > 0)
        {
            w.WriteLine("o offmesh_connections");
            for (var i = 0; i < tile.OffMesh.Length; i++)
            {
                var oc = tile.OffMesh[i];
                var s = ToWorld(oc.Start);
                var e = ToWorld(oc.End);
                var bidir = (oc.Flags & 0x1) != 0;
                w.WriteLine($"g offmesh_{i}{(bidir ? "_bidir" : "_oneway")}_uid{oc.UserId}");
                w.WriteLine($"v {s.X.ToString("F4", CultureInfo.InvariantCulture)} " +
                            $"{s.Y.ToString("F4", CultureInfo.InvariantCulture)} " +
                            $"{s.Z.ToString("F4", CultureInfo.InvariantCulture)}");
                w.WriteLine($"v {e.X.ToString("F4", CultureInfo.InvariantCulture)} " +
                            $"{e.Y.ToString("F4", CultureInfo.InvariantCulture)} " +
                            $"{e.Z.ToString("F4", CultureInfo.InvariantCulture)}");
                w.WriteLine($"l {globalVertOffset} {globalVertOffset + 1}");
                globalVertOffset += 2;
            }
            w.WriteLine();
        }

        // ---- Object: vmap WMO instance bounds ----
        if (opts.VmapsDir != null)
        {
            // Derive vmtile filename from the input mmtile name. mmtile is
            // <map:000><tileY:00><tileX:00>.mmtile; vmtile is
            // <map:000>_<tileX:00>_<tileY:00>.vmtile.
            var mmtileName = Path.GetFileNameWithoutExtension(opts.MmtilePath);
            string? vmtilePath = null;
            if (mmtileName.Length == 7
                && int.TryParse(mmtileName.Substring(0, 3), out var mapId)
                && int.TryParse(mmtileName.Substring(3, 2), out var tileY)
                && int.TryParse(mmtileName.Substring(5, 2), out var tileX))
            {
                vmtilePath = Path.Combine(opts.VmapsDir, $"{mapId:D3}_{tileX:D2}_{tileY:D2}.vmtile");
            }
            if (vmtilePath == null || !File.Exists(vmtilePath))
            {
                Console.Error.WriteLine($"# WARN: --include-vmaps could not derive matching vmtile from {opts.MmtilePath} (looked for {vmtilePath ?? "?"})");
            }
            else
            {
                if (!opts.Quiet)
                    Console.Error.WriteLine($"# loading vmtile: {vmtilePath}");
                var spawns = ParseVmtile(vmtilePath);
                w.WriteLine("o vmap_instance_bounds");
                if (!opts.Quiet)
                {
                    Console.Error.WriteLine($"# vmap spawns: {spawns.Count} (with bounds: {spawns.Count(s => s.HasBound)})");
                    // Dump first 3 spawns' raw coords for coord-system debugging
                    foreach (var s in spawns.Take(3))
                        Console.Error.WriteLine($"#   spawn {s.Name} flags=0x{s.Flags:X} pos=({s.Pos.X:F1},{s.Pos.Y:F1},{s.Pos.Z:F1}) bound=[({s.BoundLow.X:F1},{s.BoundLow.Y:F1},{s.BoundLow.Z:F1}) .. ({s.BoundHigh.X:F1},{s.BoundHigh.Y:F1},{s.BoundHigh.Z:F1})]");
                }
                int sIdx = 0;
                // vmtile coord system → WoW world coord conversion. The raw
                // instance/bound coordinates are shifted by 32 ADT tiles and
                // negated by TerrainBuilder before being copied into Detour as
                // (WoW_Y, WoW_Z, WoW_X). Inverting that transform gives:
                //   WoW_X = 32*GRID_SIZE - raw_x
                //   WoW_Y = 32*GRID_SIZE - raw_y
                //   WoW_Z = raw_z
                const float GridSize = 533.33333f;
                const float HalfWorld = 32f * GridSize;
                Vec3 VmapToWow(Vec3 raw) =>
                    new Vec3(HalfWorld - raw.X, HalfWorld - raw.Y, raw.Z);
                foreach (var s in spawns)
                {
                    if (!s.HasBound)
                        continue;
                    var safeName = SanitizeGroupName(s.Name);
                    w.WriteLine($"g vmap_{sIdx:D3}_{safeName}");
                    // Convert raw vmtile bound coords to WoW (X, Y, Z), then
                    // re-pack into Detour storage form (WoW_Y, WoW_Z, WoW_X)
                    // so ToWorld() can produce the requested OBJ output frame.
                    var wL = VmapToWow(s.BoundLow);
                    var wH = VmapToWow(s.BoundHigh);
                    // Normalize so wL is min, wH is max per axis (the negation
                    // can flip min/max for X and Y).
                    var minX = MathF.Min(wL.X, wH.X); var maxX = MathF.Max(wL.X, wH.X);
                    var minY = MathF.Min(wL.Y, wH.Y); var maxY = MathF.Max(wL.Y, wH.Y);
                    var minZ = MathF.Min(wL.Z, wH.Z); var maxZ = MathF.Max(wL.Z, wH.Z);
                    var corners = new Vec3[8]
                    {
                        WowToDetour(new Vec3(minX, minY, minZ)), WowToDetour(new Vec3(maxX, minY, minZ)),
                        WowToDetour(new Vec3(maxX, maxY, minZ)), WowToDetour(new Vec3(minX, maxY, minZ)),
                        WowToDetour(new Vec3(minX, minY, maxZ)), WowToDetour(new Vec3(maxX, minY, maxZ)),
                        WowToDetour(new Vec3(maxX, maxY, maxZ)), WowToDetour(new Vec3(minX, maxY, maxZ)),
                    };
                    var worldCorners = corners.Select(ToWorld).ToArray();
                    foreach (var v in worldCorners)
                        w.WriteLine($"v {v.X.ToString("F4", CultureInfo.InvariantCulture)} " +
                                    $"{v.Y.ToString("F4", CultureInfo.InvariantCulture)} " +
                                    $"{v.Z.ToString("F4", CultureInfo.InvariantCulture)}");
                    int b = globalVertOffset;
                    // 12 edges — bottom rect (0-1-2-3-0), top rect (4-5-6-7-4), 4 verticals
                    var edges = new (int, int)[]
                    {
                        (0, 1), (1, 2), (2, 3), (3, 0),
                        (4, 5), (5, 6), (6, 7), (7, 4),
                        (0, 4), (1, 5), (2, 6), (3, 7),
                    };
                    foreach (var (a, c) in edges)
                        w.WriteLine($"l {b + a} {b + c}");
                    globalVertOffset += 8;
                    sIdx++;
                }
                w.WriteLine();
            }
        }

        // ---- Object: markers ----
        if (opts.Markers.Count > 0)
        {
            w.WriteLine("o markers");
            for (var i = 0; i < opts.Markers.Count; i++)
            {
                var m = opts.Markers[i];
                w.WriteLine($"g marker_{i:D2}_{m.Label}");
                // Octahedron with radius 0.5y around the mark coord.
                // WoW coord -> Detour coord -> ToWorld
                var center = WowToDetour(new Vec3(m.X, m.Y, m.Z));
                var c = ToWorld(center);
                const float r = 0.5f;
                var verts = new[]
                {
                    new Vec3(c.X + r, c.Y, c.Z),
                    new Vec3(c.X - r, c.Y, c.Z),
                    new Vec3(c.X, c.Y + r, c.Z),
                    new Vec3(c.X, c.Y - r, c.Z),
                    new Vec3(c.X, c.Y, c.Z + r),
                    new Vec3(c.X, c.Y, c.Z - r),
                };
                foreach (var v in verts)
                    w.WriteLine($"v {v.X.ToString("F4", CultureInfo.InvariantCulture)} " +
                                $"{v.Y.ToString("F4", CultureInfo.InvariantCulture)} " +
                                $"{v.Z.ToString("F4", CultureInfo.InvariantCulture)}");
                int b = globalVertOffset;
                // 8 triangle faces of the octahedron
                int xp = b + 0, xn = b + 1, yp = b + 2, yn = b + 3, zp = b + 4, zn = b + 5;
                w.WriteLine($"f {xp} {yp} {zp}");
                w.WriteLine($"f {yp} {xn} {zp}");
                w.WriteLine($"f {xn} {yn} {zp}");
                w.WriteLine($"f {yn} {xp} {zp}");
                w.WriteLine($"f {yp} {xp} {zn}");
                w.WriteLine($"f {xn} {yp} {zn}");
                w.WriteLine($"f {yn} {xn} {zn}");
                w.WriteLine($"f {xp} {yn} {zn}");
                globalVertOffset += 6;
            }
            w.WriteLine();
        }

        var includedPolys = bandPolys.Values.SelectMany(v => v).OrderBy(v => v).ToArray();
        if (opts.PolyReportPath != null)
        {
            WritePolyReport(tile, includedPolys, opts.PolyReportPath);
            if (!opts.Quiet)
                Console.Error.WriteLine($"# wrote polygon report: {opts.PolyReportPath} ({includedPolys.Length} polys)");
        }

        if (!opts.Quiet)
        {
            Console.Error.WriteLine($"# OBJ written: {globalVertOffset - 1} verts total, {includedPolys.Length} polys, {bandPolys.Count} Z bands, {tile.OffMesh.Length} off-mesh, {opts.Markers.Count} markers");
        }
    }

    private static HashSet<int>? BuildReachabilityFilter(Tile tile, Args opts)
    {
        if (opts.ReachableFrom == null || opts.ReachableMode == ReachableMode.All)
            return null;

        var start = new Vec3(
            opts.ReachableFrom.Y,
            opts.ReachableFrom.Z,
            opts.ReachableFrom.X);

        var startPoly = FindClosestGroundPoly(tile, start);
        if (startPoly < 0)
        {
            Console.Error.WriteLine("# WARN: --reachable-from could not find a nearest ground poly; reachability filter disabled");
            return null;
        }

        var reachable = new HashSet<int>();
        var queue = new Queue<int>();
        reachable.Add(startPoly);
        queue.Enqueue(startPoly);

        while (queue.Count > 0)
        {
            var pIdx = queue.Dequeue();
            var poly = tile.Polys[pIdx];
            foreach (var nei in poly.Neis)
            {
                if (nei == 0)
                    continue;
                if ((nei & 0x8000) != 0)
                    continue; // external tile side; this single-tile visualizer cannot resolve it.

                var nIdx = nei - 1;
                if (nIdx < 0 || nIdx >= tile.Polys.Length || reachable.Contains(nIdx))
                    continue;

                reachable.Add(nIdx);
                queue.Enqueue(nIdx);
            }
        }

        var groundPolys = Enumerable.Range(0, tile.Polys.Length)
            .Where(i => IsRenderableNavPoly(tile.Polys[i]))
            .ToHashSet();

        var output = opts.ReachableMode == ReachableMode.Only
            ? groundPolys.Where(reachable.Contains).ToHashSet()
            : groundPolys.Where(i => !reachable.Contains(i)).ToHashSet();

        Console.Error.WriteLine(
            $"# reachability: startPoly={startPoly} reachableGround={groundPolys.Count(reachable.Contains)} " +
            $"groundTotal={groundPolys.Count} output={output.Count} mode={opts.ReachableMode}");
        return output;
    }

    private static int FindClosestGroundPoly(Tile tile, Vec3 start)
    {
        var best = -1;
        var bestDist = double.MaxValue;
        for (var i = 0; i < tile.Polys.Length; i++)
        {
            var poly = tile.Polys[i];
            if (!IsRenderableNavPoly(poly))
                continue;

            var centroid = GetPolyCentroid(tile, i);
            var dx = centroid.X - start.X;
            var dy = centroid.Y - start.Y;
            var dz = centroid.Z - start.Z;
            var dist = dx * dx + dz * dz + (dy * dy * 4.0f);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private static Vec3 GetPolyCentroid(Tile tile, int polyIndex)
    {
        var poly = tile.Polys[polyIndex];
        var x = 0f;
        var y = 0f;
        var z = 0f;
        for (var i = 0; i < poly.VertCount; i++)
        {
            var v = tile.Verts[poly.Verts[i]];
            x += v.X;
            y += v.Y;
            z += v.Z;
        }

        var inv = 1f / poly.VertCount;
        return new Vec3(x * inv, y * inv, z * inv);
    }

    private static Vec3 DetourToWow(Vec3 v) => new(v.Z, v.X, v.Y);

    private static Vec3 WowToDetour(Vec3 v) => new(v.Y, v.Z, v.X);

    private static bool PolyIntersectsCrop(Tile tile, int polyIndex, WowBounds crop)
    {
        var bounds = GetPolyWowBounds(tile, polyIndex);
        return bounds.MaxX >= crop.MinX
            && bounds.MinX <= crop.MaxX
            && bounds.MaxY >= crop.MinY
            && bounds.MinY <= crop.MaxY
            && bounds.MaxZ >= crop.MinZ
            && bounds.MinZ <= crop.MaxZ;
    }

    private static WowBounds GetPolyWowBounds(Tile tile, int polyIndex)
    {
        var poly = tile.Polys[polyIndex];
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var maxZ = float.NegativeInfinity;

        void Include(Vec3 detour)
        {
            var wow = DetourToWow(detour);
            minX = MathF.Min(minX, wow.X);
            minY = MathF.Min(minY, wow.Y);
            minZ = MathF.Min(minZ, wow.Z);
            maxX = MathF.Max(maxX, wow.X);
            maxY = MathF.Max(maxY, wow.Y);
            maxZ = MathF.Max(maxZ, wow.Z);
        }

        for (var i = 0; i < poly.VertCount; i++)
            Include(tile.Verts[poly.Verts[i]]);

        if (polyIndex < tile.DetailMeshes.Length)
        {
            var detail = tile.DetailMeshes[polyIndex];
            for (var i = 0; i < detail.VertCount; i++)
                Include(tile.DetailVerts[(int)detail.VertBase + i]);
        }

        if (float.IsPositiveInfinity(minX))
            return new WowBounds(0, 0, 0, 0, 0, 0);

        return new WowBounds(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static float GetMaxPolyEdge2D(Tile tile, Poly poly)
    {
        var max = 0f;
        for (var i = 0; i < poly.VertCount; i++)
        {
            var a = DetourToWow(tile.Verts[poly.Verts[i]]);
            var b = DetourToWow(tile.Verts[poly.Verts[(i + 1) % poly.VertCount]]);
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            max = MathF.Max(max, MathF.Sqrt(dx * dx + dy * dy));
        }

        return max;
    }

    private static float GetHorizontalArea2D(Tile tile, Poly poly)
    {
        var twiceArea = 0f;
        for (var i = 0; i < poly.VertCount; i++)
        {
            var a = DetourToWow(tile.Verts[poly.Verts[i]]);
            var b = DetourToWow(tile.Verts[poly.Verts[(i + 1) % poly.VertCount]]);
            twiceArea += (a.X * b.Y) - (b.X * a.Y);
        }

        return MathF.Abs(twiceArea) * 0.5f;
    }

    private static Vec3 GetDetailTriVertex(Tile tile, Poly poly, PolyDetail detail, byte triVertIndex)
    {
        if (triVertIndex < poly.VertCount)
            return tile.Verts[poly.Verts[triVertIndex]];

        return tile.DetailVerts[(int)detail.VertBase + triVertIndex - poly.VertCount];
    }

    private static DetailPolyMetrics GetDetailPolyMetrics(Tile tile, int polyIndex, float walkableSlopeDegrees)
    {
        var poly = tile.Polys[polyIndex];
        var detail = polyIndex < tile.DetailMeshes.Length ? tile.DetailMeshes[polyIndex] : new PolyDetail(0, 0, 0, 0);
        var totalSurfaceArea = 0f;
        var steepSurfaceArea = 0f;
        var weightedUpComponent = 0f;
        var maxSlopeDegrees = 0f;

        for (var triIndex = 0; triIndex < detail.TriCount; triIndex++)
        {
            var tri = tile.DetailTris[(int)detail.TriBase + triIndex];
            var a = GetDetailTriVertex(tile, poly, detail, tri.v0);
            var b = GetDetailTriVertex(tile, poly, detail, tri.v1);
            var c = GetDetailTriVertex(tile, poly, detail, tri.v2);

            var edgeAb = new Vec3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            var edgeAc = new Vec3(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
            var normal = new Vec3(
                edgeAb.Y * edgeAc.Z - edgeAb.Z * edgeAc.Y,
                edgeAb.Z * edgeAc.X - edgeAb.X * edgeAc.Z,
                edgeAb.X * edgeAc.Y - edgeAb.Y * edgeAc.X);

            var normalLength = MathF.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
            if (normalLength <= SteepSurfaceMinTriangleNormal)
                continue;

            var area = normalLength * 0.5f;
            var upComponent = Math.Clamp(MathF.Abs(normal.Y) / normalLength, 0f, 1f);
            var slopeDegrees = MathF.Acos(upComponent) * (180f / MathF.PI);

            totalSurfaceArea += area;
            weightedUpComponent += upComponent * area;
            maxSlopeDegrees = MathF.Max(maxSlopeDegrees, slopeDegrees);
            if (slopeDegrees > walkableSlopeDegrees + SteepSurfaceEpsilonDegrees)
                steepSurfaceArea += area;
        }

        if (totalSurfaceArea <= 0f)
            return new DetailPolyMetrics(0f, 0f, 0f, 0f);

        var avgUpComponent = Math.Clamp(weightedUpComponent / totalSurfaceArea, 0f, 1f);
        var avgSlopeDegrees = MathF.Acos(avgUpComponent) * (180f / MathF.PI);
        return new DetailPolyMetrics(totalSurfaceArea, steepSurfaceArea, avgSlopeDegrees, maxSlopeDegrees);
    }

    private static bool IsSuspiciousMixedWallPoly(Tile tile, int polyIndex, WowBounds bounds, float maxEdge2D, float horizontalArea2D)
    {
        var metrics = GetDetailPolyMetrics(tile, polyIndex, tile.Header.WalkableClimb > 0f ? 50f : 50f);
        if (metrics.TotalSurfaceArea <= 0f)
            return false;

        var steepAreaRatio = metrics.SteepSurfaceArea / metrics.TotalSurfaceArea;
        var zRange = bounds.MaxZ - bounds.MinZ;
        var minRequiredZRange = MathF.Max(MixedWallPolyMinZRange, tile.Header.WalkableClimb + MixedWallPolyClimbOvershoot);

        return steepAreaRatio >= MixedWallPolyMinSteepAreaRatio &&
               metrics.MaxSlopeDegrees > 50f + SteepSurfaceEpsilonDegrees &&
               zRange >= minRequiredZRange &&
               (maxEdge2D >= MixedWallPolyMinEdgeLength2D || horizontalArea2D >= MixedWallPolyMinArea2D);
    }

    private static bool IsWalkableLandPoly(PolyReportRow row)
    {
        return row.PolyType == 0 &&
               row.Flags != 0 &&
               (row.Flags & NavGround) != 0 &&
               row.Area != 0;
    }

    private static bool IsShadowedLedgeCandidate(PolyReportRow row)
    {
        var zRange = row.Bounds.MaxZ - row.Bounds.MinZ;
        return row.HorizontalArea2D <= ShadowedLedgeMaxArea2D &&
               row.MaxEdge2D <= ShadowedLedgeMaxEdge2D &&
               zRange <= ShadowedLedgeMaxZRange;
    }

    private static float GetBoundsOverlapArea2D(in WowBounds a, in WowBounds b)
    {
        var overlapX = MathF.Min(a.MaxX, b.MaxX) - MathF.Max(a.MinX, b.MinX);
        if (overlapX <= 0f)
            return 0f;

        var overlapY = MathF.Min(a.MaxY, b.MaxY) - MathF.Max(a.MinY, b.MinY);
        if (overlapY <= 0f)
            return 0f;

        return overlapX * overlapY;
    }

    private static bool HasShadowingUpperGroundPoly(IReadOnlyList<PolyReportRow> rows, PolyReportRow candidate)
    {
        var minOverlapArea = MathF.Max(
            ShadowedLedgeMinOverlapArea2D,
            MathF.Min(candidate.HorizontalArea2D * ShadowedLedgeMinOverlapRatio, 3.0f));

        foreach (var other in rows)
        {
            if (other.PolyIndex == candidate.PolyIndex || !IsWalkableLandPoly(other))
                continue;

            var verticalGap = other.Bounds.MinZ - candidate.Bounds.MaxZ;
            if (verticalGap < ShadowedLedgeMinVerticalGap || verticalGap > ShadowedLedgeMaxVerticalGap)
                continue;

            if (GetBoundsOverlapArea2D(candidate.Bounds, other.Bounds) < minOverlapArea)
                continue;

            return true;
        }

        return false;
    }

    private static int CountInternalNeighbors(Poly poly)
    {
        var count = 0;
        foreach (var nei in poly.Neis)
        {
            if (nei != 0 && (nei & 0x8000) == 0)
                count++;
        }

        return count;
    }

    private static void WritePolyReport(Tile tile, IReadOnlyList<int> polyIndices, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var rows = new List<PolyReportRow>(polyIndices.Count);
        foreach (var polyIndex in polyIndices)
        {
            var poly = tile.Polys[polyIndex];
            var detailTriCount = polyIndex < tile.DetailMeshes.Length ? tile.DetailMeshes[polyIndex].TriCount : 0;
            var centroid = DetourToWow(GetPolyCentroid(tile, polyIndex));
            var bounds = GetPolyWowBounds(tile, polyIndex);
            var polyType = poly.AreaAndType >> 6;
            var area = poly.AreaAndType & 0x3f;
            var zRange = bounds.MaxZ - bounds.MinZ;
            var maxEdge2D = GetMaxPolyEdge2D(tile, poly);
            var horizontalArea = GetHorizontalArea2D(tile, poly);
            var neighbors = CountInternalNeighbors(poly);
            var detailMetrics = GetDetailPolyMetrics(tile, polyIndex, 50f);
            var steepDetailAreaRatio = detailMetrics.TotalSurfaceArea <= 0f ? 0f : detailMetrics.SteepSurfaceArea / detailMetrics.TotalSurfaceArea;
            var suspiciousMixedWall = IsSuspiciousMixedWallPoly(tile, polyIndex, bounds, maxEdge2D, horizontalArea);

            rows.Add(new PolyReportRow(
                polyIndex,
                polyType,
                area,
                poly.Flags,
                poly.VertCount,
                detailTriCount,
                centroid,
                bounds,
                zRange,
                maxEdge2D,
                horizontalArea,
                neighbors,
                detailMetrics.AvgSlopeDegrees,
                detailMetrics.MaxSlopeDegrees,
                steepDetailAreaRatio,
                suspiciousMixedWall));
        }

        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.NewLine = "\n";
        writer.WriteLine("polyIndex,polyType,area,flags,vertCount,detailTriCount,centroidX,centroidY,centroidZ,minX,maxX,minY,maxY,minZ,maxZ,zRange,maxEdge2D,horizontalArea2D,internalNeighbors,avgDetailSlope,maxDetailSlope,steepDetailAreaRatio,suspiciousMixedWall,shadowedLedgeCandidate,shadowedByUpperGround");

        foreach (var row in rows)
        {
            var shadowedLedgeCandidate = IsWalkableLandPoly(row) && IsShadowedLedgeCandidate(row);
            var shadowedByUpperGround = shadowedLedgeCandidate && HasShadowingUpperGroundPoly(rows, row);

            writer.WriteLine(string.Join(',',
                row.PolyIndex.ToString(CultureInfo.InvariantCulture),
                row.PolyType.ToString(CultureInfo.InvariantCulture),
                row.Area.ToString(CultureInfo.InvariantCulture),
                row.Flags.ToString(CultureInfo.InvariantCulture),
                row.VertCount.ToString(CultureInfo.InvariantCulture),
                row.DetailTriCount.ToString(CultureInfo.InvariantCulture),
                row.Centroid.X.ToString("F4", CultureInfo.InvariantCulture),
                row.Centroid.Y.ToString("F4", CultureInfo.InvariantCulture),
                row.Centroid.Z.ToString("F4", CultureInfo.InvariantCulture),
                row.Bounds.MinX.ToString("F4", CultureInfo.InvariantCulture),
                row.Bounds.MaxX.ToString("F4", CultureInfo.InvariantCulture),
                row.Bounds.MinY.ToString("F4", CultureInfo.InvariantCulture),
                row.Bounds.MaxY.ToString("F4", CultureInfo.InvariantCulture),
                row.Bounds.MinZ.ToString("F4", CultureInfo.InvariantCulture),
                row.Bounds.MaxZ.ToString("F4", CultureInfo.InvariantCulture),
                row.ZRange.ToString("F4", CultureInfo.InvariantCulture),
                row.MaxEdge2D.ToString("F4", CultureInfo.InvariantCulture),
                row.HorizontalArea2D.ToString("F4", CultureInfo.InvariantCulture),
                row.InternalNeighbors.ToString(CultureInfo.InvariantCulture),
                row.AvgDetailSlopeDegrees.ToString("F4", CultureInfo.InvariantCulture),
                row.MaxDetailSlopeDegrees.ToString("F4", CultureInfo.InvariantCulture),
                row.SteepDetailAreaRatio.ToString("F4", CultureInfo.InvariantCulture),
                row.SuspiciousMixedWall ? "1" : "0",
                shadowedLedgeCandidate ? "1" : "0",
                shadowedByUpperGround ? "1" : "0"));
        }
    }

    private static bool IsOffMesh(Poly poly) => (poly.AreaAndType >> 6) == 1;

    // Parse vmangos .vmtile: 8-byte "VMAP_7.0" magic + uint32 numSpawns +
    // sequence of (ModelSpawn + uint32 referencedVal) records. ModelSpawn
    // binary layout from tools/MmapGen/src/game/vmap/ModelInstance.cpp::readFromFile:
    //   uint32 flags; uint16 adtId; uint32 ID;
    //   float[3] iPos; float[3] iRot; float iScale;
    //   if (flags & MOD_HAS_BOUND): float[3] bLow; float[3] bHigh;
    //   uint32 nameLen; char[nameLen] name;
    private static List<VmapSpawn> ParseVmtile(string path)
    {
        // From tools/MmapGen/src/game/vmap/ModelInstance.h: MOD_M2=1, MOD_HAS_BOUND=4.
        // Only WMOs have MOD_HAS_BOUND set; M2s do not store a bbox in the vmtile.
        const uint MOD_HAS_BOUND = 1u << 2;
        var bytes = File.ReadAllBytes(path);
        var spawns = new List<VmapSpawn>();
        if (bytes.Length < 12) return spawns;
        // magic "VMAP_7.0"
        if (Encoding.ASCII.GetString(bytes, 0, 8) != "VMAP_7.0") return spawns;
        var cursor = 8;
        var numSpawns = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor, 4));
        cursor += 4;
        for (uint i = 0; i < numSpawns && cursor < bytes.Length; i++)
        {
            if (cursor + 4 + 2 + 4 + 12 + 12 + 4 > bytes.Length) break;
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor, 4)); cursor += 4;
            /* adtId */ cursor += 2;
            /* ID    */ cursor += 4;
            var posX = BitConverter.ToSingle(bytes, cursor + 0);
            var posY = BitConverter.ToSingle(bytes, cursor + 4);
            var posZ = BitConverter.ToSingle(bytes, cursor + 8);
            cursor += 12;
            var rotX = BitConverter.ToSingle(bytes, cursor + 0);
            var rotY = BitConverter.ToSingle(bytes, cursor + 4);
            var rotZ = BitConverter.ToSingle(bytes, cursor + 8);
            cursor += 12;
            var scale = BitConverter.ToSingle(bytes, cursor); cursor += 4;
            var hasBound = (flags & MOD_HAS_BOUND) != 0;
            Vec3 bLow = default, bHigh = default;
            if (hasBound)
            {
                if (cursor + 24 > bytes.Length) break;
                bLow = new Vec3(
                    BitConverter.ToSingle(bytes, cursor + 0),
                    BitConverter.ToSingle(bytes, cursor + 4),
                    BitConverter.ToSingle(bytes, cursor + 8));
                bHigh = new Vec3(
                    BitConverter.ToSingle(bytes, cursor + 12),
                    BitConverter.ToSingle(bytes, cursor + 16),
                    BitConverter.ToSingle(bytes, cursor + 20));
                cursor += 24;
            }
            if (cursor + 4 > bytes.Length) break;
            var nameLen = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor, 4)); cursor += 4;
            if (nameLen > 500 || cursor + (int)nameLen > bytes.Length) break;
            var name = Encoding.ASCII.GetString(bytes, cursor, (int)nameLen).TrimEnd('\0');
            cursor += (int)nameLen;
            if (cursor + 4 > bytes.Length) break;
            /* referencedVal */ cursor += 4;
            spawns.Add(new VmapSpawn(
                Flags: flags,
                Pos: new Vec3(posX, posY, posZ),
                Rot: new Vec3(rotX, rotY, rotZ),
                Scale: scale,
                HasBound: hasBound,
                BoundLow: bLow,
                BoundHigh: bHigh,
                Name: name));
        }
        return spawns;
    }

    private static string SanitizeGroupName(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch == '_' || ch == '-') sb.Append(ch);
            else sb.Append('_');
        }
        return sb.Length == 0 ? "model" : sb.ToString();
    }

    private static int AddVert(List<Vec3> verts, Vec3 v)
    {
        // Could dedupe here, but for visualization fidelity we keep distinct
        // per-tri verts so face winding stays clean. OBJ index is 0-based here;
        // caller adds globalVertOffset.
        verts.Add(v);
        return verts.Count - 1;
    }
}

internal sealed record Tile(
    MeshHeader Header,
    Vec3[] Verts,
    Poly[] Polys,
    PolyDetail[] DetailMeshes,
    Vec3[] DetailVerts,
    (byte v0, byte v1, byte v2, byte flags)[] DetailTris,
    OffMeshConnection[] OffMesh);

internal sealed record MeshHeader(
    int X,
    int Y,
    int Layer,
    uint UserId,
    int PolyCount,
    int VertCount,
    int MaxLinkCount,
    int DetailMeshCount,
    int DetailVertCount,
    int DetailTriCount,
    int BvNodeCount,
    int OffMeshConCount,
    int OffMeshBase,
    float WalkableHeight,
    float WalkableRadius,
    float WalkableClimb,
    Vec3 BMin,
    Vec3 BMax,
    float BvQuantFactor);

internal sealed record Poly(
    uint FirstLink,
    ushort[] Verts,
    ushort[] Neis,
    ushort Flags,
    byte VertCount,
    byte AreaAndType);

internal sealed record PolyDetail(
    uint VertBase,
    uint TriBase,
    byte VertCount,
    byte TriCount);

internal sealed record OffMeshConnection(
    Vec3 Start,
    Vec3 End,
    float Radius,
    ushort Poly,
    byte Flags,
    byte Side,
    uint UserId);

internal readonly record struct Vec3(float X, float Y, float Z);

internal readonly record struct DetailPolyMetrics(
    float TotalSurfaceArea,
    float SteepSurfaceArea,
    float AvgSlopeDegrees,
    float MaxSlopeDegrees);

internal sealed record Marker(float X, float Y, float Z, string Label);

internal readonly record struct WowBounds(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ);

internal enum ReachableMode
{
    All,
    Only,
    Unreachable
}

internal sealed record VmapSpawn(
    uint Flags,
    Vec3 Pos,
    Vec3 Rot,
    float Scale,
    bool HasBound,
    Vec3 BoundLow,
    Vec3 BoundHigh,
    string Name);

internal readonly record struct PolyReportRow(
    int PolyIndex,
    int PolyType,
    int Area,
    ushort Flags,
    byte VertCount,
    int DetailTriCount,
    Vec3 Centroid,
    WowBounds Bounds,
    float ZRange,
    float MaxEdge2D,
    float HorizontalArea2D,
    int InternalNeighbors,
    float AvgDetailSlopeDegrees,
    float MaxDetailSlopeDegrees,
    float SteepDetailAreaRatio,
    bool SuspiciousMixedWall);

internal sealed class Args
{
    public string MmtilePath = "";
    public string OutPath = "";
    public List<Marker> Markers = new();
    public float ZBand = 2f;
    public bool ZUp;
    public bool Quiet;
    public string? VmapsDir;
    public Marker? ReachableFrom;
    public ReachableMode ReachableMode = ReachableMode.All;
    public WowBounds? CropWow;
    public HashSet<int> PolyFilter = new();
    public string? PolyReportPath;

    public static readonly string UsageText =
        "Usage: MmapVisualize --mmtile <path> --out <obj-path> [options]\n" +
        "Options:\n" +
        "  --mmtile <path>      input .mmtile file (required)\n" +
        "  --out <path>         output OBJ file (required)\n" +
        "  --mark X,Y,Z label   add a marker octahedron at WoW coord (X/Y horizontal, Z up); repeat\n" +
        "  --z-band <yards>     group polygons into Z bands of this size (default 2)\n" +
        "  --reachable-from X,Y,Z  find nearest ground poly to WoW coord and compute connected component\n" +
        "  --reachable-mode <mode> all|only|unreachable (default: only when --reachable-from is set)\n" +
        "  --crop minX,minY,minZ,maxX,maxY,maxZ  include only polygons whose WoW-space AABB intersects this box\n" +
        "  --polys i,j,k         include only these polygon indices, after reachability/crop filtering\n" +
        "  --poly-report <csv> emit CSV facts for rendered polygons (after crop/reachability filters)\n" +
        "  --z-up               output WoW-native Z-up (X/Y horizontal, Z vertical) for Blender/MeshLab\n" +
        "                       (default: Y-up OBJ, X=WoW X, Y=WoW Z, Z=-WoW Y)\n" +
        "  --include-vmaps <dir> overlay WMO instance bounding boxes from <dir>/<map>_<X>_<Y>.vmtile\n" +
        "  --quiet              suppress informational stderr\n";

    public static Args Parse(string[] argv)
    {
        var a = new Args();
        for (var i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--mmtile": a.MmtilePath = Next(argv, ref i, "--mmtile"); break;
                case "--out": a.OutPath = Next(argv, ref i, "--out"); break;
                case "--mark":
                    var coord = Next(argv, ref i, "--mark");
                    var label = Next(argv, ref i, "--mark <label>");
                    a.Markers.Add(ParseMark(coord, label));
                    break;
                case "--z-band":
                    a.ZBand = float.Parse(Next(argv, ref i, "--z-band"), CultureInfo.InvariantCulture);
                    break;
                case "--reachable-from":
                    var reachableCoord = Next(argv, ref i, "--reachable-from");
                    a.ReachableFrom = ParseMark(reachableCoord, "reachable_from");
                    if (a.ReachableMode == ReachableMode.All)
                        a.ReachableMode = ReachableMode.Only;
                    break;
                case "--reachable-mode":
                    a.ReachableMode = ParseReachableMode(Next(argv, ref i, "--reachable-mode"));
                    break;
                case "--crop":
                    a.CropWow = ParseBounds(Next(argv, ref i, "--crop"));
                    break;
                case "--polys":
                    foreach (var polyIndex in ParsePolyList(Next(argv, ref i, "--polys")))
                        a.PolyFilter.Add(polyIndex);
                    break;
                case "--poly-report":
                    a.PolyReportPath = Next(argv, ref i, "--poly-report");
                    break;
                case "--z-up": a.ZUp = true; break;
                case "--y-up": break; // accepted for backward compat with old default; new default IS y-up
                case "--include-vmaps": a.VmapsDir = Next(argv, ref i, "--include-vmaps"); break;
                case "--quiet": a.Quiet = true; break;
                case "-h":
                case "--help":
                    throw new ArgException("(help)");
                default:
                    throw new ArgException($"unknown arg: {argv[i]}");
            }
        }
        if (string.IsNullOrEmpty(a.MmtilePath)) throw new ArgException("--mmtile is required");
        if (string.IsNullOrEmpty(a.OutPath)) throw new ArgException("--out is required");
        if (!File.Exists(a.MmtilePath)) throw new ArgException($"mmtile not found: {a.MmtilePath}");
        if (a.ZBand <= 0) throw new ArgException("--z-band must be positive");
        return a;
    }

    private static ReachableMode ParseReachableMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "all" => ReachableMode.All,
            "only" => ReachableMode.Only,
            "reachable" => ReachableMode.Only,
            "unreachable" => ReachableMode.Unreachable,
            "unreachable-only" => ReachableMode.Unreachable,
            _ => throw new ArgException($"unknown reachable mode: {value}")
        };
    }

    private static string Next(string[] argv, ref int i, string name)
    {
        if (i + 1 >= argv.Length) throw new ArgException($"{name} requires a value");
        return argv[++i];
    }

    private static Marker ParseMark(string coord, string label)
    {
        var parts = coord.Split(',');
        if (parts.Length != 3) throw new ArgException($"--mark expects X,Y,Z; got {coord}");
        return new Marker(
            float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
            float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
            float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
            label);
    }

    private static WowBounds ParseBounds(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 6)
            throw new ArgException($"--crop expects minX,minY,minZ,maxX,maxY,maxZ; got {value}");

        var minX = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
        var minY = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
        var minZ = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
        var maxX = float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
        var maxY = float.Parse(parts[4].Trim(), CultureInfo.InvariantCulture);
        var maxZ = float.Parse(parts[5].Trim(), CultureInfo.InvariantCulture);

        return new WowBounds(
            MathF.Min(minX, maxX),
            MathF.Min(minY, maxY),
            MathF.Min(minZ, maxZ),
            MathF.Max(minX, maxX),
            MathF.Max(minY, maxY),
            MathF.Max(minZ, maxZ));
    }

    private static IEnumerable<int> ParsePolyList(string value)
    {
        foreach (var part in value.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
                continue;
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var polyIndex) || polyIndex < 0)
                throw new ArgException($"--polys expects non-negative integer indices; got {trimmed}");
            yield return polyIndex;
        }
    }
}

internal sealed class ArgException(string msg) : Exception(msg);
