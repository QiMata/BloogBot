using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class TargetingNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<TargetingNetworkClientComponent>> _mockLogger;
        private readonly TargetingNetworkClientComponent _targetingAgent;

        public TargetingNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<TargetingNetworkClientComponent>>();
            _targetingAgent = new TargetingNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            Assert.Equal(targetGuid, _targetingAgent.CurrentTarget);
            Assert.True(_targetingAgent.HasTarget());
            Assert.True(_targetingAgent.IsTargeted(targetGuid));
        }

        [Fact]
        public async Task SetTargetAsync_RaisesTargetChangesObservable()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            TargetingData? receivedData = null;
            bool observableTriggered = false;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe to the reactive observable
            _targetingAgent.TargetChanges.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(targetGuid, receivedData.CurrentTarget);
            Assert.Null(receivedData.PreviousTarget);
        }

        [Fact]
        public async Task SetTargetAsync_SameTarget_DoesNotTriggerObservable()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            int observableCount = 0;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe to the reactive observable
            _targetingAgent.TargetChanges.Subscribe(data => observableCount++);

            // Act
            await _targetingAgent.SetTargetAsync(targetGuid);
            await _targetingAgent.SetTargetAsync(targetGuid); // Same target again

            // Assert
            Assert.Equal(1, observableCount); // Observable should only be triggered once
        }

        [Fact]
        public async Task ClearTargetAsync_SendsZeroGuid()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.ClearTargetAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _targetingAgent.AssistAsync(playerGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_SET_SELECTION,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == playerGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            Assert.Equal(playerGuid, _targetingAgent.CurrentTarget);
        }

        [Fact]
        public void HandleTargetChanged_UpdatesStateAndRaisesObservable()
        {
            // Arrange
            ulong newTargetGuid = 0x11111111;
            TargetingData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the reactive observable
            _targetingAgent.TargetChanges.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _targetingAgent.HandleTargetChanged(newTargetGuid);

            // Assert
            Assert.Equal(newTargetGuid, _targetingAgent.CurrentTarget);
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(newTargetGuid, receivedData.CurrentTarget);
        }

        [Fact]
        public void HandleTargetChanged_WithNull_ClearsTarget()
        {
            // Arrange
            ulong initialTarget = 0x12345678;
            _targetingAgent.HandleTargetChanged(initialTarget); // Set initial target

            TargetingData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the reactive observable
            _targetingAgent.TargetChanges.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _targetingAgent.HandleTargetChanged(null);

            // Assert
            Assert.Null(_targetingAgent.CurrentTarget);
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Null(receivedData.CurrentTarget);
            Assert.Equal(initialTarget, receivedData.PreviousTarget);
        }

        [Fact]
        public void TargetChanges_Observable_WorksCorrectly()
        {
            // Arrange
            var receivedData = new List<TargetingData>();
            
            // Subscribe to the observable
            _targetingAgent.TargetChanges.Subscribe(data => receivedData.Add(data));

            // Act
            _targetingAgent.HandleTargetChanged(0x12345678);
            _targetingAgent.HandleTargetChanged(0x87654321);
            _targetingAgent.HandleTargetChanged(null);

            // Assert
            Assert.Equal(3, receivedData.Count);
            
            // First target change
            Assert.Null(receivedData[0].PreviousTarget);
            Assert.Equal((ulong)0x12345678, receivedData[0].CurrentTarget);

            // Second target change
            Assert.Equal((ulong)0x12345678, receivedData[1].PreviousTarget);
            Assert.Equal((ulong)0x87654321, receivedData[1].CurrentTarget);

            // Clear target
            Assert.Equal((ulong)0x87654321, receivedData[2].PreviousTarget);
            Assert.Null(receivedData[2].CurrentTarget);
        }

        [Fact]
        public async Task SetTargetAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _targetingAgent.SetTargetAsync(targetGuid);

            // Act
            var result = _targetingAgent.IsTargeted(testGuid);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void HandleTargetingError_RaisesTargetingErrorObservable()
        {
            // Arrange
            string errorMessage = "Target not in range";
            ulong targetGuid = 0x12345678;
            TargetingErrorData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the observable
            _targetingAgent.TargetingErrors.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _targetingAgent.HandleTargetingError(errorMessage, targetGuid);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(errorMessage, receivedData.ErrorMessage);
            Assert.Equal(targetGuid, receivedData.TargetGuid);
        }

        [Fact]
        public async Task AssistOperations_Observable_WorksCorrectly()
        {
            // Arrange
            ulong playerGuid = 0x87654321;
            AssistData? receivedData = null;
            bool observableTriggered = false;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe to the observable
            _targetingAgent.AssistOperations.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act - Call AssistAsync instead of HandleTargetChanged to properly test assist operations
            await _targetingAgent.AssistAsync(playerGuid);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(playerGuid, receivedData.PlayerGuid);
            Assert.Equal(playerGuid, receivedData.AssistTarget); // After assist, we're targeting the player we assisted
        }

        [Fact]
        public void IsOperationInProgress_InitiallyFalse()
        {
            // Arrange & Act
            var isInProgress = _targetingAgent.IsOperationInProgress;

            // Assert
            Assert.False(isInProgress);
        }

        [Fact]
        public void LastOperationTime_InitiallyNull()
        {
            // Arrange & Act
            var lastOperationTime = _targetingAgent.LastOperationTime;

            // Assert
            Assert.Null(lastOperationTime);
        }
    }
}