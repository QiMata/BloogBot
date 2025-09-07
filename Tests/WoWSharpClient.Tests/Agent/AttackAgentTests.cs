using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Agent;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class AttackAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<AttackAgent>> _mockLogger;
        private readonly AttackAgent _attackAgent;

        public AttackAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<AttackAgent>>();
            _attackAgent = new AttackAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void IsAttacking_InitiallyFalse()
        {
            // Arrange & Act
            var isAttacking = _attackAgent.IsAttacking;

            // Assert
            Assert.False(isAttacking);
        }

        [Fact]
        public async Task StartAttackAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackAgent.StartAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_ATTACKSWING,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task StopAttackAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackAgent.StopAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_ATTACKSTOP,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ToggleAttackAsync_WhenNotAttacking_StartsAttack()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackAgent.ToggleAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_ATTACKSWING, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ToggleAttackAsync_WhenAttacking_StopsAttack()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set attacking state to true via UpdateAttackingState
            _attackAgent.UpdateAttackingState(true, 0x12345678, 0x87654321);

            // Act
            await _attackAgent.ToggleAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_ATTACKSTOP, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task AttackTargetAsync_CallsTargetingAgentThenAttacks()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            var mockTargetingAgent = new Mock<ITargetingAgent>();
            
            mockTargetingAgent
                .Setup(x => x.SetTargetAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackAgent.AttackTargetAsync(targetGuid, mockTargetingAgent.Object);

            // Assert
            mockTargetingAgent.Verify(
                x => x.SetTargetAsync(targetGuid, It.IsAny<CancellationToken>()),
                Times.Once
            );

            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_ATTACKSWING, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task AttackTargetAsync_WithNullTargetingAgent_ThrowsArgumentNullException()
        {
            // Arrange
            ulong targetGuid = 0x12345678;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _attackAgent.AttackTargetAsync(targetGuid, null!));
        }

        [Fact]
        public void UpdateAttackingState_FromFalseToTrue_FiresAttackStartedEvent()
        {
            // Arrange
            ulong attackerGuid = 0x12345678;
            ulong victimGuid = 0x87654321;
            ulong? eventVictimGuid = null;
            bool eventFired = false;

            _attackAgent.AttackStarted += (victim) =>
            {
                eventVictimGuid = victim;
                eventFired = true;
            };

            // Act
            _attackAgent.UpdateAttackingState(true, attackerGuid, victimGuid);

            // Assert
            Assert.True(_attackAgent.IsAttacking);
            Assert.True(eventFired);
            Assert.Equal(victimGuid, eventVictimGuid);
        }

        [Fact]
        public void UpdateAttackingState_FromTrueToFalse_FiresAttackStoppedEvent()
        {
            // Arrange
            bool eventFired = false;

            // Set initial attacking state
            _attackAgent.UpdateAttackingState(true, 0x12345678, 0x87654321);

            _attackAgent.AttackStopped += () =>
            {
                eventFired = true;
            };

            // Act
            _attackAgent.UpdateAttackingState(false);

            // Assert
            Assert.False(_attackAgent.IsAttacking);
            Assert.True(eventFired);
        }

        [Fact]
        public void UpdateAttackingState_SameState_DoesNotFireEvent()
        {
            // Arrange
            int eventCount = 0;

            _attackAgent.AttackStarted += (victim) => eventCount++;
            _attackAgent.AttackStopped += () => eventCount++;

            // Act
            _attackAgent.UpdateAttackingState(false); // Initial state is already false
            _attackAgent.UpdateAttackingState(false); // Same state again

            // Assert
            Assert.Equal(0, eventCount); // No events should fire
        }

        [Fact]
        public void ReportAttackError_FiresAttackErrorEvent()
        {
            // Arrange
            string errorMessage = "Target not in range";
            string? eventErrorMessage = null;
            bool eventFired = false;

            _attackAgent.AttackError += (error) =>
            {
                eventErrorMessage = error;
                eventFired = true;
            };

            // Act
            _attackAgent.ReportAttackError(errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(errorMessage, eventErrorMessage);
        }

        [Fact]
        public async Task StartAttackAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _attackAgent.StartAttackAsync());
            Assert.Equal("Network error", exception.Message);
        }

        [Fact]
        public async Task StopAttackAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _attackAgent.StopAttackAsync());
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData(true, false)] // If attacking, toggle should stop
        [InlineData(false, true)] // If not attacking, toggle should start
        public async Task ToggleAttackAsync_VariousStates_TogglesCorrectly(bool initialState, bool shouldStart)
        {
            // Arrange
            if (initialState)
            {
                _attackAgent.UpdateAttackingState(true, 0x12345678, 0x87654321);
            }

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackAgent.ToggleAttackAsync();

            // Assert
            if (shouldStart)
            {
                _mockWorldClient.Verify(
                    x => x.SendMovementAsync(Opcode.CMSG_ATTACKSWING, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                    Times.Once
                );
            }
            else
            {
                _mockWorldClient.Verify(
                    x => x.SendMovementAsync(Opcode.CMSG_ATTACKSTOP, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                    Times.Once
                );
            }
        }

        [Fact]
        public void UpdateAttackingState_WithoutVictimGuid_DoesNotFireAttackStarted()
        {
            // Arrange
            bool eventFired = false;
            _attackAgent.AttackStarted += (victim) => eventFired = true;

            // Act - Start attacking but without victim GUID
            _attackAgent.UpdateAttackingState(true);

            // Assert
            Assert.True(_attackAgent.IsAttacking);
            Assert.False(eventFired); // Event should not fire without victim GUID
        }
    }
}