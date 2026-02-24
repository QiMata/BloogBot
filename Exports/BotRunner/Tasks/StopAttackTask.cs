using BotRunner.Interfaces;
using Serilog;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: stop all attacks (melee, ranged, wand).
/// Pops immediately after issuing the stop command.
/// Maps to ActionType.STOP_ATTACK from StateManager.
/// </summary>
public class StopAttackTask(IBotContext botContext) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        ObjectManager.StopAttack();
        Log.Information("[STOP_ATTACK] Stopped attack");
        BotTasks.Pop();
    }
}
