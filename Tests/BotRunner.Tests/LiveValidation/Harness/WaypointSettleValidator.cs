using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — bake-validation harness orchestrator.
///
/// Walks a <see cref="BakeFixture"/>'s expected-walkable, expected-holes,
/// smooth-path, and segment-affordance expectations against a live FG bot
/// (and optionally a paired BG bot). Emits a structured
/// <see cref="BakeValidationReport"/> with one failure per regression.
///
/// Failure-kind taxonomy (<see cref="BakeValidationFailureKinds"/>):
///
///   BAKE_REGRESSION_WALKABLE_LOST — an expectedWalkable point's settle
///     Z drifted beyond <c>settleToleranceY</c>. The walkable polygon
///     disappeared since the fixture was recorded.
///   PHANTOM_POLY — an expectedHole point's settle Z is at the original
///     xyz Z (or above the expectedSettleZ + tolerance), meaning the bot
///     stood on a polygon the fixture says shouldn't exist (the BRM
///     z=171.24 class of bug).
///   WAYPOINT_COUNT_DRIFT / ENDPOINT_MISS — smooth-path shape regressed.
///   UNSAFE_AFFORDANCE — at least one (WP[i] → WP[i+1]) segment classifier
///     returned Cliff / UnsafeDrop / Blocked.
///   FG_BG_PARITY_BREAK — FG and BG settled on the same checkpoint with
///     |dx|, |dy|, or |dz| > <c>FgBgParityToleranceY</c>.
///   TELEPORT_FAILED — settled position is missing or far from the
///     teleport target horizontally (fixture cannot be evaluated).
/// </summary>
public sealed class WaypointSettleValidator
{
    /// <summary>FG/BG settled-position parity tolerance per axis.</summary>
    public const float FgBgParityToleranceY = 0.3f;

    /// <summary>Default settle delay between teleport and snapshot.</summary>
    public static readonly TimeSpan DefaultSettleDelay = TimeSpan.FromMilliseconds(4000);

    /// <summary>
    /// Maximum 2D drift between teleport target and settled position before
    /// the checkpoint is considered TELEPORT_FAILED instead of evaluated.
    /// 8y matches LiveBotFixture's existing teleport probe tolerance.
    /// </summary>
    public const float TeleportXyDriftTolerance = 8f;

    private readonly BakeFixture _fixture;
    private readonly IBakeValidationHost _host;
    private readonly TimeSpan _settleDelay;
    private readonly Func<DateTime> _clock;
    private readonly string? _screenshotDir;

    public WaypointSettleValidator(
        BakeFixture fixture,
        IBakeValidationHost host,
        TimeSpan? settleDelay = null,
        Func<DateTime>? clock = null,
        string? screenshotDir = null)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settleDelay = settleDelay ?? DefaultSettleDelay;
        _clock = clock ?? (() => DateTime.UtcNow);
        _screenshotDir = screenshotDir;
    }

    /// <summary>
    /// Run the fixture against the given account(s). The fixture's
    /// expectedWalkable/expectedHoles checkpoints are exercised on
    /// <paramref name="fgAccount"/>; if <paramref name="bgAccount"/> is
    /// non-null, the same checkpoints are also exercised on the BG bot
    /// and FG/BG parity is asserted. BG never gets screenshots —
    /// observability for the validation cycle comes from the paired FG.
    /// </summary>
    public async Task<BakeValidationReport> ValidateAsync(
        string fgAccount,
        string? bgAccount = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fgAccount))
            throw new ArgumentException("fgAccount must be non-empty", nameof(fgAccount));

        var failures = new List<BakeValidationFailure>();
        var walkableResults = new List<BakeValidationCheckpointResult>();
        var holeResults = new List<BakeValidationCheckpointResult>();

        _host.Log($"[BAKE-VAL] start route='{_fixture.Route}' map={_fixture.MapId} " +
            $"walkable={_fixture.ExpectedWalkable.Count} holes={_fixture.ExpectedHoles.Count} " +
            $"fg={fgAccount} bg={bgAccount ?? "(none)"}");

        foreach (var c in _fixture.ExpectedWalkable)
        {
            var r = await EvaluateWalkableAsync(c, fgAccount, bgAccount, failures, ct).ConfigureAwait(false);
            walkableResults.Add(r);
        }

        foreach (var h in _fixture.ExpectedHoles)
        {
            var r = await EvaluateHoleAsync(h, fgAccount, bgAccount, failures, ct).ConfigureAwait(false);
            holeResults.Add(r);
        }

        var (smoothResult, affordanceResult) = await EvaluateSmoothPathAsync(failures, ct).ConfigureAwait(false);

        var passed = failures.Count == 0;

        var report = new BakeValidationReport(
            Route: _fixture.Route,
            TimestampUtc: _clock(),
            FgAccount: fgAccount,
            BgAccount: bgAccount,
            Walkable: walkableResults,
            Holes: holeResults,
            SmoothPath: smoothResult,
            Affordance: affordanceResult,
            Failures: failures,
            Passed: passed);

        _host.Log($"[BAKE-VAL] done route='{_fixture.Route}' passed={passed} failures={failures.Count}");
        return report;
    }

    private async Task<BakeValidationCheckpointResult> EvaluateWalkableAsync(
        BakeFixtureCheckpoint c,
        string fgAccount,
        string? bgAccount,
        List<BakeValidationFailure> failures,
        CancellationToken ct)
    {
        var (fgPos, fgPoly) = await TeleportSampleAsync(c.Label, c.Xyz, fgAccount, ct).ConfigureAwait(false);
        var (bgPos, bgPoly) = bgAccount != null
            ? await TeleportSampleAsync(c.Label, c.Xyz, bgAccount, ct).ConfigureAwait(false)
            : (default, default);

        if (fgPos == null)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.TeleportFailed,
                c.Label,
                "FG bot never produced a settled position after teleport",
                Expected: $"({c.Xyz[0]:F2},{c.Xyz[1]:F2},{c.Xyz[2]:F2})",
                Actual: null));
            return MakeCheckpoint(c, "walkable", null, null, fgPoly, bgPoly, null, "MISSING_SAMPLE");
        }

        // 1. Teleport-failed gate.
        var fgXyDrift2 = SqrDistance2D(fgPos, c.Xyz[0], c.Xyz[1]);
        if (fgXyDrift2 > TeleportXyDriftTolerance * TeleportXyDriftTolerance)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.TeleportFailed,
                c.Label,
                $"FG settled XY drifted {MathF.Sqrt(fgXyDrift2):F2}y from teleport target (tolerance {TeleportXyDriftTolerance}y)",
                Expected: $"({c.Xyz[0]:F2},{c.Xyz[1]:F2})",
                Actual: $"({fgPos.X:F2},{fgPos.Y:F2})"));
            return MakeCheckpoint(c, "walkable", fgPos, bgPos, fgPoly, bgPoly, null, "FAILED");
        }

        // 2. Walkable settle-Z gate.
        var fgDz = MathF.Abs(fgPos.Z - c.Xyz[2]);
        var status = "OK";
        if (fgDz > c.SettleToleranceY)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.BakeRegressionWalkableLost,
                c.Label,
                $"FG settle Z {fgPos.Z:F2} differs from expected {c.Xyz[2]:F2} by {fgDz:F2}y (> {c.SettleToleranceY}y).",
                Expected: $"Z within ±{c.SettleToleranceY}y of {c.Xyz[2]:F2}",
                Actual: $"Z={fgPos.Z:F2} dz={fgDz:F2}y"));
            status = "FAILED";
        }

        // 3. FG/BG parity gate.
        if (bgPos != null && fgPos != null && CheckParity(c.Label, fgPos, bgPos, failures))
            status = "FAILED";

        // 4. Multi-angle screenshots (FG only — BG never gets screenshots).
        var screenshots = await CaptureScreenshotsAsync(c.Label, fgAccount, fgPos, ct).ConfigureAwait(false);

        return MakeCheckpoint(c, "walkable", fgPos, bgPos, fgPoly, bgPoly, screenshots, status);
    }

    private async Task<BakeValidationCheckpointResult> EvaluateHoleAsync(
        BakeFixtureHole h,
        string fgAccount,
        string? bgAccount,
        List<BakeValidationFailure> failures,
        CancellationToken ct)
    {
        var (fgPos, fgPoly) = await TeleportSampleAsync(h.Label, h.Xyz, fgAccount, ct).ConfigureAwait(false);
        var (bgPos, bgPoly) = bgAccount != null
            ? await TeleportSampleAsync(h.Label, h.Xyz, bgAccount, ct).ConfigureAwait(false)
            : (default, default);

        if (fgPos == null)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.TeleportFailed,
                h.Label,
                "FG bot never produced a settled position after teleport",
                Expected: $"settle ≈ z={h.ExpectedSettleZ:F2}",
                Actual: null));
            return MakeHole(h, null, null, fgPoly, bgPoly, null, "MISSING_SAMPLE");
        }

        // 1. Phantom-poly gate. The bot must NOT settle at the original xyz
        //    z; it must fall to expectedSettleZ ± tolerance.
        var fgDzFromExpected = MathF.Abs(fgPos.Z - h.ExpectedSettleZ);
        var fgDzFromTrap = MathF.Abs(fgPos.Z - h.Xyz[2]);
        var status = "OK";

        if (fgDzFromTrap < h.SettleToleranceY)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.PhantomPoly,
                h.Label,
                $"FG settled at Z={fgPos.Z:F2}, within {fgDzFromTrap:F2}y of the trap z={h.Xyz[2]:F2}; " +
                $"fixture says this point should NOT have a walkable polygon. {h.Rationale ?? string.Empty}".TrimEnd(),
                Expected: $"settle to Z≈{h.ExpectedSettleZ:F2} ±{h.SettleToleranceY}y",
                Actual: $"Z={fgPos.Z:F2}"));
            status = "FAILED";
        }
        else if (fgDzFromExpected > h.SettleToleranceY)
        {
            // The bot fell, but not to where we expected — useful diagnostic
            // but not necessarily a phantom-poly bug. Record without failing.
            _host.Log($"[BAKE-VAL] {h.Label}: settled at Z={fgPos.Z:F2}, " +
                $"{fgDzFromExpected:F2}y from expected fall coord {h.ExpectedSettleZ:F2}. " +
                "Fixture's expectedSettleZ may need a recording-mode refresh.");
        }

        if (bgPos != null && fgPos != null && CheckParity(h.Label, fgPos, bgPos, failures))
            status = "FAILED";

        // Multi-angle screenshots (FG only).
        var screenshots = await CaptureScreenshotsAsync(h.Label, fgAccount, fgPos, ct).ConfigureAwait(false);

        return MakeHole(h, fgPos, bgPos, fgPoly, bgPoly, screenshots, status);
    }

    /// <summary>
    /// Drive the host's multi-angle capture for the given checkpoint
    /// using the bot's settled position. Returns null when no screenshot
    /// directory was configured (unit-test mode) or the host returns no
    /// paths. Failures inside capture are logged but do not break the
    /// validation cycle.
    /// </summary>
    private async Task<IReadOnlyList<string>?> CaptureScreenshotsAsync(
        string label,
        string fgAccount,
        SettledPosition fgPos,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_screenshotDir)) return null;
        try
        {
            var paths = await _host.CaptureMultiAngleAsync(
                fgAccount,
                label,
                _fixture.MapId,
                fgPos.X, fgPos.Y, fgPos.Z,
                _screenshotDir,
                ct).ConfigureAwait(false);
            return paths == null || paths.Count == 0 ? null : paths;
        }
        catch (Exception ex)
        {
            _host.Log($"[BAKE-VAL] screenshot capture failed for '{label}': {ex.Message}");
            return null;
        }
    }

    private async Task<(BakeValidationSmoothPathResult? Smooth, BakeValidationAffordanceResult? Afford)>
        EvaluateSmoothPathAsync(List<BakeValidationFailure> failures, CancellationToken ct)
    {
        var golden = _fixture.GoldenSmoothPath;
        if (golden == null) return (null, null);

        var path = await _host.QuerySmoothPathAsync(
            _fixture.MapId, _fixture.Endpoints.Start, _fixture.Endpoints.Dest, ct)
            .ConfigureAwait(false);

        if (path == null)
        {
            _host.Log("[BAKE-VAL] smooth-path query unavailable in this host; skipping golden-path checks.");
            return (null, null);
        }

        if (path.Length < 2)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.SmoothPathQueryFailed,
                _fixture.Route,
                $"QuerySmoothPath returned {path.Length} waypoint(s); need >= 2",
                Expected: ">= 2 waypoints",
                Actual: path.Length.ToString()));
            return (null, null);
        }

        var smooth = new BakeValidationSmoothPathResult(
            WaypointCount: path.Length,
            ExpectedCount: golden.WaypointCount,
            ExpectedTolerance: golden.WaypointTolerance,
            EndpointDistanceY: ComputeEndpointDistanceY(path, _fixture.Endpoints.Dest),
            EndpointToleranceY: golden.EndpointToleranceY);

        if (Math.Abs(path.Length - golden.WaypointCount) > golden.WaypointTolerance)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.WaypointCountDrift,
                _fixture.Route,
                $"Smooth-path waypoint count {path.Length} drifted from expected {golden.WaypointCount} ±{golden.WaypointTolerance}.",
                Expected: $"{golden.WaypointCount} ± {golden.WaypointTolerance}",
                Actual: path.Length.ToString()));
        }

        if (smooth.EndpointDistanceY is { } endDist && endDist > golden.EndpointToleranceY)
        {
            failures.Add(new BakeValidationFailure(
                BakeValidationFailureKinds.EndpointMiss,
                _fixture.Route,
                $"Smooth-path final waypoint is {endDist:F2}y from the dest; " +
                $"tolerance {golden.EndpointToleranceY:F2}y.",
                Expected: $"≤ {golden.EndpointToleranceY:F2}y",
                Actual: $"{endDist:F2}y"));
        }

        var afford = await EvaluateAffordancesAsync(path, failures, ct).ConfigureAwait(false);
        return (smooth, afford);
    }

    private async Task<BakeValidationAffordanceResult?> EvaluateAffordancesAsync(
        float[][] path,
        List<BakeValidationFailure> failures,
        CancellationToken ct)
    {
        var unsafeCount = 0;
        var firstUnsafeIdx = (int?)null;
        var firstUnsafeKind = (string?)null;
        var evaluated = 0;

        for (int i = 0; i < path.Length - 1; i++)
        {
            var kind = await _host.ClassifySegmentAsync(_fixture.MapId, path[i], path[i + 1], ct).ConfigureAwait(false);
            if (kind == null) return null; // host doesn't classify; skip the whole pass
            evaluated++;
            if (IsUnsafe(kind))
            {
                unsafeCount++;
                firstUnsafeIdx ??= i;
                firstUnsafeKind ??= kind;
                failures.Add(new BakeValidationFailure(
                    BakeValidationFailureKinds.UnsafeAffordance,
                    $"segment[{i}->{i + 1}]",
                    $"Unsafe segment affordance '{kind}' at smooth-path index {i}->{i + 1}",
                    Expected: "Walk / StepUp / SafeDrop / Drop / JumpGap",
                    Actual: kind));
            }
        }

        return new BakeValidationAffordanceResult(
            EvaluatedSegments: evaluated,
            UnsafeSegmentCount: unsafeCount,
            FirstUnsafeIndex: firstUnsafeIdx,
            FirstUnsafeKind: firstUnsafeKind);
    }

    private static bool IsUnsafe(string kind)
        => kind.Equals("Cliff", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("UnsafeDrop", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("Blocked", StringComparison.OrdinalIgnoreCase);

    private async Task<(SettledPosition? Pos, ulong? Poly)> TeleportSampleAsync(
        string label,
        float[] xyz,
        string accountName,
        CancellationToken ct)
    {
        var settled = await _host.TeleportAndSettleAsync(
            accountName,
            _fixture.MapId,
            xyz[0], xyz[1], xyz[2],
            _settleDelay,
            ct).ConfigureAwait(false);
        if (settled == null)
        {
            _host.Log($"[BAKE-VAL] {label}: account={accountName} returned no settled position.");
            return (null, null);
        }
        _host.Log(
            $"[BAKE-VAL] {label}: account={accountName} settled=({settled.X:F2},{settled.Y:F2},{settled.Z:F2}) " +
            $"polyRef={(settled.PolyRef.HasValue ? "0x" + settled.PolyRef.Value.ToString("X16") : "n/a")}");
        return (settled, settled.PolyRef);
    }

    private bool CheckParity(
        string label,
        SettledPosition fg,
        SettledPosition bg,
        List<BakeValidationFailure> failures)
    {
        var dx = MathF.Abs(fg.X - bg.X);
        var dy = MathF.Abs(fg.Y - bg.Y);
        var dz = MathF.Abs(fg.Z - bg.Z);
        if (dx <= FgBgParityToleranceY && dy <= FgBgParityToleranceY && dz <= FgBgParityToleranceY)
            return false;

        failures.Add(new BakeValidationFailure(
            BakeValidationFailureKinds.FgBgParityBreak,
            label,
            $"FG/BG settled coords disagree by axis: dx={dx:F2} dy={dy:F2} dz={dz:F2} " +
            $"(tolerance ±{FgBgParityToleranceY:F2} per axis).",
            Expected: $"|Δ| ≤ {FgBgParityToleranceY:F2}y per axis",
            Actual: $"|Δ|=({dx:F2},{dy:F2},{dz:F2})"));
        return true;
    }

    private static BakeValidationCheckpointResult MakeCheckpoint(
        BakeFixtureCheckpoint c,
        string kind,
        SettledPosition? fg,
        SettledPosition? bg,
        ulong? fgPoly,
        ulong? bgPoly,
        IReadOnlyList<string>? screenshots,
        string status)
        => new(
            Label: c.Label,
            Kind: kind,
            ExpectedXyz: c.Xyz,
            ExpectedSettleZ: null,
            SettleToleranceY: c.SettleToleranceY,
            FgSettled: fg == null ? null : new[] { fg.X, fg.Y, fg.Z },
            BgSettled: bg == null ? null : new[] { bg.X, bg.Y, bg.Z },
            FgPolyRefHex: fgPoly == null ? null : "0x" + fgPoly.Value.ToString("X16"),
            BgPolyRefHex: bgPoly == null ? null : "0x" + bgPoly.Value.ToString("X16"),
            Screenshots: screenshots,
            Status: status);

    private static BakeValidationCheckpointResult MakeHole(
        BakeFixtureHole h,
        SettledPosition? fg,
        SettledPosition? bg,
        ulong? fgPoly,
        ulong? bgPoly,
        IReadOnlyList<string>? screenshots,
        string status)
        => new(
            Label: h.Label,
            Kind: "hole",
            ExpectedXyz: h.Xyz,
            ExpectedSettleZ: h.ExpectedSettleZ,
            SettleToleranceY: h.SettleToleranceY,
            FgSettled: fg == null ? null : new[] { fg.X, fg.Y, fg.Z },
            BgSettled: bg == null ? null : new[] { bg.X, bg.Y, bg.Z },
            FgPolyRefHex: fgPoly == null ? null : "0x" + fgPoly.Value.ToString("X16"),
            BgPolyRefHex: bgPoly == null ? null : "0x" + bgPoly.Value.ToString("X16"),
            Screenshots: screenshots,
            Status: status);

    private static float SqrDistance2D(SettledPosition pos, float tx, float ty)
    {
        var dx = pos.X - tx;
        var dy = pos.Y - ty;
        return dx * dx + dy * dy;
    }

    private static float? ComputeEndpointDistanceY(float[][] path, float[] dest)
    {
        if (path.Length == 0) return null;
        var last = path[^1];
        if (last.Length < 3 || dest.Length < 3) return null;
        var dx = last[0] - dest[0];
        var dy = last[1] - dest[1];
        var dz = last[2] - dest[2];
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Helper for live tests: write the report to
    /// <c>tmp/test-runtime/screenshots/long-pathing/bake-validation-&lt;route&gt;-&lt;timestamp&gt;.json</c>
    /// (or whatever directory the caller passes). Returns the absolute
    /// path of the written file.
    /// </summary>
    public static string WriteReport(BakeValidationReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        var timestamp = report.TimestampUtc.ToString("yyyyMMddTHHmmssZ");
        var safeRoute = SanitizeForFilename(report.Route);
        var path = Path.Combine(directory, $"bake-validation-{safeRoute}-{timestamp}.json");
        File.WriteAllText(path, BakeValidationReportSerializer.Serialize(report));
        return path;
    }

    private static string SanitizeForFilename(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buf = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
            buf.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        return buf.ToString();
    }
}
