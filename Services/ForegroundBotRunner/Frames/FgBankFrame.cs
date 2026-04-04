using GameData.Core.Frames;
using System;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// Lua-based FG bank frame. Wraps WoW 1.12.1 bank Lua API.
/// </summary>
public sealed class FgBankFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : IBankFrame
{
    private const string BankVisibleLua =
        "if BankFrame and BankFrame:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string BankSlotCountLua =
        "{0} = GetNumBankSlots()";

    public bool IsOpen
    {
        get
        {
            var result = luaCallWithResult(BankVisibleLua);
            return result.Length > 0 && result[0] == "1";
        }
    }

    public void DepositItem(int bagId, int slotId)
    {
        // PickupContainerItem → click bank slot auto-deposits
        luaCall($"PickupContainerItem({bagId},{slotId}); AutoStoreItem()");
    }

    public void WithdrawItem(int bankSlot)
    {
        // PickupInventoryItem on a bank slot, then place in bags
        luaCall($"PickupInventoryItem({bankSlot}); AutoEquipCursorItem()");
    }

    public int GetBankSlotCount()
    {
        var result = luaCallWithResult(BankSlotCountLua);
        if (result.Length > 0 && int.TryParse(result[0], out var count))
            return count;
        return 0;
    }

    public void PurchaseBankSlot()
    {
        luaCall("PurchaseSlot()");
    }

    public void Close()
    {
        luaCall("CloseBankFrame()");
    }
}
