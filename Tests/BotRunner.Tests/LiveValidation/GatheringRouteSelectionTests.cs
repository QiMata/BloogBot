using System.Linq;

namespace BotRunner.Tests.LiveValidation;

public class GatheringRouteSelectionTests
{
    [Fact]
    public void SelectValleyCopperVeinCandidates_FiltersSortsAndLimits()
    {
        var candidates = GatheringRouteSelection.SelectValleyCopperVeinCandidates(
        [
            (entry: 1731u, map: 1, x: -910f, y: -4610f, z: 26f, distance2D: 181.8f),
            (entry: 1731u, map: 1, x: -983f, y: -4436f, z: 34f, distance2D: 193.7f),
            (entry: 1731u, map: 1, x: -1000f, y: -4500f, z: 28f, distance2D: 200.0f),
            (entry: 1731u, map: 1, x: -718f, y: -4696f, z: 38f, distance2D: 212.7f),
            (entry: 9999u, map: 1, x: -700f, y: -4500f, z: 30f, distance2D: 100.0f),
            (entry: 1731u, map: 0, x: -700f, y: -4500f, z: 30f, distance2D: 90.0f)
        ],
            nodeEntry: 1731u,
            maxDistance: 220f,
            maxCandidates: 3);

        Assert.Equal(3, candidates.Count);
        Assert.True(candidates.SequenceEqual(candidates.OrderBy(candidate => candidate.distance2D)));
        Assert.All(candidates, candidate => Assert.Equal(GatheringRouteSelection.DurotarMap, candidate.map));
        Assert.Equal(181.8f, candidates[0].distance2D);
        Assert.Equal(193.7f, candidates[1].distance2D);
        Assert.Equal(200.0f, candidates[2].distance2D);
    }

    [Fact]
    public void SelectValleyCopperVeinCandidates_RejectsOutOfRangeCandidates()
    {
        var candidates = GatheringRouteSelection.SelectValleyCopperVeinCandidates(
        [
            (entry: 1731u, map: 1, x: -900f, y: -4600f, z: 25f, distance2D: 181.8f),
            (entry: 1731u, map: 1, x: -666f, y: -4858f, z: 39f, distance2D: 382.5f)
        ],
            nodeEntry: 1731u,
            maxDistance: 260f,
            maxCandidates: 6);

        Assert.Single(candidates);
        Assert.Equal(181.8f, candidates[0].distance2D);
    }
}
