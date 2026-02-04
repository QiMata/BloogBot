using Microsoft.Extensions.Logging;
using Moq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Assert
            Assert.NotNull(_flightMasterAgent);
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FlightMasterNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FlightMasterNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        [Fact]
        public async Task HelloFlightMasterAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong flightMasterGuid = 0x123456789ABCDEF0;

            // Act
            await _flightMasterAgent.HelloFlightMasterAsync(flightMasterGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QueryTaxiNodeStatusAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong flightMasterGuid = 0x123456789ABCDEF0;

            // Act
            await _flightMasterAgent.QueryTaxiNodeStatusAsync(flightMasterGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXINODE_STATUS_QUERY,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ActivateFlightAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong flightMasterGuid = 0x123456789ABCDEF0;
            const uint sourceNodeId = 1;
            const uint destinationNodeId = 2;

            // Act
            await _flightMasterAgent.ActivateFlightAsync(flightMasterGuid, sourceNodeId, destinationNodeId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_ACTIVATETAXI,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ShowTaxiNodesAsync_NoParameters_SendsCorrectPacket()
        {
            // Act
            await _flightMasterAgent.ShowTaxiNodesAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXISHOWNODES,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void IsNodeAvailable_NodeNotAvailable_ReturnsFalse()
        {
            // Arrange
            const uint nodeId = 1;

            // Act
            var result = _flightMasterAgent.IsNodeAvailable(nodeId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetFlightCost_CostNotKnown_ReturnsNull()
        {
            // Arrange
            const uint destinationNodeId = 1;

            // Act
            var result = _flightMasterAgent.GetFlightCost(destinationNodeId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TaxiMapOpened_OpcodeStream_UpdatesStateAndEmits()
        {
            // Arrange
            const ulong flightMasterGuid = 0x123456789ABCDEF0;
            var availableNodes = new List<uint> { 1, 2, 3 };

            var subject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(x => x.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_SHOWTAXINODES))
                .Returns(subject.AsObservable());

            (ulong FlightMasterGuid, IReadOnlyList<uint> Nodes)? received = null;
            var subscription = ((WoWSharpClient.Networking.ClientComponents.I.IFlightMasterNetworkClientComponent)_flightMasterAgent)
                .TaxiMapOpened
                .Subscribe(tuple => received = tuple);

            // Build payload: GUID (8 bytes) + count (4 bytes) + nodes (uint each)
            var payload = new byte[8 + 4 + 3 * 4];
            BitConverter.GetBytes(flightMasterGuid).CopyTo(payload, 0);
            BitConverter.GetBytes((uint)availableNodes.Count).CopyTo(payload, 8);
            BitConverter.GetBytes(availableNodes[0]).CopyTo(payload, 12);
            BitConverter.GetBytes(availableNodes[1]).CopyTo(payload, 16);
            BitConverter.GetBytes(availableNodes[2]).CopyTo(payload, 20);

            // Act
            subject.OnNext(payload);

            // Assert
            Assert.True(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Equal(availableNodes.Count, _flightMasterAgent.AvailableTaxiNodes.Count);
            Assert.True(_flightMasterAgent.IsNodeAvailable(1));
            Assert.True(_flightMasterAgent.IsNodeAvailable(2));
            Assert.True(_flightMasterAgent.IsNodeAvailable(3));
            Assert.NotNull(received);
            Assert.Equal(flightMasterGuid, received.Value.FlightMasterGuid);
            Assert.Equal(availableNodes, received.Value.Nodes);

            subscription.Dispose();
        }

        [Fact]
        public void FlightActivated_OpcodeStream_Emits()
        {
            // Arrange
            const uint sourceNodeId = 1;
            const uint destinationNodeId = 2;
            const uint cost = 500;

            var subject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(x => x.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_ACTIVATETAXIREPLY))
                .Returns(subject.AsObservable());

            (uint Source, uint Dest, uint Cost)? received = null;
            var subscription = ((WoWSharpClient.Networking.ClientComponents.I.IFlightMasterNetworkClientComponent)_flightMasterAgent)
                .FlightActivated
                .Subscribe(tuple => received = tuple);

            // Build payload: src(4), dst(4), cost(4)
            var payload = new byte[12];
            BitConverter.GetBytes(sourceNodeId).CopyTo(payload, 0);
            BitConverter.GetBytes(destinationNodeId).CopyTo(payload, 4);
            BitConverter.GetBytes(cost).CopyTo(payload, 8);

            // Act
            subject.OnNext(payload);

            // Assert
            Assert.NotNull(received);
            Assert.Equal(sourceNodeId, received.Value.Source);
            Assert.Equal(destinationNodeId, received.Value.Dest);
            Assert.Equal(cost, received.Value.Cost);

            subscription.Dispose();
        }

        [Fact]
        public void UpdateFlightCost_ValidData_UpdatesCost()
        {
            // Arrange
            const uint destinationNodeId = 1;
            const uint cost = 500;

            // Act
            _flightMasterAgent.UpdateFlightCost(destinationNodeId, cost);

            // Assert
            Assert.Equal(cost, _flightMasterAgent.GetFlightCost(destinationNodeId));
        }

        [Fact]
        public void HandleFlightMasterError_NoThrow()
        {
            // Arrange
            const string errorMessage = "Invalid destination";

            // Act & Assert
            _flightMasterAgent.HandleFlightMasterError(errorMessage);
        }

        [Fact]
        public async Task CloseTaxiMapAsync_WhenOpen_UpdatesState()
        {
            // Arrange: open via compat method
            var availableNodes = new List<uint> { 1, 2, 3 };
            _flightMasterAgent.HandleTaxiMapOpened(0x123, availableNodes);

            // Act
            await _flightMasterAgent.CloseTaxiMapAsync();

            // Assert
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);
        }

        [Fact]
        public async Task ClearAllNodesAsync_ValidCall_SendsCorrectPacket()
        {
            // Act
            await _flightMasterAgent.ClearAllNodesAsync();

            // Assert
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
            // Arrange
            const uint nodeId = 1;

            // Act
            await _flightMasterAgent.EnableNodeAsync(nodeId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXIENABLENODE,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void TaxiMapClosed_OnDisconnect_UpdatesStateAndEmits()
        {
            // Arrange
            var disconnectSubject = new Subject<Exception?>();
            _mockWorldClient.Setup(x => x.WhenDisconnected).Returns(disconnectSubject.AsObservable());

            bool closedEmitted = false;
            var subscription = ((WoWSharpClient.Networking.ClientComponents.I.IFlightMasterNetworkClientComponent)_flightMasterAgent)
                .TaxiMapClosed
                .Subscribe(_ => closedEmitted = true);

            // Open state
            _flightMasterAgent.HandleTaxiMapOpened(0x123, new List<uint> { 1 });

            // Act
            disconnectSubject.OnNext(null);

            // Assert
            Assert.True(closedEmitted);
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);

            subscription.Dispose();
        }
    }
}