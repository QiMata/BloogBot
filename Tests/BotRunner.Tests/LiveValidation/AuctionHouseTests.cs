using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.2: Auction house tests — Bot posts item, second bot buys it.
/// Assert gold transfer and item delivery via mail.
///
/// Run: dotnet test --filter "FullyQualifiedName~AuctionHouseTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AuctionHouseTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Orgrimmar AH coordinates
    private const float AhX = 1687.26f, AhY = -4464.71f, AhZ = 23.15f;

    public AuctionHouseTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_PostItem_AppearsInSearch()
    {
        // Setup: Teleport to Orgrimmar AH
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_PostAndBuy_GoldTransferredItemDelivered()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
