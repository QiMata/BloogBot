using System.Threading;
using System.Threading.Tasks;
using BotRunner.Tasks;

namespace BotRunner.Interfaces;

/// <summary>
/// Phase 1 target contract for tasks executed by <c>BotRunnerService</c>.
/// </summary>
/// <remarks>
/// Per slot S1.0 (<c>docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md</c>) and
/// resolved questions R22/R23/R24/R25:
/// <list type="bullet">
///   <item><see cref="TickAsync"/> drives the task each loop iteration.</item>
///   <item><see cref="OnPushedAsync"/> fires once when the task first appears
///         at the top of the stack.</item>
///   <item><see cref="OnPoppedAsync"/> fires once when the runner pops the
///         task; <paramref name="terminal"/> reports whether it ended in
///         <see cref="BotTaskStatus.Complete"/> or
///         <see cref="BotTaskStatus.Failed"/>.</item>
///   <item><see cref="OnChildFailedAsync"/> lets a parent absorb a child
///         failure: <c>true</c> = parent keeps running, <c>false</c> =
///         escalate. Default base-class implementation returns <c>false</c>
///         (R24 — conservative escalate).</item>
/// </list>
/// Existing tasks inherit the shim provided by <see cref="BotTask"/>; per-task
/// async refactor is out of scope for S1.0 (R25 — shim-only migration).
/// </remarks>
public interface IBotTask
{
    /// <summary>Stable identifier for diagnostics / logs (e.g. "TravelTask:Crossroads-&gt;UC").</summary>
    string Name => GetType().Name;

    /// <summary>Lifecycle status; pop-on-Complete/Failed is driven by the runner.</summary>
    BotTaskStatus Status => BotTaskStatus.Running;

    /// <summary>Convenience: <see cref="Status"/> equals <see cref="BotTaskStatus.Complete"/>.</summary>
    bool IsComplete => Status == BotTaskStatus.Complete;

    /// <summary>Convenience: <see cref="Status"/> equals <see cref="BotTaskStatus.Failed"/>.</summary>
    bool IsFailed => Status == BotTaskStatus.Failed;

    /// <summary>Drive one tick. Implementations must honor <paramref name="ct"/>.</summary>
    Task TickAsync(BotTaskContext context, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Fires once when this task becomes the stack top.</summary>
    Task OnPushedAsync(BotTaskContext context, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Fires once when the runner pops this task.</summary>
    Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal) => Task.CompletedTask;

    /// <summary>
    /// Parent escalation hook. Return <c>true</c> to absorb the failure and
    /// keep running; <c>false</c> to escalate (parent fails too). R24 default
    /// is <c>false</c>.
    /// </summary>
    Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason) => Task.FromResult(false);
}
