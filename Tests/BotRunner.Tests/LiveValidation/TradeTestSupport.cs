using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

internal static class TradeTestSupport
{
    internal const uint LinenClothItemId = LiveBotFixture.TestItems.LinenCloth;
    internal const int TradeCopper = 10;
    private const uint InitiatorFundingCopper = 10000;
    private static int s_tradeCorrelationSequence;

    internal static async Task EnsureTradingSettingsAsync(
        LiveBotFixture bot,
        ITestOutputHelper output)
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await bot.EnsureSettingsAsync(settingsPath);
        bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(bot.IsReady, bot.FailureReason ?? "Live bot not ready");
        await bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");
    }

    internal static TradePair ResolvePair(
        LiveBotFixture bot,
        ITestOutputHelper output,
        bool foregroundInitiates)
    {
        var targets = bot.ResolveBotRunnerActionTargets();
        var bg = targets.Single(target => !target.IsForeground);
        var fg = targets.SingleOrDefault(target => target.IsForeground);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(fg.AccountName),
            "FG bot not available for Shodan trade pair validation.");

        output.WriteLine(
            $"[ACTION-PLAN] SHODAN {bot.ShodanAccountName}/{bot.ShodanCharacterName}: director only, no trade action dispatch.");
        output.WriteLine(
            $"[ACTION-PLAN] BG {bg.AccountName}/{bg.CharacterName}: trade participant.");
        output.WriteLine(
            $"[ACTION-PLAN] FG {fg.AccountName}/{fg.CharacterName}: trade participant.");

        return foregroundInitiates
            ? new TradePair(fg, bg)
            : new TradePair(bg, fg);
    }

    internal static async Task<TradeCancelMetrics> RunCancelScenarioAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        TradePair pair)
    {
        await StagePairAsync(bot, pair, initiatorHasItem: false, fundInitiator: false);

        var partnerGuid = await WaitForVisiblePartnerGuidAsync(bot, output, pair);
        var offer = await SendTradeActionAsync(bot, output, pair.Initiator, MakeOfferTrade(partnerGuid), "OfferTrade");
        output.WriteLine($"[TRADE] {pair.Initiator.RoleLabel} OfferTrade to 0x{partnerGuid:X}: {offer}");

        await Task.Delay(1500);
        var decline = await SendTradeActionAsync(
            bot,
            output,
            pair.Initiator,
            new ActionMessage { ActionType = ActionType.DeclineTrade },
            "DeclineTrade");
        output.WriteLine($"[TRADE] {pair.Initiator.RoleLabel} DeclineTrade: {decline}");

        await Task.Delay(1000);
        await bot.RefreshSnapshotsAsync();
        var initiatorAfter = await bot.GetSnapshotAsync(pair.Initiator.AccountName);
        var receiverAfter = await bot.GetSnapshotAsync(pair.Receiver.AccountName);

        return new TradeCancelMetrics(
            offer,
            decline,
            initiatorAfter != null && receiverAfter != null,
            HasTradeError(initiatorAfter) || HasTradeError(receiverAfter));
    }

    internal static async Task<TradeTransferMetrics> RunGoldAndItemTransferScenarioAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        TradePair pair)
    {
        await StagePairAsync(bot, pair, initiatorHasItem: true, fundInitiator: true);

        await bot.RefreshSnapshotsAsync();
        var initiatorBefore = await bot.GetSnapshotAsync(pair.Initiator.AccountName);
        var receiverBefore = await bot.GetSnapshotAsync(pair.Receiver.AccountName);
        Assert.NotNull(initiatorBefore);
        Assert.NotNull(receiverBefore);

        var initiatorItemCountBefore = CountItemSlots(initiatorBefore, LinenClothItemId);
        var receiverItemCountBefore = CountItemSlots(receiverBefore, LinenClothItemId);
        var initiatorCoinageBefore = initiatorBefore!.Player?.Coinage ?? 0;
        var receiverCoinageBefore = receiverBefore!.Player?.Coinage ?? 0;
        var (bagId, slotId) = FindItemBagSlot(initiatorBefore, LinenClothItemId);

        Assert.Equal(1, initiatorItemCountBefore);
        Assert.Equal(0, receiverItemCountBefore);
        Assert.True(bagId >= 0, $"{pair.Initiator.RoleLabel}: Linen Cloth should resolve to a bag slot.");
        Assert.True(initiatorCoinageBefore >= TradeCopper,
            $"{pair.Initiator.RoleLabel}: initiator should have enough copper for the trade.");

        var partnerGuid = await WaitForVisiblePartnerGuidAsync(bot, output, pair);
        var offer = await SendTradeActionAsync(bot, output, pair.Initiator, MakeOfferTrade(partnerGuid), "OfferTrade");
        output.WriteLine($"[TRADE] {pair.Initiator.RoleLabel} OfferTrade to 0x{partnerGuid:X}: {offer}");

        await Task.Delay(2000);
        var itemOffer = await SendTradeActionAsync(bot, output, pair.Initiator, MakeOfferItem(bagId, slotId), "OfferItem");
        output.WriteLine($"[TRADE] {pair.Initiator.RoleLabel} OfferItem bag={bagId} slot={slotId}: {itemOffer}");

        await Task.Delay(500);
        var goldOffer = await SendTradeActionAsync(bot, output, pair.Initiator, MakeOfferGold(TradeCopper), "OfferGold");
        output.WriteLine($"[TRADE] {pair.Initiator.RoleLabel} OfferGold {TradeCopper}c: {goldOffer}");

        await Task.Delay(1000);
        var receiverAccept = await SendTradeActionAsync(
            bot,
            output,
            pair.Receiver,
            new ActionMessage { ActionType = ActionType.AcceptTrade },
            "AcceptTrade");
        output.WriteLine($"[TRADE] {pair.Receiver.RoleLabel} AcceptTrade: {receiverAccept}");

        await Task.Delay(500);
        var initiatorAccept = await SendTradeActionAsync(
            bot,
            output,
            pair.Initiator,
            new ActionMessage { ActionType = ActionType.AcceptTrade },
            "AcceptTrade");
        output.WriteLine($"[TRADE] {pair.Initiator.RoleLabel} AcceptTrade: {initiatorAccept}");

        var timer = Stopwatch.StartNew();
        var transferred = await bot.WaitForSnapshotConditionAsync(
            pair.Receiver.AccountName,
            snapshot => CountItemSlots(snapshot, LinenClothItemId) >= receiverItemCountBefore + 1
                && (snapshot.Player?.Coinage ?? 0) >= receiverCoinageBefore + TradeCopper,
            TimeSpan.FromSeconds(14),
            pollIntervalMs: 400,
            progressLabel: $"{pair.Receiver.RoleLabel} trade receive");
        timer.Stop();

        await bot.RefreshSnapshotsAsync();
        var initiatorAfter = await bot.GetSnapshotAsync(pair.Initiator.AccountName);
        var receiverAfter = await bot.GetSnapshotAsync(pair.Receiver.AccountName);
        Assert.NotNull(initiatorAfter);
        Assert.NotNull(receiverAfter);

        return new TradeTransferMetrics(
            offer,
            itemOffer,
            goldOffer,
            receiverAccept,
            initiatorAccept,
            transferred,
            initiatorItemCountBefore,
            CountItemSlots(initiatorAfter, LinenClothItemId),
            receiverItemCountBefore,
            CountItemSlots(receiverAfter, LinenClothItemId),
            initiatorCoinageBefore,
            initiatorAfter!.Player?.Coinage ?? initiatorCoinageBefore,
            receiverCoinageBefore,
            receiverAfter!.Player?.Coinage ?? receiverCoinageBefore,
            (int)timer.ElapsedMilliseconds,
            HasTradeError(initiatorAfter) || HasTradeError(receiverAfter));
    }

    internal static Task StageGoldAndItemTransferPreconditionsAsync(
        LiveBotFixture bot,
        TradePair pair)
        => StagePairAsync(bot, pair, initiatorHasItem: true, fundInitiator: true);

    private static async Task StagePairAsync(
        LiveBotFixture bot,
        TradePair pair,
        bool initiatorHasItem,
        bool fundInitiator)
    {
        await bot.StageBotRunnerLoadoutAsync(
            pair.Initiator.AccountName,
            pair.Initiator.RoleLabel,
            itemsToAdd: initiatorHasItem
                ? new[] { new LiveBotFixture.ItemDirective(LinenClothItemId, 1) }
                : null,
            cleanSlate: true,
            clearInventoryFirst: true);

        await bot.StageBotRunnerLoadoutAsync(
            pair.Receiver.AccountName,
            pair.Receiver.RoleLabel,
            cleanSlate: true,
            clearInventoryFirst: true);

        if (fundInitiator)
        {
            await bot.StageBotRunnerCoinageAsync(
                pair.Initiator.AccountName,
                pair.Initiator.RoleLabel,
                InitiatorFundingCopper);
        }

        var initiatorStaged = await bot.StageBotRunnerAtOrgrimmarTradeSpotAsync(
            pair.Initiator.AccountName,
            pair.Initiator.RoleLabel,
            xOffset: -1.0f,
            cleanSlate: false);
        Assert.True(initiatorStaged, $"{pair.Initiator.RoleLabel}: expected to stage at the Orgrimmar trade spot.");

        var receiverStaged = await bot.StageBotRunnerAtOrgrimmarTradeSpotAsync(
            pair.Receiver.AccountName,
            pair.Receiver.RoleLabel,
            xOffset: 1.0f,
            cleanSlate: false);
        Assert.True(receiverStaged, $"{pair.Receiver.RoleLabel}: expected to stage at the Orgrimmar trade spot.");
    }

    private static async Task<ulong> WaitForVisiblePartnerGuidAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        TradePair pair)
    {
        ulong receiverGuid = 0;
        var gotReceiverGuid = await bot.WaitForSnapshotConditionAsync(
            pair.Receiver.AccountName,
            snapshot =>
            {
                receiverGuid = snapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
                return receiverGuid != 0;
            },
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{pair.Receiver.RoleLabel} self guid");

        Assert.True(gotReceiverGuid && receiverGuid != 0,
            $"{pair.Receiver.RoleLabel}: expected a self GUID before trade.");

        var visible = await bot.WaitForSnapshotConditionAsync(
            pair.Initiator.AccountName,
            snapshot => snapshot.NearbyUnits.Any(unit =>
                (unit.GameObject?.Base?.ObjectType ?? 0) == (uint)WoWObjectType.Player
                && (unit.GameObject?.Base?.Guid ?? 0UL) == receiverGuid),
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: $"{pair.Initiator.RoleLabel} sees {pair.Receiver.RoleLabel}");

        await bot.RefreshSnapshotsAsync();
        var initiatorSnapshot = await bot.GetSnapshotAsync(pair.Initiator.AccountName);
        var visiblePlayers = initiatorSnapshot?.NearbyUnits
            .Where(unit => (unit.GameObject?.Base?.ObjectType ?? 0) == (uint)WoWObjectType.Player)
            .Select(unit => $"{unit.GameObject?.Name ?? "?"}=0x{unit.GameObject?.Base?.Guid ?? 0UL:X}")
            .ToArray() ?? [];

        output.WriteLine(
            $"[TRADE] {pair.Initiator.RoleLabel} visible players: {string.Join(", ", visiblePlayers)}; " +
            $"{pair.Receiver.RoleLabel}=0x{receiverGuid:X}");

        Assert.True(visible,
            $"{pair.Initiator.RoleLabel}: expected to see {pair.Receiver.RoleLabel} player GUID 0x{receiverGuid:X} before trade.");

        return receiverGuid;
    }

    private static ActionMessage MakeOfferTrade(ulong partnerGuid) => new()
    {
        ActionType = ActionType.OfferTrade,
        Parameters = { new RequestParameter { LongParam = unchecked((long)partnerGuid) } },
    };

    private static ActionMessage MakeOfferGold(int copper) => new()
    {
        ActionType = ActionType.OfferGold,
        Parameters = { new RequestParameter { IntParam = copper } },
    };

    private static ActionMessage MakeOfferItem(int bagId, int slotId) => new()
    {
        ActionType = ActionType.OfferItem,
        Parameters =
        {
            new RequestParameter { IntParam = bagId },
            new RequestParameter { IntParam = slotId },
            new RequestParameter { IntParam = 1 },
            new RequestParameter { IntParam = 0 },
        },
    };

    private static async Task<ResponseResult> SendTradeActionAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        LiveBotFixture.BotRunnerActionTarget target,
        ActionMessage action,
        string stepName)
    {
        var correlationId =
            $"trade:{target.AccountName}:{Interlocked.Increment(ref s_tradeCorrelationSequence)}";
        action.CorrelationId = correlationId;

        var result = await bot.SendActionAsync(target.AccountName, action);
        if (result != ResponseResult.Success)
            return result;

        var completed = await bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => HasCompletedAction(snapshot, correlationId),
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 200,
            progressLabel: $"{target.RoleLabel} {stepName} action");

        await bot.RefreshSnapshotsAsync();
        var latest = await bot.GetSnapshotAsync(target.AccountName);
        var ack = FindLatestMatchingAck(latest, correlationId);
        if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
        {
            Assert.Fail(
                $"{target.RoleLabel} {stepName} reported ACK {ack.Status} " +
                $"(reason={ack.FailureReason ?? "(none)"}, corr={correlationId}).");
        }

        output.WriteLine($"[TRADE] {target.RoleLabel} {stepName} completion corr={correlationId}: {completed}");
        Assert.True(completed, $"{target.RoleLabel} {stepName} did not complete within the foreground action window.");
        return result;
    }

    private static bool HasCompletedAction(WoWActivitySnapshot snapshot, string correlationId)
    {
        var ack = FindLatestMatchingAck(snapshot, correlationId);
        if (ack != null && ack.Status != CommandAckEvent.Types.AckStatus.Pending)
            return true;

        return string.Equals(
            snapshot.PreviousAction?.CorrelationId,
            correlationId,
            StringComparison.Ordinal);
    }

    private static CommandAckEvent? FindLatestMatchingAck(WoWActivitySnapshot? snapshot, string correlationId)
    {
        if (snapshot == null)
            return null;

        CommandAckEvent? pendingMatch = null;
        for (var i = snapshot.RecentCommandAcks.Count - 1; i >= 0; i--)
        {
            var ack = snapshot.RecentCommandAcks[i];
            if (!string.Equals(ack.CorrelationId, correlationId, StringComparison.Ordinal))
                continue;

            if (ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                return ack;

            pendingMatch ??= ack;
        }

        return pendingMatch;
    }

    private static bool HasTradeError(WoWActivitySnapshot? snapshot)
        => snapshot?.RecentErrors?.Any(message =>
            message.Contains("trade", StringComparison.OrdinalIgnoreCase)
            || message.Contains("target too far", StringComparison.OrdinalIgnoreCase)) == true;

    private static int CountItemSlots(WoWActivitySnapshot? snapshot, uint itemId)
        => snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    private static (int bagId, int slotId) FindItemBagSlot(WoWActivitySnapshot? snapshot, uint itemId)
    {
        var bags = snapshot?.Player?.BagContents;
        if (bags == null)
            return (-1, -1);

        foreach (var item in bags)
        {
            if (item.Value == itemId)
                return (0, (int)item.Key);
        }

        return (-1, -1);
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }

    internal sealed record TradePair(
        LiveBotFixture.BotRunnerActionTarget Initiator,
        LiveBotFixture.BotRunnerActionTarget Receiver);

    internal sealed record TradeCancelMetrics(
        ResponseResult OfferResult,
        ResponseResult DeclineResult,
        bool SnapshotsAlive,
        bool SawTradeError);

    internal sealed record TradeTransferMetrics(
        ResponseResult OfferTradeResult,
        ResponseResult OfferItemResult,
        ResponseResult OfferGoldResult,
        ResponseResult ReceiverAcceptResult,
        ResponseResult InitiatorAcceptResult,
        bool TransferObserved,
        int InitiatorItemCountBefore,
        int InitiatorItemCountAfter,
        int ReceiverItemCountBefore,
        int ReceiverItemCountAfter,
        uint InitiatorCoinageBefore,
        uint InitiatorCoinageAfter,
        uint ReceiverCoinageBefore,
        uint ReceiverCoinageAfter,
        int TransferLatencyMs,
        bool SawTradeError);
}
