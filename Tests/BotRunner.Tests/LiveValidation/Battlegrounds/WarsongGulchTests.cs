using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Warsong Gulch battleground entry validation.
/// Setup still requires both teams, but this test only verifies the raid-prep -> queue -> map-entry path.
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
    public async Task WSG_PreparedRaid_QueueAndEnterBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        await BgTestHelper.WaitForBotsAsync(_bot, _output, WarsongGulchFixture.TotalBotCount, "WSG");
        // Loadout prep is now driven by the BattlegroundCoordinator's ApplyingLoadouts
        // state (P3.4) off the CharacterSettings.Loadout stamped at fixture init (P3.6).

        var minBotsOnMap = (int)(WarsongGulchFixture.TotalBotCount * 0.75);
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, WarsongGulchFixture.WsgMapId, minBotsOnMap, "WSG");

        var snapshots = await _bot.QueryAllSnapshotsAsync(logDiagnostics: true);
        var onWsg = BgTestHelper.CountBotsOnMap(snapshots, WarsongGulchFixture.WsgMapId);
        _output.WriteLine($"[WSG:Final] onWsg={onWsg}, totalSnapshots={snapshots.Count}");
        Assert.True(onWsg >= minBotsOnMap, $"Expected >= {minBotsOnMap} bots on WSG, got {onWsg}");
    }
}
