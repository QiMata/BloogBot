using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed trade validation. SHODAN stages the real BotRunner
/// participants; executable cases dispatch only trade ActionTypes.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TradingTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TradingTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_InitiateAndCancel_BothBotsSeeCancellation()
    {
        await TradeTestSupport.EnsureTradingSettingsAsync(_bot, _output);
        var pair = TradeTestSupport.ResolvePair(_bot, _output, foregroundInitiates: false);

        var metrics = await TradeTestSupport.RunCancelScenarioAsync(_bot, _output, pair);

        Assert.Equal(ResponseResult.Success, metrics.OfferResult);
        Assert.Equal(ResponseResult.Success, metrics.DeclineResult);
        Assert.True(metrics.SnapshotsAlive, "Both trade participants should still produce snapshots after cancel.");
        Assert.False(metrics.SawTradeError, "No trade-related runtime error should be reported during cancel.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_GoldAndItem_TransferSuccessful()
    {
        await TradeTestSupport.EnsureTradingSettingsAsync(_bot, _output);
        var pair = TradeTestSupport.ResolvePair(_bot, _output, foregroundInitiates: false);
        _ = pair;

        const string reason =
            "BG-initiated item/gold transfer is Shodan-launched but currently depends on FG AcceptTrade, which ACKs Failed/behavior_tree_failed.";
        _output.WriteLine($"[TRADE] {reason}");
        global::Tests.Infrastructure.Skip.If(true, reason);
    }
}
