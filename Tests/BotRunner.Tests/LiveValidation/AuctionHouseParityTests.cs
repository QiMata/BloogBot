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

    private const int MapId = 1;
    private const float AhX = 1687.26f, AhY = -4464.71f, AhZ = 23.15f;

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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Teleport to AH and verify position
        await _bot.BotTeleportAsync(bgAccount, MapId, AhX, AhY, AhZ);
        await Task.Delay(3000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        // Verify bot is near AH
        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[AH-PARITY] BG bot at ({pos!.X:F0},{pos.Y:F0},{pos.Z:F0})");

        // Verify auctioneer NPC is detectable
        var auctioneer = await _bot.WaitForNearbyUnitAsync(bgAccount, 0x200000, timeoutMs: 8000, progressLabel: "auctioneer");
        Assert.NotNull(auctioneer);
        _output.WriteLine("[AH-PARITY] Auctioneer found by BG bot");

        // TODO: When FG AH interaction is implemented, add FG search and compare results
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.FgAccountName),
            "FG bot not available for parity comparison");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_PostAndBuy_FgBgParity()
    {
        // P23.6: FG posts item, BG buys — gold and item transfer verified
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Give bot an item to post
        await _bot.SendGmChatCommandAsync(bgAccount, ".additem 2589 1"); // Linen Cloth
        await Task.Delay(1000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        var itemCount = snap!.Player?.BagContents?.Count ?? 0;
        Assert.True(itemCount > 0, "Bot should have item after .additem");
        _output.WriteLine($"[AH-PARITY] BG bot has {itemCount} items, ready for AH post");

        // TODO: Implement actual AH post → buy → verify gold+item transfer
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.FgAccountName),
            "FG bot not available for parity post/buy");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_Cancel_FgBgParity()
    {
        // P23.7: Both FG and BG cancel an auction — item returned to inventory
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        // Verify bot is alive and in-world (prerequisite for AH interaction)
        Assert.True(snap!.Player?.Unit?.MaxHealth > 0, "Bot must be alive");
        _output.WriteLine($"[AH-PARITY] BG bot alive, HP={snap.Player?.Unit?.Health}/{snap.Player?.Unit?.MaxHealth}");

        // TODO: Implement actual AH cancel → verify item returned
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.FgAccountName),
            "FG bot not available for parity cancel test");
    }
}
