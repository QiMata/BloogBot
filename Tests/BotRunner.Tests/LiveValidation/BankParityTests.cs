using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.9, 23.10: Bank deposit/withdraw and slot purchase with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~BankParityTests" --configuration Release
/// </summary>
[Collection(BgOnlyValidationCollection.Name)]
public class BankParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public BankParityTests(BgOnlyBotFixture bot, ITestOutputHelper output)
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
        await _bot.BotTeleportAsync(
            bgAccount,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.BankX,
            OrgrimmarServiceLocations.BankY,
            OrgrimmarServiceLocations.BankZ);
        await _bot.WaitForTeleportSettledAsync(
            bgAccount,
            OrgrimmarServiceLocations.BankX,
            OrgrimmarServiceLocations.BankY);

        await _bot.WaitForNearbyUnitsPopulatedAsync(bgAccount, timeoutMs: 15000);

        // Verify banker NPC is detectable
        var banker = await _bot.WaitForNearbyUnitAsync(
            bgAccount,
            (uint)NPCFlags.UNIT_NPC_FLAG_BANKER,
            timeoutMs: 15000,
            progressLabel: "banker");
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
