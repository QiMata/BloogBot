using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — bake-fixture recording mode (Layer 5).
///
/// Drives the same teleport/settle/screenshot loop the
/// <see cref="WaypointSettleValidator"/> uses, but instead of asserting
/// against a fixture's expected values, it writes a fresh fixture from
/// the observed settle behavior. Auto-classifies each candidate as
/// walkable or hole based on the gap between requested Z and settled Z:
///
///   |dz| ≤ <see cref="WalkableDzThresholdY"/>  → expectedWalkable entry
///   |dz| &gt; <see cref="WalkableDzThresholdY"/>  → expectedHole entry
///                                                  with expectedSettleZ
///                                                  pinned to the actual
///                                                  settle Z.
///
/// The user reviews the emitted fixture by hand (using the per-candidate
/// screenshots) and refines tolerances or marks holes that are
/// legitimately walkable (a misclassification — the bot fell because the
/// teleport-target was near a ledge and 4y of fall is normal physics
/// noise, not a bake hole). The recorder errs on the side of recording
/// what was observed; it does not invent thresholds.
/// </summary>
public sealed class BakeFixtureRecorder
{
    /// <summary>
    /// Settle-Z delta beyond which a candidate is classified as a hole.
    /// 1.5y matches WoW's <c>walkableClimb</c> harvested-from-client value
    /// (settling within a step is normal; falling further is a hole).
    /// </summary>
    public const float WalkableDzThresholdY = 1.5f;

    /// <summary>Default per-checkpoint walkable settle tolerance written into the fixture.</summary>
    public const float DefaultWalkableSettleTolerance = 0.75f;

    /// <summary>Default per-hole settle tolerance (wider — falls have more variance).</summary>
    public const float DefaultHoleSettleTolerance = 1.5f;

    private readonly IBakeValidationHost _host;
    private readonly TimeSpan _settleDelay;
    private readonly string? _screenshotDir;

    public BakeFixtureRecorder(
        IBakeValidationHost host,
        TimeSpan? settleDelay = null,
        string? screenshotDir = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settleDelay = settleDelay ?? WaypointSettleValidator.DefaultSettleDelay;
        _screenshotDir = screenshotDir;
    }

    /// <summary>
    /// Drive every <paramref name="candidate"/> through teleport+settle
    /// (and multi-angle capture if a screenshot dir is configured), then
    /// write a <see cref="BakeFixture"/>-shaped JSON document to
    /// <paramref name="outputPath"/>. Returns the in-memory fixture.
    /// </summary>
    public async Task<BakeFixture> RecordAsync(
        BakeFixtureRecorderInput input,
        string fgAccount,
        string outputPath,
        CancellationToken ct = default)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (string.IsNullOrWhiteSpace(fgAccount))
            throw new ArgumentException("fgAccount must be non-empty", nameof(fgAccount));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("outputPath must be non-empty", nameof(outputPath));
        if (input.Candidates.Count == 0)
            throw new InvalidOperationException("RecorderInput must contain at least one candidate.");

        var walkable = new List<BakeFixtureCheckpoint>();
        var holes = new List<BakeFixtureHole>();

        _host.Log($"[BAKE-REC] start route='{input.RouteName}' map={input.MapId} " +
            $"candidates={input.Candidates.Count} fg={fgAccount} out={outputPath}");

        foreach (var c in input.Candidates)
        {
            ct.ThrowIfCancellationRequested();
            var settled = await _host.TeleportAndSettleAsync(
                fgAccount, input.MapId, c.Xyz[0], c.Xyz[1], c.Xyz[2],
                _settleDelay, ct).ConfigureAwait(false);

            if (settled == null)
            {
                _host.Log($"[BAKE-REC] {c.Label}: no settle; skipping classification.");
                continue;
            }

            // Drive multi-angle screenshots when configured. Best-effort.
            if (!string.IsNullOrEmpty(_screenshotDir))
            {
                try
                {
                    await _host.CaptureMultiAngleAsync(
                        fgAccount, c.Label, input.MapId,
                        settled.X, settled.Y, settled.Z,
                        _screenshotDir, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _host.Log($"[BAKE-REC] screenshot capture failed for '{c.Label}': {ex.Message}");
                }
            }

            var dz = MathF.Abs(settled.Z - c.Xyz[2]);
            if (dz <= WalkableDzThresholdY)
            {
                walkable.Add(new BakeFixtureCheckpoint(
                    Label: c.Label,
                    Xyz: new[] { c.Xyz[0], c.Xyz[1], settled.Z },
                    SettleToleranceY: input.WalkableSettleTolerance ?? DefaultWalkableSettleTolerance,
                    Note: c.Note));
                _host.Log($"[BAKE-REC] {c.Label}: walkable (settled Z={settled.Z:F2}, request Z={c.Xyz[2]:F2}, dz={dz:F2}).");
            }
            else
            {
                holes.Add(new BakeFixtureHole(
                    Label: c.Label,
                    Xyz: new[] { c.Xyz[0], c.Xyz[1], c.Xyz[2] },
                    ExpectedSettleZ: settled.Z,
                    SettleToleranceY: input.HoleSettleTolerance ?? DefaultHoleSettleTolerance,
                    Rationale: c.Note));
                _host.Log($"[BAKE-REC] {c.Label}: hole (settled Z={settled.Z:F2} from request Z={c.Xyz[2]:F2}, dz={dz:F2}).");
            }
        }

        var fixture = new BakeFixture(
            Route: input.RouteName,
            Description: input.Description ?? $"Recorded by BakeFixtureRecorder on {DateTime.UtcNow:yyyy-MM-dd}",
            MapId: input.MapId,
            Agent: input.Agent,
            Endpoints: input.Endpoints,
            ExpectedWalkable: walkable,
            ExpectedHoles: holes,
            GoldenSmoothPath: input.GoldenSmoothPath,
            TileInvariants: input.TileInvariants);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, BakeFixtureLoader.Serialize(fixture));
        _host.Log($"[BAKE-REC] wrote fixture: walkable={walkable.Count} holes={holes.Count} → {outputPath}");
        return fixture;
    }
}

/// <summary>
/// Input bundle for <see cref="BakeFixtureRecorder.RecordAsync"/>. Lets
/// callers seed the recorder with both the route metadata that ends up
/// in the fixture header AND the list of candidate XYZ coords to probe.
/// </summary>
public sealed record BakeFixtureRecorderInput(
    string RouteName,
    uint MapId,
    BakeFixtureEndpoints Endpoints,
    IReadOnlyList<BakeFixtureRecorderCandidate> Candidates,
    string? Description = null,
    BakeFixtureAgent? Agent = null,
    BakeFixtureSmoothPathExpectation? GoldenSmoothPath = null,
    IReadOnlyDictionary<string, BakeFixtureTileInvariant>? TileInvariants = null,
    float? WalkableSettleTolerance = null,
    float? HoleSettleTolerance = null);

public sealed record BakeFixtureRecorderCandidate(
    string Label,
    float[] Xyz,
    string? Note = null);
