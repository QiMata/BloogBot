using GameData.Core.Frames;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG trade-frame surface backed by <see cref="ITradeNetworkClientComponent"/>.
/// Routes <see cref="ITradeFrame"/> operations through the BG packet path
/// (CMSG_INITIATE_TRADE / CMSG_SET_TRADE_GOLD / CMSG_SET_TRADE_ITEM /
/// CMSG_ACCEPT_TRADE / CMSG_CANCEL_TRADE) so InteractionSequenceBuilder's
/// trade sequences no longer short-circuit with "TradeFrame is null --
/// requires FG bot or packet-based trade path" on BG bots. Closes S1.15.
///
/// OfferLockpick and OfferEnchant are stubs pending a SpellCastingAgent
/// wire that targets the NonTraded trade slot's item. Tracked under the
/// S1.15 follow-up note in TASKS.md; not exercised by TradeParityTests.
/// </summary>
public sealed class NetworkTradeFrame(Func<ITradeNetworkClientComponent?> resolveTradeAgent) : ITradeFrame
{
    public bool IsOpen => resolveTradeAgent()?.IsTradeOpen == true;

    public void Close()
    {
        var trade = resolveTradeAgent();
        if (trade?.IsTradeOpen != true) return;
        trade.CancelTradeAsync().GetAwaiter().GetResult();
    }

    public List<IWoWItem> OfferedItems => new();

    public List<IWoWItem> OtherPlayerItems => new();

    public void OfferMoney(int copperCount)
    {
        var trade = resolveTradeAgent();
        if (trade == null) return;
        trade.OfferMoneyAsync((uint)Math.Max(0, copperCount)).GetAwaiter().GetResult();
    }

    public void OfferItem(int bagId, int slotId, int quantity, int tradeWindowSlot)
    {
        var trade = resolveTradeAgent();
        if (trade == null) return;
        // Bag/slot packet conversion mirrors InventoryManager.SetTradeItemAsync:
        //   bag 0 (player backpack) -> packet bag 0xFF, packet slot 23+slotId
        //   bag N (equipped containers) -> packet bag 18+N, packet slot = slotId
        byte packetBag = bagId == 0 ? (byte)0xFF : (byte)(18 + bagId);
        byte packetSlot = bagId == 0 ? (byte)(23 + slotId) : (byte)slotId;
        trade.OfferItemAsync((byte)tradeWindowSlot, packetBag, packetSlot).GetAwaiter().GetResult();
    }

    public void AcceptTrade()
    {
        var trade = resolveTradeAgent();
        if (trade == null) return;
        trade.AcceptTradeAsync().GetAwaiter().GetResult();
    }

    public void DeclineTrade()
    {
        var trade = resolveTradeAgent();
        if (trade == null) return;
        trade.CancelTradeAsync().GetAwaiter().GetResult();
    }

    public void OfferLockpick()
    {
        // Pending: cast Rogue spell 1804 (Pick Lock) targeting the trade window's
        // NonTraded slot item via SpellCastingAgent. Stub keeps the call non-throwing
        // so InteractionSequenceBuilder's Rogue branch doesn't NRE on BG.
    }

    public void OfferEnchant(int enchantId)
    {
        // Pending: cast `enchantId` via SpellCastingAgent targeting the trade window's
        // NonTraded slot item. Stub keeps the call non-throwing on BG.
    }
}
