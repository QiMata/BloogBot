using GameData.Core.Models;
using PathfindingService.Repository;
using Xunit;

namespace PathfindingService.Tests;

public class RawPathContractTests
{
    [Fact]
    public void CalculateRawPath_PathEndingNearStart_SurfacesEndProjectionBlockedReason()
    {
        var navigation = new Navigation(
            findPathResolver: (_, start, _, _) =>
            [
                start,
                new XYZ(start.X + 0.9f, start.Y - 0.7f, start.Z + 1.2f)
            ],
            segmentBlocker: null,
            segmentBlockerReasonResolver: null);

        var start = new XYZ(1351.3f, -4526.3f, 34.5f);
        var end = new XYZ(1320.1f, -4653.2f, 53.9f);

        var result = navigation.CalculateRawPath(1, start, end, smoothPath: true, agentRadius: 1.0247f, agentHeight: 2.625f);

        Assert.Equal("raw_detour", result.Result);
        Assert.Equal(0, result.BlockedSegmentIndex);
        Assert.StartsWith("end_projection:", result.BlockedReason, System.StringComparison.Ordinal);
        Assert.Equal(2, result.Path.Length);
        Assert.Equal(result.Path, result.RawPath);
    }

    [Fact]
    public void CalculateRawPath_PathAnchoredToRequestedEndpoints_KeepsBlockedReasonNone()
    {
        var navigation = new Navigation(
            findPathResolver: (_, start, end, _) =>
            [
                start,
                new XYZ((start.X + end.X) / 2f, (start.Y + end.Y) / 2f, (start.Z + end.Z) / 2f),
                end
            ],
            segmentBlocker: null,
            segmentBlockerReasonResolver: null);

        var start = new XYZ(10f, 20f, 30f);
        var end = new XYZ(18f, 28f, 30f);

        var result = navigation.CalculateRawPath(1, start, end, smoothPath: true, agentRadius: 1.0247f, agentHeight: 2.625f);

        Assert.Equal("raw_detour", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal("none", result.BlockedReason);
        Assert.Equal(3, result.Path.Length);
        Assert.Equal(result.Path, result.RawPath);
    }
}
