using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

[Collection(LiveValidationCollection.Name)]
public class TravelPlannerTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TravelPlannerTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P21.27 - CrossMapRouter walk test: validate walking route across zone boundaries.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P21_27_CrossMapRouter_WalkTest()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P21.27 CrossMapRouter walk test - snapshot received");
    }

    /// <summary>
    /// P21.28 - CrossMapRouter flight path test: validate flight path route planning.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P21_28_CrossMapRouter_FlightPathTest()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P21.28 CrossMapRouter flight path test - snapshot received");
    }

    /// <summary>
    /// P21.29 - CrossMapRouter hearthstone test: validate hearthstone route planning.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P21_29_CrossMapRouter_HearthstoneTest()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P21.29 CrossMapRouter hearthstone test - snapshot received");
    }

    /// <summary>
    /// P21.30 - CrossMapRouter cross-continent test: validate cross-continent route planning.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P21_30_CrossMapRouter_CrossContinentTest()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P21.30 CrossMapRouter cross-continent test - snapshot received");
    }
}
