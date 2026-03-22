using GameData.Core.Frames;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG loot-frame surface backed by LootingNetworkClientComponent.
/// This lets BotTask flows observe and drive the real loot window instead of
/// relying on packet-side auto-loot shortcuts.
/// </summary>
public sealed class NetworkLootFrame(Func<ILootingNetworkClientComponent?> resolveLootingAgent) : ILootFrame
{
    public bool IsOpen => resolveLootingAgent()?.IsLootWindowOpen == true;

    public void Close()
    {
        var lootingAgent = resolveLootingAgent();
        if (lootingAgent?.CurrentLootTarget == null)
            return;

        lootingAgent.CloseLootAsync().GetAwaiter().GetResult();
    }

    public IEnumerable<LootItem> LootItems => Array.Empty<LootItem>();

    public int LootCount => resolveLootingAgent()?.GetAvailableLoot().Count ?? 0;

    public ulong LootGuid => resolveLootingAgent()?.CurrentLootTarget ?? 0UL;

    public int Coins => 0;

    public ConcurrentDictionary<int, int> MissingIds { get; } = new();

    public void ItemCallback(int parItemId)
    {
    }

    public void LootSlot(int parSlotIndex)
    {
        var lootingAgent = resolveLootingAgent();
        if (lootingAgent?.IsLootWindowOpen != true)
            return;

        lootingAgent.LootItemAsync((byte)parSlotIndex).GetAwaiter().GetResult();
    }

    public void LootAll()
    {
        var lootingAgent = resolveLootingAgent();
        if (lootingAgent?.IsLootWindowOpen != true)
            return;

        foreach (var lootSlot in lootingAgent.GetAvailableLoot()
                     .OrderBy(slot => slot.SlotIndex)
                     .Select(slot => slot.SlotIndex)
                     .ToArray())
        {
            lootingAgent.LootItemAsync(lootSlot).GetAwaiter().GetResult();
        }
    }
}
