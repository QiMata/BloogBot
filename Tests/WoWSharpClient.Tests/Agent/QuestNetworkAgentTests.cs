using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;
using System.Reactive.Subjects;

namespace WoWSharpClient.Tests.Agent
{
    public class QuestNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<QuestNetworkClientComponent>> _mockLogger;
        private readonly QuestNetworkClientComponent _questAgent;

        // Subjects to simulate server opcode streams
        private readonly Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> _opcodeSubjects = new();

        public QuestNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<QuestNetworkClientComponent>>();

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockWorldClient
                .Setup(x => x.RegisterOpcodeHandler(It.IsAny<Opcode>()))
                .Returns((Opcode op) =>
                {
                    if (!_opcodeSubjects.TryGetValue(op, out var subject))
                    {
                        subject = new Subject<ReadOnlyMemory<byte>>();
                        _opcodeSubjects[op] = subject;
                    }
                    return subject;
                });

            _questAgent = new QuestNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        private void Emit(Opcode opcode, byte[] payload)
        {
            if (_opcodeSubjects.TryGetValue(opcode, out var subject))
            {
                subject.OnNext(payload);
            }
        }

        [Fact]
        public async Task QueryQuestGiverStatusAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong npcGuid = 0x12345678;

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
        public void QuestOffered_Emits_OnQuestDetails()
        {
            // Arrange
            uint questId = 1234;
            var questData = (QuestData?)null;
            var eventFired = false;

            var subscription = _questAgent.QuestOffered.Subscribe(data =>
            {
                questData = data;
                eventFired = true;
            });

            // Act: simulate server quest details packet
            // SMSG_QUESTGIVER_QUEST_DETAILS: questGiverGuid(8) + questId(4) + title\0 + details\0 + objectives\0
            var payload = new byte[15]; // 8 + 4 + 3 null terminators
            BitConverter.GetBytes((ulong)0xABCDEF).CopyTo(payload, 0); // questGiverGuid
            BitConverter.GetBytes(questId).CopyTo(payload, 8); // questId
            // bytes 12-14 are null terminators for title, details, objectives
            Emit(Opcode.SMSG_QUESTGIVER_QUEST_DETAILS, payload);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(questData);
            Assert.Equal(questId, questData!.QuestId);
            Assert.Equal(QuestOperationType.Offered, questData.OperationType);
            
            subscription.Dispose();
        }

        [Fact]
        public void QuestAccepted_Emits_OnConfirmAccept()
        {
            // Arrange
            uint questId = 5678;
            var questData = (QuestData?)null;
            var eventFired = false;

            var subscription = _questAgent.QuestAccepted.Subscribe(data =>
            {
                questData = data;
                eventFired = true;
            });

            // Act: simulate server accept confirm packet
            Emit(Opcode.SMSG_QUEST_CONFIRM_ACCEPT, BitConverter.GetBytes(questId));

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(questData);
            Assert.Equal(questId, questData!.QuestId);
            Assert.Equal(QuestOperationType.Accepted, questData.OperationType);
            
            subscription.Dispose();
        }

        [Fact]
        public void QuestCompleted_Emits_OnQuestComplete()
        {
            // Arrange
            uint questId = 9999;
            var questData = (QuestData?)null;
            var eventFired = false;

            var subscription = _questAgent.QuestCompleted.Subscribe(data =>
            {
                questData = data;
                eventFired = true;
            });

            // Act: simulate server quest complete packet
            Emit(Opcode.SMSG_QUESTGIVER_QUEST_COMPLETE, BitConverter.GetBytes(questId));

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(questData);
            Assert.Equal(questId, questData!.QuestId);
            Assert.Equal(QuestOperationType.Completed, questData.OperationType);
            
            subscription.Dispose();
        }

        [Fact]
        public void QuestError_Emits_OnQuestFailed()
        {
            // Arrange
            uint questId = 2222;
            var errorData = (QuestErrorData?)null;
            var eventFired = false;

            var subscription = _questAgent.QuestErrors.Subscribe(data =>
            {
                errorData = data;
                eventFired = true;
            });

            // Act: simulate server quest failed packet
            Emit(Opcode.SMSG_QUESTGIVER_QUEST_FAILED, BitConverter.GetBytes(questId));

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(errorData);
            Assert.Equal(questId, errorData!.QuestId);
            
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
        [InlineData(QuestOperationType.Offered, Opcode.SMSG_QUESTGIVER_QUEST_DETAILS)]
        [InlineData(QuestOperationType.Accepted, Opcode.SMSG_QUEST_CONFIRM_ACCEPT)]
        [InlineData(QuestOperationType.Completed, Opcode.SMSG_QUESTGIVER_QUEST_COMPLETE)]
        public void QuestOperations_Stream_Emits_ForKnownPackets(QuestOperationType expectedType, Opcode opcode)
        {
            // Arrange
            uint questId = 1234;
            var eventFired = false;

            var subscription = _questAgent.QuestOperations.Subscribe(_ => eventFired = true);

            // Act — SMSG_QUESTGIVER_QUEST_DETAILS needs guid(8)+questId(4)+nulls, others just questId(4)
            if (opcode == Opcode.SMSG_QUESTGIVER_QUEST_DETAILS)
            {
                var payload = new byte[15];
                BitConverter.GetBytes((ulong)0xABCDEF).CopyTo(payload, 0);
                BitConverter.GetBytes(questId).CopyTo(payload, 8);
                Emit(opcode, payload);
            }
            else
            {
                Emit(opcode, BitConverter.GetBytes(questId));
            }

            // Assert
            Assert.True(eventFired);

            subscription.Dispose();
        }

        [Fact(Skip = "Quest progress stream is not mapped to specific opcode in this implementation")]
        public void HandleQuestProgress_FiresQuestProgressObservable()
        {
            // Progress is not emitted in this implementation; mapping is server-core specific.
        }

        [Fact]
        public void QuestAgent_Dispose_DoesNotThrow()
        {
            // Act & Assert
            _questAgent.Dispose();
        }
    }
}