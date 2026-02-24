using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for PartyNetworkClientComponent with 1.12.1 protocol-accurate payloads.
    /// </summary>
    public class PartyNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<PartyNetworkClientComponent>> _mockLogger;
        private readonly Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> _opcodeSubjects = new();
        private readonly PartyNetworkClientComponent _partyAgent;

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

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

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

        #region Payload Builders

        /// <summary>
        /// Builds an SMSG_GROUP_LIST payload in 1.12.1 format:
        /// groupType(1) + ownFlags(1) + count(4)
        /// + [name\0 + guid(8) + online(1) + flags(1)] * count
        /// + leaderGuid(8)
        /// + [if count > 0: lootMethod(1) + looterGuid(8) + lootThreshold(1)]
        /// </summary>
        private static byte[] BuildGroupListPayload(
            byte groupType, byte ownFlags,
            (string Name, ulong Guid, byte Online, byte Flags)[] members,
            ulong leaderGuid,
            byte lootMethod = 0, ulong looterGuid = 0, byte lootThreshold = 0)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(groupType);
            ms.WriteByte(ownFlags);

            var buf = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)members.Length);
            ms.Write(buf, 0, 4);

            foreach (var m in members)
            {
                var nameBytes = Encoding.UTF8.GetBytes(m.Name);
                ms.Write(nameBytes);
                ms.WriteByte(0);

                BinaryPrimitives.WriteUInt64LittleEndian(buf, m.Guid);
                ms.Write(buf, 0, 8);
                ms.WriteByte(m.Online);
                ms.WriteByte(m.Flags);
            }

            BinaryPrimitives.WriteUInt64LittleEndian(buf, leaderGuid);
            ms.Write(buf, 0, 8);

            if (members.Length > 0)
            {
                ms.WriteByte(lootMethod);
                BinaryPrimitives.WriteUInt64LittleEndian(buf, looterGuid);
                ms.Write(buf, 0, 8);
                ms.WriteByte(lootThreshold);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Builds an SMSG_PARTY_COMMAND_RESULT payload:
        /// operation(uint32) + memberName(CString) + result(uint32)
        /// </summary>
        private static byte[] BuildPartyCommandResultPayload(uint operation, string memberName, uint result)
        {
            using var ms = new MemoryStream();
            var buf = new byte[4];

            BinaryPrimitives.WriteUInt32LittleEndian(buf, operation);
            ms.Write(buf, 0, 4);

            ms.Write(Encoding.UTF8.GetBytes(memberName));
            ms.WriteByte(0);

            BinaryPrimitives.WriteUInt32LittleEndian(buf, result);
            ms.Write(buf, 0, 4);

            return ms.ToArray();
        }

        /// <summary>
        /// Builds a CString payload (name + null terminator).
        /// </summary>
        private static byte[] BuildCStringPayload(string text)
        {
            var nameBytes = Encoding.UTF8.GetBytes(text);
            var payload = new byte[nameBytes.Length + 1];
            Array.Copy(nameBytes, payload, nameBytes.Length);
            return payload;
        }

        /// <summary>
        /// Populates group members via SMSG_GROUP_LIST. Self becomes leader when
        /// leaderGuid doesn't match any member. Sets IsInGroup/IsGroupLeader.
        /// </summary>
        private void SetupGroupWithSelfAsLeader(bool isRaid, params (string Name, ulong Guid)[] members)
        {
            var memberData = members
                .Select(m => (m.Name, m.Guid, Online: (byte)1, Flags: (byte)0))
                .ToArray();

            // leaderGuid that doesn't match any member -> self is leader
            var payload = BuildGroupListPayload(
                (byte)(isRaid ? 1 : 0), 0, memberData, 0xDEADUL,
                lootMethod: 0, looterGuid: 0, lootThreshold: 0);

            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });
            GetSubject(Opcode.SMSG_GROUP_LIST).OnNext(new ReadOnlyMemory<byte>(payload));
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var agent = new PartyNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
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
            Assert.Throws<ArgumentNullException>(() =>
                new PartyNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PartyNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region CMSG_GROUP_INVITE — name\0

        [Fact]
        public async Task InvitePlayerAsync_SendsNamePlusNullTerminator()
        {
            const string name = "Gandalf";
            await _partyAgent.InvitePlayerAsync(name);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_INVITE,
                It.Is<byte[]>(d =>
                    d.Length == name.Length + 1 &&
                    Encoding.UTF8.GetString(d, 0, name.Length) == name &&
                    d[name.Length] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvitePlayerAsync_NullName_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _partyAgent.InvitePlayerAsync(null!));
        }

        [Fact]
        public async Task InvitePlayerAsync_EmptyName_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => _partyAgent.InvitePlayerAsync(""));
        }

        #endregion

        #region CMSG_GROUP_ACCEPT / DECLINE / CANCEL — empty payloads

        [Fact]
        public async Task AcceptInviteAsync_SendsEmptyPayload()
        {
            await _partyAgent.AcceptInviteAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_ACCEPT, It.Is<byte[]>(d => d.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.False(_partyAgent.HasPendingInvite);
        }

        [Fact]
        public async Task DeclineInviteAsync_SendsEmptyPayload()
        {
            await _partyAgent.DeclineInviteAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_DECLINE, It.Is<byte[]>(d => d.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelInviteAsync_SendsEmptyPayload()
        {
            await _partyAgent.CancelInviteAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_CANCEL, It.Is<byte[]>(d => d.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region CMSG_GROUP_UNINVITE / UNINVITE_GUID

        [Fact]
        public async Task KickPlayerAsync_ByName_SendsNamePlusNull()
        {
            const string name = "Saruman";
            await _partyAgent.KickPlayerAsync(name);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_UNINVITE,
                It.Is<byte[]>(d => d.Length == name.Length + 1 && d[d.Length - 1] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task KickPlayerAsync_ByGuid_Sends8ByteGuid()
        {
            const ulong guid = 0x0123456789ABCDEF;
            await _partyAgent.KickPlayerAsync(guid);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_UNINVITE_GUID,
                It.Is<byte[]>(d => d.Length == 8 && BitConverter.ToUInt64(d) == guid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task KickPlayerAsync_NullName_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _partyAgent.KickPlayerAsync((string)null!));
        }

        #endregion

        #region CMSG_GROUP_DISBAND — leave/disband

        [Fact]
        public async Task LeaveGroupAsync_SendsEmptyPayload()
        {
            await _partyAgent.LeaveGroupAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_DISBAND, It.Is<byte[]>(d => d.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisbandGroupAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.DisbandGroupAsync());
        }

        #endregion

        #region CMSG_GROUP_SET_LEADER — ObjectGuid(8) (1.12.1)

        [Fact]
        public async Task PromoteToLeaderAsync_ByGuid_Sends8ByteGuid()
        {
            const ulong targetGuid = 0xAABBCCDD11223344;
            SetupGroupWithSelfAsLeader(false, ("Target", targetGuid));

            await _partyAgent.PromoteToLeaderAsync(targetGuid);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_SET_LEADER,
                It.Is<byte[]>(d =>
                    d.Length == 8 &&
                    BitConverter.ToUInt64(d, 0) == targetGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteToLeaderAsync_ByName_LooksUpGuidAndSendsGuid8()
        {
            const string targetName = "Legolas";
            const ulong targetGuid = 0x1234;
            SetupGroupWithSelfAsLeader(false, (targetName, targetGuid));

            await _partyAgent.PromoteToLeaderAsync(targetName);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_SET_LEADER,
                It.Is<byte[]>(d =>
                    d.Length == 8 &&
                    BitConverter.ToUInt64(d, 0) == targetGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteToLeaderAsync_ByName_ThrowsWhenPlayerNotInGroup()
        {
            SetupGroupWithSelfAsLeader(false, ("Gimli", 0x5678));

            await Assert.ThrowsAsync<ArgumentException>(
                () => _partyAgent.PromoteToLeaderAsync("Unknown"));
        }

        [Fact]
        public async Task PromoteToLeaderAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.PromoteToLeaderAsync("Anyone"));
        }

        [Fact]
        public async Task PromoteToLeaderAsync_ByGuid_WhenNotLeader_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.PromoteToLeaderAsync(0x1234UL));
        }

        [Fact]
        public async Task PromoteToLeaderAsync_NullName_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _partyAgent.PromoteToLeaderAsync((string)null!));
        }

        #endregion

        #region CMSG_GROUP_ASSISTANT_LEADER — ObjectGuid(8) + flag(1) (1.12.1)

        [Fact]
        public async Task PromoteToAssistantAsync_SendsGuid8PlusFlag1()
        {
            const string targetName = "Aragorn";
            const ulong targetGuid = 0xABCD;
            SetupGroupWithSelfAsLeader(true, (targetName, targetGuid));

            await _partyAgent.PromoteToAssistantAsync(targetName);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_ASSISTANT_LEADER,
                It.Is<byte[]>(d =>
                    d.Length == 9 &&
                    BitConverter.ToUInt64(d, 0) == targetGuid &&
                    d[8] == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PromoteToAssistantAsync_ThrowsWhenPlayerNotInGroup()
        {
            SetupGroupWithSelfAsLeader(true, ("Boromir", 0x9999));

            await Assert.ThrowsAsync<ArgumentException>(
                () => _partyAgent.PromoteToAssistantAsync("NotInGroup"));
        }

        [Fact]
        public async Task PromoteToAssistantAsync_WhenNotInRaid_ThrowsInvalidOperationException()
        {
            SetupGroupWithSelfAsLeader(false, ("Someone", 0x111));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.PromoteToAssistantAsync("Someone"));
        }

        [Fact]
        public async Task PromoteToAssistantAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            // Not a leader, not in raid
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.PromoteToAssistantAsync("Someone"));
        }

        #endregion

        #region CMSG_LOOT_METHOD — uint32(4) + ObjectGuid(8) + uint32(4) = 16 bytes

        [Fact]
        public async Task SetLootMethodAsync_Sends16BytePayload()
        {
            SetupGroupWithSelfAsLeader(false, ("Tank", 0x100));

            await _partyAgent.SetLootMethodAsync(LootMethod.GroupLoot, null, ItemQuality.Rare);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_LOOT_METHOD,
                It.Is<byte[]>(d => d.Length == 16),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetLootMethodAsync_PayloadFieldVerification()
        {
            SetupGroupWithSelfAsLeader(false, ("Healer", 0x200));
            byte[]? captured = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_LOOT_METHOD, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, data, _) => captured = data)
                .Returns(Task.CompletedTask);

            await _partyAgent.SetLootMethodAsync(LootMethod.NeedBeforeGreed, null, ItemQuality.Epic);

            Assert.NotNull(captured);
            Assert.Equal(16, captured!.Length);

            // uint32 lootMethod at offset 0
            Assert.Equal((uint)LootMethod.NeedBeforeGreed,
                BinaryPrimitives.ReadUInt32LittleEndian(captured.AsSpan(0, 4)));
            // ObjectGuid at offset 4 (no master looter)
            Assert.Equal(0UL,
                BinaryPrimitives.ReadUInt64LittleEndian(captured.AsSpan(4, 8)));
            // uint32 lootThreshold at offset 12
            Assert.Equal((uint)ItemQuality.Epic,
                BinaryPrimitives.ReadUInt32LittleEndian(captured.AsSpan(12, 4)));
        }

        [Fact]
        public async Task SetLootMethodAsync_WithMasterLooterGuid_SetsGuidField()
        {
            const ulong masterGuid = 0xFEDCBA9876543210;
            SetupGroupWithSelfAsLeader(false, ("Tank", 0x100));
            byte[]? captured = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_LOOT_METHOD, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, data, _) => captured = data)
                .Returns(Task.CompletedTask);

            await _partyAgent.SetLootMethodAsync(LootMethod.MasterLooter, masterGuid, ItemQuality.Uncommon);

            Assert.NotNull(captured);
            Assert.Equal(masterGuid,
                BinaryPrimitives.ReadUInt64LittleEndian(captured!.AsSpan(4, 8)));
        }

        [Fact]
        public async Task SetLootMethodAsync_WhenNotLeader_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.SetLootMethodAsync(LootMethod.GroupLoot));
        }

        #endregion

        #region CMSG_REQUEST_PARTY_MEMBER_STATS — ObjectGuid(8) (1.12.1)

        [Fact]
        public async Task RequestPartyMemberStatsAsync_SendsGuid8()
        {
            const ulong memberGuid = 0x1122334455667788;
            await _partyAgent.RequestPartyMemberStatsAsync(memberGuid);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_REQUEST_PARTY_MEMBER_STATS,
                It.Is<byte[]>(d =>
                    d.Length == 8 &&
                    BitConverter.ToUInt64(d, 0) == memberGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region CMSG_GROUP_RAID_CONVERT / CHANGE_SUB_GROUP / SWAP_SUB_GROUP

        [Fact]
        public async Task ConvertToRaidAsync_WhenLeader_SendsEmptyPayload()
        {
            SetupGroupWithSelfAsLeader(false, ("DPS", 0x300));
            await _partyAgent.ConvertToRaidAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GROUP_RAID_CONVERT, It.Is<byte[]>(d => d.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConvertToRaidAsync_WhenNotLeader_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.ConvertToRaidAsync());
        }

        [Fact]
        public async Task ConvertToRaidAsync_WhenAlreadyRaid_Throws()
        {
            SetupGroupWithSelfAsLeader(true, ("DPS", 0x300));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _partyAgent.ConvertToRaidAsync());
        }

        [Fact]
        public async Task ChangeSubGroupAsync_SendsNameNullAndSubGroup()
        {
            const string name = "Frodo";
            const byte subGroup = 3;
            SetupGroupWithSelfAsLeader(true, (name, 0x400));

            byte[]? captured = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_GROUP_CHANGE_SUB_GROUP, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, data, _) => captured = data)
                .Returns(Task.CompletedTask);

            await _partyAgent.ChangeSubGroupAsync(name, subGroup);

            Assert.NotNull(captured);
            Assert.Equal(name.Length + 2, captured!.Length); // name + null + subgroup
            Assert.Equal(subGroup, captured[^1]);
        }

        [Fact]
        public async Task SwapSubGroupsAsync_SendsTwoNullTerminatedNames()
        {
            const string name1 = "Sam";
            const string name2 = "Pippin";
            SetupGroupWithSelfAsLeader(true, (name1, 0x500), (name2, 0x501));

            byte[]? captured = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_GROUP_SWAP_SUB_GROUP, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, data, _) => captured = data)
                .Returns(Task.CompletedTask);

            await _partyAgent.SwapSubGroupsAsync(name1, name2);

            Assert.NotNull(captured);
            Assert.Equal(name1.Length + 1 + name2.Length + 1, captured!.Length);
        }

        #endregion

        #region MSG_RAID_READY_CHECK

        [Fact]
        public async Task InitiateReadyCheckAsync_SendsEmptyPayload()
        {
            SetupGroupWithSelfAsLeader(false, ("Tank", 0x100));
            await _partyAgent.InitiateReadyCheckAsync();
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.MSG_RAID_READY_CHECK, It.Is<byte[]>(d => d.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RespondToReadyCheckAsync_SendsOneByte()
        {
            await _partyAgent.RespondToReadyCheckAsync(true);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.MSG_RAID_READY_CHECK_CONFIRM,
                It.Is<byte[]>(d => d.Length == 1 && d[0] == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RespondToReadyCheckAsync_NotReady_SendsZero()
        {
            await _partyAgent.RespondToReadyCheckAsync(false);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.MSG_RAID_READY_CHECK_CONFIRM,
                It.Is<byte[]>(d => d.Length == 1 && d[0] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region SMSG_GROUP_INVITE — ParseGroupInvite (CString)

        [Fact]
        public void ParseGroupInvite_ParsesInviterName()
        {
            string? inviter = null;
            using var sub = _partyAgent.PartyInvites.Subscribe(n => inviter = n);

            GetSubject(Opcode.SMSG_GROUP_INVITE)
                .OnNext(new ReadOnlyMemory<byte>(BuildCStringPayload("Thorin")));

            Assert.Equal("Thorin", inviter);
            Assert.True(_partyAgent.HasPendingInvite);
        }

        [Fact]
        public void ParseGroupInvite_EmptyPayload_ReturnsUnknown()
        {
            string? inviter = null;
            using var sub = _partyAgent.PartyInvites.Subscribe(n => inviter = n);

            GetSubject(Opcode.SMSG_GROUP_INVITE)
                .OnNext(ReadOnlyMemory<byte>.Empty);

            Assert.Equal("Unknown", inviter);
        }

        [Fact]
        public void ParseGroupInvite_NoNullTerminator_UsesEntireSpan()
        {
            string? inviter = null;
            using var sub = _partyAgent.PartyInvites.Subscribe(n => inviter = n);

            // No null terminator — parser should use entire span
            var raw = Encoding.UTF8.GetBytes("Balin");
            GetSubject(Opcode.SMSG_GROUP_INVITE)
                .OnNext(new ReadOnlyMemory<byte>(raw));

            Assert.Equal("Balin", inviter);
        }

        #endregion

        #region SMSG_GROUP_LIST — ParseGroupList (full member parse)

        [Fact]
        public void ParseGroupList_SingleMember_ParsesCorrectly()
        {
            (bool IsRaid, uint MemberCount)? result = null;
            using var sub = _partyAgent.GroupUpdates.Subscribe(r => result = r);

            var payload = BuildGroupListPayload(
                groupType: 0, ownFlags: 0,
                members: [("Dwalin", 0x100, 1, 0)],
                leaderGuid: 0xDEAD, // not matching any member -> self is leader
                lootMethod: 3, looterGuid: 0, lootThreshold: 2);

            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(result);
            Assert.False(result!.Value.IsRaid);
            Assert.Equal(1u, result!.Value.MemberCount);
            Assert.True(_partyAgent.IsInGroup);
            Assert.True(_partyAgent.IsGroupLeader);
        }

        [Fact]
        public void ParseGroupList_PopulatesGroupMembers()
        {
            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });

            var payload = BuildGroupListPayload(0, 0,
                [("Alpha", 0xA, 1, 0), ("Bravo", 0xB, 0, 1)],
                leaderGuid: 0xA,
                lootMethod: 0, looterGuid: 0, lootThreshold: 0);

            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            var members = _partyAgent.GetGroupMembers();
            Assert.Equal(2, members.Count);

            Assert.Equal("Alpha", members[0].Name);
            Assert.Equal(0xAUL, members[0].Guid);
            Assert.True(members[0].IsOnline);
            Assert.True(members[0].IsLeader);

            Assert.Equal("Bravo", members[1].Name);
            Assert.Equal(0xBUL, members[1].Guid);
            Assert.False(members[1].IsOnline);
            Assert.False(members[1].IsLeader);
        }

        [Fact]
        public void ParseGroupList_DetectsSelfAsLeader()
        {
            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });

            // leaderGuid doesn't match any member -> self is leader
            var payload = BuildGroupListPayload(0, 0,
                [("Other", 0x50, 1, 0)],
                leaderGuid: 0x999,
                lootMethod: 0, looterGuid: 0, lootThreshold: 0);

            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.True(_partyAgent.IsGroupLeader);
        }

        [Fact]
        public void ParseGroupList_DetectsOtherAsLeader()
        {
            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });

            // leaderGuid matches a member -> self is NOT leader
            var payload = BuildGroupListPayload(0, 0,
                [("Leader", 0x50, 1, 0)],
                leaderGuid: 0x50,
                lootMethod: 0, looterGuid: 0, lootThreshold: 0);

            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.False(_partyAgent.IsGroupLeader);
            Assert.True(_partyAgent.GetGroupMember("Leader")!.IsLeader);
        }

        [Fact]
        public void ParseGroupList_RaidType_SetsIsInRaid()
        {
            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });

            var payload = BuildGroupListPayload(1, 0,
                [("Raider", 0x60, 1, 2)],
                leaderGuid: 0xDEAD,
                lootMethod: 2, looterGuid: 0x60, lootThreshold: 3);

            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.True(_partyAgent.IsInRaid);
        }

        [Fact]
        public void ParseGroupList_ParsesLootMethod()
        {
            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });

            var payload = BuildGroupListPayload(0, 0,
                [("Tank", 0x70, 1, 0)],
                leaderGuid: 0xDEAD,
                lootMethod: (byte)LootMethod.MasterLooter,
                looterGuid: 0x70, lootThreshold: (byte)ItemQuality.Rare);

            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.Equal(LootMethod.MasterLooter, _partyAgent.CurrentLootMethod);
        }

        [Fact]
        public void ParseGroupList_EmptyGroup_NoMembersCleared()
        {
            using var sub = _partyAgent.GroupUpdates.Subscribe(_ => { });

            // First populate with members
            var payload1 = BuildGroupListPayload(0, 0,
                [("Old", 0x80, 1, 0)],
                leaderGuid: 0xDEAD,
                lootMethod: 0, looterGuid: 0, lootThreshold: 0);
            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload1));

            Assert.Single(_partyAgent.GetGroupMembers());

            // Now send empty group list (count=0, no loot block)
            var payload2 = BuildGroupListPayload(0, 0,
                [], leaderGuid: 0);
            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(payload2));

            Assert.Empty(_partyAgent.GetGroupMembers());
            Assert.False(_partyAgent.IsInGroup);
        }

        [Fact]
        public void ParseGroupList_TruncatedPayload_ReturnsDefaults()
        {
            (bool IsRaid, uint MemberCount)? result = null;
            using var sub = _partyAgent.GroupUpdates.Subscribe(r => result = r);

            // Only 3 bytes — too short for header
            GetSubject(Opcode.SMSG_GROUP_LIST)
                .OnNext(new ReadOnlyMemory<byte>(new byte[] { 0, 0, 0 }));

            // Should return defaults (no crash)
            Assert.NotNull(result);
        }

        #endregion

        #region SMSG_GROUP_SET_LEADER — ParseGroupSetLeader (CString, no GUID)

        [Fact]
        public void ParseGroupSetLeader_ParsesLeaderName()
        {
            (string Name, ulong Guid)? result = null;
            using var sub = _partyAgent.LeadershipChanges.Subscribe(c => result = c);

            GetSubject(Opcode.SMSG_GROUP_SET_LEADER)
                .OnNext(new ReadOnlyMemory<byte>(BuildCStringPayload("Elrond")));

            Assert.NotNull(result);
            Assert.Equal("Elrond", result!.Value.Name);
        }

        [Fact]
        public void ParseGroupSetLeader_LooksUpGuidFromGroupMembers()
        {
            // First populate group members
            SetupGroupWithSelfAsLeader(false, ("Elrond", 0xEEEE));

            (string Name, ulong Guid)? result = null;
            using var sub = _partyAgent.LeadershipChanges.Subscribe(c => result = c);

            GetSubject(Opcode.SMSG_GROUP_SET_LEADER)
                .OnNext(new ReadOnlyMemory<byte>(BuildCStringPayload("Elrond")));

            Assert.NotNull(result);
            Assert.Equal("Elrond", result!.Value.Name);
            Assert.Equal(0xEEEEUL, result!.Value.Guid);
        }

        [Fact]
        public void ParseGroupSetLeader_UnknownName_ReturnsZeroGuid()
        {
            (string Name, ulong Guid)? result = null;
            using var sub = _partyAgent.LeadershipChanges.Subscribe(c => result = c);

            GetSubject(Opcode.SMSG_GROUP_SET_LEADER)
                .OnNext(new ReadOnlyMemory<byte>(BuildCStringPayload("Nobody")));

            Assert.NotNull(result);
            Assert.Equal("Nobody", result!.Value.Name);
            Assert.Equal(0UL, result!.Value.Guid);
        }

        [Fact]
        public void ParseGroupSetLeader_EmptyPayload_ReturnsUnknown()
        {
            (string Name, ulong Guid)? result = null;
            using var sub = _partyAgent.LeadershipChanges.Subscribe(c => result = c);

            GetSubject(Opcode.SMSG_GROUP_SET_LEADER)
                .OnNext(ReadOnlyMemory<byte>.Empty);

            Assert.NotNull(result);
            Assert.Equal("Unknown", result!.Value.Name);
            Assert.Equal(0UL, result!.Value.Guid);
        }

        #endregion

        #region SMSG_PARTY_COMMAND_RESULT — operation(4) + CString + result(4)

        [Fact]
        public void ParsePartyCommandResult_ParsesWithCString()
        {
            (string Op, bool Success, uint Code)? result = null;
            using var sub = _partyAgent.PartyCommandResults.Subscribe(r => result = r);

            // operation=0 (Invite), name="TestPlayer", result=0 (OK)
            var payload = BuildPartyCommandResultPayload(0, "TestPlayer", 0);
            GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(result);
            Assert.Equal("Invite", result!.Value.Op);
            Assert.True(result!.Value.Success);
            Assert.Equal(0u, result!.Value.Code);
        }

        [Fact]
        public void ParsePartyCommandResult_FailureResult()
        {
            (string Op, bool Success, uint Code)? result = null;
            using var sub = _partyAgent.PartyCommandResults.Subscribe(r => result = r);

            // operation=0 (Invite), name="Target", result=6 (ALREADY_IN_GROUP)
            var payload = BuildPartyCommandResultPayload(0, "Target", 6);
            GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(result);
            Assert.False(result!.Value.Success);
            Assert.Equal(6u, result!.Value.Code);
        }

        [Fact]
        public void ParsePartyCommandResult_EmptyName_StillParses()
        {
            (string Op, bool Success, uint Code)? result = null;
            using var sub = _partyAgent.PartyCommandResults.Subscribe(r => result = r);

            // operation=1 (Leave), empty name, result=0 (OK)
            var payload = BuildPartyCommandResultPayload(1, "", 0);
            GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(result);
            Assert.Equal("Leave", result!.Value.Op);
            Assert.True(result!.Value.Success);
        }

        [Theory]
        [InlineData(0x00u, "Invite")]
        [InlineData(0x01u, "Leave")]
        [InlineData(0x02u, "SetLeader")]
        [InlineData(0x03u, "LootMethod")]
        [InlineData(0x04u, "ChangeSubGroup")]
        [InlineData(0xFFu, "Unknown(FF)")]
        public void ParsePartyCommandResult_AllOperations(uint opCode, string expectedName)
        {
            (string Op, bool Success, uint Code)? result = null;
            using var sub = _partyAgent.PartyCommandResults.Subscribe(r => result = r);

            var payload = BuildPartyCommandResultPayload(opCode, "X", 0);
            GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT)
                .OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(result);
            Assert.Equal(expectedName, result!.Value.Op);
        }

        [Fact]
        public void ParsePartyCommandResult_TooShortPayload_ReturnsUnknown()
        {
            (string Op, bool Success, uint Code)? result = null;
            using var sub = _partyAgent.PartyCommandResults.Subscribe(r => result = r);

            // Only 4 bytes — too short (needs at least 9: uint32 + \0 + uint32)
            GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT)
                .OnNext(new ReadOnlyMemory<byte>(new byte[] { 0, 0, 0, 0 }));

            Assert.NotNull(result);
            Assert.Equal("Unknown", result!.Value.Op);
            Assert.False(result!.Value.Success);
        }

        #endregion

        #region SMSG_GROUP_DESTROYED

        [Fact]
        public void GroupDestroyed_ClearsAllState()
        {
            // First set up a group
            SetupGroupWithSelfAsLeader(true, ("Member", 0x100));
            Assert.True(_partyAgent.IsInGroup);

            string? reason = null;
            using var sub = _partyAgent.GroupLeaves.Subscribe(r => reason = r);

            GetSubject(Opcode.SMSG_GROUP_DESTROYED)
                .OnNext(ReadOnlyMemory<byte>.Empty);

            Assert.Equal("Group disbanded", reason);
            Assert.False(_partyAgent.IsInGroup);
            Assert.False(_partyAgent.IsInRaid);
            Assert.False(_partyAgent.IsGroupLeader);
            Assert.Equal(0u, _partyAgent.GroupSize);
        }

        #endregion

        #region Utility Methods

        [Fact]
        public void GetGroupMembers_ReturnsEmptyList_WhenNoMembers()
        {
            Assert.Empty(_partyAgent.GetGroupMembers());
        }

        [Fact]
        public void IsPlayerInGroup_ByName_ReturnsFalseWhenNotInGroup()
        {
            Assert.False(_partyAgent.IsPlayerInGroup("Ghost"));
        }

        [Fact]
        public void IsPlayerInGroup_ByGuid_ReturnsFalseWhenNotInGroup()
        {
            Assert.False(_partyAgent.IsPlayerInGroup(0xDEADBEEF));
        }

        [Fact]
        public void IsPlayerInGroup_ByName_ReturnsTrueAfterGroupList()
        {
            SetupGroupWithSelfAsLeader(false, ("Visible", 0x123));
            Assert.True(_partyAgent.IsPlayerInGroup("Visible"));
        }

        [Fact]
        public void IsPlayerInGroup_ByGuid_ReturnsTrueAfterGroupList()
        {
            SetupGroupWithSelfAsLeader(false, ("Visible", 0x123));
            Assert.True(_partyAgent.IsPlayerInGroup(0x123));
        }

        [Fact]
        public void GetGroupMember_ByName_ReturnsNullWhenNotFound()
        {
            Assert.Null(_partyAgent.GetGroupMember("Nobody"));
        }

        [Fact]
        public void GetGroupMember_ByGuid_ReturnsNullWhenNotFound()
        {
            Assert.Null(_partyAgent.GetGroupMember(0xDEADUL));
        }

        [Fact]
        public void GetGroupMember_ByName_CaseInsensitive()
        {
            SetupGroupWithSelfAsLeader(false, ("Bilbo", 0x777));
            Assert.NotNull(_partyAgent.GetGroupMember("BILBO"));
            Assert.NotNull(_partyAgent.GetGroupMember("bilbo"));
        }

        #endregion

        #region Stream Resilience

        [Fact]
        public void Streams_HandleInvalidPayload_Gracefully()
        {
            Exception? thrown = null;
            using var sub1 = _partyAgent.PartyInvites.Subscribe(_ => { });
            using var sub2 = _partyAgent.GroupUpdates.Subscribe(_ => { });
            using var sub3 = _partyAgent.LeadershipChanges.Subscribe(_ => { });
            using var sub4 = _partyAgent.PartyCommandResults.Subscribe(_ => { });

            try
            {
                GetSubject(Opcode.SMSG_GROUP_INVITE).OnNext(ReadOnlyMemory<byte>.Empty);
                GetSubject(Opcode.SMSG_GROUP_LIST).OnNext(ReadOnlyMemory<byte>.Empty);
                GetSubject(Opcode.SMSG_GROUP_SET_LEADER).OnNext(ReadOnlyMemory<byte>.Empty);
                GetSubject(Opcode.SMSG_PARTY_COMMAND_RESULT).OnNext(ReadOnlyMemory<byte>.Empty);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.Null(thrown);
        }

        #endregion

        #region Dispose

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            _partyAgent.Dispose();
            _partyAgent.Dispose(); // should not throw
        }

        #endregion
    }
}
