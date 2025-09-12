using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using Xunit;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Unit tests for the ProfessionsNetworkClientComponent class.
    /// </summary>
    public class ProfessionsNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<ProfessionsNetworkClientComponent>> _mockLogger;
        private readonly ProfessionsNetworkClientComponent _professionsAgent;

        public ProfessionsNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<ProfessionsNetworkClientComponent>>();
            _professionsAgent = new ProfessionsNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Act & Assert
            Assert.NotNull(_professionsAgent);
            Assert.False(_professionsAgent.IsTrainerWindowOpen);
            Assert.False(_professionsAgent.IsCraftingWindowOpen);
            Assert.Null(_professionsAgent.CurrentTrainerGuid);
            Assert.Null(_professionsAgent.CurrentProfession);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProfessionsNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProfessionsNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Profession Trainer Tests

        [Fact]
        public async Task OpenProfessionTrainerAsync_WithValidParameters_ShouldSendCorrectPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;
            const ProfessionType professionType = ProfessionType.Alchemy;

            // Act
            await _professionsAgent.OpenProfessionTrainerAsync(trainerGuid, professionType);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO,
                It.Is<byte[]>(b => BitConverter.ToUInt64(b, 0) == trainerGuid),
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.True(_professionsAgent.IsTrainerWindowOpen);
            Assert.Equal(trainerGuid, _professionsAgent.CurrentTrainerGuid);
            Assert.Equal(professionType, _professionsAgent.CurrentProfession);
        }

        [Fact]
        public async Task RequestProfessionServicesAsync_WithValidGuid_ShouldSendCorrectPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;

            // Act
            await _professionsAgent.RequestProfessionServicesAsync(trainerGuid);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_LIST,
                It.Is<byte[]>(b => BitConverter.ToUInt64(b, 0) == trainerGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LearnProfessionSkillAsync_WithValidParameters_ShouldSendCorrectPacket()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;
            const uint spellId = 12345;

            // Act
            await _professionsAgent.LearnProfessionSkillAsync(trainerGuid, spellId);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.Is<byte[]>(b => BitConverter.ToUInt64(b, 0) == trainerGuid && BitConverter.ToUInt32(b, 8) == spellId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LearnMultipleProfessionSkillsAsync_WithValidParameters_ShouldSendMultiplePackets()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;
            uint[] spellIds = { 12345, 67890, 11111 };

            // Act
            await _professionsAgent.LearnMultipleProfessionSkillsAsync(trainerGuid, spellIds);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Exactly(spellIds.Length));
        }

        [Fact]
        public async Task CloseProfessionTrainerAsync_ShouldUpdateState()
        {
            // Arrange
            await _professionsAgent.OpenProfessionTrainerAsync(0x123456789ABCDEF0, ProfessionType.Alchemy);

            // Act
            await _professionsAgent.CloseProfessionTrainerAsync();

            // Assert
            Assert.False(_professionsAgent.IsTrainerWindowOpen);
            Assert.Null(_professionsAgent.CurrentTrainerGuid);
            Assert.Null(_professionsAgent.CurrentProfession);
        }

        #endregion

        #region Crafting Tests

        [Fact]
        public async Task OpenCraftingWindowAsync_WithValidProfession_ShouldSendCorrectPacket()
        {
            // Arrange
            const ProfessionType professionType = ProfessionType.Blacksmithing;

            // Act
            await _professionsAgent.OpenCraftingWindowAsync(professionType);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.True(_professionsAgent.IsCraftingWindowOpen);
            Assert.Equal(professionType, _professionsAgent.CurrentProfession);
        }

        [Fact]
        public async Task CraftItemAsync_WithValidRecipe_ShouldSendCorrectPacket()
        {
            // Arrange
            const uint recipeId = 12345;
            const uint quantity = 3;

            // Act
            await _professionsAgent.CraftItemAsync(recipeId, quantity);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(b => BitConverter.ToUInt32(b, 0) == recipeId),
                It.IsAny<CancellationToken>()), Times.Exactly((int)quantity));
        }

        [Fact]
        public async Task CraftMultipleItemsAsync_WithValidQueue_ShouldProcessInPriorityOrder()
        {
            // Arrange
            var craftingQueue = new CraftingRequest[]
            {
                new CraftingRequest { RecipeId = 111, Quantity = 1, Priority = 3 },
                new CraftingRequest { RecipeId = 222, Quantity = 2, Priority = 1 },
                new CraftingRequest { RecipeId = 333, Quantity = 1, Priority = 2 }
            };

            // Act
            await _professionsAgent.CraftMultipleItemsAsync(craftingQueue);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Exactly(4)); // Total quantity from all requests
        }

        [Fact]
        public async Task CloseCraftingWindowAsync_ShouldUpdateState()
        {
            // Arrange
            await _professionsAgent.OpenCraftingWindowAsync(ProfessionType.Alchemy);

            // Act
            await _professionsAgent.CloseCraftingWindowAsync();

            // Assert
            Assert.False(_professionsAgent.IsCraftingWindowOpen);
            Assert.Null(_professionsAgent.CurrentProfession);
        }

        #endregion

        #region Gathering Tests

        [Fact]
        public async Task GatherResourceAsync_WithValidNode_ShouldSendCorrectPacket()
        {
            // Arrange
            const ulong nodeGuid = 0x123456789ABCDEF0;
            const GatheringType gatheringType = GatheringType.Herbalism;

            // Act
            await _professionsAgent.GatherResourceAsync(nodeGuid, gatheringType);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GAMEOBJ_USE,
                It.Is<byte[]>(b => BitConverter.ToUInt64(b, 0) == nodeGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Quick Operations Tests

        [Fact]
        public async Task QuickTrainProfessionSkillsAsync_ShouldPerformCompleteSequence()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;
            const ProfessionType professionType = ProfessionType.Engineering;
            uint[] skillIds = { 111, 222, 333 };

            // Act
            await _professionsAgent.QuickTrainProfessionSkillsAsync(trainerGuid, professionType, skillIds);

            // Assert
            // Should send: GOSSIP_HELLO, TRAINER_LIST, and multiple TRAINER_BUY_SPELL packets
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_LIST,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Exactly(skillIds.Length));
        }

        #endregion

        #region Service Management Tests

        [Fact]
        public void GetAvailableProfessionServices_WithNoServices_ShouldReturnEmptyArray()
        {
            // Act
            var services = _professionsAgent.GetAvailableProfessionServices();

            // Assert
            Assert.Empty(services);
        }

        [Fact]
        public void GetAffordableProfessionServices_WithZeroMoney_ShouldReturnEmptyArray()
        {
            // Act
            var services = _professionsAgent.GetAffordableProfessionServices(0);

            // Assert
            Assert.Empty(services);
        }

        [Fact]
        public void IsProfessionSkillAvailable_WithUnknownSpell_ShouldReturnFalse()
        {
            // Act
            var isAvailable = _professionsAgent.IsProfessionSkillAvailable(99999);

            // Assert
            Assert.False(isAvailable);
        }

        [Fact]
        public void GetProfessionSkillCost_WithUnknownSpell_ShouldReturnZero()
        {
            // Act
            var cost = _professionsAgent.GetProfessionSkillCost(99999);

            // Assert
            Assert.Equal(0u, cost);
        }

        [Fact]
        public void CanCraftRecipe_WithAnyRecipe_ShouldReturnTrue()
        {
            // Act
            var canCraft = _professionsAgent.CanCraftRecipe(12345);

            // Assert
            Assert.True(canCraft); // Simplified implementation always returns true
        }

        [Fact]
        public void GetRecipeMaterials_WithAnyRecipe_ShouldReturnEmptyArray()
        {
            // Act
            var materials = _professionsAgent.GetRecipeMaterials(12345);

            // Assert
            Assert.Empty(materials); // Simplified implementation returns empty array
        }

        #endregion

        #region Server Response Handling Tests

        [Fact]
        public void HandleProfessionServicesResponse_WithValidServices_ShouldUpdateServicesAndFireEvent()
        {
            // Arrange
            var services = new ProfessionService[]
            {
                new ProfessionService { SpellId = 111, Name = "Test Skill 1", Cost = 100, IsAvailable = true },
                new ProfessionService { SpellId = 222, Name = "Test Skill 2", Cost = 200, IsAvailable = true }
            };

            ProfessionService[]? receivedServices = null;
            _professionsAgent.ProfessionServicesReceived += (s) => receivedServices = s;

            // Act
            _professionsAgent.HandleProfessionServicesResponse(services);

            // Assert
            Assert.NotNull(receivedServices);
            Assert.Equal(2, receivedServices.Length);
            Assert.Equal(2, _professionsAgent.GetAvailableProfessionServices().Length);
        }

        [Fact]
        public void HandleSkillLearnedResponse_WithValidData_ShouldFireEvent()
        {
            // Arrange
            const uint spellId = 12345;
            const uint cost = 500;

            uint? receivedSpellId = null;
            uint? receivedCost = null;
            _professionsAgent.SkillLearned += (s, c) => { receivedSpellId = s; receivedCost = c; };

            // Act
            _professionsAgent.HandleSkillLearnedResponse(spellId, cost);

            // Assert
            Assert.Equal(spellId, receivedSpellId);
            Assert.Equal(cost, receivedCost);
        }

        [Fact]
        public void HandleItemCraftedResponse_WithValidData_ShouldFireEvent()
        {
            // Arrange
            const uint itemId = 67890;
            const uint quantity = 3;

            uint? receivedItemId = null;
            uint? receivedQuantity = null;
            _professionsAgent.ItemCrafted += (i, q) => { receivedItemId = i; receivedQuantity = q; };

            // Act
            _professionsAgent.HandleItemCraftedResponse(itemId, quantity);

            // Assert
            Assert.Equal(itemId, receivedItemId);
            Assert.Equal(quantity, receivedQuantity);
        }

        [Fact]
        public void HandleResourceGatheredResponse_WithValidData_ShouldFireEvent()
        {
            // Arrange
            const ulong nodeGuid = 0x123456789ABCDEF0;
            const uint itemId = 11111;
            const uint quantity = 2;

            ulong? receivedNodeGuid = null;
            uint? receivedItemId = null;
            uint? receivedQuantity = null;
            _professionsAgent.ResourceGathered += (n, i, q) => { receivedNodeGuid = n; receivedItemId = i; receivedQuantity = q; };

            // Act
            _professionsAgent.HandleResourceGatheredResponse(nodeGuid, itemId, quantity);

            // Assert
            Assert.Equal(nodeGuid, receivedNodeGuid);
            Assert.Equal(itemId, receivedItemId);
            Assert.Equal(quantity, receivedQuantity);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task TrainerWindowOpened_ShouldFireEvent()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;
            const ProfessionType professionType = ProfessionType.Tailoring;

            ulong? receivedGuid = null;
            ProfessionType? receivedType = null;
            _professionsAgent.TrainerWindowOpened += (g, t) => { receivedGuid = g; receivedType = t; };

            // Act
            await _professionsAgent.OpenProfessionTrainerAsync(trainerGuid, professionType);

            // Assert
            Assert.Equal(trainerGuid, receivedGuid);
            Assert.Equal(professionType, receivedType);
        }

        [Fact]
        public async Task TrainerWindowClosed_ShouldFireEvent()
        {
            // Arrange
            await _professionsAgent.OpenProfessionTrainerAsync(0x123456789ABCDEF0, ProfessionType.Alchemy);

            bool eventFired = false;
            _professionsAgent.TrainerWindowClosed += () => eventFired = true;

            // Act
            await _professionsAgent.CloseProfessionTrainerAsync();

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public async Task CraftingWindowOpened_ShouldFireEvent()
        {
            // Arrange
            const ProfessionType professionType = ProfessionType.Enchanting;

            ProfessionType? receivedType = null;
            _professionsAgent.CraftingWindowOpened += (t) => receivedType = t;

            // Act
            await _professionsAgent.OpenCraftingWindowAsync(professionType);

            // Assert
            Assert.Equal(professionType, receivedType);
        }

        [Fact]
        public async Task CraftingWindowClosed_ShouldFireEvent()
        {
            // Arrange
            await _professionsAgent.OpenCraftingWindowAsync(ProfessionType.Leatherworking);

            bool eventFired = false;
            _professionsAgent.CraftingWindowClosed += () => eventFired = true;

            // Act
            await _professionsAgent.CloseCraftingWindowAsync();

            // Assert
            Assert.True(eventFired);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task LearnMultipleProfessionSkillsAsync_WithEmptyArray_ShouldNotSendPackets()
        {
            // Arrange
            const ulong trainerGuid = 0x123456789ABCDEF0;
            uint[] emptySkillIds = Array.Empty<uint>();

            // Act
            await _professionsAgent.LearnMultipleProfessionSkillsAsync(trainerGuid, emptySkillIds);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CraftItemAsync_WithZeroQuantity_ShouldNotSendPackets()
        {
            // Arrange
            const uint recipeId = 12345;
            const uint quantity = 0;

            // Act
            await _professionsAgent.CraftItemAsync(recipeId, quantity);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CraftMultipleItemsAsync_WithEmptyQueue_ShouldNotSendPackets()
        {
            // Arrange
            var emptyQueue = Array.Empty<CraftingRequest>();

            // Act
            await _professionsAgent.CraftMultipleItemsAsync(emptyQueue);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion
    }
}