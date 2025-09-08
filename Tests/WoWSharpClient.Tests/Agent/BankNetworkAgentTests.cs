using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
using GameData.Core.Enums;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class BankNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<BankNetworkAgent>> _mockLogger;
        private readonly BankNetworkAgent _bankAgent;

        public BankNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<BankNetworkAgent>>();
            _bankAgent = new BankNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Assert
            Assert.False(_bankAgent.IsBankWindowOpen);
            Assert.Equal(0u, _bankAgent.AvailableBankSlots);
            Assert.Equal(0u, _bankAgent.PurchasedBankBagSlots);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BankNetworkAgent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BankNetworkAgent(_mockWorldClient.Object, null!));
        }

        [Fact]
        public async Task OpenBankAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _bankAgent.OpenBankAsync(bankerGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_BANKER_ACTIVATE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == bankerGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            Assert.True(_bankAgent.IsBankWindowOpen);
        }

        [Fact]
        public async Task CloseBankAsync_UpdatesState()
        {
            // Arrange
            await _bankAgent.OpenBankAsync(0x12345678);

            // Act
            await _bankAgent.CloseBankAsync();

            // Assert
            Assert.False(_bankAgent.IsBankWindowOpen);
        }

        [Fact]
        public async Task DepositItemAsync_WhenBankClosed_DoesNotSendPacket()
        {
            // Arrange
            byte bagId = 0;
            byte slotId = 5;
            uint quantity = 1;

            // Act
            await _bankAgent.DepositItemAsync(bagId, slotId, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task DepositItemToSlotAsync_WhenBankOpen_SendsCorrectPacket()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte sourceBagId = 0;
            byte sourceSlotId = 5;
            byte bankSlot = 10;
            uint quantity = 1;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _bankAgent.OpenBankAsync(bankerGuid);

            // Act
            await _bankAgent.DepositItemToSlotAsync(sourceBagId, sourceSlotId, bankSlot, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_SWAP_INV_ITEM,
                    It.Is<byte[]>(payload => 
                        payload.Length == 2 && 
                        payload[0] == (sourceBagId * 16 + sourceSlotId) &&
                        payload[1] == (24 + bankSlot)), // BANK_SLOT_COUNT + bankSlot
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task WithdrawItemAsync_WhenBankOpen_SendsCorrectPacket()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte bankSlot = 10;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _bankAgent.OpenBankAsync(bankerGuid);

            // Act
            await _bankAgent.WithdrawItemAsync(bankSlot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_AUTOBANK_ITEM,
                    It.Is<byte[]>(payload => 
                        payload.Length == 1 && 
                        payload[0] == (24 + bankSlot)), // BANK_SLOT_COUNT + bankSlot
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task PurchaseBankSlotAsync_WhenBankOpen_SendsCorrectPacket()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _bankAgent.OpenBankAsync(bankerGuid);

            // Act
            await _bankAgent.PurchaseBankSlotAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_BUY_BANK_SLOT,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void IsBankOpenWith_WithCorrectGuid_ReturnsTrue()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            _bankAgent.HandleBankWindowOpened(bankerGuid);

            // Act
            bool result = _bankAgent.IsBankOpenWith(bankerGuid);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBankOpenWith_WithIncorrectGuid_ReturnsFalse()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            ulong otherGuid = 0x87654321;
            _bankAgent.HandleBankWindowOpened(bankerGuid);

            // Act
            bool result = _bankAgent.IsBankOpenWith(otherGuid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetNextBankSlotCost_InitialState_ReturnsFirstCost()
        {
            // Act
            var cost = _bankAgent.GetNextBankSlotCost();

            // Assert
            Assert.NotNull(cost);
            Assert.Equal(1000u, cost.Value); // First cost from BankSlotCosts array
        }

        [Fact]
        public void FindEmptyBankSlot_WithSpace_ReturnsSlot()
        {
            // Arrange - Simulate bank being opened which sets available slots
            _bankAgent.HandleBankInfoUpdate(24, 0); // 24 available slots, 0 purchased bag slots

            // Act
            var slot = _bankAgent.FindEmptyBankSlot();

            // Assert
            Assert.NotNull(slot);
            Assert.Equal(0, slot.Value); // Should return first slot as placeholder
        }

        [Fact]
        public void HasBankSpace_WithDefaultSlots_ReturnsTrue()
        {
            // Arrange - Simulate bank being opened which sets available slots
            _bankAgent.HandleBankInfoUpdate(24, 0); // 24 available slots, 0 purchased bag slots

            // Act
            bool hasSpace = _bankAgent.HasBankSpace();

            // Assert
            Assert.True(hasSpace);
        }

        [Fact]
        public async Task QuickDepositAsync_PerformsCompleteWorkflow()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte bagId = 0;
            byte slotId = 5;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _bankAgent.QuickDepositAsync(bankerGuid, bagId, slotId);

            // Assert - Should call open, deposit, and close
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_BANKER_ACTIVATE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );

            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );

            Assert.False(_bankAgent.IsBankWindowOpen); // Should be closed after quick operation
        }

        [Fact]
        public void HandleBankWindowOpened_FiresEvent()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            ulong? eventBankerGuid = null;
            bool eventFired = false;

            _bankAgent.BankWindowOpened += (guid) =>
            {
                eventBankerGuid = guid;
                eventFired = true;
            };

            // Act
            _bankAgent.HandleBankWindowOpened(bankerGuid);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(bankerGuid, eventBankerGuid);
            Assert.True(_bankAgent.IsBankWindowOpen);
        }

        [Fact]
        public void HandleBankWindowClosed_FiresEvent()
        {
            // Arrange
            bool eventFired = false;
            _bankAgent.BankWindowClosed += () => eventFired = true;

            // Ensure bank is open first
            _bankAgent.HandleBankWindowOpened(0x12345678);

            // Act
            _bankAgent.HandleBankWindowClosed();

            // Assert
            Assert.True(eventFired);
            Assert.False(_bankAgent.IsBankWindowOpen);
        }

        [Fact]
        public void HandleItemDeposited_FiresEvent()
        {
            // Arrange
            ulong itemGuid = 0x11111111;
            uint itemId = 12345;
            uint quantity = 5;
            byte bankSlot = 10;

            bool eventFired = false;
            ulong? eventItemGuid = null;
            uint? eventItemId = null;
            uint? eventQuantity = null;
            byte? eventBankSlot = null;

            _bankAgent.ItemDeposited += (guid, id, qty, slot) =>
            {
                eventItemGuid = guid;
                eventItemId = id;
                eventQuantity = qty;
                eventBankSlot = slot;
                eventFired = true;
            };

            // Act
            _bankAgent.HandleItemDeposited(itemGuid, itemId, quantity, bankSlot);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(itemGuid, eventItemGuid);
            Assert.Equal(itemId, eventItemId);
            Assert.Equal(quantity, eventQuantity);
            Assert.Equal(bankSlot, eventBankSlot);
        }

        [Fact]
        public void HandleGoldDeposited_FiresEvent()
        {
            // Arrange
            uint amount = 10000;
            bool eventFired = false;
            uint? eventAmount = null;

            _bankAgent.GoldDeposited += (amt) =>
            {
                eventAmount = amt;
                eventFired = true;
            };

            // Act
            _bankAgent.HandleGoldDeposited(amount);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(amount, eventAmount);
        }

        [Fact]
        public void HandleBankOperationError_FiresEvent()
        {
            // Arrange
            var operation = BankOperationType.DepositItem;
            string errorMessage = "Test error";
            bool eventFired = false;
            BankOperationType? eventOperation = null;
            string? eventError = null;

            _bankAgent.BankOperationFailed += (op, error) =>
            {
                eventOperation = op;
                eventError = error;
                eventFired = true;
            };

            // Act
            _bankAgent.HandleBankOperationError(operation, errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(operation, eventOperation);
            Assert.Equal(errorMessage, eventError);
        }

        [Fact]
        public async Task DepositGoldAsync_LogsNotImplemented()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            uint amount = 5000;

            await _bankAgent.OpenBankAsync(bankerGuid);

            // Act
            await _bankAgent.DepositGoldAsync(amount);

            // Assert - Should not send any packets since it's not implemented
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once // Only the OpenBankAsync call
            );
        }

        [Fact]
        public async Task WithdrawGoldAsync_LogsNotImplemented()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            uint amount = 5000;

            await _bankAgent.OpenBankAsync(bankerGuid);

            // Act
            await _bankAgent.WithdrawGoldAsync(amount);

            // Assert - Should not send any packets since it's not implemented
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once // Only the OpenBankAsync call
            );
        }
    }
}