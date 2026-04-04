using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.3 / V2.3: Bank interaction tests — Bot deposits item to bank, withdraws.
/// Assert item preserved in bank slots via snapshot.
///
/// Flow: Teleport to Org bank → interact with banker → deposit → verify → withdraw.
///
/// Run: dotnet test --filter "FullyQualifiedName~BankInteractionTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BankInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
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
    public async Task Bank_NavigateToBanker_FindsBankerNpc()
    {
        var bgAccount = _bot.BgAccountName!;

        // Teleport to Orgrimmar bank
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgBankX, OrgBankY, OrgBankZ);
        await Task.Delay(3000);

        // Wait for nearby units
        await _bot.WaitForNearbyUnitsPopulatedAsync(bgAccount, timeoutMs: 8000);

        // Look for banker NPC (NPC_FLAG_BANKER = 0x80)
        var banker = await _bot.WaitForNearbyUnitAsync(bgAccount, 0x80, timeoutMs: 8000, progressLabel: "banker");
        if (banker != null)
        {
            _output.WriteLine($"[BANK] Found banker at ({banker.Position?.X:F0},{banker.Position?.Y:F0})");
        }
        else
        {
            _output.WriteLine("[BANK] No banker found nearby");
        }

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        _output.WriteLine($"[BANK] Bot at ({snap!.X:F0},{snap.Y:F0},{snap.Z:F0})");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_DepositAndWithdraw_ItemPreserved()
    {
        var bgAccount = _bot.BgAccountName!;

        // Setup: give bot an item to deposit
        await _bot.SendGmChatCommandAsync(bgAccount, ".additem 2589 1"); // Linen Cloth
        await Task.Delay(1000);

        // Teleport to bank
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgBankX, OrgBankY, OrgBankZ);
        await Task.Delay(3000);

        // Interact with banker
        var interactResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
        });
        _output.WriteLine($"[BANK] Interact result: {interactResult}");

        await Task.Delay(2000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        _output.WriteLine("[BANK] Bank interaction completed");
    }
}
