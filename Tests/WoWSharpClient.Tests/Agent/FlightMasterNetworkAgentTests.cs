using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    public class FlightMasterNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<FlightMasterNetworkClientComponent>> _mockLogger;
        private readonly FlightMasterNetworkClientComponent _flightMasterAgent;

        public FlightMasterNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<FlightMasterNetworkClientComponent>>();
            _flightMasterAgent = new FlightMasterNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            Assert.NotNull(_flightMasterAgent);
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);
            Assert.Null(_flightMasterAgent.CurrentNodeId);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FlightMasterNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FlightMasterNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region CMSG Payload Tests

        [Fact]
        public async Task HelloFlightMasterAsync_ValidGuid_SendsCorrectPacket()
        {
            const ulong flightMasterGuid = 0x123456789ABCDEF0;
            await _flightMasterAgent.HelloFlightMasterAsync(flightMasterGuid);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.Is<byte[]>(p => p.Length == 8 && BitConverter.ToUInt64(p, 0) == flightMasterGuid),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QueryTaxiNodeStatusAsync_ValidGuid_SendsCorrectPacket()
        {
            const ulong flightMasterGuid = 0x123456789ABCDEF0;
            await _flightMasterAgent.QueryTaxiNodeStatusAsync(flightMasterGuid);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXINODE_STATUS_QUERY,
                    It.Is<byte[]>(p => p.Length == 8 && BitConverter.ToUInt64(p, 0) == flightMasterGuid),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ActivateFlightAsync_ValidParameters_SendsGuidPlusTwoUints()
        {
            const ulong guid = 0x123456789ABCDEF0;
            const uint src = 5;
            const uint dst = 10;

            await _flightMasterAgent.ActivateFlightAsync(guid, src, dst);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_ACTIVATETAXI,
                    It.Is<byte[]>(p =>
                        p.Length == 16 &&
                        BitConverter.ToUInt64(p, 0) == guid &&
                        BitConverter.ToUInt32(p, 8) == src &&
                        BitConverter.ToUInt32(p, 12) == dst),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ShowTaxiNodesAsync_NoParameters_SendsEmptyPacket()
        {
            await _flightMasterAgent.ShowTaxiNodesAsync();

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXISHOWNODES,
                    It.Is<byte[]>(p => p.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ClearAllNodesAsync_SendsCorrectPacket()
        {
            await _flightMasterAgent.ClearAllNodesAsync();

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXICLEARALLNODES,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnableNodeAsync_ValidNodeId_SendsCorrectPacket()
        {
            const uint nodeId = 42;
            await _flightMasterAgent.EnableNodeAsync(nodeId);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXIENABLENODE,
                    It.Is<byte[]>(p => p.Length == 4 && BitConverter.ToUInt32(p, 0) == nodeId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region ParseShowTaxiNodes (Bitmask) Tests

        [Fact]
        public void ParseShowTaxiNodes_CorrectBitmaskFormat_ParsesNodesFromBits()
        {
            // MaNGOS format: uint32(1) + ObjectGuid(8) + uint32(curloc) + bitmask[]
            const ulong guid = 0xDEADBEEF;
            const uint curLoc = 7;

            // Build bitmask: set bits for nodes 1, 5, 32, 33
            uint word0 = (1u << 1) | (1u << 5); // nodes 1, 5
            uint word1 = (1u << 0) | (1u << 1); // nodes 32, 33

            var payload = new byte[4 + 8 + 4 + 8];
            BitConverter.GetBytes(1u).CopyTo(payload, 0);      // show flag
            BitConverter.GetBytes(guid).CopyTo(payload, 4);    // flight master GUID
            BitConverter.GetBytes(curLoc).CopyTo(payload, 12); // current node
            BitConverter.GetBytes(word0).CopyTo(payload, 16);  // bitmask word 0
            BitConverter.GetBytes(word1).CopyTo(payload, 20);  // bitmask word 1

            var (parsedGuid, currentNodeId, nodes) = FlightMasterNetworkClientComponent.ParseShowTaxiNodesPacket(payload);

            Assert.Equal(guid, parsedGuid);
            Assert.Equal(curLoc, currentNodeId);
            Assert.Equal(4, nodes.Count);
            Assert.Contains(1u, nodes);
            Assert.Contains(5u, nodes);
            Assert.Contains(32u, nodes);
            Assert.Contains(33u, nodes);
        }

        [Fact]
        public void ParseShowTaxiNodes_EmptyBitmask_ReturnsEmptyNodes()
        {
            var payload = new byte[16]; // show(4) + guid(8) + curloc(4), no bitmask
            BitConverter.GetBytes(1u).CopyTo(payload, 0);
            BitConverter.GetBytes(0x1234UL).CopyTo(payload, 4);
            BitConverter.GetBytes(3u).CopyTo(payload, 12);

            var (parsedGuid, currentNodeId, nodes) = FlightMasterNetworkClientComponent.ParseShowTaxiNodesPacket(payload);

            Assert.Equal(0x1234UL, parsedGuid);
            Assert.Equal(3u, currentNodeId);
            Assert.Empty(nodes);
        }

        [Fact]
        public void ParseShowTaxiNodes_Node0IsSkipped()
        {
            var payload = new byte[20];
            BitConverter.GetBytes(1u).CopyTo(payload, 0);
            BitConverter.GetBytes(0x1111UL).CopyTo(payload, 4);
            BitConverter.GetBytes(1u).CopyTo(payload, 12);
            BitConverter.GetBytes(0x00000001u).CopyTo(payload, 16); // only bit 0 = node 0

            var (_, _, nodes) = FlightMasterNetworkClientComponent.ParseShowTaxiNodesPacket(payload);
            Assert.Empty(nodes); // node 0 filtered out
        }

        [Fact]
        public void ParseShowTaxiNodes_AllBitsSetInWord_Returns31Nodes()
        {
            var payload = new byte[20];
            BitConverter.GetBytes(1u).CopyTo(payload, 0);
            BitConverter.GetBytes(0x2222UL).CopyTo(payload, 4);
            BitConverter.GetBytes(5u).CopyTo(payload, 12);
            BitConverter.GetBytes(0xFFFFFFFFu).CopyTo(payload, 16);

            var (_, _, nodes) = FlightMasterNetworkClientComponent.ParseShowTaxiNodesPacket(payload);
            Assert.Equal(31, nodes.Count); // 32 bits minus node 0
            Assert.DoesNotContain(0u, nodes);
            Assert.Contains(1u, nodes);
            Assert.Contains(31u, nodes);
        }

        [Fact]
        public void ParseShowTaxiNodes_TooShort_ReturnsDefaults()
        {
            var (guid, currentNodeId, nodes) = FlightMasterNetworkClientComponent.ParseShowTaxiNodesPacket(new byte[10]);
            Assert.Equal(0UL, guid);
            Assert.Equal(0u, currentNodeId);
            Assert.Empty(nodes);
        }

        [Fact]
        public void ParseShowTaxiNodes_MultipleBitmaskWords_ParsesHighNodeIds()
        {
            // 3 bitmask words: nodes in word 2 = node IDs 64+
            var payload = new byte[4 + 8 + 4 + 12]; // header + 3 words
            BitConverter.GetBytes(1u).CopyTo(payload, 0);
            BitConverter.GetBytes(0xAABBUL).CopyTo(payload, 4);
            BitConverter.GetBytes(2u).CopyTo(payload, 12);
            BitConverter.GetBytes(0u).CopyTo(payload, 16);            // word 0: no nodes
            BitConverter.GetBytes(0u).CopyTo(payload, 20);            // word 1: no nodes
            BitConverter.GetBytes((1u << 3)).CopyTo(payload, 24);     // word 2: bit 3 = node 67

            var (_, _, nodes) = FlightMasterNetworkClientComponent.ParseShowTaxiNodesPacket(payload);
            Assert.Single(nodes);
            Assert.Contains(67u, nodes); // 2*32 + 3 = 67
        }

        [Fact]
        public void ParseShowTaxiNodes_ViaObservable_UpdatesCurrentNodeId()
        {
            var subject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(x => x.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_SHOWTAXINODES))
                .Returns(subject.AsObservable());

            (ulong FlightMasterGuid, IReadOnlyList<uint> Nodes)? received = null;
            var subscription = ((WoWSharpClient.Networking.ClientComponents.I.IFlightMasterNetworkClientComponent)_flightMasterAgent)
                .TaxiMapOpened
                .Subscribe(tuple => received = tuple);

            uint word0 = (1u << 2) | (1u << 10);
            var payload = new byte[20];
            BitConverter.GetBytes(1u).CopyTo(payload, 0);
            BitConverter.GetBytes(0xABCDUL).CopyTo(payload, 4);
            BitConverter.GetBytes(2u).CopyTo(payload, 12);
            BitConverter.GetBytes(word0).CopyTo(payload, 16);

            subject.OnNext(payload);

            Assert.True(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Equal(2, _flightMasterAgent.AvailableTaxiNodes.Count);
            Assert.True(_flightMasterAgent.IsNodeAvailable(2));
            Assert.True(_flightMasterAgent.IsNodeAvailable(10));
            Assert.Equal(2u, _flightMasterAgent.CurrentNodeId);
            Assert.NotNull(received);
            Assert.Equal(0xABCDUL, received.Value.FlightMasterGuid);

            subscription.Dispose();
        }

        #endregion

        #region ParseActivateTaxiReply Tests

        [Fact]
        public void ParseActivateTaxiReply_OkResult_ReturnsZero()
        {
            var payload = new byte[4];
            BitConverter.GetBytes(0u).CopyTo(payload, 0); // ERR_TAXIOK = 0

            var (result, _, _) = FlightMasterNetworkClientComponent.ParseActivateTaxiReply(payload);
            Assert.Equal(0u, result);
        }

        [Fact]
        public void ParseActivateTaxiReply_ErrorCode_ParsedCorrectly()
        {
            var payload = new byte[4];
            BitConverter.GetBytes(3u).CopyTo(payload, 0); // ERR_TAXINOTENOUGHMONEY = 3

            var (result, _, _) = FlightMasterNetworkClientComponent.ParseActivateTaxiReply(payload);
            Assert.Equal(3u, result);
        }

        [Fact]
        public void ParseActivateTaxiReply_EmptyPayload_ReturnsZeros()
        {
            var (result, _, _) = FlightMasterNetworkClientComponent.ParseActivateTaxiReply(Array.Empty<byte>());
            Assert.Equal(0u, result);
        }

        #endregion

        #region ParseTaxiNodeStatus Tests

        [Fact]
        public void ParseTaxiNodeStatus_FullPayload_ParsesStatus()
        {
            // MaNGOS format: ObjectGuid(8) + uint8(knownNode) = 9 bytes
            var payload = new byte[9];
            BitConverter.GetBytes(0x12345678ABCDUL).CopyTo(payload, 0);
            payload[8] = 1; // known = true

            var (_, status) = FlightMasterNetworkClientComponent.ParseTaxiNodeStatus(payload);
            Assert.Equal(1, status);
        }

        [Fact]
        public void ParseTaxiNodeStatus_UnknownNode_StatusZero()
        {
            var payload = new byte[9];
            BitConverter.GetBytes(0xFFFFUL).CopyTo(payload, 0);
            payload[8] = 0;

            var (_, status) = FlightMasterNetworkClientComponent.ParseTaxiNodeStatus(payload);
            Assert.Equal(0, status);
        }

        [Fact]
        public void ParseTaxiNodeStatus_ShortPayload_ReturnsDefaultStatus()
        {
            var payload = new byte[4];
            var (_, status) = FlightMasterNetworkClientComponent.ParseTaxiNodeStatus(payload);
            Assert.Equal(0, status);
        }

        #endregion

        #region State Management Tests

        [Fact]
        public void IsNodeAvailable_NodeNotAvailable_ReturnsFalse()
        {
            Assert.False(_flightMasterAgent.IsNodeAvailable(1));
        }

        [Fact]
        public void GetFlightCost_CostNotKnown_ReturnsNull()
        {
            Assert.Null(_flightMasterAgent.GetFlightCost(1));
        }

        [Fact]
        public void UpdateFlightCost_ValidData_UpdatesCost()
        {
            _flightMasterAgent.UpdateFlightCost(1, 500);
            Assert.Equal(500u, _flightMasterAgent.GetFlightCost(1));
        }

        [Fact]
        public void HandleFlightMasterError_NoThrow()
        {
            _flightMasterAgent.HandleFlightMasterError("Invalid destination");
        }

        [Fact]
        public async Task CloseTaxiMapAsync_WhenOpen_UpdatesState()
        {
            _flightMasterAgent.HandleTaxiMapOpened(0x123, new List<uint> { 1, 2, 3 });
            await _flightMasterAgent.CloseTaxiMapAsync();
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);
        }

        [Fact]
        public void TaxiMapClosed_OnDisconnect_UpdatesStateAndEmits()
        {
            var disconnectSubject = new Subject<Exception?>();
            _mockWorldClient.Setup(x => x.WhenDisconnected).Returns(disconnectSubject.AsObservable());

            bool closedEmitted = false;
            var subscription = ((WoWSharpClient.Networking.ClientComponents.I.IFlightMasterNetworkClientComponent)_flightMasterAgent)
                .TaxiMapClosed
                .Subscribe(_ => closedEmitted = true);

            _flightMasterAgent.HandleTaxiMapOpened(0x123, new List<uint> { 1 });
            disconnectSubject.OnNext(null);

            Assert.True(closedEmitted);
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);

            subscription.Dispose();
        }

        #endregion
    }
}
