using GameData.Core.Frames;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Frames;

public sealed class FgTradeFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : ITradeFrame
{
    private const string TradeVisibleLua =
        "if TradeFrame and TradeFrame:IsVisible() then {0} = 1 else {0} = 0 end";

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, TradeVisibleLua);

    public void Close() => luaCall("if TradeFrame and TradeFrame:IsVisible() then CloseTrade() end");

    public List<IWoWItem> OfferedItems => [];

    public List<IWoWItem> OtherPlayerItems => [];

    public void OfferMoney(int copperCount)
    {
        int safeCopper = Math.Max(0, copperCount);
        luaCall(
            "if TradeFrame and TradeFrame:IsVisible() and TradePlayerInputMoneyFrame then " +
            $"MoneyInputFrame_SetCopper(TradePlayerInputMoneyFrame, {safeCopper}) " +
            "end");
    }

    public void OfferItem(int bagId, int slotId, int quantity, int tradeWindowSlot)
    {
        int safeBag = bagId == 0xFF ? 0 : Math.Max(0, bagId);
        int safeSlot = Math.Max(0, slotId) + 1;
        int safeTradeSlot = Math.Max(0, tradeWindowSlot) + 1;
        int safeQuantity = Math.Max(1, quantity);

        string pickupLua = safeQuantity > 1
            ? $"SplitContainerItem({safeBag}, {safeSlot}, {safeQuantity})"
            : $"PickupContainerItem({safeBag}, {safeSlot})";

        luaCall(
            "if TradeFrame and TradeFrame:IsVisible() then " +
            $"{pickupLua}; " +
            $"ClickTradeButton({safeTradeSlot}) " +
            "end");
    }

    public void AcceptTrade() => luaCall("if TradeFrame and TradeFrame:IsVisible() then AcceptTrade() end");

    public void DeclineTrade() => luaCall("if TradeFrame and TradeFrame:IsVisible() then CloseTrade() end");

    public void OfferLockpick()
        => luaCall("if TradeFrame and TradeFrame:IsVisible() and TradeSkill then TradeSkill() end");

    public void OfferEnchant(int enchantId)
    {
        int safeEnchantId = Math.Max(0, enchantId);
        luaCall(
            "if TradeFrame and TradeFrame:IsVisible() then " +
            $"local spellName = GetSpellInfo({safeEnchantId}); " +
            "if spellName and spellName ~= '' then CastSpellByName(spellName) end " +
            "end");
    }
}
