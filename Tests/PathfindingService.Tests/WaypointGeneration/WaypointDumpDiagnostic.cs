using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// PFS-OVERHAUL-006 — non-asserting durable diagnostics for OG zeppelin
/// pathing. Each <c>[Fact]</c> below maps to one investigation task that
/// has proven useful for diagnosing a specific kind of failure. Run any of
/// them individually with <c>--filter "FullyQualifiedName~&lt;method&gt;"</c>.
///
/// All methods read-only — they P/Invoke <c>Navigation.dll</c> against
/// the data dir set by <see cref="PathfindingValidationFixture"/>.
/// </summary>
[Trait("Category", "Unit")]
public class WaypointDumpDiagnostic : IClassFixture<PathfindingValidationFixture>
{
    private readonly PathfindingValidationFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const uint MapId = 1;
    private const float AgentRadius = 1.0247f;   // Tauren M
    private const float AgentHeight = 2.625f;
    private const float WalkableClimb = 1.8f;    // harvested-from-client; matches bake

    // Verified-from-MaNGOS-DB / FG-screenshot-confirmed reference points.
    // See memory project_pfs_overhaul_006_intra_tile_disconnect.md and
    // reference_og_zeppelin_layout.md for sourcing.
    private static readonly XYZ FmTowerTop      = new(1677f, -4315f, 61.4f);          // Doras the Wind Rider Master tower
    private static readonly XYZ Grunt1          = new(1332.76f, -4633.40f, 24.0783f); // creature.guid=3462 entry 3296 lower platform
    private static readonly XYZ Grunt2          = new(1340.71f, -4631.52f, 24.1187f); // creature.guid=3465 entry 3296 lower platform
    private static readonly XYZ Frezza          = new(1331.11f, -4649.45f, 53.6269f); // creature.guid=3464 entry 9564 upper deck
    private static readonly XYZ Snurk           = new(1353.97f, -4642.56f, 53.63f);   // creature.guid=3463 entry 12136 upper deck
    private static readonly XYZ DeckLipFoot     = new(1338.13f, -4645.96f, 51.60f);   // FG-verified lower-deck wooden platform
    private static readonly XYZ BoardingPosition = new(1320.142944f, -4653.158691f, 53.891945f); // gangplank attach

    public WaypointDumpDiagnostic(PathfindingValidationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ============================================================== Diagnostic 1
    /// <summary>
    /// Smooth-path dump for the full climb route (FM tower → Frezza). Prints
    /// every waypoint's X/Y/Z, the underlying poly's surface Z, and dz from
    /// the prev waypoint. Use to spot Z anomalies (waypoints below their own
    /// poly's surface, sudden vertical jumps).
    /// </summary>
    [Fact]
    public void Dump_ClimbRoute_SmoothPathWaypoints()
    {
        DumpSmoothPath("FM tower → Frezza", FmTowerTop, Frezza);
    }

    // ============================================================== Diagnostic 2
    /// <summary>
    /// Polygon-chain dump from one of the canonical NPC pairs. Cycle 17b
    /// proved this baseline is HEALTHY (Grunt1 → Frezza = 106 polys all in
    /// tile (40,29), file 0012940, endsAtTarget). If this fails, the bake
    /// regressed.
    /// </summary>
    [Fact]
    public void Dump_GruntToFrezza_PolygonChain()
    {
        DumpPolygonChain("Grunt1 → Frezza (in-tower path, baseline)", Grunt1, Frezza);
    }

    // ============================================================== Diagnostic 3
    /// <summary>
    /// Off-mesh-link binding audit. Prints total/linked counts via
    /// <c>CountLinkedOffMeshPolysOnMap</c> and surfaces stderr-emitted
    /// <c>[OFFLINK]</c> SKIPs (raise verbosity in test runner to capture).
    /// Healthy state: linked == total. After Cycle 16, this regressed to
    /// 0/5 because the 4 z=96 phantom anchors are gone in the fine bake.
    /// </summary>
    [Fact]
    public void Audit_OffMeshLinkBinding()
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        var counts = NavigationInterop.QueryOffMeshLinkCounts(MapId);
        _output.WriteLine($"# CountLinkedOffMeshPolysOnMap(map={MapId}):");
        _output.WriteLine($"#   Success={counts.Success} total={counts.Total} linked={counts.Linked} unlinked={counts.Total - counts.Linked}");
        _output.WriteLine($"# (look at stderr for [OFFLINK] SKIP lines if linked < total)");
    }

    // ============================================================== Diagnostic 4
    /// <summary>
    /// In-tile (40,29 / file 0012940) NPC↔NPC pathing matrix. If the tower's internal
    /// navmesh is healthy, every NPC pair returns a chain that
    /// endsAtTarget. Cycle 17b proved this is true post-Cycle-16.
    /// </summary>
    [Fact]
    public void Audit_TowerNpcPathingMatrix()
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        var pairs = new (string label, XYZ a, XYZ b)[]
        {
            ("Grunt1 → Grunt2 (lower platform)",     Grunt1, Grunt2),
            ("Grunt1 → Frezza (climb 24→53)",        Grunt1, Frezza),
            ("Grunt2 → Frezza",                      Grunt2, Frezza),
            ("Frezza → Snurk (deck only)",           Frezza, Snurk),
            ("Frezza → BoardingPosition (gangplank)", Frezza, BoardingPosition),
            ("DeckLipFoot → Frezza (the deck-lip)",  DeckLipFoot, Frezza),
        };
        _output.WriteLine("# from → to                                   | polys | endsAtTarget?");
        _output.WriteLine("# -------------------------------------------+-------+--------------");
        foreach (var (label, a, b) in pairs)
        {
            var chain = NavigationInterop.QueryPathPolygons(MapId, a, b, AgentRadius, AgentHeight, maxOut: 4096);
            int n = chain.PolyRefs.Length;
            var endProbe = NavigationInterop.QueryPolyAtCoord(MapId, b, AgentRadius, 300f);
            bool endsAtTarget = n > 0 && endProbe.HasPoly && endProbe.PolyRef == chain.PolyRefs[n - 1];
            _output.WriteLine($"#   {label,-43} |  {n,4} | {(endsAtTarget ? "YES" : "NO")}");
        }
    }

    // ============================================================== Diagnostic 5
    /// <summary>
    /// Drill-down on the deck-lip step-up that breaks the climb at runtime.
    /// Locates the largest +Z step in the smooth path and dumps the polys
    /// around it. Use to compare deck-lip step magnitude across bake
    /// variants.
    /// </summary>
    [Fact]
    public void Audit_DeckLipStep_Polygons()
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        var smooth = NavigationInterop.QuerySmoothPath(MapId, Grunt1, Frezza, AgentRadius, AgentHeight);
        if (!smooth.Success || smooth.Length < 5)
        {
            _output.WriteLine($"# ABORT: smooth path too short. Length={smooth.Length}");
            return;
        }

        int stepIdx = 0; float maxStepDz = 0f;
        for (int i = 1; i < smooth.Length; i++)
        {
            float dz = smooth.Waypoints[i].Z - smooth.Waypoints[i - 1].Z;
            if (dz > maxStepDz) { maxStepDz = dz; stepIdx = i; }
        }

        _output.WriteLine($"# Smooth path Grunt1 → Frezza: {smooth.Length} waypoints");
        _output.WriteLine($"# Largest +Z step: WP {stepIdx - 1} → {stepIdx}, dz={maxStepDz:F2}y");
        _output.WriteLine($"# Bake walkableClimb={WalkableClimb}y. Runtime tolerance check at NavigationPath.cs:533.");
        _output.WriteLine($"# If maxStepDz exceeds NavigationPath's WAYPOINT_VERTICAL_REACH_TOLERANCE,");
        _output.WriteLine($"# the bot's CanTreatWaypointAsReached returns false → stalled_near_waypoint.");
        _output.WriteLine("");
        _output.WriteLine("# idx |       waypoint X,Y,Z       | nearest polyRef     | surfaceZ | dz(prev) ");
        _output.WriteLine("# ----+----------------------------+---------------------+----------+----------");
        int lo = Math.Max(0, stepIdx - 5);
        int hi = Math.Min(smooth.Length, stepIdx + 5);
        for (int i = lo; i < hi; i++)
        {
            var w = smooth.Waypoints[i];
            var p = NavigationInterop.QueryPolyAtCoord(MapId, w, AgentRadius, 5f);
            float dz = i > 0 ? w.Z - smooth.Waypoints[i - 1].Z : 0;
            string flag = i == stepIdx ? " ← STEP-UP" : "";
            _output.WriteLine($"#   {i,3} | {w.X,8:F2},{w.Y,8:F2},{w.Z,6:F2} | 0x{p.PolyRef:X16} | {(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8} | {dz,+6:F2}y{flag}");
        }
    }

    // ============================================================== Diagnostic 6
    /// <summary>
    /// Cycle 17d hypothesis test: Snurk Bucksquick (Grom'gol zeppelin master)
    /// is on the SAME upper deck as Frezza, ~24y apart. User asserts the
    /// path Grunt1→Snurk should walk ALL the way up the spiral ramp without
    /// going through the deck-lip "shortcut" — i.e. Snurk's position forces
    /// the smooth path to use the full ramp. Frezza's path appears to take
    /// a lower-platform shortcut. Dumping both side-by-side reveals the
    /// extra waypoints Frezza is missing.
    /// </summary>
    [Fact]
    public void Compare_GruntToFrezza_vs_GruntToSnurk_SmoothPaths()
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        _output.WriteLine($"# Both NPCs are on the OG zeppelin tower upper deck at z≈53.6.");
        _output.WriteLine($"# Frezza (UC zep): ({Frezza.X:F2},{Frezza.Y:F2},{Frezza.Z:F2})");
        _output.WriteLine($"# Snurk  (Grom):   ({Snurk.X:F2},{Snurk.Y:F2},{Snurk.Z:F2})");
        _output.WriteLine($"# Frezza ↔ Snurk XY-distance: {Distance2D(Frezza, Snurk):F2}y");
        _output.WriteLine($"# Grunt1 (base):   ({Grunt1.X:F2},{Grunt1.Y:F2},{Grunt1.Z:F2})");
        _output.WriteLine("");

        DumpSmoothPathFull("Grunt1 → Snurk (Grom'gol zep, expected: full ramp climb + hook)", Grunt1, Snurk);
        DumpSmoothPathFull("Grunt1 → Frezza (UC zep, observed: shortcut through lip)", Grunt1, Frezza);

        // Also dump from a couple of upper-ramp sample points to each NPC,
        // to see if the lip-shortcut is taken from any approach direction or
        // only from below.
        var rampUpper = new XYZ(1346f, -4646f, 45f); // approximate spiral ramp upper turn
        DumpSmoothPathFull("rampUpper (z=45) → Snurk", rampUpper, Snurk);
        DumpSmoothPathFull("rampUpper (z=45) → Frezza", rampUpper, Frezza);

        // And a deck-only short hop for sanity (proves the deck cluster is
        // internally connected without a lip step).
        DumpSmoothPathFull("Snurk → Frezza (deck only, 24y)", Snurk, Frezza);
    }

    // ============================================================== Diagnostic 7
    /// <summary>
    /// Drill-down for
    /// LongPathingRouteTests.OrgrimmarZeppelinTowerUnderpassLiveStallExactRecovery.
    /// The managed route currently fails local physics on the lower spiral at
    /// segment 172→173. This diagnostic maps the stall points back to navmesh
    /// poly refs and surface Z without asserting, so cull/generation iterations
    /// can target the responsible mesh cells.
    /// </summary>
    [Fact]
    public void Audit_UnderpassLiveStall_Polys()
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        var underpassStart = new XYZ(1357.2f, -4516.2f, 32.0f);
        var boarding = new XYZ(1320.142944f, -4653.158691f, 53.891945f);
        var failFrom = new XYZ(1346.7f, -4647.5f, 26.0f);
        var failTo = new XYZ(1347.2f, -4650.7f, 27.5f);

        _output.WriteLine("# underpass native polygon chain:");
        DumpPolygonChain("underpass start → boarding", underpassStart, boarding);

        _output.WriteLine("");
        _output.WriteLine("# lower-spiral failing segment polygon chain:");
        DumpPolygonChain("segment 172 → 173", failFrom, failTo);

        _output.WriteLine("");
        _output.WriteLine("# coordinate probes:");
        var probes = new (string label, XYZ xyz, float xyExtent, float zExtent)[]
        {
            ("wall-hit-a", new XYZ(1354.0300f, -4523.0664f, 33.3155f), AgentRadius, 5f),
            ("out-zero-low", new XYZ(1346.1189f, -4524.1284f, 31.5713f), AgentRadius, 5f),
            ("out-zero-high", new XYZ(1346.1189f, -4524.1284f, 32.4846f), AgentRadius, 5f),
            ("fail-from", failFrom, AgentRadius, 5f),
            ("fail-to", failTo, AgentRadius, 5f),
            ("fail-from-wide", failFrom, 3.0f, 12f),
            ("fail-to-wide", failTo, 3.0f, 12f),
        };

        _output.WriteLine("# label          | coord X,Y,Z                  | polyRef            | type     | nearest X,Y,Z              | surfaceZ | dz");
        _output.WriteLine("# ---------------+------------------------------+--------------------+----------+----------------------------+----------+------");
        foreach (var (label, xyz, xyExtent, zExtent) in probes)
        {
            var p = NavigationInterop.QueryPolyAtCoord(MapId, xyz, xyExtent, zExtent);
            var dz = p.HasSurface ? xyz.Z - p.SurfaceZ : float.NaN;
            _output.WriteLine(
                $"# {label,-14} | {xyz.X,8:F2},{xyz.Y,8:F2},{xyz.Z,6:F2} | 0x{p.PolyRef:X16} | {p.PolyType,-8} | "
                + $"{p.NearestPoint.X,8:F2},{p.NearestPoint.Y,8:F2},{p.NearestPoint.Z,6:F2} | "
                + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "N/A"),8} | "
                + $"{(float.IsNaN(dz) ? "N/A" : dz.ToString("+0.00;-0.00")),5}");
        }
    }

    private void DumpSmoothPathFull(string label, XYZ start, XYZ end)
    {
        _output.WriteLine($"## {label}");
        _output.WriteLine($"#   start = ({start.X:F2},{start.Y:F2},{start.Z:F2})");
        _output.WriteLine($"#   end   = ({end.X:F2},{end.Y:F2},{end.Z:F2})");
        var path = NavigationInterop.QuerySmoothPath(MapId, start, end, AgentRadius, AgentHeight);
        if (!path.Success)
        {
            _output.WriteLine($"#   QuerySmoothPath FAILED");
            _output.WriteLine("");
            return;
        }
        _output.WriteLine($"#   waypoints: {path.Length}");

        // Find the largest +Z step
        int maxStepIdx = 0; float maxStepDz = 0f;
        for (int i = 1; i < path.Length; i++)
        {
            float dz = path.Waypoints[i].Z - path.Waypoints[i - 1].Z;
            if (dz > maxStepDz) { maxStepDz = dz; maxStepIdx = i; }
        }
        _output.WriteLine($"#   largest +Z step: WP {maxStepIdx - 1} → {maxStepIdx}, dz={maxStepDz:F2}y");

        // Find max Z reached
        float maxZ = float.MinValue;
        int maxZIdx = -1;
        for (int i = 0; i < path.Length; i++)
            if (path.Waypoints[i].Z > maxZ) { maxZ = path.Waypoints[i].Z; maxZIdx = i; }
        _output.WriteLine($"#   max Z reached: WP {maxZIdx} z={maxZ:F2}");

        // Print every Nth waypoint so the trajectory is visible
        int step = Math.Max(1, path.Length / 25);
        _output.WriteLine($"#   trajectory (every {step}th + last):");
        for (int i = 0; i < path.Length; i += step)
        {
            var w = path.Waypoints[i];
            float dzPrev = i > 0 ? w.Z - path.Waypoints[i - 1].Z : 0f;
            _output.WriteLine($"#     {i,3} | ({w.X,8:F2},{w.Y,8:F2},{w.Z,6:F2}) | dz={dzPrev,+6:F2}");
        }
        if (path.Length > 0)
        {
            int last = path.Length - 1;
            var lw = path.Waypoints[last];
            _output.WriteLine($"#     {last,3} | ({lw.X,8:F2},{lw.Y,8:F2},{lw.Z,6:F2}) ← final");
        }

        // Final-waypoint sanity
        var lastWp = path.Waypoints[path.Length - 1];
        _output.WriteLine($"#   final XY-dist to end={Distance2D(lastWp, end):F2}y, dz={lastWp.Z - end.Z:F2}");
        _output.WriteLine("");
    }

    // ============================================================== helpers

    private void DumpSmoothPath(string label, XYZ start, XYZ end, int headCount = 5, int tailCount = 15)
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        _output.WriteLine($"# Route: {label}");
        _output.WriteLine($"#   start = ({start.X:F2},{start.Y:F2},{start.Z:F2})");
        _output.WriteLine($"#   end   = ({end.X:F2},{end.Y:F2},{end.Z:F2})");

        var path = NavigationInterop.QuerySmoothPath(MapId, start, end, AgentRadius, AgentHeight);
        Assert.True(path.Success, "QuerySmoothPath returned no waypoints.");
        _output.WriteLine($"#   smooth waypoints returned: {path.Length}");
        _output.WriteLine("");

        var polys = new NavigationInterop.PolyAtCoordResult[path.Waypoints.Length];
        for (int i = 0; i < path.Waypoints.Length; i++)
            polys[i] = NavigationInterop.QueryPolyAtCoord(MapId, path.Waypoints[i], AgentRadius, WalkableClimb);

        _output.WriteLine("#   idx |       waypoint X,Y,Z       | polyRef          | type     | surfaceZ |  dz   | result");
        _output.WriteLine("#   ----+----------------------------+------------------+----------+----------+-------+-------");
        int n = path.Waypoints.Length;
        int headEnd = Math.Min(headCount, n);
        int tailStart = Math.Max(headEnd, n - tailCount);
        for (int i = 0; i < headEnd; i++) EmitRow(i, path.Waypoints[i], polys[i]);
        if (tailStart > headEnd)
        {
            _output.WriteLine($"#   ... ({tailStart - headEnd} waypoints elided) ...");
            for (int i = tailStart; i < n; i++) EmitRow(i, path.Waypoints[i], polys[i]);
        }
        _output.WriteLine("");
        _output.WriteLine($"# Final waypoint Z={path.Waypoints[n - 1].Z:F2}; route end Z={end.Z:F2}; XY-distance to end={Distance2D(path.Waypoints[n - 1], end):F2}y");
    }

    private void DumpPolygonChain(string label, XYZ start, XYZ end)
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        _output.WriteLine($"## {label}");
        _output.WriteLine($"#   start = ({start.X:F2},{start.Y:F2},{start.Z:F2})");
        _output.WriteLine($"#   end   = ({end.X:F2},{end.Y:F2},{end.Z:F2})");

        var probe = NavigationInterop.QueryPathPolygons(MapId, start, end, AgentRadius, AgentHeight, maxOut: 4096);
        if (!probe.Success)
        {
            _output.WriteLine("#   QueryPathPolygons returned FALSE.");
            return;
        }
        int written = probe.PolyRefs.Length;
        _output.WriteLine($"#   TotalPolyCount: {probe.TotalPolyCount}  written: {written}  off-mesh count: {probe.OffMeshPolyCount}");
        var prefixes = new HashSet<ulong>();
        foreach (var p in probe.PolyRefs) prefixes.Add((p >> 20) & 0xFFFFFFF);
        _output.WriteLine($"#   distinct tile prefixes in chain: {prefixes.Count}");
        var endProbe = NavigationInterop.QueryPolyAtCoord(MapId, end, AgentRadius, 300f);
        bool endsAtTarget = written > 0 && endProbe.HasPoly && endProbe.PolyRef == probe.PolyRefs[written - 1];
        _output.WriteLine($"#   endsAtTarget? {endsAtTarget}");
        _output.WriteLine("");
        if (written > 0 && written <= 32)
        {
            _output.WriteLine("#   Full chain:");
            for (int i = 0; i < written; i++)
            {
                ulong tile = (probe.PolyRefs[i] >> 20) & 0xFFFFFFF;
                _output.WriteLine($"#     {i,3} | 0x{probe.PolyRefs[i]:X16} | {probe.PolyTypes[i],-8} | tile=0x{tile:X7}");
            }
        }
        else if (written > 0)
        {
            _output.WriteLine($"#   First 5 + last 10 polys (of {written}):");
            for (int i = 0; i < Math.Min(5, written); i++) EmitChainRow(i, probe);
            _output.WriteLine($"#     ...");
            for (int i = Math.Max(5, written - 10); i < written; i++) EmitChainRow(i, probe);
        }
    }

    private void EmitChainRow(int i, NavigationInterop.PolygonPathResult probe)
    {
        ulong tile = (probe.PolyRefs[i] >> 20) & 0xFFFFFFF;
        _output.WriteLine($"#     {i,3} | 0x{probe.PolyRefs[i]:X16} | {probe.PolyTypes[i],-8} | tile=0x{tile:X7}");
    }

    private void EmitRow(int idx, XYZ w, NavigationInterop.PolyAtCoordResult p)
    {
        var dz = p.HasSurface ? Math.Abs(w.Z - p.SurfaceZ) : float.NaN;
        string status =
            !p.Success ? "INFRA-FAIL" :
            !p.HasPoly ? "NO-POLY" :
            p.PolyType == NavigationInterop.PolyType.OffMeshConnection ? "OFFMESH" :
            !p.HasSurface ? "NO-SURFACE" :
            (dz <= WalkableClimb ? "OK" : "OFF-SURFACE");
        _output.WriteLine(
            $"#   {idx,3} | {w.X,8:F2},{w.Y,8:F2},{w.Z,6:F2} | 0x{p.PolyRef:X14} | {p.PolyType,-8} | "
            + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8} | "
            + $"{(float.IsNaN(dz) ? "  N/A " : dz.ToString("F2")),5} | {status}");
    }

    private static float Distance2D(XYZ a, XYZ b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
