using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace WoWSharpClient.Tests.Agent
{
    public class BankNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<BankNetworkClientComponent>> _mockLogger;
        private readonly BankNetworkClientComponent _bankClientComponent;

        // MaNGOS 1.12.1 bank constants
        private const byte BANK_SLOT_ITEM_START = 39;
        private const byte BANK_BAG = 0xFF;

        public BankNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<BankNetworkClientComponent>>();

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _bankClientComponent = new BankNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            Assert.False(_bankClientComponent.IsBankWindowOpen);
            Assert.Equal(0u, _bankClientComponent.AvailableBankSlots);
            Assert.Equal(0u, _bankClientComponent.PurchasedBankBagSlots);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BankNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BankNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region OpenBank / CloseBank Tests

        [Fact]
        public async Task OpenBankAsync_SendsCorrectPacket()
        {
            ulong bankerGuid = 0x12345678;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_BANKER_ACTIVATE,
                    It.Is<byte[]>(p => p.Length == 8 && BitConverter.ToUInt64(p, 0) == bankerGuid),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.True(_bankClientComponent.IsBankWindowOpen);
            Assert.Equal(24u, _bankClientComponent.AvailableBankSlots);
        }

        [Fact]
        public async Task CloseBankAsync_UpdatesState()
        {
            await _bankClientComponent.OpenBankAsync(0x12345678);

            await _bankClientComponent.CloseBankAsync();

            Assert.False(_bankClientComponent.IsBankWindowOpen);
        }

        #endregion

        #region DepositItem Tests

        [Fact]
        public async Task DepositItemAsync_WhenBankOpen_SendsAutoBankItem()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte bagId = 0xFF;
            byte slotId = 25;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.DepositItemAsync(bagId, slotId);

            // Assert — CMSG_AUTOBANK_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_AUTOBANK_ITEM,
                    It.Is<byte[]>(p =>
                        p.Length == 2 &&
                        p[0] == bagId &&
                        p[1] == slotId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DepositItemAsync_WhenBankClosed_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bankClientComponent.DepositItemAsync(0, 5));
        }

        [Fact]
        public async Task DepositItemToSlotAsync_WhenBankOpen_SendsSwapItem()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte sourceBagId = 0xFF;
            byte sourceSlotId = 25;
            byte bankSlot = 10;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.DepositItemToSlotAsync(sourceBagId, sourceSlotId, bankSlot);

            // Assert — CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_SWAP_ITEM,
                    It.Is<byte[]>(p =>
                        p.Length == 4 &&
                        p[0] == BANK_BAG &&
                        p[1] == (byte)(BANK_SLOT_ITEM_START + bankSlot) &&
                        p[2] == sourceBagId &&
                        p[3] == sourceSlotId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DepositItemToSlotAsync_WhenBankClosed_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bankClientComponent.DepositItemToSlotAsync(0, 5, 0));
        }

        #endregion

        #region WithdrawItem Tests

        [Fact]
        public async Task WithdrawItemAsync_WhenBankOpen_SendsAutoStoreBankItem()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte bankSlot = 10;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.WithdrawItemAsync(bankSlot);

            // Assert — CMSG_AUTOSTORE_BANK_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_AUTOSTORE_BANK_ITEM,
                    It.Is<byte[]>(p =>
                        p.Length == 2 &&
                        p[0] == BANK_BAG &&
                        p[1] == (byte)(BANK_SLOT_ITEM_START + bankSlot)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task WithdrawItemAsync_WhenBankClosed_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bankClientComponent.WithdrawItemAsync(5));
        }

        [Fact]
        public async Task WithdrawItemToSlotAsync_WhenBankOpen_SendsSwapItem()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte bankSlot = 10;
            byte targetBagId = 0xFF;
            byte targetSlotId = 25;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.WithdrawItemToSlotAsync(bankSlot, targetBagId, targetSlotId);

            // Assert — CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_SWAP_ITEM,
                    It.Is<byte[]>(p =>
                        p.Length == 4 &&
                        p[0] == targetBagId &&
                        p[1] == targetSlotId &&
                        p[2] == BANK_BAG &&
                        p[3] == (byte)(BANK_SLOT_ITEM_START + bankSlot)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task WithdrawItemToSlotAsync_WhenBankClosed_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bankClientComponent.WithdrawItemToSlotAsync(5, 0, 10));
        }

        #endregion

        #region SwapItem Tests

        [Fact]
        public async Task SwapItemWithBankAsync_WhenBankOpen_SendsSwapItem()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            byte inventoryBagId = 0xFF;
            byte inventorySlotId = 25;
            byte bankSlot = 5;

            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.SwapItemWithBankAsync(inventoryBagId, inventorySlotId, bankSlot);

            // Assert — CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_SWAP_ITEM,
                    It.Is<byte[]>(p =>
                        p.Length == 4 &&
                        p[0] == BANK_BAG &&
                        p[1] == (byte)(BANK_SLOT_ITEM_START + bankSlot) &&
                        p[2] == inventoryBagId &&
                        p[3] == inventorySlotId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SwapItemWithBankAsync_WhenBankClosed_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bankClientComponent.SwapItemWithBankAsync(0, 5, 0));
        }

        #endregion

        #region PurchaseBankSlot Tests

        [Fact]
        public async Task PurchaseBankSlotAsync_WhenBankOpen_SendsBankerGuid()
        {
            // Arrange
            ulong bankerGuid = 0x12345678;
            await _bankClientComponent.OpenBankAsync(bankerGuid);

            // Act
            await _bankClientComponent.PurchaseBankSlotAsync();

            // Assert — CMSG_BUY_BANK_SLOT: bankerGuid(8)
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_BUY_BANK_SLOT,
                    It.Is<byte[]>(p =>
                        p.Length == 8 &&
                        BitConverter.ToUInt64(p, 0) == bankerGuid),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PurchaseBankSlotAsync_WhenBankClosed_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bankClientComponent.PurchaseBankSlotAsync());
        }

        #endregion

        #region SMSG Parsing Tests

        [Fact]
        public void BankWindowOpenedStream_ParsesShowBank_ExtractsBankerGuid()
        {
            // Arrange
            var showBankSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_SHOW_BANK))
                            .Returns(showBankSubject.AsObservable());

            ulong bankerGuid = 0xAABBCCDD11223344;
            bool fired = false;
            ulong receivedGuid = 0;
            using var sub = _bankClientComponent.BankWindowOpenedStream.Subscribe(g => { fired = true; receivedGuid = g; });

            // Act — SMSG_SHOW_BANK: bankerGuid(8)
            var payload = new byte[8];
            BitConverter.GetBytes(bankerGuid).CopyTo(payload, 0);
            showBankSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.Equal(bankerGuid, receivedGuid);
            Assert.True(_bankClientComponent.IsBankWindowOpen);
        }

        [Fact]
        public void BankSlotPurchasedStream_ParsesUint32Result_FiresOnSuccess()
        {
            // Arrange
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_BUY_BANK_SLOT_RESULT))
                            .Returns(resultSubject.AsObservable());

            _bankClientComponent.HandleBankInfoUpdate(24, 0);

            bool fired = false;
            BankSlotPurchaseData? data = null;
            using var sub = _bankClientComponent.BankSlotPurchasedStream.Subscribe(d => { fired = true; data = d; });

            // Act — SMSG_BUY_BANK_SLOT_RESULT: result(4, uint32) = ERR_BANKSLOT_OK (3)
            var payload = new byte[4];
            BitConverter.GetBytes((uint)BuyBankSlotResult.ERR_BANKSLOT_OK).CopyTo(payload, 0);
            resultSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.NotNull(data);
            Assert.Equal((byte)0, data!.SlotIndex);
            Assert.Equal(1000u, data.Cost);
        }

        [Fact]
        public void BankOperationFailedStream_ParsesUint32Result_FiresOnFailure()
        {
            // Arrange
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_BUY_BANK_SLOT_RESULT))
                            .Returns(resultSubject.AsObservable());

            bool fired = false;
            BankOperationErrorData? error = null;
            using var sub = _bankClientComponent.BankOperationFailedStream.Subscribe(e => { fired = true; error = e; });

            // Act — SMSG_BUY_BANK_SLOT_RESULT: result(4, uint32) = ERR_BANKSLOT_INSUFFICIENT_FUNDS (1)
            var payload = new byte[4];
            BitConverter.GetBytes((uint)BuyBankSlotResult.ERR_BANKSLOT_INSUFFICIENT_FUNDS).CopyTo(payload, 0);
            resultSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.NotNull(error);
            Assert.Equal(BankOperationType.PurchaseSlot, error!.Operation);
            Assert.Contains("Insufficient funds", error.ErrorMessage);
        }

        [Fact]
        public void ItemDepositedStream_ParsesItemPushResult()
        {
            // Arrange
            var itemPushSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_ITEM_PUSH_RESULT))
                            .Returns(itemPushSubject.AsObservable());

            bool fired = false;
            ItemMovementData? moved = null;
            using var sub = _bankClientComponent.ItemDepositedStream.Subscribe(m => { fired = true; moved = m; });

            // Build MaNGOS SMSG_ITEM_PUSH_RESULT (44 bytes minimum):
            // playerGuid(8) + newItem(4) + createdFromSpell(4) + isCreated(4) +
            // containerSlot(4) + slot(4) + itemEntry(4) + suffixFactor(4) +
            // randomPropertyId(4) + count(4)
            uint itemEntry = 12345;
            byte slot = 42;
            uint count = 5;

            var payload = new byte[44];
            BitConverter.GetBytes(0x0000000000000007UL).CopyTo(payload, 0); // playerGuid
            BitConverter.GetBytes(1u).CopyTo(payload, 8);   // newItem
            BitConverter.GetBytes(0u).CopyTo(payload, 12);  // createdFromSpell
            BitConverter.GetBytes(0u).CopyTo(payload, 16);  // isCreated
            BitConverter.GetBytes(0u).CopyTo(payload, 20);  // containerSlot
            BitConverter.GetBytes((uint)slot).CopyTo(payload, 24); // slot
            BitConverter.GetBytes(itemEntry).CopyTo(payload, 28); // itemEntry
            BitConverter.GetBytes(0u).CopyTo(payload, 32);  // suffixFactor
            BitConverter.GetBytes(0u).CopyTo(payload, 36);  // randomPropertyId
            BitConverter.GetBytes(count).CopyTo(payload, 40); // count

            // Act
            itemPushSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(fired);
            Assert.NotNull(moved);
            Assert.Equal(itemEntry, moved!.ItemId);
            Assert.Equal(count, moved.Quantity);
            Assert.Equal(slot, moved.Slot);
        }

        #endregion

        #region BankWindowClosedStream Test

        [Fact]
        public void BankWindowClosedStream_FiresOnDisconnect()
        {
            var disconnectSubject = new Subject<Exception?>();
            _mockWorldClient.SetupGet(c => c.WhenDisconnected).Returns(disconnectSubject.AsObservable());

            bool fired = false;
            using var sub = _bankClientComponent.BankWindowClosedStream.Subscribe(_ => fired = true);

            disconnectSubject.OnNext(null);

            Assert.True(fired);
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void IsBankOpenWith_WithCorrectGuid_ReturnsTrue()
        {
            ulong bankerGuid = 0x12345678;
            _bankClientComponent.HandleBankWindowOpened(bankerGuid);

            Assert.True(_bankClientComponent.IsBankOpenWith(bankerGuid));
        }

        [Fact]
        public void IsBankOpenWith_WithIncorrectGuid_ReturnsFalse()
        {
            _bankClientComponent.HandleBankWindowOpened(0x12345678);

            Assert.False(_bankClientComponent.IsBankOpenWith(0x87654321));
        }

        [Fact]
        public void GetNextBankSlotCost_InitialState_ReturnsFirstCost()
        {
            var cost = _bankClientComponent.GetNextBankSlotCost();
            Assert.NotNull(cost);
            Assert.Equal(1000u, cost.Value);
        }

        [Fact]
        public void FindEmptyBankSlot_WithSpace_ReturnsSlot()
        {
            _bankClientComponent.HandleBankInfoUpdate(24, 0);

            var slot = _bankClientComponent.FindEmptyBankSlot();

            Assert.NotNull(slot);
            Assert.Equal(0, slot.Value);
        }

        [Fact]
        public void HasBankSpace_WithDefaultSlots_ReturnsTrue()
        {
            _bankClientComponent.HandleBankInfoUpdate(24, 0);

            Assert.True(_bankClientComponent.HasBankSpace());
        }

        #endregion

        #region Gold Operation Tests

        [Fact]
        public async Task DepositGoldAsync_DoesNotSendPacket_VanillaHasNoGoldBank()
        {
            await _bankClientComponent.OpenBankAsync(0x12345678);

            await _bankClientComponent.DepositGoldAsync(5000);

            // Only the OpenBankAsync CMSG_BANKER_ACTIVATE should be sent
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task WithdrawGoldAsync_DoesNotSendPacket_VanillaHasNoGoldBank()
        {
            await _bankClientComponent.OpenBankAsync(0x12345678);

            await _bankClientComponent.WithdrawGoldAsync(5000);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Quick Operation Tests

        [Fact]
        public async Task QuickDepositAsync_OpensBank_Deposits_Closes()
        {
            ulong bankerGuid = 0x12345678;
            byte bagId = 0xFF;
            byte slotId = 25;

            await _bankClientComponent.QuickDepositAsync(bankerGuid, bagId, slotId);

            // Should send CMSG_BANKER_ACTIVATE then CMSG_AUTOBANK_ITEM
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_BANKER_ACTIVATE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_AUTOBANK_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.False(_bankClientComponent.IsBankWindowOpen);
        }

        [Fact]
        public async Task QuickWithdrawAsync_OpensBank_Withdraws_Closes()
        {
            ulong bankerGuid = 0x12345678;
            byte bankSlot = 5;

            await _bankClientComponent.QuickWithdrawAsync(bankerGuid, bankSlot);

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_BANKER_ACTIVATE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_BANK_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.False(_bankClientComponent.IsBankWindowOpen);
        }

        #endregion
    }
}
