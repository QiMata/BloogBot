using GameData.Core.Models;
using Xunit;

namespace PathfindingService.Tests;

// Direct-iteration off-mesh detector verification. Use the real OG zeppelin
// deck-edge/boarding seam, not terrain-to-boarding shortcut coords: those
// shortcuts route live clients out to the Durotar hillside.
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
    private const float DefaultZExtent  = 4.0f;

    [Fact]
    public void OgZeppelinDeckEdgeAnchor_DetectedAsOffMesh()
    {
        Assert.True(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1329.877f, -4653.495f, 53.609f),
            DefaultXyExtent,
            DefaultZExtent));
    }

    [Fact]
    public void OgZeppelinBoardingAnchor_DetectedAsOffMesh()
    {
        Assert.True(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1320.143f, -4653.159f, 53.892f),
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
            new XYZ(1329.877f, -4653.495f, 53.609f),
            xyExtent: -1f,
            zExtent: 4f));
        Assert.False(NavigationInterop.IsOffMeshConnectionAtCoord(
            Map1Kalimdor,
            new XYZ(1329.877f, -4653.495f, 53.609f),
            xyExtent: 2f,
            zExtent: -1f));
    }
}
