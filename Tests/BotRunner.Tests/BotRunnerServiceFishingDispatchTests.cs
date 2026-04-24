using BotRunner.Clients;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Generic;
using System.Reflection;
using Xas.FluentBehaviourTree;

namespace BotRunner.Tests;

public class BotRunnerServiceFishingDispatchTests
{
    [Fact]
    public void BuildBehaviorTreeFromActionMessage_StartFishing_ForwardsLocationFlagsAndWaypoints()
    {
        var service = CreateService(out _);
        var node = BuildActionTree(service, new ActionMessage
        {
            ActionType = ActionType.StartFishing,
            Parameters =
            {
                new RequestParameter { StringParam = "Ratchet" },
                new RequestParameter { IntParam = 1 },
                new RequestParameter { IntParam = 2628 },
                new RequestParameter { FloatParam = 10f },
                new RequestParameter { FloatParam = 20f },
                new RequestParameter { FloatParam = 30f },
                new RequestParameter { FloatParam = 40f },
                new RequestParameter { FloatParam = 50f },
                new RequestParameter { FloatParam = 60f },
            }
        });

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));

        var task = Assert.IsType<FishingTask>(Assert.Single(GetBotTasks(service)));
        Assert.Equal("Ratchet", GetPrivateField<string>(task, "_location"));
        Assert.True(GetPrivateField<bool>(task, "_useGmCommands"));
        Assert.Equal((uint?)2628, GetPrivateNullableField<uint>(task, "_masterPoolId"));

        var searchWaypoints = GetPrivateFieldAssignableTo<IReadOnlyList<Position>>(task, "_searchWaypoints");
        Assert.Equal(2, searchWaypoints.Count);
        Assert.Equal(10f, searchWaypoints[0].X);
        Assert.Equal(20f, searchWaypoints[0].Y);
        Assert.Equal(30f, searchWaypoints[0].Z);
        Assert.Equal(40f, searchWaypoints[1].X);
        Assert.Equal(50f, searchWaypoints[1].Y);
        Assert.Equal(60f, searchWaypoints[1].Z);
    }

    [Fact]
    public void BuildBehaviorTreeFromActionMessage_StartFishing_LegacyWaypointOnlyShapeRemainsSupported()
    {
        var service = CreateService(out _);
        var node = BuildActionTree(service, new ActionMessage
        {
            ActionType = ActionType.StartFishing,
            Parameters =
            {
                new RequestParameter { FloatParam = 1f },
                new RequestParameter { FloatParam = 2f },
                new RequestParameter { FloatParam = 3f },
            }
        });

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));

        var task = Assert.IsType<FishingTask>(Assert.Single(GetBotTasks(service)));
        Assert.Null(GetPrivateReferenceField<string>(task, "_location"));
        Assert.False(GetPrivateField<bool>(task, "_useGmCommands"));
        Assert.Null(GetPrivateNullableField<uint>(task, "_masterPoolId"));

        var searchWaypoints = GetPrivateFieldAssignableTo<IReadOnlyList<Position>>(task, "_searchWaypoints");
        var waypoint = Assert.Single(searchWaypoints);
        Assert.Equal(1f, waypoint.X);
        Assert.Equal(2f, waypoint.Y);
        Assert.Equal(3f, waypoint.Z);
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

    private static IBehaviourTreeNode BuildActionTree(BotRunnerService service, ActionMessage action)
    {
        var method = typeof(BotRunnerService).GetMethod("BuildBehaviorTreeFromActions", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var actionMap = BotRunnerService.ConvertActionMessageToCharacterActions(action);
        return Assert.IsAssignableFrom<IBehaviourTreeNode>(method!.Invoke(service, [actionMap])!);
    }

    private static Stack<IBotTask> GetBotTasks(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_botTasks", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        return Assert.IsType<Stack<IBotTask>>(field!.GetValue(service));
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : notnull
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static T? GetPrivateNullableField<T>(object instance, string fieldName) where T : struct
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(instance);
        if (value is null)
            return null;

        return Assert.IsType<T>(value);
    }

    private static T? GetPrivateReferenceField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return field!.GetValue(instance) as T;
    }

    private static T GetPrivateFieldAssignableTo<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return Assert.IsAssignableFrom<T>(field!.GetValue(instance));
    }
}
