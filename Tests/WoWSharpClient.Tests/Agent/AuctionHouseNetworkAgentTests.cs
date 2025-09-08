using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for the AuctionHouseNetworkAgent functionality.
    /// </summary>
    public class AuctionHouseNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<AuctionHouseNetworkAgent>> _mockLogger;
        private readonly AuctionHouseNetworkAgent _auctionHouseAgent;

        public AuctionHouseNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<AuctionHouseNetworkAgent>>();
            _auctionHouseAgent = new AuctionHouseNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var agent = new AuctionHouseNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.False(agent.IsAuctionHouseOpen);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuctionHouseNetworkAgent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuctionHouseNetworkAgent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Open Auction House Tests

        [Fact]
        public async Task OpenAuctionHouseAsync_WithValidGuid_ShouldSendCorrectPacket()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;

            // Act
            await _auctionHouseAgent.OpenAuctionHouseAsync(auctioneerGuid);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.Is<byte[]>(data => data.Length == 8 && BitConverter.ToUInt64(data, 0) == auctioneerGuid),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task OpenAuctionHouseAsync_WhenWorldClientThrows_ShouldRethrowException()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            _mockWorldClient.Setup(client => client.SendMovementAsync(
                    It.IsAny<GameData.Core.Enums.Opcode>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Network error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _auctionHouseAgent.OpenAuctionHouseAsync(auctioneerGuid));
        }

        #endregion

        #region Close Auction House Tests

        [Fact]
        public async Task CloseAuctionHouseAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            bool eventFired = false;
            _auctionHouseAgent.AuctionHouseClosed += () => eventFired = true;

            // Act
            await _auctionHouseAgent.CloseAuctionHouseAsync();

            // Assert
            Assert.True(eventFired);
            Assert.False(_auctionHouseAgent.IsAuctionHouseOpen);
        }

        #endregion

        #region Search Auctions Tests

        [Fact]
        public async Task SearchAuctionsAsync_WithBasicSearch_ShouldSendCorrectPacket()
        {
            // Arrange
            string itemName = "Sword";

            // Act
            await _auctionHouseAgent.SearchAuctionsAsync(itemName);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_ITEMS,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SearchAuctionsAsync_WithAllParameters_ShouldSendCorrectPacket()
        {
            // Arrange
            string itemName = "Epic Sword";
            uint levelMin = 60;
            uint levelMax = 70;
            uint category = 2;
            uint subCategory = 7;
            var quality = AuctionQuality.Epic;
            bool usable = true;

            // Act
            await _auctionHouseAgent.SearchAuctionsAsync(itemName, levelMin, levelMax, category, subCategory, quality, usable);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_ITEMS,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Get Owned Auctions Tests

        [Fact]
        public async Task GetOwnedAuctionsAsync_ShouldSendCorrectPacket()
        {
            // Act
            await _auctionHouseAgent.GetOwnedAuctionsAsync();

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_OWNER_ITEMS,
                    It.Is<byte[]>(data => data.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Get Bidder Auctions Tests

        [Fact]
        public async Task GetBidderAuctionsAsync_ShouldSendCorrectPacket()
        {
            // Act
            await _auctionHouseAgent.GetBidderAuctionsAsync();

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_BIDDER_ITEMS,
                    It.Is<byte[]>(data => data.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Place Bid Tests

        [Fact]
        public async Task PlaceBidAsync_WithValidParameters_ShouldSendCorrectPacket()
        {
            // Arrange
            uint auctionId = 12345;
            uint bidAmount = 50000; // 5 gold

            // Act
            await _auctionHouseAgent.PlaceBidAsync(auctionId, bidAmount);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_PLACE_BID,
                    It.Is<byte[]>(data => data.Length == 8 &&
                        BitConverter.ToUInt32(data, 0) == auctionId &&
                        BitConverter.ToUInt32(data, 4) == bidAmount),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Post Auction Tests

        [Fact]
        public async Task PostAuctionAsync_WithValidParameters_ShouldSendCorrectPacket()
        {
            // Arrange
            byte bagId = 0;
            byte slotId = 15;
            uint startBid = 10000; // 1 gold
            uint buyoutPrice = 50000; // 5 gold
            var duration = AuctionDuration.TwentyFourHours;

            // Act
            await _auctionHouseAgent.PostAuctionAsync(bagId, slotId, startBid, buyoutPrice, duration);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_SELL_ITEM,
                    It.Is<byte[]>(data => data.Length == 14 &&
                        data[0] == bagId &&
                        data[1] == slotId &&
                        BitConverter.ToUInt32(data, 2) == startBid &&
                        BitConverter.ToUInt32(data, 6) == buyoutPrice &&
                        BitConverter.ToUInt32(data, 10) == (uint)duration),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Cancel Auction Tests

        [Fact]
        public async Task CancelAuctionAsync_WithValidAuctionId_ShouldSendCorrectPacket()
        {
            // Arrange
            uint auctionId = 12345;

            // Act
            await _auctionHouseAgent.CancelAuctionAsync(auctionId);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_REMOVE_ITEM,
                    It.Is<byte[]>(data => data.Length == 4 &&
                        BitConverter.ToUInt32(data, 0) == auctionId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Buyout Auction Tests

        [Fact]
        public async Task BuyoutAuctionAsync_WithValidParameters_ShouldCallPlaceBid()
        {
            // Arrange
            uint auctionId = 12345;
            uint buyoutPrice = 100000; // 10 gold

            // Act
            await _auctionHouseAgent.BuyoutAuctionAsync(auctionId, buyoutPrice);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_PLACE_BID,
                    It.Is<byte[]>(data => data.Length == 8 &&
                        BitConverter.ToUInt32(data, 0) == auctionId &&
                        BitConverter.ToUInt32(data, 4) == buyoutPrice),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenNotOpen_ShouldReturnFalse()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;

            // Act
            bool result = _auctionHouseAgent.IsAuctionHouseOpenWith(auctioneerGuid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenOpenWithDifferentGuid_ShouldReturnFalse()
        {
            // Arrange
            ulong auctioneerGuid1 = 0x123456789ABCDEF0UL;
            ulong auctioneerGuid2 = 0xFEDCBA9876543210UL;
            _auctionHouseAgent.HandleAuctionHouseOpened(auctioneerGuid1);

            // Act
            bool result = _auctionHouseAgent.IsAuctionHouseOpenWith(auctioneerGuid2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenOpenWithSameGuid_ShouldReturnTrue()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            _auctionHouseAgent.HandleAuctionHouseOpened(auctioneerGuid);

            // Act
            bool result = _auctionHouseAgent.IsAuctionHouseOpenWith(auctioneerGuid);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region Event Handler Tests

        [Fact]
        public void HandleAuctionHouseOpened_ShouldUpdateStateAndFireEvent()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            bool eventFired = false;
            ulong receivedGuid = 0;

            _auctionHouseAgent.AuctionHouseOpened += (guid) =>
            {
                eventFired = true;
                receivedGuid = guid;
            };

            // Act
            _auctionHouseAgent.HandleAuctionHouseOpened(auctioneerGuid);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(auctioneerGuid, receivedGuid);
            Assert.True(_auctionHouseAgent.IsAuctionHouseOpen);
        }

        [Fact]
        public void HandleAuctionSearchResults_ShouldFireEvent()
        {
            // Arrange
            var auctions = new List<AuctionData>
            {
                new AuctionData(1, 12345, 1, 0, 0x100, 1000, 1500, 5000, 3600, 0x200, 2000),
                new AuctionData(2, 67890, 5, 0, 0x300, 500, 600, 1000, 7200, 0x400, 750)
            };

            bool eventFired = false;
            IReadOnlyList<AuctionData>? receivedAuctions = null;

            _auctionHouseAgent.AuctionSearchResults += (results) =>
            {
                eventFired = true;
                receivedAuctions = results;
            };

            // Act
            _auctionHouseAgent.HandleAuctionSearchResults(auctions);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(receivedAuctions);
            Assert.Equal(2, receivedAuctions.Count);
            Assert.Equal(auctions[0].AuctionId, receivedAuctions[0].AuctionId);
            Assert.Equal(auctions[1].ItemId, receivedAuctions[1].ItemId);
        }

        [Fact]
        public void HandleBidPlaced_ShouldFireEvent()
        {
            // Arrange
            uint auctionId = 12345;
            uint bidAmount = 50000;
            bool eventFired = false;
            uint receivedAuctionId = 0;
            uint receivedBidAmount = 0;

            _auctionHouseAgent.BidPlaced += (id, amount) =>
            {
                eventFired = true;
                receivedAuctionId = id;
                receivedBidAmount = amount;
            };

            // Act
            _auctionHouseAgent.HandleBidPlaced(auctionId, bidAmount);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(auctionId, receivedAuctionId);
            Assert.Equal(bidAmount, receivedBidAmount);
        }

        [Fact]
        public void HandleAuctionOperationFailed_ShouldFireEvent()
        {
            // Arrange
            var operation = AuctionOperationType.PlaceBid;
            string errorReason = "Insufficient funds";
            bool eventFired = false;
            AuctionOperationType receivedOperation = AuctionOperationType.Search;
            string receivedError = "";

            _auctionHouseAgent.AuctionOperationFailed += (op, error) =>
            {
                eventFired = true;
                receivedOperation = op;
                receivedError = error;
            };

            // Act
            _auctionHouseAgent.HandleAuctionOperationFailed(operation, errorReason);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(operation, receivedOperation);
            Assert.Equal(errorReason, receivedError);
        }

        #endregion

        #region Quick Methods Tests

        [Fact]
        public async Task QuickSearchAsync_ShouldPerformCompleteWorkflow()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            string itemName = "Sword";

            // Act
            await _auctionHouseAgent.QuickSearchAsync(auctioneerGuid, itemName);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_ITEMS,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QuickPostAsync_ShouldPerformCompleteWorkflow()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            byte bagId = 0;
            byte slotId = 15;
            uint startBid = 10000;
            uint buyoutPrice = 50000;
            var duration = AuctionDuration.TwentyFourHours;

            // Act
            await _auctionHouseAgent.QuickPostAsync(auctioneerGuid, bagId, slotId, startBid, buyoutPrice, duration);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_SELL_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QuickBuyoutAsync_ShouldPerformCompleteWorkflow()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            uint auctionId = 12345;
            uint buyoutPrice = 100000;

            // Act
            await _auctionHouseAgent.QuickBuyoutAsync(auctioneerGuid, auctionId, buyoutPrice);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                client => client.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_PLACE_BID,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion
    }
}