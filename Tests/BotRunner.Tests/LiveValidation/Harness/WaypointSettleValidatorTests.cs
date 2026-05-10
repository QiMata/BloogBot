using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Tests.LiveValidation.Harness;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for <see cref="WaypointSettleValidator"/>. All tests use the
/// in-memory <see cref="MockBakeValidationHost"/> instead of a live FG/BG
/// bot, so the suite doesn't require WoW.exe / StateManager / Pathfinding-
/// Service. Verifies the failure-classification taxonomy against the seven
/// canonical bug shapes.
/// </summary>
public class WaypointSettleValidatorTests
{
    private const string FgAccount = "FG1";
    private const string BgAccount = "BG1";

    [Fact]
    public async Task AllChecksGreen_NoFailures()
    {
        var fixture = MakeFixture(
            walkable: new[] { ("trail-mid", 100f, 200f, 50f) },
            holes: new[] { ("cliff", 110f, 200f, 80f, 60f) }, // expectedSettleZ=60
            golden: new BakeFixtureSmoothPathExpectation(2, 0, 1f));

        var host = new MockBakeValidationHost();
        // Walkable at the requested coord.
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 50.1f, 0xABCDul);
        // Hole — bot falls to z=60.
        host.SettleResults[(FgAccount, 110f, 200f, 80f)] = new SettledPosition(110f, 200f, 60.05f, 0x1111ul);

        host.SmoothPaths[(0, 0f, 0f, 0f, 1f, 1f, 1f)] = new[]
        {
            new[] { 0f, 0f, 0f },
            new[] { 1f, 1f, 1f },
        };
        host.SegmentClassifications.Add("Walk");

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.True(report.Passed, string.Join("; ", report.Failures.Select(f => f.Message)));
        Assert.Empty(report.Failures);
        Assert.Equal("OK", report.Walkable[0].Status);
        Assert.Equal("OK", report.Holes[0].Status);
    }

    [Fact]
    public async Task BakeRegressionWalkableLost_WhenSettleZDriftsBeyondTolerance()
    {
        var fixture = MakeFixture(
            walkable: new[] { ("regressed-platform", 100f, 200f, 50f) });

        var host = new MockBakeValidationHost();
        // Bot fell 6y instead of staying on the platform — the walkable
        // polygon disappeared.
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 44f, null);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        var f = Assert.Single(report.Failures);
        Assert.Equal(BakeValidationFailureKinds.BakeRegressionWalkableLost, f.Kind);
        Assert.Equal("regressed-platform", f.Label);
        Assert.Equal("FAILED", report.Walkable[0].Status);
    }

    [Fact]
    public async Task PhantomPoly_WhenBotStaysOnHoleCoord()
    {
        var fixture = MakeFixture(
            holes: new[] { ("brm-south-trap", -7949.7f, -1162.8f, 170.8f, 158f) });

        var host = new MockBakeValidationHost();
        // Bot stayed on the trap polygon at z=170.8 — phantom poly.
        host.SettleResults[(FgAccount, -7949.7f, -1162.8f, 170.8f)] =
            new SettledPosition(-7949.7f, -1162.8f, 170.78f, 0xCAFEul);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        var f = Assert.Single(report.Failures);
        Assert.Equal(BakeValidationFailureKinds.PhantomPoly, f.Kind);
        Assert.Equal("brm-south-trap", f.Label);
        Assert.Equal("FAILED", report.Holes[0].Status);
    }

    [Fact]
    public async Task PhantomPoly_NotFiredWhenBotFallsToExpectedZ()
    {
        var fixture = MakeFixture(
            holes: new[] { ("cliff", 0f, 0f, 100f, 50f) });

        var host = new MockBakeValidationHost();
        // Bot fell from 100 to 50.4 — within ±1y default tolerance of expectedSettleZ=50.
        host.SettleResults[(FgAccount, 0f, 0f, 100f)] = new SettledPosition(0f, 0f, 50.4f, 0x1ul);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.True(report.Passed);
        Assert.Equal("OK", report.Holes[0].Status);
    }

    [Fact]
    public async Task TeleportFailed_WhenSettlePositionMissing()
    {
        var fixture = MakeFixture(walkable: new[] { ("offline", 0f, 0f, 0f) });
        var host = new MockBakeValidationHost();
        // No SettleResults entry → host returns null.

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        var f = Assert.Single(report.Failures);
        Assert.Equal(BakeValidationFailureKinds.TeleportFailed, f.Kind);
        Assert.Equal("MISSING_SAMPLE", report.Walkable[0].Status);
    }

    [Fact]
    public async Task TeleportFailed_WhenSettleXyDriftsTooFar()
    {
        var fixture = MakeFixture(walkable: new[] { ("drift", 100f, 200f, 50f) });
        var host = new MockBakeValidationHost();
        // Bot teleported but ended up 12y off in XY. Considered teleport
        // fail (test cannot evaluate the bake at this point).
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(112f, 200f, 50f, null);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        var f = Assert.Single(report.Failures);
        Assert.Equal(BakeValidationFailureKinds.TeleportFailed, f.Kind);
    }

    [Fact]
    public async Task FgBgParityBreak_FiresWhenBgDisagreesByMoreThanTolerance()
    {
        var fixture = MakeFixture(walkable: new[] { ("plat", 100f, 200f, 50f) });
        var host = new MockBakeValidationHost();
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 50.0f, null);
        // BG settled 0.6y lower in Z — exceeds 0.3y per-axis parity.
        host.SettleResults[(BgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 50.6f, null);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount, BgAccount);

        Assert.False(report.Passed);
        var f = Assert.Single(report.Failures);
        Assert.Equal(BakeValidationFailureKinds.FgBgParityBreak, f.Kind);
        Assert.Contains("dz=0.60", f.Message);
    }

    [Fact]
    public async Task FgBgParity_GreenWhenWithinTolerance()
    {
        var fixture = MakeFixture(walkable: new[] { ("plat", 100f, 200f, 50f) });
        var host = new MockBakeValidationHost();
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 50.00f, null);
        host.SettleResults[(BgAccount, 100f, 200f, 50f)] = new SettledPosition(100.2f, 200.1f, 50.15f, null);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount, BgAccount);

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task WaypointCountDrift_FiresWhenPathTooShortOrLong()
    {
        var fixture = MakeFixture(
            walkable: Array.Empty<(string, float, float, float)>(),
            golden: new BakeFixtureSmoothPathExpectation(WaypointCount: 100, WaypointTolerance: 5, EndpointToleranceY: 1f));

        var host = new MockBakeValidationHost();
        // Path too short — only 2 waypoints, expected 100±5.
        host.SmoothPaths[(0, 0f, 0f, 0f, 1f, 1f, 1f)] = new[]
        {
            new[] { 0f, 0f, 0f },
            new[] { 1f, 1f, 1f },
        };

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        Assert.Contains(report.Failures, f => f.Kind == BakeValidationFailureKinds.WaypointCountDrift);
    }

    [Fact]
    public async Task EndpointMiss_FiresWhenFinalWpFarFromDest()
    {
        var fixture = MakeFixture(
            walkable: Array.Empty<(string, float, float, float)>(),
            golden: new BakeFixtureSmoothPathExpectation(2, 5, EndpointToleranceY: 1f));
        var host = new MockBakeValidationHost();
        // Final WP is 5y from declared dest=(1,1,1) — fail.
        host.SmoothPaths[(0, 0f, 0f, 0f, 1f, 1f, 1f)] = new[]
        {
            new[] { 0f, 0f, 0f },
            new[] { 5f, 1f, 1f },
        };

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        Assert.Contains(report.Failures, f => f.Kind == BakeValidationFailureKinds.EndpointMiss);
    }

    [Fact]
    public async Task UnsafeAffordance_FiresOnCliffSegment()
    {
        var fixture = MakeFixture(
            walkable: Array.Empty<(string, float, float, float)>(),
            golden: new BakeFixtureSmoothPathExpectation(3, 5, 1f));
        var host = new MockBakeValidationHost();
        host.SmoothPaths[(0, 0f, 0f, 0f, 1f, 1f, 1f)] = new[]
        {
            new[] { 0f, 0f, 0f },
            new[] { 0.5f, 0.5f, 0.5f },
            new[] { 1f, 1f, 1f },
        };
        // First segment Walk; second segment Cliff.
        host.SegmentClassifications.AddRange(new[] { "Walk", "Cliff" });

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.False(report.Passed);
        var unsafeFailure = Assert.Single(
            report.Failures.Where(f => f.Kind == BakeValidationFailureKinds.UnsafeAffordance));
        Assert.Contains("Cliff", unsafeFailure.Actual);
        Assert.NotNull(report.Affordance);
        Assert.Equal(1, report.Affordance!.UnsafeSegmentCount);
        Assert.Equal(1, report.Affordance.FirstUnsafeIndex);
    }

    [Fact]
    public async Task SmoothPathSkippedWhenHostReturnsNull()
    {
        var fixture = MakeFixture(
            walkable: Array.Empty<(string, float, float, float)>(),
            golden: new BakeFixtureSmoothPathExpectation(100, 5, 1f));
        var host = new MockBakeValidationHost();
        // No SmoothPaths entry → host returns null → skip golden checks.

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero);
        var report = await validator.ValidateAsync(FgAccount);

        Assert.True(report.Passed);
        Assert.Null(report.SmoothPath);
    }

    [Fact]
    public async Task ReportSerializesAndIsReadable()
    {
        var fixture = MakeFixture(walkable: new[] { ("plat", 100f, 200f, 50f) });
        var host = new MockBakeValidationHost();
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 50.05f, 0xABCDul);

        var validator = new WaypointSettleValidator(fixture, host, settleDelay: TimeSpan.Zero,
            clock: () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
        var report = await validator.ValidateAsync(FgAccount);

        var json = BakeValidationReportSerializer.Serialize(report);
        Assert.Contains("\"route\": \"TestRoute\"", json);
        Assert.Contains("\"timestampUtc\": \"2026-05-10T12:00:00Z\"", json);
        Assert.Contains("\"passed\": true", json);
    }

    private static BakeFixture MakeFixture(
        IEnumerable<(string Label, float X, float Y, float Z)>? walkable = null,
        IEnumerable<(string Label, float X, float Y, float Z, float ExpectedSettleZ)>? holes = null,
        BakeFixtureSmoothPathExpectation? golden = null)
    {
        var w = (walkable ?? Array.Empty<(string, float, float, float)>())
            .Select(t => new BakeFixtureCheckpoint(t.Label, new[] { t.X, t.Y, t.Z }, 0.5f, null))
            .ToList();
        var h = (holes ?? Array.Empty<(string, float, float, float, float)>())
            .Select(t => new BakeFixtureHole(t.Label, new[] { t.X, t.Y, t.Z }, t.ExpectedSettleZ, 1f, null))
            .ToList();
        return new BakeFixture(
            Route: "TestRoute",
            Description: "unit fixture",
            MapId: 0,
            Agent: null,
            Endpoints: new BakeFixtureEndpoints(new[] { 0f, 0f, 0f }, new[] { 1f, 1f, 1f }),
            ExpectedWalkable: w,
            ExpectedHoles: h,
            GoldenSmoothPath: golden,
            TileInvariants: null);
    }

    /// <summary>
    /// Mock host that drives <see cref="WaypointSettleValidator"/> without
    /// any live infrastructure. Tests populate <see cref="SettleResults"/>,
    /// <see cref="SmoothPaths"/>, and <see cref="SegmentClassifications"/>
    /// before calling the validator.
    /// </summary>
    private sealed class MockBakeValidationHost : IBakeValidationHost
    {
        public Dictionary<(string Account, float X, float Y, float Z), SettledPosition?> SettleResults { get; } = new();
        public Dictionary<(uint MapId, float SX, float SY, float SZ, float DX, float DY, float DZ), float[][]?> SmoothPaths { get; } = new();
        public List<string?> SegmentClassifications { get; } = new();
        public List<string> LogLines { get; } = new();

        private int _segmentIdx;

        public Task<SettledPosition?> TeleportAndSettleAsync(
            string accountName, uint mapId, float x, float y, float z,
            TimeSpan settleDelay, CancellationToken ct)
        {
            SettleResults.TryGetValue((accountName, x, y, z), out var pos);
            return Task.FromResult(pos);
        }

        public Task<float[][]?> QuerySmoothPathAsync(uint mapId, float[] start, float[] dest, CancellationToken ct)
        {
            SmoothPaths.TryGetValue((mapId, start[0], start[1], start[2], dest[0], dest[1], dest[2]), out var path);
            return Task.FromResult<float[][]?>(path);
        }

        public Task<string?> ClassifySegmentAsync(uint mapId, float[] a, float[] b, CancellationToken ct)
        {
            if (_segmentIdx < SegmentClassifications.Count)
                return Task.FromResult(SegmentClassifications[_segmentIdx++]);
            return Task.FromResult<string?>(null);
        }

        public void Log(string message) => LogLines.Add(message);
    }
}
