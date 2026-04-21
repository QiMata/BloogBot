using System;
using System.Collections.Generic;
using System.Reflection;
using BotRunner;
using BotRunner.Clients;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BotRunner.Tests;

/// <summary>
/// P3.3: verifies that BotRunnerService dispatches <see cref="ActionType.ApplyLoadout"/>
/// by pushing a <see cref="LoadoutTask"/> and keeps the outbound snapshot's
/// <c>LoadoutStatus</c>/<c>LoadoutFailureReason</c> in sync with the task's state.
/// </summary>
public sealed class BotRunnerServiceLoadoutDispatchTests
{
    [Fact]
    public void HandleApplyLoadoutAction_PushesLoadoutTask_OntoBotTasks()
    {
        var service = CreateService();
        var action = BuildApplyLoadoutAction(new LoadoutSpec { TargetLevel = 60 });

        service.HandleApplyLoadoutAction(action);

        var tasks = ReadBotTasks(service);
        Assert.True(tasks.Count >= 1);
        Assert.IsType<LoadoutTask>(tasks.Peek());

        var snapshot = ReadActivitySnapshot(service);
        Assert.Same(action, snapshot.PreviousAction);
    }

    [Fact]
    public void HandleApplyLoadoutAction_WhenLoadoutTaskAlreadyActive_IgnoresDuplicate()
    {
        var service = CreateService();
        var firstAction = BuildApplyLoadoutAction(new LoadoutSpec { TargetLevel = 60 });
        var secondAction = BuildApplyLoadoutAction(new LoadoutSpec { TargetLevel = 60 });

        service.HandleApplyLoadoutAction(firstAction);
        var countAfterFirst = ReadBotTasks(service).Count;

        service.HandleApplyLoadoutAction(secondAction);
        var countAfterSecond = ReadBotTasks(service).Count;

        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    [Fact]
    public void HandleApplyLoadoutAction_NullLoadoutSpec_TreatedAsEmptySpec()
    {
        var service = CreateService();
        var action = new ActionMessage { ActionType = ActionType.ApplyLoadout }; // LoadoutSpec == null

        service.HandleApplyLoadoutAction(action);

        var tasks = ReadBotTasks(service);
        var top = Assert.IsType<LoadoutTask>(tasks.Peek());
        Assert.NotNull(top.Spec); // Converter uses an empty spec, never null.
    }

    [Fact]
    public void SyncLoadoutStatusIntoSnapshot_ReflectsTopLoadoutTaskProgress()
    {
        var service = CreateService();
        var action = BuildApplyLoadoutAction(new LoadoutSpec()); // empty spec → short-circuits to Ready
        service.HandleApplyLoadoutAction(action);

        var tasks = ReadBotTasks(service);
        var loadoutTask = Assert.IsType<LoadoutTask>(tasks.Peek());

        // Before running the task: snapshot should still read NotStarted.
        service.SyncLoadoutStatusIntoSnapshot();
        var snapshot = ReadActivitySnapshot(service);
        Assert.Equal(LoadoutStatus.LoadoutNotStarted, snapshot.LoadoutStatus);

        // One Update tick flips the empty-spec task to Ready.
        loadoutTask.Update();
        service.SyncLoadoutStatusIntoSnapshot();
        snapshot = ReadActivitySnapshot(service);
        Assert.Equal(LoadoutStatus.LoadoutReady, snapshot.LoadoutStatus);
        Assert.Equal(string.Empty, snapshot.LoadoutFailureReason);
    }

    [Fact]
    public void SyncLoadoutStatusIntoSnapshot_WithNoLoadoutTask_KeepsLastKnownStatus()
    {
        // Regression guard: once Ready is reported, the snapshot must keep reporting
        // it even after the LoadoutTask is popped off the stack (coordinator gate
        // relies on LoadoutReady being stable across ticks).
        var service = CreateService();
        var action = BuildApplyLoadoutAction(new LoadoutSpec());
        service.HandleApplyLoadoutAction(action);

        var tasks = ReadBotTasks(service);
        var loadoutTask = Assert.IsType<LoadoutTask>(tasks.Peek());
        loadoutTask.Update();
        service.SyncLoadoutStatusIntoSnapshot();

        tasks.Pop(); // Simulate downstream logic popping the completed task.

        service.SyncLoadoutStatusIntoSnapshot();
        var snapshot = ReadActivitySnapshot(service);
        Assert.Equal(LoadoutStatus.LoadoutReady, snapshot.LoadoutStatus);
    }

    private static ActionMessage BuildApplyLoadoutAction(LoadoutSpec spec) => new()
    {
        ActionType = ActionType.ApplyLoadout,
        LoadoutSpec = spec,
    };

    private static BotRunnerService CreateService()
    {
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager
            .SetupGet(x => x.EventHandler)
            .Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);

        return new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object,
            accountName: "TESTBOT1");
    }

    private static Stack<IBotTask> ReadBotTasks(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_botTasks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Stack<IBotTask>>(field!.GetValue(service));
    }

    private static WoWActivitySnapshot ReadActivitySnapshot(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_activitySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WoWActivitySnapshot>(field!.GetValue(service));
    }
}
