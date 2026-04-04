namespace GameData.Core.Frames;

/// <summary>
/// Bank frame interface. FG: Lua-based bank interaction.
/// BG: packet-based (CMSG_AUTOBANK_ITEM, CMSG_AUTOSTORE_BANK_ITEM, etc).
/// </summary>
public interface IBankFrame
{
    bool IsOpen { get; }
    void DepositItem(int bagId, int slotId);
    void WithdrawItem(int bankSlot);
    int GetBankSlotCount();
    void PurchaseBankSlot();
    void Close();
}
