// PathPhysicsProbe — drives Navigation.dll/Physics.dll to classify the runtime
// physics affordance of every segment on a path. For an MMO bot whose Detour
// path returns success but whose runtime says StepUp/Blocked mid-route, this
// tool localizes the FIRST segment where the bake-mesh-vs-runtime-physics
// disagreement happens and dumps the underlying surface enumeration so the
// fix surface (bake vs runtime) is obvious.
//
// CLI signature is intentionally fixed across the monorepo so the
// mmo-physics-pathing-probe Skill works against any game's tool variant.
//
//   PathPhysicsProbe.exe
//     --map <id>
//     --start X,Y,Z
//     --end   X,Y,Z
//     [--path corners.txt]   # one X,Y,Z per line; overrides --start/--end
//     [--radius R]           # default 1.0247 (Tauren M)
//     [--height H]           # default 2.625
//     [--detour-resolve]     # run FindPath first; probe the resolved corner sequence
//     [--smooth]             # paired with --detour-resolve, smoothPath=true
//     [--verbose]            # full physics details + endpoint surface enumeration
//     [--json]               # machine-parseable output
//
// Exit codes:  0 = clean Walk path  |  1 = StepUp/JumpGap/SafeDrop somewhere
//              2 = Blocked/UnsafeDrop somewhere  |  3 = arg/init failure

using System.Globalization;
using System.Text.Json;
using Navigation.Physics.Tests;
using static Navigation.Physics.Tests.NavigationInterop;

namespace PathPhysicsProbe;

internal static class Program
{
    public const float DefaultRadius = 1.0247f;   // Tauren Male
    public const float DefaultHeight = 2.625f;    // Tauren Male

    private static int Main(string[] args)
    {
        try
        {
            var opts = Args.Parse(args);
            var corners = LoadCorners(opts);
            if (corners.Count < 2)
            {
                Console.Error.WriteLine("error: need at least 2 corners (--start + --end, or --path with >=2 lines)");
                return 3;
            }

            if (opts.LoadAdt)
                MaybeLoadAdtForCorners(opts, corners);

            var probe = new SegmentProbe(opts.Radius, opts.Height, opts.Verbose);
            var results = new List<ProbeResult>(corners.Count - 1);
            for (int i = 0; i < corners.Count - 1; i++)
            {
                results.Add(probe.RunSegment(opts.MapId, i, corners[i], corners[i + 1]));
            }

            Emit(opts, results);

            int exit = 0;
            foreach (var r in results)
            {
                if (r.Affordance is SegmentAffordanceResult.Blocked or SegmentAffordanceResult.UnsafeDrop or SegmentAffordanceResult.Cliff)
                    exit = Math.Max(exit, 2);
                else if (r.Affordance != SegmentAffordanceResult.Walk)
                    exit = Math.Max(exit, 1);
            }
            return exit;
        }
        catch (ArgException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine(Args.UsageText);
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"fatal: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 3;
        }
    }

    private static List<Vector3> LoadCorners(Args opts)
    {
        if (opts.PathFile != null)
        {
            var lines = File.ReadAllLines(opts.PathFile);
            var pts = new List<Vector3>(lines.Length);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                pts.Add(Args.ParseVec(line, $"path file {opts.PathFile}"));
            }
            return pts;
        }

        var list = new List<Vector3> { opts.Start!.Value, opts.End!.Value };

        if (opts.DetourResolve)
        {
            var detourPath = FindPath(opts.MapId, list[0], list[^1], opts.SmoothPath);
            if (detourPath.Length >= 2)
            {
                Console.Error.WriteLine($"# detour-resolve: {detourPath.Length} corners returned by FindPath (smooth={opts.SmoothPath})");
                return detourPath.ToList();
            }
            Console.Error.WriteLine("# detour-resolve: FindPath returned <2 corners; falling back to start/end");
        }

        return list;
    }

    private static void Emit(Args opts, List<ProbeResult> results)
    {
        if (opts.JsonOutput)
        {
            var doc = new
            {
                map = opts.MapId,
                radius = opts.Radius,
                height = opts.Height,
                segmentCount = results.Count,
                firstFailure = FirstFailureIndex(results),
                segments = results,
            };
            Console.WriteLine(JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }));
            return;
        }

        Console.WriteLine("idx\tsx\tsy\tsz\tex\tey\tez\thDist\tvDelta\taffordance\tvalidation\tclimb\tdrop\tslope\tresolvedZ");
        foreach (var r in results)
        {
            Console.WriteLine(string.Join('\t', new[]
            {
                r.Index.ToString(CultureInfo.InvariantCulture),
                r.Start.X.ToString("F2", CultureInfo.InvariantCulture),
                r.Start.Y.ToString("F2", CultureInfo.InvariantCulture),
                r.Start.Z.ToString("F2", CultureInfo.InvariantCulture),
                r.End.X.ToString("F2", CultureInfo.InvariantCulture),
                r.End.Y.ToString("F2", CultureInfo.InvariantCulture),
                r.End.Z.ToString("F2", CultureInfo.InvariantCulture),
                r.HorizontalDistance.ToString("F2", CultureInfo.InvariantCulture),
                r.VerticalDelta.ToString("F2", CultureInfo.InvariantCulture),
                r.Affordance.ToString(),
                r.Validation.ToString(),
                r.ClimbHeight.ToString("F2", CultureInfo.InvariantCulture),
                r.DropHeight.ToString("F2", CultureInfo.InvariantCulture),
                r.SlopeAngleDeg.ToString("F2", CultureInfo.InvariantCulture),
                r.ResolvedEndZ.ToString("F2", CultureInfo.InvariantCulture),
            }));
        }

        var firstFail = FirstFailureIndex(results);
        if (firstFail < 0)
        {
            Console.Error.WriteLine($"# all {results.Count} segments classified Walk — clean path");
            return;
        }

        var bad = results[firstFail];
        Console.Error.WriteLine($"# first non-Walk segment: idx={bad.Index} affordance={bad.Affordance} validation={bad.Validation}");
        Console.Error.WriteLine($"#   {bad.Start.X:F2},{bad.Start.Y:F2},{bad.Start.Z:F2} -> {bad.End.X:F2},{bad.End.Y:F2},{bad.End.Z:F2}");
        Console.Error.WriteLine($"#   hDist={bad.HorizontalDistance:F2} vDelta={bad.VerticalDelta:F2} climb={bad.ClimbHeight:F2} drop={bad.DropHeight:F2} slope={bad.SlopeAngleDeg:F2}deg resolvedEndZ={bad.ResolvedEndZ:F2}");

        if (opts.Verbose && bad.EndpointSurfaces.Count > 0)
        {
            Console.Error.WriteLine($"#   surfaces at end ({bad.EndpointSurfaces.Count}):");
            foreach (var s in bad.EndpointSurfaces)
                Console.Error.WriteLine($"#     z={s.Z:F2} instance=0x{s.InstanceId:X}");
        }
        if (opts.Verbose && bad.GroundZ != null)
        {
            var gz = bad.GroundZ.Value;
            Console.Error.WriteLine($"#   GetGroundZBypassCache: combined={gz.Combined:F2} vmap={gz.Vmap:F2} adt={gz.Adt:F2} bih={gz.Bih:F2} sceneCache={gz.SceneCache:F2} dynamic={gz.Dynamic:F2}");
        }
    }

    private static int FirstFailureIndex(List<ProbeResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].Affordance != SegmentAffordanceResult.Walk)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Initialize the runtime physics MapLoader and pre-load every tile the
    /// corner sequence crosses. Without this, runtime physics queries return
    /// -200000 sentinels (no ADT data) for the probed XYs, polluting the
    /// classifier output with artifactual Blocked/JumpGap classifications.
    ///
    /// Tile coord convention matches Exports/Navigation/MapLoader.cpp's
    /// worldToGridCoords (lines 1114-1117) — the API takes the "natural"
    /// (tileX-from-worldX, tileY-from-worldY) order even though MmapGen.exe's
    /// CLI swaps these. tileX = floor(32 - worldX/533.333),
    /// tileY = floor(32 - worldY/533.333).
    /// </summary>
    private static void MaybeLoadAdtForCorners(Args opts, List<Vector3> corners)
    {
        const float GRID_SIZE = 533.33333f;
        const float CENTER_GRID_ID = 32f;

        var dataDir = opts.AdtDataDir ?? Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            Console.Error.WriteLine("# --load-adt: no path supplied and WWOW_DATA_DIR is unset; skipping ADT init.");
            return;
        }

        var mapsDir = Path.Combine(dataDir, "maps");
        if (!Directory.Exists(mapsDir))
            mapsDir = dataDir; // some callers point straight at the maps/ root

        if (!InitializeMapLoader(mapsDir + Path.DirectorySeparatorChar))
        {
            Console.Error.WriteLine($"# --load-adt: InitializeMapLoader failed for '{mapsDir}'.");
            return;
        }

        // Dedup tile coords across the entire corner sequence. One tile is
        // 533.33y x 533.33y, so a 1000-corner BRM-scale path typically lives
        // in 5-15 tiles and dedup is cheap.
        var tiles = new HashSet<(uint X, uint Y)>();
        foreach (var c in corners)
        {
            uint tx = (uint)Math.Max(0, Math.Floor(CENTER_GRID_ID - c.X / GRID_SIZE));
            uint ty = (uint)Math.Max(0, Math.Floor(CENTER_GRID_ID - c.Y / GRID_SIZE));
            tiles.Add((tx, ty));
        }

        int loaded = 0, missing = 0;
        foreach (var (tx, ty) in tiles)
        {
            if (LoadMapTile(opts.MapId, tx, ty)) loaded++;
            else missing++;
        }
        Console.Error.WriteLine(
            $"# --load-adt: dataDir='{mapsDir}' map={opts.MapId} corner-derived tiles={tiles.Count} loaded={loaded} missing={missing}");
    }
}

internal sealed class Args
{
    public uint MapId;
    public Vector3? Start;
    public Vector3? End;
    public string? PathFile;
    public float Radius = Program.DefaultRadius;
    public float Height = Program.DefaultHeight;
    public bool DetourResolve;
    public bool SmoothPath;
    public bool Verbose;
    public bool JsonOutput;
    public bool LoadAdt;
    public string? AdtDataDir;

    public static readonly string UsageText =
        "Usage: PathPhysicsProbe --map <id> --start X,Y,Z --end X,Y,Z [options]\n" +
        "       PathPhysicsProbe --map <id> --path corners.txt [options]\n" +
        "Options:\n" +
        "  --radius R          capsule radius (default 1.0247, Tauren M)\n" +
        "  --height H          capsule height (default 2.625)\n" +
        "  --detour-resolve    run FindPath first, then probe the resolved corners\n" +
        "  --smooth            paired with --detour-resolve, request smoothPath\n" +
        "  --verbose           include endpoint surface enumeration + GroundZ breakdown\n" +
        "  --json              emit machine-parseable JSON instead of TSV\n" +
        "  --load-adt [PATH]   auto-init MapLoader against PATH (or $WWOW_DATA_DIR)\n" +
        "                      and pre-load every tile the corner sequence crosses,\n" +
        "                      so GroundZ vmap/adt/bih return real values instead of\n" +
        "                      -200000 sentinels at probe-data-blind XYs\n";

    public static Args Parse(string[] argv)
    {
        var a = new Args();
        bool sawMap = false;
        for (int i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--map":
                    a.MapId = uint.Parse(Next(argv, ref i, "--map"), CultureInfo.InvariantCulture);
                    sawMap = true;
                    break;
                case "--start": a.Start = ParseVec(Next(argv, ref i, "--start"), "--start"); break;
                case "--end": a.End = ParseVec(Next(argv, ref i, "--end"), "--end"); break;
                case "--path": a.PathFile = Next(argv, ref i, "--path"); break;
                case "--radius": a.Radius = float.Parse(Next(argv, ref i, "--radius"), CultureInfo.InvariantCulture); break;
                case "--height": a.Height = float.Parse(Next(argv, ref i, "--height"), CultureInfo.InvariantCulture); break;
                case "--detour-resolve": a.DetourResolve = true; break;
                case "--smooth": a.SmoothPath = true; break;
                case "--verbose": a.Verbose = true; break;
                case "--json": a.JsonOutput = true; break;
                case "--load-adt":
                    a.LoadAdt = true;
                    // Optional positional path arg: only consume if it doesn't start with --
                    if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--"))
                        a.AdtDataDir = argv[++i];
                    break;
                case "-h":
                case "--help":
                    throw new ArgException("(help requested)");
                default:
                    throw new ArgException($"unknown arg: {argv[i]}");
            }
        }
        if (!sawMap) throw new ArgException("--map is required");
        if (a.PathFile == null && (a.Start == null || a.End == null))
            throw new ArgException("either --path or both --start and --end are required");
        return a;
    }

    private static string Next(string[] argv, ref int i, string name)
    {
        if (i + 1 >= argv.Length) throw new ArgException($"{name} requires a value");
        return argv[++i];
    }

    public static Vector3 ParseVec(string s, string context)
    {
        var parts = s.Split(',');
        if (parts.Length != 3) throw new ArgException($"{context}: expected X,Y,Z (3 components), got: {s}");
        return new Vector3(
            float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
            float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
            float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture));
    }
}

internal sealed class ArgException : Exception { public ArgException(string m) : base(m) { } }

internal sealed record ProbeResult(
    int Index,
    Vector3 Start,
    Vector3 End,
    float HorizontalDistance,
    float VerticalDelta,
    SegmentAffordanceResult Affordance,
    SegmentValidationResult Validation,
    float ClimbHeight,
    float DropHeight,
    float SlopeAngleDeg,
    float ResolvedEndZ,
    float GapDistance,
    IReadOnlyList<EndpointSurface> EndpointSurfaces,
    GroundZBreakdown? GroundZ);

internal sealed record EndpointSurface(float Z, uint InstanceId);

internal record struct GroundZBreakdown(float Combined, float Vmap, float Adt, float Bih, float SceneCache, float Dynamic);

internal sealed class SegmentProbe
{
    private readonly float _radius;
    private readonly float _height;
    private readonly bool _verbose;

    public SegmentProbe(float radius, float height, bool verbose)
    {
        _radius = radius;
        _height = height;
        _verbose = verbose;
    }

    public ProbeResult RunSegment(uint mapId, int idx, Vector3 start, Vector3 end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var hDist = MathF.Sqrt(dx * dx + dy * dy);
        var vDelta = end.Z - start.Z;

        var affordance = ClassifyPathSegmentAffordance(
            mapId, start, end, _radius, _height,
            out var climb, out var gap, out var drop, out var slope, out var resolvedZ,
            out var validation);

        var surfaces = _verbose ? EnumerateSurfaces(mapId, end) : Array.Empty<EndpointSurface>();
        GroundZBreakdown? gz = _verbose ? QueryGroundZ(mapId, end) : null;

        return new ProbeResult(
            idx, start, end, hDist, vDelta,
            affordance, validation,
            climb, drop, slope, resolvedZ, gap,
            surfaces, gz);
    }

    private static IReadOnlyList<EndpointSurface> EnumerateSurfaces(uint mapId, Vector3 end)
    {
        const int Cap = 16;
        var zs = new float[Cap];
        var instances = new uint[Cap];
        var count = EnumerateAllSurfacesAt(mapId, end.X, end.Y, zs, instances, Cap);
        if (count <= 0) return Array.Empty<EndpointSurface>();
        var list = new List<EndpointSurface>(count);
        for (int i = 0; i < count && i < Cap; i++) list.Add(new EndpointSurface(zs[i], instances[i]));
        return list;
    }

    private static GroundZBreakdown QueryGroundZ(uint mapId, Vector3 end)
    {
        var combined = GetGroundZBypassCache(mapId, end.X, end.Y, end.Z, 5.0f,
            out var vmap, out var adt, out var bih, out var scene);
        var dyn = GetDynamicGroundZDirect(mapId, end.X, end.Y, end.Z, 5.0f);
        return new GroundZBreakdown(combined, vmap, adt, bih, scene, dyn);
    }
}
