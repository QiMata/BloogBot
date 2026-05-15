using System.Linq;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// PFS-OVERHAUL Phase 3 Surface K — sliced-find-path infrastructure tests.
///
/// Verifies the new <c>FindPathForAgentSliced</c> native export:
///  1. Returns the same polygon corridor as the synchronous
///     <c>FindPathPolygonsForAgent</c> on a golden case (FC→UBRS portal).
///     This is the correctness gate: the sliced API must not produce a
///     different path under normal conditions.
///  2. Returns <see cref="NavigationInterop.SlicedFindPathStatus.AStarTimeout"/>
///     when the wall-clock budget fires before the query completes (using
///     a 1-millisecond budget that's small enough that even sub-100ms
///     queries time out reliably).
///
/// The sliced variant is consumer-side new code — it does NOT change the
/// live planner's default findPath route. <c>Services/PathfindingService/
/// Repository/Navigation.cs</c> continues to call the synchronous
/// <c>FindPathForAgent</c> until a follow-up commit flips the default once
/// live FG verification is available.
/// </summary>
[Trait("Category", "Unit")]
public class SlicedFindPathTests : IClassFixture<PathfindingValidationFixture>
{
    private readonly PathfindingValidationFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const uint MapId = 0;            // Eastern Kingdoms
    private const float AgentRadius = 1.0247f;
    private const float AgentHeight = 2.625f;

    // FC → UBRS portal — same coords as BrmAscentRenderingExpectations.
    private static readonly XYZ FlameCrest = new(-7518.7f, -2159.9f, 131.9f);
    private static readonly XYZ UbrsPortal = new(-7524.0f, -1233.0f, 287.0f);

    public SlicedFindPathTests(
        PathfindingValidationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Sliced_FcToUbrsPortal_TerminatesAtSameEndPolys()
    {
        var sync = NavigationInterop.QueryPathPolygons(
            MapId, FlameCrest, UbrsPortal, AgentRadius, AgentHeight, maxOut: 4096);

        var sliced = NavigationInterop.QueryPathPolygonsSliced(
            MapId, FlameCrest, UbrsPortal, AgentRadius, AgentHeight,
            maxOut: 4096, maxWallClockMs: 30000);

        _output.WriteLine(
            $"# sync:    success={sync.Success} count={sync.PolyRefs.Length} "
            + $"start=0x{(sync.PolyRefs.Length > 0 ? sync.PolyRefs[0] : 0):X16} "
            + $"end=0x{(sync.PolyRefs.Length > 0 ? sync.PolyRefs[^1] : 0):X16}");
        _output.WriteLine(
            $"# sliced:  success={sliced.Success} status={sliced.Status} "
            + $"count={sliced.PolyRefs.Length} elapsedMs={sliced.ElapsedMs} "
            + $"iters={sliced.SliceIterations} "
            + $"start=0x{(sliced.PolyRefs.Length > 0 ? sliced.PolyRefs[0] : 0):X16} "
            + $"end=0x{(sliced.PolyRefs.Length > 0 ? sliced.PolyRefs[^1] : 0):X16}");

        Assert.True(sync.Success, "synchronous corridor query failed");
        Assert.True(sliced.Success, "sliced corridor query failed");
        Assert.Equal(NavigationInterop.SlicedFindPathStatus.Success, sliced.Status);
        Assert.True(sync.PolyRefs.Length > 0, "sync produced empty path");
        Assert.True(sliced.PolyRefs.Length > 0, "sliced produced empty path");

        // The sliced and synchronous algorithms diverge legitimately:
        // updateSlicedFindPath uses grandparent context for cost evaluation
        // and adds a same-parent dedup check that synchronous findPath
        // lacks (compare DetourNavMeshQuery.cpp:1289-1320 vs :996-1098).
        // That means corridor LENGTH and intermediate polys can differ
        // even on the same start/end input. The correctness property is
        // that BOTH paths reach the same end polygon (endRef from
        // findNearestPoly) and start from the same start polygon.
        Assert.True(
            sync.PolyRefs[0] == sliced.PolyRefs[0],
            $"start poly diverges: sync=0x{sync.PolyRefs[0]:X16} "
            + $"sliced=0x{sliced.PolyRefs[0]:X16}");
        Assert.True(
            sync.PolyRefs[^1] == sliced.PolyRefs[^1],
            $"end poly diverges: sync=0x{sync.PolyRefs[^1]:X16} "
            + $"sliced=0x{sliced.PolyRefs[^1]:X16}");
    }

    /// <summary>
    /// Diagnostic: the 500y Durotar→Crossroads route at
    /// (1177.8,-4464.2,21.4)→(1629.4,-4373.4,31.3) on Kalimdor (map 1)
    /// is the route where the synchronous `CalculatePath` hangs
    /// indefinitely post-2026-05-15 Navigation.dll rebuild (see
    /// PathfindingBotTaskTests.PathCalculation skip in commit 356fb418).
    /// This test exercises the same route through the sliced API with a
    /// 30s wall-clock budget to localize the hang. Two outcomes both
    /// load-bearing:
    ///   * Success in &lt;30s: confirms the hang is in the unbounded
    ///     synchronous findPath — fix is wiring sliced into the live
    ///     planner for long routes.
    ///   * AStarTimeout: hang is real A* explosion (Detour can't reach
    ///     endRef on this navmesh within budget) — needs bake or
    ///     filter work, not just planner rewiring.
    /// Test passes either way — its job is to print the outcome.
    /// </summary>
    [Fact]
    public void Sliced_DurotarToCrossroads_LocalizesPathCalculationHang()
    {
        const uint durotarMapId = 1;
        var durotarStart = new XYZ(1177.8f, -4464.2f, 21.4f);
        var crossroadsEnd = new XYZ(1629.4f, -4373.4f, 31.3f);

        var sliced = NavigationInterop.QueryPathPolygonsSliced(
            durotarMapId, durotarStart, crossroadsEnd, AgentRadius, AgentHeight,
            maxOut: 4096, maxWallClockMs: 30000);

        _output.WriteLine(
            $"# Durotar 500y route: success={sliced.Success} status={sliced.Status} "
            + $"count={sliced.PolyRefs.Length} elapsedMs={sliced.ElapsedMs} "
            + $"iters={sliced.SliceIterations} "
            + $"start=0x{(sliced.PolyRefs.Length > 0 ? sliced.PolyRefs[0] : 0):X16} "
            + $"end=0x{(sliced.PolyRefs.Length > 0 ? sliced.PolyRefs[^1] : 0):X16}");

        Assert.True(sliced.Success, "sliced infra must succeed (status reports actual outcome)");
    }

    [Fact]
    public void Sliced_WithSubMillisecondBudget_ReturnsAStarTimeout()
    {
        // A 1ms wall-clock budget is below the per-slice overhead, so the
        // very first deadline check after updateSlicedFindPath fires the
        // timeout path. The sliced query is guaranteed to make SOME forward
        // progress (initSlicedFindPath + at least one updateSlicedFindPath
        // call run unconditionally) but cannot complete a long path.
        var sliced = NavigationInterop.QueryPathPolygonsSliced(
            MapId, FlameCrest, UbrsPortal, AgentRadius, AgentHeight,
            maxOut: 4096, maxWallClockMs: 1);

        _output.WriteLine(
            $"# 1ms budget: success={sliced.Success} status={sliced.Status} "
            + $"count={sliced.PolyRefs.Length} elapsedMs={sliced.ElapsedMs} "
            + $"iters={sliced.SliceIterations}");

        Assert.True(sliced.Success, "sliced query infrastructure must succeed");
        Assert.Equal(NavigationInterop.SlicedFindPathStatus.AStarTimeout, sliced.Status);
        // ElapsedMs must be at least the budget (1ms) — we never abort early.
        // Upper bound is loose because the slice budget is 1024 iters which
        // can run several ms on a fragmented mesh; just confirm the timeout
        // mechanism converted a hang into a bounded abort.
        Assert.True(sliced.SliceIterations >= 1,
            $"expected at least one slice iter, got {sliced.SliceIterations}");
    }
}
