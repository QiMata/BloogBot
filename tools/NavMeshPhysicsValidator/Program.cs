// NavMeshPhysicsValidator — Slice A of the physics-validated mmap pipeline.
//
// Drives the runtime physics classifier (ClassifyPathSegmentAffordance — same
// C export PathPhysicsProbe and the BG runtime use) over a representative
// sample of paths through a navmesh tile. Identifies polygon-edges where the
// bake's walkability heuristics disagree with the runtime's full-physics view.
//
// Output is a JSON report:
//   - per-tile summary stats (paths found, segments classified, affordance
//     counts)
//   - the top N "worst" segments (Blocked / UnsafeDrop / Cliff) with full
//     classification details
//   - a XY heat-map: 5y x 5y cells with non-Walk segment counts, so problem
//     areas localize visually
//
// CLI:
//   NavMeshPhysicsValidator <mapId> --tile X,Y
//                           [--samples N]      (default 50 sample pairs)
//                           [--worst-top N]    (default 20 worst segments)
//                           [--cell-size R]    (default 5y XY heat-map cell)
//                           [--seed S]         (default 0 — deterministic)
//                           [--load-adt]       (default true)
//                           [--out <path>]     (default stdout)
//                           [--silent]
//
// Slice B (next session) extends this to a tile-rewrite pipeline that culls
// the bake-vs-runtime-rejected edges from the .mmtile before deployment.

using System.Globalization;
using System.Text.Json;
using Navigation.Physics.Tests;
using static Navigation.Physics.Tests.NavigationInterop;

namespace NavMeshPhysicsValidator;

internal static class Program
{
    // Tile coord ↔ world transform. MmapGen uses the same natural world-axis
    // order as MapBuilder::getTileBounds:
    //   tileX = floor(32 - WoW.X / GRID_SIZE)
    //   tileY = floor(32 - WoW.Y / GRID_SIZE)
    // The .mmtile filename still stores mapId,tileY,tileX.
    private const float GridSize = 533.33333f;
    private const float Origin = 32.0f * GridSize;     // 17066.66...

    // Capsule defaults match PathPhysicsProbe (Tauren M).
    private const float CapsuleRadius = 1.0247f;
    private const float CapsuleHeight = 2.625f;

    private static int Main(string[] args)
    {
        try
        {
            var opts = Args.Parse(args);
            if (!opts.Silent)
            {
                Console.Error.WriteLine($"# NavMeshPhysicsValidator map={opts.MapId} tile={opts.TileX},{opts.TileY} "
                    + $"samples={opts.SampleCount} seed={opts.Seed}");
            }

            // World AABB for the requested tile. The tile covers [low, high)
            // in both WoW X and WoW Y; we sample inside.
            var (xLo, xHi, yLo, yHi) = TileWorldBounds(opts.TileX, opts.TileY);
            if (!opts.Silent)
                Console.Error.WriteLine($"# tile world bounds: X=[{xLo:F1},{xHi:F1}] Y=[{yLo:F1},{yHi:F1}]");

            if (opts.LoadAdt)
            {
                MaybeLoadAdt(opts.MapId, opts.TileX, opts.TileY);
            }

            var samples = GenerateSamplePairs(opts.MapId, xLo, xHi, yLo, yHi, opts.SampleCount, opts.Seed);
            var report = new ValidationReport
            {
                MapId = opts.MapId,
                TileX = opts.TileX,
                TileY = opts.TileY,
                SamplesRequested = opts.SampleCount,
                CellSize = opts.CellSize,
            };

            var heat = new Dictionary<(int cx, int cy), int>();

            foreach (var (start, end) in samples)
            {
                Vector3[] path;
                try
                {
                    path = FindPath(opts.MapId, start, end, smoothPath: true);
                }
                catch
                {
                    report.PathQueriesFailed++;
                    continue;
                }
                if (path.Length < 2)
                {
                    report.PathQueriesEmpty++;
                    continue;
                }
                report.PathsFound++;
                report.TotalCorners += path.Length;

                // Cap segments classified per path. ClassifyPathSegmentAffordance
                // is ~10ms each, and a smooth path can have 1000+ corners. With
                // an even-stride sample we get representative coverage at a
                // bounded cost. The cap kicks in for tiles like BRM where
                // FindPath returns 1000-corner paths.
                var segCount = path.Length - 1;
                var stride = MathF.Max(1, segCount / (float)opts.MaxSegmentsPerPath);
                for (var s = 0f; s < segCount; s += stride)
                {
                    var i = (int)s;
                    if (i >= segCount) break;
                    var segStart = path[i];
                    var segEnd = path[i + 1];

                    var affordance = ClassifyPathSegmentAffordance(
                        opts.MapId, segStart, segEnd,
                        CapsuleRadius, CapsuleHeight,
                        out var climb, out var gap, out var drop, out var slope, out var resolvedZ,
                        out var validation);

                    report.TotalSegments++;
                    report.AffordanceCounts[affordance.ToString()] =
                        report.AffordanceCounts.GetValueOrDefault(affordance.ToString(), 0) + 1;

                    if (affordance != SegmentAffordanceResult.Walk)
                    {
                        report.NonWalkSegments++;

                        // "Unrecoverable" = the bot physically cannot complete
                        // this segment at runtime. SafeDrop / StepUp / Vertical
                        // / SteepClimb are non-Walk classifications the bot can
                        // still navigate (they encode "this is a recoverable
                        // non-flat traversal"). Blocked / UnsafeDrop / Cliff
                        // are the runtime saying "no path here" — these are
                        // the segments that would cause the bot to stall.
                        // The unrecoverable rate is the meaningful triage
                        // metric for bake-vs-runtime mismatch.
                        var unrecoverable = affordance is SegmentAffordanceResult.Blocked
                            or SegmentAffordanceResult.UnsafeDrop
                            or SegmentAffordanceResult.Cliff;
                        if (unrecoverable)
                            report.UnrecoverableNonWalk++;

                        // Capture polyref at each endpoint via GetPolyAtCoord
                        // ONLY for unrecoverable affordances. Slice B's cull
                        // tool reads this list and zeros each polygon's area
                        // so Detour skips it; doing that for SafeDrop /
                        // StepUp / Vertical / SteepClimb (which the bot CAN
                        // navigate) would over-cull and leave the navmesh
                        // unusable. searchExtentXY=2y + searchExtentZ=
                        // walkableClimb=1.8y matches PathfindingService.Tests
                        // default. PolyRef==0 means "no poly found nearby" —
                        // happens when the segment endpoint is in unbaked
                        // terrain (vmap/adt sparse area).
                        if (unrecoverable)
                        {
                            ulong polyA = 0, polyB = 0;
                            try
                            {
                                GetPolyAtCoord(opts.MapId, segStart, 2f, 1.8f,
                                    out polyA, out _, out _, out _, out _);
                                GetPolyAtCoord(opts.MapId, segEnd, 2f, 1.8f,
                                    out polyB, out _, out _, out _, out _);
                            }
                            catch { /* tolerate per-call failure */ }

                            if (polyA != 0 && polyB != 0)
                            {
                                var edge = new PolyEdge(polyA, polyB);
                                report.PolyEdgeCounts[edge] = report.PolyEdgeCounts.GetValueOrDefault(edge, 0) + 1;
                            }
                        }

                        // Update heat map at the SEGMENT MIDPOINT for clearer hotspot localization.
                        var midX = (segStart.X + segEnd.X) * 0.5f;
                        var midY = (segStart.Y + segEnd.Y) * 0.5f;
                        var cx = (int)MathF.Floor(midX / opts.CellSize);
                        var cy = (int)MathF.Floor(midY / opts.CellSize);
                        heat[(cx, cy)] = heat.GetValueOrDefault((cx, cy), 0) + 1;

                        report.AddBadSegment(new BadSegment
                        {
                            Start = segStart,
                            End = segEnd,
                            Affordance = affordance,
                            Validation = validation,
                            Climb = climb,
                            Drop = drop,
                            SlopeDeg = slope,
                            Gap = gap,
                            ResolvedEndZ = resolvedZ,
                        });
                    }
                }
            }

            // Pick top N worst segments by drop+climb sum (proxy for "how bad").
            report.WorstSegments = report.AllBadSegments
                .OrderByDescending(s => MathF.Max(s.Drop, s.Climb))
                .Take(opts.WorstTop)
                .ToList();

            // Targeted-coord cull: for each --cull-coord, look up polyrefs
            // via GetPolyAtCoord and add them as self-loop edges with high
            // count so the orchestrator's CullMinCount filter doesn't drop
            // them. Used when path-sampling can't reach the trap polygon
            // directly (BRM-style: the trap is reachable from many
            // directions, so sampling finds incoming neighbors but not the
            // trap itself).
            //
            // Slice E: when CullCoordZRadius > 0, probe a Z-stack at the seed
            // XY (dz in [-R, +R] at fine steps) and enumerate ALL distinct
            // polygons. WMO-interior traps stack multiple walkable polys at
            // slightly different Z values; single-Z probe only finds one,
            // letting the bot stall on a sibling polygon after cull.
            // GetPolyAtCoord uses a tight Z extent (0.4y) so each probe
            // returns the polygon AT that Z, not the nearest in a window.
            foreach (var coord in opts.CullCoords)
            {
                var foundPolys = new HashSet<ulong>();

                // XY offsets: {-R, 0, +R} when XyRadius > 0, else just {0}.
                // Z offsets: stepped {-R..+R} when ZRadius > 0, else just {0}.
                // Combined produces a 3×3×N probe matrix per seed; the
                // hash-set dedupes polyrefs found multiple times.
                const float zStep = 0.25f;
                const float zSearchExtent = 0.4f;
                var xyOffsets = opts.CullCoordXyRadius > 0f
                    ? new[] { -opts.CullCoordXyRadius, 0f, +opts.CullCoordXyRadius }
                    : new[] { 0f };
                var zR = opts.CullCoordZRadius;
                int zSteps = zR > 0f ? (int)MathF.Ceiling(2 * zR / zStep) + 1 : 1;

                foreach (var dx in xyOffsets)
                foreach (var dy in xyOffsets)
                {
                    if (zR <= 0f)
                    {
                        var probe = new Vector3(coord.X + dx, coord.Y + dy, coord.Z);
                        ulong polyRef = 0;
                        try
                        {
                            GetPolyAtCoord(opts.MapId, probe, 2f, 1.8f,
                                out polyRef, out _, out _, out _, out _);
                        }
                        catch { /* tolerate */ }
                        if (polyRef != 0) foundPolys.Add(polyRef);
                    }
                    else
                    {
                        for (float dz = -zR; dz <= zR + 1e-3f; dz += zStep)
                        {
                            var probe = new Vector3(coord.X + dx, coord.Y + dy, coord.Z + dz);
                            ulong polyRef = 0;
                            try
                            {
                                GetPolyAtCoord(opts.MapId, probe, 2f, zSearchExtent,
                                    out polyRef, out _, out _, out _, out _);
                            }
                            catch { /* tolerate */ }
                            if (polyRef != 0) foundPolys.Add(polyRef);
                        }
                    }
                }

                foreach (var polyRef in foundPolys)
                {
                    var edge = new PolyEdge(polyRef, polyRef);
                    report.PolyEdgeCounts[edge] = report.PolyEdgeCounts.GetValueOrDefault(edge, 0) + 1000;
                }
                if (!opts.Silent)
                {
                    if (foundPolys.Count == 0)
                        Console.Error.WriteLine($"# --cull-coord ({coord.X:F1},{coord.Y:F1},{coord.Z:F1}) zRadius={zR:F1} xyRadius={opts.CullCoordXyRadius:F1} → no poly found");
                    else
                        Console.Error.WriteLine($"# --cull-coord ({coord.X:F1},{coord.Y:F1},{coord.Z:F1}) zRadius={zR:F1} xyRadius={opts.CullCoordXyRadius:F1} → {foundPolys.Count} unique polys: [{string.Join(",", foundPolys)}]");
                }
            }

            // RejectedEdges: every (polyA, polyB) pair that produced at least
            // one non-Walk segment. Sorted by frequency so the "most rejected"
            // edges show first. Slice B's cull tool reads this list and marks
            // each polygon non-walkable in the .mmtile.
            report.RejectedEdges = report.PolyEdgeCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new PolyEdgeStat
                {
                    PolyA = kv.Key.PolyA,
                    PolyB = kv.Key.PolyB,
                    BadSegmentCount = kv.Value,
                })
                .ToList();

            // Hotspot cells: top by non-Walk count, with center coords.
            report.HotspotCells = heat
                .OrderByDescending(kv => kv.Value)
                .Take(opts.WorstTop)
                .Select(kv => new HotspotCell
                {
                    CellX = kv.Key.cx,
                    CellY = kv.Key.cy,
                    CenterX = (kv.Key.cx + 0.5f) * opts.CellSize,
                    CenterY = (kv.Key.cy + 0.5f) * opts.CellSize,
                    NonWalkCount = kv.Value,
                })
                .ToList();

            EmitReport(opts, report);

            // Exit code mirrors PathPhysicsProbe convention: 0=clean, 1=non-Walk
            // present (StepUp/SafeDrop/etc), 2=critical non-Walk (Blocked/Cliff/UnsafeDrop).
            int exit = 0;
            foreach (var bad in report.AllBadSegments)
            {
                if (bad.Affordance is SegmentAffordanceResult.Blocked
                    or SegmentAffordanceResult.UnsafeDrop
                    or SegmentAffordanceResult.Cliff)
                    exit = Math.Max(exit, 2);
                else
                    exit = Math.Max(exit, 1);
            }
            return exit;
        }
        catch (ArgException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine(Args.Usage);
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"fatal: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 3;
        }
    }

    private static (float xLo, float xHi, float yLo, float yHi) TileWorldBounds(int tileX, int tileY)
    {
        // tileX = floor((ORIGIN - WoW.X) / GRID_SIZE)  →  WoW.X in [ORIGIN-(tileX+1)*GRID, ORIGIN-tileX*GRID)
        // tileY = floor((ORIGIN - WoW.Y) / GRID_SIZE)  →  WoW.Y in [ORIGIN-(tileY+1)*GRID, ORIGIN-tileY*GRID)
        var xLo = Origin - (tileX + 1) * GridSize;
        var xHi = Origin - tileX * GridSize;
        var yLo = Origin - (tileY + 1) * GridSize;
        var yHi = Origin - tileY * GridSize;
        return (xLo, xHi, yLo, yHi);
    }

    private static List<(Vector3 start, Vector3 end)> GenerateSamplePairs(
        uint mapId, float xLo, float xHi, float yLo, float yHi, int count, int seed)
    {
        // Sample end-points within MaxPairDist of the start so the FindPath query
        // is more likely to hit two reachable polygons. Tile-wide random pairs
        // frequently land on disconnected navmesh components and FindPath
        // returns empty. A 100y locality keeps the bot inside one connected
        // component most of the time while still covering the whole tile across
        // many samples.
        const float MaxPairDist = 100f;

        // Altitude scan tuned for routine use. The original 1500→-50 step=25
        // range made 60+ GetGroundZ calls per sample (3,600 per tile run);
        // each call may force lazy VMAP init, so a tile took ~50min. The
        // narrower 600→-100 step=50 range makes ~14 calls per sample (~840
        // per tile run) while still covering 99% of map-0/map-1 terrain.
        // Tiles whose surface is outside [-100, 600] (e.g. Hyjal at 1000+)
        // need an explicit --probe-altitude override (TODO) or accept that
        // those samples will fall back to Z=300 + FindPath snap.
        const float ProbeAltitudeMin = -100f;
        const float ProbeAltitudeMax = 600f;
        const float ProbeStep = 50f;
        const float SearchDistance = 30f;        // larger window so fewer probe steps miss
        const float FallbackZ = 300f;

        var rng = new Random(seed);
        var pairs = new List<(Vector3, Vector3)>(count);

        Vector3 SnapToGround(float wx, float wy)
        {
            for (var z = ProbeAltitudeMax; z >= ProbeAltitudeMin; z -= ProbeStep)
            {
                var gz = GetGroundZ(mapId, wx, wy, z, SearchDistance);
                if (gz > -100000f)
                    return new Vector3(wx, wy, gz);
            }
            return new Vector3(wx, wy, FallbackZ);
        }

        for (var i = 0; i < count; i++)
        {
            var sx = xLo + (float)rng.NextDouble() * (xHi - xLo);
            var sy = yLo + (float)rng.NextDouble() * (yHi - yLo);

            var theta = (float)(rng.NextDouble() * 2 * Math.PI);
            var dist = 10f + (float)rng.NextDouble() * (MaxPairDist - 10f);
            var ex = sx + dist * MathF.Cos(theta);
            var ey = sy + dist * MathF.Sin(theta);
            ex = MathF.Max(xLo, MathF.Min(xHi - 0.01f, ex));
            ey = MathF.Max(yLo, MathF.Min(yHi - 0.01f, ey));

            pairs.Add((SnapToGround(sx, sy), SnapToGround(ex, ey)));
        }
        return pairs;
    }

    private static void MaybeLoadAdt(uint mapId, int tileX, int tileY)
    {
        var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (string.IsNullOrEmpty(dataDir)) return;
        var mapsDir = Path.Combine(dataDir, "maps");
        if (!Directory.Exists(mapsDir)) return;

        try
        {
            InitializeMapLoader(mapsDir);
            // Load the tile and its 8 neighbors for full-coverage probing.
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                    LoadMapTile(mapId, (uint)(tileX + dx), (uint)(tileY + dy));
        }
        catch
        {
            // Best-effort load; downstream queries will degrade if data is missing.
        }
    }

    private static void EmitReport(Args opts, ValidationReport report)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        });

        if (string.IsNullOrEmpty(opts.OutputPath))
        {
            Console.WriteLine(json);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opts.OutputPath))!);
            File.WriteAllText(opts.OutputPath, json);
            if (!opts.Silent)
                Console.Error.WriteLine($"# wrote {opts.OutputPath}");
        }

        if (!opts.Silent)
        {
            Console.Error.WriteLine($"# samples={report.SamplesRequested} pathsFound={report.PathsFound} "
                + $"segments={report.TotalSegments} nonWalk={report.NonWalkSegments} "
                + $"({(report.TotalSegments > 0 ? 100.0 * report.NonWalkSegments / report.TotalSegments : 0):F1}%) "
                + $"unrecoverable={report.UnrecoverableNonWalk} "
                + $"({(report.TotalSegments > 0 ? 100.0 * report.UnrecoverableNonWalk / report.TotalSegments : 0):F1}%)");
            Console.Error.WriteLine("# affordance breakdown:");
            foreach (var kv in report.AffordanceCounts.OrderByDescending(k => k.Value))
                Console.Error.WriteLine($"#   {kv.Key,-20} {kv.Value,6}");
            if (report.HotspotCells.Count > 0)
            {
                Console.Error.WriteLine($"# top {report.HotspotCells.Count} hotspot cells (XY {opts.CellSize}y):");
                foreach (var c in report.HotspotCells)
                    Console.Error.WriteLine(
                        $"#   ({c.CenterX:F1},{c.CenterY:F1}) nonWalk={c.NonWalkCount}");
            }
        }
    }
}

internal sealed class ValidationReport
{
    public uint MapId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int SamplesRequested { get; set; }
    public int PathsFound { get; set; }
    public int PathQueriesFailed { get; set; }
    public int PathQueriesEmpty { get; set; }
    public int TotalCorners { get; set; }
    public int TotalSegments { get; set; }
    public int NonWalkSegments { get; set; }
    /// "Unrecoverable" non-Walk = Blocked / UnsafeDrop / Cliff. These are the
    /// segments the runtime classifier says the bot cannot physically traverse.
    /// Recoverable non-Walk (SafeDrop / StepUp / Vertical / SteepClimb) are
    /// non-flat traversals the bot CAN navigate. Triage on this number, not
    /// on raw NonWalkSegments.
    public int UnrecoverableNonWalk { get; set; }
    public float CellSize { get; set; }
    public Dictionary<string, int> AffordanceCounts { get; } = new();
    public List<BadSegment> WorstSegments { get; set; } = new();
    public List<HotspotCell> HotspotCells { get; set; } = new();
    public List<BadSegment> AllBadSegments { get; } = new();
    /// Per-(polyRefA, polyRefB) bad-edge counts. Slice B cull pass uses these
    /// to identify edges that consistently fail physics validation — those
    /// are the ones to mark non-walkable in the .mmtile. Serialized as a
    /// flat list of {polyA, polyB, count} entries since Dictionary keys
    /// don't survive JSON well.
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<PolyEdge, int> PolyEdgeCounts { get; } = new();
    public List<PolyEdgeStat> RejectedEdges { get; set; } = new();

    private const int MaxBadSegmentsRetained = 4096;

    public void AddBadSegment(BadSegment seg)
    {
        if (AllBadSegments.Count < MaxBadSegmentsRetained)
            AllBadSegments.Add(seg);
    }
}

internal sealed class BadSegment
{
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    public SegmentAffordanceResult Affordance { get; set; }
    public SegmentValidationResult Validation { get; set; }
    public float Climb { get; set; }
    public float Drop { get; set; }
    public float SlopeDeg { get; set; }
    public float Gap { get; set; }
    public float ResolvedEndZ { get; set; }
}

internal sealed class HotspotCell
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public int NonWalkCount { get; set; }
}

/// Composite key for poly-edge counting. Edge ordering is preserved (A→B vs
/// B→A counted separately) since the runtime affordance is direction-aware
/// (climbing UP vs dropping DOWN is different).
internal readonly record struct PolyEdge(ulong PolyA, ulong PolyB);

/// Serializable form of PolyEdge with the bad-segment count for that pair.
/// Slice B cull pass reads RejectedEdges and marks each (PolyA, PolyB) as
/// non-walkable in the .mmtile.
internal sealed class PolyEdgeStat
{
    public ulong PolyA { get; set; }
    public ulong PolyB { get; set; }
    public int BadSegmentCount { get; set; }
}

internal sealed class Args
{
    public uint MapId { get; private set; }
    public int TileX { get; private set; } = -1;
    public int TileY { get; private set; } = -1;
    public int SampleCount { get; private set; } = 20;
    public int WorstTop { get; private set; } = 20;
    public float CellSize { get; private set; } = 5.0f;
    public int Seed { get; private set; } = 0;
    /// Cap segments classified per path. ClassifyPathSegmentAffordance is
    /// ~10ms each; smooth paths can be 1000+ corners. Even-stride sampling
    /// gives representative coverage at bounded cost.
    public int MaxSegmentsPerPath { get; private set; } = 100;
    /// Z-stack enumeration radius for --cull-coord. When > 0, the validator
    /// probes (X, Y, Z+dz) for dz in [-R, +R] in fine steps to find every
    /// distinct polygon at that XY. Used for WMO-interior traps where the
    /// runtime sees multiple stacked walkable polygons at slightly different
    /// Z values; a single GetPolyAtCoord finds only one. Default 0 = single
    /// probe (legacy behavior). Recommended 15y for WMO interiors.
    public float CullCoordZRadius { get; private set; } = 0f;
    /// XY-stack radius. When > 0, the validator probes a 3×3 XY grid
    /// (offsets {-R, 0, +R}) at each --cull-coord, multiplying the
    /// Z-stack enumeration. Used when the bot stall fluctuates by 0.1–1y
    /// in XY across runs — single-XY probe misses the surrounding cluster.
    public float CullCoordXyRadius { get; private set; } = 0f;
    public bool LoadAdt { get; private set; } = true;
    public bool Silent { get; private set; } = false;
    public string? OutputPath { get; private set; }
    /// Optional list of seed coords. For each, GetPolyAtCoord is called and
    /// the resulting polyref is added to RejectedEdges (as PolyA in a
    /// self-loop edge with BadSegmentCount=1). Used to surgically cull a
    /// specific trap polygon when path-sampling alone misses it (e.g., the
    /// BRM stall coord is reachable from many directions; culling its
    /// incoming neighbors per-edge isn't sufficient because the bot finds
    /// a different incoming edge each route. Culling the trap polygon
    /// itself blocks every route.).
    public List<Vector3> CullCoords { get; private set; } = new();

    public const string Usage =
        "Usage:\n" +
        "  NavMeshPhysicsValidator <mapId> --tile X,Y\n" +
        "                          [--samples N] [--max-segments-per-path N]\n" +
        "                          [--worst-top N] [--cell-size R]\n" +
        "                          [--seed S] [--no-load-adt] [--out <path>] [--silent]\n";

    public static Args Parse(string[] argv)
    {
        var a = new Args();
        var positional = new List<string>();
        for (var i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--tile" when i + 1 < argv.Length:
                    var coords = argv[++i].Split(',');
                    if (coords.Length != 2)
                        throw new ArgException("--tile expects X,Y");
                    a.TileX = int.Parse(coords[0], CultureInfo.InvariantCulture);
                    a.TileY = int.Parse(coords[1], CultureInfo.InvariantCulture);
                    break;
                case "--samples" when i + 1 < argv.Length:
                    a.SampleCount = int.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--worst-top" when i + 1 < argv.Length:
                    a.WorstTop = int.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--cell-size" when i + 1 < argv.Length:
                    a.CellSize = float.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--seed" when i + 1 < argv.Length:
                    a.Seed = int.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--max-segments-per-path" when i + 1 < argv.Length:
                    a.MaxSegmentsPerPath = int.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--cull-coord" when i + 1 < argv.Length:
                    {
                        var parts = argv[++i].Split(',');
                        if (parts.Length != 3)
                            throw new ArgException("--cull-coord expects X,Y,Z");
                        a.CullCoords.Add(new Vector3(
                            float.Parse(parts[0], CultureInfo.InvariantCulture),
                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                            float.Parse(parts[2], CultureInfo.InvariantCulture)));
                    }
                    break;
                case "--cull-coord-z-radius" when i + 1 < argv.Length:
                    a.CullCoordZRadius = float.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--cull-coord-xy-radius" when i + 1 < argv.Length:
                    a.CullCoordXyRadius = float.Parse(argv[++i], CultureInfo.InvariantCulture);
                    break;
                case "--out" when i + 1 < argv.Length:
                    a.OutputPath = argv[++i];
                    break;
                case "--no-load-adt":
                    a.LoadAdt = false;
                    break;
                case "--silent":
                    a.Silent = true;
                    break;
                case "--help" or "-h":
                    Console.Error.WriteLine(Usage);
                    Environment.Exit(0);
                    break;
                default:
                    if (argv[i].StartsWith("--"))
                        throw new ArgException($"unknown flag: {argv[i]}");
                    positional.Add(argv[i]);
                    break;
            }
        }

        if (positional.Count < 1)
            throw new ArgException("missing required <mapId>");
        if (!uint.TryParse(positional[0], out var mid))
            throw new ArgException($"invalid mapId: {positional[0]}");
        a.MapId = mid;
        if (a.TileX < 0 || a.TileY < 0)
            throw new ArgException("--tile X,Y is required");
        if (a.SampleCount < 1)
            throw new ArgException("--samples must be >= 1");
        if (a.CellSize <= 0)
            throw new ArgException("--cell-size must be > 0");
        return a;
    }
}

internal sealed class ArgException : Exception
{
    public ArgException(string message) : base(message) { }
}
