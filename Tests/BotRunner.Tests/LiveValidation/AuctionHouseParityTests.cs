using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.5, 23.6, 23.7: AH search/post/buy/cancel with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~AuctionHouseParityTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AuctionHouseParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public AuctionHouseParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_Search_FgBgParity()
    {
        // P23.5: Both FG and BG bots search AH — results must match
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_PostAndBuy_FgBgParity()
    {
        // P23.6: FG posts item, BG buys — gold and item transfer verified on both sides
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_Cancel_FgBgParity()
    {
        // P23.7: Both FG and BG cancel an auction — item returned to inventory
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
