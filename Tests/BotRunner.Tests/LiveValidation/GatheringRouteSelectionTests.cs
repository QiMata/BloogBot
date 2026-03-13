using System.Linq;

namespace BotRunner.Tests.LiveValidation;

public class GatheringRouteSelectionTests
{
    [Fact]
    public void SelectValleyCopperVeinCandidates_FiltersSortsAndKeepsPoolMetadata()
    {
        var candidates = GatheringRouteSelection.SelectValleyCopperVeinCandidates(
        [
            (entry: 1731u, map: 1, x: -910f, y: -4610f, z: 26f, distance2D: 181.8f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)"),
            (entry: 1731u, map: 1, x: -983f, y: -4436f, z: 34f, distance2D: 193.7f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)"),
            (entry: 1731u, map: 1, x: -1000f, y: -4500f, z: 28f, distance2D: 200.0f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)"),
            (entry: 1731u, map: 1, x: -718f, y: -4696f, z: 38f, distance2D: 212.7f, poolEntry: 2048u, poolDescription: "Copper Veins - Valley"),
            (entry: 9999u, map: 1, x: -700f, y: -4500f, z: 30f, distance2D: 100.0f, poolEntry: null, poolDescription: null),
            (entry: 1731u, map: 0, x: -700f, y: -4500f, z: 30f, distance2D: 90.0f, poolEntry: null, poolDescription: null)
        ],
            nodeEntry: 1731u,
            maxDistance: 220f);

        Assert.Equal(4, candidates.Count);
        Assert.True(candidates.SequenceEqual(candidates.OrderBy(candidate => candidate.distance2D)));
        Assert.All(candidates, candidate => Assert.Equal(GatheringRouteSelection.DurotarMap, candidate.map));
        Assert.Equal(181.8f, candidates[0].distance2D);
        Assert.Equal(193.7f, candidates[1].distance2D);
        Assert.Equal(200.0f, candidates[2].distance2D);
        Assert.Equal(1024u, candidates[0].poolEntry);
        Assert.Equal("Copper Veins - Valley", candidates[3].poolDescription);
    }

    [Fact]
    public void SelectValleyCopperVeinCandidates_RejectsOutOfRangeCandidates()
    {
        var candidates = GatheringRouteSelection.SelectValleyCopperVeinCandidates(
        [
            (entry: 1731u, map: 1, x: -900f, y: -4600f, z: 25f, distance2D: 181.8f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)"),
            (entry: 1731u, map: 1, x: -666f, y: -4858f, z: 39f, distance2D: 382.5f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)")
        ],
            nodeEntry: 1731u,
            maxDistance: 260f);

        Assert.Single(candidates);
        Assert.Equal(181.8f, candidates[0].distance2D);
    }

    [Fact]
    public void SelectValleyCopperVeinCandidates_CanStillApplyExplicitCap()
    {
        var candidates = GatheringRouteSelection.SelectValleyCopperVeinCandidates(
        [
            (entry: 1731u, map: 1, x: -910f, y: -4610f, z: 26f, distance2D: 181.8f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)"),
            (entry: 1731u, map: 1, x: -983f, y: -4436f, z: 34f, distance2D: 193.7f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)"),
            (entry: 1731u, map: 1, x: -1000f, y: -4500f, z: 28f, distance2D: 200.0f, poolEntry: 1024u, poolDescription: "Copper Veins - Durotar (Master Pool)")
        ],
            nodeEntry: 1731u,
            maxDistance: 220f,
            maxCandidates: 2);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(181.8f, candidates[0].distance2D);
        Assert.Equal(193.7f, candidates[1].distance2D);
    }

    [Fact]
    public void SelectDurotarHerbCandidates_FiltersSortsMultipleEntries()
    {
        var candidates = GatheringRouteSelection.SelectDurotarHerbCandidates(
        [
            (entry: 1617u, map: 1, x: -514f, y: -4869f, z: 36f, distance2D: 70.6f, poolEntry: 1022u, poolDescription: "Silverleaf in Durotar"),
            (entry: 1618u, map: 1, x: -453f, y: -4824f, z: 38f, distance2D: 52.5f, poolEntry: 1020u, poolDescription: "Peacebloom in Durotar"),
            (entry: 1619u, map: 1, x: -460f, y: -4712f, z: 37f, distance2D: 96.5f, poolEntry: 1021u, poolDescription: "Earthroot in Durotar"),
            (entry: 9999u, map: 1, x: -400f, y: -4800f, z: 30f, distance2D: 100.0f, poolEntry: null, poolDescription: null),
            (entry: 1617u, map: 0, x: -500f, y: -4800f, z: 30f, distance2D: 10.0f, poolEntry: null, poolDescription: null)
        ],
            nodeEntries: [1617u, 1618u, 1619u],
            maxDistance: 300f);

        Assert.Equal(3, candidates.Count);
        Assert.True(candidates.SequenceEqual(candidates.OrderBy(c => c.distance2D)));
        Assert.All(candidates, c => Assert.Equal(GatheringRouteSelection.DurotarMap, c.map));
        Assert.Equal(52.5f, candidates[0].distance2D);
        Assert.Equal("Peacebloom in Durotar", candidates[0].poolDescription);
        Assert.Equal("Silverleaf in Durotar", candidates[1].poolDescription);
        Assert.Equal("Earthroot in Durotar", candidates[2].poolDescription);
    }

    [Fact]
    public void SelectDurotarHerbCandidates_RejectsOutOfRangeCandidates()
    {
        var candidates = GatheringRouteSelection.SelectDurotarHerbCandidates(
        [
            (entry: 1618u, map: 1, x: -453f, y: -4824f, z: 38f, distance2D: 52.5f, poolEntry: 1020u, poolDescription: "Peacebloom in Durotar"),
            (entry: 1617u, map: 1, x: -194f, y: -4635f, z: 42f, distance2D: 346.6f, poolEntry: 1022u, poolDescription: "Silverleaf in Durotar")
        ],
            nodeEntries: [1617u, 1618u],
            maxDistance: 300f);

        Assert.Single(candidates);
        Assert.Equal(52.5f, candidates[0].distance2D);
    }

    [Fact]
    public void SelectDurotarHerbCandidates_CanApplyExplicitCap()
    {
        var candidates = GatheringRouteSelection.SelectDurotarHerbCandidates(
        [
            (entry: 1618u, map: 1, x: -453f, y: -4824f, z: 38f, distance2D: 52.5f, poolEntry: 1020u, poolDescription: "Peacebloom in Durotar"),
            (entry: 1617u, map: 1, x: -514f, y: -4869f, z: 36f, distance2D: 70.6f, poolEntry: 1022u, poolDescription: "Silverleaf in Durotar"),
            (entry: 1619u, map: 1, x: -460f, y: -4712f, z: 37f, distance2D: 96.5f, poolEntry: 1021u, poolDescription: "Earthroot in Durotar")
        ],
            nodeEntries: [1617u, 1618u, 1619u],
            maxDistance: 300f,
            maxCandidates: 2);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(52.5f, candidates[0].distance2D);
        Assert.Equal(70.6f, candidates[1].distance2D);
    }
}
