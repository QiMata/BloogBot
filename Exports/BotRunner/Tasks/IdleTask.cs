using BotRunner.Interfaces;

namespace BotRunner.Tasks;

/// <summary>
/// Pure idle task — sits at the bottom of the task stack and does nothing.
/// All behavior is directed by StateManager via ActionMessage IPC.
/// When StateManager sends an action, BotRunnerService builds a behavior tree
/// that preempts this task. When the behavior tree completes, we return to idle.
/// </summary>
public class IdleTask(IBotContext botContext) : BotTask(botContext), IBotTask
{
    public void Update() { }
}
