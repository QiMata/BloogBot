using BotRunner.Combat;
using BotRunner.Movement;
using BotRunner.Clients;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace BotRunner.Tests
{
    public class TargetEngagementServiceTests
    {
        [Fact]
        public async Task EngageAsync_AttacksWhenTargetNotSelected()
        {
            var targetingAgent = new Mock<ITargetingNetworkClientComponent>(MockBehavior.Strict);
            targetingAgent.Setup(x => x.IsTargeted(123UL)).Returns(false);

            var attackAgent = new Mock<IAttackNetworkClientComponent>(MockBehavior.Strict);
            attackAgent.SetupGet(x => x.IsAttacking).Returns(false);
            attackAgent.Setup(x => x.AttackTargetAsync(123UL, targetingAgent.Object, CancellationToken.None)).Returns(Task.CompletedTask);

            var lootingAgent = new Mock<ILootingNetworkClientComponent>(MockBehavior.Strict);

            var service = CreateEngagementService(targetingAgent, attackAgent, lootingAgent);

            var unit = new Mock<IWoWUnit>();
            unit.SetupGet(x => x.Guid).Returns(123UL);

            await service.EngageAsync(unit.Object, CancellationToken.None);

            attackAgent.Verify(x => x.AttackTargetAsync(123UL, targetingAgent.Object, CancellationToken.None), Times.Once);
            attackAgent.Verify(x => x.StartAttackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EngageAsync_StartsAttackWhenAlreadyTargeted()
        {
            var targetingAgent = new Mock<ITargetingNetworkClientComponent>(MockBehavior.Strict);
            targetingAgent.Setup(x => x.IsTargeted(456UL)).Returns(true);

            var attackAgent = new Mock<IAttackNetworkClientComponent>(MockBehavior.Strict);
            attackAgent.SetupGet(x => x.IsAttacking).Returns(false);
            attackAgent.Setup(x => x.StartAttackAsync(CancellationToken.None)).Returns(Task.CompletedTask);

            var lootingAgent = new Mock<ILootingNetworkClientComponent>(MockBehavior.Strict);

            var service = CreateEngagementService(targetingAgent, attackAgent, lootingAgent);

            var unit = new Mock<IWoWUnit>();
            unit.SetupGet(x => x.Guid).Returns(456UL);

            await service.EngageAsync(unit.Object, CancellationToken.None);

            attackAgent.Verify(x => x.StartAttackAsync(CancellationToken.None), Times.Once);
            attackAgent.Verify(x => x.AttackTargetAsync(It.IsAny<ulong>(), It.IsAny<ITargetingNetworkClientComponent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static TargetEngagementService CreateEngagementService(
            Mock<ITargetingNetworkClientComponent> targetingAgent,
            Mock<IAttackNetworkClientComponent> attackAgent,
            Mock<ILootingNetworkClientComponent> lootingAgent)
        {
            var agentFactory = MockHelpers.CreateAgentFactory(targetingAgent, attackAgent, lootingAgent);
            return new TargetEngagementService(agentFactory.Object, new BotCombatState());
        }
    }

    public class TargetPositioningServiceTests
    {
        [Fact]
        public void EnsureInCombatRange_MovesTowardWaypointWhenOutOfRange()
        {
            var player = new Mock<IWoWLocalPlayer>();
            player.SetupGet(x => x.MapId).Returns(1U);
            player.SetupGet(x => x.Position).Returns(new Position(0, 0, 0));
            player.Setup(x => x.IsFacing(It.IsAny<IWoWUnit>())).Returns(true);

            var objectManager = new Mock<IObjectManager>();
            objectManager.SetupGet(x => x.Player).Returns(player.Object);

            // Target at 50y away — well beyond the 25y default engagement range
            var target = new Mock<IWoWUnit>();
            target.SetupGet(x => x.Position).Returns(new Position(50, 0, 0));

            // NavigationPath skips waypoint[0] (current pos) and returns waypoint[1]
            var pathfindingClient = new MovementTestPathfindingClient(new[] { new Position(0, 0, 0), new Position(1, 1, 1) });

            var service = new TargetPositioningService(objectManager.Object, pathfindingClient);

            var inRange = service.EnsureInCombatRange(target.Object);

            Assert.False(inRange);
            objectManager.Verify(x => x.MoveToward(It.Is<Position>(p => p.X == 1 && p.Y == 1 && p.Z == 1)), Times.Once);
            objectManager.Verify(x => x.Face(It.IsAny<Position>()), Times.Never);
            objectManager.Verify(x => x.StopAllMovement(), Times.Never);
        }

        [Fact]
        public void EnsureInCombatRange_FacesTargetWhenInRangeButNotFacing()
        {
            var player = new Mock<IWoWLocalPlayer>();
            player.SetupGet(x => x.MapId).Returns(1U);
            player.SetupGet(x => x.Position).Returns(new Position(0, 0, 0));
            player.Setup(x => x.IsFacing(It.IsAny<IWoWUnit>())).Returns(false);

            var objectManager = new Mock<IObjectManager>();
            objectManager.SetupGet(x => x.Player).Returns(player.Object);

            var target = new Mock<IWoWUnit>();
            target.SetupGet(x => x.Position).Returns(new Position(10, 0, 0));

            var pathfindingClient = new MovementTestPathfindingClient(Array.Empty<Position>());

            var service = new TargetPositioningService(objectManager.Object, pathfindingClient);

            var inRange = service.EnsureInCombatRange(target.Object);

            Assert.False(inRange);
            objectManager.Verify(x => x.Face(It.Is<Position>(p => p.X == 10 && p.Y == 0 && p.Z == 0)), Times.Once);
            objectManager.Verify(x => x.StopAllMovement(), Times.Never);
        }

        [Fact]
        public void EnsureInCombatRange_StopsMovementWhenAlreadyFacing()
        {
            var player = new Mock<IWoWLocalPlayer>();
            player.SetupGet(x => x.MapId).Returns(1U);
            player.SetupGet(x => x.Position).Returns(new Position(0, 0, 0));
            player.Setup(x => x.IsFacing(It.IsAny<IWoWUnit>())).Returns(true);

            var objectManager = new Mock<IObjectManager>();
            objectManager.SetupGet(x => x.Player).Returns(player.Object);

            var target = new Mock<IWoWUnit>();
            target.SetupGet(x => x.Position).Returns(new Position(10, 0, 0));

            var pathfindingClient = new MovementTestPathfindingClient(Array.Empty<Position>());

            var service = new TargetPositioningService(objectManager.Object, pathfindingClient);

            var inRange = service.EnsureInCombatRange(target.Object);

            Assert.True(inRange);
            objectManager.Verify(x => x.StopAllMovement(), Times.Once);
        }
    }

    public class BotRunnerServiceTests
    {
        [Fact]
        public void ResolveNextWaypoint_ReturnsNull_WhenNoWaypoints()
        {
            var pathfindingClient = new TestPathfindingClient(Array.Empty<Position>());
            var logMessages = new List<string>();

            var result = BotRunnerService.ResolveNextWaypoint(pathfindingClient.GetPath(0, Origin, Origin), logMessages.Add);

            Assert.Null(result);
            Assert.Contains(logMessages, message => message.Contains("no waypoints", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ResolveNextWaypoint_UsesFirstWaypoint_WhenOnlyOneExists()
        {
            var waypoint = new Position(1, 2, 3);
            var pathfindingClient = new TestPathfindingClient(new[] { waypoint });
            var logMessages = new List<string>();

            var result = BotRunnerService.ResolveNextWaypoint(pathfindingClient.GetPath(0, Origin, waypoint), logMessages.Add);

            Assert.Same(waypoint, result);
            Assert.Contains(logMessages, message => message.Contains("single waypoint", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ResolveNextWaypoint_UsesSecondWaypoint_WhenMultipleExist()
        {
            var waypoint0 = new Position(0, 0, 0);
            var waypoint1 = new Position(1, 1, 1);
            var pathfindingClient = new TestPathfindingClient(new[] { waypoint0, waypoint1 });
            var logMessages = new List<string>();

            var result = BotRunnerService.ResolveNextWaypoint(pathfindingClient.GetPath(0, waypoint0, waypoint1), logMessages.Add);

            Assert.Same(waypoint1, result);
            Assert.Empty(logMessages);
        }

        private static Position Origin { get; } = new(0, 0, 0);
    }

    internal static class MockHelpers
    {
        public static Mock<IAgentFactory> CreateAgentFactory(
            Mock<ITargetingNetworkClientComponent>? targetingAgent = null,
            Mock<IAttackNetworkClientComponent>? attackAgent = null,
            Mock<ILootingNetworkClientComponent>? lootingAgent = null)
        {
            targetingAgent ??= new Mock<ITargetingNetworkClientComponent>(MockBehavior.Loose);
            attackAgent ??= new Mock<IAttackNetworkClientComponent>(MockBehavior.Loose);
            lootingAgent ??= new Mock<ILootingNetworkClientComponent>(MockBehavior.Loose);

            var agentFactory = new Mock<IAgentFactory>(MockBehavior.Strict);
            agentFactory.SetupGet(x => x.TargetingAgent).Returns(targetingAgent.Object);
            agentFactory.SetupGet(x => x.AttackAgent).Returns(attackAgent.Object);
            agentFactory.SetupGet(x => x.LootingAgent).Returns(lootingAgent.Object);

            agentFactory.SetupGet(x => x.QuestAgent).Returns(Mock.Of<IQuestNetworkClientComponent>());
            agentFactory.SetupGet(x => x.GameObjectAgent).Returns(Mock.Of<IGameObjectNetworkClientComponent>());
            agentFactory.SetupGet(x => x.VendorAgent).Returns(Mock.Of<IVendorNetworkClientComponent>());
            agentFactory.SetupGet(x => x.FlightMasterAgent).Returns(Mock.Of<IFlightMasterNetworkClientComponent>());
            agentFactory.SetupGet(x => x.DeadActorAgent).Returns(Mock.Of<IDeadActorNetworkClientComponent>());
            agentFactory.SetupGet(x => x.InventoryAgent).Returns(Mock.Of<IInventoryNetworkClientComponent>());
            agentFactory.SetupGet(x => x.ItemUseAgent).Returns(Mock.Of<IItemUseNetworkClientComponent>());
            agentFactory.SetupGet(x => x.EquipmentAgent).Returns(Mock.Of<IEquipmentNetworkClientComponent>());
            agentFactory.SetupGet(x => x.SpellCastingAgent).Returns(Mock.Of<ISpellCastingNetworkClientComponent>());
            agentFactory.SetupGet(x => x.AuctionHouseAgent).Returns(Mock.Of<IAuctionHouseNetworkClientComponent>());
            agentFactory.SetupGet(x => x.BankAgent).Returns(Mock.Of<IBankNetworkClientComponent>());
            agentFactory.SetupGet(x => x.MailAgent).Returns(Mock.Of<IMailNetworkClientComponent>());
            agentFactory.SetupGet(x => x.GuildAgent).Returns(Mock.Of<IGuildNetworkClientComponent>());
            agentFactory.SetupGet(x => x.PartyAgent).Returns(Mock.Of<IPartyNetworkClientComponent>());
            agentFactory.SetupGet(x => x.TrainerAgent).Returns(Mock.Of<ITrainerNetworkClientComponent>());
            agentFactory.SetupGet(x => x.TalentAgent).Returns(Mock.Of<ITalentNetworkClientComponent>());
            agentFactory.SetupGet(x => x.ProfessionsAgent).Returns(Mock.Of<IProfessionsNetworkClientComponent>());
            agentFactory.SetupGet(x => x.EmoteAgent).Returns(Mock.Of<IEmoteNetworkClientComponent>());
            agentFactory.SetupGet(x => x.ChatAgent).Returns(Mock.Of<IChatNetworkClientComponent>());
            agentFactory.SetupGet(x => x.GossipAgent).Returns(Mock.Of<IGossipNetworkClientComponent>());
            agentFactory.SetupGet(x => x.FriendAgent).Returns(Mock.Of<IFriendNetworkClientComponent>());
            agentFactory.SetupGet(x => x.IgnoreAgent).Returns(Mock.Of<IIgnoreNetworkClientComponent>());
            agentFactory.SetupGet(x => x.TradeAgent).Returns(Mock.Of<ITradeNetworkClientComponent>());

            return agentFactory;
        }
    }

    internal sealed class MovementTestPathfindingClient(Position[] path) : PathfindingClient
    {
        private readonly Position[] _path = path;

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false) => _path;
    }

    internal sealed class TestPathfindingClient(Position[] path) : PathfindingClient
    {
        private readonly Position[] _path = path;

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false) => _path;
    }
}
