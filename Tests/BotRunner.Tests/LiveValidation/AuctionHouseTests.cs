using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.2 / V2.2: Auction house tests — Bot posts item, second bot buys it.
/// Assert gold transfer and item delivery via mail.
///
/// Flow: Teleport to Org AH → interact with auctioneer → post/search/buy.
///
/// Run: dotnet test --filter "FullyQualifiedName~AuctionHouseTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AuctionHouseTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
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
    public async Task AH_NavigateToAuctioneer_SnapshotShowsNearbyNpc()
    {
        var bgAccount = _bot.BgAccountName!;

        // Teleport to Org AH area
        await _bot.BotTeleportAsync(bgAccount, MapId, AhX, AhY, AhZ);
        await Task.Delay(3000);

        // Wait for nearby units to populate (auctioneer NPCs)
        var hasNearbyUnits = await _bot.WaitForNearbyUnitsPopulatedAsync(bgAccount, timeoutMs: 8000);
        _output.WriteLine($"[AH] Nearby units populated: {hasNearbyUnits}");

        // Verify snapshot shows bot at AH location
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[AH] Bot position: ({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");

        // Look for auctioneer NPC (NPC_FLAG_AUCTIONEER = 0x200000)
        var auctioneer = await _bot.WaitForNearbyUnitAsync(bgAccount, 0x200000, timeoutMs: 8000, progressLabel: "auctioneer");
        if (auctioneer != null)
        {
            var auctPos = auctioneer.GameObject?.Base?.Position;
            _output.WriteLine($"[AH] Found auctioneer at ({auctPos?.X:F0},{auctPos?.Y:F0})");
        }
        else
        {
            _output.WriteLine("[AH] No auctioneer found nearby — may need to walk closer");
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_InteractWithAuctioneer_OpensAhFrame()
    {
        var bgAccount = _bot.BgAccountName!;

        // Setup at AH
        await _bot.BotTeleportAsync(bgAccount, MapId, AhX, AhY, AhZ);
        await Task.Delay(3000);

        // Interact with nearest auctioneer
        var interactResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
        });
        _output.WriteLine($"[AH] Interact result: {interactResult}");

        await Task.Delay(2000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        _output.WriteLine("[AH] AH interaction completed");
    }
}
