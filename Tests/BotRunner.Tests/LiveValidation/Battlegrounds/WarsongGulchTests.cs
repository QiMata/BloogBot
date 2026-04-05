using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Warsong Gulch battleground integration test.
/// Fixture prep owns revive/level/teleport/GM-off. The coordinator only queues and enters WSG.
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
    public async Task WSG_CoordinatorQueuesAndEntersBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        await _bot.EnsurePreparedAsync();
        await BgTestHelper.WaitForBotsAsync(_bot, _output, WarsongGulchFixture.TotalBotCount, "WSG");
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, WarsongGulchFixture.WsgMapId, WarsongGulchFixture.TotalBotCount, "WSG");

        var snapshots = await _bot.QueryAllSnapshotsAsync();
        Assert.Equal(WarsongGulchFixture.TotalBotCount, BgTestHelper.CountBotsOnMap(snapshots, WarsongGulchFixture.WsgMapId));
    }
}
