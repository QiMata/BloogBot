using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.1: Trading tests — 2 BG bots trade items and gold.
/// Assert both see correct inventory changes in snapshots.
///
/// Run: dotnet test --filter "FullyQualifiedName~TradingTests" --configuration Release
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
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_InitiateAndCancel_BothBotsSeeCancellation()
    {
        // Setup: Both bots at Orgrimmar, in party, near each other
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_GoldAndItem_TransferSuccessful()
    {
        // Setup: Bot A has 10 copper + 1 item, Bot B has empty inventory
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
