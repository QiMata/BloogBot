using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// PFS-OVERHAUL-006 follow-up — diagnostic for the
/// BotRunner.Tests.LiveValidation.LongPathingTests.FlameCrestToBrmDungeonEntrance
/// theory. Resolves each (start, end) endpoint pair to a polygon, attempts a
/// polygon-corridor query, and prints what's missing. Lets us localize bake
/// fragmentation in the BRM-area tiles before paying for a 6-minute live test.
/// </summary>
[Trait("Category", "Unit")]
public class BrmDungeonRouteDiagnostic : IClassFixture<PathfindingValidationFixture>
{
    private readonly PathfindingValidationFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const uint MapId = 0;            // Eastern Kingdoms
    private const float AgentRadius = 1.0247f;
    private const float AgentHeight = 2.625f;
    private const float WalkableClimb = 1.8f;

    // Flame Crest start — ground level near the FM tower foot (the literal
    // pad at z=165 caused the bot to fall and stall in live runs).
    private static readonly XYZ FlameCrest = new(-7518.7f, -2159.9f, 131.9f);
    // LBRS/UBRS use literal portal coords (mesh-reachable). BRD/BWL use the
    // bot-reachable APPROACH positions because the literal portal coords sit
    // on isolated polygons disconnected from the BRM exterior corridor —
    // a bake hole in BRM tiles (45,33) and (46,34). See LongPathingTests.cs
    // for the rationale.
    private static readonly XYZ BrdEntrance = new(-7187f, -958f, 254f); // approach
    private static readonly XYZ LbrsEntrance = new(-7531f, -1226f, 286f); // literal
    private static readonly XYZ UbrsEntrance = new(-7524f, -1233f, 287f); // literal
    private static readonly XYZ BwlEntrance = new(-7659f, -1214f, 291f); // approach

    public BrmDungeonRouteDiagnostic(PathfindingValidationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Audit_BrmDungeonEndpoints_ResolveAndCorridor()
    {
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");

        // Wide search extents — we want to know IF a poly exists nearby, not
        // require the literal point be on one. searchExtentZ=300y covers the
        // BWL portal at z=400 even if we feed coarse manifest values.
        var endpoints = new (string Name, XYZ Pos)[]
        {
            ("FlameCrest",  FlameCrest),
            ("BRD",         BrdEntrance),
            ("LBRS",        LbrsEntrance),
            ("UBRS",        UbrsEntrance),
            ("BWL",         BwlEntrance),
        };

        _output.WriteLine("# === ENDPOINT POLY RESOLUTION (coarse: searchXY=AgentRadius, searchZ=10y) ===");
        _output.WriteLine("#   name        | x,y,z                          | polyRef             | type     | surfaceZ | dz");
        _output.WriteLine("#   ------------+--------------------------------+---------------------+----------+----------+-----");
        foreach (var (name, pos) in endpoints)
        {
            var p = NavigationInterop.QueryPolyAtCoord(MapId, pos, AgentRadius, 10f);
            float dz = p.HasSurface ? Math.Abs(pos.Z - p.SurfaceZ) : float.NaN;
            _output.WriteLine(
                $"#   {name,-12}| {pos.X,8:F1},{pos.Y,8:F1},{pos.Z,6:F1} | 0x{p.PolyRef:X16} | {p.PolyType,-8} | "
                + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8} | "
                + $"{(float.IsNaN(dz) ? "  N/A " : dz.ToString("F2")),5}");
        }
        _output.WriteLine("");

        _output.WriteLine("# === ENDPOINT POLY RESOLUTION (wide: searchXY=10y, searchZ=300y) ===");
        _output.WriteLine("#   name        | x,y,z                          | polyRef             | type     | surfaceZ | dz");
        _output.WriteLine("#   ------------+--------------------------------+---------------------+----------+----------+-----");
        foreach (var (name, pos) in endpoints)
        {
            var p = NavigationInterop.QueryPolyAtCoord(MapId, pos, 10f, 300f);
            float dz = p.HasSurface ? Math.Abs(pos.Z - p.SurfaceZ) : float.NaN;
            _output.WriteLine(
                $"#   {name,-12}| {pos.X,8:F1},{pos.Y,8:F1},{pos.Z,6:F1} | 0x{p.PolyRef:X16} | {p.PolyType,-8} | "
                + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8} | "
                + $"{(float.IsNaN(dz) ? "  N/A " : dz.ToString("F2")),5}");
        }
        _output.WriteLine("");

        _output.WriteLine("# === FLAME CREST → DUNGEON CORRIDOR (FindPathPolygonsForAgent) ===");
        var routes = new (string Label, XYZ A, XYZ B)[]
        {
            ("Flame Crest → BRD",  FlameCrest, BrdEntrance),
            ("Flame Crest → LBRS", FlameCrest, LbrsEntrance),
            ("Flame Crest → UBRS", FlameCrest, UbrsEntrance),
            ("Flame Crest → BWL",  FlameCrest, BwlEntrance),
        };
        _output.WriteLine("#   route                              | polys | endsAtTarget? | first poly          | last poly");
        _output.WriteLine("#   -----------------------------------+-------+---------------+---------------------+---------------------");
        foreach (var (label, a, b) in routes)
        {
            var chain = NavigationInterop.QueryPathPolygons(MapId, a, b, AgentRadius, AgentHeight, maxOut: 4096);
            int n = chain.PolyRefs.Length;
            var endProbe = NavigationInterop.QueryPolyAtCoord(MapId, b, 10f, 300f);
            bool endsAtTarget = n > 0 && endProbe.HasPoly && endProbe.PolyRef == chain.PolyRefs[n - 1];
            string first = n > 0 ? $"0x{chain.PolyRefs[0]:X16}" : "-";
            string last = n > 0 ? $"0x{chain.PolyRefs[n - 1]:X16}" : "-";
            _output.WriteLine($"#   {label,-35}|  {n,4} | {(endsAtTarget ? "YES" : "NO "),12} | {first} | {last}");
        }
        _output.WriteLine("");

        _output.WriteLine("# === FLAME CREST → DUNGEON SMOOTH PATH (QuerySmoothPath, the live-bot API) ===");
        _output.WriteLine("#   route                              | wps  | startWP                    | endWP                      | dist2DToTarget | dz");
        _output.WriteLine("#   -----------------------------------+------+----------------------------+----------------------------+----------------+-------");
        foreach (var (label, a, b) in routes)
        {
            var sp = NavigationInterop.QuerySmoothPath(MapId, a, b, AgentRadius, AgentHeight);
            if (!sp.Success || sp.Length == 0)
            {
                _output.WriteLine($"#   {label,-35}|  -- | (smooth path FAILED)");
                continue;
            }
            var first = sp.Waypoints[0];
            var last = sp.Waypoints[sp.Length - 1];
            float dx = last.X - b.X;
            float dy = last.Y - b.Y;
            float d2 = MathF.Sqrt(dx * dx + dy * dy);
            float dz = last.Z - b.Z;
            _output.WriteLine(
                $"#   {label,-35}| {sp.Length,4} | "
                + $"{first.X,8:F1},{first.Y,8:F1},{first.Z,6:F1} | "
                + $"{last.X,8:F1},{last.Y,8:F1},{last.Z,6:F1} | "
                + $"{d2,12:F2}y  | {dz,+5:F2}");
        }
        _output.WriteLine("");

        // For partial paths (BRD, BWL), find where the corridor terminates
        // vs where the target lives. Reveals the disconnect that needs a
        // bake-side fix.
        _output.WriteLine("# === PARTIAL-PATH DISCONNECT ANALYSIS ===");
        var partialRoutes = new (string Label, XYZ A, XYZ B)[]
        {
            ("BRD",  FlameCrest, BrdEntrance),
            ("BWL",  FlameCrest, BwlEntrance),
        };
        _output.WriteLine("#   route | last-corridor-poly         | last-corridor-tile-key | target-poly                  | target-tile-key | dz(last->target) ");
        _output.WriteLine("#   ------+----------------------------+------------------------+------------------------------+-----------------+------------------");
        foreach (var (label, a, b) in partialRoutes)
        {
            var chain = NavigationInterop.QueryPathPolygons(MapId, a, b, AgentRadius, AgentHeight, maxOut: 4096);
            if (chain.PolyRefs.Length == 0)
            {
                _output.WriteLine($"#   {label} | (no chain)");
                continue;
            }
            var endProbe = NavigationInterop.QueryPolyAtCoord(MapId, b, 10f, 300f);
            ulong lastPolyRef = chain.PolyRefs[chain.PolyRefs.Length - 1];
            ulong targetPolyRef = endProbe.PolyRef;

            // dtPolyRef64 layout: salt(16) | tile(28) | poly(20). Extract tile.
            ulong lastTile = (lastPolyRef >> 20) & 0x0FFFFFFFul;
            ulong targetTile = (targetPolyRef >> 20) & 0x0FFFFFFFul;

            // Surface Z gap between chain terminus and target poly
            var smooth = NavigationInterop.QuerySmoothPath(MapId, a, b, AgentRadius, AgentHeight);
            float lastZ = smooth.Length > 0 ? smooth.Waypoints[smooth.Length - 1].Z : float.NaN;
            float dz = endProbe.HasSurface ? endProbe.SurfaceZ - lastZ : float.NaN;

            _output.WriteLine(
                $"#   {label,-5} | 0x{lastPolyRef:X16}        | 0x{lastTile:X7}              | "
                + $"0x{targetPolyRef:X16}          | 0x{targetTile:X7}       | {dz,+6:F2}y");
        }
        _output.WriteLine("");

        // Vertical-scan probe at the BRD and BWL portal XY: enumerate
        // walkable polys at every Z step. If we see polys at intermediate
        // heights between chain terminus and target, those COULD be
        // connected with a bake parameter change. If only target-Z and
        // terminus-Z have polys (no intermediates), the bake is missing
        // the cave/ramp altogether and a parameter tweak won't help.
        _output.WriteLine("# === VERTICAL POLY SCAN AT BRD AND BWL PORTAL XY ===");
        var verticalScans = new (string Label, XYZ Pos, float ZMin, float ZMax)[]
        {
            ("BRD portal XY (-7179,-921), z 50→300",  BrdEntrance, 50f,  300f),
            ("BWL portal XY (-7665,-1102), z 50→450", BwlEntrance, 50f,  450f),
        };
        foreach (var (label, pos, zMin, zMax) in verticalScans)
        {
            _output.WriteLine($"#   {label}");
            _output.WriteLine($"#     z   | polyRef             | type     | surfaceZ");
            _output.WriteLine($"#    -----+---------------------+----------+---------");
            for (float z = zMin; z <= zMax; z += 10f)
            {
                var probePos = new XYZ(pos.X, pos.Y, z);
                var p = NavigationInterop.QueryPolyAtCoord(MapId, probePos, AgentRadius, 5f);
                if (p.HasPoly)
                {
                    _output.WriteLine(
                        $"#    {z,4:F0} | 0x{p.PolyRef:X16} | {p.PolyType,-8} | "
                        + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8}");
                }
            }
            _output.WriteLine("");
        }

        // Control: short hops near Flame Crest. If THESE fail, it's a map-0
        // smooth-path systemic issue. If they succeed, it's a long-route
        // specific failure.
        _output.WriteLine("# === MAP 0 SHORT-ROUTE CONTROL (small hops near Flame Crest) ===");
        var controls = new (string Label, XYZ A, XYZ B)[]
        {
            ("FC + 5y north",   FlameCrest, new XYZ(FlameCrest.X, FlameCrest.Y + 5f, FlameCrest.Z)),
            ("FC + 30y north",  FlameCrest, new XYZ(FlameCrest.X, FlameCrest.Y + 30f, FlameCrest.Z)),
            ("FC + 100y north", FlameCrest, new XYZ(FlameCrest.X, FlameCrest.Y + 100f, FlameCrest.Z)),
            ("FC + 300y north", FlameCrest, new XYZ(FlameCrest.X, FlameCrest.Y + 300f, FlameCrest.Z)),
        };
        _output.WriteLine("#   route                              | wps  | dist2DToTarget");
        _output.WriteLine("#   -----------------------------------+------+----------------");
        foreach (var (label, a, b) in controls)
        {
            var sp = NavigationInterop.QuerySmoothPath(MapId, a, b, AgentRadius, AgentHeight);
            if (!sp.Success || sp.Length == 0)
            {
                _output.WriteLine($"#   {label,-35}|  -- | (smooth path FAILED)");
                continue;
            }
            var last = sp.Waypoints[sp.Length - 1];
            float d2 = MathF.Sqrt(MathF.Pow(last.X - b.X, 2) + MathF.Pow(last.Y - b.Y, 2));
            _output.WriteLine($"#   {label,-35}| {sp.Length,4} | {d2,12:F2}y");
        }
    }

    /// <summary>
    /// PFS-OVERHAUL-006 Round 2 (2026-05-09): UBRS bot stalls at
    /// `(-7949.7,-1162.8,170.8)` with `flags=0x2001` (FORWARD|JUMPING),
    /// `moved=0.2y`. The cliff classifier was relaxed in Round 1 so the
    /// route is now `supported:Drop`, but the bot still can't physically
    /// traverse it. This diagnostic dumps every smooth-path waypoint
    /// within ±25y of the stall anchor (in the X,Y plane) and computes
    /// per-segment dz, slope, and surface-Z probe so we can localize
    /// which step the runtime physics rejects.
    ///
    /// Goal: identify whether the offending segment is
    ///   (a) a single dz > walkableClimb step the bake should have
    ///       fragmented but didn't,
    ///   (b) a ledge edge where the bot's capsule clips into a wall,
    ///   (c) a ceiling overhang that the navmesh ignored.
    /// Each case has a different fix surface.
    /// </summary>
    [Fact]
    public void Dump_UbrsRoute_StallRegionWaypoints()
    {
        DumpStallRegion(stallX: -7949.7f, stallY: -1162.8f, stallZ: 170.8f,
                        label: "UBRS live test (BRM south-face stall, 2026-05-09)");
    }

    /// <summary>
    /// 2026-05-13 follow-up: the live BotRunner FlameCrestToBrmDungeonEntrance
    /// UBRS case now stalls early, at `(-7519.0,-2100.4,130.3)` on tile (35,46) —
    /// only ~60y north of the Flame Crest start, well before the historical
    /// (-7949.7,-1162.8,170.8) BRM south-face site. Screenshot evidence reads as
    /// wall/ceiling collision. The Flame Crest stall mmap crop reports a
    /// suspicious zRange=19.60y AREA_STEEP_SLOPE polygon (`area=3, flags=17`)
    /// at centroid (-7479.3,-2116.4,147.1). This dump captures the smooth-path
    /// behavior around the live stall so we can localize whether the bot is
    /// being walked into one of those steep-slope walls or whether the failure
    /// is unrelated to the corridor (true wall/ceiling collision against
    /// unbaked WMO).
    /// </summary>
    [Fact]
    public void Dump_UbrsRoute_FlameCrestStallWaypoints()
    {
        DumpStallRegion(stallX: -7519.0f, stallY: -2100.4f, stallZ: 130.3f,
                        label: "UBRS live test (Flame Crest early stall, 2026-05-13)");
    }

    /// <summary>
    /// 2026-05-13 runtime-filter regression for the BRD/BRM Flame Crest stall.
    ///
    /// Player paths must not include polygons flagged NAV_STEEP_SLOPES (slopes
    /// above the 52° player climb limit). The vmangos Map.cpp pattern is to
    /// pair includeFlags with `setExcludeFlags(NAV_STEEP_SLOPES)` so Detour
    /// will not string-pull smooth paths across rock-face polygons that FG
    /// physics rejects. Both bake-side tightening attempts on 2026-05-13
    /// regressed live FG (object exclusion / wall-collision creep, commits
    /// fd085d68 + fd943e57); the chosen fix surface is the runtime filter in
    /// `Exports/Navigation/PathFinder.cpp::createFilter` plus the player-path
    /// `setExcludeFlags(NAV_STEEP_SLOPES)` calls in
    /// `Exports/Navigation/DllMain.cpp::Find{PathCorridor,PathPolygonsForAgent,PathCornersForAgent}`.
    ///
    /// This regression goes red when any polygon in the Flame Crest → UBRS
    /// corridor (the result of `FindPathPolygonsForAgent`, which Detour
    /// produces via `findPath` under the player-path filter) has
    /// NAV_STEEP_SLOPES set. Checking the corridor directly is more correct
    /// than checking smooth-path waypoint positions: the smooth path
    /// interpolates positions ALONG the corridor, and a waypoint's nearest
    /// poly within 1y XY / 1.8y Z can still be a vertically-adjacent
    /// steep-slope poly even when the actual corridor avoids it. The corridor
    /// is exactly the set of polys the filter accepted, so it is the
    /// authoritative gate.
    ///
    /// Important: this test does NOT assert the corridor reaches the UBRS
    /// portal. The BRM south-face ascent has multi-tile bake-fidelity gaps
    /// where the only physically-implausible "corridor" went THROUGH the
    /// steep-slope polys this filter now correctly excludes. Reaching UBRS
    /// requires a separate fix surface (off-mesh connection in
    /// tools/MmapGen/offmesh.txt for the BRM ascent, or a per-tile
    /// maxSteepSlopePolyZRange bake-side knob). The runtime filter's job is
    /// solely to stop the bot from being walked into a rock face; route
    /// completeness is a downstream concern tracked separately in TASKS.md.
    ///
    /// REVERTED (2026-05-13, third attempt): the runtime NAV_STEEP_SLOPES
    /// exclude blew up Detour A* search to 170-306 seconds per FlameCrest
    /// -> UBRS findpath query (Docker pathfinding log NAV_METRICS
    /// avgNativeFindMs=170294, maxNativeFindMs=306810, smooth-path corner
    /// count 1105). The bot never received a usable path before the legacy
    /// stall guard fired (45s anchor timeout). The runtime filter is the
    /// wrong surface for the BRM mesh given its current bake density of
    /// AREA_STEEP_SLOPE polys.
    ///
    /// REVERTED (2026-05-13, FOURTH attempt — Surface 4 off-mesh BRM ascent):
    /// three off-mesh entries (FC stall → LBRS/UBRS/BWL) authored in
    /// tools/MmapGen/offmesh.txt, baked, and promoted. Bench was strong:
    /// the FC→UBRS corridor dropped 316→18 polys (94% reduction), all four
    /// routes ends-at-target=YES with smooth-path dist2D=0.00y to each
    /// portal, and A* picked the off-mesh link over the cliff chain.
    /// Live FG regressed 4/4: bot teleported to Flame Crest, dispatched
    /// TravelTo, never moved (currentSpeed=0 movementFlags=0 for 360s).
    /// Root cause: Docker pathfinding log
    /// `[PATH_REQ] id=3 still-running elapsed>=25s` — the live managed
    /// pipeline (Services/PathfindingService/Repository/Navigation.cs,
    /// 5,600 LOC, 8 repair phases, 35s budget) cannot handle off-mesh
    /// polys in the corridor and hangs / runs out of budget. The Detour
    /// layer is correct; the managed repair layer is not off-mesh-aware,
    /// and the PATHFINDING_OVERHAUL freeze contract forbids extending
    /// Navigation.cs repair phases until Phase 5 cutover. Reverted.
    ///
    /// Re-enable when EITHER (a) Phase 4 of the overhaul ships
    /// off-mesh-aware managed path consumption (likely combined with
    /// off-mesh BRM ascent entries) OR (b) a per-tile
    /// maxSteepSlopePolyZRange bake-side knob fragments the tallest
    /// steep-slope polys so the runtime filter approach (Surface 3 in the
    /// docs) becomes viable without exploding A* search.
    /// </summary>
    [Fact(Skip = "REVERTED 2026-05-13: four attempts (two bake-side slope "
        + "tightenings, one runtime NAV_STEEP_SLOPES exclude, one off-mesh "
        + "BRM ascent in tools/MmapGen/offmesh.txt) have all regressed live "
        + "FG. The off-mesh attempt was bench-green (corridor 316→18 polys, "
        + "94% reduction, smooth-path dist2D=0.00y to every portal) but live "
        + "managed repair pipeline (Navigation.cs) hangs on off-mesh "
        + "corridors (PATH_REQ elapsed>=25s, bot never moved). Re-enable when "
        + "Phase 4 of PATHFINDING_OVERHAUL ships off-mesh-aware managed path "
        + "consumption OR a per-tile maxSteepSlopePolyZRange bake-side knob "
        + "fragments the tallest steep-slope polys.")]
    public void FlameCrestToUbrsCorridor_AvoidsSteepSlopePolys()
    {
        var chain = NavigationInterop.QueryPathPolygons(MapId, FlameCrest, UbrsEntrance, AgentRadius, AgentHeight, maxOut: 4096);
        Assert.True(chain.Success && chain.PolyRefs.Length > 0,
            "Expected a non-empty corridor Flame Crest -> UBRS (partial OK). Filter exclude must not "
            + "produce no-path; if it does, the start poly is itself NAV_STEEP_SLOPES and the bake needs widening.");

        // Walk every corridor poly and verify NAV_STEEP_SLOPES bit is NOT set.
        // Skip off-mesh connection polys (their flags encode link semantics).
        var steepOffenders = new List<(int Idx, ulong PolyRef, ushort Flags, byte Area)>();
        int probedCount = 0;
        for (int i = 0; i < chain.PolyRefs.Length; i++)
        {
            if (chain.PolyTypes[i] == NavigationInterop.PolyType.OffMeshConnection)
                continue;

            var flags = NavigationInterop.QueryPolyFlags(MapId, chain.PolyRefs[i]);
            if (!flags.Success)
                continue;

            probedCount++;
            if (flags.HasSteepSlopes)
                steepOffenders.Add((i, chain.PolyRefs[i], flags.Flags, flags.Area));
        }

        Assert.True(probedCount > 0,
            "Expected at least one ground-polygon in the corridor to resolve flags via "
            + "QueryPolyFlags; none did. Investigate the GetPolyFlagsForRef export.");

        Assert.True(steepOffenders.Count == 0,
            $"Corridor Flame Crest -> UBRS includes {steepOffenders.Count} NAV_STEEP_SLOPES polygon(s). "
            + "The runtime path filter (PathFinder::createFilter + DllMain player-path filters) must "
            + "exclude NAV_STEEP_SLOPES (flag 0x10) so Detour cannot route across area=3/AREA_STEEP_SLOPE polys. "
            + $"Corridor length: {chain.PolyRefs.Length} polys, {probedCount} ground polys probed. "
            + "First 8 offenders:" + Environment.NewLine
            + string.Join(Environment.NewLine, steepOffenders.Take(8).Select(o =>
                $"  corridorIdx={o.Idx} polyRef=0x{o.PolyRef:X16} flags=0x{o.Flags:X4} area={o.Area}")));
    }

    private void DumpStallRegion(float stallX, float stallY, float stallZ, string label)
    {
        float StallAnchorX = stallX;
        float StallAnchorY = stallY;
        float StallAnchorZ = stallZ;
        const float StallSearchRadius2D = 25f;
        const float WalkableClimbY = 1.8f;
        _output.WriteLine($"# Variant: {label}");

        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
        _output.WriteLine($"# Stall anchor (UBRS live test): ({StallAnchorX:F1},{StallAnchorY:F1},{StallAnchorZ:F1})");
        _output.WriteLine($"# Search radius (2D): {StallSearchRadius2D:F1}y");
        _output.WriteLine("");

        var sp = NavigationInterop.QuerySmoothPath(MapId, FlameCrest, UbrsEntrance, AgentRadius, AgentHeight);
        Assert.True(sp.Success, "QuerySmoothPath FlameCrest -> UBRS returned no waypoints.");
        _output.WriteLine($"# UBRS smooth-path waypoint count: {sp.Length}");

        // Locate index range whose XY falls inside the stall search radius.
        int firstIdx = -1, lastIdx = -1;
        for (int i = 0; i < sp.Length; i++)
        {
            float dx = sp.Waypoints[i].X - StallAnchorX;
            float dy = sp.Waypoints[i].Y - StallAnchorY;
            float d2 = MathF.Sqrt(dx * dx + dy * dy);
            if (d2 <= StallSearchRadius2D)
            {
                if (firstIdx < 0) firstIdx = i;
                lastIdx = i;
            }
        }

        if (firstIdx < 0)
        {
            _output.WriteLine("# NO waypoints fall within the stall search radius — the smooth path may not actually traverse the stall site.");
            // Print the closest waypoint to the stall anchor anyway.
            int bestIdx = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < sp.Length; i++)
            {
                float dx = sp.Waypoints[i].X - StallAnchorX;
                float dy = sp.Waypoints[i].Y - StallAnchorY;
                float d2 = MathF.Sqrt(dx * dx + dy * dy);
                if (d2 < bestD) { bestD = d2; bestIdx = i; }
            }
            var w = sp.Waypoints[bestIdx];
            _output.WriteLine($"# Closest waypoint: idx={bestIdx} ({w.X:F2},{w.Y:F2},{w.Z:F2}) — XY-dist to stall anchor = {bestD:F2}y");
            firstIdx = Math.Max(0, bestIdx - 10);
            lastIdx = Math.Min(sp.Length - 1, bestIdx + 10);
            _output.WriteLine($"# Expanding dump to idx [{firstIdx}..{lastIdx}] for context.");
            _output.WriteLine("");
        }
        else
        {
            // Pad +/- 5 waypoints either side for context.
            firstIdx = Math.Max(0, firstIdx - 5);
            lastIdx = Math.Min(sp.Length - 1, lastIdx + 5);
            _output.WriteLine($"# Waypoints in range: idx [{firstIdx}..{lastIdx}] (incl. ±5 context)");
            _output.WriteLine("");
        }

        _output.WriteLine("# idx |   waypoint X,Y,Z         | polyRef            | type     | surfaceZ | dz(prev) | dist2D | slopeDeg | flag");
        _output.WriteLine("# ----+--------------------------+--------------------+----------+----------+----------+--------+----------+-----");

        for (int i = firstIdx; i <= lastIdx; i++)
        {
            var w = sp.Waypoints[i];
            var probe = NavigationInterop.QueryPolyAtCoord(MapId, w, AgentRadius, WalkableClimbY);

            float dz = 0f, dist2D = 0f, slopeDeg = 0f;
            string flag = "";
            if (i > 0)
            {
                var prev = sp.Waypoints[i - 1];
                dz = w.Z - prev.Z;
                float dx = w.X - prev.X;
                float dy = w.Y - prev.Y;
                dist2D = MathF.Sqrt(dx * dx + dy * dy);
                slopeDeg = dist2D > 0.01f
                    ? MathF.Atan2(MathF.Abs(dz), dist2D) * (180f / MathF.PI)
                    : (MathF.Abs(dz) > 0.5f ? 90f : 0f);

                // Highlight conditions of interest:
                //  STEP   = dz > walkableClimb (bot would need to jump-up; bake holds polys connected anyway)
                //  GAP2D  = consecutive waypoints > 4y apart in 2D (string-pulled, may hide ledges)
                //  -DROP  = dz < -2y (Drop classification by relaxed cliff rule)
                //  -CLIFF = dz < -6y AND slope > 25° (still classifies as Cliff post-Round-1)
                if (dz > WalkableClimbY) flag = "STEP";
                else if (dist2D > 4f) flag = "GAP2D";
                else if (dz < -6f && slopeDeg > 25f) flag = "-CLIFF";
                else if (dz < -2f) flag = "-DROP";
            }

            string polyStr = probe.HasPoly ? $"0x{probe.PolyRef:X16}" : "(no poly)        ";
            string surfStr = probe.HasSurface ? probe.SurfaceZ.ToString("F2") : "  N/A  ";
            _output.WriteLine($"# {i,3} | ({w.X,8:F2},{w.Y,8:F2},{w.Z,6:F2}) | {polyStr} | {probe.PolyType,-8} | {surfStr,8} | {dz,+8:F2} | {dist2D,6:F2} | {slopeDeg,8:F2} | {flag,-6}");
        }

        _output.WriteLine("");

        // Histogram across the WHOLE UBRS path (not just the stall region) so
        // we can compare local pathology vs global path shape.
        int stepCount = 0, gapCount = 0, dropCount = 0, cliffCount = 0;
        float maxStep = 0f, maxGap = 0f, mostNegativeDz = 0f;
        for (int i = 1; i < sp.Length; i++)
        {
            var prev = sp.Waypoints[i - 1];
            var w = sp.Waypoints[i];
            float dz = w.Z - prev.Z;
            float dx = w.X - prev.X;
            float dy = w.Y - prev.Y;
            float d2 = MathF.Sqrt(dx * dx + dy * dy);
            float slope = d2 > 0.01f
                ? MathF.Atan2(MathF.Abs(dz), d2) * (180f / MathF.PI)
                : (MathF.Abs(dz) > 0.5f ? 90f : 0f);

            if (dz > WalkableClimbY) { stepCount++; if (dz > maxStep) maxStep = dz; }
            if (d2 > 4f) { gapCount++; if (d2 > maxGap) maxGap = d2; }
            if (dz < -6f && slope > 25f) cliffCount++;
            else if (dz < -2f) dropCount++;
            if (dz < mostNegativeDz) mostNegativeDz = dz;
        }
        _output.WriteLine("# ===== UBRS path-wide segment histogram =====");
        _output.WriteLine($"#   total segments:           {sp.Length - 1}");
        _output.WriteLine($"#   STEP segs (dz>{WalkableClimbY:F1}y):     {stepCount} (max={maxStep:F2}y)");
        _output.WriteLine($"#   GAP2D segs (dist2D>4y):  {gapCount} (max={maxGap:F2}y)");
        _output.WriteLine($"#   -DROP segs (-6y<dz<-2y): {dropCount}");
        _output.WriteLine($"#   -CLIFF segs (post-R1):   {cliffCount}");
        _output.WriteLine($"#   most negative dz:        {mostNegativeDz:F2}y");

        // ===== Replan probe: smooth path FROM the stall coord TO UBRS =====
        // This is the corridor the bot is currently trying to walk.
        _output.WriteLine("");
        _output.WriteLine("# ===== REPLAN-FROM-STALL → UBRS (the bot's current planned route) =====");
        var stallPos = new XYZ(StallAnchorX, StallAnchorY, StallAnchorZ);
        var stallPoly = NavigationInterop.QueryPolyAtCoord(MapId, stallPos, AgentRadius, WalkableClimbY);
        if (stallPoly.HasPoly)
        {
            _output.WriteLine($"# Stall poly: 0x{stallPoly.PolyRef:X16} type={stallPoly.PolyType} surfaceZ={stallPoly.SurfaceZ:F2} dz-from-stall-z={stallPoly.SurfaceZ - StallAnchorZ:+0.00;-0.00}");
            // tile coord extraction (salt:16, tile:28, poly:20)
            ulong tile = (stallPoly.PolyRef >> 20) & 0xFFFFFFFul;
            _output.WriteLine($"# Stall tile (in polyRef): 0x{tile:X7}");
        }
        else
        {
            _output.WriteLine("# Stall coord has NO poly within walkableClimb 1.8y — the bot is wedged off-navmesh.");
        }

        var replan = NavigationInterop.QuerySmoothPath(MapId, stallPos, UbrsEntrance, AgentRadius, AgentHeight);
        if (!replan.Success || replan.Length == 0)
        {
            _output.WriteLine("# QuerySmoothPath FROM stall FAILED — no path. Bot is stuck on an isolated polygon.");
        }
        else
        {
            _output.WriteLine($"# Replan smooth-path waypoints: {replan.Length}");
            // Histogram on replan
            int rStep = 0, rGap = 0, rDrop = 0, rCliff = 0;
            float rMaxStep = 0f, rMaxGap = 0f, rMostNeg = 0f;
            for (int i = 1; i < replan.Length; i++)
            {
                var prev = replan.Waypoints[i - 1];
                var w = replan.Waypoints[i];
                float dz = w.Z - prev.Z;
                float dx = w.X - prev.X;
                float dy = w.Y - prev.Y;
                float d2 = MathF.Sqrt(dx * dx + dy * dy);
                float slope = d2 > 0.01f
                    ? MathF.Atan2(MathF.Abs(dz), d2) * (180f / MathF.PI)
                    : (MathF.Abs(dz) > 0.5f ? 90f : 0f);
                if (dz > WalkableClimbY) { rStep++; if (dz > rMaxStep) rMaxStep = dz; }
                if (d2 > 4f) { rGap++; if (d2 > rMaxGap) rMaxGap = d2; }
                if (dz < -6f && slope > 25f) rCliff++;
                else if (dz < -2f) rDrop++;
                if (dz < rMostNeg) rMostNeg = dz;
            }
            _output.WriteLine($"#   STEP segs:  {rStep} (max={rMaxStep:F2}y)");
            _output.WriteLine($"#   GAP2D segs: {rGap} (max={rMaxGap:F2}y)");
            _output.WriteLine($"#   -DROP segs: {rDrop}");
            _output.WriteLine($"#   -CLIFF segs:{rCliff}");
            _output.WriteLine($"#   most -dz:   {rMostNeg:F2}y");

            // Print first 12 waypoints to see the immediate next-steps from the stall.
            _output.WriteLine("# First 12 waypoints from stall:");
            int n = Math.Min(12, replan.Length);
            for (int i = 0; i < n; i++)
            {
                var w = replan.Waypoints[i];
                var probe = NavigationInterop.QueryPolyAtCoord(MapId, w, AgentRadius, WalkableClimbY);
                float dz = i > 0 ? w.Z - replan.Waypoints[i - 1].Z : 0f;
                float dx = i > 0 ? w.X - replan.Waypoints[i - 1].X : 0f;
                float dy = i > 0 ? w.Y - replan.Waypoints[i - 1].Y : 0f;
                float d2 = MathF.Sqrt(dx * dx + dy * dy);
                string surfStr = probe.HasSurface ? probe.SurfaceZ.ToString("F2") : "  N/A  ";
                _output.WriteLine($"#   {i,3} | ({w.X,8:F2},{w.Y,8:F2},{w.Z,6:F2}) surfaceZ={surfStr} dz={dz,+6:F2} dist2D={d2,5:F2}");
            }

            var lastWp = replan.Waypoints[replan.Length - 1];
            float endDx = lastWp.X - UbrsEntrance.X;
            float endDy = lastWp.Y - UbrsEntrance.Y;
            float endD2 = MathF.Sqrt(endDx * endDx + endDy * endDy);
            _output.WriteLine($"# Replan final WP: ({lastWp.X:F2},{lastWp.Y:F2},{lastWp.Z:F2}); dist2D-to-UBRS={endD2:F2}y, dz-to-UBRS={lastWp.Z - UbrsEntrance.Z:F2}");
        }

        // Probe surface at the stall coord with widening Z search to characterize the geometry there.
        _output.WriteLine("");
        _output.WriteLine("# ===== Vertical poly scan at the stall XY (widening Z) =====");
        _output.WriteLine("#   z   | polyRef             | type     | surfaceZ");
        _output.WriteLine("#  -----+---------------------+----------+---------");
        for (float z = StallAnchorZ - 20f; z <= StallAnchorZ + 20f; z += 1f)
        {
            var p = new XYZ(StallAnchorX, StallAnchorY, z);
            var probe = NavigationInterop.QueryPolyAtCoord(MapId, p, AgentRadius, 0.5f);
            if (probe.HasPoly)
            {
                _output.WriteLine($"#  {z,4:F1} | 0x{probe.PolyRef:X16} | {probe.PolyType,-8} | {(probe.HasSurface ? probe.SurfaceZ.ToString("F2") : "  N/A "),8}");
            }
        }
    }
}
