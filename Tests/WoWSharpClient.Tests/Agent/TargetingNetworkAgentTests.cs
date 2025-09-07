using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.Agent;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class TargetingNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<TargetingNetworkAgent>> _mockLogger;
        private readonly TargetingNetworkAgent _targetingAgent;

        public TargetingNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<TargetingNetworkAgent>>();
            _targetingAgent = new TargetingNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void CurrentTarget_InitiallyNull()
        {
            // Arrange & Act
            var currentTarget = _targetingAgent.CurrentTarget;

            // Assert
            Assert.Null(currentTarget);
        }

        [Fact]
        public void HasTarget_InitiallyFalse()
        {
            // Arrange & Act
            var hasTarget = _targetingAgent.HasTarget();

            // Assert
            Assert.False(hasTarget);
        }

        [Fact]
        public void IsTargeted_WithNoTarget_ReturnsFalse()
        {
            // Arrange
            ulong testGuid = 0x12345678;

            // Act
            var isTargeted = _targetingAgent.IsTargeted(testGuid);

            // Assert
            Assert.False(isTargeted);
        }

        [Fact]
        public async Task SetTargetAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_SET_SELECTION,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == targetGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SetTargetAsync_UpdatesCurrentTarget()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            Assert.Equal(targetGuid, _targetingAgent.CurrentTarget);
            Assert.True(_targetingAgent.HasTarget());
            Assert.True(_targetingAgent.IsTargeted(targetGuid));
        }

        [Fact]
        public async Task SetTargetAsync_FiresTargetChangedEvent()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            ulong? eventTargetGuid = null;
            bool eventFired = false;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _targetingAgent.TargetChanged += (newTarget) =>
            {
                eventTargetGuid = newTarget;
                eventFired = true;
            };

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(targetGuid, eventTargetGuid);
        }

        [Fact]
        public async Task SetTargetAsync_SameTarget_DoesNotFireEvent()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            int eventCount = 0;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _targetingAgent.TargetChanged += (newTarget) => eventCount++;

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);
            await _targetingAgent.SetTargetAsync(targetGuid); // Same target again

            // Assert
            Assert.Equal(1, eventCount); // Event should only fire once
        }

        [Fact]
        public async Task ClearTargetAsync_SendsZeroGuid()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.ClearTargetAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_SET_SELECTION,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == 0),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ClearTargetAsync_UpdatesStateCorrectly()
        {
            // Arrange
            ulong targetGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set a target first
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Act
            await _targetingAgent.ClearTargetAsync();

            // Assert
            Assert.Null(_targetingAgent.CurrentTarget);
            Assert.False(_targetingAgent.HasTarget());
            Assert.False(_targetingAgent.IsTargeted(targetGuid));
        }

        [Fact]
        public async Task AssistAsync_SendsCorrectPacketSequence()
        {
            // Arrange
            ulong playerGuid = 0x87654321;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.AssistAsync(playerGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_SET_SELECTION,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == playerGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            Assert.Equal(playerGuid, _targetingAgent.CurrentTarget);
        }

        [Fact]
        public void UpdateCurrentTarget_UpdatesStateAndFiresEvent()
        {
            // Arrange
            ulong newTargetGuid = 0x11111111;
            ulong? eventTargetGuid = null;
            bool eventFired = false;

            _targetingAgent.TargetChanged += (newTarget) =>
            {
                eventTargetGuid = newTarget;
                eventFired = true;
            };

            // Act
            _targetingAgent.UpdateCurrentTarget(newTargetGuid);

            // Assert
            Assert.Equal(newTargetGuid, _targetingAgent.CurrentTarget);
            Assert.True(eventFired);
            Assert.Equal(newTargetGuid, eventTargetGuid);
        }

        [Fact]
        public void UpdateCurrentTarget_WithZero_ClearsTarget()
        {
            // Arrange
            ulong initialTarget = 0x12345678;
            _targetingAgent.UpdateCurrentTarget(initialTarget); // Set initial target

            ulong? eventTargetGuid = null;
            bool eventFired = false;

            _targetingAgent.TargetChanged += (newTarget) =>
            {
                eventTargetGuid = newTarget;
                eventFired = true;
            };

            // Act
            _targetingAgent.UpdateCurrentTarget(0);

            // Assert
            Assert.Null(_targetingAgent.CurrentTarget);
            Assert.True(eventFired);
            Assert.Null(eventTargetGuid);
        }

        [Fact]
        public async Task SetTargetAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _targetingAgent.SetTargetAsync(targetGuid));
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData(0x12345678, true)]
        [InlineData(0x87654321, false)]
        public async Task IsTargeted_VariousGuids_ReturnsCorrectResult(ulong testGuid, bool expected)
        {
            // Arrange
            ulong targetGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _targetingAgent.SetTargetAsync(targetGuid);

            // Act
            var result = _targetingAgent.IsTargeted(testGuid);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}