using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using BotRunner;
using BotRunner.Clients;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Interfaces;
using GameData.Core.Models;
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

    [Fact]
    public void UpdateBehaviorTree_WithCorrelatedApplyLoadout_EmitsStructuredSuccessAcks()
    {
        var harness = CreateDispatchHarness();
        var action = BuildApplyLoadoutAction(
            new LoadoutSpec { SpellIdsToLearn = { 12345u } },
            correlationId: "corr-loadout-1");

        InvokeUpdateBehaviorTree(
            harness.Service,
            new WoWActivitySnapshot { CurrentAction = action });

        var snapshot = ReadActivitySnapshot(harness.Service);
        Assert.NotNull(snapshot.CurrentAction);
        Assert.Equal("corr-loadout-1", snapshot.CurrentAction.CorrelationId);

        var loadoutTask = Assert.IsType<LoadoutTask>(ReadBotTasks(harness.Service).Peek());
        Thread.Sleep(LoadoutTask.StepPacingMs + 25);
        loadoutTask.Update();
        Assert.Contains(".learn 12345", harness.SentChat);

        harness.EventHandler.Raise(
            handler => handler.OnLearnedSpell += null!,
            harness.EventHandler.Object,
            new SpellChangedArgs(12345u));

        loadoutTask.Update();
        harness.Service.SyncLoadoutStatusIntoSnapshot();
        InvokeFlushMessageBuffers(harness.Service);

        var acks = ReadActivitySnapshot(harness.Service).RecentCommandAcks.ToList();
        Assert.Collection(
            acks,
            ack =>
            {
                Assert.Equal("corr-loadout-1", ack.CorrelationId);
                Assert.Equal(ActionType.ApplyLoadout, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Pending, ack.Status);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-1/step-1", ack.CorrelationId);
                Assert.Equal(ActionType.SendChat, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Pending, ack.Status);
                Assert.Equal(12345u, ack.RelatedId);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-1/step-1", ack.CorrelationId);
                Assert.Equal(ActionType.SendChat, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Success, ack.Status);
                Assert.Equal(12345u, ack.RelatedId);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-1", ack.CorrelationId);
                Assert.Equal(ActionType.ApplyLoadout, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Success, ack.Status);
            });
    }

    [Fact]
    public void UpdateBehaviorTree_WhenDuplicateApplyLoadoutArrives_DoesNotClobberOriginalAck()
    {
        var harness = CreateDispatchHarness();
        var firstAction = BuildApplyLoadoutAction(
            new LoadoutSpec { SpellIdsToLearn = { 12345u } },
            correlationId: "corr-loadout-1");
        var duplicateAction = BuildApplyLoadoutAction(
            new LoadoutSpec { SpellIdsToLearn = { 54321u } },
            correlationId: "corr-loadout-2");

        InvokeUpdateBehaviorTree(
            harness.Service,
            new WoWActivitySnapshot { CurrentAction = firstAction });

        var loadoutTask = Assert.IsType<LoadoutTask>(ReadBotTasks(harness.Service).Peek());
        Thread.Sleep(LoadoutTask.StepPacingMs + 25);
        loadoutTask.Update();
        Assert.Contains(".learn 12345", harness.SentChat);

        InvokeUpdateBehaviorTree(
            harness.Service,
            new WoWActivitySnapshot { CurrentAction = duplicateAction });

        harness.EventHandler.Raise(
            handler => handler.OnLearnedSpell += null!,
            harness.EventHandler.Object,
            new SpellChangedArgs(12345u));
        loadoutTask.Update();

        harness.Service.SyncLoadoutStatusIntoSnapshot();
        InvokeFlushMessageBuffers(harness.Service);

        var acks = ReadActivitySnapshot(harness.Service).RecentCommandAcks.ToList();
        Assert.Collection(
            acks,
            ack =>
            {
                Assert.Equal("corr-loadout-1", ack.CorrelationId);
                Assert.Equal(ActionType.ApplyLoadout, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Pending, ack.Status);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-1/step-1", ack.CorrelationId);
                Assert.Equal(ActionType.SendChat, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Pending, ack.Status);
                Assert.Equal(12345u, ack.RelatedId);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-2", ack.CorrelationId);
                Assert.Equal(ActionType.ApplyLoadout, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Pending, ack.Status);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-2", ack.CorrelationId);
                Assert.Equal(ActionType.ApplyLoadout, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Failed, ack.Status);
                Assert.Equal("loadout_task_already_active", ack.FailureReason);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-1/step-1", ack.CorrelationId);
                Assert.Equal(ActionType.SendChat, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Success, ack.Status);
                Assert.Equal(12345u, ack.RelatedId);
            },
            ack =>
            {
                Assert.Equal("corr-loadout-1", ack.CorrelationId);
                Assert.Equal(ActionType.ApplyLoadout, ack.ActionType);
                Assert.Equal(CommandAckEvent.Types.AckStatus.Success, ack.Status);
            });
    }

    private static ActionMessage BuildApplyLoadoutAction(LoadoutSpec spec, string? correlationId = null) => new()
    {
        ActionType = ActionType.ApplyLoadout,
        CorrelationId = correlationId ?? string.Empty,
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

    private static DispatchHarness CreateDispatchHarness()
    {
        var eventHandler = new Mock<IWoWEventHandler>(MockBehavior.Loose);
        var sentChat = new List<string>();

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(x => x.Guid).Returns(1UL);
        player.SetupGet(x => x.Position).Returns(new Position(0f, 0f, 0f));
        player.SetupGet(x => x.MaxHealth).Returns(100u);
        player.SetupGet(x => x.Level).Returns(60u);
        player.SetupGet(x => x.InGhostForm).Returns(false);
        player.SetupGet(x => x.Name).Returns("TESTBOT1");

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(eventHandler.Object);
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.HasEnteredWorld).Returns(true);
        objectManager.SetupGet(x => x.IsInMapTransition).Returns(false);
        objectManager.SetupGet(x => x.KnownSpellIds).Returns(Array.Empty<uint>());
        objectManager.Setup(x => x.GetContainedItems()).Returns(Array.Empty<IWoWItem>());
        objectManager.Setup(x => x.SendChatMessage(It.IsAny<string>()))
            .Callback<string>(sentChat.Add);

        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object,
            accountName: "TESTBOT1");

        return new DispatchHarness(service, eventHandler, sentChat);
    }

    private static Stack<IBotTask> ReadBotTasks(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_botTasks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Stack<IBotTask>>(field!.GetValue(service));
    }

    private static void InvokeUpdateBehaviorTree(BotRunnerService service, WoWActivitySnapshot snapshot)
    {
        var method = typeof(BotRunnerService).GetMethod("UpdateBehaviorTree", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, new object?[] { snapshot });
    }

    private static void InvokeFlushMessageBuffers(BotRunnerService service)
    {
        var method = typeof(BotRunnerService).GetMethod("FlushMessageBuffers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, null);
    }

    private static WoWActivitySnapshot ReadActivitySnapshot(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_activitySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WoWActivitySnapshot>(field!.GetValue(service));
    }

    private sealed record DispatchHarness(
        BotRunnerService Service,
        Mock<IWoWEventHandler> EventHandler,
        List<string> SentChat);
}
