using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests.Agent
{
    public class BattlegroundNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<BattlegroundNetworkClientComponent>> _mockLogger;
        private readonly Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> _opcodeSubjects = new();
        private readonly List<(Opcode Opcode, byte[] Payload)> _sentOpcodes = [];
        private readonly BattlegroundNetworkClientComponent _battlegroundAgent;

        public BattlegroundNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<BattlegroundNetworkClientComponent>>();

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

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) =>
                    _sentOpcodes.Add((opcode, payload.ToArray())))
                .Returns(Task.CompletedTask);

            _battlegroundAgent = new BattlegroundNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task DeclineInviteAsync_SendsBattlefieldPortWithMapIdAndZeroAction()
        {
            PublishStatus(mapId: 529u, statusId: BattlegroundStatusId.WaitJoin);

            await _battlegroundAgent.DeclineInviteAsync();

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_BATTLEFIELD_PORT,
                It.Is<byte[]>(payload =>
                    payload.Length == 5
                    && BitConverter.ToUInt32(payload, 0) == 529u
                    && payload[4] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeaveAsync_WhenQueued_SendsBattlefieldPortWithZeroAction()
        {
            PublishStatus(mapId: 489u, statusId: BattlegroundStatusId.Queued);

            await _battlegroundAgent.LeaveAsync();

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_BATTLEFIELD_PORT,
                It.Is<byte[]>(payload =>
                    payload.Length == 5
                    && BitConverter.ToUInt32(payload, 0) == 489u
                    && payload[4] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_LEAVE_BATTLEFIELD,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LeaveAsync_WhenInBattleground_SendsLeaveBattlefieldOpcode()
        {
            PublishStatus(mapId: 30u, statusId: BattlegroundStatusId.InProgress);

            await _battlegroundAgent.LeaveAsync();

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_LEAVE_BATTLEFIELD,
                It.Is<byte[]>(payload => payload.Length == 2),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_BATTLEFIELD_PORT,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LeaveAsync_WhenStateUnknown_RequestsStatusAndClearsKnownQueueMapsBeforeLeaveFallback()
        {
            await _battlegroundAgent.LeaveAsync();

            Assert.Contains(_sentOpcodes, sent => sent.Opcode == Opcode.CMSG_BATTLEFIELD_STATUS && sent.Payload.Length == 0);

            var queueClears = _sentOpcodes
                .Where(sent => sent.Opcode == Opcode.CMSG_BATTLEFIELD_PORT)
                .Select(sent => (MapId: BitConverter.ToUInt32(sent.Payload, 0), Action: sent.Payload[4]))
                .ToList();

            Assert.Equal(3, queueClears.Count);
            Assert.Equal(
                new[] { (30u, (byte)0), (489u, (byte)0), (529u, (byte)0) },
                queueClears.Select(clear => (clear.MapId, clear.Action)).ToArray());

            Assert.Contains(_sentOpcodes, sent => sent.Opcode == Opcode.CMSG_LEAVE_BATTLEFIELD && sent.Payload.Length == 2);
        }

        [Fact]
        public async Task LeaveAsync_WhenStatusRefreshFindsQueuedState_ClearsReportedQueueWithoutFallbackSweep()
        {
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_BATTLEFIELD_STATUS, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => PublishStatus(mapId: 529u, statusId: BattlegroundStatusId.Queued))
                .Returns(Task.CompletedTask);

            await _battlegroundAgent.LeaveAsync();

            var queueClears = _sentOpcodes
                .Where(sent => sent.Opcode == Opcode.CMSG_BATTLEFIELD_PORT)
                .Select(sent => (MapId: BitConverter.ToUInt32(sent.Payload, 0), Action: sent.Payload[4]))
                .ToList();

            Assert.Single(queueClears);
            Assert.Equal((529u, (byte)0), queueClears[0]);
            Assert.DoesNotContain(_sentOpcodes, sent => sent.Opcode == Opcode.CMSG_LEAVE_BATTLEFIELD);
        }

        [Fact]
        public async Task GroupJoinedBattlegroundAck_TracksQueuedGroupedJoinState()
        {
            await _battlegroundAgent.JoinQueueAsync(489u, asGroup: true, battleMasterGuid: 0xF130000F3200129DUL);

            GetSubject(Opcode.SMSG_GROUP_JOINED_BATTLEGROUND).OnNext(ReadOnlyMemory<byte>.Empty);

            Assert.Equal(BattlegroundState.Queued, _battlegroundAgent.CurrentState);
            Assert.Equal(489u, _battlegroundAgent.CurrentBgTypeId);
        }

        [Fact]
        public async Task LeaveAsync_AfterGroupJoinedAck_ClearsKnownQueuedMapWithoutStatusRefresh()
        {
            await _battlegroundAgent.JoinQueueAsync(529u, asGroup: true, battleMasterGuid: 0xF130003A8500D556UL);
            GetSubject(Opcode.SMSG_GROUP_JOINED_BATTLEGROUND).OnNext(ReadOnlyMemory<byte>.Empty);
            _sentOpcodes.Clear();

            await _battlegroundAgent.LeaveAsync();

            var queueClears = _sentOpcodes
                .Where(sent => sent.Opcode == Opcode.CMSG_BATTLEFIELD_PORT)
                .Select(sent => (MapId: BitConverter.ToUInt32(sent.Payload, 0), Action: sent.Payload[4]))
                .ToList();

            Assert.Single(queueClears);
            Assert.Equal((529u, (byte)0), queueClears[0]);
            Assert.DoesNotContain(_sentOpcodes, sent => sent.Opcode == Opcode.CMSG_BATTLEFIELD_STATUS);
            Assert.DoesNotContain(_sentOpcodes, sent => sent.Opcode == Opcode.CMSG_LEAVE_BATTLEFIELD);
        }

        [Fact]
        public void BattlegroundPlayerJoined_EmitsPackedPlayerGuid()
        {
            ulong? joinedGuid = null;
            using var subscription = _battlegroundAgent.BattlegroundPlayerJoined.Subscribe(guid => joinedGuid = guid);

            GetSubject(Opcode.SMSG_BATTLEGROUND_PLAYER_JOINED).OnNext(
                new ReadOnlyMemory<byte>(BuildPackedGuidPayload(0xF1300006DC00291FUL)));

            Assert.Equal(0xF1300006DC00291FUL, joinedGuid);
        }

        [Fact]
        public void BattlegroundPlayerLeft_EmitsPackedPlayerGuid()
        {
            ulong? leftGuid = null;
            using var subscription = _battlegroundAgent.BattlegroundPlayerLeft.Subscribe(guid => leftGuid = guid);

            GetSubject(Opcode.SMSG_BATTLEGROUND_PLAYER_LEFT).OnNext(
                new ReadOnlyMemory<byte>(BuildPackedGuidPayload(0x0000000000000049UL)));

            Assert.Equal(0x0000000000000049UL, leftGuid);
        }

        private void PublishStatus(uint mapId, BattlegroundStatusId statusId)
        {
            GetSubject(Opcode.SMSG_BATTLEFIELD_STATUS).OnNext(new ReadOnlyMemory<byte>(
                BuildBattlefieldStatusPayload(queueSlot: 1u, mapId, statusId)));
        }

        private Subject<ReadOnlyMemory<byte>> GetSubject(Opcode op)
        {
            if (!_opcodeSubjects.TryGetValue(op, out var subject))
            {
                subject = new Subject<ReadOnlyMemory<byte>>();
                _opcodeSubjects[op] = subject;
            }

            return subject;
        }

        private static byte[] BuildBattlefieldStatusPayload(uint queueSlot, uint mapId, BattlegroundStatusId statusId)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[4];

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, queueSlot);
            ms.Write(buffer, 0, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, mapId);
            ms.Write(buffer, 0, 4);
            ms.WriteByte(0); // bracketId
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, 0);
            ms.Write(buffer, 0, 4); // clientInstanceId
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)statusId);
            ms.Write(buffer, 0, 4);

            return ms.ToArray();
        }

        private static byte[] BuildPackedGuidPayload(ulong guid)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            ReaderUtils.WritePackedGuid(writer, guid);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
