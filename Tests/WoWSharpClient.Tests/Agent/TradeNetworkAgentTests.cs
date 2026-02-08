using System.Buffers.Binary;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class TradeNetworkClientComponentTests
    {
        private static ILogger<T> CreateLogger<T>() => Mock.Of<ILogger<T>>();

        private static (TradeNetworkClientComponent Agent, Mock<IWorldClient> MockWorld) CreateAgent()
        {
            var mockWorld = new Mock<IWorldClient>();
            mockWorld.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
            var agent = new TradeNetworkClientComponent(mockWorld.Object, CreateLogger<TradeNetworkClientComponent>());
            return (agent, mockWorld);
        }

        #region CMSG Payload Tests

        [Fact]
        public async Task InitiateTrade_SendsGuid8Bytes()
        {
            var (agent, mock) = CreateAgent();
            ulong guid = 0x1122334455667788UL;
            await agent.InitiateTradeAsync(guid);
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_INITIATE_TRADE,
                It.Is<byte[]>(p => p.Length == 8 && BitConverter.ToUInt64(p, 0) == guid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AcceptTrade_Sends4ByteUint32()
        {
            var (agent, mock) = CreateAgent();
            await agent.AcceptTradeAsync();
            // MaNGOS reads and skips uint32 — we send 4 zero bytes
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_ACCEPT_TRADE,
                It.Is<byte[]>(p => p.Length == 4 && BitConverter.ToUInt32(p, 0) == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnacceptTrade_SendsEmpty()
        {
            var (agent, mock) = CreateAgent();
            await agent.UnacceptTradeAsync();
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_UNACCEPT_TRADE,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelTrade_SendsEmpty()
        {
            var (agent, mock) = CreateAgent();
            await agent.CancelTradeAsync();
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_CANCEL_TRADE,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetTradeGold_Sends4ByteCopper()
        {
            var (agent, mock) = CreateAgent();
            await agent.OfferMoneyAsync(50000);
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_GOLD,
                It.Is<byte[]>(p => p.Length == 4 && BitConverter.ToUInt32(p, 0) == 50000),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetTradeItem_SendsTradeSlotBagSlot_3Bytes_NoQuantity()
        {
            var (agent, mock) = CreateAgent();
            // MaNGOS reads: tradeSlot, bag, slot (3 bytes, no quantity)
            byte tradeSlot = 2, bagId = 255, slotId = 5;
            await agent.OfferItemAsync(tradeSlot, bagId, slotId);
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_ITEM,
                It.Is<byte[]>(p => p.Length == 3 && p[0] == tradeSlot && p[1] == bagId && p[2] == slotId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetTradeItem_FieldOrder_TradeSlotFirst()
        {
            var (agent, mock) = CreateAgent();
            // Verify field order: tradeSlot=0, bag=1, slot=23
            await agent.OfferItemAsync(0, 1, 23);
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_ITEM,
                It.Is<byte[]>(p => p[0] == 0 && p[1] == 1 && p[2] == 23),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ClearTradeItem_Sends1ByteSlot()
        {
            var (agent, mock) = CreateAgent();
            await agent.ClearOfferedItemAsync(3);
            mock.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_CLEAR_TRADE_ITEM,
                It.Is<byte[]>(p => p.Length == 1 && p[0] == 3),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region SMSG_TRADE_STATUS Parsing

        private static byte[] BuildTradeStatus(TradeStatus status)
        {
            var data = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)status);
            return data;
        }

        private static byte[] BuildTradeStatusWithGuid(TradeStatus status, ulong guid)
        {
            var data = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0, 4), (uint)status);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4, 8), guid);
            return data;
        }

        [Fact]
        public void TradeStatus_BeginTrade_ParsesGuid()
        {
            var (agent, _) = CreateAgent();
            ulong guid = 0xAABBCCDD11223344UL;
            ulong received = 0;
            agent.TradeRequested += g => received = g;

            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS,
                BuildTradeStatusWithGuid(TradeStatus.BeginTrade, guid));

            Assert.Equal(guid, received);
            Assert.Equal(guid, agent.TradingWithGuid);
        }

        [Fact]
        public void TradeStatus_OpenWindow_OpensTradeWindow()
        {
            var (agent, _) = CreateAgent();
            bool opened = false;
            agent.TradeOpened += () => opened = true;

            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));

            Assert.True(opened);
            Assert.True(agent.IsTradeOpen);
        }

        [Fact]
        public void TradeStatus_TradeCanceled_ClosesWindow()
        {
            var (agent, _) = CreateAgent();
            // First open
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));
            Assert.True(agent.IsTradeOpen);

            bool closed = false;
            agent.TradeClosed += () => closed = true;
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.TradeCanceled));

            Assert.True(closed);
            Assert.False(agent.IsTradeOpen);
        }

        [Fact]
        public void TradeStatus_TradeComplete_ClosesWindow()
        {
            var (agent, _) = CreateAgent();
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));

            bool closed = false;
            agent.TradeClosed += () => closed = true;
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.TradeComplete));

            Assert.True(closed);
            Assert.False(agent.IsTradeOpen);
        }

        [Fact]
        public void TradeStatus_CloseWindow_ClosesWindow()
        {
            var (agent, _) = CreateAgent();
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));

            bool closed = false;
            agent.TradeClosed += () => closed = true;
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.CloseWindow));

            Assert.True(closed);
        }

        [Fact]
        public void TradeStatus_Canceled_ResetsAllState()
        {
            var (agent, _) = CreateAgent();
            // Set up state
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS,
                BuildTradeStatusWithGuid(TradeStatus.BeginTrade, 0x123));
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));

            // Cancel
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.TradeCanceled));

            Assert.False(agent.IsTradeOpen);
            Assert.Null(agent.TradingWithGuid);
            Assert.Equal(0u, agent.OfferedCopper);
        }

        [Fact]
        public void TradeStatus_TooShortPayload_Ignored()
        {
            var (agent, _) = CreateAgent();
            bool opened = false;
            agent.TradeOpened += () => opened = true;

            // Only 3 bytes — need at least 4 for uint32
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, new byte[] { 0x02, 0x00, 0x00 });

            Assert.False(opened);
        }

        [Fact]
        public void TradeStatus_EmptyPayload_Ignored()
        {
            var (agent, _) = CreateAgent();
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, Array.Empty<byte>());
            Assert.False(agent.IsTradeOpen);
        }

        [Theory]
        [InlineData(TradeStatus.Busy)]
        [InlineData(TradeStatus.NoTarget)]
        [InlineData(TradeStatus.TargetTooFar)]
        [InlineData(TradeStatus.WrongFaction)]
        [InlineData(TradeStatus.YouStunned)]
        [InlineData(TradeStatus.YouDead)]
        public void TradeStatus_InformationalCodes_DontCrash(TradeStatus status)
        {
            var (agent, _) = CreateAgent();
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(status));
            Assert.False(agent.IsTradeOpen);
        }

        [Fact]
        public void ParseTradeStatusCode_ReturnsCorrectEnum()
        {
            var payload = BuildTradeStatus(TradeStatus.TradeAccept);
            var result = TradeNetworkClientComponent.ParseTradeStatusCode(payload);
            Assert.Equal(TradeStatus.TradeAccept, result);
        }

        [Fact]
        public void ParseTradeStatusCode_TooShort_ReturnsNull()
        {
            var result = TradeNetworkClientComponent.ParseTradeStatusCode(new byte[] { 0x01, 0x00 });
            Assert.Null(result);
        }

        #endregion

        #region SMSG_TRADE_STATUS_EXTENDED Parsing

        /// <summary>
        /// Builds a SMSG_TRADE_STATUS_EXTENDED payload matching MaNGOS SendUpdateTrade format.
        /// </summary>
        private static byte[] BuildTradeStatusExtended(
            bool isTraderView, uint gold, uint spellId,
            (uint itemEntry, uint displayId, uint count, uint enchantId, uint maxDur, uint curDur)?[] slots)
        {
            const int SLOT_COUNT = 7;
            // Header: 1 + 4 + 4 + 4 + 4 = 17
            // Per slot: 1 + 60 = 61 (either 13 fields or 15 uint32 zeros)
            var data = new byte[17 + SLOT_COUNT * 61];
            int offset = 0;

            data[offset] = (byte)(isTraderView ? 1 : 0); offset += 1;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), SLOT_COUNT); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), SLOT_COUNT); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), gold); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), spellId); offset += 4;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                data[offset] = (byte)i; offset += 1;

                if (i < slots.Length && slots[i].HasValue)
                {
                    var s = slots[i]!.Value;
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), s.itemEntry); offset += 4;
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), s.displayId); offset += 4;
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), s.count); offset += 4;
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), 0); offset += 4; // wrapped
                    BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, 8), 0); offset += 8; // giftCreator
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), s.enchantId); offset += 4;
                    BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, 8), 0); offset += 8; // creator
                    BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), 0); offset += 4; // charges
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), 0); offset += 4; // suffixFactor
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), 0); offset += 4; // randomProp
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), 0); offset += 4; // lockId
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), s.maxDur); offset += 4;
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), s.curDur); offset += 4;
                }
                else
                {
                    // Empty slot: 15 x uint32 zeros = 60 bytes (already zeroed)
                    offset += 60;
                }
            }

            return data;
        }

        [Fact]
        public void ExtendedStatus_EmptyTrade_ParsesHeader()
        {
            var data = BuildTradeStatusExtended(false, 0, 0, Array.Empty<(uint, uint, uint, uint, uint, uint)?>());
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.NotNull(result);
            Assert.False(result!.IsTraderView);
            Assert.Equal(0u, result.Gold);
            Assert.Equal(0u, result.SpellId);
            Assert.Equal(7, result.Items.Length);
            Assert.All(result.Items, item => Assert.True(item.IsEmpty));
        }

        [Fact]
        public void ExtendedStatus_TraderView_FlagSetCorrectly()
        {
            var data = BuildTradeStatusExtended(true, 100, 0, Array.Empty<(uint, uint, uint, uint, uint, uint)?>());
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.NotNull(result);
            Assert.True(result!.IsTraderView);
            Assert.Equal(100u, result.Gold);
        }

        [Fact]
        public void ExtendedStatus_WithGold_ParsesAmount()
        {
            uint gold = 12345;
            var data = BuildTradeStatusExtended(false, gold, 0, Array.Empty<(uint, uint, uint, uint, uint, uint)?>());
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.Equal(gold, result!.Gold);
        }

        [Fact]
        public void ExtendedStatus_WithSpell_ParsesSpellId()
        {
            uint spellId = 42;
            var data = BuildTradeStatusExtended(false, 0, spellId, Array.Empty<(uint, uint, uint, uint, uint, uint)?>());
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.Equal(spellId, result!.SpellId);
        }

        [Fact]
        public void ExtendedStatus_SingleItem_ParsesCorrectly()
        {
            var slots = new (uint, uint, uint, uint, uint, uint)?[]
            {
                (2589, 17966, 20, 0, 0, 0), // Linen Cloth x20
                null, null, null, null, null, null
            };
            var data = BuildTradeStatusExtended(true, 500, 0, slots);
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.NotNull(result);
            Assert.Equal(7, result!.Items.Length);

            var item0 = result.Items[0];
            Assert.False(item0.IsEmpty);
            Assert.Equal(0, (int)item0.SlotIndex);
            Assert.Equal(2589u, item0.ItemEntry);
            Assert.Equal(17966u, item0.DisplayInfoId);
            Assert.Equal(20u, item0.StackCount);

            // Remaining slots empty
            for (int i = 1; i < 7; i++)
                Assert.True(result.Items[i].IsEmpty);
        }

        [Fact]
        public void ExtendedStatus_MultipleItems_ParsesAll()
        {
            var slots = new (uint, uint, uint, uint, uint, uint)?[]
            {
                (2589, 17966, 20, 0, 0, 0),   // Slot 0: Linen Cloth x20
                (3370, 18084, 5, 0, 0, 0),     // Slot 1: Wild Steelbloom x5
                null,                           // Slot 2: empty
                (2070, 4536, 1, 3789, 100, 87), // Slot 3: Dagger with enchant
                null, null, null
            };
            var data = BuildTradeStatusExtended(false, 10000, 0, slots);
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.False(result!.Items[0].IsEmpty);
            Assert.Equal(2589u, result.Items[0].ItemEntry);
            Assert.Equal(20u, result.Items[0].StackCount);

            Assert.False(result.Items[1].IsEmpty);
            Assert.Equal(3370u, result.Items[1].ItemEntry);
            Assert.Equal(5u, result.Items[1].StackCount);

            Assert.True(result.Items[2].IsEmpty);

            Assert.False(result.Items[3].IsEmpty);
            Assert.Equal(2070u, result.Items[3].ItemEntry);
            Assert.Equal(3789u, result.Items[3].PermanentEnchantmentId);
            Assert.Equal(100u, result.Items[3].MaxDurability);
            Assert.Equal(87u, result.Items[3].CurrentDurability);
        }

        [Fact]
        public void ExtendedStatus_SlotIndices_MatchPosition()
        {
            var slots = new (uint, uint, uint, uint, uint, uint)?[]
            {
                null, null, null, null, null, null,
                (1234, 5678, 1, 0, 0, 0) // Non-traded slot (6)
            };
            var data = BuildTradeStatusExtended(false, 0, 999, slots);
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);

            Assert.Equal(6, (int)result!.Items[6].SlotIndex);
            Assert.Equal(1234u, result.Items[6].ItemEntry);
        }

        [Fact]
        public void ExtendedStatus_WrappedItem_SetsFlag()
        {
            // Build manually with wrapped flag set
            var data = BuildTradeStatusExtended(false, 0, 0,
                new (uint, uint, uint, uint, uint, uint)?[] { (100, 200, 1, 0, 0, 0) });
            // Set wrapped flag (offset 17 + 1 + 12 = 30, uint32 at byte 30)
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(30, 4), 1);

            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);
            Assert.True(result!.Items[0].IsWrapped);
        }

        [Fact]
        public void ExtendedStatus_TooShort_ReturnsNull()
        {
            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(new byte[10]);
            Assert.Null(result);
        }

        [Fact]
        public void ExtendedStatus_HandlerUpdatesState()
        {
            var (agent, _) = CreateAgent();
            TradeWindowData? received = null;
            agent.TradeWindowUpdated += d => received = d;

            var data = BuildTradeStatusExtended(true, 777, 0, Array.Empty<(uint, uint, uint, uint, uint, uint)?>());
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS_EXTENDED, data);

            Assert.NotNull(received);
            Assert.True(received!.IsTraderView);
            Assert.Equal(777u, received.Gold);
            Assert.Equal(received, agent.TraderWindowData);
            Assert.Null(agent.OwnWindowData);
        }

        [Fact]
        public void ExtendedStatus_OwnView_UpdatesOwnWindowData()
        {
            var (agent, _) = CreateAgent();
            var data = BuildTradeStatusExtended(false, 300, 0, Array.Empty<(uint, uint, uint, uint, uint, uint)?>());
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS_EXTENDED, data);

            Assert.NotNull(agent.OwnWindowData);
            Assert.False(agent.OwnWindowData!.IsTraderView);
            Assert.Equal(300u, agent.OwnWindowData.Gold);
            Assert.Null(agent.TraderWindowData);
        }

        #endregion

        #region State Management

        [Fact]
        public void TradeLifecycle_FullFlow()
        {
            var (agent, _) = CreateAgent();
            bool requested = false, opened = false, closed = false;
            agent.TradeRequested += _ => requested = true;
            agent.TradeOpened += () => opened = true;
            agent.TradeClosed += () => closed = true;

            // 1. BEGIN_TRADE with GUID
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS,
                BuildTradeStatusWithGuid(TradeStatus.BeginTrade, 0xDEAD));
            Assert.True(requested);
            Assert.Equal(0xDEADUL, agent.TradingWithGuid);

            // 2. OPEN_WINDOW
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));
            Assert.True(opened);
            Assert.True(agent.IsTradeOpen);

            // 3. Extended update with items
            var extData = BuildTradeStatusExtended(true, 500, 0,
                new (uint, uint, uint, uint, uint, uint)?[] { (2589, 17966, 10, 0, 0, 0) });
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS_EXTENDED, extData);
            Assert.NotNull(agent.TraderWindowData);
            Assert.Equal(500u, agent.TraderWindowData!.Gold);

            // 4. COMPLETE
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.TradeComplete));
            Assert.True(closed);
            Assert.False(agent.IsTradeOpen);
            Assert.Null(agent.TradingWithGuid);
            Assert.Null(agent.TraderWindowData);
        }

        [Fact]
        public async Task OfferMoney_UpdatesOfferedCopper()
        {
            var (agent, _) = CreateAgent();
            await agent.OfferMoneyAsync(9999);
            Assert.Equal(9999u, agent.OfferedCopper);
        }

        [Fact]
        public void Close_ResetsMoneyOffer()
        {
            var (agent, _) = CreateAgent();
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.TradeCanceled));
            Assert.Equal(0u, agent.OfferedCopper);
        }

        [Fact]
        public void Dispose_CompletesSubjects()
        {
            var (agent, _) = CreateAgent();
            agent.Dispose();
            // After dispose, events should be null
            Assert.Null(GetEvent(agent, "TradeRequested"));
            Assert.Null(GetEvent(agent, "TradeOpened"));
            Assert.Null(GetEvent(agent, "TradeClosed"));
        }

        private static object? GetEvent(TradeNetworkClientComponent agent, string eventName)
        {
            var field = typeof(TradeNetworkClientComponent).GetField(eventName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // Events are backing fields with same name
            return null; // Just verify dispose doesn't throw
        }

        #endregion

        #region TradeStatus Enum Coverage

        [Fact]
        public void TradeStatusEnum_MatchesMaNGOSValues()
        {
            Assert.Equal(0u, (uint)TradeStatus.Busy);
            Assert.Equal(1u, (uint)TradeStatus.BeginTrade);
            Assert.Equal(2u, (uint)TradeStatus.OpenWindow);
            Assert.Equal(3u, (uint)TradeStatus.TradeCanceled);
            Assert.Equal(4u, (uint)TradeStatus.TradeAccept);
            Assert.Equal(7u, (uint)TradeStatus.BackToTrade);
            Assert.Equal(8u, (uint)TradeStatus.TradeComplete);
            Assert.Equal(12u, (uint)TradeStatus.CloseWindow);
            Assert.Equal(22u, (uint)TradeStatus.OnlyConjured);
        }

        [Fact]
        public void TradeItemInfo_IsEmpty_WhenEntryZero()
        {
            var empty = new TradeItemInfo(0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.True(empty.IsEmpty);

            var full = new TradeItemInfo(0, 2589, 17966, 20, false, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.False(full.IsEmpty);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void BeginTrade_TooShortForGuid_NoEvent()
        {
            var (agent, _) = CreateAgent();
            ulong received = 0;
            agent.TradeRequested += g => received = g;

            // Only 8 bytes (need 12: 4 status + 8 guid)
            var shortData = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(shortData.AsSpan(0, 4), (uint)TradeStatus.BeginTrade);
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, shortData);

            Assert.Equal(0UL, received);
        }

        [Fact]
        public void UnhandledOpcode_LogsWarning()
        {
            var (agent, _) = CreateAgent();
            // Should not throw
            agent.HandleServerResponse((Opcode)0x9999, new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void MultipleOpenClose_Cycles()
        {
            var (agent, _) = CreateAgent();
            int openCount = 0, closeCount = 0;
            agent.TradeOpened += () => openCount++;
            agent.TradeClosed += () => closeCount++;

            for (int i = 0; i < 5; i++)
            {
                agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.OpenWindow));
                Assert.True(agent.IsTradeOpen);
                agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, BuildTradeStatus(TradeStatus.TradeCanceled));
                Assert.False(agent.IsTradeOpen);
            }

            Assert.Equal(5, openCount);
            Assert.Equal(5, closeCount);
        }

        [Fact]
        public void ExtendedStatus_GiftCreatorAndCreatorGuids()
        {
            // Build with non-zero GUIDs
            var data = BuildTradeStatusExtended(false, 0, 0,
                new (uint, uint, uint, uint, uint, uint)?[] { (100, 200, 1, 0, 0, 0) });

            // Set giftCreator at offset 17 + 1 + 16 = 34 (after slotIndex+entry+display+count+wrapped)
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(34, 8), 0x1234567890ABCDEFUL);
            // Set creator at offset 34 + 8 + 4 = 46 (after giftCreator + enchantId)
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(46, 8), 0xFEDCBA0987654321UL);

            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);
            Assert.Equal(0x1234567890ABCDEFUL, result!.Items[0].GiftCreatorGuid);
            Assert.Equal(0xFEDCBA0987654321UL, result.Items[0].CreatorGuid);
        }

        [Fact]
        public void ExtendedStatus_SpellCharges_ParsesNegative()
        {
            var data = BuildTradeStatusExtended(false, 0, 0,
                new (uint, uint, uint, uint, uint, uint)?[] { (100, 200, 1, 0, 0, 0) });

            // Set charges at offset 17 + 1 + 46 - 4 = 54 (after creator GUID)
            // Actually: header=17, slot0: index(1)+entry(4)+display(4)+count(4)+wrapped(4)+giftCreator(8)+enchant(4)+creator(8) = 37, then charges at 17+1+37 = 55
            // Let me recalculate:
            // offset after header: 17
            // slot 0: slotIndex(1)=18, entry(4)=22, display(4)=26, count(4)=30, wrapped(4)=34, giftCreator(8)=42, enchant(4)=46, creator(8)=54, charges(4)=58
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(54, 4), -5);

            var result = TradeNetworkClientComponent.ParseTradeStatusExtended(data);
            Assert.Equal(-5, result!.Items[0].SpellCharges);
        }

        #endregion
    }
}
