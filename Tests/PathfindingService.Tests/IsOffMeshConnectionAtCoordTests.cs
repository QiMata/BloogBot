using GameData.Core.Models;
using Xunit;

namespace PathfindingService.Tests;

// PFS-OVERHAUL-006 loop-24 Phase A5.2 — direct-iteration off-mesh detector
// verification. Reuses the OG-zeppelin tile (40, 29) trap coords from
// loop-21's diagnosis (memory: project_pfs_loop21_trap_diagnosis) which
// loop-24 Phase A2 (memory: project_pfs_loop24_phase_a2_polystack) verified
// have exactly ONE off-mesh poly between the OG harbor floor (z≈24) and the
// tower deck (z≈53). The new IsOffMeshConnectionAtCoord native export must
// return TRUE at those trap coords (where findNearestPoly returns 0) and
// FALSE at a ground coord well away from any teleport edge.
//
// This is the underlying primitive used by Navigation.cs's
// IsOffMeshSegment helper that gates per-phase repair skips in the
// managed pipeline (loop-24 Phase A5.3+ will apply skip-checks across the
// 8 repair phases).
[Trait("Category", "Unit")]
[Collection(NavigationCollection.Name)]
public sealed class IsOffMeshConnectionAtCoordTests
{
    private readonly NavigationFixture _fixture;

    public IsOffMeshConnectionAtCoordTests(NavigationFixture fixture)
    {
        _fixture = fixture;
    }

    private const uint Map1Kalimdor = 1;
    private const float DefaultXyExtent = 2.0f;
    private const float DefaultZExtent = 4.0f;

    [Fact]
    public void Coord1_OgZeppelinAirTrap_DetectedAsOffMesh()
    {
        // Loop-21 + A2 confirmed: coord (1347.3, -4540.6, 35.8) sits between
        // the OG harbor floor and the tower deck. The off-mesh-connection
        // poly's AABB spans Z [29.5, 53.7] and covers this XY. findNearestPoly
        // returns polyref=0 at this coord, but direct iteration finds the
        // off-mesh poly.
        Assert.True(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1347.3f, -4540.6f, 35.8f),
            DefaultXyExtent,
            DefaultZExtent));
    }

    [Fact]
    public void Coord3_OgZeppelinAirTrap_DetectedAsOffMesh()
    {
        // A2 confirmed: coord (1348.0, -4537.7, 35.4) has 5 polys (4 deck
        // ground + 1 off-mesh-connection). The off-mesh AABB intersects the
        // default Z window.
        Assert.True(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1348.0f, -4537.7f, 35.4f),
            DefaultXyExtent,
            DefaultZExtent));
    }

    [Fact]
    public void CrossroadsGroundCoord_NoOffMeshNearby()
    {
        // Crossroads in the Barrens is far from any off-mesh entry on map 1.
        // The detector should return false here so the managed pipeline
        // treats segments through this region as normal ground-walkable.
        Assert.False(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(-468f, -2613f, 96f),
            DefaultXyExtent,
            DefaultZExtent));
    }

    [Fact]
    public void NegativeExtents_ReturnFalseSafely()
    {
        // The native export validates extents and returns false on negative
        // values rather than crashing.
        Assert.False(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1347.3f, -4540.6f, 35.8f),
            xyExtent: -1f,
            zExtent: 4f));
        Assert.False(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1347.3f, -4540.6f, 35.8f),
            xyExtent: 2f,
            zExtent: -1f));
    }
}
