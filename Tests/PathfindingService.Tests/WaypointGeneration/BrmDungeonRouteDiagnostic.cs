using System;
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

    private static readonly XYZ FlameCrest = new(-7511f, -2188f, 165f);
    private static readonly XYZ BrdEntrance  = new(-7179f,  -921f, 165f);
    private static readonly XYZ LbrsEntrance = new(-7531f, -1226f, 286f);
    private static readonly XYZ UbrsEntrance = new(-7524f, -1233f, 287f);
    private static readonly XYZ BwlEntrance  = new(-7665f, -1102f, 400f);

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
            string last  = n > 0 ? $"0x{chain.PolyRefs[n - 1]:X16}" : "-";
            _output.WriteLine($"#   {label,-35}|  {n,4} | {(endsAtTarget ? "YES" : "NO ") ,12} | {first} | {last}");
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
}
