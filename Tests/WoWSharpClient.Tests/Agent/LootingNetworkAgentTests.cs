using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class LootingNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<LootingNetworkAgent>> _mockLogger;
        private readonly LootingNetworkAgent _lootingAgent;

        public LootingNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<LootingNetworkAgent>>();
            _lootingAgent = new LootingNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.OpenLootAsync(lootTargetGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.LootMoneyAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.LootItemAsync(lootSlot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.StoreLootInSlotAsync(lootSlot, bag, slot);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.CloseLootAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_LOOT_RELEASE,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.RollForLootAsync(lootGuid, itemSlot, rollType);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_LOOT_ROLL,
                    It.Is<byte[]>(payload => 
                        payload.Length == 10 && 
                        BitConverter.ToUInt64(payload, 0) == lootGuid &&
                        payload[8] == itemSlot &&
                        payload[9] == (byte)rollType),
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.LootAllAsync();

            // Assert - Should not send any packets since loot window is not open
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task LootAllAsync_WithOpenLootWindow_SendsCorrectSequence()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set loot window as open
            _lootingAgent.UpdateLootWindowState(true, 0x12345678);

            // Act
            await _lootingAgent.LootAllAsync();

            // Assert
            // Should call loot money once
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_LOOT_MONEY, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );

            // Should call loot item for each slot (0-7)
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(8)
            );

            // Should call loot release once
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(Opcode.CMSG_LOOT_RELEASE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task QuickLootAsync_SendsCorrectSequence()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.QuickLootAsync(lootTargetGuid);

            // Assert
            // Should first open loot
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
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
            uint? callbackItemId = null;
            uint? callbackQuantity = null;
            bool callbackInvoked = false;

            _lootingAgent.SetItemLootedCallback((id, qty) =>
            {
                callbackItemId = id;
                callbackQuantity = qty;
                callbackInvoked = true;
            });

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
            uint? callbackAmount = null;
            bool callbackInvoked = false;

            _lootingAgent.SetMoneyLootedCallback((amt) =>
            {
                callbackAmount = amt;
                callbackInvoked = true;
            });

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

        [Fact]
        public async Task OpenLootAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong lootTargetGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
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
                .Setup(x => x.SendMovementAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _lootingAgent.RollForLootAsync(lootGuid, itemSlot, rollType);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    Opcode.CMSG_LOOT_ROLL,
                    It.Is<byte[]>(payload => 
                        payload.Length == 10 && 
                        payload[9] == (byte)rollType),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Theory]
        [InlineData("item")]
        [InlineData("money")]
        [InlineData("error")]
        public void ReportLootEvent_VariousEventTypes_HandledCorrectly(string eventType)
        {
            // Arrange
            bool callbackInvoked = false;

            // Subscribe to all callbacks
            _lootingAgent.SetItemLootedCallback((id, qty) => callbackInvoked = true);
            _lootingAgent.SetMoneyLootedCallback((amt) => callbackInvoked = true);
            _lootingAgent.SetLootErrorCallback((error) => callbackInvoked = true);

            // Act
            _lootingAgent.ReportLootEvent(eventType, 1234, 100, "Test message");

            // Assert
            Assert.True(callbackInvoked);
        }
    }
}