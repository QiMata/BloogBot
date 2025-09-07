using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class FlightMasterNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<FlightMasterNetworkAgent>> _mockLogger;
        private readonly FlightMasterNetworkAgent _flightMasterAgent;

        public FlightMasterNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<FlightMasterNetworkAgent>>();
            _flightMasterAgent = new FlightMasterNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
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
            Assert.Throws<ArgumentNullException>(() => new FlightMasterNetworkAgent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FlightMasterNetworkAgent(_mockWorldClient.Object, null!));
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
                x => x.SendMovementAsync(
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
                x => x.SendMovementAsync(
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
                x => x.SendMovementAsync(
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
                x => x.SendMovementAsync(
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
        public void HandleTaxiMapOpened_ValidData_UpdatesState()
        {
            // Arrange
            const ulong flightMasterGuid = 0x123456789ABCDEF0;
            var availableNodes = new List<uint> { 1, 2, 3 };
            var eventFired = false;
            ulong? receivedGuid = null;
            IReadOnlyList<uint>? receivedNodes = null;

            _flightMasterAgent.TaxiMapOpened += (guid, nodes) =>
            {
                eventFired = true;
                receivedGuid = guid;
                receivedNodes = nodes;
            };

            // Act
            _flightMasterAgent.HandleTaxiMapOpened(flightMasterGuid, availableNodes);

            // Assert
            Assert.True(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Equal(availableNodes.Count, _flightMasterAgent.AvailableTaxiNodes.Count);
            Assert.True(_flightMasterAgent.IsNodeAvailable(1));
            Assert.True(_flightMasterAgent.IsNodeAvailable(2));
            Assert.True(_flightMasterAgent.IsNodeAvailable(3));
            Assert.True(eventFired);
            Assert.Equal(flightMasterGuid, receivedGuid);
            Assert.Equal(availableNodes, receivedNodes);
        }

        [Fact]
        public void HandleFlightActivated_ValidData_FiresEvent()
        {
            // Arrange
            const uint sourceNodeId = 1;
            const uint destinationNodeId = 2;
            const uint cost = 500;
            var eventFired = false;
            uint? receivedSource = null;
            uint? receivedDestination = null;
            uint? receivedCost = null;

            _flightMasterAgent.FlightActivated += (source, dest, c) =>
            {
                eventFired = true;
                receivedSource = source;
                receivedDestination = dest;
                receivedCost = c;
            };

            // Act
            _flightMasterAgent.HandleFlightActivated(sourceNodeId, destinationNodeId, cost);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(sourceNodeId, receivedSource);
            Assert.Equal(destinationNodeId, receivedDestination);
            Assert.Equal(cost, receivedCost);
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
        public void HandleFlightMasterError_ValidMessage_FiresEvent()
        {
            // Arrange
            const string errorMessage = "Invalid destination";
            var eventFired = false;
            string? receivedError = null;

            _flightMasterAgent.FlightMasterError += (error) =>
            {
                eventFired = true;
                receivedError = error;
            };

            // Act
            _flightMasterAgent.HandleFlightMasterError(errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(errorMessage, receivedError);
        }

        [Fact]
        public async Task CloseTaxiMapAsync_WhenOpen_UpdatesState()
        {
            // Arrange
            var availableNodes = new List<uint> { 1, 2, 3 };
            _flightMasterAgent.HandleTaxiMapOpened(0x123, availableNodes);
            var eventFired = false;

            _flightMasterAgent.TaxiMapClosed += () => eventFired = true;

            // Act
            await _flightMasterAgent.CloseTaxiMapAsync();

            // Assert
            Assert.False(_flightMasterAgent.IsTaxiMapOpen);
            Assert.Empty(_flightMasterAgent.AvailableTaxiNodes);
            Assert.True(eventFired);
        }

        [Fact]
        public async Task ClearAllNodesAsync_ValidCall_SendsCorrectPacket()
        {
            // Act
            await _flightMasterAgent.ClearAllNodesAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_TAXIENABLENODE,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}