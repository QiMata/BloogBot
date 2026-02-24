using BotRunner.Combat;
using GameData.Core.Interfaces;
using Moq;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BotRunner.Tests.Combat
{
    public class TargetEngagementServiceTests
    {
        private readonly Mock<IAgentFactory> _agentFactory;
        private readonly Mock<IAttackNetworkClientComponent> _attackAgent;
        private readonly Mock<ITargetingNetworkClientComponent> _targetingAgent;
        private readonly BotCombatState _combatState;
        private readonly TargetEngagementService _service;

        public TargetEngagementServiceTests()
        {
            _agentFactory = new Mock<IAgentFactory>();
            _attackAgent = new Mock<IAttackNetworkClientComponent>();
            _targetingAgent = new Mock<ITargetingNetworkClientComponent>();

            _agentFactory.Setup(a => a.AttackAgent).Returns(_attackAgent.Object);
            _agentFactory.Setup(a => a.TargetingAgent).Returns(_targetingAgent.Object);

            _combatState = new BotCombatState();
            _service = new TargetEngagementService(_agentFactory.Object, _combatState);
        }

        // ======== Constructor ========

        [Fact]
        public void Constructor_NullAgentFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TargetEngagementService(null!, _combatState));
        }

        [Fact]
        public void Constructor_NullCombatState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TargetEngagementService(_agentFactory.Object, null!));
        }

        // ======== CurrentTargetGuid ========

        [Fact]
        public void CurrentTargetGuid_InitiallyNull()
        {
            Assert.Null(_service.CurrentTargetGuid);
        }

        [Fact]
        public async Task CurrentTargetGuid_AfterEngage_ReturnsTargetGuid()
        {
            var target = CreateMockUnit(42);
            _targetingAgent.Setup(t => t.IsTargeted(42)).Returns(false);

            await _service.EngageAsync(target.Object, CancellationToken.None);

            Assert.Equal(42ul, _service.CurrentTargetGuid);
        }

        // ======== EngageAsync ========

        [Fact]
        public async Task EngageAsync_NullTarget_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.EngageAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task EngageAsync_NotTargeted_CallsAttackTarget()
        {
            var target = CreateMockUnit(42);
            _targetingAgent.Setup(t => t.IsTargeted(42)).Returns(false);

            await _service.EngageAsync(target.Object, CancellationToken.None);

            _attackAgent.Verify(a => a.AttackTargetAsync(42, _targetingAgent.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EngageAsync_AlreadyTargeted_NotAttacking_StartsAttack()
        {
            var target = CreateMockUnit(42);
            _targetingAgent.Setup(t => t.IsTargeted(42)).Returns(true);
            _attackAgent.Setup(a => a.IsAttacking).Returns(false);

            await _service.EngageAsync(target.Object, CancellationToken.None);

            _attackAgent.Verify(a => a.StartAttackAsync(It.IsAny<CancellationToken>()), Times.Once);
            _attackAgent.Verify(a => a.AttackTargetAsync(It.IsAny<ulong>(), It.IsAny<ITargetingNetworkClientComponent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EngageAsync_AlreadyTargetedAndAttacking_NoNewCalls()
        {
            var target = CreateMockUnit(42);
            _targetingAgent.Setup(t => t.IsTargeted(42)).Returns(true);
            _attackAgent.Setup(a => a.IsAttacking).Returns(true);

            await _service.EngageAsync(target.Object, CancellationToken.None);

            _attackAgent.Verify(a => a.AttackTargetAsync(It.IsAny<ulong>(), It.IsAny<ITargetingNetworkClientComponent>(), It.IsAny<CancellationToken>()), Times.Never);
            _attackAgent.Verify(a => a.StartAttackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EngageAsync_SetsCurrentTarget()
        {
            var target = CreateMockUnit(99);
            _targetingAgent.Setup(t => t.IsTargeted(99)).Returns(false);

            await _service.EngageAsync(target.Object, CancellationToken.None);

            Assert.Equal(99ul, _service.CurrentTargetGuid);
        }

        [Fact]
        public async Task EngageAsync_SwitchingTargets_UpdatesCurrentTarget()
        {
            var target1 = CreateMockUnit(1);
            var target2 = CreateMockUnit(2);
            _targetingAgent.Setup(t => t.IsTargeted(It.IsAny<ulong>())).Returns(false);

            await _service.EngageAsync(target1.Object, CancellationToken.None);
            Assert.Equal(1ul, _service.CurrentTargetGuid);

            await _service.EngageAsync(target2.Object, CancellationToken.None);
            Assert.Equal(2ul, _service.CurrentTargetGuid);
        }

        private static Mock<IWoWUnit> CreateMockUnit(ulong guid)
        {
            var mock = new Mock<IWoWUnit>();
            mock.Setup(u => u.Guid).Returns(guid);
            return mock;
        }
    }
}
