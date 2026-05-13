using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Interfaces;

namespace BotRunner.Tasks;

/// <summary>
/// One-tick lifecycle driver for the <see cref="IBotTask"/> stack. Extracted
/// from <c>BotRunnerService</c>'s tick loop (slot S1.0) so the per-task
/// Push/Tick/Pop/ChildFail sequence is unit-testable in isolation. The
/// production <c>BotRunnerService</c> tick loop calls
/// <see cref="DriveOneTickAsync"/> once per cycle.
/// </summary>
/// <remarks>
/// Lifecycle semantics:
/// <list type="number">
///   <item>If the stack is empty, nothing happens.</item>
///   <item>The first time a task appears on top, <see cref="IBotTask.OnPushedAsync"/>
///         fires (tracked via the caller-supplied <paramref name="pushedNotified"/> set).</item>
///   <item><see cref="IBotTask.TickAsync"/> fires only while
///         <see cref="IBotTask.Status"/> is <see cref="BotTaskStatus.Running"/>.</item>
///   <item>If the task is <see cref="BotTaskStatus.Complete"/> or
///         <see cref="BotTaskStatus.Failed"/>, the driver pops it, removes its
///         "pushed" marker, and fires <see cref="IBotTask.OnPoppedAsync"/>.</item>
///   <item>On <see cref="BotTaskStatus.Failed"/>, the new top of stack (if
///         any) is offered <see cref="IBotTask.OnChildFailedAsync"/>. Returning
///         <c>false</c> (R24 default) escalates by marking the parent failed
///         too — it pops next tick.</item>
/// </list>
/// </remarks>
public sealed class TaskStackDriver
{
    /// <summary>
    /// Drive a single tick of the supplied stack. Safe to call when
    /// <paramref name="tasks"/> is empty.
    /// </summary>
    /// <param name="tasks">The LIFO task stack.</param>
    /// <param name="pushedNotified">Tracks which task instances have already received
    /// <see cref="IBotTask.OnPushedAsync"/>. The driver adds to / removes from this set.</param>
    /// <param name="context">Per-tick context passed to lifecycle hooks.</param>
    /// <param name="ct">Cancellation observed by the driver and forwarded to async hooks.</param>
    public async Task DriveOneTickAsync(
        Stack<IBotTask> tasks,
        HashSet<IBotTask> pushedNotified,
        BotTaskContext context,
        CancellationToken ct)
    {
        if (tasks.Count == 0)
            return;

        var top = tasks.Peek();

        if (pushedNotified.Add(top))
            await top.OnPushedAsync(context, ct).ConfigureAwait(false);

        if (top.Status == BotTaskStatus.Running)
            await top.TickAsync(context, ct).ConfigureAwait(false);

        if (top.Status == BotTaskStatus.Complete || top.Status == BotTaskStatus.Failed)
        {
            tasks.Pop();
            pushedNotified.Remove(top);
            var terminal = top.Status;
            await top.OnPoppedAsync(context, terminal).ConfigureAwait(false);

            if (terminal == BotTaskStatus.Failed && tasks.Count > 0)
            {
                var parent = tasks.Peek();
                var absorbed = await parent.OnChildFailedAsync(context, top, $"{top.Name} failed")
                    .ConfigureAwait(false);
                if (!absorbed)
                    MarkFailed(parent);
            }
        }
    }

    /// <summary>
    /// Force a task into <see cref="BotTaskStatus.Failed"/>. <see cref="BotTask"/>
    /// exposes a <c>protected</c> setter via <c>MarkFailed</c>, so for
    /// non-<see cref="BotTask"/> implementers the driver falls back to a
    /// no-op (the parent simply continues running). Family slots refining
    /// the lifecycle may replace this with a richer escalation policy.
    /// </summary>
    private static void MarkFailed(IBotTask task)
    {
        if (task is BotTask bt)
            ForceBotTaskFailed(bt);
        // else: leave Status untouched; the parent did not absorb but we cannot
        // mutate a foreign IBotTask. Family slots may extend.
    }

    private static void ForceBotTaskFailed(BotTask bt)
    {
        // The protected setter on BotTask.Status is accessible within the
        // owning assembly; expose a sentinel helper rather than reflection.
        bt.RequestStatusForEscalation(BotTaskStatus.Failed);
    }
}
