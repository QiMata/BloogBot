using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Unit tests for the enhanced VendorNetworkClientComponent.
    /// Tests basic vendor operations, bulk operations, junk selling, and advanced functionality.
    /// </summary>
    public class VendorNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<VendorNetworkClientComponent>> _mockLogger;
        private readonly VendorNetworkClientComponent _vendorAgent;

        public VendorNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<VendorNetworkClientComponent>>();
            _vendorAgent = new VendorNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_vendorAgent);
            Assert.False(_vendorAgent.IsVendorWindowOpen);
            Assert.Null(_vendorAgent.CurrentVendor);
            Assert.Null(_vendorAgent.LastOperationTime);
        }

        [Fact]
        public void Constructor_NullWorldClient_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VendorNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VendorNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Basic Vendor Operations Tests

        [Fact]
        public async Task OpenVendorAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;

            // Act
            await _vendorAgent.OpenVendorAsync(vendorGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GOSSIP_HELLO,
                    It.Is<byte[]>(b => b.Length == 8),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            
            Assert.NotNull(_vendorAgent.LastOperationTime);
        }

        [Fact]
        public async Task RequestVendorInventoryAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;

            // Act
            await _vendorAgent.RequestVendorInventoryAsync(vendorGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LIST_INVENTORY,
                    It.Is<byte[]>(b => b.Length == 8),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task BuyItemAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929; // Sample item ID
            const uint quantity = 5;

            // Setup vendor window as open with the item available
            SetupVendorWithItem(vendorGuid, itemId, quantity);

            // Act
            await _vendorAgent.BuyItemAsync(vendorGuid, itemId, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_BUY_ITEM,
                    It.Is<byte[]>(b => b.Length == 16),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task BuyItemBySlotAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const byte vendorSlot = 1;
            const uint quantity = 3;

            // Act
            await _vendorAgent.BuyItemBySlotAsync(vendorGuid, vendorSlot, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_BUY_ITEM,
                    It.Is<byte[]>(b => b.Length == 13),
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
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_SELL_ITEM,
                    It.Is<byte[]>(b => b.Length == 14),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RepairItemAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const byte bagId = 0;
            const byte slotId = 1;

            // Act
            await _vendorAgent.RepairItemAsync(vendorGuid, bagId, slotId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_REPAIR_ITEM,
                    It.Is<byte[]>(b => b.Length == 10),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RepairAllItemsAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            SetupVendorWithRepair(vendorGuid);

            // Act
            await _vendorAgent.RepairAllItemsAsync(vendorGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_REPAIR_ITEM,
                    It.Is<byte[]>(b => b.Length == 10 && b[8] == 0xFF && b[9] == 0xFF),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RepairAllItemsAsync_VendorCantRepair_ThrowsInvalidOperationException()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            SetupVendorWithoutRepair(vendorGuid);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _vendorAgent.RepairAllItemsAsync(vendorGuid));
        }

        #endregion

        #region Bulk Operations Tests

        [Fact]
        public async Task BuyItemBulkAsync_ValidParameters_ExecutesMultiplePurchases()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;
            const uint totalQuantity = 250; // More than one stack
            const uint stackSize = 20;

            SetupVendorWithItem(vendorGuid, itemId, totalQuantity, stackSize);

            var options = new BulkVendorOptions
            {
                DelayBetweenOperations = TimeSpan.FromMilliseconds(10), // Speed up test
                MaxOperationTime = TimeSpan.FromMinutes(1)
            };

            // Act
            await _vendorAgent.BuyItemBulkAsync(vendorGuid, itemId, totalQuantity, options);

            // Assert
            // Should make 13 purchase calls: 12 full stacks of 20 + 1 partial stack of 10
            var expectedCalls = (int)Math.Ceiling((double)totalQuantity / stackSize);
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_BUY_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(expectedCalls));
        }

        [Fact]
        public async Task BuyItemBulkAsync_ItemNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 999; // Non-existent item
            const uint totalQuantity = 100;

            SetupVendorWithoutItem(vendorGuid);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _vendorAgent.BuyItemBulkAsync(vendorGuid, itemId, totalQuantity));
        }

        [Fact]
        public async Task SellAllJunkAsync_WithJunkItems_ReturnsExpectedValue()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint expectedValue = 500; // Total expected value

            var options = new BulkVendorOptions
            {
                MinimumJunkQuality = ItemQuality.Poor,
                MaximumJunkQuality = ItemQuality.Common,
                DelayBetweenOperations = TimeSpan.FromMilliseconds(10)
            };

            // Act
            var actualValue = await _vendorAgent.SellAllJunkAsync(vendorGuid, options);

            // Assert
            // The current implementation returns 0 since it needs game state integration
            // In a real implementation, this would return the actual value
            Assert.Equal(0u, actualValue); // Update this when game state is integrated
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void CanPurchaseItem_ItemNotInInventory_ReturnsFalse()
        {
            // Arrange
            const uint itemId = 999; // Non-existent item

            // Act
            var result = _vendorAgent.CanPurchaseItem(itemId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanPurchaseItem_ItemAvailable_ReturnsTrue()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;
            const uint quantity = 1;

            SetupVendorWithItem(vendorGuid, itemId, quantity);

            // Act
            var result = _vendorAgent.CanPurchaseItem(itemId, quantity);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanPurchaseItem_InsufficientQuantity_ReturnsFalse()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;
            const uint availableQuantity = 5;
            const uint requestedQuantity = 10;

            SetupVendorWithItem(vendorGuid, itemId, availableQuantity);

            // Act
            var result = _vendorAgent.CanPurchaseItem(itemId, requestedQuantity);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanPurchaseItem_PlayerCantUse_ReturnsFalse()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;

            SetupVendorWithUnusableItem(vendorGuid, itemId);

            // Act
            var result = _vendorAgent.CanPurchaseItem(itemId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanSellItem_ValidItem_ReturnsTrue()
        {
            // Arrange
            const byte bagId = 0;
            const byte slotId = 1;
            const uint quantity = 1;

            // Act
            var result = _vendorAgent.CanSellItem(bagId, slotId, quantity);

            // Assert
            // Currently returns true since it needs game state integration
            Assert.True(result);
        }

        #endregion

        #region Quick Operation Tests

        [Fact]
        public async Task QuickBuyAsync_ValidParameters_ExecutesCompleteSequence()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const uint itemId = 929;
            const uint quantity = 1;

            SetupVendorWithItem(vendorGuid, itemId, quantity);

            // Act
            await _vendorAgent.QuickBuyAsync(vendorGuid, itemId, quantity);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_LIST_INVENTORY, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QuickSellAsync_ValidParameters_ExecutesCompleteSequence()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            const byte bagId = 0;
            const byte slotId = 1;
            const uint quantity = 1;

            // Act
            await _vendorAgent.QuickSellAsync(vendorGuid, bagId, slotId, quantity);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_SELL_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QuickRepairAllAsync_ValidParameters_ExecutesCompleteSequence()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            SetupVendorWithRepair(vendorGuid);

            // Act
            await _vendorAgent.QuickRepairAllAsync(vendorGuid);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_REPAIR_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QuickVendorVisitAsync_WithItemsToBuy_ExecutesCompleteSequence()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            var itemsToBuy = new Dictionary<uint, uint>
            {
                { 929, 5 },
                { 930, 3 }
            };

            var options = new BulkVendorOptions
            {
                DelayBetweenOperations = TimeSpan.FromMilliseconds(10)
            };

            SetupVendorWithRepairAndItems(vendorGuid, itemsToBuy.Keys);

            // Act
            await _vendorAgent.QuickVendorVisitAsync(vendorGuid, itemsToBuy, options);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_LIST_INVENTORY, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_REPAIR_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Exactly(itemsToBuy.Count));
        }

        #endregion

        #region Event Handling Tests

        [Fact]
        public void HandleVendorWindowOpened_ValidVendorInfo_UpdatesState()
        {
            // Arrange
            const ulong vendorGuid = 0x123456789ABCDEF0;
            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Test Vendor",
                CanRepair = true,
                IsWindowOpen = false
            };

            var eventFired = false;
            VendorInfo? receivedVendorInfo = null;

            using var sub = _vendorAgent.VendorWindowsOpened.Subscribe(new ActionObserver<VendorInfo>(info =>
            {
                eventFired = true;
                receivedVendorInfo = info;
            }));

            // Act
            _vendorAgent.HandleVendorWindowOpened(vendorInfo);

            // Assert
            Assert.True(_vendorAgent.IsVendorWindowOpen);
            Assert.True(_vendorAgent.IsVendorOpen(vendorGuid));
            Assert.Equal(vendorInfo, _vendorAgent.CurrentVendor);
            Assert.True(vendorInfo.IsWindowOpen);
            Assert.True(eventFired);
            Assert.Equal(vendorInfo, receivedVendorInfo);
        }

        [Fact]
        public void HandleItemPurchased_ValidData_FiresEvent()
        {
            // Arrange
            var purchaseData = new VendorPurchaseData
            {
                VendorGuid = 0x123456789ABCDEF0,
                ItemId = 929,
                ItemName = "Test Item",
                Quantity = 5,
                TotalCost = 250,
                Result = VendorBuyResult.Success
            };

            var eventFired = false;
            VendorPurchaseData? receivedData = null;

            using var sub = _vendorAgent.ItemsPurchased.Subscribe(new ActionObserver<VendorPurchaseData>(data =>
            {
                eventFired = true;
                receivedData = data;
            }));

            // Act
            _vendorAgent.HandleItemPurchased(purchaseData);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(purchaseData, receivedData);
        }

        [Fact]
        public void HandleItemSold_ValidData_FiresEvent()
        {
            // Arrange
            var saleData = new VendorSaleData
            {
                VendorGuid = 0x123456789ABCDEF0,
                ItemId = 929,
                ItemName = "Test Item",
                Quantity = 1,
                TotalValue = 50,
                BagId = 0,
                SlotId = 1,
                Result = VendorSellResult.Success
            };

            var eventFired = false;
            VendorSaleData? receivedData = null;

            using var sub = _vendorAgent.ItemsSold.Subscribe(new ActionObserver<VendorSaleData>(data =>
            {
                eventFired = true;
                receivedData = data;
            }));

            // Act
            _vendorAgent.HandleItemSold(saleData);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(saleData, receivedData);
        }

        [Fact]
        public void HandleItemsRepaired_ValidData_FiresEvent()
        {
            // Arrange
            var repairData = new VendorRepairData
            {
                VendorGuid = 0x123456789ABCDEF0,
                IsRepairAll = true,
                TotalCost = 100,
                Result = VendorRepairResult.Success
            };

            var eventFired = false;
            VendorRepairData? receivedData = null;

            using var sub = _vendorAgent.ItemsRepairEvents.Subscribe(new ActionObserver<VendorRepairData>(data =>
            {
                eventFired = true;
                receivedData = data;
            }));

            // Act
            _vendorAgent.HandleItemsRepaired(repairData);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(repairData, receivedData);
        }

        [Fact]
        public void HandleSoulboundConfirmationRequest_ValidConfirmation_FiresEvent()
        {
            // Arrange
            var confirmation = new SoulboundConfirmation
            {
                ItemId = 929,
                ItemName = "Soulbound Item",
                ConfirmationType = SoulboundConfirmationType.BuyItem
            };

            var eventFired = false;
            SoulboundConfirmation? receivedConfirmation = null;

            using var sub = _vendorAgent.SoulboundConfirmations.Subscribe(new ActionObserver<SoulboundConfirmation>(conf =>
            {
                eventFired = true;
                receivedConfirmation = conf;
            }));

            // Act
            _vendorAgent.HandleSoulboundConfirmationRequest(confirmation);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(confirmation, receivedConfirmation);
        }

        [Fact]
        public void HandleVendorError_ValidError_FiresEvent()
        {
            // Arrange
            const string errorMessage = "Test vendor error";
            var eventFired = false;
            string? receivedError = null;

            using var sub = _vendorAgent.VendorErrors.Subscribe(new ActionObserver<string>(error =>
            {
                eventFired = true;
                receivedError = error;
            }));

            // Act
            _vendorAgent.HandleVendorError(errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(errorMessage, receivedError);
        }

        #endregion

        #region Utility Methods

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
        public void GetAvailableItems_NoVendorOpen_ReturnsEmptyList()
        {
            // Act
            var items = _vendorAgent.GetAvailableItems();

            // Assert
            Assert.NotNull(items);
            Assert.Empty(items);
        }

        [Fact]
        public void FindVendorItem_NoVendorOpen_ReturnsNull()
        {
            // Arrange
            const uint itemId = 929;

            // Act
            var item = _vendorAgent.FindVendorItem(itemId);

            // Assert
            Assert.Null(item);
        }

        [Fact]
        public async Task GetJunkItemsAsync_ValidOptions_ReturnsEmptyList()
        {
            // Arrange
            var options = new BulkVendorOptions();

            // Act
            var junkItems = await _vendorAgent.GetJunkItemsAsync(options);

            // Assert
            Assert.NotNull(junkItems);
            Assert.Empty(junkItems); // Returns empty until game state integration
        }

        #endregion

        #region Helper Methods

        private void SetupVendorWithItem(ulong vendorGuid, uint itemId, uint availableQuantity, uint stackSize = 1)
        {
            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Test Vendor",
                CanRepair = false,
                IsWindowOpen = true,
                AvailableItems = new List<VendorItem>
                {
                    new VendorItem
                    {
                        VendorSlot = 0,
                        ItemId = itemId,
                        ItemName = "Test Item",
                        Price = 50,
                        AvailableQuantity = (int)availableQuantity,
                        StackSize = stackSize,
                        Quality = ItemQuality.Common,
                        CanUse = true
                    }
                }
            };

            _vendorAgent.HandleVendorWindowOpened(vendorInfo);
        }

        private void SetupVendorWithUnusableItem(ulong vendorGuid, uint itemId)
        {
            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Test Vendor",
                CanRepair = false,
                IsWindowOpen = true,
                AvailableItems = new List<VendorItem>
                {
                    new VendorItem
                    {
                        VendorSlot = 0,
                        ItemId = itemId,
                        ItemName = "Unusable Item",
                        Price = 50,
                        AvailableQuantity = 10,
                        StackSize = 1,
                        Quality = ItemQuality.Common,
                        CanUse = false // Player can't use this item
                    }
                }
            };

            _vendorAgent.HandleVendorWindowOpened(vendorInfo);
        }

        private void SetupVendorWithoutItem(ulong vendorGuid)
        {
            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Test Vendor",
                CanRepair = false,
                IsWindowOpen = true,
                AvailableItems = new List<VendorItem>() // Empty inventory
            };

            _vendorAgent.HandleVendorWindowOpened(vendorInfo);
        }

        private void SetupVendorWithRepair(ulong vendorGuid)
        {
            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Repair Vendor",
                CanRepair = true,
                IsWindowOpen = true,
                AvailableItems = new List<VendorItem>()
            };

            _vendorAgent.HandleVendorWindowOpened(vendorInfo);
        }

        private void SetupVendorWithoutRepair(ulong vendorGuid)
        {
            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Non-Repair Vendor",
                CanRepair = false,
                IsWindowOpen = true,
                AvailableItems = new List<VendorItem>()
            };

            _vendorAgent.HandleVendorWindowOpened(vendorInfo);
        }

        private void SetupVendorWithRepairAndItems(ulong vendorGuid, IEnumerable<uint> itemIds)
        {
            var availableItems = new List<VendorItem>();
            byte slot = 0;

            foreach (var itemId in itemIds)
            {
                availableItems.Add(new VendorItem
                {
                    VendorSlot = slot++,
                    ItemId = itemId,
                    ItemName = $"Item {itemId}",
                    Price = 50,
                    AvailableQuantity = 10,
                    StackSize = 1,
                    Quality = ItemQuality.Common,
                    CanUse = true
                });
            }

            var vendorInfo = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = "Full Service Vendor",
                CanRepair = true,
                IsWindowOpen = true,
                AvailableItems = availableItems
            };

            _vendorAgent.HandleVendorWindowOpened(vendorInfo);
        }

        private sealed class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            public ActionObserver(Action<T> onNext) => _onNext = onNext;
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(T value) => _onNext(value);
        }

        #endregion
    }
}