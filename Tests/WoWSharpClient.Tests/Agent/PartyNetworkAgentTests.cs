using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for the PartyNetworkClientComponent class (reactive variant).
    /// </summary>
    public class PartyNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<PartyNetworkClientComponent>> _mockLogger;
        private readonly Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> _opcodeSubjects = new();
        private PartyNetworkClientComponent _partyAgent;

        public PartyNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<PartyNetworkClientComponent>>();

            _mockWorldClient
                .Setup(x => x.RegisterOpcodeHandler(It.IsAny<Opcode>()))
                .Returns((Opcode op) =>
                {
                    if (!_opcodeSubjects.TryGetValue(op, out var subj))
                    {
                        subj = new Subject<ReadOnlyMemory<byte>>();
                        _opcodeSubjects[op] = subj;
                    }
                    return subj;
                });

            _partyAgent = new PartyNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        private Subject<ReadOnlyMemory<byte>> GetSubject(Opcode op)
        {
            if (!_opcodeSubjects.TryGetValue(op, out var subj))
            {
                subj = new Subject<ReadOnlyMemory<byte>>();
                _opcodeSubjects[op] = subj;
            }
            return subj;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var agent = new PartyNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.False(agent.IsInGroup);
            Assert.False(agent.IsInRaid);
            Assert.False(agent.IsGroupLeader);
            Assert.Equal(0u, agent.GroupSize);
            Assert.False(agent.HasPendingInvite);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PartyNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PartyNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Party Invite Operations Tests

        [Fact]
        public async Task InvitePlayerAsync_WithValidPlayerName_SendsCorrectPacket()
        {
            // Arrange
            const string playerName = "TestPlayer";
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.InvitePlayerAsync(playerName);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_INVITE,
                It.Is<byte[]>(data => data.Length == playerName.Length + 1 && data[data.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvitePlayerAsync_WithNullPlayerName_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _partyAgent.InvitePlayerAsync(null!));
        }

        [Fact]
        public async Task InvitePlayerAsync_WithEmptyPlayerName_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _partyAgent.InvitePlayerAsync(""));
        }

        [Fact]
        public async Task InvitePlayerAsync_WithWhitespacePlayerName_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _partyAgent.InvitePlayerAsync("   "));
        }

        [Fact]
        public async Task AcceptInviteAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.AcceptInviteAsync();

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_ACCEPT,
                It.Is<byte[]>(data => data.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.False(_partyAgent.HasPendingInvite);
        }

        [Fact]
        public async Task DeclineInviteAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.DeclineInviteAsync();

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_DECLINE,
                It.Is<byte[]>(data => data.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.False(_partyAgent.HasPendingInvite);
        }

        [Fact]
        public async Task CancelInviteAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.CancelInviteAsync();

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_CANCEL,
                It.Is<byte[]>(data => data.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Member Management Tests

        [Fact]
        public async Task KickPlayerAsync_WithPlayerName_SendsCorrectPacket()
        {
            // Arrange
            const string playerName = "TestPlayer";
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.KickPlayerAsync(playerName);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_UNINVITE,
                It.Is<byte[]>(data => data.Length == playerName.Length + 1 && data[data.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task KickPlayerAsync_WithPlayerGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong playerGuid = 0x123456789ABCDEF0;
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.KickPlayerAsync(playerGuid);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_UNINVITE_GUID,
                It.Is<byte[]>(data => data.Length == 8),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task KickPlayerAsync_WithNullPlayerName_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _partyAgent.KickPlayerAsync((string)null!));
        }

        [Fact]
        public async Task KickPlayerAsync_WithEmptyPlayerName_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _partyAgent.KickPlayerAsync(""));
        }

        [Fact]
        public async Task LeaveGroupAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Act
            await _partyAgent.LeaveGroupAsync();

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_DISBAND,
                It.Is<byte[]>(data => data.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Leadership Operations Tests

        [Fact]
        public async Task PromoteToLeaderAsync_WithPlayerName_SendsCorrectPacket()
        {
            // Arrange
            const string playerName = "TestPlayer";
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Use reflection to set IsGroupLeader to true for this test
            var isGroupLeaderProperty = typeof(PartyNetworkClientComponent).GetProperty("IsGroupLeader");
            isGroupLeaderProperty?.SetValue(_partyAgent, true);

            // Act
            await _partyAgent.PromoteToLeaderAsync(playerName);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_SET_LEADER,
                It.Is<byte[]>(data => data.Length == playerName.Length + 1 && data[data.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteToLeaderAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            // Arrange
            const string playerName = "TestPlayer";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _partyAgent.PromoteToLeaderAsync(playerName));
        }

        [Fact]
        public async Task PromoteToLeaderAsync_WithNullPlayerName_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _partyAgent.PromoteToLeaderAsync((string)null!));
        }

        [Fact]
        public async Task PromoteToLeaderAsync_WithEmptyPlayerName_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _partyAgent.PromoteToLeaderAsync(""));
        }

        [Fact]
        public async Task ConvertToRaidAsync_WhenGroupLeader_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Use reflection to set IsGroupLeader to true for this test
            var isGroupLeaderProperty = typeof(PartyNetworkClientComponent).GetProperty("IsGroupLeader");
            isGroupLeaderProperty?.SetValue(_partyAgent, true);

            // Act
            await _partyAgent.ConvertToRaidAsync();

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_RAID_CONVERT,
                It.Is<byte[]>(data => data.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConvertToRaidAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _partyAgent.ConvertToRaidAsync());
        }

        #endregion

        #region Loot Settings Tests

        [Fact]
        public async Task SetLootMethodAsync_WhenGroupLeader_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            // Use reflection to set IsGroupLeader to true for this test
            var isGroupLeaderProperty = typeof(PartyNetworkClientComponent).GetProperty("IsGroupLeader");
            isGroupLeaderProperty?.SetValue(_partyAgent, true);

            // Act
            await _partyAgent.SetLootMethodAsync(LootMethod.GroupLoot, null, ItemQuality.Rare);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_LOOT_METHOD,
                It.Is<byte[]>(data => data.Length == 17),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetLootMethodAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _partyAgent.SetLootMethodAsync(LootMethod.GroupLoot));
        }

        #endregion

        #region Utility Methods Tests

        [Fact]
        public void GetGroupMembers_ReturnsEmptyList_WhenNoMembers()
        {
            // Act
            var members = _partyAgent.GetGroupMembers();

            // Assert
            Assert.NotNull(members);
            Assert.Empty(members);
        }

        [Fact]
        public void IsPlayerInGroup_ReturnsFalse_WhenPlayerNotInGroup()
        {
            // Act
            var result = _partyAgent.IsPlayerInGroup("TestPlayer");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsPlayerInGroup_WithGuid_ReturnsFalse_WhenPlayerNotInGroup()
        {
            // Act
            var result = _partyAgent.IsPlayerInGroup(0x123456789ABCDEF0);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetGroupMember_ReturnsNull_WhenPlayerNotFound()
        {
            // Act
            var member = _partyAgent.GetGroupMember("TestPlayer");

            // Assert
            Assert.Null(member);
        }

        [Fact]
        public void GetGroupMember_WithGuid_ReturnsNull_WhenPlayerNotFound()
        {
            // Act
            var member = _partyAgent.GetGroupMember(0x123456789ABCDEF0);

            // Assert
            Assert.Null(member);
        }

        #endregion

        #region Reactive Stream Tests

        [Fact]
        public void PartyInvites_Stream_EmitsOnInvite()
        {
            // Arrange
            string? inviter = null;
            using var sub = _partyAgent.PartyInvites.Subscribe(name => inviter = name);

            // Act
            GetSubject(Opcode.SMSG_GROUP_INVITE).OnNext(new ReadOnlyMemory<byte>(new byte[] { 0x01 }));

            // Assert
            Assert.NotNull(inviter);
            Assert.True(_partyAgent.HasPendingInvite);
        }

        [Fact]
        public void GroupUpdates_Stream_EmitsOnGroupList()
        {
            // Arrange
            bool? isRaid = null;
            uint? count = null;
            using var sub = _partyAgent.GroupUpdates.Subscribe(t => { isRaid = t.IsRaid; count = t.MemberCount; });

            // Act (party with 2 members)
            GetSubject(Opcode.SMSG_GROUP_LIST).OnNext(new ReadOnlyMemory<byte>(new byte[] { 0x00, 0x02, 0x00, 0x00, 0x00 }));

            // Assert
            Assert.NotNull(isRaid);
            Assert.NotNull(count);
            Assert.Equal(2u, count!.Value);
            Assert.False(isRaid!.Value);
        }

        [Fact]
        public void GroupLeaves_Stream_EmitsOnGroupDestroyed()
        {
            // Arrange
            string? reason = null;
            using var sub = _partyAgent.GroupLeaves.Subscribe(r => reason = r);

            // Act
            GetSubject(Opcode.SMSG_GROUP_DESTROYED).OnNext(new ReadOnlyMemory<byte>(new byte[] { 0x01 }));

            // Assert
            Assert.NotNull(reason);
            Assert.False(_partyAgent.IsInGroup);
        }

        [Fact]
        public void PartyCommandResults_Stream_EmitsFailure()
        {
            // Arrange
            (string Operation, bool Success, uint ResultCode)? res = null;
            using var sub = _partyAgent.PartyCommandResults.Subscribe(r => res = r);

            // Act: operation 0 (Invite), result 1 (failure)
            GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT).OnNext(new ReadOnlyMemory<byte>(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 }));

            // Assert
            Assert.NotNull(res);
            Assert.False(res!.Value.Success);
            Assert.Equal(1u, res!.Value.ResultCode);
        }

        [Fact]
        public void Streams_HandleInvalidPayload_Gracefully()
        {
            // Arrange
            Exception? thrown = null;
            using var sub1 = _partyAgent.PartyInvites.Subscribe(_ => { });
            using var sub2 = _partyAgent.GroupUpdates.Subscribe(_ => { });

            try
            {
                GetSubject(Opcode.SMSG_GROUP_INVITE).OnNext(ReadOnlyMemory<byte>.Empty);
                GetSubject(Opcode.SMSG_GROUP_LIST).OnNext(ReadOnlyMemory<byte>.Empty);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.Null(thrown);
        }

        #endregion
    }
}