using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
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
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.IsAny<Opcode>()))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _guildAgent = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            var agent = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            Assert.NotNull(agent);
            Assert.False(agent.IsInGuild);
            Assert.Null(agent.CurrentGuildId);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GuildNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GuildNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region CMSG Opcode Tests

        [Fact]
        public async Task AcceptGuildInviteAsync_ShouldSendEmptyPayload()
        {
            await _guildAgent.AcceptGuildInviteAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_ACCEPT,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeclineGuildInviteAsync_ShouldSendEmptyPayload()
        {
            await _guildAgent.DeclineGuildInviteAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_DECLINE,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvitePlayerToGuildAsync_ShouldSendNameWithNullTerminator()
        {
            const string playerName = "TestPlayer";
            await _guildAgent.InvitePlayerToGuildAsync(playerName);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_INVITE,
                It.Is<byte[]>(p =>
                    p.Length == playerName.Length + 1 &&
                    Encoding.UTF8.GetString(p, 0, playerName.Length) == playerName &&
                    p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemovePlayerFromGuildAsync_ShouldSendNameWithNullTerminator()
        {
            const string playerName = "BadPlayer";
            await _guildAgent.RemovePlayerFromGuildAsync(playerName);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_REMOVE,
                It.Is<byte[]>(p =>
                    p.Length == playerName.Length + 1 &&
                    Encoding.UTF8.GetString(p, 0, playerName.Length) == playerName &&
                    p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteGuildMemberAsync_ShouldSendCorrectPayload()
        {
            const string playerName = "GoodPlayer";
            await _guildAgent.PromoteGuildMemberAsync(playerName);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_PROMOTE,
                It.Is<byte[]>(p => p.Length == playerName.Length + 1 && p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DemoteGuildMemberAsync_ShouldSendCorrectPayload()
        {
            const string playerName = "SlackerPlayer";
            await _guildAgent.DemoteGuildMemberAsync(playerName);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_DEMOTE,
                It.Is<byte[]>(p => p.Length == playerName.Length + 1 && p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeaveGuildAsync_ShouldSendEmptyPayload()
        {
            await _guildAgent.LeaveGuildAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_LEAVE,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisbandGuildAsync_ShouldSendEmptyPayload()
        {
            await _guildAgent.DisbandGuildAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_DISBAND,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMOTDAsync_ShouldSendMotdWithNullTerminator()
        {
            const string motd = "Welcome to the guild!";
            await _guildAgent.SetGuildMOTDAsync(motd);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_MOTD,
                It.Is<byte[]>(p =>
                    p.Length == motd.Length + 1 &&
                    Encoding.UTF8.GetString(p, 0, motd.Length) == motd &&
                    p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMemberNoteAsync_PublicNote_ShouldSendPlayerNameAndNote()
        {
            const string playerName = "TestPlayer";
            const string note = "Good healer";
            await _guildAgent.SetGuildMemberNoteAsync(playerName, note, false);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_SET_PUBLIC_NOTE,
                It.Is<byte[]>(p =>
                    p.Length == playerName.Length + 1 + note.Length + 1 &&
                    Encoding.UTF8.GetString(p, 0, playerName.Length) == playerName &&
                    p[playerName.Length] == 0 &&
                    Encoding.UTF8.GetString(p, playerName.Length + 1, note.Length) == note &&
                    p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetGuildMemberNoteAsync_OfficerNote_ShouldUseOfficerOpcode()
        {
            const string playerName = "OfficerTest";
            const string note = "Promoted to officer";
            await _guildAgent.SetGuildMemberNoteAsync(playerName, note, true);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_SET_OFFICER_NOTE,
                It.Is<byte[]>(p => p.Length == playerName.Length + 1 + note.Length + 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestGuildRosterAsync_ShouldSendEmptyPayload()
        {
            await _guildAgent.RequestGuildRosterAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_ROSTER,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestGuildInfoAsync_ShouldSendEmptyPayload()
        {
            await _guildAgent.RequestGuildInfoAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_INFO,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateGuildAsync_ShouldSendGuildNameWithNullTerminator()
        {
            const string guildName = "Epic Guild";
            await _guildAgent.CreateGuildAsync(guildName);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GUILD_CREATE,
                It.Is<byte[]>(p =>
                    p.Length == guildName.Length + 1 &&
                    Encoding.UTF8.GetString(p, 0, guildName.Length) == guildName &&
                    p[p.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region SMSG_GUILD_INVITE Parsing Tests

        [Fact]
        public void GuildInvites_ShouldParseInviterAndGuildName()
        {
            // Arrange: SMSG_GUILD_INVITE = inviterName\0 + guildName\0
            var inviteSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_INVITE)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_INVITE))
                .Returns(inviteSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            (string Inviter, string GuildName) result = default;
            using var sub = component.GuildInvites.Subscribe(r => result = r);

            // Build payload: "Arthas\0" + "Frostmourne Guild\0"
            var payload = BuildCStringPayload("Arthas", "Frostmourne Guild");
            inviteSubject.OnNext(payload);

            Assert.Equal("Arthas", result.Inviter);
            Assert.Equal("Frostmourne Guild", result.GuildName);
        }

        [Fact]
        public void GuildInvites_EmptyPayload_ShouldReturnEmptyStrings()
        {
            var inviteSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_INVITE)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_INVITE))
                .Returns(inviteSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            (string Inviter, string GuildName) result = default;
            using var sub = component.GuildInvites.Subscribe(r => result = r);

            inviteSubject.OnNext(Array.Empty<byte>());

            Assert.Equal(string.Empty, result.Inviter);
            Assert.Equal(string.Empty, result.GuildName);
        }

        [Fact]
        public void HandleServerResponse_ShouldFireLegacyInviteEvent()
        {
            string? receivedInviter = null;
            string? receivedGuild = null;
            _guildAgent.OnGuildInviteReceived += (inviter, guild) =>
            {
                receivedInviter = inviter;
                receivedGuild = guild;
            };

            var payload = BuildCStringPayload("Thrall", "Horde Heroes");
            _guildAgent.HandleServerResponse(Opcode.SMSG_GUILD_INVITE, payload);

            Assert.Equal("Thrall", receivedInviter);
            Assert.Equal("Horde Heroes", receivedGuild);
        }

        #endregion

        #region SMSG_GUILD_INFO Parsing Tests

        [Fact]
        public void GuildInfos_ShouldParseGuildInfoPacket()
        {
            // Arrange: SMSG_GUILD_INFO = guildName\0 + day(4) + month(4) + year(4) + memberCount(4) + accountCount(4)
            var infoSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_INFO)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_INFO))
                .Returns(infoSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildInfo? result = null;
            using var sub = component.GuildInfos.Subscribe(r => result = r);

            // Build payload: "Test Guild\0" + day=14 + month=5 (June, 0-based) + year=2020 + members=42 + accounts=35
            var nameBytes = Encoding.UTF8.GetBytes("Test Guild");
            var payload = new byte[nameBytes.Length + 1 + 20]; // name\0 + 5×uint32
            Array.Copy(nameBytes, payload, nameBytes.Length);
            payload[nameBytes.Length] = 0;
            int offset = nameBytes.Length + 1;
            BitConverter.TryWriteBytes(payload.AsSpan(offset), (uint)14); offset += 4;     // day (0-based)
            BitConverter.TryWriteBytes(payload.AsSpan(offset), (uint)5); offset += 4;      // month (0-based, June)
            BitConverter.TryWriteBytes(payload.AsSpan(offset), (uint)2020); offset += 4;   // year
            BitConverter.TryWriteBytes(payload.AsSpan(offset), (uint)42); offset += 4;     // memberCount
            BitConverter.TryWriteBytes(payload.AsSpan(offset), (uint)35);                  // accountCount

            infoSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Test Guild", result.Name);
            Assert.Equal((uint)42, result.MemberCount);
            Assert.Equal(2020, result.CreationDate.Year);
            Assert.Equal(6, result.CreationDate.Month); // month+1
            Assert.Equal(15, result.CreationDate.Day);   // day+1
        }

        [Fact]
        public void GuildInfos_ShouldSetIsInGuildAndCurrentGuildName()
        {
            var infoSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_INFO)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_INFO))
                .Returns(infoSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            using var sub = component.GuildInfos.Subscribe(_ => { });

            Assert.False(component.IsInGuild);

            var nameBytes = Encoding.UTF8.GetBytes("My Guild");
            var payload = new byte[nameBytes.Length + 1 + 20];
            Array.Copy(nameBytes, payload, nameBytes.Length);
            infoSubject.OnNext(payload);

            Assert.True(component.IsInGuild);
            Assert.Equal("My Guild", component.CurrentGuildName);
        }

        #endregion

        #region SMSG_GUILD_ROSTER Parsing Tests

        [Fact]
        public void GuildRosters_ShouldParseRosterWithOnlineMember()
        {
            var rosterSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_ROSTER)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_ROSTER))
                .Returns(rosterSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            IReadOnlyList<GuildMember>? result = null;
            using var sub = component.GuildRosters.Subscribe(r => result = r);

            // Build a roster with 1 online member, 1 rank, MOTD, guild info text
            var payload = BuildRosterPayload(
                motd: "Welcome!",
                guildInfo: "We are strong",
                rankRights: [0x0FFF],
                members: [(guid: 100UL, online: true, name: "Warrior", rankId: 0U, level: 60, classId: 1, zoneId: 1519U, publicNote: "Tank", officerNote: "Main tank")]
            );
            rosterSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Warrior", result[0].Name);
            Assert.Equal(100UL, result[0].Guid);
            Assert.True(result[0].IsOnline);
            Assert.Equal(0U, result[0].Rank);
            Assert.Equal((uint)60, result[0].Level);
            Assert.Equal((uint)1, result[0].Class);
            Assert.Equal("1519", result[0].Zone);
            Assert.Equal("Tank", result[0].PublicNote);
            Assert.Equal("Main tank", result[0].OfficerNote);
            Assert.Null(result[0].LastLogon); // online members have no LastLogon
        }

        [Fact]
        public void GuildRosters_ShouldParseOfflineMemberWithLogoutTime()
        {
            var rosterSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_ROSTER)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_ROSTER))
                .Returns(rosterSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            IReadOnlyList<GuildMember>? result = null;
            using var sub = component.GuildRosters.Subscribe(r => result = r);

            var payload = BuildRosterPayload(
                motd: "",
                guildInfo: "",
                rankRights: [0x01],
                members: [(guid: 200UL, online: false, name: "Mage", rankId: 1U, level: 45, classId: 8, zoneId: 44U, publicNote: "", officerNote: "")]
            );
            rosterSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Mage", result[0].Name);
            Assert.False(result[0].IsOnline);
            Assert.NotNull(result[0].LastLogon);
        }

        [Fact]
        public void GuildRosters_ShouldParseMultipleMembers()
        {
            var rosterSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_ROSTER)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_ROSTER))
                .Returns(rosterSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            IReadOnlyList<GuildMember>? result = null;
            using var sub = component.GuildRosters.Subscribe(r => result = r);

            var payload = BuildRosterPayload(
                motd: "Raid tonight!",
                guildInfo: "Level 60+ guild",
                rankRights: [0xFFFF, 0x00FF],
                members: [
                    (guid: 10UL, online: true, name: "Leader", rankId: 0U, level: 60, classId: 2, zoneId: 1519U, publicNote: "GM", officerNote: "Founder"),
                    (guid: 20UL, online: true, name: "Officer", rankId: 1U, level: 58, classId: 5, zoneId: 1519U, publicNote: "", officerNote: ""),
                    (guid: 30UL, online: false, name: "Alt", rankId: 1U, level: 30, classId: 4, zoneId: 12U, publicNote: "Alt of Leader", officerNote: "")
                ]
            );
            rosterSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal("Leader", result[0].Name);
            Assert.Equal("Officer", result[1].Name);
            Assert.Equal("Alt", result[2].Name);
            Assert.True(result[0].IsOnline);
            Assert.True(result[1].IsOnline);
            Assert.False(result[2].IsOnline);
        }

        [Fact]
        public void GuildRosters_EmptyRoster_ShouldReturnEmptyList()
        {
            var rosterSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_ROSTER)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_ROSTER))
                .Returns(rosterSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            IReadOnlyList<GuildMember>? result = null;
            using var sub = component.GuildRosters.Subscribe(r => result = r);

            var payload = BuildRosterPayload(
                motd: "No members",
                guildInfo: "",
                rankRights: [0x01],
                members: []
            );
            rosterSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region SMSG_GUILD_COMMAND_RESULT Parsing Tests

        [Fact]
        public void GuildCommandResults_ShouldParseCreateSuccess()
        {
            var cmdSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_COMMAND_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_COMMAND_RESULT))
                .Returns(cmdSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildCommandResult? result = null;
            using var sub = component.GuildCommandResults.Subscribe(r => result = r);

            // commandType=0 (CREATE_S) + name="My Guild\0" + errorCode=0 (success)
            var payload = BuildCommandResultPayload(0x00, "My Guild", 0);
            cmdSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Create", result.Value.Operation);
            Assert.True(result.Value.Success);
            Assert.Equal(0U, result.Value.ResultCode);
        }

        [Fact]
        public void GuildCommandResults_ShouldParseInviteAlreadyInGuild()
        {
            var cmdSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_COMMAND_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_COMMAND_RESULT))
                .Returns(cmdSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildCommandResult? result = null;
            using var sub = component.GuildCommandResults.Subscribe(r => result = r);

            // commandType=1 (INVITE_S) + name="Player\0" + errorCode=2 (AlreadyInGuild)
            var payload = BuildCommandResultPayload(0x01, "Player", 2);
            cmdSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Invite", result.Value.Operation);
            Assert.False(result.Value.Success);
            Assert.Equal(2U, result.Value.ResultCode);
        }

        [Fact]
        public void GuildCommandResults_ShouldParseQuitSuccess()
        {
            var cmdSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_COMMAND_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_COMMAND_RESULT))
                .Returns(cmdSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildCommandResult? result = null;
            using var sub = component.GuildCommandResults.Subscribe(r => result = r);

            // commandType=3 (QUIT_S) + name="\0" + errorCode=0
            var payload = BuildCommandResultPayload(0x03, "", 0);
            cmdSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Quit", result.Value.Operation);
            Assert.True(result.Value.Success);
        }

        [Fact]
        public void GuildCommandResults_ShouldParseFounderCommand()
        {
            var cmdSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_COMMAND_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_COMMAND_RESULT))
                .Returns(cmdSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildCommandResult? result = null;
            using var sub = component.GuildCommandResults.Subscribe(r => result = r);

            // commandType=0x0E (FOUNDER_S) + name="GuildLeader\0" + errorCode=0
            var payload = BuildCommandResultPayload(0x0E, "GuildLeader", 0);
            cmdSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Founder", result.Value.Operation);
            Assert.True(result.Value.Success);
        }

        [Fact]
        public void GuildCommandResults_ShouldHandleUnknownCommandType()
        {
            var cmdSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_COMMAND_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_COMMAND_RESULT))
                .Returns(cmdSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildCommandResult? result = null;
            using var sub = component.GuildCommandResults.Subscribe(r => result = r);

            var payload = BuildCommandResultPayload(0xFF, "Test", 0);
            cmdSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.StartsWith("Unknown(", result.Value.Operation);
        }

        [Fact]
        public void GuildCommandResults_ShortPayload_ShouldReturnUnknown()
        {
            var cmdSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_COMMAND_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_COMMAND_RESULT))
                .Returns(cmdSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildCommandResult? result = null;
            using var sub = component.GuildCommandResults.Subscribe(r => result = r);

            // Payload too short (< 4 bytes)
            cmdSubject.OnNext(new byte[2]);

            Assert.NotNull(result);
            Assert.Equal("Unknown", result.Value.Operation);
            Assert.False(result.Value.Success);
        }

        #endregion

        #region SMSG_GUILD_EVENT Parsing Tests

        [Fact]
        public void MemberStatusChanges_ShouldParseSignedOnEvent()
        {
            var eventSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_EVENT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_EVENT))
                .Returns(eventSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildMemberStatusChange? result = null;
            using var sub = component.MemberStatusChanges.Subscribe(r => result = r);

            // GE_SIGNED_ON = 0x0C, strCount=1, "PlayerName\0" + guid(8)
            var payload = BuildGuildEventPayload(0x0C, ["Arthas"], 12345UL);
            eventSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Arthas", result.Value.MemberName);
            Assert.True(result.Value.IsOnline);
        }

        [Fact]
        public void MemberStatusChanges_ShouldParseSignedOffEvent()
        {
            var eventSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_EVENT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_EVENT))
                .Returns(eventSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            GuildMemberStatusChange? result = null;
            using var sub = component.MemberStatusChanges.Subscribe(r => result = r);

            // GE_SIGNED_OFF = 0x0D, strCount=1, "Mage\0"
            var payload = BuildGuildEventPayload(0x0D, ["Mage"], null);
            eventSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Mage", result.Value.MemberName);
            Assert.False(result.Value.IsOnline);
        }

        [Fact]
        public void MemberStatusChanges_NonStatusEvent_ShouldNotEmit()
        {
            var eventSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_EVENT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_EVENT))
                .Returns(eventSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            bool received = false;
            using var sub = component.MemberStatusChanges.Subscribe(_ => received = true);

            // GE_PROMOTION = 0x00 — not a sign-on/off event, should NOT yield
            var payload = BuildGuildEventPayload(0x00, ["Player", "OldRank", "NewRank"], null);
            eventSubject.OnNext(payload);

            Assert.False(received);
        }

        [Fact]
        public void MemberStatusChanges_EmptyPayload_ShouldNotEmit()
        {
            var eventSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_GUILD_EVENT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_GUILD_EVENT))
                .Returns(eventSubject);

            var component = new GuildNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            bool received = false;
            using var sub = component.MemberStatusChanges.Subscribe(_ => received = true);

            eventSubject.OnNext(Array.Empty<byte>());

            Assert.False(received);
        }

        #endregion

        #region Guild Bank Not Supported Tests

        [Fact]
        public async Task DepositItemToGuildBankAsync_ShouldNotSendPackets()
        {
            await _guildAgent.DepositItemToGuildBankAsync(0, 5, 1, 10);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawItemFromGuildBankAsync_ShouldNotSendPackets()
        {
            await _guildAgent.WithdrawItemFromGuildBankAsync(1, 10);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DepositMoneyToGuildBankAsync_ShouldNotSendPackets()
        {
            await _guildAgent.DepositMoneyToGuildBankAsync(10000);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawMoneyFromGuildBankAsync_ShouldNotSendPackets()
        {
            await _guildAgent.WithdrawMoneyFromGuildBankAsync(5000);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueryGuildBankTabAsync_ShouldNotSendPackets()
        {
            await _guildAgent.QueryGuildBankTabAsync(1);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region State Property Tests

        [Fact]
        public void IsInGuild_ShouldReturnFalse_Initially()
        {
            Assert.False(_guildAgent.IsInGuild);
            Assert.Null(_guildAgent.CurrentGuildId);
            Assert.Null(_guildAgent.CurrentGuildName);
        }

        [Fact]
        public void IsGuildBankOpen_ShouldReturnFalse()
        {
            Assert.False(_guildAgent.IsGuildBankOpen(12345));
        }

        [Fact]
        public void GetCurrentGuildRank_ShouldReturnNull()
        {
            Assert.Null(_guildAgent.GetCurrentGuildRank());
        }

        [Fact]
        public void HasGuildPermission_ShouldReturnFalse()
        {
            Assert.False(_guildAgent.HasGuildPermission(0x01));
        }

        #endregion

        #region Payload Builder Helpers

        private static byte[] BuildCStringPayload(params string[] strings)
        {
            using var ms = new MemoryStream();
            foreach (var s in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                ms.Write(bytes, 0, bytes.Length);
                ms.WriteByte(0);
            }
            return ms.ToArray();
        }

        private static byte[] BuildCommandResultPayload(uint commandType, string name, uint errorCode)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var payload = new byte[4 + nameBytes.Length + 1 + 4];
            BitConverter.TryWriteBytes(payload.AsSpan(0), commandType);
            Array.Copy(nameBytes, 0, payload, 4, nameBytes.Length);
            payload[4 + nameBytes.Length] = 0;
            BitConverter.TryWriteBytes(payload.AsSpan(4 + nameBytes.Length + 1), errorCode);
            return payload;
        }

        private static byte[] BuildGuildEventPayload(byte eventType, string[] strings, ulong? guid)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(eventType);
            ms.WriteByte((byte)strings.Length);
            foreach (var s in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                ms.Write(bytes, 0, bytes.Length);
                ms.WriteByte(0);
            }
            if (guid.HasValue)
            {
                var guidBytes = BitConverter.GetBytes(guid.Value);
                ms.Write(guidBytes, 0, 8);
            }
            return ms.ToArray();
        }

        private static byte[] BuildRosterPayload(
            string motd,
            string guildInfo,
            uint[] rankRights,
            (ulong guid, bool online, string name, uint rankId, byte level, byte classId, uint zoneId, string publicNote, string officerNote)[] members)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            // memberCount(4)
            bw.Write((uint)members.Length);

            // motd\0
            bw.Write(Encoding.UTF8.GetBytes(motd));
            bw.Write((byte)0);

            // guildinfo\0
            bw.Write(Encoding.UTF8.GetBytes(guildInfo));
            bw.Write((byte)0);

            // rankCount(4) + [rankRights(4)]
            bw.Write((uint)rankRights.Length);
            foreach (var rights in rankRights)
                bw.Write(rights);

            // member entries
            foreach (var m in members)
            {
                bw.Write(m.guid);            // guid(8)
                bw.Write(m.online ? (byte)1 : (byte)0);  // status(1)
                bw.Write(Encoding.UTF8.GetBytes(m.name));
                bw.Write((byte)0);           // name\0
                bw.Write(m.rankId);          // rankId(4)
                bw.Write(m.level);           // level(1)
                bw.Write(m.classId);         // class(1)
                bw.Write(m.zoneId);          // zoneId(4)

                if (!m.online)
                    bw.Write(3600.0f);       // logoutTime: 1 hour ago

                bw.Write(Encoding.UTF8.GetBytes(m.publicNote));
                bw.Write((byte)0);           // publicNote\0
                bw.Write(Encoding.UTF8.GetBytes(m.officerNote));
                bw.Write((byte)0);           // officerNote\0
            }

            return ms.ToArray();
        }

        #endregion
    }
}
