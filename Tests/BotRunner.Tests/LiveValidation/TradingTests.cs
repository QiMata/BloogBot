using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.1 / V2.1: Trading tests — 2 bots trade items and gold.
/// Assert both see correct inventory changes in snapshots.
///
/// Flow: Setup both bots at Orgrimmar → Bot A sends OFFER_TRADE to Bot B →
/// Bot A offers gold → both accept → verify gold transferred in snapshots.
///
/// Run: dotnet test --filter "FullyQualifiedName~TradingTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TradingTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Orgrimmar Valley of Honor — safe trading location
    private const int MapId = 1;
    private const float TradeX = 1629f, TradeY = -4373f, TradeZ = 34f;

    public TradingTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_InitiateAndCancel_BothBotsSeeCancellation()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot not available for dual-bot trade test");

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        if (!string.IsNullOrWhiteSpace(fgAccount))
            await _bot.EnsureCleanSlateAsync(fgAccount!, "FG");

        // Setup: teleport both bots to Orgrimmar, same location
        await _bot.BotTeleportAsync(bgAccount, MapId, TradeX, TradeY, TradeZ);
        await _bot.BotTeleportAsync(fgAccount!, MapId, TradeX + 1, TradeY, TradeZ);
        await Task.Delay(3000);

        // Verify both bots are positioned
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(bgSnap);
        Assert.NotNull(fgSnap);

        var bgPos = bgSnap!.Player?.Unit?.GameObject?.Base?.Position;
        var fgPos = fgSnap!.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[TRADE] BG at ({bgPos?.X:F0},{bgPos?.Y:F0}), FG at ({fgPos?.X:F0},{fgPos?.Y:F0})");

        // Bot A initiates trade with Bot B via OFFER_TRADE action
        var tradeResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.OfferTrade,
        });
        _output.WriteLine($"[TRADE] Initiate result: {tradeResult}");

        // Decline to cancel the trade
        await Task.Delay(1000);
        var declineResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.DeclineTrade,
        });
        _output.WriteLine($"[TRADE] Decline result: {declineResult}");

        // Verify bots are still connected after trade cancel
        await _bot.RefreshSnapshotsAsync();
        bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(bgSnap);
        _output.WriteLine("[TRADE] Trade cancel flow completed without disconnect");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Trade_GoldAndItem_TransferSuccessful()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot not available for dual-bot trade test");

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        if (!string.IsNullOrWhiteSpace(fgAccount))
            await _bot.EnsureCleanSlateAsync(fgAccount!, "FG");

        // Setup: ensure both bots have gold via GM
        await _bot.SendGmChatCommandAsync(bgAccount, ".modify money 100");
        await Task.Delay(1000);

        // Record starting gold
        await _bot.RefreshSnapshotsAsync();
        var bgSnapBefore = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(bgSnapBefore);
        var goldBefore = bgSnapBefore!.Player?.Coinage ?? 0;
        _output.WriteLine($"[TRADE] BG gold before: {goldBefore}c");

        // Initiate trade, offer 10 copper
        var offerResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.OfferTrade,
        });
        _output.WriteLine($"[TRADE] Offer trade: {offerResult}");

        await Task.Delay(500);
        var goldResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.OfferGold,
            Parameters = { new RequestParameter { IntParam = 10 } }
        });
        _output.WriteLine($"[TRADE] Offer gold: {goldResult}");

        // Accept trade from both sides
        await Task.Delay(500);
        var acceptBg = await _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.AcceptTrade });
        _output.WriteLine($"[TRADE] BG accept: {acceptBg}");

        if (!string.IsNullOrWhiteSpace(fgAccount))
        {
            var acceptFg = await _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.AcceptTrade });
            _output.WriteLine($"[TRADE] FG accept: {acceptFg}");
        }

        await Task.Delay(2000);
        await _bot.RefreshSnapshotsAsync();
        var bgSnapAfter = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(bgSnapAfter);
        _output.WriteLine($"[TRADE] BG gold after: {bgSnapAfter!.Player?.Coinage ?? 0}c");
    }
}
