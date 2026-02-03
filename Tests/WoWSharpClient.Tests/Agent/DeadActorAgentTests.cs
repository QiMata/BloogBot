using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    public class DeadActorAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<DeadActorClientComponent>> _mockLogger;
        private readonly DeadActorClientComponent _deadActorAgent;

        public DeadActorAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<DeadActorClientComponent>>();
            _deadActorAgent = new DeadActorClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Assert
            Assert.NotNull(_deadActorAgent);
            Assert.False(_deadActorAgent.IsDead);
            Assert.False(_deadActorAgent.IsGhost);
            Assert.False(_deadActorAgent.HasResurrectionRequest);
            Assert.Null(_deadActorAgent.CorpseLocation);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DeadActorClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DeadActorClientComponent(_mockWorldClient.Object, null!));
        }

        [Fact]
        public async Task ReleaseSpiritAsync_ValidCall_SendsCorrectPacket()
        {
            // Act
            await _deadActorAgent.ReleaseSpiritAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_REPOP_REQUEST,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ResurrectAtCorpseAsync_ValidCall_SendsCorrectPacket()
        {
            // Act
            await _deadActorAgent.ResurrectAtCorpseAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_RECLAIM_CORPSE,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AcceptResurrectionAsync_ValidCall_SendsCorrectPacket()
        {
            // Arrange
            _deadActorAgent.HandleResurrectionRequest(0x123, "TestPlayer");

            // Act
            await _deadActorAgent.AcceptResurrectionAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_RESURRECT_RESPONSE,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.False(_deadActorAgent.HasResurrectionRequest);
        }

        [Fact]
        public async Task DeclineResurrectionAsync_ValidCall_SendsCorrectPacket()
        {
            // Arrange
            _deadActorAgent.HandleResurrectionRequest(0x123, "TestPlayer");

            // Act
            await _deadActorAgent.DeclineResurrectionAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_RESURRECT_RESPONSE,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.False(_deadActorAgent.HasResurrectionRequest);
        }

        [Fact]
        public async Task ResurrectWithSpiritHealerAsync_ValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong spiritHealerGuid = 0x123456789ABCDEF0;

            // Act
            await _deadActorAgent.ResurrectWithSpiritHealerAsync(spiritHealerGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_SPIRIT_HEALER_ACTIVATE,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QueryCorpseLocationAsync_ValidCall_SendsCorrectPacket()
        {
            // Act
            await _deadActorAgent.QueryCorpseLocationAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.MSG_CORPSE_QUERY,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SelfResurrectAsync_ValidCall_SendsCorrectPacket()
        {
            // Act
            await _deadActorAgent.SelfResurrectAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    GameData.Core.Enums.Opcode.CMSG_SELF_RES,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void UpdateDeathState_BecomesDead_UpdatesState()
        {
            // Act
            _deadActorAgent.UpdateDeathState(isDead: true, isGhost: false);

            // Assert
            Assert.True(_deadActorAgent.IsDead);
            Assert.False(_deadActorAgent.IsGhost);
        }

        [Fact]
        public void UpdateDeathState_BecomesGhost_UpdatesState()
        {
            // Act
            _deadActorAgent.UpdateDeathState(isDead: true, isGhost: true);

            // Assert
            Assert.True(_deadActorAgent.IsDead);
            Assert.True(_deadActorAgent.IsGhost);
        }

        [Fact]
        public void UpdateDeathState_BecomesAlive_ClearsCorpseLocation()
        {
            // Arrange
            _deadActorAgent.UpdateDeathState(isDead: true, isGhost: true);
            _deadActorAgent.UpdateCorpseLocation(1, 2, 3);

            // Act
            _deadActorAgent.UpdateDeathState(isDead: false, isGhost: false);

            // Assert
            Assert.False(_deadActorAgent.IsDead);
            Assert.False(_deadActorAgent.IsGhost);
            Assert.Null(_deadActorAgent.CorpseLocation);
        }

        [Fact]
        public void UpdateCorpseLocation_ValidCoordinates_UpdatesLocation()
        {
            // Arrange
            const float x = 100.0f;
            const float y = 200.0f;
            const float z = 300.0f;

            // Act
            _deadActorAgent.UpdateCorpseLocation(x, y, z);

            // Assert
            Assert.NotNull(_deadActorAgent.CorpseLocation);
            Assert.Equal((x, y, z), _deadActorAgent.CorpseLocation.Value);
        }

        [Fact]
        public void GetDistanceToCorpse_WithCorpseLocation_ReturnsCorrectDistance()
        {
            // Arrange
            const float corpseX = 100.0f;
            const float corpseY = 100.0f;
            const float corpseZ = 100.0f;
            const float currentX = 103.0f;
            const float currentY = 104.0f;
            const float currentZ = 100.0f;

            _deadActorAgent.UpdateCorpseLocation(corpseX, corpseY, corpseZ);

            // Act
            var distance = _deadActorAgent.GetDistanceToCorpse(currentX, currentY, currentZ);

            // Assert
            Assert.NotNull(distance);
            Assert.Equal(5.0f, distance.Value, 0.1f); // 3-4-5 triangle
        }

        [Fact]
        public void GetDistanceToCorpse_WithoutCorpseLocation_ReturnsNull()
        {
            // Arrange
            const float currentX = 100.0f;
            const float currentY = 100.0f;
            const float currentZ = 100.0f;

            // Act
            var distance = _deadActorAgent.GetDistanceToCorpse(currentX, currentY, currentZ);

            // Assert
            Assert.Null(distance);
        }

        [Fact]
        public void IsCloseToCorpse_WithinRange_ReturnsTrue()
        {
            // Arrange
            const float corpseX = 100.0f;
            const float corpseY = 100.0f;
            const float corpseZ = 100.0f;
            const float currentX = 105.0f;
            const float currentY = 100.0f;
            const float currentZ = 100.0f;

            _deadActorAgent.UpdateCorpseLocation(corpseX, corpseY, corpseZ);

            // Act
            var isClose = _deadActorAgent.IsCloseToCorpse(currentX, currentY, currentZ, maxDistance: 10.0f);

            // Assert
            Assert.True(isClose);
        }

        [Fact]
        public void IsCloseToCorpse_OutsideRange_ReturnsFalse()
        {
            // Arrange
            const float corpseX = 100.0f;
            const float corpseY = 100.0f;
            const float corpseZ = 100.0f;
            const float currentX = 200.0f;
            const float currentY = 100.0f;
            const float currentZ = 100.0f;

            _deadActorAgent.UpdateCorpseLocation(corpseX, corpseY, corpseZ);

            // Act
            var isClose = _deadActorAgent.IsCloseToCorpse(currentX, currentY, currentZ, maxDistance: 10.0f);

            // Assert
            Assert.False(isClose);
        }

        [Fact]
        public void HandleResurrectionRequest_ValidData_UpdatesState()
        {
            // Arrange
            const ulong resurrectorGuid = 0x123456789ABCDEF0;
            const string resurrectorName = "TestPlayer";

            // Act
            _deadActorAgent.HandleResurrectionRequest(resurrectorGuid, resurrectorName);

            // Assert
            Assert.True(_deadActorAgent.HasResurrectionRequest);
        }

        [Fact]
        public void HandleSpiritHealerTime_ValidTime_UpdatesTime()
        {
            // Arrange
            var timeSpan = TimeSpan.FromSeconds(30);

            // Act
            _deadActorAgent.HandleSpiritHealerTime(timeSpan);

            // Assert
            var remainingTime = _deadActorAgent.GetSpiritHealerResurrectionTime();
            Assert.NotNull(remainingTime);
            Assert.True(remainingTime.Value.TotalSeconds > 0);
        }

        [Fact]
        public void HandleDeathError_ValidMessage_DoesNotThrow()
        {
            // Arrange
            const string errorMessage = "Cannot resurrect here";

            // Act
            var exception = Record.Exception(() => _deadActorAgent.HandleDeathError(errorMessage));

            // Assert
            Assert.Null(exception);
        }
    }
}