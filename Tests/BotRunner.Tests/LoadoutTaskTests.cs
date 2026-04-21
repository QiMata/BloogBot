using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using Moq;
using Xunit;

namespace BotRunner.Tests;

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
        // The scaffold short-circuits empty specs so coordinator-level state
        // machines can be exercised without waiting on the (not-yet-landed)
        // chat/SOAP executors.
        var task = new LoadoutTask(MockContext(), new LoadoutSpec());

        task.Update();

        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
        Assert.Equal(string.Empty, task.FailureReason);
    }

    [Fact]
    public void Update_NonEmptySpec_StaysInProgress_UntilExecutorsLand()
    {
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

    private static IBotContext MockContext() => new Mock<IBotContext>(MockBehavior.Loose).Object;
}
