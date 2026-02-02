using System;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using Xunit;
using System.Reactive.Subjects;

namespace WoWSharpClient.Tests.Agent
{
    public class GuildNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<GuildNetworkClientComponent>> _mockLogger;
        private GuildNetworkClientComponent _guildAgent;

        public GuildNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<GuildNetworkClientComponent>>();
            // Default: return empty observables for any opcode unless specifically overridden in a test
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.IsAny<Opcode>()))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _guildAgent = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        private (IGuildNetworkClientComponent GuildAgent, Mock<IWorldClient> MockWorldClient, Mock<ILogger<GuildNetworkClientComponent>> MockLogger) CreateGuildNetworkClientComponent()
        {
            return (_guildAgent, _mockWorldClient, _mockLogger);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var agent = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);

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
                new GuildNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GuildNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        [Fact]
        public async Task AcceptGuildInviteAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.AcceptGuildInviteAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_ACCEPT, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeclineGuildInviteAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.DeclineGuildInviteAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_DECLINE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvitePlayerToGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.InvitePlayerToGuildAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_INVITE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemovePlayerFromGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.RemovePlayerFromGuildAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_REMOVE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteGuildMemberAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.PromoteGuildMemberAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_PROMOTE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DemoteGuildMemberAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();
            const string playerName = "TestPlayer";

            // Act
            await guildAgent.DemoteGuildMemberAsync(playerName);

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_DEMOTE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeaveGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.LeaveGuildAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_LEAVE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisbandGuildAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.DisbandGuildAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_DISBAND, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMOTDAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();
            const string motd = "Test MOTD";

            // Act
            await guildAgent.SetGuildMOTDAsync(motd);

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_MOTD, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMemberNoteAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();
            const string playerName = "TestPlayer";
            const string note = "Test note";

            // Act
            await guildAgent.SetGuildMemberNoteAsync(playerName, note, false);

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_SET_PUBLIC_NOTE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestGuildRosterAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.RequestGuildRosterAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_ROSTER, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestGuildInfoAsync_ShouldSendCorrectPacket()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.RequestGuildInfoAsync();

            // Assert
            mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GUILD_INFO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DepositItemToGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.DepositItemToGuildBankAsync(0, 5, 1, 10);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawItemFromGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.WithdrawItemFromGuildBankAsync(1, 10);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DepositMoneyToGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.DepositMoneyToGuildBankAsync(10000);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawMoneyFromGuildBankAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.WithdrawMoneyFromGuildBankAsync(5000);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueryGuildBankTabAsync_ShouldNotSendPackets_WhenNotSupported()
        {
            // Arrange
            var (guildAgent, mockWorldClient, _) = CreateGuildNetworkClientComponent();

            // Act
            await guildAgent.QueryGuildBankTabAsync(1);

            // Assert - Should not send any packets since not supported
            mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void GuildInvitesObservable_ShouldEmitOnIncomingInvite()
        {
            // Arrange: set up a dedicated subject for the invite opcode BEFORE constructing component
            var inviteSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            // Generic setup for all opcodes EXCEPT the guild invite opcode
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_INVITE)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            // Specific setup for guild invite opcode (must not be overridden by generic)
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_INVITE))
                .Returns(inviteSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            var received = false;
            using var sub = component.GuildInvites.Subscribe(_ => received = true);

            // Act: push fake payload (parsing currently placeholder)
            inviteSubject.OnNext(new byte[16]);

            // Assert
            Assert.True(received);
        }

        [Fact]
        public void IsInGuild_ShouldReturnFalse_Initially()
        {
            // Arrange
            var (guildAgent, _, _) = CreateGuildNetworkClientComponent();

            // Act & Assert
            Assert.False(guildAgent.IsInGuild);
            Assert.Null(guildAgent.CurrentGuildId);
        }
    }
}