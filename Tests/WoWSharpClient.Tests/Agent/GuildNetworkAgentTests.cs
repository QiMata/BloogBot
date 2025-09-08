using System;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class GuildNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<GuildNetworkAgent>> _mockLogger;
        private readonly GuildNetworkAgent _guildAgent;

        public GuildNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<GuildNetworkAgent>>();
            _guildAgent = new GuildNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        private (IGuildNetworkAgent GuildAgent, Mock<IWorldClient> MockWorldClient, Mock<ILogger<GuildNetworkAgent>> MockLogger) CreateGuildNetworkAgent()
        {
            return (_guildAgent, _mockWorldClient, _mockLogger);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var agent = new GuildNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.False(agent.IsInGuild);
            Assert.Null(agent.CurrentGuildId);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GuildNetworkAgent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GuildNetworkAgent(_mockWorldClient.Object, null!));
        }

        #endregion

        [Fact]
        public async Task AcceptGuildInviteAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.AcceptGuildInviteAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_ACCEPT, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeclineGuildInviteAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.DeclineGuildInviteAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_DECLINE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvitePlayerToGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.InvitePlayerToGuildAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_INVITE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemovePlayerFromGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.RemovePlayerFromGuildAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_REMOVE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteGuildMemberAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.PromoteGuildMemberAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_PROMOTE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DemoteGuildMemberAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.DemoteGuildMemberAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_DEMOTE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeaveGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.LeaveGuildAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_LEAVE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisbandGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.DisbandGuildAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_DISBAND, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMOTDAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();
            const string motd = "Test MOTD";

            // Act
            await guildAgent.SetGuildMOTDAsync(motd);

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_MOTD, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMemberNoteAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();
            const string playerName = "TestPlayer";
            const string note = "Test note";

            // Act
            await guildAgent.SetGuildMemberNoteAsync(playerName, note, false);

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_SET_PUBLIC_NOTE, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestGuildRosterAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.RequestGuildRosterAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_ROSTER, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestGuildInfoAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.RequestGuildInfoAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendMovementAsync(
                Opcode.CMSG_GUILD_INFO, 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DepositItemToGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.DepositItemToGuildBankAsync(0, 5, 1, 10);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendMovementAsync(
                It.IsAny<Opcode>(), 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawItemFromGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.WithdrawItemFromGuildBankAsync(1, 10);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendMovementAsync(
                It.IsAny<Opcode>(), 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DepositMoneyToGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.DepositMoneyToGuildBankAsync(10000);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendMovementAsync(
                It.IsAny<Opcode>(), 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawMoneyFromGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.WithdrawMoneyFromGuildBankAsync(5000);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendMovementAsync(
                It.IsAny<Opcode>(), 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueryGuildBankTabAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkAgent();

            // Act
            await guildAgent.QueryGuildBankTabAsync(1);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendMovementAsync(
                It.IsAny<Opcode>(), 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void HandleServerResponse_ShouldProcessGuildInvite()
        {
            // Arrange
            var (guildAgent, _, _) = CreateGuildNetworkAgent();
            var inviteReceived = false;
            string? inviterName = null;
            string? guildName = null;

            ((GuildNetworkAgent)guildAgent).OnGuildInviteReceived += (inviter, guild) =>
            {
                inviteReceived = true;
                inviterName = inviter;
                guildName = guild;
            };

            // Create sample guild invite data with sufficient size
            var data = new byte[50]; // Increased size to accommodate both strings
            var inviterBytes = System.Text.Encoding.UTF8.GetBytes("TestInviter");
            var guildBytes = System.Text.Encoding.UTF8.GetBytes("TestGuild");
            
            inviterBytes.CopyTo(data, 0);
            guildBytes.CopyTo(data, 25); // Start guild name at position 25 to avoid overlap

            // Act
            ((GuildNetworkAgent)guildAgent).HandleServerResponse(Opcode.SMSG_GUILD_INVITE, data);

            // Assert
            Assert.True(inviteReceived);
        }

        [Fact]
        public void IsInGuild_ShouldReturnFalse_Initially()
        {
            // Arrange
            var (guildAgent, _, _) = CreateGuildNetworkAgent();

            // Act & Assert
            Assert.False(guildAgent.IsInGuild);
            Assert.Null(guildAgent.CurrentGuildId);
        }

        [Fact]
        public void GuildInviteReceived_ShouldTriggerBothEvents()
        {
            // Arrange
            var (guildAgent, _, _) = CreateGuildNetworkAgent();
            var standardEventFired = false;
            var backwardCompatEventFired = false;

            // Subscribe to both events
            guildAgent.GuildInviteReceived += (inviter, guild) => standardEventFired = true;
            ((GuildNetworkAgent)guildAgent).OnGuildInviteReceived += (inviter, guild) => backwardCompatEventFired = true;

            // Create test data
            var data = new byte[50];
            var inviterBytes = System.Text.Encoding.UTF8.GetBytes("TestInviter");
            var guildBytes = System.Text.Encoding.UTF8.GetBytes("TestGuild");
            
            inviterBytes.CopyTo(data, 0);
            guildBytes.CopyTo(data, 25);

            // Act
            ((GuildNetworkAgent)guildAgent).HandleServerResponse(Opcode.SMSG_GUILD_INVITE, data);

            // Assert
            Assert.True(standardEventFired);
            Assert.True(backwardCompatEventFired);
        }
    }
}