using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed trade parity coverage. SHODAN launches the parity topology
/// and FG/BG receive only trade action dispatches.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TradeParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TradeParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_InitiateCancel_FgBgParity()
    {
        await TradeTestSupport.EnsureTradingSettingsAsync(_bot, _output);
        var pair = TradeTestSupport.ResolvePair(_bot, _output, foregroundInitiates: true);

        var metrics = await TradeTestSupport.RunCancelScenarioAsync(_bot, _output, pair);

        Assert.Equal(ResponseResult.Success, metrics.OfferResult);
        Assert.Equal(ResponseResult.Success, metrics.DeclineResult);
        Assert.True(metrics.SnapshotsAlive, "Both trade participants should still produce snapshots after foreground cancel.");
        Assert.False(metrics.SawTradeError, "No trade-related runtime error should be reported during foreground cancel.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_GoldAndItem_FgBgParity()
    {
        await TradeTestSupport.EnsureTradingSettingsAsync(_bot, _output);
        var pair = TradeTestSupport.ResolvePair(_bot, _output, foregroundInitiates: true);

        var metrics = await TradeTestSupport.RunGoldAndItemTransferScenarioAsync(_bot, _output, pair);

        Assert.Equal(ResponseResult.Success, metrics.OfferTradeResult);
        Assert.Equal(ResponseResult.Success, metrics.ReceiverOpenResult);
        Assert.Equal(ResponseResult.Success, metrics.OfferItemResult);
        Assert.Equal(ResponseResult.Success, metrics.OfferGoldResult);
        Assert.Equal(ResponseResult.Success, metrics.ReceiverAcceptResult);
        Assert.Equal(ResponseResult.Success, metrics.InitiatorAcceptResult);
        Assert.True(metrics.TransferObserved, "BG receiver should observe Linen Cloth and copper after reverse trade completion.");
        Assert.True(metrics.ReceiverItemCountAfter >= metrics.ReceiverItemCountBefore + 1,
            "BG receiver should gain the staged Linen Cloth item.");
        Assert.True(metrics.ReceiverCoinageAfter >= metrics.ReceiverCoinageBefore + TradeTestSupport.TradeCopper,
            "BG receiver should gain the offered copper.");
        Assert.False(metrics.SawTradeError, "No trade-related runtime error should be reported during reverse transfer.");
    }
}
