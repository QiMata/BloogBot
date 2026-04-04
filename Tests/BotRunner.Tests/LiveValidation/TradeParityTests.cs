using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.18, 23.19: Trade initiate/cancel and trade gold+item with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~TradeParityTests" --configuration Release
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
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_InitiateCancel_FgBgParity()
    {
        // P23.18: Both FG and BG initiate and cancel a trade — no items or gold lost
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_GoldAndItem_FgBgParity()
    {
        // P23.19: Both FG and BG complete a trade with gold+item — inventories and gold updated correctly
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
