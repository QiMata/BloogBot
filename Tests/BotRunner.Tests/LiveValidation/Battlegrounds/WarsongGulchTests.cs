using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Warsong Gulch 10v10 integration test.
/// Level 60, full PvP loadout (gear, elixirs, mount), 20 BG bots.
/// Pipeline: enter world → loadout prep → queue → enter WSG → mount → verify.
/// </summary>
[Collection(WarsongGulchCollection.Name)]
public class WarsongGulchTests
{
    private readonly WarsongGulchFixture _bot;
    private readonly ITestOutputHelper _output;

    public WarsongGulchTests(WarsongGulchFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task WSG_FullMatch_PrepQueueEnterAndMount()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        // Phase 1: All 20 bots enter world
        await BgTestHelper.WaitForBotsAsync(_bot, _output, WarsongGulchFixture.TotalBotCount, "WSG");

        // Phase 2: Level 60 loadout prep (PvP gear, elixirs, mount spell)
        await _bot.EnsureLoadoutPreparedAsync();

        // Phase 3: Queue and enter WSG (individual queue, accept >= 75%)
        var minBotsOnMap = (int)(WarsongGulchFixture.TotalBotCount * 0.75);
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, WarsongGulchFixture.WsgMapId, minBotsOnMap, "WSG");

        // Phase 4: Mount up inside WSG
        await _bot.MountAllBotsAsync();
        await BgTestHelper.WaitForAccountsMountedAsync(
            _bot,
            _output,
            WarsongGulchFixture.HordeAccountsOrdered.Concat(WarsongGulchFixture.AllianceAccountsOrdered),
            expectedMounted: minBotsOnMap,
            phaseName: "WSG:Mount");

        // Phase 5: Verify bots are on WSG map and mounted
        var snapshots = await _bot.QueryAllSnapshotsAsync();
        var onWsg = BgTestHelper.CountBotsOnMap(snapshots, WarsongGulchFixture.WsgMapId);
        _output.WriteLine($"[WSG:Final] {onWsg} bots on WSG map, {snapshots.Count} total");
        Assert.True(onWsg >= minBotsOnMap, $"Expected >= {minBotsOnMap} bots on WSG, got {onWsg}");
    }
}
