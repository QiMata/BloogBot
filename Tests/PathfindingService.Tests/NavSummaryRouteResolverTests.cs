using GameData.Core.Models;
using PathfindingService.NavSummary;
using PathfindingService.Repository;
using PathfindingService.RouteCaching;

namespace PathfindingService.Tests;

public sealed class NavSummaryRouteResolverTests
{
    [Fact]
    public void TryResolve_ExpandsSummaryAnchorsThroughDetailedResolver()
    {
        var resolver = CreateResolver(CreateLinearGraph());
        var calls = new List<(XYZ From, XYZ To)>();

        var success = resolver.TryResolve(
            new NavSummaryRouteRequest(
                MapId: 1,
                Start: new XYZ(-5f, 0f, 0f),
                End: new XYZ(105f, 0f, 0f),
                SmoothPath: true,
                AgentRadius: 1f,
                AgentHeight: 2f,
                HorizontalDistance: 110f,
                DynamicOverlayCount: 0),
            (from, to) =>
            {
                calls.Add((from, to));
                return SuccessSegment(from, to);
            },
            out var resolution);

        Assert.True(success);
        Assert.Equal("nav_summary_expanded", resolution.PathResult.Result);
        Assert.Equal("summary-linear", resolution.Match.GraphId);
        Assert.Equal(3, resolution.Match.AnchorCount);
        Assert.Equal(4, resolution.Match.SegmentCount);
        Assert.Equal(4, calls.Count);
        Assert.Equal(-5f, resolution.PathResult.Path[0].X);
        Assert.Equal(105f, resolution.PathResult.Path[^1].X);
        Assert.Equal("none", resolution.PathResult.BlockedReason);
    }

    [Fact]
    public void TryResolve_BypassesDynamicOverlayRequests()
    {
        var resolver = CreateResolver(CreateLinearGraph());
        var calls = 0;

        var success = resolver.TryResolve(
            new NavSummaryRouteRequest(
                MapId: 1,
                Start: new XYZ(-5f, 0f, 0f),
                End: new XYZ(105f, 0f, 0f),
                SmoothPath: true,
                AgentRadius: 1f,
                AgentHeight: 2f,
                HorizontalDistance: 110f,
                DynamicOverlayCount: 1),
            (from, to) =>
            {
                calls++;
                return SuccessSegment(from, to);
            },
            out _);

        Assert.False(success);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void TryResolve_FallsBackWhenAnyDetailedSegmentFails()
    {
        var resolver = CreateResolver(CreateLinearGraph());
        var calls = 0;

        var success = resolver.TryResolve(
            new NavSummaryRouteRequest(
                MapId: 1,
                Start: new XYZ(-5f, 0f, 0f),
                End: new XYZ(105f, 0f, 0f),
                SmoothPath: true,
                AgentRadius: 1f,
                AgentHeight: 2f,
                HorizontalDistance: 110f,
                DynamicOverlayCount: 0),
            (from, to) =>
            {
                calls++;
                return calls == 2
                    ? new NavigationPathResult([], [], "no_path", null, "none")
                    : SuccessSegment(from, to);
            },
            out _);

        Assert.False(success);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ApplyToRouteAlgorithmSignature_IncludesGraphSignatureOnlyWhenEnabled()
    {
        var enabledResolver = CreateResolver(CreateLinearGraph());
        var disabledResolver = new NavSummaryRouteResolver(
            new NavSummaryOptions { Enabled = false },
            NavSummaryGraphStore.Empty);

        var enabledSignature = enabledResolver.ApplyToRouteAlgorithmSignature(RouteResultCache.RouteAlgorithmSignature);
        var disabledSignature = disabledResolver.ApplyToRouteAlgorithmSignature(RouteResultCache.RouteAlgorithmSignature);

        Assert.Contains("NavSummary.v1:", enabledSignature, StringComparison.Ordinal);
        Assert.Equal(RouteResultCache.RouteAlgorithmSignature, disabledSignature);
    }

    private static NavSummaryRouteResolver CreateResolver(NavSummaryGraph graph)
        => new(
            new NavSummaryOptions
            {
                Enabled = true,
                MinDistance = 1f,
                MaxAnchorDistance = 20f,
                MaxDetailEndpointDistance = 5f,
            },
            NavSummaryGraphStore.FromGraphs([graph]));

    private static NavSummaryGraph CreateLinearGraph()
        => new()
        {
            Id = "summary-linear",
            MapId = 1,
            Nodes =
            [
                new NavSummaryNode { Id = "a", X = 0f, Y = 0f, Z = 0f },
                new NavSummaryNode { Id = "b", X = 50f, Y = 0f, Z = 0f },
                new NavSummaryNode { Id = "c", X = 100f, Y = 0f, Z = 0f },
            ],
            Edges =
            [
                new NavSummaryEdge { From = "a", To = "b", Cost = 50f },
                new NavSummaryEdge { From = "b", To = "c", Cost = 50f },
            ],
        };

    private static NavigationPathResult SuccessSegment(XYZ from, XYZ to)
    {
        var midpoint = new XYZ(
            (from.X + to.X) * 0.5f,
            (from.Y + to.Y) * 0.5f,
            (from.Z + to.Z) * 0.5f);
        var path = new[] { from, midpoint, to };
        return new NavigationPathResult(path, path, "native_path", null, "none");
    }
}
