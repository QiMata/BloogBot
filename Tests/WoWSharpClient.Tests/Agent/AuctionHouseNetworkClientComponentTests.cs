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
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for the AuctionHouseNetworkClientComponent functionality.
    /// Validates correct CMSG packet formats and SMSG parsing per MaNGOS 1.12.1 protocol.
    /// </summary>
    public class AuctionHouseNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<AuctionHouseNetworkClientComponent>> _mockLogger;
        private readonly AuctionHouseNetworkClientComponent _auctionHouseClientComponent;
        private const ulong TestAuctioneerGuid = 0x123456789ABCDEF0UL;

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
            var agent = new AuctionHouseNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            Assert.NotNull(agent);
            Assert.False(agent.IsAuctionHouseOpen);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AuctionHouseNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AuctionHouseNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Open/Close Auction House Tests

        [Fact]
        public async Task OpenAuctionHouseAsync_WithValidGuid_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.MSG_AUCTION_HELLO,
                    It.Is<byte[]>(data => data.Length == 8 && BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CloseAuctionHouseAsync_ShouldCompleteSuccessfully()
        {
            var disconnectSubject = new Subject<Exception?>();
            _mockWorldClient.SetupGet(c => c.WhenDisconnected).Returns(disconnectSubject.AsObservable());

            bool eventFired = false;
            using var sub = _auctionHouseClientComponent.AuctionHouseClosedStream.Subscribe(_ => eventFired = true);

            await _auctionHouseClientComponent.CloseAuctionHouseAsync();
            disconnectSubject.OnNext(null);

            Assert.True(eventFired);
            Assert.False(_auctionHouseClientComponent.IsAuctionHouseOpen);
        }

        #endregion

        #region Search Auctions Tests (CMSG_AUCTION_LIST_ITEMS)

        [Fact]
        public async Task SearchAuctionsAsync_WithBasicSearch_ShouldSendCorrectPacket()
        {
            // Must open auction house first (auctioneerGuid required)
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            await _auctionHouseClientComponent.SearchAuctionsAsync("Sword");

            // auctioneerGuid(8) + listfrom(4) + "Sword\0"(6) + levelmin(1) + levelmax(1) + invType(4) + class(4) + subclass(4) + quality(4) + usable(1)
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_ITEMS,
                    It.Is<byte[]>(data =>
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == 0 && // listfrom = 0
                        data[12] == (byte)'S' && data[17] == 0 && // "Sword\0" (5 chars + null at [17])
                        data[18] == 0 && data[19] == 0 // levelmin=0, levelmax=0
                    ),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SearchAuctionsAsync_WithAllParameters_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            await _auctionHouseClientComponent.SearchAuctionsAsync(
                "Epic Sword", levelMin: 60, levelMax: 70, inventoryType: 0, itemClass: 2,
                itemSubClass: 7, quality: AuctionQuality.Epic, usable: true, listOffset: 50);

            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_ITEMS,
                    It.Is<byte[]>(data =>
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == 50 // listfrom = 50
                    ),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Get Owned Auctions Tests (CMSG_AUCTION_LIST_OWNER_ITEMS)

        [Fact]
        public async Task GetOwnedAuctionsAsync_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);
            await _auctionHouseClientComponent.GetOwnedAuctionsAsync();

            // Format: auctioneerGuid(8) + listfrom(4) = 12 bytes
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_OWNER_ITEMS,
                    It.Is<byte[]>(data => data.Length == 12 &&
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Get Bidder Auctions Tests (CMSG_AUCTION_LIST_BIDDER_ITEMS)

        [Fact]
        public async Task GetBidderAuctionsAsync_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);
            await _auctionHouseClientComponent.GetBidderAuctionsAsync();

            // Format: auctioneerGuid(8) + listfrom(4) + outbiddedCount(4) = 16 bytes
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_LIST_BIDDER_ITEMS,
                    It.Is<byte[]>(data => data.Length == 16 &&
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == 0 &&
                        BitConverter.ToUInt32(data, 12) == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Place Bid Tests (CMSG_AUCTION_PLACE_BID)

        [Fact]
        public async Task PlaceBidAsync_WithValidParameters_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            uint auctionId = 12345;
            uint bidAmount = 50000;

            await _auctionHouseClientComponent.PlaceBidAsync(auctionId, bidAmount);

            // Format: auctioneerGuid(8) + auctionId(4) + price(4) = 16 bytes
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_PLACE_BID,
                    It.Is<byte[]>(data => data.Length == 16 &&
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == auctionId &&
                        BitConverter.ToUInt32(data, 12) == bidAmount),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Post Auction Tests (CMSG_AUCTION_SELL_ITEM)

        [Fact]
        public async Task PostAuctionAsync_WithValidParameters_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            ulong itemGuid = 0xDEADBEEF12345678UL;
            uint startBid = 10000;
            uint buyoutPrice = 50000;
            var duration = AuctionDuration.TwentyFourHours;

            await _auctionHouseClientComponent.PostAuctionAsync(itemGuid, startBid, buyoutPrice, duration);

            // Format: auctioneerGuid(8) + itemGuid(8) + bid(4) + buyout(4) + etime(4) = 28 bytes
            // etime = 24 * 60 = 1440 minutes
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_SELL_ITEM,
                    It.Is<byte[]>(data => data.Length == 28 &&
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt64(data, 8) == itemGuid &&
                        BitConverter.ToUInt32(data, 16) == startBid &&
                        BitConverter.ToUInt32(data, 20) == buyoutPrice &&
                        BitConverter.ToUInt32(data, 24) == 1440), // 24h × 60 = 1440 min
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Cancel Auction Tests (CMSG_AUCTION_REMOVE_ITEM)

        [Fact]
        public async Task CancelAuctionAsync_WithValidAuctionId_ShouldSendCorrectPacket()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            uint auctionId = 12345;

            await _auctionHouseClientComponent.CancelAuctionAsync(auctionId);

            // Format: auctioneerGuid(8) + auctionId(4) = 12 bytes
            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_REMOVE_ITEM,
                    It.Is<byte[]>(data => data.Length == 12 &&
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == auctionId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Buyout Auction Tests

        [Fact]
        public async Task BuyoutAuctionAsync_WithValidParameters_ShouldCallPlaceBid()
        {
            await _auctionHouseClientComponent.OpenAuctionHouseAsync(TestAuctioneerGuid);

            uint auctionId = 12345;
            uint buyoutPrice = 100000;

            await _auctionHouseClientComponent.BuyoutAuctionAsync(auctionId, buyoutPrice);

            _mockWorldClient.Verify(
                client => client.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_AUCTION_PLACE_BID,
                    It.Is<byte[]>(data => data.Length == 16 &&
                        BitConverter.ToUInt64(data, 0) == TestAuctioneerGuid &&
                        BitConverter.ToUInt32(data, 8) == auctionId &&
                        BitConverter.ToUInt32(data, 12) == buyoutPrice),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenNotOpen_ShouldReturnFalse()
        {
            Assert.False(_auctionHouseClientComponent.IsAuctionHouseOpenWith(TestAuctioneerGuid));
        }

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenOpenWithDifferentGuid_ShouldReturnFalse()
        {
            _auctionHouseClientComponent.HandleAuctionHouseOpened(TestAuctioneerGuid);
            Assert.False(_auctionHouseClientComponent.IsAuctionHouseOpenWith(0xFEDCBA9876543210UL));
        }

        [Fact]
        public void IsAuctionHouseOpenWith_WithGuid_WhenOpenWithSameGuid_ShouldReturnTrue()
        {
            _auctionHouseClientComponent.HandleAuctionHouseOpened(TestAuctioneerGuid);
            Assert.True(_auctionHouseClientComponent.IsAuctionHouseOpenWith(TestAuctioneerGuid));
        }

        #endregion

        #region SMSG Parsing Tests

        [Fact]
        public void HandleAuctionHouseOpened_ShouldUpdateState_AndStreamFiresWhenListResultsArrive()
        {
            var listResultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_LIST_RESULT))
                            .Returns(listResultSubject.AsObservable());

            bool eventFired = false;
            ulong receivedGuid = 0;
            using var sub = _auctionHouseClientComponent.AuctionHouseOpenedStream.Subscribe(guid => { eventFired = true; receivedGuid = guid; });

            _auctionHouseClientComponent.HandleAuctionHouseOpened(TestAuctioneerGuid);

            // Send a minimal valid auction list payload: itemCount=0(4) + totalCount=0(4)
            var payload = new byte[8];
            listResultSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.True(eventFired);
            Assert.Equal(TestAuctioneerGuid, receivedGuid);
            Assert.True(_auctionHouseClientComponent.IsAuctionHouseOpen);
        }

        [Fact]
        public void ParseAuctionList_WithValidPayload_ShouldReturnCorrectAuctions()
        {
            var listResultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_LIST_RESULT))
                            .Returns(listResultSubject.AsObservable());

            IReadOnlyList<AuctionData>? received = null;
            using var sub = _auctionHouseClientComponent.AuctionSearchResultsStream.Subscribe(list => received = list);

            // Build a payload with 1 auction entry (64 bytes) + header (4) + footer (4) = 72 bytes
            var payload = new byte[72];
            int offset = 0;

            // itemCount = 1
            BitConverter.GetBytes(1u).CopyTo(payload, offset); offset += 4;

            // Entry: auctionId=100, itemEntry=12345, enchantId=77, randPropId=0, suffixFactor=0,
            // count=5, charges=0, ownerGuid=0x100, startBid=1000, minOutbid=100,
            // buyout=5000, timeLeftMs=3600000, bidderGuid=0x200, currentBid=2000
            BitConverter.GetBytes(100u).CopyTo(payload, offset); offset += 4;   // auctionId
            BitConverter.GetBytes(12345u).CopyTo(payload, offset); offset += 4; // itemEntry
            BitConverter.GetBytes(77u).CopyTo(payload, offset); offset += 4;    // enchantmentId
            BitConverter.GetBytes(0u).CopyTo(payload, offset); offset += 4;     // randomPropertyId
            BitConverter.GetBytes(0u).CopyTo(payload, offset); offset += 4;     // suffixFactor
            BitConverter.GetBytes(5u).CopyTo(payload, offset); offset += 4;     // count
            BitConverter.GetBytes(0u).CopyTo(payload, offset); offset += 4;     // spellCharges
            BitConverter.GetBytes(0x100UL).CopyTo(payload, offset); offset += 8; // ownerGuid
            BitConverter.GetBytes(1000u).CopyTo(payload, offset); offset += 4;  // startBid
            BitConverter.GetBytes(100u).CopyTo(payload, offset); offset += 4;   // minOutbid
            BitConverter.GetBytes(5000u).CopyTo(payload, offset); offset += 4;  // buyoutPrice
            BitConverter.GetBytes(3600000u).CopyTo(payload, offset); offset += 4; // timeLeftMs
            BitConverter.GetBytes(0x200UL).CopyTo(payload, offset); offset += 8; // bidderGuid
            BitConverter.GetBytes(2000u).CopyTo(payload, offset); offset += 4;  // currentBid

            // totalCount = 42
            BitConverter.GetBytes(42u).CopyTo(payload, offset);

            listResultSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Single(received!);
            var auction = received![0];
            Assert.Equal(100u, auction.AuctionId);
            Assert.Equal(12345u, auction.ItemEntry);
            Assert.Equal(77u, auction.EnchantmentId);
            Assert.Equal(5u, auction.ItemCount);
            Assert.Equal(0x100UL, auction.OwnerGuid);
            Assert.Equal(1000u, auction.StartBid);
            Assert.Equal(100u, auction.MinOutbid);
            Assert.Equal(5000u, auction.BuyoutPrice);
            Assert.Equal(3600000u, auction.TimeLeftMs);
            Assert.Equal(0x200UL, auction.BidderGuid);
            Assert.Equal(2000u, auction.CurrentBid);
            Assert.Equal(42u, _auctionHouseClientComponent.TotalSearchResultCount);
        }

        [Fact]
        public void ParseCommandResult_SuccessfulBid_ShouldFireBidPlacedStream()
        {
            var commandSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_COMMAND_RESULT))
                            .Returns(commandSubject.AsObservable());

            BidPlacedData? received = null;
            using var sub = _auctionHouseClientComponent.BidPlacedStream.Subscribe(data => received = data);

            // SMSG_AUCTION_COMMAND_RESULT format: auctionId(4) + action(4) + error(4) + [outbid(4)]
            var payload = new byte[16];
            BitConverter.GetBytes(12345u).CopyTo(payload, 0);           // auctionId
            BitConverter.GetBytes((uint)AuctionAction.BidPlaced).CopyTo(payload, 4); // action=2
            BitConverter.GetBytes((uint)AuctionError.Ok).CopyTo(payload, 8);         // error=0
            BitConverter.GetBytes(500u).CopyTo(payload, 12);            // outbid amount

            commandSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Equal(12345u, received!.AuctionId);
            Assert.Equal(500u, received.OutbidAmount);
        }

        [Fact]
        public void AuctionOperationFailedStream_ShouldFire_WhenFailureCommandArrives()
        {
            var commandSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_COMMAND_RESULT))
                            .Returns(commandSubject.AsObservable());

            AuctionOperationError? received = null;
            using var sub = _auctionHouseClientComponent.AuctionOperationFailedStream.Subscribe(err => received = err);

            // Build failure payload: auctionId(4) + action(4) + error(4)
            var payload = new byte[12];
            BitConverter.GetBytes(1111u).CopyTo(payload, 0);                    // auctionId
            BitConverter.GetBytes((uint)AuctionAction.BidPlaced).CopyTo(payload, 4); // action
            BitConverter.GetBytes((uint)AuctionError.NotEnoughMoney).CopyTo(payload, 8); // error=2

            commandSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Equal(AuctionAction.BidPlaced, received!.Action);
            Assert.Equal(AuctionError.NotEnoughMoney, received.Error);
        }

        [Fact]
        public void AuctionPostedStream_ShouldFire_WhenAuctionCreatedSuccessfully()
        {
            var commandSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_COMMAND_RESULT))
                            .Returns(commandSubject.AsObservable());

            AuctionPostedData? received = null;
            using var sub = _auctionHouseClientComponent.AuctionPostedStream.Subscribe(data => received = data);

            // AUCTION_STARTED(0) + OK(0)
            var payload = new byte[12];
            BitConverter.GetBytes(999u).CopyTo(payload, 0);                      // auctionId
            BitConverter.GetBytes((uint)AuctionAction.Started).CopyTo(payload, 4); // action=0
            BitConverter.GetBytes((uint)AuctionError.Ok).CopyTo(payload, 8);      // error=0

            commandSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Equal(999u, received!.AuctionId);
        }

        [Fact]
        public void AuctionRemovedStream_ShouldFire_WhenRemovedNotificationArrives()
        {
            var removedSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_REMOVED_NOTIFICATION))
                            .Returns(removedSubject.AsObservable());

            AuctionNotificationData? received = null;
            using var sub = _auctionHouseClientComponent.AuctionRemovedStream.Subscribe(data => received = data);

            // SMSG_AUCTION_REMOVED_NOTIFICATION: auctionId(4) + itemEntry(4) + randomPropertyId(4) = 12 bytes
            var payload = new byte[12];
            BitConverter.GetBytes(555u).CopyTo(payload, 0);  // auctionId
            BitConverter.GetBytes(4321u).CopyTo(payload, 4); // itemEntry
            BitConverter.GetBytes(99u).CopyTo(payload, 8);   // randomPropertyId

            removedSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Equal(555u, received!.AuctionId);
            Assert.Equal(4321u, received.ItemEntry);
            Assert.Equal(99u, received.RandomPropertyId);
        }

        [Fact]
        public void AuctionNotificationStream_BidderNotification_ParsesCorrectly()
        {
            var bidderSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_BIDDER_NOTIFICATION))
                            .Returns(bidderSubject.AsObservable());
            // Must also set up owner notification to avoid null in Observable.Merge
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_OWNER_NOTIFICATION))
                            .Returns(Observable.Empty<ReadOnlyMemory<byte>>());

            AuctionNotificationData? received = null;
            using var sub = _auctionHouseClientComponent.AuctionNotificationStream.Subscribe(data => received = data);

            // SMSG_AUCTION_BIDDER_NOTIFICATION (32 bytes):
            // houseId(4) + auctionId(4) + bidderGuid(8) + outbidAmount(4) + minOutbid(4) + itemEntry(4) + randPropId(4)
            var payload = new byte[32];
            BitConverter.GetBytes(1u).CopyTo(payload, 0);        // houseId
            BitConverter.GetBytes(777u).CopyTo(payload, 4);      // auctionId
            BitConverter.GetBytes(0x300UL).CopyTo(payload, 8);   // bidderGuid
            BitConverter.GetBytes(250u).CopyTo(payload, 16);     // outbidAmount (>0 = outbid)
            BitConverter.GetBytes(50u).CopyTo(payload, 20);      // minOutbid
            BitConverter.GetBytes(8888u).CopyTo(payload, 24);    // itemEntry
            BitConverter.GetBytes(0u).CopyTo(payload, 28);       // randomPropertyId

            bidderSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Equal(AuctionNotificationType.Outbid, received!.NotificationType);
            Assert.Equal(777u, received.AuctionId);
            Assert.Equal(8888u, received.ItemEntry);

            var bidder = Assert.IsType<AuctionBidderNotificationData>(received);
            Assert.Equal(250u, bidder.OutbidAmount);
        }

        [Fact]
        public void AuctionNotificationStream_OwnerNotification_Sold_ParsesCorrectly()
        {
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_BIDDER_NOTIFICATION))
                            .Returns(Observable.Empty<ReadOnlyMemory<byte>>());
            var ownerSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(GameData.Core.Enums.Opcode.SMSG_AUCTION_OWNER_NOTIFICATION))
                            .Returns(ownerSubject.AsObservable());

            AuctionNotificationData? received = null;
            using var sub = _auctionHouseClientComponent.AuctionNotificationStream.Subscribe(data => received = data);

            // SMSG_AUCTION_OWNER_NOTIFICATION (28 bytes):
            // auctionId(4) + bidAmount(4) + minOutbid(4) + buyerGuid(8) + itemEntry(4) + randPropId(4)
            var payload = new byte[28];
            BitConverter.GetBytes(888u).CopyTo(payload, 0);      // auctionId
            BitConverter.GetBytes(50000u).CopyTo(payload, 4);    // bidAmount (>0 = sold)
            BitConverter.GetBytes(500u).CopyTo(payload, 8);      // minOutbid
            BitConverter.GetBytes(0x400UL).CopyTo(payload, 12);  // buyerGuid
            BitConverter.GetBytes(9999u).CopyTo(payload, 20);    // itemEntry
            BitConverter.GetBytes(0u).CopyTo(payload, 24);       // randomPropertyId

            ownerSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            Assert.NotNull(received);
            Assert.Equal(AuctionNotificationType.Sold, received!.NotificationType);
            Assert.Equal(888u, received.AuctionId);
            Assert.Equal(9999u, received.ItemEntry);

            var owner = Assert.IsType<AuctionOwnerNotificationData>(received);
            Assert.Equal(50000u, owner.BidAmount);
        }

        [Fact]
        public void HandleAuctionSearchResults_ShouldUpdateStateOnly()
        {
            var auctions = new List<AuctionData>
            {
                new AuctionData(1, 12345, 0, 0, 0, 1, 0, 0x100, 1000, 100, 5000, 3600000, 0x200, 2000),
                new AuctionData(2, 67890, 0, 0, 0, 5, 0, 0x300, 500, 50, 1000, 7200000, 0x400, 750)
            };

            _auctionHouseClientComponent.HandleAuctionSearchResults(auctions);

            Assert.Equal(2, _auctionHouseClientComponent.CurrentAuctions.Count);
        }

        #endregion

        #region Quick Methods Tests

        [Fact]
        public async Task QuickSearchAsync_ShouldPerformCompleteWorkflow()
        {
            await _auctionHouseClientComponent.QuickSearchAsync(TestAuctioneerGuid, "Sword");

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
            ulong itemGuid = 0xDEADBEEF12345678UL;
            uint startBid = 10000;
            uint buyoutPrice = 50000;
            var duration = AuctionDuration.TwentyFourHours;

            await _auctionHouseClientComponent.QuickPostAsync(TestAuctioneerGuid, itemGuid, startBid, buyoutPrice, duration);

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
            uint auctionId = 12345;
            uint buyoutPrice = 100000;

            await _auctionHouseClientComponent.QuickBuyoutAsync(TestAuctioneerGuid, auctionId, buyoutPrice);

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

        #region Auction House Not Open Tests

        [Fact]
        public async Task PlaceBidAsync_WithoutOpenAH_ShouldThrowInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _auctionHouseClientComponent.PlaceBidAsync(123, 5000));
        }

        [Fact]
        public async Task CancelAuctionAsync_WithoutOpenAH_ShouldThrowInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _auctionHouseClientComponent.CancelAuctionAsync(123));
        }

        [Fact]
        public async Task PostAuctionAsync_WithoutOpenAH_ShouldThrowInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _auctionHouseClientComponent.PostAuctionAsync(0x123UL, 1000, 5000, AuctionDuration.TwelveHours));
        }

        #endregion
    }
}
