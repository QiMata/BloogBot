using BotRunner.Clients;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xas.FluentBehaviourTree;

namespace BotRunner.Tests;

public class BotRunnerServiceInventoryFallbackTests
{
    [Fact]
    public void EnumerateByIdFallbackSlots_CoversBackpackAndEquippedBags()
    {
        var slots = BotRunnerService.EnumerateByIdFallbackSlots().ToArray();

        Assert.Equal(BotRunnerService.BackpackSlots + (BotRunnerService.ExtraBagCount * BotRunnerService.ExtraBagSlots), slots.Length);
        Assert.Contains((0, 0), slots);
        Assert.Contains((0, 15), slots);
        Assert.Contains((1, 0), slots);
        Assert.Contains((4, 19), slots);
        Assert.DoesNotContain((0, 16), slots);
        Assert.DoesNotContain((4, 20), slots);
        Assert.DoesNotContain((5, 0), slots);
    }

    [Fact]
    public void BuildEquipItemByIdSequence_Fallback_ProbesExtraBagSlots()
    {
        var service = CreateService(out var objectManager);
        var method = typeof(BotRunnerService).GetMethod("BuildEquipItemByIdSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var node = Assert.IsAssignableFrom<IBehaviourTreeNode>(method!.Invoke(service, [18831])!);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.EquipItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<EquipSlot?>()),
            Times.Exactly(BotRunnerService.BackpackSlots + (BotRunnerService.ExtraBagCount * BotRunnerService.ExtraBagSlots)));
        objectManager.Verify(o => o.EquipItem(1, 0, null), Times.Once);
        objectManager.Verify(o => o.EquipItem(4, 19, null), Times.Once);
        objectManager.Verify(o => o.EquipItem(0, 16, null), Times.Never);
    }

    [Fact]
    public void BuildUseItemByIdSequence_Fallback_ProbesExtraBagSlots()
    {
        var service = CreateService(out var objectManager);
        var method = typeof(BotRunnerService).GetMethod("BuildUseItemByIdSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var node = Assert.IsAssignableFrom<IBehaviourTreeNode>(method!.Invoke(service, [13444])!);
        var status = node.Tick(new TimeData(0.1f));

        Assert.Equal(BehaviourTreeStatus.Success, status);
        objectManager.Verify(o => o.UseItem(It.IsAny<int>(), It.IsAny<int>(), 0),
            Times.Exactly(BotRunnerService.BackpackSlots + (BotRunnerService.ExtraBagCount * BotRunnerService.ExtraBagSlots)));
        objectManager.Verify(o => o.UseItem(1, 0, 0), Times.Once);
        objectManager.Verify(o => o.UseItem(4, 19, 0), Times.Once);
        objectManager.Verify(o => o.UseItem(0, 16, 0), Times.Never);
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
}
