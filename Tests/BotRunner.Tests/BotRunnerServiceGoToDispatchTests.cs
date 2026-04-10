using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tests;

public class BotRunnerServiceGoToDispatchTests
{
    [Fact]
    public void UpsertGoToTask_NoExistingTask_PushesNewTask()
    {
        var tasks = new Stack<IBotTask>();
        var context = CreateContext(tasks);

        var result = BotRunnerService.UpsertGoToTask(tasks, context, 100f, 200f, 10f, 8f);

        Assert.Equal(BotRunnerService.GoToTaskUpsertResult.Pushed, result);
        Assert.Single(tasks);

        var goTo = Assert.IsType<GoToTask>(tasks.Peek());
        Assert.Equal(100f, goTo.Target.X);
        Assert.Equal(200f, goTo.Target.Y);
        Assert.Equal(10f, goTo.Target.Z);
        Assert.Equal(8f, goTo.Tolerance);
    }

    [Fact]
    public void UpsertGoToTask_MatchingTask_ReturnsDuplicateWithoutPush()
    {
        var tasks = new Stack<IBotTask>();
        var context = CreateContext(tasks);
        var existing = new GoToTask(context, 100f, 200f, 10f, 8f);
        tasks.Push(existing);

        var result = BotRunnerService.UpsertGoToTask(tasks, context, 100f, 200f, 10f, 8f);

        Assert.Equal(BotRunnerService.GoToTaskUpsertResult.Duplicate, result);
        Assert.Single(tasks);
        Assert.Same(existing, tasks.Peek());
    }

    [Fact]
    public void UpsertGoToTask_DifferentTarget_RetargetsExistingTask()
    {
        var tasks = new Stack<IBotTask>();
        var context = CreateContext(tasks);
        var existing = new GoToTask(context, 100f, 200f, 10f, 8f);
        tasks.Push(existing);

        var result = BotRunnerService.UpsertGoToTask(tasks, context, 500f, 600f, 20f, 5f);

        Assert.Equal(BotRunnerService.GoToTaskUpsertResult.Retargeted, result);
        Assert.Single(tasks);
        Assert.Same(existing, tasks.Peek());
        Assert.Equal(500f, existing.Target.X);
        Assert.Equal(600f, existing.Target.Y);
        Assert.Equal(20f, existing.Target.Z);
        Assert.Equal(5f, existing.Tolerance);
    }

    [Fact]
    public void UpsertGoToTask_GoToBelowTopTask_RetargetsWithoutGrowingStack()
    {
        var tasks = new Stack<IBotTask>();
        var context = CreateContext(tasks);
        var existing = new GoToTask(context, 100f, 200f, 10f, 8f);
        tasks.Push(existing);
        tasks.Push(new NoOpTask());

        var result = BotRunnerService.UpsertGoToTask(tasks, context, 300f, 350f, 12f, 4f);

        Assert.Equal(BotRunnerService.GoToTaskUpsertResult.Retargeted, result);
        Assert.Equal(2, tasks.Count);
        Assert.IsType<NoOpTask>(tasks.Peek());

        var goTo = Assert.Single(tasks.OfType<GoToTask>());
        Assert.Equal(300f, goTo.Target.X);
        Assert.Equal(350f, goTo.Target.Y);
        Assert.Equal(12f, goTo.Target.Z);
        Assert.Equal(4f, goTo.Tolerance);
    }

    private static IBotContext CreateContext(Stack<IBotTask> tasks)
    {
        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.BotTasks).Returns(tasks);
        return context.Object;
    }

    private sealed class NoOpTask : IBotTask
    {
        public void Update()
        {
        }
    }
}
