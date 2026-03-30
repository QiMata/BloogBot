using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Arathi Basin and Alterac Valley entry tests.
///
/// Run:
///   dotnet test --filter "Namespace~Battlegrounds" --configuration Release -v n --blame-hang --blame-hang-timeout 60m
/// </summary>

[Collection(ArathiBasinCollection.Name)]
public class ArathiBasinTests
{
    private readonly ArathiBasinFixture _bot;
    private readonly ITestOutputHelper _output;

    public ArathiBasinTests(ArathiBasinFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task AB_AllBotsEnterWorld()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB");
    }

    [SkippableFact]
    public async Task AB_QueueAndEnterBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB");
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, ArathiBasinFixture.AbMapId, 15, "AB");
    }
}

[Collection(AlteracValleyCollection.Name)]
public class AlteracValleyTests
{
    private readonly AlteracValleyFixture _bot;
    private readonly ITestOutputHelper _output;

    public AlteracValleyTests(AlteracValleyFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task AV_AllBotsEnterWorld()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        await BgTestHelper.WaitForBotsAsync(_bot, _output, AlteracValleyFixture.TotalBotCount, "AV");
    }

    [SkippableFact]
    public async Task AV_QueueAndEnterBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        await BgTestHelper.WaitForBotsAsync(_bot, _output, AlteracValleyFixture.TotalBotCount, "AV");
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, AlteracValleyFixture.AvMapId, 40, "AV");
    }
}

/// <summary>Shared BG test helper methods.</summary>
internal static class BgTestHelper
{
    public static async Task WaitForBotsAsync(LiveBotFixture bot, ITestOutputHelper output, int expected, string bgName)
    {
        var sw = Stopwatch.StartNew();
        var lastCount = 0;
        var lastChange = sw.Elapsed;

        while (sw.Elapsed < TimeSpan.FromMinutes(5))
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{bgName}:Enter] CRASHED");

            await bot.RefreshSnapshotsAsync();
            var count = bot.AllBots.Count;

            if (count >= expected)
            {
                output.WriteLine($"[{bgName}:Enter] All {count} bots entered world at {sw.Elapsed.TotalSeconds:F0}s");
                return;
            }

            if (count != lastCount)
            {
                output.WriteLine($"[{bgName}:Enter] {count}/{expected} bots at {sw.Elapsed.TotalSeconds:F0}s");
                lastCount = count;
                lastChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastChange > TimeSpan.FromSeconds(60))
                Assert.Fail($"[{bgName}:Enter] STALE — stuck at {count}/{expected}");

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"[{bgName}:Enter] TIMEOUT — only {lastCount}/{expected} bots entered");
    }

    public static async Task WaitForBgEntryAsync(LiveBotFixture bot, ITestOutputHelper output, uint bgMapId, int minOnMap, string bgName)
    {
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastChange = sw.Elapsed;

        while (sw.Elapsed < TimeSpan.FromMinutes(15))
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{bgName}:BG] CRASHED");

            await bot.RefreshSnapshotsAsync();
            var onBg = bot.AllBots.Count(s => (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == bgMapId);
            var grouped = bot.AllBots.Count(s => s.PartyLeaderGuid != 0);
            var fingerprint = $"bg={onBg},grp={grouped}";

            if (onBg >= minOnMap)
            {
                output.WriteLine($"[{bgName}:BG] {onBg} bots on BG map at {sw.Elapsed.TotalSeconds:F0}s");
                Assert.True(onBg >= minOnMap);
                return;
            }

            if (fingerprint != lastFingerprint)
            {
                output.WriteLine($"[{bgName}:BG] {fingerprint} at {sw.Elapsed.TotalSeconds:F0}s");
                lastFingerprint = fingerprint;
                lastChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastChange > TimeSpan.FromMinutes(2))
                Assert.Fail($"[{bgName}:BG] STALE — {fingerprint}");

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        Assert.Fail($"[{bgName}:BG] TIMEOUT");
    }
}
