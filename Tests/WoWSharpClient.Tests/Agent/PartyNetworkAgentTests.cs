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
    /// Tests for the PartyNetworkClientComponent class.
    /// </summary>
    public class PartyNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<PartyNetworkClientComponent>> _mockLogger;
        private readonly PartyNetworkClientComponent _partyAgent;

        public PartyNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<PartyNetworkClientComponent>>();
            _partyAgent = new PartyNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
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
            await _partyAgent.SetLootMethodAsync(LootMethod.GroupLoot, null, LootQuality.Rare);

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

        #region Event Firing Tests

        [Fact]
        public void PartyInviteReceived_Event_CanBeSubscribedAndFired()
        {
            // Arrange
            string? receivedInviterName = null;
            _partyAgent.PartyInviteReceived += (inviterName) => receivedInviterName = inviterName;

            // Act
            var data = new byte[] { 0x01 }; // Simple test data
            _partyAgent.HandleServerResponse(Opcode.SMSG_GROUP_INVITE, data);

            // Assert
            Assert.NotNull(receivedInviterName);
        }

        [Fact]
        public void GroupJoined_Event_CanBeSubscribedAndFired()
        {
            // Arrange
            bool? isRaid = null;
            uint? memberCount = null;
            _partyAgent.GroupJoined += (raid, count) => { isRaid = raid; memberCount = count; };

            // Act
            var data = new byte[] { 0x00, 0x02, 0x00, 0x00, 0x00 }; // Party with 2 members
            _partyAgent.HandleServerResponse(Opcode.SMSG_GROUP_LIST, data);

            // Assert
            Assert.NotNull(isRaid);
            Assert.NotNull(memberCount);
        }

        [Fact]
        public void GroupLeft_Event_CanBeSubscribedAndFired()
        {
            // Arrange
            string? leftReason = null;
            _partyAgent.GroupLeft += (reason) => leftReason = reason;

            // Act
            var data = new byte[] { 0x01 }; // Simple test data
            _partyAgent.HandleServerResponse(Opcode.SMSG_GROUP_DESTROYED, data);

            // Assert
            Assert.NotNull(leftReason);
        }

        [Fact]
        public void PartyOperationFailed_Event_CanBeSubscribedAndFired()
        {
            // Arrange
            string? operation = null;
            string? error = null;
            _partyAgent.PartyOperationFailed += (op, err) => { operation = op; error = err; };

            // Act
            var data = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 }; // Operation 0, Result 1 (failure)
            _partyAgent.HandleServerResponse(Opcode.SMSG_PARTY_COMMAND_RESULT, data);

            // Assert
            Assert.NotNull(operation);
            Assert.NotNull(error);
        }

        #endregion

        #region Server Response Handling Tests

        [Fact]
        public void HandleServerResponse_WithUnknownOpcode_DoesNotThrow()
        {
            // Arrange
            var data = new byte[] { 0x01, 0x02, 0x03 };

            // Act & Assert (should not throw)
            _partyAgent.HandleServerResponse(Opcode.SMSG_AUTH_CHALLENGE, data);
        }

        [Fact]
        public void HandleServerResponse_WithInvalidData_DoesNotThrow()
        {
            // Arrange
            var data = new byte[] { }; // Empty data

            // Act & Assert (should not throw)
            _partyAgent.HandleServerResponse(Opcode.SMSG_GROUP_INVITE, data);
        }

        #endregion
    }
}