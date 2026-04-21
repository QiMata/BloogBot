using Xunit;

namespace BotRunner.Tests.LiveValidation;

public class RatchetFishingStageAttributionTests
{
    [Fact]
    public void ShouldAttributeNoPoolFailureToRuntime_ReturnsTrue_WhenVisiblePoolReadinessWasReached()
    {
        var stagePreparation = new RatchetFishingStagePreparation(
            StageName: "packet-capture",
            StageX: -949.9f,
            StageY: -3766.9f,
            StageZ: 3.9f,
            Readiness: RatchetFishingStageReadiness.VisiblePoolReady,
            SpawnedLocalPoolEntries: [2620u],
            SearchWaypoints: []);

        var shouldAttribute = RatchetFishingStageAttribution.ShouldAttributeNoPoolFailureToRuntime(
            stagePreparation,
            float.MaxValue,
            float.MaxValue);

        Assert.True(shouldAttribute);
    }

    [Fact]
    public void ShouldAttributeNoPoolFailureToRuntime_ReturnsFalse_WhenLocalChildSpawnedButStayedInvisible()
    {
        var stagePreparation = new RatchetFishingStagePreparation(
            StageName: "packet-capture",
            StageX: -949.9f,
            StageY: -3766.9f,
            StageZ: 3.9f,
            Readiness: RatchetFishingStageReadiness.LocalChildSpawnedButInvisible,
            SpawnedLocalPoolEntries: [2620u, 2627u],
            SearchWaypoints: [(-957.2f, -3778.9f, 5f)]);

        var shouldAttribute = RatchetFishingStageAttribution.ShouldAttributeNoPoolFailureToRuntime(
            stagePreparation,
            float.MaxValue,
            float.MaxValue);

        Assert.False(shouldAttribute);
    }

    [Fact]
    public void ShouldAttributeNoPoolFailureToRuntime_ReturnsFalse_WhenOnlyDirectProbeSpawnedLocalPool()
    {
        var stagePreparation = new RatchetFishingStagePreparation(
            StageName: "packet-capture",
            StageX: -949.9f,
            StageY: -3766.9f,
            StageZ: 3.9f,
            Readiness: RatchetFishingStageReadiness.LocalChildSpawnedOnDirectProbeOnly,
            SpawnedLocalPoolEntries: [2620u],
            SearchWaypoints: [(-957.2f, -3778.9f, 5f)]);

        var shouldAttribute = RatchetFishingStageAttribution.ShouldAttributeNoPoolFailureToRuntime(
            stagePreparation,
            float.MaxValue,
            float.MaxValue);

        Assert.False(shouldAttribute);
    }

    [Fact]
    public void ShouldAttributeNoPoolFailureToRuntime_ReturnsTrue_WhenPoolBecameVisibleDuringRun()
    {
        var stagePreparation = new RatchetFishingStagePreparation(
            StageName: "packet-capture",
            StageX: -949.9f,
            StageY: -3766.9f,
            StageZ: 3.9f,
            Readiness: RatchetFishingStageReadiness.LocalChildSpawnedOnDirectProbeOnly,
            SpawnedLocalPoolEntries: [2620u],
            SearchWaypoints: [(-957.2f, -3778.9f, 5f)]);

        var shouldAttribute = RatchetFishingStageAttribution.ShouldAttributeNoPoolFailureToRuntime(
            stagePreparation,
            float.MaxValue,
            18f);

        Assert.True(shouldAttribute);
    }

    [Fact]
    public void FormatPreparation_EmitsExplicitDirectProbeOutcome()
    {
        var stagePreparation = new RatchetFishingStagePreparation(
            StageName: "packet-capture",
            StageX: -949.9f,
            StageY: -3766.9f,
            StageZ: 3.9f,
            Readiness: RatchetFishingStageReadiness.LocalChildSpawnedOnDirectProbeOnly,
            SpawnedLocalPoolEntries: [2620u, 2627u],
            SearchWaypoints: [(-957.2f, -3778.9f, 5f), (-969.8f, -3805.1f, 5f)]);

        var formatted = RatchetFishingStageAttribution.FormatPreparation(stagePreparation);

        Assert.Contains("local_child_spawned_on_direct_probe_only[2620,2627]", formatted);
        Assert.Contains("searchWaypoints=2", formatted);
    }
}
