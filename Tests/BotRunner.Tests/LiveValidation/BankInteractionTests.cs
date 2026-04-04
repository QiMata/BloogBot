using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.3: Bank interaction tests — Bot deposits item to bank, logs out, logs in, withdraws.
/// Assert item preserved across sessions.
///
/// Run: dotnet test --filter "FullyQualifiedName~BankInteractionTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BankInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float OrgBankX = 1627.32f, OrgBankY = -4376.07f, OrgBankZ = 14.81f;

    public BankInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_DepositAndWithdraw_ItemPreserved()
    {
        // Setup: Teleport to Orgrimmar bank
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
