using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// PFS-OVERHAUL-006 / Phase 6 (2026-05-07): waypoint-correctness gate.
///
/// What this test class proves:
///   For each canonical route in tools/scripts/routes/og-zeppelin.json,
///   the smooth-path corner sequence returned by Detour
///   (FindPathCornersForAgent) consists ENTIRELY of corners that sit on
///   real walkable polygons in the navmesh — not synthetic interpolation
///   samples between dangling off-mesh anchor endpoints.
///
/// What it does NOT prove:
///   That the bot can EXECUTE the corner sequence (runtime physics, capsule
///   sweep, collision response, step-up tolerance). That is the live
///   ClimbOrgrimmarZeppelinTowerRampToFrezza test's job. Waypoint
///   correctness is a strict prerequisite for execution validity:
///   if the bake produced corners floating in space, no amount of
///   runtime physics tuning can make the walk succeed.
///
/// Why this matters:
///   MmapVisualize on tile (1, 29, 40) (pre-fix) shows the OG zeppelin
///   tower's lower deck at z=48-70 has NO walkable polygons — the lowest
///   walkable Detour vert in this tile is at z=71.44. The tile has 4
///   off-mesh connections with anchor endpoints at z=51.6 and z=53.89,
///   but those anchors are not real walkable polys. Detour's
///   findStraightPath will happily emit corners that interpolate the
///   off-mesh anchor coords, producing a smooth path the bot cannot
///   stand on. This test catches that condition crisply.
///
/// Validation contract per corner:
///   1. Within (agentRadius x walkableClimb x agentRadius) of a real
///      navmesh polygon (GetPolyAtCoord returns outPolyRef != 0).
///   2. The poly is a ground polygon (not DT_POLYTYPE_OFFMESH_CONNECTION).
///      Off-mesh polys are legitimate routing primitives but a smooth-path
///      corner ON one means the bot would have to traverse the off-mesh
///      link as walking — wrong.
///   3. The poly's surface Z at corner.XY (via getPolyHeight) is within
///      walkableClimb (1.8y, harvested-from-client) of corner.Z. A larger
///      delta means the corner is "in space" relative to the surface.
///
/// Tolerance source:
///   walkableClimb = 1.8y is a BAKE PARAMETER, harvested from the WoW
///   client and locked by feedback_pathfinding_anti_patterns.md (do NOT
///   lower it). The test does not invent a tolerance — it reuses the
///   bake's own walkableClimb.
/// </summary>
[Trait("Category", "Unit")]
public class WaypointGenerationTests : IClassFixture<PathfindingValidationFixture>
{
    private readonly PathfindingValidationFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly RouteManifest _manifest;

    private const string OgZeppelinManifestPath = "tools/scripts/routes/og-zeppelin.json";

    /// <summary>
    /// Harvested-from-client walkableClimb. Locked by the freeze contract;
    /// see feedback_pathfinding_anti_patterns.md.
    /// </summary>
    private const float WalkableClimb = 1.8f;

    public WaypointGenerationTests(PathfindingValidationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _manifest = RouteManifestLoader.Load(OgZeppelinManifestPath);
        _output.WriteLine($"# WWOW_DATA_DIR resolved to: {_fixture.DataDir}");
        _output.WriteLine($"# Validation port reservation: {_fixture.Port} (service spawn opt-in via WWOW_USE_VALIDATION_PATHFINDING_SERVICE=1)");
        _output.WriteLine($"# Manifest: {OgZeppelinManifestPath} (schema v{_manifest.SchemaVersion}, {_manifest.Routes.Count} routes)");
    }

    public static IEnumerable<object[]> RouteCases()
    {
        var manifest = RouteManifestLoader.Load(OgZeppelinManifestPath);
        foreach (var r in manifest.Routes)
            yield return new object[] { r.Name };
    }

    /// <summary>
    /// Validates the full smooth-path waypoint sequence (FindPathForAgent
    /// with smoothPath=true) — i.e. the exact list of points NavigationPath
    /// feeds the bot. This is a denser sampling than findStraightPath and
    /// catches off-mesh-link interpolations that span unwalkable terrain.
    /// The user-reported "WP[1] settles at z=42.32 from teleport at z=51.7"
    /// signal would surface here as a failed surface-Z check at that index.
    /// </summary>
    [Theory]
    [MemberData(nameof(RouteCases))]
    public void Route_SmoothPathWaypointsSitOnRealWalkablePolygons(string routeName)
    {
        var route = _manifest.Routes.FirstOrDefault(r => r.Name == routeName)
            ?? throw new InvalidOperationException($"Route '{routeName}' not found in manifest.");

        var start = new XYZ(route.Start[0], route.Start[1], route.Start[2]);
        var end = new XYZ(route.End[0], route.End[1], route.End[2]);

        var path = NavigationInterop.QuerySmoothPath(
            route.Map, start, end, _manifest.Agent.Radius, _manifest.Agent.Height);

        Assert.True(path.Success,
            $"Route '{route.Name}': QuerySmoothPath returned no waypoints (Detour smoothPath synthesis failure).");
        Assert.True(path.Length >= 2,
            $"Route '{route.Name}': only {path.Length} smooth waypoint(s) (need >= 2).");

        var polys = new NavigationInterop.PolyAtCoordResult[path.Waypoints.Length];
        for (int i = 0; i < path.Waypoints.Length; i++)
        {
            polys[i] = NavigationInterop.QueryPolyAtCoord(
                route.Map,
                path.Waypoints[i],
                searchExtentXY: _manifest.Agent.Radius,
                searchExtentZ: WalkableClimb);
        }

        EmitSmoothPathDiagnosticTable(route, path.Waypoints, polys);

        var failures = CollectFailures(path.Waypoints, polys);
        if (failures.Count == 0)
            return;

        var firstIdx = failures[0].Index;
        var firstWp = path.Waypoints[firstIdx];
        var firstPoly = polys[firstIdx];
        var summary =
            $"Route '{route.Name}' failed smooth-path waypoint validation: "
            + $"{failures.Count}/{path.Waypoints.Length} waypoints are not on real walkable polygons.\n"
            + $"First failure at waypoint [{firstIdx:D3}] WoW=({firstWp.X:F2},{firstWp.Y:F2},{firstWp.Z:F2}); "
            + $"polyRef=0x{firstPoly.PolyRef:X16}, polyType={firstPoly.PolyType}, "
            + $"surfaceZ={(firstPoly.HasSurface ? firstPoly.SurfaceZ.ToString("F2") : "no-surface")}, "
            + $"reason={failures[0].Reason}.";
        if (route.ExpectedStallCoord != null && route.ExpectedStallCoord.Length == 3)
        {
            summary +=
                $"\nManifest expectedStallCoord=({route.ExpectedStallCoord[0]:F2},{route.ExpectedStallCoord[1]:F2},{route.ExpectedStallCoord[2]:F2}); "
                + $"expectedStallReason='{route.ExpectedStallReason ?? "(none)"}'.";
        }
        Assert.Fail(summary);
    }

    private void EmitSmoothPathDiagnosticTable(
        RouteEntry route, XYZ[] waypoints, NavigationInterop.PolyAtCoordResult[] polys)
    {
        _output.WriteLine($"# Route: {route.Name} -- SMOOTH PATH ({waypoints.Length} waypoints)");

        // For long routes (>32 waypoints) emit only the summary plus any
        // failure indices to keep the trx readable. Healthy long routes
        // emit just summary; failing ones still emit failure context.
        var failureIndices = new HashSet<int>();
        for (int i = 0; i < waypoints.Length; i++)
        {
            var dz = polys[i].HasSurface ? Math.Abs(waypoints[i].Z - polys[i].SurfaceZ) : float.NaN;
            if (ClassifyCornerStatus(polys[i], dz) != "OK")
                failureIndices.Add(i);
        }

        bool verbose = waypoints.Length <= 32 || failureIndices.Count > 0;
        if (!verbose)
        {
            _output.WriteLine($"#   all {waypoints.Length} waypoints OK (compact-mode output suppressed; >32 waypoints, no failures)");
            return;
        }

        _output.WriteLine("#   idx |       waypoint X,Y,Z       | polyRef          | type     | surfaceZ |  dz   | result");
        _output.WriteLine("#   ----+----------------------------+------------------+----------+----------+-------+-------");
        for (int i = 0; i < waypoints.Length; i++)
        {
            // For long failing routes, only emit a window around each failure.
            if (waypoints.Length > 32 && failureIndices.Count > 0)
            {
                bool nearFailure = false;
                foreach (var f in failureIndices)
                {
                    if (Math.Abs(i - f) <= 3) { nearFailure = true; break; }
                }
                if (!nearFailure && i != 0 && i != waypoints.Length - 1)
                    continue;
            }
            var w = waypoints[i];
            var p = polys[i];
            var dz = p.HasSurface ? Math.Abs(w.Z - p.SurfaceZ) : float.NaN;
            string status = ClassifyCornerStatus(p, dz);
            _output.WriteLine(
                $"#   {i,3} | {w.X,8:F2},{w.Y,8:F2},{w.Z,6:F2} | 0x{p.PolyRef:X14} | {p.PolyType,-8} | "
                + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8} | "
                + $"{(float.IsNaN(dz) ? "  N/A " : dz.ToString("F2")),5} | {status}");
        }
    }

    [Theory]
    [MemberData(nameof(RouteCases))]
    public void Route_SmoothPathCornersSitOnRealWalkablePolygons(string routeName)
    {
        var route = _manifest.Routes.FirstOrDefault(r => r.Name == routeName)
            ?? throw new InvalidOperationException($"Route '{routeName}' not found in manifest.");

        var (corners, polys) = QueryAndAnalyze(route);

        EmitDiagnosticTable(route, corners, polys);

        var failures = CollectFailures(corners, polys);
        if (failures.Count == 0)
            return;

        var firstIdx = failures[0].Index;
        var firstCorner = corners[firstIdx];
        var firstPoly = polys[firstIdx];
        var summary =
            $"Route '{route.Name}' failed waypoint validation: "
            + $"{failures.Count}/{corners.Length} corners are not on real walkable polygons.\n"
            + $"First failure at corner [{firstIdx:D3}] WoW=({firstCorner.X:F2},{firstCorner.Y:F2},{firstCorner.Z:F2}); "
            + $"polyRef=0x{firstPoly.PolyRef:X16}, polyType={firstPoly.PolyType}, "
            + $"surfaceZ={(firstPoly.HasSurface ? firstPoly.SurfaceZ.ToString("F2") : "no-surface")}, "
            + $"reason={failures[0].Reason}.";
        if (route.ExpectedStallCoord != null && route.ExpectedStallCoord.Length == 3)
        {
            summary +=
                $"\nManifest expectedStallCoord=({route.ExpectedStallCoord[0]:F2},{route.ExpectedStallCoord[1]:F2},{route.ExpectedStallCoord[2]:F2}); "
                + $"expectedStallReason='{route.ExpectedStallReason ?? "(none)"}'.";
        }
        Assert.Fail(summary);
    }

    private (XYZ[] Corners, NavigationInterop.PolyAtCoordResult[] Polys) QueryAndAnalyze(RouteEntry route)
    {
        var start = new XYZ(route.Start[0], route.Start[1], route.Start[2]);
        var end = new XYZ(route.End[0], route.End[1], route.End[2]);

        var cornerResult = NavigationInterop.QueryPathCorners(
            route.Map,
            start,
            end,
            _manifest.Agent.Radius,
            _manifest.Agent.Height,
            maxCorners: 96,
            options: NavigationInterop.StraightPathOptions.None);

        Assert.True(
            cornerResult.Success,
            $"Route '{route.Name}': QueryPathCorners failed (Detour findPath/findStraightPath returned an error). "
            + "This is a routing-layer failure, not a bake-fidelity failure — investigate before treating as a waypoint bug.");
        Assert.True(
            cornerResult.CornerCount >= 2,
            $"Route '{route.Name}': only {cornerResult.CornerCount} corner(s) returned (need >= 2).");

        var polys = new NavigationInterop.PolyAtCoordResult[cornerResult.Corners.Length];
        for (int i = 0; i < cornerResult.Corners.Length; i++)
        {
            polys[i] = NavigationInterop.QueryPolyAtCoord(
                route.Map,
                cornerResult.Corners[i],
                searchExtentXY: _manifest.Agent.Radius,
                searchExtentZ: WalkableClimb);
        }

        return (cornerResult.Corners, polys);
    }

    private void EmitDiagnosticTable(RouteEntry route, XYZ[] corners, NavigationInterop.PolyAtCoordResult[] polys)
    {
        _output.WriteLine($"# Route: {route.Name}");
        _output.WriteLine($"#   description: {route.Description ?? "(none)"}");
        _output.WriteLine($"#   start=({route.Start[0]:F2},{route.Start[1]:F2},{route.Start[2]:F2}) end=({route.End[0]:F2},{route.End[1]:F2},{route.End[2]:F2})");
        _output.WriteLine($"#   agent: r={_manifest.Agent.Radius:F4} h={_manifest.Agent.Height:F3} (walkableClimb={WalkableClimb}y)");
        _output.WriteLine($"#   corners returned: {corners.Length}");
        _output.WriteLine("#   idx |       corner X,Y,Z       | polyRef          | type     | surfaceZ |  dz   | result");
        _output.WriteLine("#   ----+--------------------------+------------------+----------+----------+-------+-------");
        for (int i = 0; i < corners.Length; i++)
        {
            var c = corners[i];
            var p = polys[i];
            var dz = p.HasSurface ? Math.Abs(c.Z - p.SurfaceZ) : float.NaN;
            string status = ClassifyCornerStatus(p, dz);
            _output.WriteLine(
                $"#   {i,3} | {c.X,8:F2},{c.Y,8:F2},{c.Z,6:F2} | 0x{p.PolyRef:X14} | {p.PolyType,-8} | "
                + $"{(p.HasSurface ? p.SurfaceZ.ToString("F2") : "  N/A "),8} | "
                + $"{(float.IsNaN(dz) ? "  N/A " : dz.ToString("F2")),5} | {status}");
        }
    }

    private static List<(int Index, string Reason)> CollectFailures(
        XYZ[] corners, NavigationInterop.PolyAtCoordResult[] polys)
    {
        var failures = new List<(int, string)>();
        for (int i = 0; i < corners.Length; i++)
        {
            var c = corners[i];
            var p = polys[i];
            if (!p.Success)
            {
                failures.Add((i, "GetPolyAtCoord infrastructure failure"));
                continue;
            }
            if (!p.HasPoly)
            {
                failures.Add((i, $"no walkable polygon within (agentRadius x walkableClimb={WalkableClimb}y) of corner"));
                continue;
            }
            if (p.PolyType == NavigationInterop.PolyType.OffMeshConnection)
            {
                failures.Add((i, "corner sits on an off-mesh-connection polygon (anchor endpoint, not walkable ground)"));
                continue;
            }
            if (!p.HasSurface)
            {
                failures.Add((i, "polygon found but getPolyHeight failed at corner.XY (corner outside polygon's 2D footprint)"));
                continue;
            }
            float dz = Math.Abs(c.Z - p.SurfaceZ);
            if (dz > WalkableClimb)
            {
                failures.Add((i, $"|corner.Z - surfaceZ|={dz:F2}y exceeds walkableClimb={WalkableClimb}y"));
            }
        }
        return failures;
    }

    private static string ClassifyCornerStatus(NavigationInterop.PolyAtCoordResult p, float dz)
    {
        if (!p.Success) return "INFRA-FAIL";
        if (!p.HasPoly) return "NO-POLY";
        if (p.PolyType == NavigationInterop.PolyType.OffMeshConnection) return "OFFMESH";
        if (!p.HasSurface) return "NO-SURFACE";
        return dz <= WalkableClimb ? "OK" : "OFF-SURFACE";
    }
}
