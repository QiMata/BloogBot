using BotRunner.Interfaces;
using Serilog;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: equip an item from a bag slot.
/// Calls UseContainerItem which auto-equips equippable items.
/// Maps to ActionType.EQUIP_ITEM from StateManager.
/// </summary>
public class EquipItemTask(IBotContext botContext, int bagId, int slotId) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        ObjectManager.UseContainerItem(bagId, slotId);
        Log.Information("[EQUIP_ITEM] Equipped item from bag={Bag} slot={Slot}", bagId, slotId);
        BotTasks.Pop();
    }
}
