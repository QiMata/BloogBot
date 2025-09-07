using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Agent;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class QuestNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<QuestNetworkAgent>> _mockLogger;
        private readonly QuestNetworkAgent _questAgent;

        public QuestNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<QuestNetworkAgent>>();
            _questAgent = new QuestNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task QueryQuestGiverStatusAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong npcGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.QueryQuestGiverStatusAsync(npcGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.HelloQuestGiverAsync(questGiverGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.QueryQuestAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.AcceptQuestAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.CompleteQuestAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.RequestQuestRewardAsync(questGiverGuid, questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.ChooseQuestRewardAsync(questGiverGuid, questId, rewardIndex);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.CancelQuestInteractionAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.RemoveQuestFromLogAsync(questLogSlot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _questAgent.PushQuestToPartyAsync(questId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_PUSHQUESTTOPARTY,
                    It.Is<byte[]>(payload => payload.Length == 4 && BitConverter.ToUInt32(payload, 0) == questId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void ReportQuestEvent_QuestOffered_FiresQuestOfferedEvent()
        {
            // Arrange
            uint questId = 1234;
            uint? eventQuestId = null;
            bool eventFired = false;

            _questAgent.QuestOffered += (id) =>
            {
                eventQuestId = id;
                eventFired = true;
            };

            // Act
            _questAgent.ReportQuestEvent("offered", questId);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(questId, eventQuestId);
        }

        [Fact]
        public void ReportQuestEvent_QuestAccepted_FiresQuestAcceptedEvent()
        {
            // Arrange
            uint questId = 5678;
            uint? eventQuestId = null;
            bool eventFired = false;

            _questAgent.QuestAccepted += (id) =>
            {
                eventQuestId = id;
                eventFired = true;
            };

            // Act
            _questAgent.ReportQuestEvent("accepted", questId);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(questId, eventQuestId);
        }

        [Fact]
        public void ReportQuestEvent_QuestCompleted_FiresQuestCompletedEvent()
        {
            // Arrange
            uint questId = 9999;
            uint? eventQuestId = null;
            bool eventFired = false;

            _questAgent.QuestCompleted += (id) =>
            {
                eventQuestId = id;
                eventFired = true;
            };

            // Act
            _questAgent.ReportQuestEvent("completed", questId);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(questId, eventQuestId);
        }

        [Fact]
        public void ReportQuestEvent_QuestProgress_FiresQuestProgressUpdatedEvent()
        {
            // Arrange
            uint questId = 1111;
            string message = "Quest progress updated";
            uint? eventQuestId = null;
            string? eventMessage = null;
            bool eventFired = false;

            _questAgent.QuestProgressUpdated += (id, msg) =>
            {
                eventQuestId = id;
                eventMessage = msg;
                eventFired = true;
            };

            // Act
            _questAgent.ReportQuestEvent("progress", questId, message);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(questId, eventQuestId);
            Assert.Equal(message, eventMessage);
        }

        [Fact]
        public void ReportQuestEvent_QuestError_FiresQuestErrorEvent()
        {
            // Arrange
            uint questId = 2222;
            string errorMessage = "Quest error occurred";
            string? eventErrorMessage = null;
            bool eventFired = false;

            _questAgent.QuestError += (error) =>
            {
                eventErrorMessage = error;
                eventFired = true;
            };

            // Act
            _questAgent.ReportQuestEvent("error", questId, errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(errorMessage, eventErrorMessage);
        }

        [Fact]
        public async Task QueryQuestGiverStatusAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong npcGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _questAgent.QueryQuestGiverStatusAsync(npcGuid));
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData("offered")]
        [InlineData("accepted")]
        [InlineData("completed")]
        [InlineData("progress")]
        [InlineData("error")]
        public void ReportQuestEvent_VariousEventTypes_HandledCorrectly(string eventType)
        {
            // Arrange
            uint questId = 1234;
            string message = "Test message";
            bool eventFired = false;

            // Subscribe to all events
            _questAgent.QuestOffered += (id) => eventFired = true;
            _questAgent.QuestAccepted += (id) => eventFired = true;
            _questAgent.QuestCompleted += (id) => eventFired = true;
            _questAgent.QuestProgressUpdated += (id, msg) => eventFired = true;
            _questAgent.QuestError += (error) => eventFired = true;

            // Act
            _questAgent.ReportQuestEvent(eventType, questId, message);

            // Assert
            Assert.True(eventFired);
        }
    }
}