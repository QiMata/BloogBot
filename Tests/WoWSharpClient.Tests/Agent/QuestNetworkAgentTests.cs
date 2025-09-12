using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class QuestNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<QuestNetworkClientComponent>> _mockLogger;
        private readonly QuestNetworkClientComponent _questAgent;

        public QuestNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<QuestNetworkClientComponent>>();
            _questAgent = new QuestNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task QueryQuestGiverStatusAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong npcGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.QueryQuestGiverStatusAsync(npcGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_STATUS_QUERY,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == npcGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task HelloQuestGiverAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong questGiverGuid = 0x87654321;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.HelloQuestGiverAsync(questGiverGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_HELLO,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == questGiverGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task QueryQuestAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong questGiverGuid = 0x12345678;
            uint questId = 1234;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.QueryQuestAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_QUERY_QUEST,
                    It.Is<byte[]>(payload => 
                        payload.Length == 12 && 
                        BitConverter.ToUInt64(payload, 0) == questGiverGuid &&
                        BitConverter.ToUInt32(payload, 8) == questId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task AcceptQuestAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong questGiverGuid = 0x12345678;
            uint questId = 5678;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.AcceptQuestAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_ACCEPT_QUEST,
                    It.Is<byte[]>(payload => 
                        payload.Length == 12 && 
                        BitConverter.ToUInt64(payload, 0) == questGiverGuid &&
                        BitConverter.ToUInt32(payload, 8) == questId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task CompleteQuestAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong questGiverGuid = 0x12345678;
            uint questId = 9999;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.CompleteQuestAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_COMPLETE_QUEST,
                    It.Is<byte[]>(payload => 
                        payload.Length == 12 && 
                        BitConverter.ToUInt64(payload, 0) == questGiverGuid &&
                        BitConverter.ToUInt32(payload, 8) == questId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task RequestQuestRewardAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong questGiverGuid = 0x12345678;
            uint questId = 1111;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.RequestQuestRewardAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_REQUEST_REWARD,
                    It.Is<byte[]>(payload => 
                        payload.Length == 12 && 
                        BitConverter.ToUInt64(payload, 0) == questGiverGuid &&
                        BitConverter.ToUInt32(payload, 8) == questId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ChooseQuestRewardAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong questGiverGuid = 0x12345678;
            uint questId = 2222;
            uint rewardIndex = 1;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.ChooseQuestRewardAsync(questGiverGuid, questId, rewardIndex);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_CHOOSE_REWARD,
                    It.Is<byte[]>(payload => 
                        payload.Length == 16 && 
                        BitConverter.ToUInt64(payload, 0) == questGiverGuid &&
                        BitConverter.ToUInt32(payload, 8) == questId &&
                        BitConverter.ToUInt32(payload, 12) == rewardIndex),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task CancelQuestInteractionAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.CancelQuestInteractionAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTGIVER_CANCEL,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task RemoveQuestFromLogAsync_SendsCorrectPacket()
        {
            // Arrange
            byte questLogSlot = 3;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.RemoveQuestFromLogAsync(questLogSlot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_QUESTLOG_REMOVE_QUEST,
                    It.Is<byte[]>(payload => payload.Length == 1 && payload[0] == questLogSlot),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task PushQuestToPartyAsync_SendsCorrectPacket()
        {
            // Arrange
            uint questId = 4444;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.PushQuestToPartyAsync(questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_PUSHQUESTTOPARTY,
                    It.Is<byte[]>(payload => payload.Length == 4 && BitConverter.ToUInt32(payload, 0) == questId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void ReportQuestEvent_QuestOffered_UsesReactiveObservable()
        {
            // Arrange
            uint questId = 1234;
            var questData = (QuestData?)null;
            var eventFired = false;

            // Use reactive observable instead of event
            var subscription = _questAgent.QuestOffered.Subscribe(data =>
            {
                questData = data;
                eventFired = true;
            });

            // Act
            _questAgent.HandleQuestOperation(questId, "Test Quest", 0x123456789UL, QuestOperationType.Offered);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(questData);
            Assert.Equal(questId, questData.QuestId);
            Assert.Equal("Test Quest", questData.QuestTitle);
            Assert.Equal(QuestOperationType.Offered, questData.OperationType);
            
            subscription.Dispose();
        }

        [Fact]
        public void ReportQuestEvent_QuestAccepted_UsesReactiveObservable()
        {
            // Arrange
            uint questId = 5678;
            var questData = (QuestData?)null;
            var eventFired = false;

            // Use reactive observable instead of event
            var subscription = _questAgent.QuestAccepted.Subscribe(data =>
            {
                questData = data;
                eventFired = true;
            });

            // Act
            _questAgent.HandleQuestOperation(questId, "Test Quest", 0x123456789UL, QuestOperationType.Accepted);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(questData);
            Assert.Equal(questId, questData.QuestId);
            Assert.Equal("Test Quest", questData.QuestTitle);
            Assert.Equal(QuestOperationType.Accepted, questData.OperationType);
            
            subscription.Dispose();
        }

        [Fact]
        public void ReportQuestEvent_QuestCompleted_UsesReactiveObservable()
        {
            // Arrange
            uint questId = 9999;
            var questData = (QuestData?)null;
            var eventFired = false;

            // Use reactive observable instead of event
            var subscription = _questAgent.QuestCompleted.Subscribe(data =>
            {
                questData = data;
                eventFired = true;
            });

            // Act
            _questAgent.HandleQuestOperation(questId, "Test Quest", 0x123456789UL, QuestOperationType.Completed);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(questData);
            Assert.Equal(questId, questData.QuestId);
            Assert.Equal("Test Quest", questData.QuestTitle);
            Assert.Equal(QuestOperationType.Completed, questData.OperationType);
            
            subscription.Dispose();
        }

        [Fact]
        public void HandleQuestProgress_FiresQuestProgressObservable()
        {
            // Arrange
            uint questId = 1111;
            string questTitle = "Test Progress Quest";
            string progressText = "Kill 5/10 orcs";
            uint completedObjectives = 5;
            uint totalObjectives = 10;
            var progressData = (QuestProgressData?)null;
            var eventFired = false;

            // Use reactive observable
            var subscription = _questAgent.QuestProgress.Subscribe(data =>
            {
                progressData = data;
                eventFired = true;
            });

            // Act
            _questAgent.HandleQuestProgress(questId, questTitle, progressText, completedObjectives, totalObjectives);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(progressData);
            Assert.Equal(questId, progressData.QuestId);
            Assert.Equal(questTitle, progressData.QuestTitle);
            Assert.Equal(progressText, progressData.ProgressText);
            Assert.Equal(completedObjectives, progressData.CompletedObjectives);
            Assert.Equal(totalObjectives, progressData.TotalObjectives);
            
            subscription.Dispose();
        }

        [Fact]
        public void HandleQuestError_FiresQuestErrorObservable()
        {
            // Arrange
            uint questId = 2222;
            string errorMessage = "Quest error occurred";
            ulong questGiverGuid = 0x123456789UL;
            var errorData = (QuestErrorData?)null;
            var eventFired = false;

            // Use reactive observable
            var subscription = _questAgent.QuestErrors.Subscribe(data =>
            {
                errorData = data;
                eventFired = true;
            });

            // Act
            _questAgent.HandleQuestError(errorMessage, questId, questGiverGuid);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(errorData);
            Assert.Equal(errorMessage, errorData.ErrorMessage);
            Assert.Equal(questId, errorData.QuestId);
            Assert.Equal(questGiverGuid, errorData.QuestGiverGuid);
            
            subscription.Dispose();
        }

        [Fact]
        public async Task QueryQuestGiverStatusAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong npcGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _questAgent.QueryQuestGiverStatusAsync(npcGuid));
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData(QuestOperationType.Offered)]
        [InlineData(QuestOperationType.Accepted)]
        [InlineData(QuestOperationType.Completed)]
        [InlineData(QuestOperationType.Abandoned)]
        public void HandleQuestOperation_VariousOperationTypes_HandledCorrectly(QuestOperationType operationType)
        {
            // Arrange
            uint questId = 1234;
            string questTitle = "Test Quest";
            ulong questGiverGuid = 0x123456789UL;
            var eventFired = false;

            // Subscribe to the main quest operations observable
            var subscription = _questAgent.QuestOperations.Subscribe(data => eventFired = true);

            // Act
            _questAgent.HandleQuestOperation(questId, questTitle, questGiverGuid, operationType);

            // Assert
            Assert.True(eventFired);
            
            subscription.Dispose();
        }

        [Fact]
        public async Task HelloQuestGiverAsync_ValidGuid_SendsPacketAndFiresQuestOfferedEvent()
        {
            // Arrange
            var questGiverGuid = 0x123456789UL;
            var questId = 123U;
            var eventFired = false;

            // Use observable instead of event
            var subscription = _questAgent.QuestOffered.Subscribe(questData =>
            {
                eventFired = true;
                Assert.Equal(questId, questData.QuestId);
            });

            // Act
            await _questAgent.HelloQuestGiverAsync(questGiverGuid);

            // Simulate server response that would fire the event
            _questAgent.HandleQuestOperation(questId, "Test Quest", questGiverGuid, QuestOperationType.Offered);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_QUESTGIVER_HELLO,
                It.Is<byte[]>(data => BitConverter.ToUInt64(data, 0) == questGiverGuid),
                It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.True(eventFired);
            subscription.Dispose();
        }

        [Fact]
        public async Task AcceptQuestAsync_ValidQuestIdAndGuid_SendsPacketAndFiresQuestAcceptedEvent()
        {
            // Arrange
            var questGiverGuid = 0x123456789UL;
            var questId = 123U;
            var eventFired = false;

            // Use observable instead of event
            var subscription = _questAgent.QuestAccepted.Subscribe(questData =>
            {
                eventFired = true;
                Assert.Equal(questId, questData.QuestId);
            });

            // Act
            await _questAgent.AcceptQuestAsync(questGiverGuid, questId);

            // Simulate server response that would fire the event
            _questAgent.HandleQuestOperation(questId, "Test Quest", questGiverGuid, QuestOperationType.Accepted);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_QUESTGIVER_ACCEPT_QUEST,
                It.Is<byte[]>(data => 
                    BitConverter.ToUInt64(data, 0) == questGiverGuid &&
                    BitConverter.ToUInt32(data, 8) == questId),
                It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.True(eventFired);
            subscription.Dispose();
        }

        [Fact]
        public void QuestAgent_Dispose_CompletesAllObservables()
        {
            // Arrange
            var questOperationsCompleted = false;
            var questProgressCompleted = false;
            var questRewardsCompleted = false;
            var questErrorsCompleted = false;

            // Subscribe to observables and track completion
            _questAgent.QuestOperations.Subscribe(
                onNext: _ => { },
                onCompleted: () => questOperationsCompleted = true);

            _questAgent.QuestProgress.Subscribe(
                onNext: _ => { },
                onCompleted: () => questProgressCompleted = true);

            _questAgent.QuestRewards.Subscribe(
                onNext: _ => { },
                onCompleted: () => questRewardsCompleted = true);

            _questAgent.QuestErrors.Subscribe(
                onNext: _ => { },
                onCompleted: () => questErrorsCompleted = true);

            // Act
            _questAgent.Dispose();

            // Assert
            Assert.True(questOperationsCompleted);
            Assert.True(questProgressCompleted);
            Assert.True(questRewardsCompleted);
            Assert.True(questErrorsCompleted);
        }
    }
}