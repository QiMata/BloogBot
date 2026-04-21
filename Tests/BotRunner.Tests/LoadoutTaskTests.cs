using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace BotRunner.Tests;

/// <summary>
/// P3.3: status state-machine + empty-spec short-circuit contract.
/// Plan-builder and per-step executor coverage lives in
/// <see cref="LoadoutTaskExecutorTests"/>.
/// </summary>
public sealed class LoadoutTaskTests
{
    [Fact]
    public void NewTask_StatusIsNotStarted()
    {
        var task = new LoadoutTask(MockContext(), new LoadoutSpec());
        Assert.Equal(LoadoutStatus.LoadoutNotStarted, task.Status);
    }

    [Fact]
    public void Update_EmptySpec_TransitionsToReady()
    {
        // Empty spec produces an empty plan, so the task short-circuits to
        // Ready on the very first Update. Coordinator can drive its state
        // machine end-to-end even when a bot has nothing to prep.
        var task = new LoadoutTask(MockContext(), new LoadoutSpec());

        task.Update();

        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
        Assert.Equal(string.Empty, task.FailureReason);
    }

    [Fact]
    public void Update_NonEmptySpec_WithNoOnlinePlayer_StaysInProgress()
    {
        // With no ObjectManager/Player attached (loose mock), every step's
        // TryExecute returns false (not-ready), so the task accumulates
        // retry attempts without burning dispatch. Still InProgress after
        // one tick.
        var task = new LoadoutTask(MockContext(), new LoadoutSpec
        {
            TargetLevel = 60,
        });

        task.Update();

        Assert.Equal(LoadoutStatus.LoadoutInProgress, task.Status);
    }

    [Fact]
    public void Update_ReadyTask_DoesNotRegress()
    {
        var task = new LoadoutTask(MockContext(), new LoadoutSpec());
        task.Update();
        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);

        task.Update();
        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
    }

    [Fact]
    public void Constructor_RejectsNullSpec()
    {
        var ctx = MockContext();
        Assert.Throws<System.ArgumentNullException>(() => new LoadoutTask(ctx, null!));
    }

    [Fact]
    public void Constructor_RejectsNullContext()
    {
        Assert.Throws<System.ArgumentNullException>(() => new LoadoutTask(null!, new LoadoutSpec()));
    }

    private static IBotContext MockContext()
    {
        var ctx = new Mock<IBotContext>(MockBehavior.Loose);
        ctx.SetupGet(c => c.BotTasks).Returns(new Stack<IBotTask>());
        return ctx.Object;
    }
}
