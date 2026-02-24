using BotRunner.Interfaces;
using Serilog;

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
        if (targetGuid != 0)
            ObjectManager.SetTarget(targetGuid);

        ObjectManager.UseItem(bagId, slotId, targetGuid);
        Log.Information("[USE_ITEM] Used item at bag={Bag} slot={Slot}", bagId, slotId);
        BotTasks.Pop();
    }
}
