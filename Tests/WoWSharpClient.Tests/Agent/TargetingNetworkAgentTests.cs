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
        public async Task SetTargetAsync_InvokesTargetChangedCallback()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            ulong? callbackTargetGuid = null;
            bool callbackInvoked = false;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _targetingAgent.SetTargetChangedCallback((newTarget) =>
            {
                callbackTargetGuid = newTarget;
                callbackInvoked = true;
            });

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal(targetGuid, callbackTargetGuid);
        }

        [Fact]
        public async Task SetTargetAsync_SameTarget_DoesNotInvokeCallback()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            int callbackCount = 0;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _targetingAgent.SetTargetChangedCallback((newTarget) => callbackCount++);

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);
            await _targetingAgent.SetTargetAsync(targetGuid); // Same target again

            // Assert
            Assert.Equal(1, callbackCount); // Callback should only be invoked once
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
        public void UpdateCurrentTarget_UpdatesStateAndInvokesCallback()
        {
            // Arrange
            ulong newTargetGuid = 0x11111111;
            ulong? callbackTargetGuid = null;
            bool callbackInvoked = false;

            _targetingAgent.SetTargetChangedCallback((newTarget) =>
            {
                callbackTargetGuid = newTarget;
                callbackInvoked = true;
            });

            // Act
            _targetingAgent.UpdateCurrentTarget(newTargetGuid);

            // Assert
            Assert.Equal(newTargetGuid, _targetingAgent.CurrentTarget);
            Assert.True(callbackInvoked);
            Assert.Equal(newTargetGuid, callbackTargetGuid);
        }

        [Fact]
        public void UpdateCurrentTarget_WithZero_ClearsTarget()
        {
            // Arrange
            ulong initialTarget = 0x12345678;
            _targetingAgent.UpdateCurrentTarget(initialTarget); // Set initial target

            ulong? callbackTargetGuid = null;
            bool callbackInvoked = false;

            _targetingAgent.SetTargetChangedCallback((newTarget) =>
            {
                callbackTargetGuid = newTarget;
                callbackInvoked = true;
            });

            // Act
            _targetingAgent.UpdateCurrentTarget(0);

            // Assert
            Assert.Null(_targetingAgent.CurrentTarget);
            Assert.True(callbackInvoked);
            Assert.Null(callbackTargetGuid);
        }

        [Fact]
        public void SetTargetChangedCallback_ReplacesExistingCallback()
        {
            // Arrange
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;

            _targetingAgent.SetTargetChangedCallback((target) => firstCallbackCount++);
            _targetingAgent.SetTargetChangedCallback((target) => secondCallbackCount++);

            // Act
            _targetingAgent.UpdateCurrentTarget(0x12345678);

            // Assert
            Assert.Equal(0, firstCallbackCount); // First callback should not be called
            Assert.Equal(1, secondCallbackCount); // Second callback should be called
        }

        [Fact]
        public void SetTargetChangedCallback_WithNull_ClearsCallback()
        {
            // Arrange
            int callbackCount = 0;
            _targetingAgent.SetTargetChangedCallback((target) => callbackCount++);
            _targetingAgent.SetTargetChangedCallback(null); // Clear callback

            // Act
            _targetingAgent.UpdateCurrentTarget(0x12345678);

            // Assert
            Assert.Equal(0, callbackCount); // No callback should be invoked
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