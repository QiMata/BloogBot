using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class LootingNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<LootingNetworkClientComponent>> _mockLogger;
        private readonly LootingNetworkClientComponent _lootingAgent;

        public LootingNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<LootingNetworkClientComponent>>();
            _lootingAgent = new LootingNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void IsLootWindowOpen_InitiallyFalse()
        {
            // Arrange & Act
            var isOpen = _lootingAgent.IsLootWindowOpen;

            // Assert
            Assert.False(isOpen);
        }

        [Fact]
        public async Task OpenLootAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.OpenLootAsync(lootTargetGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == lootTargetGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task LootMoneyAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.LootMoneyAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_MONEY,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task LootItemAsync_SendsCorrectPacket()
        {
            // Arrange
            byte lootSlot = 2;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.LootItemAsync(lootSlot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_AUTOSTORE_LOOT_ITEM,
                    It.Is<byte[]>(payload => payload.Length == 1 && payload[0] == lootSlot),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task StoreLootInSlotAsync_SendsCorrectPacket()
        {
            // Arrange
            byte lootSlot = 1;
            byte bag = 0;
            byte slot = 15;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.StoreLootInSlotAsync(lootSlot, bag, slot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_STORE_LOOT_IN_SLOT,
                    It.Is<byte[]>(payload => 
                        payload.Length == 3 && 
                        payload[0] == lootSlot &&
                        payload[1] == bag &&
                        payload[2] == slot),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task CloseLootAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.CloseLootAsync();

            // Assert - CMSG_LOOT_RELEASE (1.12.1): ObjectGuid lootGuid (8) = 8 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_RELEASE,
                    It.Is<byte[]>(payload => payload.Length == 8),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task CloseLootAsync_WithOpenLootWindow_SendsLootTargetGuid()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Open loot window first to set _currentLootTarget
            _lootingAgent.HandleLootWindowChanged(true, lootTargetGuid);

            // Act
            await _lootingAgent.CloseLootAsync();

            // Assert - should send the loot target GUID
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_RELEASE,
                    It.Is<byte[]>(payload =>
                        payload.Length == 8 &&
                        BitConverter.ToUInt64(payload, 0) == lootTargetGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task RollForLootAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong lootGuid = 0x87654321;
            byte itemSlot = 1;
            LootRollType rollType = LootRollType.Need;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.RollForLootAsync(lootGuid, itemSlot, rollType);

            // Assert - CMSG_LOOT_ROLL (1.12.1): ObjectGuid(8) + uint32 itemSlot(4) + uint8 rollType(1) = 13 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_ROLL,
                    It.Is<byte[]>(payload =>
                        payload.Length == 13 &&
                        BitConverter.ToUInt64(payload, 0) == lootGuid &&
                        BitConverter.ToUInt32(payload, 8) == (uint)itemSlot &&
                        payload[12] == (byte)rollType),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task LootAllAsync_WithClosedLootWindow_ReturnsEarly()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.LootAllAsync();

            // Assert - Should not send any packets since loot window is not open
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task LootAllAsync_WithOpenLootWindow_SendsCorrectSequence()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set loot window as open using new method
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);

            // Act
            await _lootingAgent.LootAllAsync();

            // Assert
            // Should call loot money once
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_LOOT_MONEY, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );

            // Should call loot item for each slot (0-7)
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(8)
            );

            // Should call loot release once
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task QuickLootAsync_SendsCorrectSequence()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.QuickLootAsync(lootTargetGuid);

            // Assert
            // Should first open loot
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == lootTargetGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            // Note: The subsequent loot all operations won't be verified here since the loot window
            // state is not automatically updated in this test scenario
        }

        [Fact]
        public void HandleLootWindowChanged_OpenWindow_RaisesLootWindowOpenedObservable()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;
            LootWindowData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the observable
            _lootingAgent.LootWindowOpened.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _lootingAgent.HandleLootWindowChanged(true, lootTargetGuid);

            // Assert
            Assert.True(_lootingAgent.IsLootWindowOpen);
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.True(receivedData.IsOpen);
            Assert.Equal(lootTargetGuid, receivedData.LootTargetGuid);
        }

        [Fact]
        public void HandleLootWindowChanged_CloseWindow_RaisesLootWindowClosedObservable()
        {
            // Arrange
            LootWindowData? receivedData = null;
            bool observableTriggered = false;

            // First open the window
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);

            // Subscribe to the observable
            _lootingAgent.LootWindowClosed.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _lootingAgent.HandleLootWindowChanged(false, null);

            // Assert
            Assert.False(_lootingAgent.IsLootWindowOpen);
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.False(receivedData.IsOpen);
        }

        [Fact]
        public void HandleItemLooted_RaisesItemLootObservable()
        {
            // Arrange
            uint itemId = 1234;
            uint quantity = 5;
            ulong lootTargetGuid = 0x12345678;
            LootData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the observable
            _lootingAgent.ItemLoot.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _lootingAgent.HandleItemLooted(lootTargetGuid, itemId, "Test Item", quantity, ItemQuality.Common, 1);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(itemId, receivedData.ItemId);
            Assert.Equal(quantity, receivedData.Quantity);
            Assert.Equal(lootTargetGuid, receivedData.LootTargetGuid);
            Assert.Equal("Test Item", receivedData.ItemName);
        }

        [Fact]
        public void HandleMoneyLooted_RaisesMoneyLootObservable()
        {
            // Arrange
            uint amount = 1000; // 10 silver in copper
            ulong lootTargetGuid = 0x12345678;
            MoneyLootData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the observable
            _lootingAgent.MoneyLoot.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _lootingAgent.HandleMoneyLooted(lootTargetGuid, amount);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(amount, receivedData.Amount);
            Assert.Equal(lootTargetGuid, receivedData.LootTargetGuid);
        }

        [Fact]
        public void HandleLootError_RaisesLootErrorObservable()
        {
            // Arrange
            string errorMessage = "Inventory is full";
            ulong lootTargetGuid = 0x12345678;
            LootErrorData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the observable
            _lootingAgent.LootErrors.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _lootingAgent.HandleLootError(errorMessage, lootTargetGuid, 1);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(errorMessage, receivedData.ErrorMessage);
            Assert.Equal(lootTargetGuid, receivedData.LootTargetGuid);
            Assert.Equal((byte)1, receivedData.LootSlot);
        }

        [Fact]
        public void LootWindowChanges_Observable_WorksCorrectly()
        {
            // Arrange
            var receivedData = new List<LootWindowData>();
            
            // Subscribe to the observable
            _lootingAgent.LootWindowChanges.Subscribe(data => receivedData.Add(data));

            // Act
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678, 3, 1000);
            _lootingAgent.HandleLootWindowChanged(false, null);

            // Assert
            Assert.Equal(2, receivedData.Count);
            
            var openData = receivedData[0];
            Assert.True(openData.IsOpen);
            Assert.Equal((ulong)0x12345678, openData.LootTargetGuid);
            Assert.Equal((uint)3, openData.AvailableItems);
            Assert.Equal((uint)1000, openData.AvailableMoney);

            var closeData = receivedData[1];
            Assert.False(closeData.IsOpen);
            Assert.Null(closeData.LootTargetGuid);
        }

        [Fact]
        public async Task OpenLootAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _lootingAgent.OpenLootAsync(lootTargetGuid));
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData(LootRollType.Pass)]
        [InlineData(LootRollType.Need)]
        [InlineData(LootRollType.Greed)]
        public async Task RollForLootAsync_VariousRollTypes_SendsCorrectRollType(LootRollType rollType)
        {
            // Arrange
            ulong lootGuid = 0x87654321;
            byte itemSlot = 0;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.RollForLootAsync(lootGuid, itemSlot, rollType);

            // Assert - CMSG_LOOT_ROLL (1.12.1): ObjectGuid(8) + uint32 itemSlot(4) + uint8 rollType(1) = 13 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_ROLL,
                    It.Is<byte[]>(payload =>
                        payload.Length == 13 &&
                        payload[12] == (byte)rollType),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HandleLootWindowChanged_VariousStates_HandledCorrectly(bool isOpen)
        {
            // Arrange
            var receivedData = new List<LootWindowData>();
            ulong lootTargetGuid = 0x12345678;

            // Subscribe to the observable
            _lootingAgent.LootWindowChanges.Subscribe(data => receivedData.Add(data));

            // Act
            if (isOpen)
            {
                _lootingAgent.HandleLootWindowChanged(isOpen, lootTargetGuid, 2, 500);
            }
            else
            {
                _lootingAgent.HandleLootWindowChanged(isOpen, null);
            }

            // Assert
            Assert.Single(receivedData);
            Assert.Equal(isOpen, receivedData[0].IsOpen);
            
            if (isOpen)
            {
                Assert.Equal(lootTargetGuid, receivedData[0].LootTargetGuid);
                Assert.Equal((uint)2, receivedData[0].AvailableItems);
                Assert.Equal((uint)500, receivedData[0].AvailableMoney);
            }
            else
            {
                Assert.Null(receivedData[0].LootTargetGuid);
            }
        }

        [Fact]
        public void HandleLootRoll_RaisesLootRollObservable()
        {
            // Arrange
            ulong lootGuid = 0x87654321;
            byte itemSlot = 1;
            uint itemId = 12345;
            LootRollType rollType = LootRollType.Need;
            uint rollResult = 85;
            LootRollData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the observable
            _lootingAgent.LootRolls.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _lootingAgent.HandleLootRoll(lootGuid, itemSlot, itemId, rollType, rollResult);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(lootGuid, receivedData.LootGuid);
            Assert.Equal(itemSlot, receivedData.ItemSlot);
            Assert.Equal(itemId, receivedData.ItemId);
            Assert.Equal(rollType, receivedData.RollType);
            Assert.Equal(rollResult, receivedData.RollResult);
        }

        [Fact]
        public void CanLoot_WithOpenWindow_ReturnsTrue()
        {
            // Arrange
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);

            // Act
            var canLoot = _lootingAgent.CanLoot();

            // Assert
            Assert.True(canLoot);
        }

        [Fact]
        public void CanLoot_WithClosedWindow_ReturnsFalse()
        {
            // Arrange & Act
            var canLoot = _lootingAgent.CanLoot();

            // Assert
            Assert.False(canLoot);
        }

        [Fact]
        public void ValidateLootOperation_WithOpenWindow_ReturnsValid()
        {
            // Arrange
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);
            
            // Add loot items to the available loot
            var lootSlots = new List<LootSlotInfo>
            {
                new(0, 1, "Item 1", 1, ItemQuality.Poor, false, false, LootSlotType.Item),
                new(1, 2, "Item 2", 1, ItemQuality.Common, false, false, LootSlotType.Item),
                new(2, 3, "Item 3", 1, ItemQuality.Uncommon, false, false, LootSlotType.Item),
                new(3, 4, "Item 4", 1, ItemQuality.Rare, false, false, LootSlotType.Item)
            };
            
            _lootingAgent.HandleLootList(0x12345678, lootSlots);

            // Act
            var result = _lootingAgent.ValidateLootOperation(3);

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void ValidateLootOperation_WithClosedWindow_ReturnsInvalid()
        {
            // Arrange & Act
            var result = _lootingAgent.ValidateLootOperation(3);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Loot window is not open", result.ErrorMessage);
        }

        [Fact]
        public void ValidateLootOperation_WithInvalidSlot_ReturnsInvalid()
        {
            // Arrange
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);

            // Act
            var result = _lootingAgent.ValidateLootOperation(10); // Out of range

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Loot slot index is out of range", result.ErrorMessage);
        }

        #region Group Loot and Master Loot Protocol Tests

        [Fact]
        public async Task AssignMasterLootAsync_ValidParameters_SendsCorrectPacket()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;
            byte lootSlot = 2;
            ulong targetPlayerGuid = 0xAABBCCDD;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set up loot window and master looter state
            _lootingAgent.HandleLootWindowChanged(true, lootTargetGuid);
            _lootingAgent.HandleGroupLootMethodChanged(GroupLootMethod.MasterLoot, lootTargetGuid, ItemQuality.Uncommon);

            // Act
            await _lootingAgent.AssignMasterLootAsync(lootSlot, targetPlayerGuid);

            // Assert - CMSG_LOOT_MASTER_GIVE (1.12.1): ObjectGuid lootGuid(8) + uint8 slotId(1) + ObjectGuid targetGuid(8) = 17 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_MASTER_GIVE,
                    It.Is<byte[]>(payload =>
                        payload.Length == 17 &&
                        BitConverter.ToUInt64(payload, 0) == lootTargetGuid &&
                        payload[8] == lootSlot &&
                        BitConverter.ToUInt64(payload, 9) == targetPlayerGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task AssignMasterLootAsync_NotMasterLooter_ThrowsInvalidOperationException()
        {
            // Arrange - don't set up master looter state

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _lootingAgent.AssignMasterLootAsync(0, 0x12345678));
        }

        [Fact]
        public async Task SetGroupLootMethodAsync_ValidMethod_SendsCorrectPacket()
        {
            // Arrange
            var method = GroupLootMethod.RoundRobin;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.SetGroupLootMethodAsync(method);

            // Assert - CMSG_LOOT_METHOD (1.12.1): uint32 method(4) + ObjectGuid masterGuid(8) + uint32 threshold(4) = 16 bytes
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_METHOD,
                    It.Is<byte[]>(payload =>
                        payload.Length == 16 &&
                        BitConverter.ToUInt32(payload, 0) == (uint)method &&
                        BitConverter.ToUInt64(payload, 4) == 0UL && // no master looter change
                        BitConverter.ToUInt32(payload, 12) == (uint)ItemQuality.Uncommon), // default threshold
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SetLootThresholdAsync_ValidThreshold_SendsCorrectPacket()
        {
            // Arrange
            var threshold = ItemQuality.Rare;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.SetLootThresholdAsync(threshold);

            // Assert - uses CMSG_LOOT_METHOD with current method preserved
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_METHOD,
                    It.Is<byte[]>(payload =>
                        payload.Length == 16 &&
                        BitConverter.ToUInt32(payload, 0) == (uint)GroupLootMethod.FreeForAll && // default method
                        BitConverter.ToUInt32(payload, 12) == (uint)threshold),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SetMasterLooterAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            ulong masterLooterGuid = 0xDEADBEEF;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.SetMasterLooterAsync(masterLooterGuid);

            // Assert - CMSG_LOOT_METHOD with MasterLoot method and the master looter GUID
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_LOOT_METHOD,
                    It.Is<byte[]>(payload =>
                        payload.Length == 16 &&
                        BitConverter.ToUInt32(payload, 0) == (uint)GroupLootMethod.MasterLoot &&
                        BitConverter.ToUInt64(payload, 4) == masterLooterGuid &&
                        BitConverter.ToUInt32(payload, 12) == (uint)ItemQuality.Uncommon), // default threshold
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ConfirmBindOnPickupAsync_Accept_DelegatesToLootItem()
        {
            // Arrange
            byte lootSlot = 3;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.ConfirmBindOnPickupAsync(lootSlot, confirm: true);

            // Assert - should delegate to LootItemAsync which sends CMSG_AUTOSTORE_LOOT_ITEM
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_AUTOSTORE_LOOT_ITEM,
                    It.Is<byte[]>(payload => payload.Length == 1 && payload[0] == lootSlot),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ConfirmBindOnPickupAsync_Decline_DoesNotSendPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.ConfirmBindOnPickupAsync(0, confirm: false);

            // Assert - should not send any packet when declining
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        #endregion

        #region Legacy Method Tests

        [Fact]
        public void UpdateLootWindowState_OpenWindow_InvokesLootWindowOpenedCallback()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;
            ulong? callbackTargetGuid = null;
            bool callbackInvoked = false;

            _lootingAgent.SetLootWindowOpenedCallback((targetGuid) =>
            {
                callbackTargetGuid = targetGuid;
                callbackInvoked = true;
            });

            // Act
            _lootingAgent.UpdateLootWindowState(true, lootTargetGuid);

            // Assert
            Assert.True(_lootingAgent.IsLootWindowOpen);
            Assert.True(callbackInvoked);
            Assert.Equal(lootTargetGuid, callbackTargetGuid);
        }

        [Fact]
        public void UpdateLootWindowState_CloseWindow_InvokesLootWindowClosedCallback()
        {
            // Arrange
            bool callbackInvoked = false;

            // First open the window
            _lootingAgent.UpdateLootWindowState(true, 0x12345678);

            _lootingAgent.SetLootWindowClosedCallback(() =>
            {
                callbackInvoked = true;
            });

            // Act
            _lootingAgent.UpdateLootWindowState(false);

            // Assert
            Assert.False(_lootingAgent.IsLootWindowOpen);
            Assert.True(callbackInvoked);
        }

        [Fact]
        public void ReportLootEvent_ItemLooted_InvokesItemLootedCallback()
        {
            // Arrange
            uint itemId = 1234;
            uint quantity = 5;
            ulong lootTargetGuid = 0x12345678;
            uint? callbackItemId = null;
            uint? callbackQuantity = null;
            bool callbackInvoked = false;

            _lootingAgent.SetItemLootedCallback((id, qty) =>
            {
                callbackItemId = id;
                callbackQuantity = qty;
                callbackInvoked = true;
            });

            // Set up a loot target first since the new implementation requires it
            _lootingAgent.HandleLootWindowChanged(true, lootTargetGuid);

            // Act
            _lootingAgent.ReportLootEvent("item", itemId, quantity);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal(itemId, callbackItemId);
            Assert.Equal(quantity, callbackQuantity);
        }

        [Fact]
        public void ReportLootEvent_MoneyLooted_InvokesMoneyLootedCallback()
        {
            // Arrange
            uint amount = 1000; // 10 silver in copper
            ulong lootTargetGuid = 0x12345678;
            uint? callbackAmount = null;
            bool callbackInvoked = false;

            _lootingAgent.SetMoneyLootedCallback((amt) =>
            {
                callbackAmount = amt;
                callbackInvoked = true;
            });

            // Set up a loot target first since the new implementation requires it
            _lootingAgent.HandleLootWindowChanged(true, lootTargetGuid);

            // Act
            _lootingAgent.ReportLootEvent("money", null, amount);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal(amount, callbackAmount);
        }

        [Fact]
        public void ReportLootEvent_LootError_InvokesLootErrorCallback()
        {
            // Arrange
            string errorMessage = "Inventory is full";
            string? callbackErrorMessage = null;
            bool callbackInvoked = false;

            _lootingAgent.SetLootErrorCallback((error) =>
            {
                callbackErrorMessage = error;
                callbackInvoked = true;
            });

            // Act
            _lootingAgent.ReportLootEvent("error", null, null, errorMessage);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal(errorMessage, callbackErrorMessage);
        }

        [Fact]
        public void SetLootWindowOpenedCallback_ReplacesExistingCallback()
        {
            // Arrange
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;
            
            _lootingAgent.SetLootWindowOpenedCallback((targetGuid) => firstCallbackCount++);
            _lootingAgent.SetLootWindowOpenedCallback((targetGuid) => secondCallbackCount++);

            // Act
            _lootingAgent.UpdateLootWindowState(true, 0x12345678);

            // Assert
            Assert.Equal(0, firstCallbackCount); // First callback should not be called
            Assert.Equal(1, secondCallbackCount); // Second callback should be called
        }

        [Fact]
        public void SetLootWindowOpenedCallback_WithNull_ClearsCallback()
        {
            // Arrange
            int callbackCount = 0;
            _lootingAgent.SetLootWindowOpenedCallback((targetGuid) => callbackCount++);
            _lootingAgent.SetLootWindowOpenedCallback(null); // Clear callback

            // Act
            _lootingAgent.UpdateLootWindowState(true, 0x12345678);

            // Assert
            Assert.Equal(0, callbackCount); // No callback should be invoked
        }

        #endregion

        [Fact]
        public void HandleLootWindowChanged_LootWindowOpened_SetsState()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;

            // Act
            _lootingAgent.HandleLootWindowChanged(true, lootTargetGuid);

            // Assert
            Assert.True(_lootingAgent.IsLootWindowOpen);
            Assert.Equal(lootTargetGuid, _lootingAgent.CurrentLootTarget);
        }

        [Fact]
        public void HandleLootWindowChanged_LootWindowClosed_ClearsState()
        {
            // Arrange
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);

            // Act
            _lootingAgent.HandleLootWindowChanged(false, null);

            // Assert
            Assert.False(_lootingAgent.IsLootWindowOpen);
            Assert.Null(_lootingAgent.CurrentLootTarget);
        }

        [Fact]
        public void HandleItemLooted_ReportsLootEvent_EmitsObservable()
        {
            // Arrange
            uint itemId = 1234;
            uint quantity = 5;
            var lootData = (LootData?)null;
            var eventFired = false;

            var subscription = _lootingAgent.ItemLoot.Subscribe(data =>
            {
                lootData = data;
                eventFired = true;
            });

            // Act
            _lootingAgent.HandleItemLooted(0x12345678, itemId, "Test Item", quantity, ItemQuality.Common, 0);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(lootData);
            Assert.Equal(itemId, lootData.ItemId);
            Assert.Equal(quantity, lootData.Quantity);
            
            subscription.Dispose();
        }

        [Fact]
        public void HandleMoneyLooted_ReportsLootEvent_EmitsObservable()
        {
            // Arrange
            uint amount = 500;
            var moneyData = (MoneyLootData?)null;
            var eventFired = false;

            var subscription = _lootingAgent.MoneyLoot.Subscribe(data =>
            {
                moneyData = data;
                eventFired = true;
            });

            // Act
            _lootingAgent.HandleMoneyLooted(0x12345678, amount);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(moneyData);
            Assert.Equal(amount, moneyData.Amount);
            
            subscription.Dispose();
        }

        [Fact]
        public void HandleLootError_ReportsLootEvent_EmitsObservable()
        {
            // Arrange
            string errorMessage = "Test error";
            var errorData = (LootErrorData?)null;
            var eventFired = false;

            var subscription = _lootingAgent.LootErrors.Subscribe(data =>
            {
                errorData = data;
                eventFired = true;
            });

            // Act
            _lootingAgent.HandleLootError(errorMessage, null, null);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(errorData);
            Assert.Equal(errorMessage, errorData.ErrorMessage);
            
            subscription.Dispose();
        }

        [Fact]
        public void LootWindowChanges_ObservableFilters_WorkCorrectly()
        {
            // Arrange
            var openEvents = new List<LootWindowData>();
            var closeEvents = new List<LootWindowData>();

            var openSubscription = _lootingAgent.LootWindowOpened.Subscribe(data => openEvents.Add(data));
            var closeSubscription = _lootingAgent.LootWindowClosed.Subscribe(data => closeEvents.Add(data));

            // Act
            _lootingAgent.HandleLootWindowChanged(true, 0x12345678);
            _lootingAgent.HandleLootWindowChanged(false, null);

            // Assert
            Assert.Single(openEvents);
            Assert.Single(closeEvents);
            Assert.True(openEvents[0].IsOpen);
            Assert.False(closeEvents[0].IsOpen);
            
            openSubscription.Dispose();
            closeSubscription.Dispose();
        }

        [Fact]
        public void QualityFiltering_GetLootByQuality_ReturnsCorrectItems()
        {
            // Arrange
            var lootSlots = new List<LootSlotInfo>
            {
                new(0, 1, "Poor Item", 1, ItemQuality.Poor, false, false, LootSlotType.Item),
                new(1, 2, "Common Item", 1, ItemQuality.Common, false, false, LootSlotType.Item),
                new(2, 3, "Uncommon Item", 1, ItemQuality.Uncommon, false, false, LootSlotType.Item),
                new(3, 4, "Rare Item", 1, ItemQuality.Rare, false, false, LootSlotType.Item)
            };

            _lootingAgent.HandleLootList(0x12345678, lootSlots);

            // Act
            var uncommonAndAbove = _lootingAgent.GetLootByQuality(ItemQuality.Uncommon);

            // Assert
            Assert.Equal(2, uncommonAndAbove.Count);
            Assert.Contains(uncommonAndAbove, item => item.Quality == ItemQuality.Uncommon);
            Assert.Contains(uncommonAndAbove, item => item.Quality == ItemQuality.Rare);
        }
    }
}