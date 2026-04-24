using System.Collections.Generic;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

public class FishingPoolActivationAnalyzerTests
{
    [Fact]
    public void ClassifyPoolSpawnStateResponses_ReturnsSpawnedWhenSpawnRowsPresent()
    {
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(
            2620u,
            ["guid=14567 pool 2620 School of Tastyfish x=-957.2 y=-3778.9 z=0.1 map=1"]);

        Assert.Equal(FishingPoolActivationState.Spawned, state);
    }

    [Fact]
    public void ClassifyPoolSpawnStateResponses_ReturnsUnknownWhenNoSpawnRowsAreCaptured()
    {
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(2620u, []);

        Assert.Equal(FishingPoolActivationState.Unknown, state);
    }

    [Fact]
    public void ClassifyPoolSpawnStateResponses_KeepsPositivePoolUpdateCountLinesAsUnknown()
    {
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(
            2620u,
            ["Pool #2620: 1 objects spawned [limit = 1]"]);

        Assert.Equal(FishingPoolActivationState.Unknown, state);
    }

    [Fact]
    public void ClassifyPoolSpawnStateResponses_KeepsZeroCountPoolUpdateLinesAsUnknown()
    {
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(
            2620u,
            ["Pool #2620: 0 objects spawned [limit = 1]"]);

        Assert.Equal(FishingPoolActivationState.Unknown, state);
    }

    [Fact]
    public void ClassifyPoolSpawnStateResponses_IgnoresSpawnRowsForDifferentPool()
    {
        var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(
            2620u,
            ["guid=14567 pool 2607 Oily Blackmouth School x=-1982.0 y=-3767.3 z=0.0 map=1"]);

        Assert.Equal(FishingPoolActivationState.Unknown, state);
    }

    [Fact]
    public void DetermineBlockerKind_ReturnsMasterPoolSelectedNonLocalWhenOnlyRemotePoolsSpawn()
    {
        var sites = FishingPoolActivationAnalyzer.MaterializeSites(
            [
                (2620u, "Ratchet local", 180582u, 1, -957.2f, -3778.9f, 0f),
                (2607u, "Barrens remote", 180582u, 1, -1982.0f, -3767.3f, 0f)
            ],
            localPoolEntries: [2620u],
            packetStageX: -949.9f,
            packetStageY: -3766.9f,
            parityStageX: -967.2f,
            parityStageY: -3760.0f);
        var probes = FishingPoolActivationAnalyzer.MaterializeProbes(
            sites,
            new Dictionary<uint, IReadOnlyList<string>>
            {
                [2620u] = ["syntax error"],
                [2607u] = ["guid=87654 pool 2607 Oily Blackmouth School x=-1982.0 y=-3767.3 z=0.0 map=1"]
            });

        var blockerKind = FishingPoolActivationAnalyzer.DetermineBlockerKind(
            localPoolSpawnedDuringStage: false,
            probes);

        Assert.Equal(FishingPoolBlockerKind.MasterPoolSelectedNonLocal, blockerKind);
    }

    [Fact]
    public void DetermineBlockerKind_PrefersLocalPoolSpawnedButInvisibleWhenStageAlreadySpawnedLocalPool()
    {
        var sites = FishingPoolActivationAnalyzer.MaterializeSites(
            [
                (2620u, "Ratchet local", 180582u, 1, -957.2f, -3778.9f, 0f),
                (2607u, "Barrens remote", 180582u, 1, -1982.0f, -3767.3f, 0f)
            ],
            localPoolEntries: [2620u],
            packetStageX: -949.9f,
            packetStageY: -3766.9f,
            parityStageX: -967.2f,
            parityStageY: -3760.0f);
        var probes = FishingPoolActivationAnalyzer.MaterializeProbes(
            sites,
            new Dictionary<uint, IReadOnlyList<string>>
            {
                [2620u] = ["syntax error"],
                [2607u] = ["guid=87654 pool 2607 Oily Blackmouth School x=-1982.0 y=-3767.3 z=0.0 map=1"]
            });

        var blockerKind = FishingPoolActivationAnalyzer.DetermineBlockerKind(
            localPoolSpawnedDuringStage: true,
            probes);

        Assert.Equal(FishingPoolBlockerKind.LocalPoolSpawnedButInvisible, blockerKind);
    }

    [Fact]
    public void DetermineBlockerKind_ReturnsLocalPoolSpawnedOnlyOnDirectProbeWhenStageNeverSpawnedLocalPool()
    {
        var sites = FishingPoolActivationAnalyzer.MaterializeSites(
            [
                (2620u, "Ratchet local", 180582u, 1, -957.2f, -3778.9f, 0f),
                (2607u, "Barrens remote", 180582u, 1, -1982.0f, -3767.3f, 0f)
            ],
            localPoolEntries: [2620u],
            packetStageX: -949.9f,
            packetStageY: -3766.9f,
            parityStageX: -967.2f,
            parityStageY: -3760.0f);
        var probes = FishingPoolActivationAnalyzer.MaterializeProbes(
            sites,
            new Dictionary<uint, IReadOnlyList<string>>
            {
                [2620u] = ["guid=14567 pool 2620 School of Tastyfish x=-957.2 y=-3778.9 z=0.1 map=1"],
                [2607u] = ["guid=87654 pool 2607 Oily Blackmouth School x=-1982.0 y=-3767.3 z=0.0 map=1"]
            });

        var blockerKind = FishingPoolActivationAnalyzer.DetermineBlockerKind(
            localPoolSpawnedDuringStage: false,
            probes);

        Assert.Equal(FishingPoolBlockerKind.LocalPoolSpawnedOnlyOnDirectProbe, blockerKind);
    }
}
