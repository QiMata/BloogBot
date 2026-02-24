using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests.Combat;

// ==================== WaitTask Tests ====================

public class WaitTaskTests
{
    private readonly Mock<IBotContext> _ctx;
    private readonly Stack<IBotTask> _taskStack;

    public WaitTaskTests()
    {
        _ctx = new Mock<IBotContext>();
        var om = new Mock<IObjectManager>();
        var eventHandler = new Mock<IWoWEventHandler>();
        _taskStack = new Stack<IBotTask>();

        _ctx.Setup(c => c.ObjectManager).Returns(om.Object);
        _ctx.Setup(c => c.Config).Returns(new BotBehaviorConfig());
        _ctx.Setup(c => c.EventHandler).Returns(eventHandler.Object);
        _ctx.Setup(c => c.BotTasks).Returns(_taskStack);
    }

    [Fact]
    public void Update_FirstCall_DoesNotPop_WhenDurationNotElapsed()
    {
        var task = new WaitTask(_ctx.Object, 5000);
        _taskStack.Push(task);

        task.Update();

        // Task should still be on stack (duration hasn't elapsed)
        Assert.Single(_taskStack);
    }

    [Fact]
    public void Update_ZeroDuration_PopsOnFirstUpdate()
    {
        // With 0ms duration, first Update sets _startTime then immediately sees elapsed >= 0ms → pops
        var task = new WaitTask(_ctx.Object, 0);
        _taskStack.Push(task);

        task.Update();

        Assert.Empty(_taskStack);
    }

    [Fact]
    public void Update_LongDuration_DoesNotPopImmediately()
    {
        var task = new WaitTask(_ctx.Object, 60000);
        _taskStack.Push(task);

        // Multiple updates should not pop with 60s duration
        for (int i = 0; i < 10; i++)
            task.Update();

        Assert.Single(_taskStack);
    }

    [Fact]
    public void Update_MultipleTasks_OnlyPopsTop()
    {
        var bottomTask = new WaitTask(_ctx.Object, 60000);
        var topTask = new WaitTask(_ctx.Object, 0);
        _taskStack.Push(bottomTask);
        _taskStack.Push(topTask);

        topTask.Update(); // Should pop the top (0ms duration)

        Assert.Single(_taskStack);
        Assert.Same(bottomTask, _taskStack.Peek());
    }
}

// ==================== TeleportTask Tests ====================

public class TeleportTaskTests
{
    private readonly Mock<IBotContext> _ctx;
    private readonly Mock<IObjectManager> _om;
    private readonly Mock<IWoWLocalPlayer> _player;
    private readonly Stack<IBotTask> _taskStack;

    public TeleportTaskTests()
    {
        _ctx = new Mock<IBotContext>();
        _om = new Mock<IObjectManager>();
        _player = new Mock<IWoWLocalPlayer>();
        var eventHandler = new Mock<IWoWEventHandler>();
        _taskStack = new Stack<IBotTask>();

        _ctx.Setup(c => c.ObjectManager).Returns(_om.Object);
        _ctx.Setup(c => c.Config).Returns(new BotBehaviorConfig());
        _ctx.Setup(c => c.EventHandler).Returns(eventHandler.Object);
        _ctx.Setup(c => c.BotTasks).Returns(_taskStack);

        _om.Setup(o => o.Player).Returns(_player.Object);
        _player.Setup(p => p.Position).Returns(new Position(100, 200, 300));
        _player.Setup(p => p.Name).Returns("TestChar");
        _player.Setup(p => p.Guid).Returns(1UL);
        _player.Setup(p => p.Health).Returns(100u);
        _player.Setup(p => p.InGhostForm).Returns(false);
        _player.Setup(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_NONE);
        _player.Setup(p => p.Bytes1).Returns([0u]);
    }

    [Fact]
    public void Update_NullPlayer_DoesNothing()
    {
        _om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update();

        // Should not pop, should not send chat
        Assert.Single(_taskStack);
        _om.Verify(o => o.SendChatMessage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Update_NullPosition_DoesNothing()
    {
        _player.Setup(p => p.Position).Returns((Position?)null);
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update();

        Assert.Single(_taskStack);
        _om.Verify(o => o.SendChatMessage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Update_EmptyPlayerName_WaitsForName()
    {
        _player.Setup(p => p.Name).Returns(string.Empty);
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update();

        // Should not send command yet — waiting for name
        Assert.Single(_taskStack);
        _om.Verify(o => o.SendChatMessage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Update_SendsTeleportCommand()
    {
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update();

        _om.Verify(o => o.SendChatMessage(".tele name TestChar Orgrimmar"), Times.Once);
    }

    [Fact]
    public void Update_CommandSentOnce_NotRepeated()
    {
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update(); // Sends command
        task.Update(); // Should not send again

        _om.Verify(o => o.SendChatMessage(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Update_PositionChanged_Pops()
    {
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        // First update: sends command, records start position (100, 200, 300)
        task.Update();

        // Simulate position change (far away from start)
        _player.Setup(p => p.Position).Returns(new Position(500, 600, 300));

        // Second update: checks position → distance > 10 → pops
        task.Update();

        Assert.Empty(_taskStack);
    }

    [Fact]
    public void Update_SmallPositionChange_DoesNotPop()
    {
        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update(); // Sends command, start position = (100, 200, 300)

        // Small movement (< 10 units)
        _player.Setup(p => p.Position).Returns(new Position(105, 200, 300));

        task.Update();

        // Should still be on stack (distance = 5, < 10)
        Assert.Single(_taskStack);
    }

    [Fact]
    public void Update_DestinationPreserved()
    {
        var task = new TeleportTask(_ctx.Object, "IronForge");
        _taskStack.Push(task);

        task.Update();

        _om.Verify(o => o.SendChatMessage(".tele name TestChar IronForge"), Times.Once);
    }

    [Fact]
    public void Update_DestinationWithSpaces()
    {
        var task = new TeleportTask(_ctx.Object, "Stormwind City");
        _taskStack.Push(task);

        task.Update();

        _om.Verify(o => o.SendChatMessage(".tele name TestChar Stormwind City"), Times.Once);
    }

    [Fact]
    public void Update_DeadOrGhost_PopsWithoutSendingCommand()
    {
        _player.Setup(p => p.Health).Returns(0u);
        _player.Setup(p => p.InGhostForm).Returns(true);
        _player.Setup(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);

        var task = new TeleportTask(_ctx.Object, "Orgrimmar");
        _taskStack.Push(task);

        task.Update();

        Assert.Empty(_taskStack);
        _om.Verify(o => o.SendChatMessage(It.IsAny<string>()), Times.Never);
    }
}

