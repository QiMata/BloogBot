using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class VendorNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<VendorNetworkAgent>> _mockLogger;
        private readonly VendorNetworkAgent _vendorAgent;

        public VendorNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<VendorNetworkAgent>>();
            _vendorAgent = new VendorNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Assert
            Assert.NotNull(_vendorAgent);
            Assert.False(_vendorAgent.IsVendorWindowOpen);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VendorNetworkAgent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VendorNetworkAgent(_mockWorldClient.Object, null!));
        }

        [Fact]
        public async Task OpenVendorAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;

            // Act
            await _vendorAgent.OpenVendorAsync(vendorGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task BuyItemAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;
            const uint quantity = 5;

            // Act
            await _vendorAgent.BuyItemAsync(vendorGuid, itemId, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_BUY_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SellItemAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const byte bagId = 0;
            const byte slotId = 1;
            const uint quantity = 1;

            // Act
            await _vendorAgent.SellItemAsync(vendorGuid, bagId, slotId, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_SELL_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RepairAllItemsAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;

            // Act
            await _vendorAgent.RepairAllItemsAsync(vendorGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_REPAIR_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void IsVendorOpen_NoVendorOpen_ReturnsFalse()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;

            // Act
            var result = _vendorAgent.IsVendorOpen(vendorGuid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HandleVendorWindowOpened_ValidGuid_UpdatesState()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            var eventFired = false;
            ulong? receivedGuid = null;

            _vendorAgent.VendorWindowOpened += (guid) =>
            {
                eventFired = true;
                receivedGuid = guid;
            };

            // Act
            _vendorAgent.HandleVendorWindowOpened(vendorGuid);

            // Assert
            Assert.True(_vendorAgent.IsVendorWindowOpen);
            Assert.True(_vendorAgent.IsVendorOpen(vendorGuid));
            Assert.True(eventFired);
            Assert.Equal(vendorGuid, receivedGuid);
        }

        [Fact]
        public void HandleItemPurchased_ValidData_FiresEvent()
        {
            // Arrange
            const uint itemId = 929;
            const uint quantity = 5;
            const uint cost = 250;
            var eventFired = false;
            uint? receivedItemId = null;
            uint? receivedQuantity = null;
            uint? receivedCost = null;

            _vendorAgent.ItemPurchased += (id, qty, c) =>
            {
                eventFired = true;
                receivedItemId = id;
                receivedQuantity = qty;
                receivedCost = c;
            };

            // Act
            _vendorAgent.HandleItemPurchased(itemId, quantity, cost);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(itemId, receivedItemId);
            Assert.Equal(quantity, receivedQuantity);
            Assert.Equal(cost, receivedCost);
        }

        [Fact]
        public void HandleVendorError_ValidMessage_FiresEvent()
        {
            // Arrange
            const string errorMessage = "Insufficient funds";
            var eventFired = false;
            string? receivedError = null;

            _vendorAgent.VendorError += (error) =>
            {
                eventFired = true;
                receivedError = error;
            };

            // Act
            _vendorAgent.HandleVendorError(errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(errorMessage, receivedError);
        }

        [Fact]
        public async Task QuickBuyAsync_ValidParameters_CallsMultipleMethods()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;
            const uint quantity = 5;

            // Act
            await _vendorAgent.QuickBuyAsync(vendorGuid, itemId, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_LIST_INVENTORY,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_BUY_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}