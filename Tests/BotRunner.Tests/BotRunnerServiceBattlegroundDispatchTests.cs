using BotRunner.Clients;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using BotRunner.Tasks.Battlegrounds;
using BotRunner.Travel;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Generic;
using System.Reflection;
using Xas.FluentBehaviourTree;

namespace BotRunner.Tests;

public class BotRunnerServiceBattlegroundDispatchTests
{
    [Fact]
    public void BuildBehaviorTreeFromActions_JoinBattleground_PushesQueueTask()
    {
        var service = CreateService(out _);

        var node = BuildActionTree(
            service,
            CharacterAction.JoinBattleground,
            (int)BattlemasterData.BattlegroundType.ArathiBasin,
            529);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));

        var task = Assert.IsType<BattlegroundQueueTask>(Assert.Single(GetBotTasks(service)));
        Assert.Equal(
            BattlemasterData.BattlegroundType.ArathiBasin,
            GetPrivateField<BattlemasterData.BattlegroundType>(task, "_bgType"));
        Assert.Equal(529u, GetPrivateField<uint>(task, "_expectedBgMapId"));
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_JoinBattleground_DuplicateDispatch_DoesNotGrowTaskStack()
    {
        var service = CreateService(out _);

        var firstNode = BuildActionTree(
            service,
            CharacterAction.JoinBattleground,
            (int)BattlemasterData.BattlegroundType.WarsongGulch,
            489);
        Assert.Equal(BehaviourTreeStatus.Success, firstNode.Tick(new TimeData(0.1f)));

        var secondNode = BuildActionTree(
            service,
            CharacterAction.JoinBattleground,
            (int)BattlemasterData.BattlegroundType.WarsongGulch,
            489);
        Assert.Equal(BehaviourTreeStatus.Success, secondNode.Tick(new TimeData(0.1f)));

        var tasks = GetBotTasks(service);
        var queueTask = Assert.IsType<BattlegroundQueueTask>(Assert.Single(tasks));
        Assert.Equal(
            BattlemasterData.BattlegroundType.WarsongGulch,
            GetPrivateField<BattlemasterData.BattlegroundType>(queueTask, "_bgType"));
        Assert.Equal(489u, GetPrivateField<uint>(queueTask, "_expectedBgMapId"));
    }

    private static BotRunnerService CreateService(out Mock<IObjectManager> objectManager)
    {
        objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(o => o.Objects).Returns(System.Array.Empty<IWoWObject>());
        objectManager.SetupGet(o => o.Units).Returns(System.Array.Empty<IWoWUnit>());
        objectManager.Setup(o => o.GetContainedItems()).Returns(System.Array.Empty<IWoWItem>());

        var dependencies = new Mock<IDependencyContainer>(MockBehavior.Loose);
        var updateClient = new CharacterStateUpdateClient(NullLogger.Instance);
        return new BotRunnerService(objectManager.Object, updateClient, dependencies.Object);
    }

    private static IBehaviourTreeNode BuildActionTree(BotRunnerService service, CharacterAction action, params object[] parameters)
    {
        var method = typeof(BotRunnerService).GetMethod("BuildBehaviorTreeFromActions", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var actionMap = new List<(CharacterAction, List<object>)>
        {
            (action, [.. parameters])
        };

        return Assert.IsAssignableFrom<IBehaviourTreeNode>(method!.Invoke(service, [actionMap])!);
    }

    private static Stack<IBotTask> GetBotTasks(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_botTasks", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        return Assert.IsType<Stack<IBotTask>>(field!.GetValue(service));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return Assert.IsType<T>(field!.GetValue(instance));
    }
}
