namespace BotRunner.Interfaces;

/// <summary>
/// Terminal lifecycle status for an <see cref="IBotTask"/>.
/// </summary>
/// <remarks>
/// Phase 1 substrate per slot S1.0. <see cref="Running"/> means the task is
/// still active on the stack; <see cref="Complete"/> and <see cref="Failed"/>
/// signal the runner to pop the task and fire the parent's
/// <see cref="IBotTask.OnChildFailedAsync"/> escalation on
/// <see cref="Failed"/> (per R24).
/// </remarks>
public enum BotTaskStatus
{
    Running,
    Complete,
    Failed
}
