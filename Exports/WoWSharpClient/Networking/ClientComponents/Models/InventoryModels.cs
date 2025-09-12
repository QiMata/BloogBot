namespace WoWSharpClient.Networking.ClientComponents.Models
{
    // Inventory event models used by InventoryNetworkClientComponent reactive streams
    public readonly record struct ItemMovedData(
        ulong ItemGuid,
        byte SourceBag,
        byte SourceSlot,
        byte DestinationBag,
        byte DestinationSlot
    );

    public readonly record struct ItemSplitData(
        ulong ItemGuid,
        uint SplitQuantity
    );

    public readonly record struct ItemDestroyedData(
        ulong ItemGuid,
        uint Quantity
    );
}
