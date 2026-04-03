using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Travel;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Dungeons;

/// <summary>
/// Shared dungeon entry test logic. Each dungeon collection's test class delegates here.
/// Validates: all bots enter world, coordinator forms group, bots enter instance map.
/// </summary>
public static class DungeonEntryTestRunner
{
    /// <summary>
    /// Core dungeon entry test: bots enter world → coordinator preps → enter instance.
    /// </summary>
    public static async Task RunDungeonEntryTest(
        DungeonInstanceFixture bot,
        ITestOutputHelper output,
        int expectedBotCount = 10)
    {
        var dungeon = bot.Dungeon!;
        output.WriteLine($"=== {dungeon.Name} ({dungeon.Abbreviation}) ===");
        output.WriteLine($"Instance MapId: {dungeon.InstanceMapId}");
        output.WriteLine($"Entrance: map={dungeon.EntranceMapId} @ ({dungeon.EntrancePosition.X:F0},{dungeon.EntrancePosition.Y:F0},{dungeon.EntrancePosition.Z:F0})");
        if (dungeon.MeetingStonePosition != null)
            output.WriteLine($"Meeting Stone: ({dungeon.MeetingStonePosition.X:F0},{dungeon.MeetingStonePosition.Y:F0},{dungeon.MeetingStonePosition.Z:F0})");

        Assert.True(bot.IsReady, bot.FailureReason ?? "Fixture not ready");

        // Phase 1: Bots enter world
        await WaitForProgressAsync(bot, output,
            phaseName: "BotsEnterWorld",
            maxTimeout: TimeSpan.FromMinutes(2),
            staleTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(3),
            evaluate: snapshots =>
            {
                var count = snapshots.Count;
                return (count == expectedBotCount, count, $"bots={count}");
            });

        Assert.Equal(expectedBotCount, bot.AllBots.Count);

        // Phase 2: Coordinator pipeline — group, travel, enter dungeon
        var botsOnInstanceMap = await WaitForProgressAsync(bot, output,
            phaseName: $"Enter_{dungeon.Abbreviation}",
            maxTimeout: TimeSpan.FromMinutes(8),
            staleTimeout: TimeSpan.FromSeconds(60),
            pollInterval: TimeSpan.FromSeconds(5),
            evaluate: snapshots =>
            {
                var grouped = snapshots.Count(s => s.PartyLeaderGuid != 0);
                var onInstance = snapshots.Count(s =>
                    (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == dungeon.InstanceMapId);
                var totalSpells = snapshots.Sum(s => s.Player?.SpellList?.Count ?? 0);

                var posHash = string.Join("|", snapshots.Select(s =>
                {
                    var p = s.Player?.Unit?.GameObject?.Base?.Position;
                    var m = s.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                    return $"{m}:{p?.X:F0},{p?.Y:F0}";
                }));

                var fingerprint = $"grp={grouped},instance={onInstance},spells={totalSpells}," +
                    $"pos={posHash.GetHashCode():X8}";
                return (onInstance >= 2, onInstance, fingerprint);
            });

        // Final report
        await bot.RefreshSnapshotsAsync();
        var finalBots = bot.AllBots;
        var groupedBots = finalBots.Where(s => s.PartyLeaderGuid != 0).ToList();

        output.WriteLine($"\n=== {dungeon.Abbreviation} ENTRY SUMMARY ===");
        output.WriteLine($"Total bots: {finalBots.Count}");
        output.WriteLine($"Grouped: {groupedBots.Count}");
        output.WriteLine($"On instance map {dungeon.InstanceMapId}: {botsOnInstanceMap}");

        foreach (var snap in finalBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            var hp = snap.Player?.Unit?.Health ?? 0;
            var maxHp = snap.Player?.Unit?.MaxHealth ?? 1;
            output.WriteLine($"  {snap.AccountName}: map={mapId}, HP={hp * 100 / maxHp}%, " +
                $"pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        Assert.Equal(expectedBotCount, finalBots.Count);
        Assert.Equal(expectedBotCount, groupedBots.Count);
        Assert.True(botsOnInstanceMap >= 2,
            $"At least 2 bots must enter {dungeon.Abbreviation} instance map {dungeon.InstanceMapId} (got {botsOnInstanceMap})");
    }

    private static async Task<TResult> WaitForProgressAsync<TResult>(
        DungeonInstanceFixture bot,
        ITestOutputHelper output,
        string phaseName,
        TimeSpan maxTimeout,
        TimeSpan staleTimeout,
        TimeSpan pollInterval,
        Func<IReadOnlyList<WoWActivitySnapshot>, (bool done, TResult result, string fingerprint)> evaluate)
    {
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastFingerprintChange = sw.Elapsed;
        TResult lastResult = default!;

        while (sw.Elapsed < maxTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED — {bot.CrashMessage ?? "process exited"}");

            await bot.RefreshSnapshotsAsync();
            var (done, result, fingerprint) = evaluate(bot.AllBots);
            lastResult = result;

            if (done)
            {
                output.WriteLine($"[{phaseName}] Complete at {sw.Elapsed.TotalSeconds:F0}s");
                return result;
            }

            if (fingerprint != lastFingerprint)
            {
                output.WriteLine($"[{phaseName}] Progress at {sw.Elapsed.TotalSeconds:F0}s: {fingerprint}");
                lastFingerprint = fingerprint;
                lastFingerprintChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastFingerprintChange > staleTimeout)
                Assert.Fail($"[{phaseName}] STALE — no progress for {(sw.Elapsed - lastFingerprintChange).TotalSeconds:F0}s");

            await Task.Delay(pollInterval);
        }

        Assert.Fail($"[{phaseName}] TIMEOUT — {maxTimeout.TotalSeconds:F0}s elapsed");
        return lastResult;
    }
}

// =========================================================================
// Individual dungeon test classes — each uses its collection's fixture.
// The test body delegates to DungeonEntryTestRunner for the standard flow.
// =========================================================================

[Collection(ShadowfangKeepCollection.Name)]
public class ShadowfangKeepTests(ShadowfangKeepFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task SFK_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(BlackfathomDeepsCollection.Name)]
public class BlackfathomDeepsTests(BlackfathomDeepsFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task BFD_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(GnomereganCollection.Name)]
public class GnomereganTests(GnomereganFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task GNOMER_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(RazorfenKraulCollection.Name)]
public class RazorfenKraulTests(RazorfenKraulFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task RFK_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(ScarletMonasteryCollection.Name)]
public class ScarletMonasteryTests(ScarletMonasteryFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task SM_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(RazorfenDownsCollection.Name)]
public class RazorfenDownsTests(RazorfenDownsFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task RFD_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(UldamanCollection.Name)]
public class UldamanTests(UldamanFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task ULDA_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(ZulFarrakCollection.Name)]
public class ZulFarrakTests(ZulFarrakFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task ZF_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(MaraudonCollection.Name)]
public class MaraudonTests(MaraudonFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task MARA_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(SunkenTempleCollection.Name)]
public class SunkenTempleTests(SunkenTempleFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task ST_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(BlackrockDepthsCollection.Name)]
public class BlackrockDepthsTests(BlackrockDepthsFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task BRD_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(LowerBlackrockSpireCollection.Name)]
public class LowerBlackrockSpireTests(LowerBlackrockSpireFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task LBRS_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(UpperBlackrockSpireCollection.Name)]
public class UpperBlackrockSpireTests(UpperBlackrockSpireFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task UBRS_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(DireMaulEastCollection.Name)]
public class DireMaulEastTests(DireMaulEastFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task DME_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(DireMaulWestCollection.Name)]
public class DireMaulWestTests(DireMaulWestFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task DMW_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(DireMaulNorthCollection.Name)]
public class DireMaulNorthTests(DireMaulNorthFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task DMN_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(StratholmeLivingCollection.Name)]
public class StratholmeLivingTests(StratholmeLivingFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task STRAT_LIVE_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(StratholmeUndeadCollection.Name)]
public class StratholmeUndeadTests(StratholmeUndeadFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task STRAT_UD_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(ScholomanceCollection.Name)]
public class ScholomanceTests(ScholomanceFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task SCHOLO_GroupFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}
