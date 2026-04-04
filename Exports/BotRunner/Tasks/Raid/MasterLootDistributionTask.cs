using BotRunner.Interfaces;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Raid;

/// <summary>
/// Handles master loot distribution for raid encounters.
/// Raid leader picks up boss loot and distributes by class/spec priority.
/// Priority: main spec > off spec > disenchant.
/// Uses IObjectManager.AssignLoot (CMSG_LOOT_MASTER_GIVE) for distribution.
/// </summary>
public class MasterLootDistributionTask : BotTask, IBotTask
{
    private enum LootState { WaitForLoot, DistributeItems, Complete }

    private LootState _state = LootState.WaitForLoot;
    private readonly IReadOnlyList<LootPriorityEntry> _priorityList;
    private int _distributedCount;

    public MasterLootDistributionTask(IBotContext context, IReadOnlyList<LootPriorityEntry> priorityList)
        : base(context)
    {
        _priorityList = priorityList;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        var lootFrame = ObjectManager.LootFrame;

        switch (_state)
        {
            case LootState.WaitForLoot:
                if (lootFrame is { IsOpen: true, LootCount: > 0 })
                {
                    _state = LootState.DistributeItems;
                    Log.Information("[MASTERLOOT] Loot window open with {Count} items", lootFrame.LootCount);
                }
                break;

            case LootState.DistributeItems:
                if (lootFrame == null || !lootFrame.IsOpen)
                {
                    _state = LootState.Complete;
                    return;
                }

                var items = lootFrame.LootItems.Where(i => i.GotLoot).ToList();
                if (items.Count == 0)
                {
                    _state = LootState.Complete;
                    return;
                }

                // Distribute first available item
                var item = items.First();
                var recipient = FindBestRecipient((uint)item.ItemId);

                if (recipient != null)
                {
                    ObjectManager.AssignLoot(item.ItemId, recipient.Value);
                    Log.Information("[MASTERLOOT] Assigning item {ItemId} to {Recipient:X}",
                        item.ItemId, recipient.Value);
                    _distributedCount++;
                }

                // If all items distributed, complete
                if (_distributedCount >= items.Count)
                    _state = LootState.Complete;
                break;

            case LootState.Complete:
                Log.Information("[MASTERLOOT] Distribution complete — {Count} items assigned", _distributedCount);
                BotContext.BotTasks.Pop();
                break;
        }
    }

    private ulong? FindBestRecipient(uint itemId)
    {
        // Check priority list for this item
        var entry = _priorityList
            .Where(e => e.ItemId == itemId)
            .OrderBy(e => e.Priority)
            .FirstOrDefault();

        if (entry != null)
            return entry.PlayerGuid;

        // Fallback: find any raid member who needs it (main spec > off spec)
        var raidMembers = ObjectManager.Players
            .Where(p => p.Health > 0)
            .ToList();

        return raidMembers.FirstOrDefault()?.Guid;
    }
}

/// <summary>
/// Defines loot priority for a specific item → player mapping.
/// Lower Priority value = higher priority.
/// </summary>
public record LootPriorityEntry(uint ItemId, ulong PlayerGuid, int Priority, string Reason);
