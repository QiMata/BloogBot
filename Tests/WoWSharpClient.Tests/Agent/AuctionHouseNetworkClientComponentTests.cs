using Microsoft.Extensions.Logging;
using Moq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for the AuctionHouseNetworkClientComponent functionality.
    /// </summary>
    public class AuctionHouseNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<AuctionHouseNetworkClientComponent>> _mockLogger;
        private readonly AuctionHouseNetworkClientComponent _auctionHouseClientComponent;

        public AuctionHouseNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<AuctionHouseNetworkClientComponent>>();
            _auctionHouseClientComponent = new AuctionHouseNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var agent = new AuctionHouseNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.False(agent.IsAuctionHouseOpen);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuctionHouseNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuctionHouseNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Open Auction House Tests

        [Fact]
        public async Task OpenAuctionHouseAsync_WithValidGuid_ShouldSendCorrectPacket()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;

            // Act
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(auctioneerGuid);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            _mockWorldClient.Setup(client => client.SendOpcodeAsync(
                    It.IsAny<GameData.Core.Enums.Opcode>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Network error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _auctionHouseClientComponent.OpenAuctionHouseAsync(auctioneerGuid));
        }

        #endregion

        #region Close Auction House Tests

        [Fact]
        public async Task CloseAuctionHouseAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            var disconnectSubject = new Subject<Exception?>();
            _mockWorldClient.SetupGet(c => c.WhenDisconnected).Returns(disconnectSubject.AsObservable());

            bool eventFired = false;
            using var sub = _auctionHouseClientComponent.AuctionHouseClosedStream.Subscribe(_ => eventFired = true);

            // Act
            await _auctionHouseClientComponent.CloseAuctionHouseAsync();
            // Drive the closed stream via disconnect emission
            disconnectSubject.OnNext(null);

            // Assert
            Assert.True(eventFired);
            Assert.False(_auctionHouseClientComponent.IsAuctionHouseOpen);
        }

        #endregion

        #region Search Auctions Tests

        [Fact]
        public async Task SearchAuctionsAsync_WithBasicSearch_ShouldSendCorrectPacket()
        {
            // Arrange
            string itemName = "Sword";

            // Act
            await _auctionHouseClientComponent.SearchAuctionsAsync(itemName);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.SearchAuctionsAsync(itemName, levelMin, levelMax, category, subCategory, quality, usable);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.GetOwnedAuctionsAsync();

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.GetBidderAuctionsAsync();

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.PlaceBidAsync(auctionId, bidAmount);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.PostAuctionAsync(bagId, slotId, startBid, buyoutPrice, duration);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.CancelAuctionAsync(auctionId);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.BuyoutAuctionAsync(auctionId, buyoutPrice);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            bool result = _auctionHouseClientComponent.IsAuctionHouseOpenWith(auctioneerGuid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenOpenWithDifferentGuid_ShouldReturnFalse()
        {
            // Arrange
            ulong auctioneerGuid1 = 0x123456789ABCDEF0UL;
            ulong auctioneerGuid2 = 0xFEDCBA9876543210UL;
            _auctionHouseClientComponent.HandleAuctionHouseOpened(auctioneerGuid1);

            // Act
            bool result = _auctionHouseClientComponent.IsAuctionHouseOpenWith(auctioneerGuid2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenOpenWithSameGuid_ShouldReturnTrue()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            _auctionHouseClientComponent.HandleAuctionHouseOpened(auctioneerGuid);

            // Act
            bool result = _auctionHouseClientComponent.IsAuctionHouseOpenWith(auctioneerGuid);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region Event/Stream Tests Driven By Mocked WorldClient

        [Fact]
        public void HandleAuctionHouseOpened_ShouldUpdateState_AndStreamFiresWhenListResultsArrive()
        {
            // Arrange
            ulong auctioneerGuid = 0x123456789ABCDEF0UL;
            var listResultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_LIST_RESULT))
                            .Returns(listResultSubject.AsObservable());

            bool eventFired = false;
            ulong receivedGuid = 0;
            using var sub = _auctionHouseClientComponent.AuctionHouseOpenedStream.Subscribe(guid => { eventFired = true; receivedGuid = guid; });

            // Act: set state to known auctioneer, then simulate server list result
            _auctionHouseClientComponent.HandleAuctionHouseOpened(auctioneerGuid);
            listResultSubject.OnNext(new ReadOnlyMemory<byte>(new byte[] { 0 }));

            // Assert
            Assert.True(eventFired);
            Assert.Equal(auctioneerGuid, receivedGuid);
            Assert.True(_auctionHouseClientComponent.IsAuctionHouseOpen);
        }

        [Fact]
        public void HandleAuctionSearchResults_ShouldUpdateStateOnly()
        {
            // Arrange
            var auctions = new List<AuctionData>
            {
                new AuctionData(1, 12345, 1, 0, 0x100, 1000, 1500, 5000, 3600, 0x200, 2000),
                new AuctionData(2, 67890, 5, 0, 0x300, 500, 600, 1000, 7200, 0x400, 750)
            };

            // Act
            _auctionHouseClientComponent.HandleAuctionSearchResults(auctions);

            // Assert: verify state captured
            Assert.Equal(2, (_auctionHouseClientComponent as dynamic).CurrentAuctions.Count);
        }

        [Fact]
        public void BidPlacedStream_ShouldFire_WhenCommandResultArrives()
        {
            // Arrange
            var commandSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_COMMAND_RESULT))
                            .Returns(commandSubject.AsObservable());

            uint auctionId = 12345;
            uint bidAmount = 50000;
            bool eventFired = false;
            uint receivedAuctionId = 0;
            uint receivedBidAmount = 0;
            using var sub = _auctionHouseClientComponent.BidPlacedStream.Subscribe(data =>
            {
                eventFired = true;
                receivedAuctionId = data.AuctionId;
                receivedBidAmount = data.BidAmount;
            });

            // Build payload matching ParseCommandResult heuristic
            var payload = new byte[13];
            payload[0] = 0; // success
            BitConverter.GetBytes((uint)AuctionOperationType.PlaceBid).CopyTo(payload, 1);
            BitConverter.GetBytes(auctionId).CopyTo(payload, 5);
            BitConverter.GetBytes(bidAmount).CopyTo(payload, 9);

            // Act
            commandSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(eventFired);
            Assert.Equal(auctionId, receivedAuctionId);
            Assert.Equal(bidAmount, receivedBidAmount);
        }

        [Fact]
        public void AuctionOperationFailedStream_ShouldFire_WhenFailureCommandArrives()
        {
            // Arrange
            var commandSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_COMMAND_RESULT))
                            .Returns(commandSubject.AsObservable());

            bool eventFired = false;
            AuctionOperationError? received = null;
            using var sub = _auctionHouseClientComponent.AuctionOperationFailedStream.Subscribe(err => { eventFired = true; received = err; });

            // Build failure payload (non-zero result byte)
            var payload = new byte[9];
            payload[0] = 1; // failure
            BitConverter.GetBytes((uint)AuctionOperationType.PlaceBid).CopyTo(payload, 1);
            BitConverter.GetBytes((uint)1111).CopyTo(payload, 5);

            // Act
            commandSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(received);
            Assert.Equal(AuctionOperationType.PlaceBid, received!.Operation);
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
            await _auctionHouseClientComponent.QuickSearchAsync(auctioneerGuid, itemName);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.QuickPostAsync(auctioneerGuid, bagId, slotId, startBid, buyoutPrice, duration);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
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
            await _auctionHouseClientComponent.QuickBuyoutAsync(auctioneerGuid, auctionId, buyoutPrice);

            // Assert
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_PLACE_BID,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion
    }
}