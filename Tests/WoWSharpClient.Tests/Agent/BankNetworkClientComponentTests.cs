using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using Xunit;
using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Linq;

namespace WoWSharpClient.Tests.Agent
{
    public class BankNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<BankNetworkClientComponent>> _mockLogger;
        private readonly BankNetworkClientComponent _bankClientComponent;

        public BankNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<BankNetworkClientComponent>>();
            _bankClientComponent = new BankNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Assert
            Assert.False(_bankClientComponent.IsBankWindowOpen);
            Assert.Equal(0u, _bankClientComponent.AvailableBankSlots);
            Assert.Equal(0u, _bankClientComponent.PurchasedBankBagSlots);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BankNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BankNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        [Fact]
        public async Task OpenBankAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_BANKER_ACTIVATE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == bankerGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            Assert.True(_bankClientComponent.IsBankWindowOpen);
        }

        [Fact]
        public async Task CloseBankAsync_UpdatesState()
        {
            // Arrange
            await _bankClientComponent.OpenBankAsync(0x12345678);

            // Act
            await _bankClientComponent.CloseBankAsync();

            // Assert
            Assert.False(_bankClientComponent.IsBankWindowOpen);
        }

        [Fact]
        public async Task DepositItemAsync_WhenBankClosed_DoesNotSendPacket()
        {
            // Arrange
            byte bagId = 0;
            byte slotId = 5;
            uint quantity = 1;

            // Act
            await _bankClientComponent.DepositItemAsync(bagId, slotId, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.DepositItemToSlotAsync(sourceBagId, sourceSlotId, bankSlot, quantity);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.WithdrawItemAsync(bankSlot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.PurchaseBankSlotAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
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
            _bankClientComponent.HandleBankWindowOpened(bankerGuid);

            // Act
            bool result = _bankClientComponent.IsBankOpenWith(bankerGuid);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBankOpenWith_WithIncorrectGuid_ReturnsFalse()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            ulong otherGuid = 0x87654321;
            _bankClientComponent.HandleBankWindowOpened(bankerGuid);

            // Act
            bool result = _bankClientComponent.IsBankOpenWith(otherGuid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetNextBankSlotCost_InitialState_ReturnsFirstCost()
        {
            // Act
            var cost = _bankClientComponent.GetNextBankSlotCost();

            // Assert
            Assert.NotNull(cost);
            Assert.Equal(1000u, cost.Value); // First cost from BankSlotCosts array
        }

        [Fact]
        public void FindEmptyBankSlot_WithSpace_ReturnsSlot()
        {
            // Arrange - Simulate bank being opened which sets available slots
            _bankClientComponent.HandleBankInfoUpdate(24, 0); // 24 available slots, 0 purchased bag slots

            // Act
            var slot = _bankClientComponent.FindEmptyBankSlot();

            // Assert
            Assert.NotNull(slot);
            Assert.Equal(0, slot.Value); // Should return first slot as placeholder
        }

        [Fact]
        public void HasBankSpace_WithDefaultSlots_ReturnsTrue()
        {
            // Arrange - Simulate bank being opened which sets available slots
            _bankClientComponent.HandleBankInfoUpdate(24, 0); // 24 available slots, 0 purchased bag slots

            // Act
            bool hasSpace = _bankClientComponent.HasBankSpace();

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
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _bankClientComponent.QuickDepositAsync(bankerGuid, bagId, slotId);

            // Assert - Should call open, deposit, and close
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_BANKER_ACTIVATE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );

            Assert.False(_bankClientComponent.IsBankWindowOpen); // Should be closed after quick operation
        }

        [Fact]
        public void BankWindowOpenedStream_FiresOnShowBank()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            _bankClientComponent.HandleBankWindowOpened(bankerGuid); // set state for emission

            var showBankSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_SHOW_BANK))
                            .Returns(showBankSubject.AsObservable());

            bool fired = false;
            ulong receivedGuid = 0;
            using var sub = _bankClientComponent.BankWindowOpenedStream.Subscribe(g => { fired = true; receivedGuid = g; });

            // Act
            showBankSubject.OnNext(new ReadOnlyMemory<byte>(new byte[] { 0 }));

            // Assert
            Assert.True(fired);
            Assert.Equal(bankerGuid, receivedGuid);
        }

        [Fact]
        public void BankWindowClosedStream_FiresOnDisconnect()
        {
            // Arrange
            var disconnectSubject = new Subject<Exception?>();
            _mockWorldClient.SetupGet(c => c.WhenDisconnected).Returns(disconnectSubject.AsObservable());

            bool fired = false;
            using var sub = _bankClientComponent.BankWindowClosedStream.Subscribe(_ => fired = true);

            // Act
            disconnectSubject.OnNext(null);

            // Assert
            Assert.True(fired);
        }

        [Fact]
        public void BankSlotPurchasedStream_FiresOnSuccess()
        {
            // Arrange
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_BUY_BANK_SLOT_RESULT))
                            .Returns(resultSubject.AsObservable());

            // Ensure initial bag slots is 0
            _bankClientComponent.HandleBankInfoUpdate(24, 0);

            bool fired = false;
            BankSlotPurchaseData? data = null;
            using var sub = _bankClientComponent.BankSlotPurchasedStream.Subscribe(d => { fired = true; data = d; });

            // Build success payload: first byte = ERR_BANKSLOT_OK (3)
            var payload = new byte[] { 3 };

            // Act
            resultSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.NotNull(data);
            Assert.Equal((byte)0, data!.SlotIndex); // first purchased slot index
            Assert.Equal(1000u, data.Cost); // first cost
        }

        [Fact]
        public void BankOperationFailedStream_FiresOnFailure()
        {
            // Arrange
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_BUY_BANK_SLOT_RESULT))
                            .Returns(resultSubject.AsObservable());

            bool fired = false;
            BankOperationErrorData? error = null;
            using var sub = _bankClientComponent.BankOperationFailedStream.Subscribe(e => { fired = true; error = e; });

            // Build failure payload: first byte = ERR_BANKSLOT_INSUFFICIENT_FUNDS (1)
            var payload = new byte[] { 1 };

            // Act
            resultSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.NotNull(error);
            Assert.Equal(BankOperationType.PurchaseSlot, error!.Operation);
            Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage));
        }

        [Fact]
        public void ItemDepositedStream_FiresOnItemPush()
        {
            // Arrange
            var itemPushSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_ITEM_PUSH_RESULT))
                            .Returns(itemPushSubject.AsObservable());

            bool fired = false;
            ItemMovementData? moved = null;
            using var sub = _bankClientComponent.ItemDepositedStream.Subscribe(m => { fired = true; moved = m; });

            // Build heuristic payload: guid(8) id(4) qty(4) slot(1)
            ulong itemGuid = 0x11111111UL;
            uint itemId = 12345;
            uint qty = 5;
            byte slot = 10;
            var payload = new byte[17];
            BitConverter.GetBytes(itemGuid).CopyTo(payload, 0);
            BitConverter.GetBytes(itemId).CopyTo(payload, 8);
            BitConverter.GetBytes(qty).CopyTo(payload, 12);
            payload[16] = slot;

            // Act
            itemPushSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.NotNull(moved);
            Assert.Equal(itemGuid, moved!.ItemGuid);
            Assert.Equal(itemId, moved.ItemId);
            Assert.Equal(qty, moved.Quantity);
            Assert.Equal(slot, moved.Slot);
        }

        [Fact]
        public async Task DepositGoldAsync_LogsNotImplemented()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            uint amount = 5000;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.DepositGoldAsync(amount);

            // Assert - Should not send any packets since it's not implemented
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once // Only the OpenBankAsync call
            );
        }

        [Fact]
        public async Task WithdrawGoldAsync_LogsNotImplemented()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            uint amount = 5000;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.WithdrawGoldAsync(amount);

            // Assert - Should not send any packets since it's not implemented
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once // Only the OpenBankAsync call
            );
        }
    }
}