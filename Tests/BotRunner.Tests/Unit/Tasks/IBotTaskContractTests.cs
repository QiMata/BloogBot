using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using Moq;
using Xunit;

namespace BotRunner.Tests.Unit.Tasks;

/// <summary>
/// Phase 1 slot S1.0 — IBotTask contract migration. Asserts the six
/// lifecycle invariants from R24 directly against
/// <see cref="TaskStackDriver"/> so the loop step is verified without
/// dragging in the full <c>BotRunnerService</c> tick machinery.
/// </summary>
public sealed class IBotTaskContractTests
{
    private static BotTaskContext NewContext()
    {
        var om = new Mock<IObjectManager>(MockBehavior.Loose).Object;
        var bot = new Mock<IBotContext>(MockBehavior.Loose).Object;
        return new BotTaskContext(
            ObjectManager: om,
            Pathfinding: null,
            Chat: static (_, _) => { },
            Metrics: NoOpMetricsSink.Instance,
            Bot: bot,
            Cancellation: CancellationToken.None);
    }

    [Fact]
    public async Task TickAsync_IsCalled_OncePerLoopTick()
    {
        var driver = new TaskStackDriver();
        var stack = new Stack<IBotTask>();
        var notified = new HashSet<IBotTask>();
        var task = new FakeTask();
        stack.Push(task);

        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);

        Assert.Equal(3, task.TickCount);
    }

    [Fact]
    public async Task OnPushedAsync_FiresExactlyOnce_OnFirstAppearance()
    {
        var driver = new TaskStackDriver();
        var stack = new Stack<IBotTask>();
        var notified = new HashSet<IBotTask>();
        var task = new FakeTask();
        stack.Push(task);

        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);

        Assert.Equal(1, task.PushCount);
    }

    [Fact]
    public async Task OnPoppedAsync_FiresExactlyOnce_WithTerminalStatus()
    {
        var driver = new TaskStackDriver();
        var stack = new Stack<IBotTask>();
        var notified = new HashSet<IBotTask>();
        var task = new FakeTask();
        stack.Push(task);

        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        // Mark Complete after the first tick; the next tick pops + fires OnPoppedAsync.
        task.Status = BotTaskStatus.Complete;
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);

        Assert.Equal(1, task.PopCount);
        Assert.Equal(BotTaskStatus.Complete, task.PoppedTerminal);
        Assert.Empty(stack);
        Assert.DoesNotContain(task, notified);
    }

    [Fact]
    public async Task OnChildFailedAsync_TrueReturn_KeepsParentRunning()
    {
        var driver = new TaskStackDriver();
        var stack = new Stack<IBotTask>();
        var notified = new HashSet<IBotTask>();
        var parent = new FakeTask { AbsorbChildFailure = true };
        var child = new FakeTask();
        stack.Push(parent);
        stack.Push(child);

        // Tick once so child receives OnPushedAsync + a single TickAsync.
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        // Force child failure; next tick pops + escalates.
        child.Status = BotTaskStatus.Failed;
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);

        Assert.Equal(1, parent.ChildFailCount);
        Assert.Equal(BotTaskStatus.Running, parent.Status);
        Assert.Single(stack);
        Assert.Same(parent, stack.Peek());
    }

    [Fact]
    public async Task OnChildFailedAsync_FalseReturn_PopsParentToo()
    {
        var driver = new TaskStackDriver();
        var stack = new Stack<IBotTask>();
        var notified = new HashSet<IBotTask>();
        // Use a real BotTask subclass for the parent so the driver's
        // RequestStatusForEscalation hook can flip its Status to Failed.
        var parent = new EscalatingBotTask();
        var child = new FakeTask();
        stack.Push(parent);
        stack.Push(child);

        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        child.Status = BotTaskStatus.Failed;
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);

        Assert.Equal(1, parent.ChildFailCount);
        Assert.Equal(BotTaskStatus.Failed, parent.Status);

        // Next tick pops the parent too.
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        Assert.Empty(stack);
        Assert.Equal(1, parent.PopCount);
    }

    [Fact]
    public async Task TickAsync_NotCalled_AfterStatusBecomesCompleteOrFailed()
    {
        var driver = new TaskStackDriver();
        var stack = new Stack<IBotTask>();
        var notified = new HashSet<IBotTask>();
        var task = new FakeTask();
        stack.Push(task);

        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        Assert.Equal(1, task.TickCount);

        task.Status = BotTaskStatus.Complete;
        await driver.DriveOneTickAsync(stack, notified, NewContext(), CancellationToken.None);
        // The driver should NOT have called TickAsync again once Status flipped.
        Assert.Equal(1, task.TickCount);
        Assert.Empty(stack);
    }

    // -----------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------

    private sealed class FakeTask : IBotTask
    {
        public string Name => "fake";
        public BotTaskStatus Status { get; set; } = BotTaskStatus.Running;
        public int TickCount;
        public int PushCount;
        public int PopCount;
        public int ChildFailCount;
        public bool AbsorbChildFailure;
        public BotTaskStatus? PoppedTerminal;

        public Task TickAsync(BotTaskContext context, CancellationToken ct)
        {
            TickCount++;
            return Task.CompletedTask;
        }

        public Task OnPushedAsync(BotTaskContext context, CancellationToken ct)
        {
            PushCount++;
            return Task.CompletedTask;
        }

        public Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)
        {
            PopCount++;
            PoppedTerminal = terminal;
            return Task.CompletedTask;
        }

        public Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)
        {
            ChildFailCount++;
            return Task.FromResult(AbsorbChildFailure);
        }
    }

    /// <summary>
    /// <see cref="BotTask"/>-derived parent so the driver's escalation path
    /// (which mutates <see cref="BotTask.Status"/> via the internal
    /// RequestStatusForEscalation hook) is exercised end-to-end.
    /// </summary>
    private sealed class EscalatingBotTask : BotTask
    {
        public int ChildFailCount;
        public int PopCount;

        public EscalatingBotTask() : base(new StubBotContext()) { }

        // Default base shim's OnTick would try to reflect into Update();
        // override it here so the test does not depend on the reflection
        // path.
        protected override void OnTick(BotTaskContext context) { }

        public override Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)
        {
            ChildFailCount++;
            return Task.FromResult(false); // do not absorb -> driver should escalate.
        }

        public override Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)
        {
            PopCount++;
            return base.OnPoppedAsync(context, terminal);
        }

        // BotTask declares no abstract members other than the shim helpers,
        // so we only need to satisfy them via Moq-less stubs.
        public void Update() { }
    }

    private sealed class StubBotContext : IBotContext
    {
        public Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory => null;
        public IObjectManager ObjectManager => null!;
        public Stack<IBotTask> BotTasks { get; } = new();
        public BotRunner.Interfaces.IDependencyContainer Container => null!;
        public BotRunner.Constants.BotBehaviorConfig Config { get; } = new();
        public IWoWEventHandler EventHandler => null!;
        public void AddDiagnosticMessage(string message) { }
    }
}
