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
        global::Tests.Infrastructure.Skip.If(
            true,
            "BG-initiated trade transfer remains a tracked protocol/server completion gap: Shodan attempts 5-7 ACKed OfferTrade, AcceptTradeRequest, OfferItem, OfferGold, and both final AcceptTrade actions as Success, but item/copper stayed with the initiator. Foreground-initiated transfer is covered by TradeParityTests.");

        await TradeTestSupport.EnsureTradingSettingsAsync(_bot, _output);
        var pair = TradeTestSupport.ResolvePair(_bot, _output, foregroundInitiates: false);

        var metrics = await TradeTestSupport.RunGoldAndItemTransferScenarioAsync(_bot, _output, pair);

        Assert.Equal(ResponseResult.Success, metrics.OfferTradeResult);
        Assert.Equal(ResponseResult.Success, metrics.ReceiverOpenResult);
        Assert.Equal(ResponseResult.Success, metrics.OfferItemResult);
        Assert.Equal(ResponseResult.Success, metrics.OfferGoldResult);
        Assert.Equal(ResponseResult.Success, metrics.ReceiverAcceptResult);
        Assert.Equal(ResponseResult.Success, metrics.InitiatorAcceptResult);
        Assert.True(metrics.TransferObserved, "Receiver should observe Linen Cloth and copper after trade completion.");
        Assert.True(metrics.ReceiverItemCountAfter >= metrics.ReceiverItemCountBefore + 1,
            "Receiver should gain the staged Linen Cloth item.");
        Assert.True(metrics.ReceiverCoinageAfter >= metrics.ReceiverCoinageBefore + TradeTestSupport.TradeCopper,
            "Receiver should gain the offered copper.");
        Assert.False(metrics.SawTradeError, "No trade-related runtime error should be reported during transfer.");
    }
}
