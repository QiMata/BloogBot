using System;
using System.Linq;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// PFS-OVERHAUL — property tests rooted in
/// <c>tmp/test-runtime/screenshots/brm-ascent-recon/RECON_SUMMARY.md</c>.
/// Each test codifies one observation from the FG-rendering recon at a
/// known coord on the FlameCrest → BRM dungeon-entrance route.
///
/// These tests are intentionally RED on the current bake. They are the
/// iteration gate for Phase 2 of the BRM ascent fix. A test going green
/// means the bake/runtime change has actually closed the world-bake
/// disagreement at that coord. They MUST be the goalpost for declaring
/// the FlameCrest → BRM ascent fixed — passing
/// <c>BrmDungeonRouteDiagnostic.Audit_BrmDungeonEndpoints_ResolveAndCorridor</c>'s
/// <c>endsAtTarget=YES</c> alone is NOT sufficient (that has flipped to
/// YES on every reverted attempt and live FG still regressed).
///
/// Pre-Phase-2 baseline (2026-05-13):
///   `Walkable_*` + `Walkable_AllFourPortals_HaveGroundPoly` — already green
///       (bake reports walkable polygons at every render-confirmed coord).
///   `Corridor_*MustReachTarget` — varies; LBRS/UBRS green per audit, BRD/BWL
///       partial.
///   `Corridor_*DoesNotPassThroughFcStallPoly` — RED (the fc_stall poly
///       0x0001000015001ECA sits in the FC→UBRS corridor).
///   `SmoothPath_*NoUnreasonableZJump` — RED expected (smooth path string-
///       pulls through cliff faces with multi-yard dz).
///   `SmoothPath_*NoCliffWaypointsNearFcStall` — RED expected.
///
/// All tests are gated as <c>[Fact]</c> (no env var) — they should run
/// in normal CI to track Phase 2 progress.
/// </summary>
[Trait("Category", "Unit")]
public class BrmAscentRenderingExpectations : IClassFixture<PathfindingValidationFixture>
{
    private readonly PathfindingValidationFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const uint MapId = 0;
    private const float AgentRadius = 1.0247f;
    private const float AgentHeight = 2.625f;
    private const float WalkableClimb = 1.8f;

    // Recon coords (must match BrmAscentReconTests.Coords + BrmAscentReconPolyrefDump).
    private static readonly XYZ FlameCrest      = new(-7518.7f, -2159.9f, 131.9f);
    private static readonly XYZ FcStall         = new(-7519.0f, -2100.4f, 130.3f);
    private static readonly XYZ RuinsWall       = new(-7665.0f, -1808.0f, 137.0f);
    private static readonly XYZ BrmSouthLo      = new(-7949.7f, -1162.8f, 170.8f);
    private static readonly XYZ BrmSouthNew     = new(-7825.4f, -1129.2f, 133.8f);
    private static readonly XYZ BrmMidLbrs      = new(-7647.1f, -1197.1f, 225.2f);
    private static readonly XYZ BrmMidBwl       = new(-7640.0f, -1213.4f, 228.4f);
    private static readonly XYZ UbrsPortal      = new(-7524.0f, -1233.0f, 287.0f);
    private static readonly XYZ LbrsPortal      = new(-7531.0f, -1226.0f, 286.0f);
    private static readonly XYZ BwlPortal       = new(-7659.0f, -1214.0f, 291.0f);
    private static readonly XYZ BrdPortal       = new(-7187.0f,  -958.0f, 254.0f);

    // fc_stall polyRef captured by the recon's 2026-05-13 polyref dump.
    // 0x0001000015001ECA tile=0x000150 (35,46), poly=0x01ECA. RECON_SUMMARY
    // entry #2 — bake says Ground area=1 but rendering shows the coord is
    // wedged inside a rock outcrop with lava on one flank.
    private const ulong FcStallPolyRef = 0x0001000015001ECAul;

    public BrmAscentRenderingExpectations(
        PathfindingValidationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ---------------------------------------------------------------------
    // Walkable_* — bake must have a Ground poly at every render-confirmed
    // walkable coord. These are the cheap baseline gates; they passed on
    // the 2026-05-13 baseline and should stay green.
    // ---------------------------------------------------------------------

    [Fact]
    public void Walkable_RuinsWall_HasGroundPoly()
        => AssertWalkablePolyAtCoord("ruins_wall", RuinsWall);

    [Fact]
    public void Walkable_BrmSouthLo_HasGroundPoly()
        => AssertWalkablePolyAtCoord("brm_south_lo", BrmSouthLo);

    [Fact]
    public void Walkable_AllFourPortals_HaveGroundPoly()
    {
        AssertWalkablePolyAtCoord("ubrs_portal", UbrsPortal);
        AssertWalkablePolyAtCoord("lbrs_portal", LbrsPortal);
        AssertWalkablePolyAtCoord("bwl_portal",  BwlPortal);
        AssertWalkablePolyAtCoord("brd_portal",  BrdPortal);
    }

    // ---------------------------------------------------------------------
    // Corridor_*MustReachTarget — for each of the four BRM dungeon portal
    // coords, the FC→portal polygon corridor must terminate at that
    // coord's nearest walkable poly. Currently LBRS/UBRS pass per the
    // 2026-05-13 audit; BRD/BWL partial.
    // ---------------------------------------------------------------------

    [Fact]
    public void Corridor_FcToUbrsPortal_TerminatesAtPortalPoly()
        => AssertCorridorTerminatesAtPortal("UBRS", FlameCrest, UbrsPortal);

    [Fact]
    public void Corridor_FcToLbrsPortal_TerminatesAtPortalPoly()
        => AssertCorridorTerminatesAtPortal("LBRS", FlameCrest, LbrsPortal);

    [Fact]
    public void Corridor_FcToBrdPortal_TerminatesAtPortalPoly()
        => AssertCorridorTerminatesAtPortal("BRD", FlameCrest, BrdPortal);

    [Fact]
    public void Corridor_FcToBwlPortal_TerminatesAtPortalPoly()
        => AssertCorridorTerminatesAtPortal("BWL", FlameCrest, BwlPortal);

    // ---------------------------------------------------------------------
    // Corridor_*DoesNotPassThroughFcStallPoly — RECON_SUMMARY entry #2:
    // the fc_stall coord (-7519,-2100,130) is rendered as INSIDE a rock
    // outcrop with lava on one flank. The bake's Ground poly there
    // (0x0001000015001ECA) should NEVER appear in a corridor from
    // FlameCrest; if Detour traverses it, the smooth-path string-pull
    // will route the bot into rock.
    //
    // These tests are RED on the current bake — they're the diagnostic
    // gates for whether Phase 2's chosen fix surface (likely a
    // PolyRef-targeted cull of FcStallPolyRef) actually closes the
    // world-bake disagreement.
    // ---------------------------------------------------------------------

    [Fact]
    public void Corridor_FcToUbrsPortal_DoesNotPassThroughFcStallPoly()
        => AssertCorridorAvoidsFcStall("UBRS", FlameCrest, UbrsPortal);

    [Fact]
    public void Corridor_FcToLbrsPortal_DoesNotPassThroughFcStallPoly()
        => AssertCorridorAvoidsFcStall("LBRS", FlameCrest, LbrsPortal);

    [Fact]
    public void Corridor_FcToBrdPortal_DoesNotPassThroughFcStallPoly()
        => AssertCorridorAvoidsFcStall("BRD", FlameCrest, BrdPortal);

    [Fact]
    public void Corridor_FcToBwlPortal_DoesNotPassThroughFcStallPoly()
        => AssertCorridorAvoidsFcStall("BWL", FlameCrest, BwlPortal);

    // ---------------------------------------------------------------------
    // SmoothPath_* — these are the player-facing properties. The bot
    // consumes the smooth-path waypoints, so per-WP jumps and proximity
    // to known phantom polys directly cause stalls.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Skill-spec property: every smooth-path waypoint pair must have
    /// |dz| ≤ walkableClimb (1.8y). Larger jumps mean Detour string-pulled
    /// across a vertical step the bot's NavigationPath cannot accept (the
    /// 1.25y WAYPOINT_VERTICAL_REACH_TOLERANCE gates the bot, but even
    /// with that constant, jumps >1.8y span impassable terrain).
    ///
    /// RED expected: the BRM ascent has known cliff string-pulls.
    /// </summary>
    [Fact]
    public void SmoothPath_FcToUbrsPortal_NoUnreasonableZJump()
        => AssertSmoothPathHasNoUnreasonableZJump("UBRS", FlameCrest, UbrsPortal);

    /// <summary>
    /// The fc_stall coord (-7519,-2100,130) is rendered as rock-wedged
    /// (RECON_SUMMARY #2). No smooth-path waypoint may land within 5y XY
    /// of it AT z within ±5y; if any do, the bot is being walked into
    /// the rock pocket.
    /// </summary>
    [Fact]
    public void SmoothPath_FcToUbrsPortal_NoCliffWaypointsNearFcStall()
        => AssertSmoothPathAvoidsCoord(
            label: "UBRS",
            start: FlameCrest,
            dest: UbrsPortal,
            avoidCoord: FcStall,
            xyRadius: 5f,
            zRadius: 5f);

    [Fact]
    public void SmoothPath_FcToLbrsPortal_NoCliffWaypointsNearFcStall()
        => AssertSmoothPathAvoidsCoord(
            label: "LBRS",
            start: FlameCrest,
            dest: LbrsPortal,
            avoidCoord: FcStall,
            xyRadius: 5f,
            zRadius: 5f);

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void AssertWalkablePolyAtCoord(string label, XYZ coord)
    {
        var probe = NavigationInterop.QueryPolyAtCoord(
            MapId, coord, AgentRadius, WalkableClimb);

        _output.WriteLine(
            $"# {label} ({coord.X:F1},{coord.Y:F1},{coord.Z:F1}): "
            + $"hasPoly={probe.HasPoly} polyRef=0x{probe.PolyRef:X16} type={probe.PolyType} "
            + (probe.HasSurface ? $"surfaceZ={probe.SurfaceZ:F2} dz={Math.Abs(coord.Z - probe.SurfaceZ):F2}" : "no surface"));

        Assert.True(
            probe.HasPoly,
            $"RECON_SUMMARY rendering shows walkable terrain at {label}, but bake has no poly within "
            + $"agentRadius={AgentRadius}y / walkableClimb={WalkableClimb}y of ({coord.X:F1},{coord.Y:F1},{coord.Z:F1}).");
        Assert.True(
            probe.HasSurface,
            $"{label}: bake has poly 0x{probe.PolyRef:X16} but no surfaceZ; rendering says walkable.");
        Assert.True(
            Math.Abs(coord.Z - probe.SurfaceZ) <= WalkableClimb,
            $"{label}: bake surfaceZ={probe.SurfaceZ:F2} is {Math.Abs(coord.Z - probe.SurfaceZ):F2}y from rendering Z={coord.Z:F2}; "
            + $"exceeds walkableClimb={WalkableClimb}y.");
    }

    private void AssertCorridorTerminatesAtPortal(string label, XYZ start, XYZ portal)
    {
        var chain = NavigationInterop.QueryPathPolygons(
            MapId, start, portal, AgentRadius, AgentHeight, maxOut: 4096);
        Assert.True(chain.Success && chain.PolyRefs.Length > 0,
            $"FC→{label}: corridor query returned no path; expected at least a partial chain.");

        var endProbe = NavigationInterop.QueryPolyAtCoord(MapId, portal, 10f, 300f);
        Assert.True(endProbe.HasPoly,
            $"{label}: portal coord ({portal.X:F1},{portal.Y:F1},{portal.Z:F1}) does not resolve to a poly even with wide search.");

        var lastPoly = chain.PolyRefs[chain.PolyRefs.Length - 1];
        _output.WriteLine(
            $"# FC→{label}: corridor {chain.PolyRefs.Length} polys, "
            + $"first=0x{chain.PolyRefs[0]:X16} last=0x{lastPoly:X16} "
            + $"portal=0x{endProbe.PolyRef:X16}");

        Assert.True(
            lastPoly == endProbe.PolyRef,
            $"FC→{label}: corridor terminates at 0x{lastPoly:X16}, "
            + $"but portal nearest poly is 0x{endProbe.PolyRef:X16}. "
            + $"Detour returned a partial path — rendering confirms the portal is in-game reachable, "
            + $"so the bake is missing connectivity between corridor terminus and portal.");
    }

    private void AssertCorridorAvoidsFcStall(string label, XYZ start, XYZ dest)
    {
        var chain = NavigationInterop.QueryPathPolygons(
            MapId, start, dest, AgentRadius, AgentHeight, maxOut: 4096);
        Assert.True(chain.Success && chain.PolyRefs.Length > 0,
            $"FC→{label}: corridor query returned no path; cannot evaluate fc_stall avoidance.");

        var hits = chain.PolyRefs
            .Select((polyRef, idx) => (idx, polyRef))
            .Where(t => t.polyRef == FcStallPolyRef)
            .ToList();

        _output.WriteLine(
            $"# FC→{label}: corridor {chain.PolyRefs.Length} polys, fc_stall poly hits: {hits.Count}");

        Assert.True(
            hits.Count == 0,
            $"FC→{label}: corridor includes the fc_stall poly 0x{FcStallPolyRef:X16} at "
            + $"{(hits.Count <= 4 ? string.Join(",", hits.Select(h => $"idx={h.idx}")) : "many positions")}. "
            + $"RECON_SUMMARY entry #2 shows this coord is geometrically wedged inside rock with lava on one flank — "
            + $"the corridor must route around it. Targeted fix: cull this poly from the bake "
            + $"(NavMeshTileEditor flags=0 on tile (35,46) poly 0x1ECA).");
    }

    private void AssertSmoothPathHasNoUnreasonableZJump(string label, XYZ start, XYZ dest)
    {
        var sp = NavigationInterop.QuerySmoothPath(MapId, start, dest, AgentRadius, AgentHeight);
        Assert.True(sp.Success && sp.Length >= 2,
            $"FC→{label}: smooth path returned {sp.Length} waypoints; need at least 2 to check WP-to-WP dz.");

        int worstIdx = -1;
        float worstDz = 0f;
        int violations = 0;
        for (int i = 1; i < sp.Length; i++)
        {
            float dz = Math.Abs(sp.Waypoints[i].Z - sp.Waypoints[i - 1].Z);
            if (dz > WalkableClimb)
                violations++;
            if (dz > worstDz)
            {
                worstDz = dz;
                worstIdx = i;
            }
        }

        _output.WriteLine(
            $"# FC→{label}: smooth-path {sp.Length} WPs, {violations} WP-to-WP dz > walkableClimb({WalkableClimb}y); "
            + $"worst at idx {worstIdx} dz={worstDz:F2}");

        Assert.True(
            violations == 0,
            $"FC→{label}: {violations} smooth-path waypoint pair(s) have |dz| > walkableClimb({WalkableClimb}y). "
            + $"Worst at idx {worstIdx}: dz={worstDz:F2} between "
            + $"({sp.Waypoints[worstIdx - 1].X:F1},{sp.Waypoints[worstIdx - 1].Y:F1},{sp.Waypoints[worstIdx - 1].Z:F1}) and "
            + $"({sp.Waypoints[worstIdx].X:F1},{sp.Waypoints[worstIdx].Y:F1},{sp.Waypoints[worstIdx].Z:F1}). "
            + $"NavigationPath.WAYPOINT_VERTICAL_REACH_TOLERANCE=1.25y will stall the bot at any jump exceeding it; "
            + $"the bake is producing impassable step transitions where the rendering shows continuous walkable terrain.");
    }

    private void AssertSmoothPathAvoidsCoord(
        string label, XYZ start, XYZ dest, XYZ avoidCoord, float xyRadius, float zRadius)
    {
        var sp = NavigationInterop.QuerySmoothPath(MapId, start, dest, AgentRadius, AgentHeight);
        Assert.True(sp.Success && sp.Length >= 1,
            $"FC→{label}: smooth path returned no waypoints; cannot evaluate proximity to ({avoidCoord.X:F1},{avoidCoord.Y:F1},{avoidCoord.Z:F1}).");

        var hits = sp.Waypoints
            .Select((wp, idx) => (idx, wp,
                d2: MathF.Sqrt((wp.X - avoidCoord.X) * (wp.X - avoidCoord.X)
                             + (wp.Y - avoidCoord.Y) * (wp.Y - avoidCoord.Y)),
                dz: Math.Abs(wp.Z - avoidCoord.Z)))
            .Where(t => t.d2 <= xyRadius && t.dz <= zRadius)
            .ToList();

        _output.WriteLine(
            $"# FC→{label}: smooth-path {sp.Length} WPs, hits within ({xyRadius:F0}y XY, {zRadius:F0}y Z) of "
            + $"avoid=({avoidCoord.X:F1},{avoidCoord.Y:F1},{avoidCoord.Z:F1}): {hits.Count}");

        Assert.True(
            hits.Count == 0,
            $"FC→{label}: smooth path lands {hits.Count} waypoint(s) within ({xyRadius:F0}y XY, {zRadius:F0}y Z) of fc_stall "
            + $"({avoidCoord.X:F1},{avoidCoord.Y:F1},{avoidCoord.Z:F1}). "
            + $"RECON_SUMMARY entry #2 shows fc_stall is rock-wedged. First hits: "
            + string.Join("; ", hits.Take(4).Select(h =>
                $"idx={h.idx} wp=({h.wp.X:F1},{h.wp.Y:F1},{h.wp.Z:F1}) d2={h.d2:F2}y dz={h.dz:F2}y")));
    }
}
