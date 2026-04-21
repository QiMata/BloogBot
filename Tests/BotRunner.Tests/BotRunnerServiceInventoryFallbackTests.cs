using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xas.FluentBehaviourTree;

namespace BotRunner.Tests;

public class BotRunnerServiceInventoryResolutionTests
{
    [Fact]
    public void BuildEquipItemByIdSequence_TrackedInventoryItem_EquipsResolvedSlot()
    {
        var service = CreateService(out var objectManager);
        var item = new Mock<IWoWItem>(MockBehavior.Loose);
        item.SetupGet(i => i.ItemId).Returns(18831u);
        objectManager.Setup(o => o.GetContainedItem(2, 7)).Returns(item.Object);

        var node = InvokeInteractionSequence(service, "BuildEquipItemByIdSequence", 18831);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.EquipItem(2, 7, null), Times.Once);
        objectManager.Verify(o => o.EquipItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<EquipSlot?>()),
            Times.Once);
    }

    [Fact]
    public void BuildEquipItemByIdSequence_ItemMissing_FailsWithoutFallback()
    {
        var service = CreateService(out var objectManager);
        var node = InvokeInteractionSequence(service, "BuildEquipItemByIdSequence", 18831);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Failure, status);
        objectManager.Verify(o => o.EquipItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<EquipSlot?>()), Times.Never);
    }

    [Fact]
    public void BuildUseItemByIdSequence_TrackedInventoryItem_UsesResolvedSlot()
    {
        var service = CreateService(out var objectManager);
        var item = new Mock<IWoWItem>(MockBehavior.Loose);
        item.SetupGet(i => i.ItemId).Returns(13444u);
        objectManager.Setup(o => o.GetContainedItem(3, 4)).Returns(item.Object);

        var node = InvokeInteractionSequence(service, "BuildUseItemByIdSequence", 13444);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.UseItem(3, 4, 0), Times.Once);
        objectManager.Verify(o => o.UseItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ulong>()), Times.Once);
    }

    [Fact]
    public void BuildUseItemByIdSequence_ItemMissing_FailsWithoutFallback()
    {
        var service = CreateService(out var objectManager);
        var node = InvokeInteractionSequence(service, "BuildUseItemByIdSequence", 13444);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Failure, status);
        objectManager.Verify(o => o.UseItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void BuildUseItemByIdSequence_DbDerivedMountItemIndoors_SkipsUse()
    {
        var service = CreateService(out var objectManager);
        objectManager.SetupGet(o => o.PhysicsAllowsMountByEnvironment).Returns(false);
        objectManager.SetupGet(o => o.PhysicsEnvironmentFlags).Returns(SceneEnvironmentFlags.Indoors);

        var node = InvokeInteractionSequence(service, "BuildUseItemByIdSequence", 23720);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.UseItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_DbDerivedMountSpellIndoors_SkipsCast()
    {
        var service = CreateService(out var objectManager);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.PhysicsAllowsMountByEnvironment).Returns(false);
        objectManager.SetupGet(o => o.PhysicsEnvironmentFlags).Returns(SceneEnvironmentFlags.Indoors);

        var node = BuildActionTree(service, CharacterAction.CastSpell, 458, 0UL);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.CastSpell(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        objectManager.Verify(o => o.StopAllMovement(), Times.Never);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_DbDerivedMountSpellIndoors_EnqueuesMountBlockDiagnostic()
    {
        var service = CreateService(out var objectManager);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.PhysicsAllowsMountByEnvironment).Returns(false);
        objectManager.SetupGet(o => o.PhysicsEnvironmentFlags).Returns(SceneEnvironmentFlags.Indoors);

        var node = BuildActionTree(service, CharacterAction.CastSpell, 458, 0UL);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        Assert.Contains(GetRecentChatMessages(service), message =>
            message.Contains("[MOUNT-BLOCK] spell=458", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_ZeroTargetFishingSpell_BypassesCanCastProbeAndCasts()
    {
        var service = CreateService(out var objectManager);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.Setup(o => o.CanCastSpell((int)FishingData.FishingRank1, 0UL)).Returns(false);

        var node = BuildActionTree(service, CharacterAction.CastSpell, (int)FishingData.FishingRank1, 0UL);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.StopAllMovement(), Times.Once);
        objectManager.Verify(o => o.CastSpell((int)FishingData.FishingRank1, -1, false), Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_MountItemIndoors_SkipsUse()
    {
        var service = CreateService(out var objectManager);
        var item = new Mock<IWoWItem>(MockBehavior.Loose);
        item.SetupGet(i => i.ItemId).Returns(23720u);
        objectManager.Setup(o => o.GetContainedItem(0, 3)).Returns(item.Object);
        objectManager.SetupGet(o => o.PhysicsAllowsMountByEnvironment).Returns(false);
        objectManager.SetupGet(o => o.PhysicsEnvironmentFlags).Returns(SceneEnvironmentFlags.Indoors);

        var node = BuildActionTree(service, CharacterAction.UseItem, 0, 3, 0UL);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.UseItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ulong>()), Times.Never);
    }

    private static BotRunnerService CreateService(out Mock<IObjectManager> objectManager)
    {
        objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(o => o.Objects).Returns(Array.Empty<IWoWObject>());
        objectManager.Setup(o => o.GetContainedItems()).Returns(Array.Empty<IWoWItem>());

        var dependencies = new Mock<IDependencyContainer>(MockBehavior.Loose);
        var updateClient = new CharacterStateUpdateClient(NullLogger.Instance);
        return new BotRunnerService(objectManager.Object, updateClient, dependencies.Object);
    }

    private static IBehaviourTreeNode InvokeInteractionSequence(BotRunnerService service, string methodName, params object[] arguments)
    {
        var field = typeof(BotRunnerService).GetField("_interactionSequences", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        var builder = field!.GetValue(service);
        Assert.NotNull(builder);

        var method = builder!.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        return Assert.IsAssignableFrom<IBehaviourTreeNode>(method!.Invoke(builder, arguments)!);
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

    private static IReadOnlyCollection<string> GetRecentChatMessages(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_recentChatMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        return Assert.IsType<Queue<string>>(field!.GetValue(service)).ToArray();
    }
}
