using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq; // for Subscribe overloads

namespace WoWSharpClient.Tests.Agent
{
    public class GameObjectNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<GameObjectNetworkClientComponent>> _mockLogger;
        private readonly GameObjectNetworkClientComponent _gameObjectAgent;

        public GameObjectNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<GameObjectNetworkClientComponent>>();
            _gameObjectAgent = new GameObjectNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task InteractWithGameObjectAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong gameObjectGuid = 0x12345678;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.InteractWithGameObjectAsync(gameObjectGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == gameObjectGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task OpenChestAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong chestGuid = 0x87654321;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.OpenChestAsync(chestGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == chestGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task GatherFromNodeAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong nodeGuid = 0x11111111;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.GatherFromNodeAsync(nodeGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == nodeGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task UseDoorAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong doorGuid = 0x22222222;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.UseDoorAsync(doorGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == doorGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ActivateButtonAsync_SendsCorrectPacket()
        {
            // Arrange
            ulong buttonGuid = 0x33333333;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.ActivateButtonAsync(buttonGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == buttonGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Theory]
        [InlineData(GameObjectType.Chest)]
        [InlineData(GameObjectType.Goober)]
        [InlineData(GameObjectType.Door)]
        [InlineData(GameObjectType.Button)]
        [InlineData(GameObjectType.Generic)]
        public async Task SmartInteractAsync_VariousObjectTypes_CallsCorrectMethod(GameObjectType objectType)
        {
            // Arrange
            ulong gameObjectGuid = 0x44444444;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.SmartInteractAsync(gameObjectGuid, objectType);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == gameObjectGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SmartInteractAsync_WithoutObjectType_UsesGenericInteraction()
        {
            // Arrange
            ulong gameObjectGuid = 0x55555555;

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _gameObjectAgent.SmartInteractAsync(gameObjectGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_GAMEOBJ_USE,
                    It.Is<byte[]>(payload => payload.Length == 8 && BitConverter.ToUInt64(payload, 0) == gameObjectGuid),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void CanInteractWith_ReturnsTrue()
        {
            // Arrange
            ulong gameObjectGuid = 0x66666666;
            var interactionType = GameObjectInteractionType.OpenChest;

            // Act
            var canInteract = _gameObjectAgent.CanInteractWith(gameObjectGuid, interactionType);

            // Assert
            Assert.True(canInteract);
        }

        [Theory]
        [InlineData(GameObjectType.Chest, 3.0f)]
        [InlineData(GameObjectType.Goober, 3.5f)]
        [InlineData(GameObjectType.Door, 2.5f)]
        [InlineData(GameObjectType.Button, 2.0f)]
        [InlineData(GameObjectType.QuestGiver, 4.0f)]
        [InlineData(GameObjectType.Mailbox, 3.0f)]
        [InlineData(GameObjectType.AuctionHouse, 4.0f)]
        [InlineData(GameObjectType.SpellCaster, 4.0f)]
        [InlineData(GameObjectType.Generic, 3.0f)]
        public void GetInteractionDistance_VariousObjectTypes_ReturnsCorrectDistance(GameObjectType objectType, float expectedDistance)
        {
            // Act
            var distance = _gameObjectAgent.GetInteractionDistance(objectType);

            // Assert
            Assert.Equal(expectedDistance, distance);
        }

        [Fact]
        public void ReportInteractionEvent_Success_EmitsGameObjectInteracted()
        {
            // Arrange
            ulong gameObjectGuid = 0x77777777;
            ulong? eventGameObjectGuid = null;
            bool eventFired = false;
            using var sub = _gameObjectAgent.GameObjectInteracted.Subscribe(guid => { eventGameObjectGuid = guid; eventFired = true; });

            // Act
            _gameObjectAgent.ReportInteractionEvent("success", gameObjectGuid);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(gameObjectGuid, eventGameObjectGuid);
        }

        [Fact]
        public void ReportInteractionEvent_ChestOpened_EmitsChestOpenedAndInteracted()
        {
            // Arrange
            ulong chestGuid = 0x88888888;
            ulong? eventChestGuid = null;
            bool chestEventFired = false;
            bool interactedEventFired = false;
            using var sub1 = _gameObjectAgent.ChestOpened.Subscribe(guid => { eventChestGuid = guid; chestEventFired = true; });
            using var sub2 = _gameObjectAgent.GameObjectInteracted.Subscribe(_ => interactedEventFired = true);

            // Act
            _gameObjectAgent.ReportInteractionEvent("chest_opened", chestGuid);

            // Assert
            Assert.True(chestEventFired);
            Assert.True(interactedEventFired);
            Assert.Equal(chestGuid, eventChestGuid);
        }

        [Fact]
        public void ReportInteractionEvent_NodeHarvested_EmitsNodeHarvestedAndInteracted()
        {
            // Arrange
            ulong nodeGuid = 0x99999999;
            uint itemId = 2447;
            ulong? eventNodeGuid = null;
            uint? eventItemId = null;
            bool nodeEventFired = false;
            bool interactedEventFired = false;
            using var sub1 = _gameObjectAgent.NodeHarvested.Subscribe(t => { eventNodeGuid = t.GameObjectGuid; eventItemId = t.ItemId; nodeEventFired = true; });
            using var sub2 = _gameObjectAgent.GameObjectInteracted.Subscribe(_ => interactedEventFired = true);

            // Act
            _gameObjectAgent.ReportInteractionEvent("node_harvested", nodeGuid, itemId);

            // Assert
            Assert.True(nodeEventFired);
            Assert.True(interactedEventFired);
            Assert.Equal(nodeGuid, eventNodeGuid);
            Assert.Equal(itemId, eventItemId);
        }

        [Fact]
        public void ReportInteractionEvent_GatheringFailed_EmitsGatheringFailed()
        {
            // Arrange
            ulong nodeGuid = 0xAAAAAAAA;
            string errorMessage = "Gathering failed: You need Mining skill";
            ulong? eventNodeGuid = null;
            string? eventErrorMessage = null;
            bool eventFired = false;
            using var sub = _gameObjectAgent.GatheringFailed.Subscribe(t => { eventNodeGuid = t.GameObjectGuid; eventErrorMessage = t.Reason; eventFired = true; });

            // Act
            _gameObjectAgent.ReportInteractionEvent("gathering_failed", nodeGuid, null, errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(nodeGuid, eventNodeGuid);
            Assert.Equal(errorMessage, eventErrorMessage);
        }

        [Fact]
        public void ReportInteractionEvent_InteractionFailed_EmitsInteractionFailed()
        {
            // Arrange
            ulong gameObjectGuid = 0xBBBBBBBB;
            string errorMessage = "You cannot use that";
            ulong? eventGameObjectGuid = 0;
            string? eventErrorMessage = null;
            bool eventFired = false;
            using var sub = _gameObjectAgent.GameObjectInteractionFailed.Subscribe(t => { eventGameObjectGuid = t.GameObjectGuid; eventErrorMessage = t.Reason; eventFired = true; });

            // Act
            _gameObjectAgent.ReportInteractionEvent("interaction_failed", gameObjectGuid, null, errorMessage);

            // Assert
            Assert.True(eventFired);
            Assert.Equal(gameObjectGuid, eventGameObjectGuid);
            Assert.Equal(errorMessage, eventErrorMessage);
        }

        [Fact]
        public void UpdateGameObjectState_UpdatesCorrectly()
        {
            // Arrange
            ulong gameObjectGuid = 0xCCCCCCCC;
            string newState = "looted";

            // Act & Assert - Should not throw
            _gameObjectAgent.UpdateGameObjectState(gameObjectGuid, newState);
        }

        [Fact]
        public async Task InteractWithGameObjectAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            ulong gameObjectGuid = 0x12345678;
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _gameObjectAgent.InteractWithGameObjectAsync(gameObjectGuid));
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData("success")]
        [InlineData("interacted")]
        [InlineData("chest_opened")]
        [InlineData("node_harvested")]
        [InlineData("gathered")]
        [InlineData("gathering_failed")]
        [InlineData("interaction_failed")]
        [InlineData("failed")]
        public void ReportInteractionEvent_VariousEventTypes_Emits(string eventType)
        {
            // Arrange
            ulong gameObjectGuid = 0x12345678;
            bool eventFired = false;
            using var s1 = _gameObjectAgent.GameObjectInteracted.Subscribe(_ => eventFired = true);
            using var s2 = _gameObjectAgent.ChestOpened.Subscribe(_ => eventFired = true);
            using var s3 = _gameObjectAgent.NodeHarvested.Subscribe(_ => eventFired = true);
            using var s4 = _gameObjectAgent.GatheringFailed.Subscribe(_ => eventFired = true);
            using var s5 = _gameObjectAgent.GameObjectInteractionFailed.Subscribe(_ => eventFired = true);

            // Act
            _gameObjectAgent.ReportInteractionEvent(eventType, gameObjectGuid, 1234, "Test message");

            // Assert
            Assert.True(eventFired);
        }

        [Theory]
        [InlineData(GameObjectInteractionType.Generic)]
        [InlineData(GameObjectInteractionType.OpenChest)]
        [InlineData(GameObjectInteractionType.Gather)]
        [InlineData(GameObjectInteractionType.UseDoor)]
        [InlineData(GameObjectInteractionType.ActivateButton)]
        [InlineData(GameObjectInteractionType.Read)]
        public void CanInteractWith_VariousInteractionTypes_ReturnsTrue(GameObjectInteractionType interactionType)
        {
            // Arrange
            ulong gameObjectGuid = 0x12345678;

            // Act
            var canInteract = _gameObjectAgent.CanInteractWith(gameObjectGuid, interactionType);

            // Assert
            Assert.True(canInteract);
        }
    }
}