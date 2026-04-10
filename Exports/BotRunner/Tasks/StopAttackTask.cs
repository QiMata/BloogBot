using BotRunner.Interfaces;
using Microsoft.Extensions.Logging;

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
        Logger.LogInformation("[STOP_ATTACK] Stopped attack");
        BotTasks.Pop();
    }
}
