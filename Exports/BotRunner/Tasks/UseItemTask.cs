using BotRunner.Helpers;
using BotRunner.Interfaces;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: use an item from inventory by bag and slot.
/// Calls ObjectManager.UseItem(bag, slot), then pops.
/// Maps to ActionType.USE_ITEM from StateManager.
/// </summary>
public class UseItemTask(IBotContext botContext, int bagId, int slotId, ulong targetGuid = 0) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        var item = ObjectManager.GetContainedItem(bagId, slotId);
        if (MountUsageGuard.TryGetBlockedReasonForItem(ObjectManager, item, out var blockReason))
        {
            Logger.LogInformation("[USE_ITEM] Skipped mount item at bag={Bag} slot={Slot}: {Reason}", bagId, slotId, blockReason);
            BotContext.AddDiagnosticMessage($"[MOUNT-BLOCK] item={item?.ItemId ?? 0u} bag={bagId} slot={slotId} {blockReason}");
            BotTasks.Pop();
            return;
        }

        if (targetGuid != 0)
            ObjectManager.SetTarget(targetGuid);

        ObjectManager.UseItem(bagId, slotId, targetGuid);
        Logger.LogInformation("[USE_ITEM] Used item at bag={Bag} slot={Slot}", bagId, slotId);
        BotTasks.Pop();
    }
}
