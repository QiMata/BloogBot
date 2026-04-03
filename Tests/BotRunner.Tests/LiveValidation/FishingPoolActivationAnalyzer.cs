using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BotRunner.Tests.LiveValidation;

internal enum FishingPoolActivationState
{
    Unknown,
    Empty,
    Spawned
}

internal enum FishingPoolBlockerKind
{
    LocalPoolSpawnedButInvisible,
    MasterPoolSelectedNonLocal,
    LocalPoolSpawnedOnlyOnDirectProbe,
    NoChildPoolsSpawned
}

internal sealed record FishingPoolActivationSite(
    uint PoolEntry,
    string? PoolDescription,
    float X,
    float Y,
    float Z,
    float DistanceToPacketCapture,
    float DistanceToParity,
    bool IsLocalRatchetChild);

internal sealed record FishingPoolActivationProbe(
    FishingPoolActivationSite Site,
    FishingPoolActivationState State,
    IReadOnlyList<string> Responses);

internal static class FishingPoolActivationAnalyzer
{
    public static IReadOnlyList<FishingPoolActivationSite> MaterializeSites(
        IEnumerable<(uint poolEntry, string? poolDescription, uint entry, int map, float x, float y, float z)> childSpawns,
        IReadOnlyCollection<uint> localPoolEntries,
        float packetStageX,
        float packetStageY,
        float parityStageX,
        float parityStageY)
        => childSpawns
            .GroupBy(spawn => spawn.poolEntry)
            .Select(group =>
            {
                var sample = group.First();
                return new FishingPoolActivationSite(
                    sample.poolEntry,
                    sample.poolDescription,
                    sample.x,
                    sample.y,
                    sample.z,
                    Distance2D(packetStageX, packetStageY, sample.x, sample.y),
                    Distance2D(parityStageX, parityStageY, sample.x, sample.y),
                    localPoolEntries.Contains(sample.poolEntry));
            })
            .OrderBy(site => site.PoolEntry)
            .ToList();

    public static FishingPoolActivationState ClassifyPoolSpawnStateResponses(uint poolEntry, IEnumerable<string> responses)
    {
        var materializedResponses = responses
            .Where(response => !string.IsNullOrWhiteSpace(response))
            .ToArray();

        if (materializedResponses.Length == 0)
            return FishingPoolActivationState.Unknown;

        if (materializedResponses.Any(response => IsSpawnRowForPool(response, poolEntry)))
            return FishingPoolActivationState.Spawned;

        if (materializedResponses.Any(response => IsPositivePoolUpdateCountLine(response, poolEntry)))
            return FishingPoolActivationState.Spawned;

        return FishingPoolActivationState.Unknown;
    }

    public static IReadOnlyList<FishingPoolActivationProbe> MaterializeProbes(
        IReadOnlyList<FishingPoolActivationSite> sites,
        IReadOnlyDictionary<uint, IReadOnlyList<string>> responsesByPool)
        => sites
            .Select(site =>
            {
                var responses = responsesByPool.TryGetValue(site.PoolEntry, out var mappedResponses)
                    ? mappedResponses
                    : Array.Empty<string>();
                return new FishingPoolActivationProbe(
                    site,
                    ClassifyPoolSpawnStateResponses(site.PoolEntry, responses),
                    responses);
            })
            .ToList();

    public static FishingPoolBlockerKind DetermineBlockerKind(
        bool localPoolSpawnedDuringStage,
        IReadOnlyList<FishingPoolActivationProbe> probes)
    {
        if (localPoolSpawnedDuringStage)
            return FishingPoolBlockerKind.LocalPoolSpawnedButInvisible;

        var localSpawnedOnDirectProbe = probes.Any(probe => probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Spawned);
        if (localSpawnedOnDirectProbe)
            return FishingPoolBlockerKind.LocalPoolSpawnedOnlyOnDirectProbe;

        var nonLocalSpawned = probes.Any(probe => !probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Spawned);
        if (nonLocalSpawned)
            return FishingPoolBlockerKind.MasterPoolSelectedNonLocal;

        return FishingPoolBlockerKind.NoChildPoolsSpawned;
    }

    public static string FormatSummary(IReadOnlyList<FishingPoolActivationProbe> probes)
    {
        static string FormatPoolList(IEnumerable<FishingPoolActivationProbe> selected)
            => string.Join(", ", selected.Select(probe => $"{probe.Site.PoolEntry}@({probe.Site.X:F1},{probe.Site.Y:F1},{probe.Site.Z:F1})"));

        var localSpawned = probes.Where(probe => probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Spawned).ToArray();
        var localEmpty = probes.Where(probe => probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Empty).ToArray();
        var nonLocalSpawned = probes.Where(probe => !probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Spawned).ToArray();
        var nonLocalEmpty = probes.Where(probe => !probe.Site.IsLocalRatchetChild && probe.State == FishingPoolActivationState.Empty).ToArray();
        var unknown = probes.Where(probe => probe.State == FishingPoolActivationState.Unknown).ToArray();

        return
            $"localSpawned=[{FormatPoolList(localSpawned)}] " +
            $"localEmpty=[{FormatPoolList(localEmpty)}] " +
            $"nonLocalSpawned=[{FormatPoolList(nonLocalSpawned)}] " +
            $"nonLocalEmpty=[{FormatPoolList(nonLocalEmpty)}] " +
            $"unknown=[{FormatPoolList(unknown)}]";
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool IsPoolCommandErrorResponse(string response)
        => response.Contains("syntax", StringComparison.OrdinalIgnoreCase)
           || response.Contains("error", StringComparison.OrdinalIgnoreCase)
           || response.Contains("non-instance", StringComparison.OrdinalIgnoreCase)
           || response.Contains("not found", StringComparison.OrdinalIgnoreCase)
           || response.Contains("unknown", StringComparison.OrdinalIgnoreCase);

    private static bool IsSpawnRowForPool(string response, uint poolEntry)
    {
        if (string.IsNullOrWhiteSpace(response) || IsPoolCommandErrorResponse(response))
            return false;

        if (response.Contains($"Pool #{poolEntry}:", StringComparison.OrdinalIgnoreCase))
            return false;

        return response.Contains($"pool {poolEntry}", StringComparison.OrdinalIgnoreCase)
            || response.Contains($"poolid {poolEntry}", StringComparison.OrdinalIgnoreCase)
            || response.Contains($"pool={poolEntry}", StringComparison.OrdinalIgnoreCase)
            || response.Contains($"[pool {poolEntry}]", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPositivePoolUpdateCountLine(string response, uint poolEntry)
    {
        if (string.IsNullOrWhiteSpace(response) || IsPoolCommandErrorResponse(response))
            return false;

        var match = Regex.Match(
            response,
            $@"Pool\s*#\s*{poolEntry}\s*:\s*(\d+)\s+objects\s+spawned",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return false;

        return int.TryParse(match.Groups[1].Value, out var count) && count > 0;
    }
}
