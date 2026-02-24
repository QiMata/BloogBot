using BotRunner.Interfaces;
using Serilog;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: start melee auto-attack on a target GUID.
/// Sets the target and initiates melee attack, then pops immediately.
/// Maps to ActionType.START_MELEE_ATTACK from StateManager.
/// </summary>
public class StartAttackTask(IBotContext botContext, ulong targetGuid) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        if (targetGuid == 0)
        {
            Log.Warning("[START_ATTACK] No target GUID provided");
            BotTasks.Pop();
            return;
        }

        ObjectManager.SetTarget(targetGuid);
        ObjectManager.StartMeleeAttack();
        Log.Information("[START_ATTACK] Started melee attack on {Guid:X}", targetGuid);
        BotTasks.Pop();
    }
}
