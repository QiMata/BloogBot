using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Tests.LiveValidation.Harness;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for <see cref="BakeFixtureRecorder"/>. Verifies the
/// settle-Z auto-classification heuristic and on-disk fixture round-trip
/// using an in-memory <see cref="RecorderMockHost"/>.
/// </summary>
public class BakeFixtureRecorderTests
{
    private const string FgAccount = "FG1";

    [Fact]
    public async Task Record_ClassifiesSmallDzAsWalkable()
    {
        var host = new RecorderMockHost();
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 49.9f, null);

        var input = MakeInput(("plat", 100f, 200f, 50f));
        var path = TempFixturePath();
        var recorder = new BakeFixtureRecorder(host, settleDelay: TimeSpan.Zero);

        var fixture = await recorder.RecordAsync(input, FgAccount, path);

        try
        {
            Assert.Single(fixture.ExpectedWalkable);
            Assert.Empty(fixture.ExpectedHoles);
            // Walkable z is pinned to the settled value, not the request.
            Assert.Equal(49.9f, fixture.ExpectedWalkable[0].Xyz[2], 4);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Record_ClassifiesLargeDzAsHole_PinsExpectedSettleZ()
    {
        var host = new RecorderMockHost();
        // Bot fell from request 80 → settled 60. dz=20y → hole.
        host.SettleResults[(FgAccount, 110f, 200f, 80f)] = new SettledPosition(110f, 200f, 60f, null);

        var input = MakeInput(("cliff", 110f, 200f, 80f));
        var path = TempFixturePath();
        var recorder = new BakeFixtureRecorder(host, settleDelay: TimeSpan.Zero);

        var fixture = await recorder.RecordAsync(input, FgAccount, path);

        try
        {
            Assert.Empty(fixture.ExpectedWalkable);
            var hole = Assert.Single(fixture.ExpectedHoles);
            Assert.Equal(80f, hole.Xyz[2], 4); // original request preserved
            Assert.Equal(60f, hole.ExpectedSettleZ, 4); // observed settle pinned
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Record_ThresholdBoundary_ExactlyAtThresholdIsWalkable()
    {
        var host = new RecorderMockHost();
        // dz exactly = WalkableDzThresholdY (1.5). Threshold is inclusive.
        host.SettleResults[(FgAccount, 0f, 0f, 100f)] = new SettledPosition(0f, 0f, 98.5f, null);

        var input = MakeInput(("at-boundary", 0f, 0f, 100f));
        var path = TempFixturePath();
        var recorder = new BakeFixtureRecorder(host, settleDelay: TimeSpan.Zero);

        var fixture = await recorder.RecordAsync(input, FgAccount, path);

        try
        {
            Assert.Single(fixture.ExpectedWalkable);
            Assert.Empty(fixture.ExpectedHoles);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Record_SkipsCandidateWhenSettleMissing()
    {
        var host = new RecorderMockHost();
        // No SettleResults entry for this candidate — host returns null.
        var input = MakeInput(("offline", 0f, 0f, 0f));
        var path = TempFixturePath();
        var recorder = new BakeFixtureRecorder(host, settleDelay: TimeSpan.Zero);

        var fixture = await recorder.RecordAsync(input, FgAccount, path);

        try
        {
            Assert.Empty(fixture.ExpectedWalkable);
            Assert.Empty(fixture.ExpectedHoles);
            Assert.Contains(host.LogLines, l => l.Contains("no settle"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Record_WritesFixtureFileThatLoadsBack()
    {
        var host = new RecorderMockHost();
        host.SettleResults[(FgAccount, 100f, 200f, 50f)] = new SettledPosition(100f, 200f, 50f, null);
        host.SettleResults[(FgAccount, 110f, 200f, 80f)] = new SettledPosition(110f, 200f, 50f, null);

        var input = MakeInput(
            ("plat", 100f, 200f, 50f),
            ("cliff", 110f, 200f, 80f));

        var path = TempFixturePath();
        var recorder = new BakeFixtureRecorder(host, settleDelay: TimeSpan.Zero);

        try
        {
            await recorder.RecordAsync(input, FgAccount, path);
            var loaded = BakeFixtureLoader.LoadFromPath(path);

            Assert.Equal("RecordedRoute", loaded.Route);
            Assert.Single(loaded.ExpectedWalkable);
            Assert.Single(loaded.ExpectedHoles);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Record_DrivesScreenshotCaptureWhenScreenshotDirSet()
    {
        var host = new RecorderMockHost();
        host.SettleResults[(FgAccount, 0f, 0f, 0f)] = new SettledPosition(0f, 0f, 0.1f, null);
        var input = MakeInput(("only-cand", 0f, 0f, 0f));
        var path = TempFixturePath();

        var recorder = new BakeFixtureRecorder(
            host,
            settleDelay: TimeSpan.Zero,
            screenshotDir: "out");

        try
        {
            await recorder.RecordAsync(input, FgAccount, path);
            Assert.Single(host.CaptureCalls);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Record_RejectsEmptyCandidateList()
    {
        var host = new RecorderMockHost();
        var input = new BakeFixtureRecorderInput(
            RouteName: "Empty",
            MapId: 0,
            Endpoints: new BakeFixtureEndpoints(new[] { 0f, 0f, 0f }, new[] { 1f, 1f, 1f }),
            Candidates: Array.Empty<BakeFixtureRecorderCandidate>());
        var recorder = new BakeFixtureRecorder(host, settleDelay: TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await recorder.RecordAsync(input, FgAccount, TempFixturePath());
        });
    }

    private static BakeFixtureRecorderInput MakeInput(params (string Label, float X, float Y, float Z)[] candidates)
    {
        return new BakeFixtureRecorderInput(
            RouteName: "RecordedRoute",
            MapId: 0,
            Endpoints: new BakeFixtureEndpoints(new[] { 0f, 0f, 0f }, new[] { 1f, 1f, 1f }),
            Candidates: candidates
                .Select(c => new BakeFixtureRecorderCandidate(c.Label, new[] { c.X, c.Y, c.Z }))
                .ToList());
    }

    private static string TempFixturePath()
        => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");

    /// <summary>
    /// Minimal mock host for the recorder. Mirrors the validator-tests'
    /// <c>MockBakeValidationHost</c> shape but only exercises the methods
    /// the recorder calls.
    /// </summary>
    private sealed class RecorderMockHost : IBakeValidationHost
    {
        public Dictionary<(string Account, float X, float Y, float Z), SettledPosition?> SettleResults { get; } = new();
        public List<string> LogLines { get; } = new();
        public List<(string Label, string Account)> CaptureCalls { get; } = new();

        public Task<SettledPosition?> TeleportAndSettleAsync(
            string accountName, uint mapId, float x, float y, float z,
            TimeSpan settleDelay, CancellationToken ct)
        {
            SettleResults.TryGetValue((accountName, x, y, z), out var pos);
            return Task.FromResult(pos);
        }

        public Task<float[][]?> QuerySmoothPathAsync(uint mapId, float[] start, float[] dest, CancellationToken ct)
            => Task.FromResult<float[][]?>(null);

        public Task<string?> ClassifySegmentAsync(uint mapId, float[] a, float[] b, CancellationToken ct)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<string>> CaptureMultiAngleAsync(
            string accountName, string baseLabel, uint mapId,
            float settledX, float settledY, float settledZ,
            string outputDir, CancellationToken ct)
        {
            CaptureCalls.Add((baseLabel, accountName));
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public void Log(string message) => LogLines.Add(message);
    }
}
