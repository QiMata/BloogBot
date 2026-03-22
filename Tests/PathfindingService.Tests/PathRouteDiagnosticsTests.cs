using GameData.Core.Models;

namespace PathfindingService.Tests;

public class PathRouteDiagnosticsTests
{
    [Fact]
    public void ShouldLogRoute_ReturnsTrue_ForShortRouteEvenWhenHealthy()
    {
        var shouldLog = PathRouteDiagnostics.ShouldLogRoute(
            distance2D: 22.0f,
            sanitizedCornerCount: 4,
            rawCornerCount: 4,
            result: "native_path",
            requestOrdinal: 2);

        Assert.True(shouldLog);
        Assert.Equal(
            "short_route",
            PathRouteDiagnostics.GetReason(22.0f, 4, 4, "native_path", 2));
    }

    [Fact]
    public void ShouldLogRoute_ReturnsFalse_ForHealthyLongUnsampledRoute()
    {
        var shouldLog = PathRouteDiagnostics.ShouldLogRoute(
            distance2D: 180.0f,
            sanitizedCornerCount: 5,
            rawCornerCount: 5,
            result: "native_path",
            requestOrdinal: 2);

        Assert.False(shouldLog);
        Assert.Equal(
            "none",
            PathRouteDiagnostics.GetReason(180.0f, 5, 5, "native_path", 2));
    }

    [Fact]
    public void GetReason_IncludesAllRelevantDiagnosticSignals()
    {
        var reason = PathRouteDiagnostics.GetReason(
            distance2D: 18.0f,
            sanitizedCornerCount: 1,
            rawCornerCount: 3,
            result: "blocked_by_dynamic_overlay",
            requestOrdinal: 2);

        Assert.Equal(
            "sparse_result,short_route,blocked_by_dynamic_overlay,sanitized_corners",
            reason);
    }

    [Fact]
    public void FormatCorners_TruncatesAfterConfiguredLimit()
    {
        var corners = Enumerable.Range(0, PathRouteDiagnostics.MaxLoggedCorners + 2)
            .Select(i => new XYZ(i, i + 0.25f, i + 0.5f))
            .ToArray();

        var formatted = PathRouteDiagnostics.FormatCorners(corners);

        Assert.StartsWith("(0.0,0.2,0.5) -> (1.0,1.2,1.5)", formatted);
        Assert.EndsWith("-> ...", formatted);
    }
}
