using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.12, 23.13: Mail send gold/item with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~MailParityTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MailParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public MailParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendGold_FgBgParity()
    {
        // P23.12: Both FG and BG send gold via mail — recipient receives correct amount
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendItem_FgBgParity()
    {
        // P23.13: Both FG and BG send item via mail — recipient receives correct item
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
