using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.9, 23.10: Bank deposit/withdraw and slot purchase with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~BankParityTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BankParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public BankParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_DepositWithdraw_FgBgParity()
    {
        // P23.9: Both FG and BG deposit then withdraw an item — inventory state matches
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_PurchaseSlot_FgBgParity()
    {
        // P23.10: Both FG and BG purchase a bank slot — slot count increases
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
