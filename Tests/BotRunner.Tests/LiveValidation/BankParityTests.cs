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

    private const int MapId = 1;
    private const float OrgBankX = 1627.32f, OrgBankY = -4376.07f, OrgBankZ = 37f;

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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Give bot an item
        await _bot.SendGmChatCommandAsync(bgAccount, ".additem 2589 1");
        await Task.Delay(1000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        var itemCount = snap!.Player?.BagContents?.Count ?? 0;
        Assert.True(itemCount > 0, "Bot should have item after .additem");
        _output.WriteLine($"[BANK-PARITY] BG bot has {itemCount} items");

        // Teleport near bank
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgBankX, OrgBankY, OrgBankZ);
        await Task.Delay(3000);

        // Verify banker NPC is detectable
        var banker = await _bot.WaitForNearbyUnitAsync(bgAccount, 0x80, timeoutMs: 8000, progressLabel: "banker");
        Assert.NotNull(banker);
        _output.WriteLine("[BANK-PARITY] Banker found by BG bot");

        // TODO: Implement actual deposit → withdraw → verify item count unchanged
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.FgAccountName),
            "FG bot not available for parity comparison");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_PurchaseSlot_FgBgParity()
    {
        // P23.10: Both FG and BG purchase a bank slot — slot count increases
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        Assert.True(snap!.Player?.Unit?.MaxHealth > 0, "Bot must be alive");
        _output.WriteLine($"[BANK-PARITY] BG bot alive, HP={snap.Player?.Unit?.Health}/{snap.Player?.Unit?.MaxHealth}");

        // TODO: Implement actual slot purchase → verify slot count increase
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.FgAccountName),
            "FG bot not available for parity comparison");
    }
}
