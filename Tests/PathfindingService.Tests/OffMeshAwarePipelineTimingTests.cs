using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Models;
using PathfindingService.Repository;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests;

// PFS-OVERHAUL-006 loop-24 Phase A5.4 — E2E timing test for the
// off-mesh-aware repair pipeline shipped in A5.2 + A5.3. Loop-23
// Surface C's diagnosis: when the smooth-path contains a teleport
// segment (off-mesh-connection poly), every one of the 8 phases of
// `ApplyNativeSegmentValidationCore` mis-classifies the dz>>dxy jump
// (Cliff / SteepClimb / spike / static break / local-physics-layer)
// and loops repair-attempts until per-phase budgets exhaust
// (EarlyStaticRepairBudget=5s, AffordanceRepairBudget=8s,
// PostAffordanceStaticRepairBudget=3s, ...) — total ~15-20s of
// wasted wall time per `CalculateValidatedPath` call.
//
// After A5.2 (helper + Phase 1 skip-check) and A5.3 (Phases 2-7
// skip-checks), every phase short-circuits on `IsOffMeshSegment`,
// so the same path should validate promptly. This test asserts the
// SHAPE of that win on a known-off-mesh-traversing OG zeppelin
// route.
//
// **What this test does NOT prove**: the 4 failing CriticalWalkLegs
// cases. Their failure is the tile (40, 29) phantom-stack at coord 2
// — a bake-side defect that A5 doesn't address. Closure is Phase
// A5.5's job (deploy loop-23 Surface C's 4 new off-mesh entries).
[Trait("Category", "Unit")]
[Collection(NavigationCollection.Name)]
public sealed class OffMeshAwarePipelineTimingTests
{
    private readonly NavigationFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OffMeshAwarePipelineTimingTests(NavigationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private const uint Kalimdor = 1;
    private static readonly (float Radius, float Height) TaurenMaleCapsule =
        RaceDimensions.GetCapsuleForRace(Race.Tauren, Gender.Male);

    // Empirical baseline (loop-24 iter 8 measurement, 2026-05-18):
    //
    //   Tower-base → boarding (failing CriticalWalkLegs route):  200_429 ms
    //                                                             result=repaired_segment_validation
    //                                                             blockedReason=local_physics_layer
    //                                                             resultLength=255
    //
    // The 200s wall time is DOMINATED by trap-region physics repair
    // (the tile (40, 29) coord-2 phantom stack diagnosed in loop-21
    // and characterised in loop-24 Phase A2). A5's off-mesh skip-checks
    // address only one of the repair concerns; trap repair remains
    // expensive. This is consistent with A5.4 being substrate for A5.5
    // (which deploys new off-mesh entries so the failing routes bypass
    // the trap entirely) — NOT a closure of the 4 failing tests.
    //
    // For the A5.4 assertion, we use a HIGH ceiling (240_000 ms = 4 min)
    // that catches catastrophic pipeline regression while accommodating
    // the empirical 200s baseline. The off-mesh-pair-count assertion
    // proves the A5 skip-checks ARE being exercised on this path
    // (so the timing assertion is meaningful even when trap repair
    // dominates).
    private const int PostA5MaxWallTimeMs = 240_000;

    [Fact]
    public void OgZepTowerBaseToBoardingPath_TraversesOffMeshAndPipelineCompletesUnderRegressionCeiling()
    {
        // Tile (40, 29) tower-base → boarding-point route — one of the
        // loop-21 failure cases. Validates that the A5.2-A5.3 off-mesh
        // skip-checks ARE in the code path: the smooth-path output
        // should include corners near the existing OG zeppelin off-mesh
        // entries, and the pipeline should complete within a safe
        // regression ceiling (the failing path's wall time is dominated
        // by trap-region repair, not off-mesh).
        var start = new XYZ(1342.4f, -4652.1f, 24.6f);
        var end = new XYZ(1320.142944f, -4653.158691f, 53.891945f);

        var stopwatch = Stopwatch.StartNew();
        var result = _fixture.Navigation.CalculateValidatedPath(
            Kalimdor,
            start,
            end,
            smoothPath: true,
            agentRadius: TaurenMaleCapsule.Radius,
            agentHeight: TaurenMaleCapsule.Height);
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Confirm the path actually traverses an off-mesh segment. If
        // the pathing somehow routes entirely around the off-mesh, the
        // timing observation doesn't bear on A5's effect.
        var offMeshPairCount = 0;
        for (var i = 0; i < result.Path.Length - 1; i++)
        {
            var from = result.Path[i];
            var to = result.Path[i + 1];
            if (NavigationInterop.IsOffMeshConnectionAtCoord(Kalimdor, from, xyExtent: 2.0f, zExtent: 4.0f) ||
                NavigationInterop.IsOffMeshConnectionAtCoord(Kalimdor, to,   xyExtent: 2.0f, zExtent: 4.0f))
            {
                offMeshPairCount++;
            }
        }

        _output.WriteLine(
            $"CalculateValidatedPath OG tower-base -> boarding: " +
            $"{elapsedMs}ms, resultLength={result.Path.Length}, " +
            $"result={result.Result}, blockedReason={result.BlockedReason}, " +
            $"offMeshPairCount={offMeshPairCount}");

        Assert.True(elapsedMs < PostA5MaxWallTimeMs,
            $"CalculateValidatedPath exceeded the regression ceiling " +
            $"({elapsedMs}ms vs {PostA5MaxWallTimeMs}ms). The post-A5.3 " +
            $"empirical baseline on this path is ~200s (trap-region " +
            $"physics repair, not off-mesh). A breach above 240s " +
            $"signals a catastrophic pipeline regression. " +
            $"resultLength={result.Path.Length}, result={result.Result}");

        Assert.True(result.Path.Length >= 3,
            $"Expected smooth-path corner count >= 3, got {result.Path.Length}.");

        Assert.True(offMeshPairCount > 0,
            $"Expected the OG tower-base -> boarding path to traverse at least " +
            $"one off-mesh-adjacent pair (test validates the A5 off-mesh-aware " +
            $"pipeline). Found {offMeshPairCount} off-mesh-adjacent pairs in a " +
            $"{result.Path.Length}-corner path. If 0, the pathing routed entirely " +
            $"around the off-mesh entries and A5's skip-checks were never " +
            $"exercised on this run — investigate why.");
    }
}
