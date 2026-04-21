using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tests.LiveValidation;

internal enum RatchetFishingStageReadiness
{
    VisiblePoolReady,
    LocalChildSpawnedButInvisible,
    LocalChildSpawnedOnDirectProbeOnly,
    NoLocalChildSpawned
}

internal readonly record struct RatchetFishingStagePreparation(
    string StageName,
    float StageX,
    float StageY,
    float StageZ,
    RatchetFishingStageReadiness Readiness,
    IReadOnlyList<uint> SpawnedLocalPoolEntries,
    IReadOnlyList<(float x, float y, float z)> SearchWaypoints);

internal static class RatchetFishingStageAttribution
{
    public static bool ShouldAttributeNoPoolFailureToRuntime(
        RatchetFishingStagePreparation? stagePreparation,
        float initialVisiblePoolDistance,
        float bestPoolDistance)
    {
        var poolWasVisible = initialVisiblePoolDistance < float.MaxValue || bestPoolDistance < float.MaxValue;
        if (poolWasVisible || stagePreparation == null)
            return true;

        return stagePreparation.Value.Readiness == RatchetFishingStageReadiness.VisiblePoolReady;
    }

    public static string FormatPreparation(RatchetFishingStagePreparation? stagePreparation)
    {
        if (stagePreparation == null)
            return "Ratchet preflight=not_captured";

        var preparation = stagePreparation.Value;
        return
            $"Ratchet preflight stage='{preparation.StageName}' " +
            $"pos=({preparation.StageX:F1},{preparation.StageY:F1},{preparation.StageZ:F1}) " +
            $"outcome={FormatReadiness(preparation.Readiness, preparation.SpawnedLocalPoolEntries)} " +
            $"searchWaypoints={preparation.SearchWaypoints.Count}";
    }

    public static string FormatReadiness(
        RatchetFishingStageReadiness readiness,
        IReadOnlyList<uint> spawnedLocalPoolEntries)
    {
        var spawnedPools = spawnedLocalPoolEntries.Count > 0
            ? string.Join(",", spawnedLocalPoolEntries.OrderBy(poolEntry => poolEntry))
            : "none";

        return readiness switch
        {
            RatchetFishingStageReadiness.VisiblePoolReady =>
                "visible_pool_ready",
            RatchetFishingStageReadiness.LocalChildSpawnedButInvisible =>
                $"local_child_spawned_but_invisible[{spawnedPools}]",
            RatchetFishingStageReadiness.LocalChildSpawnedOnDirectProbeOnly =>
                $"local_child_spawned_on_direct_probe_only[{spawnedPools}]",
            RatchetFishingStageReadiness.NoLocalChildSpawned =>
                "no_local_child_spawned",
            _ =>
                $"unknown[{spawnedPools}]"
        };
    }
}
