using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Models;
using WoWSharpClient.Networking.ClientComponents.I;
using Xas.FluentBehaviourTree;

namespace BotRunner.Tests;

public class BotRunnerServiceCombatDispatchTests
{
    [Fact]
    public void BuildBehaviorTreeFromActions_StartMeleeAttack_BadFacingRetry_PrimesExactFacingThenRetriesSwing()
    {
        var service = CreateService(out var objectManager);
        const ulong playerGuid = 0x10;
        const ulong targetGuid = 0x1234;

        var player = new WoWLocalPlayer(new HighGuid(playerGuid))
        {
            Position = new Position(0f, 0f, 0f),
            Facing = 0f,
            CombatReach = 1.5f,
            TargetGuid = 0,
            IsAutoAttacking = false,
        };

        var target = new WoWUnit(new HighGuid(targetGuid))
        {
            Position = new Position(1f, 0f, 0f),
            Health = 100,
            CombatReach = 1.5f,
        };

        var hadRecentFacingReject = false;
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.SetupGet(o => o.Units).Returns([target]);
        objectManager.SetupGet(o => o.Objects).Returns([target]);
        objectManager.Setup(o => o.SetTarget(targetGuid))
            .Callback<ulong>(guid => player.TargetGuid = guid);
        objectManager.Setup(o => o.Face(It.IsAny<Position>()))
            .Callback<Position>(position => player.Facing = CalculateFacing(player.Position, position));
        objectManager.Setup(o => o.SetFacing(It.IsAny<float>()))
            .Callback<float>(facing => player.Facing = facing);
        objectManager.Setup(o => o.StartMeleeAttack())
            .Callback(() => player.IsAutoAttacking = true);
        objectManager.Setup(o => o.HadRecentMeleeFacingRejection(targetGuid))
            .Returns(() => hadRecentFacingReject);

        var node = BuildActionTree(service, CharacterAction.StartMeleeAttack, targetGuid);

        Assert.Equal(BehaviourTreeStatus.Running, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.SetTarget(targetGuid), Times.Exactly(2));
        objectManager.Verify(o => o.Face(It.Is<Position>(p => p.X == 1f && p.Y == 0f && p.Z == 0f)), Times.Once);
        objectManager.Verify(o => o.StartMeleeAttack(), Times.Never);

        Assert.Equal(BehaviourTreeStatus.Running, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StartMeleeAttack(), Times.Once);

        player.IsAutoAttacking = false;
        hadRecentFacingReject = true;

        Assert.Equal(BehaviourTreeStatus.Running, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StopAttack(), Times.Once);
        objectManager.Verify(o => o.SetFacing(It.Is<float>(f => MathF.Abs(f) < 0.001f)), Times.Once);
        objectManager.Verify(o => o.StartMeleeAttack(), Times.Once);

        Assert.Equal(BehaviourTreeStatus.Running, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StartMeleeAttack(), Times.Exactly(2));
        objectManager.Verify(o => o.SetFacing(It.IsAny<float>()), Times.Once);
        objectManager.Verify(o => o.StopAttack(), Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_StartWandAttack_FacesTargetBeforeShoot()
    {
        var service = CreateService(out var objectManager);
        const ulong targetGuid = 0x5678;

        var player = new WoWLocalPlayer(new HighGuid(0x11))
        {
            Position = new Position(0f, 0f, 0f),
            Facing = MathF.PI,
            TargetGuid = 0,
        };

        var target = new WoWUnit(new HighGuid(targetGuid))
        {
            Position = new Position(2f, 0f, 0f),
            Health = 100,
        };

        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.SetupGet(o => o.Units).Returns([target]);
        objectManager.SetupGet(o => o.Objects).Returns([target]);
        objectManager.Setup(o => o.SetTarget(targetGuid))
            .Callback<ulong>(guid => player.TargetGuid = guid);
        objectManager.Setup(o => o.Face(It.IsAny<Position>()))
            .Callback<Position>(position => player.Facing = CalculateFacing(player.Position, position));

        var node = BuildActionTree(service, CharacterAction.StartWandAttack, targetGuid);

        Assert.Equal(BehaviourTreeStatus.Running, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.SetTarget(targetGuid), Times.Exactly(2));
        objectManager.Verify(o => o.StopAllMovement(), Times.Once);
        objectManager.Verify(o => o.Face(It.Is<Position>(p => p.X == 2f && p.Y == 0f && p.Z == 0f)), Times.Once);
        objectManager.Verify(o => o.StartWandAttack(), Times.Never);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StartWandAttack(), Times.Once);
        objectManager.Verify(o => o.Face(It.IsAny<Position>()), Times.Exactly(2));
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_InteractWithMissingGuid_ReturnsFailureWithoutInteraction()
    {
        var service = CreateService(out var objectManager);

        var node = BuildActionTree(service, CharacterAction.InteractWith);

        Assert.Equal(BehaviourTreeStatus.Failure, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.InteractWithNpcAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        objectManager.Verify(o => o.InteractWithGameObject(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_InteractWithGameObject_StopsBeforeInteraction()
    {
        var service = CreateService(out var objectManager);
        const ulong guid = 0xABCDUL;
        var gameObject = new Mock<IWoWGameObject>(MockBehavior.Strict);
        var callOrder = new List<string>();

        objectManager.SetupGet(o => o.GameObjects).Returns([gameObject.Object]);
        gameObject.SetupGet(go => go.Guid).Returns(guid);
        gameObject.Setup(go => go.Interact())
            .Callback(() => callOrder.Add("interact"));

        var node = BuildActionTree(service, CharacterAction.InteractWith, guid);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        Assert.Equal(["interact"], callOrder);
        objectManager.Verify(o => o.ForceStopImmediate(), Times.Never);
        gameObject.Verify(go => go.Interact(), Times.Once);
        objectManager.Verify(o => o.SetTarget(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_InteractWithSpiritHealer_WhenGhost_UsesDeadActorAgent()
    {
        const ulong healerGuid = 0x6491UL;
        var deadActor = new Mock<IDeadActorNetworkClientComponent>(MockBehavior.Strict);
        deadActor
            .Setup(agent => agent.ResurrectWithSpiritHealerAsync(healerGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agentFactory = new Mock<IAgentFactory>(MockBehavior.Strict);
        agentFactory.SetupGet(factory => factory.DeadActorAgent).Returns(deadActor.Object);

        var service = CreateService(
            out var objectManager,
            agentFactoryAccessor: () => agentFactory.Object);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.Health).Returns(0);
        player.SetupGet(p => p.InGhostForm).Returns(true);
        player.SetupGet(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);
        player.SetupGet(p => p.Bytes1).Returns([0u]);

        var healer = new WoWUnit(new HighGuid(healerGuid))
        {
            Name = "Spirit Healer",
            NpcFlags = (NPCFlags)0x20,
        };

        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.GameObjects).Returns(Array.Empty<IWoWGameObject>());
        objectManager.SetupGet(o => o.Units).Returns([healer]);
        objectManager.SetupGet(o => o.Objects).Returns([healer]);
        objectManager
            .Setup(o => o.InteractWithNpcAsync(healerGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var node = BuildActionTree(service, CharacterAction.InteractWith, healerGuid);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.InteractWithNpcAsync(healerGuid, It.IsAny<CancellationToken>()), Times.Once);
        deadActor.Verify(
            agent => agent.ResurrectWithSpiritHealerAsync(healerGuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_InteractWithNpc_WhenGhostAndUnitMissing_UsesDeadActorAgent()
    {
        const ulong npcGuid = 0xF13000195B009E7CUL;
        var deadActor = new Mock<IDeadActorNetworkClientComponent>(MockBehavior.Strict);
        deadActor
            .Setup(agent => agent.ResurrectWithSpiritHealerAsync(npcGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agentFactory = new Mock<IAgentFactory>(MockBehavior.Strict);
        agentFactory.SetupGet(factory => factory.DeadActorAgent).Returns(deadActor.Object);

        var service = CreateService(
            out var objectManager,
            agentFactoryAccessor: () => agentFactory.Object);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.Health).Returns(0);
        player.SetupGet(p => p.InGhostForm).Returns(true);
        player.SetupGet(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);
        player.SetupGet(p => p.Bytes1).Returns([0u]);

        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.GameObjects).Returns(Array.Empty<IWoWGameObject>());
        objectManager.SetupGet(o => o.Units).Returns(Array.Empty<IWoWUnit>());
        objectManager.SetupGet(o => o.Objects).Returns(Array.Empty<IWoWGameObject>());
        objectManager
            .Setup(o => o.InteractWithNpcAsync(npcGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var node = BuildActionTree(service, CharacterAction.InteractWith, npcGuid);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.InteractWithNpcAsync(npcGuid, It.IsAny<CancellationToken>()), Times.Once);
        deadActor.Verify(
            agent => agent.ResurrectWithSpiritHealerAsync(npcGuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_InteractWithGuidInGameObjects_WhenGhost_UsesDeadActorAgent()
    {
        const ulong npcGuid = 0xF13000195B009E7CUL;
        var deadActor = new Mock<IDeadActorNetworkClientComponent>(MockBehavior.Strict);
        deadActor
            .Setup(agent => agent.ResurrectWithSpiritHealerAsync(npcGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agentFactory = new Mock<IAgentFactory>(MockBehavior.Strict);
        agentFactory.SetupGet(factory => factory.DeadActorAgent).Returns(deadActor.Object);

        var service = CreateService(
            out var objectManager,
            agentFactoryAccessor: () => agentFactory.Object);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.Health).Returns(0);
        player.SetupGet(p => p.InGhostForm).Returns(true);
        player.SetupGet(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);
        player.SetupGet(p => p.Bytes1).Returns([0u]);

        var gameObject = new Mock<IWoWGameObject>(MockBehavior.Strict);
        gameObject.SetupGet(go => go.Guid).Returns(npcGuid);

        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.GameObjects).Returns([gameObject.Object]);
        objectManager.SetupGet(o => o.Units).Returns(Array.Empty<IWoWUnit>());
        objectManager.SetupGet(o => o.Objects).Returns([gameObject.Object]);
        objectManager
            .Setup(o => o.InteractWithNpcAsync(npcGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var node = BuildActionTree(service, CharacterAction.InteractWith, npcGuid);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.InteractWithNpcAsync(npcGuid, It.IsAny<CancellationToken>()), Times.Once);
        gameObject.Verify(go => go.Interact(), Times.Never);
        deadActor.Verify(
            agent => agent.ResurrectWithSpiritHealerAsync(npcGuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_CheckMailMissingGuid_ReturnsFailureWithoutMailCollection()
    {
        var service = CreateService(out var objectManager);

        var node = BuildActionTree(service, CharacterAction.CheckMail);

        Assert.Equal(BehaviourTreeStatus.Failure, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.CollectAllMailAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_ReleaseCorpse_HealthZeroWithoutStandDead_ReleasesSpirit()
    {
        var service = CreateService(out var objectManager);
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.Health).Returns(0);
        player.SetupGet(p => p.InGhostForm).Returns(false);
        player.SetupGet(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_NONE);
        player.SetupGet(p => p.Bytes1).Returns([0u]);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);

        var node = BuildActionTree(service, CharacterAction.ReleaseCorpse);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.ReleaseSpirit(), Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_TravelTo_SameMap_UpsertsPersistentGoToTask()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new HighGuid(0x20))
        {
            MapId = 1,
            Position = new Position(10f, 20f, 5f),
        };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var node = BuildActionTree(service, CharacterAction.TravelTo, 1, 100f, 200f, 15f);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StopAllMovement(), Times.Never);

        var tasks = GetBotTasks(service);
        var goTo = Assert.IsType<GoToTask>(Assert.Single(tasks));
        Assert.Equal(100f, goTo.Target.X);
        Assert.Equal(200f, goTo.Target.Y);
        Assert.Equal(15f, goTo.Target.Z);
        Assert.Equal(15f, goTo.Tolerance);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_TravelTo_AlreadyWithinLegacyArrivalTolerance_StopsWithoutTask()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new HighGuid(0x21))
        {
            MapId = 1,
            Position = new Position(100f, 100f, 5f),
        };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var node = BuildActionTree(service, CharacterAction.TravelTo, 1, 110f, 100f, 5f);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StopAllMovement(), Times.Once);
        Assert.Empty(GetBotTasks(service));
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_TravelTo_CrossMap_ReturnsFailureWithoutTask()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new HighGuid(0x22))
        {
            MapId = 0,
            Position = new Position(10f, 20f, 5f),
        };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var node = BuildActionTree(service, CharacterAction.TravelTo, 1, 100f, 200f, 15f);

        Assert.Equal(BehaviourTreeStatus.Failure, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.StopAllMovement(), Times.Never);
        Assert.Empty(GetBotTasks(service));
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_TrainTalentWithoutParameters_UsesTalentService()
    {
        var talentService = new Mock<ITalentService>(MockBehavior.Strict);
        talentService
            .Setup(t => t.AllocateAvailablePointsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService(out var objectManager, talentService: talentService.Object);
        objectManager.SetupGet(o => o.Player).Returns(new WoWLocalPlayer(new HighGuid(0x23))
        {
            Class = Class.Warrior,
        });

        var node = BuildActionTree(service, CharacterAction.TrainTalent);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        talentService.Verify(t => t.AllocateAvailablePointsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_TrainTalentWithoutParametersAndNoTalentService_ReturnsFailure()
    {
        var service = CreateService(out var objectManager);
        objectManager.SetupGet(o => o.Player).Returns(new WoWLocalPlayer(new HighGuid(0x24))
        {
            Class = Class.Warrior,
        });

        var node = BuildActionTree(service, CharacterAction.TrainTalent);

        Assert.Equal(BehaviourTreeStatus.Failure, node.Tick(new TimeData(0.1f)));
    }

    [Fact]
    public void BuildBehaviorTreeFromActions_TrainTalentSingleParameter_WithOnePoint_LearnsTalent()
    {
        var service = CreateService(out var objectManager);
        var talentFrame = new Mock<ITalentFrame>(MockBehavior.Strict);
        talentFrame.SetupGet(t => t.TalentPointsAvailable).Returns(1);
        talentFrame.Setup(t => t.LearnTalent(16462));
        objectManager.SetupGet(o => o.TalentFrame).Returns(talentFrame.Object);

        var node = BuildActionTree(service, CharacterAction.TrainTalent, 16462);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        talentFrame.Verify(t => t.LearnTalent(16462), Times.Once);
    }

    [Theory]
    [InlineData(".targetguid 4660", 0x1234UL)]
    [InlineData(".targetguid 0x1234", 0x1234UL)]
    public void BuildBehaviorTreeFromActions_SendChatTargetGuid_SelectsGuidWithoutServerChat(
        string command,
        ulong expectedGuid)
    {
        var service = CreateService(out var objectManager);
        objectManager.Setup(o => o.SetTarget(expectedGuid));

        var node = BuildActionTree(service, CharacterAction.SendChat, command);

        Assert.Equal(BehaviourTreeStatus.Success, node.Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.SetTarget(expectedGuid), Times.Once);
        objectManager.Verify(o => o.SendChatMessage(It.IsAny<string>()), Times.Never);
        objectManager.Verify(o => o.SendGmCommandAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    private static BotRunnerService CreateService(
        out Mock<IObjectManager> objectManager,
        ITalentService? talentService = null,
        Func<IAgentFactory?>? agentFactoryAccessor = null)
    {
        objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(o => o.Objects).Returns(System.Array.Empty<IWoWObject>());
        objectManager.SetupGet(o => o.Units).Returns(System.Array.Empty<IWoWUnit>());
        objectManager.Setup(o => o.GetContainedItems()).Returns(System.Array.Empty<IWoWItem>());

        var dependencies = new Mock<IDependencyContainer>(MockBehavior.Loose);
        var updateClient = new CharacterStateUpdateClient(NullLogger.Instance);
        return new BotRunnerService(
            objectManager.Object,
            updateClient,
            dependencies.Object,
            agentFactoryAccessor,
            talentService: talentService);
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

    private static float CalculateFacing(Position from, Position to)
    {
        var facing = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        return facing < 0f
            ? facing + (MathF.PI * 2f)
            : facing;
    }
}
