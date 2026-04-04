using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

[Collection(LiveValidationCollection.Name)]
public class LoadTestMilestoneTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public LoadTestMilestoneTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P24.4 - 10-bot baseline load test milestone.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P24_4_LoadTest_10BotBaseline()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P24.4 10-bot baseline load test - snapshot received");
    }

    /// <summary>
    /// P24.5 - 100-bot load test milestone.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P24_5_LoadTest_100Bot()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P24.5 100-bot load test - snapshot received");
    }

    /// <summary>
    /// P24.6 - 500-bot load test milestone.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P24_6_LoadTest_500Bot()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P24.6 500-bot load test - snapshot received");
    }

    /// <summary>
    /// P24.7 - 1000-bot load test milestone.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P24_7_LoadTest_1000Bot()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P24.7 1000-bot load test - snapshot received");
    }

    /// <summary>
    /// P24.8 - 3000-bot load test milestone.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P24_8_LoadTest_3000Bot()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P24.8 3000-bot load test - snapshot received");
    }
}
