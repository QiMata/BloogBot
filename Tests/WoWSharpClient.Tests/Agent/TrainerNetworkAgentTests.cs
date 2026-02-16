using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Unit tests for the TrainerNetworkClientComponent class.
    /// Tests all trainer-related operations including opening trainer windows,
    /// learning spells, and handling trainer responses.
    /// </summary>
    public class TrainerNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<TrainerNetworkClientComponent>> _mockLogger;
        private readonly TrainerNetworkClientComponent _trainerAgent;

        public TrainerNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<TrainerNetworkClientComponent>>();
            _trainerAgent = new TrainerNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_SetsProperties()
        {
            // Assert
            Assert.False(_trainerAgent.IsTrainerWindowOpen);
            Assert.Null(_trainerAgent.CurrentTrainerGuid);
            Assert.Empty(_trainerAgent.GetAvailableServices());
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TrainerNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TrainerNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Open Trainer Tests

        [Fact]
        public async Task OpenTrainerAsync_WithValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            var expectedPayload = new byte[8];
            BitConverter.GetBytes(trainerGuid).CopyTo(expectedPayload, 0);

            // Act
            await _trainerAgent.OpenTrainerAsync(trainerGuid);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OpenTrainerAsync_WhenWorldClientThrows_PropagatesException()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<GameData.Core.Enums.Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Connection error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _trainerAgent.OpenTrainerAsync(trainerGuid));
        }

        #endregion

        #region Request Trainer Services Tests

        [Fact]
        public async Task RequestTrainerServicesAsync_WithValidGuid_SendsCorrectPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            var expectedPayload = new byte[8];
            BitConverter.GetBytes(trainerGuid).CopyTo(expectedPayload, 0);

            // Act
            await _trainerAgent.RequestTrainerServicesAsync(trainerGuid);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_LIST,
                It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Learn Spell Tests

        [Fact]
        public async Task LearnSpellAsync_WithValidParameters_SendsCorrectPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            const uint spellId = 12345;
            var expectedPayload = new byte[12];
            BitConverter.GetBytes(trainerGuid).CopyTo(expectedPayload, 0);
            BitConverter.GetBytes(spellId).CopyTo(expectedPayload, 8);

            // Act
            await _trainerAgent.LearnSpellAsync(trainerGuid, spellId);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_BUY_SPELL,
                It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LearnSpellByIndexAsync_WithValidService_CallsLearnSpellAsync()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            const uint serviceIndex = 1;
            const uint spellId = 12345;

            var services = new[]
            {
                new TrainerServiceData { ServiceIndex = 0, SpellId = 11111, CanLearn = true },
                new TrainerServiceData { ServiceIndex = 1, SpellId = spellId, CanLearn = true },
                new TrainerServiceData { ServiceIndex = 2, SpellId = 33333, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            var expectedPayload = new byte[12];
            BitConverter.GetBytes(trainerGuid).CopyTo(expectedPayload, 0);
            BitConverter.GetBytes(spellId).CopyTo(expectedPayload, 8);

            // Act
            await _trainerAgent.LearnSpellByIndexAsync(trainerGuid, serviceIndex);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_BUY_SPELL,
                It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LearnSpellByIndexAsync_WithInvalidIndex_DoesNotSendPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            const uint invalidIndex = 999;

            // Act
            await _trainerAgent.LearnSpellByIndexAsync(trainerGuid, invalidIndex);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                It.IsAny<GameData.Core.Enums.Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Quick Learn Tests

        [Fact]
        public async Task QuickLearnSpellAsync_WithValidParameters_SendsMultiplePackets()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            const uint spellId = 12345;

            // Act
            await _trainerAgent.QuickLearnSpellAsync(trainerGuid, spellId);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_LIST,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_BUY_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LearnMultipleSpellsAsync_WithValidSpells_SendsCorrectPackets()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            var spellIds = new uint[] { 12345, 23456, 34567 };

            // Act
            await _trainerAgent.LearnMultipleSpellsAsync(trainerGuid, spellIds);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_LIST,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                GameData.Core.Enums.Opcode.CMSG_TRAINER_BUY_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        #endregion

        #region Service Query Tests

        [Fact]
        public void IsSpellAvailable_WithAvailableSpell_ReturnsTrue()
        {
            // Arrange
            const uint spellId = 12345;
            const ulong trainerGuid = 0x123456789ABCDEF0UL;

            var services = new[]
            {
                new TrainerServiceData { SpellId = spellId, CanLearn = true },
                new TrainerServiceData { SpellId = 23456, CanLearn = false }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.IsSpellAvailable(spellId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsSpellAvailable_WithUnavailableSpell_ReturnsFalse()
        {
            // Arrange
            const uint spellId = 12345;
            const ulong trainerGuid = 0x123456789ABCDEF0UL;

            var services = new[]
            {
                new TrainerServiceData { SpellId = spellId, CanLearn = false },
                new TrainerServiceData { SpellId = 23456, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.IsSpellAvailable(spellId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetSpellCost_WithExistingSpell_ReturnsCost()
        {
            // Arrange
            const uint spellId = 12345;
            const uint expectedCost = 5000;
            const ulong trainerGuid = 0x123456789ABCDEF0UL;

            var services = new[]
            {
                new TrainerServiceData { SpellId = spellId, Cost = expectedCost, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.GetSpellCost(spellId);

            // Assert
            Assert.Equal(expectedCost, result);
        }

        [Fact]
        public void GetSpellCost_WithNonexistentSpell_ReturnsNull()
        {
            // Arrange
            const uint spellId = 99999;
            const ulong trainerGuid = 0x123456789ABCDEF0UL;

            var services = new[]
            {
                new TrainerServiceData { SpellId = 12345, Cost = 5000, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.GetSpellCost(spellId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetAvailableServices_ReturnsOnlyLearnableServices()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;

            var services = new[]
            {
                new TrainerServiceData { SpellId = 12345, CanLearn = true },
                new TrainerServiceData { SpellId = 23456, CanLearn = false },
                new TrainerServiceData { SpellId = 34567, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.GetAvailableServices();

            // Assert
            Assert.Equal(2, result.Length);
            Assert.All(result, service => Assert.True(service.CanLearn));
        }

        [Fact]
        public void GetAffordableServices_WithSufficientMoney_ReturnsAffordableServices()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            const uint currentMoney = 10000;

            var services = new[]
            {
                new TrainerServiceData { SpellId = 12345, Cost = 5000, CanLearn = true },
                new TrainerServiceData { SpellId = 23456, Cost = 15000, CanLearn = true },
                new TrainerServiceData { SpellId = 34567, Cost = 8000, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.GetAffordableServices(currentMoney);

            // Assert
            Assert.Equal(2, result.Length);
            Assert.All(result, service => Assert.True(service.Cost <= currentMoney));
        }

        #endregion

        #region State Tests

        [Fact]
        public void IsTrainerOpen_WithCorrectGuid_ReturnsTrue()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            var services = new TrainerServiceData[0];

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.IsTrainerOpen(trainerGuid);

            // Assert
            Assert.True(result);
            Assert.True(_trainerAgent.IsTrainerWindowOpen);
            Assert.Equal(trainerGuid, _trainerAgent.CurrentTrainerGuid);
        }

        [Fact]
        public void IsTrainerOpen_WithWrongGuid_ReturnsFalse()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            const ulong wrongGuid = 0xFEDCBA9876543210UL;
            var services = new TrainerServiceData[0];

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            var result = _trainerAgent.IsTrainerOpen(wrongGuid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CloseTrainerAsync_ClearsState()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            var services = new[]
            {
                new TrainerServiceData { SpellId = 12345, CanLearn = true }
            };

            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Act
            await _trainerAgent.CloseTrainerAsync();

            // Assert
            Assert.False(_trainerAgent.IsTrainerWindowOpen);
            Assert.Null(_trainerAgent.CurrentTrainerGuid);
            Assert.Empty(_trainerAgent.GetAvailableServices());
        }

        #endregion

        #region Event Tests

        [Fact]
        public void HandleTrainerWindowOpened_FiresEvents()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0UL;
            var services = new[]
            {
                new TrainerServiceData { SpellId = 12345, CanLearn = true }
            };

            ulong? eventTrainerGuid = null;
            TrainerServiceData[]? eventServices = null;

            _trainerAgent.TrainerWindowOpened += (guid) => eventTrainerGuid = guid;
            _trainerAgent.TrainerServicesReceived += (receivedServices) => eventServices = receivedServices;

            // Act
            _trainerAgent.HandleTrainerWindowOpened(trainerGuid, services);

            // Assert
            Assert.Equal(trainerGuid, eventTrainerGuid);
            Assert.Equal(services, eventServices);
        }

        [Fact]
        public void HandleSpellLearned_FiresEvent()
        {
            // Arrange
            const uint spellId = 12345;
            const uint cost = 5000;

            uint? eventSpellId = null;
            uint? eventCost = null;

            _trainerAgent.SpellLearned += (id, spellCost) =>
            {
                eventSpellId = id;
                eventCost = spellCost;
            };

            // Act
            _trainerAgent.HandleSpellLearned(spellId, cost);

            // Assert
            Assert.Equal(spellId, eventSpellId);
            Assert.Equal(cost, eventCost);
        }

        [Fact]
        public void HandleTrainerError_FiresEvent()
        {
            // Arrange
            const string errorMessage = "Test error message";

            string? eventError = null;

            _trainerAgent.TrainerError += (error) => eventError = error;

            // Act
            _trainerAgent.HandleTrainerError(errorMessage);

            // Assert
            Assert.Equal(errorMessage, eventError);
        }

        [Fact]
        public async Task CloseTrainerAsync_FiresEvent()
        {
            // Arrange
            bool eventFired = false;

            _trainerAgent.TrainerWindowClosed += () => eventFired = true;

            // Act
            await _trainerAgent.CloseTrainerAsync();

            // Assert
            Assert.True(eventFired);
        }

        #endregion

        #region Service Update Tests

        [Fact]
        public void UpdateTrainerServices_UpdatesServicesAndFiresEvent()
        {
            // Arrange
            var initialServices = new[]
            {
                new TrainerServiceData { SpellId = 12345, CanLearn = true }
            };

            var updatedServices = new[]
            {
                new TrainerServiceData { SpellId = 23456, CanLearn = true },
                new TrainerServiceData { SpellId = 34567, CanLearn = false }
            };

            TrainerServiceData[]? eventServices = null;
            _trainerAgent.TrainerServicesReceived += (services) => eventServices = services;

            // Set initial services
            _trainerAgent.UpdateTrainerServices(initialServices);

            // Act
            _trainerAgent.UpdateTrainerServices(updatedServices);

            // Assert
            var currentServices = _trainerAgent.GetAvailableServices();
            Assert.Single(currentServices); // Only one learnable service
            Assert.Equal(23456U, currentServices[0].SpellId);
            Assert.Equal(updatedServices, eventServices);
        }

        #endregion
    }
}